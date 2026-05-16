namespace MES.Data.Entities;

/// <summary>
/// 点检记录
/// </summary>
public class InspectionRecord : BaseEntity
{
    /// <summary>
    /// 点检记录编号，格式 DJ-YYYYMMDD-XXX
    /// </summary>
    public string RecordNo { get; set; } = string.Empty;

    /// <summary>
    /// 关联设备
    /// </summary>
    public int EquipmentId { get; set; }

    /// <summary>
    /// 实际点检日
    /// </summary>
    public DateTime? ActualDate { get; set; }

    /// <summary>
    /// 点检人
    /// </summary>
    public string? Inspector { get; set; }

    /// <summary>
    /// 执行简述
    /// </summary>
    public string? ExecutionSummary { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
