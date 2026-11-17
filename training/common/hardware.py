"""Hardware check (models.md 6.4): ``python -m training.common.hardware``.

Detects CUDA or Apple MPS, memory and disk, prints the expected durations and exits with 1 when neither CUDA nor
MPS is available (training on CPU is refused). Works without torch; when torch is installed it is used to confirm
the device (imported lazily).
"""
from __future__ import annotations

import argparse
import json
import os
import platform
import shutil
import subprocess
import sys
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any

# 6.4 table: minimum requirements and expected training time for a 0.6B model, 20,000 examples
REQUIREMENTS = {
    "cuda": {"vram_gb": 12, "ram_gb": 32, "disk_gb": 40, "duration": "1 to 3 hours"},
    "mps": {"ram_gb": 16, "ram_gb_1b": 32, "disk_gb": 40, "duration": "6 to 10 hours"},
    "cpu": {"cores": 8, "ram_gb": 16, "duration": "1 to 2 hours for the platform test (evaluation only, GGUF)"},
}

# Minimum VRAM and RAM of a CUDA LoRA run by model size (models.md 9.4 "Hardware per step"; ESTIMATES, not measurements): (largest parameter
# count, VRAM GB, RAM GB). The first row whose limit holds the model applies.
CUDA_BY_SIZE = [(1_000_000_000, 12, 32), (2_500_000_000, 16, 32), (5_000_000_000, 24, 48), (10_000_000_000, 40, 64), (10**15, 80, 128)]
WSL_NVIDIA_SMI = "/usr/lib/wsl/lib/nvidia-smi"


@dataclass
class HardwareInfo:
    device: str                      # "cuda", "mps" or "cpu"
    name: str
    ram_gb: float
    vram_gb: float | None
    disk_free_gb: float
    cpu_cores: int
    python: str
    verified_by: str                 # "torch", "nvidia-smi", "platform" or "none"
    notes: list[str] = field(default_factory=list)
    wsl: bool = False


def _run(cmd: list[str]) -> str | None:
    try:
        res = subprocess.run(cmd, capture_output=True, text=True, timeout=10)
    except (OSError, subprocess.SubprocessError):
        return None
    return res.stdout.strip() if res.returncode == 0 else None


def is_wsl() -> bool:
    """True inside Windows Subsystem for Linux (WSL 2 reports ``microsoft`` in the kernel release)."""
    if sys.platform != "linux":
        return False
    if os.environ.get("WSL_DISTRO_NAME") or os.environ.get("WSL_INTEROP"):
        return True
    try:
        return "microsoft" in Path("/proc/version").read_text(encoding="utf-8", errors="ignore").lower()
    except OSError:
        return False


def windows_drive_root(path: str | Path) -> str | None:
    """``/mnt/c`` for a path under ``/mnt/c/...`` (a Windows drive seen from WSL), else ``None``."""
    parts = Path(os.path.abspath(str(path))).parts
    if len(parts) >= 3 and parts[1] == "mnt" and len(parts[2]) == 1 and parts[2].isalpha():
        return "/" + "/".join(parts[1:3])
    return None


def nvidia_smi_path() -> str | None:
    """``nvidia-smi`` on the PATH, else the copy WSL provides, which is not on the PATH of every distribution."""
    return shutil.which("nvidia-smi") or (WSL_NVIDIA_SMI if os.path.exists(WSL_NVIDIA_SMI) else None)


def min_cuda_requirements(model_parameters: int) -> tuple[int, int]:
    """``(vram_gb, ram_gb)`` a CUDA LoRA run needs for a model of this size."""
    for limit, vram, ram in CUDA_BY_SIZE:
        if model_parameters <= limit:
            return vram, ram
    return CUDA_BY_SIZE[-1][1], CUDA_BY_SIZE[-1][2]


def _windows_ram_gb() -> float:
    """Physical memory on native Windows through ``GlobalMemoryStatusEx`` (``wmic`` is removed from current Windows)."""
    import ctypes

    class Status(ctypes.Structure):
        _fields_ = [("length", ctypes.c_ulong), ("load", ctypes.c_ulong), ("total_phys", ctypes.c_ulonglong), ("avail_phys", ctypes.c_ulonglong),
                    ("total_page", ctypes.c_ulonglong), ("avail_page", ctypes.c_ulonglong), ("total_virtual", ctypes.c_ulonglong),
                    ("avail_virtual", ctypes.c_ulonglong), ("avail_extended", ctypes.c_ulonglong)]

    st = Status()
    st.length = ctypes.sizeof(Status)
    if ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(st)):  # type: ignore[attr-defined]
        return st.total_phys / 2**30
    return 0.0


