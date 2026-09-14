using System.Collections.Generic;

namespace MES.Core.Constants;

/// <summary>
/// NCR 不合格品处置方式英文稳定 Key 常量及双向映射。存储层与后端匹配一律使用英文 Key
/// （如 "InProcessWarehouse"），显示层使用中文（让步放行/返工/返整(新卡流转)/入在制库(可改制)/
/// 可入备库(尺寸偏差)/入次品库(修正改制)/入次品库(报废)/退货，可经配置表 DictValueDefinitions
/// （DictKey=NcrDisposalKey）改名）。
/// 属可扩展配置字典：用户在配置表可新增处置方式（Key 固定、Name 可改）。
/// 内置 5 档沿用原 DisposalMethod 枚举名，存量 NCR 数据零迁移。
/// 与<see cref="FlowDirection"/>（流向，枚举 5 档，由检验记录带出）是两个不同概念：
/// 流向 = 物料实际去向；处置方式 = 质量负责人判定的处置措施，含「让步放行」「返工」（操作前预先判定）
/// 与更细的「入次品库(修正改制/报废)」，比流向多 3 档。
/// </summary>
public static class NcrDisposalKeys
{
    // ========== 内置处置方式英文 Key 常量 ==========

    /// <summary>让步放行（生产及检验工段在正式操作前预先判定，不产生流向）</summary>
    public const string Concession = "Concession";

    /// <summary>返工（生产及检验工段在正式操作前预先判定，不产生流向）</summary>
    public const string Reprocess = "Reprocess";

    /// <summary>返整(新卡流转)</summary>
    public const string Rework = "Rework";

    /// <summary>入在制库(可改制)</summary>
    public const string InProcessWarehouse = "InProcessWarehouse";

    /// <summary>可入备库(尺寸偏差)</summary>
    public const string FinishedWarehouse = "FinishedWarehouse";

    /// <summary>入次品库(修正改制)</summary>
    public const string ScrapCorrection = "ScrapCorrection";

    /// <summary>入次品库(报废)</summary>
    public const string Scrap = "Scrap";

    /// <summary>退货</summary>
    public const string Return = "Return";

    /// <summary>所有内置处置方式 Key 的有序列表（顺序即配置表默认 DisplayOrder）</summary>
    public static readonly string[] All =
    [
        Concession, Reprocess, Rework, InProcessWarehouse,
        FinishedWarehouse, ScrapCorrection, Scrap, Return
    ];

    /// <summary>Key → 规范中文（显示兜底）</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyToChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Concession] = "让步放行",
            [Reprocess] = "返工",
            [Rework] = "返整(新卡流转)",
            [InProcessWarehouse] = "入在制库(可改制)",
            [FinishedWarehouse] = "可入备库(尺寸偏差)",
            [ScrapCorrection] = "入次品库(修正改制)",
            [Scrap] = "入次品库(报废)",
            [Return] = "退货",
        };

    /// <summary>规范中文 → Key（迁移前存量归一/导入解析用）</summary>
    public static readonly IReadOnlyDictionary<string, string> ChineseToKey =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["让步放行"] = Concession,
            ["返工"] = Reprocess,
            ["返整(新卡流转)"] = Rework,
            ["入在制库(可改制)"] = InProcessWarehouse,
            ["可入备库(尺寸偏差)"] = FinishedWarehouse,
            ["入次品库(修正改制)"] = ScrapCorrection,
            ["入次品库(报废)"] = Scrap,
            ["退货"] = Return,
        };

    private static readonly HashSet<string> KeySet = new(All, StringComparer.Ordinal);

    /// <summary>是否为合法处置方式 Key（Ordinal）</summary>
    public static bool IsKey(string? value)
        => !string.IsNullOrEmpty(value) && KeySet.Contains(value!);

    /// <summary>
    /// 归一为稳定 Key：已是 Key 原样返回；中文反查；未知返回 null。
    /// </summary>
    public static string? ToKey(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (KeySet.Contains(value)) return value;
        return ChineseToKey.TryGetValue(value, out var key) ? key : null;
    }

    /// <summary>
    /// 归一为显示中文：Key → 中文；已是中文（存量）原样返回；未知返回 null。
    /// </summary>
    public static string? ToChinese(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (KeyToChinese.TryGetValue(value, out var cn)) return cn;
        // 已是中文（存量）或未知值：原样返回
        return value;
    }
}
