using FluentAssertions;
using MES.Core.DTOs.Configuration;
using MES.Core.Helpers;
using Xunit;

namespace MES.Tests;

/// <summary>
/// 操作人候选「生产工段 + 工序组」双条件匹配纯函数测试（扫码端与 PC 手工录入端共享）。
/// </summary>
public class OperatorMatchHelperTests
{
    private static EmployeeDto Emp(string? sectionName = null, string? groupName = null) => new()
    {
        Id = 1,
        Code = "1001",
        Name = "张三",
        SectionName = sectionName,
        GroupName = groupName,
        IsActive = true
    };

    // ==================== MatchesSection ====================

    [Fact]
    public void MatchesSection_单工段命中()
    {
        OperatorMatchHelper.MatchesSection(Emp("Cut"), "Cut").Should().BeTrue();
    }

    [Fact]
    public void MatchesSection_多工段逗号串任一命中()
    {
        OperatorMatchHelper.MatchesSection(Emp("Cut,Pickle"), "Pickle").Should().BeTrue();
    }

    [Fact]
    public void MatchesSection_不命中()
    {
        OperatorMatchHelper.MatchesSection(Emp("Cut"), "Pickle").Should().BeFalse();
    }

    [Fact]
    public void MatchesSection_员工未配工段不匹配()
    {
        OperatorMatchHelper.MatchesSection(Emp(null), "Cut").Should().BeFalse();
    }

    [Fact]
    public void MatchesSection_大小写不敏感()
    {
        OperatorMatchHelper.MatchesSection(Emp("ColdRollDraw"), "coldrolldraw").Should().BeTrue();
    }

    [Fact]
    public void MatchesSection_目标工段为空不匹配()
    {
        // MatchesSection 只在目标工段非空时被调用（GetRowOperatorOptions 空工段外层通配）
        OperatorMatchHelper.MatchesSection(Emp("Cut"), null).Should().BeFalse();
    }

    [Fact]
    public void MatchesSection_逗号串剔除空段与首尾空白()
    {
        OperatorMatchHelper.MatchesSection(Emp(" Cut ,, Pickle "), "Pickle").Should().BeTrue();
        OperatorMatchHelper.MatchesSection(Emp(" Cut ,, "), "Pickle").Should().BeFalse();
    }

    // ==================== MatchesProcessGroup ====================

    [Fact]
    public void MatchesProcessGroup_员工工序组为空_通配()
    {
        OperatorMatchHelper.MatchesProcessGroup(Emp("Cut", null), "ColdRoll60").Should().BeTrue();
        OperatorMatchHelper.MatchesProcessGroup(Emp("Cut", "   "), "ColdRoll60").Should().BeTrue();
    }

    [Fact]
    public void MatchesProcessGroup_工序组命中()
    {
        OperatorMatchHelper.MatchesProcessGroup(Emp("Cut", "ColdRoll60"), "ColdRoll60").Should().BeTrue();
    }

    [Fact]
    public void MatchesProcessGroup_多工序组逗号串任一命中()
    {
        OperatorMatchHelper.MatchesProcessGroup(Emp("Cut", "ColdRoll60,ColdRoll80"), "ColdRoll80").Should().BeTrue();
    }

    [Fact]
    public void MatchesProcessGroup_不命中()
    {
        OperatorMatchHelper.MatchesProcessGroup(Emp("Cut", "ColdRoll60"), "Piercing").Should().BeFalse();
    }

    [Fact]
    public void MatchesProcessGroup_大小写不敏感()
    {
        OperatorMatchHelper.MatchesProcessGroup(Emp("Cut", "ColdRoll60"), "coldroll60").Should().BeTrue();
    }

    [Fact]
    public void MatchesProcessGroup_目标工序为空_仅空工序组通配()
    {
        OperatorMatchHelper.MatchesProcessGroup(Emp("Cut", null), null).Should().BeTrue();
        OperatorMatchHelper.MatchesProcessGroup(Emp("Cut", "ColdRoll60"), null).Should().BeFalse();
    }
}
