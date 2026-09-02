// 文件路径: MES.Api/Program.cs

using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;
using MES.Api.Middlewares;
using MES.Api.Services;
using MES.Api.Utils;
using MES.Auth.Services;
using MES.Core.Interfaces.Auth;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Auth;
using MES.Data.Seed;
using MES.Services;
using MES.Services.Batch;
using MES.Services.Quality;
using MES.Services.WorkOrder;
using MES.Services.Infrastructure;
using MES.Services.Warehouse;
using MES.Services.Materials;
using MES.Services.Equipment;
using MES.Services.DataExchange;
using MES.Services.DataFix;
using MES.Services.Order;
using MES.Services.Configuration;
using MES.Services.StandardRegister;
using MES.Services.Scheduling;
using QuestPDF.Infrastructure;
using MES.Shared.Settings;
using MES.Core.Helpers;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Interfaces.Equipment;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Materials;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.StandardRegister;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Interfaces.Payroll;

var builder = WebApplication.CreateBuilder(args);

// ========== 数据库连接字符串（优先环境变量，回退配置文件） ==========
var connectionString = Environment.GetEnvironmentVariable("MES_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "未配置数据库连接字符串。请通过环境变量 MES_CONNECTION_STRING 或在 appsettings.json 的 ConnectionStrings:Default 中配置。");

// ========== Hangfire 配置 ==========
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

builder.Services.AddHangfireServer(options =>
{
    options.ServerName = $"MES.Server.{Environment.MachineName}";
    options.WorkerCount = 1; // 定时任务专用，减少并发
});

// 注册 Hangfire 定时任务服务
builder.Services.AddScoped<HangfireJobService>();

// 内存缓存（用于 GetFilterContexts 等高频查询）
builder.Services.AddMemoryCache();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, o => o.CommandTimeout(120)));

// 密码策略：仅要求 6 位以上，无需大小写/数字/特殊字符（现场录入员使用简单密码）
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequiredUniqueChars = 0;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireDigit = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings?.Issuer,
        ValidAudience = jwtSettings?.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.Secret ?? ""))
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();

