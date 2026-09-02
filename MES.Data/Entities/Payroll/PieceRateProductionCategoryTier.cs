using System.ComponentModel.DataAnnotations;

namespace MES.Data.Entities.Payroll;

/// <summary>
/// 生产计件类别维度档（子表，2026-09-02 模型重构引入）。
/// 一行一条维度档：区间维（外径/壁厚/长度/断切率 = MinValue/MaxValue，定尺 = MinInt/MaxInt）或等值维（牌号/状态/特殊设备号 = MatchValue）。
/// 档行只存系数 Ratio（命中即乘），不冗余基准价。类别下未配档的维度 = 系数 1，不参与结算。
/// 无例外价/绝对价行——任何价格必须能表达为 类别.BasePrice × 档 Ratio 连乘。
/// </summary>
public class PieceRateProductionCategoryTier : BaseEntity
{
    /// <summary>所属类别（级联删除）</summary>
    public int CategoryId { get; set; }

    /// <summary>所属类别导航</summary>
    public PieceRateProductionCategory? Category { get; set; }

    /// <summary>维度英文 Key（见 PieceRateDimensionKeys：OuterDiameter/WallThickness/Length/CutRate/FixedLengthCount/SpecialGrade/SpecialState/SpecialDevice）</summary>
    [Required]
    [MaxLength(30)]
    public string DimensionKey { get; set; } = string.Empty;

    /// <summary>区间原文（如 "D&gt;54"、"54≥D&gt;41"）或取值文本（等值维）</summary>
    [MaxLength(100)]
    public string? RangeText { get; set; }

    /// <summary>区间维下界（含；外径/壁厚/长度/断切率用）</summary>
    public decimal? MinValue { get; set; }

    /// <summary>区间维上界（含；外径/壁厚/长度/断切率用）</summary>
    public decimal? MaxValue { get; set; }

    /// <summary>定尺维下界（含，整数）</summary>
    public int? MinInt { get; set; }

    /// <summary>定尺维上界（含，整数）</summary>
    public int? MaxInt { get; set; }

    /// <summary>等值维取值（特殊牌号 PlantGrade / 特殊制造状态 Key / 特殊设备号文本）</summary>
    [MaxLength(100)]
    public string? MatchValue { get; set; }

    /// <summary>加价系数（命中即乘；默认 1；精度 decimal(18,6) 承接 0.8697 类多位数）</summary>
    public decimal Ratio { get; set; } = 1;

    /// <summary>当前启用（true=参与结算；false=忽略）</summary>
    public bool IsActive { get; set; } = true;
}
