using MES.Data.Entities.Batch;

namespace MES.Data.Entities.Quality;

/// <summary>
/// 巡检单 — 质量检验的一种类型（过程巡检记录）。
/// 记录「谁、何时、对哪个批次哪道工序工段、在什么在产条件下、巡检了哪些项、结果如何」，
/// 涉及整改时记录整改内容与验证结果；照片按类型分存附件子表（巡检照片 / 整改验证照片）。
/// 记录模式对齐不合格反馈：从批次冗余工单号/牌号，从工序组对齐执行序号，产类自动计算。
/// </summary>
public class InspectionPatrol : BaseEntity
{
    // ========== G1: 巡检信息 ==========

    /// <summary>
    /// 巡检日期
    /// </summary>
    public DateTime PatrolDate { get; set; }

    /// <summary>
    /// 巡检人
    /// </summary>
    public string Inspector { get; set; } = null!;

    /// <summary>
    /// 数据来源（SCAN=扫码报工，MANUAL=手动录入），默认 MANUAL
    /// </summary>
    public string? DataSource { get; set; }

    // ========== G2: 位置信息 ==========

    /// <summary>
    /// 关联生产批次ID
    /// </summary>
    public int ProductionBatchId { get; set; }

    /// <summary>
    /// 生产编号
    /// </summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>
    /// 工单号（从批次冗余）
    /// </summary>
    public string? WorkOrderNo { get; set; }

    /// <summary>
    /// 关联工序组ID
    /// </summary>
    public int ProcessGroupId { get; set; }

    /// <summary>
    /// 工序名称（从工序组冗余，存英文 Key）
    /// </summary>
    public string ProcessName { get; set; } = null!;

    /// <summary>
    /// 制造规格（从工序组冗余）
    /// </summary>
    public string? ManufacturingSpec { get; set; }

    /// <summary>
    /// 工段名称（存英文 Key）
    /// </summary>
    public string SectionName { get; set; } = null!;

    /// <summary>
    /// 执行序号（来自工序组中该工段的顺序值）
    /// </summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// 产类（系统自动计算，存英文 Key）
    /// </summary>
    public string? ProductStatus { get; set; }

    /// <summary>
    /// 工厂牌号（从批次冗余）
    /// </summary>
    public string? PlantGrade { get; set; }

    /// <summary>
    /// 在产单位/车间（文本，录入时可选委外单位档案）
    /// </summary>
    public string? ProductionUnit { get; set; }

    /// <summary>
    /// 在产设备名（文本，手工录入，允许留空；不提供设备台账下拉）
    /// </summary>
    public string? EquipmentName { get; set; }

    /// <summary>
    /// 在产操作人（文本，录入时走员工档案实名选择器）
    /// </summary>
    public string? ProductionOperator { get; set; }

    // ========== G4: 整改闭环（涉及整改为开关，其余仅在「是」时有值） ==========

    /// <summary>
    /// 涉及整改
    /// </summary>
    public bool NeedRectification { get; set; }

    /// <summary>
    /// 整改内容描述
    /// </summary>
    public string? RectificationDescription { get; set; }

    /// <summary>
    /// 验证结果（文本）
    /// </summary>
    public string? VerificationResult { get; set; }

    /// <summary>
    /// 整改人（第二次扫码补整改时的操作人，供追溯；与巡检人可不同人）
    /// </summary>
    public string? RectificationOperator { get; set; }

    /// <summary>
    /// 是否闭环（整改验证通过后勾选）
    /// </summary>
    public bool IsClosed { get; set; }

    // ========== 导航属性 ==========

    /// <summary>
    /// 所属生产批次
    /// </summary>
    public ProductionBatch ProductionBatch { get; set; } = null!;

    /// <summary>
    /// 所属工序组
    /// </summary>
    public ProcessGroup ProcessGroup { get; set; } = null!;

    /// <summary>
    /// 巡检明细（巡检项 / 巡检结果 / 备注，一对多）
    /// </summary>
    public List<InspectionPatrolItem> Items { get; set; } = new();

    /// <summary>
    /// 照片附件（巡检照片 / 整改验证照片）
    /// </summary>
    public List<InspectionPatrolAttachment> Attachments { get; set; } = new();
}
