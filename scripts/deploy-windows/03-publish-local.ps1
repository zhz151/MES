# ============================================================
# 03-publish-local.ps1 - Run on the DEV machine (has .NET SDK)
#   1) dotnet publish MES.Api + MES.Blazor (Release)
#   2) rewrite published Blazor appsettings.json ApiSettings:BaseUrl to "" (same-origin /api)
#   3) zip both into a single file and print the path to upload
# ============================================================
param(
    [string]$OutZip = ""   # optional custom zip output path
)
$ErrorActionPreference = "Stop"

# locate repo root = two levels above this script
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $root

$pubApi  = Join-Path $root "publish\api"
$pubBlz  = Join-Path $root "publish\blazor"

Write-Host "=== 1/3 Publishing MES.Api ===" -ForegroundColor Cyan
dotnet publish "$root\MES.Api\MES.Api.csproj"    -c Release -o $pubApi   --nologo
if ($LASTEXITCODE -ne 0) { throw "MES.Api publish failed" }

Write-Host "=== 2/3 Publishing MES.Blazor ===" -ForegroundColor Cyan
dotnet publish "$root\MES.Blazor\MES.Blazor.csproj" -c Release -o $pubBlz --nologo
if ($LASTEXITCODE -ne 0) { throw "MES.Blazor publish failed" }

# rewrite BaseUrl to ""  (published file at publish/blazor/wwwroot/appsettings.json)
$appJson = Join-Path $pubBlz "wwwroot\appsettings.json"
if (-not (Test-Path $appJson)) { throw "published appsettings.json not found: $appJson" }
$json = Get-Content $appJson -Raw | ConvertFrom-Json
$json.ApiSettings.BaseUrl = ""
$json | ConvertTo-Json -Depth 5 | Set-Content $appJson -Encoding UTF8
Write-Host "    BaseUrl rewritten to '' (same-origin /api) at $appJson"

# sanity: blazor publish contains PWA files
foreach ($f in @("index.html","manifest.json","service-worker.js","service-worker.published.js","_framework\blazor.webassembly.js")) {
    if (-not (Test-Path (Join-Path $pubBlz "wwwroot\$f"))) {
        Write-Host "    [WARN] missing PWA asset in publish: $f" -ForegroundColor Yellow
    }
}

Write-Host "=== 3/3 Packaging ===" -ForegroundColor Cyan
if (-not $OutZip) { $OutZip = Join-Path $env:TEMP ("mes-deploy-{0:yyyyMMdd_HHmm}.zip" -f (Get-Date)) }
if (Test-Path $OutZip) { Remove-Item $OutZip -Force }

# Stage zip so top-level folders are:  api\  and  web\  (web = blazor wwwroot content)
$stage = Join-Path $root "publish\_stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path "$stage\api", "$stage\web" | Out-Null
Copy-Item -Path "$pubApi\*"   -Destination "$stage\api" -Recurse
Copy-Item -Path "$pubBlz\wwwroot\*" -Destination "$stage\web" -Recurse
Push-Location $stage
try {
    Compress-Archive -Path "api", "web" -DestinationPath $OutZip -CompressionLevel Optimal
} finally { Pop-Location }
Remove-Item $stage -Recurse -Force

Write-Host "[OK] package created:" -ForegroundColor Green
Write-Host "    $OutZip"
Write-Host ""
Write-Host "Upload it to the server (RDP copy or pscp), then on the server unzip directly to C:\mes\ :"
Write-Host "    api\  -> C:\mes\api     (MES.Api.dll sits in C:\mes\api\)"
Write-Host "    web\  -> C:\mes\web     (index.html sits in C:\mes\web\)"
Write-Host "Then run 04-setup-api.ps1 on the server."
