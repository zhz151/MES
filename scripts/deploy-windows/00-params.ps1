# ============================================================
# 00-params.ps1 - Central parameters for MES Windows deployment
# Source this file from other scripts:  . .\00-params.ps1
# EDIT the SECRET values below before going live.
# ============================================================

# ---- Site / network ----
$Domain        = "zhz.js.cn"
$PublicIp      = "111.231.10.18"

# ---- Paths (single 60GB system disk C: - trial, no separate data disk for now) ----
$DataDrive     = "C:"
$MesRoot       = "C:\mes"
$ApiDir        = "$MesRoot\api"          # MES.Api publish output
$WebDir        = "$MesRoot\web"          # MES.Blazor publish output (wwwroot)
$LogsDir       = "$MesRoot\logs"
$PublishArchive= "$MesRoot\publish"      # history archives for rollback
$ToolsDir      = "$MesRoot\tools"        # nssm only
$NginxDir      = "$MesRoot\nginx"        # unzipped nginx for windows lives here
$SslDir        = "$NginxDir\conf\ssl"

# ---- SQL ----
# NOTE: this server ships with SQL Server 2022 EXPRESS as NAMED instance SQLEXPRESS.
# Express caps RAM ~1.4GB (fine on the 4GB box) and has NO SQL Agent (daily backup
# is done via a Windows Scheduled Task instead - see 07-setup-backup.ps1).
$SqlServer     = "localhost\SQLEXPRESS"
$DbName        = "MES"
$SqlDataDir    = "C:\MSSQL\DATA"
$SqlBackupDir  = "C:\MSSQL\Backup"
# Windows-integrated connection (running user must be sysadmin; the account that
# installed SQL, or an admin added to sysadmin, is used by 02/07 scripts).
$ConnectionString = "Server=localhost\SQLEXPRESS;Database=MES;Integrated Security=true;TrustServerCertificate=true;MultipleActiveResultSets=true"

# ---- Services / tools ----
$ApiServiceName  = "MES.API"
$NginxServiceName= "nginx"
$NginxExe        = "$NginxDir\nginx.exe"
$NssmExe         = "$ToolsDir\nssm\nssm.exe"
$NginxConfTemplate = Join-Path $PSScriptRoot "nginx-mes.conf.template"

# ---- API listening ----
$ApiBindUrl      = "http://127.0.0.1:7000"

# ============================================================
# SECRETS - CHANGE these (or override via env) before production
# ============================================================
$JwtSecret        = "13d71c1c9fa64ff8bac01f5c935ec439"
$AdminPassword    = "151CZxinya2"
$HangfireUser     = "hf_admin"
$HangfirePassword = "f5c935ec439"
$CorsOrigins      = "https://zhz.js.cn"

function Test-ParamsSecrets {
    if ($JwtSecret -like "REPLACE_ME*" -or $AdminPassword -like "REPLACE_ME*" -or $HangfirePassword -like "REPLACE_ME*") {
        Write-Host "[WARN] Default/placeholder secrets still in 00-params.ps1." -ForegroundColor Yellow
        Write-Host "       Edit 00-params.ps1 and set JwtSecret/AdminPassword/HangfirePassword first!" -ForegroundColor Yellow
        return $false
    }
    return $true
}
