"""``python -m training.assistant.labels``: builds ``training/assistant/eval/human-labels.jsonl``, the items a person labels once per suite
version so that the judge can be calibrated on them (models.md 4.4: 120 items, 60 per language, the judge must agree on at least 0.90).

    labels select [--answers items-{id}.jsonl ...] [--per-language 60]    pick the items from the frozen test set into a work file
    labels label  [--lang en|it]                                          label them in the terminal, one item at a time; resumable
    labels status                                                         how many are labelled
    labels finish                                                         check the work file and write human-labels.jsonl

The work file (``training/out/assistant/labels/todo.jsonl``, one item per line) can also be completed with an editor: set ``faithful`` and
``useful`` to ``true`` or ``false`` on every line, then run ``finish``. Every answer a person labels is saved at once, so quitting loses nothing.

What is labelled is an answer, so both labels must occur: the items alternate between the teacher's reference answer and, when ``--answers``
gives the rows of an evaluation (``training/out/assistant/candidates/items-{id}.jsonl``, set ``platform_test``), the answer the model really gave.
Without ``--answers`` every answer is the reference and almost every label will be true: the calibration then proves little, and the command says so.
No model runs, no judge is called and no model host is needed.

A label is two judgements about the answer, against the numbered sections and the reference (4.3): ``faithful``, every statement of the answer is
supported by the sections; ``useful``, the answer gives what the user needs to act, nothing missing that the gold section states.
"""
from __future__ import annotations

import argparse
import hashlib
import sys
from pathlib import Path
from typing import Any, Callable, Mapping, Sequence

from training.assistant import eval as AE
from training.common.jsonio import read_jsonl, write_jsonl

LANGS = AE.LANGS
IN_SCOPE_ORDER = ("pos_distant", "pos_mid", "pos_near", "pos_multi")          # taken in turn, so every overlap band is labelled
LABEL_KEYS = ["id", "question", "context", "gold", "reference", "answer", "faithful", "useful"]     # exactly what `eval` reads (judge.agreement)
SEED = 20261106


class LabelError(RuntimeError):
    pass


def todo_path(env: AE.Env) -> Path:
    return env.out / "labels" / "todo.jsonl"


def labels_path(env: AE.Env, cfg: Mapping[str, Any]) -> Path:
    return env.repo_root / cfg["judge"]["human_labels"]


def _order_key(seed: int, item_id: str) -> str:
    return hashlib.sha256(f"{seed}:{item_id}".encode("utf-8")).hexdigest()


# ---------------------------------------------------------------- select

def pick_items(items: Sequence[Mapping[str, Any]], per_language: int, seed: int = SEED) -> dict[str, list[Mapping[str, Any]]]:
    """Per language ``per_language`` in-scope items, the classes taken in turn, each class in a seeded order. Deterministic."""
    out: dict[str, list[Mapping[str, Any]]] = {}
    for lang in LANGS:
        by_class = {c: sorted((i for i in items if i["lang"] == lang and i["class"] == c and i.get("target")), key=lambda i: _order_key(seed, i["id"]))
                    for c in IN_SCOPE_ORDER}
        chosen: list[Mapping[str, Any]] = []
        cursor = {c: 0 for c in IN_SCOPE_ORDER}
        while len(chosen) < per_language and any(cursor[c] < len(by_class[c]) for c in IN_SCOPE_ORDER):
            for c in IN_SCOPE_ORDER:
                if cursor[c] < len(by_class[c]) and len(chosen) < per_language:
                    chosen.append(by_class[c][cursor[c]])
                    cursor[c] += 1
        out[lang] = chosen
    return out


def model_answers(paths: Sequence[Path]) -> dict[str, str]:
    """Answers the model really gave: rows of the set ``platform_test`` with outcome ``answered``, by item id (the first file that has one wins)."""
    found: dict[str, str] = {}
    for p in paths:
        for r in read_jsonl(p):
            if r.get("set") == "platform_test" and r.get("outcome") == "answered" and r.get("answer") and r["id"] not in found:
                found[r["id"]] = r["answer"]
    return found


