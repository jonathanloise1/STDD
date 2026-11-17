"""Teacher job builders and response ingestion for the stages of models.md 5.6.

A ``Pipeline`` holds the state of every domain. For each stage in order, ``jobs(stage)`` derives the job records from
the state, and ``ingest(stage, jobs, texts)`` parses the responses (from the cache, or from the offline teacher in a
dry run) and updates the state. Responses that do not parse are logged as rejections and the item is dropped.
"""
from __future__ import annotations

import json
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Callable, Mapping

from training.assistant import lexical, plan as plan_mod
from training.assistant.assemble import QRec
from training.assistant.domain import (Domain, DomainError, StorySpec, check_page, check_story, parse_domain_response,
                                       parse_json_list, story_questions)
from training.assistant.sections import HEADINGS, PROBLEM_COLUMNS, KEY_ORDER, page_sections, split_questions, strip_front_matter
from training.common.hashing import derive_seed, rng
from training.common.teacher import make_job
from training.common.templates import Template

STAGES = ["domain", "stories", "pages", "questions", "near_miss", "oos", "answers", "verify"]
TEMPLATE_NAMES = {"domain": "domain.v1", "stories": "stories.v1", "pages": "pages.v1", "questions": "questions.v1",
                  "multi": "multi.v1", "near_miss": "near_miss.v1", "oos": "oos.v1", "answers": "answers.v1",
                  "verify_answer": "verify_answer.v1", "verify_near_miss": "verify_near_miss.v1"}
PARAMS = {"domain": {"temperature": 0.7, "max_tokens": 4000}, "stories": {"temperature": 0.7, "max_tokens": 1800},
          "pages": {"temperature": 0.5, "max_tokens": 1500}, "questions": {"temperature": 0.8, "max_tokens": 1500},
          "near_miss": {"temperature": 0.8, "max_tokens": 1500}, "oos": {"temperature": 0.8, "max_tokens": 1500},
          "answers": {"temperature": 0.3, "max_tokens": 400}, "verify": {"temperature": 0.0, "max_tokens": 200}}
LANG_NAMES = {"en": "English", "it": "Italian"}


def load_templates(folder: Path) -> dict[str, Template]:
    return {k: Template.load(folder, v) for k, v in TEMPLATE_NAMES.items()}


@dataclass
class DomainState:
    cfg: dict[str, Any]
    plan: plan_mod.DomainPlan
    domain: Domain | None = None
    dropped: str | None = None
    sections: dict[str, list[dict[str, Any]]] = field(default_factory=dict)
    index: dict[str, lexical.Index] = field(default_factory=dict)
    pool: dict[str, list[QRec]] = field(default_factory=dict)

    @property
    def id(self) -> str:
        return self.cfg["id"]

    @property
    def alive(self) -> bool:
        return self.dropped is None


