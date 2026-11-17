"""Python port of the masking policy (operations.md section 8), used on the training side for three things only:

- ``leak_findings``: the validator that rejects an example in which an unmasked value is visible (11.6, validators);
- ``mask_signal``: masking of the triggering records E1 with the ``signal`` section (E1 is a signal, not a tool result);
- ``FakeToolRunner`` (``tools.py``): the test double of the monitor's executor.

The real masking is the monitor's (C#); this port exists so that a leak in generated data cannot go unnoticed. Patterns are
.NET regular expressions in the policy; the ones shipped are also valid Python ``re`` syntax.
"""
from __future__ import annotations

import hashlib
import re
from typing import Any, Mapping

CODE_RE = re.compile(r"^[A-Za-z0-9_.:/{}+<>\[\]-]{1,200}$")
GUID_RE = re.compile(r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")
ISO_RE = re.compile(r"^\d{4}-\d{2}-\d{2}(?:[T ]\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:\d{2})?)?$")


class Masker:
    def __init__(self, policy: Mapping[str, Any]):
        self.policy = policy
        self.protected = [re.compile(p["regex"]) for p in policy["protected"]] + [re.compile(r"\bh:[0-9a-f]{12}\b")]   # hash class output
        self.patterns = [(p["name"], re.compile(p["regex"], re.I if p.get("ignore_case") else 0), p["replacement"]) for p in policy["patterns"]]
        self.max_chars = policy["free_text_max_chars"]

    def _spans(self, text: str) -> list[tuple[int, int]]:
        spans: list[tuple[int, int]] = []
        for rx in self.protected:
            spans += [m.span() for m in rx.finditer(text)]
        return sorted(spans)

    def _outside(self, text: str) -> list[tuple[int, int, bool]]:
        """Pieces of text as (start, end, protected)."""
        pieces, pos = [], 0
        for a, b in self._spans(text):
            if a < pos:
                continue
            if a > pos:
                pieces.append((pos, a, False))
            pieces.append((a, b, True))
            pos = b
        if pos < len(text):
            pieces.append((pos, len(text), False))
        return pieces

    def free_text(self, text: str) -> str:
        out = []
        for a, b, prot in self._outside(text):
            piece = text[a:b]
            if not prot:
                for _n, rx, repl in self.patterns:
                    piece = rx.sub(lambda m, r=repl: _expand(r, m), piece)
            out.append(piece)
        return "".join(out)[: self.max_chars]

    def leaks(self, text: str) -> list[str]:
        """Names of the patterns that still match outside protected spans (an empty list means nothing leaks)."""
        found = []
        for a, b, prot in self._outside(text):
            if prot:
                continue
            piece = text[a:b]
            for name, rx, _r in self.patterns:
                if rx.search(piece):
                    found.append(name)
        return sorted(set(found))

    def apply_class(self, cls: str, value: Any) -> Any:
        s = value if isinstance(value, str) else ("" if value is None else str(value))
        if cls == "id":
            return value if (GUID_RE.match(s) or re.fullmatch(r"\d+", s) or re.fullmatch(r"[0-9a-f]{32}|[0-9a-f]{16}", s)) else "[masked]"
        if cls == "code":
            return value if CODE_RE.match(s) else "[masked]"
        if cls == "number":
            try:
                float(s)
                return value
            except ValueError:
                return "[masked]"
        if cls == "boolean":
            return value if isinstance(value, bool) or s.lower() in ("true", "false") else "[masked]"
        if cls == "timestamp":
            return value if ISO_RE.match(s) else "[masked]"
        if cls == "free_text":
            return self.free_text(s)
        if cls == "hash":
            return "h:" + hashlib.sha256(s.strip().lower().encode("utf-8")).hexdigest()[:12]
        if cls == "pack":
            return value
        return "[masked]"

    def mask_fields(self, section: Mapping[str, str], row: Mapping[str, Any]) -> tuple[dict[str, Any], int]:
        """Keep only the fields the section lists (case-insensitive), masked by class; the second value counts the dropped ones."""
        lower = {k.lower(): (k, c) for k, c in section.items()}
        out, dropped = {}, 0
        for k, v in row.items():
            hit = lower.get(k.lower())
            if hit is None:
                dropped += 1
            else:
                out[hit[0]] = self.apply_class(hit[1], v)
        return out, dropped

    def mask_signal(self, record: Mapping[str, Any]) -> dict[str, Any]:
        """The ``signal`` section on a log record of format 3.4 (fields, then properties by name or ``*Suffix`` glob)."""
        fields, props = self.policy["signal"]["fields"], self.policy["signal"]["properties"]
        out: dict[str, Any] = {}
        for k, v in record.items():
            if k == "Properties":
                out[k] = {pk: self.apply_class(_prop_class(props, pk), pv) for pk, pv in v.items()}
            elif k == "Exception":
                out[k] = {"Type": self.apply_class("code", v.get("Type")), "Message": self.free_text(str(v.get("Message", ""))),
                          "Frames": [self.apply_class("code", f) for f in v.get("Frames", [])[:3]]}
            elif k in fields:
                out[k] = self.apply_class(fields[k], v)
        return out


def _prop_class(props: Mapping[str, str], name: str) -> str:
    if name in props:
        return props[name]
    for pat, cls in props.items():
        if pat.startswith("*") and name.endswith(pat[1:]):
            return cls
    return "free_text"


def _expand(repl: str, m: re.Match[str]) -> str:
    return re.sub(r"\$(\d)", lambda g: m.group(int(g.group(1))), repl)