def build_todo(items: Sequence[Mapping[str, Any]], test_sha: str, per_language: int, answers: Mapping[str, str], seed: int = SEED) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for lang, chosen in pick_items(items, per_language, seed).items():
        for k, it in enumerate(chosen):
            own = answers.get(it["id"]) if k % 2 == 1 else None               # alternate, so that wrong answers are among the items
            rows.append({"id": it["id"], "lang": lang, "class": it["class"], "question": it["question"], "context": it["context"], "gold": it["gold"],
                         "reference": it["target"], "answer": own or it["target"], "answer_source": "model" if own else "reference",
                         "test_set_sha256": test_sha, "faithful": None, "useful": None})
    return rows


def cmd_select(env: AE.Env, args: argparse.Namespace) -> int:
    cfg = AE.load_eval_config(env)
    test_sha = AE.check_frozen(env, cfg)
    todo = todo_path(env)
    if todo.exists() and not args.force:
        raise LabelError(f"{todo} exists and may hold labels: continue with `labels label`, or pass --force to start over")
    items = AE.load_items(env, "test")
    answers = model_answers(args.answers or [])
    rows = build_todo(items, test_sha, args.per_language, answers, args.seed)
    have = {lang: sum(r["lang"] == lang for r in rows) for lang in LANGS}
    if any(n < args.per_language for n in have.values()):
        env.log(f"warning: the test set has too few in-scope items for {args.per_language} per language (selected {have}); `finish` then needs --per-language")
    write_jsonl(todo, rows)
    own = sum(r["answer_source"] == "model" for r in rows)
    env.log(f"{len(rows)} items written to {todo} ({own} with a model answer, {len(rows) - own} with the reference answer)")
    if not own:
        env.log("warning: no model answers (--answers): the labels will be almost all true and the calibration proves little (models.md 4.4)")
    env.log("next: python -m training.assistant.labels label    (or edit the file, then `finish`)")
    return 0


# ---------------------------------------------------------------- label

def _load_todo(env: AE.Env) -> list[dict[str, Any]]:
    p = todo_path(env)
    if not p.exists():
        raise LabelError(f"{p} does not exist: run `labels select` first")
    return read_jsonl(p)


def counts(rows: Sequence[Mapping[str, Any]]) -> dict[str, int]:
    done = sum(r["faithful"] is not None and r["useful"] is not None for r in rows)
    return {"total": len(rows), "labelled": done, "left": len(rows) - done}


def render_item(row: Mapping[str, Any], position: str) -> str:
    sections = "\n\n".join(f"[{i}] {s['title']}\n{s['text']}" for i, s in enumerate(row["context"], 1))
    gold = ", ".join(f"[{g}]" for g in row["gold"]) or "none"
    bar = "=" * 78
    return (f"\n{bar}\nItem {position}  {row['id']}  ({row['lang']}, {row['class']})\n{bar}\nSECTIONS\n{sections}\n\nGOLD SECTION(S): {gold}\n"
            f"REFERENCE ANSWER\n{row['reference']}\n\nQUESTION\n{row['question']}\n\nANSWER TO LABEL\n{row['answer']}\n")


def _ask(prompt: str, read: Callable[[str], str]) -> bool | None:
    """``True`` for y, ``False`` for n, ``None`` to stop. Anything else asks again."""
    while True:
        a = read(prompt).strip().lower()
        if a in ("y", "yes"):
            return True
        if a in ("n", "no"):
            return False
        if a in ("q", "quit"):
            return None


def cmd_label(env: AE.Env, args: argparse.Namespace, read: Callable[[str], str] = input) -> int:
    rows = _load_todo(env)
    path = todo_path(env)
    order = [i for i, r in enumerate(rows) if (not args.lang or r["lang"] == args.lang) and (r["faithful"] is None or r["useful"] is None)]
    env.log("Label each answer. faithful: every statement is supported by the sections above. useful: the answer gives what the user needs, "
            "nothing the gold section states and the question needs is missing. y = yes, n = no, q = quit and save (nothing is lost).")
    for n, i in enumerate(order, 1):
        row = rows[i]
        env.log(render_item(row, f"{n}/{len(order)} to label"))
        faithful = _ask("faithful? [y/n/q] ", read)
        if faithful is None:
            break
        useful = _ask("useful?   [y/n/q] ", read)
        if useful is None:
            break
        row["faithful"], row["useful"] = faithful, useful
        write_jsonl(path, rows)                                            # saved after every item: the flow is resumable
    c = counts(rows)
    env.log(f"{c['labelled']} of {c['total']} labelled" + ("" if c["left"] else "; run `labels finish`"))
    return 0


