# ============================================================
# 08-restore-db.ps1 - restore the REAL database backup (.bak)
#   into this server's MES database.  (run on server as Administrator)
#
# Why: the codebase's EF migration chain is an incremental chain that
#   starts from an EMPTY "InitialCompressed" marker -- it can NEVER build
#   a fresh empty database.  Fresh deploys must therefore restore a full
#   .bak produced from the real DB (zhou\MESMN), exactly as we do here.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\08-restore-db.ps1 -BackupFile C:\mes\MESMN_20260905.bak
#
# Steps:
#   1) grant NT AUTHORITY\SYSTEM sysadmin (runtime identity of the MES.API
#      service, which connects with Integrated Security)
#   2) drop any existing (broken / half-created) MES database
#   3) RESTORE the .bak as MES, moving data/log files to C:\MSSQL\DATA
#   4) verify table count
# ============================================================
param(
    [Parameter(Mandatory = $true)][string]$BackupFile
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "00-params.ps1")

function Resolve-SqlCmd {
    $c = Get-Command sqlcmd.exe -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    foreach ($v in @("160","150")) {
        foreach ($p in @("C:\Program Files\Microsoft SQL Server\$v\Tools\Binn\SQLCMD.EXE",
                         "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE")) {
            if (Test-Path $p) { return $p }
        }
    }
    return $null
}

if (-not (Test-Path $BackupFile)) {
    Write-Host "[FATAL] backup not found: $BackupFile" -ForegroundColor Red
    exit 1
}

$sqlcmd = Resolve-SqlCmd
if (-not $sqlcmd) {
    Write-Host "[FATAL] sqlcmd not found on this server." -ForegroundColor Red
    exit 1
}
Write-Host "[OK] sqlcmd: $sqlcmd"
Write-Host "[OK] backup file: $BackupFile"

# 0) reachability
& $sqlcmd -S $SqlServer -E -b -d master -Q "SELECT 1" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "[FATAL] Cannot connect to SQL instance '$SqlServer' with Windows auth." -ForegroundColor Red
    exit 1
}
Write-Host "[OK] connected to SQL instance $SqlServer"

# 1) make NT AUTHORITY\SYSTEM a sysadmin on the instance.
#    The MES.API Windows service (registered by 04-setup-api.ps1 via NSSM)
#    runs as LocalSystem and connects with Integrated Security.
Write-Host "[1/4] granting NT AUTHORITY\SYSTEM sysadmin on $SqlServer"
& $sqlcmd -S $SqlServer -E -b -d master -Q "IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'NT AUTHORITY\SYSTEM') CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS; ALTER SERVER ROLE sysadmin ADD MEMBER [NT AUTHORITY\SYSTEM];"
if ($LASTEXITCODE -ne 0) {
    Write-Host "[FATAL] sysadmin grant failed - run this script as the account that is already sysadmin (the one that ran 02-init-db.ps1)." -ForegroundColor Red
    exit 1
}
Write-Host "[OK] SYSTEM granted sysadmin"

# 2) drop any existing MES (the earlier empty-database attempt left a broken DB)
Write-Host "[2/4] dropping any existing 'MES' database"
& $sqlcmd -S $SqlServer -E -b -d master -Q "IF DB_ID('MES') IS NOT NULL BEGIN ALTER DATABASE [MES] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [MES]; END"
if ($LASTEXITCODE -ne 0) {
    Write-Host "[FATAL] failed to drop existing MES database." -ForegroundColor Red
    exit 1
}
Write-Host "[OK] old MES dropped (if it existed)"

# 3) read the logical file list from the backup and build the RESTORE ... WITH MOVE
Write-Host "[3/4] reading file list from backup"
# sqlcmd flag note: -W/-y/-Y all conflict with each other here, but FILELISTONLY
# columns are short so we parse the default width with -s "|" separator only.
$rows = & $sqlcmd -S $SqlServer -E -b -d master -h -1 -s "|" -Q "RESTORE FILELISTONLY FROM DISK = '$BackupFile'"
if ($LASTEXITCODE -ne 0) {
    Write-Host "[FATAL] failed to read backup file list (wrong file? permission?)." -ForegroundColor Red
    exit 1
}
$moves = @()
$dataCount = 0
$logCount = 0
foreach ($line in $rows) {
    $line = [string]$line
    $line = $line.Trim()
    if ($line -eq "") { continue }
    $f = $line -split '\|'
    if ($f.Count -lt 3) { continue }
    $logical = $f[0].Trim()
    $type    = $f[2].Trim()
    if ($type -eq 'D') {
        $dataCount++
        $target = if ($dataCount -eq 1) { "$SqlDataDir\MES.mdf" } else { "$SqlDataDir\MES_$dataCount.ndf" }
        $moves += "MOVE N'$logical' TO N'$target'"
        Write-Host "  data file: $logical -> $target"
    } elseif ($type -eq 'L') {
        $logCount++
        $target = if ($logCount -eq 1) { "$SqlDataDir\MES_log.ldf" } else { "$SqlDataDir\MES_log_$logCount.ldf" }
        $moves += "MOVE N'$logical' TO N'$target'"
        Write-Host "  log  file: $logical -> $target"
    }
}
if ($moves.Count -eq 0) {
    Write-Host "[FATAL] no data/log file entries could be parsed from the backup." -ForegroundColor Red
    exit 1
}

Write-Host "[4/4] restoring MES from backup (this can take a while)..."
$restoreSql = "RESTORE DATABASE [MES] FROM DISK = '$BackupFile' WITH $($moves -join ', '), REPLACE, RECOVERY, STATS=10"
& $sqlcmd -S $SqlServer -E -b -d master -Q $restoreSql
if ($LASTEXITCODE -ne 0) {
    Write-Host "[FATAL] RESTORE failed." -ForegroundColor Red
    exit 1
}
Write-Host "[OK] database restored as 'MES'"

# verify
$verifySql = "SELECT (SELECT COUNT(*) FROM sys.tables) AS table_count, (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'InventoryBatch')) AS inventorybatch_cols;"
Write-Host "--- verification (table count / InventoryBatch columns) ---"
& $sqlcmd -S $SqlServer -E -b -d MES -W -Q $verifySql
if ($LASTEXITCODE -ne 0) {
    Write-Host "[WARN] verification query failed - check restore state manually." -ForegroundColor Yellow
}

Write-Host "=== Restore done. Next: re-run the API foreground check (04-setup-api.ps1 flow), then answer y there. ===" -ForegroundColor Green
