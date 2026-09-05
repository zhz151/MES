# ============================================================
# 04-setup-api.ps1 - Register MES.API as a Windows service via NSSM
# Run on the SERVER as Administrator, AFTER:
#   - C:\mes\api  contains the published MES.Api (MES.Api.dll present)
#   - empty database MES exists (02-init-db.ps1 done)
# NOTE: verify the API ONCE in the foreground before starting the service.
# ============================================================
param([switch]$StartNow)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "00-params.ps1")
if (-not (Test-ParamsSecrets)) {
    Write-Host "Aborting: edit 00-params.ps1 secrets first." -ForegroundColor Red
    exit 1
}

# ---- checks ----
if (-not (Test-Path "$ApiDir\MES.Api.dll")) {
    Write-Host "[FATAL] MES.Api.dll not found at $ApiDir\MES.Api.dll" -ForegroundColor Red
    Write-Host "        Unzip the 03-publish package into C:\mes\  first (api\ -> $ApiDir)."
    exit 1
}
if (-not (Test-Path $NssmExe)) { Write-Host "[FATAL] nssm not found: $NssmExe"; exit 1 }
New-Item -ItemType Directory -Force -Path $LogsDir | Out-Null

$dotnet = (Get-Command dotnet).Source

if (Get-Service $ApiServiceName -ErrorAction SilentlyContinue) {
    Write-Host "[WARN] service '$ApiServiceName' already exists." -ForegroundColor Yellow
} else {
    Write-Host "=== Installing service '$ApiServiceName' ===" -ForegroundColor Cyan
    & $NssmExe install $ApiServiceName $dotnet "$ApiDir\MES.Api.dll"
    if ($LASTEXITCODE -ne 0) { Write-Host "[FATAL] nssm install failed"; exit 1 }
}

# ---- config ----
& $NssmExe set $ApiServiceName AppDirectory $ApiDir
& $NssmExe set $ApiServiceName AppStdout "$LogsDir\api.out.log"
& $NssmExe set $ApiServiceName AppStderr "$LogsDir\api.err.log"
& $NssmExe set $ApiServiceName AppStdoutCreationDisposition 4     # append
& $NssmExe set $ApiServiceName AppStderrCreationDisposition 4
& $NssmExe set $ApiServiceName AppExit Default Restart
& $NssmExe set $ApiServiceName AppRestartDelay 5000

$envs = @(
    "MES_CONNECTION_STRING=$ConnectionString",
    "ASPNETCORE_URLS=$ApiBindUrl",
    "ASPNETCORE_ENVIRONMENT=Production",
    "JwtSettings__Secret=$JwtSecret",
    "Seed__AdminPassword=$AdminPassword",
    "Hangfire__Username=$HangfireUser",
    "Hangfire__Password=$HangfirePassword",
    "CorsOrigins=$CorsOrigins"
)
& $NssmExe set $ApiServiceName AppEnvironmentExtra $envs
if ($LASTEXITCODE -ne 0) { Write-Host "[WARN] AppEnvironmentExtra set may have partially failed. Verify via: nssm dump $ApiServiceName" -ForegroundColor Yellow }

Write-Host ""
Write-Host "=== IMPORTANT: verify ONCE in foreground before starting the service ===" -ForegroundColor Yellow
Write-Host "Open a NEW PowerShell window as Administrator and run:"
Write-Host "    cd $ApiDir"
Write-Host "    `$env:MES_CONNECTION_STRING = '$ConnectionString'"
Write-Host "    `$env:ASPNETCORE_URLS = '$ApiBindUrl'"
Write-Host "    `$env:ASPNETCORE_ENVIRONMENT = 'Production'"
Write-Host "    `$env:JwtSettings__Secret = '$JwtSecret'"
Write-Host "    `$env:Seed__AdminPassword = '$AdminPassword'"
Write-Host "    dotnet MES.Api.dll"
Write-Host "Wait until you see no error and the app stays listening (first run creates all tables + seeds 46 roles + Admin). Then press Ctrl+C and come back here."
Write-Host ""

$answer = Read-Host "Have you verified the foreground run successfully? [y/N]"
if ($answer -notmatch '^[yY]') {
    Write-Host "Service NOT started. Rerun this script after foreground verification." -ForegroundColor Yellow
    exit 1
}

& $NssmExe start $ApiServiceName
if ($LASTEXITCODE -ne 0) { Write-Host "[FATAL] failed to start service. Check $LogsDir\api.err.log"; exit 1 }
Start-Sleep -Seconds 3
Write-Host "[OK] service '$ApiServiceName' started." -ForegroundColor Green
Write-Host "    logs: $LogsDir\api.out.log / api.err.log"
Write-Host "    dump: nssm dump $ApiServiceName"
