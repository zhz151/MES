using FluentAssertions;
using MES.Blazor.Shared;

namespace MES.Tests.Components;

/// <summary>
/// 手机端「横屏提示条」判定规则（LandscapeHintRule）回归测试。
/// 全站移动策略：除「首页 + 扫码流」外其余页面横屏查看 → 竖屏宽表页应提示。
/// 凡改 LandscapeHintRule 集合或 AppMenu 菜单（新增宽表叶/扫码叶）漏同步，在此暴露。
/// </summary>
public class LandscapeHintRuleTests
{
    [Fact]
    public void 归一化_剥查询哈希规整尾斜杠与空串()
    {
        LandscapeHintRule.NormalizePath("/payroll/attendance?x=1").Should().Be("/payroll/attendance");
        LandscapeHintRule.NormalizePath("/payroll/attendance#top").Should().Be("/payroll/attendance");
        LandscapeHintRule.NormalizePath("payroll/attendance/").Should().Be("/payroll/attendance");
        LandscapeHintRule.NormalizePath(" /a/b/ ").Should().Be("/a/b");
        LandscapeHintRule.NormalizePath("/").Should().Be("/");
        LandscapeHintRule.NormalizePath("").Should().Be("/");
        LandscapeHintRule.NormalizePath(null).Should().Be("/");
    }

    [Fact]
    public void 首页_不在宽页集合()
    {
        LandscapeHintRule.IsWideRoute("/").Should().BeFalse();
    }

    [Fact]
    public void 两个扫码窄叶_不在宽页集合()
    {
        LandscapeHintRule.IsWideRoute("/mobile-report").Should().BeFalse();
        LandscapeHintRule.IsWideRoute("/equipment-scan").Should().BeFalse();
    }

    [Fact]
    public void 仓库进出货扫码流_为窄页含Code详情()
    {
        LandscapeHintRule.IsWideRoute("/warehouse/inbound").Should().BeFalse();
        LandscapeHintRule.IsWideRoute("/warehouse/inbound/ABC").Should().BeFalse();
        LandscapeHintRule.IsWideRoute("/warehouse/outbound").Should().BeFalse();
        LandscapeHintRule.IsWideRoute("/warehouse/outbound/XYZ").Should().BeFalse();
    }

    [Fact]
    public void 登录与设备维修扫码_为窄页()
    {
        LandscapeHintRule.IsWideRoute("/login").Should().BeFalse();
        LandscapeHintRule.IsWideRoute("/equipment-repair").Should().BeFalse();
        LandscapeHintRule.IsWideRoute("/repair-execute").Should().BeFalse();
    }

    [Fact]
    public void 表单子路径_不提示横屏()
    {
        LandscapeHintRule.IsWideRoute("/quality/furnace/create").Should().BeFalse();
        LandscapeHintRule.IsWideRoute("/payroll/wages/piece/edit/123").Should().BeFalse();
    }

    [Fact]
    public void 全菜单叶子_除首页与两个扫码窄叶外_均应按宽页提示()
    {
        // 防漂移：菜单新增宽表页若漏接「竖屏提示横屏」，此处立即失败。
        var narrow = new[] { "/", "/mobile-report", "/equipment-scan" };
        var leaves = AppMenu.AllLeaves()
            .Where(n => n.Href is not null)
            .Select(n => LandscapeHintRule.NormalizePath(n.Href!))
            .Where(href => !narrow.Contains(href))
            .ToList();

        leaves.Should().NotBeEmpty();
        foreach (var href in leaves)
        {
            LandscapeHintRule.IsWideRoute(href)
                .Should().BeTrue($"菜单叶子 {href} 应判定为宽页（竖屏提示横屏）");
        }
    }
}
