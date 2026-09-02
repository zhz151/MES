using System.ComponentModel.DataAnnotations;

namespace MES.Data.Entities.Payroll;

/// <summary>
/// 成检计件类别维度档（子表，2026-09-03 引入）。
/// 一行一条维度档：区间维（外径/壁厚/长度 = MinValue/MaxValue，检验支数 = MinInt/MaxInt）或等值维（长度状态/牌号/状态/特殊设备号 = MatchValue）。
/// 档行只存系数 Ratio（命中即乘），不冗余基准价。类别下未配档的维度 = 系数 1，不参与结算。
/// 维度英文 Key 域见 PieceRateInspectionDimensionKeys。
/// </summary>
public class PieceRateFinalInspectionCategoryTier : BaseEntity
{
    /// <summary>所属类别（级联删除）</summary>
    public int CategoryId { get; set; }

    /// <summary>所属类别导航</summary>
    public PieceRateFinalInspectionCategory? Category { get; set; }

    /// <summary>维度英文 Key（见 PieceRateInspectionDimensionKeys：OuterDiameter/WallThickness/Length/InspectionCount/LengthStatus/SpecialGrade/SpecialState/SpecialDevice）</summary>
    [Required]
    [MaxLength(30)]
    public string DimensionKey { get; set; } = string.Empty;

    /// <summary>区间原文（如 "D&gt;219"、"60≤D&gt;50"）或取值文本（等值维）</summary>
    [MaxLength(100)]
    public string? RangeText { get; set; }

    /// <summary>区间维下界（含；外径/壁厚/长度用）</summary>
    public decimal? MinValue { get; set; }

    /// <summary>区间维上界（含；外径/壁厚/长度用）</summary>
    public decimal? MaxValue { get; set; }

    /// <summary>检验支数维下界（含，整数）</summary>
    public int? MinInt { get; set; }

    /// <summary>检验支数维上界（含，整数）</summary>
    public int? MaxInt { get; set; }

    /// <summary>等值维取值（长度状态 Fixed/Range/NonFixed / 特殊牌号 PlantGrade / 特殊制造状态 Key / 特殊设备号文本）</summary>
    [MaxLength(100)]
    public string? MatchValue { get; set; }

    /// <summary>加价系数（命中即乘；默认 1；精度 decimal(18,6)）</summary>
    public decimal Ratio { get; set; } = 1;

    /// <summary>当前启用（true=参与结算；false=忽略）</summary>
    public bool IsActive { get; set; } = true;
}
