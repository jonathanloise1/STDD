# Windows PC checklist before the first training session

Follow the steps in order. Details are in `docs/guidelines/models.md` section 9.4 (W1 to W12); this page does not repeat them.
**Every command below is untested** (9.4). **Every size, memory figure and duration is an estimate**: write measured values back into 9.4.

## 1. Hardware and disk (estimates, 9.4)

- [ ] Windows 11 up to date; virtualisation on in the firmware; sleep off and update restarts paused for the run window.
- [ ] GPU VRAM (estimate): 24 GB minimum for the teacher at 2 slots, 32 GB comfortable; 12 GB for the 0.8B LoRA, 16 GB for the 2B, 24 GB for the 4B.
- [ ] RAM (estimate): 48 GB or more; 64 GB for the 35B-A3B merge.
- [ ] Free space on the drive that holds the WSL disk, `C:` by default (estimate): 300 GB; 500 GB with the large tier.

## 2. Windows side (PowerShell as administrator, W1)

- [ ] Recent NVIDIA driver on Windows. Do not install an NVIDIA driver inside Linux.
- [ ] Run:

```powershell
nvidia-smi                      # the Windows driver; it also provides CUDA inside WSL
wsl --install -d Ubuntu-24.04
wsl --update
wsl --version                   # WSL 2
```

- [ ] Create `%UserProfile%\.wslconfig` with the content below, then run `wsl --shutdown`:

```
[wsl2]
memory=48GB
swap=32GB
```

## 3. Inside Ubuntu 24.04 (W2 to W7)

- [ ] Packages, CUDA toolkit (not `cuda` or `cuda-drivers`), Node, .NET SDK (W2):

```bash
sudo apt update && sudo apt install -y build-essential cmake git curl tmux python3.12 python3.12-venv python3.12-dev libcurl4-openssl-dev
wget https://developer.download.nvidia.com/compute/cuda/repos/wsl-ubuntu/x86_64/cuda-keyring_1.1-1_all.deb
sudo dpkg -i cuda-keyring_1.1-1_all.deb && sudo apt update && sudo apt install -y cuda-toolkit-12-8
curl -fsSL https://deb.nodesource.com/setup_22.x | sudo -E bash - && sudo apt install -y nodejs
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
cat >> ~/.bashrc << 'EOF'
export PATH=/usr/local/cuda/bin:$HOME/.dotnet:$PATH
export DOTNET_ROOT=$HOME/.dotnet
EOF
. ~/.bashrc
git config --global core.autocrlf input
nvidia-smi && nvcc --version && node --version && dotnet --version
```

- [ ] Node 22.18 or later, .NET 10 SDK, tmux are present (the last line above shows the versions).
- [ ] Repository in the Linux file system, not under `/mnt/c` (W3). `{repository url}` and `{branch}` are placeholders to replace:

```bash
mkdir -p ~/src && cd ~/src && git clone {repository url} stdd && cd stdd && git checkout {branch}
file src/support/model-host/start.sh       # must not say "CRLF"
test -x src/support/model-host/start.sh || chmod +x src/support/model-host/start.sh
```

- [ ] Python environment and requirements (W4). Pins: torch 2.15.0, transformers 5.22.1, peft 0.21.2, accelerate 1.15.0 (checked on PyPI, never run with the training code). The default torch wheel of Windows is CPU only and the Linux one is built for CUDA 13: if the check below fails, install torch from the index of your CUDA version and write the pin into `training/requirements.txt`. The `torch` check must print `True` and the GPU's name; `pytest` must pass:

```bash
python3.12 -m venv .venv && . .venv/bin/activate
pip install -r training/requirements.txt
python -c "import torch; print(torch.__version__, torch.version.cuda, torch.cuda.is_available(), torch.cuda.get_device_name(0))"
python -m pytest training/tests
```

- [ ] llama.cpp built with CUDA at the tag written in `export.json` (W6). `{llama_cpp.tag}` is a placeholder:

```bash
git clone https://github.com/ggml-org/llama.cpp ~/tools/llama.cpp
cd ~/tools/llama.cpp && git checkout {llama_cpp.tag}
cmake -B build -DGGML_CUDA=ON -DCMAKE_BUILD_TYPE=Release && cmake --build build -j
cd ~/src/stdd
cat >> ~/.bashrc << 'EOF'
export LLAMA_CPP_DIR=$HOME/tools/llama.cpp
export PATH="$LLAMA_CPP_DIR/build/bin:$PATH"
EOF
. ~/.bashrc && . .venv/bin/activate
pip install sentencepiece protobuf
```

- [ ] transformers.js checkout at the tag in `export.json`, and the dry run changes none of `torch`, `transformers` or `peft`; otherwise stop (W7). `{transformers_js.tag}` and `{version in src/frontend/package.json}` are placeholders:

