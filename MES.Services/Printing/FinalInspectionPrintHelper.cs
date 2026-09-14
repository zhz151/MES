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
using MES.Core.Helpers;
using MES.Data.Entities.Quality;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MES.Services.Printing;

public static class FinalInspectionPrintHelper
{
    private static readonly Dictionary<string, Func<object?, string>> ValueResolvers = new()
    {
        ["InspectionItem"] = v => v is InspectionItem item ? EnumHelper.GetDisplayName(item) : (v?.ToString() ?? ""),
        ["ProductionType"] = v => v is string s && !string.IsNullOrEmpty(s) && Enum.TryParse<ProductionType>(s, true, out var pt) ? EnumHelper.GetDisplayName(pt) : (v?.ToString() ?? ""),
        ["DataSource"] = v => StringEnumDisplayHelper.GetDataSourceText(v?.ToString()),
        ["LengthStatus"] = v => v is string s && !string.IsNullOrEmpty(s) && Enum.TryParse<LengthStatus>(s, true, out var ls) ? EnumHelper.GetDisplayName(ls) : (v?.ToString() ?? ""),
        ["CutLengthMatchType"] = v => v is CutLengthMatchType ct ? CutLengthMatchHelper.GetText(ct) : "",
        ["DeliveryState"] = v => v is string s && !string.IsNullOrEmpty(s) && Enum.TryParse<DeliveryState>(s, true, out var ds) ? EnumHelper.GetDisplayName(ds) : (v?.ToString() ?? "")
    };

