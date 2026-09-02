using System.Reflection;
using FluentAssertions;
using MES.Shared.Constants;
using Xunit;

namespace MES.Tests;

/// <summary>
/// 角色权限模型（纯一级：14 菜单 × 3 档 + Admin，共 43 角色）策略一致性测试：
///  1) 每个策略常量尾部均含 Admin（隐式全权）
///  2) 每菜单三档包含关系：View ⊇ {Viewer,Editor,Full}；Edit ⊇ {Editor,Full,Admin}；Delete ⊇ {Full,Admin}
///  3) GetAllRoles = Admin + 14 菜单 × 3 档 = 43，且唯一
/// </summary>
public class RolesPoliciesTests
{
    private static IEnumerable<string> AllPolicyValues()
    {
        foreach (var field in typeof(Roles.Policies).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType == typeof(string) && field.GetValue(null) is string s && !string.IsNullOrEmpty(s))
                yield return s;
        }
    }

    private static string GetPolicy(string name)
    {
        var field = typeof(Roles.Policies).GetField(name, BindingFlags.Public | BindingFlags.Static);
        field.Should().NotBeNull($"策略 {name} 应存在");
        return (string)field!.GetValue(null)!;
    }

    [Fact]
    public void EveryPolicy_ContainsAdmin()
    {
        var all = AllPolicyValues().ToList();
        all.Should().NotBeEmpty();
        foreach (var p in all)
        {
            p.Split(',').Should().Contain("Admin", $"策略 [{p}] 应包含 Admin（隐式全权）");
        }
    }

    [Theory]
    [InlineData("Order")]
    [InlineData("WorkOrder")]
    [InlineData("Scheduling")]
    [InlineData("Batch")]
    [InlineData("Quality")]
    [InlineData("Material")]
    [InlineData("Warehouse")]
    [InlineData("Equipment")]
    [InlineData("Standard")]
    [InlineData("Report")]
    [InlineData("DataTool")]
    [InlineData("Scan")]
    [InlineData("Salary")]
    [InlineData("Configuration")]
    [InlineData("User")]
    public void MenuTierPolicies_AreConsistent(string prefix)
    {
        var viewer = $"{prefix}Viewer";
        var editor = $"{prefix}Editor";
        var full = $"{prefix}Full";

        // View：查档至少含 Viewer + Editor + Full（报表数据域另含 Report 角色，故用 Contain 断言）
        var view = GetPolicy($"{prefix}View").Split(',');
        view.Should().Contain(viewer).And.Contain(editor).And.Contain(full);

        // Edit：查增改至少含 Editor + Full + Admin（不含 Viewer）
        var edit = GetPolicy($"{prefix}Edit").Split(',');
        edit.Should().Contain(editor).And.Contain(full).And.Contain("Admin");
        edit.Should().NotContain(viewer);

        // Delete：查增改删至少含 Full + Admin
        var del = GetPolicy($"{prefix}Delete").Split(',');
        del.Should().Contain(full).And.Contain("Admin");

        // 三档包含关系：高级档含低级档能力由策略包含关系体现
        view.Should().Contain(full);
        edit.Should().Contain(full);
        del.Should().Contain(full);
    }

    [Fact]
    public void GetAllRoles_ReturnsAllMenuTierRolesAndAdmin()
    {
        var all = Roles.GetAllRoles();
        // Admin + 15 菜单 × 3 档 = 1 + 45 = 46
        all.Should().HaveCount(46);
        all.Should().Contain(Roles.Admin);
        foreach (var prefix in new[]
                 {
                     "Order", "WorkOrder", "Scheduling", "Batch", "Quality", "Material", "Warehouse",
                     "Equipment", "Standard", "Report", "DataTool", "Scan", "Salary", "Configuration", "User"
                 })
        {
            all.Should().Contain($"{prefix}Viewer").And.Contain($"{prefix}Editor").And.Contain($"{prefix}Full");
        }
        all.Should().OnlyHaveUniqueItems();
    }
}
