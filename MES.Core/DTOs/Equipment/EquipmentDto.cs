using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using MES.Core.Helpers;
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
    public EquipmentTaskStatus InspectionStatus { get; set; }  // 物化存储
    public string InspectionStatusDisplay => EnumHelper.GetDisplayName(InspectionStatus);

    // 保养
    public bool NeedMaintenance { get; set; }
    public string? MaintPerson { get; set; }
    public int MaintCycleDays { get; set; }
    public DateTime? LastMaintDate { get; set; }
    public DateTime? CurrentMaintStartDate { get; set; }
    public EquipmentTaskStatus MaintStatus { get; set; }  // 物化存储
    public string MaintStatusDisplay => EnumHelper.GetDisplayName(MaintStatus);

    // 维修
    public DateTime? LastRepairDate { get; set; }

    // 状态
    public LifecycleStatus LifecycleStatus { get; set; }
    public string LifecycleStatusDisplay => EnumHelper.GetDisplayName(LifecycleStatus);
    public UsageType UsageType { get; set; }
    public string UsageTypeDisplay => EnumHelper.GetDisplayName(UsageType);
    public RunningStatus RunningStatus { get; set; }  // 物化存储
    public string RunningStatusDisplay => EnumHelper.GetDisplayName(RunningStatus);

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
    public LifecycleStatus LifecycleStatus { get; set; } = LifecycleStatus.Active;
    public UsageType UsageType { get; set; } = UsageType.Primary;
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
    public LifecycleStatus LifecycleStatus { get; set; }
    public UsageType UsageType { get; set; }
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
    public LifecycleStatus? LifecycleStatus { get; set; }
    public UsageType? UsageType { get; set; }
    public RunningStatus? RunningStatus { get; set; }
    public EquipmentTaskStatus? InspectionStatus { get; set; }
    public EquipmentTaskStatus? MaintStatus { get; set; }
    public string? Location { get; set; }
    public string? RelatedSection { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
}
