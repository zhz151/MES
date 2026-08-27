using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Materials;
using MES.Data.Entities.WorkOrder;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Exceptions;
using MES.Core.DTOs.Shared;
using WoEntity = MES.Data.Entities.WorkOrder.WorkOrder;

namespace MES.Services.Printing;

/// <summary>
/// 用料计划 PDF 打印模板（QuestPDF）
/// </summary>
public static class MaterialPlanPrintHelper
{
    // ==============================
    // 1. 原料采购申请单
    // ==============================
    public static byte[] GenerateSemiPlanPdf(PurchaseSemiPlan plan, WoEntity workOrder)
    {
        return CreateSemiPlanDocument(plan, workOrder).GeneratePdf();
    }

    public static Document CreateSemiPlanDocument(PurchaseSemiPlan plan, WoEntity workOrder)
    {
        return CreateBatchSemiPlanDocument(new List<(PurchaseSemiPlan, WoEntity)> { (plan, workOrder) });
    }

    // ==============================
    // 2. 成品采购申请单
    // ==============================
    public static byte[] GenerateFinishPlanPdf(PurchaseFinishedPlan plan, WoEntity workOrder)
    {
        return CreateFinishPlanDocument(plan, workOrder).GeneratePdf();
    }

    public static Document CreateFinishPlanDocument(PurchaseFinishedPlan plan, WoEntity workOrder)
    {
        return CreateBatchFinishPlanDocument(new List<(PurchaseFinishedPlan, WoEntity)> { (plan, workOrder) });
    }

    // ==============================
    // 3. 库存使用单
    // ==============================
    public static byte[] GenerateInventoryPlanPdf(InventoryPlan plan, WoEntity workOrder)
    {
        return CreateInventoryPlanDocument(plan, workOrder).GeneratePdf();
    }

    public static Document CreateInventoryPlanDocument(InventoryPlan plan, WoEntity workOrder)
    {
        return CreateBatchInventoryPlanDocument(new List<(InventoryPlan, WoEntity)> { (plan, workOrder) });
    }

    // ==============================
    // 4. 库料改制单
    // ==============================
    public static byte[] GenerateReworkPlanPdf(InventoryPlan plan, WoEntity workOrder)
    {
        return CreateReworkPlanDocument(plan, workOrder).GeneratePdf();
    }

    public static Document CreateReworkPlanDocument(InventoryPlan plan, WoEntity workOrder)
    {
        return CreateBatchReworkPlanDocument(new List<(InventoryPlan, WoEntity)> { (plan, workOrder) });
    }

    // ==============================
    // 5. 在产改制申请单
    // ==============================
    public static byte[] GenerateInProcessReworkPlanPdf(InProcessReworkPlan plan, WoEntity workOrder)
    {
        return CreateInProcessReworkPlanDocument(plan, workOrder).GeneratePdf();
    }

    public static Document CreateInProcessReworkPlanDocument(InProcessReworkPlan plan, WoEntity workOrder)
    {
        return CreateBatchInProcessReworkPlanDocument(new List<(InProcessReworkPlan, WoEntity)> { (plan, workOrder) });
    }

    // ==============================
    // 10. 批量打印 - 在产改制汇总
    // ==============================
    public static Document CreateBatchInProcessReworkPlanDocument(List<(InProcessReworkPlan plan, WoEntity workOrder)> items)
    {
        if (!items.Any()) throw new BusinessException("打印数据不能为空");
        var rows = items.Select(i =>
        {
            var (plan, workOrder) = i;
            return new Dictionary<string, object>
            {
                ["WorkOrderNo"] = workOrder.WorkOrderNo,
                ["PlanDate"] = plan.PlanDate.ToString("yyyy-MM-dd"),
                ["BatchNo"] = plan.BatchNo,
                ["BatchTagNo"] = plan.BatchTagNo ?? "-",
                ["PlantGrade"] = plan.PlantGrade,
                ["Specification"] = plan.Specification,
                ["LengthStatus"] = Enum.TryParse<LengthStatus>(plan.LengthStatus, out var ls) ? EnumHelper.GetDisplayName(ls) : (plan.LengthStatus ?? "-"),
                ["InputMultiple"] = plan.InputMultiple.ToString(),
                ["UsedQuantity"] = plan.UsedQuantity?.ToString() is string q ? $"{q} 支" : "-",
                ["UsedWeight"] = $"{plan.UsedWeight:G29} kg",
                ["Remark"] = plan.Remark ?? "-"
            };
        }).ToList();

        return TablePrintHelper.CreateDocument("在产改制计划", rows, new List<PrintColumnDef>
        {
            new() { Key = "WorkOrderNo", Label = "工单号" },
            new() { Key = "PlanDate", Label = "计划日期" },
            new() { Key = "BatchNo", Label = "生产编号" },
            new() { Key = "BatchTagNo", Label = "挂牌号" },
            new() { Key = "PlantGrade", Label = "工厂牌号" },
            new() { Key = "Specification", Label = "规格" },
            new() { Key = "LengthStatus", Label = "长度状态" },
            new() { Key = "InputMultiple", Label = "投料制成倍" },
            new() { Key = "UsedQuantity", Label = "使用支数" },
            new() { Key = "UsedWeight", Label = "使用重量(kg)" },
            new() { Key = "Remark", Label = "备注" }
        });
    }

