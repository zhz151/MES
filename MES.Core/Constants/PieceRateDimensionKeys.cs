namespace MES.Core.Constants;

/// <summary>
/// 生产计件类别维度档的英文 Key 常量（子表 PieceRateProductionCategoryTier 的 DimensionKey，2026-09-02 重构引入）。
/// 替代旧 PieceRateDimensionTemplate（按类别固化启用维）与旧 PieceRateFactorKeys 的维度档/特殊行语义：
/// 新模型不存「启用维度集合」，类别下实际配了档行的维才参与结算（未配=系数 1）。
/// 5 个区间维（外径/壁厚/长度/断切率 = MinValue/MaxValue，定尺 = MinInt/MaxInt）+ 4 个等值维
/// （牌号/状态/设备 = MatchValue 相等；冷拔类型 = MatchValue 为备注关键词，按 Remark.Contains 命中）。
/// </summary>
public static class PieceRateDimensionKeys
{
    // ========== 区间维 ==========
    /// <summary>外径档</summary>
    public const string OuterDiameter = "OuterDiameter";

    /// <summary>壁厚档</summary>
    public const string WallThickness = "WallThickness";

    /// <summary>长度档</summary>
    public const string Length = "Length";

    /// <summary>断切率档（油管地切专用）</summary>
    public const string CutRate = "CutRate";

    /// <summary>定尺种类档（切挫专用，整数区间）</summary>
    public const string FixedLengthCount = "FixedLengthCount";

    // ========== 等值维（补充档，未设置默认 1） ==========
    /// <summary>特殊牌号（工厂牌号 PlantGrade，源自牌号对照表）</summary>
    public const string SpecialGrade = "SpecialGrade";

    /// <summary>特殊制造状态（见 PieceRateStateKeys）</summary>
    public const string SpecialState = "SpecialState";

    /// <summary>特殊设备号（报工 EquipmentName 文本等值；仅列出设备乘系数，未列出=1，语义同特殊牌号）</summary>
    public const string SpecialDevice = "SpecialDevice";

    /// <summary>冷拔类型（备注关键词自由文本值维：MatchValue 存关键词，报工备注 Remark.Contains(关键词) 即乘系数；未命中=1。仅冷拔类别配档）</summary>
    public const string ColdDrawType = "ColdDrawType";

    /// <summary>所有维度 Key 的有序列表（编辑器分区顺序）</summary>
    public static readonly string[] All =
    [
        OuterDiameter, WallThickness, Length, CutRate, FixedLengthCount,
        SpecialGrade, SpecialState, SpecialDevice, ColdDrawType
    ];

    /// <summary>Key → 规范中文（显示）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese = new Dictionary<string, string>
    {
        [OuterDiameter] = "外径",
        [WallThickness] = "壁厚",
        [Length] = "长度",
        [CutRate] = "断切率",
        [FixedLengthCount] = "定尺",
        [SpecialGrade] = "特殊牌号",
        [SpecialState] = "特殊制造状态",
        [SpecialDevice] = "特殊设备号",
        [ColdDrawType] = "冷拔类型",
    };

    private static readonly HashSet<string> IntervalSet = new(
        [OuterDiameter, WallThickness, Length, CutRate, FixedLengthCount], StringComparer.Ordinal);

    /// <summary>是否为合法维度 Key（Ordinal）</summary>
    public static bool IsKey(string? value)
        => !string.IsNullOrEmpty(value) && All.Contains(value!, StringComparer.Ordinal);

    /// <summary>归一为显示中文：Key → 中文；未知返回 null。</summary>
    public static string? ToChinese(string? value)
        => !string.IsNullOrEmpty(value) && KeyToChinese.TryGetValue(value, out var cn) ? cn : null;

    /// <summary>是否为区间维（外径/壁厚/长度/断切率/定尺；false=等值补充档）</summary>
    public static bool IsInterval(string? dimKey)
        => !string.IsNullOrEmpty(dimKey) && IntervalSet.Contains(dimKey);

    /// <summary>是否为等值补充维（特殊牌号/特殊制造状态/特殊设备号/冷拔类型；true 时行存 MatchValue 非区间）</summary>
    public static bool IsValueDimension(string? dimKey)
        => !string.IsNullOrEmpty(dimKey) && !IntervalSet.Contains(dimKey);
}