    public static byte[] GenerateBatchPdf(List<FinalInspectionDto> items, List<PrintColumnDef> columns)
    {
        return TablePrintHelper.GeneratePdf("成品检验列表", items, columns, ValueResolvers);
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

    // 紧凑排版档（2026-09-12 拍板）：成检字段区含探伤参数时多达 24 行，压缩字号/行高确保「字段页」恒为单页，
    // 照片统一另起第 2 页（见 ComposeImages）。
    private const float FieldFontSize = 8f;
    private const float SectionTitleFontSize = 10f;
    private const float SectionTitlePaddingTop = 6f;
    private const float SectionTitlePaddingVertical = 3f;
    private const float CellPaddingVertical = 2f;

    /// <summary>
    /// 生成「成品检验记录」单据式 PDF：A4 竖版，每条记录独立成页（有照片时该记录占 2 页：字段页 + 照片页）。
    /// 专用参数区按检验项目条件渲染（尺寸 / 水压·水下气压 / 涡流·超声波），其余项目整区省略。
    /// </summary>
    public static byte[] GeneratePagePdf(
        IReadOnlyList<FinalInspection> records,
        IReadOnlyDictionary<int, IReadOnlyList<PrintImage>>? imagesByRecord = null)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Portrait());
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("SimSun"));

                page.Header().Element(ComposeDocHeader);
                page.Content().Element(c => ComposeDocContent(c, records, imagesByRecord));
                page.Footer().Element(ComposeDocFooter);
            });
        }).GeneratePdf();
    }

    private static void ComposeDocHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(5).AlignCenter().Text("成 品 检 验 记 录")
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
        IReadOnlyList<FinalInspection> records,
        IReadOnlyDictionary<int, IReadOnlyList<PrintImage>>? imagesByRecord)
    {
        container.Column(col =>
        {
            for (var i = 0; i < records.Count; i++)
            {
                ComposeRecord(col, records[i], imagesByRecord);
                if (i < records.Count - 1) col.Item().PageBreak();
            }
        });
    }

    private static void ComposeRecord(
        ColumnDescriptor col,
        FinalInspection n,
        IReadOnlyDictionary<int, IReadOnlyList<PrintImage>>? imagesByRecord)
    {
        // 记录编号 + 成检类型
        col.Item().PaddingTop(4).Row(row =>
        {
            row.RelativeItem().AlignLeft().Text($"编号: CPJY-{n.Id:D4}").FontSize(11).Bold();
            row.RelativeItem().AlignRight()
                .Text(InspectionTypeText(n.InspectionType))
                .FontSize(11).Bold().FontColor(Colors.Blue.Darken2);
        });

        // G1 检验信息
        ComposeSection(col, "G1 检验信息", "1565c0", "e3f2fd", table =>
        {
            AppendFieldRow(table, "生产编号", n.BatchNo, "检验项目", EnumHelper.GetDisplayName(n.InspectionItem));
            AppendFieldRow(table, "检验日期", n.InspectionDate.ToString("yyyy-MM-dd"),
                "班次", n.Shift.HasValue ? EnumHelper.GetDisplayName(n.Shift.Value) : "");
            AppendFieldRow(table, "操作人", OperatorNameHelper.ToNamesOnly(n.Operator), "检验设备", n.EquipmentName ?? "");
            AppendFieldRow(table, "数据来源", StringEnumDisplayHelper.GetDataSourceText(n.DataSource), "", "");
        });

        // G2 长度信息（定尺/非定尺互斥展示）
        ComposeSection(col, "G2 长度信息", "00695c", "e0f2f1", table =>
        {
            AppendFieldRow(table, "定尺长度", n.FixedLength ?? "",
                "非定尺长度范围", n.NonFixedLengthRange ?? "");
            AppendFieldRow(table, "定尺长度匹配", CutLengthMatchText(n.CutLengthMatchType), "", "");
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

        // G5 不合格（5 档 + 理论重量）
        ComposeSection(col, "G5 不合格", "c62828", "ffebee", table =>
        {
            AppendFieldRow(table, "返整支数", n.DefectReworkQuantity?.ToString("G29") ?? "",
                "理论返整重(kg)", n.DefectReworkWeight?.ToString("G29") ?? "");
            AppendFieldRow(table, "入在制库支数", n.DefectInProcessWarehouseQuantity?.ToString("G29") ?? "",
                "理论入在制重(kg)", n.DefectInProcessWarehouseWeight?.ToString("G29") ?? "");
            AppendFieldRow(table, "可入备库支数", n.DefectWarehouseQuantity?.ToString("G29") ?? "",
                "理论可入备库重(kg)", n.DefectWarehouseWeight?.ToString("G29") ?? "");
            AppendFieldRow(table, "入次品库支数", n.DefectScrapQuantity?.ToString("G29") ?? "",
                "理论入次库重(kg)", n.DefectScrapWeight?.ToString("G29") ?? "");
            AppendFieldRow(table, "退货支数", n.DefectReturnQuantity?.ToString("G29") ?? "",
                "理论退货重(kg)", n.DefectReturnWeight?.ToString("G29") ?? "");
            AppendFieldSpan(table, "不合格情况描述", n.DefectDescription ?? "");
        });

        // G6 专用参数（按检验项目条件渲染）
        ComposeItemSpecificSection(col, n);

        // G7 备注
        ComposeSection(col, "G7 备注", "37474f", "eceff1", table =>
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
            ComposeImages(col, $"编号: CPJY-{n.Id:D4}", "检验照片", images);
    }

    /// <summary>
    /// 专用参数区：仅渲染该检验项目实际使用的字段，其余项目整区省略。
    /// 与扫码报工表单的字段可见性条件保持一致。
    /// </summary>
    private static void ComposeItemSpecificSection(ColumnDescriptor col, FinalInspection n)
    {
        switch (n.InspectionItem)
        {
            case InspectionItem.Dimension:
                ComposeSection(col, "G6 尺寸偏差范围", "f57c00", "fff8e1", table =>
                {
                    AppendFieldRow(table, "外径范围", n.OuterDiameterRange ?? "",
                        "壁厚范围", n.WallThicknessRange ?? "");
                    AppendFieldRow(table, "长度余量范围", n.LengthAllowanceRange ?? "", "", "");
                });
                break;

            case InspectionItem.HydrostaticPressure:
            case InspectionItem.UnderwaterPneumatic:
                ComposeSection(col, "G6 压力参数", "f57c00", "fff8e1", table =>
                {
                    AppendFieldRow(table, "压力(Mpa)", n.Pressure?.ToString("G29") ?? "",
                        "保压时间(s)", n.HoldTime?.ToString("G29") ?? "");
                });
                break;

            case InspectionItem.EddyCurrent:
            case InspectionItem.Ultrasonic:
                ComposeSection(col, "G6 探伤参数", "f57c00", "fff8e1", table =>
                {
                    AppendFieldRow(table, "资格等级", n.QualificationLevel ?? "",
                        "检验标准", n.InspectionStandard ?? "");
                    AppendFieldRow(table, "验收等级", n.InspectionGrade ?? "",
                        "仪器型号", n.InstrumentModel ?? "");
                    AppendFieldRow(table, "检验方式", n.NdtMethod ?? "",
                        "标样尺寸", n.StandardSampleSize ?? "");
                    AppendFieldRow(table, "标样人工缺陷", n.StandardSampleDefect ?? "",
                        "探头类型", n.ProbeType ?? "");
                    AppendFieldRow(table, "耦合剂", n.Couplant ?? "",
                        "设备校准频率", n.CalibrationFrequency ?? "");
                    AppendFieldRow(table, "检测频率", n.DetectionFrequency ?? "",
                        "检测灵敏度", n.DetectionSensitivity ?? "");
                    AppendFieldRow(table, "检测相位", n.DetectionPhase ?? "",
                        "检测速度", n.DetectionSpeed ?? "");
                });
                break;
        }
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

    /// <summary>成检类型（实体存枚举名）→ 中文显示名</summary>
    private static string InspectionTypeText(string? value)
    {
        var type = EnumHelper.TryParse<InspectionType>(value);
        return type.HasValue ? EnumHelper.GetDisplayName(type.Value) : "";
    }

    /// <summary>定尺切割长度匹配标识（实体存枚举名）→ 中文显示名</summary>
    private static string CutLengthMatchText(string? value)
    {
        var match = EnumHelper.TryParse<CutLengthMatchType>(value);
        return match.HasValue ? CutLengthMatchHelper.GetText(match) : "";
    }
}
