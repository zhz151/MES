namespace MES.Blazor.Shared;

/// <summary>
/// 手机端「横屏提示条」纯规则：决定当前相对路径是否属于「竖屏应提示横屏」的宽表页。
/// 全站移动策略（2026-09-06 拍板）：除「首页 + 扫码流」外，其余页面一律横向查看——
/// 宽表竖屏物理上不可读，横屏（innerWidth≥700）由 ResponsiveLayout 自动切桌面宽表。
/// 本类只管「哪条路径值得提示」的判定，与 AppMenu 单向依赖（宽页 = 全部叶子 - 首页 - 窄叶），
/// 菜单新增宽表页漏接提示由 LandscapeHintRuleTests 防漂移断言兜底。
/// </summary>
public static class LandscapeHintRule
{
    /// <summary>提示条「知道了」的本地持久键（blazored localStorage，无历史前缀惯例，全新键）。</summary>
    public const string DismissedStorageKey = "landscape_hint_dismissed";

    /// <summary>竖屏本就流畅的扫码菜单叶子：不提示横屏。</summary>
    private static readonly HashSet<string> NarrowMenuLeafHrefs = new(StringComparer.OrdinalIgnoreCase)
    {
        "/mobile-report",   // 扫码报工（点选流）
        "/equipment-scan",  // 设备扫码
    };

    /// <summary>非菜单的窄页（登录/维修扫码点选流），精确命中即窄页。</summary>
    private static readonly HashSet<string> NarrowNonMenuExact = new(StringComparer.OrdinalIgnoreCase)
    {
        "/login",             // 登录页
        "/equipment-repair",  // 设备维修点选流
        "/repair-execute",    // 维修执行点选流
    };

    /// <summary>非菜单窄页前缀（进出货扫码流，含 /{Code} 详情），精确或 `前缀/` 命中即窄页。</summary>
    private static readonly string[] NarrowNonMenuPrefixes =
    {
        "/warehouse/inbound",
        "/warehouse/outbound",
    };

    /// <summary>
    /// 宽页集合：全部菜单叶子，剔除首页与两个扫码窄叶。
    /// 惰性求值一次；命中 = 精确匹配，或 `leaf + "/"` 前缀（覆盖 {leaf}/{id} 详情路由）。
    /// </summary>
    private static readonly Lazy<HashSet<string>> WideLeafHrefs = new(() =>
    {
        var narrow = new HashSet<string>(NarrowMenuLeafHrefs, StringComparer.OrdinalIgnoreCase) { "/" };
        var leaves = AppMenu.AllLeaves()
            .Where(n => n.Href is not null)
            .Select(n => NormalizePath(n.Href!))
            .Where(href => !narrow.Contains(href));
        return new HashSet<string>(leaves, StringComparer.OrdinalIgnoreCase);
    });

    /// <summary>
    /// 路径归一化：剥 query/hash、去首尾空白、去尾斜杠、确保前导 "/"。
    /// 空串/纯 "/" 归一为 "/"。
    /// </summary>
    public static string NormalizePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return "/";
        var path = relativePath.Trim();
        var q = path.IndexOfAny(['?', '#']);
        if (q >= 0)
            path = path[..q];
        path = path.TrimEnd('/');
        if (path.Length == 0)
            return "/";
        if (!path.StartsWith('/'))
            path = "/" + path;
        return path;
    }

    /// <summary>表单子路径（含 /create 或 /edit）→ 表单窄页，不提示横屏。</summary>
    public static bool IsFormPath(string normalizedPath)
        => normalizedPath.Contains("/create", StringComparison.OrdinalIgnoreCase)
           || normalizedPath.Contains("/edit", StringComparison.OrdinalIgnoreCase);

    /// <summary>窄页判定（精确集合 + 前缀集合），入参须已归一化。</summary>
    public static bool IsNarrowPath(string normalizedPath)
    {
        if (NarrowMenuLeafHrefs.Contains(normalizedPath) || NarrowNonMenuExact.Contains(normalizedPath))
            return true;
        foreach (var prefix in NarrowNonMenuPrefixes)
        {
            if (string.Equals(normalizedPath, prefix, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 是否「竖屏应提示横屏」的宽表页：命中菜单宽叶（含子路径前缀）且非表单。
    /// 窄叶/窄非菜单路由天然不在宽叶集合内 → 返回 false。
    /// </summary>
    public static bool IsWideRoute(string normalizedPath)
    {
        if (IsFormPath(normalizedPath))
            return false;
        if (WideLeafHrefs.Value.Contains(normalizedPath))
            return true;
        foreach (var leaf in WideLeafHrefs.Value)
        {
            if (normalizedPath.StartsWith(leaf + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
