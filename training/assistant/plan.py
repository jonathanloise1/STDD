"""Deterministic plan of the dataset: the size of every domain, question styles and distance targets (models.md 5.4, 5.6).

Everything here is a pure function of ``dataset.json`` and ``domains.json``: the estimate never depends on the
teacher cache, so its id stays stable while a run is in progress.
"""
from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from training.common.hashing import rng

STYLES = ["symptom", "goal", "colleague", "terse", "rambling", "typo"]
STYLE_DESCRIPTIONS = {
    "symptom": "what goes wrong or looks wrong from the user's side, as a complaint or a puzzled question",
    "goal": "what the user wants to get done, without any screen, button or field words",
    "colleague": "the words a coworker would use, with synonyms and everyday names instead of the manual's terms",
    "terse": "three to six words, like a search query",
    "rambling": "two sentences: first some context about the situation, then the question",
    "typo": "all lower case, no final punctuation, with one or two typing mistakes",
}
MULTI_STYLES = ["terse", "rambling", "colleague"]
NEAR_MISS_STYLES = ["symptom", "goal", "colleague", "terse", "rambling", "typo"]
DISTANCES = ["distant", "mid", "near"]
DISTANCE_INSTRUCTIONS = {
    "distant": "use none of the words of the section: describe the situation in your own words, as someone who has never read the manual would say it",
    "mid": "share a few words with the section (about a quarter of your words) and use everyday synonyms for the rest",
    "near": "use the section's own words: the same screen, button and field names are fine",
}
SECTION_WEIGHTS = {"steps": 4, "problems": 2, "rules": 2, "result": 1, "before": 1, "overview": 1}


@dataclass
class DomainPlan:
    id: str
    n_areas: int
    n_stories: int


def plan_domain(cfg: dict[str, Any], dcfg: dict[str, Any]) -> DomainPlan:
    r = rng(cfg["master_seed"], f"{dcfg['id']}/plan")
    lo_a, hi_a = cfg["per_domain"]["areas"]
    lo_s, hi_s = cfg["per_domain"]["stories"]
    n_areas = r.randint(lo_a, hi_a)
    n_stories = max(r.randint(lo_s, hi_s), 2 * n_areas)
    return DomainPlan(dcfg["id"], n_areas, n_stories)


def style_counts(master_seed: int, story_id: str, lang: str, total: int) -> dict[str, int]:
    """Spread ``total`` questions over the six styles; the remainder goes to styles drawn by seed."""
    base, rem = divmod(total, len(STYLES))
    order = list(STYLES)
    rng(master_seed, f"{story_id}/{lang}/style-remainder").shuffle(order)
    return {s: base + (1 if order.index(s) < rem else 0) for s in STYLES}


def distance_for(master_seed: int, key: str, shares: dict[str, float]) -> str:
    weights = [shares["pos_distant"], shares["pos_mid"], shares["pos_near"]]
    return rng(master_seed, f"{key}/distance").choices(DISTANCES, weights=weights, k=1)[0]


def largest_remainder(total: int, shares: dict[str, float]) -> dict[str, int]:
    """Integer counts that sum to ``total`` and follow ``shares`` (which need not sum to 1)."""
    s = sum(shares.values())
    raw = {k: total * v / s for k, v in shares.items()}
    out = {k: int(v) for k, v in raw.items()}
    left = total - sum(out.values())
    for k in sorted(raw, key=lambda k: (-(raw[k] - out[k]), k))[:left]:
        out[k] += 1
    return out
