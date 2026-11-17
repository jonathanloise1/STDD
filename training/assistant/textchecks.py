"""Text checks shared by assembly and validators (V04)."""
from __future__ import annotations

import re
from pathlib import Path

EMAIL = re.compile(r"[\w.+-]+@[\w-]+\.[\w.-]+")
URL = re.compile(r"https?://\S+|www\.\S+|\b[\w-]+\.(?:com|org|net|io|it|eu|edu|gov)\b", re.I)
PHONE = re.compile(r"(?<![\w.])(?:\+\d[\d\s().-]{7,}\d|\(?\d{2,4}\)?[\s.-]\d{3,4}[\s.-]\d{3,4}|\d{9,})(?![\w])")
BLOCKLIST_PATH = Path(__file__).with_name("config") / "blocklist.txt"


def load_blocklist(path: Path = BLOCKLIST_PATH) -> list[str]:
    return [l.strip() for l in path.read_text(encoding="utf-8").splitlines() if l.strip() and not l.startswith("#")]


def text_problem(text: str, blocklist: list[str]) -> str | None:
    """Why the text breaks V04, or ``None``."""
    for name in blocklist:
        if re.search(rf"(?<![\w]){re.escape(name)}(?![\w])", text):
            return f"blocklisted name: {name}"
    if EMAIL.search(text):
        return "e-mail address"
    if URL.search(text):
        return "URL"
    if PHONE.search(text):
        return "phone number"
    return None
