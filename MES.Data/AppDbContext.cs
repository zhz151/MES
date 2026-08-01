using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MES.Core.Interfaces.Infrastructure;
using MES.Data.Entities;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Order;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Quality;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Infrastructure;
using MES.Core.Enums;

namespace MES.Data;

public partial class AppDbContext : IdentityDbContext<AppUser>
{
    private readonly ICurrentUser? _currentUser;

    // 无参构造函数（用于工具项目）
    public AppDbContext() : base()
    {
        _currentUser = null;
    }

    // 仅 DbContextOptions 构造函数（用于工具项目）
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        _currentUser = null;
    }

    // 完整构造函数（用于 Web API 项目）
    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser) : base(options)
    {
        _currentUser = currentUser;
    }

    // ========== 订单上下文 ==========
    public DbSet<SalesOrder> SalesOrders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;
    public DbSet<CustomerProfile> CustomerProfiles { get; set; } = null!;
    public DbSet<ProductRequirement> ProductRequirements { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<OrderDemandAdjustment> OrderDemandAdjustments { get; set; } = null!;
    public DbSet<OrderListSummary> OrderListSummaries { get; set; } = null!;

    // ========== 工单上下文 ==========
    public DbSet<WorkOrder> WorkOrders { get; set; } = null!;
    public DbSet<WorkOrderListSummary> WorkOrderListSummaries { get; set; } = null!;
    public DbSet<WorkOrderExecutionSummary> WorkOrderExecutionSummaries { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<PurchaseSemiPlan> PurchaseSemiPlans { get; set; } = null!;
    public DbSet<PurchaseFinishedPlan> PurchaseFinishedPlans { get; set; } = null!;
    public DbSet<RoundBarPiercingPlan> RoundBarPiercingPlans { get; set; } = null!;
    public DbSet<InventoryPlan> InventoryPlans { get; set; } = null!;
    public DbSet<SemiPlanProcessGroup> SemiPlanProcessGroups { get; set; } = null!;
    public DbSet<InventoryPlanProcessGroup> InventoryPlanProcessGroups { get; set; } = null!;
    public DbSet<PiercingPlanProcessGroup> PiercingPlanProcessGroups { get; set; } = null!;
    public DbSet<InProcessReworkPlan> InProcessReworkPlans { get; set; } = null!;
    public DbSet<InProcessReworkPlanProcessGroup> InProcessReworkPlanProcessGroups { get; set; } = null!;
    public DbSet<InMainWorkOrderPlan> InMainWorkOrderPlans { get; set; } = null!;
    public DbSet<FixedLengthWorkOrder> FixedLengthWorkOrders { get; set; } = null!;

    // ========== 仓库上下文 ==========

    public DbSet<Warehouse> Warehouses { get; set; } = null!;
    public DbSet<InventoryBatch> InventoryBatches { get; set; } = null!;
    public DbSet<OutboundRecord> OutboundRecords { get; set; } = null!;

    // ========== 物料上下文 ==========

    public DbSet<Material> Materials { get; set; } = null!;
    public DbSet<SupplierProfile> SupplierProfiles { get; set; } = null!;
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; } = null!;
    public DbSet<SubcontractOrder> SubcontractOrders { get; set; } = null!;
    public DbSet<SubcontractReturnItem> SubcontractReturnItems { get; set; } = null!;

    // ========== 批次上下文 ==========

    public DbSet<ProductionBatch> ProductionBatches { get; set; } = null!;
    public DbSet<ProcessGroup> ProcessGroups { get; set; } = null!;
    public DbSet<ProductionBatchInventory> ProductionBatchInventories { get; set; } = null!;
    public DbSet<ProductionRecord> ProductionRecords { get; set; } = null!;
    public DbSet<SectionOutsource> SectionOutsources { get; set; } = null!;
    public DbSet<OutsourceRecovery> OutsourceRecoveries { get; set; } = null!;
    public DbSet<OperationLog> OperationLogs { get; set; } = null!;
    public DbSet<PicklingInRecord> PicklingInRecords { get; set; } = null!;
    public DbSet<PicklingOutRecord> PicklingOutRecords { get; set; } = null!;

    // ========== 质量上下文 ==========

    public DbSet<ProcessInspection> ProcessInspections { get; set; } = null!;
    public DbSet<MaterialReceiveCheck> MaterialReceiveChecks { get; set; } = null!;
    public DbSet<FurnaceRegistration> FurnaceRegistrations { get; set; } = null!;
    public DbSet<FinalInspection> FinalInspections { get; set; } = null!;
    public DbSet<ChemicalAnalysis> ChemicalAnalyses { get; set; } = null!;
    public DbSet<HardnessTest> HardnessTests { get; set; } = null!;
    public DbSet<GrainSizeTest> GrainSizeTests { get; set; } = null!;
    public DbSet<PittingCorrosionTest> PittingCorrosionTests { get; set; } = null!;
    public DbSet<IntergranularCorrosionTest> IntergranularCorrosionTests { get; set; } = null!;
    public DbSet<TensileTest> TensileTests { get; set; } = null!;
    public DbSet<MetallographicTest> MetallographicTests { get; set; } = null!;
    public DbSet<FlatteningTest> FlatteningTests { get; set; } = null!;
    public DbSet<FlaringTest> FlaringTests { get; set; } = null!;
    public DbSet<Ncr> Ncrs { get; set; } = null!;
    public DbSet<QualityProcessTracking> QualityProcessTrackings { get; set; } = null!;
    public DbSet<Certificate> Certificates { get; set; } = null!;
    public DbSet<CertificateItem> CertificateItems { get; set; } = null!;

    // ========== 设备上下文 ==========

    public DbSet<Equipment> Equipment { get; set; } = null!;
    public DbSet<RepairOrder> RepairOrders { get; set; } = null!;
    public DbSet<MaintenanceOrder> MaintenanceOrders { get; set; } = null!;
    public DbSet<InspectionRecord> InspectionRecords { get; set; } = null!;

    // ========== Scheduling 上下文 ==========
    public DbSet<RawMaterialLockPreExecution> RawMaterialLockPreExecutions { get; set; } = null!;
    public DbSet<WorkOrderPlan> WorkOrderPlans { get; set; } = null!;
    public DbSet<SectionFlowCategorySetting> SectionFlowCategorySettings { get; set; } = null!;
    public DbSet<SectionFlowCategoryItem> SectionFlowCategoryItems { get; set; } = null!;
    public DbSet<ColdRollSpecSchedule> ColdRollSpecSchedules { get; set; } = null!;
    public DbSet<BatchPlanSchedule> BatchPlanSchedules { get; set; } = null!;
    public DbSet<BatchPlanTarget> BatchPlanTargets { get; set; } = null!;

    // ========== Configuration 上下文 ==========
    public DbSet<StandardWorkDay> StandardWorkDays { get; set; } = null!;
    public DbSet<StandardWorkDayDeliveryState> StandardWorkDayDeliveryStates { get; set; } = null!;
    public DbSet<ConfigParameter> ConfigParameters { get; set; } = null!;
    public DbSet<DailyOutputEstimate> DailyOutputEstimates { get; set; } = null!;
    public DbSet<DailyProductionCapacity> DailyProductionCapacities { get; set; } = null!;
    public DbSet<Workstation> Workstations { get; set; } = null!;
    public DbSet<Employee> Employees { get; set; } = null!;

    // ========== StandardRegister 上下文 ==========
    public DbSet<StandardGradeMapping> StandardGradeMappings { get; set; } = null!;
    public DbSet<StandardRegister> StandardRegisters { get; set; } = null!;
    public DbSet<StandardRegisterItem> StandardRegisterItems { get; set; } = null!;
    public DbSet<GradeChemicalComposition> GradeChemicalCompositions { get; set; } = null!;
    public DbSet<GradePhysicalProperty> GradePhysicalProperties { get; set; } = null!;
    public DbSet<SubStandardQuickView> SubStandardQuickViews { get; set; } = null!;
    public DbSet<StandardInspectionRequirement> StandardInspectionRequirements { get; set; } = null!;
    public DbSet<ChemicalComposition> ChemicalCompositions { get; set; } = null!;
    public DbSet<ChemicalValidationRule> ChemicalValidationRules { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(entity =>
        {
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastLoginAt);
        });

        // ========== 订单上下文 ==========
        ConfigureSalesOrder(builder);
        ConfigureOrderItem(builder);
        ConfigureCustomerProfile(builder);
        ConfigureProductRequirement(builder);
        ConfigureRefreshToken(builder);
        ConfigureOrderDemandAdjustment(builder);
        ConfigureOrderListSummary(builder);

        // ========== 工单上下文 ==========
        ConfigureWorkOrder(builder);
        ConfigureWorkOrderListSummary(builder);
        ConfigureWorkOrderExecutionSummary(builder);
        ConfigureNotification(builder);
        ConfigurePurchaseSemiPlan(builder);
        ConfigurePurchaseFinishedPlan(builder);
        ConfigureRoundBarPiercingPlan(builder);
        ConfigureInventoryPlan(builder);
        ConfigureSemiPlanProcessGroup(builder);
        ConfigureInventoryPlanProcessGroup(builder);
        ConfigurePiercingPlanProcessGroup(builder);
        ConfigureInProcessReworkPlan(builder);
        ConfigureInProcessReworkPlanProcessGroup(builder);
        ConfigureInMainWorkOrderPlan(builder);
        ConfigureFixedLengthWorkOrder(builder);

        // ========== 物料上下文 ==========
        ConfigureMaterial(builder);
        ConfigureSupplierProfile(builder);
        ConfigurePurchaseOrder(builder);
        ConfigureSubcontractOrder(builder);
        ConfigureSubcontractReturnItem(builder);

        // ========== 仓库上下文 ==========
        ConfigureWarehouse(builder);
        ConfigureInventoryBatch(builder);
        ConfigureOutboundRecord(builder);
        // ========== 批次上下文 ==========
        ConfigureProductionBatch(builder);
        ConfigureProcessGroup(builder);
        ConfigureProductionBatchInventory(builder);
        ConfigureProductionRecord(builder);
        ConfigureSectionOutsource(builder);
        ConfigureOutsourceRecovery(builder);
        ConfigureOperationLog(builder);
        ConfigurePicklingInRecord(builder);
        ConfigurePicklingOutRecord(builder);

        // ========== 质量上下文 ==========
        ConfigureProcessInspection(builder);
        ConfigureMaterialReceiveCheck(builder);
        ConfigureFurnaceRegistration(builder);
        ConfigureFinalInspection(builder);
        ConfigureChemicalAnalysis(builder);
        ConfigureHardnessTest(builder);
        ConfigureGrainSizeTest(builder);
        ConfigurePittingCorrosionTest(builder);
        ConfigureIntergranularCorrosionTest(builder);
        ConfigureTensileTest(builder);
        ConfigureMetallographicTest(builder);
        ConfigureFlatteningTest(builder);
        ConfigureFlaringTest(builder);
        ConfigureNcr(builder);
        ConfigureQualityProcessTracking(builder);
        ConfigureCertificate(builder);
        ConfigureCertificateItem(builder);

        // ========== 设备上下文 ==========
        ConfigureEquipment(builder);
        ConfigureRepairOrder(builder);
        ConfigureMaintenanceOrder(builder);
        ConfigureInspectionRecord(builder);

        // ========== Scheduling 上下文 ==========
        ConfigureRawMaterialLockPreExecution(builder);
        ConfigureWorkOrderPlan(builder);
        ConfigureSectionFlowCategorySetting(builder);
        ConfigureSectionFlowCategoryItem(builder);
        ConfigureColdRollSpecSchedule(builder);
        ConfigureBatchPlanSchedule(builder);
        ConfigureBatchPlanTarget(builder);

        // ========== Configuration 上下文 ==========
        ConfigureStandardWorkDay(builder);
        ConfigureStandardWorkDayDeliveryState(builder);
        ConfigureConfigParameter(builder);
        ConfigureDailyOutputEstimate(builder);
        ConfigureDailyProductionCapacity(builder);
        ConfigureEmployee(builder);
        ConfigureWorkstation(builder);

        // ========== StandardRegister 上下文 ==========
        ConfigureStandardGradeMapping(builder);
        ConfigureStandardRegister(builder);
        ConfigureStandardRegisterItem(builder);
        ConfigureGradeChemicalComposition(builder);
        ConfigureGradePhysicalProperty(builder);
        ConfigureSubStandardQuickView(builder);
        ConfigureStandardInspectionRequirement(builder);
        ConfigureChemicalComposition(builder);
        ConfigureChemicalValidationRule(builder);

        // 为所有继承 BaseEntity 的实体统一配置审计字段长度
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                builder.Entity(entityType.ClrType).Property("CreatedBy").HasMaxLength(50);
                builder.Entity(entityType.ClrType).Property("UpdatedBy").HasMaxLength(50);
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<IAuditableEntity>();
        var now = DateTimeOffset.Now;
        var currentUser = GetCurrentUser();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedTime = now;
                    entry.Entity.UpdatedTime = now;
                    entry.Entity.CreatedBy = currentUser;
                    entry.Entity.UpdatedBy = currentUser;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedTime = now;
                    entry.Entity.UpdatedBy = currentUser;
                    break;

                case EntityState.Deleted:
                    // 所有实体统一使用物理删除，保持 Deleted 状态让 EF Core 执行物理删除
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    private string GetCurrentUser()
    {
        if (_currentUser == null)
            return "system";

        var userName = _currentUser.GetUserName();
        return string.IsNullOrEmpty(userName) ? "system" : userName;
    }
}
