namespace MES.Core.DTOs;

/// <summary>
/// 段落分类明细 DTO — 用于参数表管理页面
/// </summary>
public class SectionFlowCategoryItemDto
{
    public int Id { get; set; }
    public int SettingId { get; set; }
    public string ProcessGroupName { get; set; } = null!;
    public string SectionName { get; set; } = null!;
    public decimal Coefficient { get; set; }
    public int DisplayOrder { get; set; }
}
