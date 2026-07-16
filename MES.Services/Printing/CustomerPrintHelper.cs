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

namespace MES.Services.Printing;

/// <summary>
/// 客户档案 PDF 打印模板（QuestPDF）
/// </summary>
public static class CustomerPrintHelper
{
    public static byte[] GeneratePdf(CustomerProfileDto customer)
    {
        return GenerateBatchPdf(new List<CustomerProfileDto> { customer });
    }

    public static byte[] GenerateBatchPdf(List<CustomerProfileDto> customers)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("SimSun"));

                page.Header().Element(h => ComposeHeader(h, "客 户 档 案 列 表"));

                page.Content().Element(c => ComposeContent(c, customers));

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

    private static void ComposeContent(IContainer container, List<CustomerProfileDto> customers)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(28);   // 序号
                columns.ConstantColumn(80);   // 客户编码
                columns.ConstantColumn(60);   // 业务员
                columns.RelativeColumn();     // 客户单位
                columns.RelativeColumn();     // 最终客户
                columns.RelativeColumn();     // 联系人
                columns.ConstantColumn(80);   // 联系电话
                columns.ConstantColumn(50);   // 状态
                columns.RelativeColumn();     // 备注
            });

            table.Header(header =>
            {
                string[] headers = { "序号", "客户编码", "业务员", "客户单位", "最终客户", "联系人", "联系电话", "状态", "备注" };
                foreach (var h in headers)
                    header.Cell().Element(CellHeaderStyle).Text(h).FontSize(8).AlignCenter();
            });

            int seq = 0;
            foreach (var c in customers)
            {
                seq++;
                table.Cell().Element(CellStyle).Text(seq.ToString()).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(c.CustomerCode).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(c.Salesman).FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(c.CustomerUnit).FontSize(8);
                table.Cell().Element(CellStyle).Text(c.EndCustomer).FontSize(8);
                table.Cell().Element(CellStyle).Text(c.ContactPerson).FontSize(8);
                table.Cell().Element(CellStyle).Text(c.ContactPhone).FontSize(8);
                table.Cell().Element(CellStyle).Text(c.Status == CustomerStatus.Active ? "启用" : "停用").FontSize(8).AlignCenter();
                table.Cell().Element(CellStyle).Text(c.Remark ?? "-").FontSize(8);
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
