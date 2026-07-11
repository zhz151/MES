using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.ProductionStandard;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;

namespace MES.Services.Printing;

/// <summary>
/// 设备台账 PDF 打印模板（复用 TablePrintHelper）
/// </summary>
public static class EquipmentPrintHelper
{
    /// <summary>
    /// 按指定列定义生成PDF（用于前端按可见列打印）
    /// </summary>
    public static byte[] GenerateBatchPdf(List<EquipmentListDto> equipments, List<PrintColumnDef> columns)
    {
        var items = equipments.Select(m =>
        {
            var dict = new Dictionary<string, object>
            {
                ["Id"] = m.Id,
                ["EquipmentCode"] = m.EquipmentCode ?? "",
                ["EquipmentName"] = m.EquipmentName ?? "",
                ["ModelNumber"] = m.ModelNumber ?? "",
                ["TechnicalParams"] = m.TechnicalParams ?? "",
                ["Manufacturer"] = m.Manufacturer ?? "",
                ["InstallationDate"] = m.InstallationDate?.ToString("yyyy-MM-dd") ?? "",
                ["Remark"] = m.Remark ?? "",
                ["Location"] = m.Location ?? "",
                ["RelatedSection"] = m.RelatedSection ?? "",
                ["NeedInspection"] = m.NeedInspection,
                ["InspectionPerson"] = m.InspectionPerson ?? "",
                ["InspectionCycleDays"] = m.InspectionCycleDays,
                ["LastInspectionDate"] = m.LastInspectionDate?.ToString("yyyy-MM-dd") ?? "",
                ["CurrentInspectionStartDate"] = m.CurrentInspectionStartDate?.ToString("yyyy-MM-dd") ?? "",
                ["InspectionStatus"] = m.InspectionStatus ?? "",
                ["NeedMaintenance"] = m.NeedMaintenance,
                ["MaintPerson"] = m.MaintPerson ?? "",
                ["MaintCycleDays"] = m.MaintCycleDays,
                ["LastMaintDate"] = m.LastMaintDate?.ToString("yyyy-MM-dd") ?? "",
                ["CurrentMaintStartDate"] = m.CurrentMaintStartDate?.ToString("yyyy-MM-dd") ?? "",
                ["MaintStatus"] = m.MaintStatus ?? "",
                ["LastRepairDate"] = m.LastRepairDate?.ToString("yyyy-MM-dd") ?? "",
                ["LifecycleStatus"] = m.LifecycleStatus ?? "",
                ["UsageType"] = m.UsageType ?? "",
                ["RunningStatus"] = m.RunningStatus ?? "",
                ["CreatedTime"] = m.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
                ["UpdatedTime"] = m.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm")
            };
            return dict;
        }).ToList();

        return TablePrintHelper.GeneratePdf("设备台账列表", items, columns);
    }
}
