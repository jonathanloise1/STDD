"""Answer backends: the one small interface between an evaluation and a model runtime (models.md 4, 6.6).

``AnswerBackend.generate(messages)`` takes chat messages and returns the text and its timings. Everything above this
module (metrics, thresholds, reports, the investigator host) is tested with ``FakeBackend``. The real backends import
their heavy dependencies lazily, inside the methods that need them, and are never constructed by a test or a dry run:

- ``TransformersBackend``: the merged weights (or a base model) with transformers, greedy decoding;
- ``OnnxBackend``: the browser export (``onnx/model_q4f16.onnx``) with onnxruntime and a hand-written greedy loop;
- ``LlamaServerBackend``: a ``llama-server`` process started on the GGUF export, queried over HTTP.

Decoding is greedy everywhere (3.8). ``chat_template_kwargs`` (3.7) are passed identically to every backend.
"""
from __future__ import annotations

import json
import os
import socket
import subprocess
import time
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Callable, Protocol, Sequence

Messages = list[dict[str, str]]


@dataclass
class GenResult:
    text: str
    total_ms: float = 0.0
    ttft_ms: float | None = None
    new_tokens: int | None = None


class BackendError(RuntimeError):
    pass


class AnswerBackend(Protocol):
    name: str

    def info(self) -> dict[str, Any]: ...
    def generate(self, messages: Messages, *, max_new_tokens: int = 200, constraint: dict[str, Any] | None = None) -> GenResult: ...
    def apply_template(self, messages: Messages) -> str: ...
    def close(self) -> None: ...


# ---------------------------------------------------------------- fake

