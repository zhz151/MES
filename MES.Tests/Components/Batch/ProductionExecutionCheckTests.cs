using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.Batches;
using MES.Blazor.Services;
using MES.Core.DTOs.Batch;

namespace MES.Tests.Components;

/// <summary>
/// 生产执行核查页（2026-09-08 由批次首页拆出）：承载「批次-错疑执行」聚合卡 + 页内自足精简批次列表。
/// 断言：标题 + 聚合卡默认展开渲染 4 类错疑（与后端 doubt-execution-summary 同源）。
/// </summary>
public class ProductionExecutionCheckTests : TestBase
{
    public ProductionExecutionCheckTests()
    {
        RegisterServices(typeof(BatchService));
        ConfigureEmptyResponse("/api/batch/list");
        ConfigureEmptyResponse("/api/batch/filter-contexts");
        ConfigureDoubtSummary();
    }

    private void ConfigureDoubtSummary()
    {
        ConfigureResponse("/api/batch/doubt-execution-summary", new ApiResponse<List<BatchDoubtExecutionItemDto>>
        {
            Success = true,
            Code = 200,
            Data = new List<BatchDoubtExecutionItemDto>
            {
                new() { DoubtType = BatchDoubtExecutionType.MatchOrder, BatchCount = 1, InputWeight = 100m },
                new() { DoubtType = BatchDoubtExecutionType.FlowDoubt, BatchCount = 2, InputWeight = 250m },
                new() { DoubtType = BatchDoubtExecutionType.NeedAdjust, BatchCount = 0, InputWeight = 0m },
                new() { DoubtType = BatchDoubtExecutionType.CutDoubt, BatchCount = 3, InputWeight = 400m },
            }
        });
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = RenderPage<ProductionExecutionCheck>();
        cut.Markup.Should().Contain("生产执行核查");
    }

    [Fact]
    public void Render_NoTopSearchBars()
    {
        // 2026-09-09 拍板：本页顶部「模糊搜索 + 登记日期」整行删除（只读核查页，定位靠错疑卡联动+列头筛选），
        // 断言搜索/日期输入框不再渲染，且列显隐工具仍存在
        var cut = RenderPage<ProductionExecutionCheck>();
        cut.Markup.Should().NotContain("模糊搜索");
        cut.Markup.Should().NotContain("登记日期");
        cut.Markup.Should().Contain("生产执行核查");
    }