def total_ram_gb() -> float:
    if sys.platform == "darwin":
        out = _run(["sysctl", "-n", "hw.memsize"])
        if out and out.isdigit():
            return int(out) / 2**30
    try:
        pages, size = os.sysconf("SC_PHYS_PAGES"), os.sysconf("SC_PAGE_SIZE")
        return pages * size / 2**30
    except (ValueError, OSError, AttributeError):
        pass
    if sys.platform == "win32":
        try:
            return _windows_ram_gb()
        except (OSError, AttributeError, ValueError):
            pass
    return 0.0


def detect(disk_path: str | Path = ".") -> HardwareInfo:
    notes: list[str] = []
    disk = shutil.disk_usage(str(disk_path)).free / 2**30
    wsl = is_wsl()
    if wsl:
        drive = windows_drive_root(Path(disk_path).resolve())
        if drive:
            notes.append(f"WSL: the working folder is on the Windows drive {drive}: the free disk is that drive's. Work in the Linux file system instead (models.md 9.4 W3)")
        else:
            notes.append("WSL: the free disk is the limit of the virtual disk, not the free space of the Windows drive: check it with Get-PSDrive C in PowerShell (models.md 9.4 W5)")
    cores = os.cpu_count() or 1
    ram = total_ram_gb()
    device, name, vram, verified = "cpu", platform.processor() or platform.machine(), None, "none"

    torch = None
    try:  # lazy and optional
        import torch as _torch  # type: ignore
        torch = _torch
    except Exception:  # noqa: BLE001
        notes.append("torch is not installed: devices are detected without it")

    if torch is not None and torch.cuda.is_available():
        props = torch.cuda.get_device_properties(0)
        device, name, vram, verified = "cuda", props.name, props.total_memory / 2**30, "torch"
    elif torch is not None and getattr(torch.backends, "mps", None) is not None and torch.backends.mps.is_available():
        device, name, verified = "mps", f"Apple Silicon ({platform.machine()})", "torch"
    else:
        smi_bin = nvidia_smi_path()
        smi = _run([smi_bin, "--query-gpu=name,memory.total", "--format=csv,noheader,nounits"]) if smi_bin else None
        if smi:
            first = smi.splitlines()[0].split(",")
            device, name, vram, verified = "cuda", first[0].strip(), float(first[1]) / 1024, "nvidia-smi"
        elif sys.platform == "darwin" and platform.machine() == "arm64":
            major = int((platform.mac_ver()[0] or "0").split(".")[0] or 0)
            if major >= 14:
                device, name, verified = "mps", f"Apple Silicon ({platform.machine()})", "platform"
                if torch is None:
                    notes.append("MPS is assumed from the platform; install torch to confirm it")
            else:
                notes.append("macOS 14 or later is required for Apple MPS training")
    return HardwareInfo(device, name, round(ram, 1), None if vram is None else round(vram, 1), round(disk, 1),
                        cores, platform.python_version(), verified, notes, wsl)


def evaluate(info: HardwareInfo, model_parameters: int = 600_000_000) -> tuple[bool, list[str]]:
    """Return ``(ok_for_training, problems)`` against the table of 6.4."""
    problems: list[str] = []
    if info.device == "cpu":
        return False, ["no CUDA or Apple MPS device: training on CPU is refused (CPU is for GGUF evaluation only)"]
    req = REQUIREMENTS[info.device]
    if info.device == "cuda":
        need_vram, need_ram = min_cuda_requirements(model_parameters)
        if info.vram_gb is not None and info.vram_gb < need_vram:
            problems.append(f"VRAM {info.vram_gb} GB is below {need_vram} GB for a model of {model_parameters:,} parameters")
        if info.ram_gb < need_ram:
            problems.append(f"RAM {info.ram_gb} GB is below {need_ram} GB for a model of {model_parameters:,} parameters" + (" (under WSL this is the limit of .wslconfig)" if info.wsl else ""))
    else:
        need = req["ram_gb_1b"] if model_parameters > 700_000_000 else req["ram_gb"]
        if info.ram_gb < need:
            problems.append(f"unified memory {info.ram_gb} GB is below {need} GB for this model size")
    if info.disk_free_gb < req["disk_gb"]:
        problems.append(f"free disk {info.disk_free_gb} GB is below {req['disk_gb']} GB")
    if sys.version_info[:2] not in ((3, 11), (3, 12)):
        problems.append(f"Python {info.python}: training expects 3.11 or 3.12")
    return not problems, problems


