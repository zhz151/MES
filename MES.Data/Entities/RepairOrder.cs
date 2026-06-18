namespace MES.Data.Entities;

/// <summary>
/// 维修记录（维修工单）
/// </summary>
public class RepairOrder : BaseEntity
{
    /// <summary>
    /// 工单编号，格式 WX-YYYYMMDD-XXX
    /// </summary>
    public string RepairOrderNo { get; set; } = string.Empty;

    /// <summary>
    /// 关联设备
    /// </summary>
    public int EquipmentId { get; set; }

    /// <summary>
    /// 故障描述
    /// </summary>
    public string FaultDescription { get; set; } = string.Empty;

    /// <summary>
    /// 故障类型
    /// </summary>
    public string? FaultType { get; set; }

    /// <summary>
    /// 优先级：Normal(普通) / Urgent(紧急) / Emergency(特急)
    /// </summary>
    public string Priority { get; set; } = nameof(MES.Core.Enums.RepairPriority.Normal);

    /// <summary>
    /// 维修状态：Pending(待维修) / InProgress(维修中) / Completed(已完成)
    /// </summary>
    public string RepairStatus { get; set; } = nameof(MES.Core.Enums.RepairOrderStatus.Pending);

    // ========== 报修 ==========

    /// <summary>
    /// 报修人
    /// </summary>
    public string ReportPerson { get; set; } = string.Empty;

    /// <summary>
    /// 报修时间
    /// </summary>
    public DateTime ReportTime { get; set; }

    // ========== 维修 ==========

    /// <summary>
    /// 维修人
    /// </summary>
    public string? RepairPerson { get; set; }

    /// <summary>
    /// 维修类别：厂内维修 / 外协维修 / 换模
    /// </summary>
    public string? RepairCategory { get; set; }

    /// <summary>
    /// 维修开始时间
    /// </summary>
    public DateTime? RepairStartTime { get; set; }

    /// <summary>
    /// 维修结束时间
    /// </summary>
    public DateTime? RepairEndTime { get; set; }

    /// <summary>
    /// 维修内容/结果
    /// </summary>
    public string? RepairContent { get; set; }

    /// <summary>
    /// 备件更换记录（JSON或简要文字）
    /// </summary>
    public string? SparePartUsed { get; set; }

    /// <summary>
    /// 辅助维修人（多人协作时补充，逗号分隔）
    /// </summary>
    public string? OtherRepairPersons { get; set; }
}
