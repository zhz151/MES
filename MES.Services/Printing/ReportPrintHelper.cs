using MES.Core.DTOs.Report;
using MES.Services.Report;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MES.Services.Printing;

/// <summary>
/// 产量报表 PDF 打印模板 — Mode A 自包含布局
/// </summary>
public static class ReportPrintHelper
{
    /// <summary>
    /// 生成产量报表 PDF
    /// </summary>
    /// <param name="title">报表标题</param>
    /// <param name="report">报表数据</param>
    /// <param name="visibleColumnKeys">前端选定的可见列 Key 列表（按显示顺序），null 表示全部列</param>
    public static byte[] GenerateProductionReportPdf(string title, DailyProductionReportResponse report, List<string>? visibleColumnKeys = null)
    {
        var columns = visibleColumnKeys ?? report.SectionColumns;
        var rows = report.Rows;

        // 确保所有选定列在数据中存在
        var validColumns = columns.Where(c => report.SectionColumns.Contains(c)).ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(16);
                page.DefaultTextStyle(x => x.FontSize(7).FontFamily("SimSun"));

                page.Header().Element(h => ComposeHeader(h, title));
                page.Content().Element(c => ComposeContent(c, validColumns, rows));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, string title)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(2).AlignCenter().Text(title).FontSize(14).Bold();
            col.Item().PaddingVertical(2).LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingVertical(2).LineHorizontal(1).LineColor(Colors.Black);
            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Text($"打印日期：{DateTime.Now:yyyy-MM-dd}").FontSize(7);
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.CurrentPageNumber().FontSize(7);
                    t.Span("/").FontSize(7);
                    t.TotalPages().FontSize(7);
                });
            });
        });
    }

    private static void ComposeContent(IContainer container, List<string> columns, List<DailyProductionReportRow> rows)
    {
        if (rows.Count == 0)
        {
            container.AlignCenter().Text("暂无数据").FontSize(10);
            return;
        }

        // 列数过多时使用较小字号
        var fontSize = columns.Count > 15 ? 6 : 7;

        container.Table(table =>
        {
            // 列定义：日期列固定 80px，数据列等分
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(80); // 日期列
                foreach (var _ in columns)
                    cols.RelativeColumn();
            });

            // 表头
            table.Header(header =>
            {
                header.Cell().Element(CellHeaderStyle).Text("日期").FontSize(fontSize).Bold().AlignCenter();
                foreach (var col in columns)
                {
                    header.Cell().Element(CellHeaderStyle)
                        .Text(col).FontSize(fontSize).Bold().AlignCenter();
                }
            });

            // 数据行
            foreach (var row in rows)
            {
                table.Cell().Element(CellStyle).Text(row.DisplayDate).FontSize(fontSize).AlignCenter();

                foreach (var col in columns)
                {
                    var hasValue = row.Values.TryGetValue(col, out var weight);
                    var display = hasValue && weight > 0 ? ((int)weight).ToString() : "-";
                    table.Cell().Element(CellStyle)
                        .Text(display).FontSize(fontSize).AlignRight();
                }
            }

            // 合计行
            table.Cell().Element(FooterCellStyle).Text("合计").FontSize(fontSize).Bold().AlignCenter();
            foreach (var col in columns)
            {
                var sum = rows.Sum(r => r.Values.GetValueOrDefault(col, 0m));
                var display = sum > 0 ? ((int)sum).ToString() : "-";
                table.Cell().Element(FooterCellStyle)
                    .Text(display).FontSize(fontSize).Bold().AlignRight();
            }
        });
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container
            .Border(0.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingHorizontal(3)
            .PaddingVertical(2)
            .AlignMiddle();
    }

    private static IContainer CellHeaderStyle(IContainer container)
    {
        return container
            .Border(0.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten3)
            .PaddingHorizontal(3)
            .PaddingVertical(3)
            .AlignMiddle();
    }

    private static IContainer FooterCellStyle(IContainer container)
    {
        return container
            .Border(0.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten4)
            .PaddingHorizontal(3)
            .PaddingVertical(2)
            .AlignMiddle();
    }
}