// Swagger configuration (supports JWT authentication)
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MES API",
        Version = "v1",
        Description = "MES Manufacturing Execution System API"
    });

    // Add JWT authentication configuration
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Please enter JWT Token, format: Bearer {your token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Register authentication services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();

// Register order service
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPendingDeliveryQueryService, PendingDeliveryQueryService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
// Register auxiliary services
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IGradeMappingService, GradeMappingService>();
builder.Services.AddScoped<IGradeChemicalCompositionService, GradeChemicalCompositionService>();
builder.Services.AddScoped<IGradePhysicalPropertyService, GradePhysicalPropertyService>();
builder.Services.AddScoped<ISubStandardQuickViewService, SubStandardQuickViewService>();
builder.Services.AddScoped<IStandardInspectionRequirementService, StandardInspectionRequirementService>();
builder.Services.AddScoped<IFactoryInspectionRequirementService, FactoryInspectionRequirementService>();
builder.Services.AddScoped<IStandardWorkDayService, StandardWorkDayService>();
builder.Services.AddScoped<ISectionNameDisplayService, SectionNameDisplayService>();
builder.Services.AddScoped<IProcessDefinitionService, ProcessDefinitionService>();
builder.Services.AddScoped<IEnumDisplayDefinitionService, EnumDisplayDefinitionService>();
builder.Services.AddScoped<IDictValueDefinitionService, DictValueDefinitionService>();
builder.Services.AddScoped<IStandardWorkDayDeliveryStateService, StandardWorkDayDeliveryStateService>();
builder.Services.AddScoped<IConfigParameterService, ConfigParameterService>();
builder.Services.AddScoped<IProcessCardColumnDefinitionService, ProcessCardColumnDefinitionService>();
builder.Services.AddScoped<IProcessCardStyleDefinitionService, ProcessCardStyleDefinitionService>();
builder.Services.AddScoped<ICertificatePrintSettingService, CertificatePrintSettingService>();
builder.Services.AddScoped<ICertificatePrintColumnDefinitionService, CertificatePrintColumnDefinitionService>();
builder.Services.AddScoped<IDailyOutputEstimateService, DailyOutputEstimateService>();
builder.Services.AddScoped<IMaterialPlanProcessGroupService, MaterialPlanProcessGroupService>();
builder.Services.AddScoped<IProductRequirementService, ProductRequirementService>();

builder.Services.AddScoped<IWorkOrderService, WorkOrderService>();
builder.Services.AddScoped<IWorkOrderListSummaryRefreshService, WorkOrderListSummaryRefreshService>();

builder.Services.AddScoped<IMaterialPlanService, MaterialPlanService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IInventoryBatchWriteService, InventoryBatchWriteService>();
builder.Services.AddScoped<IOutboundWriteService, OutboundWriteService>();
builder.Services.AddScoped<IInventorySyncService, InventorySyncService>();
builder.Services.AddScoped<ICertificateService, CertificateService>();

// Register batch context services
builder.Services.AddScoped<IBatchService, BatchService>();
builder.Services.AddScoped<IProductionRecordService, ProductionRecordService>();
builder.Services.AddScoped<IProcessInspectionService, ProcessInspectionService>();
builder.Services.AddScoped<IChemicalCompositionService, ChemicalCompositionService>();
builder.Services.AddScoped<IFurnaceRegistrationService, FurnaceRegistrationService>();
builder.Services.AddScoped<IChemicalValidationRuleService, ChemicalValidationRuleService>();
builder.Services.AddScoped<IFinalInspectionService, FinalInspectionService>();
builder.Services.AddScoped<IChemicalAnalysisService, ChemicalAnalysisService>();
builder.Services.AddScoped<IHardnessTestService, HardnessTestService>();
builder.Services.AddScoped<IGrainSizeTestService, GrainSizeTestService>();
builder.Services.AddScoped<IPittingCorrosionTestService, PittingCorrosionTestService>();
builder.Services.AddScoped<IIntergranularCorrosionTestService, IntergranularCorrosionTestService>();
builder.Services.AddScoped<ITensileTestService, TensileTestService>();
builder.Services.AddScoped<IMetallographicTestService, MetallographicTestService>();
builder.Services.AddScoped<IFlatteningTestService, FlatteningTestService>();
builder.Services.AddScoped<IFlaringTestService, FlaringTestService>();
builder.Services.AddScoped<INcrService, NcrService>();
builder.Services.AddScoped<ISectionOutsourceService, SectionOutsourceService>();
builder.Services.AddScoped<IPicklingService, PicklingService>();
builder.Services.AddScoped<IMaterialReceiveCheckService, MaterialReceiveCheckService>();

// Register material context services
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<ISubcontractOrderService, SubcontractOrderService>();

// Register equipment context services
builder.Services.AddScoped<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<IRepairOrderService, RepairOrderService>();
builder.Services.AddScoped<IMaintenanceOrderService, MaintenanceOrderService>();
builder.Services.AddScoped<IInspectionRecordService, InspectionRecordService>();

// 数据导入导出服务
builder.Services.AddScoped<IDataImportService, DataImportService>();
builder.Services.AddScoped<IDataExportService, DataExportService>();
builder.Services.AddScoped<IDataExchangeService, DataExchangeService>();

// 数据修复服务
builder.Services.AddScoped<IDataFixService, DataFixService>();

// 扫码执行服务
builder.Services.AddScoped<IScanService, ScanService>();
builder.Services.AddScoped<IQrCodeService, QrCodeService>();
builder.Services.AddScoped<IWorkstationService, WorkstationService>();
builder.Services.AddScoped<IEmployeeService, MES.Services.Configuration.EmployeeService>();
builder.Services.AddScoped<IAttendanceService, MES.Services.Payroll.AttendanceService>();
builder.Services.AddScoped<IPieceRateProductionCategoryService, MES.Services.Payroll.PieceRateProductionCategoryService>();
builder.Services.AddScoped<IPieceRateCategoryImportService, MES.Services.Payroll.PieceRateCategoryImportService>();
builder.Services.AddScoped<IOperatorNameValidator, OperatorNameValidator>();
builder.Services.AddScoped<IDailyProductionCapacityService, MES.Services.Configuration.DailyProductionCapacityService>();
builder.Services.AddScoped<IStandardRegisterService, StandardRegisterService>();

// ========== 读模型上下文 ==========
builder.Services.AddScoped<IWorkOrderExecutionService, WorkOrderExecutionService>();
builder.Services.AddScoped<IFixedLengthWorkOrderService, FixedLengthWorkOrderService>();

// ========== Scheduling 上下文 ==========
builder.Services.AddScoped<IOrderDemandAdjustmentService, OrderDemandAdjustmentService>();
builder.Services.AddScoped<IRawMaterialLockPlanAndExecutionService, RawMaterialLockPlanAndExecutionService>();
builder.Services.AddScoped<IProductionOverviewService, ProductionOverviewService>();
builder.Services.AddScoped<ISectionProductionStatusService, SectionProductionStatusService>();
builder.Services.AddScoped<ISectionParagraphFlowAnalysisService, SectionParagraphFlowAnalysisService>();
builder.Services.AddScoped<ISectionParagraphConfigService, MES.Services.Configuration.SectionParagraphConfigService>();
builder.Services.AddScoped<IWorkOrderScheduleService, WorkOrderScheduleService>();
builder.Services.AddScoped<IBatchPlanService, BatchPlanService>();
builder.Services.AddScoped<IColdRollPlanService, ColdRollPlanService>();
builder.Services.AddScoped<IColdRollSpecScheduleService, ColdRollSpecScheduleService>();
builder.Services.AddScoped<IColdRollCapacityService, ColdRollCapacityService>();
builder.Services.AddScoped<IColdRollMachineConfigService, ColdRollMachineConfigService>();
builder.Services.AddScoped<IColdRollMachineGroupConfigService, ColdRollMachineGroupConfigService>();
builder.Services.AddScoped<IBatchPlanScheduleService, BatchPlanScheduleService>();
builder.Services.AddScoped<IFinalInspectionPlanService, FinalInspectionPlanService>();

// 操作日志服务
builder.Services.AddScoped<IOperationLogService, OperationLogService>();

// 质量过程跟踪
builder.Services.AddScoped<IQualityProcessTrackingService, QualityProcessTrackingService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();

var corsOrigins = builder.Configuration.GetValue<string>("CorsOrigins")?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? new[] { "https://localhost:5001", "http://localhost:5000" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// QuestPDF 许可设置（社区版免费）
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// ========== 初始化数据库 ==========
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DatabaseMigrator.ApplyMigrationsAsync(db);
    await DbInitializer.InitializeAsync(scope.ServiceProvider);

    // 枚举显示配置注入 EnumHelper（配置表优先，兜底静态字典），
    // 服务端打印/DataExchange 导入导出/反向解析走配置表新中文
    var enumDisplayService = scope.ServiceProvider.GetRequiredService<IEnumDisplayDefinitionService>();
    var enumDisplayMap = await enumDisplayService.GetDisplayMapAsync();
    foreach (var kvp in enumDisplayMap)
        EnumHelper.ApplyEnumOverrides(kvp.Key, kvp.Value);

    // 字典显示配置注入 DictValueDisplayHelper（配置表优先，兜底 Keys 常量类），服务端打印/DataExchange 走新中文
    var dictDisplayService = scope.ServiceProvider.GetRequiredService<IDictValueDefinitionService>();
    var dictDisplayMap = await dictDisplayService.GetDisplayMapAsync();
    DictValueDisplayHelper.OverrideMap = dictDisplayMap;

    // 用料计划执行容差静态快照注入 MaterialPlanToleranceProvider（读 MaterialPlanTolerance 类目 InputConsistencyTolerance 键），
    // 三处消费（排序/筛选表达式 + DTO 计算属性）口径一致；改配置表由 ConfigParameterService 写操作即时刷新，无需重启
    var configParamService = scope.ServiceProvider.GetRequiredService<IConfigParameterService>();
    var configMap = await configParamService.GetConfigMapAsync("MaterialPlanTolerance");
    MaterialPlanToleranceProvider.Apply(configMap.GetValueOrDefault("InputConsistencyTolerance"));

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("读模型刷新已移除，使用实时查询模式");
}

// ========== 中间件配置 ==========
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 启用 Hangfire 面板（必须在 UseHangfireServer 之后）
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireCustomBasicAuthorizationFilter() }
});

