// Auto-generated partial class for Material entity configurations
using Microsoft.EntityFrameworkCore;
using MES.Data.Entities.Materials;
using MES.Core.Enums;

namespace MES.Data;

public partial class AppDbContext
{
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
}
