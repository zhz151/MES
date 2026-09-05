# ============================================================
# 05-setup-nginx.ps1 - Generate nginx.conf and register nginx as a service
# Run on the SERVER as Administrator, AFTER:
#   - nginx.exe present at $NginxDir (see 01-prepare)
#   - SSL .crt/.key present in $NginxDir\conf\ssl
#   - C:\mes\web  contains the Blazor static content (index.html present)
# ============================================================
param([switch]$SkipSecretsCheck)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "00-params.ps1")
if (-not $SkipSecretsCheck) { $null = Test-ParamsSecrets }

# ---- checks ----
if (-not (Test-Path $NginxExe))            { Write-Host "[FATAL] nginx not found: $NginxExe"; exit 1 }
if (-not (Test-Path $NginxConfTemplate))   { Write-Host "[FATAL] template not found: $NginxConfTemplate"; exit 1 }
if (-not (Test-Path "$WebDir\index.html")) { Write-Host "[FATAL] $WebDir\index.html missing - unzip the web part of the 03 package to $WebDir"; exit 1 }

$crt = Get-ChildItem "$SslDir\*.crt" -ErrorAction SilentlyContinue | Select-Object -First 1
$key = Get-ChildItem "$SslDir\*.key" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $crt -or -not $key) { Write-Host "[FATAL] place .crt and .key into $SslDir"; exit 1 }

# ---- generate conf ----
# NOTE: ssl cert/key are injected as ABSOLUTE paths. nginx resolves relative
# paths in a config against the directory of nginx.conf (not the prefix), so a
# relative "conf/ssl/..." would wrongly become ".../conf/conf/ssl/...".
$conf = Get-Content $NginxConfTemplate -Raw
$conf = $conf.Replace('{DOMAIN}',  $Domain)
$sslAbs = $SslDir.Replace('\','/')
$conf = $conf.Replace('{SSL_CERT_ABS}', "$sslAbs/$($crt.Name)")
$conf = $conf.Replace('{SSL_KEY_ABS}',  "$sslAbs/$($key.Name)")
$conf = $conf.Replace('{WEBROOT}',  $WebDir.Replace('\','/'))
$conf = $conf.Replace('{API_PORT}', ($ApiBindUrl -replace '^http://127\.0\.0\.1:',''))
$out = "$NginxDir\conf\nginx.conf"
New-Item -ItemType Directory -Force -Path "$NginxDir\conf" | Out-Null
Set-Content -Path $out -Value $conf -Encoding ASCII
Write-Host "[OK] generated $out"

# ---- test config ----
Push-Location $NginxDir
try {
    & "$NginxExe" -t -p "$NginxDir\"
    if ($LASTEXITCODE -ne 0) { Write-Host "[FATAL] nginx -t failed; fix config/ssl and rerun." -ForegroundColor Red; exit 1 }
} finally { Pop-Location }
Write-Host "[OK] nginx -t passed"

# ---- register service ----
if (Get-Service $NginxServiceName -ErrorAction SilentlyContinue) {
    Write-Host "[WARN] service '$NginxServiceName' already exists; reloading instead." -ForegroundColor Yellow
    & "$NginxExe" -s reload -p "$NginxDir\"
} else {
    if (-not (Test-Path $NssmExe)) { Write-Host "[FATAL] nssm not found: $NssmExe"; exit 1 }
    & $NssmExe install $NginxServiceName $NginxExe
    & $NssmExe set $NginxServiceName AppDirectory $NginxDir
    & $NssmExe set $NginxServiceName AppExit Default Restart
    & $NssmExe start $NginxServiceName
    if ($LASTEXITCODE -ne 0) { Write-Host "[FATAL] nginx service start failed." -ForegroundColor Red; exit 1 }
}
Start-Sleep -Seconds 2
Write-Host "[OK] nginx is running." -ForegroundColor Green
Write-Host "    Next steps: open firewall/security-group for 80/443, then run 06-verify.ps1."
