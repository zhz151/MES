using MES.Core.Enums;

namespace MES.Core.Helpers;

/// <summary>
/// 班次判定工具 — 按时刻自动判定班次，扫码报工端统一使用，无需手工选择
/// 6:00 ≤ t &lt; 18:00 白班 / 18:00 ≤ t &lt; 24:00 中班 / 0:00 ≤ t &lt; 6:00 夜班
/// </summary>
public static class ShiftHelper
{
    /// <summary>
    /// 按时刻判定班次
    /// </summary>
    public static ShiftType GetShiftByTime(DateTime? time = null)
    {
        var hour = (time ?? DateTime.Now).Hour;
        return hour switch
        {
            >= 6 and < 18 => ShiftType.DayShift,
            >= 18         => ShiftType.MiddleShift,
            _             => ShiftType.NightShift
        };
    }
}