    // ==============================
    // 7. 在产主工单计划
    // ==============================
    public static byte[] GenerateInMainWorkOrderPlanPdf(InMainWorkOrderPlan plan, WoEntity workOrder)
    {
        return CreateInMainWorkOrderPlanDocument(plan, workOrder).GeneratePdf();
    }

    public static Document CreateInMainWorkOrderPlanDocument(InMainWorkOrderPlan plan, WoEntity workOrder)
    {
        return CreateBatchInMainWorkOrderPlanDocument(new List<(InMainWorkOrderPlan, WoEntity)> { (plan, workOrder) });
    }

    // ==============================
    // 11. 批量打印 - 在产主工单汇总
    // ==============================
    public static Document CreateBatchInMainWorkOrderPlanDocument(List<(InMainWorkOrderPlan plan, WoEntity workOrder)> items)
    {
        if (!items.Any()) throw new BusinessException("打印数据不能为空");
        var rows = items.Select(i =>
        {
            var (plan, workOrder) = i;
            return new Dictionary<string, object>
            {
                ["WorkOrderNo"] = workOrder.WorkOrderNo,
                ["PlanDate"] = plan.PlanDate.ToString("yyyy-MM-dd"),
                ["BatchNo"] = plan.BatchNo,
                ["MainWorkOrderNo"] = plan.MainWorkOrderNo,
                ["AllocatedQuantity"] = plan.AllocatedQuantity?.ToString() is string q ? $"{q} 支" : "-",
                ["AllocatedWeight"] = $"{plan.AllocatedWeight:G29} kg",
                ["RequiredDate"] = plan.RequiredDate?.ToString("yyyy-MM-dd") ?? "-",
                ["PlanStatus"] = EnumHelper.GetDisplayName(plan.PlanStatus),
                ["Remark"] = plan.Remark ?? "-"
            };
        }).ToList();

        return TablePrintHelper.CreateDocument("在产主工单计划", rows, new List<PrintColumnDef>
        {
            new() { Key = "WorkOrderNo", Label = "工单号" },
            new() { Key = "PlanDate", Label = "计划日期" },
            new() { Key = "BatchNo", Label = "生产编号" },
            new() { Key = "MainWorkOrderNo", Label = "源主工单号" },
            new() { Key = "AllocatedQuantity", Label = "分配支数" },
            new() { Key = "AllocatedWeight", Label = "分配重量(kg)" },
            new() { Key = "RequiredDate", Label = "要求到位日" },
            new() { Key = "PlanStatus", Label = "状态" },
            new() { Key = "Remark", Label = "备注" }
        });
    }

