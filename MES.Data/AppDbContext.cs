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
using MES.Data.Entities.ProductionStandard;
using MES.Data.Entities.Quality;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.WorkOrder;
using MES.Core.Enums;

namespace MES.Data;

public class AppDbContext : IdentityDbContext<AppUser>
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
    public DbSet<OrderChangeNotification> OrderChangeNotifications { get; set; } = null!;
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

    // ========== 仓库上下文 ==========

    public DbSet<Warehouse> Warehouses { get; set; } = null!;
    public DbSet<InventoryBatch> InventoryBatches { get; set; } = null!;
    public DbSet<OutboundRecord> OutboundRecords { get; set; } = null!;
    public DbSet<InventoryBatchDeleteLog> InventoryBatchDeleteLogs { get; set; } = null!;

    // ========== 物料上下文 ==========

    public DbSet<Material> Materials { get; set; } = null!;
    public DbSet<SupplierProfile> SupplierProfiles { get; set; } = null!;
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; } = null!;
    public DbSet<SubcontractOrder> SubcontractOrders { get; set; } = null!;
    public DbSet<SubcontractReturnItem> SubcontractReturnItems { get; set; } = null!;

    // ========== 批次上下文 ==========

    public DbSet<ProductionBatch> ProductionBatches { get; set; } = null!;
    public DbSet<ProcessGroup> ProcessGroups { get; set; } = null!;
    public DbSet<ProductionRecord> ProductionRecords { get; set; } = null!;
    public DbSet<SectionOutsource> SectionOutsources { get; set; } = null!;
    public DbSet<OutsourceRecovery> OutsourceRecoveries { get; set; } = null!;
    public DbSet<BatchOperationLog> BatchOperationLogs { get; set; } = null!;
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

    // ========== 生产标准上下文 ==========
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
        ConfigureOrderChangeNotification(builder);
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
        ConfigureInventoryBatchDeleteLog(builder);

        // ========== 批次上下文 ==========
        ConfigureProductionBatch(builder);
        ConfigureProcessGroup(builder);
        ConfigureProductionRecord(builder);
        ConfigureSectionOutsource(builder);
        ConfigureOutsourceRecovery(builder);
        ConfigureBatchOperationLog(builder);
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

        // ========== 生产标准上下文 ==========
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

    private static void ConfigureSalesOrder(ModelBuilder builder)
    {
        builder.Entity<SalesOrder>(entity =>
        {
            entity.ToTable("SalesOrder");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SignDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(SalesOrderStatus.Pending);
            entity.Property(e => e.RowVersion).IsRequired().IsRowVersion();
            entity.HasIndex(e => e.OrderNumber).IsUnique().HasDatabaseName("UK_SalesOrder_OrderNumber");
            entity.HasIndex(e => e.CustomerId).HasDatabaseName("IX_SalesOrder_CustomerId");
            entity.HasIndex(e => e.SignDate).HasDatabaseName("IX_SalesOrder_SignDate");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_SalesOrder_Status");
            entity.HasOne(e => e.Customer).WithMany(c => c.SalesOrders).HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOrderItem(ModelBuilder builder)
    {
        builder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItem");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Sequence).IsRequired();
            entity.Property(e => e.OrderNumber).HasMaxLength(50);
            entity.Property(e => e.DeliveryDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.DelayPenalty).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.SettlementMethod).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.MaterialName).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.DeliveryState).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.StandardGrade).HasMaxLength(50);
            entity.Property(e => e.PlantGrade).HasMaxLength(50);
            entity.Property(e => e.Density).IsRequired().HasColumnType("decimal(18,4)");
            entity.Property(e => e.StandardNo).HasMaxLength(100);
            entity.Property(e => e.OuterDiameter).IsRequired().HasColumnType("decimal(18,3)");
            entity.Property(e => e.WallThickness).IsRequired().HasColumnType("decimal(18,3)");
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(50);
            entity.Property(e => e.OuterDiameterNegative).HasColumnName("OuterDiameterMinus").IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.OuterDiameterPositive).HasColumnName("OuterDiameterPlus").IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.WallThicknessNegative).HasColumnName("WallThicknessMinus").IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.WallThicknessPositive).HasColumnName("WallThicknessPlus").IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.LengthStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.MinLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MaxLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Quantity).HasDefaultValue(0);
            entity.Property(e => e.Meters).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ContractWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.TheoreticalWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.HasIndex(e => new { e.SalesOrderId, e.Sequence })
                .HasDatabaseName("UK_OrderItem_Sequence_Active")
                .IsUnique();
            entity.HasIndex(e => e.SalesOrderId).HasDatabaseName("IX_OrderItem_SalesOrderId");
            entity.HasOne(e => e.SalesOrder).WithMany(s => s.OrderItems).HasForeignKey(e => e.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureCustomerProfile(ModelBuilder builder)
    {
        builder.Entity<CustomerProfile>(entity =>
        {
            entity.ToTable("CustomerProfile");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CustomerCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Salesman).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CustomerUnit).IsRequired().HasMaxLength(200);
            entity.Property(e => e.EndCustomer).HasMaxLength(200);
            entity.Property(e => e.ContactPerson).HasMaxLength(50);
            entity.Property(e => e.ContactPhone).HasMaxLength(50);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(CustomerStatus.Active);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.HasIndex(e => e.CustomerCode).IsUnique().HasDatabaseName("UK_CustomerProfile_Code");
            entity.HasIndex(e => e.CustomerUnit).HasDatabaseName("IX_CustomerProfile_CustomerUnit");
        });
    }

    private static void ConfigureProductRequirement(ModelBuilder builder)
    {
        builder.Entity<ProductRequirement>(entity =>
        {
            entity.ToTable("ProductRequirement");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderItemId).IsRequired();
            entity.Property(e => e.OrderNo).HasMaxLength(50);
            entity.Property(e => e.ItemSequence);
            entity.Property(e => e.RequirementType).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(RequirementType.Normal);
            entity.Property(e => e.ChemicalComposition).HasMaxLength(1000);
            entity.Property(e => e.MechanicalProperty).HasMaxLength(500);
            entity.Property(e => e.ToleranceRequirement).HasMaxLength(500);
            entity.Property(e => e.SurfaceQuality).HasMaxLength(500);
            entity.Property(e => e.NdtRequirement).HasMaxLength(500);
            entity.Property(e => e.OtherRequirement).HasMaxLength(1000);
            entity.HasIndex(e => e.OrderItemId).IsUnique().HasDatabaseName("UK_ProductRequirement_OrderItemId");
            entity.HasIndex(e => e.RequirementType).HasDatabaseName("IX_ProductRequirement_RequirementType");
            entity.HasOne(e => e.OrderItem).WithOne(oi => oi.ProductRequirement).HasForeignKey<ProductRequirement>(e => e.OrderItemId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureStandardGradeMapping(ModelBuilder builder)
    {
        builder.Entity<StandardGradeMapping>(entity =>
        {
            entity.ToTable("StandardGradeMapping");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StandardGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.StandardGradeCategory).HasMaxLength(50);
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Density).IsRequired().HasColumnType("decimal(18,4)");
            entity.Property(e => e.HeatTreatment).HasMaxLength(100);
            entity.Property(e => e.SpecialMaterial).HasDefaultValue(false);
            entity.Property(e => e.SpecialNote).HasMaxLength(500);
            entity.Property(e => e.SteelProperty).IsRequired().HasMaxLength(20).HasDefaultValue("镍基合金");
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.HasIndex(e => new { e.StandardGrade, e.StandardGradeCategory }).IsUnique().HasDatabaseName("UK_StandardGradeMapping_StandardGrade_Category");
            entity.HasIndex(e => e.PlantGrade).HasDatabaseName("IX_StandardGradeMapping_PlantGrade");
            entity.HasIndex(e => e.SpecialMaterial).HasDatabaseName("IX_StandardGradeMapping_SpecialMaterial");
        });
    }

    private static void ConfigureGradeChemicalComposition(ModelBuilder builder)
    {
        builder.Entity<GradeChemicalComposition>(entity =>
        {
            entity.ToTable("GradeChemicalComposition");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StandardGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.StandardGradeCategory).HasMaxLength(50);
            entity.Property(e => e.Carbon).HasMaxLength(100);
            entity.Property(e => e.Silicon).HasMaxLength(100);
            entity.Property(e => e.Manganese).HasMaxLength(100);
            entity.Property(e => e.Phosphorus).HasMaxLength(100);
            entity.Property(e => e.Sulfur).HasMaxLength(100);
            entity.Property(e => e.Nickel).HasMaxLength(100);
            entity.Property(e => e.Chromium).HasMaxLength(100);
            entity.Property(e => e.Molybdenum).HasMaxLength(100);
            entity.Property(e => e.Copper).HasMaxLength(100);
            entity.Property(e => e.Nitrogen).HasMaxLength(100);
            entity.Property(e => e.Niobium).HasMaxLength(100);
            entity.Property(e => e.Titanium).HasMaxLength(100);
            entity.Property(e => e.Iron).HasMaxLength(100);
            entity.Property(e => e.Aluminum).HasMaxLength(100);
            entity.Property(e => e.Tungsten).HasMaxLength(100);
            entity.HasIndex(e => new { e.StandardGrade, e.StandardGradeCategory }).IsUnique().HasDatabaseName("UK_GradeChemicalComposition_StandardGrade_Category");
        });
    }

    private static void ConfigureSubStandardQuickView(ModelBuilder builder)
    {
        builder.Entity<SubStandardQuickView>(entity =>
        {
            entity.ToTable("SubStandardQuickView");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StandardNo).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ChemicalComposition).HasMaxLength(200);
            entity.Property(e => e.HydrostaticTest).HasMaxLength(200);
            entity.Property(e => e.EddyCurrent).HasMaxLength(200);
            entity.Property(e => e.UltrasonicTest).HasMaxLength(200);
            entity.Property(e => e.RadiographicTest).HasMaxLength(200);
            entity.Property(e => e.HardnessRockwell).HasMaxLength(200);
            entity.Property(e => e.HardnessBrinell).HasMaxLength(200);
            entity.Property(e => e.HardnessVickers).HasMaxLength(200);
            entity.Property(e => e.TensileRoomTemp).HasMaxLength(200);
            entity.Property(e => e.TensileHighTemp).HasMaxLength(200);
            entity.Property(e => e.WeldJointTensile).HasMaxLength(200);
            entity.Property(e => e.ImpactTest).HasMaxLength(200);
            entity.Property(e => e.WeldJointImpact).HasMaxLength(200);
            entity.Property(e => e.FlatteningTest).HasMaxLength(200);
            entity.Property(e => e.FlaringTest).HasMaxLength(200);
            entity.Property(e => e.ExpandingTest).HasMaxLength(200);
            entity.Property(e => e.BendTest).HasMaxLength(200);
            entity.Property(e => e.WeldJointBend).HasMaxLength(200);
            entity.Property(e => e.GrainSize).HasMaxLength(200);
            entity.Property(e => e.IntergranularCorrosion).HasMaxLength(200);
            entity.Property(e => e.PittingCorrosion).HasMaxLength(200);
            entity.Property(e => e.FerriteContent).HasMaxLength(200);
            entity.Property(e => e.Macrostructure).HasMaxLength(200);
            entity.HasIndex(e => e.StandardNo).IsUnique().HasDatabaseName("UK_SubStandardQuickView_StandardNo");
        });
    }

    private static void ConfigureStandardInspectionRequirement(ModelBuilder builder)
    {
        builder.Entity<StandardInspectionRequirement>(entity =>
        {
            entity.ToTable("StandardInspectionRequirement");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StandardNo).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ChemicalComposition).HasMaxLength(200);
            entity.Property(e => e.HydrostaticTest).HasMaxLength(200);
            entity.Property(e => e.EddyCurrent).HasMaxLength(200);
            entity.Property(e => e.UltrasonicTest).HasMaxLength(200);
            entity.Property(e => e.RadiographicTest).HasMaxLength(200);
            entity.Property(e => e.HardnessRockwell).HasMaxLength(200);
            entity.Property(e => e.HardnessBrinell).HasMaxLength(200);
            entity.Property(e => e.HardnessVickers).HasMaxLength(200);
            entity.Property(e => e.TensileRoomTemp).HasMaxLength(200);
            entity.Property(e => e.TensileHighTemp).HasMaxLength(200);
            entity.Property(e => e.WeldJointTensile).HasMaxLength(200);
            entity.Property(e => e.ImpactTest).HasMaxLength(200);
            entity.Property(e => e.WeldJointImpact).HasMaxLength(200);
            entity.Property(e => e.FlatteningTest).HasMaxLength(200);
            entity.Property(e => e.FlaringTest).HasMaxLength(200);
            entity.Property(e => e.ExpandingTest).HasMaxLength(200);
            entity.Property(e => e.BendTest).HasMaxLength(200);
            entity.Property(e => e.WeldJointBend).HasMaxLength(200);
            entity.Property(e => e.GrainSize).HasMaxLength(200);
            entity.Property(e => e.IntergranularCorrosion).HasMaxLength(200);
            entity.Property(e => e.PittingCorrosion).HasMaxLength(200);
            entity.Property(e => e.FerriteContent).HasMaxLength(200);
            entity.Property(e => e.Macrostructure).HasMaxLength(200);
            entity.HasIndex(e => e.StandardNo).IsUnique().HasDatabaseName("UK_StandardInspectionRequirement_StandardNo");
        });
    }

    private static void ConfigureGradePhysicalProperty(ModelBuilder builder)
    {
        builder.Entity<GradePhysicalProperty>(entity =>
        {
            entity.ToTable("GradePhysicalProperty");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StandardGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.StandardGradeCategory).HasMaxLength(50);
            entity.Property(e => e.Density).IsRequired().HasColumnType("decimal(18,4)");
            entity.Property(e => e.HeatTreatmentTemp).HasMaxLength(100);
            entity.Property(e => e.HardnessRockwell).HasMaxLength(100);
            entity.Property(e => e.HardnessVickers).HasMaxLength(100);
            entity.Property(e => e.HardnessBrinell).HasMaxLength(100);
            entity.Property(e => e.TensileStrength).HasMaxLength(100);
            entity.Property(e => e.YieldStrength02).HasMaxLength(100);
            entity.Property(e => e.YieldStrength10).HasMaxLength(100);
            entity.Property(e => e.Elongation).HasMaxLength(100);
            entity.Property(e => e.GrainSize).HasMaxLength(100);
            entity.HasIndex(e => new { e.StandardGrade, e.StandardGradeCategory }).IsUnique().HasDatabaseName("UK_GradePhysicalProperty_StandardGrade_Category");
        });
    }

    private static void ConfigureWorkOrder(ModelBuilder builder)
    {
        builder.Entity<WorkOrder>(entity =>
        {
            entity.ToTable("WorkOrder");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkOrderNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SalesOrderNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProductionMainNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProductionSubNo).HasMaxLength(50);
            entity.Property(e => e.OrderItemIds).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(WorkOrderStatus.NotGenerated);
            entity.Property(e => e.RowVersion).IsRequired().IsRowVersion();
            entity.Property(e => e.SignDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Salesman).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EndCustomer).HasMaxLength(200);
            entity.Property(e => e.DeliveryDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.MaterialName).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.SettlementMethod).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.StandardCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DeliveryState).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(50);
            entity.Property(e => e.OuterDiameterNegative).HasColumnName("OuterDiameterMinus").IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0);
            entity.Property(e => e.OuterDiameterPositive).HasColumnName("OuterDiameterPlus").IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0);
            entity.Property(e => e.WallThicknessNegative).HasColumnName("WallThicknessMinus").IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0);
            entity.Property(e => e.WallThicknessPositive).HasColumnName("WallThicknessPlus").IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0);
            entity.Property(e => e.LengthStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.MinLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MaxLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.TotalMeters).IsRequired().HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.TotalWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0);
            entity.Property(e => e.TotalItemCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.ItemDetails).HasColumnType("nvarchar(max)");
            entity.Property(e => e.TechnicalRequirements).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(RequirementType.Normal);

            // 用料计划状态
            entity.Property(e => e.MaterialPlanStatus).IsRequired().HasDefaultValue(MaterialPlanStatus.NotPlanned);
            entity.Property(e => e.MaterialPlanRate).IsRequired().HasColumnType("decimal(5,2)").HasDefaultValue(0m);

            // 索引
            entity.HasIndex(e => e.WorkOrderNo).IsUnique().HasDatabaseName("UK_WorkOrder_WorkOrderNo");
            entity.HasIndex(e => new { e.SalesOrderNo, e.ProductionMainNo, e.ProductionSubNo })
                .IsUnique()
                .HasDatabaseName("UK_WorkOrder_MainSub");
            entity.HasIndex(e => e.SalesOrderNo).HasDatabaseName("IX_WorkOrder_SalesOrderNo");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_WorkOrder_Status");
            entity.HasIndex(e => e.DeliveryDate).HasDatabaseName("IX_WorkOrder_DeliveryDate");
            entity.HasIndex(e => e.MaterialName).HasDatabaseName("IX_WorkOrder_MaterialName");
            entity.HasIndex(e => e.Specification).HasDatabaseName("IX_WorkOrder_Specification");
        });
    }

    private static void ConfigureOrderChangeNotification(ModelBuilder builder)
    {
        builder.Entity<OrderChangeNotification>(entity =>
        {
            entity.ToTable("OrderChangeNotification");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ChangeType).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.WorkOrderCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.IsRead).IsRequired().HasDefaultValue(false);
            entity.HasIndex(e => e.CreatedTime).HasDatabaseName("IX_OrderChangeNotification_CreatedTime");
            entity.HasIndex(e => e.IsRead).HasDatabaseName("IX_OrderChangeNotification_IsRead");
            entity.HasIndex(e => e.OrderNumber).HasDatabaseName("IX_OrderChangeNotification_OrderNumber");
        });
    }

    private static void ConfigureRefreshToken(ModelBuilder builder)
    {
        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshToken");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(200);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Expires).IsRequired();
            entity.Property(e => e.IsRevoked).IsRequired().HasDefaultValue(false);
            entity.HasIndex(e => e.Token).IsUnique().HasDatabaseName("UK_RefreshToken_Token");
            entity.HasIndex(e => e.UserId).HasDatabaseName("IX_RefreshToken_UserId");
            entity.HasIndex(e => e.Expires).HasDatabaseName("IX_RefreshToken_Expires");
        });
    }

    private static void ConfigurePurchaseSemiPlan(ModelBuilder builder)
    {
        builder.Entity<PurchaseSemiPlan>(entity =>
        {
            entity.ToTable("PurchaseSemiPlan");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkOrderId).IsRequired();
            entity.Property(e => e.PlanDate).IsRequired().HasColumnType("date");
            entity.Property(e => e.AdjustedWallThickness).IsRequired().HasColumnType("decimal(18,3)");
            entity.Property(e => e.YieldRate).IsRequired().HasColumnType("decimal(5,2)");
            entity.Property(e => e.InputMultiple).IsRequired().HasDefaultValue(1);
            entity.Property(e => e.QualifiedRate).IsRequired().HasColumnType("decimal(5,2)");
            entity.Property(e => e.Density).HasColumnType("decimal(18,4)");
            entity.Property(e => e.UnitWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.RawUnitWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.RequiredPieces);
            entity.Property(e => e.RequiredWeight).IsRequired().HasColumnType("decimal(18,3)");
            entity.Property(e => e.RawMaterialType).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.RawMaterialSpec).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RequiredUnitWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.RequiredDate).IsRequired().HasColumnType("date");
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.StandardCycle).IsRequired().HasDefaultValue(0);
            entity.HasIndex(e => e.WorkOrderId).HasDatabaseName("IX_PurchaseSemiPlan_WorkOrderId");

            entity.HasOne<WorkOrder>()
                .WithMany()
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePurchaseFinishedPlan(ModelBuilder builder)
    {
        builder.Entity<PurchaseFinishedPlan>(entity =>
        {
            entity.ToTable("PurchaseFinishedPlan");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkOrderId).IsRequired();
            entity.Property(e => e.PlanDate).IsRequired().HasColumnType("date");
            entity.Property(e => e.ProductType).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.RequiredPiece);
            entity.Property(e => e.RequiredWeight).IsRequired().HasColumnType("decimal(18,3)");
            entity.Property(e => e.InputMultiple);
            entity.Property(e => e.RequiredDate).HasColumnType("date");
            entity.Property(e => e.Remark).HasMaxLength(500);

            // 工单冗余字段
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(50);
            entity.Property(e => e.OuterDiameterNegative).HasColumnType("decimal(18,3)");
            entity.Property(e => e.OuterDiameterPositive).HasColumnType("decimal(18,3)");
            entity.Property(e => e.WallThicknessNegative).HasColumnType("decimal(18,3)");
            entity.Property(e => e.WallThicknessPositive).HasColumnType("decimal(18,3)");
            entity.Property(e => e.LengthStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.MinLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MaxLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DeliveryState).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.StandardCycle).IsRequired().HasDefaultValue(0);

            entity.HasIndex(e => e.WorkOrderId).HasDatabaseName("IX_PurchaseFinishedPlan_WorkOrderId");

            entity.HasOne<WorkOrder>()
                .WithMany()
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureRoundBarPiercingPlan(ModelBuilder builder)
    {
        builder.Entity<RoundBarPiercingPlan>(entity =>
        {
            entity.ToTable("RoundBarPiercingPlan");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkOrderId).IsRequired();
            entity.Property(e => e.PlanDate).IsRequired().HasColumnType("date");
            entity.Property(e => e.AdjustedWallThickness).IsRequired().HasColumnType("decimal(18,3)");
            entity.Property(e => e.YieldRate).IsRequired().HasColumnType("decimal(5,2)");
            entity.Property(e => e.InputMultiple).IsRequired().HasDefaultValue(1);
            entity.Property(e => e.QualifiedRate).IsRequired().HasColumnType("decimal(5,2)");
            entity.Property(e => e.Density).HasColumnType("decimal(18,4)");
            entity.Property(e => e.UnitWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.RawUnitWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.RequiredPieces);
            entity.Property(e => e.RequiredWeight).IsRequired().HasColumnType("decimal(18,3)");
            entity.Property(e => e.RawMaterialType).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.RoundBarSpec).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PiercingSpec).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RequiredUnitWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.RequiredDate).IsRequired().HasColumnType("date");
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.StandardCycle).IsRequired().HasDefaultValue(0);
            entity.HasIndex(e => e.WorkOrderId).HasDatabaseName("IX_RoundBarPiercingPlan_WorkOrderId");

            entity.HasOne<WorkOrder>()
                .WithMany()
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    // ================================================================
    //                      仓库上下文配置
    // ================================================================

    private static void ConfigureWarehouse(ModelBuilder builder)
    {
        builder.Entity<Warehouse>(entity =>
        {
            entity.ToTable("Warehouse");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_Warehouse_Code");
        });
    }

    private static void ConfigureInventoryBatch(ModelBuilder builder)
    {
        builder.Entity<InventoryBatch>(entity =>
        {
            entity.ToTable("InventoryBatch");
            entity.HasKey(e => e.Id);

            // 基础标识
            entity.Property(e => e.BatchNo).IsRequired().HasMaxLength(50);

            // 仓库与物料
            entity.Property(e => e.WarehouseId).IsRequired();
            entity.Property(e => e.MaterialType).IsRequired().HasMaxLength(30);
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);

            // 来源信息
            entity.Property(e => e.InboundSource).IsRequired().HasMaxLength(20);
            entity.Property(e => e.SourceName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.InboundDate).IsRequired().HasColumnType("datetime2");

            // 钢种与规格
            entity.Property(e => e.HeatNo).HasMaxLength(50);
            entity.Property(e => e.ProductionBatchNo).HasMaxLength(50);
            entity.Property(e => e.LengthStatus).HasMaxLength(20);
            entity.Property(e => e.MinLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MaxLength).HasColumnType("decimal(18,2)");

            // 数量与重量
            entity.Property(e => e.InitialQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.InitialWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.UnitWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Meters).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RemainingQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.RemainingWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);

            // 实际规格
            entity.Property(e => e.ActualSpecification).HasMaxLength(100);
            entity.Property(e => e.ActualOuterDiameter).HasColumnType("decimal(18,3)");
            entity.Property(e => e.ActualWallThickness).HasColumnType("decimal(18,3)");

            // 位置与状态
            entity.Property(e => e.SurfaceCondition).HasMaxLength(50);
            entity.Property(e => e.LocationArea).HasMaxLength(50);
            entity.Property(e => e.LocationRack).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(500);

            // 次品相关
            entity.Property(e => e.DefectReason).HasMaxLength(200);
            entity.Property(e => e.LiabilityType).HasMaxLength(50);
            entity.Property(e => e.OriginalSupplier).HasMaxLength(200);
            entity.Property(e => e.TagNo).HasMaxLength(50);
            entity.Property(e => e.DefectRemark).HasMaxLength(500);

            // 工单关联
            entity.Property(e => e.IsLinkedToWorkOrder).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.WorkOrderNo).HasMaxLength(50);
            entity.Property(e => e.SalesOrderNo).HasMaxLength(50);
            entity.Property(e => e.OrderItemIds).HasMaxLength(500);

            // 乐观并发控制
            entity.Property(e => e.RowVersion).IsRowVersion();

            // 索引
            entity.HasIndex(e => e.BatchNo).IsUnique().HasDatabaseName("UK_InventoryBatch_BatchNo");
            entity.HasIndex(e => e.WarehouseId).HasDatabaseName("IX_InventoryBatch_WarehouseId");
            entity.HasIndex(e => e.MaterialType).HasDatabaseName("IX_InventoryBatch_MaterialType");
            entity.HasIndex(e => e.PlantGrade).HasDatabaseName("IX_InventoryBatch_PlantGrade");
            entity.HasIndex(e => e.WorkOrderNo).HasDatabaseName("IX_InventoryBatch_WorkOrderNo");
            entity.HasIndex(e => e.SalesOrderNo).HasDatabaseName("IX_InventoryBatch_SalesOrderNo");
            entity.HasIndex(e => e.ProductionBatchNo).HasDatabaseName("IX_InventoryBatch_ProductionBatchNo");
            entity.HasIndex(e => e.RemainingWeight).HasDatabaseName("IX_InventoryBatch_RemainingWeight")
                .HasFilter("[RemainingWeight] > 0");

            entity.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureOutboundRecord(ModelBuilder builder)
    {
        builder.Entity<OutboundRecord>(entity =>
        {
            entity.ToTable("OutboundRecord");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.InventoryBatchId).IsRequired();
            entity.Property(e => e.OutboundType).IsRequired().HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.SourceOrderNo).HasMaxLength(50);
            entity.Property(e => e.TargetCompany).HasMaxLength(200);
            entity.Property(e => e.OutboundQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.OutboundWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.OutboundDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Remark).HasMaxLength(500);

            // 审计字段
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).IsRequired().HasMaxLength(50);

            // 索引
            entity.HasIndex(e => e.InventoryBatchId).HasDatabaseName("IX_OutboundRecord_InventoryBatchId");
            entity.HasIndex(e => e.OutboundDate).HasDatabaseName("IX_OutboundRecord_OutboundDate");

            entity.HasOne<InventoryBatch>()
                .WithMany()
                .HasForeignKey(e => e.InventoryBatchId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureNotification(ModelBuilder builder)
    {
        builder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notification");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.NotificationType).IsRequired().HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(500);
            entity.Property(e => e.IsRead).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.Receiver).IsRequired().HasMaxLength(50);
        });
    }

    private static void ConfigureInventoryBatchDeleteLog(ModelBuilder builder)
    {
        builder.Entity<InventoryBatchDeleteLog>(entity =>
        {
            entity.ToTable("InventoryBatchDeleteLog");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.BatchNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Operator).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DeletedTime).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.BatchData).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property(e => e.Reason).HasMaxLength(500);
        });
    }

    private static void ConfigureInventoryPlan(ModelBuilder builder)
    {
        builder.Entity<InventoryPlan>(entity =>
        {
            entity.ToTable("InventoryPlan");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkOrderId).IsRequired();
            entity.Property(e => e.PlanDate).IsRequired().HasColumnType("date");
            entity.Property(e => e.InventoryBatchNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.BatchNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.MaterialType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LocationArea).HasMaxLength(100);
            entity.Property(e => e.LocationRack).HasMaxLength(100);
            entity.Property(e => e.InputMultiple).IsRequired().HasDefaultValue(1);
            entity.Property(e => e.UsageMode).IsRequired().HasMaxLength(10).HasDefaultValue("All");
            entity.Property(e => e.UsedQuantity);
            entity.Property(e => e.UsedWeight).IsRequired().HasColumnType("decimal(18,3)");
            entity.Property(e => e.RequiredDate).HasColumnType("date");
            entity.Property(e => e.PlanStatus).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(InventoryPlanStatus.Planned);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.ReworkType).HasMaxLength(20).HasConversion<string>();
            entity.Property(e => e.StandardCycle).IsRequired().HasDefaultValue(0);
            entity.HasIndex(e => e.WorkOrderId).HasDatabaseName("IX_InventoryPlan_WorkOrderId");
            entity.HasIndex(e => e.InventoryBatchNo).HasDatabaseName("IX_InventoryPlan_InventoryBatchNo");
            entity.HasIndex(e => e.PlanStatus).HasDatabaseName("IX_InventoryPlan_PlanStatus");
        });
    }

    // ================================================================
    //                      物料上下文配置
    // ================================================================

    private static void ConfigureMaterial(ModelBuilder builder)
    {
        builder.Entity<Material>(entity =>
        {
            entity.ToTable("Material");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MaterialCode).IsRequired().HasMaxLength(6);
            entity.Property(e => e.MaterialCategory).IsRequired().HasMaxLength(30);
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.HasIndex(e => e.MaterialCode).IsUnique().HasDatabaseName("UK_Material_Code");
            entity.HasIndex(e => new { e.MaterialCategory, e.PlantGrade, e.Specification })
                .IsUnique()
                .HasDatabaseName("UK_Material_Combo");
            entity.HasIndex(e => e.MaterialCategory).HasDatabaseName("IX_Material_Category");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_Material_IsActive");
        });
    }

    private static void ConfigureSupplierProfile(ModelBuilder builder)
    {
        builder.Entity<SupplierProfile>(entity =>
        {
            entity.ToTable("SupplierProfile");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SupplierCode).IsRequired().HasMaxLength(6);
            entity.Property(e => e.SupplierName).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.SupplierCode).IsUnique().HasDatabaseName("UK_Supplier_Code");
            entity.Property(e => e.ContactPerson).HasMaxLength(50);
            entity.Property(e => e.ContactPhone).HasMaxLength(50);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.MaterialCategory).HasMaxLength(100);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.Remark).HasMaxLength(500);
        });
    }

    private static void ConfigurePurchaseOrder(ModelBuilder builder)
    {
        builder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("PurchaseOrder");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNo).IsRequired().HasMaxLength(20);
            entity.Property(e => e.SupplierId).IsRequired();
            entity.Property(e => e.SupplierName).HasMaxLength(200);
            entity.Property(e => e.OrderDate).IsRequired().HasColumnType("date");
            entity.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(PurchaseOrderStatus.Open);
            entity.Property(e => e.IsForceCompleted).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.MaterialCategory).IsRequired().HasMaxLength(30);
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UnitWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Quantity);
            entity.Property(e => e.Weight).IsRequired().HasColumnType("decimal(18,3)");
            entity.Property(e => e.RequiredDate).IsRequired().HasColumnType("date");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.LastArrivalDate).HasColumnType("date");
            entity.Property(e => e.ReceivedQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.ReceivedWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.SourceWorkOrderNo).HasMaxLength(50);
            entity.Property(e => e.InputMultiple);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.HasIndex(e => e.OrderNo).IsUnique().HasDatabaseName("UK_PurchaseOrder_OrderNo");
            entity.HasIndex(e => e.SupplierId).HasDatabaseName("IX_PurchaseOrder_SupplierId");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_PurchaseOrder_Status");
            entity.HasIndex(e => e.SourceWorkOrderNo).HasDatabaseName("IX_PurchaseOrder_SourceWO");
            entity.HasIndex(e => e.RequiredDate).HasDatabaseName("IX_PurchaseOrder_RequiredDate");

            entity.HasOne<SupplierProfile>()
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureSubcontractOrder(ModelBuilder builder)
    {
        builder.Entity<SubcontractOrder>(entity =>
        {
            entity.ToTable("SubcontractOrder");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNo).IsRequired().HasMaxLength(20);
            entity.Property(e => e.SupplierId).IsRequired();
            entity.Property(e => e.SupplierName).HasMaxLength(200);
            entity.Property(e => e.OrderDate).IsRequired().HasColumnType("date");
            entity.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(SubcontractOrderStatus.Sent);
            entity.Property(e => e.IsForceCompleted).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.FurnaceNumber).HasMaxLength(50);
            entity.Property(e => e.ProcessType).IsRequired().HasMaxLength(30);
            entity.Property(e => e.OutMaterialCategory).IsRequired().HasMaxLength(30);
            entity.Property(e => e.OutPlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.OutSpecification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.OutQuantity).IsRequired();
            entity.Property(e => e.OutWeight).IsRequired().HasColumnType("decimal(18,3)");
            entity.Property(e => e.ReturnDeadline).HasColumnType("date");
            entity.Property(e => e.InQuantity);
            entity.Property(e => e.InWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.HasIndex(e => e.OrderNo).IsUnique().HasDatabaseName("UK_SubcontractOrder_OrderNo");
            entity.HasIndex(e => e.SupplierId).HasDatabaseName("IX_SubcontractOrder_SupplierId");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_SubcontractOrder_Status");

            entity.HasOne<SupplierProfile>()
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(e => e.ReturnItems)
                .WithOne(r => r.SubcontractOrder)
                .HasForeignKey(r => r.SubcontractOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSubcontractReturnItem(ModelBuilder builder)
    {
        builder.Entity<SubcontractReturnItem>(entity =>
        {
            entity.ToTable("SubcontractReturnItem");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SubcontractOrderId).IsRequired();
            entity.Property(e => e.OrderNo).HasMaxLength(50);
            entity.Property(e => e.Sequence).IsRequired();
            entity.Property(e => e.MaterialCategory).IsRequired().HasMaxLength(30);
            entity.Property(e => e.PlantGrade).HasMaxLength(50);
            entity.Property(e => e.ProcessSpecification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UnitWeight).HasColumnType("decimal(18,4)");
            entity.Property(e => e.RequiredWeight).HasColumnType("decimal(18,4)");
            entity.Property(e => e.InputMultiple);
            entity.Property(e => e.ProcessStatusRemark).HasMaxLength(500);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.ProcessUnitPrice).HasColumnType("decimal(18,4)");
            entity.Property(e => e.ProcessTotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SourceWorkOrderNo).HasMaxLength(50);
            entity.Property(e => e.ReturnedQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.ReturnedWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.ProcessStatus).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(SubcontractProcessStatus.Pending);
            entity.Property(e => e.IsForceCompleted).IsRequired().HasDefaultValue(false);
            entity.HasIndex(e => new { e.SubcontractOrderId, e.Sequence })
                .IsUnique()
                .HasDatabaseName("UK_ReturnItem_Seq");
            entity.HasIndex(e => e.SubcontractOrderId).HasDatabaseName("IX_ReturnItem_OrderId");
        });
    }

    // ================================================================
    //                      批次上下文配置
    // ================================================================

    private static void ConfigureProductionBatch(ModelBuilder builder)
    {
        builder.Entity<ProductionBatch>(entity =>
        {
            entity.ToTable("ProductionBatch");
            entity.HasKey(e => e.Id);

            // 批次自身字段
            entity.Property(e => e.BatchNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(BatchStatus.None);
            entity.Property(e => e.TagNo).HasMaxLength(50);
            entity.Property(e => e.ProductionType).HasMaxLength(20);
            entity.Property(e => e.ManufacturingItem).IsRequired().HasMaxLength(30);
            entity.Property(e => e.IsForceCompleted).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.QualityRemark).HasMaxLength(500);
            entity.Property(e => e.SolutionParams).HasMaxLength(500);
            entity.Property(e => e.CurrentExecDate).HasColumnType("datetime2");
            entity.Property(e => e.CurrentGroupName).HasMaxLength(50);
            entity.Property(e => e.CurrentSectionName).HasMaxLength(50);
            entity.Property(e => e.CurrentEquipmentName).HasMaxLength(100);
            entity.Property(e => e.CurrentOutsource).HasMaxLength(200);
            entity.Property(e => e.NextSectionName).HasMaxLength(50);
            entity.Property(e => e.CorrespondingSpec).HasMaxLength(100);
            entity.Property(e => e.NextProcess).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.RowVersion).IsRequired().IsRowVersion();

            // 工单冗余字段
            entity.Property(e => e.WorkOrderNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SalesOrderNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProductionMainNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProductionSubNo).HasMaxLength(50);
            entity.Property(e => e.OrderItemIds).IsRequired().HasMaxLength(500);
            entity.Property(e => e.SignDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Salesman).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EndCustomer).HasMaxLength(200);
            entity.Property(e => e.DeliveryDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.DelayPenalty).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.MaterialName).IsRequired().HasMaxLength(20);
            entity.Property(e => e.SettlementMethod).IsRequired().HasMaxLength(20);
            entity.Property(e => e.StandardCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DeliveryState).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.OuterDiameterNegative).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.OuterDiameterPositive).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.WallThicknessNegative).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.WallThicknessPositive).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.LengthStatus).IsRequired().HasMaxLength(20);
            entity.Property(e => e.MinLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MaxLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.TotalMeters).IsRequired().HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            entity.Property(e => e.TotalWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.TotalItemCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.ItemDetails).HasColumnType("nvarchar(max)");
            entity.Property(e => e.TechnicalRequirements).IsRequired().HasMaxLength(20);

            // 仓库冗余字段
            entity.Property(e => e.SourceBatchNo).HasMaxLength(50);
            entity.Property(e => e.SourceMaterialType).HasMaxLength(30);
            entity.Property(e => e.InboundSource).HasMaxLength(20);
            entity.Property(e => e.SourceName).HasMaxLength(200);
            entity.Property(e => e.InboundDate).HasColumnType("datetime2");
            entity.Property(e => e.SourceHeatNo).HasMaxLength(50);
            entity.Property(e => e.SourcePlantGrade).HasMaxLength(50);
            entity.Property(e => e.SourceSpecification).HasMaxLength(100);
            entity.Property(e => e.SourceLengthStatus).HasMaxLength(20);
            entity.Property(e => e.SourceUnitWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.InputWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.CurrentValidQty);
            entity.Property(e => e.CurrentValidWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.IsClosed).IsRequired().HasDefaultValue(false);

            // 索引
            entity.HasIndex(e => e.BatchNo).IsUnique().HasDatabaseName("UK_ProductionBatch_BatchNo");
            entity.HasIndex(e => e.WorkOrderNo).HasDatabaseName("IX_ProductionBatch_WorkOrderNo");
            entity.HasIndex(e => e.SalesOrderNo).HasDatabaseName("IX_ProductionBatch_SalesOrderNo");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_ProductionBatch_Status");
            entity.HasIndex(e => e.TagNo).HasDatabaseName("IX_ProductionBatch_TagNo");
        });
    }

    private static void ConfigureProcessGroup(ModelBuilder builder)
    {
        builder.Entity<ProcessGroup>(entity =>
        {
            entity.ToTable("ProcessGroup");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductionBatchId).IsRequired();
            entity.Property(e => e.SequenceNumber).IsRequired();
            entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ManufacturingSpec).HasMaxLength(100);
            entity.Property(e => e.OuterDiameterTolerance).HasMaxLength(50);
            entity.Property(e => e.WallThicknessTolerance).HasMaxLength(50);
            entity.Property(e => e.ManufacturingLength).HasMaxLength(100);
            entity.Property(e => e.CuttingTreatment).HasMaxLength(200);
            entity.Property(e => e.ManufacturingMultiple).IsRequired();
            entity.Property(e => e.BatchNo).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(500);

            // 15个工段字段（int?，无默认值）
            entity.Property(e => e.ColdRollDraw);
            entity.Property(e => e.OilPipeCut);
            entity.Property(e => e.Degrease);
            entity.Property(e => e.Solution);
            entity.Property(e => e.Straighten);
            entity.Property(e => e.Cut);
            entity.Property(e => e.ThicknessMeasure);
            entity.Property(e => e.Pickle);
            entity.Property(e => e.OuterPolish);
            entity.Property(e => e.InnerGrinding);
            entity.Property(e => e.OuterSpotGrinding);
            entity.Property(e => e.Inspection);
            entity.Property(e => e.WeldingHead);
            entity.Property(e => e.Lubrication);
            entity.Property(e => e.Warehouse);

            // 关系与索引
            entity.HasOne(e => e.ProductionBatch)
                .WithMany(p => p.ProcessGroups)
                .HasForeignKey(e => e.ProductionBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ProductionBatchId).HasDatabaseName("IX_ProcessGroup_BatchId");
            entity.HasIndex(e => new { e.ProductionBatchId, e.SequenceNumber })
                .IsUnique()
                .HasDatabaseName("UK_ProcessGroup_Seq");
        });
    }

    // ================================================================
    //                      用料计划工序组配置
    // ================================================================

    private static void ConfigureSemiPlanProcessGroup(ModelBuilder builder)
    {
        builder.Entity<SemiPlanProcessGroup>(entity =>
        {
            entity.ToTable("SemiPlanProcessGroup");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PurchaseSemiPlanId).IsRequired();
            entity.Property(e => e.SequenceNumber).IsRequired();
            entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ManufacturingSpec).HasMaxLength(100);
            entity.Property(e => e.OuterDiameterTolerance).HasMaxLength(50);
            entity.Property(e => e.WallThicknessTolerance).HasMaxLength(50);
            entity.Property(e => e.ManufacturingLength).HasMaxLength(100);
            entity.Property(e => e.CuttingTreatment).HasMaxLength(200);
            entity.Property(e => e.ManufacturingMultiple).IsRequired();
            entity.Property(e => e.Remark).HasMaxLength(500);

            entity.Property(e => e.ColdRollDraw);
            entity.Property(e => e.OilPipeCut);
            entity.Property(e => e.Degrease);
            entity.Property(e => e.Solution);
            entity.Property(e => e.Straighten);
            entity.Property(e => e.Cut);
            entity.Property(e => e.ThicknessMeasure);
            entity.Property(e => e.Pickle);
            entity.Property(e => e.OuterPolish);
            entity.Property(e => e.InnerGrinding);
            entity.Property(e => e.OuterSpotGrinding);
            entity.Property(e => e.Inspection);
            entity.Property(e => e.WeldingHead);
            entity.Property(e => e.Lubrication);
            entity.Property(e => e.Warehouse);

            entity.HasOne<PurchaseSemiPlan>()
                .WithMany()
                .HasForeignKey(e => e.PurchaseSemiPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.PurchaseSemiPlanId).HasDatabaseName("IX_SemiPlanProcessGroup_PlanId");
            entity.HasIndex(e => new { e.PurchaseSemiPlanId, e.SequenceNumber })
                .IsUnique()
                .HasDatabaseName("UK_SemiPlanProcessGroup_Seq");
        });
    }

    private static void ConfigureInventoryPlanProcessGroup(ModelBuilder builder)
    {
        builder.Entity<InventoryPlanProcessGroup>(entity =>
        {
            entity.ToTable("InventoryPlanProcessGroup");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.InventoryPlanId).IsRequired();
            entity.Property(e => e.SequenceNumber).IsRequired();
            entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ManufacturingSpec).HasMaxLength(100);
            entity.Property(e => e.OuterDiameterTolerance).HasMaxLength(50);
            entity.Property(e => e.WallThicknessTolerance).HasMaxLength(50);
            entity.Property(e => e.ManufacturingLength).HasMaxLength(100);
            entity.Property(e => e.CuttingTreatment).HasMaxLength(200);
            entity.Property(e => e.ManufacturingMultiple).IsRequired();
            entity.Property(e => e.Remark).HasMaxLength(500);

            entity.Property(e => e.ColdRollDraw);
            entity.Property(e => e.OilPipeCut);
            entity.Property(e => e.Degrease);
            entity.Property(e => e.Solution);
            entity.Property(e => e.Straighten);
            entity.Property(e => e.Cut);
            entity.Property(e => e.ThicknessMeasure);
            entity.Property(e => e.Pickle);
            entity.Property(e => e.OuterPolish);
            entity.Property(e => e.InnerGrinding);
            entity.Property(e => e.OuterSpotGrinding);
            entity.Property(e => e.Inspection);
            entity.Property(e => e.WeldingHead);
            entity.Property(e => e.Lubrication);
            entity.Property(e => e.Warehouse);

            entity.HasOne<InventoryPlan>()
                .WithMany()
                .HasForeignKey(e => e.InventoryPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.InventoryPlanId).HasDatabaseName("IX_InventoryPlanProcessGroup_PlanId");
            entity.HasIndex(e => new { e.InventoryPlanId, e.SequenceNumber })
                .IsUnique()
                .HasDatabaseName("UK_InventoryPlanProcessGroup_Seq");
        });
    }

    private static void ConfigurePiercingPlanProcessGroup(ModelBuilder builder)
    {
        builder.Entity<PiercingPlanProcessGroup>(entity =>
        {
            entity.ToTable("PiercingPlanProcessGroup");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RoundBarPiercingPlanId).IsRequired();
            entity.Property(e => e.SequenceNumber).IsRequired();
            entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ManufacturingSpec).HasMaxLength(100);
            entity.Property(e => e.OuterDiameterTolerance).HasMaxLength(50);
            entity.Property(e => e.WallThicknessTolerance).HasMaxLength(50);
            entity.Property(e => e.ManufacturingLength).HasMaxLength(100);
            entity.Property(e => e.CuttingTreatment).HasMaxLength(200);
            entity.Property(e => e.ManufacturingMultiple).IsRequired();
            entity.Property(e => e.Remark).HasMaxLength(500);

            entity.Property(e => e.ColdRollDraw);
            entity.Property(e => e.OilPipeCut);
            entity.Property(e => e.Degrease);
            entity.Property(e => e.Solution);
            entity.Property(e => e.Straighten);
            entity.Property(e => e.Cut);
            entity.Property(e => e.ThicknessMeasure);
            entity.Property(e => e.Pickle);
            entity.Property(e => e.OuterPolish);
            entity.Property(e => e.InnerGrinding);
            entity.Property(e => e.OuterSpotGrinding);
            entity.Property(e => e.Inspection);
            entity.Property(e => e.WeldingHead);
            entity.Property(e => e.Lubrication);
            entity.Property(e => e.Warehouse);

            entity.HasOne<RoundBarPiercingPlan>()
                .WithMany()
                .HasForeignKey(e => e.RoundBarPiercingPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.RoundBarPiercingPlanId).HasDatabaseName("IX_PiercingPlanProcessGroup_PlanId");
            entity.HasIndex(e => new { e.RoundBarPiercingPlanId, e.SequenceNumber })
                .IsUnique()
                .HasDatabaseName("UK_PiercingPlanProcessGroup_Seq");
        });
    }

    private static void ConfigureInProcessReworkPlan(ModelBuilder builder)
    {
        builder.Entity<InProcessReworkPlan>(entity =>
        {
            entity.ToTable("InProcessReworkPlan");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkOrderId).IsRequired();
            entity.Property(e => e.PlanDate).IsRequired().HasColumnType("date");
            entity.Property(e => e.ProductionBatchId).IsRequired();
            entity.Property(e => e.BatchNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.BatchTagNo).HasMaxLength(50);
            entity.Property(e => e.MaterialName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LengthStatus).IsRequired().HasMaxLength(20);
            entity.Property(e => e.InputMultiple).IsRequired().HasDefaultValue(1);
            entity.Property(e => e.UsedQuantity);
            entity.Property(e => e.UsedWeight).IsRequired().HasColumnType("decimal(18,3)");
            entity.Property(e => e.RequiredDate).HasColumnType("date");
            entity.Property(e => e.PlanStatus).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(InventoryPlanStatus.Planned);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.ReworkType).IsRequired().HasMaxLength(20).HasConversion<string>();
            entity.Property(e => e.StandardCycle).IsRequired().HasDefaultValue(0);
            entity.HasIndex(e => e.WorkOrderId).HasDatabaseName("IX_InProcessReworkPlan_WorkOrderId");
            entity.HasIndex(e => e.ProductionBatchId).HasDatabaseName("IX_InProcessReworkPlan_ProductionBatchId");
            entity.HasIndex(e => e.PlanStatus).HasDatabaseName("IX_InProcessReworkPlan_PlanStatus");
        });
    }

    private static void ConfigureInProcessReworkPlanProcessGroup(ModelBuilder builder)
    {
        builder.Entity<InProcessReworkPlanProcessGroup>(entity =>
        {
            entity.ToTable("InProcessReworkPlanProcessGroup");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.InProcessReworkPlanId).IsRequired();
            entity.Property(e => e.SequenceNumber).IsRequired();
            entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ManufacturingSpec).HasMaxLength(100);
            entity.Property(e => e.OuterDiameterTolerance).HasMaxLength(50);
            entity.Property(e => e.WallThicknessTolerance).HasMaxLength(50);
            entity.Property(e => e.ManufacturingLength).HasMaxLength(100);
            entity.Property(e => e.CuttingTreatment).HasMaxLength(200);
            entity.Property(e => e.ManufacturingMultiple).IsRequired();
            entity.Property(e => e.Remark).HasMaxLength(500);

            entity.Property(e => e.ColdRollDraw);
            entity.Property(e => e.OilPipeCut);
            entity.Property(e => e.Degrease);
            entity.Property(e => e.Solution);
            entity.Property(e => e.Straighten);
            entity.Property(e => e.Cut);
            entity.Property(e => e.ThicknessMeasure);
            entity.Property(e => e.Pickle);
            entity.Property(e => e.OuterPolish);
            entity.Property(e => e.InnerGrinding);
            entity.Property(e => e.OuterSpotGrinding);
            entity.Property(e => e.Inspection);
            entity.Property(e => e.WeldingHead);
            entity.Property(e => e.Lubrication);
            entity.Property(e => e.Warehouse);

            entity.HasOne<InProcessReworkPlan>()
                .WithMany()
                .HasForeignKey(e => e.InProcessReworkPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.InProcessReworkPlanId).HasDatabaseName("IX_InProcessReworkPlanPG_PlanId");
            entity.HasIndex(e => new { e.InProcessReworkPlanId, e.SequenceNumber })
                .IsUnique()
                .HasDatabaseName("UK_InProcessReworkPlanPG_Seq");
        });
    }

    // ================================================================
    //                      生产记录上下文配置
    // ================================================================

    private static void ConfigureProductionRecord(ModelBuilder builder)
    {
        builder.Entity<ProductionRecord>(entity =>
        {
            entity.ToTable("ProductionRecord");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductionBatchId).IsRequired();
            entity.Property(e => e.ProcessGroupId).IsRequired();
            entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ManufacturingSpec).HasMaxLength(100);
            entity.Property(e => e.SectionName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SequenceNumber).IsRequired();
            entity.Property(e => e.ExecDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.EquipmentName).HasMaxLength(100);
            entity.Property(e => e.Operator).HasMaxLength(50);
            entity.Property(e => e.Shift).HasMaxLength(10);
            entity.Property(e => e.Quantity);
            entity.Property(e => e.Weight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.ProductStatus).HasMaxLength(20);
            entity.Property(e => e.CuttingMultiple).HasColumnType("decimal(5,2)");
            entity.Property(e => e.FinishedCutLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PostCutQuantity);
            entity.Property(e => e.TagNo).HasMaxLength(50);
            entity.Property(e => e.PlantGrade).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.DataSource).HasMaxLength(10).HasDefaultValue("MANUAL");

            entity.HasOne(e => e.ProductionBatch)
                .WithMany()
                .HasForeignKey(e => e.ProductionBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ProcessGroup)
                .WithMany()
                .HasForeignKey(e => e.ProcessGroupId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => e.ProductionBatchId).HasDatabaseName("IX_ProductionRecord_BatchId");
            entity.HasIndex(e => e.ProcessGroupId).HasDatabaseName("IX_ProductionRecord_ProcessGroupId");
            entity.HasIndex(e => new { e.ProductionBatchId, e.ProcessGroupId, e.SectionName })
                .IsUnique()
                .HasDatabaseName("UK_ProductionRecord_Section");
        });
    }

    private static void ConfigureSectionOutsource(ModelBuilder builder)
    {
        builder.Entity<SectionOutsource>(entity =>
        {
            entity.ToTable("SectionOutsource");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductionBatchId).IsRequired();
            entity.Property(e => e.ProcessGroupId).IsRequired();
            entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ManufacturingSpec).HasMaxLength(100);
            entity.Property(e => e.SectionName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SequenceNumber).IsRequired();
            entity.Property(e => e.OutsourceVendor).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SendOutDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.SendQuantity);
            entity.Property(e => e.SendWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasConversion<string>().HasDefaultValue(SectionOutsourceStatus.PendingRecovery);
            entity.Property(e => e.TagNo).HasMaxLength(50);
            entity.Property(e => e.PlantGrade).HasMaxLength(50);
            entity.Property(e => e.OutsourceSpec).HasMaxLength(100);
            entity.Property(e => e.ExpectedReturnDate).HasColumnType("datetime2");
            entity.Property(e => e.IsUrgent).HasDefaultValue(false);
            entity.Property(e => e.ProductStatus).HasMaxLength(20);
            entity.Property(e => e.Remark).HasMaxLength(500);

            entity.HasOne(e => e.ProductionBatch)
                .WithMany()
                .HasForeignKey(e => e.ProductionBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ProcessGroup)
                .WithMany()
                .HasForeignKey(e => e.ProcessGroupId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(e => e.OutsourceRecoveries)
                .WithOne(r => r.SectionOutsource)
                .HasForeignKey(r => r.SectionOutsourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ProductionBatchId).HasDatabaseName("IX_SectionOutsource_BatchId");
            entity.HasIndex(e => new { e.ProductionBatchId, e.ProcessGroupId, e.SectionName })
                .IsUnique()
                .HasDatabaseName("UK_SectionOutsource_Section");
        });
    }

    private static void ConfigureOutsourceRecovery(ModelBuilder builder)
    {
        builder.Entity<OutsourceRecovery>(entity =>
        {
            entity.ToTable("OutsourceRecovery");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SectionOutsourceId).IsRequired();
            entity.Property(e => e.RecoveryDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.RecoveryQuantity);
            entity.Property(e => e.RecoveryWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.UnprocessedQuantity);
            entity.Property(e => e.UnprocessedWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Remark).HasMaxLength(500);

            entity.HasIndex(e => e.SectionOutsourceId).HasDatabaseName("IX_OutsourceRecovery_OutsourceId");
        });
    }

    private static void ConfigurePicklingInRecord(ModelBuilder builder)
    {
        builder.Entity<PicklingInRecord>(entity =>
        {
            entity.ToTable("PicklingInRecord");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductionBatchId).IsRequired();
            entity.Property(e => e.ProcessGroupId).IsRequired();
            entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ManufacturingSpec).HasMaxLength(100);
            entity.Property(e => e.SectionName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SequenceNumber).IsRequired();
            entity.Property(e => e.InDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(15).HasDefaultValue(PicklingStatus.Soaking);
            entity.Property(e => e.EquipmentName).HasMaxLength(100);
            entity.Property(e => e.Operator).HasMaxLength(50);
            entity.Property(e => e.Shift).HasMaxLength(10);
            entity.Property(e => e.Quantity);
            entity.Property(e => e.Weight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.ProductStatus).HasMaxLength(20);
            entity.Property(e => e.TagNo).HasMaxLength(50);
            entity.Property(e => e.PlantGrade).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.DataSource).HasMaxLength(10).HasDefaultValue("MANUAL");

            entity.HasOne(e => e.ProductionBatch)
                .WithMany()
                .HasForeignKey(e => e.ProductionBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ProcessGroup)
                .WithMany()
                .HasForeignKey(e => e.ProcessGroupId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => e.ProductionBatchId).HasDatabaseName("IX_PicklingInRecord_BatchId");
            entity.HasIndex(e => new { e.ProductionBatchId, e.ProcessGroupId, e.SectionName })
                .IsUnique()
                .HasDatabaseName("UK_PicklingInRecord_Section");
        });
    }

    private static void ConfigurePicklingOutRecord(ModelBuilder builder)
    {
        builder.Entity<PicklingOutRecord>(entity =>
        {
            entity.ToTable("PicklingOutRecord");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PicklingInRecordId).IsRequired();
            entity.Property(e => e.CompleteDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.DataSource).HasMaxLength(10).HasDefaultValue("MANUAL");

            // 冗余字段（计件工资结算用）
            entity.Property(e => e.ProductionBatchId).IsRequired();
            entity.Property(e => e.ManufacturingSpec).HasMaxLength(100);
            entity.Property(e => e.SectionName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EquipmentName).HasMaxLength(100);
            entity.Property(e => e.Operator).HasMaxLength(50);
            entity.Property(e => e.Shift).HasMaxLength(10);
            entity.Property(e => e.Quantity);
            entity.Property(e => e.Weight).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ProductStatus).HasMaxLength(20);
            entity.Property(e => e.PlantGrade).HasMaxLength(50);

            entity.HasOne(e => e.PicklingInRecord)
                .WithMany(e => e.PicklingOutRecords)
                .HasForeignKey(e => e.PicklingInRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.PicklingInRecordId).HasDatabaseName("IX_PicklingOutRecord_InRecordId");
        });
    }

    private static void ConfigureMaterialReceiveCheck(ModelBuilder builder)
    {
        builder.Entity<MaterialReceiveCheck>(entity =>
        {
            entity.ToTable("MaterialReceiveCheck");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductionBatchId).IsRequired();
            entity.Property(e => e.ReceiveDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Shift).HasMaxLength(10);
            entity.Property(e => e.Checker).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.DataSource).HasMaxLength(10);

            entity.Property(e => e.BatchNo).HasMaxLength(50);
            entity.Property(e => e.ManufacturingItem).HasMaxLength(50);
            entity.Property(e => e.TagNo).HasMaxLength(50);
            entity.Property(e => e.WorkOrderNo).HasMaxLength(50);
            entity.Property(e => e.SalesOrderNo).HasMaxLength(50);
            entity.Property(e => e.SourceUnit).HasMaxLength(200);
            entity.Property(e => e.FurnaceNo).HasMaxLength(50);
            entity.Property(e => e.PlantGrade).HasMaxLength(50);
            entity.Property(e => e.Specification).HasMaxLength(100);
            entity.Property(e => e.ProductionType).HasMaxLength(50);
            entity.Property(e => e.ProductionWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.IsForceCompleted);
            entity.Property(e => e.Salesman).HasMaxLength(50);
            entity.Property(e => e.DeliveryState).HasMaxLength(50);

            entity.HasOne(e => e.ProductionBatch)
                .WithMany()
                .HasForeignKey(e => e.ProductionBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ProductionBatchId)
                .IsUnique()
                .HasDatabaseName("UK_MaterialReceiveCheck_BatchId");

            entity.HasIndex(e => e.ReceiveDate)
                .HasDatabaseName("IX_MaterialReceiveCheck_ReceiveDate");

            entity.HasIndex(e => e.BatchNo)
                .HasDatabaseName("IX_MaterialReceiveCheck_BatchNo");

            entity.HasIndex(e => e.PlantGrade)
                .HasDatabaseName("IX_MaterialReceiveCheck_PlantGrade");

            entity.HasIndex(e => e.Specification)
                .HasDatabaseName("IX_MaterialReceiveCheck_Specification");
        });
    }

    private static void ConfigureBatchOperationLog(ModelBuilder builder)
    {
        builder.Entity<BatchOperationLog>(entity =>
        {
            entity.ToTable("BatchOperationLog");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.OperationType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Detail).HasMaxLength(2000);

            entity.HasOne(e => e.ProductionBatch)
                .WithMany()
                .HasForeignKey(e => e.ProductionBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ProductionBatchId)
                .HasDatabaseName("IX_BatchOperationLog_BatchId");
        });
    }

    // ================================================================
    //                      质量上下文配置
    // ================================================================

    private static void ConfigureChemicalComposition(ModelBuilder builder)
    {
        builder.Entity<ChemicalComposition>(entity =>
        {
            entity.ToTable("ChemicalComposition");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Carbon).HasMaxLength(100);
            entity.Property(e => e.Silicon).HasMaxLength(100);
            entity.Property(e => e.Manganese).HasMaxLength(100);
            entity.Property(e => e.Phosphorus).HasMaxLength(100);
            entity.Property(e => e.Sulfur).HasMaxLength(100);
            entity.Property(e => e.Nickel).HasMaxLength(100);
            entity.Property(e => e.Chromium).HasMaxLength(100);
            entity.Property(e => e.Molybdenum).HasMaxLength(100);
            entity.Property(e => e.Copper).HasMaxLength(100);
            entity.Property(e => e.Nitrogen).HasMaxLength(100);
            entity.Property(e => e.Niobium).HasMaxLength(100);
            entity.Property(e => e.Titanium).HasMaxLength(100);
            entity.Property(e => e.Iron).HasMaxLength(100);
            entity.Property(e => e.Aluminum).HasMaxLength(100);
            entity.Property(e => e.Tungsten).HasMaxLength(100);
            entity.Property(e => e.PREN).HasMaxLength(100);

            entity.HasIndex(e => e.PlantGrade)
                .IsUnique()
                .HasDatabaseName("UK_ChemicalComposition_PlantGrade");
        });
    }

    private static void ConfigureFurnaceRegistration(ModelBuilder builder)
    {
        builder.Entity<FurnaceRegistration>(entity =>
        {
            entity.ToTable("FurnaceRegistration");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.IncomingDate).IsRequired().HasColumnType("date");
            entity.Property(e => e.RawMaterialUnit).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RawMaterialType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RegisteredGrade).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RelatedPlantGrade).HasMaxLength(100);
            entity.Property(e => e.FurnaceNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Specification).HasMaxLength(100);
            entity.Property(e => e.Weight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Carbon).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Silicon).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Manganese).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Phosphorus).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Sulfur).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Nickel).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Chromium).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Molybdenum).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Copper).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Nitrogen).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Niobium).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Titanium).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Iron).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Aluminum).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Tungsten).HasColumnType("decimal(18,3)");
            entity.Property(e => e.PREN).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Remark).HasMaxLength(500);

            entity.HasIndex(e => e.FurnaceNumber)
                .HasDatabaseName("IX_FurnaceRegistration_FurnaceNumber");
        });
    }

    private static void ConfigureChemicalValidationRule(ModelBuilder builder)
    {
        builder.Entity<ChemicalValidationRule>(entity =>
        {
            entity.ToTable("ChemicalValidationRule");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CMin).HasMaxLength(50);
            entity.Property(e => e.CMax).HasMaxLength(50);
            entity.Property(e => e.SiMin).HasMaxLength(50);
            entity.Property(e => e.SiMax).HasMaxLength(50);
            entity.Property(e => e.MnMin).HasMaxLength(50);
            entity.Property(e => e.MnMax).HasMaxLength(50);
            entity.Property(e => e.PMin).HasMaxLength(50);
            entity.Property(e => e.PMax).HasMaxLength(50);
            entity.Property(e => e.SMin).HasMaxLength(50);
            entity.Property(e => e.SMax).HasMaxLength(50);
            entity.Property(e => e.NiMin).HasMaxLength(50);
            entity.Property(e => e.NiMax).HasMaxLength(50);
            entity.Property(e => e.CrMin).HasMaxLength(50);
            entity.Property(e => e.CrMax).HasMaxLength(50);
            entity.Property(e => e.MoMin).HasMaxLength(50);
            entity.Property(e => e.MoMax).HasMaxLength(50);
            entity.Property(e => e.CuMin).HasMaxLength(50);
            entity.Property(e => e.CuMax).HasMaxLength(50);
            entity.Property(e => e.NMin).HasMaxLength(50);
            entity.Property(e => e.NMax).HasMaxLength(50);
            entity.Property(e => e.NbMin).HasMaxLength(50);
            entity.Property(e => e.NbMax).HasMaxLength(50);
            entity.Property(e => e.TiMin).HasMaxLength(50);
            entity.Property(e => e.TiMax).HasMaxLength(50);
            entity.Property(e => e.FeMin).HasMaxLength(50);
            entity.Property(e => e.FeMax).HasMaxLength(50);
            entity.Property(e => e.AlMin).HasMaxLength(50);
            entity.Property(e => e.AlMax).HasMaxLength(50);
            entity.Property(e => e.WMin).HasMaxLength(50);
            entity.Property(e => e.WMax).HasMaxLength(50);
            entity.Property(e => e.PRENMin).HasMaxLength(50);

            entity.HasIndex(e => e.PlantGrade)
                .IsUnique()
                .HasDatabaseName("UK_ChemicalValidationRule_PlantGrade");
        });
    }

    private static void ConfigureProcessInspection(ModelBuilder builder)
    {
        builder.Entity<ProcessInspection>(entity =>
        {
            entity.ToTable("ProcessInspection");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductionBatchId).IsRequired();
            entity.Property(e => e.ProcessGroupId).IsRequired();
            entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ManufacturingSpec).HasMaxLength(100);
            entity.Property(e => e.SectionName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SequenceNumber).IsRequired();
            entity.Property(e => e.InspectionDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.EquipmentName).HasMaxLength(100);
            entity.Property(e => e.Inspector).HasMaxLength(50);
            entity.Property(e => e.Shift).HasMaxLength(10);
            entity.Property(e => e.Quantity);
            entity.Property(e => e.Weight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.InspectionItem).HasMaxLength(100);
            entity.Property(e => e.QualifiedQuantity);
            entity.Property(e => e.QualifiedWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.DefectReworkQuantity);
            entity.Property(e => e.DefectWarehouseQuantity);
            entity.Property(e => e.DefectScrapQuantity);
            entity.Property(e => e.DefectDescription).HasMaxLength(500);
            entity.Property(e => e.SourceUnit).HasMaxLength(200);
            entity.Property(e => e.TagNo).HasMaxLength(50);
            entity.Property(e => e.BatchNo).HasMaxLength(50);
            entity.Property(e => e.PlantGrade).HasMaxLength(50);
            entity.Property(e => e.ProductStatus).HasMaxLength(20);
            entity.Property(e => e.Remark).HasMaxLength(500);

            entity.HasOne(e => e.ProductionBatch)
                .WithMany()
                .HasForeignKey(e => e.ProductionBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ProcessGroup)
                .WithMany()
                .HasForeignKey(e => e.ProcessGroupId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => e.ProductionBatchId).HasDatabaseName("IX_ProcessInspection_BatchId");
            entity.HasIndex(e => e.ProcessGroupId).HasDatabaseName("IX_ProcessInspection_ProcessGroupId");
            entity.HasIndex(e => e.InspectionDate).HasDatabaseName("IX_ProcessInspection_InspectionDate");
            entity.HasIndex(e => e.BatchNo).HasDatabaseName("IX_ProcessInspection_BatchNo");
        });
    }

    private static void ConfigureFinalInspection(ModelBuilder builder)
    {
        builder.Entity<FinalInspection>(entity =>
        {
            entity.ToTable("FinalInspection");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.InspectionItem).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.InspectionDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.BatchNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProductionBatchId).IsRequired();

            // 批次冗余字段
            entity.Property(e => e.MaterialName).HasMaxLength(50);
            entity.Property(e => e.TagNo).HasMaxLength(50);
            entity.Property(e => e.WorkOrderNo).HasMaxLength(50);
            entity.Property(e => e.SalesOrderNo).HasMaxLength(50);
            entity.Property(e => e.SourceUnit).HasMaxLength(200);
            entity.Property(e => e.FurnaceNo).HasMaxLength(50);
            entity.Property(e => e.PlantGrade).HasMaxLength(50);
            entity.Property(e => e.Specification).HasMaxLength(100);
            entity.Property(e => e.FixedLength).HasMaxLength(50);
            entity.Property(e => e.ProductionType).HasMaxLength(50);

            // 执行信息
            entity.Property(e => e.EquipmentName).HasMaxLength(100);
            entity.Property(e => e.Shift).HasMaxLength(10);
            entity.Property(e => e.Operator).HasMaxLength(50);

            // 数量/重量
            entity.Property(e => e.Quantity);
            entity.Property(e => e.Weight).HasColumnType("decimal(18,3)");

            // 检验结果
            entity.Property(e => e.QualifiedQuantity);
            entity.Property(e => e.QualifiedWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.DefectReworkQuantity);
            entity.Property(e => e.DefectWarehouseQuantity);
            entity.Property(e => e.DefectScrapQuantity);
            entity.Property(e => e.DefectDescription).HasMaxLength(500);

            // 尺寸检验专用
            entity.Property(e => e.OuterDiameterRange).HasMaxLength(100);
            entity.Property(e => e.WallThicknessRange).HasMaxLength(100);
            entity.Property(e => e.LengthAllowanceRange).HasMaxLength(100);

            // 水压/水下气压专用
            entity.Property(e => e.Pressure).HasColumnType("decimal(18,3)");
            entity.Property(e => e.HoldTime);

            // 其他
            entity.Property(e => e.Remark).HasMaxLength(500);

            // 关系
            entity.HasOne(e => e.ProductionBatch)
                .WithMany()
                .HasForeignKey(e => e.ProductionBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            // 索引
            entity.HasIndex(e => e.BatchNo).HasDatabaseName("IX_FinalInspection_BatchNo");
            entity.HasIndex(e => e.InspectionDate).HasDatabaseName("IX_FinalInspection_InspectionDate");
            entity.HasIndex(e => e.InspectionItem).HasDatabaseName("IX_FinalInspection_InspectionItem");
        });
    }

    private static void ConfigureChemicalAnalysis(ModelBuilder builder)
    {
        builder.Entity<ChemicalAnalysis>(entity =>
        {
            entity.ToTable("ChemicalAnalysis");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.AnalysisDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Analyst).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FurnaceNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Grade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AnalysisCount);
            entity.Property(e => e.AnalysisStandard).HasMaxLength(100);

            // 化学元素含量
            entity.Property(e => e.C).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Si).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Mn).HasColumnType("decimal(18,6)");
            entity.Property(e => e.P).HasColumnType("decimal(18,6)");
            entity.Property(e => e.S).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Ni).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Cr).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Mo).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Cu).HasColumnType("decimal(18,6)");
            entity.Property(e => e.N).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Nb).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Ti).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Fe).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Al).HasColumnType("decimal(18,6)");
            entity.Property(e => e.W).HasColumnType("decimal(18,6)");

            // 索引
            entity.HasIndex(e => e.FurnaceNo).HasDatabaseName("IX_ChemicalAnalysis_FurnaceNo");
            entity.HasIndex(e => e.Grade).HasDatabaseName("IX_ChemicalAnalysis_Grade");
            entity.HasIndex(e => e.AnalysisDate).HasDatabaseName("IX_ChemicalAnalysis_AnalysisDate");
        });
    }

    private static void ConfigureHardnessTest(ModelBuilder builder)
    {
        builder.Entity<HardnessTest>(entity =>
        {
            entity.ToTable("HardnessTest");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.InspectionDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Inspector).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FurnaceNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Grade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SampleNo);
            entity.Property(e => e.SampleSize).HasMaxLength(50);
            entity.Property(e => e.InspectionStandard).HasMaxLength(100);
            entity.Property(e => e.HardnessMode).HasMaxLength(50);
            entity.Property(e => e.HardnessValue).HasMaxLength(200);
            entity.Property(e => e.Judgment).HasMaxLength(50);

            // 索引
            entity.HasIndex(e => e.FurnaceNo).HasDatabaseName("IX_HardnessTest_FurnaceNo");
            entity.HasIndex(e => e.Grade).HasDatabaseName("IX_HardnessTest_Grade");
            entity.HasIndex(e => e.InspectionDate).HasDatabaseName("IX_HardnessTest_InspectionDate");
        });
    }

    private static void ConfigureGrainSizeTest(ModelBuilder builder)
    {
        builder.Entity<GrainSizeTest>(entity =>
        {
            entity.ToTable("GrainSizeTest");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.InspectionDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Inspector).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FurnaceNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Grade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SampleNo);
            entity.Property(e => e.SampleSize).HasMaxLength(50);
            entity.Property(e => e.InspectionStandard).HasMaxLength(100);
            entity.Property(e => e.GrainSizeGrade).HasMaxLength(50);
            entity.Property(e => e.GrainSizeMethod).HasMaxLength(50);
            entity.Property(e => e.Magnification).HasMaxLength(50);
            entity.Property(e => e.Judgment).HasMaxLength(50);

            // 索引
            entity.HasIndex(e => e.FurnaceNo).HasDatabaseName("IX_GrainSizeTest_FurnaceNo");
            entity.HasIndex(e => e.Grade).HasDatabaseName("IX_GrainSizeTest_Grade");
            entity.HasIndex(e => e.InspectionDate).HasDatabaseName("IX_GrainSizeTest_InspectionDate");
        });
    }

    private static void ConfigurePittingCorrosionTest(ModelBuilder builder)
    {
        builder.Entity<PittingCorrosionTest>(entity =>
        {
            entity.ToTable("PittingCorrosionTest");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.InspectionDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Inspector).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FurnaceNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Grade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SampleNo);
            entity.Property(e => e.SampleSize).HasMaxLength(50);
            entity.Property(e => e.InspectionStandard).HasMaxLength(100);
            entity.Property(e => e.PolishingGrade).HasMaxLength(100);
            entity.Property(e => e.RawWeight).HasColumnType("decimal(18,6)");
            entity.Property(e => e.CorrosionSolution).HasMaxLength(100);
            entity.Property(e => e.CorrosionTemperature).HasMaxLength(50);
            entity.Property(e => e.CorrosionTime).HasMaxLength(50);
            entity.Property(e => e.FinalWeight).HasColumnType("decimal(18,6)");
            entity.Property(e => e.CorrosionRate).HasColumnType("decimal(18,6)");
            entity.Property(e => e.MaxPitDepth).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Judgment).HasMaxLength(50);

            entity.HasIndex(e => e.FurnaceNo).HasDatabaseName("IX_PittingCorrosionTest_FurnaceNo");
            entity.HasIndex(e => e.Grade).HasDatabaseName("IX_PittingCorrosionTest_Grade");
            entity.HasIndex(e => e.InspectionDate).HasDatabaseName("IX_PittingCorrosionTest_InspectionDate");
        });
    }

    private static void ConfigureIntergranularCorrosionTest(ModelBuilder builder)
    {
        builder.Entity<IntergranularCorrosionTest>(entity =>
        {
            entity.ToTable("IntergranularCorrosionTest");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.InspectionDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Inspector).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FurnaceNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Grade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SampleNo);
            entity.Property(e => e.SampleSize).HasMaxLength(50);
            entity.Property(e => e.InspectionStandard).HasMaxLength(100);
            entity.Property(e => e.SensitizationTemperature).HasMaxLength(50);
            entity.Property(e => e.SensitizationDuration).HasMaxLength(50);
            entity.Property(e => e.CorrosionSolution).HasMaxLength(100);
            entity.Property(e => e.CorrosionTime).HasMaxLength(50);
            entity.Property(e => e.BendDegree).HasMaxLength(50);
            entity.Property(e => e.Magnification).HasMaxLength(50);
            entity.Property(e => e.ObservationResult).HasMaxLength(200);
            entity.Property(e => e.Judgment).HasMaxLength(50);

            entity.HasIndex(e => e.FurnaceNo).HasDatabaseName("IX_IntergranularCorrosionTest_FurnaceNo");
            entity.HasIndex(e => e.Grade).HasDatabaseName("IX_IntergranularCorrosionTest_Grade");
            entity.HasIndex(e => e.InspectionDate).HasDatabaseName("IX_IntergranularCorrosionTest_InspectionDate");
        });
    }

    private static void ConfigureTensileTest(ModelBuilder builder)
    {
        builder.Entity<TensileTest>(entity =>
        {
            entity.ToTable("TensileTest");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.InspectionDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Inspector).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FurnaceNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Grade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SampleNo);
            entity.Property(e => e.SampleSize).HasMaxLength(50);
            entity.Property(e => e.InspectionStandard).HasMaxLength(100);
            entity.Property(e => e.OriginalGaugeLength).HasColumnType("decimal(18,6)");
            entity.Property(e => e.FinalGaugeLength).HasColumnType("decimal(18,6)");
            entity.Property(e => e.TensileStrength).HasColumnType("decimal(18,6)");
            entity.Property(e => e.YieldStrengthRp02).HasColumnType("decimal(18,6)");
            entity.Property(e => e.YieldStrengthRp1).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Elongation).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Judgment).HasMaxLength(50);

            entity.HasIndex(e => e.FurnaceNo).HasDatabaseName("IX_TensileTest_FurnaceNo");
            entity.HasIndex(e => e.Grade).HasDatabaseName("IX_TensileTest_Grade");
            entity.HasIndex(e => e.InspectionDate).HasDatabaseName("IX_TensileTest_InspectionDate");
        });
    }

    private static void ConfigureMetallographicTest(ModelBuilder builder)
    {
        builder.Entity<MetallographicTest>(entity =>
        {
            entity.ToTable("MetallographicTest");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.InspectionDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Inspector).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FurnaceNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Grade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SampleNo);
            entity.Property(e => e.SampleSize).HasMaxLength(50);
            entity.Property(e => e.InspectionStandard).HasMaxLength(100);
            entity.Property(e => e.EtchingMethod).HasMaxLength(100);
            entity.Property(e => e.ElectrolyticVoltage).HasMaxLength(50);
            entity.Property(e => e.ElectrolyticTime).HasMaxLength(50);
            entity.Property(e => e.Magnification).HasMaxLength(50);
            entity.Property(e => e.FerriteContent).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Judgment).HasMaxLength(50);

            entity.HasIndex(e => e.FurnaceNo).HasDatabaseName("IX_MetallographicTest_FurnaceNo");
            entity.HasIndex(e => e.Grade).HasDatabaseName("IX_MetallographicTest_Grade");
            entity.HasIndex(e => e.InspectionDate).HasDatabaseName("IX_MetallographicTest_InspectionDate");
        });
    }

    private static void ConfigureFlatteningTest(ModelBuilder builder)
    {
        builder.Entity<FlatteningTest>(entity =>
        {
            entity.ToTable("FlatteningTest");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.InspectionDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Inspector).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FurnaceNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Grade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SampleNo);
            entity.Property(e => e.SampleSize).HasMaxLength(50);
            entity.Property(e => e.InspectionStandard).HasMaxLength(100);
            entity.Property(e => e.FlatteningGap).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Observation).HasMaxLength(200);
            entity.Property(e => e.Judgment).HasMaxLength(50);

            entity.HasIndex(e => e.FurnaceNo).HasDatabaseName("IX_FlatteningTest_FurnaceNo");
            entity.HasIndex(e => e.Grade).HasDatabaseName("IX_FlatteningTest_Grade");
            entity.HasIndex(e => e.InspectionDate).HasDatabaseName("IX_FlatteningTest_InspectionDate");
        });
    }

    private static void ConfigureFlaringTest(ModelBuilder builder)
    {
        builder.Entity<FlaringTest>(entity =>
        {
            entity.ToTable("FlaringTest");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.InspectionDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Inspector).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FurnaceNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Grade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SampleNo);
            entity.Property(e => e.SampleSize).HasMaxLength(50);
            entity.Property(e => e.InspectionStandard).HasMaxLength(100);
            entity.Property(e => e.MandrelTaper).HasMaxLength(50);
            entity.Property(e => e.FlaredDiameter).HasColumnType("decimal(18,6)");
            entity.Property(e => e.FlaringRate).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Observation).HasMaxLength(200);
            entity.Property(e => e.Judgment).HasMaxLength(50);

            entity.HasIndex(e => e.FurnaceNo).HasDatabaseName("IX_FlaringTest_FurnaceNo");
            entity.HasIndex(e => e.Grade).HasDatabaseName("IX_FlaringTest_Grade");
            entity.HasIndex(e => e.InspectionDate).HasDatabaseName("IX_FlaringTest_InspectionDate");
        });
    }

    private static void ConfigureNcr(ModelBuilder builder)
    {
        builder.Entity<Ncr>(entity =>
        {
            entity.ToTable("Ncr");
            entity.HasKey(e => e.Id);

            // G1: 问题反馈
            entity.Property(e => e.ReportDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.ReportDepartment).HasMaxLength(50);
            entity.Property(e => e.Reporter).HasMaxLength(50);
            entity.Property(e => e.PipeCategory).IsRequired().HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.BatchNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.WorkOrderNo).HasMaxLength(100);
            entity.Property(e => e.PlantGrade).HasMaxLength(50);
            entity.Property(e => e.Specification).HasMaxLength(100);
            entity.Property(e => e.DefectiveQuantity);
            entity.Property(e => e.ProblemDescription).HasMaxLength(500);
            entity.Property(e => e.SourceInspectionItem).HasMaxLength(100);

            // G2: 不合格品处置
            entity.Property(e => e.DisposalMethod).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.DisposalRemark).HasMaxLength(500);
            entity.Property(e => e.DisposalIsCompleted);
            entity.Property(e => e.DisposalCompleteDate).HasColumnType("datetime2");

            // G3: 原因分析
            entity.Property(e => e.RootCauseAnalysis).HasMaxLength(1000);
            entity.Property(e => e.Severity).HasConversion<string>().HasMaxLength(10);
            entity.Property(e => e.AnalysisConfirmer).HasMaxLength(50);
            entity.Property(e => e.AnalysisConfirmDate).HasColumnType("datetime2");

            // G4: 责任人及处理
            entity.Property(e => e.ResponsibilityCategory).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.ResponsibleDept).HasMaxLength(100);
            entity.Property(e => e.OperationDate).HasColumnType("datetime2");
            entity.Property(e => e.ResponsiblePerson).HasMaxLength(50);
            entity.Property(e => e.PersonDisposition).HasMaxLength(500);
            entity.Property(e => e.PersonIsCompleted);
            entity.Property(e => e.PersonCompleteDate).HasColumnType("datetime2");

            // G5: 纠正预防措施及结果验证
            entity.Property(e => e.CorrectiveAction).HasMaxLength(1000);
            entity.Property(e => e.ActionPlanner).HasMaxLength(50);
            entity.Property(e => e.ActionPlanDate).HasColumnType("datetime2");
            entity.Property(e => e.ActionVerifier).HasMaxLength(50);
            entity.Property(e => e.ActionVerifyDate).HasColumnType("datetime2");
            entity.Property(e => e.ActionResult).HasMaxLength(200);
            entity.Property(e => e.VerifyResult).HasConversion<string>().HasMaxLength(20);

            // 状态
            entity.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(15);

            // 索引
            entity.HasIndex(e => e.BatchNo).HasDatabaseName("IX_Ncr_BatchNo");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_Ncr_Status");
            entity.HasIndex(e => e.ReportDate).HasDatabaseName("IX_Ncr_ReportDate");
            entity.HasIndex(e => e.Severity).HasDatabaseName("IX_Ncr_Severity");
            entity.HasIndex(e => e.DisposalMethod).HasDatabaseName("IX_Ncr_DisposalMethod");
        });
    }

    private static void ConfigureQualityProcessTracking(ModelBuilder builder)
    {
        builder.Entity<QualityProcessTracking>(entity =>
        {
            entity.ToTable("QualityProcessTracking");
            entity.HasKey(e => e.Id);

            // 关联标识
            entity.Property(e => e.MaterialReceiveCheckId).IsRequired();
            entity.Property(e => e.ProductionBatchId).IsRequired();

            // G1: 批次信息
            entity.Property(e => e.BatchNo).HasMaxLength(50);
            entity.Property(e => e.ManufacturingItem).HasMaxLength(50);
            entity.Property(e => e.TagNo).HasMaxLength(100);
            entity.Property(e => e.WorkOrderNo).HasMaxLength(50);
            entity.Property(e => e.SalesOrderNo).HasMaxLength(50);
            entity.Property(e => e.SourceUnit).HasMaxLength(100);
            entity.Property(e => e.FurnaceNo).HasMaxLength(50);
            entity.Property(e => e.PlantGrade).HasMaxLength(100);
            entity.Property(e => e.Specification).HasMaxLength(100);
            entity.Property(e => e.ProductionType).HasMaxLength(20);
            entity.Property(e => e.LengthStatus).HasMaxLength(20);
            entity.Property(e => e.ProductionWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.ReceiveDate).IsRequired().HasColumnType("date");
            entity.Property(e => e.Shift).HasMaxLength(20);
            entity.Property(e => e.Checker).HasMaxLength(50);
            entity.Property(e => e.Salesman).HasMaxLength(50);
            entity.Property(e => e.DeliveryState).HasMaxLength(50);
            entity.Property(e => e.IsForceCompleted).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.PbBatchNo).HasMaxLength(50);

            // G2: 检验日期
            entity.Property(e => e.PmiDate).HasColumnType("date");
            entity.Property(e => e.VisualDate).HasColumnType("date");
            entity.Property(e => e.DimensionDate).HasColumnType("date");
            entity.Property(e => e.EndoscopyDate).HasColumnType("date");
            entity.Property(e => e.HydroDate).HasColumnType("date");
            entity.Property(e => e.UnderwaterPneumaticDate).HasColumnType("date");
            entity.Property(e => e.EddyCurrentDate).HasColumnType("date");
            entity.Property(e => e.UltrasonicDate).HasColumnType("date");
            entity.Property(e => e.PortColoringDate).HasColumnType("date");
            entity.Property(e => e.InspectionCount).IsRequired().HasDefaultValue(0);

            // G3: 检验汇总
            entity.Property(e => e.ProductionCutQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.TotalQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.QualifiedQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.DefectReworkQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.DefectWarehouseQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.DefectScrapQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.MaxInspectionDate).HasColumnType("date");

            // G4: 成品入库
            entity.Property(e => e.InboundQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.InboundWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.InboundDate).HasColumnType("date");

            // G5: 执行状态
            entity.Property(e => e.QualityStatus).IsRequired().HasMaxLength(20).HasDefaultValue("待检验");

            // 刷新追踪
            entity.Property(e => e.LastRefreshTime).HasColumnType("datetime2");

            // 索引
            entity.HasIndex(e => e.MaterialReceiveCheckId).IsUnique().HasDatabaseName("UK_QPT_MaterialReceiveCheckId");
            entity.HasIndex(e => e.ProductionBatchId).HasDatabaseName("IX_QPT_ProductionBatchId");
            entity.HasIndex(e => e.BatchNo).HasDatabaseName("IX_QPT_BatchNo");
            entity.HasIndex(e => e.SalesOrderNo).HasDatabaseName("IX_QPT_SalesOrderNo");
            entity.HasIndex(e => e.WorkOrderNo).HasDatabaseName("IX_QPT_WorkOrderNo");
            entity.HasIndex(e => e.QualityStatus).HasDatabaseName("IX_QPT_QualityStatus");
            entity.HasIndex(e => e.ReceiveDate).HasDatabaseName("IX_QPT_ReceiveDate");
        });
    }

    // ================================================================
    //                      设备上下文配置
    // ================================================================

    private static void ConfigureEquipment(ModelBuilder builder)
    {
        builder.Entity<Equipment>(entity =>
        {
            entity.ToTable("Equipment");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EquipmentCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EquipmentName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ModelNumber).HasMaxLength(100);
            entity.Property(e => e.TechnicalParams).HasMaxLength(500);
            entity.Property(e => e.Manufacturer).HasMaxLength(200);
            entity.Property(e => e.InstallationDate).HasColumnType("date");
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.Location).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RelatedSection).HasMaxLength(100);

            // 点检参数
            entity.Property(e => e.NeedInspection).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.InspectionPerson).HasMaxLength(50);
            entity.Property(e => e.InspectionCycleDays).IsRequired().HasDefaultValue(7);
            entity.Property(e => e.LastInspectionDate).HasColumnType("date");
            entity.Property(e => e.CurrentInspectionStartDate).HasColumnType("date");

            // 保养参数
            entity.Property(e => e.NeedMaintenance).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.MaintPerson).HasMaxLength(50);
            entity.Property(e => e.MaintCycleDays).IsRequired().HasDefaultValue(30);
            entity.Property(e => e.LastMaintDate).HasColumnType("date");
            entity.Property(e => e.CurrentMaintStartDate).HasColumnType("date");
            entity.Property(e => e.LastRepairDate).HasColumnType("date");

            // 状态字段
            entity.Property(e => e.LifecycleStatus)
                .IsRequired().HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(nameof(MES.Core.Enums.LifecycleStatus.Active));
            entity.Property(e => e.UsageType)
                .IsRequired().HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(nameof(MES.Core.Enums.UsageType.Primary));

            // 物化状态字段
            entity.Property(e => e.RunningStatus)
                .IsRequired().HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(nameof(MES.Core.Enums.RunningStatus.Normal));
            entity.Property(e => e.InspectionStatus)
                .IsRequired().HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(nameof(MES.Core.Enums.EquipmentTaskStatus.NotApplicable));
            entity.Property(e => e.MaintStatus)
                .IsRequired().HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(nameof(MES.Core.Enums.EquipmentTaskStatus.NotApplicable));

            // 索引
            entity.HasIndex(e => e.EquipmentCode).IsUnique().HasDatabaseName("UK_Equipment_Code");
            entity.HasIndex(e => e.EquipmentName).HasDatabaseName("IX_Equipment_Name");
            entity.HasIndex(e => e.LifecycleStatus).HasDatabaseName("IX_Equipment_LifecycleStatus");
            entity.HasIndex(e => e.Location).HasDatabaseName("IX_Equipment_Location");
            entity.HasIndex(e => e.RelatedSection).HasDatabaseName("IX_Equipment_RelatedSection");
            entity.HasIndex(e => e.NeedInspection).HasDatabaseName("IX_Equipment_NeedInspection");
            entity.HasIndex(e => e.NeedMaintenance).HasDatabaseName("IX_Equipment_NeedMaintenance");
        });
    }

    private static void ConfigureRepairOrder(ModelBuilder builder)
    {
        builder.Entity<RepairOrder>(entity =>
        {
            entity.ToTable("RepairOrder");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RepairOrderNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EquipmentId).IsRequired();
            entity.Property(e => e.FaultDescription).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FaultType).HasMaxLength(50);
            entity.Property(e => e.Priority).IsRequired().HasMaxLength(20).HasDefaultValue(nameof(MES.Core.Enums.RepairPriority.Normal));
            entity.Property(e => e.RepairStatus)
                .IsRequired().HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(MES.Core.Enums.RepairOrderStatus.Pending);

            entity.Property(e => e.ReportPerson).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ReportTime).IsRequired().HasColumnType("datetime2");

            entity.Property(e => e.RepairPerson).HasMaxLength(50);
            entity.Property(e => e.RepairStartTime).HasColumnType("datetime2");
            entity.Property(e => e.RepairEndTime).HasColumnType("datetime2");
            entity.Property(e => e.RepairContent).HasMaxLength(1000);
            entity.Property(e => e.SparePartUsed).HasMaxLength(500);

            entity.HasOne<Equipment>()
                .WithMany()
                .HasForeignKey(e => e.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.RepairOrderNo).IsUnique().HasDatabaseName("UK_RepairOrder_No");
            entity.HasIndex(e => e.EquipmentId).HasDatabaseName("IX_RepairOrder_EquipmentId");
            entity.HasIndex(e => e.RepairStatus).HasDatabaseName("IX_RepairOrder_Status");
            entity.HasIndex(e => e.ReportTime).HasDatabaseName("IX_RepairOrder_ReportTime");
        });
    }

    private static void ConfigureMaintenanceOrder(ModelBuilder builder)
    {
        builder.Entity<MaintenanceOrder>(entity =>
        {
            entity.ToTable("MaintenanceOrder");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MaintOrderNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EquipmentId).IsRequired();
            entity.Property(e => e.ActualDate).HasColumnType("date");
            entity.Property(e => e.Executor).HasMaxLength(50);
            entity.Property(e => e.ExecutionSummary).HasMaxLength(500); entity.Property(e => e.Remark).HasMaxLength(500);

            entity.HasOne<Equipment>()
                .WithMany()
                .HasForeignKey(e => e.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.MaintOrderNo).IsUnique().HasDatabaseName("UK_MaintenanceOrder_No");
            entity.HasIndex(e => e.EquipmentId).HasDatabaseName("IX_MaintenanceOrder_EquipmentId");
        });
    }

    private static void ConfigureInspectionRecord(ModelBuilder builder)
    {
        builder.Entity<InspectionRecord>(entity =>
        {
            entity.ToTable("InspectionRecord");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RecordNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EquipmentId).IsRequired();
            entity.Property(e => e.ActualDate).HasColumnType("date");
            entity.Property(e => e.Inspector).HasMaxLength(50);
            entity.Property(e => e.ExecutionSummary).HasMaxLength(500);
            entity.Property(e => e.Remark).HasMaxLength(500);

            entity.HasOne<Equipment>()
                .WithMany()
                .HasForeignKey(e => e.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.RecordNo).IsUnique().HasDatabaseName("UK_InspectionRecord_No");
            entity.HasIndex(e => e.EquipmentId).HasDatabaseName("IX_InspectionRecord_EquipmentId");
        });
    }

    private static void ConfigureWorkOrderExecutionSummary(ModelBuilder builder)
    {
        builder.Entity<WorkOrderExecutionSummary>(entity =>
        {
            entity.ToTable("WorkOrderExecutionSummary");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.WorkOrderId).IsRequired();
            entity.Property(e => e.WorkOrderNo).IsRequired().HasMaxLength(50);

            // Group 1
            entity.Property(e => e.Salesman).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SignDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.DeliveryDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.DelayPenalty).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.SettlementMethod).IsRequired().HasMaxLength(20);
            entity.Property(e => e.SalesOrderNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProductionMainNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProductionSubNo).HasMaxLength(50);
            entity.Property(e => e.MaterialName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DeliveryState).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LengthStatus).IsRequired().HasMaxLength(20);
            entity.Property(e => e.MinLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MaxLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalItemCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.TotalQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.TotalMeters).IsRequired().HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            entity.Property(e => e.TotalWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);

            // Group 2
            entity.Property(e => e.LatestPlanDate).HasColumnType("date");
            entity.Property(e => e.MaterialPlanRate).HasColumnType("decimal(7,2)").HasDefaultValue(0m);
            entity.Property(e => e.MaterialPlanStatus).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.MainNoMaterialPlanRate).HasColumnType("decimal(7,2)").HasDefaultValue(0m);
            entity.Property(e => e.MainNoMaterialPlanStatus).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.ProcessCycle).IsRequired().HasDefaultValue(0);

            // Group 5
            entity.Property(e => e.PendingRoughTubeQty).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.PendingRoughTubeWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.PendingOutsourceFinishQty).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.PendingOutsourceFinishWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.TheoreticalFinishQty).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.TheoreticalFinishWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);

            // Group 3
            entity.Property(e => e.InputStartDate).HasColumnType("date");
            entity.Property(e => e.InputEndDate).HasColumnType("date");
            entity.Property(e => e.TotalBatchCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.InputQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.InputWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.TheoreticalOutputQty).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.TheoreticalOutputWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.InputOutputRatio).HasColumnType("decimal(8,2)").HasDefaultValue(0m);
            entity.Property(e => e.InputStatus).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.MainNoInputOutputRatio).HasColumnType("decimal(8,2)").HasDefaultValue(0m);
            entity.Property(e => e.MainNoInputStatus).IsRequired().HasDefaultValue(0);

            // Group 4
            entity.Property(e => e.ValidBatchCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.ValidInputQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.ValidInputWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.ValidOutputQty).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.ValidOutputWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);

            // Group 6
            entity.Property(e => e.ReworkInputEndDate).HasColumnType("date");
            entity.Property(e => e.ReworkBatchCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.ReworkInputQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.ReworkInputWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.ReworkTheoreticalOutputQty).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.ReworkTheoreticalOutputWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);

            // Group 7
            entity.Property(e => e.FlowOutputRatio).HasColumnType("decimal(8,2)").HasDefaultValue(0m);
            entity.Property(e => e.FlowStatus).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.MainNoFlowOutputRatio).HasColumnType("decimal(8,2)").HasDefaultValue(0m);
            entity.Property(e => e.MainNoFlowStatus).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.FlowTotalBatchCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.FlowIncompleteBatchCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.FlowMaxRemainingWorkDays).IsRequired().HasDefaultValue(0);

            // Group 8: 过程不合格
            entity.Property(e => e.DefectiveRawQty).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.DefectiveRawWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.DefectiveOutputQty).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.DefectiveOutputWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.DefectiveRatio).HasColumnType("decimal(8,2)").HasDefaultValue(0m);

            // Group 9: 成检不合格
            entity.Property(e => e.InspectionStartDate).HasColumnType("datetime2");
            entity.Property(e => e.InspectionEndDate).HasColumnType("datetime2");
            entity.Property(e => e.InspectionDefectQty).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.InspectionDefectWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.InspectionDefectRatio).HasColumnType("decimal(8,2)").HasDefaultValue(0m);

            // Group 10: 汇总不合格
            entity.Property(e => e.GeneralDefectWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.GeneralDefectRatio).HasColumnType("decimal(8,2)").HasDefaultValue(0m);
            entity.Property(e => e.SeriousDefectWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.SeriousDefectRatio).HasColumnType("decimal(8,2)").HasDefaultValue(0m);
            entity.Property(e => e.ScrapWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.ScrapRatio).HasColumnType("decimal(8,2)").HasDefaultValue(0m);

            // Group 11: 成品入库
            entity.Property(e => e.WarehousingStartDate).HasColumnType("datetime2");
            entity.Property(e => e.WarehousingEndDate).HasColumnType("datetime2");
            entity.Property(e => e.WarehousingTotalQty).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.WarehousingTotalWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.WoWarehousingStatus).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.MainNoWarehousingStatus).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.OrderWarehousingStatus).IsRequired().HasDefaultValue(0);

            // G12: 实时关注
            entity.Property(e => e.ScheduleStage).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.TotalRemainingWorkDays).HasColumnType("int");
            entity.Property(e => e.CapacityWorkDays).HasColumnType("int");
            entity.Property(e => e.UrgencyLevel).HasMaxLength(20);
            entity.Property(e => e.EstimatedProcessCompletionDate).HasColumnType("date");
            entity.Property(e => e.DaysDiffFromDelivery).HasColumnType("int");

            // G12: 原锁备注
            entity.Property(e => e.RawMaterialLockRemark).HasMaxLength(20);

            // Group 14: 在产节点待量
            entity.Property(e => e.PendingSectionRoughTube).HasColumnType("decimal(18,3)");
            entity.Property(e => e.PendingSectionWarehouseFix).HasColumnType("decimal(18,3)");
            entity.Property(e => e.PendingSection60Roll).HasColumnType("decimal(18,3)");
            entity.Property(e => e.PendingSection50Roll).HasColumnType("decimal(18,3)");
            entity.Property(e => e.PendingSection30Roll).HasColumnType("decimal(18,3)");
            entity.Property(e => e.PendingSection20Roll).HasColumnType("decimal(18,3)");
            entity.Property(e => e.PendingSectionThreeRoll).HasColumnType("decimal(18,3)");
            entity.Property(e => e.PendingSectionDrawBench).HasColumnType("decimal(18,3)");
            entity.Property(e => e.ProductionAttentionProcess).HasMaxLength(50);

            // 刷新追踪
            entity.Property(e => e.LastRefreshTime).HasColumnType("datetime2");

            // 索引
            entity.HasIndex(e => e.WorkOrderId).IsUnique().HasDatabaseName("UK_WES_WorkOrderId");
            entity.HasIndex(e => e.WorkOrderNo).HasDatabaseName("IX_WES_WorkOrderNo");
            entity.HasIndex(e => e.SalesOrderNo).HasDatabaseName("IX_WES_SalesOrderNo");
            entity.HasIndex(e => e.ProductionMainNo).HasDatabaseName("IX_WES_ProductionMainNo");
            entity.HasIndex(e => e.InputStatus).HasDatabaseName("IX_WES_InputStatus");
        });
    }

    private static void ConfigureOrderListSummary(ModelBuilder builder)
    {
        builder.Entity<OrderListSummary>(entity =>
        {
            entity.ToTable("OrderListSummary");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.OrderId).IsRequired();
            entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SignDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Salesman).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EndCustomer).HasMaxLength(200);
            entity.Property(e => e.DeliveryStart).HasColumnType("date");
            entity.Property(e => e.DeliveryEnd).HasColumnType("date");
            entity.Property(e => e.HasDelayPenalty).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.TotalContractWeight).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.ItemCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.HasTechReqCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.RowVersion).IsRowVersion().IsRequired(false);
            entity.Property(e => e.LastChangeDate).HasColumnType("datetime2");

            // 索引
            entity.HasIndex(e => e.OrderId).IsUnique().HasDatabaseName("UK_OLS_OrderId");
            entity.HasIndex(e => e.OrderNumber).HasDatabaseName("IX_OLS_OrderNumber");
            entity.HasIndex(e => e.CustomerName).HasDatabaseName("IX_OLS_CustomerName");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_OLS_Status");
            entity.HasIndex(e => e.SignDate).HasDatabaseName("IX_OLS_SignDate");
            entity.HasIndex(e => e.DeliveryEnd).HasDatabaseName("IX_OLS_DeliveryEnd");
        });
    }

    private static void ConfigureWorkOrderListSummary(ModelBuilder builder)
    {
        builder.Entity<WorkOrderListSummary>(entity =>
        {
            entity.ToTable("WorkOrderListSummary");
            entity.HasKey(e => e.Id);

            // Group A: WorkOrder 基础字段
            entity.Property(e => e.WorkOrderId).IsRequired();
            entity.Property(e => e.WorkOrderNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SalesOrderNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProductionMainNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProductionSubNo).HasMaxLength(50);
            entity.Property(e => e.OrderItemIds).HasMaxLength(500);
            entity.Property(e => e.SignDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.Salesman).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EndCustomer).HasMaxLength(200);
            entity.Property(e => e.DeliveryDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.DelayPenalty).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.SettlementMethod).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.MaterialName).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.StandardCode).HasMaxLength(100);
            entity.Property(e => e.DeliveryState).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.OuterDiameterNegative).HasColumnType("decimal(18,2)");
            entity.Property(e => e.OuterDiameterPositive).HasColumnType("decimal(18,2)");
            entity.Property(e => e.WallThicknessNegative).HasColumnType("decimal(18,2)");
            entity.Property(e => e.WallThicknessPositive).HasColumnType("decimal(18,2)");
            entity.Property(e => e.LengthStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.MinLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MaxLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.TotalMeters).IsRequired().HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            entity.Property(e => e.TotalWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.TotalItemCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.ItemDetails).HasColumnType("nvarchar(max)");
            entity.Property(e => e.TechnicalRequirements).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(RequirementType.Normal);
            entity.Property(e => e.Status).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.CreatedTime).IsRequired().HasColumnType("datetimeoffset");

            // Group B: 预计算计划聚合
            entity.Property(e => e.LatestPlanDate).HasColumnType("date");
            entity.Property(e => e.MaterialPlanRate).HasColumnType("decimal(7,2)").HasDefaultValue(0m);
            entity.Property(e => e.MaterialPlanStatus).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.SemiPlanTotalWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.SemiPlanTotalPieces).IsRequired(false);
            entity.Property(e => e.FinishedPlanTotalWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.FinishedPlanTotalPieces).IsRequired(false);
            entity.Property(e => e.InventoryPlanTotalWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.InventoryPlanTotalPieces).IsRequired(false);
            entity.Property(e => e.ReworkPlanTotalWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.ReworkPlanTotalPieces).IsRequired(false);
            entity.Property(e => e.PiercingPlanTotalWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.PiercingPlanTotalPieces).IsRequired(false);
            entity.Property(e => e.InProcessReworkPlanTotalWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.InProcessReworkPlanTotalPieces).IsRequired(false);
            entity.Property(e => e.MaxStandardCycle).IsRequired().HasDefaultValue(0);

            // Group C: 预计算主号/订单聚合
            entity.Property(e => e.MainNoMaterialPlanRate).HasColumnType("decimal(7,2)").HasDefaultValue(0m);
            entity.Property(e => e.MainNoMaterialPlanStatus).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.OrderMaterialPlanStatus).IsRequired().HasDefaultValue(0);

            // Group D: 行元数据
            entity.Property(e => e.RowVersion).IsRowVersion().IsRequired(false);
            entity.Property(e => e.LastRefreshTime).HasColumnType("datetime2");

            // 索引
            entity.HasIndex(e => e.WorkOrderId).IsUnique().HasDatabaseName("UK_WOLS_WorkOrderId");
            entity.HasIndex(e => e.WorkOrderNo).HasDatabaseName("IX_WOLS_WorkOrderNo");
            entity.HasIndex(e => e.SalesOrderNo).HasDatabaseName("IX_WOLS_SalesOrderNo");
            entity.HasIndex(e => e.ProductionMainNo).HasDatabaseName("IX_WOLS_ProductionMainNo");
            entity.HasIndex(e => e.MaterialPlanStatus).HasDatabaseName("IX_WOLS_MaterialPlanStatus");
            entity.HasIndex(e => e.MainNoMaterialPlanStatus).HasDatabaseName("IX_WOLS_MainNoMaterialPlanStatus");
            entity.HasIndex(e => e.OrderMaterialPlanStatus).HasDatabaseName("IX_WOLS_OrderMaterialPlanStatus");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_WOLS_Status");
            entity.HasIndex(e => e.LatestPlanDate).HasDatabaseName("IX_WOLS_LatestPlanDate");
        });
    }

    // ========== 工单上下文 ==========

    // ========== Scheduling 上下文 ==========

    private static void ConfigureOrderDemandAdjustment(ModelBuilder builder)
    {
        builder.Entity<OrderDemandAdjustment>(entity =>
        {
            entity.ToTable("OrderDemandAdjustment");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkOrderId).IsRequired();
            entity.Property(e => e.IsUrging).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.IsBatchDelivery).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.IsPaused).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.AdjustmentRemark).HasMaxLength(500);
            entity.HasIndex(e => e.WorkOrderId).IsUnique().HasDatabaseName("UK_ODA_WorkOrderId");
        });
    }

    private static void ConfigureRawMaterialLockPreExecution(ModelBuilder builder)
    {
        builder.Entity<RawMaterialLockPreExecution>(entity =>
        {
            entity.ToTable("RawMaterialLockPreExecution");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkOrderId).IsRequired();
            entity.Property(e => e.IsPreInput).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.BudgetInputDate).HasColumnType("date");
            entity.Property(e => e.IsMainNoMaterialComplete).IsRequired().HasDefaultValue(false);

            // 索引
            entity.HasIndex(e => e.WorkOrderId).IsUnique().HasDatabaseName("UK_RMLPE_WorkOrderId");
        });
    }

    private static void ConfigureWorkOrderPlan(ModelBuilder builder)
    {
        builder.Entity<WorkOrderPlan>(entity =>
        {
            entity.ToTable("WorkOrderPlan");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkOrderId).IsRequired();
            entity.Property(e => e.UrgencyLevel).HasMaxLength(50);
            entity.Property(e => e.ProductionAttentionProcess).HasMaxLength(100);
            entity.Property(e => e.ProductionFlowProperty).HasMaxLength(50);

            // 唯一索引
            entity.HasIndex(e => e.WorkOrderId).IsUnique().HasDatabaseName("UK_WOP_WorkOrderId");
        });
    }

    private static void ConfigureColdRollSpecSchedule(ModelBuilder builder)
    {
        builder.Entity<ColdRollSpecSchedule>(entity =>
        {
            entity.ToTable("ColdRollSpecSchedule");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProcessType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.BilletSpec).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RollingSpec).IsRequired().HasMaxLength(100);
            entity.Property(e => e.MachineNo).HasMaxLength(200);
            entity.Property(e => e.CompletionType).IsRequired().HasMaxLength(20).HasDefaultValue("None");
            entity.Property(e => e.RollType).IsRequired().HasMaxLength(20).HasDefaultValue("None");
            entity.Property(e => e.MergeDisplay).HasMaxLength(300);
            entity.Property(e => e.Remark).HasMaxLength(500);

            // 唯一索引：四维度组合不重复（ProcessType + BilletSpec + RollingSpec + IsFinished）
            entity.HasIndex(e => new { e.ProcessType, e.BilletSpec, e.RollingSpec, e.IsFinished })
                .IsUnique()
                .HasDatabaseName("UK_CRSS_Dimensions");
        });
    }

    // ========== Configuration 上下文 ==========

    private static void ConfigureStandardWorkDay(ModelBuilder builder)
    {
        builder.Entity<StandardWorkDay>(entity =>
        {
            entity.ToTable("StandardWorkDays");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SectionName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PlantGradePrefix).HasMaxLength(50);
            entity.Property(e => e.StandardDays).IsRequired().HasColumnType("float");
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => new { e.SectionName, e.PlantGradePrefix })
                .IsUnique()
                .HasDatabaseName("UK_SWD_SectionName_PlantGradePrefix");
        });
    }

    private static void ConfigureStandardWorkDayDeliveryState(ModelBuilder builder)
    {
        builder.Entity<StandardWorkDayDeliveryState>(entity =>
        {
            entity.ToTable("StandardWorkDayDeliveryStates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeliveryState).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ExtraDays).IsRequired().HasColumnType("float");
            entity.Property(e => e.PlantGradePrefix).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => new { e.DeliveryState, e.PlantGradePrefix })
                .IsUnique()
                .HasDatabaseName("UK_SWDDS_DeliveryState_PlantGradePrefix");
        });
    }

    private static void ConfigureConfigParameter(ModelBuilder builder)
    {
        builder.Entity<ConfigParameter>(entity =>
        {
            entity.ToTable("ConfigParameters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CategoryDisplay).HasMaxLength(100);
            entity.Property(e => e.Context).HasMaxLength(50);
            entity.Property(e => e.ParamKey).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ParamValue).IsRequired().HasColumnType("decimal(18,4)");
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => new { e.Category, e.ParamKey })
                .IsUnique()
                .HasDatabaseName("UK_CP_Category_ParamKey");
        });
    }

    private static void ConfigureDailyOutputEstimate(ModelBuilder builder)
    {
        builder.Entity<DailyOutputEstimate>(entity =>
        {
            entity.ToTable("DailyOutputEstimates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MinOuterDiameter).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.DailyOutputTons).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.Remark).HasMaxLength(200);
        });
    }

    private static void ConfigureDailyProductionCapacity(ModelBuilder builder)
    {
        builder.Entity<DailyProductionCapacity>(entity =>
        {
            entity.ToTable("DailyProductionCapacities");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DailyCapacity).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.Remark).HasMaxLength(200);
        });
    }

    private static void ConfigureEmployee(ModelBuilder builder)
    {
        builder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Department).HasMaxLength(100);
            entity.Property(e => e.Position).HasMaxLength(100);
            entity.Property(e => e.PositionRemark).HasMaxLength(200);
            entity.Property(e => e.SalaryMode).HasMaxLength(50);
            entity.Property(e => e.SalaryRemark).HasMaxLength(200);
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("UK_Emp_Code");
        });
    }

    private static void ConfigureWorkstation(ModelBuilder builder)
    {
        builder.Entity<Workstation>(entity =>
        {
            entity.ToTable("Workstations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.EquipmentName).HasMaxLength(100);
            entity.Property(e => e.SectionName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ReportType).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("UK_WS_Code");
        });
    }

    private static void ConfigureSectionFlowCategorySetting(ModelBuilder builder)
    {
        builder.Entity<SectionFlowCategorySetting>(entity =>
        {
            entity.ToTable("SectionFlowCategorySettings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CategoryCode).IsRequired().HasMaxLength(10);
            entity.Property(e => e.CategoryName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DailyProductionTarget).HasColumnType("decimal(18,2)");
            entity.Property(e => e.LowerLimitDays).HasColumnType("decimal(18,2)");
            entity.Property(e => e.UpperLimitDays).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => e.CategoryCode)
                .IsUnique()
                .HasDatabaseName("UK_SFCS_CategoryCode");
        });
    }

    private static void ConfigureSectionFlowCategoryItem(ModelBuilder builder)
    {
        builder.Entity<SectionFlowCategoryItem>(entity =>
        {
            entity.ToTable("SectionFlowCategoryItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProcessGroupName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SectionName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Coefficient).IsRequired().HasColumnType("decimal(18,4)");
            entity.HasOne(e => e.Setting)
                .WithMany(s => s.Items)
                .HasForeignKey(e => e.SettingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.SettingId, e.ProcessGroupName, e.SectionName })
                .IsUnique()
                .HasDatabaseName("UK_SFCI_SettingId_ProcessGroupName_SectionName");
        });
    }

    private static void ConfigureBatchPlanSchedule(ModelBuilder builder)
    {
        builder.Entity<BatchPlanSchedule>(entity =>
        {
            entity.ToTable("BatchPlanSchedules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BatchId).IsRequired();
            entity.Property(e => e.IsFlow).IsRequired();
            entity.Property(e => e.FlowLevel).IsRequired();
            entity.Property(e => e.FlowTarget).HasMaxLength(50);
            entity.Property(e => e.FlowCRType).HasMaxLength(100);
            entity.Property(e => e.FlowExecSpec).HasMaxLength(100);
            entity.Property(e => e.IsGrabOrder).IsRequired();
            entity.Property(e => e.PlanRemark).HasMaxLength(500);

            // 唯一索引
            entity.HasIndex(e => e.BatchId).IsUnique().HasDatabaseName("UK_BPS_BatchId");
        });
    }

    private static void ConfigureBatchPlanTarget(ModelBuilder builder)
    {
        builder.Entity<BatchPlanTarget>(entity =>
        {
            entity.ToTable("BatchPlanTargets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SectionName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DailyTarget).HasColumnType("decimal(18,2)");

            // 唯一索引：每个工段一条目标
            entity.HasIndex(e => e.SectionName).IsUnique().HasDatabaseName("UK_BPT_SectionName");
        });
    }

    // ================================================================
    //                      生产标准上下文配置
    // ================================================================

    private static void ConfigureStandardRegister(ModelBuilder builder)
    {
        builder.Entity<StandardRegister>(entity =>
        {
            entity.ToTable("StandardRegister");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StandardNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.StandardName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.RefSpecification).HasMaxLength(200);
            entity.Property(e => e.StandardLevel).HasMaxLength(20);
            entity.Property(e => e.ManufactureMethod).HasMaxLength(50);
            entity.Property(e => e.SteelType).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.HasIndex(e => e.StandardNo).IsUnique().HasDatabaseName("UK_StandardRegister_No");
        });
    }

    private static void ConfigureStandardRegisterItem(ModelBuilder builder)
    {
        builder.Entity<StandardRegisterItem>(entity =>
        {
            entity.ToTable("StandardRegisterItem");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InspectionCategory).HasMaxLength(50);
            entity.Property(e => e.InspectionItem).IsRequired().HasMaxLength(200);
            entity.Property(e => e.IsMandatory).HasMaxLength(50);
            entity.Property(e => e.SamplingRequirement).HasMaxLength(200);
            entity.Property(e => e.ApplicableRange).HasMaxLength(200);
            entity.Property(e => e.RefStandard).HasMaxLength(200);
            entity.Property(e => e.DetailRequirement).HasMaxLength(2000);
            entity.HasOne(e => e.StandardRegister)
                .WithMany(s => s.Items)
                .HasForeignKey(e => e.StandardRegisterId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.StandardRegisterId).HasDatabaseName("IX_StandardRegisterItem_RegisterId");
        });
    }
}