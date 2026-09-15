using MES.Shared.Constants;
using MudBlazor;

namespace MES.Blazor.Shared;

/// <summary>
/// 首页「常用入口」单条。
/// 不复用 <see cref="AppMenuNode"/>：菜单节点只有 Label/Href/Policy，而首页入口要做成图标磁贴，
/// 需要额外的图标与强调色（菜单树若加图标字段会牵动侧栏/手机两套渲染）。
/// </summary>
public sealed class AppShortcut
{
    /// <summary>必须与所指向菜单叶子的 Label 完全一致（AppShortcutsTests 断言）。</summary>
    public required string Label { get; init; }

    /// <summary>必须已存在于 <see cref="AppMenu.AllLeaves"/>。</summary>
    public required string Href { get; init; }

    /// <summary>见 <see cref="AppShortcuts"/> 注释第 2 条；null = 仅需登录。</summary>
    public string? Policy { get; init; }

    /// <summary>磁贴图标（<c>Icons.Material.Filled.*</c>）。</summary>
    public string Icon { get; init; } = "";

    /// <summary>图标强调色（Material 调色板十六进制）。</summary>
    public string Accent { get; init; } = "#1976D2";
}

/// <summary>
/// 首页「常用入口」清单（2026-09-10 首版，2026-09-10 二次收敛为 9 项，2026-09-14 补齐扫码组为 11 项）：
/// 面向日常高频操作的少数直达入口，**不是全量菜单复刻**
/// （AppMenu 全树约 90 个叶子，平铺既过长又比侧栏难找）。
///
/// 约束：
/// 1. 每条 <see cref="AppShortcut.Href"/> 必须已存在于 <see cref="AppMenu.AllLeaves"/>，
///    Label 与 Href 必须与所指向菜单叶子一致
///    （由 AppShortcutsTests 断言兜底，防止菜单改名/移动后此处静默漂移）；
/// 2. Policy 必须等于该项在菜单树中的**有效策略**（叶子自身 Policy，未设则继承所属分组的 Policy）。
///    例：/orders 叶子自身无 Policy，有效策略是其分组「订单管理」的 OrderMenu。
///    —— 首页入口与侧栏菜单可见性必须严格一致：既不能比菜单宽（越权暴露），也不能比菜单窄（该有的看不到）；
/// 3. Policy 为 null 表示仅需登录（当前为扫码组三项：「报工扫码」「巡检扫码」「不合格反馈扫码」，
///    该组整体不挂档、仅需登录，与侧栏菜单一致）；
/// 4. 渲染侧按 Policy 过滤 → 各角色只看到自己相关的入口，无需按角色维护多份清单。
/// </summary>
public static class AppShortcuts
{
    /// <summary>
    /// 顺序按业务主线排列（报表 → 订单 → 计划 → 质量 → 库存 → 扫码），不严格照抄菜单树顺序，
    /// 扫码组三项（报工/巡检/不合格反馈）相邻排列，便于现场操作员快速定位；
    /// 但每条仍必须是菜单叶子的忠实投影；新增/删除请同步 AppShortcutsTests 断言。
    /// 注意：此处用**分组门控**（XxxMenu，不含 Report 角色），与侧栏菜单完全一致 —— 报表角色本就不该从首页直进数据域页面。
    /// </summary>
    public static readonly IReadOnlyList<AppShortcut> Items =
    [
        new() { Label = "报表总览", Href = "/reports/overview", Policy = Roles.Policies.ReportView, Icon = Icons.Material.Filled.Assessment, Accent = "#00897B" },
        new() { Label = "订单列表", Href = "/orders", Policy = Roles.Policies.OrderMenu, Icon = Icons.Material.Filled.List, Accent = "#1976D2" },
        new() { Label = "工单用料", Href = "/material-plan-overview", Policy = Roles.Policies.WorkOrderMenu, Icon = Icons.Material.Filled.FactCheck, Accent = "#5E35B1" },
        new() { Label = "批次计划", Href = "/batch-plans", Policy = Roles.Policies.SchedulingMenu, Icon = Icons.Material.Filled.CalendarMonth, Accent = "#3949AB" },
        new() { Label = "成检计划", Href = "/final-inspection-plan", Policy = Roles.Policies.SchedulingMenu, Icon = Icons.Material.Filled.Science, Accent = "#00838F" },
        new() { Label = "不合格报告", Href = "/quality/ncr", Policy = Roles.Policies.QualityMenu, Icon = Icons.Material.Filled.WarningAmber, Accent = "#E53935" },
        new() { Label = "原料库", Href = "/warehouse/raw", Policy = Roles.Policies.WarehouseMenu, Icon = Icons.Material.Filled.Inventory2, Accent = "#6D4C41" },
        new() { Label = "成品库", Href = "/warehouse/fg", Policy = Roles.Policies.WarehouseMenu, Icon = Icons.Material.Filled.Warehouse, Accent = "#2E7D32" },
        new() { Label = "报工扫码", Href = "/mobile-report", Icon = Icons.Material.Filled.QrCodeScanner, Accent = "#F57C00" },
        new() { Label = "巡检扫码", Href = "/mobile-quality/patrol", Icon = Icons.Material.Filled.Checklist, Accent = "#00695C" },
        new() { Label = "不合格反馈扫码", Href = "/mobile-quality/feedback", Icon = Icons.Material.Filled.ReportProblem, Accent = "#AD1457" },
    ];
}
