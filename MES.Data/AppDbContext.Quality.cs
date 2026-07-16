// Auto-generated partial class for Quality entity configurations
using Microsoft.EntityFrameworkCore;
using MES.Data.Entities.Quality;
using MES.Core.Enums;

namespace MES.Data;

public partial class AppDbContext
{
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
    private static void ConfigureCertificate(ModelBuilder builder)
    {
        builder.Entity<Certificate>(entity =>
        {
            entity.ToTable("Certificate");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CertificateNo).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IssueDate).IsRequired();
            entity.Property(e => e.CustomerName).HasMaxLength(200);
            entity.Property(e => e.ProductStandard).HasMaxLength(100);
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.DeliveryStatus).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.HasIndex(e => e.CertificateNo).IsUnique().HasDatabaseName("UK_Certificate_No");
            entity.HasMany(e => e.Items)
                  .WithOne()
                  .HasForeignKey(e => e.CertificateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
    private static void ConfigureCertificateItem(ModelBuilder builder)
    {
        builder.Entity<CertificateItem>(entity =>
        {
            entity.ToTable("CertificateItem");
            entity.HasKey(e => e.Id);

            // 仓库信息
            entity.Property(e => e.InventoryBatchNo).HasMaxLength(50);
            entity.Property(e => e.ProductionBatchNo).HasMaxLength(50);
            entity.Property(e => e.HeatNo).HasMaxLength(50);
            entity.Property(e => e.SteelGrade).HasMaxLength(100);
            entity.Property(e => e.Specification).HasMaxLength(200);
            entity.Property(e => e.LengthDesc).HasMaxLength(100);

            // 化学成分
            entity.Property(e => e.ChemC).HasPrecision(10, 4);
            entity.Property(e => e.ChemSi).HasPrecision(10, 4);
            entity.Property(e => e.ChemMn).HasPrecision(10, 4);
            entity.Property(e => e.ChemP).HasPrecision(10, 4);
            entity.Property(e => e.ChemS).HasPrecision(10, 4);
            entity.Property(e => e.ChemNi).HasPrecision(10, 4);
            entity.Property(e => e.ChemCr).HasPrecision(10, 4);
            entity.Property(e => e.ChemMo).HasPrecision(10, 4);
            entity.Property(e => e.ChemCu).HasPrecision(10, 4);
            entity.Property(e => e.ChemN).HasPrecision(10, 4);
            entity.Property(e => e.ChemNb).HasPrecision(10, 4);
            entity.Property(e => e.ChemTi).HasPrecision(10, 4);
            entity.Property(e => e.ChemFe).HasPrecision(10, 4);
            entity.Property(e => e.ChemAl).HasPrecision(10, 4);
            entity.Property(e => e.ChemW).HasPrecision(10, 4);
            entity.Property(e => e.ChemPREN).HasPrecision(10, 4);

            // 数值精度
            entity.Property(e => e.Meters).HasPrecision(18, 3);
            entity.Property(e => e.Weight).HasPrecision(18, 3);

            // 成品检验
            entity.Property(e => e.InspPMI).HasMaxLength(100);
            entity.Property(e => e.InspVisual).HasMaxLength(100);
            entity.Property(e => e.InspDimension).HasMaxLength(100);
            entity.Property(e => e.InspEndoscopy).HasMaxLength(100);
            entity.Property(e => e.InspHydro).HasMaxLength(100);
            entity.Property(e => e.InspUnderwaterPneumatic).HasMaxLength(100);
            entity.Property(e => e.InspEddyCurrent).HasMaxLength(100);
            entity.Property(e => e.InspUltrasonic).HasMaxLength(100);
            entity.Property(e => e.InspPortDye).HasMaxLength(100);

            // 理化检测 — 拉伸
            entity.Property(e => e.TensileStrength_1).HasPrecision(10, 2);
            entity.Property(e => e.TensileStrength_2).HasPrecision(10, 2);
            entity.Property(e => e.YieldRp02_1).HasPrecision(10, 2);
            entity.Property(e => e.YieldRp02_2).HasPrecision(10, 2);
            entity.Property(e => e.YieldRp10_1).HasPrecision(10, 2);
            entity.Property(e => e.YieldRp10_2).HasPrecision(10, 2);
            entity.Property(e => e.Elongation_1).HasPrecision(10, 2);
            entity.Property(e => e.Elongation_2).HasPrecision(10, 2);

            // 理化检测 — 硬度/晶粒度
            entity.Property(e => e.Hardness_1).HasMaxLength(50);
            entity.Property(e => e.Hardness_2).HasMaxLength(50);
            entity.Property(e => e.GrainSize_1).HasMaxLength(50);
            entity.Property(e => e.GrainSize_2).HasMaxLength(50);

            // 理化检测 — 金相
            entity.Property(e => e.FerriteContent_1).HasPrecision(10, 2);
            entity.Property(e => e.FerriteContent_2).HasPrecision(10, 2);

            // 理化检测 — 扩口/压扁/晶间腐蚀/点腐蚀
            entity.Property(e => e.FlaringResult).HasMaxLength(100);
            entity.Property(e => e.FlatteningResult).HasMaxLength(100);
            entity.Property(e => e.IntergranularResult).HasMaxLength(100);
            entity.Property(e => e.PittingResult).HasMaxLength(100);
        });
    }
}
