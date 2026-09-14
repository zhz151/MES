using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MES.Core.Constants;
using MES.Core.Helpers;
using MES.Data.Entities.Quality;

namespace MES.Services.Printing;

/// <summary>
/// 巡检单 PDF 打印模板（QuestPDF — 单据/富布局），按巡检单逐条成页，含巡检照片与整改验证照片原图。
/// 整改块仅当「涉及整改」为是时渲染；工序名称/工段名称以配置表映射转中文，缺配置兜底常量规范中文。
/// </summary>
public static class InspectionPatrolPrintHelper
{
    /// <summary>照片（磁盘原图字节 + 原始文件名），由调用方读取后传入</summary>
    public sealed class PrintImage
    {
        public string FileName { get; init; } = "";
        public byte[] Data { get; init; } = Array.Empty<byte>();
    }

    /// <summary>
    /// 单张照片占位高度（pt）。2026-09-12 二次拍板「单列 + 每页 2 张」（原每行 2 张 / 120pt 挤 6 张）：
    /// 照片单列铺满页宽，高度 290pt 时 A4 竖版正文（≈709pt）首页容纳「归属行 + 组标题行 + 2 张」
    /// （≈29 + 18 + 2×(290+27) ≈ 681pt，留 ~25pt 余量），其余张数自动续页。
    /// ⚠️ 取 290 而非 300：本单照片表多一个「区内组标题行」，300pt 时余量仅 ~5pt 易随引擎版本飘页。
    /// ⚠️ 照片实际尺寸只由高度决定（FitArea 等比缩放，宽度受高度约束）。
    /// </summary>
    private const float ImageBoxHeight = 290f;

