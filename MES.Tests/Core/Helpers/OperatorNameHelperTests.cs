using FluentAssertions;
using MES.Core.Helpers;
using Xunit;

namespace MES.Tests;

/// <summary>
/// 操作人显示串 拆分/解析/匹配/格式化 纯函数测试。
/// </summary>
public class OperatorNameHelperTests
{
    // ==================== Split ====================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Split_空串返回空列表(string? text)
    {
        OperatorNameHelper.Split(text).Should().BeEmpty();
    }

    [Fact]
    public void Split_混合分隔符正确拆分()
    {
        OperatorNameHelper.Split("张三、李四,王五，赵六")
            .Should().Equal("张三", "李四", "王五", "赵六");
    }

    [Fact]
    public void Split_剔除空白段与首尾空白()
    {
        OperatorNameHelper.Split("张三、、,李四 ， 王五 ")
            .Should().Equal("张三", "李四", "王五");
    }

    // ==================== TryParseSegment ====================

    [Fact]
    public void TryParseSegment_含工号解析出姓名与工号()
    {
        OperatorNameHelper.TryParseSegment("张三(1001)", out var name, out var code).Should().BeTrue();
        name.Should().Be("张三");
        code.Should().Be("1001");
    }

    [Fact]
    public void TryParseSegment_纯姓名工号为null()
    {
        OperatorNameHelper.TryParseSegment("张三", out var name, out var code).Should().BeTrue();
        name.Should().Be("张三");
        code.Should().BeNull();
    }

    [Fact]
    public void TryParseSegment_全角括号按纯姓名处理()
    {
        // 全角括号「（）」不匹配半角正则 → 整段按纯姓名
        OperatorNameHelper.TryParseSegment("张三（1001）", out var name, out var code).Should().BeTrue();
        name.Should().Be("张三（1001）");
        code.Should().BeNull();
    }

    [Fact]
    public void TryParseSegment_首尾空格被剔除()
    {
        OperatorNameHelper.TryParseSegment(" 张三(1001) ", out var name, out var code).Should().BeTrue();
        name.Should().Be("张三");
        code.Should().Be("1001");
    }

    [Fact]
    public void TryParseSegment_空白段返回false()
    {
        OperatorNameHelper.TryParseSegment("   ", out _, out _).Should().BeFalse();
    }

    // ==================== Format ====================

    [Fact]
    public void Format_拼接姓名与工号()
    {
        OperatorNameHelper.Format("张三", "1001").Should().Be("张三(1001)");
    }

    // ==================== FindUnmatched ====================

    private static ActiveEmployeeSet BuildActive(
        params (string Name, string Code)[] employees)
    {
        var set = new ActiveEmployeeSet();
        foreach (var (name, code) in employees)
        {
            set.Names.Add(name);
            set.ByCode[code] = name;
        }
        return set;
    }

    [Fact]
    public void FindUnmatched_全部命中返回空()
    {
        var active = BuildActive(("张三", "1001"), ("李四", "1002"));
        OperatorNameHelper.FindUnmatched(active, "张三(1001)、李四(1002)")
            .Should().BeEmpty();
    }

    [Fact]
    public void FindUnmatched_空串与纯姓名命中均通过()
    {
        var active = BuildActive(("张三", "1001"));
        OperatorNameHelper.FindUnmatched(active, null).Should().BeEmpty();
        OperatorNameHelper.FindUnmatched(active, "").Should().BeEmpty();
        OperatorNameHelper.FindUnmatched(active, "张三").Should().BeEmpty();
    }

    [Fact]
    public void FindUnmatched_串号被拒()
    {
        // 工号 1001 归属张三，李四用 1001 属于串号 → 拒绝
        var active = BuildActive(("张三", "1001"), ("李四", "1002"));
        OperatorNameHelper.FindUnmatched(active, "李四(1001)")
            .Should().Equal("李四(1001)");
    }

    [Fact]
    public void FindUnmatched_未命中员工被列出()
    {
        var active = BuildActive(("张三", "1001"));
        OperatorNameHelper.FindUnmatched(active, "张三(1001)、王五(9999)、切割")
            .Should().Equal("王五(9999)", "切割");
    }

    [Fact]
    public void FindUnmatched_工号不存在被拒()
    {
        var active = BuildActive(("张三", "1001"));
        OperatorNameHelper.FindUnmatched(active, "张三(1001A)").Should().Equal("张三(1001A)");
    }

    [Fact]
    public void FindUnmatched_Code大小写不敏感()
    {
        // 员工工号全大写，提交小写仍命中
        var active = BuildActive(("张三", "1001A"));
        OperatorNameHelper.FindUnmatched(active, "张三(1001a)").Should().BeEmpty();
    }

    [Fact]
    public void FindUnmatched_纯姓名大小写不敏感()
    {
        var active = BuildActive(("ZhangSan", "1001"));
        OperatorNameHelper.FindUnmatched(active, "zhangsan").Should().BeEmpty();
    }
}
