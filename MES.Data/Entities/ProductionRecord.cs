namespace MES.Data.Entities;

/// <summary>
/// 内部生产记录 — 工段内部执行记录
/// </summary>
public class ProductionRecord : BaseEntity
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

    // ========== 执行信息 ==========

    /// <summary>
    /// 工段名称（如"冷轧拔""矫直"）
    /// </summary>
    public string SectionName { get; set; } = null!;

    /// <summary>
    /// 执行序号（来自工序组中该工段的顺序值，前端计算传入）
    /// </summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// 执行日期
    /// </summary>
    public DateTime ExecDate { get; set; }

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
    /// 不合格支数
    /// </summary>
    public int? DefectQuantity { get; set; }

    /// <summary>
    /// 不合格重量(kg)
    /// </summary>
    public decimal? DefectWeight { get; set; }

    /// <summary>
    /// 是否成品
    /// </summary>
    public bool IsFinished { get; set; }

    /// <summary>
    /// 断切倍数
    /// </summary>
    public decimal? CuttingMultiple { get; set; }

    /// <summary>
    /// 成品断切长度(mm)
    /// </summary>
    public decimal? FinishedCutLength { get; set; }

    /// <summary>
    /// 切后支数
    /// </summary>
    public int? PostCutQuantity { get; set; }

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

    // ========== 导航属性 ==========

    /// <summary>
    /// 所属生产批次
    /// </summary>
    public ProductionBatch ProductionBatch { get; set; } = null!;

    /// <summary>
    /// 所属工序组
    /// </summary>
    public ProcessGroup ProcessGroup { get; set; } = null!;
}
