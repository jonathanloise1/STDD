"""Merge and exports (models.md 6.5) and the template check (6.6), shared by both models.

The orchestration (``run_export``) is plain Python and is tested with ``FakeExportTools``; ``RealExportTools`` does the work with
lazily imported libraries and subprocesses (transformers and peft for the merge, the pinned transformers.js conversion script for
ONNX, the pinned llama.cpp scripts for GGUF, node for the template check) and is never constructed by a test or a dry run.

Layout of ``training/out/{model}/runs/{run_id}/``:

    merged/                         bf16 weights and tokenizer files
    onnx/                           the browser export root: config.json, tokenizer files, onnx/model_q4f16.onnx
    gguf/{model}-{version}-{variant}.gguf
    exports.json                    every file with SHA-256 and size (paths relative to the export's folder), template check result
"""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Mapping, Protocol, Sequence

from .backends import LlamaServerBackend
from .hashing import canonical_sha256, sha256_file
from .jsonio import load_json, write_json

RUNTIME = {"onnx": "browser", "gguf": "server"}
ONNX_MODEL_FILE = {"q4f16": "onnx/model_q4f16.onnx", "q8": "onnx/model_quantized.onnx"}
ONNX_SIDE_FILES = ["config.json", "generation_config.json", "tokenizer.json", "tokenizer_config.json"]


class ExportError(RuntimeError):
    pass


class ExportTools(Protocol):
    def merge(self, base: Mapping[str, Any], adapter_dir: Path | None, out: Path) -> None: ...
    def to_onnx(self, merged: Path, out: Path, variant: str) -> None: ...
    def to_gguf(self, merged: Path, outfile: Path, variant: str) -> None: ...
    def render_python(self, merged: Path, kwargs: Mapping[str, Any], cases: Sequence[Sequence[Mapping[str, str]]]) -> list[str]: ...
    def render_js(self, onnx_root: Path, kwargs: Mapping[str, Any], cases: Sequence[Sequence[Mapping[str, str]]]) -> list[str]: ...
    def render_llama(self, gguf: Path, kwargs: Mapping[str, Any], cases: Sequence[Sequence[Mapping[str, str]]]) -> list[str]: ...


def format_command(template: Sequence[str], values: Mapping[str, str]) -> list[str]:
    return [t.format(**values) for t in template]


def plan_commands(cfg: Mapping[str, Any], formats: Sequence[str], variants: Mapping[str, str], run_dir: Path, model: str, version: str,
                  checkouts: Mapping[str, str] | None = None, tier: str | None = None) -> list[tuple[str, list[str]]]:
    """The external commands a real export would run, with the placeholders of ``export.json`` filled in (for ``--dry-run``)."""
    checkouts = checkouts or {}
    merged = str(run_dir / "merged")
    steps: list[tuple[str, list[str]]] = [("merge", ["python", "<peft merge_and_unload>", merged])]
    if "onnx" in formats:
        tj = cfg["transformers_js"]
        script = str(Path(checkouts.get("transformers_js") or f"${{{tj['checkout_env']}}}") / tj["script"])
        steps.append(("onnx", format_command(tj["command"], {"script": script, "merged": merged, "out_dir": str(run_dir / "onnx"), "variant": variants["onnx"]})))
    if "gguf" in formats:
        lc = cfg["llama_cpp"]
        base = Path(checkouts.get("llama_cpp") or f"${{{lc['checkout_env']}}}")
        name = cfg["gguf_name"].format(model=model, model_version=version, variant=variants["gguf"], tier=tier or "")
        bf16 = str(run_dir / "gguf" / "model-bf16.gguf")
        values = {"script": str(base / lc["convert_script"]), "merged": merged, "bf16_gguf": bf16, "quantize_bin": str(find_quantize_bin(base, lc["quantize_bin"])),
                  "outfile": str(run_dir / "gguf" / name), "variant": variants["gguf"]}
        steps.append(("gguf-convert", format_command(lc["convert_command"], values)))
        steps.append(("gguf-quantize", format_command(lc["quantize_command"], values)))
    return steps


def find_quantize_bin(base: Path, name: str) -> Path:
    """``llama-quantize`` of a llama.cpp checkout: ``build/bin`` (Linux, macOS) or ``build/bin/Release`` (Visual Studio builds), with or without
    ``.exe``. When none exists the first candidate is returned, so that the error names a path."""
    stem = name[:-4] if name.lower().endswith(".exe") else name
    options = [base / "build" / "bin" / n for n in (stem, f"{stem}.exe")] + [base / "build" / "bin" / "Release" / n for n in (stem, f"{stem}.exe")]
    return next((p for p in options if p.is_file()), options[0])


