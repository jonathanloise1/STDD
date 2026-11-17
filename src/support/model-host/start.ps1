#requires -Version 7.0
<#
.SYNOPSIS
  Starts the llama.cpp model host for one model (models.md 7.4). Shared by the assistant and the investigator.
  The same logic as start.sh, for Windows hosts.

.DESCRIPTION
  Environment:
    MODEL_HOST_MODEL           path of the GGUF file (required)
    MODEL_HOST_ALIAS           "{model id}-{model_version}", for example assistant-1.0.0 (required);
                               the launcher appends "@gpu" or "@cpu" and passes the result as --alias
    MODEL_HOST_PORT            default 8091
    MODEL_HOST_DEVICE          auto (default), gpu or cpu
    MODEL_HOST_THREADS         threads of the CPU build; default: physical cores
    MODEL_HOST_PARALLEL        --parallel, set to MyApp:Assistant:Server:MaxConcurrent; default 2
    MODEL_HOST_CTX_SIZE        --ctx-size, shared by the slots; default 4096
    MODEL_HOST_GPU_BIN         llama-server of the GPU build; default llama-server
    MODEL_HOST_CPU_BIN         llama-server of the CPU build; default llama-server
    MODEL_HOST_START_TIMEOUT   seconds to wait for GET /health; default 120
    MODEL_HOST_GPU_PRESENT     1 or 0 to skip the GPU detection of auto
    MODEL_HOST_BIND            address to listen on; default 127.0.0.1 (the Docker setup uses 0.0.0.0)

  Flags that change what the model writes (--jinja and the like) are read from shared-flags.txt next to this script, the file the
  evaluation reads too, so that what was evaluated is what runs. Do not add them here.

  auto: when a GPU is present (an NVIDIA device or a Vulkan device) the GPU build starts with --n-gpu-layers 999;
  the launcher waits for /health and reads the start log: unless every layer is offloaded, or when the start fails,
  it stops that process and starts the CPU build with --n-gpu-layers 0. gpu and cpu force a device and never fall
  back. The host listens on MODEL_HOST_BIND (default 127.0.0.1, loopback only). The script stays in the foreground and ends with the host.

  Status: BEST EFFORT. Written with the same logic as start.sh and not yet run (no working PowerShell was available when it
  was written). Run it once on a Windows host before relying on it; the training pipeline does not use it (models.md 9.4 W11).
#>
$ErrorActionPreference = 'Stop'

function Say([string]$Message) { Write-Host "model-host: $Message" }

function Stop-Launcher([string]$Message, [int]$Code = 1) {
    [Console]::Error.WriteLine("model-host: $Message")
    exit $Code
}

function Get-Setting([string]$Name, [string]$Default) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) { return $Default }
    return $value
}

$Model = Get-Setting 'MODEL_HOST_MODEL' ''
$AliasBase = Get-Setting 'MODEL_HOST_ALIAS' ''
$Port = Get-Setting 'MODEL_HOST_PORT' '8091'
$Bind = Get-Setting 'MODEL_HOST_BIND' '127.0.0.1'
$Device = Get-Setting 'MODEL_HOST_DEVICE' 'auto'
$Parallel = Get-Setting 'MODEL_HOST_PARALLEL' '2'
$CtxSize = Get-Setting 'MODEL_HOST_CTX_SIZE' '4096'
$GpuBin = Get-Setting 'MODEL_HOST_GPU_BIN' 'llama-server'
$CpuBin = Get-Setting 'MODEL_HOST_CPU_BIN' 'llama-server'
$StartTimeout = [int](Get-Setting 'MODEL_HOST_START_TIMEOUT' '120')

