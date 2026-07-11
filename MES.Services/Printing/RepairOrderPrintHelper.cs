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
/// 维修工单 PDF 打印模板（复用 TablePrintHelper）
/// </summary>
public static class RepairOrderPrintHelper
{
    /// <summary>
    /// 按指定列定义生成PDF（用于前端按可见列打印）
    /// </summary>
    public static byte[] GenerateBatchPdf(List<RepairOrderListDto> orders, List<PrintColumnDef> columns)
    {
        var items = orders.Select(m =>
        {
            var dict = new Dictionary<string, object>
            {
                ["Id"] = m.Id,
                ["RepairOrderNo"] = m.RepairOrderNo ?? "",
                ["EquipmentId"] = m.EquipmentId,
                ["EquipmentName"] = m.EquipmentName ?? "",
                ["EquipmentCode"] = m.EquipmentCode ?? "",
                ["EquipmentLocation"] = m.EquipmentLocation ?? "",
                ["FaultDescription"] = m.FaultDescription ?? "",
                ["FaultType"] = m.FaultType ?? "",
                ["Priority"] = m.Priority ?? "",
                ["RepairStatus"] = m.RepairStatus ?? "",
                ["ReportPerson"] = m.ReportPerson ?? "",
                ["ReportTime"] = m.ReportTime.ToString("yyyy-MM-dd HH:mm"),
                ["RepairPerson"] = m.RepairPerson ?? "",
                ["RepairStartTime"] = m.RepairStartTime?.ToString("yyyy-MM-dd HH:mm") ?? "",
                ["RepairEndTime"] = m.RepairEndTime?.ToString("yyyy-MM-dd HH:mm") ?? "",
                ["RepairContent"] = m.RepairContent ?? "",
                ["SparePartUsed"] = m.SparePartUsed ?? ""
            };
            return dict;
        }).ToList();

        return TablePrintHelper.GeneratePdf("维修工单列表", items, columns);
    }
}
