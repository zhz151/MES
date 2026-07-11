using MES.Core.Models;

namespace MES.Core.DTOs.Equipment;

/// <summary>
/// 点检记录查询参数
/// </summary>
public class InspectionRecordQueryParams : QueryParams
{
    /// <summary>
    /// 设备ID筛选
    /// </summary>
    public int? EquipmentId { get; set; }
}
