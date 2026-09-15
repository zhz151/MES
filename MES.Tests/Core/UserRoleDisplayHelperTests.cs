using FluentAssertions;
using MES.Blazor.Helpers;
using Xunit;

namespace MES.Tests;

/// <summary>
/// 用户管理界面角色展示 helper 测试（纯一级模型，2026-08-26 用户决策取消二级）：
///  角色 = {菜单前缀}{档位}（Viewer/Editor/Full），Admin 隐式全权。
///  新建默认：9 业务域菜单 = 查；系统菜单（报表/数据工具/扫码/工资结算/参数表/用户）= 无。
/// </summary>
public class UserRoleDisplayHelperTests
{
    [Fact]
    public void GetDefaultTiers_业务域默认查_系统菜单默认无()
    {
        var tiers = UserRoleDisplayHelper.GetDefaultTiers();

        tiers.Should().HaveCount(UserRoleDisplayHelper.MenuTiers.Count);
        // 9 业务域菜单（含仓库）= Viewer（查）
        foreach (var prefix in UserRoleDisplayHelper.DefaultViewerMenus)
            tiers[prefix].Should().Be(UserRoleDisplayHelper.TierViewer, $"{prefix} 默认应为「查」");
        // 系统菜单（Report/DataTool/Scan/Salary/Configuration/User）= None（无，工资结算默认不授权）
        foreach (var menu in UserRoleDisplayHelper.MenuTiers)
        {
            if (!UserRoleDisplayHelper.DefaultViewerMenus.Contains(menu.Prefix))
                tiers[menu.Prefix].Should().Be(UserRoleDisplayHelper.TierNone, $"{menu.DisplayName} 默认应为「无」");
        }
    }

    [Fact]
    public void GetDefaultTiers_业务域恰为9个()
    {
        UserRoleDisplayHelper.DefaultViewerMenus.Should().HaveCount(9);
        UserRoleDisplayHelper.DefaultViewerMenus.Should().Contain("Warehouse");
    }

    [Fact]
    public void MenuTiers_共15个主菜单_含工资结算()
    {
        UserRoleDisplayHelper.MenuTiers.Should().HaveCount(15);
        UserRoleDisplayHelper.MenuTiers.Select(m => m.Prefix).Should().Contain(new[]
        {
            "Order", "WorkOrder", "Scheduling", "Batch", "Quality", "Material", "Warehouse",
            "Equipment", "Standard", "Report", "DataTool", "Scan", "Salary", "Configuration", "User"
        });
        // 工资结算归系统菜单：新建默认「无」（显式授权），与 Roles.Menus 顺序一致（Scan 后 / Configuration 前）
        UserRoleDisplayHelper.MenuTiers.Single(m => m.Prefix == "Salary").DisplayName.Should().Be("工资结算");
        UserRoleDisplayHelper.DefaultViewerMenus.Should().NotContain("Salary");
    }

    [Fact]
    public void MenuTiers_显示名与一级菜单同步_无旧名残留()
    {
        // 防漂移护栏：MenuTiers.DisplayName 是「用户管理 → 角色分配弹窗」与角色列的唯一出口，
        // 一级菜单改名若漏改此处 → 菜单叫新名、用户管理仍显示旧名（Salary 曾因漏加档位致弹窗授不了权）。
        UserRoleDisplayHelper.MenuTiers.Single(m => m.Prefix == "Standard").DisplayName.Should().Be("产品标准"); // 2026-09-15 原「生产标准」
        UserRoleDisplayHelper.MenuTiers.Single(m => m.Prefix == "Batch").DisplayName.Should().Be("生产执行");    // 2026-09-15 原「批次管理」

        UserRoleDisplayHelper.MenuTiers.Should().NotContain(m => m.DisplayName == "生产标准");
        UserRoleDisplayHelper.MenuTiers.Should().NotContain(m => m.DisplayName == "批次管理");
    }

    [Fact]
    public void MenuTiers_顺序与Roles一致_工资结算在Scan与Configuration之间()
    {
        var prefixes = UserRoleDisplayHelper.MenuTiers.Select(m => m.Prefix).ToList();
        prefixes.IndexOf("Salary").Should().BeGreaterThan(prefixes.IndexOf("Scan"));
        prefixes.IndexOf("Salary").Should().BeLessThan(prefixes.IndexOf("Configuration"));
    }