    [Fact]
    public void DoubtCard_DefaultCollapsed_ToggleExpands_ShowsFourCategories()
    {
        var cut = RenderPage<ProductionExecutionCheck>();

        // 聚合卡默认折叠：卡内容不渲染（懒加载未触发）——以聚合卡专有表头「批次数量」判定（列表列头无此字）；
        // 折叠按钮常驻显示新卡名「错疑-生产批次执行」
        cut.Markup.Should().Contain("错疑-生产批次执行");
        cut.Markup.Should().NotContain("批次数量");
        // 点开折叠按钮 → 展开并懒加载 4 类错疑汇总（匹配工单/工段流转同名单列头无关，以「批次数量」+ 类别词判定）
        var toggle = cut.FindAll("button").First(b => b.TextContent.Contains("错疑-生产批次执行"));
        toggle.Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("批次数量"));
        cut.Markup.Should().Contain("匹配工单");
        cut.Markup.Should().Contain("工段流转");
        cut.Markup.Should().Contain("有效投料");
        cut.Markup.Should().Contain("成品切割");
    }

    [Fact]
    public void TheoreticalOutputColumns_DefaultVisible()
    {
        // G3 理论产出对照默认显示产出折算三列：过程检侧（过程检理论成支）+ 现有效侧（现理论成支/理论成品重）
        // 2026-09-08 精简列名（…理论成品支→现理论成支 / 过程检理论成品支→过程检理论成支）+ 过程检列前移 + 组名改名；同日取消「过程检成重」列
        var cut = RenderPage<ProductionExecutionCheck>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("现理论成支"));
        cut.Markup.Should().Contain("理论成品重");
        cut.Markup.Should().Contain("过程检理论成支");
        // 过程检成重列已取消，列头不再渲染
        cut.Markup.Should().NotContain("过程检成重");
        // 过程检理论成支列排在现理论成支之前
        var idxProcQty = cut.Markup.IndexOf("过程检理论成支");
        var idxCurQty = cut.Markup.IndexOf("现理论成支");
        idxProcQty.Should().BeGreaterThan(-1);
        idxCurQty.Should().BeGreaterThan(-1);
        idxProcQty.Should().BeLessThan(idxCurQty);
    }

    [Fact]
    public void DoubtCard_ShowsBatchCountWeightCells()
    {
        var cut = RenderPage<ProductionExecutionCheck>();

        // 默认折叠 → 先点开再断言：有批次的行显示「N批/xxKg」；BatchCount=0 行显示 "-"
        var toggle = cut.FindAll("button").First(b => b.TextContent.Contains("错疑-生产批次执行"));
        toggle.Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("1批/100Kg"));
        cut.Markup.Should().Contain("2批/250Kg");
        cut.Markup.Should().Contain("3批/400Kg");
        cut.Markup.Should().Contain("\">-</td>");
    }

    [Fact]
    public void GroupColumn_MergeAndRename_Applied()
    {
        var cut = RenderPage<ProductionExecutionCheck>();

        // 原「批次与执行」+「关联工单」合并为「批次与工单」
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("批次与工单"));
        cut.Markup.Should().Contain("执行核查");
        // G3 组名收束后改名「理论产出对照」（2026-09-08）
        cut.Markup.Should().Contain("理论产出对照");
        // 执行核查组灯名重命名（执行匹配→匹配工单 / 流转判定→工段流转 / 需调整→投料需调整）
        cut.Markup.Should().Contain("匹配工单");
        cut.Markup.Should().Contain("工段流转");
        cut.Markup.Should().Contain("投料需调整");
    }

    [Fact]
    public void RemovedColumns_HeadersAbsent()
    {
        var cut = RenderPage<ProductionExecutionCheck>();

        // 被裁剪的 6 列表头不再渲染：当前工序/当前工段/截止执行日/工段完工（批次与执行）、订单号/主号（关联工单）
        cut.Markup.Should().NotContain("当前工序");
        cut.Markup.Should().NotContain("当前工段");
        cut.Markup.Should().NotContain("截止执行日");
        cut.Markup.Should().NotContain("工段完工");
        cut.Markup.Should().NotContain("订单号");
        cut.Markup.Should().NotContain("主号");
    }

    [Fact]
    public void G3_TrimmedToReferenceColumns()
    {
        var cut = RenderPage<ProductionExecutionCheck>();

        // 「理论产出对照」组收束为错疑缘由数据对照组：过程检理论成支 + 现理论成支/理论成品重 + 成切三数值列（过程检成重列已取消）
        cut.Markup.Should().Contain("过程检理论成支");
        cut.Markup.Should().Contain("成切需求");
        cut.Markup.Should().Contain("成切执行");
        cut.Markup.Should().Contain("成切支数");
        // 领料/现有效原料/缺陷量 + 过程检成重 列彻底删除，列头不再渲染
        cut.Markup.Should().NotContain("过程检成重");
        cut.Markup.Should().NotContain("领料支数");
        cut.Markup.Should().NotContain("领料重量");
        cut.Markup.Should().NotContain("现有效原料支数");
        cut.Markup.Should().NotContain("现有效原料重量");
        cut.Markup.Should().NotContain("缺陷-返整量");
        cut.Markup.Should().NotContain("缺陷-纯次品量");
    }

    [Fact]
    public void CutDoubt_BackInExecutionCheckGroup()
    {
        var cut = RenderPage<ProductionExecutionCheck>();

        // 成切存疑灯回归执行核查组（表头渲染「成切存疑」），成切支数对照列仍在理论产出对照组
        cut.Markup.Should().Contain("成切存疑");
        cut.Markup.Should().Contain("成切支数");
    }
}