class Pipeline:
    def __init__(self, cfg: dict[str, Any], domains: list[dict[str, Any]], templates: Mapping[str, Template],
                 retrieval: Mapping[str, Any], splits: set[str] | None = None, protocol: Mapping[str, Any] | None = None):
        from training.assistant import protocol as P
        self.cfg, self.templates, self.retrieval = cfg, templates, retrieval
        self.protocol = protocol or P.load_protocol()
        self.answers: dict[str, str] = {}
        self.verdicts: dict[str, dict[str, Any]] = {}
        self._assembly = None
        self.master = cfg["master_seed"]
        self.langs: list[str] = cfg["languages"]
        self.teacher = cfg["teacher"]
        self.state = {d["id"]: DomainState(d, plan_mod.plan_domain(cfg, d)) for d in domains if splits is None or d["split"] in splits}
        self.rejections: list[dict[str, Any]] = []

    # ---------------------------------------------------------------- helpers
    def reject(self, stage: str, validator: str, key: str, reason: str, domain: str, lang: str | None = None) -> None:
        self.rejections.append({"stage": stage, "validator": validator, "key": key, "domain": domain, "lang": lang, "reason": reason})

    def alive(self) -> list[DomainState]:
        return [s for s in self.state.values() if s.alive and s.domain is not None]

    def _job(self, stage: str, tmpl: str, key: str, st: DomainState, lang: str | None, variables: dict[str, Any], model: str | None = None) -> dict[str, Any]:
        return make_job(stage=stage, key=key, domain=st.id, lang=lang, template=self.templates[tmpl], variables=variables,
                        model=model or self.teacher["model"], params=dict(PARAMS[stage]), master_seed=self.master)

    def assembly_examples(self):
        from training.assistant.dataset import assemble
        return assemble(self).all()

    def render(self, job: dict[str, Any]) -> tuple[str, str]:
        """Render a job record to (system, user) with the template named in the record."""
        tmpl = next(t for t in self.templates.values() if t.name == job["template"])
        return tmpl.render(job["vars"])

    # ---------------------------------------------------------------- jobs
    def jobs(self, stage: str) -> list[dict[str, Any]]:
        return getattr(self, f"_jobs_{stage}")()

    def _jobs_domain(self) -> list[dict[str, Any]]:
        return [self._job("domain", "domain", f"{s.id}/domain", s, None,
                          {"sector": s.cfg["sector"], "app": s.cfg["app"], "users": s.cfg["users"],
                           "n_areas": s.plan.n_areas, "n_stories": s.plan.n_stories}) for s in self.state.values()]

    def _jobs_stories(self) -> list[dict[str, Any]]:
        out = []
        for s in self.alive():
            d = s.domain
            for a in d.areas:
                for st in a.stories:
                    n = st.id.rsplit("-", 1)[1]
                    out.append(self._job("stories", "stories", f"{s.id}/en/stories/{st.id}", s, "en",
                                         {"app_name": d.app_name, "sector": s.cfg["sector"], "area_title": a.title, "story_id": st.id,
                                          "feature_id": f"F-{a.code}-{n}", "title": st.title, "priority": st.priority,
                                          "user_input": st.user_input, "goal": st.goal, "screen": st.screen, "roles": ", ".join(d.roles)}))
        return out

    def _jobs_pages(self) -> list[dict[str, Any]]:
        out = []
        for s in self.alive():
            d = s.domain
            for lang in self.langs:
                kinds: list[tuple[str, str, str]] = [("home", "home", f"Application: {d.app_name}\nSector: {s.cfg['sector']}\nUsers: {s.cfg['users']}\nAreas: " + "; ".join(a.title for a in d.areas))]
                for a in d.areas:
                    flow = " ".join(a.ui_flow)
                    kinds.append(("area", a.folder, f"Area: {a.title}\n{a.overview}\n{flow}"))
                    for st in a.stories:
                        body = (st.md or "").split("\n## Questions users ask")[0]
                        kinds.append(("story", f"{a.folder}/{st.id}", body))
                for kind, page_id, source in kinds:
                    out.append(self._job("pages", "pages", f"{s.id}/{lang}/pages/{page_id}", s, lang, {
                        "language_name": LANG_NAMES[lang], "app_name": d.app_name, "page_kind": kind, "page_id": page_id,
                        "source_text": source, "headings": ", ".join(HEADINGS[lang][k] for k in KEY_ORDER if k != "related"),
                        "problem_columns_row": " | ".join(PROBLEM_COLUMNS[lang])}))
        return out

    def _target_section(self, st: DomainState, lang: str, story: StorySpec, key: str) -> dict[str, Any] | None:
        secs = [x for x in st.sections[lang] if x["page_id"] == f"{story.area}/{story.id}"]
        if not secs:
            return None
        weights = [plan_mod.SECTION_WEIGHTS.get(x["key"], 1) for x in secs]
        return rng(self.master, f"{key}/section").choices(secs, weights=weights, k=1)[0]

    def _jobs_questions(self) -> list[dict[str, Any]]:
        out = []
        per = self.cfg["per_domain"]["in_scope_per_story_lang"]
        for s in self.alive():
            d = s.domain
            for lang in self.langs:
                for story in d.stories():
                    counts = plan_mod.style_counts(self.master, story.id, lang, per)
                    for style in plan_mod.STYLES:
                        if counts[style] == 0:
                            continue
                        key = f"{s.id}/{lang}/questions/{story.id}/{style}"
                        sec = self._target_section(s, lang, story, key)
                        if sec is None:
                            continue
                        distance = plan_mod.distance_for(self.master, key, self.cfg["classes"])
                        forbidden = []
                        if distance == "distant":
                            forbidden = sorted(set(lexical.analyze(sec["title"], lang)) | set(lexical.top_tfidf_stems(sec, s.sections[lang], lang, 12)))
                        out.append(self._job("questions", "questions", key, s, lang, {
                            "language_name": LANG_NAMES[lang], "app_name": d.app_name, "page_title": sec["title"].split(" - ")[0],
                            "section_title": sec["title"], "section_text": sec["text"], "gold_section": sec["id"],
                            "count": counts[style], "style": style, "style_description": plan_mod.STYLE_DESCRIPTIONS[style],
                            "distance": distance, "distance_instruction": plan_mod.DISTANCE_INSTRUCTIONS[distance],
                            "forbidden": ", ".join(forbidden) if forbidden else "(none)",
                            "label_rule": "You may quote button, field and screen names in double quotes." if distance == "near" else "Do not quote any button, field or screen name."}))
                    secs = [x for x in s.sections[lang] if x["page_id"] == f"{story.area}/{story.id}"]
                    if len(secs) >= 2:
                        key = f"{s.id}/{lang}/questions/{story.id}/multi"
                        r = rng(self.master, f"{key}/pair")
                        steps = [x for x in secs if x["key"] == "steps"]
                        a = steps[0] if steps else r.choice(secs)
                        b = r.choice([x for x in secs if x["id"] != a["id"]])
                        pair = sorted([a, b], key=lambda x: x["id"])
                        out.append(self._job("questions", "multi", key, s, lang, {
                            "language_name": LANG_NAMES[lang], "app_name": d.app_name, "count": 2,
                            "title_a": pair[0]["title"], "text_a": pair[0]["text"], "gold_a": pair[0]["id"],
                            "title_b": pair[1]["title"], "text_b": pair[1]["text"], "gold_b": pair[1]["id"]}))
        return out

    def _jobs_near_miss(self) -> list[dict[str, Any]]:
        out = []
        n = self.cfg["per_domain"]["near_miss_per_area_lang"]
        for s in self.alive():
            for lang in self.langs:
                for a in s.domain.areas:
                    secs = [x for x in s.sections[lang] if x["area"] == a.folder and x["story"]]
                    if not secs:
                        continue
                    key = f"{s.id}/{lang}/near_miss/{a.folder}"
                    r = rng(self.master, f"{key}/sections")
                    chosen = r.sample(secs, min(3, len(secs)))
                    styles = list(plan_mod.NEAR_MISS_STYLES)
                    r.shuffle(styles)
                    out.append(self._job("near_miss", "near_miss", key, s, lang, {
                        "language_name": LANG_NAMES[lang], "app_name": s.domain.app_name, "area_title": a.title, "count": n,
                        "sections": "\n\n".join(f"[{x['title']}]\n{x['text']}" for x in chosen),
                        "styles": ", ".join(styles[:n]), "style_list": styles[:n]}))
        return out

    def _jobs_oos(self) -> list[dict[str, Any]]:
        out = []
        n = self.cfg["per_domain"]["oos_per_domain_lang"]
        for s in self.alive():
            for lang in self.langs:
                key = f"{s.id}/{lang}/oos"
                styles = [plan_mod.STYLES[i % 6] for i in range(n)]
                rng(self.master, f"{key}/styles").shuffle(styles)
                out.append(self._job("oos", "oos", key, s, lang, {
                    "language_name": LANG_NAMES[lang], "app_name": s.domain.app_name, "sector": s.cfg["sector"], "count": n,
                    "styles": ", ".join(sorted(set(styles))), "style_list": styles}))
        return out

    def _jobs_answers(self) -> list[dict[str, Any]]:
        from training.assistant.dataset import answer_jobs
        return answer_jobs(self)

    def _jobs_verify(self) -> list[dict[str, Any]]:
        from training.assistant.dataset import verify_jobs
        return verify_jobs(self)

    # ---------------------------------------------------------------- ingestion
    def ingest(self, stage: str, jobs: list[dict[str, Any]], texts: Mapping[str, str | None]) -> None:
        getattr(self, f"_ingest_{stage}", lambda j, t: None)(jobs, texts)

    def _ingest_domain(self, jobs: list[dict[str, Any]], texts: Mapping[str, str | None]) -> None:
        for job in jobs:
            st = self.state[job["domain"]]
            text = texts.get(job["key"])
            if text is None:
                st.dropped = "domain response missing"
                continue
            try:
                st.domain = parse_domain_response(text, st.cfg, st.plan.n_areas, st.plan.n_stories)
            except DomainError as exc:
                st.dropped = f"domain: {exc}"
                self.reject("domain", "format", job["key"], str(exc), st.id)

    def _ingest_stories(self, jobs: list[dict[str, Any]], texts: Mapping[str, str | None]) -> None:
        for job in jobs:
            st = self.state[job["domain"]]
            sid = job["key"].rsplit("/", 1)[1]
            story = next(x for x in st.domain.stories() if x.id == sid)
            text = texts.get(job["key"])
            if text is None:
                continue
            md = text.strip().replace("\r\n", "\n") + "\n"
            problems = check_story(md, sid)
            if problems:
                self.reject("stories", "V02", job["key"], "; ".join(problems), st.id, "en")
                continue
            story.md = md
            story.questions = story_questions(md)
        for st in self.alive():
            for a in st.domain.areas:
                a.stories = [x for x in a.stories if x.md is not None]
            st.domain.areas = [a for a in st.domain.areas if a.stories]
            if not st.domain.areas:
                st.dropped = "no valid story"

    def _ingest_pages(self, jobs: list[dict[str, Any]], texts: Mapping[str, str | None]) -> None:
        for job in jobs:
            st = self.state[job["domain"]]
            lang = job["lang"]
            page_id = job["vars"]["page_id"]
            text = texts.get(job["key"])
            if text is None:
                continue
            kind = job["vars"]["page_kind"]
            body = text.strip().replace("\r\n", "\n") + "\n"
            problems = check_page(body, lang, kind)
            if problems:
                self.reject("pages", "V03", job["key"], "; ".join(problems), st.id, lang)
                continue
            st.domain.pages.setdefault(lang, {})[page_id] = body
        for st in self.alive():
            d = st.domain
            for a in d.areas:                                   # a story needs a valid page in every language
                a.stories = [x for x in a.stories if all(f"{a.folder}/{x.id}" in d.pages.get(l, {}) for l in self.langs)]
            d.areas = [a for a in d.areas if a.stories]
            if not d.areas:
                st.dropped = "no story with pages in every language"
                continue
            self._build_sections(st)

    def _build_sections(self, st: DomainState) -> None:
        d = st.domain
        for lang in self.langs:
            secs: list[dict[str, Any]] = []
            for page_id, md in sorted(d.pages.get(lang, {}).items()):
                if page_id == "home":
                    secs += page_sections(page_id, md, lang, area="home", story=None)
                elif "/" in page_id:
                    folder, sid = page_id.split("/", 1)
                    story = next((x for x in d.stories() if x.id == sid), None)
                    if story is None:
                        continue
                    ss = page_sections(page_id, md, lang, area=folder, story=sid)
                    indexed = split_questions(sid, lang, story.questions.get(lang, []))[1]
                    for x in ss:
                        x["questions"] = indexed
                    secs += ss
                else:
                    if any(a.folder == page_id for a in d.areas):
                        secs += page_sections(page_id, md, lang, area=page_id, story=None)
            st.sections[lang] = secs
            st.index[lang] = lexical.Index(secs, lang)

    def _add_questions(self, st: DomainState, job: dict[str, Any], texts: Mapping[str, str | None], *, kind: str,
                       styles: list[str], gold: list[str], story: str | None, area: str | None) -> None:
        lang = job["lang"]
        text = texts.get(job["key"])
        if text is None:
            return
        try:
            qs = parse_json_list(text, job["vars"]["count"])
        except DomainError as exc:
            self.reject(job["stage"], "format", job["key"], str(exc), st.id, lang)
            return
        if kind == "oos":
            st.domain.bench_oos[lang] = list(qs)
        for i, q in enumerate(qs):
            st.pool.setdefault(lang, []).append(QRec(job["key"], i, st.id, lang, kind, q, styles[i % len(styles)], story, area, list(gold)))

    def _ingest_questions(self, jobs: list[dict[str, Any]], texts: Mapping[str, str | None]) -> None:
        for job in jobs:
            st = self.state[job["domain"]]
            v = job["vars"]
            story = job["key"].split("/")[3]
            area = next((a.folder for a in st.domain.areas if any(x.id == story for x in a.stories)), None)
            if job["template"] == "multi.v1":
                self._add_questions(st, job, texts, kind="multi", styles=plan_mod.MULTI_STYLES, gold=[v["gold_a"], v["gold_b"]], story=story, area=area)
            else:
                self._add_questions(st, job, texts, kind="story", styles=[v["style"]], gold=[v["gold_section"]], story=story, area=area)

    def _ingest_near_miss(self, jobs: list[dict[str, Any]], texts: Mapping[str, str | None]) -> None:
        for job in jobs:
            st = self.state[job["domain"]]
            self._add_questions(st, job, texts, kind="near_miss", styles=job["vars"]["style_list"], gold=[], story=None, area=job["key"].rsplit("/", 1)[1])

    def _ingest_oos(self, jobs: list[dict[str, Any]], texts: Mapping[str, str | None]) -> None:
        for job in jobs:
            st = self.state[job["domain"]]
            self._add_questions(st, job, texts, kind="oos", styles=job["vars"]["style_list"], gold=[], story=None, area=None)

    def _ingest_answers(self, jobs: list[dict[str, Any]], texts: Mapping[str, str | None]) -> None:
        for job in jobs:
            text = texts.get(job["key"])
            if text is not None:
                self.answers[job["vars"]["example_id"]] = text.strip().replace("\r\n", "\n")

    def _ingest_verify(self, jobs: list[dict[str, Any]], texts: Mapping[str, str | None]) -> None:
        for job in jobs:
            text = texts.get(job["key"])
            if text is None:
                continue
            try:
                data = json.loads(text.strip().strip("`").removeprefix("json").strip())
                if not isinstance(data, dict):
                    raise ValueError
            except ValueError:
                self.reject("verify", "format", job["key"], "verifier answer is not a JSON object", job["domain"], job["lang"])
                continue
            self.verdicts[job["vars"]["example_id"]] = data
