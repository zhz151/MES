using Blazored.LocalStorage;
using MES.Blazor;
using MES.Blazor.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// 配置 MudBlazor 服务 - Snackbar 显示在顶部居中
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
});

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7001") });
builder.Services.AddScoped<AuthHttpClient>();

builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<ProductionStandardService>();
builder.Services.AddScoped<GradeMappingService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ProductRequirementService>();
builder.Services.AddScoped<WorkOrderService>();
builder.Services.AddScoped<MaterialPlanService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<WarehouseService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ColumnPrefsService>();
builder.Services.AddScoped<OutboundStateService>();
builder.Services.AddScoped<BatchService>();
builder.Services.AddScoped<ProductionRecordService>();
builder.Services.AddScoped<ProcessInspectionService>();
builder.Services.AddScoped<ChemicalCompositionService>();
builder.Services.AddScoped<FurnaceRegistrationService>();
builder.Services.AddScoped<ChemicalValidationRuleService>();
builder.Services.AddScoped<FinalInspectionService>();
builder.Services.AddScoped<SectionOutsourceService>();
builder.Services.AddScoped<MaterialService>();
builder.Services.AddScoped<SupplierService>();
builder.Services.AddScoped<PurchaseOrderService>();
builder.Services.AddScoped<SubcontractOrderService>();
builder.Services.AddScoped<DataExchangeService>();
await builder.Build().RunAsync();