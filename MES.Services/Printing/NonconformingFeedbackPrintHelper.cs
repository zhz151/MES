using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Data.Entities.Quality;

namespace MES.Services.Printing;

/// <summary>
/// 不合格反馈单 PDF 打印模板（QuestPDF — 单据/富布局），按反馈单逐条成页，含问题照片原图。
/// 工序名称/工段名称以配置表映射（ProcessNameMap / SectionNameMap）转中文，缺配置兜底常量规范中文。
/// </summary>
public static class NonconformingFeedbackPrintHelper
{
    /// <summary>问题照片（磁盘原图字节 + 原始文件名），由调用方读取后传入</summary>
    public sealed class PrintImage
    {
        public string FileName { get; init; } = "";
        public byte[] Data { get; init; } = Array.Empty<byte>();
    }

    /// <summary>
    /// 单张照片占位高度（pt）。2026-09-12 二次拍板「单列 + 每页 2 张」（原每行 2 张 / 200pt）：
    /// 照片单列铺满页宽，高度 290pt 时 A4 竖版正文（≈709pt）恰好容纳「归属行 + 2 张」（≈2×(290+27)+33），
    /// 第 3 张自动续页。⚠️ 照片实际尺寸只由高度决定（FitArea 等比缩放，宽度受高度约束），
    /// 故「放大照片」的唯一手段是加大高度，而高度上限由每页张数决定。
    /// </summary>
    private const float ImageBoxHeight = 290f;