$FlagsFile = Join-Path $PSScriptRoot 'shared-flags.txt'
if (-not (Test-Path -LiteralPath $FlagsFile -PathType Leaf)) { Stop-Launcher "$FlagsFile is missing (the flags shared with the evaluation)." }
$SharedFlags = @(Get-Content -LiteralPath $FlagsFile | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') } | ForEach-Object { $_ -split '\s+' })
if ($SharedFlags.Count -eq 0) { Stop-Launcher "$FlagsFile lists no flag." }

if (-not $Model) { Stop-Launcher 'MODEL_HOST_MODEL is not set (path of the GGUF file).' }
if (-not (Test-Path -LiteralPath $Model -PathType Leaf)) { Stop-Launcher "MODEL_HOST_MODEL '$Model' is not a file." }
if (-not $AliasBase) { Stop-Launcher 'MODEL_HOST_ALIAS is not set (for example assistant-1.0.0).' }
if ($Device -notin @('auto', 'gpu', 'cpu')) { Stop-Launcher "MODEL_HOST_DEVICE must be auto, gpu or cpu (got '$Device')." }

$LogDir = Join-Path ([IO.Path]::GetTempPath()) ("model-host." + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $LogDir | Out-Null
$script:Server = $null
$script:ServerLogs = @()

function Get-PhysicalCores {
    $threads = [Environment]::GetEnvironmentVariable('MODEL_HOST_THREADS')
    if ($threads) { return [int]$threads }
    if ($IsWindows) {
        $cores = (Get-CimInstance Win32_Processor | Measure-Object -Property NumberOfCores -Sum).Sum
        if ($cores -gt 0) { return [int]$cores }
    }
    return [Environment]::ProcessorCount
}

function Test-GpuPresent {
    switch ([Environment]::GetEnvironmentVariable('MODEL_HOST_GPU_PRESENT')) {
        '1' { return $true }
        '0' { return $false }
    }
    if (Get-Command nvidia-smi -ErrorAction SilentlyContinue) {
        if ((& nvidia-smi -L 2>$null) -match '^GPU ') { return $true }
    }
    if (Get-Command vulkaninfo -ErrorAction SilentlyContinue) {
        if ((& vulkaninfo --summary 2>$null) -match 'deviceType\s*=\s*PHYSICAL_DEVICE_TYPE_(DISCRETE|INTEGRATED)_GPU') { return $true }
    }
    return $false
}

# Start-Process joins -ArgumentList with spaces and quotes nothing: an argument with a space (a path under "C:\Program Files" or a user name) would split.
function ConvertTo-QuotedArgument([string]$Value) {
    if ($Value -notmatch '[\s"]') { return $Value }
    return '"' + ($Value -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"'
}

function Test-Binary([string]$Name) { return [bool](Get-Command $Name -ErrorAction SilentlyContinue) }

# Starts the host; sets $script:Server and $script:ServerLogs (llama.cpp writes its log to stderr).
function Start-Host([string]$Bin, [string]$Kind) {
    $arguments = @('--model', $Model, '--host', $Bind, '--port', $Port, '--alias', "$AliasBase@$Kind",
        '--ctx-size', $CtxSize, '--parallel', $Parallel) + $SharedFlags
    if ($Kind -eq 'gpu') { $arguments += @('--n-gpu-layers', '999') }
    else { $arguments += @('--n-gpu-layers', '0', '--threads', (Get-PhysicalCores)) }

    $out = Join-Path $LogDir "$Kind.out.log"
    $err = Join-Path $LogDir "$Kind.err.log"
    $script:ServerLogs = @($out, $err)
    Say "starting $Bin on the $Kind (alias $AliasBase@$Kind, port $Port)"
    $quoted = @($arguments | ForEach-Object { ConvertTo-QuotedArgument ([string]$_) })
    $script:Server = Start-Process -FilePath (Get-Command $Bin).Source -ArgumentList $quoted -NoNewWindow -PassThru `
        -RedirectStandardOutput $out -RedirectStandardError $err
    # Reading Handle once keeps the process handle open: without it ExitCode can be $null after the process has ended.
    $null = $script:Server.Handle
}

function Stop-Host {
    if ($script:Server -and -not $script:Server.HasExited) {
        Stop-Process -Id $script:Server.Id -Force -ErrorAction SilentlyContinue
        $script:Server.WaitForExit()
    }
    $script:Server = $null
}

# $true when GET /health answers before the timeout, $false when the process ends or the time runs out.
function Wait-Healthy {
    for ($waited = 0; $waited -lt $StartTimeout; $waited++) {
        if ($script:Server.HasExited) { return $false }
        try {
            $response = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/health" -TimeoutSec 2 -UseBasicParsing -NoProxy
            if ($response.StatusCode -eq 200) { return $true }
        } catch { }
        Start-Sleep -Seconds 1
    }
    return $false
}

# 'full', 'partial' or 'unknown' from the start log of the running host.
function Get-OffloadState {
    $pattern = 'offloaded (\d+)/(\d+) layers to GPU'
    $line = $script:ServerLogs | Where-Object { Test-Path $_ } | ForEach-Object { Get-Content -LiteralPath $_ } |
        Where-Object { $_ -match $pattern } | Select-Object -Last 1
    if (-not $line) { return 'unknown' }
    $null = $line -match $pattern
    $loaded = [int]$Matches[1]; $total = [int]$Matches[2]
    if ($total -gt 0 -and $loaded -eq $total) { return 'full' }
    return 'partial'
}

# Keeps the script alive as long as the host runs; ends with the host's exit code.
function Wait-Host {
    # Polled, not WaitForExit(): a blocking wait cannot be interrupted, so Ctrl+C would skip the clean-up in the finally block below.
    while (-not $script:Server.HasExited) { Start-Sleep -Milliseconds 500 }
    $script:Server.WaitForExit()
    $code = $script:Server.ExitCode
    Say "the host ended with code $code"
    $script:Server = $null
    return $code
}

function Invoke-Cpu {
    if (-not (Test-Binary $CpuBin)) { Stop-Launcher "'$CpuBin' was not found. Install llama.cpp (llama-server) or set MODEL_HOST_CPU_BIN." 127 }
    Start-Host $CpuBin 'cpu'
    if (-not (Wait-Healthy)) {
        $script:ServerLogs | Where-Object { Test-Path $_ } | ForEach-Object { Get-Content -LiteralPath $_ -Tail 20 | ForEach-Object { [Console]::Error.WriteLine($_) } }
        Stop-Host
        Stop-Launcher 'the CPU build did not become healthy.'
    }
    Say 'the host is ready on the cpu'
    return (Wait-Host)
}

function Invoke-GpuForced {
    if (-not (Test-Binary $GpuBin)) { Stop-Launcher "'$GpuBin' was not found. Install a GPU build of llama.cpp or set MODEL_HOST_GPU_BIN." 127 }
    Start-Host $GpuBin 'gpu'
    if (-not (Wait-Healthy)) {
        $script:ServerLogs | Where-Object { Test-Path $_ } | ForEach-Object { Get-Content -LiteralPath $_ -Tail 20 | ForEach-Object { [Console]::Error.WriteLine($_) } }
        Stop-Host
        Stop-Launcher 'the GPU build did not become healthy.'
    }
    if ((Get-OffloadState) -ne 'full') { Say 'warning: not every layer is offloaded to the GPU' }
    Say 'the host is ready on the gpu'
    return (Wait-Host)
}

function Invoke-Auto {
    if (-not (Test-GpuPresent)) {
        Say 'no GPU found'
        return (Invoke-Cpu)
    }
    if (-not (Test-Binary $GpuBin)) {
        Say "'$GpuBin' was not found, trying the CPU build"
        return (Invoke-Cpu)
    }

    Start-Host $GpuBin 'gpu'
    if (Wait-Healthy) {
        switch (Get-OffloadState) {
            'full' {
                Say 'the host is ready on the gpu'
                return (Wait-Host)
            }
            'partial' { Say 'not every layer is offloaded to the GPU, falling back to the CPU build' }
            default { Say 'the start log does not confirm that every layer is offloaded, falling back to the CPU build' }
        }
    } else {
        Say 'the GPU build did not start, falling back to the CPU build'
    }
    Stop-Host
    return (Invoke-Cpu)
}

# Ctrl+C: PowerShell raises a stop, the finally block below stops the host and removes the logs.
try {
    $exitCode = switch ($Device) {
        'cpu' { Invoke-Cpu }
        'gpu' { Invoke-GpuForced }
        default { Invoke-Auto }
    }
} finally {
    Stop-Host
    Remove-Item -LiteralPath $LogDir -Recurse -Force -ErrorAction SilentlyContinue
}
exit $exitCode