def interpreter(cfg_section: Mapping[str, Any], env: Mapping[str, str]) -> str:
    """The Python that runs a converter: the interpreter named by the environment variable in the section's ``python_env`` (a converter with
    requirements of its own gets its own environment), else the one running this pipeline (``sys.executable``), never the first ``python`` on the PATH."""
    var = cfg_section.get("python_env")
    named = env.get(var) if var else None
    return named or sys.executable


def with_interpreter(cmd: Sequence[str], python: str) -> list[str]:
    return [python if i == 0 and c in ("python", "python3") else c for i, c in enumerate(cmd)]


def check_pins(cfg: Mapping[str, Any], formats: Sequence[str], env: Mapping[str, str]) -> list[str]:
    """Problems that stop a real export: unpinned exporter tags, missing checkouts."""
    problems = []
    for fmt, key in (("onnx", "transformers_js"), ("gguf", "llama_cpp")):
        if fmt not in formats:
            continue
        c = cfg[key]
        if not c.get("tag"):
            problems.append(f"export.json {key}.tag is not pinned (models.md 6.5)")
        d = env.get(c["checkout_env"])
        if not d or not Path(d).is_dir():
            problems.append(f"environment variable {c['checkout_env']} must name a checkout of {c['repo']} at the pinned tag")
    return problems


def manifest_files(root: Path, paths: Sequence[str]) -> list[dict[str, Any]]:
    return [{"path": p, "sha256": sha256_file(root / p), "bytes": (root / p).stat().st_size} for p in paths]


def list_files(root: Path) -> list[str]:
    return sorted(p.relative_to(root).as_posix() for p in root.rglob("*") if p.is_file())


def link_premade(source: Path, dest: Path) -> None:
    """Make the pre-made GGUF appear in the run's ``gguf`` folder under the name the lock expects: a symbolic link (a 21 GB file is not copied),
    a copy where links are not allowed."""
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.unlink(missing_ok=True)
    try:
        dest.symlink_to(source.resolve())
    except OSError:
        shutil.copyfile(source, dest)


def template_check(tools: ExportTools, run_dir: Path, kwargs: Mapping[str, Any], cases: Sequence[Sequence[Mapping[str, str]]], formats: Sequence[str],
                   gguf_file: Path | None) -> dict[str, Any]:
    """The rendered chat prompt of every case from the Python tokenizer, transformers.js and llama.cpp must be equal byte for byte (6.6)."""
    renders = {"python": tools.render_python(run_dir / "merged", kwargs, cases)}
    if "onnx" in formats:
        renders["transformers.js"] = tools.render_js(run_dir / "onnx", kwargs, cases)
    if "gguf" in formats and gguf_file is not None:
        renders["llama.cpp"] = tools.render_llama(gguf_file, kwargs, cases)
    mismatches = []
    for i in range(len(cases)):
        ref = renders["python"][i]
        for name, outs in renders.items():
            if outs[i] != ref:
                mismatches.append({"case": i, "renderer": name, "python": ref[:200], "other": outs[i][:200]})
    return {"passed": not mismatches, "cases": len(cases), "renderers": sorted(renders), "mismatches": mismatches[:10]}


@dataclass
class ExportRequest:
    model: str
    run_dir: Path
    version: str
    base: Mapping[str, Any]
    adapter_dir: Path | None
    kwargs: Mapping[str, Any]
    formats: Sequence[str]
    variants: Mapping[str, str]
    cases: Sequence[Sequence[Mapping[str, str]]]
    cfg: Mapping[str, Any]
    extras: Callable[[str, Mapping[str, Any]], dict[str, Any]] | None = None     # per-export extra keys (investigator: tier, context, memory)
    tier: str | None = None
    premade_gguf: Path | None = None        # an existing 4-bit GGUF of an untrained candidate: no merge, no conversion, the file is linked
    premade_sha256: str | None = None       # its hash, already checked against the configuration


