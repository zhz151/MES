namespace MES.Data.Entities;

/// <summary>
/// 点腐蚀检验记录
/// </summary>
public class PittingCorrosionTest : BaseEntity
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

    /// <summary>试样研磨粒度</summary>
    public string? PolishingGrade { get; set; }

    /// <summary>试样原始重量mg</summary>
    public decimal? RawWeight { get; set; }

    /// <summary>浸蚀溶液</summary>
    public string? CorrosionSolution { get; set; }

    /// <summary>浸蚀温度</summary>
    public string? CorrosionTemperature { get; set; }

    /// <summary>浸蚀时间</summary>
    public string? CorrosionTime { get; set; }

    /// <summary>浸蚀后试样重量mg</summary>
    public decimal? FinalWeight { get; set; }

    /// <summary>腐蚀率g/(m2.h)</summary>
    public decimal? CorrosionRate { get; set; }

    /// <summary>腐蚀最大孔深mm</summary>
    public decimal? MaxPitDepth { get; set; }

    /// <summary>判定</summary>
    public string? Judgment { get; set; }
}
