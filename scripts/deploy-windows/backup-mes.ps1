# ============================================================
# backup-mes.ps1 - Daily full backup of MES (called by Scheduled Task,
#                  because SQL Express has NO SQL Agent).
#   Backs up to C:\MSSQL\Backup\MES_FULL_yyyyMMdd.bak, keeps 5 days.
#   Logs to C:\mes\logs\backup.log
# ============================================================
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '00-params.ps1')

$logDir   = Join-Path (Split-Path $PSScriptRoot -Parent) 'logs'   # C:\mes\logs
$logFile  = Join-Path $logDir 'backup.log'
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Force -Path $logDir | Out-Null }
function Write-Log($msg) {
    $line = '{0}  {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $msg
    Add-Content -Path $logFile -Value $line -Encoding UTF8
}

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

$sqlcmd = Resolve-SqlCmd
if (-not $sqlcmd) {
    Write-Log "[FAIL] sqlcmd not found - backup aborted"
    exit 1
}

$stamp = Get-Date -Format 'yyyyMMdd'
$file  = Join-Path $SqlBackupDir ("MES_FULL_{0}.bak" -f $stamp)
if (-not (Test-Path $SqlBackupDir)) { New-Item -ItemType Directory -Force -Path $SqlBackupDir | Out-Null }

# 1) full backup (overwrite, checksum). NOTE: NO COMPRESSION - SQL Express
#    does not support BACKUP ... WITH COMPRESSION (error 1844).
& $sqlcmd -S $SqlServer -E -b -Q ("BACKUP DATABASE [{0}] TO DISK=N'{1}' WITH INIT, CHECKSUM" -f $DbName, $file)
if ($LASTEXITCODE -ne 0) {
    Write-Log "[FAIL] BACKUP DATABASE {0} failed (exit {1})" -f $DbName, $LASTEXITCODE
    exit 1
}

# 2) prune backups older than 5 days (60GB system disk - trial).
#    (PowerShell version - avoids forfiles' noisy "no files found" stderr.)
Get-ChildItem "$SqlBackupDir\MES_FULL_*.bak" -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-5) } |
    Remove-Item -Force -ErrorAction SilentlyContinue

Write-Log ("[OK] backup written: {0}" -f $file)
Write-Output "Backup OK: $file"
exit 0
