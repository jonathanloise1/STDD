#!/usr/bin/env bash
# Entry point of the model host containers (compose.yaml): checks that a model file is mounted, says so clearly
# when it is not, then hands over to start.sh with the container's settings. The llama.cpp server image has no
# GPU or CPU split: the same binary serves both, and start.sh decides the device (MODEL_HOST_DEVICE).
#
# Expected: the host folder of MODELS_DIR mounted read-only at /models, and MODEL_HOST_MODEL naming a file in it.
set -u

model="${MODEL_HOST_MODEL:-}"
if [ -z "$model" ] || [ ! -f "$model" ]; then
  cat >&2 << MESSAGE
model-host: NO MODEL FILE IS MOUNTED.
  Expected the GGUF file '${model:-(MODEL_HOST_MODEL is not set)}' inside the container.
  Put the file in the folder MODELS_DIR points to on the host (.env.example: ~/myapp-models; no default, compose requires it) and set the variable
  that names it relative to that folder (ASSISTANT_MODEL_FILE, INVESTIGATOR_MODEL_FILE or TEACHER_MODEL_FILE in .env).
  This image never downloads weights. See docs/guidelines/models.md section 7 and 9.
MESSAGE
  exit 78
fi

export MODEL_HOST_CPU_BIN="${MODEL_HOST_CPU_BIN:-/app/llama-server}"
export MODEL_HOST_GPU_BIN="${MODEL_HOST_GPU_BIN:-/app/llama-server}"
exec bash /opt/model-host/start.sh
