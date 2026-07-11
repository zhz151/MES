using MES.Core.Models;

namespace MES.Core.DTOs.Equipment;

/// <summary>
/// 保养记录查询参数
/// </summary>
public class MaintenanceOrderQueryParams : QueryParams
{
    /// <summary>
    /// 设备ID筛选
    /// </summary>
    public int? EquipmentId { get; set; }
}
