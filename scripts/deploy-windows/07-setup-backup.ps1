# ============================================================
# 07-setup-backup.ps1 - Register a DAILY Windows Scheduled Task that runs
#   backup-mes.ps1 at 02:30 (SQL Express has no SQL Agent, so no Agent job).
#   Run on the SERVER as Administrator, AFTER 02-init-db.ps1 succeeded.
#   Steps:
#     1) grant NT AUTHORITY\SYSTEM sysadmin on the SQL instance (task runs as SYSTEM)
#     2) register Scheduled Task 'MES_Backup_Daily'
#     3) run backup-mes.ps1 once now to verify a .bak is produced
# ============================================================
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '00-params.ps1')

# ---- resolve sqlcmd ----
$c = Get-Command sqlcmd.exe -ErrorAction SilentlyContinue
if (-not $c) {
    Write-Host "[FATAL] sqlcmd not found. Install SSMS / SQL command line utilities first." -ForegroundColor Red
    exit 1
}
$sqlcmd = $c.Source
Write-Host "[OK] sqlcmd: $sqlcmd"

# ---- 1) make sure the task's run-as account (SYSTEM) can log into SQL as sysadmin ----
Write-Host "=== Granting NT AUTHORITY\\SYSTEM sysadmin on $SqlServer ===" -ForegroundColor Cyan
& $sqlcmd -S $SqlServer -E -b -Q "IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'NT AUTHORITY\SYSTEM') CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS; ALTER SERVER ROLE sysadmin ADD MEMBER [NT AUTHORITY\SYSTEM];"
if ($LASTEXITCODE -ne 0) {
    Write-Host "[FATAL] failed to grant sysadmin. Are you running as a sysadmin (the account that installed SQL)?" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] NT AUTHORITY\\SYSTEM granted sysadmin"

# ---- 2) register scheduled task (daily 02:30) ----
$jobScript = Join-Path $PSScriptRoot 'backup-mes.ps1'
if (-not (Test-Path $jobScript)) { Write-Host "[FATAL] backup-mes.ps1 not found next to this script: $jobScript" -ForegroundColor Red; exit 1 }

$action  = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument ('-NoProfile -ExecutionPolicy Bypass -File "' + $jobScript + '"')
$trigger = New-ScheduledTaskTrigger -Daily -At '02:30'
$settings= New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 1) -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName 'MES_Backup_Daily' -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest -User 'SYSTEM' -Force | Out-Null
Write-Host "[OK] Scheduled Task 'MES_Backup_Daily' registered (daily 02:30, runs as SYSTEM)."

# ---- 3) run once now to verify ----
Write-Host "=== Running one backup now to verify ===" -ForegroundColor Cyan
& $jobScript
if ($LASTEXITCODE -ne 0) {
    Write-Host "[WARN] trial backup failed - see C:\mes\logs\backup.log. Task is registered anyway; fix and test again." -ForegroundColor Yellow
    exit 1
}
$latest = Get-ChildItem "$SqlBackupDir\MES_FULL_*.bak" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($latest) {
    Write-Host "[OK] trial backup verified: $($latest.FullName) ($([math]::Round($latest.Length/1MB,1)) MB)" -ForegroundColor Green
} else {
    Write-Host "[WARN] no .bak found in $SqlBackupDir yet - check C:\mes\logs\backup.log" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next: verify in Task Scheduler (taskschd.msc -> MES_Backup_Daily, Run now if you like)." -ForegroundColor Green
Write-Host "Backup log: C:\mes\logs\backup.log" -ForegroundColor Green
