# 停止所有 dotnet 进程（本项目 API/Blazor/build server），释放 dll 占用
$procs = Get-Process dotnet -ErrorAction SilentlyContinue
if ($procs) {
  foreach ($p in $procs) {
    Write-Host "Kill dotnet PID $($p.Id) ($($p.ProcessName))"
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
  }
} else {
  Write-Host "无 dotnet 进程"
}
Start-Sleep -Milliseconds 800
$left = @(Get-Process dotnet -ErrorAction SilentlyContinue).Count
Write-Host "剩余 dotnet 进程: $left"
