namespace MES.Data.Entities.Quality;

/// <summary>
/// 化学分析记录（光谱分析仪检测数据）
/// </summary>
public class ChemicalAnalysis : BaseEntity
{
    /// <summary>分析日期</summary>
    public DateTime AnalysisDate { get; set; }

    /// <summary>分析员</summary>
    public string Analyst { get; set; } = null!;

    /// <summary>炉号</summary>
    public string FurnaceNo { get; set; } = null!;

    /// <summary>牌号</summary>
    public string Grade { get; set; } = null!;

    /// <summary>分析次数</summary>
    public int? AnalysisCount { get; set; }

    /// <summary>分析标准</summary>
    public string? AnalysisStandard { get; set; }

    // ===== 化学元素含量（质量百分比） =====
    public decimal? C { get; set; }
    public decimal? Si { get; set; }
    public decimal? Mn { get; set; }
    public decimal? P { get; set; }
    public decimal? S { get; set; }
    public decimal? Ni { get; set; }
    public decimal? Cr { get; set; }
    public decimal? Mo { get; set; }
    public decimal? Cu { get; set; }
    public decimal? N { get; set; }
    public decimal? Nb { get; set; }
    public decimal? Ti { get; set; }
    public decimal? Fe { get; set; }
    public decimal? Al { get; set; }
    public decimal? W { get; set; }
}
