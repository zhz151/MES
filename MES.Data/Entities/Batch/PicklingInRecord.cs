using MES.Core.Enums;

namespace MES.Data.Entities.Batch;

/// <summary>
/// 去油/酸洗入缸记录 — 入缸报工，浸泡完成后关联完工记录（PicklingOutRecord）
/// </summary>
public class PicklingInRecord : BaseEntity
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

    // ========== 入缸信息 ==========

    /// <summary>
    /// 工段名称（"去油"或"酸洗"）
    /// </summary>
    public string SectionName { get; set; } = null!;

    /// <summary>
    /// 执行序号（来自工序组中该工段的顺序值）
    /// </summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// 入缸日期
    /// </summary>
    public DateTime InDate { get; set; }

    /// <summary>
    /// 状态（浸泡中/已完工）
    /// </summary>
    public PicklingStatus Status { get; set; } = PicklingStatus.Soaking;

    // ========== 执行信息 ==========

    /// <summary>
    /// 设备名称
    /// </summary>
    public string? EquipmentName { get; set; }

    /// <summary>
    /// 操作人
    /// </summary>
    public string? Operator { get; set; }

    /// <summary>
    /// 班次（白班/中班/夜班）
    /// </summary>
    public string? Shift { get; set; }

    // ========== 数量/重量 ==========

    /// <summary>
    /// 加工数量（支数）
    /// </summary>
    public int? Quantity { get; set; }

    /// <summary>
    /// 加工重量(kg)
    /// </summary>
    public decimal? Weight { get; set; }

    /// <summary>
    /// 制造状态（荒管/在制/成品）
    /// </summary>
    public string? ProductStatus { get; set; }

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
    /// 完工记录（1:1，一次出缸一条完工记录）
    /// </summary>
    public List<PicklingOutRecord> PicklingOutRecords { get; set; } = new();
}
