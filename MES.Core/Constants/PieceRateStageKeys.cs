namespace MES.Core.Constants;

/// <summary>
/// 作业阶段的英文 Key 常量（生产计件类别第 4 键约束，2026-09-02 重构引入）。
/// 酸洗/去油一段劳动分入缸/出缸两段、操作人不同，各自独立计酬；PicklingInRecord→InTank、PicklingOutRecord→OutTank、
/// 普通报工 ProductionRecord→无阶段。类别 StageKeys 空=全选（含普通报工无阶段，无阶段不需 Key）。
/// DB 存英文 Key，前端显示中文。可随业务扩展新增阶段 Key。
/// </summary>
public static class PieceRateStageKeys
{
    /// <summary>入缸（装缸/酸洗入缸端操作）</summary>
    public const string InTank = "InTank";

    /// <summary>出缸（卸缸/完工出缸端操作）</summary>
    public const string OutTank = "OutTank";

    /// <summary>所有作业阶段 Key 的有序列表（不含「无阶段」——空=全选语义天然覆盖）</summary>
    public static readonly string[] All =
    [
        InTank, OutTank
    ];

    /// <summary>Key → 规范中文（显示）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese = new Dictionary<string, string>
    {
        [InTank] = "入缸",
        [OutTank] = "出缸",
    };

    /// <summary>中文 → Key（供导入/归一化）</summary>
    public static readonly IReadOnlyDictionary<string, string> ChineseToKey = BuildChineseToKey();

    private static IReadOnlyDictionary<string, string> BuildChineseToKey()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in KeyToChinese)
        {
            if (!map.ContainsKey(kvp.Value))
                map[kvp.Value] = kvp.Key;
        }
        return map;
    }

    /// <summary>是否为合法作业阶段 Key（Ordinal）</summary>
    public static bool IsKey(string? value)
        => !string.IsNullOrEmpty(value) && All.Contains(value!, StringComparer.Ordinal);

    /// <summary>中文文本 → 英文 Key；已是 Key 原样返回；未知返回 null。</summary>
    public static string? ToKey(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (IsKey(value)) return value;
        return ChineseToKey.TryGetValue(value, out var key) ? key : null;
    }

    /// <summary>归一为显示中文：Key → 规范中文；已是中文原样返回；未知返回 null。</summary>
    public static string? ToChinese(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (KeyToChinese.TryGetValue(value, out var cn)) return cn;
        return value;
    }
}
