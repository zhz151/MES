namespace MES.Core.Constants;

/// <summary>
/// 计划流转判定值常量（段落流转分析 PlanFlowJudgment 派生值）。
/// 计划流转量 > 日产/日流转设定 → 加速，否则 "-"。前后端共享，改名需同步。
/// </summary>
public static class PlanFlowJudgmentKeys
{
    /// <summary>计划流转量超设定 → 加速（前端红色高亮）</summary>
    public const string Accelerate = "加速";

    /// <summary>未超设定 → 占位符 "-"</summary>
    public const string NormalDash = "-";
}
