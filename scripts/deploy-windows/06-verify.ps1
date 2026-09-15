# ============================================================
# 06-verify.ps1 - Smoke-test the public site (run anywhere with internet)
# Checks: home page, PWA manifest, API reachability, /api/health, Hangfire auth,
#         and a Blazor ROUTE GATE on the deployed wasm.
#
# ROUTE GATE - why it exists (2026-09-14 outage):
#   A batch edit once stripped the leading '@' from '@page "..."' in 95 .razor
#   files, turning the directive into plain text. The result COMPILED WITH 0
#   ERRORS AND 0 WARNINGS and every unit test still passed, but no route was
#   registered, so every page fell through to App.razor's <NotFound> and the
#   whole site showed "page not found".
#   The four HTTP-code checks in this script ALL PASSED during that outage -
#   nginx's SPA fallback keeps "/" at 200 no matter what. The wasm marker check
#   below is what actually catches it.
#
# NOTE - KEEP THIS FILE PURE ASCII.
#   Windows PowerShell 5.1 reads a BOM-less .ps1 as ANSI. Non-ASCII characters
#   mis-decode, can swallow the end-of-line, and silently merge the NEXT code
#   line into a comment (that line then never runs). All text below is ASCII.
# ============================================================
param([string]$BaseUrl = "https://zhz.js.cn")
$ErrorActionPreference = "Continue"

function Http-Code([string]$Method, [string]$Url, [string]$Body = $null) {
    # NOTE: named $curlArgs on purpose - $args is an automatic variable (unbound function args).
    $curlArgs = @("-s","-o","NUL","-w","%{http_code}","--noproxy","*","-X",$Method,"-k",$Url)
    if ($Body) { $curlArgs += @("-H","Content-Type: application/json","-d",$Body) }
    return (& curl.exe @curlArgs 2>$null)
}

# Same as Http-Code but returns the RESPONSE BODY instead of the status code.
# Needed because this API always answers HTTP 200 and carries the business status
# inside the ApiResponse body ("success"/"code") - see the business-code gate below.
function Http-Body([string]$Method, [string]$Url, [string]$Body = $null) {
    $curlArgs = @("-s","--noproxy","*","-X",$Method,"-k",$Url)
    if ($Body) { $curlArgs += @("-H","Content-Type: application/json","-d",$Body) }
    return (& curl.exe @curlArgs 2>$null)
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

Write-Host "=== Smoke test $BaseUrl ===" -ForegroundColor Cyan

# NOTE: do NOT name this $home - PowerShell variable names are case-insensitive and
# $HOME is a read-only automatic variable, which makes the assignment fail.
$homeCode = Http-Code "GET"  "$BaseUrl/"
$manifest = Http-Code "GET"  "$BaseUrl/manifest.json"
$login    = Http-Code "POST" "$BaseUrl/api/auth/login" "{}"
$health   = Http-Code "GET"  "$BaseUrl/api/health"
$hangfire = Http-Code "GET"  "$BaseUrl/hangfire"
$loginBody = Http-Body "POST" "$BaseUrl/api/auth/login" "{}"

$rows = @(
    @{Check="Home page  (expect 200)";          Code=$homeCode; Exp=@("200")},
    @{Check="PWA manifest (expect 200)";        Code=$manifest; Exp=@("200")},
    @{Check="API /api/auth/login (expect 200)"; Code=$login;    Exp=@("200")},
    @{Check="API /api/health (expect 200)";     Code=$health;   Exp=@("200")},
    @{Check="Hangfire (expect 401=protected)";  Code=$hangfire; Exp=@("400","401")}
)
$ok = $true
foreach ($r in $rows) {
    $pass = $r.Code -in $r.Exp
    if (-not $pass) { $ok = $false }
    Write-Host ("  [{0}] {1}  -> {2}" -f $(if($pass){"OK "}else{"FAIL"}), $r.Check, $r.Code)
}

# --- Business-code gate ---------------------------------------------------
# This API answers HTTP 200 for BOTH success and business failure: the real
# status lives in the ApiResponse body as "success"/"code" (see
# MES.Core/Models/ApiResponse.cs). An empty login must come back with
# code 400/401 in the body - checking only the HTTP code cannot see this.
# History: this check used to assert HTTP 400/401 and therefore reported a
# false FAIL on every healthy deploy.
Write-Host ""
Write-Host "--- ApiResponse business-code gate ---" -ForegroundColor Cyan
if ($loginBody -match '"code"\s*:\s*(400|401)') {
    Write-Host "  [OK ] empty login answered code 400/401 in the body"
} else {
    Write-Host ("  [FAIL] empty login should answer code 400/401 in the body; got: " + $loginBody) -ForegroundColor Red
    $ok = $false
}

Write-Host ""
Write-Host "--- Blazor route gate (deployed wasm) ---" -ForegroundColor Cyan
$wasmUrl = "$BaseUrl/_framework/MES.Blazor.wasm"
$wasmTmp = Join-Path $env:TEMP "mes-verify-MES.Blazor.wasm"
$wasmCode = Http-Code "GET" $wasmUrl
if ($wasmCode -ne "200") {
    Write-Host ("  [FAIL] cannot download wasm -> HTTP " + $wasmCode) -ForegroundColor Red
    $ok = $false
} else {
    Remove-Item $wasmTmp -Force -ErrorAction SilentlyContinue
    & curl.exe -s -k --noproxy "*" -o $wasmTmp $wasmUrl 2>$null
    if (-not (Test-Path $wasmTmp)) {
        Write-Host "  [FAIL] wasm download produced no file" -ForegroundColor Red
        $ok = $false
    } else {
        $size = [math]::Round((Get-Item $wasmTmp).Length / 1MB, 1)
        Write-Host ("  downloaded " + $size + " MB")
        if (Test-Utf16Literal -Path $wasmTmp -Marker 'page "/') {
            Write-Host '  [FAIL] ROUTE GATE: wasm contains a lost-@page marker, so no routes are registered - every page will show "page not found"' -ForegroundColor Red
            Write-Host '         Fix the source, rebuild, and re-run 03-publish-local.ps1 + 09-apply-deploy.ps1' -ForegroundColor Yellow
            $ok = $false
        } else {
            Write-Host "  [OK ] route gate passed (no lost @page directives)"
        }
        Remove-Item $wasmTmp -Force -ErrorAction SilentlyContinue
    }
}

if ($ok) { Write-Host "=== All smoke checks passed ===" -ForegroundColor Green }
else     { Write-Host "=== Some checks FAILED - see above ===" -ForegroundColor Red }

Write-Host ""
Write-Host "Manual mobile/PWA verification:"
Write-Host "  1) Phone on the same WAN: open $BaseUrl  (expect padlock, HTTPS)"
Write-Host "  2) Log in with Admin (password = Seed__AdminPassword you set)"
Write-Host "  3) Browser menu -> 'Add to Home screen' / 'Install app' -> open standalone"
Write-Host "  4) Scan a workstation/batch QR with the camera flow (needs HTTPS)"
Write-Host "  5) Hangfire: $BaseUrl/hangfire with Hangfire__Username/Hangfire__Password"
Write-Host "  6) Click through the main menus once (the route gate above covers the"
Write-Host "     common failure mode, but only a human notices a single broken page)"
