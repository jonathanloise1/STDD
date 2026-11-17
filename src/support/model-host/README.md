# Model host

Starts the llama.cpp `llama-server` for one model, on `127.0.0.1` (models.md 7.4). The same launcher serves the
assistant and the investigator; one process per model.

| File | For |
|------|-----|
| `start.sh` | Linux and macOS |
| `shared-flags.txt` | The `llama-server` flags that change what the model writes (`--jinja`). Both launchers and the evaluation (`training/common/backends.py`) read it; add such a flag there, never in one launcher |
| `start.ps1` | Windows (PowerShell 7). Same logic, **best effort and not yet run**: test it on a Windows host before relying on it. It quotes paths with spaces, keeps the process handle for the exit code, sends no request through a proxy, and stops the host on Ctrl+C; none of that has been tried. The training pipeline runs the host through `start.sh` in WSL (models.md 9.4) |
| `docker-entrypoint.sh` | Entry point of the model host containers of `compose.yaml` (below) |

## Run

```
MODEL_HOST_MODEL=~/myapp-models/assistant/assistant.gguf \
MODEL_HOST_ALIAS=assistant-1.0.0 \
MODEL_HOST_PARALLEL=2 \
src/support/model-host/start.sh
```

`llama-server` and `curl` must be on the PATH (or set `MODEL_HOST_GPU_BIN` and `MODEL_HOST_CPU_BIN` to two builds).
The script stays in the foreground and ends with the host. Point the backend at it with
`MyApp:Assistant:Server:Endpoint` and `MyApp:Assistant:Server:Enabled=true`.

## Environment

| Variable | Default | Meaning |
|----------|---------|---------|
| `MODEL_HOST_MODEL` | none, required | Path of the GGUF file |
| `MODEL_HOST_ALIAS` | none, required | `{model id}-{model_version}`; the launcher adds `@gpu` or `@cpu`. The backend compares the alias served by `GET /v1/models` with `models/model.lock` |
| `MODEL_HOST_PORT` | `8091` | `8091` for the assistant |
| `MODEL_HOST_DEVICE` | `auto` | `auto`, `gpu` or `cpu` |
| `MODEL_HOST_THREADS` | physical cores | Threads of the CPU build |
| `MODEL_HOST_PARALLEL` | `2` | `--parallel`; set it to `MyApp:Assistant:Server:MaxConcurrent` |
| `MODEL_HOST_CTX_SIZE` | `4096` | `--ctx-size`, shared by the slots (2,048 each at 2 slots) |
| `MODEL_HOST_GPU_BIN`, `MODEL_HOST_CPU_BIN` | `llama-server` | The two builds, when they are separate |
| `MODEL_HOST_START_TIMEOUT` | `120` | Seconds to wait for `GET /health` |
| `MODEL_HOST_GPU_PRESENT` | detected | `1` or `0` skips the detection of `auto` |
| `MODEL_HOST_BIND` | `127.0.0.1` | Address to listen on. Only the containers set `0.0.0.0` |

## Device

- `auto`: when a GPU is present (Apple silicon, an NVIDIA device or a Vulkan device) the GPU build starts with
  `--n-gpu-layers 999`. The launcher waits for `/health` and reads the start log line `offloaded N/N layers to GPU`.
  If not every layer is offloaded, if the log does not say, or if the start fails, it stops that process and starts
  the CPU build with `--n-gpu-layers 0 --threads {physical cores}`.
- `gpu`, `cpu`: force a device and never fall back. A forced GPU start warns when a layer stays on the CPU.
- With no `llama-server` for the device the launcher says which variable to set and exits with code 127.

The backend reads model and device from the alias (`assistant-1.0.0@gpu`); a host whose alias does not carry the
lock's `model_version` is not used (models.md 7.5).

## In Docker

`compose.yaml` runs one container per model from the official llama.cpp server image, CPU by default: profiles
`assistant-model` (port 8091), `investigator-model` (8092) and `teacher` (8093, the local OpenAI-compatible endpoint
`http://localhost:8093/v1` of the training pipeline's teacher and judge; default file `Qwen3.8-27B-Q4_K_M.gguf`, alias `qwen3.8-27b`). `docker-entrypoint.sh` checks that the model
file is mounted (`MODELS_DIR` of `.env`, read-only at `/models`), says `NO MODEL FILE IS MOUNTED` and stops when it is
not, and otherwise runs `start.sh` with `MODEL_HOST_BIND=0.0.0.0` and the same `shared-flags.txt`. Each port is published on
the host's `127.0.0.1` only. `compose.gpu.yaml` swaps in the CUDA image and reserves an NVIDIA GPU; macOS containers have no
GPU. Nothing is downloaded. See README "Run with Docker".
