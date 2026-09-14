using FluentAssertions;
using MES.Blazor.Shared;
using MES.Shared.Constants;

namespace MES.Tests.Components;

/// <summary>
/// 首页「常用入口」清单（AppShortcuts）与菜单树（AppMenu）的一致性回归测试。
///
/// 首页横条是菜单的**子集投影**：Href 必须真实存在、Label 与**有效策略**必须与所指向菜单叶子一致，
/// 否则会出现「菜单改名后首页仍显示旧名」「首页入口比侧栏菜单宽（越权暴露）或更窄（该有的看不到）」
/// 这类静默漂移。改动菜单或常用入口请同步更新这些断言。
///
/// ⚠️ 注意 AppMenu 的角色策略挂在**分组节点**上（例：/orders 叶子 Policy=null，门控在「订单管理」组），
/// 因此必须比较「叶子自身 Policy ?? 最近祖先分组 Policy」，不能直接比叶子自身 Policy。
/// </summary>
public class AppShortcutsTests
{
    private sealed record EffectiveLeaf(AppMenuNode Leaf, string? EffectivePolicy);

    /// <summary>Href → (叶子, 有效策略)。有效策略 = 自身 Policy ?? 最近祖先分组 Policy。</summary>
    private static Dictionary<string, EffectiveLeaf> EffectiveLeaves()
    {
        var result = new Dictionary<string, EffectiveLeaf>(StringComparer.OrdinalIgnoreCase);

        void Walk(IEnumerable<AppMenuNode> nodes, string? inherited)
        {
            foreach (var node in nodes)
            {
                var effective = node.Policy ?? inherited;
                if (node.IsLeaf)
                    result[node.Href!] = new EffectiveLeaf(node, effective);
                else
                    Walk(node.Children, effective);
            }
        }

        Walk(AppMenu.Root, null);
        return result;
    }

    [Fact]
    public void 每条常用入口_必须指向真实菜单叶子_且标签与有效策略一致()
    {
        var leaves = EffectiveLeaves();

        foreach (var item in AppShortcuts.Items)
        {
            item.Href.Should().NotBeNullOrWhiteSpace($"常用入口「{item.Label}」必须有跳转地址");
            leaves.Should().ContainKey(item.Href!, $"常用入口「{item.Label}」必须指向菜单树中真实存在的叶子");

            var leaf = leaves[item.Href!];
            item.Label.Should().Be(leaf.Leaf.Label, $"常用入口「{item.Label}」的名称应与菜单叶子一致");
            item.Policy.Should().Be(leaf.EffectivePolicy,
                $"常用入口「{item.Label}」的可见性应与侧栏菜单一致（有效策略 = 自身 ?? 分组门控）");
        }
    }

    [Fact]
    public void 常用入口_无重复且不做全量菜单复刻()
    {
        AppShortcuts.Items.Select(i => i.Href).Should().OnlyHaveUniqueItems();

        // 全树约 90 叶；常用入口只放高频少数，超过 20 条即失去「常用」意义（改清单前先确认这是有意为之）
        AppShortcuts.Items.Count.Should().BeLessThan(20);
    }

    [Fact]
    public void 常用入口_仅扫码组三项无角色策略()
    {
        // 与扫码组一致：三项扫码入口仅需登录（Policy=null），其余入口必须带策略以便按角色过滤。
        // 注意：该断言是「扫码组不挂档」这一拍板的护栏 —— 若把扫码入口改挂 ScanView 档，
        // 侧栏菜单仍仅需登录，首页就会比菜单窄（该有的看不到）。
        var loginOnly = AppShortcuts.Items.Where(i => i.Policy is null).Select(i => i.Href).ToList();
        loginOnly.Should().BeEquivalentTo(new[] { "/mobile-report", "/mobile-quality/patrol", "/mobile-quality/feedback" });
    }

    [Fact]
    public void 常用入口_覆盖各主要生产角色域()
    {
        // 防止清单被改成只剩一两个域（如全放质量），导致其它角色首页磁贴区为空
        var policies = AppShortcuts.Items.Select(i => i.Policy).ToHashSet();
        policies.Should().Contain(Roles.Policies.ReportView);
        policies.Should().Contain(Roles.Policies.OrderMenu);
        policies.Should().Contain(Roles.Policies.WorkOrderMenu);
        policies.Should().Contain(Roles.Policies.SchedulingMenu);
        policies.Should().Contain(Roles.Policies.QualityMenu);
        policies.Should().Contain(Roles.Policies.WarehouseMenu);
    }

    [Fact]
    public void 常用入口_每条都带图标与强调色()
    {
        // 首页磁贴是「图标 + 名称」结构，缺图标会渲染成空白方块
        foreach (var item in AppShortcuts.Items)
        {
            // MudBlazor 6.x 的 Icons.Material.Filled.* 常量值是 SVG path 标记（非图标名串）
            item.Icon.Should().StartWith("<", $"常用入口「{item.Label}」必须配置 Icons.Material.Filled.* 图标");
            item.Accent.Should().MatchRegex("^#[0-9A-Fa-f]{6}$", $"常用入口「{item.Label}」的强调色须为 #RRGGBB");
        }
    }
}
