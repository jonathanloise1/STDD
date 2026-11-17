"""``models/model.lock``: record and verify (models.md section 8).

    python -m training.common.lock --record --model assistant --run 2026-11-20-a [--artifact-url URL]
    python -m training.common.lock --record --model embeddings --run 2026-11-20-a --artifact-url URL
    python -m training.common.lock --verify [--model assistant]

The ``embeddings`` entry (models.md 8.1, 7.8) is recorded from ``training/out/embeddings/runs/{run_id}/`` (``run.json``, ``exports.json``,
``calibration.json``); it has no adapter, protocol, evaluation or card, and ``--verify`` checks it with L02, L05, L07, L08 and L09.

The investigator has one export per tier (8.1, operations.md 11.2) and each tier is trained, exported and evaluated by its own run. Recording
a second run of the same ``model_version`` for another tier adds its exports to the entry instead of replacing it: the entry then carries
``tiers`` (per tier: ``status``, ``base_model``, ``adapter``, ``dataset``, ``training``, ``evaluation``) and its top-level fields mirror the
smallest tier recorded. Recording the same tier again replaces only that tier. A different ``model_version`` starts a new entry.

``--record`` reads ``training/out/{model}/runs/{run_id}/``:

- ``run.json``: top-level keys written by train and export (see ``RUN_KEYS``);
- ``exports.json``: ``{"exports": [{"format", "variant", "runtime", "files": [{"path", "sha256", "bytes"}]}]}`` (6.5);
- ``eval-{export}.json`` (4.6) for ``merged`` and for every export format.

Which reports must exist and pass is decided per model (``GATING``). The assistant's merged weights and every export must pass (models.md 6.6).
The investigator's gating runtime is ``gguf``, decoded under the grammar of operations.md 11.3; its merged weights run under transformers
without that grammar, so ``eval-merged.json`` is informative: it is copied when present and never required to exist or pass. Every export that
is deployed must still have passed. A report marked ``partial`` (a ``--suite`` or ``--limit`` run) is refused.

The gating report is copied to ``models/{model}/eval/{model_version}.json`` and is the one the lock records (``merged`` for the assistant, ``gguf``
for the investigator); the other reports are copied next to it as ``{model_version}-{export}.json``.
"""
from __future__ import annotations

import argparse
import json
import re
import shutil
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from .config import REPO_ROOT, out_dir
from .hashing import sha256_bytes, sha256_file
from .jsonio import dumps_pretty, load_json
from .weights import local_path, weights_dir

SCHEMA_VERSION = 2                                                              # 2 since the ``embeddings`` entry; a version 1 lock is read as 2 (8.2 L01)
READABLE_SCHEMAS = (1, 2)
ENTRY_KEYS = ["model_version", "status", "base_model", "adapter", "protocol", "retrieval", "contracts", "dataset", "training",
              "exports", "evaluation", "card", "recorded_at"]
EMBEDDINGS_KEYS = ["model_version", "status", "base_model", "distillation", "contracts", "exports", "calibration", "card", "recorded_at"]
TIERS_AFTER = "evaluation"                                                      # the key an investigator entry's ``tiers`` follows
TIERS = ["small", "large"]                                                     # smallest first
TIER_KEYS = ["status", "base_model", "adapter", "dataset", "training", "evaluation"]
RUN_KEYS = ["model_version", "status", "base_model", "adapter", "dataset", "training"]   # plus optional "artifact_url"
CONTRACT_KEYS = {"assistant": ["manual-page", "help-pack", "assistant-protocol", "assistant-retrieval"],
                 "embeddings": ["help-pack", "assistant-retrieval"]}
CARD_HEADINGS = ["Summary", "Intended use", "Out-of-scope use", "Base model and licence", "Training data", "Evaluation", "Exports",
                 "Runtime", "Browser check", "Limitations", "Version history"]
