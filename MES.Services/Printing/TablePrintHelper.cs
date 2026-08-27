using System.Reflection;
using System.Text.Json;
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
using MES.Core.Exceptions;
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
    /// <summary>单列最小宽度(pt)：容纳两个中文字符(≈14pt) + 单元格左右 Padding(3*2=6) + 边框(0.5*2=1) + 余量。低于此值时单元格可读性差（单字符放不下 → QuestPDF 抛 DocumentLayoutException）</summary>
    private const float MinColumnWidth = 22f;
    /// <summary>可显示列数上限：每列保底 MinColumnWidth 时总宽不超过 A4 可用宽度（列更多时短内容列被压到过窄，单字符放不下 → DocumentLayoutException 裸 500）。floor((802-28)/22)=35</summary>
    private static readonly int MaxPrintColumns = (int)Math.Floor((AvailableWidth - SeqColumnWidth) / MinColumnWidth);

    /// <summary>
    /// 生成通用表格PDF
    /// </summary>
    /// <param name="title">PDF 标题</param>
    /// <param name="items">数据行</param>
    /// <param name="columns">列定义</param>
    /// <param name="valueResolvers">可选：列值自定义解析器（Key=列名, Func=原始值→显示文本），用于枚举转中文等场景</param>
    public static byte[] GeneratePdf<T>(string title, List<T> items, List<PrintColumnDef> columns,
        Dictionary<string, Func<object?, string>>? valueResolvers = null,
        bool autoWidth = true,
        bool alignCenter = true,
        int headerMaxLines = 0)
    {
        return CreateDocument(title, items, columns, valueResolvers, autoWidth, alignCenter, headerMaxLines).GeneratePdf();
    }

    /// <summary>
    /// 生成通用表格 PDF 文档（列表显示模式：内容自适应列宽 + 整页宽度铺满 + 数据居中 + 表头行数不限）。
    /// 返回 Document 供多文档 Merge（如按类型汇总一页一表后合并为一个 PDF）。
    /// </summary>
    public static Document CreateDocument<T>(string title, List<T> items, List<PrintColumnDef> columns,
        Dictionary<string, Func<object?, string>>? valueResolvers = null,
        bool autoWidth = true,
        bool alignCenter = true,
        int headerMaxLines = 0)
    {
        // 列数超限：列过多时各列被压缩到单字符放不下的宽度 → QuestPDF 抛 DocumentLayoutException(裸500)，统一转友好业务异常提示精简列
        if (columns.Count > MaxPrintColumns)
            throw new BusinessException($"打印列数过多（{columns.Count} 列），超出 A4 可显示列数上限 {MaxPrintColumns} 列（每列最窄需 {MinColumnWidth}pt 容纳内容），请通过列显隐精简后再打印");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("SimSun"));

                page.Header().Element(h => ComposeHeader(h, title));
                page.Content().Element(c => ComposeContent(c, items, columns, valueResolvers, autoWidth, alignCenter, headerMaxLines));
                page.Footer().Element(ComposeFooter);
            });
        });
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
        Dictionary<string, Func<object?, string>>? valueResolvers = null,
        bool autoWidth = false, bool alignCenter = false, int headerMaxLines = 0)
    {
        if (items.Count == 0)
        {
            container.AlignCenter().Text("暂无数据").FontSize(12);
            return;
        }

        // getters 提前计算（autoWidth 估算列宽需要）
        var getters = GetValueGetters<T>(columns, valueResolvers);

        container.Table(table =>
        {
            // 列定义：序号（固定）+ 动态列（autoWidth=按内容估算固定宽；否则按比例分配）
            table.ColumnsDefinition(columnsDef =>
            {
                columnsDef.ConstantColumn(28); // 序号
                if (autoWidth)
                {
                    foreach (var w in EstimateColumnWidths(items, columns, getters, headerMaxLines))
                        columnsDef.ConstantColumn(w);
                }
                else
                {
                    foreach (var col in columns)
                        columnsDef.RelativeColumn(col.Width ?? 1); // 用 Width 作为比例权重
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
            foreach (var item in items)
            {
                seq++;
                table.Cell().Element(CellStyle).Text(seq.ToString()).FontSize(7).AlignCenter();
                foreach (var col in columns)
                {
                    var value = getters[col.Key](item);
                    var cell = table.Cell().Element(CellStyle).Text(value).FontSize(7);
                    if (alignCenter) cell.AlignCenter();
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
            // HTTP 请求反序列化后 object 值为 JsonElement：取原始值（字符串去引号），避免输出 JSON 文本
            JsonElement je => je.ValueKind switch
            {
                JsonValueKind.String => je.GetString() ?? "",
                JsonValueKind.Null or JsonValueKind.Undefined => "",
                JsonValueKind.True => "是",
                JsonValueKind.False => "否",
                _ => je.GetRawText()
            },
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

    // ========== 内容自适应列宽（autoWidth 模式） ==========

    /// <summary>A4 横向内容区可用宽度(pt)：页宽 842 - margin 20*2</summary>
    private const float AvailableWidth = 802f;
    private const float SeqColumnWidth = 28f;

    /// <summary>按数据内容与表头估算每列宽度权重(pt)；表头最多 headerMaxLines 行（0=不限制单行优先）；列总宽始终铺满页面可用宽度（内容长则宽、短则窄）</summary>
    private static List<float> EstimateColumnWidths<T>(List<T> items, List<PrintColumnDef> columns,
        Dictionary<string, Func<T, string>> getters, int headerMaxLines)
    {
        var raw = new List<float>();
        foreach (var col in columns)
        {
            // 数据最长内容
            float maxData = 0f;
            foreach (var item in items)
                maxData = Math.Max(maxData, EstimateTextWidth(getters[col.Key](item)));

            // 表头：最多 headerMaxLines 行时每行至少容纳 ceil(len/headerMaxLines) 字符（Text 自动换行 → 不超过上限行数）
            var headerWidth = headerMaxLines > 0
                ? EstimateTextWidth(col.Label) / headerMaxLines
                : EstimateTextWidth(col.Label);

            // 内容宽 + 水平Padding(3*2) + 边框(0.5*2) + 余量
            raw.Add(Math.Max(maxData, headerWidth) + 7f);
        }

        // 列总宽始终铺满页面：内容自然宽作为分配权重整体等比缩放（不足铺满则放大、超出则压缩）
        var totalRaw = raw.Sum();
        if (totalRaw <= 0) return raw;

        // MinColumnWidth 为类级常量（22pt）：极窄列保底，容纳两个中文字符保证可读，否则 QuestPDF 抛 DocumentLayoutException；保底后总宽必须≤可用宽度
        var maxTotal = AvailableWidth - SeqColumnWidth;
        var scale = maxTotal / totalRaw;
        var widths = raw.Select(w => Math.Max(w * scale, MinColumnWidth)).ToList();

        // 保底抬升导致总宽超限（列多场景）时，从各列按可压缩空间等比收回，确保不抛 QuestPDF 布局冲突
        var overflow = widths.Sum() - maxTotal;
        if (overflow > 0)
        {
            var compressible = widths.Sum(w => w - MinColumnWidth);
            if (compressible > 0)
            {
                var factor = Math.Clamp(1 - overflow / compressible, 0f, 1f);
                widths = widths.Select(w => MinColumnWidth + (w - MinColumnWidth) * factor).ToList();
            }
        }
        return widths;
    }

    private static float EstimateTextWidth(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        float w = 0f;
        foreach (var c in text)
            w += c > 0xFF ? 7f : 4f; // FontSize(7) 下：中文≈7pt、半角≈4pt
        return w;
    }
}
