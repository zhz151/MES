// Auto-generated partial class for WorkOrder entity configurations
using Microsoft.EntityFrameworkCore;
using MES.Data.Entities.WorkOrder;
using MES.Core.Enums;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Order;

namespace MES.Data;

public partial class AppDbContext
{
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
            entity.Property(e => e.PipeManufacturingType).HasColumnName("MaterialName").IsRequired().HasConversion<string>().HasMaxLength(20);
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
            entity.HasIndex(e => e.PipeManufacturingType).HasDatabaseName("IX_WorkOrder_MaterialName");
            entity.HasIndex(e => e.Specification).HasDatabaseName("IX_WorkOrder_Specification");
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
            entity.Property(e => e.Remark).HasMaxLength(500);

            entity.Property(e => e.ColdRollDraw);
            entity.Property(e => e.OilPipeCut);
            entity.Property(e => e.Degrease);
            entity.Property(e => e.EmulsionWash);
            entity.Property(e => e.UltrasonicWash);
            entity.Property(e => e.ClothPolish);
            entity.Property(e => e.BrightAnnealing);
            entity.Property(e => e.Solution);
            entity.Property(e => e.Straighten);
            entity.Property(e => e.Cut);
            entity.Property(e => e.ThicknessMeasure);
            entity.Property(e => e.Pickle);
            entity.Property(e => e.OuterPolish);
            entity.Property(e => e.InnerPolish);
            entity.Property(e => e.InnerGrinding);
            entity.Property(e => e.OuterSpotGrinding);
            entity.Property(e => e.SandBlasting);
            entity.Property(e => e.ShotBlasting);
            entity.Property(e => e.Inspection);
            entity.Property(e => e.WeldingHead);
            entity.Property(e => e.Welding);
            entity.Property(e => e.Lubrication);
            entity.Property(e => e.Packing);
            entity.Property(e => e.Warehouse);
            entity.Property(e => e.Extra1);
            entity.Property(e => e.Extra2);

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
            entity.Property(e => e.Remark).HasMaxLength(500);

            entity.Property(e => e.ColdRollDraw);
            entity.Property(e => e.OilPipeCut);
            entity.Property(e => e.Degrease);
            entity.Property(e => e.EmulsionWash);
            entity.Property(e => e.UltrasonicWash);
            entity.Property(e => e.ClothPolish);
            entity.Property(e => e.BrightAnnealing);
            entity.Property(e => e.Solution);
            entity.Property(e => e.Straighten);
            entity.Property(e => e.Cut);
            entity.Property(e => e.ThicknessMeasure);
            entity.Property(e => e.Pickle);
            entity.Property(e => e.OuterPolish);
            entity.Property(e => e.InnerPolish);
            entity.Property(e => e.InnerGrinding);
            entity.Property(e => e.OuterSpotGrinding);
            entity.Property(e => e.SandBlasting);
            entity.Property(e => e.ShotBlasting);
            entity.Property(e => e.Inspection);
            entity.Property(e => e.WeldingHead);
            entity.Property(e => e.Welding);
            entity.Property(e => e.Lubrication);
            entity.Property(e => e.Packing);
            entity.Property(e => e.Warehouse);
            entity.Property(e => e.Extra1);
            entity.Property(e => e.Extra2);

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
            entity.Property(e => e.Remark).HasMaxLength(500);

