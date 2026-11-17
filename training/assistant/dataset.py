"""Assembly of the dataset from the pipeline state, answer and verification jobs (models.md 5.3, 5.6, 5.9)."""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

from training.assistant import assemble as A, protocol as P, textchecks
from training.assistant.jobs import Pipeline
from training.common.hashing import derive_seed, rng

SPLITS = ["train", "validation", "test"]


@dataclass
class Assembly:
    examples: dict[str, list[A.Example]] = field(default_factory=dict)       # train, validation, test, seen
    counts_target: dict[str, dict[str, int]] = field(default_factory=dict)   # train class counts per language
    shortfall: dict[str, Any] = field(default_factory=dict)
    skipped: int = 0

    def all(self) -> list[A.Example]:
        return [e for k in ("train", "validation", "test", "seen") for e in self.examples.get(k, [])]


def assemble(pipe: Pipeline) -> Assembly:
    """Deterministic: the same teacher cache gives the same examples. Memoised on the pipeline."""
    if pipe._assembly is not None:
        return pipe._assembly
    cfg, master = pipe.cfg, pipe.master
    blocklist = textchecks.load_blocklist()
    res = Assembly(examples={"train": [], "validation": [], "test": [], "seen": []})
    pools: dict[tuple[str, str], dict[str, list[A.QRec]]] = {}

    for st in pipe.alive():
        for lang in pipe.langs:
            by_id = {s["id"]: s for s in st.sections.get(lang, [])}
            raw = sorted(st.pool.get(lang, []), key=lambda q: (q.key, q.idx))

            def rej(v: str, q: A.QRec, reason: str, _st=st, _lang=lang) -> None:
                pipe.reject("questions", v, q.qid, reason, _st.id, _lang)

            kept = A.prepare_pool(questions=raw, index=st.index[lang], sections_by_id=by_id, lang=lang,
                                  retrieval=pipe.retrieval, blocklist=blocklist, reject=rej)
            pools[(st.id, lang)] = {k: [q for q in kept if q.kind == k] for k in ("story", "multi", "near_miss", "oos")}

    pending: dict[tuple[str, str, str], list[tuple[str, A.QRec]]] = {}   # (split-ish, domain, lang) -> (class, qrec)

    # evaluation splits
    for st in pipe.alive():
        split = st.cfg["split"]
        for lang in pipe.langs:
            p = pools[(st.id, lang)]
            if split in A.EVAL_COMPOSITION:
                items, short = A.eval_selection(split, p["story"], p["near_miss"], p["oos"], master, st.id, lang)
                pending[(split, st.id, lang)] = items
                if any(short.values()):
                    res.shortfall[f"{st.id}/{lang}"] = short

    # training split: global quotas per language, seen-domain holdout first
    train_states = [s for s in pipe.alive() if s.cfg["split"] == "train"]
    for lang in pipe.langs:
        story_pool: list[A.QRec] = []
        multi: list[A.QRec] = []
        near: list[A.QRec] = []
        oos: list[A.QRec] = []
        for st in train_states:
            p = pools[(st.id, lang)]
            kept, seen = A.hold_out_seen(p["story"], master, st.id, lang)
            story_pool += kept
            multi += p["multi"]
            near += p["near_miss"]
            oos += p["oos"]
            pending[("seen", st.id, lang)] = [(f"pos_{q.bucket}", q) for q in seen]
        buckets = {b: [q for q in story_pool if q.bucket == b] for b in ("distant", "mid", "near")}
        pool_sizes = {"pos_distant": len(buckets["distant"]), "pos_mid": len(buckets["mid"]), "pos_near": len(buckets["near"]),
                      "pos_multi": len(multi), "ref_near_miss": len(near), "ref_oos": len(oos)}
        target = cfg["target_examples"]["train"] // len(pipe.langs)
        counts = A.train_counts(pool_sizes, len(story_pool), cfg["classes"], target)
        res.counts_target[lang] = counts
        if sum(counts.values()) < target:
            res.shortfall[f"train/{lang}"] = {"target": target, "assembled": sum(counts.values()), "pools": pool_sizes}
        chosen: list[tuple[str, A.QRec]] = []
        used: set[str] = set()
        for cls, src in (("pos_distant", buckets["distant"]), ("pos_mid", buckets["mid"]), ("pos_near", buckets["near"]),
                         ("pos_multi", multi), ("ref_near_miss", near), ("ref_oos", oos)):
            ordered = sorted(src, key=lambda q: q.qid)
            rng(master, f"train/{lang}/{cls}").shuffle(ordered)
            take = ordered[: counts[cls]]
            chosen += [(cls, q) for q in take]
            used |= {q.qid for q in take if q.kind == "story"}
        rest = sorted((q for q in story_pool if q.qid not in used), key=lambda q: q.qid)
        rng(master, f"train/{lang}/ref_removed").shuffle(rest)
        chosen += [("ref_removed", q) for q in rest[: counts["ref_removed"]]]
        by_domain: dict[str, list[tuple[str, A.QRec]]] = {}
        for cls, q in chosen:
            by_domain.setdefault(q.domain, []).append((cls, q))
        for d, items in by_domain.items():
            pending[("train", d, lang)] = items

    # ids, contexts
    counters: dict[tuple[str, str], int] = {}
    for kind in ("train", "seen", "validation", "test"):
        for st in pipe.alive():
            for lang in pipe.langs:
                items = pending.get((kind, st.id, lang))
                if not items:
                    continue
                items = list(items)
                rng(master, f"{st.id}/{lang}/{kind}/order").shuffle(items)
                for cls, q in items:
                    n = counters.get((st.id, lang), 0) + 1
                    counters[(st.id, lang)] = n
                    eid = f"{st.id}-{lang}-{n:06d}"
                    train_like = kind == "train"
                    k = rng(master, f"{eid}/k").randint(2, 4) if train_like else None
                    built = A.build_context(protocol=pipe.protocol, retrieval=pipe.retrieval, index=st.index[lang], question=q.text,
                                            gold=q.gold, k=k, cls=cls, master_seed=master, example_id=eid, train_order=train_like)
                    if built is None:
                        res.skipped += 1
                        continue
                    ctx, positions, top = built
                    overlap = q.overlap if cls.startswith("pos_") else _overlap_vs(q.text, top, lang)
                    target = pipe.protocol["languages"][lang]["refusal"] if cls.startswith("ref_") else None
                    res.examples[kind].append(A.Example(
                        id=eid, split=("train" if kind == "seen" else kind), domain=st.id, lang=lang, app_name=st.domain.app_name,
                        cls=cls, style=q.style, question=q.text,
                        context=[{"id": s["id"], "title": s["title"], "text": s["text"]} for s in ctx],
                        gold=positions, overlap=overlap, seed=derive_seed(master, f"example/{eid}"), qkey=q.qid, target=target))
    pipe._assembly = res
    return res


