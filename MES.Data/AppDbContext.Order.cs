// Auto-generated partial class for Order entity configurations
using Microsoft.EntityFrameworkCore;
using MES.Data.Entities.Order;
using MES.Core.Enums;
using MES.Data.Entities.Auth;
using MES.Data.Entities.WorkOrder;

namespace MES.Data;

public partial class AppDbContext
{
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
            entity.HasIndex(e => e.SignDate).HasDatabaseName("IX_SalesOrder_SignDate");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_SalesOrder_Status");
            // CustomerId FK 已移除——订单不再维护与 CustomerProfile 的外键关系，仅保留快照字段
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
            entity.Property(e => e.PipeManufacturingType).HasColumnName("MaterialName").IsRequired().HasConversion<string>().HasMaxLength(20);
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
            entity.Property(e => e.EndCustomer).IsRequired().HasMaxLength(200);
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
            // 化学分析(成品)：bit，默认 false
            entity.Property(e => e.ChemicalComposition).IsRequired().HasDefaultValue(false);
            // 10 个成品检验项（含射线探伤）：枚举字符串存储（终/预/预+终/-），默认「终」=仅正式成检
            entity.Property(e => e.PmiInspection).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(InspectionRequirementStage.FinalOnly);
            entity.Property(e => e.SurfaceInspection).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(InspectionRequirementStage.FinalOnly);
            entity.Property(e => e.Dimension).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(InspectionRequirementStage.FinalOnly);
            entity.Property(e => e.Endoscopy).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(InspectionRequirementStage.FinalOnly);
            entity.Property(e => e.HydrostaticTest).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(InspectionRequirementStage.FinalOnly);
            entity.Property(e => e.UnderwaterPressure).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(InspectionRequirementStage.FinalOnly);
            entity.Property(e => e.EddyCurrent).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(InspectionRequirementStage.FinalOnly);
            entity.Property(e => e.UltrasonicTest).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(InspectionRequirementStage.FinalOnly);
            entity.Property(e => e.PortColoring).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(InspectionRequirementStage.FinalOnly);
            entity.Property(e => e.RadiographicTest).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(InspectionRequirementStage.FinalOnly);
            entity.Property(e => e.HardnessRockwell).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.HardnessBrinell).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.HardnessVickers).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.TensileRoomTemp).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.TensileHighTemp).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.WeldJointTensile).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.ImpactTest).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.WeldJointImpact).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.FlatteningTest).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.FlaringTest).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.ExpandingTest).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.BendTest).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.WeldJointBend).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.GrainSize).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.IntergranularCorrosion).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.PittingCorrosion).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.FerriteContent).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.Macrostructure).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.OtherRequirement).HasMaxLength(1000);
            entity.HasIndex(e => e.OrderItemId).IsUnique().HasDatabaseName("UK_ProductRequirement_OrderItemId");
            entity.HasIndex(e => e.RequirementType).HasDatabaseName("IX_ProductRequirement_RequirementType");
            entity.HasOne(e => e.OrderItem).WithOne(oi => oi.ProductRequirement).HasForeignKey<ProductRequirement>(e => e.OrderItemId).OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(e => e.FinishedInboundWeight).IsRequired().HasDefaultValue(0m).HasColumnType("decimal(18,2)");
            entity.Property(e => e.FinishedOutboundWeight).IsRequired().HasDefaultValue(0m).HasColumnType("decimal(18,2)");
            entity.Property(e => e.FinishedStockWeight).IsRequired().HasDefaultValue(0m).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BusinessCompleted).IsRequired().HasDefaultValue(false);

            // 索引
            entity.HasIndex(e => e.OrderId).IsUnique().HasDatabaseName("UK_OLS_OrderId");
            entity.HasIndex(e => e.OrderNumber).HasDatabaseName("IX_OLS_OrderNumber");
            entity.HasIndex(e => e.CustomerName).HasDatabaseName("IX_OLS_CustomerName");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_OLS_Status");
            entity.HasIndex(e => e.SignDate).HasDatabaseName("IX_OLS_SignDate");
            entity.HasIndex(e => e.DeliveryEnd).HasDatabaseName("IX_OLS_DeliveryEnd");
        });
    }
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
            entity.Property(e => e.IsForceCompleted).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.AdjustmentRemark).HasMaxLength(500);
            entity.HasIndex(e => e.WorkOrderId).IsUnique().HasDatabaseName("UK_ODA_WorkOrderId");
        });
    }
}
