namespace MES.Data.Entities;

/// <summary>
/// 标准号子项目 — 检验项目要求
/// </summary>
public class StandardRegisterItem : BaseEntity
{
    /// <summary>所属标准号 ID</summary>
    public int StandardRegisterId { get; set; }

    /// <summary>序号</summary>
    public int SeqNo { get; set; }

    /// <summary>检验项目类别</summary>
    public string? InspectionCategory { get; set; }

    /// <summary>检验项目</summary>
    public string InspectionItem { get; set; } = string.Empty;

    /// <summary>强制性（关键/主要/一般）</summary>
    public string? IsMandatory { get; set; }

    /// <summary>取样要求</summary>
    public string? SamplingRequirement { get; set; }

    /// <summary>适用范围</summary>
    public string? ApplicableRange { get; set; }

    /// <summary>引用标准</summary>
    public string? RefStandard { get; set; }

    /// <summary>详细要求</summary>
    public string? DetailRequirement { get; set; }

    /// <summary>所属标准号</summary>
    public StandardRegister StandardRegister { get; set; } = null!;
}
