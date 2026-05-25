using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MES.Core.DTOs;
using MES.Core.Enums;

namespace MES.Services.Printing;

/// <summary>
/// 采购订单 PDF 打印模板（QuestPDF）
/// </summary>
public static class PurchaseOrderPrintHelper
{
    public static byte[] GeneratePdf(PurchaseOrderDto order)
    {
        return GenerateBatchPdf(new List<PurchaseOrderDto> { order });
    }

    public static byte[] GenerateBatchPdf(List<PurchaseOrderDto> orders)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("SimSun"));

                page.Header().Element(h => ComposeHeader(h, "采 购 订 单 列 表"));

                page.Content().Element(c => ComposeContent(c, orders));

                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, string title)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(4).AlignCenter().Text(title).FontSize(16).Bold();
            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingVertical(3).LineHorizontal(1).LineColor(Colors.Black);
            col.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text($"打印日期：{DateTime.Now:yyyy-MM-dd}").FontSize(8);
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.CurrentPageNumber().FontSize(8);
                    t.Span("/").FontSize(8);
                    t.TotalPages().FontSize(8);
                });
            });
        });
    }

    private static void ComposeContent(IContainer container, List<PurchaseOrderDto> orders)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(24);   // 序号
                columns.ConstantColumn(72);   // 采购单号
                columns.ConstantColumn(55);   // 下单日期
                columns.ConstantColumn(65);   // 来源工单号
                columns.ConstantColumn(50);   // 物料分类
                columns.ConstantColumn(55);   // 厂内钢种
                columns.ConstantColumn(65);   // 规格
                columns.ConstantColumn(38);   // 单支重量
                columns.ConstantColumn(30);   // 支数
                columns.ConstantColumn(38);   // 投料倍率
                columns.ConstantColumn(50);   // 采购重量
                columns.ConstantColumn(55);   // 要求到货日
                columns.RelativeColumn();     // 供应商
                columns.ConstantColumn(32);   // 状态
                columns.ConstantColumn(55);   // 已到货
            });

            table.Header(header =>
            {
                string[] headers = { "序号", "采购单号", "下单日期", "来源工单号", "物料分类", "厂内钢种", "规格",
                    "单支重量", "支数", "投料倍率", "采购重量", "要求到货日", "供应商", "状态", "已到货" };
                foreach (var h in headers)
                    header.Cell().Element(CellHeaderStyle).Text(h).FontSize(7).AlignCenter();
            });

            int seq = 0;
            foreach (var o in orders)
            {
                seq++;
                table.Cell().Element(CellStyle).Text(seq.ToString()).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(o.OrderNo).FontSize(7);
                table.Cell().Element(CellStyle).Text(o.OrderDate.ToString("yyyy-MM-dd")).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(o.SourceWorkOrderNo ?? "-").FontSize(7);
                table.Cell().Element(CellStyle).Text(o.MaterialCategory).FontSize(7);
                table.Cell().Element(CellStyle).Text(o.PlantGrade).FontSize(7);
                table.Cell().Element(CellStyle).Text(o.Specification).FontSize(7);
                table.Cell().Element(CellStyle).Text(o.UnitWeight?.ToString("G29") ?? "-").FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(o.Quantity?.ToString("G29") ?? "-").FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(o.InputMultiple?.ToString() ?? "-").FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(o.Weight.ToString("G29")).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(o.RequiredDate.ToString("yyyy-MM-dd")).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(o.SupplierName).FontSize(7);
                table.Cell().Element(CellStyle).Text(GetStatusText(o.Status)).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text($"{o.ReceivedQuantity}支/{o.ReceivedWeight.ToString("G29")}kg").FontSize(7).AlignCenter();
            }
        });
    }

    private static string GetStatusText(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Open => "已下单",
        PurchaseOrderStatus.Partial => "部分到货",
        PurchaseOrderStatus.Completed => "已完成",
        PurchaseOrderStatus.Cancelled => "已取消",
        _ => status.ToString()
    };

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
