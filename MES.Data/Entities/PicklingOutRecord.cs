namespace MES.Data.Entities;

/// <summary>
/// 去油/酸洗完工记录 — 出缸+冲洗完成后登记，关联入缸记录（PicklingInRecord）
/// 冗余字段在创建时从入缸记录复制，用于计件工资结算和历史数据冻结
/// </summary>
public class PicklingOutRecord : BaseEntity
{
    /// <summary>
    /// 关联入缸记录ID
    /// </summary>
    public int PicklingInRecordId { get; set; }

    /// <summary>
    /// 完工日期（出缸+冲洗日期）
    /// </summary>
    public DateTime CompleteDate { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 数据来源（SCAN=扫码报工，MANUAL=手动录入），默认 MANUAL
    /// </summary>
    public string? DataSource { get; set; }

    // ========== 冗余字段（从入缸记录复制，用于计件工资结算）==========

    /// <summary>
    /// 关联生产批次ID
    /// </summary>
    public int ProductionBatchId { get; set; }

    /// <summary>
    /// 制造规格（从ProcessGroup冗余）
    /// </summary>
    public string? ManufacturingSpec { get; set; }

    /// <summary>
    /// 工段名称（"去油"或"酸洗"）
    /// </summary>
    public string SectionName { get; set; } = null!;

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

    /// <summary>
    /// 加工数量（支数）
    /// </summary>
    public int? Quantity { get; set; }

    /// <summary>
    /// 加工重量(kg)
    /// </summary>
    public decimal? Weight { get; set; }

    /// <summary>
    /// 是否成品
    /// </summary>
    public bool IsFinished { get; set; }

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string? PlantGrade { get; set; }

    // ========== 导航属性 ==========

    /// <summary>
    /// 所属入缸记录
    /// </summary>
    public PicklingInRecord PicklingInRecord { get; set; } = null!;
}