```bash
git clone https://github.com/huggingface/transformers.js ~/tools/transformers.js
git -C ~/tools/transformers.js checkout {transformers_js.tag}
echo 'export TRANSFORMERS_JS_DIR=$HOME/tools/transformers.js' >> ~/.bashrc && . ~/.bashrc && . .venv/bin/activate
pip install --dry-run -r "$TRANSFORMERS_JS_DIR/scripts/requirements.txt"
npm install --prefix training/assistant/js --no-save --no-package-lock @huggingface/transformers@{version in src/frontend/package.json}
```

## 4. Model files (W8; 9.1 step 4; 9.2 step 5)

Revisions are read from the candidate files. Sizes are the 9.4 estimates of the Hugging Face cache, except the teacher GGUF.

| Model | Repository id | Pinned revision | Role | Size (estimate) |
|-------|---------------|-----------------|------|-----------------|
| Qwen3.5-0.8B | `Qwen/Qwen3.5-0.8B` | `2fc06364715b967f1860aea9cf38778875588b17` | Assistant | 2 GB |
| Qwen3.5-2B | `Qwen/Qwen3.5-2B` | `15852e8c16360a2fea060d615a32b45270f8a8fc` | Assistant; investigator small | 5 GB |
| Qwen3.5-4B | `Qwen/Qwen3.5-4B` | `851bf6e806efd8d0a36b00ddf55e13ccb7b8cd0a` | Investigator small | 9 GB |
| Qwen3.5-9B | `Qwen/Qwen3.5-9B` | `c202236235762e1c871ad0ccb60c8ee5ba337b9a` | Investigator large (trainable) | 19 GB |
| Qwen3.6-35B-A3B | `Qwen/Qwen3.6-35B-A3B` | `345d467b117d1fd5eda9b879a5988de9ae5f47a7` | Investigator large (untrained) | 70 GB (GGUF about 21 GB) |
| Qwen3-0.6B | `Qwen/Qwen3-0.6B` | `c1899de289a04d12100db370d81485cdf75e47ca` | Assistant fallback | not sized in 9.4 |
| Qwen3.8-27B, `Q4_K_M` GGUF | `unsloth/Qwen3.8-27B-GGUF` (unverified) or self-made from `Qwen/Qwen3.8-27B` | `37561816e66b5b9153b1b64c10968c18c1e6f4a0` (for a self-made one) | Teacher and judge | 17 GB |
| Qwen3-Embedding-0.6B | `Qwen/Qwen3-Embedding-0.6B` | `97b0c614be4d77ee51c0cef4e5f07c00f9eb65b3` (from `training/assistant/config/embed.json`) | Help embedding table, built once (models.md 7.8, 9.5); needs `transformers` 4.51.0 or later | 1.2 GB (estimate) |

- [ ] Licences reviewed (all listed as Apache-2.0 in the candidate files; 6.1) and `verified` set to `true` in both `candidates.json` files. Unverified entries are skipped unless `--allow-unverified` is given.
- [ ] Teacher GGUF at `~/myapp-models/teacher/Qwen3.8-27B-Q4_K_M.gguf` (W8), outside the repository; source and hash in the `teacher.weights` block of `training/assistant/config/dataset.json`; the hash recorded (`python -m training.common.weights FILE`). Never committed.
- [ ] Base models are in `~/.cache/huggingface` (the first command that needs them downloads them, or copy them in). Weights are never committed.
- [ ] Embedding model: licence reviewed on its model card (the card's prose calls it "proprietary" while its metadata says Apache-2.0, models.md 7.8), `verified` set to `true` and `checked` updated in `training/assistant/config/embed.json`. The first `python -m training.assistant.embed build` downloads it (or put the folder path in `hub_id` and set `HF_HUB_OFFLINE=1`). The step list is models.md 9.5.

## 5. Ready check (W5, W9)

- [ ] Hardware check, with `.venv` active, read against the table in section 1 and 9.4 "Hardware per step":

```bash
python -m training.common.hardware --parameters 600000000
```

- [ ] Teacher and judge host started in its own terminal (`tmux new -s teacher`), with the Keep-open note of W9:

```bash
cd ~/src/stdd
MODEL_HOST_MODEL=$HOME/myapp-models/teacher/Qwen3.8-27B-Q4_K_M.gguf MODEL_HOST_ALIAS=qwen3.8-27b \
MODEL_HOST_PORT=8093 MODEL_HOST_DEVICE=gpu MODEL_HOST_PARALLEL=4 MODEL_HOST_CTX_SIZE=32768 \
MODEL_HOST_START_TIMEOUT=600 src/support/model-host/start.sh
```

- [ ] In another terminal, `curl -s http://localhost:8093/v1/models` names `qwen3.8-27b@gpu`. Then set `max_concurrency` in `dataset.json` and run the `--measure` commands (W9, 9.1 step 4).
- [ ] The owner has said that training may start. Each `[COST]` step is approved with its estimate or plan id.
