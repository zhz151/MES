using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.ProductionStandard;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Services.Printing;

/// <summary>
/// 销售订单 PDF 打印模板（QuestPDF）
/// 页面方向：A4 横向（Landscape），因项次明细列数较多
/// </summary>
public static class SalesOrderPrintHelper
{
    // ==============================
    // 1. 订单确认单（单条/批量合并）
    // ==============================
    public static byte[] GenerateOrderPdf(SalesOrderDetailDto order)
    {
        return GenerateBatchOrderPdf(new List<SalesOrderDetailDto> { order });
    }

    /// <summary>
    /// 批量订单合并打印（连续排版，每单独占区域）
    /// </summary>
    public static byte[] GenerateBatchOrderPdf(List<SalesOrderDetailDto> orders)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("SimSun"));

                page.Header().Element(h => ComposeDocHeader(h, "销 售 订 单 确 认 单"));

                page.Content().Element(c => ComposeOrderContent(c, orders));

                page.Footer().Element(ComposeDocFooter);
            });
        }).GeneratePdf();
    }

    // ==============================
    // 2. 技术要求确认单
    // ==============================
    public static byte[] GenerateRequirementsPdf(SalesOrderDetailDto order, List<ProductRequirementDto> requirements)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("SimSun"));

                page.Header().Element(h => ComposeDocHeader(h, "技 术 要 求 确 认 单"));

                page.Content().Element(c => ComposeRequirementsContent(c, order, requirements));

                page.Footer().Element(ComposeDocFooter);
            });
        }).GeneratePdf();
    }

    // ========== 页眉 ==========

    private static void ComposeDocHeader(IContainer container, string title)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(4).AlignCenter().Text(title)
                .FontSize(18).Bold();

            col.Item().PaddingVertical(3)
                .LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    // ========== 页脚 ==========

    private static void ComposeDocFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingVertical(3)
                .LineHorizontal(1).LineColor(Colors.Black);

            col.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text($"打印日期：{DateTime.Now:yyyy-MM-dd}").FontSize(9);
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.CurrentPageNumber().FontSize(9);
                    t.Span("/").FontSize(9);
                    t.TotalPages().FontSize(9);
                });
            });
        });
    }

    // ========== 订单内容（单条或批量） ==========

    private static void ComposeOrderContent(IContainer container, List<SalesOrderDetailDto> orders)
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

                // 订单头信息
                ComposeOrderHeader(col.Item(), order);

                col.Item().PaddingVertical(3);

                // 项次明细表
                ComposeOrderItemsTable(col.Item(), order.Items);

                // 汇总行
                ComposeOrderSummary(col.Item(), order.Items);
            }
        });
    }

    private static void ComposeOrderHeader(IContainer container, SalesOrderDetailDto order)
    {
        container.Row(row =>
        {
            row.RelativeItem(2).Text(t =>
            {
                t.Span("订单编号：").Bold().FontSize(10);
                t.Span(order.OrderNumber).FontSize(10);
            });
            row.RelativeItem(2).Text(t =>
            {
                t.Span("签订日期：").Bold().FontSize(10);
                t.Span(order.SignDate.ToString("yyyy-MM-dd")).FontSize(10);
            });
            row.RelativeItem(2).Text(t =>
            {
                t.Span("业务员：").Bold().FontSize(10);
                t.Span(order.Salesman).FontSize(10);
            });
            row.RelativeItem(3).Text(t =>
            {
                t.Span("客户名称：").Bold().FontSize(10);
                t.Span(order.CustomerName).FontSize(10);
            });
            row.RelativeItem(2).Text(t =>
            {
                t.Span("最终客户：").Bold().FontSize(10);
                t.Span(order.EndCustomer ?? "-").FontSize(10);
            });
            row.RelativeItem(2).Text(t =>
            {
                t.Span("状态：").Bold().FontSize(10);
                t.Span(EnumHelper.GetDisplayName(order.Status)).FontSize(10);
            });
        });
    }

    private static void ComposeOrderItemsTable(IContainer container, List<OrderItemDto> items)
    {
        container.Table(table =>
        {
            // 列宽定义（20列）
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(28);   // 项次
                columns.ConstantColumn(55);   // 交货日期
                columns.ConstantColumn(28);   // 延期罚款
                columns.ConstantColumn(38);   // 结算方式
                columns.ConstantColumn(38);   // 物料名称
                columns.ConstantColumn(48);   // 产品标准
                columns.ConstantColumn(42);   // 交货状态
                columns.ConstantColumn(42);   // 牌号
                columns.ConstantColumn(72);   // 规格(外径×壁厚)
                columns.ConstantColumn(32);   // 外径下差
                columns.ConstantColumn(32);   // 外径上差
                columns.ConstantColumn(32);   // 壁厚下差
                columns.ConstantColumn(32);   // 壁厚上差
                columns.ConstantColumn(36);   // 长度状态
                columns.ConstantColumn(32);   // 最小长度
                columns.ConstantColumn(32);   // 最大长度
                columns.ConstantColumn(30);   // 支数
                columns.ConstantColumn(42);   // 米数
                columns.ConstantColumn(50);   // 合同重量
                columns.RelativeColumn();     // 理算重量
            });

            // 表头
            string[] headers = { "项次", "交货日期", "罚款", "结算", "物料", "标准", "交货状态", "牌号", "规格(外径×壁厚)", "外径下差", "外径上差", "壁厚下差", "壁厚上差", "长度状态", "最小长度", "最大长度", "支数", "米数", "合同重量", "理算重量" };

            foreach (var header in headers)
            {
                table.Cell().Element(CellHeaderStyle).Text(header).FontSize(7).AlignCenter();
            }

            // 数据行
            foreach (var item in items.OrderBy(i => i.Sequence))
            {
                table.Cell().Element(CellStyle).Text(item.Sequence.ToString()).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(item.DeliveryDate.ToString("yyyy-MM-dd")).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(item.DelayPenalty ? "是" : "否").FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(EnumHelper.GetDisplayName(item.SettlementMethod)).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(EnumHelper.GetDisplayName(item.MaterialName)).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(item.StandardNo).FontSize(6).AlignCenter();
                table.Cell().Element(CellStyle).Text(EnumHelper.GetDisplayName(item.DeliveryState)).FontSize(6).AlignCenter();
                table.Cell().Element(CellStyle).Text(item.StandardGrade).FontSize(6).AlignCenter();
                table.Cell().Element(CellStyle).Text(item.Specification).FontSize(6).AlignCenter();
                table.Cell().Element(CellStyle).Text(FormatDecimal(item.OuterDiameterNegative)).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(FormatDecimal(item.OuterDiameterPositive)).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(FormatDecimal(item.WallThicknessNegative)).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(FormatDecimal(item.WallThicknessPositive)).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(EnumHelper.GetDisplayName(item.LengthStatus)).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(FormatNullableDecimal(item.MinLength)).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(FormatNullableDecimal(item.MaxLength)).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(item.Quantity?.ToString() ?? "-").FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(FormatNullableDecimal(item.Meters)).FontSize(7).AlignCenter();
                table.Cell().Element(CellStyle).Text(FormatDecimal(item.ContractWeight)).FontSize(7).AlignRight();
                table.Cell().Element(CellStyle).Text(FormatDecimal(item.TheoreticalWeight)).FontSize(7).AlignRight();
            }
        });
    }

    private static void ComposeOrderSummary(IContainer container, List<OrderItemDto> items)
    {
        var totalQty = items.Sum(i => i.Quantity ?? 0);
        var totalMeters = items.Sum(i => i.Meters ?? 0);
        var totalContractWeight = items.Sum(i => i.ContractWeight);
        var totalTheoryWeight = items.Sum(i => i.TheoreticalWeight);

        container.AlignRight().Text(t =>
        {
            t.Span($"合计：{totalQty} 支  /  {FormatDecimal(totalMeters)} 米  /  合同重量 {FormatDecimal(totalContractWeight)} kg  /  理算重量 {FormatDecimal(totalTheoryWeight)} kg")
                .FontSize(9).Bold();
        });
    }

    // ========== 技术要求内容 ==========

    private static void ComposeRequirementsContent(IContainer container, SalesOrderDetailDto order, List<ProductRequirementDto> requirements)
    {
        container.Column(col =>
        {
            // 订单概要信息（与销售订单确认单统一）
            ComposeOrderHeader(col.Item(), order);

            col.Item().PaddingVertical(5);

            // 技术要求表
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);   // 项次
                    columns.ConstantColumn(36);   // 要求类型
                    columns.RelativeColumn();     // 化学成分
                    columns.RelativeColumn();     // 机械性能
                    columns.RelativeColumn();     // 尺寸公差
                    columns.RelativeColumn();     // 表面质量
                    columns.RelativeColumn();     // 无损检测
                    columns.RelativeColumn();     // 其他要求
                });

                // 表头
                string[] headers = { "项次", "要求类型", "化学成分", "机械性能", "尺寸公差", "表面质量", "无损检测", "其他要求" };
                foreach (var header in headers)
                {
                    table.Cell().Element(CellHeaderStyle).Text(header).FontSize(8).AlignCenter();
                }

                // 数据行
                foreach (var req in requirements.OrderBy(r => r.Sequence))
                {
                    table.Cell().Element(CellStyle).Text(req.Sequence.ToString()).FontSize(8).AlignCenter();
                    table.Cell().Element(CellStyle).Text(EnumHelper.GetDisplayName(req.RequirementType)).FontSize(8).AlignCenter();
                    table.Cell().Element(CellStyle).Text(req.ChemicalComposition ?? "-").FontSize(8);
                    table.Cell().Element(CellStyle).Text(req.MechanicalProperty ?? "-").FontSize(8);
                    table.Cell().Element(CellStyle).Text(req.ToleranceRequirement ?? "-").FontSize(8);
                    table.Cell().Element(CellStyle).Text(req.SurfaceQuality ?? "-").FontSize(8);
                    table.Cell().Element(CellStyle).Text(req.NdtRequirement ?? "-").FontSize(8);
                    table.Cell().Element(CellStyle).Text(req.OtherRequirement ?? "-").FontSize(8);
                }
            });

            if (requirements.Count == 0)
            {
                col.Item().PaddingTop(8).AlignCenter().Text("暂无技术要求数据").FontSize(10).FontColor(Colors.Grey.Medium);
            }
        });
    }

    // ========== 表格单元格样式 ==========

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

    // ========== 辅助方法 ==========

    private static string FormatDecimal(decimal value) => value == 0 ? "0" : value.ToString("G29");
    private static string FormatNullableDecimal(decimal? value) => value.HasValue && value.Value != 0 ? value.Value.ToString("G29") : "-";
}
