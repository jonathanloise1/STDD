"""Offline teacher: deterministic, format-correct responses for every stage, with no network and no cost.

It is used (1) by ``generate --estimate`` and ``--dry-run`` to stand in for upstream outputs that do not exist yet, so
that dependent jobs can be rendered and counted; and (2) by the tests as a fake ``TeacherClient``. The texts are
synthetic placeholders, not usable training data.
"""
from __future__ import annotations

import json
import re
from typing import Any, Callable

from training.assistant import lexical, protocol as P
from training.assistant.domain import ROLES
from training.assistant.sections import HEADINGS, PROBLEM_COLUMNS
from training.common.hashing import rng
from training.common.teacher import Completion

AREA_NOUNS = [("orders", "ordini"), ("visits", "visite"), ("members", "soci"), ("reports", "rapporti"), ("payments", "pagamenti"),
              ("schedules", "turni"), ("contacts", "contatti"), ("notices", "avvisi"), ("stock", "scorte"), ("permits", "permessi"),
              ("bookings", "prenotazioni"), ("samples", "campioni")]
VERBS = [("Create", "Crea"), ("Edit", "Modifica"), ("Remove", "Rimuovi"), ("Find", "Trova"), ("Export", "Esporta"),
         ("Review", "Controlla"), ("Approve", "Approva"), ("Cancel", "Annulla"), ("Assign", "Assegna"), ("Archive", "Archivia")]
SYL = ["za", "vel", "mo", "rin", "tu", "kol", "pa", "dex", "li", "sor", "ban", "qui", "no", "fre", "gla", "wim"]
FILLER = {"en": ["thing", "stuck", "wrong", "weird", "again", "somebody", "whatever", "ages", "broken", "happening", "sorted", "fine", "please", "today", "anyone", "lately", "yesterday", "morning", "colleague", "customer"],
          "it": ["cosa", "bloccato", "strano", "ancora", "qualcuno", "comunque", "sempre", "rotto", "succede", "sistemato", "oggi", "ieri", "mattina", "collega", "cliente", "adesso", "subito", "problema", "aiuto", "presto"]}
FUNC = {"en": ["how", "do", "i", "the", "my", "to", "is", "of", "in", "when"], "it": ["come", "posso", "il", "la", "mio", "di", "per", "con", "quando", "non"]}
OOS_BANK = {
    "en": ["What is the capital of Australia?", "How do I bake sourdough bread at home?", "Who won the football world cup in 2010?", "How can I reset my phone to factory settings?",
           "What is the boiling point of water on a mountain?", "Can you recommend a good novel for the holidays?", "How many kilometres is it from Rome to Paris?", "What is a good name for a puppy?",
           "How do I change a flat tyre on my bicycle?", "Explain how photosynthesis works.", "What is the weather going to be like next weekend?", "How do I write a cover letter for a new job?",
           "Which planet is the largest in the solar system?", "How can I learn to play the guitar quickly?", "What is the best way to remove a coffee stain from a shirt?", "How long should I boil an egg?",
           "Translate good morning into Japanese.", "What year did the Berlin wall fall?", "How do I install a spreadsheet program on my laptop?", "Who painted the ceiling of the Sistine Chapel?",
           "How much sleep does an adult need?", "What is the tallest mountain in Europe?", "Can you tell me a joke about cats?", "How do I fix a dripping tap in my kitchen?"],
    "it": ["Qual e la capitale dell'Australia?", "Come si prepara il pane con il lievito madre a casa?", "Chi ha vinto i mondiali di calcio nel 2010?", "Come posso ripristinare il mio telefono alle impostazioni di fabbrica?",
           "A che temperatura bolle l'acqua in montagna?", "Mi consigli un bel romanzo per le vacanze?", "Quanti chilometri ci sono da Roma a Parigi?", "Che nome posso dare al mio cucciolo?",
           "Come si cambia una gomma bucata della bicicletta?", "Spiegami come funziona la fotosintesi.", "Che tempo farà il prossimo fine settimana?", "Come si scrive una lettera di presentazione per un nuovo lavoro?",
           "Qual è il pianeta più grande del sistema solare?", "Come posso imparare a suonare la chitarra in fretta?", "Come si toglie una macchia di caffè da una camicia?", "Quanto deve bollire un uovo?",
           "Traduci buongiorno in giapponese.", "In che anno è caduto il muro di Berlino?", "Come installo un foglio di calcolo sul mio portatile?", "Chi ha dipinto il soffitto della Cappella Sistina?",
           "Quante ore deve dormire un adulto?", "Qual è la montagna più alta d'Europa?", "Mi racconti una barzelletta sui gatti?", "Come si ripara un rubinetto che gocciola in cucina?"],
}
NEAR_TAILS = {"en": ["maximum number allowed", "what happens after a week", "can I undo it later", "is there a limit", "does it send a message", "can I do it in bulk"],
              "it": ["numero massimo consentito", "cosa succede dopo una settimana", "posso annullarlo dopo", "c'è un limite", "manda un messaggio", "posso farlo in blocco"]}


