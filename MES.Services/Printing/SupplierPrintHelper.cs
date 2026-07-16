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

namespace MES.Services.Printing;

/// <summary>
/// 供应商档案 PDF 打印模板（QuestPDF）
/// </summary>
public static class SupplierPrintHelper
{
    public static byte[] GeneratePdf(SupplierProfileDto supplier)
    {
        return GenerateBatchPdf(new List<SupplierProfileDto> { supplier });
    }

    public static byte[] GenerateBatchPdf(List<SupplierProfileDto> suppliers)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("SimSun"));

                page.Header().Element(h => ComposeHeader(h, "供 应 商 档 案 列 表"));

                page.Content().Element(c => ComposeContent(c, suppliers));

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

    private static void ComposeContent(IContainer container, List<SupplierProfileDto> suppliers)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(28);   // 序号
                columns.ConstantColumn(70);   // 供应商编码
                columns.RelativeColumn();     // 供应商名称
                columns.ConstantColumn(70);   // 物料分类
                columns.RelativeColumn();     // 联系人
                columns.ConstantColumn(80);   // 联系电话
                columns.ConstantColumn(50);   // 状态
                columns.RelativeColumn();     // 备注
            });

            table.Header(header =>
            {
                string[] headers = { "序号", "供应商编码", "供应商名称", "物料分类", "联系人", "联系电话", "状态", "备注" };
                foreach (var h in headers)
                    header.Cell().Element(CellHeaderStyle).Text(h).FontSize(8).AlignCenter();
            });

            int seq = 0;
            foreach (var s in suppliers)
            {
                seq++;
                table.Cell().Element(CellStyle).Text(seq.ToString()).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(s.SupplierCode).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(s.SupplierName).FontSize(8);
                table.Cell().Element(CellStyle).Text(s.MaterialCategory ?? "-").FontSize(8);
                table.Cell().Element(CellStyle).Text(s.ContactPerson ?? "-").FontSize(8);
                table.Cell().Element(CellStyle).Text(s.ContactPhone ?? "-").FontSize(8);
                table.Cell().Element(CellStyle).Text(s.IsActive ? "启用" : "停用").FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(s.Remark ?? "-").FontSize(8);
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
