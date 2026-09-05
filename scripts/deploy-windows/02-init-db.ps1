# ============================================================
# 02-init-db.ps1 - SQL Server init (run on server as Administrator)
#   Targets SQL Server 2022 EXPRESS, NAMED instance SQLEXPRESS (see 00-params).
#   1) cap SQL max server memory (Express auto-caps ~1.4GB; setting is harmless)
#   2) create empty database MES on C:\MSSQL\DATA  (MUST run before API first start)
# Grants the SQL service account write permission on C:\MSSQL.
# ============================================================
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "00-params.ps1")

function Resolve-SqlCmd {
    $c = Get-Command sqlcmd.exe -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    # common install paths for SQL Server 2022 (160) / 2019 (150)
    foreach ($v in @("160","150")) {
        foreach ($p in @("C:\Program Files\Microsoft SQL Server\$v\Tools\Binn\SQLCMD.EXE",
                         "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE")) {
            if (Test-Path $p) { return $p }
        }
    }
    return $null
}

$sqlcmd = Resolve-SqlCmd
if (-not $sqlcmd) {
    Write-Host "[FATAL] sqlcmd not found. Install 'SQL Server Command Line Utilities' or check SQL Server install." -ForegroundColor Red
    exit 1
}
Write-Host "[OK] sqlcmd: $sqlcmd"

# 0) ensure dirs exist
New-Item -ItemType Directory -Force -Path $SqlDataDir,$SqlBackupDir | Out-Null

# 0.5) preflight: make sure we can actually reach the (named) instance with Windows auth
& $sqlcmd -S $SqlServer -E -b -Q "SELECT 1" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "[FATAL] Cannot connect to SQL instance '$SqlServer' with Windows auth." -ForegroundColor Red
    Write-Host "       1) Confirm the instance name in 00-params.ps1 (this box ships SQLEXPRESS)." -ForegroundColor Yellow
    Write-Host "       2) Confirm the SQL Server service is Running (services.msc -> 'SQL Server (SQLEXPRESS)')." -ForegroundColor Yellow
    Write-Host "       3) For a NAMED instance, start the browser service, then rerun:" -ForegroundColor Yellow
    Write-Host "            Start-Service SQLBrowser" -ForegroundColor Yellow
    exit 1
}
Write-Host "[OK] connected to SQL instance $SqlServer"

# 1) grant the SQL engine service account NTFS rights on C:\MSSQL.
#    Discover the engine service: default instance service is 'MSSQLSERVER',
#    named instance (Express SQLEXPRESS) is 'MSSQL$SQLEXPRESS'. Excludes SQLAgent$.
$sqlSvc = Get-CimInstance Win32_Service | Where-Object { $_.Name -eq 'MSSQLSERVER' -or $_.Name -like 'MSSQL$*' } | Select-Object -First 1
$svcAcct = $null
if ($sqlSvc) { $svcAcct = $sqlSvc.StartName }
if (-not $svcAcct) {
    Write-Host "[WARN] No SQL Server engine service (MSSQLSERVER / MSSQL$...) found." -ForegroundColor Yellow
    Write-Host "       Target instance is $SqlServer - is SQL Server installed and started?" -ForegroundColor Yellow
} else {
    Write-Host "[OK] SQL service account: $svcAcct  (service: $($sqlSvc.Name))"
    $acct = ($svcAcct -split '\\')[-1]   # strip machine prefix if any
    if ($svcAcct -like "NT Service\*") { $grantName = $svcAcct } else { $grantName = $acct }
    icacls $SqlDataDir  /grant "${grantName}:(OI)(CI)M" 2>&1 | Out-Null
    icacls $SqlBackupDir /grant "${grantName}:(OI)(CI)M" 2>&1 | Out-Null
}

# 2) cap memory (Express edition auto-caps ~1.4GB regardless; the setting is harmless)
& $sqlcmd -S $SqlServer -E -b -Q "EXEC sp_configure 'show advanced options', 1; RECONFIGURE; EXEC sp_configure 'max server memory', 2048; RECONFIGURE;"
if ($LASTEXITCODE -ne 0) { Write-Host "[FATAL] failed to set max memory (are you sysadmin?)." -ForegroundColor Red; exit 1 }
Write-Host "[OK] SQL max server memory capped (Express auto-caps ~1.4GB, safe on this 4GB box)"

# 3) create empty database if not exists (files on C:)
$createSql = @"
IF DB_ID('$DbName') IS NULL
BEGIN
    CREATE DATABASE [$DbName]
    ON (NAME = N'$DbName', FILENAME = N'$SqlDataDir\$DbName.mdf', SIZE = 128MB, FILEGROWTH = 128MB)
    LOG ON (NAME = N'${DbName}_log', FILENAME = N'$SqlDataDir\${DbName}_log.ldf', SIZE = 64MB, FILEGROWTH = 64MB);
END
"@
& $sqlcmd -S $SqlServer -E -b -Q $createSql
if ($LASTEXITCODE -ne 0) { Write-Host "[FATAL] failed to create database $DbName." -ForegroundColor Red; exit 1 }
Write-Host "[OK] database '$DbName' ready at $SqlDataDir"

Write-Host "=== DB init done. Next: publish on dev machine (03-publish-local.ps1), then 04-setup-api.ps1 ===" -ForegroundColor Green
