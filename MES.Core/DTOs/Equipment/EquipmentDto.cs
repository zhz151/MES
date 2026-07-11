using MES.Core.DTOs.Shared;
namespace MES.Core.DTOs.Equipment;

/// <summary>
/// 设备列表 DTO（全字段）
/// </summary>
public class EquipmentListDto
{
    public int Id { get; set; }
    public string EquipmentCode { get; set; } = null!;
    public string EquipmentName { get; set; } = null!;
    public string? ModelNumber { get; set; }
    public string? TechnicalParams { get; set; }
    public string? Manufacturer { get; set; }
    public DateTime? InstallationDate { get; set; }
    public string? Remark { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? RelatedSection { get; set; }

    // 点检
    public bool NeedInspection { get; set; }
    public string? InspectionPerson { get; set; }
    public int InspectionCycleDays { get; set; }
    public DateTime? LastInspectionDate { get; set; }
    public DateTime? CurrentInspectionStartDate { get; set; }
    public string InspectionStatus { get; set; } = null!;  // 物化存储

    // 保养
    public bool NeedMaintenance { get; set; }
    public string? MaintPerson { get; set; }
    public int MaintCycleDays { get; set; }
    public DateTime? LastMaintDate { get; set; }
    public DateTime? CurrentMaintStartDate { get; set; }
    public string MaintStatus { get; set; } = null!;  // 物化存储

    // 维修
    public DateTime? LastRepairDate { get; set; }

    // 状态
    public string LifecycleStatus { get; set; } = null!;
    public string UsageType { get; set; } = null!;
    public string RunningStatus { get; set; } = null!;  // 物化存储

    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 设备详情 DTO（保留向后兼容，内容同 ListDto）
/// </summary>
public class EquipmentDetailDto : EquipmentListDto
{
}

/// <summary>
/// 创建设备请求
/// </summary>
public class CreateEquipmentRequest
{
    public string EquipmentCode { get; set; } = null!;
    public string EquipmentName { get; set; } = null!;
    public string? ModelNumber { get; set; }
    public string? TechnicalParams { get; set; }
    public string? Manufacturer { get; set; }
    public DateTime? InstallationDate { get; set; }
    public string? Remark { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? RelatedSection { get; set; }

    // 点检
    public bool NeedInspection { get; set; }
    public string? InspectionPerson { get; set; }
    public int InspectionCycleDays { get; set; } = 7;
    public DateTime? CurrentInspectionStartDate { get; set; }

    // 保养
    public bool NeedMaintenance { get; set; }
    public string? MaintPerson { get; set; }
    public int MaintCycleDays { get; set; } = 30;
    public DateTime? CurrentMaintStartDate { get; set; }

    // 状态
    public string LifecycleStatus { get; set; } = "Active";
    public string UsageType { get; set; } = "Primary";
}

/// <summary>
/// 更新设备请求（不含状态字段）
/// </summary>
public class UpdateEquipmentRequest
{
    public string EquipmentCode { get; set; } = null!;
    public string EquipmentName { get; set; } = null!;
    public string? ModelNumber { get; set; }
    public string? TechnicalParams { get; set; }
    public string? Manufacturer { get; set; }
    public DateTime? InstallationDate { get; set; }
    public string? Remark { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? RelatedSection { get; set; }

    // 点检
    public bool NeedInspection { get; set; }
    public string? InspectionPerson { get; set; }
    public int InspectionCycleDays { get; set; } = 7;
    public DateTime? CurrentInspectionStartDate { get; set; }

    // 保养
    public bool NeedMaintenance { get; set; }
    public string? MaintPerson { get; set; }
    public int MaintCycleDays { get; set; } = 30;
    public DateTime? CurrentMaintStartDate { get; set; }

    // 状态（不含点检/保养/运行状态）
    public string LifecycleStatus { get; set; } = null!;
    public string UsageType { get; set; } = null!;
}

/// <summary>
/// 设备台账打印请求（批量）
/// </summary>
public class EquipmentPrintBatchRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 设备台账打印请求（全部）
/// </summary>
public class EquipmentPrintAllRequest
{
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; }
    public string? LifecycleStatus { get; set; }
    public string? UsageType { get; set; }
    public string? RunningStatus { get; set; }
    public string? InspectionStatus { get; set; }
    public string? MaintStatus { get; set; }
    public string? Location { get; set; }
    public string? RelatedSection { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
}
