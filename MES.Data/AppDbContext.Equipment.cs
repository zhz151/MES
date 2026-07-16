// Auto-generated partial class for Equipment entity configurations
using Microsoft.EntityFrameworkCore;
using MES.Data.Entities.Equipment;

namespace MES.Data;

public partial class AppDbContext
{
    private static void ConfigureEquipment(ModelBuilder builder)
    {
        builder.Entity<Equipment>(entity =>
        {
            entity.ToTable("Equipment");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EquipmentCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EquipmentName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ModelNumber).HasMaxLength(100);
            entity.Property(e => e.TechnicalParams).HasMaxLength(500);
            entity.Property(e => e.Manufacturer).HasMaxLength(200);
            entity.Property(e => e.InstallationDate).HasColumnType("date");
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.Location).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RelatedSection).HasMaxLength(100);

            // 点检参数
            entity.Property(e => e.NeedInspection).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.InspectionPerson).HasMaxLength(50);
            entity.Property(e => e.InspectionCycleDays).IsRequired().HasDefaultValue(7);
            entity.Property(e => e.LastInspectionDate).HasColumnType("date");
            entity.Property(e => e.CurrentInspectionStartDate).HasColumnType("date");

            // 保养参数
            entity.Property(e => e.NeedMaintenance).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.MaintPerson).HasMaxLength(50);
            entity.Property(e => e.MaintCycleDays).IsRequired().HasDefaultValue(30);
            entity.Property(e => e.LastMaintDate).HasColumnType("date");
            entity.Property(e => e.CurrentMaintStartDate).HasColumnType("date");
            entity.Property(e => e.LastRepairDate).HasColumnType("date");

            // 状态字段
            entity.Property(e => e.LifecycleStatus)
                .IsRequired().HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(nameof(MES.Core.Enums.LifecycleStatus.Active));
            entity.Property(e => e.UsageType)
                .IsRequired().HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(nameof(MES.Core.Enums.UsageType.Primary));

            // 物化状态字段
            entity.Property(e => e.RunningStatus)
                .IsRequired().HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(nameof(MES.Core.Enums.RunningStatus.Normal));
            entity.Property(e => e.InspectionStatus)
                .IsRequired().HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(nameof(MES.Core.Enums.EquipmentTaskStatus.NotApplicable));
            entity.Property(e => e.MaintStatus)
                .IsRequired().HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(nameof(MES.Core.Enums.EquipmentTaskStatus.NotApplicable));

            // 索引
            entity.HasIndex(e => e.EquipmentCode).IsUnique().HasDatabaseName("UK_Equipment_Code");
            entity.HasIndex(e => e.EquipmentName).HasDatabaseName("IX_Equipment_Name");
            entity.HasIndex(e => e.LifecycleStatus).HasDatabaseName("IX_Equipment_LifecycleStatus");
            entity.HasIndex(e => e.Location).HasDatabaseName("IX_Equipment_Location");
            entity.HasIndex(e => e.RelatedSection).HasDatabaseName("IX_Equipment_RelatedSection");
            entity.HasIndex(e => e.NeedInspection).HasDatabaseName("IX_Equipment_NeedInspection");
            entity.HasIndex(e => e.NeedMaintenance).HasDatabaseName("IX_Equipment_NeedMaintenance");
        });
    }
    private static void ConfigureRepairOrder(ModelBuilder builder)
    {
        builder.Entity<RepairOrder>(entity =>
        {
            entity.ToTable("RepairOrder");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RepairOrderNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EquipmentId).IsRequired();
            entity.Property(e => e.FaultDescription).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FaultType).HasMaxLength(50);
            entity.Property(e => e.Priority).IsRequired().HasMaxLength(20).HasDefaultValue(nameof(MES.Core.Enums.RepairPriority.Normal));
            entity.Property(e => e.RepairStatus)
                .IsRequired().HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(MES.Core.Enums.RepairOrderStatus.Pending);

            entity.Property(e => e.ReportPerson).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ReportTime).IsRequired().HasColumnType("datetime2");

            entity.Property(e => e.RepairPerson).HasMaxLength(50);
            entity.Property(e => e.RepairStartTime).HasColumnType("datetime2");
            entity.Property(e => e.RepairEndTime).HasColumnType("datetime2");
            entity.Property(e => e.RepairContent).HasMaxLength(1000);
            entity.Property(e => e.SparePartUsed).HasMaxLength(500);

            entity.HasOne<Equipment>()
                .WithMany()
                .HasForeignKey(e => e.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.RepairOrderNo).IsUnique().HasDatabaseName("UK_RepairOrder_No");
            entity.HasIndex(e => e.EquipmentId).HasDatabaseName("IX_RepairOrder_EquipmentId");
            entity.HasIndex(e => e.RepairStatus).HasDatabaseName("IX_RepairOrder_Status");
            entity.HasIndex(e => e.ReportTime).HasDatabaseName("IX_RepairOrder_ReportTime");
        });
    }
    private static void ConfigureMaintenanceOrder(ModelBuilder builder)
    {
        builder.Entity<MaintenanceOrder>(entity =>
        {
            entity.ToTable("MaintenanceOrder");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MaintOrderNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EquipmentId).IsRequired();
            entity.Property(e => e.ActualDate).HasColumnType("date");
            entity.Property(e => e.Executor).HasMaxLength(50);
            entity.Property(e => e.ExecutionSummary).HasMaxLength(500); entity.Property(e => e.Remark).HasMaxLength(500);

            entity.HasOne<Equipment>()
                .WithMany()
                .HasForeignKey(e => e.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.MaintOrderNo).IsUnique().HasDatabaseName("UK_MaintenanceOrder_No");
            entity.HasIndex(e => e.EquipmentId).HasDatabaseName("IX_MaintenanceOrder_EquipmentId");
        });
    }
    private static void ConfigureInspectionRecord(ModelBuilder builder)
    {
        builder.Entity<InspectionRecord>(entity =>
        {
            entity.ToTable("InspectionRecord");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RecordNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EquipmentId).IsRequired();
            entity.Property(e => e.ActualDate).HasColumnType("date");
            entity.Property(e => e.Inspector).HasMaxLength(50);
            entity.Property(e => e.ExecutionSummary).HasMaxLength(500);
            entity.Property(e => e.Remark).HasMaxLength(500);

            entity.HasOne<Equipment>()
                .WithMany()
                .HasForeignKey(e => e.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.RecordNo).IsUnique().HasDatabaseName("UK_InspectionRecord_No");
            entity.HasIndex(e => e.EquipmentId).HasDatabaseName("IX_InspectionRecord_EquipmentId");
        });
    }
}
