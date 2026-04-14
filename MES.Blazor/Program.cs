using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using MES.Blazor.Services;
using MES.Blazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient 配置 - 根据当前页面协议自动选择
var apiBaseUrl = builder.HostEnvironment.BaseAddress.Contains("https") 
    ? "https://localhost:5001" 
    : "http://localhost:5000";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

// MudBlazor
builder.Services.AddMudServices();

// 认证服务
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddScoped<CustomAuthenticationStateProvider>();

// 业务服务
builder.Services.AddScoped<IAuthService, AuthService>();

await builder.Build().RunAsync();
