namespace MES.Core.Constants;

/// <summary>
/// 可持续天数充足度状态常量（段落流转分析 Status 派生值）。
/// 可持续天数 &lt; 下限 → 偏少（黄）；&gt; 上限 → 过多（红）；否则正常（绿）。前后端共享，改名需同步。
/// </summary>
public static class SustainStatusKeys
{
    /// <summary>可持续天数低于下限 → 偏少（前端黄色高亮）</summary>
    public const string Insufficient = "偏少";

    /// <summary>可持续天数高于上限 → 过多（前端红色高亮）</summary>
    public const string Excessive = "过多";

    /// <summary>可持续天数在上下限区间 → 正常（前端绿色高亮）</summary>
    public const string Normal = "正常";
}
