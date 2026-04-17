// 文件路径: MES.Blazor/Program.cs
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MES.Blazor;
using MudBlazor.Services;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using MES.Blazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

// 注册服务
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IProductionStandardService, ProductionStandardService>();
builder.Services.AddScoped<IGradeMappingService, GradeMappingService>();
builder.Services.AddScoped<IOrderService, OrderService>();

// 配置 HttpClient - 使用正确的 API 地址
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7001") });

await builder.Build().RunAsync();