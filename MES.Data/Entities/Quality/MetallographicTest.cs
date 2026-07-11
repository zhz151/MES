namespace MES.Data.Entities.Quality;

/// <summary>
/// 金相检验记录
/// </summary>
public class MetallographicTest : BaseEntity
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

    /// <summary>浸蚀方式</summary>
    public string? EtchingMethod { get; set; }

    /// <summary>电解电压</summary>
    public string? ElectrolyticVoltage { get; set; }

    /// <summary>电解时间</summary>
    public string? ElectrolyticTime { get; set; }

    /// <summary>检测观察倍数</summary>
    public string? Magnification { get; set; }

    /// <summary>对照测定铁素体含量(%)</summary>
    public decimal? FerriteContent { get; set; }

    /// <summary>判定</summary>
    public string? Judgment { get; set; }
}
