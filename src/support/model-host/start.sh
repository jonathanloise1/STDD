#!/usr/bin/env bash
# Starts the llama.cpp model host for one model (models.md 7.4). Shared by the assistant and the investigator.
#
# Environment:
#   MODEL_HOST_MODEL           path of the GGUF file (required)
#   MODEL_HOST_ALIAS           "{model id}-{model_version}", for example assistant-1.0.0 (required);
#                              the launcher appends "@gpu" or "@cpu" and passes the result as --alias
#   MODEL_HOST_PORT            default 8091
#   MODEL_HOST_DEVICE          auto (default), gpu or cpu
#   MODEL_HOST_THREADS         threads of the CPU build; default: physical cores
#   MODEL_HOST_PARALLEL        --parallel, set to MyApp:Assistant:Server:MaxConcurrent; default 2
#   MODEL_HOST_CTX_SIZE        --ctx-size, shared by the slots; default 4096
#   MODEL_HOST_GPU_BIN         llama-server of the GPU build; default llama-server
#   MODEL_HOST_CPU_BIN         llama-server of the CPU build; default llama-server
#   MODEL_HOST_START_TIMEOUT   seconds to wait for GET /health; default 120
#   MODEL_HOST_GPU_PRESENT     1 or 0 to skip the GPU detection of auto
#   MODEL_HOST_BIND            address to listen on; default 127.0.0.1. The Docker setup sets 0.0.0.0, because the
#                              container's port is published on the host's 127.0.0.1 only (compose.yaml)
#
# Flags that change what the model writes (--jinja and the like) are read from shared-flags.txt next to this script, the file the
# evaluation reads too, so that what was evaluated is what runs. Do not add them here.
#
# auto: when a GPU is present (Apple silicon, an NVIDIA device or a Vulkan device) the GPU build starts with
# --n-gpu-layers 999; the launcher waits for /health and reads the start log: unless every layer is offloaded, or
# when the start fails, it stops that process and starts the CPU build with --n-gpu-layers 0. gpu and cpu force a
# device and never fall back. The host listens on 127.0.0.1 only. The script stays in the foreground and ends
# with the host.

set -u

die() {
  echo "model-host: $1" >&2
  exit "${2:-1}"
}

say() {
  echo "model-host: $*"
}

MODEL="${MODEL_HOST_MODEL:-}"
ALIAS_BASE="${MODEL_HOST_ALIAS:-}"
PORT="${MODEL_HOST_PORT:-8091}"
BIND="${MODEL_HOST_BIND:-127.0.0.1}"
DEVICE="${MODEL_HOST_DEVICE:-auto}"
PARALLEL="${MODEL_HOST_PARALLEL:-2}"
CTX_SIZE="${MODEL_HOST_CTX_SIZE:-4096}"
GPU_BIN="${MODEL_HOST_GPU_BIN:-llama-server}"
CPU_BIN="${MODEL_HOST_CPU_BIN:-llama-server}"
START_TIMEOUT="${MODEL_HOST_START_TIMEOUT:-120}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FLAGS_FILE="$SCRIPT_DIR/shared-flags.txt"
SHARED_FLAGS=()
[ -f "$FLAGS_FILE" ] || die "$FLAGS_FILE is missing (the flags shared with the evaluation)."
while IFS= read -r line || [ -n "$line" ]; do
  case "$line" in '' | '#'*) continue ;; esac
  read -r -a words <<< "$line"
  SHARED_FLAGS+=("${words[@]}")
done < "$FLAGS_FILE"
[ "${#SHARED_FLAGS[@]}" -gt 0 ] || die "$FLAGS_FILE lists no flag."

[ -n "$MODEL" ] || die "MODEL_HOST_MODEL is not set (path of the GGUF file)."
[ -f "$MODEL" ] || die "MODEL_HOST_MODEL '$MODEL' is not a file."
[ -n "$ALIAS_BASE" ] || die "MODEL_HOST_ALIAS is not set (for example assistant-1.0.0)."
case "$DEVICE" in auto | gpu | cpu) ;; *) die "MODEL_HOST_DEVICE must be auto, gpu or cpu (got '$DEVICE')." ;; esac
command -v curl > /dev/null 2>&1 || die "curl is required to wait for the host."

LOG_DIR="$(mktemp -d "${TMPDIR:-/tmp}/model-host.XXXXXX")" || die "cannot create a temporary folder."
SERVER_PID=""

cleanup() {
  if [ -n "$SERVER_PID" ] && kill -0 "$SERVER_PID" 2> /dev/null; then
    kill "$SERVER_PID" 2> /dev/null
    wait "$SERVER_PID" 2> /dev/null
  fi
  rm -rf "$LOG_DIR"
}
trap cleanup EXIT
trap 'exit 143' TERM
trap 'exit 130' INT

physical_cores() {
  if [ -n "${MODEL_HOST_THREADS:-}" ]; then
    echo "$MODEL_HOST_THREADS"
    return
  fi
  local n=""
  case "$(uname -s)" in
    Darwin) n="$(sysctl -n hw.physicalcpu 2> /dev/null)" ;;
    *) n="$(lscpu -p=core,socket 2> /dev/null | grep -v '^#' | sort -u | wc -l | tr -d ' ')" ;;
  esac
  case "$n" in '' | 0 | *[!0-9]*) n="$(getconf _NPROCESSORS_ONLN 2> /dev/null || echo 4)" ;; esac
  echo "$n"
}

