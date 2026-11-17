<#
.SYNOPSIS
    Registers the monitor as a Windows service (docs/guidelines/operations.md 14.3, Windows service).

.DESCRIPTION
    The published executable runs with UseWindowsService(), so the service manager starts and stops it and its log
    goes to the Windows event log. Publish first:

        dotnet publish src/support/MyApp.Monitor -c Release -r win-x64 --self-contained -o C:\MyApp\Monitor

    Copy the operations pack (pack build --profile onprem) to C:\MyApp\Monitor\pack, then run this script as an
    administrator. Secrets are not written into the service definition: they are machine-level environment variables
    (Monitor__Masking__HashKey, Monitor__Connections__main-db, Monitor__Notifications__Smtp__Password, ...) that the
    script reads from the current session and stores for the service account only.

    Notes
      - Run the service as a low-privilege account that can read the application's log folder and write to -DataPath.
      - The monitor never prompts. A missing secret or an invalid pack stops the start with one Critical entry in the
        event log (source MyApp.Monitor).
      - Dashboard: dashboard.auth "local" binds to loopback only. To serve other machines, set
        Monitor__Settings__DashboardAuth=entra, Monitor__Dashboard__Urls, Monitor__Settings__DashboardGroupId and the
        Monitor__Dashboard__AzureAd__* values.
      - Stop with `Stop-Service MyApp.Monitor`: the batch in flight is finished and its checkpoint saved.
#>
[CmdletBinding()]
param(
    [string] $InstallPath = 'C:\MyApp\Monitor',
    [string] $DataPath = 'C:\MyApp\Monitor\data',
    [string] $Profile = 'onprem',
    [string] $ServiceName = 'MyApp.Monitor',
    [string] $Account = 'NT SERVICE\MyApp.Monitor',
    [string] $LogGlob = ''
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $InstallPath 'MyApp.Monitor.exe'
$pack = Join-Path $InstallPath 'pack'
if (-not (Test-Path $exe)) { throw "MyApp.Monitor.exe not found in $InstallPath. Publish the monitor first." }
if (-not (Test-Path $pack)) { throw "The operations pack is missing: $pack" }
if (-not $env:Monitor__Masking__HashKey -or $env:Monitor__Masking__HashKey.Length -lt 32) {
    throw 'Set Monitor__Masking__HashKey (at least 32 bytes) in this session: the monitor refuses to start without it.'
}

New-Item -ItemType Directory -Force -Path $DataPath | Out-Null

$binary = '"{0}" run --profile {1} --pack "{2}"' -f $exe, $Profile, $pack
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}
New-Service -Name $ServiceName -BinaryPathName $binary -DisplayName 'MyApp monitor' `
    -Description 'Incident loop, dashboard and notifications (operations guidelines section 14).' -StartupType Automatic | Out-Null

# restart after a failure: 10 s, 60 s, then every 5 minutes; reset the count after a day
sc.exe failure $ServiceName reset= 86400 actions= restart/10000/restart/60000/restart/300000 | Out-Null

# environment of the service (REG_MULTI_SZ under the service key); secrets come from the current session
$settings = @(
    "Monitor__ContentRoot=$DataPath",
    "Monitor__Masking__HashKey=$($env:Monitor__Masking__HashKey)"
)
if ($LogGlob) { $settings += "Monitor__Settings__LogGlob=$LogGlob" }
foreach ($name in 'Monitor__Connections__main-db', 'Monitor__Notifications__Smtp__Username', 'Monitor__Notifications__Smtp__Password',
                  'Monitor__Notifications__Webhook__Authorization', 'Monitor__Settings__ApiBaseUrl', 'Monitor__Settings__MailTo',
                  'Monitor__Settings__EmailEnabled', 'Monitor__Settings__SmtpHost') {
    $value = [Environment]::GetEnvironmentVariable($name)
    if ($value) { $settings += "$name=$value" }
}
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName" -Name Environment -Value $settings -Type MultiString

# the service account reads the application's log folder (grant that yourself) and writes only its own data folder
icacls $DataPath /grant "${Account}:(OI)(CI)M" | Out-Null

Start-Service -Name $ServiceName
Get-Service -Name $ServiceName | Format-Table Name, Status, StartType
Write-Host "Dashboard: http://localhost:5310 (loopback only unless dashboard.auth is entra)."
