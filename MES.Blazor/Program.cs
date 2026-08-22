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

// MudBlazor 中文本地化
builder.Services.AddSingleton<MudLocalizer, MESMudLocalizer>();

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

var apiBaseUrl = builder.Configuration.GetValue<string>("ApiSettings:BaseUrl") ?? "https://localhost:7001";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<AuthHttpClient>();

// ========== 订单上下文 ==========
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ProductRequirementService>();
builder.Services.AddScoped<OrderDemandAdjustmentService>();

// ========== 生产标准上下文 ==========
builder.Services.AddScoped<GradeMappingService>();
builder.Services.AddScoped<GradeChemicalCompositionService>();
builder.Services.AddScoped<GradePhysicalPropertyService>();
builder.Services.AddScoped<SubStandardQuickViewService>();
builder.Services.AddScoped<StandardInspectionRequirementService>();
builder.Services.AddScoped<FactoryInspectionRequirementService>();
builder.Services.AddScoped<ChemicalCompositionService>();
builder.Services.AddScoped<ChemicalValidationRuleService>();
builder.Services.AddScoped<StandardRegisterService>();

// ========== 工单上下文 ==========
builder.Services.AddScoped<WorkOrderService>();
builder.Services.AddScoped<MaterialPlanService>();
builder.Services.AddScoped<MaterialPlanProcessGroupService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<WorkOrderExecutionService>();

// ========== 仓库上下文 ==========
builder.Services.AddScoped<WarehouseService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<PendingDeliveryService>();

// ========== 批次上下文 ==========
builder.Services.AddScoped<BatchService>();
builder.Services.AddScoped<ProductionRecordService>();
builder.Services.AddScoped<SectionOutsourceService>();
builder.Services.AddScoped<PicklingService>();

// ========== 质量上下文 ==========
builder.Services.AddScoped<QualityProcessTrackingService>();
builder.Services.AddScoped<ProcessInspectionService>();
builder.Services.AddScoped<FurnaceRegistrationService>();
builder.Services.AddScoped<FinalInspectionService>();
builder.Services.AddScoped<ChemicalAnalysisService>();
builder.Services.AddScoped<HardnessTestService>();
builder.Services.AddScoped<GrainSizeTestService>();
builder.Services.AddScoped<PittingCorrosionTestService>();
builder.Services.AddScoped<IntergranularCorrosionTestService>();
builder.Services.AddScoped<TensileTestService>();
builder.Services.AddScoped<MetallographicTestService>();
builder.Services.AddScoped<FlatteningTestService>();
builder.Services.AddScoped<FlaringTestService>();
builder.Services.AddScoped<NcrService>();
builder.Services.AddScoped<MaterialReceiveCheckService>();
builder.Services.AddScoped<CertificateService>();

// ========== 物料上下文 ==========
builder.Services.AddScoped<SupplierService>();
builder.Services.AddScoped<PurchaseOrderService>();
builder.Services.AddScoped<SubcontractOrderService>();

// ========== 设备上下文 ==========
builder.Services.AddScoped<EquipmentService>();
builder.Services.AddScoped<RepairOrderService>();
builder.Services.AddScoped<MaintenanceOrderService>();
builder.Services.AddScoped<InspectionRecordService>();

// ========== 数据交换 ==========
builder.Services.AddScoped<DataExchangeService>();

// ========== 基础设施 ==========
builder.Services.AddScoped<ColumnPrefsService>();
builder.Services.AddScoped<PageStateService>();
builder.Services.AddScoped<OutboundStateService>();

// ========== 扫码执行 ==========
builder.Services.AddScoped<ScanService>();

// ========== Scheduling 上下文 ==========
builder.Services.AddScoped<RawMaterialLockPlanAndExecutionService>();
builder.Services.AddScoped<ProductionOverviewService>();
builder.Services.AddScoped<SectionProductionStatusService>();
builder.Services.AddScoped<MES.Blazor.Services.SectionFlowAnalysisService>();
builder.Services.AddScoped<SectionParagraphFlowAnalysisService>();
builder.Services.AddScoped<SectionFlowCategoryService>();
builder.Services.AddScoped<SectionParagraphConfigService>();
builder.Services.AddScoped<CombinationGroupService>();
builder.Services.AddScoped<WorkOrderScheduleService>();
builder.Services.AddScoped<BatchPlanService>();
builder.Services.AddScoped<ColdRollPlanService>();
builder.Services.AddScoped<ColdRollSpecScheduleService>();
builder.Services.AddScoped<ColdRollCapacityService>();
builder.Services.AddScoped<ColdRollMachineConfigService>();
builder.Services.AddScoped<BatchPlanScheduleService>();
builder.Services.AddScoped<BatchPlanTargetService>();
builder.Services.AddScoped<FinalInspectionPlanService>();

// ========== 用户管理 ==========
builder.Services.AddScoped<UserService>();

// ========== Configuration 上下文 ==========
builder.Services.AddScoped<StandardWorkDayService>();
builder.Services.AddScoped<StandardWorkDayDeliveryStateService>();
builder.Services.AddScoped<ConfigParameterService>();
builder.Services.AddScoped<DailyOutputEstimateService>();
builder.Services.AddScoped<DailyProductionCapacityService>();
builder.Services.AddScoped<WorkstationService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<ProcessDefinitionService>();
builder.Services.AddScoped<EnumDisplayDefinitionService>();
builder.Services.AddScoped<DictValueDefinitionService>();
builder.Services.AddScoped<ProcessCardColumnDefinitionService>();
builder.Services.AddScoped<ProcessCardStyleDefinitionService>();
builder.Services.AddScoped<CertificatePrintSettingService>();
builder.Services.AddScoped<CertificatePrintColumnDefinitionService>();

// ========== 报表 ==========
builder.Services.AddScoped<ReportService>();

await builder.Build().RunAsync();