gpu_present() {
  case "${MODEL_HOST_GPU_PRESENT:-}" in
    1) return 0 ;;
    0) return 1 ;;
  esac
  if [ "$(uname -s)" = "Darwin" ]; then
    [ "$(uname -m)" = "arm64" ] && return 0
    return 1
  fi
  if command -v nvidia-smi > /dev/null 2>&1 && nvidia-smi -L 2> /dev/null | grep -q '^GPU '; then
    return 0
  fi
  if command -v vulkaninfo > /dev/null 2>&1 && vulkaninfo --summary 2> /dev/null | grep -q 'deviceType *= *PHYSICAL_DEVICE_TYPE_\(DISCRETE\|INTEGRATED\)_GPU'; then
    return 0
  fi
  return 1
}

# start_host BIN DEVICE: starts the host in the background; sets SERVER_PID and SERVER_LOG.
start_host() {
  local bin="$1" device="$2" layers threads_args=()
  if [ "$device" = "gpu" ]; then
    layers=999
  else
    layers=0
    threads_args=(--threads "$(physical_cores)")
  fi
  SERVER_LOG="$LOG_DIR/$device.log"
  say "starting $bin on the $device (alias ${ALIAS_BASE}@${device}, port $PORT)"
  "$bin" --model "$MODEL" --host "$BIND" --port "$PORT" --alias "${ALIAS_BASE}@${device}" \
    --ctx-size "$CTX_SIZE" --parallel "$PARALLEL" --n-gpu-layers "$layers" "${SHARED_FLAGS[@]}" ${threads_args[@]+"${threads_args[@]}"} \
    > "$SERVER_LOG" 2>&1 &
  SERVER_PID=$!
}

stop_host() {
  if [ -n "$SERVER_PID" ]; then
    kill "$SERVER_PID" 2> /dev/null
    wait "$SERVER_PID" 2> /dev/null
    SERVER_PID=""
  fi
}

# wait_healthy: 0 when GET /health answers before the timeout, 1 when the process ends or the time runs out.
wait_healthy() {
  local waited=0
  while [ "$waited" -lt "$START_TIMEOUT" ]; do
    kill -0 "$SERVER_PID" 2> /dev/null || return 1
    if curl -fsS -m 2 "http://127.0.0.1:${PORT}/health" > /dev/null 2>&1; then
      return 0
    fi
    sleep 1
    waited=$((waited + 1))
  done
  return 1
}

# offload_state: prints "full", "partial" or "unknown" from the start log of the running host.
offload_state() {
  local line loaded total
  line="$(grep -E 'offloaded [0-9]+/[0-9]+ layers to GPU' "$SERVER_LOG" 2> /dev/null | tail -n 1)"
  [ -n "$line" ] || { echo unknown; return; }
  loaded="$(echo "$line" | sed -E 's/.*offloaded ([0-9]+)\/([0-9]+) layers to GPU.*/\1/')"
  total="$(echo "$line" | sed -E 's/.*offloaded ([0-9]+)\/([0-9]+) layers to GPU.*/\2/')"
  if [ "$total" -gt 0 ] && [ "$loaded" -eq "$total" ]; then echo full; else echo partial; fi
}

# supervise: keeps the script alive as long as the host runs; ends with the host's exit code.
supervise() {
  local code=0
  wait "$SERVER_PID" || code=$?
  SERVER_PID=""
  say "the host ended with code $code"
  exit "$code"
}

have_bin() {
  command -v "$1" > /dev/null 2>&1
}

run_cpu() {
  have_bin "$CPU_BIN" || die "'$CPU_BIN' was not found. Install llama.cpp (llama-server) or set MODEL_HOST_CPU_BIN." 127
  start_host "$CPU_BIN" cpu
  if ! wait_healthy; then
    tail -n 20 "$SERVER_LOG" >&2 2> /dev/null
    stop_host
    die "the CPU build did not become healthy."
  fi
  say "the host is ready on the cpu"
  supervise
}

run_gpu_forced() {
  have_bin "$GPU_BIN" || die "'$GPU_BIN' was not found. Install a GPU build of llama.cpp or set MODEL_HOST_GPU_BIN." 127
  start_host "$GPU_BIN" gpu
  if ! wait_healthy; then
    tail -n 20 "$SERVER_LOG" >&2 2> /dev/null
    stop_host
    die "the GPU build did not become healthy."
  fi
  [ "$(offload_state)" = full ] || say "warning: not every layer is offloaded to the GPU"
  say "the host is ready on the gpu"
  supervise
}

run_auto() {
  if ! gpu_present; then
    say "no GPU found"
    run_cpu
  fi
  if ! have_bin "$GPU_BIN"; then
    say "'$GPU_BIN' was not found, trying the CPU build"
    run_cpu
  fi

  start_host "$GPU_BIN" gpu
  if wait_healthy; then
    case "$(offload_state)" in
      full)
        say "the host is ready on the gpu"
        supervise
        ;;
      partial) say "not every layer is offloaded to the GPU, falling back to the CPU build" ;;
      *) say "the start log does not confirm that every layer is offloaded, falling back to the CPU build" ;;
    esac
  else
    say "the GPU build did not start, falling back to the CPU build"
  fi
  stop_host
  run_cpu
}

case "$DEVICE" in
  cpu) run_cpu ;;
  gpu) run_gpu_forced ;;
  auto) run_auto ;;
esac