def _stage_hash(req: ExportRequest, what: str) -> str:
    """Hash of the inputs of one stage: a change of the GGUF variant does not rebuild the merge or the ONNX export."""
    adapter = sha256_file(req.adapter_dir / "adapter_model.safetensors") if req.adapter_dir and (req.adapter_dir / "adapter_model.safetensors").exists() else None
    ident: dict[str, Any] = {"what": what, "base": [req.base.get("hub_id"), req.base.get("revision")], "adapter": adapter}
    if what.startswith("onnx"):
        ident.update(variant=req.variants["onnx"], tool=req.cfg["transformers_js"], merge=_stage_hash(req, "merge"))
    elif what.startswith("gguf"):
        ident.update(variant=req.variants["gguf"], tool=req.cfg["llama_cpp"], name=req.cfg["gguf_name"], version=req.version, tier=req.tier,
                     merge=_stage_hash(req, "merge"), premade=req.premade_sha256)
    return canonical_sha256(ident)


def run_export(req: ExportRequest, tools: ExportTools, log: Callable[[str], None] = print) -> dict[str, Any]:
    """Merge, convert, build the manifest and check the template. Each stage is skipped when its output exists with a matching
    input hash (resumable). Returns the manifest written to ``exports.json``."""
    d = req.run_dir
    state_path = d / "export-state.json"
    state: dict[str, str] = load_json(state_path) if state_path.exists() else {}

    def stage(name: str, produces: Callable[[], Path | None], build: Callable[[], None]) -> None:
        h = _stage_hash(req, name)
        out = produces()
        if state.get(name) == h and out is not None and out.exists():
            log(f"{name}: up to date, skipped")
            return
        log(f"{name}: building")
        build()
        state[name] = h
        write_json(state_path, state)

    merged = d / "merged"
    if req.premade_gguf is not None:
        if "onnx" in req.formats:
            raise ExportError("a pre-made GGUF has no ONNX export: use --formats gguf")
        log("merge: skipped (pre-made GGUF)")
    else:
        stage("merge", lambda: merged / "config.json", lambda: tools.merge(req.base, req.adapter_dir, merged))
    gguf_file: Path | None = None
    exports: list[dict[str, Any]] = []
    if "onnx" in req.formats:
        onnx_root = d / "onnx"
        v = req.variants["onnx"]
        stage(f"onnx-{v}", lambda: onnx_root / ONNX_MODEL_FILE[v], lambda: tools.to_onnx(merged, onnx_root, v))
        missing = [p for p in [ONNX_MODEL_FILE[v], *ONNX_SIDE_FILES] if not (onnx_root / p).exists()]
        if missing:
            raise ExportError(f"the ONNX export lacks: {', '.join(missing)}")
        entry = {"format": "onnx", "variant": v, "runtime": RUNTIME["onnx"], "files": manifest_files(onnx_root, list_files(onnx_root))}
        exports.append(entry)
    if "gguf" in req.formats:
        v = req.variants["gguf"]
        name = req.cfg["gguf_name"].format(model=req.model, model_version=req.version, variant=v, tier=req.tier or "")
        gguf_file = d / "gguf" / name
        stage(f"gguf-{v}", lambda: gguf_file, (lambda: link_premade(req.premade_gguf, gguf_file)) if req.premade_gguf is not None else (lambda: tools.to_gguf(merged, gguf_file, v)))
        if not gguf_file.exists():
            raise ExportError(f"the GGUF export {name} was not produced")
        exports.append({"format": "gguf", "variant": v, "runtime": RUNTIME["gguf"], "files": manifest_files(d / "gguf", [name])})
    for e in exports:
        if req.extras:
            e.update(req.extras(e["format"], e))
    check = template_check(tools, d, req.kwargs, req.cases, req.formats, gguf_file)
    manifest = {"schema_version": 1, "model": req.model, "model_version": req.version, "exports": exports, "template_check": check}
    write_json(d / "exports.json", manifest)
    return manifest


# ---------------------------------------------------------------- the real tools