    // ==============================
    // 5. 批量打印 - 原料采购汇总
    // ==============================
    public static Document CreateBatchSemiPlanDocument(List<(PurchaseSemiPlan plan, WoEntity workOrder)> items)
    {
        if (!items.Any()) throw new BusinessException("打印数据不能为空");
        var rows = items.Select(i =>
        {
            var (plan, workOrder) = i;
            return new Dictionary<string, object>
            {
                ["WorkOrderNo"] = workOrder.WorkOrderNo,
                ["PlanDate"] = plan.PlanDate.ToString("yyyy-MM-dd"),
                ["RawMaterialType"] = EnumHelper.GetDisplayName(plan.RawMaterialType),
                ["PlantGrade"] = plan.PlantGrade,
                ["RawMaterialSpec"] = plan.RawMaterialSpec,
                ["RequiredUnitWeight"] = plan.RequiredUnitWeight?.ToString("G29") is string uw ? $"{uw} kg/支" : "-",
                ["RequiredPieces"] = plan.RequiredPieces?.ToString() is string rp ? $"{rp} 支" : "-",
                ["RequiredWeight"] = $"{plan.RequiredWeight:G29} kg",
                ["InputMultiple"] = $"{plan.InputMultiple}",
                ["RequiredDate"] = plan.RequiredDate.ToString("yyyy-MM-dd"),
                ["Remark"] = plan.Remark ?? "-"
            };
        }).ToList();

        return TablePrintHelper.CreateDocument("原料采购计划", rows, new List<PrintColumnDef>
        {
            new() { Key = "WorkOrderNo", Label = "工单号" },
            new() { Key = "PlanDate", Label = "计划日期" },
            new() { Key = "RawMaterialType", Label = "原料类型" },
            new() { Key = "PlantGrade", Label = "工厂牌号" },
            new() { Key = "RawMaterialSpec", Label = "原料规格" },
            new() { Key = "RequiredUnitWeight", Label = "需求单重" },
            new() { Key = "RequiredPieces", Label = "需求支数" },
            new() { Key = "RequiredWeight", Label = "需求重量" },
            new() { Key = "InputMultiple", Label = "投料制成倍" },
            new() { Key = "RequiredDate", Label = "要求到货日" },
            new() { Key = "Remark", Label = "备注" }
        });
    }

    // ==============================
    // 6. 批量打印 - 成品采购汇总
    // ==============================
    public static Document CreateBatchFinishPlanDocument(List<(PurchaseFinishedPlan plan, WoEntity workOrder)> items)
    {
        if (!items.Any()) throw new BusinessException("打印数据不能为空");
        var rows = items.Select(i =>
        {
            var (plan, workOrder) = i;
            var lengthStatusText = EnumHelper.GetDisplayName(plan.LengthStatus);
            var lengthStr = (plan.MinLength, plan.MaxLength) switch
            {
                (null, null) => lengthStatusText,
                (null, var max) => $"{lengthStatusText} ≤{max:G29}",
                (var min, null) => $"{lengthStatusText} ≥{min:G29}",
                (var min, var max) => $"{lengthStatusText} {min:G29}~{max:G29}"
            };
            return new Dictionary<string, object>
            {
                ["WorkOrderNo"] = workOrder.WorkOrderNo,
                ["PlanDate"] = plan.PlanDate.ToString("yyyy-MM-dd"),
                ["ProductType"] = EnumHelper.GetDisplayName(plan.ProductType),
                ["PlantGrade"] = plan.PlantGrade,
                ["Specification"] = plan.Specification,
                ["OdTol"] = $"-{plan.OuterDiameterNegative:G29}/+{plan.OuterDiameterPositive:G29}",
                ["WtTol"] = $"-{plan.WallThicknessNegative:G29}/+{plan.WallThicknessPositive:G29}",
                ["LengthStatus"] = lengthStatusText,
                ["LengthStr"] = lengthStr,
                ["DeliveryState"] = EnumHelper.GetDisplayName(plan.DeliveryState),
                ["RequiredPiece"] = plan.RequiredPiece?.ToString() is string rp ? $"{rp} 支" : "-",
                ["RequiredWeight"] = $"{plan.RequiredWeight:G29} kg",
                ["InputMultiple"] = plan.InputMultiple?.ToString() ?? "-",
                ["RequiredDate"] = plan.RequiredDate?.ToString("yyyy-MM-dd") ?? "-",
                ["Remark"] = plan.Remark ?? "-"
            };
        }).ToList();

        return TablePrintHelper.CreateDocument("成品采购计划", rows, new List<PrintColumnDef>
        {
            new() { Key = "WorkOrderNo", Label = "工单号" },
            new() { Key = "PlanDate", Label = "计划日期" },
            new() { Key = "ProductType", Label = "成品类型" },
            new() { Key = "PlantGrade", Label = "工厂牌号" },
            new() { Key = "Specification", Label = "规格" },
            new() { Key = "OdTol", Label = "外径公差" },
            new() { Key = "WtTol", Label = "壁厚公差" },
            new() { Key = "LengthStatus", Label = "长度状态" },
            new() { Key = "LengthStr", Label = "长度(mm)" },
            new() { Key = "DeliveryState", Label = "交货状态" },
            new() { Key = "RequiredPiece", Label = "需用支数" },
            new() { Key = "RequiredWeight", Label = "需用重量" },
            new() { Key = "InputMultiple", Label = "投料制成倍" },
            new() { Key = "RequiredDate", Label = "要求到货日" },
            new() { Key = "Remark", Label = "备注" }
        });
    }

