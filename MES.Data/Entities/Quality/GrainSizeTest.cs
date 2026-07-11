namespace MES.Data.Entities.Quality;

/// <summary>
/// 晶粒度检验记录
/// </summary>
public class GrainSizeTest : BaseEntity
{
    /// <summary>检验日期</summary>
    public DateTime InspectionDate { get; set; }

    /// <summary>检验员</summary>
    public string Inspector { get; set; } = null!;

    /// <summary>炉批号</summary>
    public string FurnaceNo { get; set; } = null!;

    /// <summary>牌号</summary>
    public string Grade { get; set; } = null!;

    /// <summary>规格</summary>
    public string Specification { get; set; } = null!;

    /// <summary>试样编号</summary>
    public int? SampleNo { get; set; }

    /// <summary>试样尺寸</summary>
    public string? SampleSize { get; set; }

    /// <summary>检验标准</summary>
    public string? InspectionStandard { get; set; }

    /// <summary>晶粒度级别</summary>
    public string? GrainSizeGrade { get; set; }

    /// <summary>晶粒度测定方法</summary>
    public string? GrainSizeMethod { get; set; }

    /// <summary>观察倍数</summary>
    public string? Magnification { get; set; }

    /// <summary>判定</summary>
    public string? Judgment { get; set; }
}