def _overlap_vs(question: str, section: dict[str, Any], lang: str) -> float:
    from training.assistant import lexical
    return lexical.overlap(question, [section], lang)


def answer_jobs(pipe: Pipeline) -> list[dict[str, Any]]:
    out = []
    for ex in assemble(pipe).all():
        if not ex.cls.startswith("pos_"):
            continue
        system, user = P.render_messages(pipe.protocol, ex.lang, ex.app_name, ex.context, ex.question)
        out.append(pipe._job("answers", "answers", f"{ex.domain}/{ex.lang}/answers/{ex.id}", pipe.state[ex.domain], ex.lang,
                             {"example_id": ex.id, "system": system, "user": user}))
    return out


def verify_jobs(pipe: Pipeline) -> list[dict[str, Any]]:
    out = []
    model = pipe.teacher["verifier_model"]
    for ex in assemble(pipe).all():
        st = pipe.state[ex.domain]
        if ex.cls.startswith("pos_"):
            answer = pipe.answers.get(ex.id)
            if answer is None or P.parse_answer(pipe.protocol, ex.lang, answer, len(ex.context)).outcome != "answered":
                continue
            sections = "\n\n".join(P.render_section(pipe.protocol, i, s["title"], s["text"]) for i, s in enumerate(ex.context, 1))
            out.append(pipe._job("verify", "verify_answer", f"{ex.domain}/{ex.lang}/verify/{ex.id}", st, ex.lang,
                                 {"example_id": ex.id, "sections": sections, "question": ex.question, "answer": answer}, model=model))
        elif ex.cls == "ref_near_miss":
            top = [s for s, _ in st.index[ex.lang].rank(ex.question, pipe.retrieval)][:4]
            sections = "\n\n".join(P.render_section(pipe.protocol, i, s["title"], s["text"]) for i, s in enumerate(top, 1))
            out.append(pipe._job("verify", "verify_near_miss", f"{ex.domain}/{ex.lang}/verify/{ex.id}", st, ex.lang,
                                 {"example_id": ex.id, "sections": sections, "question": ex.question}, model=model))
    return out