            entity.Property(e => e.ColdRollDraw);
            entity.Property(e => e.OilPipeCut);
            entity.Property(e => e.Degrease);
            entity.Property(e => e.EmulsionWash);
            entity.Property(e => e.UltrasonicWash);
            entity.Property(e => e.ClothPolish);
            entity.Property(e => e.BrightAnnealing);
            entity.Property(e => e.Solution);
            entity.Property(e => e.Straighten);
            entity.Property(e => e.Cut);
            entity.Property(e => e.ThicknessMeasure);
            entity.Property(e => e.Pickle);
            entity.Property(e => e.OuterPolish);
            entity.Property(e => e.InnerPolish);
            entity.Property(e => e.InnerGrinding);
            entity.Property(e => e.OuterSpotGrinding);
            entity.Property(e => e.SandBlasting);
            entity.Property(e => e.ShotBlasting);
            entity.Property(e => e.Inspection);
            entity.Property(e => e.WeldingHead);
            entity.Property(e => e.Welding);
            entity.Property(e => e.Lubrication);
            entity.Property(e => e.Packing);
            entity.Property(e => e.Warehouse);
            entity.Property(e => e.Extra1);
            entity.Property(e => e.Extra2);

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
            entity.Property(e => e.PlantGrade).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Specification).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LengthStatus).IsRequired().HasMaxLength(20);
            entity.Property(e => e.InputMultiple).IsRequired().HasDefaultValue(1);
            entity.Property(e => e.UsedQuantity);
            entity.Property(e => e.UsedWeight).IsRequired().HasColumnType("decimal(18,3)");
            entity.Property(e => e.RequiredDate).HasColumnType("date");
            entity.Property(e => e.PlanStatus).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(InventoryPlanStatus.Planned);
            entity.Property(e => e.Remark).HasMaxLength(500);
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
            entity.Property(e => e.Remark).HasMaxLength(500);

            entity.Property(e => e.ColdRollDraw);
            entity.Property(e => e.OilPipeCut);
            entity.Property(e => e.Degrease);
            entity.Property(e => e.EmulsionWash);
            entity.Property(e => e.UltrasonicWash);
            entity.Property(e => e.ClothPolish);
            entity.Property(e => e.BrightAnnealing);
            entity.Property(e => e.Solution);
            entity.Property(e => e.Straighten);
            entity.Property(e => e.Cut);
            entity.Property(e => e.ThicknessMeasure);
            entity.Property(e => e.Pickle);
            entity.Property(e => e.OuterPolish);
            entity.Property(e => e.InnerPolish);
            entity.Property(e => e.InnerGrinding);
            entity.Property(e => e.OuterSpotGrinding);
            entity.Property(e => e.SandBlasting);
            entity.Property(e => e.ShotBlasting);
            entity.Property(e => e.Inspection);
            entity.Property(e => e.WeldingHead);
            entity.Property(e => e.Welding);
            entity.Property(e => e.Lubrication);
            entity.Property(e => e.Packing);
            entity.Property(e => e.Warehouse);
            entity.Property(e => e.Extra1);
            entity.Property(e => e.Extra2);

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
            entity.Property(e => e.MaterialPlanStatus).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.MainNoMaterialPlanRate).HasColumnType("decimal(7,2)").HasDefaultValue(0m);
            entity.Property(e => e.MainNoMaterialPlanStatus).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.ProcessCycle).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.TheoreticalCutoffDate).HasColumnType("date");

            // Group 3（新）: 圆棒穿孔计划执行
            entity.Property(e => e.PiercingPlanWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.PiercingSubOutWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.PiercingSubStatus).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.PiercingSubInWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.PiercingSubPendingWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.PiercingReturnStatus).IsRequired().HasDefaultValue(0);

            // Group 4（新）: 荒管采购计划执行
            entity.Property(e => e.SemiPlanWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.SemiOrderWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.SemiOrderStatus).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.SemiInWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.SemiPendingWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.SemiInStatus).IsRequired().HasDefaultValue(0);

            // Group 5（新）: 成品采购计划执行
            entity.Property(e => e.FinishPlanWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.FinishOrderWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.FinishOrderStatus).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.FinishInWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.FinishPendingWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.FinishInStatus).IsRequired().HasDefaultValue(0);

            // Group 6（新）: 库存使用计划执行
            entity.Property(e => e.InventoryPlanWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.InventoryOutWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.InventoryOutStatus).IsRequired().HasDefaultValue(0);

            // Group 7（新）: 库料改制计划执行
            entity.Property(e => e.ReworkPlanWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.ReworkPlanInputWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.ReworkPlanInputStatus).IsRequired().HasDefaultValue(0);

            // Group 8（新）: 在产改制计划执行
            entity.Property(e => e.InProcessReworkPlanWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.InProcessReworkInputWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.InProcessReworkInputStatus).IsRequired().HasDefaultValue(0);

            // Group 9（新）: 在产主工单计划执行
            entity.Property(e => e.InMainPlanWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.InMainInputWeight).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
            entity.Property(e => e.InMainInputStatus).IsRequired().HasDefaultValue(0);

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
            entity.Property(e => e.ProcessInspectionReworkWeight);
            entity.Property(e => e.FinalInspectionReworkWeight);
            entity.Property(e => e.ReworkTheoreticalProduceQty);
            entity.Property(e => e.ReworkTheoreticalProduceWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.PendingReworkOutputQty).HasColumnType("decimal(18,3)");
            entity.Property(e => e.PendingReworkOutputWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.ReworkMainNoStatus).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.ReworkInputConsistency);
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
            entity.Property(e => e.InMainWorkOrderPlanTotalWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.InMainWorkOrderPlanTotalPieces).IsRequired(false);
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

    private static void ConfigureFixedLengthWorkOrder(ModelBuilder builder)
    {
        builder.Entity<FixedLengthWorkOrder>(entity =>
        {
            entity.ToTable("FixedLengthWorkOrder");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkOrderId).IsRequired();
            entity.Property(e => e.WorkOrderNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SalesOrderNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProductionMainNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Length).IsRequired().HasColumnType("decimal(18,2)");
            entity.Property(e => e.PlannedQuantity).IsRequired();

            entity.HasOne<WorkOrder>()
                .WithMany()
                .HasForeignKey(e => e.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.WorkOrderId).HasDatabaseName("IX_FixedLengthWorkOrder_WorkOrderId");
            entity.HasIndex(e => e.WorkOrderNo).HasDatabaseName("IX_FixedLengthWorkOrder_WorkOrderNo");
            entity.HasIndex(e => new { e.SalesOrderNo, e.ProductionMainNo, e.Length })
                .HasDatabaseName("IX_FixedLengthWorkOrder_SalesOrderMainNoLength");
            entity.HasIndex(e => new { e.WorkOrderId, e.Length })
                .IsUnique()
                .HasDatabaseName("UK_FixedLengthWorkOrder_WorkOrderLength");
        });
    }

    private static void ConfigureInMainWorkOrderPlan(ModelBuilder builder)
    {
        builder.Entity<InMainWorkOrderPlan>(entity =>
        {
            entity.ToTable("InMainWorkOrderPlan");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkOrderId).IsRequired();
            entity.Property(e => e.PlanDate).IsRequired().HasColumnType("date");
            entity.Property(e => e.ProductionBatchId).IsRequired();
            entity.Property(e => e.BatchNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.MainWorkOrderNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AllocatedWeight).IsRequired().HasColumnType("decimal(18,3)");
            entity.Property(e => e.AllocatedQuantity);
            entity.Property(e => e.ProductionRatio).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.StandardCycle).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.RequiredDate).HasColumnType("date");
            entity.Property(e => e.PlanStatus).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(InventoryPlanStatus.Planned);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.HasIndex(e => e.WorkOrderId).HasDatabaseName("IX_InMainWorkOrderPlan_WorkOrderId");
            entity.HasIndex(e => e.ProductionBatchId).HasDatabaseName("IX_InMainWorkOrderPlan_ProductionBatchId");
            entity.HasIndex(e => e.PlanStatus).HasDatabaseName("IX_InMainWorkOrderPlan_PlanStatus");
        });
    }
}
