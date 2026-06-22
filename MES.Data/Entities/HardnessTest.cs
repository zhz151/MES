namespace MES.Data.Entities;

/// <summary>
/// 硬度检验记录
/// </summary>
public class HardnessTest : BaseEntity
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

    /// <summary>硬度模式（如洛氏硬度）</summary>
    public string? HardnessMode { get; set; }

    /// <summary>硬度测定值</summary>
    public string? HardnessValue { get; set; }

    /// <summary>判定</summary>
    public string? Judgment { get; set; }
}
