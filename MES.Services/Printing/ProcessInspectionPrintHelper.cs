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
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Data.Entities.Quality;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MES.Services.Printing;

public static class ProcessInspectionPrintHelper
{
    private static readonly Dictionary<string, Func<object?, string>> ValueResolvers = new()
    {
        ["DataSource"] = v => StringEnumDisplayHelper.GetDataSourceText(v?.ToString())
    };

    public static byte[] GenerateBatchPdf(List<ProcessInspectionDto> items, List<PrintColumnDef> columns,
        IReadOnlyDictionary<string, string>? processNameMap = null)
    {
        var resolvers = new Dictionary<string, Func<object?, string>>(ValueResolvers);
        if (processNameMap != null)
            resolvers["ProcessName"] = v => ProcessDisplayText(v?.ToString(), processNameMap);
        return TablePrintHelper.GeneratePdf("过程检验列表", items, columns, resolvers);
    }

    /// <summary>工序 Key/中文 → 打印显示中文（配置表 map 优先，ProcessKeys 兜底）</summary>
    private static string ProcessDisplayText(string? keyOrName, IReadOnlyDictionary<string, string>? processNameMap)
    {
        if (!string.IsNullOrEmpty(keyOrName) && processNameMap != null && processNameMap.TryGetValue(keyOrName, out var cn))
            return cn;
        return ProcessKeys.ToChinese(keyOrName) ?? "";
    }

    // ============================================================
    // 单据式打印（A4 竖版，每条记录一页，含检验照片）
    // ============================================================

    /// <summary>照片（磁盘原图字节 + 原始文件名），由调用方读取后传入</summary>
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

    // 紧凑排版档（2026-09-12 拍板）：本单据字段区行数多，压缩字号/行高确保「字段页」恒为单页，
    // 照片统一另起第 2 页（见 ComposeImages）。
    private const float FieldFontSize = 8f;
    private const float SectionTitleFontSize = 10f;
    private const float SectionTitlePaddingTop = 6f;
    private const float SectionTitlePaddingVertical = 3f;
    private const float CellPaddingVertical = 2f;

