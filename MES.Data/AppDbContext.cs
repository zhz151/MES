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

        // ========== 仓库上下文 ==========
        ConfigureWarehouse(builder);
        ConfigureInventoryBatch(builder);
        ConfigureOutboundRecord(builder);
        ConfigureNotification(builder);
        ConfigureInventoryBatchDeleteLog(builder);

        // 软删除过滤：只为需要软删除的实体添加（排除 WorkOrder 和 OrderChangeNotification）
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                // 工单、通知、刷新令牌、采购计划不应用软删除过滤（使用物理删除）
                if (entityType.ClrType == typeof(WorkOrder) ||
                    entityType.ClrType == typeof(OrderChangeNotification) ||
                    entityType.ClrType == typeof(RefreshToken) ||
                    entityType.ClrType == typeof(PurchaseSemiPlan) ||
                    entityType.ClrType == typeof(PurchaseFinishedPlan) ||
                    entityType.ClrType == typeof(InventoryPlan))
                {
                    continue;
                }

                var method = typeof(AppDbContext).GetMethod(nameof(SetSoftDeleteFilter),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    ?.MakeGenericMethod(entityType.ClrType);
                method?.Invoke(null, new object[] { builder });
            }
        }
    }

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder builder) where TEntity : BaseEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<BaseEntity>();
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
                    entry.Entity.IsDeleted = false;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedTime = now;
                    entry.Entity.UpdatedBy = currentUser;
                    break;

                case EntityState.Deleted:
                    // 工单、通知、刷新令牌、采购计划：物理删除，保持 Deleted 状态
                    if (entry.Entity is WorkOrder || entry.Entity is OrderChangeNotification || entry.Entity is RefreshToken ||
                        entry.Entity is PurchaseSemiPlan || entry.Entity is PurchaseFinishedPlan || entry.Entity is InventoryPlan)
                    {
                        // 保持 Deleted 状态，让 EF Core 执行物理删除
                        break;
                    }
                    // 其他实体：软删除
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedTime = now;
                    entry.Entity.UpdatedBy = currentUser;
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
            entity.Property(e => e.SignDate).IsRequired().HasColumnType("datetime");
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
            entity.Property(e => e.DeliveryDate).IsRequired().HasColumnType("datetime");
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
                .HasFilter("[IsDeleted] = 0")
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
            entity.Property(e => e.SignDate).IsRequired().HasColumnType("datetime");
            entity.Property(e => e.Salesman).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EndCustomer).HasMaxLength(200);
            entity.Property(e => e.DeliveryDate).IsRequired().HasColumnType("datetime");
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

            // 索引（不包含 IsDeleted 条件，因为工单使用物理删除）
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
            entity.Property(e => e.RequiredDate).HasColumnType("date");
            entity.Property(e => e.ProcessPlan).HasColumnType("nvarchar(max)");
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.HasIndex(e => e.WorkOrderId).HasDatabaseName("IX_PurchaseSemiPlan_WorkOrderId");
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
            entity.HasIndex(e => e.WorkOrderId).HasDatabaseName("IX_PurchaseFinishedPlan_WorkOrderId");
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
            entity.Property(e => e.InboundDate).IsRequired().HasColumnType("datetime");

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
                .HasFilter("[RemainingWeight] > 0 AND [IsDeleted] = 0");
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
            entity.Property(e => e.TargetCompany).HasMaxLength(200);
            entity.Property(e => e.OutboundQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.OutboundWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.OutboundDate).IsRequired().HasColumnType("datetime");
            entity.Property(e => e.Operator).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(500);

            // 审计字段
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).IsRequired().HasMaxLength(50);
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

            // 索引
            entity.HasIndex(e => e.InventoryBatchId).HasDatabaseName("IX_OutboundRecord_InventoryBatchId");
            entity.HasIndex(e => e.OutboundDate).HasDatabaseName("IX_OutboundRecord_OutboundDate");
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
            entity.Property(e => e.DeletedTime).IsRequired().HasColumnType("datetime");
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
            entity.Property(e => e.InventoryBatchId).IsRequired();
            entity.Property(e => e.BatchNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
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
            entity.HasIndex(e => e.InventoryBatchId).HasDatabaseName("IX_InventoryPlan_InventoryBatchId");
            entity.HasIndex(e => e.PlanStatus).HasDatabaseName("IX_InventoryPlan_PlanStatus");
        });
    }
}