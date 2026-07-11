using MES.Core.Models;

namespace MES.Core.DTOs.Equipment;

/// <summary>
/// 维修记录查询参数
/// </summary>
public class RepairOrderQueryParams : QueryParams
{
    /// <summary>
    /// 设备ID筛选
    /// </summary>
    public int? EquipmentId { get; set; }

    /// <summary>
    /// 维修状态筛选
    /// </summary>
    public string? RepairStatus { get; set; }

    /// <summary>
    /// 优先级筛选
    /// </summary>
    public string? Priority { get; set; }

    /// <summary>
    /// 报修日期范围-开始
    /// </summary>
    public DateTime? ReportTimeFrom { get; set; }

    /// <summary>
    /// 报修日期范围-结束
    /// </summary>
    public DateTime? ReportTimeTo { get; set; }
}
