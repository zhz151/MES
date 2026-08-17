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
using MES.Data.Entities;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Batch;
using MES.Services.Helpers;
using MES.Core.Constants;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MES.Services.Printing;

/// <summary>
/// 工艺流转卡 PDF 打印模板（QuestPDF）
/// A4 横向，表格行布局：
/// - 批次基本信息 → 多列1行
/// - 质量要求 → 多列1行
/// - 投料信息 → 多列1行
/// - 工单信息 → 多列2行
/// - 工序组 → 动态列表格
/// 列宽根据内容自动调整（AutoColumn）
/// </summary>
public static class ProcessCardPrintHelper
{
    public static byte[] GeneratePdf(string title, List<ProductionBatch> batches, List<ProcessCardColumnDef> columns, string? companyName = null, IReadOnlyDictionary<string, string>? sectionNameMap = null, IReadOnlyDictionary<string, string>? processNameMap = null, IReadOnlyDictionary<string, string>? style = null)
    {
        var visibleCols = columns.Where(c => c.Visible).ToList();

        return Document.Create(container =>
        {
            foreach (var batch in batches)
            {
                var groups = batch.ProcessGroups.OrderBy(pg => pg.SequenceNumber).ToList();

                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(12);
                    page.DefaultTextStyle(x => x
                        .FontSize(GetStyleFloat(style, ProcessCardStyleKeys.PageFontSize, 9))
                        .FontFamily(GetStyleString(style, ProcessCardStyleKeys.PageFontFamily, "华文仿宋")));

                    var displayTitle = string.IsNullOrEmpty(companyName) ? title : $"{companyName} - {title}";
                    page.Header().Element(h => ComposeHeader(h, displayTitle, batch, style));
                    page.Content().Element(c => ComposeContent(c, batch, groups, visibleCols, style, sectionNameMap, processNameMap));
                });
            }
        }).GeneratePdf();
    }

    // ========== 页眉 ==========

    private static void ComposeHeader(IContainer container, string title, ProductionBatch batch, IReadOnlyDictionary<string, string>? style)
    {
        var qrBytes = QRCodeHelper.GeneratePng(batch.BatchNo);

        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                // 左占位：与右侧生产编号列等宽，保证中间内容视觉上整行居中
                row.RelativeItem();
                // 中间：二维码在左、主标题在右，二者整体居中
                row.RelativeItem(2).AlignCenter().Row(inner =>
                {
                    inner.AutoItem().Width(60).Height(60).Image(qrBytes);
                    inner.AutoItem().PaddingLeft(6).PaddingRight(6).AlignMiddle().Text(title)
                        .FontSize(GetStyleFloat(style, ProcessCardStyleKeys.HeaderFontSize, 20))
                        .Bold().FontFamily(GetStyleString(style, ProcessCardStyleKeys.HeaderFontFamily, "SimSun"));
                    // 标题右侧对称二维码
                    inner.AutoItem().Width(60).Height(60).Image(qrBytes);
                });
                // 右侧：生产编号（垂直居中显示）
                row.RelativeItem().AlignRight().AlignMiddle().Text(t =>
                {
                    t.Span("生产编号：").Bold().FontSize(GetStyleFloat(style, ProcessCardStyleKeys.BatchNoFontSize, 12));
                    t.Span(batch.BatchNo).FontSize(GetStyleFloat(style, ProcessCardStyleKeys.BatchNoFontSize, 12));
                });
            });

            col.Item().PaddingVertical(2)
                .LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    // ========== 内容 ==========

    private static void ComposeContent(IContainer container, ProductionBatch batch, List<ProcessGroup> groups, List<ProcessCardColumnDef> visibleCols, IReadOnlyDictionary<string, string>? style, IReadOnlyDictionary<string, string>? sectionNameMap = null, IReadOnlyDictionary<string, string>? processNameMap = null)
    {
        container.Column(col =>
        {
            bool anyBlockRendered = false;

            // 各区块行/列顺序/列宽权重全部由列定义（RowIndex/ColumnIndex/ColumnWeight）动态驱动，
            // 经格式设置（ProcessCardColumnDefinition 配置表）可调，不再硬编码 rows/rowCols/rowColRatios。

            // Block 1: 批次基本信息（多行）
            var batchInfoFields = GetBatchInfoFields(batch, visibleCols, sectionNameMap, processNameMap);
            if (batchInfoFields.Count > 0)
            {
                col.Item().Element(c => ComposeBlockTable(c, "批次基本信息", batchInfoFields, style));
                anyBlockRendered = true;
            }

            // Block 2: 质量要求
            var qualityFields = GetQualityFields(batch, visibleCols);
            if (qualityFields.Count > 0)
            {
                if (anyBlockRendered) col.Item().PaddingTop(1);
                col.Item().Element(c => ComposeBlockTable(c, "质量要求", qualityFields, style));
                anyBlockRendered = true;
            }

            // Block 3: 投料信息
            var warehouseFields = GetWarehouseFields(batch, visibleCols);
            if (warehouseFields.Count > 0)
            {
                if (anyBlockRendered) col.Item().PaddingTop(1);
                col.Item().Element(c => ComposeBlockTable(c, "投料信息", warehouseFields, style));
                anyBlockRendered = true;
            }

            // Block 4: 工单信息（多行）
            var workOrderFields = GetWorkOrderFields(batch, visibleCols);
            if (workOrderFields.Count > 0)
            {
                if (anyBlockRendered) col.Item().PaddingTop(1);
                col.Item().Element(c => ComposeBlockTable(c, "工单信息", workOrderFields, style));
                anyBlockRendered = true;
            }

            // Block 5: 工序组（独立表格，列顺序/权重按配置动态）
            var pgColumns = GetProcessGroupColumns(visibleCols);
            if (pgColumns.Count > 0 && groups.Count > 0)
            {
                if (anyBlockRendered) col.Item().PaddingTop(1);
                col.Item().Element(c => ComposeBlockTitle(c, "工序组", style));
                col.Item().Element(c => ComposeProcessGroupTable(c, groups, pgColumns, style, processNameMap));
            }

        });
    }

    // ========== 区块组件 ==========

    /// <summary>独立区块标题（用于工序组）</summary>
    private static void ComposeBlockTitle(IContainer container, string title, IReadOnlyDictionary<string, string>? style)
    {
        container.Background(Colors.Grey.Lighten4)
            .Border(0.3f).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(1).PaddingHorizontal(6)
            .Text(title).FontSize(GetStyleFloat(style, ProcessCardStyleKeys.BlockTitleFontSize, 10))
            .Bold().FontColor(Colors.Grey.Darken3);
    }

    /// <summary>
    /// 区块数据表格：标题行 + 按所属行（RowIndex）分组逐行渲染。
    /// 每行字段按列顺序（ColumnIndex）升序排列，列宽按列权重（ColumnWeight）比例分配。
    /// </summary>
    private static void ComposeBlockTable(IContainer container, string blockTitle, List<FieldEntry> fields, IReadOnlyDictionary<string, string>? style)
    {
        if (fields.Count == 0) return;

        container.Column(col =>
        {
            // 标题行
            col.Item().Element(TitleCellStyle)
                .Text(blockTitle).FontSize(GetStyleFloat(style, ProcessCardStyleKeys.BlockTitleFontSize, 10)).Bold();

            // 按所属行分组（RowIndex 升序），每行字段按 ColumnIndex 升序
            foreach (var rowFields in fields.GroupBy(f => f.RowIndex).OrderBy(g => g.Key))
            {
                var rowItems = rowFields.OrderBy(f => f.ColumnIndex).ToList();

                col.Item().Table(tb =>
                {
                    tb.ColumnsDefinition(cd =>
                    {
                        foreach (var f in rowItems)
                            cd.RelativeColumn(Math.Max(1, f.ColumnWeight));
                    });

                    // 表头子行
                    foreach (var f in rowItems)
                        tb.Cell().Element(HeaderCellStyle).Text(f.Label)
                            .FontSize(GetStyleFloat(style, ProcessCardStyleKeys.TableHeaderFontSize, 8.5f)).Bold().AlignCenter();

                    // 数据子行
                    foreach (var f in rowItems)
                        tb.Cell().Element(DataCellStyle).Text(f.Value)
                            .FontSize(GetStyleFloat(style, ProcessCardStyleKeys.CellFontSize, 8.5f)).AlignCenter();
                });
            }
        });
    }

    /// <summary>工序组动态列表格（列顺序/权重按配置 ColumnIndex/ColumnWeight 动态）</summary>
    private static void ComposeProcessGroupTable(IContainer container, List<ProcessGroup> groups, List<(string Key, string Label, int ColumnIndex, int ColumnWeight)> columns, IReadOnlyDictionary<string, string>? style, IReadOnlyDictionary<string, string>? processNameMap = null)
    {
        if (groups.Count == 0 || columns.Count == 0) return;

        container.Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(20);
                foreach (var (_, _, _, weight) in columns)
                    cd.RelativeColumn(Math.Max(1, weight));
            });

            // 表头
            table.Header(header =>
            {
                header.Cell().Element(CellHeaderStyle).Text("#")
                    .FontSize(GetStyleFloat(style, ProcessCardStyleKeys.TableHeaderFontSize, 8.5f)).AlignCenter();
                foreach (var (_, label, _, _) in columns)
                    header.Cell().Element(CellHeaderStyle).Text(label)
                        .FontSize(GetStyleFloat(style, ProcessCardStyleKeys.TableHeaderFontSize, 8.5f)).AlignCenter();
            });

            // 数据行
            int seq = 0;
            foreach (var g in groups)
            {
                seq++;
                table.Cell().Element(CellStyle).Text(seq.ToString())
                    .FontSize(GetStyleFloat(style, ProcessCardStyleKeys.CellFontSize, 8.5f)).AlignCenter();
                foreach (var (key, _, _, _) in columns)
                {
                    var value = GetProcessGroupFieldValue(g, key, processNameMap);
                    table.Cell().Element(CellStyle).Text(value)
                        .FontSize(GetStyleFloat(style, ProcessCardStyleKeys.CellFontSize, 8.5f)).AlignCenter();
                }
            }
        });
    }

    // ========== 单元格样式 ==========

    private static IContainer TitleCellStyle(IContainer container)
    {
        return container.Background(Colors.Grey.Lighten4)
            .Border(0.3f).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(1).PaddingHorizontal(6)
            .AlignMiddle();
    }

    private static IContainer DataCellStyle(IContainer container)
    {
        return container.Border(0.3f).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(1).PaddingHorizontal(6)
            .AlignMiddle();
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container.Border(0.3f).BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten4)
            .PaddingVertical(1).PaddingHorizontal(6)
            .AlignMiddle();
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.Border(0.3f).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(1).PaddingHorizontal(4)
            .AlignMiddle();
    }

    private static IContainer CellHeaderStyle(IContainer container)
    {
        return container.Border(0.3f).BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(1).PaddingHorizontal(3)
            .AlignMiddle();
    }

    // ========== 字段值提取 ==========

    private static List<FieldEntry> GetBatchInfoFields(ProductionBatch b, List<ProcessCardColumnDef> visibleCols, IReadOnlyDictionary<string, string>? sectionNameMap = null, IReadOnlyDictionary<string, string>? processNameMap = null)
    {
        var map = new Dictionary<string, (string Label, Func<string> Value)>
        {
            ["BatchNo"] = ("生产编号", () => b.BatchNo),
            ["Status"] = ("状态", () => EnumHelper.GetDisplayName(b.Status)),
            ["TagNo"] = ("挂牌号", () => b.TagNo ?? "-"),
            ["ProductionType"] = ("生产类型", () => Enum.TryParse<ProductionType>(b.ProductionType, out var pt) ? EnumHelper.GetDisplayName(pt) : (b.ProductionType ?? "-")),
            ["ProductionRatio"] = ("制成倍数", () => b.ProductionRatio.ToString()),
            ["IsForceCompleted"] = ("强制完成", () => b.IsForceCompleted ? "是" : "否"),
            ["Remark"] = ("备注", () => b.Remark ?? "-"),
            ["CurrentExecDate"] = ("截止执行日", () => FormatDate(b.CurrentExecDate)),
            ["ManufacturingItem"] = ("制造物品", () => EnumHelper.GetDisplayName<MaterialType>(b.ManufacturingItem)),
            ["ManufacturingStatus"] = ("制造状态", () => string.IsNullOrEmpty(b.ManufacturingStatus) ? "-" : (Enum.TryParse<DeliveryState>(b.ManufacturingStatus, out var ms) ? EnumHelper.GetDisplayName(ms) : b.ManufacturingStatus)),
            ["CurrentGroupName"] = ("当前工序", () => ProcessDisplayText(b.CurrentGroupName, processNameMap) ?? "-"),
            ["CurrentSectionName"] = ("当前工段", () => SectionDisplayText(b.CurrentSectionName, sectionNameMap) ?? "-"),
            ["CurrentEquipmentName"] = ("当前设备", () => b.CurrentEquipmentName ?? "-"),
            ["CurrentOutsource"] = ("当前委外", () => b.CurrentOutsource ?? "-"),
            ["CurrentSpec"] = ("当前规格", () => b.CurrentSpec ?? "-"),
            ["NextSectionName"] = ("下一工段", () => SectionDisplayText(b.NextSectionName, sectionNameMap) ?? "-"),
            ["CorrespondingSpec"] = ("对应规格", () => b.CorrespondingSpec ?? "-"),
            ["NextProcess"] = ("下一工序", () => ProcessDisplayText(b.NextProcess, processNameMap) ?? "-"),
            ["CreatedBy"] = ("创建人", () => b.CreatedBy ?? "-"),
            ["CreatedTime"] = ("创建时间", () => FormatDate(b.CreatedTime, "yyyy-MM-dd HH:mm")),
        };
        return BuildFieldList(visibleCols, "BatchInfo", map);
    }

    private static List<FieldEntry> GetQualityFields(ProductionBatch b, List<ProcessCardColumnDef> visibleCols)
    {
        var map = new Dictionary<string, (string Label, Func<string> Value)>
        {
            ["SolutionParams"] = ("固溶参数", () => b.SolutionParams ?? "-"),
            ["QualityRemark"] = ("质量备注", () => b.QualityRemark ?? "-"),
        };
        return BuildFieldList(visibleCols, "Quality", map);
    }

    private static List<FieldEntry> GetWarehouseFields(ProductionBatch b, List<ProcessCardColumnDef> visibleCols)
    {
        var map = new Dictionary<string, (string Label, Func<string> Value)>
        {
            ["SourceBatchNo"] = ("仓库批", () => b.SourceBatchNo ?? "-"),
            ["SourceMaterialType"] = ("原料类型", () => EnumHelper.GetDisplayName<MaterialType>(b.SourceMaterialType)),
            ["SourceName"] = ("来料单位", () => b.SourceName ?? "-"),
            ["SourceHeatNo"] = ("炉号", () => b.SourceHeatNo ?? "-"),
            ["SourcePlantGrade"] = ("工厂牌号", () => b.SourcePlantGrade ?? "-"),
            ["SourceSpecification"] = ("名义规格", () => b.SourceSpecification ?? "-"),
            ["SourceLengthStatus"] = ("长度状态", () => Enum.TryParse<LengthStatus>(b.SourceLengthStatus, out var ls) ? EnumHelper.GetDisplayName(ls) : (b.SourceLengthStatus ?? "-")),
            ["SourceUnitWeight"] = ("单支重(kg)", () => b.SourceUnitWeight?.ToString("G29") ?? "-"),
            ["InputQuantity"] = ("领料支数", () => b.InputQuantity?.ToString() ?? "-"),
            ["InputWeight"] = ("领料重量(kg)", () => b.InputWeight?.ToString("G29") ?? "-"),
            ["InputType"] = ("投料类型", () => EnumHelper.GetDisplayName<BatchInputType>(b.InputType)),
            ["SourceProductionNo"] = ("源生产编号", () => b.SourceProductionNo ?? "-"),
            ["SourceRemark"] = ("原料备注", () => b.SourceRemark ?? "-"),
            ["CurrentValidQty"] = ("有效原料支数", () => b.CurrentValidQty?.ToString() ?? "-"),
            ["CurrentValidWeight"] = ("有效原料重量(kg)", () => b.CurrentValidWeight?.ToString("G29") ?? "-"),
        };
        return BuildFieldList(visibleCols, "Warehouse", map);
    }

    private static List<FieldEntry> GetWorkOrderFields(ProductionBatch b, List<ProcessCardColumnDef> visibleCols)
    {
        var map = new Dictionary<string, (string Label, Func<string> Value)>
        {
            ["WorkOrderNo"] = ("工单号", () => b.WorkOrderNo),
            ["SalesOrderNo"] = ("源订单号", () => b.SalesOrderNo),
            ["ProductionMainNo"] = ("主号", () => b.ProductionMainNo),
            ["ProductionSubNo"] = ("次号", () => b.ProductionSubNo ?? "-"),
            ["OrderItemIds"] = ("项次ID", () => b.OrderItemIds),
            ["SignDate"] = ("签订日期", () => FormatDate(b.SignDate)),
            ["Salesman"] = ("业务员", () => b.Salesman),
            ["EndCustomer"] = ("最终用户", () => b.EndCustomer ?? "-"),
            ["DeliveryDate"] = ("交货日期", () => FormatDate(b.DeliveryDate)),
            ["DelayPenalty"] = ("延期罚款", () => b.DelayPenalty ? "是" : "否"),
            ["MaterialName"] = ("物料名称", () => Enum.TryParse<PipeManufacturingType>(b.MaterialName, out var pmt) ? EnumHelper.GetDisplayName(pmt) : (b.MaterialName ?? "-")),
            ["SettlementMethod"] = ("结算方式", () => Enum.TryParse<SettlementMethod>(b.SettlementMethod, out var sm) ? EnumHelper.GetDisplayName(sm) : (b.SettlementMethod ?? "-")),
            ["StandardCode"] = ("标准编码", () => b.StandardCode),
            ["DeliveryState"] = ("交货状态", () => Enum.TryParse<DeliveryState>(b.DeliveryState, out var ds) ? EnumHelper.GetDisplayName(ds) : (b.DeliveryState ?? "-")),
            ["PlantGrade"] = ("工厂牌号", () => b.PlantGrade),
            ["Specification"] = ("规格", () => b.Specification),
            ["OuterDiameterTolerance"] = ("外径公差", () => $"-{b.OuterDiameterNegative:G29}/+{b.OuterDiameterPositive:G29}"),
            ["WallThicknessTolerance"] = ("壁厚公差", () => $"-{b.WallThicknessNegative:G29}/+{b.WallThicknessPositive:G29}"),
            ["LengthStatus"] = ("长度状态", () => Enum.TryParse<LengthStatus>(b.LengthStatus, out var ls) ? EnumHelper.GetDisplayName(ls) : (b.LengthStatus ?? "-")),
            ["MinLength"] = ("最小长度(mm)", () => b.MinLength?.ToString("G29") ?? "-"),
            ["MaxLength"] = ("最大长度(mm)", () => b.MaxLength?.ToString("G29") ?? "-"),
            ["TotalQuantity"] = ("总支数", () => $"{b.TotalQuantity} 支"),
            ["TotalMeters"] = ("总米数(m)", () => b.TotalMeters.ToString("G29")),
            ["TotalWeight"] = ("总重量(kg)", () => b.TotalWeight.ToString("G29")),
            ["TotalItemCount"] = ("总项次数", () => b.TotalItemCount.ToString()),
            ["ItemDetails"] = ("明细", () => b.ItemDetails ?? "-"),
            ["TechnicalRequirements"] = ("技术要求", () => Enum.TryParse<RequirementType>(b.TechnicalRequirements, out var tr) ? EnumHelper.GetDisplayName(tr) : (b.TechnicalRequirements ?? "-")),
        };
        return BuildFieldList(visibleCols, "WorkOrder", map);
    }

    private static List<(string Key, string Label, int ColumnIndex, int ColumnWeight)> GetProcessGroupColumns(List<ProcessCardColumnDef> visibleCols)
    {
        return visibleCols
            .Where(c => c.BlockKey == "ProcessGroup")
            .OrderBy(c => c.ColumnIndex)
            .Select(c => (c.Key, c.Label, c.ColumnIndex, c.ColumnWeight))
            .ToList();
    }

    private static string GetProcessGroupFieldValue(ProcessGroup g, string key, IReadOnlyDictionary<string, string>? processNameMap = null)
    {
        return key switch
        {
            "ProcessName" => ProcessDisplayText(g.ProcessName, processNameMap),
            "ManufacturingSpec" => g.ManufacturingSpec ?? "-",
            "OuterDiameterTolerance" => g.OuterDiameterTolerance ?? "-",
            "WallThicknessTolerance" => g.WallThicknessTolerance ?? "-",
            "ManufacturingLength" => g.ManufacturingLength ?? "-",
            "CuttingTreatment" => g.CuttingTreatment ?? "-",
            "Remark" => g.Remark ?? "-",
            "ColdRollDraw" => g.ColdRollDraw?.ToString() ?? "-",
            "OilPipeCut" => g.OilPipeCut?.ToString() ?? "-",
            "Degrease" => g.Degrease?.ToString() ?? "-",
            "EmulsionWash" => g.EmulsionWash?.ToString() ?? "-",
            "UltrasonicWash" => g.UltrasonicWash?.ToString() ?? "-",
            "ClothPolish" => g.ClothPolish?.ToString() ?? "-",
            "BrightAnnealing" => g.BrightAnnealing?.ToString() ?? "-",
            "Solution" => g.Solution?.ToString() ?? "-",
            "Straighten" => g.Straighten?.ToString() ?? "-",
            "Cut" => g.Cut?.ToString() ?? "-",
            "ThicknessMeasure" => g.ThicknessMeasure?.ToString() ?? "-",
            "Pickle" => g.Pickle?.ToString() ?? "-",
            "OuterPolish" => g.OuterPolish?.ToString() ?? "-",
            "InnerPolish" => g.InnerPolish?.ToString() ?? "-",
            "InnerGrinding" => g.InnerGrinding?.ToString() ?? "-",
            "OuterSpotGrinding" => g.OuterSpotGrinding?.ToString() ?? "-",
            "SandBlasting" => g.SandBlasting?.ToString() ?? "-",
            "ShotBlasting" => g.ShotBlasting?.ToString() ?? "-",
            "Inspection" => g.Inspection?.ToString() ?? "-",
            "WeldingHead" => g.WeldingHead?.ToString() ?? "-",
            "Welding" => g.Welding?.ToString() ?? "-",
            "Lubrication" => g.Lubrication?.ToString() ?? "-",
            "Packing" => g.Packing?.ToString() ?? "-",
            "Warehouse" => g.Warehouse?.ToString() ?? "-",
            "Extra1" => g.Extra1?.ToString() ?? "-",
            "Extra2" => g.Extra2?.ToString() ?? "-",
            _ => "-"
        };
    }

    // ========== 辅助 ==========

    /// <summary>
    /// 版式配置数字解析：style 中缺项或非法值回退默认（字号非法不阻断打印），
    /// 配置源为 ProcessCardStyleDefinition 配置表（格式设置面板「打印版式」Tab）。
    /// </summary>
    private static float GetStyleFloat(IReadOnlyDictionary<string, string>? style, string key, float defaultValue)
        => style != null && style.TryGetValue(key, out var v) && float.TryParse(v, out var f) ? f : defaultValue;

    /// <summary>版式配置字符串取值：style 中缺项或空白值回退默认（字体族）</summary>
    private static string GetStyleString(IReadOnlyDictionary<string, string>? style, string key, string defaultValue)
        => style != null && style.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : defaultValue;

    /// <summary>
    /// 日期格式化：null 或默认值(0001-01-01)显示为空字符串，防止无效日期视觉污染；
    /// 其余按指定格式输出。
    /// </summary>
    private static string FormatDate(DateTime? value, string format = "yyyy-MM-dd")
        => value == null || value.Value == default(DateTime) ? string.Empty : value.Value.ToString(format);

    /// <summary>同 <see cref="FormatDate(DateTime?, string)"/>，支持 DateTimeOffset（如 CreatedTime）</summary>
    private static string FormatDate(DateTimeOffset? value, string format = "yyyy-MM-dd")
        => value == null || value.Value == default(DateTimeOffset) ? string.Empty : value.Value.ToString(format);

    /// <summary>区块字段布局条目：显示名/取值 + 所属行/列顺序/列权重（打印时按配置动态排版）</summary>
    private readonly record struct FieldEntry(string Label, string Value, int RowIndex, int ColumnIndex, int ColumnWeight);

    /// <summary>
    /// 按可见列定义构建字段列表：显示名取列定义 Label（配置表可改中文），行/列顺序/权重取列定义，
    /// 列顺序按 ColumnIndex 升序（稳定排序保留同序原始顺序）。
    /// </summary>
    private static List<FieldEntry> BuildFieldList(
        List<ProcessCardColumnDef> visibleCols,
        string blockKey,
        Dictionary<string, (string Label, Func<string> Value)> fieldMap)
    {
        var result = new List<FieldEntry>();
        foreach (var col in visibleCols
            .Where(c => c.BlockKey == blockKey)
            .OrderBy(c => c.ColumnIndex))
        {
            if (fieldMap.TryGetValue(col.Key, out var entry))
                result.Add(new FieldEntry(col.Label, entry.Value(), col.RowIndex, col.ColumnIndex, col.ColumnWeight));
        }
        return result;
    }

    /// <summary>
    /// 工段 Key → 中文：配置表 map 优先，兜底 SectionKeys 规范中文（未知值原样返回）。
    /// </summary>
    private static string? SectionDisplayText(string? keyOrName, IReadOnlyDictionary<string, string>? sectionNameMap)
    {
        if (!string.IsNullOrEmpty(keyOrName) && sectionNameMap != null && sectionNameMap.TryGetValue(keyOrName, out var cn))
            return cn;
        return SectionKeys.ToChinese(keyOrName);
    }

    /// <summary>
    /// 工序 Key → 中文：配置表 map 优先，兜底 ProcessKeys 规范中文（未知值原样返回）。
    /// </summary>
    private static string ProcessDisplayText(string? keyOrName, IReadOnlyDictionary<string, string>? processNameMap)
    {
        if (!string.IsNullOrEmpty(keyOrName) && processNameMap != null && processNameMap.TryGetValue(keyOrName, out var cn))
            return cn;
        return ProcessKeys.ToChinese(keyOrName) ?? "";
    }

}
