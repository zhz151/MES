using MES.Core.DTOs;

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
