using MES.Shared.Constants;

namespace MES.Blazor.Helpers;

/// <summary>
/// 用户管理界面角色展示/解析/构建 helper。
/// 角色模型：14 主菜单 × 3 档（Viewer=查 / Editor=查增改 / Full=查增改删）+ Admin（隐式全权）。
/// 纯一级模型（2026-08-26 用户决策，取消全部二级菜单权限）：角色 = {菜单前缀}{档位}。
/// </summary>
public static class UserRoleDisplayHelper
{
    /// <summary>档位：无（无权限）</summary>
    public const string TierNone = "None";
    /// <summary>档位：查（仅查看）</summary>
    public const string TierViewer = "Viewer";
    /// <summary>档位：查增改（查看+新增+修改+审批/刷新，不含删除）</summary>
    public const string TierEditor = "Editor";
    /// <summary>档位：查增改删（全量 CRUD）</summary>
    public const string TierFull = "Full";

    public static readonly IReadOnlyList<string> TierOptions = new[] { TierNone, TierViewer, TierEditor, TierFull };

    /// <summary>新建用户默认档位「查」的菜单前缀：9 业务域菜单；其余系统菜单默认「无」</summary>
    public static readonly IReadOnlySet<string> DefaultViewerMenus = new HashSet<string>(StringComparer.Ordinal)
    {
        "Order", "WorkOrder", "Scheduling", "Batch", "Quality",
        "Material", "Equipment", "Standard", "Warehouse",
    };

    /// <summary>返回新建用户的默认档位映射（业务域菜单=Viewer「查」/ 其余系统菜单=None），返回全新字典供调用方修改。</summary>
    public static Dictionary<string, string> GetDefaultTiers()
    {
        var tiers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var menu in MenuTiers)
        {
            tiers[menu.Prefix] = DefaultViewerMenus.Contains(menu.Prefix) ? TierViewer : TierNone;
        }
        return tiers;
    }

    /// <summary>14 主菜单（前缀 + 中文名），顺序与 Roles.Menus / 菜单栏一致。</summary>
    public static readonly IReadOnlyList<MenuTier> MenuTiers = new List<MenuTier>
    {
        new("Order", "订单管理"),
        new("WorkOrder", "工单管理"),
        new("Scheduling", "计划排程"),
        new("Batch", "批次管理"),
        new("Quality", "质量管理"),
        new("Material", "物料管理"),
        new("Warehouse", "仓库管理"),
        new("Equipment", "设备管理"),
        new("Standard", "生产标准"),
        new("Report", "报表系统"),
        new("DataTool", "数据工具"),
        new("Scan", "扫码管理"),
        new("Configuration", "参数表"),
        new("User", "用户管理"),
    };

    public sealed record MenuTier(string Prefix, string DisplayName);

    /// <summary>档位是否为有效一级权限（查/查增改/查增改删），None 返回 false</summary>
    public static bool HasTier(string? tier) => tier is TierViewer or TierEditor or TierFull;

    /// <summary>档位中文显示：None→无 / Viewer→查 / Editor→查增改 / Full→查增改删</summary>
    public static string GetTierDisplayName(string? tier) => tier switch
    {
        TierNone => "无",
        TierViewer => "查",
        TierEditor => "查增改",
        TierFull => "查增改删",
        _ => tier ?? "",
    };

    /// <summary>角色 → 中文显示名。Admin=超级管理员；{前缀}{档位}=「{菜单名}-{档位中文}」；未知原样返回。</summary>
    public static string GetRoleDisplayName(string role)
    {
        if (role == Roles.Admin) return "超级管理员";
        foreach (var menu in MenuTiers)
        {
            foreach (var tier in new[] { TierViewer, TierEditor, TierFull })
            {
                if (string.Equals(role, menu.Prefix + tier, StringComparison.Ordinal))
                    return $"{menu.DisplayName}-{GetTierDisplayName(tier)}";
            }
        }
        return role;
    }

    /// <summary>
    /// 从用户角色列表反向解析出「每菜单档位」+「是否 Admin」。
    /// 用于编辑弹窗预填（旧模型角色/未知角色忽略）。匹配 {前缀}{档位} 一级角色。
    /// </summary>
    public static (Dictionary<string, string> TierByMenu, bool IsAdmin) ParseRoles(IEnumerable<string> roles)
    {
        var tierByMenu = MenuTiers.ToDictionary(m => m.Prefix, _ => TierNone);
        var isAdmin = false;
        foreach (var role in roles)
        {
            if (role == Roles.Admin) { isAdmin = true; continue; }
            foreach (var menu in MenuTiers)
            {
                if (role.Length > menu.Prefix.Length
                    && role.StartsWith(menu.Prefix, StringComparison.Ordinal))
                {
                    var suffix = role[menu.Prefix.Length..];
                    if (TierOptions.Contains(suffix) && suffix != TierNone)
                        tierByMenu[menu.Prefix] = suffix;
                }
            }
        }
        return (tierByMenu, isAdmin);
    }

    /// <summary>根据「每菜单档位」+「是否 Admin」构建角色列表（供创建/更新请求提交）。</summary>
    public static List<string> BuildRoles(Dictionary<string, string> tierByMenu, bool isAdmin)
    {
        var roles = new List<string>();
        if (isAdmin) roles.Add(Roles.Admin);
        foreach (var menu in MenuTiers)
        {
            if (HasTier(tierByMenu.GetValueOrDefault(menu.Prefix)))
                roles.Add(menu.Prefix + tierByMenu[menu.Prefix]);
        }
        return roles;
    }
}
