using System.Reflection;
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
using MES.Core.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MES.Services.Printing;

/// <summary>
/// 通用表格打印帮助类，根据列定义动态生成PDF
/// </summary>
public static class TablePrintHelper
{
    /// <summary>
    /// 生成通用表格PDF
    /// </summary>
    /// <param name="title">PDF 标题</param>
    /// <param name="items">数据行</param>
    /// <param name="columns">列定义</param>
    /// <param name="valueResolvers">可选：列值自定义解析器（Key=列名, Func=原始值→显示文本），用于枚举转中文等场景</param>
    public static byte[] GeneratePdf<T>(string title, List<T> items, List<PrintColumnDef> columns,
        Dictionary<string, Func<object?, string>>? valueResolvers = null)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("SimSun"));

                page.Header().Element(h => ComposeHeader(h, title));
                page.Content().Element(c => ComposeContent(c, items, columns, valueResolvers));
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

    private static void ComposeContent<T>(IContainer container, List<T> items, List<PrintColumnDef> columns,
        Dictionary<string, Func<object?, string>>? valueResolvers = null)
    {
        if (items.Count == 0)
        {
            container.AlignCenter().Text("暂无数据").FontSize(12);
            return;
        }

        container.Table(table =>
        {
            // 列定义：序号 + 动态列
            table.ColumnsDefinition(columnsDef =>
            {
                columnsDef.ConstantColumn(28); // 序号
                foreach (var col in columns)
                {
                    if (col.Width is > 0)
                        columnsDef.ConstantColumn(col.Width.Value);
                    else
                        columnsDef.RelativeColumn();
                }
            });

            // 表头
            table.Header(header =>
            {
                header.Cell().Element(CellHeaderStyle).Text("序号").FontSize(7).AlignCenter();
                foreach (var col in columns)
                    header.Cell().Element(CellHeaderStyle).Text(col.Label).FontSize(7).AlignCenter();
            });

            // 数据行
            int seq = 0;
            var getters = GetValueGetters<T>(columns, valueResolvers);
            foreach (var item in items)
            {
                seq++;
                table.Cell().Element(CellStyle).Text(seq.ToString()).FontSize(7).AlignCenter();
                foreach (var col in columns)
                {
                    var value = getters[col.Key](item);
                    table.Cell().Element(CellStyle)
                        .Text(value)
                        .FontSize(7);
                }
            }
        });
    }

    private static Dictionary<string, Func<T, string>> GetValueGetters<T>(List<PrintColumnDef> columns,
        Dictionary<string, Func<object?, string>>? valueResolvers = null)
    {
        var dict = new Dictionary<string, Func<T, string>>();
        var type = typeof(T);

        // 处理 Dictionary<string, object> 类型
        if (typeof(System.Collections.IDictionary).IsAssignableFrom(type) ||
            type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
        {
            foreach (var col in columns)
            {
                dict[col.Key] = item =>
                {
                    // 有自定义解析器则优先使用
                    if (valueResolvers != null && valueResolvers.TryGetValue(col.Key, out var resolver))
                    {
                        var dictItem = (IDictionary<string, object>)item!;
                        dictItem.TryGetValue(col.Key, out var raw);
                        return resolver(raw);
                    }
                    var dictItem2 = (IDictionary<string, object>)item!;
                    if (dictItem2.TryGetValue(col.Key, out var raw2) && raw2 != null && raw2 != DBNull.Value)
                        return FormatValue(raw2);
                    return "";
                };
            }
        }
        else
        {
            // 处理普通模型类
            foreach (var col in columns)
            {
                var prop = type.GetProperty(col.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null)
                {
                    dict[col.Key] = item =>
                    {
                        // 有自定义解析器则优先使用
                        if (valueResolvers != null && valueResolvers.TryGetValue(col.Key, out var resolver))
                        {
                            var raw = prop.GetValue(item);
                            return resolver(raw);
                        }
                        var raw2 = prop.GetValue(item);
                        if (raw2 == null || raw2 == DBNull.Value) return "";
                        return FormatValue(raw2);
                    };
                }
                else
                {
                    dict[col.Key] = _ => "";
                }
            }
        }

        return dict;
    }

    private static string FormatValue(object raw)
    {
        return raw switch
        {
            DateTime dt => dt.ToString("yyyy-MM-dd"),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd"),
            bool b => b ? "是" : "否",
            decimal d => d.ToString("G29"),
            int i => i.ToString(),
            long l => l.ToString(),
            _ when raw.GetType().IsEnum => EnumHelper.GetDisplayName(raw.GetType(), raw),
            _ => raw.ToString() ?? ""
        };
    }

    private static IContainer CellHeaderStyle(IContainer container)
    {
        return container.Border(0.5f).BorderColor(Colors.Black)
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(3).PaddingHorizontal(3)
            .AlignMiddle();
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.Border(0.5f).BorderColor(Colors.Grey.Medium)
            .PaddingVertical(2).PaddingHorizontal(3)
            .AlignMiddle();
    }
}
