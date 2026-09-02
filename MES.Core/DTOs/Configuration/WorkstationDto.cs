using MES.Core.Enums;
using MES.Core.Helpers;

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
    /// <summary>工段英文 Key（成检到料/成品检验工位选填可空）</summary>
    public string? SectionName { get; set; }
    public ReportTemplateType ReportType { get; set; }

    /// <summary>成品检验项目（仅 ReportType=FinalInspection 时非空）</summary>
    public InspectionItem? InspectionItem { get; set; }

    /// <summary>检验项目中文显示</summary>
    public string? InspectionItemDisplay => InspectionItem.HasValue ? EnumHelper.GetDisplayName(InspectionItem.Value) : null;
    public bool IsActive { get; set; } = true;
}
