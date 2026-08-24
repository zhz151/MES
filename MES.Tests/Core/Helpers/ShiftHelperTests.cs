using FluentAssertions;
using MES.Core.Enums;
using MES.Core.Helpers;
using Xunit;

namespace MES.Tests;

/// <summary>
/// 班次自动判定静态测试：6:00-18:00 白班 / 18:00-24:00 中班 / 0:00-6:00 夜班，边界与时段抽样。
/// </summary>
public class ShiftHelperTests
{
    [Theory]
    [InlineData(0, 0, ShiftType.NightShift)]    // 00:00 夜班下边界
    [InlineData(5, 59, ShiftType.NightShift)]   // 05:59 夜班
    [InlineData(6, 0, ShiftType.DayShift)]      // 06:00 白班下边界
    [InlineData(12, 0, ShiftType.DayShift)]     // 白班时段抽样
    [InlineData(17, 59, ShiftType.DayShift)]    // 17:59 白班
    [InlineData(18, 0, ShiftType.MiddleShift)]  // 18:00 中班下边界
    [InlineData(21, 0, ShiftType.MiddleShift)]  // 中班时段抽样
    [InlineData(23, 59, ShiftType.MiddleShift)] // 23:59 中班
    public void GetShiftByTime_按时刻判定班次(int hour, int minute, ShiftType expected)
    {
        var time = new DateTime(2026, 8, 24, hour, minute, 0);

        var shift = ShiftHelper.GetShiftByTime(time);

        shift.Should().Be(expected);
    }

    [Fact]
    public void GetShiftByTime_默认参数_使用当前时间()
    {
        // 不传参数不抛异常且返回合法班次值
        var shift = ShiftHelper.GetShiftByTime();

        shift.Should().BeOneOf(ShiftType.DayShift, ShiftType.MiddleShift, ShiftType.NightShift);
    }
}
