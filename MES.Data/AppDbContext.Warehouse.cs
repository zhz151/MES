// Auto-generated partial class for Warehouse entity configurations
using Microsoft.EntityFrameworkCore;
using MES.Data.Entities.Warehouse;

namespace MES.Data;

public partial class AppDbContext
{
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
            entity.Property(e => e.RemainingMeters).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RemainingQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.RemainingWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);

            // 实际规格
            entity.Property(e => e.ActualSpecification).HasMaxLength(100);

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
            entity.Property(e => e.OutboundMeters).HasColumnType("decimal(18,2)");
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
}
