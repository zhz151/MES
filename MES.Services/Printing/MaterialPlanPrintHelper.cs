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
        if (!items.Any()) throw new BusinessException("items cannot be empty");
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("SimSun"));
                page.Header().Element(h => ComposeDocHeader(h, "在 产 改 制 计 划（批量）"));
                page.Content().Element(c => ComposeBatchInProcessReworkContent(c, items));
                page.Footer().Element(ComposeDocFooter);
            });
        });
    }

    private static void ComposeBatchInProcessReworkContent(IContainer container, List<(InProcessReworkPlan plan, WoEntity workOrder)> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(75);
                columns.ConstantColumn(55);
                columns.ConstantColumn(65);
                columns.ConstantColumn(45);
                columns.ConstantColumn(55);
                columns.ConstantColumn(55);
                columns.ConstantColumn(45);
                columns.ConstantColumn(45);
                columns.ConstantColumn(55);
                columns.ConstantColumn(55);
                columns.RelativeColumn();
            });

            table.Header(header =>
            {
                string[] headers = { "工单号", "计划日期", "生产编号", "挂牌号", "工厂牌号", "规格", "长度状态", "投料制成倍", "使用支数", "使用重量(kg)", "改制类型" };
                foreach (var h in headers)
                    header.Cell().Element(CellHeaderStyle).Text(h).FontSize(8).AlignCenter();
            });

            foreach (var (plan, workOrder) in items)
            {
                var reworkTypeText = EnumHelper.GetDisplayName(plan.ReworkType);

                table.Cell().Element(CellStyle).Text(workOrder.WorkOrderNo).FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.PlanDate.ToString("yyyy-MM-dd")).FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.BatchNo).FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.BatchTagNo ?? "-").FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.PlantGrade).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.Specification).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.LengthStatus).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.InputMultiple.ToString()).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.UsedQuantity?.ToString() is string q ? $"{q} 支" : "-").FontSize(8);
                table.Cell().Element(CellStyle).Text($"{plan.UsedWeight:G29} kg").FontSize(8);
                table.Cell().Element(CellStyle).Text(reworkTypeText).FontSize(8).AlignCenter();
            }
        });
    }

    // ========== 公共组件 ==========

    private static void ComposeDocHeader(IContainer container, string title)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(5).AlignCenter().Text(title)
                .FontSize(18).Bold();

            col.Item().PaddingVertical(4)
                .LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    private static void ComposeDocFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingVertical(4)
                .LineHorizontal(1).LineColor(Colors.Black);

            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Text("制单人：").FontSize(10);
                row.RelativeItem().Text("审核人：").FontSize(10);
                row.RelativeItem().Text($"打印日期：{DateTime.Now:yyyy-MM-dd}").FontSize(10);
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.Span("第 ").FontSize(9);
                    t.CurrentPageNumber().FontSize(9);
                    t.Span(" 页 / 共 ").FontSize(9);
                    t.TotalPages().FontSize(9);
                    t.Span(" 页").FontSize(9);
                });
            });
        });
    }

    // ==============================
    // 5. 批量打印 - 原料采购汇总
    // ==============================
    public static Document CreateBatchSemiPlanDocument(List<(PurchaseSemiPlan plan, WoEntity workOrder)> items)
    {
        if (!items.Any()) throw new BusinessException("items cannot be empty");
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("SimSun"));
                page.Header().Element(h => ComposeDocHeader(h, "原 料 采 购 计 划（批量）"));
                page.Content().Element(c => ComposeBatchSemiContent(c, items));
                page.Footer().Element(ComposeDocFooter);
            });
        });
    }

    private static void ComposeBatchSemiContent(IContainer container, List<(PurchaseSemiPlan plan, WoEntity workOrder)> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(75);
                columns.ConstantColumn(55);
                columns.ConstantColumn(40);
                columns.ConstantColumn(60);
                columns.ConstantColumn(65);
                columns.ConstantColumn(45);
                columns.ConstantColumn(40);
                columns.ConstantColumn(50);
                columns.ConstantColumn(45);
                columns.ConstantColumn(55);
                columns.ConstantColumn(50);
            });

            table.Header(header =>
            {
                header.Cell().Element(CellHeaderStyle).Text("工单号").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("计划日期").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("原料类型").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("工厂牌号").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("原料规格").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("需求单重").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("需求支数").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("需求重量").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("投料制成倍").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("要求到货日").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("备注").FontSize(8).AlignCenter();
            });

            foreach (var (plan, workOrder) in items)
            {
                var rawMatType = EnumHelper.GetDisplayName(plan.RawMaterialType);

                table.Cell().Element(CellStyle).Text(workOrder.WorkOrderNo).FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.PlanDate.ToString("yyyy-MM-dd")).FontSize(8);
                table.Cell().Element(CellStyle).Text(rawMatType).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.PlantGrade).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.RawMaterialSpec).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.RequiredUnitWeight?.ToString("G29") is string uw ? $"{uw} kg/支" : "-").FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.RequiredPieces?.ToString() is string rp ? $"{rp} 支" : "-").FontSize(8);
                table.Cell().Element(CellStyle).Text($"{plan.RequiredWeight:G29} kg").FontSize(8);
                table.Cell().Element(CellStyle).Text($"{plan.InputMultiple}").FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.RequiredDate.ToString("yyyy-MM-dd")).FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.Remark ?? "-").FontSize(8);
            }
        });
    }

    // ==============================
    // 6. 批量打印 - 成品采购汇总
    // ==============================
    public static Document CreateBatchFinishPlanDocument(List<(PurchaseFinishedPlan plan, WoEntity workOrder)> items)
    {
        if (!items.Any()) throw new BusinessException("items cannot be empty");
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("SimSun"));
                page.Header().Element(h => ComposeDocHeader(h, "成 品 采 购 计 划（批量）"));
                page.Content().Element(c => ComposeBatchFinishContent(c, items));
                page.Footer().Element(ComposeDocFooter);
            });
        });
    }

    private static void ComposeBatchFinishContent(IContainer container, List<(PurchaseFinishedPlan plan, WoEntity workOrder)> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(75);
                columns.ConstantColumn(55);
                columns.ConstantColumn(45);
                columns.ConstantColumn(55);
                columns.ConstantColumn(55);
                columns.ConstantColumn(50);
                columns.ConstantColumn(50);
                columns.ConstantColumn(40);
                columns.ConstantColumn(50);
                columns.ConstantColumn(45);
                columns.ConstantColumn(40);
                columns.ConstantColumn(50);
                columns.ConstantColumn(45);
                columns.ConstantColumn(50);
                columns.RelativeColumn();
            });

            table.Header(header =>
            {
                string[] headers = { "工单号", "计划日期", "成品类型", "工厂牌号", "规格", "外径公差", "壁厚公差", "长度状态", "长度(mm)", "交货状态", "需用支数", "需用重量", "投料制成倍", "要求到货日", "备注" };
                foreach (var h in headers)
                    header.Cell().Element(CellHeaderStyle).Text(h).FontSize(8).AlignCenter();
            });

            foreach (var (plan, workOrder) in items)
            {
                var productType = EnumHelper.GetDisplayName(plan.ProductType);
                var odTol = $"-{plan.OuterDiameterNegative:G29}/+{plan.OuterDiameterPositive:G29}";
                var wtTol = $"-{plan.WallThicknessNegative:G29}/+{plan.WallThicknessPositive:G29}";

                var lengthStatusText = EnumHelper.GetDisplayName(plan.LengthStatus);
                var lengthStr = (plan.MinLength, plan.MaxLength) switch
                {
                    (null, null) => lengthStatusText,
                    (null, var max) => $"{lengthStatusText} ≤{max:G29}",
                    (var min, null) => $"{lengthStatusText} ≥{min:G29}",
                    (var min, var max) => $"{lengthStatusText} {min:G29}~{max:G29}"
                };

                var deliveryStateText = EnumHelper.GetDisplayName(plan.DeliveryState);

                table.Cell().Element(CellStyle).Text(workOrder.WorkOrderNo).FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.PlanDate.ToString("yyyy-MM-dd")).FontSize(8);
                table.Cell().Element(CellStyle).Text(productType).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.PlantGrade).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.Specification).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(odTol).FontSize(8);
                table.Cell().Element(CellStyle).Text(wtTol).FontSize(8);
                table.Cell().Element(CellStyle).Text(lengthStatusText).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(lengthStr).FontSize(8);
                table.Cell().Element(CellStyle).Text(deliveryStateText).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.RequiredPiece?.ToString() is string rp ? $"{rp} 支" : "-").FontSize(8);
                table.Cell().Element(CellStyle).Text($"{plan.RequiredWeight:G29} kg").FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.InputMultiple?.ToString() ?? "-").FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.RequiredDate?.ToString("yyyy-MM-dd") ?? "-").FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.Remark ?? "-").FontSize(8);
            }
        });
    }

    // ==============================
    // 7. 批量打印 - 库存使用汇总
    // ==============================
    public static Document CreateBatchInventoryPlanDocument(List<(InventoryPlan plan, WoEntity workOrder)> items)
    {
        if (!items.Any()) throw new BusinessException("items cannot be empty");
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("SimSun"));
                page.Header().Element(h => ComposeDocHeader(h, "库 存 使 用 计 划（批量）"));
                page.Content().Element(c => ComposeBatchInventoryContent(c, items));
                page.Footer().Element(ComposeDocFooter);
            });
        });
    }

    private static void ComposeBatchInventoryContent(IContainer container, List<(InventoryPlan plan, WoEntity workOrder)> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(75);
                columns.ConstantColumn(55);
                columns.ConstantColumn(70);
                columns.ConstantColumn(55);
                columns.ConstantColumn(55);
                columns.ConstantColumn(60);
                columns.ConstantColumn(45);
                columns.ConstantColumn(55);
                columns.ConstantColumn(55);
                columns.RelativeColumn();
            });

            table.Header(header =>
            {
                string[] headers = { "工单号", "计划日期", "批次号", "物料名称", "工厂牌号", "规格", "使用模式", "出库支数", "出库重量(kg)", "放置框架" };
                foreach (var h in headers)
                    header.Cell().Element(CellHeaderStyle).Text(h).FontSize(8).AlignCenter();
            });

            foreach (var (plan, workOrder) in items)
            {
                var usageMode = plan.UsageMode == "All" ? "全部" : "部分";
                var qtyText = plan.UsageMode == "All"
                    ? $"全部({plan.UsedQuantity?.ToString() ?? "0"} 支)"
                    : $"{plan.UsedQuantity?.ToString() ?? "-"} 支";
                var location = string.IsNullOrEmpty(plan.LocationArea) && string.IsNullOrEmpty(plan.LocationRack)
                    ? "-"
                    : string.IsNullOrEmpty(plan.LocationArea) ? plan.LocationRack
                    : string.IsNullOrEmpty(plan.LocationRack) ? plan.LocationArea
                    : $"{plan.LocationArea}/{plan.LocationRack}";

                table.Cell().Element(CellStyle).Text(workOrder.WorkOrderNo).FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.PlanDate.ToString("yyyy-MM-dd")).FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.BatchNo).FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.MaterialType).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.PlantGrade).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.Specification).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(usageMode).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(qtyText).FontSize(8);
                table.Cell().Element(CellStyle).Text($"{plan.UsedWeight:G29} kg").FontSize(8);
                table.Cell().Element(CellStyle).Text(location).FontSize(8);
            }
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
        if (!items.Any()) throw new BusinessException("items cannot be empty");
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("SimSun"));
                page.Header().Element(h => ComposeDocHeader(h, "圆 棒 穿 孔 计 划（批量）"));
                page.Content().Element(c => ComposeBatchPiercingContent(c, items));
                page.Footer().Element(ComposeDocFooter);
            });
        });
    }

    private static void ComposeBatchPiercingContent(IContainer container, List<(RoundBarPiercingPlan plan, WoEntity workOrder)> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(75);
                columns.ConstantColumn(55);
                columns.ConstantColumn(40);
                columns.ConstantColumn(60);
                columns.ConstantColumn(55);
                columns.ConstantColumn(55);
                columns.ConstantColumn(45);
                columns.ConstantColumn(40);
                columns.ConstantColumn(50);
                columns.ConstantColumn(45);
                columns.ConstantColumn(50);
            });

            table.Header(header =>
            {
                header.Cell().Element(CellHeaderStyle).Text("工单号").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("计划日期").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("原料类型").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("工厂牌号").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("圆棒规格").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("穿孔规格").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("需求单重").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("需求支数").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("需求重量").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("投料制成倍").FontSize(8).AlignCenter();
                header.Cell().Element(CellHeaderStyle).Text("要求到货日").FontSize(8).AlignCenter();
            });

            foreach (var (plan, workOrder) in items)
            {
                var rawMatType = EnumHelper.GetDisplayName(plan.RawMaterialType);

                table.Cell().Element(CellStyle).Text(workOrder.WorkOrderNo).FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.PlanDate.ToString("yyyy-MM-dd")).FontSize(8);
                table.Cell().Element(CellStyle).Text(rawMatType).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.PlantGrade).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.RoundBarSpec).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.PiercingSpec).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.RequiredUnitWeight?.ToString("G29") is string uw ? $"{uw} kg/支" : "-").FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.RequiredPieces?.ToString() is string rp ? $"{rp} 支" : "-").FontSize(8);
                table.Cell().Element(CellStyle).Text($"{plan.RequiredWeight:G29} kg").FontSize(8);
                table.Cell().Element(CellStyle).Text($"{plan.InputMultiple}").FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.RequiredDate.ToString("yyyy-MM-dd")).FontSize(8);
            }
        });
    }

    // ==============================
    // 8. 批量打印 - 库料改制汇总
    // ==============================
    public static Document CreateBatchReworkPlanDocument(List<(InventoryPlan plan, WoEntity workOrder)> items)
    {
        if (!items.Any()) throw new BusinessException("items cannot be empty");
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("SimSun"));
                page.Header().Element(h => ComposeDocHeader(h, "库 料 改 制 计 划（批量）"));
                page.Content().Element(c => ComposeBatchReworkContent(c, items));
                page.Footer().Element(ComposeDocFooter);
            });
        });
    }

    private static void ComposeBatchReworkContent(IContainer container, List<(InventoryPlan plan, WoEntity workOrder)> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(75);
                columns.ConstantColumn(55);
                columns.ConstantColumn(65);
                columns.ConstantColumn(55);
                columns.ConstantColumn(55);
                columns.ConstantColumn(60);
                columns.ConstantColumn(45);
                columns.ConstantColumn(55);
                columns.ConstantColumn(55);
                columns.ConstantColumn(55);
                columns.ConstantColumn(50);
            });

            table.Header(header =>
            {
                string[] headers = { "工单号", "计划日期", "批次号", "物料名称", "工厂牌号", "规格", "使用模式", "出库支数", "出库重量(kg)", "放置框架", "改制类型" };
                foreach (var h in headers)
                    header.Cell().Element(CellHeaderStyle).Text(h).FontSize(8).AlignCenter();
            });

            foreach (var (plan, workOrder) in items)
            {
                var usageMode = plan.UsageMode == "All" ? "全部" : "部分";
                var qtyText = plan.UsageMode == "All"
                    ? $"全部({plan.UsedQuantity?.ToString() ?? "0"} 支)"
                    : $"{plan.UsedQuantity?.ToString() ?? "-"} 支";
                var location = string.IsNullOrEmpty(plan.LocationArea) && string.IsNullOrEmpty(plan.LocationRack)
                    ? "-"
                    : string.IsNullOrEmpty(plan.LocationArea) ? plan.LocationRack
                    : string.IsNullOrEmpty(plan.LocationRack) ? plan.LocationArea
                    : $"{plan.LocationArea}/{plan.LocationRack}";

                var reworkTypeText = plan.ReworkType.HasValue ? EnumHelper.GetDisplayName(plan.ReworkType.Value) : "-";

                table.Cell().Element(CellStyle).Text(workOrder.WorkOrderNo).FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.PlanDate.ToString("yyyy-MM-dd")).FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.BatchNo).FontSize(8);
                table.Cell().Element(CellStyle).Text(plan.MaterialType).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.PlantGrade).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(plan.Specification).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(usageMode).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(qtyText).FontSize(8);
                table.Cell().Element(CellStyle).Text($"{plan.UsedWeight:G29} kg").FontSize(8);
                table.Cell().Element(CellStyle).Text(location).FontSize(8);
                table.Cell().Element(CellStyle).Text(reworkTypeText).FontSize(8).AlignCenter();
            }
        });
    }

    // ========== 表格样式 ==========

    private static IContainer CellHeaderStyle(IContainer container)
    {
        return container.Border(0.5f).BorderColor(Colors.Black)
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(3).PaddingHorizontal(2)
            .AlignMiddle();
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.Border(0.5f).BorderColor(Colors.Grey.Medium)
            .PaddingVertical(2).PaddingHorizontal(2)
            .AlignMiddle();
    }

}