    /// <summary>
    /// 生成「过程检验记录」单据式 PDF：A4 竖版，每条记录独立成页（有照片时该记录占 2 页：字段页 + 照片页）。
    /// 照片按记录 Id 分组传入（无照片的记录不产生照片页）。
    /// </summary>
    public static byte[] GeneratePagePdf(
        IReadOnlyList<ProcessInspection> records,
        IReadOnlyDictionary<int, IReadOnlyList<PrintImage>>? imagesByRecord = null,
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
                page.Content().Element(c => ComposeDocContent(c, records, imagesByRecord, processNameMap, sectionNameMap));
                page.Footer().Element(ComposeDocFooter);
            });
        }).GeneratePdf();
    }

    private static void ComposeDocHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(5).AlignCenter().Text("过 程 检 验 记 录")
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

    private static void ComposeDocContent(
        IContainer container,
        IReadOnlyList<ProcessInspection> records,
        IReadOnlyDictionary<int, IReadOnlyList<PrintImage>>? imagesByRecord,
        IReadOnlyDictionary<string, string>? processNameMap,
        IReadOnlyDictionary<string, string>? sectionNameMap)
    {
        container.Column(col =>
        {
            for (var i = 0; i < records.Count; i++)
            {
                ComposeRecord(col, records[i], imagesByRecord, processNameMap, sectionNameMap);
                if (i < records.Count - 1) col.Item().PageBreak();
            }
        });
    }

    private static void ComposeRecord(
        ColumnDescriptor col,
        ProcessInspection n,
        IReadOnlyDictionary<int, IReadOnlyList<PrintImage>>? imagesByRecord,
        IReadOnlyDictionary<string, string>? processNameMap,
        IReadOnlyDictionary<string, string>? sectionNameMap)
    {
        // 记录编号 + 数据来源
        col.Item().PaddingTop(4).Row(row =>
        {
            row.RelativeItem().AlignLeft().Text($"编号: GCJY-{n.Id:D4}").FontSize(11).Bold();
            row.RelativeItem().AlignRight()
                .Text(StringEnumDisplayHelper.GetDataSourceText(n.DataSource))
                .FontSize(10).FontColor(Colors.Grey.Darken2);
        });

        // G1 基本信息
        ComposeSection(col, "G1 基本信息", "1565c0", "e3f2fd", table =>
        {
            AppendFieldRow(table, "生产编号", n.BatchNo ?? "", "工厂牌号", n.PlantGrade ?? "");
            AppendFieldRow(table, "工序名称", ProcessDisplayText(n.ProcessName, processNameMap),
                "制造规格", n.ManufacturingSpec ?? "");
            AppendFieldRow(table, "工段名称", SectionDisplayText(n.SectionName, sectionNameMap),
                "执行序号", n.SequenceNumber.ToString());
            AppendFieldRow(table, "产类", DictValueDisplayHelper.GetText(DictValueDefaults.ProductStatus, n.ProductStatus) ?? "",
                "挂牌号", n.TagNo ?? "");
        });

        // G2 检验执行
        ComposeSection(col, "G2 检验执行", "00695c", "e0f2f1", table =>
        {
            AppendFieldRow(table, "检验日期", n.InspectionDate.ToString("yyyy-MM-dd"),
                "班次", EnumHelper.GetDisplayName<ShiftType>(n.Shift));
            AppendFieldRow(table, "检验人", OperatorNameHelper.ToNamesOnly(n.Inspector), "检验设备", n.EquipmentName ?? "");
            AppendFieldRow(table, "检验项目", InspectionItemText(n.InspectionItem), "来料单位", n.SourceUnit ?? "");
        });

        // G3 检验数量
        ComposeSection(col, "G3 检验数量", "4527a0", "ede7f6", table =>
        {
            AppendFieldRow(table, "检验支数", n.Quantity?.ToString("G29") ?? "",
                "检验重量(kg)", n.Weight?.ToString("G29") ?? "");
        });

        // G4 合格
        ComposeSection(col, "G4 合格", "2e7d32", "e8f5e9", table =>
        {
            AppendFieldRow(table, "合格支数", n.QualifiedQuantity?.ToString("G29") ?? "",
                "合格重量(kg)", n.QualifiedWeight?.ToString("G29") ?? "");
            AppendFieldRow(table, "让步放行支数", n.QualifiedConcessionQuantity?.ToString("G29") ?? "",
                "让步说明", n.ConcessionRemark ?? "");
        });

        // G5 不合格（4 档 + 理论重量）
        ComposeSection(col, "G5 不合格", "c62828", "ffebee", table =>
        {
            AppendFieldRow(table, "返整支数", n.DefectReworkQuantity?.ToString("G29") ?? "",
                "理论返整重(kg)", n.TheoreticalReworkWeight?.ToString("G29") ?? "");
            AppendFieldRow(table, "入在制库支数", n.DefectWarehouseQuantity?.ToString("G29") ?? "",
                "理论入在制重(kg)", n.TheoreticalWarehouseWeight?.ToString("G29") ?? "");
            AppendFieldRow(table, "入次品库支数", n.DefectScrapQuantity?.ToString("G29") ?? "",
                "理论入次库重(kg)", n.TheoreticalScrapWeight?.ToString("G29") ?? "");
            AppendFieldRow(table, "退货支数", n.DefectReturnQuantity?.ToString("G29") ?? "",
                "理论退货重(kg)", n.TheoreticalReturnWeight?.ToString("G29") ?? "");
            AppendFieldSpan(table, "不合格情况描述", n.DefectDescription ?? "");
        });

        // G6 备注
        ComposeSection(col, "G6 备注", "37474f", "eceff1", table =>
        {
            AppendFieldSpan(table, "备注", n.Remark ?? "");
        });

        // 审计（留在字段页末尾）
        col.Item().PaddingTop(6).PaddingBottom(4)
            .AlignCenter().Text(t =>
            {
                t.Span($"创建时间: {n.CreatedTime.LocalDateTime:yyyy-MM-dd HH:mm} | 更新时间: {n.UpdatedTime.LocalDateTime:yyyy-MM-dd HH:mm}")
                    .FontSize(8).FontColor(Colors.Grey.Darken2);
            });

        // 检验照片（统一另起第 2 页，不挤压字段页版式）
        if (imagesByRecord != null && imagesByRecord.TryGetValue(n.Id, out var images))
            ComposeImages(col, $"编号: GCJY-{n.Id:D4}", "检验照片", images);
    }

    /// <summary>
    /// 照片页：强制另起一页，页首归属行（编号 + 区名 + 张数），照片单列铺排（每页 2 张，超出自动续页）；
    /// 无照片时整页省略（不产生空白页）。
    /// </summary>
    private static void ComposeImages(ColumnDescriptor col, string ownerLabel, string title, IReadOnlyList<PrintImage> images)
    {
        if (images.Count == 0) return;

        col.Item().PageBreak();

        col.Item().PaddingTop(SectionTitlePaddingTop).BorderLeft(4f).BorderColor(Color.FromHex("5c6bc0"))
            .Background(Color.FromHex("e8eaf6"))
            .PaddingVertical(SectionTitlePaddingVertical).PaddingHorizontal(8)
            .Row(row =>
            {
                row.RelativeItem().Text(ownerLabel).FontSize(SectionTitleFontSize).Bold();
                row.RelativeItem().AlignRight().Text($"{title}（{images.Count} 张）")
                    .FontSize(FieldFontSize).FontColor(Colors.Grey.Darken2);
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
        col.Item().PaddingTop(SectionTitlePaddingTop).BorderLeft(4f).BorderColor(Color.FromHex(borderColor))
            .Background(Color.FromHex(bgColor))
            .PaddingVertical(SectionTitlePaddingVertical).PaddingHorizontal(8)
            .Text(title).FontSize(SectionTitleFontSize).Bold();
    }

    private static void AppendFieldRow(TableDescriptor table, string label1, string value1, string label2, string value2)
    {
        table.Cell().Element(CellStyle).Background(Colors.Grey.Lighten4).Text(label1).FontSize(FieldFontSize);
        table.Cell().Element(CellStyle).Text(value1).FontSize(FieldFontSize);
        table.Cell().Element(CellStyle).Background(Colors.Grey.Lighten4).Text(label2).FontSize(FieldFontSize);
        table.Cell().Element(CellStyle).Text(value2).FontSize(FieldFontSize);
    }

    private static void AppendFieldSpan(TableDescriptor table, string label, string value)
    {
        table.Cell().Element(CellStyle).Background(Colors.Grey.Lighten4).Text(label).FontSize(FieldFontSize);
        table.Cell().ColumnSpan(3).Element(CellStyle).Text(value).FontSize(FieldFontSize);
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.Border(0.5f).BorderColor(Colors.Grey.Medium)
            .PaddingVertical(CellPaddingVertical).PaddingHorizontal(6)
            .AlignMiddle();
    }

    /// <summary>工段 Key → 中文：配置表 map 优先，兜底 SectionKeys 规范中文</summary>
    private static string SectionDisplayText(string? keyOrName, IReadOnlyDictionary<string, string>? sectionNameMap)
    {
        if (!string.IsNullOrEmpty(keyOrName) && sectionNameMap != null && sectionNameMap.TryGetValue(keyOrName, out var cn))
            return cn;
        return SectionKeys.ToChinese(keyOrName) ?? "";
    }

    /// <summary>检验项目（实体存枚举名）→ 中文显示名</summary>
    private static string InspectionItemText(string? value)
    {
        var item = EnumHelper.TryParse<InspectionItem>(value);
        return item.HasValue ? EnumHelper.GetDisplayName(item.Value) : (value ?? "");
    }
}
