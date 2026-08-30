namespace MES.Core.Constants;

/// <summary>
/// 看板统计窗口常量：今日/前3日/前7日 汇总口径统一出口。
/// 消除 FinalInspectionService 与 BatchPlanService 等看板摘要的重复窗口定义，
/// 调整窗口天数只需改此处（两处口径保持一致）。
/// </summary>
public static class SummaryWindows
{
    /// <summary>「前7日」窗口起点偏移（窗口含今日，起点 = 今天 − 6，即前 7 个自然日）</summary>
    public const int Last7DaysStartOffset = -6;

    /// <summary>「前3日」窗口起点偏移（不含今日，起点 = 今天 − 3）</summary>
    public const int Last3DaysStartOffset = -3;

    /// <summary>窗口排他结束偏移（含当日全天，结束 = 今天 + 1，即 &lt; 明天）</summary>
    public const int EndExclusiveOffset = 1;
}
