# ============================================================
# 01-prepare.ps1 - Server preflight (run on the Windows Server as Administrator)
# Checks: C: free space, .NET 8 runtime, nginx/nssm tools, SSL cert files.
# ============================================================
param([switch]$SkipSecretsCheck)
$ErrorActionPreference = "Continue"
. (Join-Path $PSScriptRoot "00-params.ps1")
if (-not $SkipSecretsCheck) { $null = Test-ParamsSecrets }

Write-Host "=== MES deployment preflight ===" -ForegroundColor Cyan

# 1. System drive C: free space (single 60GB disk - trial)
$d = Get-PSDrive -Name ($DataDrive.TrimEnd(':')) -ErrorAction SilentlyContinue
if (-not $d) {
    Write-Host "[STEP 1] Drive $DataDrive not found?!" -ForegroundColor Red
    exit 1
} else {
    $freeGb = [math]::Round($d.Free / 1GB, 1)
    Write-Host "[STEP 1] System drive $DataDrive : $freeGb GB free"
    if ($freeGb -lt 15) {
        Write-Host "        [WARN] Less than 15GB free - SQL + backups will not fit comfortably." -ForegroundColor Yellow
        Write-Host "        Free up space or watch disk usage during the trial." -ForegroundColor Yellow
    }
}

# 2. .NET 8 ASP.NET Core runtime
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host "[STEP 2] dotnet not found. Install the ASP.NET Core 8.0 Runtime (x64) from"
    Write-Host "        https://dotnet.microsoft.com/download/dotnet/8.0  (aspnetcore-runtime, NOT just runtime, NOT sdk)"
    Write-Host "        then reopen PowerShell and rerun."
    exit 1
}
$aspNet = & dotnet --list-runtimes 2>$null | Select-String "Microsoft.AspNetCore.App 8\.0" | Select-Object -First 1
if (-not $aspNet) {
    Write-Host "[STEP 2] ASP.NET Core 8.0 runtime missing (only .NET runtime present)." -ForegroundColor Yellow
    Write-Host "        Install 'ASP.NET Core Runtime 8.x' (aspnetcore-runtime-8.0.x-win-x64.exe)."
    exit 1
} else {
    Write-Host "[STEP 2] ASP.NET Core 8 runtime OK: $($aspNet.Line.Trim())"
}

# 3. nssm
if (-not (Test-Path $NssmExe)) {
    Write-Host "[STEP 3] NSSM not found at $NssmExe" -ForegroundColor Yellow
    Write-Host "        Download https://nssm.cc/download  (latest zip), extract nssm.exe into $ToolsDir\nssm\"
    Write-Host "        Expected path: $NssmExe"
    exit 1
} else { Write-Host "[STEP 3] nssm OK: $NssmExe" }

# 4. nginx
if (-not (Test-Path $NginxExe)) {
    Write-Host "[STEP 4] nginx not found at $NginxExe" -ForegroundColor Yellow
    Write-Host "        Download https://nginx.org/en/download.html (Windows stable >=1.25 to get .wasm mime)"
    Write-Host "        Unzip and copy the CONTENTS into $NginxDir (so nginx.exe sits directly under it)."
    exit 1
} else { Write-Host "[STEP 4] nginx OK: $NginxExe" }

# 5. SSL cert files
$crt = Get-ChildItem "$SslDir\*.crt" -ErrorAction SilentlyContinue
$key = Get-ChildItem "$SslDir\*.key" -ErrorAction SilentlyContinue
if (-not $crt -or -not $key) {
    Write-Host "[STEP 5] SSL cert not found in $SslDir" -ForegroundColor Yellow
    Write-Host "        Tencent Cloud -> SSL Certificates -> Apply free DV cert for $Domain"
    Write-Host "        Download type: 'Nginx' -> place the .crt and .key files into $SslDir"
    Write-Host "        Rerun after placing both files."
    exit 1
} else {
    Write-Host "[STEP 5] SSL cert OK: $($crt[0].Name), $($key[0].Name)"
}

# 6. Create runtime dirs
New-Item -ItemType Directory -Force -Path $ApiDir,$WebDir,$LogsDir,$SqlDataDir,$SqlBackupDir,$PublishArchive | Out-Null
Write-Host "[STEP 6] Directories ensured under $MesRoot, $SqlDataDir, $SqlBackupDir"

Write-Host "=== Preflight done. Next: install SQL Server 2022 (see README 3.x), then run 02-init-db.ps1 ===" -ForegroundColor Green