    // ==============================
    // 7. 批量打印 - 库存使用汇总
    // ==============================
    public static Document CreateBatchInventoryPlanDocument(List<(InventoryPlan plan, WoEntity workOrder)> items)
    {
        if (!items.Any()) throw new BusinessException("打印数据不能为空");
        var rows = items.Select(i =>
        {
            var (plan, workOrder) = i;
            var usageMode = plan.UsageMode == "All" ? "全部" : "部分";
            var qtyText = plan.UsageMode == "All"
                ? $"全部({plan.UsedQuantity?.ToString() ?? "0"} 支)"
                : $"{plan.UsedQuantity?.ToString() ?? "-"} 支";
            var location = string.IsNullOrEmpty(plan.LocationArea) && string.IsNullOrEmpty(plan.LocationRack)
                ? "-"
                : string.IsNullOrEmpty(plan.LocationArea) ? plan.LocationRack
                : string.IsNullOrEmpty(plan.LocationRack) ? plan.LocationArea
                : $"{plan.LocationArea}/{plan.LocationRack}";
            return new Dictionary<string, object>
            {
                ["WorkOrderNo"] = workOrder.WorkOrderNo,
                ["PlanDate"] = plan.PlanDate.ToString("yyyy-MM-dd"),
                ["BatchNo"] = plan.BatchNo,
                ["MaterialType"] = EnumHelper.GetDisplayName<MaterialType>(plan.MaterialType) ?? plan.MaterialType,
                ["PlantGrade"] = plan.PlantGrade,
                ["Specification"] = plan.Specification,
                ["UsageMode"] = usageMode,
                ["UsedQuantity"] = qtyText,
                ["UsedWeight"] = $"{plan.UsedWeight:G29} kg",
                ["Location"] = location ?? "-",
                ["Remark"] = plan.Remark ?? "-"
            };
        }).ToList();

        return TablePrintHelper.CreateDocument("库存使用计划", rows, new List<PrintColumnDef>
        {
            new() { Key = "WorkOrderNo", Label = "工单号" },
            new() { Key = "PlanDate", Label = "计划日期" },
            new() { Key = "BatchNo", Label = "批次号" },
            new() { Key = "MaterialType", Label = "物料名称" },
            new() { Key = "PlantGrade", Label = "工厂牌号" },
            new() { Key = "Specification", Label = "规格" },
            new() { Key = "UsageMode", Label = "使用模式" },
            new() { Key = "UsedQuantity", Label = "出库支数" },
            new() { Key = "UsedWeight", Label = "出库重量(kg)" },
            new() { Key = "Location", Label = "放置框架" },
            new() { Key = "Remark", Label = "备注" }
        });
    }

    // ==============================
    // 5. 圆棒穿孔申请单
    // ==============================
    public static byte[] GeneratePiercingPlanPdf(RoundBarPiercingPlan plan, WoEntity workOrder)
    {
        return CreatePiercingPlanDocument(plan, workOrder).GeneratePdf();
    }

    public static Document CreatePiercingPlanDocument(RoundBarPiercingPlan plan, WoEntity workOrder)
    {
        return CreateBatchPiercingPlanDocument(new List<(RoundBarPiercingPlan, WoEntity)> { (plan, workOrder) });
    }

