// 文件路径: MES.Api/Program.cs

using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using MES.Api.Middlewares;
using MES.Api.Services;
using MES.Api.Utils;
using MES.Auth.Services;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Seed;
using MES.Services;
using MES.Services.DataExchange;
using MES.Services.DataFix;
using MES.Services.Order;
using QuestPDF.Infrastructure;
using MES.Shared.Settings;

var builder = WebApplication.CreateBuilder(args);

// ========== Hangfire 配置 ==========
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("Default"), new SqlServerStorageOptions
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

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentity<AppUser, IdentityRole>()
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
builder.Services.AddControllers();
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

// Register order service
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
// Register auxiliary services
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IProductionStandardService, ProductionStandardService>();
builder.Services.AddScoped<IGradeMappingService, GradeMappingService>();
builder.Services.AddScoped<IProductRequirementService, ProductRequirementService>();

builder.Services.AddScoped<IWorkOrderService, WorkOrderService>();
builder.Services.AddScoped<IMaterialPlanService, MaterialPlanService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();

// Register batch context services
builder.Services.AddScoped<IBatchService, BatchService>();
builder.Services.AddScoped<IProductionRecordService, ProductionRecordService>();
builder.Services.AddScoped<IProcessInspectionService, ProcessInspectionService>();
builder.Services.AddScoped<IChemicalCompositionService, ChemicalCompositionService>();
builder.Services.AddScoped<IFurnaceRegistrationService, FurnaceRegistrationService>();
builder.Services.AddScoped<IChemicalValidationRuleService, ChemicalValidationRuleService>();
builder.Services.AddScoped<IFinalInspectionService, FinalInspectionService>();
builder.Services.AddScoped<ISectionOutsourceService, SectionOutsourceService>();

// Register material context services
builder.Services.AddScoped<IMaterialService, MaterialService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<ISubcontractOrderService, SubcontractOrderService>();

// Register equipment context services
builder.Services.AddScoped<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<IRepairOrderService, RepairOrderService>();
builder.Services.AddScoped<IMaintenanceOrderService, MaintenanceOrderService>();
builder.Services.AddScoped<IInspectionRecordService, InspectionRecordService>();

// 数据导入导出服务
builder.Services.AddScoped<IDataExchangeService, DataExchangeService>();

// 数据修复服务
builder.Services.AddScoped<IDataFixService, DataFixService>();

// 扫码执行服务
builder.Services.AddScoped<IScanService, ScanService>();

// ========== 读模型上下文 ==========
builder.Services.AddScoped<IWorkOrderExecutionService, WorkOrderExecutionService>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.WithOrigins("https://localhost:5001", "http://localhost:5000")
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
    await DbInitializer.InitializeAsync(scope.ServiceProvider);
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
    "check-order-change",
    service => service.CheckOrderChangeJob(),
    "*/5 * * * *",
    jobOptions);
    
recurringJobManager.AddOrUpdate<HangfireJobService>(
    "cleanup-old-notifications",
    service => service.CleanupOldNotificationsJob(),
    "0 2 * * *");  // 每天凌晨2点执行

recurringJobManager.AddOrUpdate<HangfireJobService>(
    "material-sync",
    service => service.MaterialSyncJob(),
    "37 * * * *",  // 每小时的第37分钟执行
    jobOptions);

app.UseHttpsRedirection();
app.UseCors("AllowBlazor");

// Use custom middleware
app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();