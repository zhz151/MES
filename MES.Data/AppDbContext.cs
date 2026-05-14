using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using MES.Data.Entities;
using MES.Core.Enums;

namespace MES.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    // 无参构造函数（用于工具项目）
    public AppDbContext() : base()
    {
        _httpContextAccessor = null;
    }

    // 仅 DbContextOptions 构造函数（用于工具项目）
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        _httpContextAccessor = null;
    }

    // 完整构造函数（用于 Web API 项目）
    public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<SalesOrder> SalesOrders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;
    public DbSet<CustomerProfile> CustomerProfiles { get; set; } = null!;
    public DbSet<ProductionStandard> ProductionStandards { get; set; } = null!;
    public DbSet<ProductRequirement> ProductRequirements { get; set; } = null!;
    public DbSet<StandardGradeMapping> StandardGradeMappings { get; set; } = null!;
    public DbSet<WorkOrder> WorkOrders { get; set; } = null!;
    public DbSet<OrderChangeNotification> OrderChangeNotifications { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<PurchaseSemiPlan> PurchaseSemiPlans { get; set; } = null!;
    public DbSet<PurchaseFinishedPlan> PurchaseFinishedPlans { get; set; } = null!;

    // ========== 仓库上下文 ==========

    public DbSet<Warehouse> Warehouses { get; set; } = null!;
    public DbSet<InventoryBatch> InventoryBatches { get; set; } = null!;
    public DbSet<OutboundRecord> OutboundRecords { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<InventoryBatchDeleteLog> InventoryBatchDeleteLogs { get; set; } = null!;
    public DbSet<InventoryPlan> InventoryPlans { get; set; } = null!;

    // ========== 物料上下文 ==========

    public DbSet<Material> Materials { get; set; } = null!;
    public DbSet<SupplierProfile> SupplierProfiles { get; set; } = null!;
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; } = null!;
    public DbSet<SubcontractOrder> SubcontractOrders { get; set; } = null!;
    public DbSet<SubcontractReturnItem> SubcontractReturnItems { get; set; } = null!;

    // ========== 批次上下文 ==========

    public DbSet<ProductionBatch> ProductionBatches { get; set; } = null!;
    public DbSet<ProcessGroup> ProcessGroups { get; set; } = null!;

    // ========== 生产记录上下文 ==========

    public DbSet<ProductionRecord> ProductionRecords { get; set; } = null!;
    public DbSet<SectionOutsource> SectionOutsources { get; set; } = null!;
    public DbSet<OutsourceRecovery> OutsourceRecoveries { get; set; } = null!;
    public DbSet<MaterialReceiveCheck> MaterialReceiveChecks { get; set; } = null!;
    public DbSet<BatchOperationLog> BatchOperationLogs { get; set; } = null!;

    // ========== 质量上下文 ==========

    public DbSet<ProcessInspection> ProcessInspections { get; set; } = null!;
    public DbSet<ChemicalComposition> ChemicalCompositions { get; set; } = null!;
    public DbSet<FurnaceRegistration> FurnaceRegistrations { get; set; } = null!;
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

        ConfigureSalesOrder(builder);
        ConfigureOrderItem(builder);
        ConfigureCustomerProfile(builder);
        ConfigureProductionStandard(builder);
        ConfigureProductRequirement(builder);
        ConfigureStandardGradeMapping(builder);
        ConfigureWorkOrder(builder);
        ConfigureOrderChangeNotification(builder);
        ConfigureRefreshToken(builder);
        ConfigurePurchaseSemiPlan(builder);
        ConfigurePurchaseFinishedPlan(builder);
        ConfigureInventoryPlan(builder);

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
        ConfigureNotification(builder);
        ConfigureInventoryBatchDeleteLog(builder);

        // ========== 批次上下文 ==========
        ConfigureProductionBatch(builder);
        ConfigureProcessGroup(builder);

        // ========== 生产记录上下文 ==========
        ConfigureProductionRecord(builder);
        ConfigureSectionOutsource(builder);
        ConfigureOutsourceRecovery(builder);
        ConfigureMaterialReceiveCheck(builder);
        ConfigureBatchOperationLog(builder);

        // ========== 质量上下文 ==========
        ConfigureProcessInspection(builder);
        ConfigureChemicalComposition(builder);
        ConfigureFurnaceRegistration(builder);
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
        if (_httpContextAccessor == null)
            return "system";

        var userName = _httpContextAccessor?.HttpContext?.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userName))
            return userName;

        var emailClaim = _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Email);
        if (emailClaim != null)
            return emailClaim.Value;
        return "system";
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
            entity.Property(e => e.DeliveryDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.DelayPenalty).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.SettlementMethod).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.MaterialName).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.DeliveryState).IsRequired().HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.StandardGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Density).IsRequired().HasColumnType("decimal(18,4)");
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
            entity.HasIndex(e => e.ProductionStandardId).HasDatabaseName("IX_OrderItem_ProductStandardId");
            entity.HasIndex(e => e.StandardGrade).HasDatabaseName("IX_OrderItem_StandardGrade");
            entity.HasOne(e => e.SalesOrder).WithMany(s => s.OrderItems).HasForeignKey(e => e.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ProductionStandard).WithMany(p => p.OrderItems).HasForeignKey(e => e.ProductionStandardId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.GradeMapping).WithMany(g => g.OrderItems).HasForeignKey(e => e.StandardGrade).HasPrincipalKey(g => g.StandardGrade).OnDelete(DeleteBehavior.Restrict);
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

    private static void ConfigureProductionStandard(ModelBuilder builder)
    {
        builder.Entity<ProductionStandard>(entity =>
        {
            entity.ToTable("ProductionStandard");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StandardCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.StandardName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.HasIndex(e => e.StandardCode).IsUnique().HasDatabaseName("UK_ProductionStandard_Code");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_ProductionStandard_IsActive");
            entity.HasIndex(e => e.SortOrder).HasDatabaseName("IX_ProductionStandard_SortOrder");
        });
    }

    private static void ConfigureProductRequirement(ModelBuilder builder)
    {
        builder.Entity<ProductRequirement>(entity =>
        {
            entity.ToTable("ProductRequirement");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderItemId).IsRequired();
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
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Density).IsRequired().HasColumnType("decimal(18,4)");
            entity.Property(e => e.HeatTreatment).HasMaxLength(100);
            entity.Property(e => e.SpecialMaterial).HasDefaultValue(false);
            entity.Property(e => e.SpecialNote).HasMaxLength(500);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.HasIndex(e => e.StandardGrade).IsUnique().HasDatabaseName("UK_StandardGradeMapping_StandardGrade");
            entity.HasIndex(e => e.PlantGrade).HasDatabaseName("IX_StandardGradeMapping_PlantGrade");
            entity.HasIndex(e => e.SpecialMaterial).HasDatabaseName("IX_StandardGradeMapping_SpecialMaterial");
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
            entity.Property(e => e.ProcessPlan).HasColumnType("nvarchar(max)");
            entity.Property(e => e.Remark).HasMaxLength(500);
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

            entity.HasIndex(e => e.WorkOrderId).HasDatabaseName("IX_PurchaseFinishedPlan_WorkOrderId");

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
            entity.Property(e => e.ProcessPlan).HasColumnType("nvarchar(max)");
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
            entity.Property(e => e.Sequence).IsRequired();
            entity.Property(e => e.MaterialCategory).IsRequired().HasMaxLength(30);
            entity.Property(e => e.PlantGrade).HasMaxLength(50);
            entity.Property(e => e.ProcessSpecification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UnitWeight).HasColumnType("decimal(18,4)");
            entity.Property(e => e.RequiredWeight).HasColumnType("decimal(18,4)");
            entity.Property(e => e.ProcessStatusRemark).HasMaxLength(500);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.ProcessUnitPrice).HasColumnType("decimal(18,4)");
            entity.Property(e => e.ProcessTotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SourceWorkOrderNo).HasMaxLength(50);
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
            entity.Property(e => e.IsForceCompleted).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.QualityRemark).HasMaxLength(500);
            entity.Property(e => e.SolutionParams).HasMaxLength(500);
            entity.Property(e => e.CurrentExecDate).HasColumnType("datetime2");
            entity.Property(e => e.CurrentGroupName).HasMaxLength(50);
            entity.Property(e => e.CurrentSectionName).HasMaxLength(50);
            entity.Property(e => e.CurrentEquipmentName).HasMaxLength(100);
            entity.Property(e => e.CurrentOutsource).HasMaxLength(200);
            entity.Property(e => e.NextSectionName).HasMaxLength(50);
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
            entity.Property(e => e.IsFinished).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.CuttingMultiple).HasColumnType("decimal(5,2)");
            entity.Property(e => e.FinishedCutLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PostCutQuantity);
            entity.Property(e => e.TagNo).HasMaxLength(50);
            entity.Property(e => e.PlantGrade).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(500);

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

    private static void ConfigureMaterialReceiveCheck(ModelBuilder builder)
    {
        builder.Entity<MaterialReceiveCheck>(entity =>
        {
            entity.ToTable("MaterialReceiveCheck");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductionBatchId).IsRequired();
            entity.Property(e => e.ReceiveDate).IsRequired().HasColumnType("datetime2");
            entity.Property(e => e.ReceivedQuantity);
            entity.Property(e => e.ReceivedWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.Shift).HasMaxLength(10);
            entity.Property(e => e.Checker).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(500);

            entity.HasOne(e => e.ProductionBatch)
                .WithMany()
                .HasForeignKey(e => e.ProductionBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ProductionBatchId)
                .IsUnique()
                .HasDatabaseName("UK_MaterialReceiveCheck_BatchId");
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
            entity.Property(e => e.PlantGrade).HasMaxLength(50);
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
        });
    }
}