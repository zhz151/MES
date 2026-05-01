using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MES.Data.Entities;
using MES.Core.Enums;

namespace MES.Services.Printing;

/// <summary>
/// 用料计划 PDF 打印模板（QuestPDF）
/// </summary>
public static class MaterialPlanPrintHelper
{
    // ==============================
    // 1. 原料采购申请单
    // ==============================
    public static byte[] GenerateSemiPlanPdf(PurchaseSemiPlan plan, WorkOrder workOrder)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("SimSun"));

                page.Header().Element(h => ComposeDocHeader(h, "采 购 申 请 单", $"PUR-{plan.PlanDate:yyyyMMdd}-{plan.Id:D4}", plan.PlanDate));
                page.Content().Element(c => ComposeSemiContent(c, plan, workOrder));
                page.Footer().Element(f => ComposeDocFooter(f, plan.PlanDate));
            });
        }).GeneratePdf();
    }

    // ==============================
    // 2. 成品采购申请单
    // ==============================
    public static byte[] GenerateFinishPlanPdf(PurchaseFinishedPlan plan, WorkOrder workOrder)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("SimSun"));

                page.Header().Element(h => ComposeDocHeader(h, "采 购 申 请 单", $"PUR-{plan.PlanDate:yyyyMMdd}-{plan.Id:D4}", plan.PlanDate));
                page.Content().Element(c => ComposeFinishContent(c, plan, workOrder));
                page.Footer().Element(f => ComposeDocFooter(f, plan.PlanDate));
            });
        }).GeneratePdf();
    }

    // ==============================
    // 3. 库存使用单
    // ==============================
    public static byte[] GenerateInventoryPlanPdf(InventoryPlan plan, WorkOrder workOrder)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("SimSun"));

                var docNo = $"INV-{plan.PlanDate:yyyyMMdd}-{plan.Id:D4}";
                page.Header().Element(h => ComposeDocHeader(h, "库 存 使 用 单", docNo, plan.PlanDate));
                page.Content().Element(c => ComposeInventoryContent(c, plan, workOrder));
                page.Footer().Element(f => ComposeDocFooter(f, plan.PlanDate));
            });
        }).GeneratePdf();
    }

    // ==============================
    // 4. 库料改制单
    // ==============================
    public static byte[] GenerateReworkPlanPdf(InventoryPlan plan, WorkOrder workOrder)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("SimSun"));

                var docNo = $"REW-{plan.PlanDate:yyyyMMdd}-{plan.Id:D4}";
                page.Header().Element(h => ComposeDocHeader(h, "库 料 改 制 单", docNo, plan.PlanDate));
                page.Content().Element(c => ComposeReworkContent(c, plan, workOrder));
                page.Footer().Element(f => ComposeDocFooter(f, plan.PlanDate));
            });
        }).GeneratePdf();
    }

    // ========== 公共组件 ==========

    private static void ComposeDocHeader(IContainer container, string title, string docNo, DateTime date)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(5).AlignCenter().Text(title)
                .FontSize(18).Bold();

            col.Item().Row(row =>
            {
                row.RelativeItem().AlignLeft().Text($"编号：{docNo}").FontSize(10);
                row.RelativeItem().AlignRight().Text($"日期：{date:yyyy-MM-dd}").FontSize(10);
            });

            col.Item().PaddingVertical(4)
                .LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    private static void ComposeDocFooter(IContainer container, DateTime date)
    {
        container.Column(col =>
        {
            col.Item().PaddingVertical(4)
                .LineHorizontal(1).LineColor(Colors.Black);

            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Text("制单人：").FontSize(10);
                row.RelativeItem().Text("审核人：").FontSize(10);
                row.RelativeItem().AlignRight().Text($"打印日期：{DateTime.Now:yyyy-MM-dd}").FontSize(9);
            });
        });
    }

    private static void ComposeSectionHeader(IContainer container, string title)
    {
        container.PaddingVertical(4).Text(title).FontSize(12).Bold();
    }

    private static void ComposeInfoRow(IContainer container, string label, string value)
    {
        container.PaddingVertical(1).Row(row =>
        {
            row.ConstantItem(120).Text(label).FontSize(10);
            row.RelativeItem().Text(value ?? "-").FontSize(10);
        });
    }

    private static void ComposeWorkOrderInfo(IContainer container, WorkOrder wo)
    {
        container.Column(col =>
        {
            ComposeSectionHeader(col.Item(), "二、工单信息");
            ComposeInfoRow(col.Item(), "工单号：", wo.WorkOrderNo);
            ComposeInfoRow(col.Item(), "订单号：", wo.SalesOrderNo);
            ComposeInfoRow(col.Item(), "工厂牌号：", wo.PlantGrade);
            ComposeInfoRow(col.Item(), "成品规格：", wo.Specification);
            var lenText = wo.LengthStatus == LengthStatus.Fixed
                ? $"定尺 {wo.MaxLength?.ToString("G29") ?? ""}mm"
                : $"{wo.LengthStatus} {(wo.MaxLength.HasValue ? $"(最长{wo.MaxLength.Value}mm)" : "")}";
            ComposeInfoRow(col.Item(), "长度状态：", lenText);
            ComposeInfoRow(col.Item(), "总量：", $"{wo.TotalQuantity} 支 / {wo.TotalWeight:G29} kg");
            ComposeInfoRow(col.Item(), "结算方式：", wo.SettlementMethod.ToString());
            ComposeInfoRow(col.Item(), "交货状态：", wo.DeliveryState.ToString());
        });
    }

    // ========== 原料采购内容 ==========

    private static void ComposeSemiContent(IContainer container, PurchaseSemiPlan plan, WorkOrder workOrder)
    {
        container.Column(col =>
        {
            ComposeSectionHeader(col.Item(), "一、采购原料");
            var rawTypeText = plan.RawMaterialType == RawMaterialType.SemiFinished ? "荒管" : "半成品";
            ComposeInfoRow(col.Item(), "原料类型：", rawTypeText);
            ComposeInfoRow(col.Item(), "原料规格：", plan.RawMaterialSpec);
            ComposeInfoRow(col.Item(), "工厂牌号：", workOrder.PlantGrade);
            ComposeInfoRow(col.Item(), "采购支数：", plan.RequiredPieces?.ToString() + " 支");
            ComposeInfoRow(col.Item(), "采购重量：", $"{plan.RequiredWeight:G29} kg");
            ComposeInfoRow(col.Item(), "原料单重：", plan.RawUnitWeight?.ToString("G29") + " kg/支");
            ComposeInfoRow(col.Item(), "成材率：", $"{plan.YieldRate:G29}%");
            ComposeInfoRow(col.Item(), "正品率：", $"{plan.QualifiedRate:G29}%");
            ComposeInfoRow(col.Item(), "投料倍率：", $"{plan.InputMultiple} (1:{plan.InputMultiple})");
            ComposeInfoRow(col.Item(), "要求到货：", plan.RequiredDate?.ToString("yyyy-MM-dd") ?? "-");
            ComposeInfoRow(col.Item(), "备注：", plan.Remark ?? "-");

            col.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Medium);

            ComposeWorkOrderInfo(col.Item(), workOrder);
        });
    }

    // ========== 成品采购内容 ==========

    private static void ComposeFinishContent(IContainer container, PurchaseFinishedPlan plan, WorkOrder workOrder)
    {
        container.Column(col =>
        {
            ComposeSectionHeader(col.Item(), "一、采购内容");
            ComposeInfoRow(col.Item(), "成品类型：", plan.ProductType == FinishedProductType.Critical ? "临界成品" : "订单成品");
            ComposeInfoRow(col.Item(), "采购支数：", plan.RequiredPiece?.ToString() + " 支");
            ComposeInfoRow(col.Item(), "采购重量：", $"{plan.RequiredWeight:G29} kg");
            ComposeInfoRow(col.Item(), "要求到货：", plan.RequiredDate?.ToString("yyyy-MM-dd") ?? "-");
            ComposeInfoRow(col.Item(), "备注：", plan.Remark ?? "-");

            col.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Medium);

            ComposeWorkOrderInfo(col.Item(), workOrder);
        });
    }

    // ========== 库存使用内容 ==========

    private static void ComposeInventoryContent(IContainer container, InventoryPlan plan, WorkOrder workOrder)
    {
        container.Column(col =>
        {
            ComposeSectionHeader(col.Item(), "一、使用物料");
            ComposeInfoRow(col.Item(), "批次号：", plan.BatchNo);
            ComposeInfoRow(col.Item(), "工厂牌号：", plan.PlantGrade);
            ComposeInfoRow(col.Item(), "规格：", plan.Specification);

            var usageModeText = plan.UsageMode == "All" ? "全部" : "部分";
            var qtyText = plan.UsageMode == "All"
                ? $"全部({plan.UsedQuantity?.ToString() ?? "0"} 支)"
                : $"{plan.UsedQuantity?.ToString() ?? "-"} 支";
            ComposeInfoRow(col.Item(), "出库支数：", qtyText);
            ComposeInfoRow(col.Item(), "出库重量：", $"{plan.UsedWeight:G29} kg");
            ComposeInfoRow(col.Item(), "投料倍率：", $"1:{plan.InputMultiple}");
            ComposeInfoRow(col.Item(), "使用模式：", usageModeText);
            ComposeInfoRow(col.Item(), "备注：", plan.Remark ?? "-");

            col.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Medium);

            ComposeWorkOrderInfo(col.Item(), workOrder);
        });
    }

    // ========== 库料改制内容 ==========

    private static void ComposeReworkContent(IContainer container, InventoryPlan plan, WorkOrder workOrder)
    {
        container.Column(col =>
        {
            ComposeSectionHeader(col.Item(), "一、改制物料");
            ComposeInfoRow(col.Item(), "批次号：", plan.BatchNo);
            ComposeInfoRow(col.Item(), "工厂牌号：", plan.PlantGrade);
            ComposeInfoRow(col.Item(), "规格：", plan.Specification);

            var reworkTypeText = plan.ReworkType switch
            {
                ReworkType.EmptyDrawing => "空拉改制",
                ReworkType.FewerPass => "少道次改制",
                ReworkType.ManualSelect => "人工选择改制",
                _ => plan.ReworkType?.ToString() ?? "-"
            };
            ComposeInfoRow(col.Item(), "改制类型：", reworkTypeText);

            var usageModeText = plan.UsageMode == "All" ? "全部" : "部分";
            var qtyText = plan.UsageMode == "All"
                ? $"全部({plan.UsedQuantity?.ToString() ?? "0"} 支)"
                : $"{plan.UsedQuantity?.ToString() ?? "-"} 支";
            ComposeInfoRow(col.Item(), "出库支数：", qtyText);
            ComposeInfoRow(col.Item(), "出库重量：", $"{plan.UsedWeight:G29} kg");
            ComposeInfoRow(col.Item(), "投料倍率：", $"1:{plan.InputMultiple}");
            ComposeInfoRow(col.Item(), "使用模式：", usageModeText);

            // 工艺路线
            if (!string.IsNullOrEmpty(plan.ProcessPlan))
            {
                try
                {
                    var steps = System.Text.Json.JsonSerializer.Deserialize<List<ProcessStep>>(plan.ProcessPlan);
                    if (steps?.Any() == true)
                    {
                        var specStr = string.Join(" → ", steps.OrderBy(s => s.step).Select(s => s.spec));
                        ComposeInfoRow(col.Item(), "工艺路线：", specStr);
                    }
                }
                catch
                {
                    ComposeInfoRow(col.Item(), "工艺路线：", plan.ProcessPlan);
                }
            }

            ComposeInfoRow(col.Item(), "备注：", plan.Remark ?? "-");

            col.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Medium);

            ComposeWorkOrderInfo(col.Item(), workOrder);
        });
    }

    private class ProcessStep
    {
        public int step { get; set; }
        public string spec { get; set; } = null!;
    }
}
