using Bunit;
using FluentAssertions;
using MES.Blazor.Services;
using MES.Blazor.Shared;
using MES.Core.DTOs.Order;
using MES.Core.Models;

namespace MES.Tests.Components;

/// <summary>
/// 首页两张查询卡（2026-09-14 新增）：
///  ① 订单进度查询卡 → 结果**就地渲染**订单进度树（不是链接跳转），且传 Compact=true
///     → **全部主号默认折叠**（首页半屏宽，避免刷屏），点主号才展开分支；
///  ② 生产批次进度查询卡 → 卡内输入生产编号，查不到时给业务提示且不渲染进度卡。
/// 两卡均有「清除」按钮（取消查询）：清空输入与结果、回到未查询空态（用户反馈原无取消入口）。
/// 数据源端点（api/order/progress、api/batch/by-batch-no、api/batch/{id}/tracking）的授权口径
/// 另由 <see cref="MES.Tests.Controllers.HomeQueryEndpointAuthorizationTests"/> 锁定。
/// </summary>
public class HomeQueryCardsTests : TestBase
{
    public HomeQueryCardsTests()
    {
        RegisterServices(typeof(OrderProgressService), typeof(BatchService));
    }

    private static OrderProgressTreeDto BuildTree() => new()
    {
        SalesOrderNo = "SO-001",
        CustomerName = "测试客户",
        OrderTotalWeightKg = 12000m,
        ItemCount = 2,
        MainNos = new List<OrderMainProgressDto>
        {
            new()
            {
                ProductionMainNo = "X01",
                ScheduleStage = 1,          // 已完结
                TotalWeightKg = 6000m,
                Warehousing = new MainProgressBranchDto
                {
                    Key = "Warehousing",
                    Title = "订单成品入库",
                    Leaves = new List<MainProgressLeafDto>
                    {
                        new() { Key = "Inbound", Text = "入库", WeightKg = 5900m }
                    }
                }
            },
            new()
            {
                ProductionMainNo = "X02",
                ScheduleStage = 3,          // 生产执行（未完结）
                TotalWeightKg = 6000m,
                Production = new MainProgressBranchDto
                {
                    Key = "Production",
                    Title = "生产执行",
                    Leaves = new List<MainProgressLeafDto>
                    {
                        new() { Key = "InProgress", Text = "在产", WeightKg = 1200m }
                    }
                }
            }
        }
    };

    [Fact]
    public void 订单进度查询卡_点击查询_就地渲染进度树且全部主号默认折叠()
    {
        ConfigureResponse("/api/order/progress", ApiResponse<OrderProgressTreeDto?>.Ok(BuildTree()));

        var cut = RenderPage<OrderProgressQueryCard>();

        cut.Markup.Should().Contain("订单进度查询");
        cut.FindAll(".op-root").Should().BeEmpty("未查询前不渲染任何树");

        cut.Find("input").Change("SO-001");
        cut.FindAll("button")[0].Click();   // [0]=查询 [1]=清除

        cut.WaitForAssertion(() => cut.FindAll(".op-root").Count.Should().Be(1));

        // Compact=true：含未完结的 X02 在内**所有主号默认折叠** → 三级分支一条都不渲染
        cut.FindAll(".op-main-head").Count.Should().Be(2);
        cut.FindAll(".op-stage").Should().BeEmpty("首页紧凑态下所有主号默认折叠，展开才出分支");

        // 点开未完结主号：分支与叶子就地出现（证明是就地展开，不是跳转到 /orders/progress）
        cut.FindAll(".op-main-head")[1].Click();
        cut.WaitForAssertion(() => cut.FindAll(".op-stage").Count.Should().Be(1));
        cut.Markup.Should().Contain("在产");
    }

    [Fact]
    public void 订单进度查询卡_查不到订单_给业务提示()
    {
        ConfigureResponse("/api/order/progress", ApiResponse<OrderProgressTreeDto?>.Ok(null!));

        var cut = RenderPage<OrderProgressQueryCard>();
        cut.Find("input").Change("NO-SUCH-ORDER");
        cut.FindAll("button")[0].Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("未找到该订单的工单数据"));
        cut.FindAll(".op-root").Should().BeEmpty();
    }

    [Fact]
    public void 订单进度查询卡_点清除_取消查询并复位为空态()
    {
        ConfigureResponse("/api/order/progress", ApiResponse<OrderProgressTreeDto?>.Ok(BuildTree()));

        var cut = RenderPage<OrderProgressQueryCard>();
        cut.Find("input").Change("SO-001");
        cut.FindAll("button")[0].Click();
        cut.WaitForAssertion(() => cut.FindAll(".op-root").Count.Should().Be(1));

        // 点「清除」→ 结果区清空（回到未查询态，不是把树上某些节点收起）
        cut.FindAll("button")[1].Click();
        cut.FindAll(".op-root").Should().BeEmpty();

        // 再点「查询」不应把结果查回来 —— 证明输入框也已被清空（否则会重新发起查询）
        cut.FindAll("button")[0].Click();
        cut.FindAll(".op-root").Should().BeEmpty("清除后输入框为空，再点查询应直接返回、不发起请求");
    }

    [Fact]
    public void 生产批次进度查询卡_查不到生产编号_给业务提示且不渲染进度卡()
    {
        // 未配置路由 → FakeHttpMessageHandler 默认 404 → BatchService 返回 Fail → _batchId 保持 0
        var cut = RenderPage<BatchProgressQueryCard>();

        cut.Markup.Should().Contain("生产批次进度查询");

        cut.Find("input").Change("NO-SUCH-BATCH");
        cut.FindAll("button")[0].Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("未找到该生产编号的批次"));
        cut.FindComponents<BatchProgressCard>().Should().BeEmpty();
    }

    [Fact]
    public void 生产批次进度查询卡_点清除_取消查询并复位为空态()
    {
        var cut = RenderPage<BatchProgressQueryCard>();
        cut.Find("input").Change("NO-SUCH-BATCH");
        cut.FindAll("button")[0].Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("未找到该生产编号的批次"));

        cut.FindAll("button")[1].Click();

        cut.Markup.Should().NotContain("未找到该生产编号的批次");
        cut.FindComponents<BatchProgressCard>().Should().BeEmpty();
    }
}