// ========== 注册定时任务（必须在 UseHangfireDashboard 之后） ==========
// 使用 IApplicationBuilder 的扩展方法注册定时任务
var jobOptions = new RecurringJobOptions
{
    TimeZone = TimeZoneInfo.Local
};

// 使用 BackgroundJob 的静态方法需要先确保 JobStorage 已初始化
// 通过 IApplicationBuilder 的 ApplicationServices 获取服务
var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();

recurringJobManager.AddOrUpdate<HangfireJobService>(
    "cleanup-old-notifications",
    service => service.CleanupOldNotificationsJob(),
    "0 2 * * *");  // 每天凌晨2点执行

// 质量过程跟踪物化表增量刷新：每小时执行一次（避开整点减少并发峰值）
recurringJobManager.AddOrUpdate<HangfireJobService>(
    "refresh-quality-process-tracking",
    service => service.RefreshQualityProcessTrackingJob(),
    "7 * * * *");

// 全项目数据更新兜底任务：中午 11:55 + 晚上 23:55 各一次
// 重建所有物化读模型（工单执行/订单列表/用料计划总览/质量过程跟踪）+ 失效派生缓存，
// 修复增量刷新漏网（如 DataExchange Excel 导入直写 DbContext 无刷新）导致的读模型过期。
recurringJobManager.AddOrUpdate<HangfireJobService>(
    "full-project-data-refresh-noon",
    service => service.FullProjectDataRefreshJob(),
    "55 11 * * *");

recurringJobManager.AddOrUpdate<HangfireJobService>(
    "full-project-data-refresh-midnight",
    service => service.FullProjectDataRefreshJob(),
    "55 23 * * *");

// 开发环境不启用 HTTPS 重定向（API 仅 HTTP 端口时避免预检请求重定向导致 CORS 失败）
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowBlazor");

// Use custom middleware
app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();