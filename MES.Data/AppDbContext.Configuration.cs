// Auto-generated partial class for Configuration entity configurations
using Microsoft.EntityFrameworkCore;
using MES.Data.Entities.Configuration;

namespace MES.Data;

public partial class AppDbContext
{
    private static void ConfigureStandardWorkDay(ModelBuilder builder)
    {
        builder.Entity<StandardWorkDay>(entity =>
        {
            entity.ToTable("StandardWorkDays");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SectionName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SectionKey).HasMaxLength(50);
            entity.Property(e => e.EnglishName).HasMaxLength(100);
            entity.Property(e => e.DisplayOrder).IsRequired();
            entity.Property(e => e.IsEnabled).IsRequired();
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
}
