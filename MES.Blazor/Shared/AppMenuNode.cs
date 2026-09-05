namespace MES.Blazor.Shared;

/// <summary>
/// 菜单树节点。桌面 MainLayout（MudNavGroup）与手机 MobileLayout（MudExpansionPanel）
/// 共用同一棵树渲染，从根本上避免两份手写菜单漂移（历史：订单组残留「牌号对照」、
/// 手机缺理化检测/工资结算/用户管理等）。
/// </summary>
public class AppMenuNode
{
    public required string Label { get; init; }

    /// <summary>叶子链接；为 null 表示是分组节点（见 Children）。</summary>
    public string? Href { get; init; }

    /// <summary>角色策略（Roles.Policies.*，逗号分隔角色串）。null = 仅需登录即可见。</summary>
    public string? Policy { get; init; }

    /// <summary>首页用 NavLinkMatch.All（精确匹配 /），其余默认前缀匹配。</summary>
    public bool MatchAll { get; init; }

    public IReadOnlyList<AppMenuNode> Children { get; init; } = [];

    /// <summary>叶子 = 有 Href 的导航链接；否则为分组。</summary>
    public bool IsLeaf => Href is not null;
}
