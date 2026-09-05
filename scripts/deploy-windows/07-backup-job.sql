-- ============================================================
-- 07-backup-job.sql - Daily compressed full backup of MES + cleanup
-- Run on the server as sysadmin:
--     sqlcmd -S localhost -E -i 07-backup-job.sql
-- Creates SQL Agent job 'MES_Backup_Daily' at 02:30 every day.
-- Backup target : C:\MSSQL\Backup\MES_FULL_yyyyMMdd.bak
-- Retention    : 5 days (trial runs on the single 60GB system disk - keep it short.
--                Copy backups off the box regularly, or widen the /d -5 once a data disk is added.)
--
-- >>> APPLIES ONLY IF THE SQL HAS SQL AGENT (Standard/Developer, DEFAULT instance) <<<
--     The target server ships SQL Server EXPRESS (named instance SQLEXPRESS),
--     which has NO SQL Agent - this file WILL NOT WORK there.
--     On Express use instead:   powershell -File .\07-setup-backup.ps1
--     (registers a Windows Scheduled Task that calls backup-mes.ps1).
-- ============================================================
USE [msdb];
GO

IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'MES_Backup_Daily')
BEGIN
    EXEC msdb.dbo.sp_delete_job @job_name = N'MES_Backup_Daily';
END
GO

EXEC msdb.dbo.sp_add_job
    @job_name           = N'MES_Backup_Daily',
    @enabled            = 1,
    @description        = N'Daily compressed full backup of MES + prune backups older than 5 days',
    @notify_level_eventlog = 0;
GO

EXEC msdb.dbo.sp_add_jobstep
    @job_name           = N'MES_Backup_Daily',
    @step_name          = N'Backup database MES (compressed)',
    @subsystem          = N'TSQL',
    @command            = N'
DECLARE @file nvarchar(260);
SET @file = N''C:\MSSQL\Backup\MES_FULL_'' + REPLACE(CONVERT(varchar(10), GETDATE(), 112), N''-'', N'''') + N''.bak'';
BACKUP DATABASE [MES] TO DISK = @file WITH COMPRESSION, INIT, CHECKSUM;
',
    @database_name      = N'master',
    @on_success_action  = 3,          -- go to next step
    @on_fail_action     = 2;          -- quit with failure
GO

-- cleanup via CmdExec (forfiles delete >5 days). Needs Agent CmdExec subsystem enabled.
EXEC msdb.dbo.sp_add_jobstep
    @job_name           = N'MES_Backup_Daily',
    @step_name          = N'Prune backups older than 5 days',
    @subsystem          = N'CmdExec',
    @command            = N'forfiles /p "C:\MSSQL\Backup" /m "MES_FULL_*.bak" /d -5 /c "cmd /c del @file"',
    @on_success_action  = 1,          -- quit with success
    @on_fail_action     = 2;
GO

EXEC msdb.dbo.sp_add_schedule
    @schedule_name      = N'Daily_0230',
    @freq_type          = 4,          -- daily
    @freq_interval      = 1,
    @active_start_time  = 23000;      -- 02:30
GO

EXEC msdb.dbo.sp_attach_schedule
    @job_name           = N'MES_Backup_Daily',
    @schedule_name      = N'Daily_0230';
GO

EXEC msdb.dbo.sp_add_jobserver @job_name = N'MES_Backup_Daily';
GO

PRINT N'Job MES_Backup_Daily created.';
GO