    public static byte[] GeneratePdf(
        NonconformingFeedback entity,
        IReadOnlyList<PrintImage> images,
        IReadOnlyDictionary<string, string>? processNameMap = null,
        IReadOnlyDictionary<string, string>? sectionNameMap = null)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Portrait());
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("SimSun"));

                page.Header().Element(ComposeDocHeader);
                page.Content().Element(c => ComposeContent(c, entity, images, processNameMap, sectionNameMap));
                page.Footer().Element(ComposeDocFooter);
            });
        }).GeneratePdf();
    }

    private static void ComposeDocHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(5).AlignCenter().Text("不 合 格 反 馈 单")
                .FontSize(20).Bold().FontColor(Colors.Red.Darken3);
        });
    }

    private static void ComposeDocFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingVertical(4)
                .LineHorizontal(1).LineColor(Colors.Black);
            col.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text($"打印日期：{DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(8);
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.Span("第 ").FontSize(8);
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" 页 / 共 ").FontSize(8);
                    t.TotalPages().FontSize(8);
                    t.Span(" 页").FontSize(8);
                });
            });
        });
    }

    private static void ComposeContent(
        IContainer container,
        NonconformingFeedback n,
        IReadOnlyList<PrintImage> images,
        IReadOnlyDictionary<string, string>? processNameMap,
        IReadOnlyDictionary<string, string>? sectionNameMap)
    {
        container.Column(col =>
        {
            // 单号
            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().AlignLeft().Text($"编号: FB-{n.Id:D4}").FontSize(11).Bold();
            });

            // G1 反馈信息
            ComposeSection(col, "G1 反馈信息", "d32f2f", "fff5f5", table =>
            {
                AppendFieldRow(table, "反馈日期", n.ReportDate.ToString("yyyy-MM-dd"),
                    "反馈人", OperatorNameHelper.ToNamesOnly(n.Reporter));
                AppendFieldRow(table, "数据来源", StringEnumDisplayHelper.GetDataSourceText(n.DataSource),
                    "来源类型", GetSourceTypeText(n.SourceType));
            });

            // G2 位置信息（工序名称 → 工段名称 → 工厂牌号 → 制造规格，对齐录入页字段顺序）
            ComposeSection(col, "G2 位置信息", "f57c00", "fff8e1", table =>
            {
                AppendFieldRow(table, "生产编号", n.BatchNo, "工单号", n.WorkOrderNo ?? "");
                AppendFieldRow(table, "工序名称", ProcessDisplayText(n.ProcessName, processNameMap),
                    "工段名称", SectionDisplayText(n.SectionName, sectionNameMap));
                AppendFieldRow(table, "工厂牌号", n.PlantGrade ?? "", "制造规格", n.ManufacturingSpec ?? "");
                AppendFieldRow(table, "产类", DictValueDisplayHelper.GetText(DictValueDefaults.ProductStatus, n.ProductStatus) ?? "",
                    "检验项目", GetInspectionItemText(n.InspectionItem));
            });

            // G3 数量信息
            ComposeSection(col, "G3 数量信息", "1976d2", "e3f2fd", table =>
            {
                AppendFieldRow(table, "来料支数", n.IncomingQuantity?.ToString() ?? "",
                    "来料重量(kg)", n.IncomingWeight?.ToString("G29") ?? "");
                AppendFieldRow(table, "不合格支数", n.DefectQuantity?.ToString() ?? "",
                    "不合格重量(kg)", n.DefectWeight?.ToString() ?? "");
            });

            // G4 问题信息
            ComposeSection(col, "G4 问题信息", "388e3c", "e8f5e9", table =>
            {
                AppendFieldSpan(table, "问题描述", n.ProblemDescription ?? "");
            });

            // 审计（留在字段页末尾）
            col.Item().PaddingTop(10).PaddingBottom(4)
                .AlignCenter().Text(t =>
                {
                    t.Span($"创建时间: {n.CreatedTime.LocalDateTime:yyyy-MM-dd HH:mm} | 更新时间: {n.UpdatedTime.LocalDateTime:yyyy-MM-dd HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                });

            // 问题照片（统一另起第 2 页，不挤压字段页版式）
            ComposeImages(col, $"编号: FB-{n.Id:D4}", "问题照片", images);
        });
    }

    /// <summary>
    /// 照片页：强制另起一页，页首归属行（编号 + 区名 + 张数），照片单列铺排（每页 2 张，超出自动续页）；
    /// 无照片时整页省略（不产生空白页）。
    /// </summary>
    private static void ComposeImages(ColumnDescriptor col, string ownerLabel, string title, IReadOnlyList<PrintImage> images)
    {
        if (images.Count == 0) return;

        col.Item().PageBreak();

        col.Item().PaddingTop(10).BorderLeft(4f).BorderColor(Color.FromHex("5c6bc0"))
            .Background(Color.FromHex("e8eaf6"))
            .PaddingVertical(4).PaddingHorizontal(8)
            .Row(row =>
            {
                row.RelativeItem().Text(ownerLabel).FontSize(11).Bold();
                row.RelativeItem().AlignRight().Text($"{title}（{images.Count} 张）")
                    .FontSize(9).FontColor(Colors.Grey.Darken2);
            });

        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(c => c.RelativeColumn());

            foreach (var img in images)
            {
                table.Cell().Element(CellStyle).Padding(4).Column(cell =>
                {
                    cell.Item().Height(ImageBoxHeight).AlignMiddle().AlignCenter()
                        .Image(img.Data).FitArea();
                    cell.Item().PaddingTop(2).AlignCenter()
                        .Text(img.FileName).FontSize(7).FontColor(Colors.Grey.Darken2);
                });
            }
        });
    }

    private static void ComposeSection(ColumnDescriptor col, string title, string borderColor, string bgColor, Action<TableDescriptor> buildFields)
    {
        ComposeSectionTitle(col, title, borderColor, bgColor);

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(90);
                c.RelativeColumn();
                c.ConstantColumn(90);
                c.RelativeColumn();
            });

            buildFields(table);
        });
    }

    private static void ComposeSectionTitle(ColumnDescriptor col, string title, string borderColor, string bgColor)
    {
        col.Item().PaddingTop(10).BorderLeft(4f).BorderColor(Color.FromHex(borderColor))
            .Background(Color.FromHex(bgColor))
            .PaddingVertical(4).PaddingHorizontal(8)
            .Text(title).FontSize(11).Bold();
    }

    private static void AppendFieldRow(TableDescriptor table, string label1, string value1, string label2, string value2)
    {
        table.Cell().Element(CellStyle).Background(Colors.Grey.Lighten4).Text(label1).FontSize(9);
        table.Cell().Element(CellStyle).Text(value1).FontSize(9);
        table.Cell().Element(CellStyle).Background(Colors.Grey.Lighten4).Text(label2).FontSize(9);
        table.Cell().Element(CellStyle).Text(value2).FontSize(9);
    }

    private static void AppendFieldSpan(TableDescriptor table, string label, string value)
    {
        table.Cell().Element(CellStyle).Background(Colors.Grey.Lighten4).Text(label).FontSize(9);
        table.Cell().ColumnSpan(3).Element(CellStyle).Text(value).FontSize(9);
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.Border(0.5f).BorderColor(Colors.Grey.Medium)
            .PaddingVertical(3).PaddingHorizontal(6)
            .AlignMiddle();
    }

    // ========== 显示名辅助 ==========

    /// <summary>工段 Key → 中文：配置表 map 优先，兜底 SectionKeys 规范中文（未知值原样返回）</summary>
    private static string SectionDisplayText(string? keyOrName, IReadOnlyDictionary<string, string>? sectionNameMap)
    {
        if (!string.IsNullOrEmpty(keyOrName) && sectionNameMap != null && sectionNameMap.TryGetValue(keyOrName, out var cn))
            return cn;
        return SectionKeys.ToChinese(keyOrName) ?? "";
    }

    /// <summary>工序 Key → 中文：配置表 map 优先，兜底 ProcessKeys 规范中文（未知值原样返回）</summary>
    private static string ProcessDisplayText(string? keyOrName, IReadOnlyDictionary<string, string>? processNameMap)
    {
        if (!string.IsNullOrEmpty(keyOrName) && processNameMap != null && processNameMap.TryGetValue(keyOrName, out var cn))
            return cn;
        return ProcessKeys.ToChinese(keyOrName) ?? "";
    }

    /// <summary>来源类型 → 中文（空/未知返回 "-"）</summary>
    private static string GetSourceTypeText(string? sourceType)
        => string.IsNullOrWhiteSpace(sourceType)
            ? "-"
            : EnumHelper.GetDisplayName<NonconformingFeedbackSourceType>(sourceType);

    /// <summary>检验项目 → 中文（空返回 "-"，仅成品检验来源有值）</summary>
    private static string GetInspectionItemText(InspectionItem? item)
        => item.HasValue ? EnumHelper.GetDisplayName(item.Value) : "-";
}
