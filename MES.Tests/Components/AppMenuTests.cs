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

        // 不合格处置（2026-09-13 收为三级子组）：反馈上报 → 报告判定 是同一条闭环的两端
        var nonconforming = quality.Children.Single(n => n.Label == "不合格处置");
        nonconforming.IsLeaf.Should().BeFalse();
        // 子组自身不设 Policy → 有效策略仍回退到「质量管理」的 QualityMenu，权限零变更
        nonconforming.Policy.Should().BeNull();
        nonconforming.Children.Select(n => (n.Label, n.Href)).Should().Equal(
            ("不合格反馈", "/quality/nonconforming-feedback"),
            ("不合格报告", "/quality/ncr"));
        AppMenu.AllLeaves().Count(n => n.Href == "/quality/nonconforming-feedback").Should().Be(1);

        // 位置：排在「成检追踪」之后（检验动作在前，异常处置在后）
        var trackingIndex = quality.Children.Select((n, i) => (n, i)).First(x => x.n.Label == "成检追踪").i;
        var nonconformingIndex = quality.Children.Select((n, i) => (n, i)).First(x => x.n.Label == "不合格处置").i;
        nonconformingIndex.Should().BeGreaterThan(trackingIndex);

        // 巡检（2026-09-11 新增）：质量管理首项，位于「过程检验」之前
        quality.Children.Should().ContainSingle(n => n.Label == "巡检" && n.Href == "/quality/inspection-patrol");
        var patrolIndex = quality.Children.Select((n, i) => (n, i)).First(x => x.n.Label == "巡检").i;
        var processIndex = quality.Children.Select((n, i) => (n, i)).First(x => x.n.Label == "过程检验").i;
        patrolIndex.Should().BeLessThan(processIndex);
        AppMenu.AllLeaves().Count(n => n.Href == "/quality/inspection-patrol").Should().Be(1);
    }

    [Fact]
    public void 工单管理_六项并列二级_无子分组()
    {
        var wo = Node("工单管理");
        wo.IsLeaf.Should().BeFalse();
        wo.Policy.Should().Be(Roles.Policies.WorkOrderMenu);

        // 2026-09-15 拍平：原「工单操作 / 工单查询」两个三级分组取消，6 项直接为二级叶子
        wo.Children.Select(n => (n.Label, n.Href)).Should().Equal(
            ("工单生成", "/workorders"),
            ("需求调整", "/workorders-demand-adjustment"),
            ("工单用料", "/material-plan-overview"),
            ("用投料核查", "/material-input-consistency"),
            ("查询工单执行", "/workorder-execution"),
            ("查询定尺工单", "/fixed-length-work-order-view"));

        wo.Children.Should().OnlyContain(n => n.IsLeaf);
        // 旧分组名全树（含非叶节点）不得残留
        AppMenu.Find("工单操作").Should().BeNull();
        AppMenu.Find("工单查询").Should().BeNull();
    }

    [Fact]
    public void 生产执行组_生产批次与生产执行核查并列()
    {
        // 2026-09-15 用户决策：原「批次管理」更名「生产执行」（组内 7 项全属生产执行动作，
        // 且与「计划排程」构成 计划→执行 关系）；角色代码 BatchViewer/Editor/Full 未改。
        var batch = Node("生产执行");
        batch.IsLeaf.Should().BeFalse();
        batch.Policy.Should().Be(Roles.Policies.BatchMenu);
        batch.Children.Select(n => (n.Label, n.Href)).Should().Equal(
            ("生产批次", "/batches"),
            ("生产执行核查", "/production-execution-check"),
            ("生产记录", "/production-records"),
            ("去油酸洗", "/pickling-in-records"),
            ("工段委外", "/section-outsources"),
            ("委外单位管理", "/outsource-vendors"),
            ("工艺卡打印", "/process-card-print"));

        // 旧菜单名「批次首页」全树不得残留
        AppMenu.AllLeaves().Should().NotContain(n => n.Label == "批次首页");
        AppMenu.AllLeaves().Count(n => n.Label == "生产批次").Should().Be(1);
        AppMenu.AllLeaves().Count(n => n.Label == "生产执行核查").Should().Be(1);
        AppMenu.AllLeaves().Count(n => n.Label == "委外单位管理").Should().Be(1);
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
        scan.Children.Should().Contain(n => n.Label == "报工扫码" && n.Href == "/mobile-report" && n.Policy == null);
        scan.Children.Should().Contain(n => n.Label == "设备扫码" && n.Href == "/equipment-scan" && n.Policy == null);

        // 巡检 / 不合格反馈扫码（2026-09-11 新增）：生产现场反馈，仅需登录不带质量角色档
        scan.Children.Should().Contain(n => n.Label == "巡检扫码" && n.Href == "/mobile-quality/patrol" && n.Policy == null);
        scan.Children.Should().Contain(n => n.Label == "不合格反馈扫码" && n.Href == "/mobile-quality/feedback" && n.Policy == null);

        scan.Children.Should().Contain(n => n.Label == "工位管理" && n.Href == "/workstations" && n.Policy == Roles.Policies.ScanView);
        scan.Children.Should().Contain(n => n.Label == "员工管理" && n.Href == "/employees" && n.Policy == Roles.Policies.ScanView);
    }

    [Fact]
    public void 根级顺序_与电脑版历史一致()
    {
        AppMenu.Root.Select(n => n.Label).Should().Equal(
            "首页", "报表总览", "订单管理", "工单管理", "计划排程", "生产执行", "质量管理",
            "物料管理", "仓库管理", "设备管理", "生产标准", "扫码管理",
            "工资结算", "参数表", "数据工具", "用户管理");
    }

    [Fact]
    public void 树内叶子无重复目标链接()
    {
        var hrefs = AppMenu.AllLeaves().Where(n => n.Href != null).Select(n => n.Href!);
        hrefs.Should().OnlyHaveUniqueItems();
    }
}