    public static byte[] GeneratePdf(
        InspectionPatrol entity,
        IReadOnlyList<InspectionPatrolItem> items,
        IReadOnlyList<PrintImage> patrolImages,
        IReadOnlyList<PrintImage> rectificationImages,
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
                page.Content().Element(c => ComposeContent(
                    c, entity, items, patrolImages, rectificationImages, processNameMap, sectionNameMap));
                page.Footer().Element(ComposeDocFooter);
            });
        }).GeneratePdf();
    }

    private static void ComposeDocHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(5).AlignCenter().Text("巡 检 单")
                .FontSize(20).Bold().FontColor(Colors.Blue.Darken3);
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
        InspectionPatrol n,
        IReadOnlyList<InspectionPatrolItem> items,
        IReadOnlyList<PrintImage> patrolImages,
        IReadOnlyList<PrintImage> rectificationImages,
        IReadOnlyDictionary<string, string>? processNameMap,
        IReadOnlyDictionary<string, string>? sectionNameMap)
    {
        container.Column(col =>
        {
            // 单号 + 闭环状态
            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().AlignLeft().Text($"编号: XJ-{n.Id:D4}").FontSize(11).Bold();
                row.RelativeItem().AlignRight()
                    .Text(n.NeedRectification ? (n.IsClosed ? "整改已闭环" : "整改未闭环") : "无需整改")
                    .FontSize(11).Bold()
                    .FontColor(n.NeedRectification
                        ? (n.IsClosed ? Colors.Green.Darken2 : Colors.Orange.Darken2)
                        : Colors.Grey.Darken2);
            });

            // G1 巡检信息
            ComposeSection(col, "G1 巡检信息", "1565c0", "e3f2fd", table =>
            {
                AppendFieldRow(table, "巡检日期", n.PatrolDate.ToString("yyyy-MM-dd"),
                    "巡检人", OperatorNameHelper.ToNamesOnly(n.Inspector));
                AppendFieldRow(table, "数据来源", StringEnumDisplayHelper.GetDataSourceText(n.DataSource),
                    "涉及整改", n.NeedRectification ? "是" : "否");
            });

            // G2 位置信息
            ComposeSection(col, "G2 位置信息", "f57c00", "fff8e1", table =>
            {
                AppendFieldRow(table, "生产编号", n.BatchNo, "工单号", n.WorkOrderNo ?? "");
                AppendFieldRow(table, "工序名称", ProcessDisplayText(n.ProcessName, processNameMap),
                    "工段名称", SectionDisplayText(n.SectionName, sectionNameMap));
                AppendFieldRow(table, "工厂牌号", n.PlantGrade ?? "", "制造规格", n.ManufacturingSpec ?? "");
                AppendFieldSpan(table, "产类", DictValueDisplayHelper.GetText(DictValueDefaults.ProductStatus, n.ProductStatus) ?? "");
                AppendFieldRow(table, "在产单位/车间", n.ProductionUnit ?? "", "在产设备名", n.EquipmentName ?? "");
                AppendFieldSpan(table, "在产操作人", OperatorNameHelper.ToNamesOnly(n.ProductionOperator));
            });

            // G3 巡检明细
            ComposeItemTable(col, items);

            // G4 整改闭环（仅涉及整改时）
            if (n.NeedRectification)
            {
                ComposeSection(col, "G4 整改闭环", "c62828", "ffebee", table =>
                {
                    AppendFieldSpan(table, "整改内容描述", n.RectificationDescription ?? "");
                    AppendFieldSpan(table, "验证结果", n.VerificationResult ?? "");
                    AppendFieldRow(table, "是否闭环", n.IsClosed ? "是" : "否", "", "");
                });
            }

            // 审计（留在字段页末尾）
            col.Item().PaddingTop(10).PaddingBottom(4)
                .AlignCenter().Text(t =>
                {
                    t.Span($"创建时间: {n.CreatedTime.LocalDateTime:yyyy-MM-dd HH:mm} | 更新时间: {n.UpdatedTime.LocalDateTime:yyyy-MM-dd HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                });

            // 照片（统一另起第 2 页；巡检照片 / 整改验证照片 合并为同一张连续表格）
            ComposePhotoPage(col, $"编号: XJ-{n.Id:D4}", patrolImages,
                n.NeedRectification ? rectificationImages : Array.Empty<PrintImage>());
        });
    }

    /// <summary>巡检明细表（序号 / 巡检项 / 巡检结果 / 备注），无明细时整区省略</summary>
    private static void ComposeItemTable(ColumnDescriptor col, IReadOnlyList<InspectionPatrolItem> items)
    {
        if (items.Count == 0) return;

        ComposeSectionTitle(col, $"G3 巡检明细（{items.Count} 项）", "2e7d32", "e8f5e9");

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(40);
                c.RelativeColumn(3);
                c.RelativeColumn(3);
                c.RelativeColumn(3);
            });

            table.Cell().Element(HeaderCellStyle).Text("序号").FontSize(9);
            table.Cell().Element(HeaderCellStyle).Text("巡检项").FontSize(9);
            table.Cell().Element(HeaderCellStyle).Text("巡检结果").FontSize(9);
            table.Cell().Element(HeaderCellStyle).Text("备注").FontSize(9);

            for (var i = 0; i < items.Count; i++)
            {
                var it = items[i];
                table.Cell().Element(CellStyle).Text((i + 1).ToString()).FontSize(9);
                table.Cell().Element(CellStyle).Text(it.ItemName).FontSize(9);
                table.Cell().Element(CellStyle).Text(it.Result ?? "").FontSize(9);
                table.Cell().Element(CellStyle).Text(it.Remark ?? "").FontSize(9);
            }
        });
    }

    /// <summary>
    /// 照片页：强制另起一页，页首归属行（编号 + 「照片（N 张）」）。
    /// 巡检照片与整改验证照片合并为同一张连续表格（区内以组标题行区分），照片单列铺排、每页 2 张超出自动续页；
    /// 两个区均无照片时整页省略（不产生空白页）。
    /// </summary>
    private static void ComposePhotoPage(
        ColumnDescriptor col,
        string ownerLabel,
        IReadOnlyList<PrintImage> patrolImages,
        IReadOnlyList<PrintImage> rectificationImages)
    {
        var total = patrolImages.Count + rectificationImages.Count;
        if (total == 0) return;

        col.Item().PageBreak();

        col.Item().PaddingTop(6).BorderLeft(4f).BorderColor(Color.FromHex("5c6bc0"))
            .Background(Color.FromHex("e8eaf6"))
            .PaddingVertical(4).PaddingHorizontal(8)
            .Row(row =>
            {
                row.RelativeItem().Text(ownerLabel).FontSize(11).Bold();
                row.RelativeItem().AlignRight().Text($"照片（{total} 张）")
                    .FontSize(9).FontColor(Colors.Grey.Darken2);
            });

        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(c => c.RelativeColumn());

            // 巡检照片区
            if (patrolImages.Count > 0)
            {
                AppendImageGroupHeader(table, $"巡检照片（{patrolImages.Count} 张）");
                AppendImageCells(table, patrolImages);
            }

            // 整改验证照片区（仅涉及整改时传入）
            if (rectificationImages.Count > 0)
            {
                AppendImageGroupHeader(table, $"整改验证照片（{rectificationImages.Count} 张）");
                AppendImageCells(table, rectificationImages);
            }
        });
    }

    /// <summary>照片组标题行（用于同一张连续表格内分区）</summary>
    private static void AppendImageGroupHeader(TableDescriptor table, string title)
    {
        table.Cell().Element(HeaderCellStyle).Text(title).FontSize(9);
    }

    /// <summary>照片单元格组：单列铺排（每页 2 张，超出自动续页）</summary>
    private static void AppendImageCells(TableDescriptor table, IReadOnlyList<PrintImage> images)
    {
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

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container.Border(0.5f).BorderColor(Colors.Grey.Medium)
            .Background(Colors.Grey.Lighten3)
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
}
