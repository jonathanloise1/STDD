"""``models/{id}/MODEL_CARD.md``: refresh the tables from the lock and the reports (models.md 8.3).

    python -m training.common.card --model assistant

Exactly the 11 H2 headings in order. The text of ``Limitations`` and ``Browser check`` is preserved; ``Version history``
keeps its lines and gains one for the recorded version.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any

from .config import REPO_ROOT
from .jsonio import atomic_write_text, load_json
from .lock import CARD_HEADINGS, LockError, lock_path, read_lock

DEFAULTS = {"Limitations": "To be written by the owner.",
            "Browser check": "Not run yet. The check of models.md 6.6: 20 fixed items of the sample app bench asked through the help panel on a machine with a real GPU.\n\n| Item | Browser answer equals the ONNX report | Notes |\n|------|---------------------------------------|-------|"}
PRESERVED = ("Limitations", "Browser check")


def split_card(text: str) -> dict[str, str]:
    """Body text of each H2 section of an existing card."""
    out: dict[str, str] = {}
    cur: str | None = None
    buf: list[str] = []
    for line in text.split("\n"):
        m = re.match(r"^## (.+?)\s*$", line)
        if m:
            if cur is not None:
                out[cur] = "\n".join(buf).strip("\n")
            cur, buf = m.group(1), []
        elif cur is not None:
            buf.append(line)
    if cur is not None:
        out[cur] = "\n".join(buf).strip("\n")
    return out


def _table(header: list[str], rows: list[list[Any]]) -> str:
    lines = ["| " + " | ".join(header) + " |", "|" + "|".join("---" for _ in header) + "|"]
    lines += ["| " + " | ".join(str(c) for c in r) + " |" for r in rows]
    return "\n".join(lines)


def _mb(n: int) -> str:
    return f"{n / 1_000_000:.1f} MB"


def render(model: str, entry: dict[str, Any], root: Path, existing: str | None = None, report: dict[str, Any] | None = None) -> str:
    old = split_card(existing) if existing else {}
    ver, status = entry["model_version"], entry["status"]
    bm = entry["base_model"]
    ds = entry.get("dataset")
    ppath = root / entry["protocol"]["path"] if entry.get("protocol", {}).get("path") else None
    protocol = load_json(ppath) if ppath is not None and ppath.suffix == ".json" and ppath.exists() else {}      # the investigator's protocol is a block of a markdown file
    inv = model == "investigator"
    langs = sorted(protocol.get("languages", {})) or ["en", "it"]
    sec: dict[str, str] = {}

    what = ("investigates a residual incident of a running application with read-only tools and writes an evidence-backed report, or abstains. It runs on the "
            "server from the exports listed below") if inv else ("answers a user's question from retrieved manual sections, or refuses. It runs in the browser or "
                                                                "on the server from the exports listed below")
    sec["Summary"] = (f"{_table(['Field', 'Value'], [['Model id', model], ['Version', ver], ['Status', status], ['Recorded at', entry['recorded_at']]])}\n\n"
                      f"The `{model}` model {what} and is used only with the protocol recorded in the lock.")
    sec["Intended use"] = (f"The operations investigator of the monitor. The protocol is the block of `{entry.get('protocol', {}).get('path', 'n/a')}` between its markers "
                           f"(SHA-256 `{entry.get('protocol', {}).get('sha256', 'n/a')}`): one system and one user message, then one call or one report per turn, with "
                           f"decoding constrained by the grammar of operations.md 11.3. Language: English.") if inv else (f"The in-page assistant of the help panel. The protocol is `{entry.get('protocol', {}).get('path', 'n/a')}` "
                           f"(SHA-256 `{entry.get('protocol', {}).get('sha256', 'n/a')}`): two messages, system and user, with up to four whole manual sections and the "
                           f"question; the answer is a short text and a `Source:` line, or the fixed refusal sentence. Languages: {', '.join(langs)}.")
    sec["Out-of-scope use"] = ("Any use outside the protocol or without the monitor's host: acting on systems (the tools are read-only), investigating without masked inputs, "
                               "or use as a general chat model. Without the model the monitor reports the rule verdict only.") if inv else ("Any use outside the protocol: other prompts, answering without a help pack, answering from sections of another project's "
                               "manual, or use as a general chat model. Without a compatible pack the application uses retrieval only.")
    sec["Base model and licence"] = _table(["Field", "Value"], [["Hub id", bm.get("hub_id")], ["Revision", bm.get("revision")], ["Licence", bm.get("license")], ["Parameters", f"{bm.get('parameters'):,}" if isinstance(bm.get("parameters"), int) else bm.get("parameters")]])
    if ds is None:
        sec["Training data"] = "None: this is an untrained candidate. No project content is in the weights."
    else:
        d = ds.get("domains", {})
        classes = ""
        ds_cfg = root / "training" / model / "config" / "dataset.json"
        if ds_cfg.exists():
            shares = load_json(ds_cfg).get("classes", {})
            classes = " Class shares: " + ", ".join(f"{k} {v:.0%}" for k, v in shares.items()) + "."
        sec["Training data"] = (f"Synthetic only: {d.get('train', '?')} train, {d.get('validation', '?')} validation and {d.get('test', '?')} test domains (fictional applications), "
                                f"{ds.get('examples', '?')} examples.{classes} Teacher models: {', '.join(ds.get('teacher_models', [])) or 'n/a'}. "
                                f"Dataset SHA-256 `{ds.get('dataset_sha256')}`. No project content is in the weights.")
    ev = entry["evaluation"]
    ev_lines = [f"Suite version {ev['suite_version']}; passed: {ev['passed']}; report `{ev['report']}` (SHA-256 `{ev['report_sha256']}`)."]
    if report:
        rows = []
        for suite, by_lang in report.get("results", {}).items():
            if suite == "latency" or not isinstance(by_lang, dict):
                continue
            if "metrics" in by_lang:                       # investigator suites: one result per suite
                rows.append([suite, "-", ", ".join(f"{k} {v}" for k, v in by_lang["metrics"].items())])
                continue
            for lang, m in by_lang.items():
                rows.append([suite, lang, ", ".join(f"{k} {v}" for k, v in m.items()) if isinstance(m, dict) else str(m)])
        if rows:
            ev_lines.append(_table(["Suite", "Language", "Metrics"], rows))
        if report.get("judge") is None and inv:
            ev_lines.append("No judge: every metric is computed deterministically from the reports (operations.md 11.4).")
        else:
            ha = (report.get("judge") or {}).get("human_agreement")
            ev_lines.append("Judge agreement with human labels: " + (", ".join(f"{k} {v}" for k, v in ha.items()) if ha else "not recorded") + ".")
            audit = report.get("audit")
            ev_lines.append("Human audit (30 random judged items): " + (str(audit) if audit else "not recorded") + ".")
    sec["Evaluation"] = "\n\n".join(ev_lines)
    sec["Exports"] = _table(["Format", "Variant", "Runtime", "File", "Size", "SHA-256"],
                            [[x["format"], x["variant"] + (f" ({x['tier']})" if x.get("tier") else ""), x["runtime"], fl["path"], _mb(fl["bytes"]), f"`{fl['sha256']}`"] for x in entry["exports"] for fl in x["files"]])
    rt = ["Tiers: server only (GGUF, llama.cpp, constrained decoding), GPU then CPU, then rules only."] if inv else ["Tiers: browser (ONNX, WebGPU), server (GGUF, llama.cpp), then retrieval only."]
    lat = (report or {}).get("results", {}).get("latency")
    if lat:
        rt.append(_table(["Measure", "Value"], [[k, v] for k, v in lat.items()]))
    disabled = (report or {}).get("disabled_tiers") or []
    rt.append("Disabled tiers: " + (", ".join(f"{t.get('tier')} ({t.get('reason')})" for t in disabled) if disabled else "none recorded") + ".")
    sec["Runtime"] = "\n\n".join(rt)
    for h in PRESERVED:
        sec[h] = old.get(h) or DEFAULTS[h]
    hist = [l for l in old.get("Version history", "").split("\n") if l.strip()]
    line = f"- {ver} ({entry['recorded_at'][:10]}): {status}, evaluation passed: {ev['passed']}"
    hist = [l for l in hist if not l.startswith(f"- {ver} ")] + [line]
    sec["Version history"] = "\n".join(hist)
    return f"# {model} model card\n\n" + "\n\n".join(f"## {h}\n\n{sec[h]}" for h in CARD_HEADINGS) + "\n"


def refresh(model: str, root: Path = REPO_ROOT) -> Path:
    data = read_lock(lock_path(root))
    if model not in data["models"]:
        raise LockError(f"no {model} entry in the lock: run lock --record first")
    if model == "embeddings":
        raise LockError("the embeddings entry has no card: no trained weights ship, and the model, revision and licence are in the entry (models.md 8.1)")
    entry = data["models"][model]
    path = root / entry.get("card", f"models/{model}/MODEL_CARD.md")
    existing = path.read_text(encoding="utf-8") if path.exists() else None
    rp = root / entry["evaluation"]["report"]
    report = load_json(rp) if rp.exists() else None
    atomic_write_text(path, render(model, entry, root, existing, report))
    return path


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="python -m training.common.card", description="Refresh models/{id}/MODEL_CARD.md from the lock (models.md 8.3).")
    ap.add_argument("--model", required=True)
    ap.add_argument("--root", type=Path, default=REPO_ROOT, help="repository root (tests)")
    args = ap.parse_args(argv)
    try:
        print(f"wrote {refresh(args.model, args.root)}")
    except LockError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
