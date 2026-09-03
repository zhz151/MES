// Auto-generated partial class for Configuration entity configurations
using Microsoft.EntityFrameworkCore;
using MES.Core.Enums;
using MES.Data.Entities.Configuration;

namespace MES.Data;

public partial class AppDbContext
{
    private static void ConfigureSectionParagraphConfig(ModelBuilder builder)
    {
        builder.Entity<SectionParagraphConfig>(entity =>
        {
            entity.ToTable("SectionParagraphConfigs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ParagraphName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ParagraphKey).HasMaxLength(50);
            entity.Property(e => e.CategoryType).HasMaxLength(20);
            entity.Property(e => e.DailyFlowTarget).HasColumnType("decimal(18,2)");
            entity.Property(e => e.LowerLimitDays).HasColumnType("decimal(18,2)");
            entity.Property(e => e.UpperLimitDays).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Remark).HasMaxLength(200);
            // 段落身份由 (CategoryType, ParagraphKey) 唯一标识（段落名是配置派生的显示名，不再唯一约束）
            entity.HasIndex(e => new { e.CategoryType, e.ParagraphKey })
                .IsUnique()
                .HasDatabaseName("UK_SPC_CategoryType_ParagraphKey");
        });
    }
    private static void ConfigureStandardWorkDay(ModelBuilder builder)
    {
        builder.Entity<StandardWorkDay>(entity =>
        {
            entity.ToTable("StandardWorkDays");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SectionName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SectionKey).HasMaxLength(50);
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
    private static void ConfigureProcessDefinition(ModelBuilder builder)
    {
        builder.Entity<ProcessDefinition>(entity =>
        {
            entity.ToTable("ProcessDefinitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProcessKey).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DisplayOrder).IsRequired();
            entity.Property(e => e.IsEnabled).IsRequired();
            entity.Property(e => e.IsColdRoll).IsRequired();
            entity.Property(e => e.IsColdDraw).IsRequired();
            entity.Property(e => e.DefaultSections).HasMaxLength(500);
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => e.ProcessKey)
                .IsUnique()
                .HasDatabaseName("UK_PD_ProcessKey");
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
    private static void ConfigureEnumDisplayDefinition(ModelBuilder builder)
    {
        builder.Entity<EnumDisplayDefinition>(entity =>
        {
            entity.ToTable("EnumDisplayDefinitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EnumKey).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DisplayOrder).IsRequired();
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => new { e.EnumKey, e.Value })
                .IsUnique()
                .HasDatabaseName("UK_EDD_EnumKey_Value");
        });
    }
    private static void ConfigureDictValueDefinition(ModelBuilder builder)
    {
        builder.Entity<DictValueDefinition>(entity =>
        {
            entity.ToTable("DictValueDefinitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DictKey).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DisplayOrder).IsRequired();
            entity.Property(e => e.IsEnabled).IsRequired();
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => new { e.DictKey, e.Value })
                .IsUnique()
                .HasDatabaseName("UK_DVD_DictKey_Value");
        });
    }
    private static void ConfigureProcessCardColumnDefinition(ModelBuilder builder)
    {
        builder.Entity<ProcessCardColumnDefinition>(entity =>
        {
            entity.ToTable("ProcessCardColumnDefinitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BlockKey).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FieldKey).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Label).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Visible).IsRequired();
            entity.Property(e => e.RowIndex).IsRequired();
            entity.Property(e => e.ColumnIndex).IsRequired();
            entity.Property(e => e.ColumnWeight).IsRequired();
            entity.HasIndex(e => new { e.BlockKey, e.FieldKey })
                .IsUnique()
                .HasDatabaseName("UK_PCCD_BlockKey_FieldKey");
        });
    }
    private static void ConfigureProcessCardStyleDefinition(ModelBuilder builder)
    {
        builder.Entity<ProcessCardStyleDefinition>(entity =>
        {
            entity.ToTable("ProcessCardStyleDefinitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => e.Key)
                .IsUnique()
                .HasDatabaseName("UK_PCSD_Key");
        });
    }
    private static void ConfigureCertificatePrintSetting(ModelBuilder builder)
    {
        builder.Entity<CertificatePrintSetting>(entity =>
        {
            entity.ToTable("CertificatePrintSettings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(500);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => e.Key)
                .IsUnique()
                .HasDatabaseName("UK_CPST_Key");
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
            entity.Property(e => e.SalaryMode).HasMaxLength(50).HasConversion(new NullableEnumStringConverter<SalaryMode>());
            entity.Property(e => e.SalaryRemark).HasMaxLength(200);
            entity.Property(e => e.AttendancePositions).HasMaxLength(200);
            entity.Property(e => e.AttendanceCoefficient).HasColumnType("decimal(18,4)");
            entity.Property(e => e.HourlyWage).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DailyWage).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MonthlyWage).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SectionName).HasMaxLength(200);
            entity.Property(e => e.InspectionItems).HasMaxLength(200);
            entity.Property(e => e.GroupName).HasMaxLength(200);
            // 过程检验/成检到料为布尔开关（是否属于该环节操作人），存 bit
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("UK_Emp_Code");
        });
    }
    private static void ConfigureCertificatePrintColumnDefinition(ModelBuilder builder)
    {
        builder.Entity<CertificatePrintColumnDefinition>(entity =>
        {
            entity.ToTable("CertificatePrintColumnDefinitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BlockKey).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FieldKey).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Label).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LabelEn).HasMaxLength(50);
            entity.Property(e => e.Visible).IsRequired();
            entity.Property(e => e.ColumnIndex).IsRequired();
            entity.Property(e => e.ColumnWeight).IsRequired();
            entity.HasIndex(e => new { e.BlockKey, e.FieldKey })
                .IsUnique()
                .HasDatabaseName("UK_CPCD_BlockKey_FieldKey");
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
            // 工段可空：成检到料/成品检验工位业务不消费工段，选填；其余类型由 Service 校验必填
            entity.Property(e => e.SectionName).HasMaxLength(50);
            entity.Property(e => e.ReportType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.InspectionItem).HasMaxLength(30);
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("UK_WS_Code");
        });
    }
}
