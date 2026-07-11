namespace MES.Data.Entities.Equipment;

/// <summary>
/// 保养记录（保养工单）
/// </summary>
public class MaintenanceOrder : BaseEntity
{
    /// <summary>
    /// 工单编号，格式 BY-YYYYMMDD-XXX
    /// </summary>
    public string MaintOrderNo { get; set; } = string.Empty;

    /// <summary>
    /// 关联设备
    /// </summary>
    public int EquipmentId { get; set; }

    /// <summary>
    /// 实际执行日期
    /// </summary>
    public DateTime? ActualDate { get; set; }

    /// <summary>
    /// 执行人
    /// </summary>
    public string? Executor { get; set; }

    /// <summary>
    /// 执行简述
    /// </summary>
    public string? ExecutionSummary { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
