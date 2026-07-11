namespace MES.Data.Entities.Quality;

/// <summary>
/// 压扁检验记录
/// </summary>
public class FlatteningTest : BaseEntity
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

    /// <summary>压后平板间距(mm)</summary>
    public decimal? FlatteningGap { get; set; }

    /// <summary>观察</summary>
    public string? Observation { get; set; }

    /// <summary>判定</summary>
    public string? Judgment { get; set; }
}
