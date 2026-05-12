namespace MES.Data.Entities;

/// <summary>
/// 工序组 — 对应工艺卡中的一行生产工序
/// </summary>
public class ProcessGroup : BaseEntity
{
    /// <summary>
    /// 关联生产批次ID
    /// </summary>
    public int ProductionBatchId { get; set; }

    /// <summary>
    /// 组内序号
    /// </summary>
    public int SequenceNumber { get; set; }

    // ========== 基础信息（文本） ==========

    /// <summary>
    /// 工序名称（如"60冷轧"）
    /// </summary>
    public string ProcessName { get; set; } = null!;

    /// <summary>
    /// 制造规格
    /// </summary>
    public string? ManufacturingSpec { get; set; }

    /// <summary>
    /// 外径公差
    /// </summary>
    public string? OuterDiameterTolerance { get; set; }

    /// <summary>
    /// 壁厚公差
    /// </summary>
    public string? WallThicknessTolerance { get; set; }

    /// <summary>
    /// 制造长度
    /// </summary>
    public string? ManufacturingLength { get; set; }

    /// <summary>
    /// 断切处理
    /// </summary>
    public string? CuttingTreatment { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    // ========== 工段执行顺序（数值类型，表示执行顺序，空=本工序不涉及） ==========

    /// <summary>
    /// 冷轧拔
    /// </summary>
    public int? ColdRollDraw { get; set; }

    /// <summary>
    /// 油管断
    /// </summary>
    public int? OilPipeCut { get; set; }

    /// <summary>
    /// 去油
    /// </summary>
    public int? Degrease { get; set; }

    /// <summary>
    /// 固溶
    /// </summary>
    public int? Solution { get; set; }

    /// <summary>
    /// 矫直
    /// </summary>
    public int? Straighten { get; set; }

    /// <summary>
    /// 断切
    /// </summary>
    public int? Cut { get; set; }

    /// <summary>
    /// 侧壁（测量壁厚）
    /// </summary>
    public int? ThicknessMeasure { get; set; }

    /// <summary>
    /// 酸洗
    /// </summary>
    public int? Pickle { get; set; }

    /// <summary>
    /// 外抛光
    /// </summary>
    public int? OuterPolish { get; set; }

    /// <summary>
    /// 内修磨
    /// </summary>
    public int? InnerGrinding { get; set; }

    /// <summary>
    /// 外点磨
    /// </summary>
    public int? OuterSpotGrinding { get; set; }

    /// <summary>
    /// 检验
    /// </summary>
    public int? Inspection { get; set; }

    /// <summary>
    /// 打焊头
    /// </summary>
    public int? WeldingHead { get; set; }

    /// <summary>
    /// 润滑
    /// </summary>
    public int? Lubrication { get; set; }

    /// <summary>
    /// 入库
    /// </summary>
    public int? Warehouse { get; set; }

    // ========== 导航属性 ==========

    /// <summary>
    /// 所属生产批次
    /// </summary>
    public ProductionBatch ProductionBatch { get; set; } = null!;
}
