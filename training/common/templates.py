"""Teacher prompt templates: one file per stage, versioned in the name (``questions.v1.txt``).

A template is plain text with ``{{name}}`` placeholders. An optional line ``=== USER ===`` splits it into a system
part (before) and a user part (after). Substitution is a single pass; substituted values are not scanned again.
"""
from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping

from .hashing import sha256_bytes

USER_MARKER = "=== USER ==="
_PLACEHOLDER = re.compile(r"\{\{([a-z_][a-z0-9_]*)\}\}")


@dataclass(frozen=True)
class Template:
    name: str          # "questions.v1"
    text: str
    sha256: str

    @classmethod
    def load(cls, folder: Path, name: str) -> "Template":
        path = Path(folder) / f"{name}.txt"
        data = path.read_bytes()
        return cls(name=name, text=data.decode("utf-8").replace("\r\n", "\n"), sha256=sha256_bytes(data))

    def render(self, variables: Mapping[str, Any]) -> tuple[str, str]:
        """Return ``(system, user)``; system is empty without a marker. Unknown placeholders raise ``KeyError``."""
        system, user = "", self.text
        marker = f"\n{USER_MARKER}\n"
        if marker in self.text:
            system, user = self.text.split(marker, 1)
        return _sub(system, variables).strip(), _sub(user, variables).strip()


def _sub(text: str, variables: Mapping[str, Any]) -> str:
    def repl(m: re.Match[str]) -> str:
        key = m.group(1)
        if key not in variables:
            raise KeyError(f"template variable not provided: {key}")
        value = variables[key]
        return value if isinstance(value, str) else _stringify(value)
    return _PLACEHOLDER.sub(repl, text)


def _stringify(value: Any) -> str:
    import json
    return json.dumps(value, ensure_ascii=False)