def pseudo(key: str, n: int = 3) -> str:
    r = rng(1, f"pseudo/{key}")
    return "".join(r.choice(SYL) for _ in range(n))


class FakeTeacher:
    """``complete`` answers any stage; ``respond(job)`` returns the text only. ``pipeline`` gives the answers stage access
    to the gold positions of the examples (a real teacher never sees them)."""

    def __init__(self, pipeline: Any = None, *, overrides: Callable[[dict[str, Any]], str | None] | None = None):
        self.pipeline = pipeline
        self.overrides = overrides
        self.calls: list[str] = []

    wants_job = True    # the JobRunner passes the job record along with the prompt

    def complete(self, *, model: str, system: str, user: str, params: dict[str, Any], seed: int, job: dict[str, Any]) -> Completion:
        text = self.respond(job)
        return Completion(text, round((len(system) + len(user)) / 3.5), round(len(text) / 3.5))

    def respond(self, job: dict[str, Any]) -> str:
        self.calls.append(job["key"])
        if self.overrides:
            forced = self.overrides(job)
            if forced is not None:
                return forced
        return getattr(self, f"_{job['stage']}")(job)

    # ---------------------------------------------------------------- stages
    def _domain(self, job: dict[str, Any]) -> str:
        v = job["vars"]
        r = rng(7, job["key"])
        nouns = r.sample(AREA_NOUNS, v["n_areas"])
        base, rem = divmod(v["n_stories"], v["n_areas"])
        areas = []
        for i, (en, _it) in enumerate(nouns):
            folder = en
            code = re.sub(r"[^A-Z]", "", (en[:4]).upper()) or "AREA"
            n = base + (1 if i < rem else 0)
            verbs = r.sample(VERBS, n) if n <= len(VERBS) else [VERBS[j % len(VERBS)] for j in range(n)]
            stories = [{"title": f"{vb[0]} {en[:-1] if en.endswith('s') else en} {pseudo(job['domain'] + folder + str(j), 2)}",
                        "priority": ["High", "Medium", "Low", "Critical"][j % 4], "user_input": "Yes" if j % 2 == 0 else "No",
                        "goal": f"{vb[0].lower()} a {en[:-1] if en.endswith('s') else en} entry", "screen": f"{en.title()}", "route": f"/{folder}"}
                       for j, vb in enumerate(verbs)]
            areas.append({"folder": folder, "code": code, "title": en.title(), "overview": f"This area lets users work with {en}. It is used every day by the team.",
                          "business_rules": [f"Only an Admin can change {en} after approval."], "ui_flow": [f"{en.title()}: the user opens the list and picks an entry."], "stories": stories})
        return json.dumps({"app_name": f"{pseudo(job['domain'], 2).title()}Desk", "roles": ["Admin", "Editor", "Viewer"], "areas": areas})

    def _stories(self, job: dict[str, Any]) -> str:
        v = job["vars"]
        p = pseudo(v["story_id"], 3)
        verb = v["title"].split()[0]
        qs_en = [f"how do I {verb.lower()} the {p} entry", f"{p} is stuck again", f"where is the {p} entry I made yesterday", f"can somebody help with the {p} thing"]
        qs_it = [f"come faccio a gestire il {p} di ieri", f"il {p} è bloccato ancora", f"dove trovo il {p} che ho fatto", f"qualcuno può aiutarmi con il {p}"]
        return (f"# {v['story_id']}: {v['title']}\n\n| Field | Value |\n|-------|-------|\n| Priority | {v['priority']} |\n| Feature | {v['feature_id']} |\n| User input | {v['user_input']} |\n\n"
                f"## Story\n\n**As a** team member\n**I want** to {v['goal'].rstrip('.')}\n**So that** my work stays organised\n\n## Acceptance Criteria\n\n"
                f"1. The user sees the \"{verb} {p}\" button.\n2. The entry appears in the list.\n\n## Access Control\n\n| Role | Allowed |\n|------|---------|\n| Admin | Yes |\n| Viewer | No |\n\n"
                f"## Business Rules\n\nNone.\n\n## Error States\n\n| Situation | Message | What the user can do |\n|-----------|---------|----------------------|\n| Access was removed | \"You no longer have access to this {p}.\" | Ask an Admin to add you back |\n\n"
                f"## Non-Goals\n\n- Bulk changes are not supported.\n\n## Questions users ask\n\n### en\n\n" + "\n".join(f"{i}. {q}" for i, q in enumerate(qs_en, 1)) +
                "\n\n### it\n\n" + "\n".join(f"{i}. {q}" for i, q in enumerate(qs_it, 1)) + "\n")

    def _pages(self, job: dict[str, Any]) -> str:
        v, lang = job["vars"], job["lang"]
        if v["page_kind"] == "home":
            return f"# {v['app_name']}\n\n" + ("Use this manual to learn the application." if lang == "en" else "Usa questo manuale per imparare l'applicazione.") + "\n"
        if v["page_kind"] == "area":
            title = next((l[6:] for l in v["source_text"].split("\n") if l.startswith("Area: ")), v["page_id"])
            return f"# {title}\n\n" + ("This area lets you work with your entries." if lang == "en" else "Questa area ti permette di lavorare con le tue voci.") + "\n"
        m = re.match(r"# (US-[A-Z]+-\d+): (.+)", v["source_text"])
        sid, title = m.group(1), m.group(2)
        p = pseudo(sid, 3)
        verb_en = title.split()[0]
        verb = dict(VERBS).get(verb_en, verb_en) if lang == "it" else verb_en
        h = HEADINGS[lang]
        c1, c2 = PROBLEM_COLUMNS[lang]
        if lang == "en":
            return (f"# {title}\n\nUse this page to work with the {p} entry so that your work stays organised.\n\n## {h['steps']}\n\n1. Open the Entries page.\n2. Click \"{verb} {p}\".\n3. In the \"Name {p}\" field, type the name.\n4. Click \"Save {p}\".\n\n"
                    f"## {h['result']}\n\n- The {p} entry appears in the list.\n\n## {h['rules']}\n\n- Only an Admin can {verb.lower()} a {p} entry.\n\n## {h['problems']}\n\n| {c1} | {c2} |\n|---|---|\n| \"You no longer have access to this {p}.\" | Ask an Admin to add you back. |\n")
        return (f"# {verb} {p}\n\nUsa questa pagina per lavorare con la voce {p} in modo che il lavoro resti ordinato.\n\n## {h['steps']}\n\n1. Apri la pagina Voci.\n2. Fai clic su \"{verb} {p}\".\n3. Nel campo \"Nome {p}\" scrivi il nome.\n4. Fai clic su \"Salva {p}\".\n\n"
                f"## {h['result']}\n\n- La voce {p} compare nell'elenco.\n\n## {h['rules']}\n\n- Solo un Admin può usare {verb.lower()} sulla voce {p}.\n\n## {h['problems']}\n\n| {c1} | {c2} |\n|---|---|\n| \"You no longer have access to this {p}.\" | Chiedi a un Admin di aggiungerti di nuovo. |\n")

    def _words(self, text: str, lang: str, exclude: set[str]) -> list[str]:
        out, seen = [], set()
        for t in lexical.tokens(text, lang):
            if len(t) > 3 and t not in seen and lexical._stem_cached(t, lang) not in exclude and not t.isdigit():
                seen.add(t)
                out.append(t)
        return out

    def _question(self, r, lang: str, sec_words: list[str], frac: float, tag: str, total: int = 8) -> str:
        n_sec = min(len(sec_words), round(frac * total))
        picks = r.sample(sec_words, n_sec) if n_sec else []
        fill = [w for w in FILLER[lang] if w not in picks]
        words = picks + r.sample(fill, total - n_sec - 1) + [pseudo(tag, 2)]
        r.shuffle(words)
        func = FUNC[lang]
        return " ".join(func[:2] + words[:4] + func[3:4] + words[4:])

    def _questions(self, job: dict[str, Any]) -> str:
        v, lang = job["vars"], job["lang"]
        r = rng(11, job["key"])
        if "gold_a" in v:
            wa, wb = self._words(v["text_a"], lang, set()), self._words(v["text_b"], lang, set())
            qs = []
            for i in range(v["count"]):
                words = r.sample(wa, min(2, len(wa))) + r.sample(wb, min(2, len(wb)))
                qs.append(" ".join(FUNC[lang][:2] + words + [pseudo(f"{job['key']}{i}", 2)] + r.sample(FILLER[lang], 3)))
            return json.dumps(qs)
        forbidden = {w.strip() for w in v["forbidden"].split(",")}
        sec_words = self._words(v["section_text"] + " " + v["section_title"], lang, forbidden if v["distance"] == "distant" else set())
        frac = {"distant": 0.0, "mid": 0.30, "near": 0.70}[v["distance"]]
        qs = [self._question(r, lang, sec_words, frac, f"{job['key']}{i}") for i in range(v["count"])]
        if v["style"] == "terse":
            qs = [" ".join(q.split()[:6]) for q in qs]
        if v["style"] == "typo":
            qs = [q.lower().rstrip("?") for q in qs]
        return json.dumps(qs)

    def _near_miss(self, job: dict[str, Any]) -> str:
        v, lang = job["vars"], job["lang"]
        r = rng(13, job["key"])
        words = self._words(v["sections"], lang, set())
        qs = []
        for i in range(v["count"]):
            tail = NEAR_TAILS[lang][i % len(NEAR_TAILS[lang])]
            qs.append(" ".join(FUNC[lang][:2] + r.sample(words, min(4, len(words))) + [tail, pseudo(f"{job['key']}{i}", 2)]))
        return json.dumps(qs)

    def _oos(self, job: dict[str, Any]) -> str:
        v, lang = job["vars"], job["lang"]
        bank = OOS_BANK[lang]
        r = rng(17, job["key"])
        picks = r.sample(bank, min(v["count"], len(bank)))
        picks += [f"{bank[i % len(bank)].rstrip('?.')} {pseudo(job['key'] + str(i), 2)}?" for i in range(v["count"] - len(picks))]
        return json.dumps(picks)

    def _answers(self, job: dict[str, Any]) -> str:
        eid = job["vars"]["example_id"]
        ex = next((e for e in self.pipeline.assembly_examples() if e.id == eid), None)
        if ex is None:
            gold = [1]
            ctx_sections = []
        else:
            gold, ctx_sections = ex.gold, ex.context
        parts = []
        for g in gold:
            text = ctx_sections[g - 1]["text"] if ctx_sections else "Do it on the page."
            parts.append(text.split("\n")[0][:250])
        body = "\n".join(parts)
        return body + "\nSource: " + ", ".join(f"[{g}]" for g in sorted(gold))

    def _verify(self, job: dict[str, Any]) -> str:
        if job["template"] == "verify_near_miss.v1":
            return json.dumps({"answered": False})
        return json.dumps({"supported": True, "answers_question": True})
