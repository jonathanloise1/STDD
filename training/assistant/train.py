"""``python -m training.assistant.train``: LoRA adapter training of the assistant (models.md 6.1 to 6.4).

    python -m training.assistant.train --dry-run                  validate configuration and dataset, print the plan (no torch)
    python -m training.assistant.train --approved-plan {plan id}  the run [COST]; resumes from its saved state when run again

The implementation is shared with the investigator (``training/common/train_cli.py``, ``train_core.py``).
"""
from __future__ import annotations

from pathlib import Path

from training.assistant.adapter import AssistantSetup
from training.common import train_cli
from training.common.config import TRAINING_DIR

PROG = "python -m training.assistant.train"


def main(argv: list[str] | None = None, *, training_dir: Path = TRAINING_DIR, **kw) -> int:
    setup = AssistantSetup(training_dir)
    if "judge_factory" not in kw:
        def make(real: bool):
            from training.assistant import eval as E
            env = E.Env(training_dir=training_dir)
            return E.build_judge(env, E.load_eval_config(env), real)
        kw["judge_factory"] = make
    return train_cli.main(setup, argv, prog=PROG, **kw)


if __name__ == "__main__":
    raise SystemExit(main())
