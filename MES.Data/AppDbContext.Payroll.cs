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
}
