using MES.Data.Entities.Batch;

namespace MES.Data.Entities.Quality;

/// <summary>
/// 过程检验 — 工序过程中的质量检验记录
/// </summary>
public class ProcessInspection : BaseEntity
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
    /// 执行序号（来自工序组中该工段的顺序值）
    /// </summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// 检验日期
    /// </summary>
    public DateTime InspectionDate { get; set; }

    /// <summary>
    /// 设备名称
    /// </summary>
    public string? EquipmentName { get; set; }

    /// <summary>
    /// 检验员
    /// </summary>
    public string? Inspector { get; set; }

    /// <summary>
    /// 班次（白班/中班/夜班）
    /// </summary>
    public string? Shift { get; set; }

    // ========== 数量/重量 ==========

    /// <summary>
    /// 检验数量（支数）
    /// </summary>
    public int? Quantity { get; set; }

    /// <summary>
    /// 检验重量(kg)
    /// </summary>
    public decimal? Weight { get; set; }

    // ========== 检验结果 ==========

    /// <summary>
    /// 检验项目
    /// </summary>
    public string? InspectionItem { get; set; }

    /// <summary>
    /// 合格支数
    /// </summary>
    public int? QualifiedQuantity { get; set; }

    /// <summary>
    /// 合格重量(kg)
    /// </summary>
    public decimal? QualifiedWeight { get; set; }

    /// <summary>
    /// 合格中让步放行支数
    /// </summary>
    public int? QualifiedConcessionQuantity { get; set; }

    /// <summary>
    /// 让步说明
    /// </summary>
    public string? ConcessionRemark { get; set; }

    /// <summary>
    /// 不合格返整支数
    /// </summary>
    public int? DefectReworkQuantity { get; set; }

    /// <summary>
    /// 不合格入在制库支数（原「入库」，2026-09-11 更名）
    /// </summary>
    public int? DefectWarehouseQuantity { get; set; }

    /// <summary>
    /// 不合格入次品库支数（原「报废」，2026-09-11 更名）
    /// </summary>
    public int? DefectScrapQuantity { get; set; }

    /// <summary>
    /// 不合格退货支数（2026-09-11 新增第 4 档）
    /// </summary>
    public int? DefectReturnQuantity { get; set; }

    // ========== 理论重量（自动计算） ==========

    /// <summary>
    /// 理论返整重(kg) = 检验重量/检验支数 × 返整支数，四舍五入取整
    /// </summary>
    public int? TheoreticalReworkWeight { get; set; }

    /// <summary>
    /// 理论入在制重(kg) = 检验重量/检验支数 × 入在制库支数，四舍五入取整
    /// </summary>
    public int? TheoreticalWarehouseWeight { get; set; }

    /// <summary>
    /// 理论入次库重(kg) = 检验重量/检验支数 × 入次品库支数，四舍五入取整
    /// </summary>
    public int? TheoreticalScrapWeight { get; set; }

    /// <summary>
    /// 理论退货重(kg) = 检验重量/检验支数 × 退货支数，四舍五入取整
    /// </summary>
    public int? TheoreticalReturnWeight { get; set; }

    /// <summary>
    /// 不合格情况描述
    /// </summary>
    public string? DefectDescription { get; set; }

    /// <summary>
    /// 来料单位
    /// </summary>
    public string? SourceUnit { get; set; }

    // ========== 批次冗余字段 ==========

    /// <summary>
    /// 挂牌号
    /// </summary>
    public string? TagNo { get; set; }

    /// <summary>
    /// 批次号（从 ProductionBatch 冗余，用于数据导入覆盖匹配）
    /// </summary>
    public string? BatchNo { get; set; }

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string? PlantGrade { get; set; }

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
    /// 检验照片附件（每记录限 QualityPhotoLimits.PerRecord 张）
    /// </summary>
    public ICollection<ProcessInspectionAttachment> Attachments { get; set; } = new List<ProcessInspectionAttachment>();
}
