using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor.Services;
using MES.Blazor.Services;
using MES.Blazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// 加载额外的配置文件
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// HttpClient 配置 - 支持开发环境配置
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    var apiUrl = configuration.GetValue<string>("ApiBaseUrl");

    // 如果配置了API URL，则使用配置的URL；否则使用当前基础地址
    var baseAddress = string.IsNullOrEmpty(apiUrl)
        ? navigationManager.BaseUri
        : apiUrl;

    Console.WriteLine($"API Base Address: {baseAddress}");

    var httpClient = new HttpClient { BaseAddress = new Uri(baseAddress) };

    // 添加超时设置
    httpClient.Timeout = TimeSpan.FromSeconds(30);

    return httpClient;
});

// MudBlazor
builder.Services.AddMudServices();

// 认证服务
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddScoped<CustomAuthenticationStateProvider>();

// 业务服务
builder.Services.AddScoped<IAuthService, AuthService>();

await builder.Build().RunAsync();
