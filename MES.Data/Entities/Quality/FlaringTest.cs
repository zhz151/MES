namespace MES.Data.Entities.Quality;

/// <summary>
/// 扩口检验记录
/// </summary>
public class FlaringTest : BaseEntity
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

    /// <summary>顶心锥度</summary>
    public string? MandrelTaper { get; set; }

    /// <summary>扩后外径(mm)</summary>
    public decimal? FlaredDiameter { get; set; }

    /// <summary>扩口率(%)</summary>
    public decimal? FlaringRate { get; set; }

    /// <summary>观察</summary>
    public string? Observation { get; set; }

    /// <summary>判定</summary>
    public string? Judgment { get; set; }
}