GATING = {"investigator": "gguf"}                                                # the report an entry records; "merged" for any other model
INFORMATIVE = {"investigator": ("merged",)}                                    # reports that are copied when present and need not pass
SHA_RE = re.compile(r"^[0-9a-f]{64}$")
REVISION_RE = re.compile(r"^[0-9a-f]{40}$")
SEMVER_RE = re.compile(r"^\d+\.\d+\.\d+$")


class LockError(RuntimeError):
    pass


@dataclass
class Finding:
    id: str
    model: str | None
    message: str

    def __str__(self) -> str:
        return f"{self.id} {self.model or '-'}: {self.message}"


def lock_path(root: Path = REPO_ROOT) -> Path:
    return root / "models" / "model.lock"


def read_lock(path: Path) -> dict[str, Any]:
    """Parse the lock (L01). Raises ``LockError``."""
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise LockError(f"{path} does not exist") from exc
    except json.JSONDecodeError as exc:
        raise LockError(f"{path} does not parse: {exc}") from exc
    if not isinstance(data, dict) or data.get("schema_version") not in READABLE_SCHEMAS or not isinstance(data.get("models"), dict):
        raise LockError(f"{path}: unknown schema_version or missing models")
    return data


def ordered_entry(entry: dict[str, Any]) -> dict[str, Any]:
    out: dict[str, Any] = {}
    for k in (EMBEDDINGS_KEYS if "distillation" in entry else ENTRY_KEYS):
        if k in entry:
            out[k] = entry[k]
        if k == TIERS_AFTER and "tiers" in entry:
            out["tiers"] = entry["tiers"]
    out.update({k: v for k, v in entry.items() if k not in out})
    return out


def write_lock(path: Path, data: dict[str, Any]) -> None:
    """Keys in the order of 8.1, models sorted by id."""
    from .jsonio import atomic_write_text
    body = {"schema_version": SCHEMA_VERSION, "models": {m: ordered_entry(e) for m, e in sorted(data["models"].items())}}
    atomic_write_text(path, dumps_pretty(body))


def current_contracts(root: Path, model: str) -> dict[str, int]:
    doc = load_json(root / "docs" / "contracts.json")["contracts"]
    keys = CONTRACT_KEYS.get(model)
    if keys is None:
        raise LockError(f"no contract list for model {model}: pass 'contracts' in run.json")
    missing = [k for k in keys if k not in doc]
    if missing:
        raise LockError(f"docs/contracts.json lacks: {', '.join(missing)}")
    return {k: doc[k]["version"] for k in keys}


def _rel(path: Path, root: Path) -> str:
    return path.relative_to(root).as_posix()