def micro_batch_plan(info: HardwareInfo, effective_batch: int = 32, model_parameters: int = 600_000_000, override: int | None = None) -> dict[str, int]:
    """Micro-batch and gradient accumulation for an effective batch (6.2). Used by the train command.

    The micro-batch follows the VRAM and shrinks with the model: its weights and activations take the memory the batch would use (halved above
    3 billion parameters, quartered above 8 billion). ``override`` (``--micro-batch``) replaces the choice; it is rounded down to a divisor
    of the effective batch so that the accumulation is exact."""
    if override is not None:
        micro = max(1, min(int(override), effective_batch))
        while effective_batch % micro:
            micro -= 1
        return {"micro_batch": micro, "grad_accum": effective_batch // micro, "effective_batch": effective_batch}
    if info.device == "cuda":
        vram = info.vram_gb or 12
        micro = 16 if vram >= 40 else 8 if vram >= 24 else 4
        if model_parameters > 8_000_000_000:
            micro = max(1, micro // 4)
        elif model_parameters > 3_000_000_000:
            micro = max(1, micro // 2)
    elif info.device == "mps":
        micro = 4 if info.ram_gb >= 32 else 2
    else:
        micro = 1
    micro = min(micro, effective_batch)
    return {"micro_batch": micro, "grad_accum": max(1, effective_batch // micro), "effective_batch": micro * max(1, effective_batch // micro)}


def build_report(info: HardwareInfo, ok: bool, problems: list[str], model_parameters: int = 600_000_000) -> dict[str, Any]:
    key = info.device
    req = dict(REQUIREMENTS[key])
    if key == "cuda":
        req["vram_gb"], req["ram_gb"] = min_cuda_requirements(model_parameters)
    return {"hardware": asdict(info), "ok_for_training": ok, "problems": problems,
            "requirements": req, "expected_duration_0_6b_20k": REQUIREMENTS[key]["duration"],
            "batch_plan": micro_batch_plan(info, model_parameters=model_parameters)}


def format_report(rep: dict[str, Any]) -> str:
    h = rep["hardware"]
    lines = [f"Device:     {h['device']} ({h['name']}) verified by {h['verified_by']}" + (" (WSL)" if h.get("wsl") else ""),
             f"Memory:     {h['ram_gb']} GB RAM" + (f", {h['vram_gb']} GB VRAM" if h["vram_gb"] else ""),
             f"Disk free:  {h['disk_free_gb']} GB",
             f"CPU cores:  {h['cpu_cores']}",
             f"Python:     {h['python']}",
             f"Expected training time (0.6B, 20,000 examples): {rep['expected_duration_0_6b_20k']}",
             f"Batch plan: {rep['batch_plan']}"]
    lines += [f"Note:       {n}" for n in h["notes"]]
    lines += [f"Problem:    {p}" for p in rep["problems"]]
    lines.append("Result:     " + ("OK for training" if rep["ok_for_training"] else "not suitable for training"))
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="python -m training.common.hardware", description="Check the machine for training (models.md 6.4). Exit 1 without CUDA or Apple MPS.")
    ap.add_argument("--json", action="store_true", help="print the report as JSON")
    ap.add_argument("--parameters", type=int, default=600_000_000, help="model parameters (memory need depends on size)")
    args = ap.parse_args(argv)
    info = detect()
    ok, problems = evaluate(info, args.parameters)
    rep = build_report(info, ok, problems, args.parameters)
    print(json.dumps(rep, indent=2) if args.json else format_report(rep))
    return 0 if info.device in ("cuda", "mps") else 1


if __name__ == "__main__":
    raise SystemExit(main())
