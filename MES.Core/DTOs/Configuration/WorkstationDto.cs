using MES.Core.Enums;

namespace MES.Core.DTOs.Configuration;

/// <summary>
/// 工位信息
/// </summary>
public class WorkstationDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string? Name { get; set; }
    public string? EquipmentName { get; set; }
    public string SectionName { get; set; } = null!;
    public ReportTemplateType ReportType { get; set; }
    public bool IsActive { get; set; } = true;
}
