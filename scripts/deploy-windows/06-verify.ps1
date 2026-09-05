# ============================================================
# 06-verify.ps1 - Smoke-test the public site (run anywhere with internet)
# Checks: home page, PWA manifest, API reachability, Hangfire auth.
# ============================================================
param([string]$BaseUrl = "https://zhz.js.cn")
$ErrorActionPreference = "Continue"

function Http-Code([string]$Method, [string]$Url, [string]$Body = $null) {
    $args = @("-s","-o","NUL","-w","%{http_code}","--noproxy","*","-X",$Method,"-k",$Url)
    if ($Body) { $args += @("-H","Content-Type: application/json","-d",$Body) }
    return (& curl.exe @args 2>$null)
}

Write-Host "=== Smoke test $BaseUrl ===" -ForegroundColor Cyan

$home    = Http-Code "GET"  "$BaseUrl/"
$manifest= Http-Code "GET"  "$BaseUrl/manifest.json"
$login   = Http-Code "POST" "$BaseUrl/api/auth/login" "{}"
$hangfire= Http-Code "GET"  "$BaseUrl/hangfire"

$rows = @(
    @{Check="Home page  (expect 200)";        Code=$home},
    @{Check="PWA manifest (expect 200)";      Code=$manifest},
    @{Check="API /api/auth/login (expect 400)";Code=$login},
    @{Check="Hangfire (expect 401=protected)"; Code=$hangfire}
)
$ok = $true
foreach ($r in $rows) {
    $exp = @("200") ; if ($r.Code -in @("400","401")) { $exp = @("400","401") }
    $pass = $r.Code -in $exp
    if (-not $pass) { $ok = $false }
    Write-Host ("  [{0}] {1}  -> {2}" -f $(if($pass){"OK "}else{"FAIL"}), $r.Check, $r.Code)
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