class RealExportTools:
    """Real exporters. Every heavy import is inside a method; subprocess commands come from ``export.json``."""

    def __init__(self, cfg: Mapping[str, Any], env: Mapping[str, str] | None = None, node_script: Path | None = None,
                 runner: Callable[[list[str], Path | None], None] | None = None, log: Callable[[str], None] = print):
        self.cfg, self.env = cfg, dict(env if env is not None else os.environ)
        self.node_script = node_script
        self.log = log
        self.hub_tokenizer: tuple[str, str | None] | None = None      # (hub id, revision) for the template check of a pre-made GGUF with no merged folder
        self.run = runner or self._run

    def _run(self, cmd: list[str], cwd: Path | None = None) -> None:
        self.log("$ " + " ".join(cmd))
        subprocess.run(cmd, cwd=cwd, check=True)

    def merge(self, base: Mapping[str, Any], adapter_dir: Path | None, out: Path) -> None:
        out.mkdir(parents=True, exist_ok=True)
        if adapter_dir is None:
            # An untrained candidate has nothing to merge: take the pinned snapshot as it is. The model is not loaded, so the BF16 copy in memory
            # (about 70 GB for the 35B-A3B) is not made; the converters read the files.
            from huggingface_hub import snapshot_download  # type: ignore
            snapshot_download(base["hub_id"], revision=base.get("revision"), local_dir=str(out))
            return
        import torch  # type: ignore
        from transformers import AutoTokenizer  # type: ignore
        from peft import PeftModel  # type: ignore
        from .loading import load_text_model
        model = load_text_model(base["hub_id"], base.get("revision"), torch.bfloat16, {"freeze_vision_tower": False})
        model = PeftModel.from_pretrained(model, str(adapter_dir)).merge_and_unload()
        model.save_pretrained(str(out), safe_serialization=True)
        AutoTokenizer.from_pretrained(base["hub_id"], revision=base.get("revision")).save_pretrained(str(out))

    def to_onnx(self, merged: Path, out: Path, variant: str) -> None:
        tj = self.cfg["transformers_js"]
        script = Path(self.env[tj["checkout_env"]]) / tj["script"]
        out.mkdir(parents=True, exist_ok=True)
        cmd = format_command(tj["command"], {"script": str(script), "merged": str(merged), "out_dir": str(out), "variant": variant})
        self.run(with_interpreter(cmd, interpreter(tj, self.env)), None)
        nested = out / merged.name                       # the converter writes {output_parent_dir}/{model name}/...
        if nested.is_dir():
            for item in nested.iterdir():
                shutil.move(str(item), str(out / item.name))
            nested.rmdir()

    def to_gguf(self, merged: Path, outfile: Path, variant: str) -> None:
        lc = self.cfg["llama_cpp"]
        base = Path(self.env[lc["checkout_env"]])
        outfile.parent.mkdir(parents=True, exist_ok=True)
        bf16 = outfile.parent / "model-bf16.gguf"
        values = {"script": str(base / lc["convert_script"]), "merged": str(merged), "bf16_gguf": str(bf16), "variant": variant, "outfile": str(outfile),
                  "quantize_bin": str(find_quantize_bin(base, lc["quantize_bin"]))}
        self.run(with_interpreter(format_command(lc["convert_command"], values), interpreter(lc, self.env)), None)
        self.run(format_command(lc["quantize_command"], values), None)
        bf16.unlink(missing_ok=True)

    def render_python(self, merged: Path, kwargs: Mapping[str, Any], cases: Sequence[Sequence[Mapping[str, str]]]) -> list[str]:
        from transformers import AutoTokenizer  # type: ignore
        if not (merged / "config.json").exists() and self.hub_tokenizer:
            tok = AutoTokenizer.from_pretrained(self.hub_tokenizer[0], revision=self.hub_tokenizer[1])
        else:
            tok = AutoTokenizer.from_pretrained(str(merged))
        return [tok.apply_chat_template(list(c), tokenize=False, add_generation_prompt=True, **kwargs) for c in cases]

    def render_js(self, onnx_root: Path, kwargs: Mapping[str, Any], cases: Sequence[Sequence[Mapping[str, str]]]) -> list[str]:
        script = self.node_script or Path(__file__).resolve().parents[1] / "assistant" / "js" / "template_check.mjs"
        with tempfile.TemporaryDirectory() as tmp:
            src = Path(tmp) / "cases.json"
            src.write_text(json.dumps({"model_dir": str(onnx_root), "kwargs": dict(kwargs), "cases": [list(c) for c in cases]}), encoding="utf-8")
            try:
                res = subprocess.run(["node", str(script), str(src)], capture_output=True, text=True, check=True)
            except FileNotFoundError as exc:
                raise ExportError("node was not found: the template check needs Node 22.18 or later (models.md 9.1 step 9)") from exc
            except subprocess.CalledProcessError as exc:
                raise ExportError(f"the transformers.js template check failed (exit {exc.returncode}): {(exc.stderr or '').strip()[-600:]}") from exc
        return json.loads(res.stdout)

    def render_llama(self, gguf: Path, kwargs: Mapping[str, Any], cases: Sequence[Sequence[Mapping[str, str]]]) -> list[str]:
        lc = self.cfg["llama_cpp"]
        server = LlamaServerBackend(gguf, lc.get("server_bin", "llama-server"), kwargs, n_gpu_layers=0)
        try:
            return [server.apply_template(list(c)) for c in cases]
        finally:
            server.close()
