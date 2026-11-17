"""Teacher client, job format, cache and resume (models.md 5.6).

Nothing here calls the network unless ``HttpTeacherClient.complete`` is invoked, and that only happens in a real
``generate`` run approved with ``--approved-estimate`` or in the ``--measure`` command, both against the configured local
host. Tests and dry runs use ``FakeTeacher``-style clients.
"""
from __future__ import annotations

import json
import os
import re
import time
import urllib.error
import urllib.request
from concurrent.futures import FIRST_COMPLETED, ThreadPoolExecutor, wait
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Callable, Iterable, Protocol

from .hashing import canonical_sha256, derive_seed
from .jsonio import atomic_write_text, dumps_pretty
from .templates import Template

JOB_SCHEMA_VERSION = 1


def make_job(*, stage: str, key: str, domain: str, lang: str | None, template: Template, variables: dict[str, Any],
             model: str, params: dict[str, Any], master_seed: int) -> dict[str, Any]:
    """The job record of 5.6, keys in the order of the document. ``lang`` is ``null`` for language-free jobs."""
    return {
        "schema_version": JOB_SCHEMA_VERSION,
        "stage": stage,
        "key": key,
        "domain": domain,
        "lang": lang,
        "template": template.name,
        "template_sha256": template.sha256,
        "vars": variables,
        "model": model,
        "params": params,
        "seed": derive_seed(master_seed, key),
    }


def job_hash(job: dict[str, Any]) -> str:
    """SHA-256 of the canonical job record: the cache key."""
    return canonical_sha256(job)


@dataclass
class Completion:
    text: str
    input_tokens: int
    output_tokens: int


class TeacherClient(Protocol):
    def complete(self, *, model: str, system: str, user: str, params: dict[str, Any], seed: int) -> Completion: ...


class TeacherError(RuntimeError):
    pass


class TransientTeacherError(TeacherError):
    """Rate limit, timeout or server error: the runner retries."""


DEFAULT_BASE_URL = "http://localhost:8093/v1"
# Qwen3.5 and later think by default; the pipeline wants the answer only (models.md 5.6). The Qwen3 /think and /nothink switches are not supported.
DEFAULT_TEMPLATE_KWARGS = {"enable_thinking": False}


def strip_think(text: str) -> str:
    """Remove ``<think>...</think>`` blocks. A ``</think>`` with no opening tag (the template already opened it) drops everything before it;
    an opening tag that is never closed drops the rest (a cut-off thought is not an answer). Text without think tags is returned unchanged."""
    if "think>" not in text:
        return text
    text = re.sub(r"<think>.*?</think>", "", text, flags=re.S)
    if "</think>" in text:
        text = text.rsplit("</think>", 1)[1]
    if "<think>" in text:
        text = text.split("<think>", 1)[0]
    return text.strip()


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    """A redirect would send the request to a host nobody configured: it is refused."""

    def redirect_request(self, req, fp, code, msg, headers, newurl):  # noqa: D401, ANN001
        return None


class HttpTeacherClient:
    """OpenAI-compatible chat endpoint through the standard library; the only protocol the pipeline speaks.

    ``base_url`` defaults to the local ``teacher`` host. The API key is optional: it is sent only when the environment
    variable named by ``api_key_env`` is set (a local proxy that requires one). Requests go only to ``base_url``: no
    redirect is followed and no proxy from the environment is used.
    """

    def __init__(self, base_url: str | None = None, api_key_env: str | None = None, timeout: float = 600.0,
                 environ: dict[str, str] | None = None, chat_template_kwargs: dict[str, Any] | None = None):
        self.template_kwargs = dict(DEFAULT_TEMPLATE_KWARGS if chat_template_kwargs is None else chat_template_kwargs)
        self.base_url = (base_url or DEFAULT_BASE_URL).rstrip("/")
        if not self.base_url.startswith(("http://", "https://")):
            raise ValueError(f"base_url must start with http:// or https://: {self.base_url}")
        self.api_key_env, self.timeout = api_key_env or None, timeout
        self._environ = environ if environ is not None else os.environ
        self._opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), _NoRedirect)

    def _key(self) -> str | None:
        return (self._environ.get(self.api_key_env) or None) if self.api_key_env else None

    def complete(self, *, model: str, system: str, user: str, params: dict[str, Any], seed: int) -> Completion:
        headers = {"content-type": "application/json"}
        key = self._key()
        if key:
            headers["authorization"] = f"Bearer {key}"
        msgs = ([{"role": "system", "content": system}] if system else []) + [{"role": "user", "content": user}]
        body = {"model": model, "max_tokens": params.get("max_tokens", 1024), "temperature": params.get("temperature", 0.0),
                "messages": msgs, "seed": seed % (2 ** 31)}
        if self.template_kwargs:
            body["chat_template_kwargs"] = self.template_kwargs
        req = urllib.request.Request(f"{self.base_url}/chat/completions", data=json.dumps(body).encode("utf-8"), headers=headers,
                                     method="POST")
        try:
            with self._opener.open(req, timeout=self.timeout) as resp:  # noqa: S310 (the configured host only)
                payload = json.loads(resp.read().decode("utf-8"))
        except urllib.error.HTTPError as exc:
            if exc.code in (408, 409, 429) or exc.code >= 500:
                raise TransientTeacherError(f"HTTP {exc.code}") from exc
            raise TeacherError(f"HTTP {exc.code}: {exc.read()[:300]!r}") from exc
        except (urllib.error.URLError, TimeoutError, ConnectionError) as exc:
            raise TransientTeacherError(f"{exc} (is the teacher host running at {self.base_url}?)") from exc
        try:
            text = strip_think(payload["choices"][0]["message"]["content"] or "")
        except (KeyError, IndexError, TypeError) as exc:
            raise TeacherError("the endpoint answered without choices[0].message.content") from exc
        usage = payload.get("usage") or {}
        return Completion(text, int(usage.get("prompt_tokens", 0)), int(usage.get("completion_tokens", 0)))


