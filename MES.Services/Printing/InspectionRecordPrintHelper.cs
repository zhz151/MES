using MES.Core.DTOs;

namespace MES.Services.Printing;

/// <summary>
/// 点检记录 PDF 打印模板（复用 TablePrintHelper）
/// </summary>
public static class InspectionRecordPrintHelper
{
    /// <summary>
    /// 按指定列定义生成PDF（用于前端按可见列打印）
    /// </summary>
    public static byte[] GenerateBatchPdf(List<InspectionRecordListDto> records, List<PrintColumnDef> columns)
    {
        var items = records.Select(m =>
        {
            var dict = new Dictionary<string, object>
            {
                ["Id"] = m.Id,
                ["RecordNo"] = m.RecordNo ?? "",
                ["EquipmentId"] = m.EquipmentId,
                ["EquipmentName"] = m.EquipmentName ?? "",
                ["EquipmentCode"] = m.EquipmentCode ?? "",
                ["Location"] = m.Location ?? "",
                ["ActualDate"] = m.ActualDate?.ToString("yyyy-MM-dd") ?? "",
                ["Inspector"] = m.Inspector ?? "",
                ["ExecutionSummary"] = m.ExecutionSummary ?? "",
                ["Remark"] = m.Remark ?? ""
            };
            return dict;
        }).ToList();

        return TablePrintHelper.GeneratePdf("点检记录列表", items, columns);
    }
}
