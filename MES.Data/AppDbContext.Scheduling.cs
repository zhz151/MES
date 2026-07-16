// Auto-generated partial class for Scheduling entity configurations
using Microsoft.EntityFrameworkCore;
using MES.Data.Entities.Scheduling;
using MES.Core.Enums;
using MES.Data.Entities.Configuration;

namespace MES.Data;

public partial class AppDbContext
{
    private static void ConfigureRawMaterialLockPreExecution(ModelBuilder builder)
    {
        builder.Entity<RawMaterialLockPreExecution>(entity =>
        {
            entity.ToTable("RawMaterialLockPreExecution");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkOrderId).IsRequired();
            entity.Property(e => e.IsPreInput).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.BudgetInputDate).HasColumnType("date");
            entity.Property(e => e.IsMainNoMaterialComplete).IsRequired().HasDefaultValue(false);

            // 索引
            entity.HasIndex(e => e.WorkOrderId).IsUnique().HasDatabaseName("UK_RMLPE_WorkOrderId");
        });
    }
    private static void ConfigureWorkOrderPlan(ModelBuilder builder)
    {
        builder.Entity<WorkOrderPlan>(entity =>
        {
            entity.ToTable("WorkOrderPlan");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkOrderId).IsRequired();
            entity.Property(e => e.UrgencyLevel).HasMaxLength(50);
            entity.Property(e => e.ProductionAttentionProcess).HasMaxLength(100);
            entity.Property(e => e.ProductionFlowProperty).HasMaxLength(50);

            // 唯一索引
            entity.HasIndex(e => e.WorkOrderId).IsUnique().HasDatabaseName("UK_WOP_WorkOrderId");
        });
    }
    private static void ConfigureColdRollSpecSchedule(ModelBuilder builder)
    {
        builder.Entity<ColdRollSpecSchedule>(entity =>
        {
            entity.ToTable("ColdRollSpecSchedule");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProcessType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.BilletSpec).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RollingSpec).IsRequired().HasMaxLength(100);
            entity.Property(e => e.MachineNo).HasMaxLength(200);
            entity.Property(e => e.CompletionType).IsRequired().HasMaxLength(20).HasDefaultValue("None");
            entity.Property(e => e.RollType).IsRequired().HasMaxLength(20).HasDefaultValue("None");
            entity.Property(e => e.MergeDisplay).HasMaxLength(300);
            entity.Property(e => e.Remark).HasMaxLength(500);

            // 唯一索引：四维度组合不重复（ProcessType + BilletSpec + RollingSpec + IsFinished）
            entity.HasIndex(e => new { e.ProcessType, e.BilletSpec, e.RollingSpec, e.IsFinished })
                .IsUnique()
                .HasDatabaseName("UK_CRSS_Dimensions");
        });
    }
    private static void ConfigureSectionFlowCategorySetting(ModelBuilder builder)
    {
        builder.Entity<SectionFlowCategorySetting>(entity =>
        {
            entity.ToTable("SectionFlowCategorySettings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CategoryCode).IsRequired().HasMaxLength(10);
            entity.Property(e => e.CategoryName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DailyProductionTarget).HasColumnType("decimal(18,2)");
            entity.Property(e => e.LowerLimitDays).HasColumnType("decimal(18,2)");
            entity.Property(e => e.UpperLimitDays).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => e.CategoryCode)
                .IsUnique()
                .HasDatabaseName("UK_SFCS_CategoryCode");
        });
    }
    private static void ConfigureSectionFlowCategoryItem(ModelBuilder builder)
    {
        builder.Entity<SectionFlowCategoryItem>(entity =>
        {
            entity.ToTable("SectionFlowCategoryItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProcessGroupName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SectionName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Coefficient).IsRequired().HasColumnType("decimal(18,4)");
            entity.HasOne(e => e.Setting)
                .WithMany(s => s.Items)
                .HasForeignKey(e => e.SettingId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.SettingId, e.ProcessGroupName, e.SectionName })
                .IsUnique()
                .HasDatabaseName("UK_SFCI_SettingId_ProcessGroupName_SectionName");
        });
    }
    private static void ConfigureBatchPlanSchedule(ModelBuilder builder)
    {
        builder.Entity<BatchPlanSchedule>(entity =>
        {
            entity.ToTable("BatchPlanSchedules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BatchId).IsRequired();
            entity.Property(e => e.IsFlow).IsRequired();
            entity.Property(e => e.FlowLevel).IsRequired();
            entity.Property(e => e.FlowTarget).HasMaxLength(50);
            entity.Property(e => e.FlowCRType).HasMaxLength(100);
            entity.Property(e => e.FlowExecSpec).HasMaxLength(100);
            entity.Property(e => e.IsGrabOrder).IsRequired();
            entity.Property(e => e.PlanRemark).HasMaxLength(500);

            // 唯一索引
            entity.HasIndex(e => e.BatchId).IsUnique().HasDatabaseName("UK_BPS_BatchId");
        });
    }
    private static void ConfigureBatchPlanTarget(ModelBuilder builder)
    {
        builder.Entity<BatchPlanTarget>(entity =>
        {
            entity.ToTable("BatchPlanTargets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SectionName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DailyTarget).HasColumnType("decimal(18,2)");

            // 唯一索引：每个工段一条目标
            entity.HasIndex(e => e.SectionName).IsUnique().HasDatabaseName("UK_BPT_SectionName");
        });
    }
}
