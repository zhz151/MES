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

    public static byte[] GenerateBatchPdf(List<ProductionRecordDto> records, List<PrintColumnDef> columns)
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
                new() { Key = "ProductStatus", Label = "制造状态" },
                new() { Key = "Remark", Label = "备注" },
            };
        }

        var items = records.Select(r =>
        {
            var dict = new Dictionary<string, object>
            {
                ["BatchNo"] = r.BatchNo ?? "",
                ["ProcessName"] = r.ProcessName,
                ["ManufacturingSpec"] = r.ManufacturingSpec ?? "",
                ["SectionName"] = r.SectionName,
                ["ExecDate"] = r.ExecDate.ToString("yyyy-MM-dd"),
                ["EquipmentName"] = r.EquipmentName ?? "",
                ["Operator"] = r.Operator ?? "",
                ["Shift"] = r.Shift ?? "",
                ["Quantity"] = r.Quantity?.ToString("G29") ?? "",
                ["Weight"] = r.Weight?.ToString("G29") ?? "",
                ["ProductStatus"] = r.ProductStatus ?? "在制",
                ["Remark"] = r.Remark ?? "",
                ["CreatedTime"] = r.CreatedTime.ToString("yyyy-MM-dd HH:mm"),
                ["UpdatedTime"] = r.UpdatedTime.ToString("yyyy-MM-dd HH:mm")
            };
            return dict;
        }).ToList();

        return TablePrintHelper.GeneratePdf("生产记录列表", items, columns);
    }
}
