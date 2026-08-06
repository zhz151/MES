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
    public static byte[] GeneratePdf(string title, List<ProductionBatch> batches, List<ProcessCardColumnDef> columns, string? companyName = null, IReadOnlyDictionary<string, string>? sectionNameMap = null, IReadOnlyDictionary<string, string>? processNameMap = null)
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
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("SimSun"));

                    var displayTitle = string.IsNullOrEmpty(companyName) ? title : $"{companyName} - {title}";
                    page.Header().Element(h => ComposeHeader(h, displayTitle, batch));
                    page.Content().Element(c => ComposeContent(c, batch, groups, visibleCols, sectionNameMap, processNameMap));
                });
            }
        }).GeneratePdf();
    }

    // ========== 页眉 ==========

    private static void ComposeHeader(IContainer container, string title, ProductionBatch batch)
    {
        var qrBytes = QRCodeHelper.GeneratePng(batch.BatchNo);

        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().AlignCenter().Row(inner =>
                {
                    inner.AutoItem().Text(title).FontSize(16).Bold();
                    inner.AutoItem().PaddingLeft(6).Width(80).Height(80).Image(qrBytes);
                });
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.Span("生产编号：").Bold().FontSize(12);
                    t.Span(batch.BatchNo).FontSize(12);
                });
            });

            col.Item().PaddingVertical(2)
                .LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    // ========== 内容 ==========

    private static void ComposeContent(IContainer container, ProductionBatch batch, List<ProcessGroup> groups, List<ProcessCardColumnDef> visibleCols, IReadOnlyDictionary<string, string>? sectionNameMap = null, IReadOnlyDictionary<string, string>? processNameMap = null)
    {
        container.Column(col =>
        {
            bool anyBlockRendered = false;

            // Block 1: 批次基本信息（多列2行）
            var batchInfoFields = GetBatchInfoFields(batch, visibleCols, sectionNameMap, processNameMap);
            if (batchInfoFields.Count > 0)
            {
                col.Item().Element(c => ComposeBlockTable(c, "批次基本信息", batchInfoFields, rows: 2, rowCols: new[] { 9, 11 },
                    rowColRatios: new[] { new[] { 2, 2, 2, 2, 1, 3, 2, 2, 2 }, Array.Empty<int>() }));
                anyBlockRendered = true;
            }

            // Block 2: 质量要求（多列1行）
            var qualityFields = GetQualityFields(batch, visibleCols);
            if (qualityFields.Count > 0)
            {
                if (anyBlockRendered) col.Item().PaddingTop(2);
                col.Item().Element(c => ComposeBlockTable(c, "质量要求", qualityFields, rows: 1,
                    rowColRatios: new[] { new[] { 1, 8 } }));
                anyBlockRendered = true;
            }

            // Block 3: 投料信息（多列1行）
            var warehouseFields = GetWarehouseFields(batch, visibleCols);
            if (warehouseFields.Count > 0)
            {
                if (anyBlockRendered) col.Item().PaddingTop(2);
                col.Item().Element(c => ComposeBlockTable(c, "投料信息", warehouseFields, rows: 1,
                    rowColRatios: new[] { new[] { 1, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 } }));
                anyBlockRendered = true;
            }

            // Block 4: 工单信息（多列3行）
            var workOrderFields = GetWorkOrderFields(batch, visibleCols);
            if (workOrderFields.Count > 0)
            {
                if (anyBlockRendered) col.Item().PaddingTop(2);
                col.Item().Element(c => ComposeBlockTable(c, "工单信息", workOrderFields, rows: 3, rowCols: new[] { 14, 11, 2 },
                    rowColRatios: new[] { new[] { 4, 3, 1, 1, 2, 2, 2, 4, 2, 2, 2, 2, 2, 2 }, Array.Empty<int>(), new[] { 1, 15 } }));
                anyBlockRendered = true;
            }

            // Block 5: 工序组（独立表格）
            var pgColumns = GetProcessGroupColumns(visibleCols);
            if (pgColumns.Count > 0 && groups.Count > 0)
            {
                if (anyBlockRendered) col.Item().PaddingTop(2);
                col.Item().Element(c => ComposeBlockTitle(c, "工序组"));

                // 列宽比：冷轧拔→入库（状态字段）为 1，其余描述字段为 3
                var narrowKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ColdRollDraw", "OilPipeCut", "Degrease", "EmulsionWash",
                    "UltrasonicWash", "ClothPolish", "BrightAnnealing", "Solution",
                    "Straighten", "Cut", "ThicknessMeasure", "Pickle",
                    "OuterPolish", "InnerPolish", "InnerGrinding", "OuterSpotGrinding",
                    "SandBlasting", "ShotBlasting", "Inspection", "WeldingHead",
                    "Welding", "Lubrication", "Packing", "Warehouse", "Extra1", "Extra2"
                };
                var ratios = pgColumns.Select(c =>
                {
                    if (c.Key.Equals("Remark", StringComparison.OrdinalIgnoreCase)) return 4;
                    return narrowKeys.Contains(c.Key) ? 1 : 3;
                }).ToArray();
                col.Item().Element(c => ComposeProcessGroupTable(c, groups, pgColumns, ratios, processNameMap));
            }

        });
    }

    // ========== 区块组件 ==========

    /// <summary>独立区块标题（用于工序组）</summary>
    private static void ComposeBlockTitle(IContainer container, string title)
    {
        container.Background(Colors.Grey.Lighten4)
            .Border(0.3f).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(2).PaddingHorizontal(6)
            .Text(title).FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
    }

    /// <summary>
    /// 区块数据表格：标题行 + 每行独立表头/数据子行（自动适配列数，无空白占位列）
    /// </summary>
    private static void ComposeBlockTable(IContainer container, string blockTitle, List<(string Label, string Value)> fields, int rows, int[]? rowCols = null, int[][]? rowColRatios = null)
    {
        if (fields.Count == 0) return;

        int[] colsPerRow;
        if (rowCols != null)
        {
            colsPerRow = rowCols;
        }
        else
        {
            int cpr = (int)Math.Ceiling((double)fields.Count / rows);
            colsPerRow = Enumerable.Repeat(cpr, rows).ToArray();
        }

        container.Column(col =>
        {
            // 标题行
            col.Item().Element(TitleCellStyle)
                .Text(blockTitle).FontSize(11).Bold();

            // 每行单独成表，列数按实际字段数动态分配
            int idx = 0;
            for (int r = 0; r < rows; r++)
            {
                if (idx >= fields.Count) break;

                int cpr = colsPerRow[r];
                int itemsInRow = Math.Min(cpr, fields.Count - idx);
                if (itemsInRow <= 0) break;

                int localCols = itemsInRow;
                int[]? ratios = rowColRatios?.Length > r ? rowColRatios[r] : null;

                col.Item().Table(tb =>
                {
                    tb.ColumnsDefinition(cd =>
                    {
                        if (ratios != null && ratios.Length >= localCols)
                        {
                            for (int i = 0; i < localCols; i++)
                                cd.RelativeColumn(ratios[i]);
                        }
                        else
                        {
                            for (int i = 0; i < localCols; i++)
                                cd.RelativeColumn();
                        }
                    });

                    // 表头子行
                    for (int i = 0; i < localCols; i++)
                    {
                        var (label, _) = fields[idx + i];
                        tb.Cell().Element(HeaderCellStyle).Text(label).FontSize(9).Bold().AlignCenter();
                    }

                    // 数据子行
                    for (int i = 0; i < localCols; i++)
                    {
                        var (_, value) = fields[idx + i];
                        tb.Cell().Element(DataCellStyle).Text(value).FontSize(9).AlignCenter();
                    }
                });

                idx += itemsInRow;
            }
        });
    }

    /// <summary>工序组动态列表格</summary>
    private static void ComposeProcessGroupTable(IContainer container, List<ProcessGroup> groups, List<(string Key, string Label)> columns, int[]? columnRatios = null, IReadOnlyDictionary<string, string>? processNameMap = null)
    {
        if (groups.Count == 0 || columns.Count == 0) return;

        container.Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(20);
                for (int i = 0; i < columns.Count; i++)
                {
                    if (columnRatios != null && i < columnRatios.Length)
                        cd.RelativeColumn(columnRatios[i]);
                    else
                        cd.RelativeColumn();
                }
            });

            // 表头
            table.Header(header =>
            {
                header.Cell().Element(CellHeaderStyle).Text("#").FontSize(9).AlignCenter();
                foreach (var (_, label) in columns)
                    header.Cell().Element(CellHeaderStyle).Text(label).FontSize(9).AlignCenter();
            });

            // 数据行
            int seq = 0;
            foreach (var g in groups)
            {
                seq++;
                table.Cell().Element(CellStyle).Text(seq.ToString()).FontSize(9).AlignCenter();
                foreach (var (key, _) in columns)
                {
                    var value = GetProcessGroupFieldValue(g, key, processNameMap);
                    table.Cell().Element(CellStyle).Text(value).FontSize(9).AlignCenter();
                }
            }
        });
    }

    // ========== 单元格样式 ==========

    private static IContainer TitleCellStyle(IContainer container)
    {
        return container.Background(Colors.Grey.Lighten4)
            .Border(0.3f).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(2).PaddingHorizontal(6)
            .AlignMiddle();
    }

    private static IContainer DataCellStyle(IContainer container)
    {
        return container.Border(0.3f).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(2).PaddingHorizontal(6)
            .AlignMiddle();
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container.Border(0.3f).BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten4)
            .PaddingVertical(2).PaddingHorizontal(6)
            .AlignMiddle();
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.Border(0.3f).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(2).PaddingHorizontal(4)
            .AlignMiddle();
    }

    private static IContainer CellHeaderStyle(IContainer container)
    {
        return container.Border(0.3f).BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(2).PaddingHorizontal(3)
            .AlignMiddle();
    }

    // ========== 字段值提取 ==========

    private static List<(string Label, string Value)> GetBatchInfoFields(ProductionBatch b, List<ProcessCardColumnDef> visibleCols, IReadOnlyDictionary<string, string>? sectionNameMap = null, IReadOnlyDictionary<string, string>? processNameMap = null)
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
            ["CurrentExecDate"] = ("截止执行日", () => b.CurrentExecDate?.ToString("yyyy-MM-dd") ?? "-"),
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
            ["CreatedTime"] = ("创建时间", () => b.CreatedTime.ToString("yyyy-MM-dd HH:mm")),
        };
        return BuildFieldList(visibleCols, "BatchInfo", map);
    }

    private static List<(string Label, string Value)> GetQualityFields(ProductionBatch b, List<ProcessCardColumnDef> visibleCols)
    {
        var map = new Dictionary<string, (string Label, Func<string> Value)>
        {
            ["SolutionParams"] = ("固溶参数", () => b.SolutionParams ?? "-"),
            ["QualityRemark"] = ("质量备注", () => b.QualityRemark ?? "-"),
        };
        return BuildFieldList(visibleCols, "Quality", map);
    }

    private static List<(string Label, string Value)> GetWarehouseFields(ProductionBatch b, List<ProcessCardColumnDef> visibleCols)
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

    private static List<(string Label, string Value)> GetWorkOrderFields(ProductionBatch b, List<ProcessCardColumnDef> visibleCols)
    {
        var map = new Dictionary<string, (string Label, Func<string> Value)>
        {
            ["WorkOrderNo"] = ("工单号", () => b.WorkOrderNo),
            ["SalesOrderNo"] = ("源订单号", () => b.SalesOrderNo),
            ["ProductionMainNo"] = ("主号", () => b.ProductionMainNo),
            ["ProductionSubNo"] = ("次号", () => b.ProductionSubNo ?? "-"),
            ["OrderItemIds"] = ("项次ID", () => b.OrderItemIds),
            ["SignDate"] = ("签订日期", () => b.SignDate.ToString("yyyy-MM-dd")),
            ["Salesman"] = ("业务员", () => b.Salesman),
            ["EndCustomer"] = ("最终用户", () => b.EndCustomer ?? "-"),
            ["DeliveryDate"] = ("交货日期", () => b.DeliveryDate.ToString("yyyy-MM-dd")),
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

    private static List<(string Key, string Label)> GetProcessGroupColumns(List<ProcessCardColumnDef> visibleCols)
    {
        return visibleCols
            .Where(c => c.BlockKey == "ProcessGroup")
            .Select(c => (c.Key, c.Label))
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

    private static List<(string Label, string Value)> BuildFieldList(
        List<ProcessCardColumnDef> visibleCols,
        string blockKey,
        Dictionary<string, (string Label, Func<string> Value)> fieldMap)
    {
        var result = new List<(string, string)>();
        foreach (var col in visibleCols.Where(c => c.BlockKey == blockKey))
        {
            if (fieldMap.TryGetValue(col.Key, out var entry))
                result.Add((entry.Label, entry.Value()));
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