class FakeBackend:
    """Deterministic backend for tests and rehearsals: ``responder(messages, constraint)`` returns the text."""

    name = "fake"

    def __init__(self, responder: Callable[..., str], latency_ms: float = 100.0, device: str = "cpu"):
        self.responder, self.latency_ms, self.device = responder, latency_ms, device
        self.calls: list[Messages] = []
        self.constraints: list[dict[str, Any] | None] = []

    def info(self) -> dict[str, Any]:
        return {"device": self.device, "name": "fake backend"}

    def generate(self, messages: Messages, *, max_new_tokens: int = 200, constraint: dict[str, Any] | None = None) -> GenResult:
        self.calls.append(messages)
        self.constraints.append(constraint)
        text = self.responder(messages, constraint) if _takes_constraint(self.responder) else self.responder(messages)
        n = len(self.calls)
        return GenResult(text, self.latency_ms + (n % 7) * 10, self.latency_ms / 4, max(1, len(text) // 4))

    def apply_template(self, messages: Messages) -> str:
        return "".join(f"<|{m['role']}|>{m['content']}<|end|>" for m in messages) + "<|assistant|>"

    def close(self) -> None:
        pass


def _takes_constraint(fn: Callable[..., Any]) -> bool:
    import inspect
    try:
        params = [p for p in inspect.signature(fn).parameters.values() if p.kind in (p.POSITIONAL_ONLY, p.POSITIONAL_OR_KEYWORD)]
    except (TypeError, ValueError):
        return False
    return len(params) >= 2


# ---------------------------------------------------------------- transformers (merged weights, base models)

class TransformersBackend:
    name = "transformers"

    def __init__(self, model_dir: str | Path, revision: str | None = None, chat_template_kwargs: dict[str, Any] | None = None,
                 device: str = "auto"):
        self.model_dir, self.revision = str(model_dir), revision
        self.kwargs = chat_template_kwargs or {}
        self.device_request = device
        self._model = self._tok = None
        self._device = "cpu"

    def _load(self) -> None:
        if self._model is not None:
            return
        import torch  # type: ignore
        from transformers import AutoTokenizer  # type: ignore
        from .loading import load_text_model
        device = self.device_request
        if device == "auto":
            device = "cuda" if torch.cuda.is_available() else "mps" if getattr(torch.backends, "mps", None) and torch.backends.mps.is_available() else "cpu"
        dtype = torch.bfloat16 if device in ("cuda", "mps") else torch.float32
        self._tok = AutoTokenizer.from_pretrained(self.model_dir, revision=self.revision)
        self._model = load_text_model(self.model_dir, self.revision, dtype, {"freeze_vision_tower": False}).to(device).eval()
        self._device = device

    def info(self) -> dict[str, Any]:
        self._load()
        dev = {"cuda": "gpu", "mps": "gpu", "cpu": "cpu"}[self._device]
        name = self._device
        if self._device == "cuda":
            import torch  # type: ignore
            name = torch.cuda.get_device_name(0)
        return {"device": dev, "name": name}

    def apply_template(self, messages: Messages) -> str:
        self._load()
        return self._tok.apply_chat_template(messages, tokenize=False, add_generation_prompt=True, **self.kwargs)

    def generate(self, messages: Messages, *, max_new_tokens: int = 200, constraint: dict[str, Any] | None = None) -> GenResult:
        self._load()
        import torch  # type: ignore
        from transformers import LogitsProcessor, LogitsProcessorList  # type: ignore

        class FirstToken(LogitsProcessor):                 # called once per generated token, after the prompt is processed
            def __init__(self) -> None:
                self.first: float | None = None

            def __call__(self, input_ids, scores):         # noqa: ANN001
                if self.first is None:
                    self.first = time.perf_counter()
                return scores

        text = self.apply_template(messages)
        enc = self._tok(text, return_tensors="pt", add_special_tokens=False).to(self._device)
        probe = FirstToken()
        start = time.perf_counter()
        with torch.no_grad():
            out = self._model.generate(**enc, max_new_tokens=max_new_tokens, do_sample=False, temperature=None, top_p=None, top_k=None,
                                       logits_processor=LogitsProcessorList([probe]), pad_token_id=self._tok.pad_token_id or self._tok.eos_token_id)
        end = time.perf_counter()
        new = out[0][enc["input_ids"].shape[1]:]
        return GenResult(self._tok.decode(new, skip_special_tokens=True), (end - start) * 1000,
                         None if probe.first is None else (probe.first - start) * 1000, int(new.shape[0]))

    def close(self) -> None:
        self._model = self._tok = None


# ---------------------------------------------------------------- onnx (the browser export, in Python)

class OnnxBackend:
    """Greedy decoding with onnxruntime over the files the browser loads (6.6): ``onnx/model_q4f16.onnx`` with its
    ``config.json`` and tokenizer files, using the usual ``past_key_values.{i}.key|value`` inputs of transformers.js exports.
    CPU or GPU execution providers are chosen by onnxruntime availability."""

    name = "onnx"

    def __init__(self, export_dir: str | Path, model_file: str = "onnx/model_q4f16.onnx", chat_template_kwargs: dict[str, Any] | None = None):
        self.dir, self.model_file = Path(export_dir), model_file
        self.kwargs = chat_template_kwargs or {}
        self._sess = self._tok = self._cfg = None
        self._provider = "CPUExecutionProvider"

    def _load(self) -> None:
        if self._sess is not None:
            return
        import onnxruntime as ort  # type: ignore
        from transformers import AutoTokenizer  # type: ignore
        avail = ort.get_available_providers()
        providers = [p for p in ("CUDAExecutionProvider", "CoreMLExecutionProvider") if p in avail] + ["CPUExecutionProvider"]
        self._sess = ort.InferenceSession(str(self.dir / self.model_file), providers=providers)
        self._provider = self._sess.get_providers()[0]
        self._tok = AutoTokenizer.from_pretrained(str(self.dir))
        self._cfg = json.loads((self.dir / "config.json").read_text(encoding="utf-8"))

    def info(self) -> dict[str, Any]:
        self._load()
        return {"device": "cpu" if self._provider == "CPUExecutionProvider" else "gpu", "name": f"onnxruntime {self._provider}"}

    def apply_template(self, messages: Messages) -> str:
        self._load()
        return self._tok.apply_chat_template(messages, tokenize=False, add_generation_prompt=True, **self.kwargs)

    def generate(self, messages: Messages, *, max_new_tokens: int = 200, constraint: dict[str, Any] | None = None) -> GenResult:
        self._load()
        import numpy as np  # type: ignore
        cfg, sess, tok = self._cfg, self._sess, self._tok
        layers = cfg["num_hidden_layers"]
        kv_heads = cfg.get("num_key_value_heads", cfg["num_attention_heads"])
        head_dim = cfg.get("head_dim") or cfg["hidden_size"] // cfg["num_attention_heads"]
        kv_type = next((i.type for i in sess.get_inputs() if i.name.startswith("past_key_values")), "tensor(float16)")
        dtype = np.float16 if "float16" in kv_type else np.float32
        input_names = {i.name for i in sess.get_inputs()}
        ids = tok(self.apply_template(messages), add_special_tokens=False)["input_ids"]
        past = {f"past_key_values.{i}.{kv}": np.zeros((1, kv_heads, 0, head_dim), dtype=dtype) for i in range(layers) for kv in ("key", "value")}
        eos = {tok.eos_token_id} | set(cfg.get("eos_token_id", []) if isinstance(cfg.get("eos_token_id"), list) else [cfg.get("eos_token_id")])
        generated: list[int] = []
        cur = np.array([ids], dtype=np.int64)
        total = len(ids)
        start = time.perf_counter()
        first: float | None = None
        for _ in range(max_new_tokens):
            feeds: dict[str, Any] = {"input_ids": cur, "attention_mask": np.ones((1, total), dtype=np.int64), **past}
            if "position_ids" in input_names:
                feeds["position_ids"] = np.arange(total - cur.shape[1], total, dtype=np.int64)[None, :]
            outs = sess.run(None, feeds)
            if first is None:
                first = time.perf_counter()
            nxt = int(np.argmax(outs[0][0, -1]))
            if nxt in eos:
                break
            generated.append(nxt)
            names = [o.name for o in sess.get_outputs()][1:]
            past = {n.replace("present.", "past_key_values."): v for n, v in zip(names, outs[1:])}
            cur = np.array([[nxt]], dtype=np.int64)
            total += 1
        end = time.perf_counter()
        return GenResult(tok.decode(generated, skip_special_tokens=True), (end - start) * 1000,
                         None if first is None else (first - start) * 1000, len(generated))

    def close(self) -> None:
        self._sess = self._tok = None


# ---------------------------------------------------------------- llama.cpp server (the GGUF export)

def free_port() -> int:
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


SHARED_FLAGS_FILE = Path(__file__).resolve().parents[2] / "src" / "support" / "model-host" / "shared-flags.txt"


def shared_server_flags(path: Path = SHARED_FLAGS_FILE) -> list[str]:
    """The llama-server flags that change what the model writes, read from the file the launchers read too (``src/support/model-host/shared-flags.txt``),
    so that the evaluation and the production host cannot drift apart."""
    flags = [ln.strip() for ln in path.read_text(encoding="utf-8").splitlines() if ln.strip() and not ln.lstrip().startswith("#")]
    if not flags:
        raise BackendError(f"{path} lists no flag: the evaluation would not match the host")
    return flags


class LlamaServerBackend:
    """Starts ``llama-server`` on a GGUF file (or attaches to ``base_url``) and queries ``/v1/chat/completions``.

    ``n_gpu_layers`` 999 offloads everything when a GPU build is used; ``0`` forces CPU. ``process_factory`` and
    ``opener`` exist for tests; a real run uses ``subprocess.Popen`` and ``urllib``."""

    name = "llama-server"

    def __init__(self, gguf: str | Path | None = None, server_bin: str = "llama-server", chat_template_kwargs: dict[str, Any] | None = None,
                 ctx_size: int = 4096, n_gpu_layers: int = 999, threads: int | None = None, port: int | None = None,
                 base_url: str | None = None, start_timeout: float = 600.0, request_timeout: float = 300.0,
                 process_factory: Callable[[list[str]], Any] | None = None, device_label: str | None = None):
        self.gguf, self.server_bin, self.kwargs = gguf, server_bin, chat_template_kwargs or {}
        self.ctx_size, self.n_gpu_layers, self.threads = ctx_size, n_gpu_layers, threads
        self.port = port or (None if base_url else free_port())
        self.base_url = (base_url or f"http://127.0.0.1:{self.port}").rstrip("/")
        self.start_timeout, self.request_timeout = start_timeout, request_timeout
        self.process_factory = process_factory or (lambda cmd: subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL))
        self.device_label = device_label or ("gpu" if n_gpu_layers else "cpu")
        self._proc: Any = None

    def command(self) -> list[str]:
        # --parallel 1: one request at a time, so the whole context size is one slot's, as on the investigator's host (models.md 9.2 step 11)
        cmd = [self.server_bin, "-m", str(self.gguf), "--host", "127.0.0.1", "--port", str(self.port), "--ctx-size", str(self.ctx_size),
               "--parallel", "1", "--n-gpu-layers", str(self.n_gpu_layers), *shared_server_flags()]
        if self.threads:
            cmd += ["--threads", str(self.threads)]
        return cmd

    def start(self) -> None:
        if self._proc is not None or not self.gguf:
            return
        self._proc = self.process_factory(self.command())
        deadline = time.monotonic() + self.start_timeout
        while time.monotonic() < deadline:
            if hasattr(self._proc, "poll") and self._proc.poll() is not None:
                raise BackendError("llama-server exited during start-up")
            try:
                if self._get("/health").get("status") in (None, "ok"):
                    return
            except (BackendError, OSError):
                time.sleep(0.5)
        raise BackendError("llama-server did not become healthy in time")

    def _get(self, path: str) -> dict[str, Any]:
        try:
            with urllib.request.urlopen(self.base_url + path, timeout=10) as r:  # noqa: S310 (localhost)
                return json.loads(r.read().decode("utf-8") or "{}")
        except urllib.error.URLError as exc:
            raise BackendError(str(exc)) from exc

    def _post(self, path: str, body: dict[str, Any], stream: bool = False):
        req = urllib.request.Request(self.base_url + path, data=json.dumps(body).encode("utf-8"),
                                     headers={"content-type": "application/json"}, method="POST")
        try:
            return urllib.request.urlopen(req, timeout=self.request_timeout)  # noqa: S310 (localhost)
        except urllib.error.URLError as exc:
            raise BackendError(str(exc)) from exc

    def info(self) -> dict[str, Any]:
        return {"device": self.device_label, "name": f"llama.cpp server ({Path(str(self.gguf)).name if self.gguf else self.base_url})"}

    def apply_template(self, messages: Messages) -> str:
        self.start()
        with self._post("/apply-template", {"messages": messages}) as r:
            return json.loads(r.read().decode("utf-8"))["prompt"]

    def generate(self, messages: Messages, *, max_new_tokens: int = 200, constraint: dict[str, Any] | None = None) -> GenResult:
        self.start()
        body: dict[str, Any] = {"messages": messages, "temperature": 0, "top_k": 1, "seed": 0, "max_tokens": max_new_tokens, "stream": True}
        if self.kwargs:
            body["chat_template_kwargs"] = self.kwargs
        if constraint:
            if "grammar" in constraint:
                body["grammar"] = constraint["grammar"]
            elif "json_schema" in constraint:
                body["json_schema"] = constraint["json_schema"]
        parts: list[str] = []
        first: float | None = None
        start = time.perf_counter()
        with self._post("/v1/chat/completions", body, stream=True) as resp:
            for raw in resp:
                line = raw.decode("utf-8").strip()
                if not line.startswith("data:"):
                    continue
                data = line[5:].strip()
                if data == "[DONE]":
                    break
                delta = json.loads(data)["choices"][0].get("delta", {}).get("content")
                if delta:
                    if first is None:
                        first = time.perf_counter()
                    parts.append(delta)
        end = time.perf_counter()
        return GenResult("".join(parts), (end - start) * 1000, None if first is None else (first - start) * 1000, len(parts))

    def close(self) -> None:
        if self._proc is not None:
            self._proc.terminate()
            try:
                self._proc.wait(timeout=20)
            except Exception:  # noqa: BLE001
                self._proc.kill()
            self._proc = None


class RoleMappedBackend:
    """Sends the protocol's result messages (role ``tool``) as ``user`` messages.

    The chat template of Qwen3.5 and later renders a ``tool`` message inside ``<tool_response>`` tags, which is the model's own
    tool format. The investigator protocol is the single definition of what a result looks like (operations.md 11.3), so a
    candidate with ``tool_role: "user"`` gets its results as plain user turns and nothing of the native format is injected."""

    def __init__(self, inner: Any, result_role: str = "tool"):
        self.inner, self.result_role = inner, result_role
        self.name = inner.name

    def _map(self, messages: Messages) -> Messages:
        return [{**m, "role": "user"} if m["role"] == self.result_role else m for m in messages]

    def info(self) -> dict[str, Any]:
        return self.inner.info()

    def generate(self, messages: Messages, **kw: Any) -> GenResult:
        return self.inner.generate(self._map(messages), **kw)

    def apply_template(self, messages: Messages) -> str:
        return self.inner.apply_template(self._map(messages))

    def close(self) -> None:
        self.inner.close()


def make_backend(export: str, run_dir: Path, *, kwargs: dict[str, Any] | None, manifest: dict[str, Any] | None, model: str,
                 version: str, base: dict[str, Any] | None = None, tools: dict[str, Any] | None = None,
                 device: str = "auto", ctx_size: int = 4096, tool_role: str | None = None) -> AnswerBackend:
    """The real backend of an export of a run: ``merged``, ``onnx`` or ``gguf``; ``base`` (hub id and revision) instead
    of the merged folder for an untrained candidate. ``tool_role`` ``user`` maps result messages to user turns."""
    inner = _make_backend(export, run_dir, kwargs=kwargs, manifest=manifest, model=model, version=version, base=base, tools=tools, device=device, ctx_size=ctx_size)
    return RoleMappedBackend(inner) if tool_role == "user" else inner


def _make_backend(export: str, run_dir: Path, *, kwargs: dict[str, Any] | None, manifest: dict[str, Any] | None, model: str,
                  version: str, base: dict[str, Any] | None, tools: dict[str, Any] | None, device: str, ctx_size: int) -> AnswerBackend:
    tools = tools or {}
    if export == "merged":
        if base:
            return TransformersBackend(base["hub_id"], base.get("revision"), kwargs, device)
        return TransformersBackend(run_dir / "merged", None, kwargs, device)
    if export == "onnx":
        return OnnxBackend(run_dir / "onnx", chat_template_kwargs=kwargs)
    if export == "gguf":
        entry = next((e for e in (manifest or {}).get("exports", []) if e["format"] == "gguf"), None)
        if entry is None:
            raise BackendError("exports.json has no gguf export")
        gguf = run_dir / "gguf" / entry["files"][0]["path"]
        layers = 0 if device == "cpu" else 999
        return LlamaServerBackend(gguf, tools.get("server_bin", "llama-server"), kwargs, ctx_size, layers)
    raise BackendError(f"unknown export {export}")
