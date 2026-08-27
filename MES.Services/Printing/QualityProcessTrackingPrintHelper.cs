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
                ["ManufacturingItem"] = dto.ManufacturingItem.HasValue ? EnumHelper.GetDisplayName(dto.ManufacturingItem.Value) : "",
                ["PlantGrade"] = dto.PlantGrade ?? "",
                ["Specification"] = dto.Specification ?? "",
                ["LengthStatus"] = dto.LengthStatus.HasValue ? EnumHelper.GetDisplayName(dto.LengthStatus.Value) : "",
                ["TagNo"] = dto.TagNo ?? "",
                ["WorkOrderNo"] = dto.WorkOrderNo ?? "",
                ["SalesOrderNo"] = dto.SalesOrderNo ?? "",
                ["ProductionMainNo"] = dto.ProductionMainNo ?? "",
                ["FurnaceNo"] = dto.FurnaceNo ?? "",
                ["SourceUnit"] = dto.SourceUnit ?? "",
                ["ProductionType"] = dto.ProductionType.HasValue ? EnumHelper.GetDisplayName(dto.ProductionType.Value) : "",
                ["Salesman"] = dto.Salesman ?? "",
                ["ManufacturingStatus"] = dto.ManufacturingStatusDisplay ?? "",
                ["DeliveryState"] = dto.DeliveryState.HasValue ? EnumHelper.GetDisplayName(dto.DeliveryState.Value) : "",
                ["InspectionType"] = dto.InspectionTypeDisplay ?? "",
                ["IsDeliveryStatus"] = dto.IsDeliveryStatusDisplay ?? "",
                ["EndCustomer"] = dto.EndCustomer ?? "",
                ["ProductionWeight"] = dto.ProductionWeight?.ToString("G29") ?? "",
                ["ProductionCutQuantity"] = dto.ProductionCutQuantity.ToString(),

                // G2: 检验来料
                ["ReceiveDate"] = dto.ReceiveDate.ToString("yyyy-MM-dd"),
                ["Shift"] = EnumHelper.GetDisplayName<ShiftType>(dto.Shift?.ToString()),
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
                ["MaxInspectionDate"] = dto.MaxInspectionDate?.ToString("yyyy-MM-dd") ?? "-",

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

                // G6: 执行状态（预成检统一显示"略"）
                ["QualityStatus"] = dto.InspectionType == MES.Core.Enums.InspectionType.PreInspection
                    ? "略"
                    : dto.IsForceCompleted ? "异常完成" : (dto.QualityStatus ?? ""),
                ["UpdatedTime"] = dto.UpdatedTime.ToString("yyyy-MM-dd HH:mm"),
            };
            return dict;
        }).ToList();

        return TablePrintHelper.GeneratePdf("成检追踪列表", dictItems, columns);
    }
}
