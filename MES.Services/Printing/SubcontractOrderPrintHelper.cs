using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
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
/// 委外加工单 PDF 打印模板（QuestPDF）
/// 仿"订单管理"模式：每单显示表头信息 + 明细加工数据
/// 页面方向：A4 横向（Landscape）
/// </summary>
public static class SubcontractOrderPrintHelper
{
    public static byte[] GeneratePdf(SubcontractOrderDto order)
    {
        return GenerateBatchPdf(new List<SubcontractOrderDto> { order });
    }

    public static byte[] GenerateBatchPdf(List<SubcontractOrderDto> orders)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("SimSun"));

                page.Header().Element(h => ComposeHeader(h, "委 外 加 工 单 列 表"));

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

    private static void ComposeContent(IContainer container, List<SubcontractOrderDto> orders)
    {
        container.Column(col =>
        {
            for (int i = 0; i < orders.Count; i++)
            {
                var order = orders[i];

                if (i > 0)
                {
                    // 批量模式下，订单间用分隔线
                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                }

                // 订单头信息（单行）
                ComposeOrderHeader(col.Item(), order);

                col.Item().PaddingVertical(3);

                // 委外明细表（加工数据）
                ComposeReturnItemsTable(col.Item(), order.ReturnItems);

                // 汇总行
                ComposeOrderSummary(col.Item(), order.ReturnItems);
            }
        });
    }

    private static void ComposeOrderHeader(IContainer container, SubcontractOrderDto order)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem(2).Text(t =>
                {
                    t.Span("委外单号：").Bold().FontSize(9);
                    t.Span(order.OrderNo).FontSize(9);
                });
                row.RelativeItem(2.5f).Text(t =>
                {
                    t.Span("供应商：").Bold().FontSize(9);
                    t.Span(order.SupplierName).FontSize(9);
                });
                row.RelativeItem(2).Text(t =>
                {
                    t.Span("下单日期：").Bold().FontSize(9);
                    t.Span(order.OrderDate.ToString("yyyy-MM-dd")).FontSize(9);
                });
                row.RelativeItem(1.5f).Text(t =>
                {
                    t.Span("加工类型：").Bold().FontSize(9);
                    t.Span(EnumHelper.GetDisplayName(order.ProcessType)).FontSize(9);
                });
                row.RelativeItem(1.5f).Text(t =>
                {
                    t.Span("炉号：").Bold().FontSize(9);
                    t.Span(order.FurnaceNumber ?? "-").FontSize(9);
                });
                row.RelativeItem(2).Text(t =>
                {
                    t.Span("物料分类：").Bold().FontSize(9);
                    t.Span(EnumHelper.GetDisplayName(order.OutMaterialCategory)).FontSize(9);
                });
                row.RelativeItem(2).Text(t =>
                {
                    t.Span("工厂牌号：").Bold().FontSize(9);
                    t.Span(order.OutPlantGrade).FontSize(9);
                });
            });

            col.Item().PaddingTop(2).Row(row =>
            {
                row.RelativeItem(2).Text(t =>
                {
                    t.Span("规格：").Bold().FontSize(9);
                    t.Span(order.OutSpecification).FontSize(9);
                });
                row.RelativeItem(2).Text(t =>
                {
                    t.Span("发出支数：").Bold().FontSize(9);
                    t.Span(order.OutQuantity.ToString("G29")).FontSize(9);
                });
                row.RelativeItem(2).Text(t =>
                {
                    t.Span("发出重量：").Bold().FontSize(9);
                    t.Span($"{order.OutWeight:G29} kg").FontSize(9);
                });
                row.RelativeItem(2).Text(t =>
                {
                    t.Span("收回期限：").Bold().FontSize(9);
                    t.Span(order.ReturnDeadline?.ToString("yyyy-MM-dd") ?? "-").FontSize(9);
                });
                row.RelativeItem(1.5f).Text(t =>
                {
                    t.Span("状态：").Bold().FontSize(9);
                    t.Span(EnumHelper.GetDisplayName(order.Status)).FontSize(9);
                });
                row.RelativeItem(2.5f).Text(t =>
                {
                    t.Span("已回收：").Bold().FontSize(9);
                    t.Span($"{order.InQuantity?.ToString("G29") ?? "0"}支 / {order.InWeight?.ToString("G29") ?? "0"}kg").FontSize(9);
                });
                row.RelativeItem(2).Text(t =>
                {
                    t.Span("备注：").Bold().FontSize(9);
                    t.Span(order.Remark ?? "-").FontSize(9);
                });
            });
        });
    }

    private static void ComposeReturnItemsTable(IContainer container, List<SubcontractReturnItemDto> items)
    {
        container.Table(table =>
        {
            // 列宽定义
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(28);   // 序号
                columns.ConstantColumn(55);   // 物料分类
                columns.ConstantColumn(55);   // 加工牌号
                columns.ConstantColumn(72);   // 加工规格
                columns.ConstantColumn(42);   // 单重
                columns.ConstantColumn(42);   // 需求支数
                columns.ConstantColumn(50);   // 需求重量
                columns.ConstantColumn(50);   // 加工状态
                columns.ConstantColumn(50);   // 单价
                columns.ConstantColumn(55);   // 加工金额
                columns.ConstantColumn(72);   // 来源工单号
                columns.RelativeColumn();     // 备注
            });

            // 表头
            string[] headers = { "序号", "物料分类", "加工牌号", "加工规格", "单重", "需求支数", "需求重量",
                "加工状态", "加工单价", "加工金额", "来源工单号", "备注" };
            foreach (var h in headers)
                table.Cell().Element(CellHeaderStyle).Text(h).FontSize(7).AlignCenter();

            // 数据行
            foreach (var item in items.OrderBy(i => i.Sequence))
            {
                table.Cell().Element(CellStyle).Text(item.Sequence.ToString()).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(EnumHelper.GetDisplayName(item.MaterialCategory)).FontSize(7);
                table.Cell().Element(CellStyle).Text(item.PlantGrade ?? "-").FontSize(7);
                table.Cell().Element(CellStyle).Text(item.ProcessSpecification).FontSize(7);
                table.Cell().Element(CellStyle).Text(FormatNullableDecimal(item.UnitWeight)).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(item.RequiredQuantity?.ToString("G29") ?? "-").FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(FormatNullableDecimal(item.RequiredWeight)).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(item.ProcessStatusRemark ?? "-").FontSize(7);
                table.Cell().Element(CellStyle).Text(FormatNullableDecimal(item.ProcessUnitPrice)).FontSize(7).AlignRight();
                table.Cell().Element(CellStyle).Text(FormatNullableDecimal(item.ProcessTotalAmount)).FontSize(7).AlignRight();
                table.Cell().Element(CellStyle).Text(item.SourceWorkOrderNo ?? "-").FontSize(7);
                table.Cell().Element(CellStyle).Text(item.Remark ?? "-").FontSize(7);
            }

            // 无数据时显示
            if (items.Count == 0)
            {
                table.Cell().Element(CellStyle).Text("暂无加工明细数据").FontSize(7).FontColor(Colors.Grey.Medium);
            }
        });
    }

    private static void ComposeOrderSummary(IContainer container, List<SubcontractReturnItemDto> items)
    {
        var totalQty = items.Sum(i => i.RequiredQuantity ?? 0);
        var totalWeight = items.Sum(i => i.RequiredWeight ?? 0);
        var totalAmount = items.Sum(i => i.ProcessTotalAmount ?? 0);

        container.AlignRight().PaddingTop(3).Text(t =>
        {
            t.Span($"合计：{totalQty} 支  /  需求重量 {totalWeight:G29} kg")
                .FontSize(8).Bold();
            if (totalAmount > 0)
                t.Span($"  /  加工金额 {totalAmount:G29} 元").FontSize(8).Bold();
        });
    }

    private static string FormatNullableDecimal(decimal? value)
        => value.HasValue && value.Value != 0 ? value.Value.ToString("G29") : "-";

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
