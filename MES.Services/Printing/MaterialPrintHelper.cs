using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MES.Core.DTOs;

namespace MES.Services.Printing;

/// <summary>
/// 物料档案 PDF 打印模板（QuestPDF）
/// </summary>
public static class MaterialPrintHelper
{
    public static byte[] GeneratePdf(MaterialDto material)
    {
        return GenerateBatchPdf(new List<MaterialDto> { material });
    }

    public static byte[] GenerateBatchPdf(List<MaterialDto> materials)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("SimSun"));

                page.Header().Element(h => ComposeHeader(h, "物 料 档 案 列 表"));

                page.Content().Element(c => ComposeContent(c, materials));

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

    private static void ComposeContent(IContainer container, List<MaterialDto> materials)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(28);   // 序号
                columns.ConstantColumn(70);   // 物料编码
                columns.RelativeColumn();     // 物料分类
                columns.RelativeColumn();     // 厂内钢种
                columns.RelativeColumn();     // 名义规格
                columns.ConstantColumn(50);   // 状态
                columns.RelativeColumn();     // 备注
            });

            table.Header(header =>
            {
                string[] headers = { "序号", "物料编码", "物料分类", "厂内钢种", "名义规格", "状态", "备注" };
                foreach (var h in headers)
                    header.Cell().Element(CellHeaderStyle).Text(h).FontSize(8).AlignCenter();
            });

            int seq = 0;
            foreach (var m in materials)
            {
                seq++;
                table.Cell().Element(CellStyle).Text(seq.ToString()).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(m.MaterialCode).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(m.MaterialCategory).FontSize(8);
                table.Cell().Element(CellStyle).Text(m.PlantGrade).FontSize(8);
                table.Cell().Element(CellStyle).Text(m.Specification).FontSize(8);
                table.Cell().Element(CellStyle).Text(m.IsActive ? "启用" : "停用").FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(m.Remark ?? "-").FontSize(8);
            }
        });
    }

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
