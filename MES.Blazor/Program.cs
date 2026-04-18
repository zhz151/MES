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

// 注册 AuthHttpClient（替代直接使用 HttpClient）
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7001") });
builder.Services.AddScoped<AuthHttpClient>();

// 注册服务（使用 AuthHttpClient）
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<ProductionStandardService>();
builder.Services.AddScoped<GradeMappingService>();
builder.Services.AddScoped<OrderService>();

await builder.Build().RunAsync();