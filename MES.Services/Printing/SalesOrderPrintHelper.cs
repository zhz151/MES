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
using MES.Core.Helpers;

namespace MES.Services.Printing;

/// <summary>
/// 销售订单 PDF 打印模板（QuestPDF）
/// 页面方向：A4 横向（Landscape），因项次明细列数较多
/// </summary>
public static class SalesOrderPrintHelper
{
    // ==============================
    // 1. 订单确认单（单条/批量合并）
    // ==============================
    public static byte[] GenerateOrderPdf(SalesOrderDetailDto order, bool includeAmounts = true)
    {
        return GenerateBatchOrderPdf(new List<SalesOrderDetailDto> { order }, includeAmounts);
    }

    /// <summary>
    /// 批量订单合并打印（连续排版，每单独占区域）
    /// </summary>
    /// <param name="includeAmounts">true=含金额模式（显示结算方式/计价单位/单价/总价并合计订单总价）；false=不含金额模式（仅保留结算方式，供不同用户）</param>
    public static byte[] GenerateBatchOrderPdf(List<SalesOrderDetailDto> orders, bool includeAmounts = true)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("SimSun"));

                page.Header().Element(h => ComposeDocHeader(h, "销 售 订 单 确 认 单"));

                page.Content().Element(c => ComposeOrderContent(c, orders, includeAmounts));

                page.Footer().Element(ComposeDocFooter);
            });
        }).GeneratePdf();
    }

    // ==============================
    // 2. 技术要求确认单
    // ==============================
    public static byte[] GenerateRequirementsPdf(SalesOrderDetailDto order, List<ProductRequirementDto> requirements)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("SimSun"));

                page.Header().Element(h => ComposeDocHeader(h, "技 术 要 求 确 认 单"));

                page.Content().Element(c => ComposeRequirementsContent(c, order, requirements));

                page.Footer().Element(ComposeDocFooter);
            });
        }).GeneratePdf();
    }

    // ========== 页眉 ==========

    private static void ComposeDocHeader(IContainer container, string title)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(4).AlignCenter().Text(title)
                .FontSize(18).Bold();

            col.Item().PaddingVertical(3)
                .LineHorizontal(1).LineColor(Colors.Black);
        });
    }

    // ========== 页脚 ==========

    private static void ComposeDocFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingVertical(3)
                .LineHorizontal(1).LineColor(Colors.Black);

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

    // ========== 订单内容（单条或批量） ==========

    private static void ComposeOrderContent(IContainer container, List<SalesOrderDetailDto> orders, bool includeAmounts)
    {
        container.Column(col =>
        {
            for (int i = 0; i < orders.Count; i++)
            {
                var order = orders[i];

                if (i > 0)
                {
                    // 批量模式下，订单间用分隔线
                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                }

                // 订单头信息
                ComposeOrderHeader(col.Item(), order);

                col.Item().PaddingVertical(3);

                // 项次明细表
                ComposeOrderItemsTable(col.Item(), order.Items, includeAmounts);

                // 汇总行
                ComposeOrderSummary(col.Item(), order.Items, includeAmounts);
            }
        });
    }

    private static void ComposeOrderHeader(IContainer container, SalesOrderDetailDto order)
    {
        container.Row(row =>
        {
            row.RelativeItem(2).Text(t =>
            {
                t.Span("订单编号：").Bold().FontSize(10);
                t.Span(order.OrderNumber).FontSize(10);
            });
            row.RelativeItem(2).Text(t =>
            {
                t.Span("签订日期：").Bold().FontSize(10);
                t.Span(order.SignDate.ToString("yyyy-MM-dd")).FontSize(10);
            });
            row.RelativeItem(2).Text(t =>
            {
                t.Span("业务员：").Bold().FontSize(10);
                t.Span(order.Salesman).FontSize(10);
            });
            row.RelativeItem(3).Text(t =>
            {
                t.Span("客户名称：").Bold().FontSize(10);
                t.Span(order.CustomerName).FontSize(10);
            });
            row.RelativeItem(2).Text(t =>
            {
                t.Span("最终客户：").Bold().FontSize(10);
                t.Span(order.EndCustomer ?? "-").FontSize(10);
            });
            row.RelativeItem(2).Text(t =>
            {
                t.Span("状态：").Bold().FontSize(10);
                t.Span(EnumHelper.GetDisplayName(order.Status)).FontSize(10);
            });
        });
    }

    /// <summary>
    /// 项次明细表（结算方式置于理算重量之后；含金额模式追加 计价单位/单价/总价 三列）
    /// </summary>
    private static void ComposeOrderItemsTable(IContainer container, List<OrderItemDto> items, bool includeAmounts)
    {
        // 列定义（顺序=输出顺序）。备注为唯一相对列，其余为常量列；
        // 常量列总宽须显著小于 A4 横向内容宽 ≈782pt（含金额模式常量合计 708pt），
        // 否则 RelativeColumn 分不到空间会抛 DocumentLayoutException
        var columns = BuildItemColumns(includeAmounts);

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                foreach (var col in columns)
                {
                    if (col.Width.HasValue)
                        cols.ConstantColumn(col.Width.Value);
                    else
                        cols.RelativeColumn();
                }
            });

            foreach (var col in columns)
            {
                table.Cell().Element(CellHeaderStyle).Text(col.Header).FontSize(7).AlignCenter();
            }

            foreach (var item in items.OrderBy(i => i.Sequence))
            {
                foreach (var col in columns)
                {
                    table.Cell().Element(CellStyle).Text(d =>
                    {
                        d.Span(col.Render(item)).FontSize(col.FontSize);
                        if (col.AlignRight) d.AlignRight(); else d.AlignCenter();
                    });
                }
            }
        });
    }

    /// <summary>
    /// 汇总行（含金额模式追加 订单总价）
    /// </summary>
    private static void ComposeOrderSummary(IContainer container, List<OrderItemDto> items, bool includeAmounts)
    {
        var totalQty = items.Sum(i => i.Quantity ?? 0);
        var totalMeters = items.Sum(i => i.Meters ?? 0);
        var totalContractWeight = items.Sum(i => i.ContractWeight);
        var totalTheoryWeight = items.Sum(i => i.TheoreticalWeight);

        container.AlignRight().Text(t =>
        {
            t.Span($"合计：{totalQty.ToString("G29")} 支  /  {FormatWeightRound1(totalMeters)} 米  /  合同重量 {FormatWeightRound1(totalContractWeight)} kg  /  理算重量 {FormatWeightRound1(totalTheoryWeight)} kg")
                .FontSize(9).Bold();

            if (includeAmounts)
            {
                var totalPrice = items.Sum(i => i.TotalPrice ?? 0);
                t.Span($"  /  订单总价 {FormatMoney2(totalPrice)} 元").FontSize(9).Bold();
            }
        });
    }

    /// <summary>
    /// 列规格（Width=null 表示相对列，须置于末尾）
    /// </summary>
    private sealed class OrderPrintColumn
    {
        public OrderPrintColumn(float? width, string header, Func<OrderItemDto, string> render, int fontSize = 7, bool alignRight = false)
        {
            Width = width;
            Header = header;
            Render = render;
            FontSize = fontSize;
            AlignRight = alignRight;
        }

        public float? Width { get; }
        public string Header { get; }
        public Func<OrderItemDto, string> Render { get; }
        public int FontSize { get; }
        public bool AlignRight { get; }
    }

    /// <summary>
    /// 订单项次列清单（结算方式紧跟理算重量；含金额模式在结算方式后补三金额列）
    /// </summary>
    private static List<OrderPrintColumn> BuildItemColumns(bool includeAmounts)
    {
        var columns = new List<OrderPrintColumn>
        {
            new(20, "项次", it => it.Sequence.ToString()),
            new(50, "交货日期", it => it.DeliveryDate.ToString("yyyy-MM-dd")),
            new(20, "延期罚款", it => it.DelayPenalty ? "是" : "否"),
            new(32, "物料名称", it => EnumHelper.GetDisplayName(it.PipeManufacturingType)),
            new(44, "产品标准", it => it.StandardNo, fontSize: 6),
            new(32, "交货状态", it => EnumHelper.GetDisplayName(it.DeliveryState), fontSize: 6),
            new(32, "牌号", it => it.StandardGrade, fontSize: 6),
            new(56, "规格(外径×壁厚)", it => it.Specification, fontSize: 6),
            new(22, "外径下差", it => FormatDecimal(it.OuterDiameterNegative), alignRight: true),
            new(22, "外径上差", it => FormatDecimal(it.OuterDiameterPositive), alignRight: true),
            new(22, "壁厚下差", it => FormatDecimal(it.WallThicknessNegative), alignRight: true),
            new(22, "壁厚上差", it => FormatDecimal(it.WallThicknessPositive), alignRight: true),
            new(26, "长度状态", it => EnumHelper.GetDisplayName(it.LengthStatus)),
            new(26, "最小长度", it => FormatNullableDecimal(it.MinLength)),
            new(26, "最大长度", it => FormatNullableDecimal(it.MaxLength)),
            new(20, "支数", it => it.Quantity.HasValue ? it.Quantity.Value.ToString("G29") : "-", alignRight: true),
            new(32, "米数", it => FormatWeightRound1(it.Meters), alignRight: true),
            new(38, "合同重量", it => FormatWeightRound1(it.ContractWeight), alignRight: true),
            new(38, "理算重量", it => FormatWeightRound1(it.TheoreticalWeight), alignRight: true),
            new(28, "结算方式", it => EnumHelper.GetDisplayName(it.SettlementMethod)),
        };

        if (includeAmounts)
        {
            columns.Add(new(30, "计价单位", it => it.PricingUnit.HasValue ? EnumHelper.GetDisplayName(it.PricingUnit.Value) : "-"));
            columns.Add(new(28, "单价", it => it.UnitPrice.HasValue ? FormatMoney2(it.UnitPrice.Value) : "-", alignRight: true));
            columns.Add(new(42, "总价", it => it.TotalPrice.HasValue ? FormatMoney2(it.TotalPrice.Value) : "-", alignRight: true));
        }

        columns.Add(new(null, "备注", it => string.IsNullOrEmpty(it.Remark) ? "-" : it.Remark, fontSize: 6));
        return columns;
    }

    // ========== 技术要求内容 ==========

    private static void ComposeRequirementsContent(IContainer container, SalesOrderDetailDto order, List<ProductRequirementDto> requirements)
    {
        container.Column(col =>
        {
            // 订单概要信息（与销售订单确认单统一）
            ComposeOrderHeader(col.Item(), order);

            col.Item().PaddingVertical(5);

            // 技术要求表（A4 横向，33 列）
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(28);   // 项次
                    columns.ConstantColumn(42);   // 要求类型
                    columns.RelativeColumn();     // 化学分析(成品)
                    columns.RelativeColumn();     // PMI检验
                    columns.RelativeColumn();     // 表检
                    columns.RelativeColumn();     // 尺寸
                    columns.RelativeColumn();     // 内窥
                    columns.RelativeColumn();     // 液压检验
                    columns.RelativeColumn();     // 水下气压
                    columns.RelativeColumn();     // 涡流探伤
                    columns.RelativeColumn();     // 超声波检验
                    columns.RelativeColumn();     // 端口着色
                    columns.RelativeColumn();     // 射线探伤
                    columns.RelativeColumn();     // 硬度(洛氏)
                    columns.RelativeColumn();     // 硬度(布氏)
                    columns.RelativeColumn();     // 硬度(维氏)
                    columns.RelativeColumn();     // 拉伸(室温)
                    columns.RelativeColumn();     // 拉伸(高温)
                    columns.RelativeColumn();     // 焊接接头拉伸
                    columns.RelativeColumn();     // 冲击试验
                    columns.RelativeColumn();     // 焊接接头冲击
                    columns.RelativeColumn();     // 压扁试验
                    columns.RelativeColumn();     // 卷边试验
                    columns.RelativeColumn();     // 扩口试验
                    columns.RelativeColumn();     // 弯曲试验
                    columns.RelativeColumn();     // 焊接接头弯曲
                    columns.RelativeColumn();     // 晶粒度
                    columns.RelativeColumn();     // 晶间腐蚀
                    columns.RelativeColumn();     // 点腐蚀
                    columns.RelativeColumn();     // 金相检验
                    columns.RelativeColumn();     // 低倍组织
                    columns.RelativeColumn();     // 其他要求
                });

                // 表头
                string[] headers = { "项次", "要求类型", "化学分析(成品)", "PMI检验", "表检", "尺寸", "内窥", "液压检验", "水下气压", "涡流探伤", "超声波检验", "端口着色", "射线探伤", "硬度(洛氏)", "硬度(布氏)", "硬度(维氏)", "拉伸(室温)", "拉伸(高温)", "焊接接头拉伸", "冲击试验", "焊接接头冲击", "压扁试验", "卷边试验", "扩口试验", "弯曲试验", "焊接接头弯曲", "晶粒度", "晶间腐蚀", "点腐蚀", "金相检验", "低倍组织", "其他要求" };
                foreach (var header in headers)
                {
                    table.Cell().Element(CellHeaderStyle).Text(header).FontSize(6).AlignCenter();
                }

                // 数据行
                foreach (var req in requirements.OrderBy(r => r.Sequence))
                {
                    table.Cell().Element(CellStyle).Text(req.Sequence.ToString()).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(EnumHelper.GetDisplayName(req.RequirementType)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.ChemicalComposition)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(StageText(req.PmiInspection)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(StageText(req.SurfaceInspection)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(StageText(req.Dimension)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(StageText(req.Endoscopy)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(StageText(req.HydrostaticTest)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(StageText(req.UnderwaterPressure)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(StageText(req.EddyCurrent)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(StageText(req.UltrasonicTest)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(StageText(req.PortColoring)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(StageText(req.RadiographicTest)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.HardnessRockwell)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.HardnessBrinell)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.HardnessVickers)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.TensileRoomTemp)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.TensileHighTemp)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.WeldJointTensile)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.ImpactTest)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.WeldJointImpact)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.FlatteningTest)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.FlaringTest)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.ExpandingTest)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.BendTest)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.WeldJointBend)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.GrainSize)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.IntergranularCorrosion)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.PittingCorrosion)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.FerriteContent)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(BoolText(req.Macrostructure)).FontSize(6).AlignCenter();
                    table.Cell().Element(CellStyle).Text(req.OtherRequirement ?? "-").FontSize(6);
                }
            });

            if (requirements.Count == 0)
            {
                col.Item().PaddingTop(8).AlignCenter().Text("暂无技术要求数据").FontSize(10).FontColor(Colors.Grey.Medium);
            }
        });
    }

    private static string BoolText(bool value) => value ? "是" : "-";

    /// <summary>
    /// 检验阶段枚举中文文本（终/预/预+终/-）
    /// </summary>
    private static string StageText(InspectionRequirementStage value) => EnumHelper.GetDisplayName(value);

    // ========== 表格单元格样式 ==========

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

    // ========== 辅助方法 ==========

    private static string FormatDecimal(decimal value) => value == 0 ? "0" : value.ToString("G29");
    private static string FormatNullableDecimal(decimal? value) => value.HasValue && value.Value != 0 ? value.Value.ToString("G29") : "-";

    /// <summary>
    /// 重量/米数打印收敛：四舍五入保留 1 位小数并去尾零（与前端 DisplayHelper.FormatWeight1 一致）
    /// </summary>
    private static string FormatWeightRound1(decimal value) => Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("G29");

    /// <summary>
    /// 重量/米数打印收敛（可空版），空值或 0 显示 "-"
    /// </summary>
    private static string FormatWeightRound1(decimal? value) => value.HasValue && value.Value != 0 ? FormatWeightRound1(value.Value) : "-";

    /// <summary>
    /// 金额打印收敛：四舍五入保留 ≤2 位小数并去尾零（与前端 DisplayHelper.FormatMoney2 一致）
    /// </summary>
    private static string FormatMoney2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("G29");
}