def build_entry(model: str, run_id: str, root: Path = REPO_ROOT, artifact_url: str | None = None,
                runs_root: Path | None = None, now: datetime | None = None) -> tuple[dict[str, Any], dict[str, Path]]:
    """Build the lock entry of a run and return it with the report files to copy ``{name: source path}``.
    Raises ``LockError`` when a report is missing or did not pass."""
    if model == "embeddings":
        return build_embeddings_entry(run_id, root, artifact_url, runs_root, now)
    run_dir = (runs_root or out_dir(model, root / "training")) / "runs" / run_id
    try:
        run = load_json(run_dir / "run.json")
        manifest = load_json(run_dir / "exports.json")
    except FileNotFoundError as exc:
        raise LockError(f"missing {exc.filename}") from exc
    absent = [k for k in RUN_KEYS if k not in run]
    if absent:
        raise LockError(f"run.json lacks: {', '.join(absent)}")
    version = run["model_version"]
    if not SEMVER_RE.match(version):
        raise LockError(f"model_version {version!r} is not a semantic version")
    if run["status"] not in ("trained", "untrained-candidate"):
        raise LockError("status must be trained or untrained-candidate")
    if run["status"] == "untrained-candidate" and (run["adapter"] is not None or run["dataset"] is not None):
        raise LockError("an untrained candidate has adapter and dataset null")
    base_url = artifact_url or run.get("artifact_url")
    if not base_url:
        raise LockError("the artifact store URL is needed: pass --artifact-url or set artifact_url in run.json")

    run_tiers = sorted({e["tier"] for e in manifest["exports"] if e.get("tier")} | ({run["tier"]} if run.get("tier") else set()), key=_tier_rank)
    if len(run_tiers) > 1:
        raise LockError(f"a run holds one tier; this one has {', '.join(run_tiers)}: record one run per tier")
    if run_tiers and run_tiers[0] not in TIERS:
        raise LockError(f"tier {run_tiers[0]!r} is not one of {', '.join(TIERS)}")
    tier = run_tiers[0] if run_tiers else None
    gating = GATING.get(model, "merged")
    informative = INFORMATIVE.get(model, ())
    formats = ["merged"] + [e["format"] for e in manifest["exports"]]
    if gating not in formats:
        raise LockError(f"the {model} is recorded on its {gating} report, and this run has no {gating} export")
    reports: dict[str, Path] = {}
    for fmt in formats:
        p = run_dir / f"eval-{fmt}.json"
        if fmt in informative and not p.exists():
            continue
        if not p.exists():
            raise LockError(f"missing evaluation report {p.name}")
        rep = load_json(p)
        if rep.get("partial"):
            if fmt in informative:
                continue                                                       # not a result: left out of the copies
            raise LockError(f"evaluation report {p.name} is a partial run ({rep['partial']}): run the full evaluation again; --record refuses it")
        if rep.get("model_version") != version:
            raise LockError(f"{p.name} is for model_version {rep.get('model_version')}, run.json says {version}")
        if rep.get("passed") is not True and fmt not in informative:
            raise LockError(f"evaluation report {p.name} did not pass: --record refuses it")
        reports[fmt] = p
    main_report = load_json(reports[gating])

    model_dir = root / "models" / model
    stem = f"{version}-{tier}" if tier else version                      # a tier keeps its own reports
    copies: dict[str, Path] = {f"{stem}.json": reports[gating]}
    copies.update({f"{stem}-{fmt}.json": p for fmt, p in reports.items() if fmt != gating})
    report_sha = sha256_file(reports[gating])

    entry: dict[str, Any] = {
        "model_version": version, "status": run["status"], "base_model": run["base_model"], "adapter": run["adapter"],
        "protocol": run.get("protocol") or _file_ref(root, f"training/{model}/protocol.json"),
        "retrieval": run.get("retrieval") if "retrieval" in run else (_file_ref(root, f"training/{model}/retrieval.json") if (root / "training" / model / "retrieval.json").exists() else None),
        "contracts": run.get("contracts") or current_contracts(root, model),
        "dataset": run["dataset"], "training": run["training"],
        "exports": [{"format": e["format"], "variant": e["variant"], "runtime": e["runtime"],
                     "url": e.get("url") or f"{base_url.rstrip('/')}/{e['format']}/",
                     **{k: e[k] for k in ("tier", "context_length", "memory_mb") if k in e},
                     "files": e["files"]} for e in manifest["exports"]],
        "evaluation": {"suite_version": main_report["suite_version"], "report": f"models/{model}/eval/{stem}.json",
                       "report_sha256": report_sha, "passed": True},
        "card": f"models/{model}/MODEL_CARD.md",
        "recorded_at": (now or datetime.now(timezone.utc)).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    if entry["retrieval"] is None:
        del entry["retrieval"]
    if tier:
        entry["tiers"] = {tier: {k: entry[k] for k in TIER_KEYS}}
    return entry, {str(model_dir / "eval" / name): src for name, src in copies.items()}


def build_embeddings_entry(run_id: str, root: Path = REPO_ROOT, artifact_url: str | None = None, runs_root: Path | None = None,
                           now: datetime | None = None) -> tuple[dict[str, Any], dict[str, Path]]:
    """The ``embeddings`` entry of 8.1 from ``run.json``, ``exports.json`` and ``calibration.json`` of a run of
    ``training/out/embeddings/runs/{run_id}/``. Refuses, writing nothing, when one is missing, when the run is not ``status: built`` or used an
    unverified model, when the calibration lacks a language of the exports or names another table, or when no artifact URL is known."""
    run_dir = (runs_root or out_dir("embeddings", root / "training")) / "runs" / run_id
    try:
        run = load_json(run_dir / "run.json")
        manifest = load_json(run_dir / "exports.json")
        cal_path = run_dir / "calibration.json"
        cal = load_json(cal_path)
    except FileNotFoundError as exc:
        name = Path(exc.filename).name
        hint = ": run `npm run help:bench -- --calibrate --embeddings {run folder} --unlocked` (manual.md 6.7.7)" if name == "calibration.json" else ""
        raise LockError(f"missing {name}{hint}") from exc
    if run.get("status") != "built":
        raise LockError("run.json is not status built")
    if run.get("verified") is not True:
        raise LockError("the run used a model whose verified flag is not true (rehearsal): record only a run made with a verified model")
    absent = [k for k in ("model_version", "base_model", "distillation") if k not in run]
    if absent:
        raise LockError(f"run.json lacks: {', '.join(absent)}")
    version = run["model_version"]
    if not SEMVER_RE.match(version):
        raise LockError(f"model_version {version!r} is not a semantic version")
    base_url = artifact_url or run.get("artifact_url")
    if not base_url:
        raise LockError("the artifact store URL is needed: pass --artifact-url or set artifact_url in run.json")
    exports = manifest.get("exports") or []
    if not exports or not all(x.get("files") for x in exports):
        raise LockError("exports.json has no files")
    langs: dict[str, str] = {}
    for x in exports:
        for fl in x["files"]:
            m = re.fullmatch(r"([a-z]{2})\.bin", fl["path"])
            if not m:
                raise LockError(f"exports.json: {fl['path']} is not a table file ({{lang}}.bin)")
            langs[m.group(1)] = fl["sha256"]
    cal_langs = cal.get("languages") or {}
    for lang, sha in sorted(langs.items()):
        c = cal_langs.get(lang)
        if not c:
            raise LockError(f"calibration.json has no language {lang}")
        if c.get("table_sha256") != sha:
            raise LockError(f"calibration.json for {lang} is for another table (table_sha256 differs from the run's {lang}.bin): calibrate again")
        if not isinstance(c.get("params"), dict):
            raise LockError(f"calibration.json has no params for {lang}")
    base = run["base_model"]
    entry: dict[str, Any] = {
        "model_version": version, "status": "distilled",
        "base_model": {k: base[k] for k in ("hub_id", "revision", "license", "parameters", "dimensions")},
        "distillation": run["distillation"],
        "contracts": current_contracts(root, "embeddings"),
        "exports": [{"format": x["format"], "variant": x["variant"], "runtime": x["runtime"],
                     "url": x.get("url") or f"{base_url.rstrip('/')}/", "files": x["files"]} for x in exports],
        "calibration": {"report": f"models/embeddings/calibration/{version}.json", "report_sha256": sha256_file(cal_path),
                        "params": {lang: cal_langs[lang]["params"] for lang in sorted(langs)}},
        "card": None,
        "recorded_at": (now or datetime.now(timezone.utc)).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    return entry, {str(root / "models" / "embeddings" / "calibration" / f"{version}.json"): cal_path}


def _tier_rank(tier: str) -> int:
    return TIERS.index(tier) if tier in TIERS else len(TIERS)


def merge_tier(old: dict[str, Any], new: dict[str, Any]) -> dict[str, Any]:
    """Add the tier of ``new`` to the entry ``old`` of the same model version (operations.md 11.2: one export per tier).

    Refuses when the two runs were made against a different protocol or contracts: a tier trained on another protocol cannot share an entry."""
    for key in ("protocol", "contracts"):
        if old.get(key) != new.get(key):
            raise LockError(f"the {key} of this run differs from the {key} of the tiers already recorded for {new['model_version']}: "
                            "retrain the other tier or bump the model version")
    tiers = {t: dict(v) for t, v in (old.get("tiers") or {}).items()}
    tiers.update({t: dict(v) for t, v in new["tiers"].items()})
    replaced = set(new["tiers"])
    exports = [x for x in old["exports"] if x.get("tier") not in replaced] + list(new["exports"])
    exports.sort(key=lambda x: (_tier_rank(x.get("tier", "")), x["format"]))
    first = min(tiers, key=_tier_rank)                                      # the top-level fields mirror the smallest tier
    merged = {**new, **tiers[first], "exports": exports, "tiers": dict(sorted(tiers.items(), key=lambda kv: _tier_rank(kv[0])))}
    return merged


def _file_ref(root: Path, rel: str) -> dict[str, str]:
    p = root / rel
    if not p.exists():
        raise LockError(f"{rel} does not exist")
    return {"path": rel, "sha256": sha256_file(p)}


def record(model: str, run_id: str, root: Path = REPO_ROOT, artifact_url: str | None = None, runs_root: Path | None = None,
           now: datetime | None = None) -> dict[str, Any]:
    entry, copies = build_entry(model, run_id, root, artifact_url, runs_root, now)
    path = lock_path(root)
    data = read_lock(path) if path.exists() else {"schema_version": SCHEMA_VERSION, "models": {}}
    old = data["models"].get(model)
    if entry.get("tiers") and old and old.get("model_version") == entry["model_version"] and old.get("tiers"):
        entry = merge_tier(old, entry)                      # may refuse: before anything is written
    for dest, src in copies.items():
        Path(dest).parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(src, dest)
    data["models"][model] = entry
    write_lock(path, data)
    return entry


# ---------------------------------------------------------------- verify (8.2)

def verify(root: Path = REPO_ROOT, only: str | None = None, lock: Path | None = None) -> list[Finding]:
    """Checks L01 to L09. An empty list means the lock verifies."""
    path = lock or lock_path(root)
    try:
        data = read_lock(path)
    except LockError as exc:
        return [Finding("L01", None, str(exc))]
    findings: list[Finding] = []
    for model, e in sorted(data["models"].items()):
        if only and model != only:
            continue
        findings += _verify_entry(model, e, root)
    if only and only not in data["models"]:
        findings.append(Finding("L01", only, "no entry in the lock"))
    return findings


def _current_analyzer(root: Path, lang: str) -> dict[str, Any]:
    """The analyzer a table must have been built with: the Python port's version and the stopword list of ``root`` (or the port's own)."""
    from training.assistant import lexical as lex                                   # noqa: PLC0415 (lazy: keeps the lock tool light)
    for p in (root / "src" / "frontend" / "src" / "help" / "lexical" / f"stopwords.{lang}.txt",
              root / "training" / "assistant" / "config" / f"stopwords.{lang}.txt", lex.stopwords_path(lang)):
        if p.is_file():
            return {"id": f"snowball-{lang}", "version": lex.ANALYZER_VERSION, "stopwords_sha256": sha256_file(p)}
    raise LockError(f"no stopword list for {lang}")


def _verify_embeddings_entry(model: str, e: dict[str, Any], root: Path) -> list[Finding]:
    """L09 and the generic L02, L05, L07 and L08 for the ``embeddings`` entry. L03, L04 and L06 do not apply (8.2)."""
    f: list[Finding] = []
    add = lambda i, m: f.append(Finding(i, model, m))   # noqa: E731
    try:
        doc = load_json(root / "docs" / "contracts.json")["contracts"]
        for k, v in (e.get("contracts") or {}).items():
            cur = doc.get(k, {}).get("version")
            if cur != v:
                add("L02", f"contract {k} is {v} in the lock and {cur} in docs/contracts.json: rebuild the tables (models.md section 11)")
    except (OSError, KeyError, json.JSONDecodeError) as exc:
        add("L02", f"docs/contracts.json cannot be read: {exc}")
    exports = e.get("exports")
    if not isinstance(exports, list) or not exports:
        add("L05", "no exports")
    else:
        for x in exports:
            label = f"{x.get('format')}/{x.get('variant')}"
            if not x.get("url"):
                add("L05", f"export {label} has no url")
            if not x.get("files"):
                add("L05", f"export {label} has no file list")
                continue
            for fl in x["files"]:
                if not fl.get("path") or not SHA_RE.match(str(fl.get("sha256", ""))) or not isinstance(fl.get("bytes"), int):
                    add("L05", f"export {label}: file entry without path, SHA-256 or size: {fl}")
    f += _verify_unlocked_packs(model, root)
    for x in exports if isinstance(exports, list) else []:
        for fl in x.get("files") or []:
            w = weights_dir(model, root) / local_path(model, x, fl)
            if w.is_file() and sha256_file(w) != fl.get("sha256"):
                add("L08", f"{w} differs from the recorded SHA-256")

    # L09
    base = e.get("base_model") or {}
    if not REVISION_RE.match(str(base.get("revision", ""))):
        add("L09", f"base_model.revision {base.get('revision')!r} is not 40 hexadecimal characters")
    if not base.get("license"):
        add("L09", "base_model.license is empty")
    dist = e.get("distillation") or {}
    cfg = root / ((dist.get("config") or {}).get("path") or "training/assistant/config/embed.json")
    if not cfg.is_file():
        add("L09", f"{cfg.relative_to(root) if cfg.is_relative_to(root) else cfg} does not exist")
    elif sha256_file(cfg) != (dist.get("config") or {}).get("sha256"):
        add("L09", "distillation.config.sha256 differs from the current embed.json: rebuild the tables")
    for lang, d in sorted((dist.get("languages") or {}).items()):
        try:
            cur = _current_analyzer(root, lang)
        except LockError as exc:
            add("L09", str(exc))
            continue
        if d.get("analyzer") != cur:
            add("L09", f"the {lang} analyzer {d.get('analyzer')} differs from the current {cur}: rebuild the tables (help:build would refuse them)")
    cal = e.get("calibration") or {}
    rp = root / cal.get("report", "")
    if not cal.get("report") or not rp.is_file():
        add("L09", f"the calibration report {cal.get('report')} does not exist")
    elif sha256_file(rp) != cal.get("report_sha256"):
        add("L09", "the calibration report SHA-256 differs from the lock")
    params = cal.get("params") or {}
    for x in exports if isinstance(exports, list) else []:
        for fl in x.get("files") or []:
            m = re.fullmatch(r"([a-z]{2})\.bin", fl.get("path", ""))
            if m and not isinstance(params.get(m.group(1)), dict):
                add("L09", f"calibration.params has no entry for {m.group(1)}")
    return f


def _verify_unlocked_packs(model: str, root: Path) -> list[Finding]:
    """L07, second half: a built pack whose ``embeddings`` block has ``locked: false`` (a table used with --unlocked)."""
    f: list[Finding] = []
    for pack in sorted((root / "src" / "frontend" / "public" / "help").glob("*/pack.json")):
        try:
            block = json.loads(pack.read_text(encoding="utf-8")).get("embeddings")
        except json.JSONDecodeError:
            continue
        if isinstance(block, dict) and block.get("locked") is False:
            f.append(Finding("L07", model, f"the built pack {pack.parent.name}/pack.json uses a table that is not locked (embeddings.locked is false)"))
    return f


def _verify_entry(model: str, e: dict[str, Any], root: Path) -> list[Finding]:
    if model == "embeddings":
        return _verify_embeddings_entry(model, e, root)
    f: list[Finding] = []
    add = lambda i, m: f.append(Finding(i, model, m))   # noqa: E731

    # L02 contracts
    try:
        doc = load_json(root / "docs" / "contracts.json")["contracts"]
        for k, v in (e.get("contracts") or {}).items():
            cur = doc.get(k, {}).get("version")
            if cur != v:
                add("L02", f"contract {k} is {v} in the lock and {cur} in docs/contracts.json: retrain (models.md section 11)")
    except (OSError, KeyError, json.JSONDecodeError) as exc:
        add("L02", f"docs/contracts.json cannot be read: {exc}")

    # L03 protocol and retrieval
    for key in ("protocol", "retrieval"):
        ref = e.get(key)
        if not ref:
            if key == "protocol":
                add("L03", "no protocol entry")
            continue
        if "path" not in ref:
            continue
        if "markers" in ref:                           # investigator protocol block: hashed between its markers (operations.md 11.3)
            f += _verify_marked_block(model, ref, root)
            continue
        p = root / ref["path"]
        if not p.exists():
            add("L03", f"{ref['path']} does not exist")
        elif sha256_file(p) != ref.get("sha256"):
            add("L03", f"{key}.sha256 differs from the current bytes of {ref['path']}")

    # L04 evaluation (one per tier for a tiered entry, and the top-level one)
    _verify_evaluation(model, e.get("evaluation") or {}, root, "", f)
    for t, section in sorted((e.get("tiers") or {}).items()):
        _verify_evaluation(model, section.get("evaluation") or {}, root, f"tier {t}: ", f)

    # L05 exports
    exports = e.get("exports")
    if not isinstance(exports, list) or not exports:
        add("L05", "no exports")
    else:
        for x in exports:
            label = f"{x.get('format')}/{x.get('variant')}"
            if not x.get("url"):
                add("L05", f"export {label} has no url")
            files = x.get("files")
            if not files:
                add("L05", f"export {label} has no file list")
                continue
            for fl in files:
                if not fl.get("path") or not SHA_RE.match(str(fl.get("sha256", ""))) or not isinstance(fl.get("bytes"), int):
                    add("L05", f"export {label}: file entry without path, SHA-256 or size: {fl}")

    f += _verify_tiers(model, e)

    # L06 card
    card = root / (e.get("card") or f"models/{model}/MODEL_CARD.md")
    if not card.is_file():
        add("L06", f"the card {card.relative_to(root) if card.is_relative_to(root) else card} is missing")
    else:
        heads = re.findall(r"^## (.+?)\s*$", card.read_text(encoding="utf-8"), re.M)
        missing = [h for h in CARD_HEADINGS if h not in heads]
        if missing:
            add("L06", f"the card lacks the headings: {', '.join(missing)}")
        elif heads != CARD_HEADINGS:
            add("L06", f"the card headings are not exactly {CARD_HEADINGS} in order")

    # L07 built packs
    if model == "assistant":
        for pack in sorted((root / "src" / "frontend" / "public" / "help").glob("*/pack.json")):
            try:
                comp = json.loads(pack.read_text(encoding="utf-8")).get("assistant", {}).get("compatible")
            except json.JSONDecodeError:
                add("L07", f"{pack.parent.name}/pack.json does not parse")
                continue
            if comp is False:
                add("L07", f"the built pack {pack.parent.name}/pack.json has assistant.compatible false")
        f += _verify_unlocked_packs(model, root)

    # L08 weights
    for x in exports if isinstance(exports, list) else []:
        for fl in x.get("files") or []:
            w = weights_dir(model, root) / local_path(model, x, fl)
            if w.is_file() and sha256_file(w) != fl.get("sha256"):
                add("L08", f"{w} differs from the recorded SHA-256")
    return f


def _verify_marked_block(model: str, ref: dict[str, Any], root: Path) -> list[Finding]:
    """L03 for a protocol that is a block between two markers of a file: the SHA-256 of the bytes between them (models.md 8.1)."""
    p = root / ref["path"]
    if not p.exists():
        return [Finding("L03", model, f"{ref['path']} does not exist")]
    begin, end = (str(m).encode() for m in ref["markers"])
    data = p.read_bytes()
    if data.count(begin) != 1 or data.count(end) != 1:
        return [Finding("L03", model, f"the protocol markers must occur exactly once in {ref['path']}")]
    a, b = data.index(begin) + len(begin), data.index(end)
    if b < a or sha256_bytes(data[a:b]) != ref.get("sha256"):
        return [Finding("L03", model, f"protocol.sha256 differs from the current block of {ref['path']}")]
    return []


def _verify_evaluation(model: str, ev: dict[str, Any], root: Path, label: str, f: list[Finding]) -> None:
    add = lambda i, m: f.append(Finding(i, model, label + m))   # noqa: E731
    rp = root / ev.get("report", "")
    if ev.get("passed") is not True:
        add("L04", "evaluation.passed is not true")
    if not ev.get("report") or not rp.is_file():
        add("L04", f"report {ev.get('report')} does not exist")
        return
    if sha256_file(rp) != ev.get("report_sha256"):
        add("L04", "the report SHA-256 differs from the lock")
    try:
        want = load_json(rp).get("thresholds_sha256")
        cfg = root / "training" / model / "config" / "eval.json"
        if not cfg.exists():
            add("L04", f"training/{model}/config/eval.json does not exist")
        elif sha256_file(cfg) != want:
            add("L04", "thresholds_sha256 of the report differs from the current eval.json")
    except json.JSONDecodeError:
        add("L04", "the report does not parse")


def _verify_tiers(model: str, e: dict[str, Any]) -> list[Finding]:
    """L05 for the tier fields of an investigator entry: every tiered export names a known tier with its context length and memory, one
    export per tier and format, and a ``tiers`` section exists for every tier that has an export."""
    exports = [x for x in (e.get("exports") or []) if isinstance(x, dict)]
    if not any("tier" in x for x in exports) and not e.get("tiers"):
        return []
    f: list[Finding] = []
    add = lambda m: f.append(Finding("L05", model, m))   # noqa: E731
    seen: set[tuple[str, str]] = set()
    for x in exports:
        label = f"{x.get('format')}/{x.get('variant')}"
        t = x.get("tier")
        if t not in TIERS:
            add(f"export {label} has no valid tier ({', '.join(TIERS)})")
            continue
        for k in ("context_length", "memory_mb"):
            if not isinstance(x.get(k), int) or isinstance(x.get(k), bool) or x[k] <= 0:
                add(f"export {label} ({t}) has no positive integer {k}")
        if (t, x.get("format")) in seen:
            add(f"two {x.get('format')} exports for tier {t}")
        seen.add((t, x.get("format")))
        if t not in (e.get("tiers") or {}):
            add(f"tier {t} has an export but no section in tiers")
    for t, section in (e.get("tiers") or {}).items():
        if t not in TIERS:
            add(f"tiers has the unknown tier {t}")
        elif not any(x.get("tier") == t for x in exports):
            add(f"tier {t} has a section but no export")
        elif any(k not in section for k in TIER_KEYS):
            add(f"tier {t} lacks {', '.join(k for k in TIER_KEYS if k not in section)}")
    return f


# ---------------------------------------------------------------- CLI

def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="python -m training.common.lock", description="Record or verify models/model.lock (models.md section 8).")
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--record", action="store_true", help="record a run in the lock (refuses a run whose reports did not pass)")
    g.add_argument("--verify", action="store_true", help="checks L01 to L09; exit 1 on any failure")
    ap.add_argument("--model", help="model id (required for --record)")
    ap.add_argument("--run", help="run id {yyyy-mm-dd}-{letter} (required for --record)")
    ap.add_argument("--artifact-url", help="base URL of the artifact store for the exports (or artifact_url in run.json)")
    ap.add_argument("--root", type=Path, default=REPO_ROOT, help="repository root (tests)")
    args = ap.parse_args(argv)
    if args.record:
        if not args.model or not args.run:
            ap.error("--record needs --model and --run")
        try:
            e = record(args.model, args.run, args.root, args.artifact_url)
        except LockError as exc:
            print(f"refused: {exc}", file=sys.stderr)
            return 1
        print(f"recorded {args.model} {e['model_version']} in {lock_path(args.root)}")
        return 0
    findings = verify(args.root, args.model)
    for x in findings:
        print(x)
    print("lock verifies" if not findings else f"{len(findings)} failure(s)")
    return 1 if findings else 0


if __name__ == "__main__":
    raise SystemExit(main())
