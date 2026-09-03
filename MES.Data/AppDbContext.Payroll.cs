// Auto-generated partial class for Payroll entity configurations
using Microsoft.EntityFrameworkCore;
using MES.Data.Entities.Payroll;

namespace MES.Data;

public partial class AppDbContext
{
    private static void ConfigurePieceRateProductionCategory(ModelBuilder builder)
    {
        builder.Entity<PieceRateProductionCategory>(entity =>
        {
            entity.ToTable("PieceRateProductionCategories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BasePrice).HasColumnType("decimal(18,4)");
            // 工段 + 启用：匹配候选扫描索引
            entity.HasIndex(e => new { e.SectionKey, e.IsActive }).HasDatabaseName("IX_Category_Section_Active");
            // 类别 → 维档：级联删除
            entity.HasMany(e => e.Tiers)
                .WithOne(t => t.Category)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            // 类别 → 约束集合成员行：级联删除
            entity.HasMany(e => e.ConstraintKeys)
                .WithOne(k => k.Category)
                .HasForeignKey(k => k.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePieceRateProductionCategoryTier(ModelBuilder builder)
    {
        builder.Entity<PieceRateProductionCategoryTier>(entity =>
        {
            entity.ToTable("PieceRateProductionCategoryTiers");
            entity.HasKey(e => e.Id);
            // 区间维数值边界（外径/壁厚/长度/断切率）
            entity.Property(e => e.MinValue).HasColumnType("decimal(10,2)");
            entity.Property(e => e.MaxValue).HasColumnType("decimal(10,2)");
            // 系数承接 0.8697/1.0002 等多位数
            entity.Property(e => e.Ratio).HasColumnType("decimal(18,6)");
            entity.HasIndex(e => e.CategoryId).HasDatabaseName("IX_Tier_Category");
        });
    }

    private static void ConfigurePieceRateProductionCategoryKey(ModelBuilder builder)
    {
        builder.Entity<PieceRateProductionCategoryKey>(entity =>
        {
            entity.ToTable("PieceRateProductionCategoryKeys");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConstraintType).HasMaxLength(30);
            entity.Property(e => e.Key).HasMaxLength(50);
            // 同类别同约束类型键唯一：防同键重复（无成员行=该维全选，冗余重复行无意义且破坏计数语义）
            entity.HasIndex(e => new { e.CategoryId, e.ConstraintType, e.Key })
                .IsUnique()
                .HasDatabaseName("UK_CategoryKey_Type_Key");
        });
    }

    private static void ConfigurePieceRateFinalInspectionCategory(ModelBuilder builder)
    {
        builder.Entity<PieceRateFinalInspectionCategory>(entity =>
        {
            entity.ToTable("PieceRateFinalInspectionCategories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BasePrice).HasColumnType("decimal(18,4)");
            // 同一成检项目同时仅一条启用类别（过滤唯一索引；停用历史可多条并存）
            entity.HasIndex(e => e.ItemKey)
                .IsUnique()
                .HasFilter("[IsActive] = 1")
                .HasDatabaseName("UK_FinalInspectionCategory_Item_Active");
            // 类别 → 维档：级联删除
            entity.HasMany(e => e.Tiers)
                .WithOne(t => t.Category)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePieceRateFinalInspectionCategoryTier(ModelBuilder builder)
    {
        builder.Entity<PieceRateFinalInspectionCategoryTier>(entity =>
        {
            entity.ToTable("PieceRateFinalInspectionCategoryTiers");
            entity.HasKey(e => e.Id);
            // 区间维数值边界（外径/壁厚/长度）
            entity.Property(e => e.MinValue).HasColumnType("decimal(10,2)");
            entity.Property(e => e.MaxValue).HasColumnType("decimal(10,2)");
            // 系数承接 0.8697/1.0002 等多位数
            entity.Property(e => e.Ratio).HasColumnType("decimal(18,6)");
            entity.HasIndex(e => e.CategoryId).HasDatabaseName("IX_FinalInspectionTier_Category");
        });
    }

    private static void ConfigureAttendanceRecord(ModelBuilder builder)
    {
        builder.Entity<AttendanceRecord>(entity =>
        {
            entity.ToTable("AttendanceRecords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AttendDate).HasColumnType("date");
            entity.Property(e => e.WorkHours).HasColumnType("decimal(4,1)");
            entity.Property(e => e.Remark).HasMaxLength(200);
            // 员工 + 日期唯一：同一员工同一天只有一条考勤
            entity.HasIndex(e => new { e.EmployeeId, e.AttendDate })
                .IsUnique()
                .HasDatabaseName("UK_Attendance_Employee_Date");
        });
    }

    private static void ConfigurePayrollDailyWageRecord(ModelBuilder builder)
    {
        builder.Entity<PayrollDailyWageRecord>(entity =>
        {
            entity.ToTable("PayrollDailyWageRecords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WageDate).HasColumnType("date");
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SalaryMode).HasMaxLength(20);
            entity.Property(e => e.Remark).HasMaxLength(200);
            // 员工 + 日期唯一：同一员工同一天只有一条每日工资（不重复月份保存）
            entity.HasIndex(e => new { e.EmployeeId, e.WageDate })
                .IsUnique()
                .HasDatabaseName("UK_PayrollDailyWage_Employee_Date");
        });
    }

    private static void ConfigurePayrollCollectiveScore(ModelBuilder builder)
    {
        builder.Entity<PayrollCollectiveScore>(entity =>
        {
            entity.ToTable("PayrollCollectiveScores");
            entity.HasKey(e => e.Id);
            // 分值 1–10 可 1 位小数（如 8.5）：decimal(3,1)
            entity.Property(e => e.Score).HasColumnType("decimal(3,1)");
            // 员工 + 结算月唯一：同一员工同一个月只有一条评分（整月 upsert）
            entity.HasIndex(e => new { e.EmployeeId, e.Year, e.Month })
                .IsUnique()
                .HasDatabaseName("UK_PayrollCollectiveScore_Employee_Month");
        });
    }

    private static void ConfigurePayrollCollectiveWageRecord(ModelBuilder builder)
    {
        builder.Entity<PayrollCollectiveWageRecord>(entity =>
        {
            entity.ToTable("PayrollCollectiveWageRecords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Position).HasMaxLength(50);
            entity.Property(e => e.Score).HasColumnType("decimal(3,1)");
            entity.Property(e => e.AttendanceHours).HasColumnType("decimal(18,1)");
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            // 员工 + 结算月唯一：同一员工同一个月只有一条月结快照（不重复月份保存）
            entity.HasIndex(e => new { e.EmployeeId, e.WageYear, e.WageMonth })
                .IsUnique()
                .HasDatabaseName("UK_PayrollCollectiveWage_Employee_Month");
        });
    }

    private static void ConfigurePayrollAttendanceWageRecord(ModelBuilder builder)
    {
        builder.Entity<PayrollAttendanceWageRecord>(entity =>
        {
            entity.ToTable("PayrollAttendanceWageRecords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AttendancePositions).HasMaxLength(200);
            entity.Property(e => e.AttendanceHours).HasColumnType("decimal(18,1)");
            entity.Property(e => e.AttendanceCoefficient).HasColumnType("decimal(18,4)");
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            // 员工 + 结算月唯一：同一员工同一个月只有一条月结快照（不重复月份保存）
            entity.HasIndex(e => new { e.EmployeeId, e.WageYear, e.WageMonth })
                .IsUnique()
                .HasDatabaseName("UK_PayrollAttendanceWage_Employee_Month");
        });
    }

    private static void ConfigurePayrollMiscWorkRecord(ModelBuilder builder)
    {
        builder.Entity<PayrollMiscWorkRecord>(entity =>
        {
            entity.ToTable("PayrollMiscWorkRecords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkDate).HasColumnType("date");
            entity.Property(e => e.Content).HasMaxLength(500);
            entity.Property(e => e.Hours).HasColumnType("decimal(18,1)");
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Remark).HasMaxLength(200);
            // 杂辅台账：每人每天可多条 → 无唯一索引；仅建日期索引供按月区间查询
            entity.HasIndex(e => e.WorkDate).HasDatabaseName("IX_PayrollMiscWork_WorkDate");
        });
    }

    private static void ConfigurePayrollAllowanceRecord(ModelBuilder builder)
    {
        builder.Entity<PayrollAllowanceRecord>(entity =>
        {
            entity.ToTable("PayrollAllowanceRecords");
            entity.HasKey(e => e.Id);
            // 9 个金额项目列：整元手工录入（decimal(18,2) 可空，空=未填等价 0 元）
            entity.Property(e => e.FullAttendanceBonus).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SeniorityBonus).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NightShiftAllowance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PositionAllowance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.HighTempAllowance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.InjurySubsidy).HasColumnType("decimal(18,2)");
            entity.Property(e => e.LeadBonus).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Penalty).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SocialSecurity).HasColumnType("decimal(18,2)");
            // 员工 + 结算月唯一：每人每月一行（整月 upsert，不是台账多条）
            entity.HasIndex(e => new { e.EmployeeId, e.Year, e.Month })
                .IsUnique()
                .HasDatabaseName("UK_PayrollAllowance_Employee_Month");
        });
    }

    private static void ConfigurePayrollMonthlySummaryRecord(ModelBuilder builder)
    {
        builder.Entity<PayrollMonthlySummaryRecord>(entity =>
        {
            entity.ToTable("PayrollMonthlySummaryRecords");
            entity.HasKey(e => e.Id);
            // 各金额列：派生快照，decimal(18,2)；处罚/代缴社保存负（源表正数录入、扣减语义）
            entity.Property(e => e.BaseWage).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MiscWorkAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PositionAllowance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SeniorityBonus).HasColumnType("decimal(18,2)");
            entity.Property(e => e.FullAttendanceBonus).HasColumnType("decimal(18,2)");
            entity.Property(e => e.LeadBonus).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NightShiftAllowance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.HighTempAllowance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.InjurySubsidy).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Penalty).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SocialSecurity).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalPayable).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalPaid).HasColumnType("decimal(18,2)");
            // 员工 + 结算月唯一：每人每月一行（整月替换快照，不重复保存）
            entity.HasIndex(e => new { e.EmployeeId, e.Year, e.Month })
                .IsUnique()
                .HasDatabaseName("UK_PayrollMonthlySummary_Employee_Month");
        });
    }
}
