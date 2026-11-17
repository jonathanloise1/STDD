"""``python -m training.assistant.export``: merge and the two exports of models.md 6.5, the template check and the evaluation hook of 6.6.

    python -m training.assistant.export --dry-run                 validate and print every step and command (no heavy imports)
    python -m training.assistant.export [--run ID]                merged bf16 weights, ONNX q4f16 (browser), GGUF Q4_K_M (server), manifest, template check
    python -m training.assistant.export --candidate ID            export an untrained candidate (models.md 4.1)
    python -m training.assistant.export --evaluate --approved-estimate ID   then `eval --export` for each output [COST]

The implementation is shared with the investigator (``training/common/export_cli.py``, ``export_core.py``).
"""
from __future__ import annotations

from pathlib import Path

from training.assistant.adapter import AssistantSetup
from training.common import export_cli
from training.common.config import TRAINING_DIR

PROG = "python -m training.assistant.export"


def main(argv: list[str] | None = None, *, training_dir: Path = TRAINING_DIR, **kw) -> int:
    setup = AssistantSetup(training_dir)
    if "eval_main" not in kw:
        def run_eval(a: list[str]) -> int:
            from training.assistant import eval as E
            return E.main(a, E.Env(training_dir=training_dir))
        kw["eval_main"] = run_eval
    return export_cli.main(setup, argv, prog=PROG, **kw)


if __name__ == "__main__":
    raise SystemExit(main())
