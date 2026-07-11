namespace MES.Data.Entities.Equipment;

/// <summary>
/// 设备台账
/// </summary>
public class Equipment : BaseEntity
{
    /// <summary>
    /// 设备编号
    /// </summary>
    public string EquipmentCode { get; set; } = string.Empty;

    /// <summary>
    /// 设备名称
    /// </summary>
    public string EquipmentName { get; set; } = string.Empty;

    /// <summary>
    /// 型号规格
    /// </summary>
    public string? ModelNumber { get; set; }

    /// <summary>
    /// 技术参数
    /// </summary>
    public string? TechnicalParams { get; set; }

    /// <summary>
    /// 制造商
    /// </summary>
    public string? Manufacturer { get; set; }

    /// <summary>
    /// 安装日期
    /// </summary>
    public DateTime? InstallationDate { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 所在区域
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// 关联工段
    /// </summary>
    public string? RelatedSection { get; set; }

    // ========== 点检参数 ==========

    /// <summary>
    /// 是否需点检
    /// </summary>
    public bool NeedInspection { get; set; }

    /// <summary>
    /// 点检负责人
    /// </summary>
    public string? InspectionPerson { get; set; }

    /// <summary>
    /// 点检周期（天），默认7
    /// </summary>
    public int InspectionCycleDays { get; set; }

    /// <summary>
    /// 最近点检日期
    /// </summary>
    public DateTime? LastInspectionDate { get; set; }

    /// <summary>
    /// 本次点检日起始
    /// </summary>
    public DateTime? CurrentInspectionStartDate { get; set; }

    // ========== 保养参数 ==========

    /// <summary>
    /// 是否需保养
    /// </summary>
    public bool NeedMaintenance { get; set; }

    /// <summary>
    /// 保养负责人
    /// </summary>
    public string? MaintPerson { get; set; }

    /// <summary>
    /// 保养周期（天），默认30
    /// </summary>
    public int MaintCycleDays { get; set; }

    /// <summary>
    /// 最近保养日期
    /// </summary>
    public DateTime? LastMaintDate { get; set; }

    /// <summary>
    /// 本次保养日起始
    /// </summary>
    public DateTime? CurrentMaintStartDate { get; set; }

    /// <summary>最近维修日期（从RepairOrder取最近RepairEndTime）</summary>
    public DateTime? LastRepairDate { get; set; }

    // ========== 物化状态字段（由 RepairOrder/InspectionRecord/MaintenanceOrder 写操作同步更新） ==========

    /// <summary>
    /// 运行状态：Normal(正常) / Pending(待维修) / InProgress(维修中)
    /// </summary>
    public string RunningStatus { get; set; } = nameof(MES.Core.Enums.RunningStatus.Normal);

    /// <summary>
    /// 点检状况：NotApplicable(不适用) / Pending(待执行) / Normal(正常) / Overdue(逾期)
    /// </summary>
    public string InspectionStatus { get; set; } = nameof(MES.Core.Enums.EquipmentTaskStatus.NotApplicable);

    /// <summary>
    /// 保养状况：NotApplicable(不适用) / Pending(待执行) / Normal(正常) / Overdue(逾期)
    /// </summary>
    public string MaintStatus { get; set; } = nameof(MES.Core.Enums.EquipmentTaskStatus.NotApplicable);

    // ========== 状态字段（存储） ==========

    /// <summary>
    /// 生命周期：Active(在用) / Standby(备用) / Scrapped(报废)
    /// </summary>
    public string LifecycleStatus { get; set; } = nameof(MES.Core.Enums.LifecycleStatus.Active);

    /// <summary>
    /// 作用类型：Primary(主生产设备) / Secondary(辅生产设备) / Other(其它)
    /// </summary>
    public string UsageType { get; set; } = nameof(MES.Core.Enums.UsageType.Primary);
}
