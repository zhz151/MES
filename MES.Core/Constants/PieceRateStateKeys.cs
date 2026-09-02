using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.Constants;

/// <summary>
/// 特殊制造/交货状态（计件「特殊状态」维）英文稳定 Key。
/// 2026-09-02 对齐 MES 制造状态枚举 <see cref="DeliveryState"/> 全集：下拉可配置全部交货状态
/// （固溶酸洗系列/光亮系列/硬态/固溶矫直等），逐状态一档一系数，未配置 = 系数 1。
/// 存量旧数据仅使用 Bright（光亮 ×1.35），对齐后不受影响。存储英文枚举名，前端显示中文。
/// </summary>
public static class PieceRateStateKeys
{
    /// <summary>光亮（交货状态，切挫 ×1.35）——存量唯一使用值，保留常量引用</summary>
    public const string Bright = nameof(DeliveryState.Bright);

    /// <summary>所有特殊制造状态 Key 的有序列表（= DeliveryState 枚举全集）</summary>
    public static readonly string[] All;

    /// <summary>Key → 规范中文（显示，取自 EnumHelper.DeliveryState 映射）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese;

    /// <summary>中文 → Key（供导入/归一化）</summary>
    public static readonly IReadOnlyDictionary<string, string> ChineseToKey;

    private static readonly HashSet<string> KeySet;

    static PieceRateStateKeys()
    {
        All = Enum.GetNames<DeliveryState>();
        var toCn = new Dictionary<string, string>(StringComparer.Ordinal);
        var toKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in All)
        {
            var cn = EnumHelper.GetDisplayName<DeliveryState>(key) ?? key;
            toCn[key] = cn;
            if (!toKey.ContainsKey(cn))
                toKey[cn] = key;
        }
        KeyToChinese = toCn;
        ChineseToKey = toKey;
        KeySet = new HashSet<string>(All, StringComparer.Ordinal);
    }

    /// <summary>是否为合法特殊制造状态 Key（Ordinal）</summary>
    public static bool IsKey(string? value)
        => !string.IsNullOrEmpty(value) && KeySet.Contains(value!);

    /// <summary>中文文本 → 英文 Key；已是 Key 原样返回；未知返回 null。</summary>
    public static string? ToKey(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (KeySet.Contains(value)) return value;
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
