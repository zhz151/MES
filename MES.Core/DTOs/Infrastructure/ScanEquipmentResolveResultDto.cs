namespace MES.Core.DTOs.Infrastructure;

/// <summary>
/// 设备扫码解析结果
/// </summary>
public class ScanEquipmentResolveResultDto
{
    /// <summary>设备 ID</summary>
    public int EquipmentId { get; set; }

    /// <summary>设备编号</summary>
    public string EquipmentCode { get; set; } = null!;

    /// <summary>设备名称</summary>
    public string EquipmentName { get; set; } = null!;

    /// <summary>所在区域</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>关联工段</summary>
    public string? RelatedSection { get; set; }

    /// <summary>型号规格</summary>
    public string? ModelNumber { get; set; }
}
