using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;

namespace MES.Services.Printing;

/// <summary>
/// 保养工单 PDF 打印模板（复用 TablePrintHelper）
/// </summary>
public static class MaintenanceOrderPrintHelper
{
    /// <summary>
    /// 按指定列定义生成PDF（用于前端按可见列打印）
    /// </summary>
    public static byte[] GenerateBatchPdf(List<MaintenanceOrderListDto> orders, List<PrintColumnDef> columns)
    {
        var items = orders.Select(m =>
        {
            var dict = new Dictionary<string, object>
            {
                ["Id"] = m.Id,
                ["MaintOrderNo"] = m.MaintOrderNo ?? "",
                ["EquipmentId"] = m.EquipmentId,
                ["EquipmentName"] = m.EquipmentName ?? "",
                ["EquipmentCode"] = m.EquipmentCode ?? "",
                ["Location"] = m.Location ?? "",
                ["ActualDate"] = m.ActualDate?.ToString("yyyy-MM-dd") ?? "",
                ["Executor"] = m.Executor ?? "",
                ["ExecutionSummary"] = m.ExecutionSummary ?? "",
                ["Remark"] = m.Remark ?? ""
            };
            return dict;
        }).ToList();

        return TablePrintHelper.GeneratePdf("保养工单列表", items, columns);
    }
}
