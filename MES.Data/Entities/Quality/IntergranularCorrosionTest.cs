namespace MES.Data.Entities.Quality;

/// <summary>
/// 晶间腐蚀检验记录
/// </summary>
public class IntergranularCorrosionTest : BaseEntity
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

    /// <summary>试样尺寸</summary>
    public string? SampleSize { get; set; }

    /// <summary>检验标准</summary>
    public string? InspectionStandard { get; set; }

    /// <summary>试样敏化温度</summary>
    public string? SensitizationTemperature { get; set; }

    /// <summary>敏化持续时间</summary>
    public string? SensitizationDuration { get; set; }

    /// <summary>浸蚀溶液</summary>
    public string? CorrosionSolution { get; set; }

    /// <summary>浸蚀时间</summary>
    public string? CorrosionTime { get; set; }

    /// <summary>试样弯曲度数</summary>
    public string? BendDegree { get; set; }

    /// <summary>观察放大倍数</summary>
    public string? Magnification { get; set; }

    /// <summary>观察结果</summary>
    public string? ObservationResult { get; set; }

    /// <summary>判定</summary>
    public string? Judgment { get; set; }
}
