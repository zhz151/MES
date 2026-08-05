namespace MES.Core.Constants;

/// <summary>
/// 工段英文稳定 Key 常量及双向映射。存储层与后端匹配一律使用英文 Key（如 "Cut"），
/// 显示层使用中文（SectionDefs 中文常量或配置表 SectionName）。
/// 与 <see cref="SectionDefs"/> 一一对应：SectionDefs 值是中文（显示契约），
/// SectionKeys 值是与常量名一致的英文 Key（程序识别契约），两者通过本类双向转换。
/// </summary>
public static class SectionKeys
{
    // ========== 26 个工段英文 Key 常量（与 SectionDefs 常量名一致） ==========
    public const string ColdRollDraw = "ColdRollDraw";
    public const string OilPipeCut = "OilPipeCut";
    public const string Degrease = "Degrease";
    public const string EmulsionWash = "EmulsionWash";
    public const string UltrasonicWash = "UltrasonicWash";
    public const string ClothPolish = "ClothPolish";
    public const string BrightAnnealing = "BrightAnnealing";
    public const string Solution = "Solution";
    public const string Straighten = "Straighten";
    public const string Cut = "Cut";
    public const string ThicknessMeasure = "ThicknessMeasure";
    public const string Pickle = "Pickle";
    public const string OuterPolish = "OuterPolish";
    public const string InnerPolish = "InnerPolish";
    public const string InnerGrinding = "InnerGrinding";
    public const string OuterSpotGrinding = "OuterSpotGrinding";
    public const string SandBlasting = "SandBlasting";
    public const string ShotBlasting = "ShotBlasting";
    public const string Inspection = "Inspection";
    public const string WeldingHead = "WeldingHead";
    public const string Welding = "Welding";
    public const string Lubrication = "Lubrication";
    public const string Packing = "Packing";
    public const string Warehouse = "Warehouse";
    public const string Extra1 = "Extra1";
    public const string Extra2 = "Extra2";

    /// <summary>所有工段 Key 的有序列表（顺序同 SectionDefs.All）</summary>
    public static readonly string[] All =
    [
        ColdRollDraw, OilPipeCut, Degrease, EmulsionWash, UltrasonicWash, ClothPolish, BrightAnnealing,
        Solution, Straighten, Cut, ThicknessMeasure, Pickle, OuterPolish, InnerPolish, InnerGrinding,
        OuterSpotGrinding, SandBlasting, ShotBlasting, Inspection, WeldingHead, Welding, Lubrication,
        Packing, Warehouse, Extra1, Extra2
    ];

    /// <summary>Key → 规范中文（SectionDefs.PropertyToName，显示兜底）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese =
        SectionDefs.PropertyToName;

    /// <summary>规范中文（含别名）→ Key。程序识别输入归一用。</summary>
    public static readonly IReadOnlyDictionary<string, string> ChineseToKey = BuildChineseToKey();

    /// <summary>所有 Key 的集合（Ordinal 比较，供 IsKey 快速判定）</summary>
    private static readonly HashSet<string> KeySet = new(All, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> BuildChineseToKey()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        // 规范中文 → Key（反查 PropertyToName）
        foreach (var kvp in SectionDefs.PropertyToName)
        {
            map[kvp.Value] = kvp.Key;
        }
        // 别名 → 规范 Key（Aliases：别名中文 → 规范中文）
        foreach (var kvp in SectionDefs.Aliases)
        {
            if (map.TryGetValue(kvp.Value, out var key))
                map[kvp.Key] = key;
        }
        return map;
    }

    /// <summary>是否为合法英文 Key（Ordinal）</summary>
    public static bool IsKey(string? value)
        => !string.IsNullOrEmpty(value) && KeySet.Contains(value!);

    /// <summary>
    /// 归一为稳定 Key：已是 Key 原样返回；规范中文/别名反查；未知返回 null。
    /// </summary>
    public static string? ToKey(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (KeySet.Contains(value)) return value;
        return ChineseToKey.TryGetValue(value, out var key) ? key : null;
    }

    /// <summary>
    /// 归一为显示中文：Key → 规范中文；已是中文（规范/别名）原样返回（兼容迁移前存量）；未知返回 null。
    /// </summary>
    public static string? ToChinese(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (SectionDefs.PropertyToName.TryGetValue(value, out var cn)) return cn;
        // 已是中文（含别名）或未知值：原样返回
        return value;
    }
}
