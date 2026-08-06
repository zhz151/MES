using MES.Core.Enums;
using MES.Core.Models;

namespace MES.Core.DTOs.Equipment;

/// <summary>
/// 设备查询参数
/// </summary>
public class EquipmentQueryParams : QueryParams
{
    /// <summary>
    /// 生命周期筛选
    /// </summary>
    public LifecycleStatus? LifecycleStatus { get; set; }

    /// <summary>
    /// 作用类型筛选
    /// </summary>
    public UsageType? UsageType { get; set; }

    /// <summary>
    /// 运行状态筛选
    /// </summary>
    public RunningStatus? RunningStatus { get; set; }

    /// <summary>
    /// 点检状况筛选
    /// </summary>
    public EquipmentTaskStatus? InspectionStatus { get; set; }

    /// <summary>
    /// 保养状况筛选
    /// </summary>
    public EquipmentTaskStatus? MaintStatus { get; set; }

    /// <summary>
    /// 所在区域筛选
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// 关联工段筛选
    /// </summary>
    public string? RelatedSection { get; set; }
}