    [Fact]
    public void ParseRoles_BuildRoles_三档往返()
    {
        var roles = new List<string> { "WarehouseFull", "OrderViewer", "Admin", "UnknownRole" };
        var parsed = UserRoleDisplayHelper.ParseRoles(roles);
        parsed.IsAdmin.Should().BeTrue();
        parsed.TierByMenu["Warehouse"].Should().Be(UserRoleDisplayHelper.TierFull);
        parsed.TierByMenu["Order"].Should().Be(UserRoleDisplayHelper.TierViewer);
        parsed.TierByMenu["Batch"].Should().Be(UserRoleDisplayHelper.TierNone); // 未分配 = 无

        var rebuilt = UserRoleDisplayHelper.BuildRoles(parsed.TierByMenu, parsed.IsAdmin);
        rebuilt.Should().Contain("Admin");
        rebuilt.Should().Contain("WarehouseFull");
        rebuilt.Should().Contain("OrderViewer");
        rebuilt.Should().NotContain("UnknownRole"); // 未知角色不重建
    }

    [Fact]
    public void ParseRoles_同一菜单多档_取最高档()
    {
        var roles = new List<string> { "QualityViewer", "QualityEditor", "QualityFull" };
        var parsed = UserRoleDisplayHelper.ParseRoles(roles);
        parsed.TierByMenu["Quality"].Should().Be(UserRoleDisplayHelper.TierFull);

        var rebuilt = UserRoleDisplayHelper.BuildRoles(parsed.TierByMenu, parsed.IsAdmin);
        rebuilt.Should().Contain("QualityFull");
        rebuilt.Should().NotContain("QualityViewer").And.NotContain("QualityEditor");
    }

    [Fact]
    public void ParseRoles_无角色_全None非Admin()
    {
        var parsed = UserRoleDisplayHelper.ParseRoles(Enumerable.Empty<string>());
        parsed.IsAdmin.Should().BeFalse();
        foreach (var menu in UserRoleDisplayHelper.MenuTiers)
            parsed.TierByMenu[menu.Prefix].Should().Be(UserRoleDisplayHelper.TierNone);
    }

    [Fact]
    public void BuildRoles_一级档位None_无输出()
    {
        var tiers = UserRoleDisplayHelper.MenuTiers.ToDictionary(m => m.Prefix, _ => UserRoleDisplayHelper.TierNone);
        var roles = UserRoleDisplayHelper.BuildRoles(tiers, isAdmin: false);
        roles.Should().BeEmpty();
    }

    [Fact]
    public void BuildRoles_仅Admin_输出Admin()
    {
        var tiers = UserRoleDisplayHelper.MenuTiers.ToDictionary(m => m.Prefix, _ => UserRoleDisplayHelper.TierNone);
        var roles = UserRoleDisplayHelper.BuildRoles(tiers, isAdmin: true);
        roles.Should().ContainSingle().Which.Should().Be("Admin");
    }

    [Fact]
    public void GetRoleDisplayName_一级角色显示中文带档位()
    {
        UserRoleDisplayHelper.GetRoleDisplayName("WarehouseViewer").Should().Be("仓库管理-查");
        UserRoleDisplayHelper.GetRoleDisplayName("WarehouseEditor").Should().Be("仓库管理-查增改");
        UserRoleDisplayHelper.GetRoleDisplayName("WarehouseFull").Should().Be("仓库管理-查增改删");
        UserRoleDisplayHelper.GetRoleDisplayName("OrderViewer").Should().Be("订单管理-查");
        UserRoleDisplayHelper.GetRoleDisplayName("SalaryViewer").Should().Be("工资结算-查");
        UserRoleDisplayHelper.GetRoleDisplayName("StandardViewer").Should().Be("产品标准-查"); // 2026-09-15 原「生产标准-查」
        UserRoleDisplayHelper.GetRoleDisplayName("Admin").Should().Be("超级管理员");
        UserRoleDisplayHelper.GetRoleDisplayName("UnknownRole").Should().Be("UnknownRole");
    }
}
