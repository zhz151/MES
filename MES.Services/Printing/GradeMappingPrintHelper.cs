using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MES.Core.DTOs;

namespace MES.Services.Printing;

/// <summary>
/// 牌号对照 PDF 打印模板（QuestPDF）
/// </summary>
public static class GradeMappingPrintHelper
{
    public static byte[] GeneratePdf(StandardGradeMappingDto mapping)
    {
        return GenerateBatchPdf(new List<StandardGradeMappingDto> { mapping });
    }

    public static byte[] GenerateBatchPdf(List<StandardGradeMappingDto> mappings)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("SimSun"));

                page.Header().Element(h => ComposeHeader(h, "牌 号 对 照 列 表"));

                page.Content().Element(c => ComposeContent(c, mappings));

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

    private static void ComposeContent(IContainer container, List<StandardGradeMappingDto> mappings)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(28);   // 序号
                columns.ConstantColumn(100);  // 标准牌号
                columns.ConstantColumn(100);  // 工厂牌号
                columns.ConstantColumn(70);   // 密度
                columns.ConstantColumn(80);   // 热处理工艺（压窄）
                columns.ConstantColumn(50);   // 特殊材料
                columns.RelativeColumn();     // 备注（放宽）
            });

            table.Header(header =>
            {
                string[] headers = { "序号", "标准牌号", "工厂牌号", "密度(g/cm³)", "热处理工艺", "特殊材料", "备注" };
                foreach (var h in headers)
                    header.Cell().Element(CellHeaderStyle).Text(h).FontSize(8).AlignCenter();
            });

            int seq = 0;
            foreach (var m in mappings)
            {
                seq++;
                table.Cell().Element(CellStyle).Text(seq.ToString()).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(m.StandardGrade).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(m.PlantGrade).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(m.Density.ToString("F4")).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(m.HeatTreatment ?? "-").FontSize(8);
                table.Cell().Element(CellStyle).Text(m.SpecialMaterial ? "特殊" : "常规").FontSize(8).AlignCenter();
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
