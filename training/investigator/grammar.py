"""GBNF grammar of an investigator turn (operations.md 11.3): ``<call>{...}</call>`` with ``arguments`` constrained to the tool's
parameter schema, or ``<report>{...}</report>`` constrained to ``#/$defs/investigation``, or only the report.

The converter covers the schema subset of 7.1 and writes compact JSON (no whitespace), as the protocol renders it. ``pattern``,
``minLength``, ``maxLength``, ``minimum``, ``maximum`` and ``maxItems`` are not expressed in the grammar (the host's validation after
the report enforces them). The text is passed to llama.cpp's ``grammar`` request field; it has been checked here only for structure
(every rule used is defined), not run against llama.cpp.
"""
from __future__ import annotations

import json
from typing import Any, Mapping, Sequence

PRIMITIVES = {
    "string": r'string ::= "\"" ([^"\\\x7F\x00-\x1F] | "\\" (["\\/bfnrt] | "u" [0-9a-fA-F]{4}))* "\""',
    "integer": r'integer ::= "-"? ("0" | [1-9] [0-9]*)',
    "number": r'number ::= "-"? ("0" | [1-9] [0-9]*) ("." [0-9]+)?',
    "boolean": r'boolean ::= "true" | "false"',
    "null": r'null ::= "null"',
}


def _lit(text: str) -> str:
    return json.dumps(text)


class Builder:
    def __init__(self, defs: Mapping[str, Any] | None = None, entities: Sequence[str] = ()):
        self.rules: dict[str, str] = {}
        self.defs, self.entities = dict(defs or {}), list(entities)
        self.n = 0

    def _name(self, hint: str) -> str:
        self.n += 1
        return f"{hint}-{self.n}"

    def value(self, schema: Mapping[str, Any], hint: str = "v") -> str:
        """The name of a rule matching ``schema``."""
        if "$ref" in schema:
            key = schema["$ref"].rsplit("/", 1)[1]
            name = "def-" + key.replace("_", "-")   # GBNF rule names allow letters, digits and dashes
            if name not in self.rules:
                self.rules[name] = ""                   # reserve: recursion guard
                self.rules[name] = self._body(self.defs[key], name)
            return name
        name = self._name(hint)
        self.rules[name] = self._body(schema, name)
        return name

    def _body(self, schema: Mapping[str, Any], name: str) -> str:
        if "anyOf" in schema:
            return " | ".join(self.value(s, "alt") for s in schema["anyOf"])
        if "enum" in schema:
            return " | ".join(_lit(json.dumps(v, ensure_ascii=False)) for v in schema["enum"])
        types = schema.get("type")
        if isinstance(types, list):
            return " | ".join(self.value({**schema, "type": t}, "t") for t in types)
        if types == "object":
            return self._object(schema)
        if types == "array":
            item = self.value(schema.get("items", {"type": "string"}), "item")
            return f'"[" ({item} ("," {item})*)? "]"'
        if schema.get("x-enum-from") == "entities" and self.entities:
            return " | ".join(_lit(json.dumps(e)) for e in self.entities)
        if types in PRIMITIVES:
            self.rules.setdefault(types, PRIMITIVES[types].split(" ::= ", 1)[1])
            return types
        return "string"

    def _object(self, schema: Mapping[str, Any]) -> str:
        props = schema.get("properties", {})
        required = [k for k in props if k in schema.get("required", [])]
        optional = [k for k in props if k not in required]
        if not props:
            return '"{" "}"'
        parts = []
        for i, k in enumerate(required):
            sub = self.value(props[k], k.replace("_", "-"))
            parts.append(("\",\" " if i else "") + f'{_lit(json.dumps(k) + ":")} {sub}')
        out = '"{" ' + " ".join(parts)
        for k in optional:
            sub = self.value(props[k], k.replace("_", "-"))
            out += f' ("," {_lit(json.dumps(k) + ":")} {sub})?' if required else f' ({_lit(json.dumps(k) + ":")} {sub})?'
        return out + ' "}"'

    def text(self, root: str) -> str:
        lines = [f"root ::= {root}"] + [f"{n} ::= {b}" for n, b in self.rules.items()]
        for p in ("string", "integer", "number", "boolean", "null"):
            if p in self.rules:
                lines[lines.index(f"{p} ::= {self.rules[p]}")] = PRIMITIVES[p]
        return "\n".join(lines) + "\n"


def turn_grammar(catalog: Mapping[str, Any], report_schema: Mapping[str, Any], entities: Sequence[str], only_report: bool = False) -> str:
    """The grammar of one assistant turn: ``call | report`` or only ``report`` (when no calls are left or a stop rule applies)."""
    b = Builder(report_schema.get("$defs", {}), entities)
    rep = b.value({k: v for k, v in report_schema.items() if k != "$defs"}, "report")
    report_rule = f'"<report>" {rep} "</report>"'
    if only_report:
        return b.text(report_rule)
    calls = []
    for t in catalog["tools"]:
        args = b.value(t["parameters"], "args-" + t["name"].replace("_", "-"))
        calls.append(f'"<call>{{\\"tool\\":\\"{t["name"]}\\",\\"arguments\\":" {args} "}}</call>"')
    return b.text(" | ".join(calls + [report_rule]))


def undefined_rules(grammar: str) -> list[str]:
    """Names used on a right-hand side that no rule defines (a structural check; empty means well-formed)."""
    import re
    defined = set(re.findall(r"^([A-Za-z][\w-]*) ::=", grammar, re.M))
    used: set[str] = set()
    for line in grammar.splitlines():
        rhs = line.split(" ::= ", 1)[1] if " ::= " in line else ""
        rhs = re.sub(r'"(?:\\.|[^"\\])*"', "", rhs)           # drop literals
        rhs = re.sub(r"\[(?:\\.|[^\]\\])*\]", "", rhs)         # drop character classes
        used |= set(re.findall(r"[A-Za-z][\w-]*", rhs))
    return sorted(used - defined)
