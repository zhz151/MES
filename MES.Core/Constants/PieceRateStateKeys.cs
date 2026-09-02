namespace MES.Core.Constants;

/// <summary>
/// 特殊制造状态的英文 Key 常量（特殊制造状态倍数行）。计件标准中按制造/交货状态的
/// 特殊计价（如光亮管交货状态=光亮 ×1.35），与 PieceRateSectionKeys 同模式：
/// DB 存英文 Key，前端显示中文。可随业务扩展新增状态（退火/固溶等）。
/// 2026-09-02 新增：承接原材料类别中的「光亮管」（本质为交货状态=光亮）。
/// </summary>
public static class PieceRateStateKeys
{
    /// <summary>光亮（交货状态，切挫 ×1.35）</summary>
    public const string Bright = "Bright";

    /// <summary>所有特殊制造状态 Key 的有序列表</summary>
    public static readonly string[] All =
    [
        Bright
    ];

    /// <summary>Key → 规范中文（显示）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese = new Dictionary<string, string>
    {
        [Bright] = "光亮",
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

    /// <summary>是否为合法特殊制造状态 Key（Ordinal）</summary>
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