class JobCache:
    """``training/out/assistant/teacher-cache/{h[0:2]}/{h}.json``; a job with a cached response is never sent again."""

    def __init__(self, root: Path):
        self.root = Path(root)

    def path(self, h: str) -> Path:
        return self.root / h[0:2] / f"{h}.json"

    def get(self, job: dict[str, Any]) -> dict[str, Any] | None:
        p = self.path(job_hash(job))
        if not p.exists():
            return None
        try:
            return json.loads(p.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            return None  # half-written entry: treated as missing, will be redone

    def put(self, job: dict[str, Any], entry: dict[str, Any]) -> None:
        atomic_write_text(self.path(job_hash(job)), dumps_pretty(entry))


@dataclass
class RunResult:
    sent: int = 0
    cached: int = 0
    failed: list[tuple[str, str]] = field(default_factory=list)
    stopped_for_time: bool = False
    elapsed_seconds: float = 0.0
    pending: int = 0

    @property
    def complete(self) -> bool:
        return not self.failed and not self.stopped_for_time and self.pending == 0


class JobRunner:
    """Runs jobs through a client with cache, resume, retries, concurrency and an optional wall-clock limit.

    ``max_seconds`` (``None`` for no limit) stops sending new jobs once the run has lasted that long; running the same
    command again resumes, because every answered job is cached.
    """

    def __init__(self, cache: JobCache, client: TeacherClient, render: Callable[[dict[str, Any]], tuple[str, str]],
                 max_seconds: float | None = None, max_concurrency: int = 8, retries: int = 3,
                 sleep: Callable[[float], None] = time.sleep, on_progress: Callable[[str], None] | None = None,
                 clock: Callable[[], float] = time.monotonic):
        self.cache, self.client, self.render = cache, client, render
        self.max_seconds, self.max_concurrency, self.retries, self.sleep = max_seconds, max(1, max_concurrency), retries, sleep
        self.clock = clock
        self.on_progress = on_progress or (lambda _m: None)

    def _send(self, job: dict[str, Any]) -> tuple[Completion, float]:
        system, user = self.render(job)
        delay = 1.0
        for attempt in range(self.retries + 1):
            try:
                extra = {"job": job} if getattr(self.client, "wants_job", False) else {}
                t0 = self.clock()
                comp = self.client.complete(model=job["model"], system=system, user=user, params=job["params"], seed=job["seed"], **extra)
                return comp, max(0.0, self.clock() - t0)
            except TransientTeacherError:
                if attempt == self.retries:
                    raise
                self.sleep(delay)
                delay *= 2
        raise AssertionError("unreachable")

    def run(self, jobs: Iterable[dict[str, Any]]) -> RunResult:
        result = RunResult()
        todo: list[dict[str, Any]] = []
        for job in jobs:
            if self.cache.get(job) is not None:
                result.cached += 1
            else:
                todo.append(job)
        started = self.clock()

        def out_of_time() -> bool:
            return self.max_seconds is not None and self.clock() - started >= self.max_seconds

        queue = list(todo)
        in_flight: dict[Any, dict[str, Any]] = {}
        with ThreadPoolExecutor(max_workers=self.max_concurrency) as pool:
            while queue or in_flight:
                while queue and len(in_flight) < self.max_concurrency and not out_of_time():
                    job = queue.pop(0)
                    in_flight[pool.submit(self._send, job)] = job
                if not in_flight:
                    break  # time limit reached with nothing in flight
                done, _ = wait(list(in_flight), return_when=FIRST_COMPLETED)
                for fut in done:
                    job = in_flight.pop(fut)
                    try:
                        comp, seconds = fut.result()
                    except Exception as exc:  # noqa: BLE001 - recorded per job, run continues
                        result.failed.append((job["key"], f"{type(exc).__name__}: {exc}"))
                        continue
                    self.cache.put(job, {"job_sha256": job_hash(job), "key": job["key"], "stage": job["stage"],
                                         "model": job["model"], "response": comp.text,
                                         "usage": {"input_tokens": comp.input_tokens, "output_tokens": comp.output_tokens},
                                         "seconds": round(seconds, 3)})
                    result.sent += 1
                    self.on_progress(f"{job['stage']} {job['key']}")
        result.pending = len(queue)
        result.stopped_for_time = bool(queue) and out_of_time()
        result.elapsed_seconds = self.clock() - started
        return result
