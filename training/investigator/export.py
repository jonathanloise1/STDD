"""``python -m training.investigator.export``: merge and the GGUF export of one tier (models.md 6.5, 7.6; operations.md 11.2).

    python -m training.investigator.export --tier small --dry-run
    python -m training.investigator.export --run {run id} [--evaluate --approved-estimate ID]

Same implementation as the assistant's (``training/common/export_cli.py``). The investigator runs only on the server: merged weights and
GGUF, no ONNX. The lock entry of each export carries ``tier``, ``context_length`` and ``memory_mb``.
"""
from __future__ import annotations

from pathlib import Path

from training.common import export_cli
from training.common.config import TRAINING_DIR
from training.investigator.adapter import InvestigatorSetup

PROG = "python -m training.investigator.export"


def main(argv: list[str] | None = None, *, training_dir: Path = TRAINING_DIR, **kw) -> int:
    if "eval_main" not in kw:
        def run_eval(a: list[str]) -> int:
            from training.investigator import eval as E
            return E.main(a, E.Env(training_dir=training_dir))
        kw["eval_main"] = run_eval
    return export_cli.main(InvestigatorSetup(training_dir), argv, prog=PROG, **kw)


if __name__ == "__main__":
    raise SystemExit(main())
