namespace MES.Core.Constants;

/// <summary>
/// 成检计件类别维度档的英文 Key 常量（子表 PieceRateFinalInspectionCategoryTier 的 DimensionKey，2026-09-03 引入）。
/// 与生产计件维度集（PieceRateDimensionKeys）的差异（业务对齐）：
/// ① 断切率 CutRate → <see cref="LengthStatus"/>（等值三档 定尺/范围尺/非定尺，按批次长度状态设比）；
/// ② 定尺种类 FixedLengthCount → 检验支数（整数区间，批支数多则效率高系数低，如 1-10→2 … &gt;1000→0.8）。
/// 其余 6 维（外径/壁厚/长度/特殊牌号/特殊制造状态/特殊设备号）语义与生产一致，Key 字符串同值（方便未来引擎共用）。
/// 区间 4（外径/壁厚/长度 = MinValue/MaxValue，检验支数 = MinInt/MaxInt）+ 等值 4（长度状态/牌号/状态/设备 = MatchValue）。
/// ⚠️ 长度区间档量纲 = mm（档值域约 1500~16000，最大 16m 管）。全长度状态参与命中：Fixed = 实际定尺长，
/// Range/NonFixed 批取数缺省按 6000mm（=6m）折算（业务规约 2026-09-03 拍板「两者都要」：既进 Length 档也作计费折算基数）。
/// 当前真库档位下 6000 兜底落在 5001-7500 主档（系数 1.0），调整长度档位须先评估对范围尺/非定尺单价的影响。
/// </summary>
public static class PieceRateInspectionDimensionKeys
{
    // ========== 区间维 ==========
    /// <summary>外径档（复用生产同值）</summary>
    public const string OuterDiameter = PieceRateDimensionKeys.OuterDiameter;

    /// <summary>壁厚档（复用生产同值）</summary>
    public const string WallThickness = PieceRateDimensionKeys.WallThickness;

    /// <summary>长度档（复用生产同值；量纲 mm，Fixed=实际定尺长，Range/NonFixed 取数缺省按 6000 折算参与）</summary>
    public const string Length = PieceRateDimensionKeys.Length;

    /// <summary>检验支数档（整数区间 MinInt/MaxInt，按检验批支数分段；段须连续闭带且末档开口，防大单落空跳基准）</summary>
    public const string InspectionCount = "InspectionCount";

    // ========== 等值维（补充档，未设置默认 1） ==========
    /// <summary>长度状态（MES.Core.Enums.LengthStatus：Fixed=定尺/Range=范围尺/NonFixed=非定尺）</summary>
    public const string LengthStatus = "LengthStatus";

    /// <summary>特殊牌号（工厂牌号 PlantGrade，源自批次/牌号对照；复用生产同值）</summary>
    public const string SpecialGrade = PieceRateDimensionKeys.SpecialGrade;

    /// <summary>特殊制造状态（见 PieceRateStateKeys；复用生产同值）</summary>
    public const string SpecialState = PieceRateDimensionKeys.SpecialState;

    /// <summary>特殊设备号（报工 EquipmentName 文本等值；复用生产同值）</summary>
    public const string SpecialDevice = PieceRateDimensionKeys.SpecialDevice;

    /// <summary>所有维度 Key 的有序列表（编辑器分区顺序：区间维在前，等值维在后）</summary>
    public static readonly string[] All =
    [
        OuterDiameter, WallThickness, Length, InspectionCount,
        LengthStatus, SpecialGrade, SpecialState, SpecialDevice
    ];

    /// <summary>Key → 规范中文（显示）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese = new Dictionary<string, string>
    {
        [OuterDiameter] = "外径",
        [WallThickness] = "壁厚",
        [Length] = "长度",
        [InspectionCount] = "检验支数",
        [LengthStatus] = "长度状态",
        [SpecialGrade] = "特殊牌号",
        [SpecialState] = "特殊制造状态",
        [SpecialDevice] = "特殊设备号",
    };

    private static readonly HashSet<string> IntervalSet = new(
        [OuterDiameter, WallThickness, Length, InspectionCount], StringComparer.Ordinal);

    /// <summary>是否为合法维度 Key（Ordinal）</summary>
    public static bool IsKey(string? value)
        => !string.IsNullOrEmpty(value) && All.Contains(value!, StringComparer.Ordinal);

    /// <summary>归一为显示中文：Key → 中文；未知返回 null。</summary>
    public static string? ToChinese(string? value)
        => !string.IsNullOrEmpty(value) && KeyToChinese.TryGetValue(value, out var cn) ? cn : null;

    /// <summary>是否为区间维（外径/壁厚/长度/检验支数；false=等值补充档）</summary>
    public static bool IsInterval(string? dimKey)
        => !string.IsNullOrEmpty(dimKey) && IntervalSet.Contains(dimKey);

    /// <summary>是否为等值维（长度状态/特殊牌号/特殊制造状态/特殊设备号；true 时行存 MatchValue 非区间）</summary>
    public static bool IsValueDimension(string? dimKey)
        => !string.IsNullOrEmpty(dimKey) && !IntervalSet.Contains(dimKey);
}
