namespace MES.Data.Entities;

/// <summary>
/// 室温拉伸检验记录
/// </summary>
public class TensileTest : BaseEntity
{
    /// <summary>检验日期</summary>
    public DateTime InspectionDate { get; set; }

    /// <summary>检验员</summary>
    public string Inspector { get; set; } = null!;

    /// <summary>生产编号</summary>
    public string FurnaceNo { get; set; } = null!;

    /// <summary>牌号</summary>
    public string Grade { get; set; } = null!;

    /// <summary>规格</summary>
    public string Specification { get; set; } = null!;

    /// <summary>试样编号</summary>
    public int? SampleNo { get; set; }

    /// <summary>试样尺寸(mm)</summary>
    public string? SampleSize { get; set; }

    /// <summary>检验标准</summary>
    public string? InspectionStandard { get; set; }

    /// <summary>原始标距(mm)</summary>
    public decimal? OriginalGaugeLength { get; set; }

    /// <summary>断后标距(mm)</summary>
    public decimal? FinalGaugeLength { get; set; }

    /// <summary>抗拉强度(MPa)</summary>
    public decimal? TensileStrength { get; set; }

    /// <summary>屈服强度Rp0.2</summary>
    public decimal? YieldStrengthRp02 { get; set; }

    /// <summary>屈服强度Rp1</summary>
    public decimal? YieldStrengthRp1 { get; set; }

    /// <summary>延伸率(%)</summary>
    public decimal? Elongation { get; set; }

    /// <summary>判定</summary>
    public string? Judgment { get; set; }
}
