namespace MES.Core.Constants;

/// <summary>
/// 计件单价体系的单位英文 Key 常量（结算单价单位）。
/// 本阶段仅存单位标识，数量换算（元/吨×重量、元/千米×千米数、元/支×支数、元/头×头数）属核算阶段。
/// </summary>
public static class PieceRateUnitKeys
{
    /// <summary>元/吨（按重量，千克/1000）</summary>
    public const string PerTon = "PerTon";

    /// <summary>元/千米（按长度，米/1000）</summary>
    public const string PerKm = "PerKm";

    /// <summary>元/支（按支数）</summary>
    public const string PerPiece = "PerPiece";

    /// <summary>元/头（按头数）</summary>
    public const string PerHead = "PerHead";

    /// <summary>所有单位 Key 的有序列表</summary>
    public static readonly string[] All =
    [
        PerTon, PerKm, PerPiece, PerHead
    ];

    /// <summary>Key → 规范中文（显示）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese = new Dictionary<string, string>
    {
        [PerTon] = "元/吨",
        [PerKm] = "元/千米",
        [PerPiece] = "元/支",
        [PerHead] = "元/头",
    };

    /// <summary>是否为合法单位 Key（Ordinal）</summary>
    public static bool IsKey(string? value)
        => !string.IsNullOrEmpty(value) && All.Contains(value!, StringComparer.Ordinal);

    /// <summary>归一为显示中文：Key → 规范中文；已是中文原样返回；未知返回 null。</summary>
    public static string? ToChinese(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (KeyToChinese.TryGetValue(value, out var cn)) return cn;
        return value;
    }

    // ========== 数量取数维度（2026-09-02，核算阶段取数源：结算工资 = 单价 × 数量） ==========

    /// <summary>单位对应的数量取数维度</summary>
    public enum QuantityDimension
    {
        /// <summary>按重量（元/吨 × 重量，千克/1000）</summary>
        Weight,

        /// <summary>按长度（元/千米 × 千米数，米/1000）</summary>
        Meters,

        /// <summary>按支数（元/支 × 支数）</summary>
        PieceCount,

        /// <summary>按头数（元/头 × 头数）</summary>
        HeadCount
    }

    /// <summary>单位 → 数量取数维度映射（未知单位兜底 Weight）</summary>
    public static QuantityDimension GetQuantityDimension(string? unit)
        => unit switch
        {
            PerTon => QuantityDimension.Weight,
            PerKm => QuantityDimension.Meters,
            PerPiece => QuantityDimension.PieceCount,
            PerHead => QuantityDimension.HeadCount,
            _ => QuantityDimension.Weight
        };
}
