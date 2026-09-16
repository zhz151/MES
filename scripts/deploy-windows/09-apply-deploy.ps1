# ============================================================
# 09-apply-deploy.ps1 - apply a deploy package. Run ON THE SERVER as Administrator:
#
#     powershell -ExecutionPolicy Bypass -File C:\mes\deploy\09-apply-deploy.ps1
#
# Set the package name in the $Zip default below, or pass it on the command line:
#
#     powershell -ExecutionPolicy Bypass -File C:\mes\deploy\09-apply-deploy.ps1 -Zip C:\mes\mes-deploy-20260916_1658.zip
#
# What it does:
#   1) check the package exists
#   2) stop MES.API and wait until the service is Stopped AND port 7000 is released
#   3) back up C:\mes\api and C:\mes\web to C:\mes\_backup\<timestamp>\ (+ ROLLBACK.txt)
#   4) expand to C:\mes\_stage and verify the payload BEFORE touching anything:
#        - api\MES.Api.dll / web\index.html / web\_framework\blazor.boot.json exist
#        - ROUTE GATE on web\_framework\MES.Blazor.wasm (see note below)
#      Any failure aborts here, with api/web still untouched.
#   5) replace api/web (drops the stale web\_framework folder first)
#   6) start MES.API, wait for the port, poll /api/health, print the deployed
#      front-end asset version string
#
# ROUTE GATE - why it exists (2026-09-14 outage):
#   A batch edit once stripped the leading '@' from '@page "..."' in 95 .razor
#   files, turning the directive into plain text. The result COMPILED WITH 0
#   ERRORS AND 0 WARNINGS and every unit test still passed, but no route was
#   registered, so every page fell through to App.razor's <NotFound> and the
#   whole site showed "page not found". The marker 'page "/' (without the '@')
#   survives into the compiled wasm as a user string literal, so searching for
#   it is a cheap, reliable gate.
#
# NOTE - KEEP THIS FILE PURE ASCII.
#   Windows PowerShell 5.1 reads a BOM-less .ps1 as ANSI. Non-ASCII characters
#   mis-decode, can swallow the end-of-line, and silently merge the NEXT code
#   line into a comment (that line then never runs). All text below is ASCII.
# ============================================================
param(
    [string]$Zip         = "C:\mes\mes-deploy-20260916_1658.zip",
    [string]$MesRoot     = "C:\mes",
    [string]$ServiceName = "MES.API",
    [string]$Nssm        = "C:\mes\tools\nssm\nssm.exe",
    [int]   $ApiPort     = 7000
)

$ErrorActionPreference = "Stop"

function Step([string]$m) { Write-Host ("== " + $m) -ForegroundColor Cyan }
function Fail([string]$m) {
    Write-Host ("[FAIL] " + $m) -ForegroundColor Red
    exit 1
}

# Searches a compiled Blazor wasm for a string literal.
# .NET stores user string literals as UTF-16LE in the metadata #US heap, so a
# plain ASCII byte search ALWAYS misses; the two passes below cover both even
# and odd heap alignments.
function Test-Utf16Literal {
    param([string]$Path, [string]$Marker)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ([System.Text.Encoding]::Unicode.GetString($bytes).IndexOf($Marker) -ge 0) { return $true }
    return ([System.Text.Encoding]::Unicode.GetString($bytes, 1, $bytes.Length - 1).IndexOf($Marker) -ge 0)
}

$apiDir = Join-Path $MesRoot "api"
$webDir = Join-Path $MesRoot "web"

# ---------- 1) package ----------
Step "1/6 Checking package"
if (-not (Test-Path $Zip)) { Fail ("package not found: " + $Zip) }
$item = Get-Item $Zip
Write-Host ("   " + $item.FullName)
Write-Host ("   " + [math]::Round($item.Length / 1MB, 1) + " MB, modified " + $item.LastWriteTime)

# ---------- 2) stop ----------
Step ("2/6 Stopping " + $ServiceName)
if (Test-Path $Nssm) { & $Nssm stop $ServiceName | Out-Null }
else { Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue }

# 'nssm stop' returns immediately; starting too early makes Kestrel die with
# "Failed to bind to address http://127.0.0.1:7000: address already in use".
$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 1
    $svc  = Get-Service $ServiceName -ErrorAction SilentlyContinue
    $busy = Get-NetTCPConnection -LocalPort $ApiPort -State Listen -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -eq "Stopped" -and -not $busy) { $ready = $true; break }
}
if (-not $ready) { Fail ($ServiceName + " did not stop / port " + $ApiPort + " still busy after 30s - aborted, nothing changed") }
Write-Host "   stopped, port released"

