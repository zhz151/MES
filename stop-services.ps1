# 停止占用 5000/5001/7000/7001 端口的 dotnet 服务进程
$ports = 5000, 5001, 7000, 7001
$pids = @()
foreach ($p in $ports) {
  $conns = Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue
  foreach ($c in $conns) {
    if ($pids -notcontains $c.OwningProcess) { $pids += $c.OwningProcess }
  }
}
foreach ($id in $pids) {
  Write-Host "Killing PID $id"
  Stop-Process -Id $id -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Milliseconds 800
$left = 0
foreach ($p in $ports) {
  $left += (Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue | Measure-Object).Count
}
Write-Host "剩余监听连接: $left"
