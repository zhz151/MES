using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MES.Data.Entities;
using MES.Core.Enums;

namespace MES.Services.Printing;

/// <summary>
/// 工单详情 PDF 打印模板（QuestPDF）
/// 卡片式布局，A4 纵向
/// </summary>
public static class WorkOrderPrintHelper
{
    public static byte[] GeneratePdf(WorkOrder entity)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(35);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("SimSun"));

                page.Header().Element(h => ComposeHeader(h, entity));
                page.Content().Element(c => ComposeContent(c, entity));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    /// <summary>
    /// 按订单号批量打印所有工单（每页2个工单，自动分页）
    /// </summary>
    public static byte[] GenerateBatchPdf(string salesOrderNo, List<WorkOrder> workOrders)
    {
        return Document.Create(container =>
        {
            for (int i = 0; i < workOrders.Count; i += 2)
            {
                var batch = workOrders.Skip(i).Take(2).ToList();
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(35);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("SimSun"));

                    page.Header().Element(h => ComposeBatchHeader(h, salesOrderNo));
                    page.Content().Element(c => ComposeBatchContent(c, batch));
                    page.Footer().Element(ComposeFooter);
                });
            }
        }).GeneratePdf();
    }

    /// <summary>
    /// 多订单批量打印（按订单分组，每页2个工单）
    /// </summary>
    public static byte[] GenerateMultiBatchPdf(List<WorkOrder> workOrders)
    {
        return Document.Create(container =>
        {
            var orderGroups = workOrders
                .GroupBy(wo => wo.SalesOrderNo)
                .OrderBy(g => g.Key);

            foreach (var group in orderGroups)
            {
                var list = group.ToList();
                for (int i = 0; i < list.Count; i += 2)
                {
                    var batch = list.Skip(i).Take(2).ToList();
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(35);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily("SimSun"));

                        page.Header().Element(h => ComposeBatchHeader(h, group.Key));
                        page.Content().Element(c => ComposeBatchContent(c, batch));
                        page.Footer().Element(ComposeFooter);
                    });
                }
            }
        }).GeneratePdf();
    }

    // ========== 页眉 ==========

    private static void ComposeHeader(IContainer container, WorkOrder entity)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().AlignLeft().Text("工 单 详 情 卡")
                    .FontSize(16).Bold();
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.Span("工单号：").Bold().FontSize(10);
                    t.Span(entity.WorkOrderNo).FontSize(10);
                });
            });

            col.Item().PaddingTop(2).Row(row =>
            {
                row.RelativeItem().AlignLeft().Text(GetStatusText(entity.Status))
                    .FontSize(9).FontColor(GetStatusColor(entity.Status));
            });

            col.Item().PaddingVertical(4)
                .LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    // ========== 页脚 ==========

    private static void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingVertical(3)
                .LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text($"打印日期：{DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(8);
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" / ").FontSize(8);
                    t.TotalPages().FontSize(8);
                });
            });
        });
    }

    // ========== 批量打印页眉 ==========

    private static void ComposeBatchHeader(IContainer container, string salesOrderNo)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().AlignLeft().Text("工 单 详 情 卡")
                    .FontSize(16).Bold();
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.Span("源订单号：").Bold().FontSize(10);
                    t.Span(salesOrderNo).FontSize(10);
                });
            });

            col.Item().PaddingTop(2).Text("批量打印")
                .FontSize(9).FontColor(Colors.Grey.Darken1);

            col.Item().PaddingVertical(4)
                .LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    // ========== 内容 ==========

    private static void ComposeContent(IContainer container, WorkOrder entity)
    {
        ComposeWorkOrderCard(container, entity);
    }

    /// <summary>
    /// 单个工单卡片内容（3组数据，不含页眉页脚）
    /// </summary>
    private static void ComposeWorkOrderCard(IContainer container, WorkOrder entity)
    {
        container.Column(col =>
        {
            // 工单头部（工单号+主号/次号）
            col.Item().PaddingBottom(3).Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span("工单号：").Bold().FontSize(10);
                    t.Span(entity.WorkOrderNo).FontSize(10);
                    t.Span("  [").FontSize(8);
                    t.Span(GetStatusText(entity.Status)).FontSize(8).FontColor(GetStatusColor(entity.Status));
                    t.Span("]").FontSize(8);
                });
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.Span($"主号：{entity.ProductionMainNo}").FontSize(9);
                    if (!string.IsNullOrEmpty(entity.ProductionSubNo))
                        t.Span($"  次号：{entity.ProductionSubNo}").FontSize(9);
                });
            });

            // 第1组：基本信息
            col.Item().Element(c => ComposeGroupTitle(c, "基本信息"));
            col.Item().PaddingBottom(4).Element(c => ComposeFieldGrid(c, GetGroup1Fields(entity)));

            col.Item().PaddingVertical(2)
                .LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            // 第2组：工艺参数+汇总
            col.Item().PaddingTop(2).Element(c => ComposeGroupTitle(c, "工艺参数与汇总"));
            col.Item().PaddingBottom(4).Element(c => ComposeFieldGrid(c, GetGroup2Fields(entity)));

            col.Item().PaddingVertical(2)
                .LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            // 第3组：项次+明细
            col.Item().PaddingTop(2).Element(c => ComposeGroupTitle(c, "项次与明细"));
            col.Item().Element(c => ComposeFieldGrid(c, GetGroup3Fields(entity)));
        });
    }

    /// <summary>
    /// 批量打印内容：遍历工单列表，逐个渲染卡片，工单间用分隔线
    /// </summary>
    private static void ComposeBatchContent(IContainer container, List<WorkOrder> workOrders)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(3).Text($"共 {workOrders.Count} 个工单")
                .FontSize(9).FontColor(Colors.Grey.Darken1);

            for (int i = 0; i < workOrders.Count; i++)
            {
                if (i > 0)
                {
                    col.Item().PaddingVertical(5)
                        .LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                }

                col.Item().Element(c => ComposeWorkOrderCard(c, workOrders[i]));
            }
        });
    }

    private static void ComposeGroupTitle(IContainer container, string title)
    {
        container.Background(Colors.Grey.Lighten4)
            .PaddingVertical(3).PaddingHorizontal(6)
            .Text(title).FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
    }

    /// <summary>
    /// 以网格形式展示标签-值对，每行2列
    /// </summary>
    private static void ComposeFieldGrid(IContainer container, List<(string Label, string Value)> fields)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            for (int i = 0; i < fields.Count; i++)
            {
                var (label, value) = fields[i];
                table.Cell().Element(CellStyle).Text(t =>
                {
                    t.Span(label + "：").Bold().FontSize(8);
                    t.Span(value).FontSize(8);
                });
            }

            // 如果字段数为奇数，补齐空单元格
            if (fields.Count % 2 != 0)
            {
                table.Cell().Element(EmptyCellStyle);
            }
        });
    }

    // ========== 数据分组 ==========

    private static List<(string Label, string Value)> GetGroup1Fields(WorkOrder entity)
    {
        return new List<(string, string)>
        {
            ("工单号", entity.WorkOrderNo),
            ("源订单号", entity.SalesOrderNo),
            ("主号", entity.ProductionMainNo),
            ("次号", entity.ProductionSubNo ?? "-"),
            ("签订日期", entity.SignDate.ToString("yyyy-MM-dd")),
            ("业务员", entity.Salesman),
            ("最终用户", entity.EndCustomer ?? "-"),
            ("交货日期", entity.DeliveryDate.ToString("yyyy-MM-dd")),
            ("延期罚款", entity.DelayPenalty ? "是" : "否"),
            ("物料名称", GetMaterialText(entity.MaterialName)),
            ("结算方式", GetSettlementText(entity.SettlementMethod)),
            ("标准编码", entity.StandardCode),
        };
    }

    private static List<(string Label, string Value)> GetGroup2Fields(WorkOrder entity)
    {
        return new List<(string, string)>
        {
            ("交货状态", GetDeliveryStateText(entity.DeliveryState)),
            ("工厂牌号", entity.PlantGrade),
            ("规格", FormatSpec(entity.Specification)),
            ("外径公差", $"-{entity.OuterDiameterNegative:G29}/+{entity.OuterDiameterPositive:G29}"),
            ("壁厚公差", $"-{entity.WallThicknessNegative:G29}/+{entity.WallThicknessPositive:G29}"),
            ("长度状态", GetLengthStatusText(entity.LengthStatus)),
            ("最小长度", entity.MinLength.HasValue ? $"{entity.MinLength:G29} mm" : "-"),
            ("最大长度", entity.MaxLength.HasValue ? $"{entity.MaxLength:G29} mm" : "-"),
            ("总支数", $"{entity.TotalQuantity} 支"),
            ("总米数", $"{entity.TotalMeters:G29} m"),
            ("总重量", $"{entity.TotalWeight:G29} kg"),
            ("理论单支重", $"{CalculateUnitWeight(entity):G29} kg"),
            ("技术要求", entity.TechnicalRequirements == RequirementType.Special ? "特殊" : "常规"),
        };
    }

    private static List<(string Label, string Value)> GetGroup3Fields(WorkOrder entity)
    {
        return new List<(string, string)>
        {
            ("总项次数", entity.TotalItemCount.ToString()),
            ("明细", entity.ItemDetails ?? "-"),
        };
    }

    // ========== 单元格样式 ==========

    private static IContainer CellStyle(IContainer container)
    {
        return container.Border(0.3f).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(4).PaddingHorizontal(6)
            .AlignMiddle();
    }

    private static IContainer EmptyCellStyle(IContainer container)
    {
        return container.Border(0.3f).BorderColor(Colors.Grey.Lighten2);
    }

    // ========== 辅助方法 ==========

    private static string GetStatusText(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.NotGenerated => "未编制",
        WorkOrderStatus.Confirmed => "已确定",
        WorkOrderStatus.Pending => "待修正",
        WorkOrderStatus.Cancelled => "已取消",
        _ => status.ToString()
    };

    private static string GetStatusColor(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Confirmed => Colors.Green.Darken1,
        WorkOrderStatus.Pending => Colors.Orange.Darken1,
        WorkOrderStatus.Cancelled => Colors.Red.Darken1,
        _ => Colors.Grey.Darken1
    };

    private static string GetMaterialText(MaterialName name) => name switch
    {
        MaterialName.SeamlessPipe => "无缝管",
        MaterialName.WeldedPipe => "焊管",
        _ => name.ToString()
    };

    private static string GetSettlementText(SettlementMethod method) => method switch
    {
        SettlementMethod.Theoretical => "理算",
        SettlementMethod.Weighing => "过磅",
        SettlementMethod.WeighingNegative => "过磅-负",
        _ => method.ToString()
    };

    private static string GetDeliveryStateText(DeliveryState state) => state switch
    {
        DeliveryState.SolutionAnnealedAndPickled => "固溶酸洗",
        DeliveryState.SolutionAnnealedAndPickledUTube => "固溶酸洗-U型管",
        DeliveryState.SolutionAnnealedAndPickledExternalPolished => "固溶酸洗-外抛光",
        DeliveryState.SolutionAnnealedAndPickledInternalPolished => "固溶酸洗-内抛光",
        DeliveryState.SolutionAnnealedAndPickledBothPolished => "固溶酸洗-内外抛光",
        DeliveryState.SolutionAnnealedAndPickledCoiled => "固溶酸洗-盘管",
        DeliveryState.Bright => "光亮",
        DeliveryState.BrightUTube => "光亮-U型管",
        DeliveryState.BrightCoiled => "光亮-盘管",
        DeliveryState.Hard => "硬态",
        _ => state.ToString()
    };

    private static string GetLengthStatusText(LengthStatus status) => status switch
    {
        LengthStatus.Fixed => "定尺",
        LengthStatus.Range => "范围尺",
        LengthStatus.NonFixed => "非定尺",
        _ => status.ToString()
    };

    private static string FormatSpec(string specification)
    {
        if (string.IsNullOrEmpty(specification)) return "";
        var parts = specification.Split('*');
        if (parts.Length != 2) return specification;
        var od = decimal.TryParse(parts[0], out var odValue) ? odValue.ToString("G29") : parts[0];
        var wt = decimal.TryParse(parts[1], out var wtValue) ? wtValue.ToString("G29") : parts[1];
        return $"{od}*{wt}";
    }

    private static decimal? CalculateUnitWeight(WorkOrder entity)
    {
        if (string.IsNullOrEmpty(entity.Specification)) return null;

        var nominalOd = SpecificationParser.ParseOuterDiameter(entity.Specification);
        var nominalWt = SpecificationParser.ParseWallThickness(entity.Specification);
        if (nominalOd == null || nominalWt == null || nominalOd <= 0 || nominalWt <= 0) return null;

        var odActual = nominalOd.Value - 0.5m * entity.OuterDiameterNegative + 0.5m * entity.OuterDiameterPositive;
        var wtActual = nominalWt.Value - 0.5m * entity.WallThicknessNegative + 0.5m * entity.WallThicknessPositive;

        if (odActual <= 0 || wtActual <= 0) return null;

        var weightPerMeter = (odActual - wtActual) * wtActual * 0.02466m;
        var maxLengthMm = entity.LengthStatus == LengthStatus.Fixed
            ? entity.MaxLength ?? 4500m
            : 4500m;

        return Math.Round(weightPerMeter * maxLengthMm / 1000m, 3);
    }
}
