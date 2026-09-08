# 改包名后，管理员 PowerShell 直接运行：powershell -ExecutionPolicy Bypass -File .\09-apply-deploy.ps1
$Zip   = "C:\mes\mes-deploy-20260906_1945.zip"   # 改成你的包名
$Nssm  = "C:\mes\tools\nssm\nssm.exe"
& $Nssm stop MES.API; Start-Sleep -Seconds 2
$S = "C:\mes\_stage"; if (Test-Path $S) { Remove-Item $S -Recurse -Force }
Expand-Archive -Path $Zip -DestinationPath $S -Force
Remove-Item C:\mes\web\_framework -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item "$S\api\*" C:\mes\api -Recurse -Force
Copy-Item "$S\web\*" C:\mes\web -Recurse -Force
Remove-Item $S -Recurse -Force
& $Nssm start MES.API
Get-Service MES.API