    // ==============================
    // 9. 批量打印 - 圆棒穿孔汇总
    // ==============================
    public static Document CreateBatchPiercingPlanDocument(List<(RoundBarPiercingPlan plan, WoEntity workOrder)> items)
    {
        if (!items.Any()) throw new BusinessException("打印数据不能为空");
        var rows = items.Select(i =>
        {
            var (plan, workOrder) = i;
            return new Dictionary<string, object>
            {
                ["WorkOrderNo"] = workOrder.WorkOrderNo,
                ["PlanDate"] = plan.PlanDate.ToString("yyyy-MM-dd"),
                ["RawMaterialType"] = EnumHelper.GetDisplayName(plan.RawMaterialType),
                ["PlantGrade"] = plan.PlantGrade,
                ["RoundBarSpec"] = plan.RoundBarSpec,
                ["PiercingSpec"] = plan.PiercingSpec,
                ["RequiredUnitWeight"] = plan.RequiredUnitWeight?.ToString("G29") is string uw ? $"{uw} kg/支" : "-",
                ["RequiredPieces"] = plan.RequiredPieces?.ToString() is string rp ? $"{rp} 支" : "-",
                ["RequiredWeight"] = $"{plan.RequiredWeight:G29} kg",
                ["InputMultiple"] = $"{plan.InputMultiple}",
                ["RequiredDate"] = plan.RequiredDate.ToString("yyyy-MM-dd"),
                ["Remark"] = plan.Remark ?? "-"
            };
        }).ToList();

        return TablePrintHelper.CreateDocument("圆棒穿孔计划", rows, new List<PrintColumnDef>
        {
            new() { Key = "WorkOrderNo", Label = "工单号" },
            new() { Key = "PlanDate", Label = "计划日期" },
            new() { Key = "RawMaterialType", Label = "原料类型" },
            new() { Key = "PlantGrade", Label = "工厂牌号" },
            new() { Key = "RoundBarSpec", Label = "圆棒规格" },
            new() { Key = "PiercingSpec", Label = "穿孔规格" },
            new() { Key = "RequiredUnitWeight", Label = "需求单重" },
            new() { Key = "RequiredPieces", Label = "需求支数" },
            new() { Key = "RequiredWeight", Label = "需求重量" },
            new() { Key = "InputMultiple", Label = "投料制成倍" },
            new() { Key = "RequiredDate", Label = "要求到货日" },
            new() { Key = "Remark", Label = "备注" }
        });
    }

    // ==============================
    // 8. 批量打印 - 库料改制汇总
    // ==============================
    public static Document CreateBatchReworkPlanDocument(List<(InventoryPlan plan, WoEntity workOrder)> items)
    {
        if (!items.Any()) throw new BusinessException("打印数据不能为空");
        var rows = items.Select(i =>
        {
            var (plan, workOrder) = i;
            var usageMode = plan.UsageMode == "All" ? "全部" : "部分";
            var qtyText = plan.UsageMode == "All"
                ? $"全部({plan.UsedQuantity?.ToString() ?? "0"} 支)"
                : $"{plan.UsedQuantity?.ToString() ?? "-"} 支";
            var location = string.IsNullOrEmpty(plan.LocationArea) && string.IsNullOrEmpty(plan.LocationRack)
                ? "-"
                : string.IsNullOrEmpty(plan.LocationArea) ? plan.LocationRack
                : string.IsNullOrEmpty(plan.LocationRack) ? plan.LocationArea
                : $"{plan.LocationArea}/{plan.LocationRack}";
            return new Dictionary<string, object>
            {
                ["WorkOrderNo"] = workOrder.WorkOrderNo,
                ["PlanDate"] = plan.PlanDate.ToString("yyyy-MM-dd"),
                ["BatchNo"] = plan.BatchNo,
                ["MaterialType"] = EnumHelper.GetDisplayName<MaterialType>(plan.MaterialType) ?? plan.MaterialType,
                ["PlantGrade"] = plan.PlantGrade,
                ["Specification"] = plan.Specification,
                ["UsageMode"] = usageMode,
                ["UsedQuantity"] = qtyText,
                ["UsedWeight"] = $"{plan.UsedWeight:G29} kg",
                ["Location"] = location ?? "-",
                ["ReworkType"] = plan.ReworkType.HasValue ? EnumHelper.GetDisplayName(plan.ReworkType.Value) : "-",
                ["Remark"] = plan.Remark ?? "-"
            };
        }).ToList();

        return TablePrintHelper.CreateDocument("库料改制计划", rows, new List<PrintColumnDef>
        {
            new() { Key = "WorkOrderNo", Label = "工单号" },
            new() { Key = "PlanDate", Label = "计划日期" },
            new() { Key = "BatchNo", Label = "批次号" },
            new() { Key = "MaterialType", Label = "物料名称" },
            new() { Key = "PlantGrade", Label = "工厂牌号" },
            new() { Key = "Specification", Label = "规格" },
            new() { Key = "UsageMode", Label = "使用模式" },
            new() { Key = "UsedQuantity", Label = "出库支数" },
            new() { Key = "UsedWeight", Label = "出库重量(kg)" },
            new() { Key = "Location", Label = "放置框架" },
            new() { Key = "ReworkType", Label = "改制类型" },
            new() { Key = "Remark", Label = "备注" }
        });
    }

}
