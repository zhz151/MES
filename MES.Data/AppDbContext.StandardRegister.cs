// Auto-generated partial class for StandardRegister entity configurations
using Microsoft.EntityFrameworkCore;
using MES.Data.Entities.StandardRegister;

namespace MES.Data;

public partial class AppDbContext
{
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
    private static void ConfigureFactoryInspectionRequirement(ModelBuilder builder)
    {
        builder.Entity<FactoryInspectionRequirement>(entity =>
        {
            entity.ToTable("FactoryInspectionRequirement");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StandardNo).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ChemicalComposition).HasMaxLength(200);
            entity.Property(e => e.PmiInspection).HasMaxLength(200);
            entity.Property(e => e.SurfaceInspection).HasMaxLength(200);
            entity.Property(e => e.Dimension).HasMaxLength(200);
            entity.Property(e => e.Endoscopy).HasMaxLength(200);
            entity.Property(e => e.HydrostaticTest).HasMaxLength(200);
            entity.Property(e => e.UnderwaterPressure).HasMaxLength(200);
            entity.Property(e => e.EddyCurrent).HasMaxLength(200);
            entity.Property(e => e.UltrasonicTest).HasMaxLength(200);
            entity.Property(e => e.PortColoring).HasMaxLength(200);
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
            entity.HasIndex(e => e.StandardNo).IsUnique().HasDatabaseName("UK_FactoryInspectionRequirement_StandardNo");
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
