namespace MES.Core.DTOs.Configuration;

using MES.Core.Constants;

/// <summary>
/// 组合归类 DTO — 以(工序组, 工段, 产类)为基准的唯一归属映射
/// </summary>
public class CombinationGroupDto
{
    public int Id { get; set; }

    /// <summary>工序组（英文 Key，"全部"=通配任意工序组）</summary>
    public string ProcessGroupName { get; set; } = null!;

    /// <summary>工段（英文 Key）</summary>
    public string SectionName { get; set; } = null!;

    /// <summary>产类（ProductStatuses.RoughTube/InProgress/Finished，或 AllStatus 哨兵=不限定）</summary>
    public string ProductStatus { get; set; } = ProductStatuses.AllStatus;

    /// <summary>归属流转类别 Id（SectionFlowCategorySetting.Id，空=未归属）</summary>
    public int? FlowCategoryId { get; set; }

    /// <summary>归属生产段落（中文段落名，SectionParagraphConfig.ParagraphName；空=未归属段落）</summary>
    public string? ParagraphName { get; set; }
}
