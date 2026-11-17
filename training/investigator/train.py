"""``python -m training.investigator.train``: LoRA adapter training of the investigator (models.md 6, operations.md 11).

    python -m training.investigator.train --tier small --dry-run
    python -m training.investigator.train --tier small --approved-plan {plan id}      [COST]

Same implementation as the assistant's (``training/common/train_cli.py``), parameterised by this model's setup: conversations with
calls and results as examples, loss on every assistant turn, deterministic validation scoring (no judge).
"""
from __future__ import annotations

from pathlib import Path

from training.common import train_cli
from training.common.config import TRAINING_DIR
from training.investigator.adapter import InvestigatorSetup

PROG = "python -m training.investigator.train"


def main(argv: list[str] | None = None, *, training_dir: Path = TRAINING_DIR, **kw) -> int:
    return train_cli.main(InvestigatorSetup(training_dir), argv, prog=PROG, **kw)


if __name__ == "__main__":
    raise SystemExit(main())
