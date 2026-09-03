using FluentAssertions;
using MES.Blazor.Helpers;
using Xunit;

namespace MES.Tests.Blazor;

/// <summary>
/// DisplayHelper 前端显示辅助方法单测。
/// 覆盖 NCR 反馈人「姓名(编号)」→ 纯姓名简化裁剪。
/// </summary>
public class DisplayHelperTests
{
    [Theory]
    [InlineData("殷海红(YG044)", "殷海红")]
    [InlineData("钱利(YG001)", "钱利")]
    [InlineData("薛立(YG005)", "薛立")]
    [InlineData("王五(AB123)", "王五")]
    [InlineData("赵六(YG02)", "赵六")]
    public void FormatPersonName_实名串裁剪为纯姓名(string full, string expected)
    {
        DisplayHelper.FormatPersonName(full).Should().Be(expected);
    }

    [Theory]
    [InlineData("张三")]
    [InlineData("李四")]
    [InlineData("  ") ]
    [InlineData(null)]
    public void FormatPersonName_无括号实名原样返回(string? full)
    {
        DisplayHelper.FormatPersonName(full).Should().Be(full?.Trim() ?? "");
    }

    [Theory]
    [InlineData("张三(夜班)")]
    [InlineData("李四(白班)")]
    [InlineData("王五(组)")]
    public void FormatPersonName_括号内中文不裁剪(string full)
    {
        DisplayHelper.FormatPersonName(full).Should().Be(full);
    }
}
