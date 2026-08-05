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
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Constants;
using MES.Services.Printing;

namespace MES.Services.Printing;

/// <summary>
/// 生产记录 PDF 打印模板（复用 TablePrintHelper）
/// </summary>
public static class ProductionRecordPrintHelper
{
    public static byte[] GeneratePdf(ProductionRecordDto record)
    {
        return GenerateBatchPdf(new List<ProductionRecordDto> { record }, new List<PrintColumnDef>());
    }

    public static byte[] GenerateBatchPdf(List<ProductionRecordDto> records, List<PrintColumnDef> columns, IReadOnlyDictionary<string, string>? sectionNameMap = null)
    {
        // 如果未指定列，使用默认列
        if (columns == null || columns.Count == 0)
        {
            columns = new List<PrintColumnDef>
            {
                new() { Key = "BatchNo", Label = "批次号" },
                new() { Key = "ProcessName", Label = "工序名称" },
                new() { Key = "ManufacturingSpec", Label = "制造规格" },
                new() { Key = "SectionName", Label = "工段名称" },
                new() { Key = "ExecDate", Label = "执行日期" },
                new() { Key = "EquipmentName", Label = "设备名称" },
                new() { Key = "Operator", Label = "操作人" },
                new() { Key = "Shift", Label = "班次" },
                new() { Key = "Quantity", Label = "加工支数" },
                new() { Key = "Weight", Label = "加工重量" },
                new() { Key = "ProductStatus", Label = "产类" },
                new() { Key = "DataSource", Label = "数据来源" },
                new() { Key = "Remark", Label = "备注" },
            };
        }

        var items = records.Select(r =>
        {
            var dict = new Dictionary<string, object>
            {
                ["BatchNo"] = r.BatchNo ?? "",
                ["WorkOrderNo"] = r.WorkOrderNo ?? "",
                ["SalesOrderNo"] = r.SalesOrderNo ?? "",
                ["ProductionMainNo"] = r.ProductionMainNo ?? "",
                ["ProcessName"] = r.ProcessName,
                ["ManufacturingSpec"] = r.ManufacturingSpec ?? "",
                ["SectionName"] = SectionDisplayText(r.SectionName, sectionNameMap),
                ["SequenceNumber"] = r.SequenceNumber.ToString(),
                ["ExecDate"] = r.ExecDate.ToString("yyyy-MM-dd"),
                ["EquipmentName"] = r.EquipmentName ?? "",
                ["Operator"] = r.Operator ?? "",
                ["Shift"] = EnumHelper.GetDisplayName<ShiftType>(r.Shift?.ToString()),
                ["Quantity"] = r.Quantity?.ToString("G29") ?? "",
                ["Weight"] = r.Weight?.ToString("G29") ?? "",
                ["CuttingMultiple"] = r.CuttingMultiple?.ToString("G29") ?? "",
                ["FinishedCutLength"] = r.FinishedCutLength?.ToString("G29") ?? "",
                ["PostCutQuantity"] = r.PostCutQuantity?.ToString() ?? "",
                ["FaceCutCount"] = r.FaceCutCount?.ToString() ?? "",
                ["SolutionTemperature"] = r.SolutionTemperature?.ToString("G29") ?? "",
                ["SoakTime"] = r.SoakTime?.ToString() ?? "",
                ["TagNo"] = r.TagNo ?? "",
                ["PlantGrade"] = r.PlantGrade ?? "",
                ["DataSource"] = r.DataSource switch
                {
                    "SCAN" => "扫码",
                    "MANUAL" => "手动",
                    _ => ""
                },
                ["ProductStatus"] = r.ProductStatus ?? "在制",
                ["LengthStatus"] = EnumHelper.GetDisplayName<LengthStatus>(r.LengthStatus),
                ["Remark"] = r.Remark ?? "",
                ["CreatedTime"] = r.CreatedTime.ToString("yyyy-MM-dd HH:mm"),
                ["UpdatedTime"] = r.UpdatedTime.ToString("yyyy-MM-dd HH:mm")
            };
            return dict;
        }).ToList();

        return TablePrintHelper.GeneratePdf("生产记录列表", items, columns);
    }

    /// <summary>
    /// 工段 Key → 中文：配置表 map 优先，兜底 SectionKeys 规范中文（未知值原样返回）。
    /// </summary>
    private static string SectionDisplayText(string? keyOrName, IReadOnlyDictionary<string, string>? sectionNameMap)
    {
        if (!string.IsNullOrEmpty(keyOrName) && sectionNameMap != null && sectionNameMap.TryGetValue(keyOrName, out var cn))
            return cn;
        return SectionKeys.ToChinese(keyOrName) ?? "";
    }
}
