$env:ASPNETCORE_ENVIRONMENT='Development'

# MES.Api
Start-Process -FilePath 'dotnet' `
  -ArgumentList 'E:\MES项目\MES\MES.Api\bin\Debug\net8.0\MES.Api.dll','--urls','https://localhost:7001;http://localhost:7000' `
  -WorkingDirectory 'E:\MES项目\MES\MES.Api' -WindowStyle Hidden

# MES.Blazor (WASM dev server)
Start-Process -FilePath 'dotnet' `
  -ArgumentList 'run','--project','E:\MES项目\MES\MES.Blazor','--no-build','--urls','http://localhost:5000;https://localhost:5001' `
  -WorkingDirectory 'E:\MES项目\MES' -WindowStyle Hidden
