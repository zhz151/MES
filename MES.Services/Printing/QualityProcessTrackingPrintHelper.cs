using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Services.Printing;

/// <summary>
/// 成检追踪 PDF 打印模板（复用 TablePrintHelper）
/// </summary>
public static class QualityProcessTrackingPrintHelper
{
    public static byte[] GenerateBatchPdf(List<QualityProcessTrackingDto> items, List<PrintColumnDef> columns)
    {
        var dictItems = items.Select(dto =>
        {
            var dict = new Dictionary<string, object>
            {
                // G1: 批次信息
                ["BatchNo"] = dto.BatchNo ?? "",
                ["ManufacturingItem"] = !string.IsNullOrEmpty(dto.ManufacturingItem) && Enum.TryParse<ManufacturingItem>(dto.ManufacturingItem, out var mi) ? EnumHelper.GetDisplayName(mi) : (dto.ManufacturingItem ?? ""),
                ["PlantGrade"] = dto.PlantGrade ?? "",
                ["Specification"] = dto.Specification ?? "",
                ["LengthStatus"] = !string.IsNullOrEmpty(dto.LengthStatus) && Enum.TryParse<LengthStatus>(dto.LengthStatus, out var ls) ? EnumHelper.GetDisplayName(ls) : (dto.LengthStatus ?? ""),
                ["TagNo"] = dto.TagNo ?? "",
                ["WorkOrderNo"] = dto.WorkOrderNo ?? "",
                ["SalesOrderNo"] = dto.SalesOrderNo ?? "",
                ["FurnaceNo"] = dto.FurnaceNo ?? "",
                ["SourceUnit"] = dto.SourceUnit ?? "",
                ["ProductionType"] = !string.IsNullOrEmpty(dto.ProductionType) && Enum.TryParse<ProductionType>(dto.ProductionType, out var pt) ? EnumHelper.GetDisplayName(pt) : (dto.ProductionType ?? ""),
                ["Salesman"] = dto.Salesman ?? "",
                ["DeliveryState"] = !string.IsNullOrEmpty(dto.DeliveryState) && Enum.TryParse<DeliveryState>(dto.DeliveryState, out var ds) ? EnumHelper.GetDisplayName(ds) : (dto.DeliveryState ?? ""),
                ["ProductionWeight"] = dto.ProductionWeight?.ToString("G29") ?? "",
                ["ProductionCutQuantity"] = dto.ProductionCutQuantity.ToString(),

                // G2: 检验来料
                ["ReceiveDate"] = dto.ReceiveDate.ToString("yyyy-MM-dd"),
                ["Shift"] = dto.Shift ?? "",
                ["Checker"] = dto.Checker ?? "",
                ["IsForceCompleted"] = dto.IsForceCompleted ? "是" : "否",

                // G3: 各项检验的日期
                ["InspectionCount"] = dto.InspectionCount.ToString(),
                ["PmiDate"] = dto.PmiDate?.ToString("yyyy-MM-dd") ?? "",
                ["VisualDate"] = dto.VisualDate?.ToString("yyyy-MM-dd") ?? "",
                ["DimensionDate"] = dto.DimensionDate?.ToString("yyyy-MM-dd") ?? "",
                ["EndoscopyDate"] = dto.EndoscopyDate?.ToString("yyyy-MM-dd") ?? "",
                ["HydroDate"] = dto.HydroDate?.ToString("yyyy-MM-dd") ?? "",
                ["UnderwaterPneumaticDate"] = dto.UnderwaterPneumaticDate?.ToString("yyyy-MM-dd") ?? "",
                ["EddyCurrentDate"] = dto.EddyCurrentDate?.ToString("yyyy-MM-dd") ?? "",
                ["UltrasonicDate"] = dto.UltrasonicDate?.ToString("yyyy-MM-dd") ?? "",
                ["PortColoringDate"] = dto.PortColoringDate?.ToString("yyyy-MM-dd") ?? "",

                // G4: 检验的数量信息
                ["TotalQuantity"] = dto.TotalQuantity.ToString(),
                ["QualifiedQuantity"] = dto.QualifiedQuantity.ToString(),
                ["DefectReworkQuantity"] = dto.DefectReworkQuantity.ToString(),
                ["DefectWarehouseQuantity"] = dto.DefectWarehouseQuantity.ToString(),
                ["DefectScrapQuantity"] = dto.DefectScrapQuantity.ToString(),

                // G5: 入库的信息
                ["InboundDate"] = dto.InboundDate?.ToString("yyyy-MM-dd") ?? "",
                ["InboundQuantity"] = dto.InboundQuantity.ToString(),
                ["InboundWeight"] = dto.InboundWeight?.ToString("G29") ?? "",

                // G6: 执行状态
                ["QualityStatus"] = dto.IsForceCompleted ? "异常完成" : (dto.QualityStatus ?? ""),
            };
            return dict;
        }).ToList();

        return TablePrintHelper.GeneratePdf("成检追踪列表", dictItems, columns);
    }
}
