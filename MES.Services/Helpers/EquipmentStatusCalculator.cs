using Microsoft.EntityFrameworkCore;
using MES.Core.Enums;
using MES.Data;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.ProductionStandard;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Equipment;

namespace MES.Services.Helpers;

/// <summary>
/// 设备物化状态计算器
/// 由 EquipmentService / RepairOrderService / InspectionRecordService / MaintenanceOrderService 在写操作后调用
/// </summary>
public static class EquipmentStatusCalculator
{
    /// <summary>
    /// 重新计算并持久化指定设备的全部物化状态（RunningStatus + InspectionStatus + MaintStatus）
    /// </summary>
    public static async Task RecalculateAndSaveAsync(AppDbContext context, int equipmentId)
    {
        var equipment = await context.Equipment.FirstOrDefaultAsync(e => e.Id == equipmentId);
        if (equipment == null) return;

        var today = DateTime.Today;

        // RunningStatus：查最新 RepairOrder
        var latestRepair = await context.RepairOrders
            .AsNoTracking()
            .Where(r => r.EquipmentId == equipmentId)
            .OrderByDescending(r => r.RepairStartTime ?? r.RepairEndTime ?? r.CreatedTime.DateTime)
            .Select(r => new { r.RepairStartTime, r.RepairEndTime })
            .FirstOrDefaultAsync();

        equipment.RunningStatus = latestRepair == null
            ? nameof(RunningStatus.Normal)
            : latestRepair.RepairEndTime != null
                ? nameof(RunningStatus.Normal)
                : latestRepair.RepairStartTime != null
                    ? nameof(RunningStatus.InProgress)
                    : nameof(RunningStatus.Pending);

        // InspectionStatus：查所有 InspectionRecord（ComputeTaskStatus 需要在周期范围内检查全部记录）
        var inspectionDates = await context.InspectionRecords
            .AsNoTracking()
            .Where(r => r.EquipmentId == equipmentId && r.ActualDate != null)
            .Select(r => r.ActualDate!.Value)
            .ToListAsync();

        equipment.InspectionStatus = ComputeTaskStatus(
            equipment.NeedInspection,
            equipment.CurrentInspectionStartDate,
            equipment.InspectionCycleDays,
            inspectionDates,
            today);

        // MaintStatus：查所有 MaintenanceOrder
        var maintDates = await context.MaintenanceOrders
            .AsNoTracking()
            .Where(m => m.EquipmentId == equipmentId && m.ActualDate != null)
            .Select(m => m.ActualDate!.Value)
            .ToListAsync();

        equipment.MaintStatus = ComputeTaskStatus(
            equipment.NeedMaintenance,
            equipment.CurrentMaintStartDate,
            equipment.MaintCycleDays,
            maintDates,
            today);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// 重新计算并持久化指定设备的 InspectionStatus（仅点检相关操作后调用）
    /// </summary>
    public static async Task RecalculateInspectionStatusAsync(AppDbContext context, int equipmentId)
    {
        var equipment = await context.Equipment.FirstOrDefaultAsync(e => e.Id == equipmentId);
        if (equipment == null) return;

        var inspectionDates = await context.InspectionRecords
            .AsNoTracking()
            .Where(r => r.EquipmentId == equipmentId && r.ActualDate != null)
            .Select(r => r.ActualDate!.Value)
            .ToListAsync();

        equipment.InspectionStatus = ComputeTaskStatus(
            equipment.NeedInspection,
            equipment.CurrentInspectionStartDate,
            equipment.InspectionCycleDays,
            inspectionDates,
            DateTime.Today);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// 重新计算并持久化指定设备的 MaintStatus（仅保养相关操作后调用）
    /// </summary>
    public static async Task RecalculateMaintStatusAsync(AppDbContext context, int equipmentId)
    {
        var equipment = await context.Equipment.FirstOrDefaultAsync(e => e.Id == equipmentId);
        if (equipment == null) return;

        var maintDates = await context.MaintenanceOrders
            .AsNoTracking()
            .Where(m => m.EquipmentId == equipmentId && m.ActualDate != null)
            .Select(m => m.ActualDate!.Value)
            .ToListAsync();

        equipment.MaintStatus = ComputeTaskStatus(
            equipment.NeedMaintenance,
            equipment.CurrentMaintStartDate,
            equipment.MaintCycleDays,
            maintDates,
            DateTime.Today);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// 重新计算并持久化指定设备的 RunningStatus（仅维修相关操作后调用）
    /// </summary>
    public static async Task RecalculateRunningStatusAsync(AppDbContext context, int equipmentId)
    {
        var equipment = await context.Equipment.FirstOrDefaultAsync(e => e.Id == equipmentId);
        if (equipment == null) return;

        var latestRepair = await context.RepairOrders
            .AsNoTracking()
            .Where(r => r.EquipmentId == equipmentId)
            .OrderByDescending(r => r.RepairStartTime ?? r.RepairEndTime ?? r.CreatedTime.DateTime)
            .Select(r => new { r.RepairStartTime, r.RepairEndTime })
            .FirstOrDefaultAsync();

        equipment.RunningStatus = latestRepair == null
            ? nameof(RunningStatus.Normal)
            : latestRepair.RepairEndTime != null
                ? nameof(RunningStatus.Normal)
                : latestRepair.RepairStartTime != null
                    ? nameof(RunningStatus.InProgress)
                    : nameof(RunningStatus.Pending);

        await context.SaveChangesAsync();
    }

    private static string ComputeTaskStatus(
        bool needTask,
        DateTime? currentStartDate,
        int cycleDays,
        List<DateTime> actualDates,
        DateTime today)
    {
        if (!needTask) return nameof(EquipmentTaskStatus.NotApplicable);
        if (currentStartDate == null) return nameof(EquipmentTaskStatus.Pending);

        if (today < currentStartDate) return nameof(EquipmentTaskStatus.Normal);

        var periodEnd = currentStartDate.Value.AddDays(cycleDays - 1);
        var hasRecord = actualDates.Any(ad => ad >= currentStartDate && ad <= periodEnd);

        if (hasRecord) return nameof(EquipmentTaskStatus.Normal);
        if (today > periodEnd) return nameof(EquipmentTaskStatus.Overdue);
        return nameof(EquipmentTaskStatus.Pending);
    }
}
