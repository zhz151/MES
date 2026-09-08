using FluentAssertions;
using MES.Blazor.Shared;
using MES.Shared.Constants;

namespace MES.Tests.Components;

/// <summary>
/// 菜单树（AppMenu）结构回归测试。
/// 桌面 MainLayout 与手机 MobileLayout 共用此树，凡树结构被误改导致「两版漂移」
/// （历史：手机订单组残留牌号对照、缺理化检测/质量证明书/工资结算/用户管理等）在此暴露。
/// 改动菜单请同步更新这些断言。
/// </summary>
public class AppMenuTests
{
    private static AppMenuNode Node(string label) =>
        AppMenu.Root.Single(n => n.Label == label);

    [Fact]
    public void 订单组_仅三个子项_不含牌号对照()
    {
        var order = Node("订单管理");
        order.IsLeaf.Should().BeFalse();
        order.Policy.Should().Be(Roles.Policies.OrderMenu);
        order.Children.Select(c => c.Href).Should().Equal(
            "/orders", "/customers", "/orders/pending-delivery");
        order.Children.Should().NotContain(n => n.Label == "牌号对照" || n.Href == "/grade-mappings");
    }

    [Fact]
    public void 牌号对照_仅存在于生产标准组()
    {
        var standard = Node("生产标准");
        standard.Policy.Should().Be(Roles.Policies.StandardView);
        standard.Children.Should().ContainSingle(n => n.Label == "牌号对照" && n.Href == "/grade-mappings");

        // 全树「牌号对照」只能出现这一次
        AppMenu.AllLeaves().Count(n => n.Label == "牌号对照").Should().Be(1);
    }

    [Fact]
    public void 质量管理_含理化检测整组与质量证明书()
    {
        var quality = Node("质量管理");
        quality.Children.Should().ContainSingle(n => n.Label == "质量证明书" && n.Href == "/quality/certificates");

        var physical = quality.Children.Single(n => n.Label == "理化检测");
        physical.IsLeaf.Should().BeFalse();
        physical.Children.Select(c => c.Label).Should().Equal(
            "化学检验", "硬度检验", "晶粒度检验", "点腐蚀检验", "晶间腐蚀检验",
            "室温拉伸检验", "金相检验", "压扁检验", "扩口检验");

        // 炉号/化学 子组仍在
        quality.Children.Should().ContainSingle(n => n.Label == "炉号/化学" && !n.IsLeaf);
    }

    [Fact]
    public void 工单管理_二级分组_操作与查询()
    {
        var wo = Node("工单管理");
        wo.IsLeaf.Should().BeFalse();
        wo.Policy.Should().Be(Roles.Policies.WorkOrderMenu);

        var operate = wo.Children.Single(n => n.Label == "工单操作");
        operate.IsLeaf.Should().BeFalse();
        operate.Children.Select(n => (n.Label, n.Href)).Should().Equal(
            ("工单生成", "/workorders"),
            ("用料计划", "/material-plan-overview"),
            ("用料投料核查", "/material-input-consistency"),
            ("工单需求调整", "/workorders-demand-adjustment"));

        var query = wo.Children.Single(n => n.Label == "工单查询");
        query.IsLeaf.Should().BeFalse();
        query.Children.Select(n => (n.Label, n.Href)).Should().Equal(
            ("工单执行状况", "/workorder-execution"),
            ("定尺工单定尺", "/fixed-length-work-order-view"));
    }

    [Fact]
    public void 批次管理组_生产批次与生产执行核查并列()
    {
        var batch = Node("批次管理");
        batch.IsLeaf.Should().BeFalse();
        batch.Policy.Should().Be(Roles.Policies.BatchMenu);
        batch.Children.Select(n => (n.Label, n.Href)).Should().Equal(
            ("生产批次", "/batches"),
            ("生产执行核查", "/production-execution-check"),
            ("生产记录", "/production-records"),
            ("去油酸洗", "/pickling-in-records"),
            ("工段委外", "/section-outsources"),
            ("工艺卡打印", "/process-card-print"));

        // 旧菜单名「批次首页」全树不得残留
        AppMenu.AllLeaves().Should().NotContain(n => n.Label == "批次首页");
        AppMenu.AllLeaves().Count(n => n.Label == "生产批次").Should().Be(1);
        AppMenu.AllLeaves().Count(n => n.Label == "生产执行核查").Should().Be(1);
    }

    [Fact]
    public void 手机曾缺失的模块_均在树内()
    {
        // 首页（精确匹配 /）
        var home = Node("首页");
        home.IsLeaf.Should().BeTrue();
        home.Href.Should().Be("/");
        home.MatchAll.Should().BeTrue();
        home.Policy.Should().BeNull();

        // 工资结算整组（2026-09-01 新增时曾只加电脑版，漏手机版）
        var salary = Node("工资结算");
        salary.Policy.Should().Be(Roles.Policies.SalaryView);
        salary.Children.Should().Contain(n => n.Label == "考勤表" && n.Href == "/payroll/attendance");
        salary.Children.Count.Should().Be(11);

        // 用户管理单项
        var users = Node("用户管理");
        users.IsLeaf.Should().BeTrue();
        users.Href.Should().Be("/admin/users");
        users.Policy.Should().Be(Roles.Policies.UserView);

        // 数据工具单项
        var dataTool = Node("数据工具");
        dataTool.IsLeaf.Should().BeTrue();
        dataTool.Href.Should().Be("/data-exchange");
        dataTool.Policy.Should().Be(Roles.Policies.DataToolView);
    }

    [Fact]
    public void 扫码组_整组仅登录_工位员工带ScanView()
    {
        var scan = Node("扫码管理");
        scan.Policy.Should().BeNull(); // 整组仅需登录
        scan.Children.Should().Contain(n => n.Label == "扫码报工" && n.Href == "/mobile-report" && n.Policy == null);
        scan.Children.Should().Contain(n => n.Label == "设备扫码" && n.Href == "/equipment-scan" && n.Policy == null);
        scan.Children.Should().Contain(n => n.Label == "工位管理" && n.Href == "/workstations" && n.Policy == Roles.Policies.ScanView);
        scan.Children.Should().Contain(n => n.Label == "员工管理" && n.Href == "/employees" && n.Policy == Roles.Policies.ScanView);
    }

    [Fact]
    public void 根级顺序_与电脑版历史一致()
    {
        AppMenu.Root.Select(n => n.Label).Should().Equal(
            "首页", "订单管理", "工单管理", "计划排程", "批次管理", "质量管理",
            "物料管理", "仓库管理", "设备管理", "生产标准", "报表系统", "数据工具",
            "扫码管理", "工资结算", "参数表", "用户管理");
    }

    [Fact]
    public void 树内叶子无重复目标链接()
    {
        var hrefs = AppMenu.AllLeaves().Where(n => n.Href != null).Select(n => n.Href!);
        hrefs.Should().OnlyHaveUniqueItems();
    }
}