def cmd_status(env: AE.Env, args: argparse.Namespace) -> int:
    rows = _load_todo(env)
    c = counts(rows)
    for lang in LANGS:
        sub = counts([r for r in rows if r["lang"] == lang])
        env.log(f"{lang}: {sub['labelled']} of {sub['total']} labelled")
    env.log(f"total: {c['labelled']} of {c['total']} labelled")
    return 0


# ---------------------------------------------------------------- finish

def check_complete(rows: Sequence[Mapping[str, Any]], test_ids: set[str], test_sha: str, per_language: int) -> list[str]:
    problems = []
    for lang in LANGS:
        n = sum(r["lang"] == lang for r in rows)
        if n != per_language:
            problems.append(f"{lang}: {n} items, {per_language} are needed (models.md 4.4)")
    ids = [r["id"] for r in rows]
    if len(set(ids)) != len(ids):
        problems.append("an id occurs twice")
    for r in rows:
        if r["id"] not in test_ids:
            problems.append(f"{r['id']} is not an item of the frozen test set")
        elif r.get("test_set_sha256") != test_sha:
            problems.append(f"{r['id']} was selected from another test set: select again")
        if not isinstance(r["faithful"], bool) or not isinstance(r["useful"], bool):
            problems.append(f"{r['id']} is not labelled: faithful and useful must be true or false")
    return problems[:30]


def cmd_finish(env: AE.Env, args: argparse.Namespace) -> int:
    cfg = AE.load_eval_config(env)
    test_sha = AE.check_frozen(env, cfg)
    rows = _load_todo(env)
    problems = check_complete(rows, {i["id"] for i in AE.load_items(env, "test")}, test_sha, len(rows) // len(LANGS) if args.per_language is None else args.per_language)
    if problems:
        raise LabelError("not finished:\n  " + "\n  ".join(problems))
    out = labels_path(env, cfg)
    write_jsonl(out, [{k: r[k] for k in LABEL_KEYS} for r in rows])
    env.log(f"{len(rows)} labelled items written to {out}")
    for metric in ("faithful", "useful"):
        yes = sum(r[metric] for r in rows)
        env.log(f"  {metric}: {yes} true, {len(rows) - yes} false")
        if yes in (0, len(rows)):
            env.log(f"  warning: every {metric} label is the same, so the judge cannot be shown to tell the two apart")
    env.log("commit the file; it is used by every evaluation of this suite version (models.md 4.4)")
    return 0


# ---------------------------------------------------------------- CLI

def build_parser() -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser(prog="python -m training.assistant.labels", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="command", required=True)
    s = sub.add_parser("select", help="pick the items from the frozen test set into the work file")
    s.add_argument("--answers", type=Path, action="append", help="items-{id}.jsonl of an evaluation: its answers are labelled on every second item (repeatable)")
    s.add_argument("--per-language", type=int, default=60)
    s.add_argument("--seed", type=int, default=SEED)
    s.add_argument("--force", action="store_true", help="replace an existing work file, labels included")
    l = sub.add_parser("label", help="label the items in the terminal; resumable")
    l.add_argument("--lang", choices=list(LANGS), help="only this language")
    sub.add_parser("status", help="how many items are labelled")
    f = sub.add_parser("finish", help="check the work file and write human-labels.jsonl")
    f.add_argument("--per-language", type=int, help="expected items per language (default: the size of the work file divided by two)")
    return ap


def main(argv: list[str] | None = None, env: AE.Env | None = None, read: Callable[[str], str] = input) -> int:
    args = build_parser().parse_args(argv)
    env = env or AE.Env()
    try:
        if args.command == "select":
            return cmd_select(env, args)
        if args.command == "label":
            return cmd_label(env, args, read)
        if args.command == "status":
            return cmd_status(env, args)
        return cmd_finish(env, args)
    except (LabelError, AE.EvalError, FileNotFoundError) as exc:
        print(f"refused: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
