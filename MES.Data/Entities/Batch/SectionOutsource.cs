using MES.Core.Enums;

namespace MES.Data.Entities.Batch;

/// <summary>
/// 工段委外 — 工段委外发出记录
/// </summary>
public class SectionOutsource : BaseEntity
{
    /// <summary>
    /// 关联生产批次ID
    /// </summary>
    public int ProductionBatchId { get; set; }

    /// <summary>
    /// 关联工序组ID
    /// </summary>
    public int ProcessGroupId { get; set; }

    // ========== 工序冗余字段 ==========

    /// <summary>
    /// 工序名称（从ProcessGroup冗余）
    /// </summary>
    public string ProcessName { get; set; } = null!;

    /// <summary>
    /// 制造规格（从ProcessGroup冗余）
    /// </summary>
    public string? ManufacturingSpec { get; set; }

    // ========== 委外信息 ==========

    /// <summary>
    /// 委外工段名称
    /// </summary>
    public string SectionName { get; set; } = null!;

    /// <summary>
    /// 执行序号（来自工序组中该工段的顺序值）
    /// </summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// 是否厂内（虚拟发外）。true=厂内（仅限冷轧拔工段，车间名称存 OutsourceVendor，无需回收记录，状态=略），false=真正委外
    /// </summary>
    public bool IsInternal { get; set; }

    /// <summary>
    /// 委外单位（厂内时为车间名称）
    /// </summary>
    public string OutsourceVendor { get; set; } = null!;

    /// <summary>
    /// 发出日期
    /// </summary>
    public DateTime SendOutDate { get; set; }

    /// <summary>
    /// 发出数量（支数）
    /// </summary>
    public int? SendQuantity { get; set; }

    /// <summary>
    /// 发出重量(kg)
    /// </summary>
    public decimal? SendWeight { get; set; }

    // ========== 计价信息（厂内 IsInternal=true 时无价，三字段均为 null）==========

    /// <summary>
    /// 计价单位（PerKg/PerPiece/PerMeter，DB 存英文 Key）
    /// </summary>
    public PricingUnit? PricingUnit { get; set; }

    /// <summary>
    /// 单价（元/Kg、元/支或元/米，厂内无价）
    /// </summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>
    /// 总价（元，可手改；缺省自动 = 单价×取量，保留 2 位）
    /// </summary>
    public decimal? TotalAmount { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public SectionOutsourceStatus Status { get; set; } = SectionOutsourceStatus.PendingRecovery;

    // ========== 批次冗余字段 ==========

    /// <summary>
    /// 挂牌号
    /// </summary>
    public string? TagNo { get; set; }

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string? PlantGrade { get; set; }

    /// <summary>
    /// 委外规格
    /// </summary>
    public string? OutsourceSpec { get; set; }

    /// <summary>
    /// 要求收回日期
    /// </summary>
    public DateTime? ExpectedReturnDate { get; set; }

    /// <summary>
    /// 是否紧急
    /// </summary>
    public bool IsUrgent { get; set; }

    /// <summary>
    /// 产类（荒管/在制/成品）
    /// </summary>
    public string? ProductStatus { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 数据来源（SCAN=扫码报工，MANUAL=手动录入），默认 MANUAL
    /// </summary>
    public string? DataSource { get; set; }

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
    /// 委外回收记录列表
    /// </summary>
    public List<OutsourceRecovery> OutsourceRecoveries { get; set; } = new();
}
