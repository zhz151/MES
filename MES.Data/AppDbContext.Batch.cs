// Auto-generated partial class for Batch entity configurations
using Microsoft.EntityFrameworkCore;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Infrastructure;
using MES.Core.Enums;

namespace MES.Data;

public partial class AppDbContext
{
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
            entity.Property(e => e.ManufacturingStatus).HasMaxLength(50);
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

            // 投料信息字段
            entity.Property(e => e.SourceBatchNo).HasMaxLength(50);
            entity.Property(e => e.SourceMaterialType).HasMaxLength(30);
            entity.Property(e => e.SourceName).HasMaxLength(200);
            entity.Property(e => e.SourceHeatNo).HasMaxLength(50);
            entity.Property(e => e.SourcePlantGrade).HasMaxLength(50);
            entity.Property(e => e.SourceSpecification).HasMaxLength(100);
            entity.Property(e => e.SourceLengthStatus).HasMaxLength(20);
            entity.Property(e => e.SourceUnitWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.InputWeight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.InputType).IsRequired().HasConversion<string>().HasMaxLength(20).HasDefaultValue(BatchInputType.SplitFromNumber);
            entity.Property(e => e.CurrentValidQty);
            entity.Property(e => e.CurrentValidWeight).HasColumnType("decimal(18,3)");

            // 成切跟踪字段
            entity.Property(e => e.CutRequirement).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.CutExecution);
            entity.Property(e => e.CutQuantity);
            entity.Property(e => e.CutDoubt).HasConversion<string>().HasMaxLength(20);

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
            entity.Property(e => e.BatchNo).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(500);

            // 26个工段字段（int?，无默认值）
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
    private static void ConfigureProductionBatchInventory(ModelBuilder builder)
    {
        builder.Entity<ProductionBatchInventory>(entity =>
        {
            entity.ToTable("ProductionBatchInventory");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductionBatchId).IsRequired();
            entity.Property(e => e.InventoryBatchId).IsRequired();
            entity.Property(e => e.OutboundRecordId).IsRequired(false);
            entity.Property(e => e.InputQuantity).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.InputWeight).IsRequired().HasColumnType("decimal(18,3)").HasDefaultValue(0m);

            entity.HasOne(e => e.ProductionBatch)
                .WithMany(p => p.ProductionBatchInventories)
                .HasForeignKey(e => e.ProductionBatchId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.InventoryBatch)
                .WithMany(p => p.ProductionBatchInventories)
                .HasForeignKey(e => e.InventoryBatchId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.OutboundRecord)
                .WithMany()
                .HasForeignKey(e => e.OutboundRecordId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => e.ProductionBatchId).HasDatabaseName("IX_PBI_ProductionBatchId");
            entity.HasIndex(e => e.InventoryBatchId).HasDatabaseName("IX_PBI_InventoryBatchId");
            entity.HasIndex(e => e.OutboundRecordId).HasDatabaseName("IX_PBI_OutboundRecordId");
        });
    }
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
            entity.Property(e => e.Shift).HasMaxLength(20);
            entity.Property(e => e.Quantity);
            entity.Property(e => e.Weight).HasColumnType("decimal(18,3)");
            entity.Property(e => e.ProductStatus).HasMaxLength(20);
            entity.Property(e => e.LengthStatus).HasMaxLength(20);
            entity.Property(e => e.CuttingMultiple).HasColumnType("decimal(5,2)");
            entity.Property(e => e.FinishedCutLength).HasColumnType("decimal(18,2)");
            entity.Property(e => e.CutLengthMatchType).HasMaxLength(20);
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
            // 非断切工段：同一批次+工序组内工段唯一
            entity.HasIndex(e => new { e.ProductionBatchId, e.ProcessGroupId, e.SectionName })
                .IsUnique()
                .HasDatabaseName("UK_ProductionRecord_Section")
                .HasFilter("[FinishedCutLength] IS NULL");
            // 断切工段：同一批次+工序组+工段，按执行日期+成品断切长度唯一（同批可多条不同长度）
            entity.HasIndex(e => new { e.ProductionBatchId, e.ProcessGroupId, e.SectionName, e.ExecDate, e.FinishedCutLength })
                .IsUnique()
                .HasDatabaseName("UK_ProductionRecord_Section_Cut")
                .HasFilter("[FinishedCutLength] IS NOT NULL");
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
            entity.Property(e => e.IsInternal).HasDefaultValue(false);
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
            entity.Property(e => e.Shift).HasMaxLength(20);
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
            entity.Property(e => e.Shift).HasMaxLength(20);
            entity.Property(e => e.Quantity);
            entity.Property(e => e.Weight).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ProductStatus).HasMaxLength(20);
            entity.Property(e => e.PlantGrade).HasMaxLength(50);
            entity.Property(e => e.BatchNo).HasMaxLength(50);
            entity.Property(e => e.ProcessName).HasMaxLength(50);
            entity.Property(e => e.TagNo).HasMaxLength(50);

            entity.HasOne(e => e.PicklingInRecord)
                .WithMany(e => e.PicklingOutRecords)
                .HasForeignKey(e => e.PicklingInRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.PicklingInRecordId).HasDatabaseName("IX_PicklingOutRecord_InRecordId");
        });
    }
    private static void ConfigureOperationLog(ModelBuilder builder)
    {
        builder.Entity<OperationLog>(entity =>
        {
            entity.ToTable("OperationLog");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Module).IsRequired().HasMaxLength(20);
            entity.Property(e => e.EntityId).IsRequired();
            entity.Property(e => e.OperationType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Detail).HasMaxLength(2000);

            entity.HasIndex(e => new { e.Module, e.EntityId })
                .HasDatabaseName("IX_OperationLog_Module_EntityId");
        });
    }
}