# ---------- 3) rollback backup ----------
Step "3/6 Backing up current api and web"
$stamp  = Get-Date -Format "yyyyMMdd_HHmmss"
$backup = Join-Path $MesRoot ("_backup\" + $stamp)
New-Item -ItemType Directory -Force -Path $backup | Out-Null
foreach ($d in @("api", "web")) {
    $src = Join-Path $MesRoot $d
    if (Test-Path $src) {
        $dst = Join-Path $backup $d
        Copy-Item -Path $src -Destination $dst -Recurse -Force
        Write-Host ("   " + $src + "  ->  " + $dst)
    }
}
$rollback = Join-Path $backup "ROLLBACK.txt"
Set-Content -Path $rollback -Encoding ASCII -Value @(
    "Rollback this deployment (run as Administrator):",
    "",
    "  " + $Nssm + " stop " + $ServiceName,
    "  Remove-Item '" + $apiDir + "' -Recurse -Force",
    "  Remove-Item '" + $webDir + "' -Recurse -Force",
    "  Copy-Item '" + (Join-Path $backup "api") + "' '" + $apiDir + "' -Recurse -Force",
    "  Copy-Item '" + (Join-Path $backup "web") + "' '" + $webDir + "' -Recurse -Force",
    "  " + $Nssm + " start " + $ServiceName
)
Write-Host ("   rollback notes: " + $rollback)

# ---------- 4) expand + verify ----------
Step "4/6 Expanding and verifying package"
$stage = Join-Path $MesRoot "_stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
Expand-Archive -Path $Zip -DestinationPath $stage -Force

$need = @(
    (Join-Path $stage "api\MES.Api.dll"),
    (Join-Path $stage "web\index.html"),
    (Join-Path $stage "web\_framework\blazor.boot.json")
)
foreach ($p in $need) {
    if (-not (Test-Path $p)) { Fail ("package incomplete, missing: " + $p + " - api/web NOT touched") }
}
Write-Host "   payload files present"

$wasm = Join-Path $stage "web\_framework\MES.Blazor.wasm"
if (-not (Test-Path $wasm)) { Fail ("package incomplete, missing: " + $wasm + " - api/web NOT touched") }
if (Test-Utf16Literal -Path $wasm -Marker 'page "/') {
    Fail ("ROUTE GATE FAILED: this build lost its Razor @page directives - every page would show 'page not found'. api/web NOT touched.")
}
Write-Host "   route gate passed (no lost @page directives)"

# ---------- 5) replace ----------
Step "5/6 Replacing api and web"
$fw = Join-Path $webDir "_framework"
if (Test-Path $fw) { Remove-Item $fw -Recurse -Force }
Copy-Item -Path (Join-Path $stage "api\*") -Destination $apiDir -Recurse -Force
Copy-Item -Path (Join-Path $stage "web\*") -Destination $webDir -Recurse -Force
Remove-Item $stage -Recurse -Force
Write-Host "   api and web replaced"

# ---------- 6) start + verify ----------
Step ("6/6 Starting " + $ServiceName)
if (Test-Path $Nssm) { & $Nssm start $ServiceName | Out-Null }
else { Start-Service $ServiceName }

$up = $false
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 1
    if (Get-NetTCPConnection -LocalPort $ApiPort -State Listen -ErrorAction SilentlyContinue) { $up = $true; break }
}
if (-not $up) {
    Write-Host ("[FAIL] port " + $ApiPort + " not listening after 60s - files were already replaced.") -ForegroundColor Red
    Write-Host ("       To roll back, run the commands in: " + $rollback) -ForegroundColor Yellow
    Get-Service $ServiceName
    exit 1
}
Write-Host ("   listening on 127.0.0.1:" + $ApiPort)

# /api/health is anonymous, so the probe needs no token. Warn (not fail) when the
# endpoint is absent: older packages predate it.
try {
    $h = Invoke-WebRequest -Uri ("http://127.0.0.1:" + $ApiPort + "/api/health") -UseBasicParsing -TimeoutSec 30
    Write-Host ("   health HTTP " + [int]$h.StatusCode + " " + $h.Content)
} catch {
    $code = $null
    if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
    if ($code) { Write-Host ("   [WARN] health HTTP " + $code + " (service up, but health check not healthy)") -ForegroundColor Yellow }
    else { Write-Host ("   [WARN] health probe failed: " + $_.Exception.Message) -ForegroundColor Yellow }
}

$idx = Join-Path $webDir "index.html"
$ver = (Select-String -Path $idx -Pattern 'app\.css\?v=\d+' -AllMatches |
        ForEach-Object { $_.Matches.Value }) -join ", "
Write-Host ("   deployed front-end assets: " + $ver)
Write-Host "   (browsers cache css/app.css for a year - hard refresh with Ctrl+F5 if styles look stale)"

Write-Host ""
Write-Host "[OK] deploy finished" -ForegroundColor Green
Write-Host ("     rollback backup: " + $backup)
Get-Service $ServiceName
