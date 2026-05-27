using MES.Core.DTOs;

namespace MES.Services.Printing;

/// <summary>
/// 检验到料 PDF 打印模板（复用 TablePrintHelper）
/// </summary>
public static class MaterialCheckPrintHelper
{
    /// <summary>
    /// 按指定列定义生成PDF（用于前端按可见列打印）
    /// </summary>
    public static byte[] GenerateBatchPdf(List<MaterialReceiveCheckDto> checks, List<PrintColumnDef> columns)
    {
        var items = checks.Select(m =>
        {
            var dict = new Dictionary<string, object>
            {
                ["BatchNo"] = m.BatchNo ?? "",
                ["ReceiveDate"] = m.ReceiveDate.ToString("yyyy-MM-dd"),
                ["ManufacturingItem"] = m.ManufacturingItem ?? "",
                ["PlantGrade"] = m.PlantGrade ?? "",
                ["Specification"] = m.Specification ?? "",
                ["TagNo"] = m.TagNo ?? "",
                ["WorkOrderNo"] = m.WorkOrderNo ?? "",
                ["SalesOrderNo"] = m.SalesOrderNo ?? "",
                ["FurnaceNo"] = m.FurnaceNo ?? "",
                ["SourceUnit"] = m.SourceUnit ?? "",
                ["ProductionType"] = m.ProductionType ?? "",
                ["Shift"] = m.Shift ?? "",
                ["Checker"] = m.Checker ?? "",
                ["ProductionCutQuantity"] = m.ProductionCutQuantity.ToString(),
                ["IsForceCompleted"] = m.IsForceCompleted ? "是" : "否",
                ["Remark"] = m.Remark ?? "",
                ["CreatedTime"] = m.CreatedTime.ToString("yyyy-MM-dd HH:mm"),
                ["UpdatedTime"] = m.UpdatedTime.ToString("yyyy-MM-dd HH:mm")
            };
            return dict;
        }).ToList();

        return TablePrintHelper.GeneratePdf("检验到料列表", items, columns);
    }
}
