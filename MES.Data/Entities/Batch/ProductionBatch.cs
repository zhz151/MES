using MES.Core.Enums;

namespace MES.Data.Entities.Batch;

/// <summary>
/// 生产批次（生产编号）
/// </summary>
public class ProductionBatch : BaseEntity
{
    // ========== 批次自身字段 ==========

    /// <summary>
    /// 生产编号（业务唯一标识）
    /// 格式：YYMM-0001（年的后两位+月+4位序号）
    /// </summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>
    /// 批次状态（None=未产 InProgress=在产 Completed=完成）
    /// </summary>
    public BatchStatus Status { get; set; } = BatchStatus.None;

    /// <summary>
    /// 挂牌号（批次的补充标识）
    /// </summary>
    public string? TagNo { get; set; }

    /// <summary>
    /// 生产类型（荒管生产/在制生产/库存/外购/返整/委外生产/对外加工）
    /// </summary>
    public string? ProductionType { get; set; }

    /// <summary>
    /// 制造物品（MaterialType 枚举名：OrderFinished/Finished/Surplus/SpecialDeliveryStatus）
    /// </summary>
    public string ManufacturingItem { get; set; } = null!;

    /// <summary>
    /// 制成倍数（定尺时= floor(投料单重/工单单重)）
    /// </summary>
    public int ProductionRatio { get; set; }

    /// <summary>
    /// 状态辅助（false=自动计算，true=强制完成）
    /// </summary>
    public bool IsForceCompleted { get; set; }

    /// <summary>
    /// 质量备注
    /// </summary>
    public string? QualityRemark { get; set; }

    /// <summary>
    /// 固溶参数（如"1080℃ ±15℃"）
    /// </summary>
    public string? SolutionParams { get; set; }

    /// <summary>
    /// 截止执行日
    /// </summary>
    public DateTime? CurrentExecDate { get; set; }

    /// <summary>
    /// 当前执行的工序名称
    /// </summary>
    public string? CurrentGroupName { get; set; }

    /// <summary>
    /// 当前执行的工段名称
    /// </summary>
    public string? CurrentSectionName { get; set; }

    /// <summary>
    /// 在产设备名称
    /// </summary>
    public string? CurrentEquipmentName { get; set; }

    /// <summary>
    /// 当前委外单位
    /// </summary>
    public string? CurrentOutsource { get; set; }

    /// <summary>
    /// 当前规格（当前工段所在工序块的制造规格）
    /// </summary>
    public string? CurrentSpec { get; set; }

    /// <summary>
    /// 下个执行工段
    /// </summary>
    public string? NextSectionName { get; set; }

    /// <summary>
    /// 对应规格（下个工段所在工序块的制造规格）
    /// </summary>
    public string? CorrespondingSpec { get; set; }

    /// <summary>
    /// 下一工序（下个工段所在工序组的工序名称）
    /// </summary>
    public string? NextProcess { get; set; }

    /// <summary>
    /// 有效投料疑问（正常/疑问），基于最近过程检验与投料量比值计算，由 UpdateBatchTrackingFromRecords 刷新
    /// </summary>
    public bool? ValidInputQuestion { get; set; }

    /// <summary>
    /// 当前工段是否完工：冷轧拔总重≥有效原料×95%→完工；工段委外有回收→完工；其它有记录即完工
    /// null=无记录、true=完工、false=生产中
    /// </summary>
    public bool? CurrentSectionCompleted { get; set; }

    /// <summary>
    /// 剩余工量（天）：从当前工段到最终完成日的预计天数，根据各工段类型累加计算
    /// 完成/作废状态为0；四舍五入取整
    /// </summary>
    public int RemainingWorkDays { get; set; }

    /// <summary>
    /// 全工量（天）：从组内序号1开始，所有工段的标准天数累加
    /// 用于反映批次总工时，不受当前进度影响；四舍五入取整
    /// </summary>
    public int TotalWorkDays { get; set; }

    /// <summary>
    /// 质量过程是否完结（人控开关，手动切换）
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 乐观并发控制版本号
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    // ========== 工单冗余字段（从WorkOrder复制，只读） ==========

    /// <summary>
    /// 关联工单号
    /// </summary>
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>
    /// 源订单号
    /// </summary>
    public string SalesOrderNo { get; set; } = null!;

    /// <summary>
    /// 主号
    /// </summary>
    public string ProductionMainNo { get; set; } = null!;

    /// <summary>
    /// 次号
    /// </summary>
    public string? ProductionSubNo { get; set; }

    /// <summary>
    /// 合并的项次ID列表（逗号分隔）
    /// </summary>
    public string OrderItemIds { get; set; } = null!;

    /// <summary>
    /// 签订日期
    /// </summary>
    public DateTime SignDate { get; set; }

    /// <summary>
    /// 业务员
    /// </summary>
    public string Salesman { get; set; } = null!;

    /// <summary>
    /// 最终用户
    /// </summary>
    public string? EndCustomer { get; set; }

    /// <summary>
    /// 交货日期
    /// </summary>
    public DateTime DeliveryDate { get; set; }

    /// <summary>
    /// 是否延期罚款
    /// </summary>
    public bool DelayPenalty { get; set; }

    /// <summary>
    /// 钢管制造类别（SeamlessPipe/WeldedPipe）
    /// </summary>
    public string MaterialName { get; set; } = null!;

    /// <summary>
    /// 结算方式
    /// </summary>
    public string SettlementMethod { get; set; } = null!;

    /// <summary>
    /// 产品标准编码
    /// </summary>
    public string StandardCode { get; set; } = null!;

    /// <summary>
    /// 交货状态（工单约定的交付要求）
    /// </summary>
    public string DeliveryState { get; set; } = null!;

    /// <summary>
    /// 制造状态（批次执行的实际制造状态，与交货状态同枚举）
    /// </summary>
    public string? ManufacturingStatus { get; set; }

    /// <summary>
    /// 工厂牌号（钢种）
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 规格（外径*壁厚）
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 外径负公差
    /// </summary>
    public decimal OuterDiameterNegative { get; set; }

    /// <summary>
    /// 外径正公差
    /// </summary>
    public decimal OuterDiameterPositive { get; set; }

    /// <summary>
    /// 壁厚负公差
    /// </summary>
    public decimal WallThicknessNegative { get; set; }

    /// <summary>
    /// 壁厚正公差
    /// </summary>
    public decimal WallThicknessPositive { get; set; }

    /// <summary>
    /// 长度状态
    /// </summary>
    public string LengthStatus { get; set; } = null!;

    /// <summary>
    /// 最小长度(mm)
    /// </summary>
    public decimal? MinLength { get; set; }

    /// <summary>
    /// 最大长度(mm)
    /// </summary>
    public decimal? MaxLength { get; set; }

    /// <summary>
    /// 总数量（支数）
    /// </summary>
    public int TotalQuantity { get; set; }

    /// <summary>
    /// 总米数
    /// </summary>
    public decimal TotalMeters { get; set; }

    /// <summary>
    /// 总重量
    /// </summary>
    public decimal TotalWeight { get; set; }

    /// <summary>
    /// 总项次数
    /// </summary>
    public int TotalItemCount { get; set; }

    /// <summary>
    /// 明细（格式：项次号,长度mm,支数;）
    /// </summary>
    public string? ItemDetails { get; set; }

    /// <summary>
    /// 技术要求
    /// </summary>
    public string TechnicalRequirements { get; set; } = null!;

    // ========== 仓库信息冗余字段（从InventoryBatch复制） ==========

    /// <summary>
    /// 来源库存批次号
    /// </summary>
    public string? SourceBatchNo { get; set; }

    /// <summary>
    /// 关联仓库ID
    /// </summary>
    public int? WarehouseId { get; set; }

    /// <summary>
    /// 原料类型（荒管/圆钢等）
    /// </summary>
    public string? SourceMaterialType { get; set; }

    /// <summary>
    /// 入库来源
    /// </summary>
    public string? InboundSource { get; set; }

    /// <summary>
    /// 来料单位
    /// </summary>
    public string? SourceName { get; set; }

    /// <summary>
    /// 入库日期
    /// </summary>
    public DateTime? InboundDate { get; set; }

    /// <summary>
    /// 炉号
    /// </summary>
    public string? SourceHeatNo { get; set; }

    /// <summary>
    /// 来源工厂牌号（钢种）
    /// </summary>
    public string? SourcePlantGrade { get; set; }

    /// <summary>
    /// 来源名义规格
    /// </summary>
    public string? SourceSpecification { get; set; }

    /// <summary>
    /// 来源长度状态
    /// </summary>
    public string? SourceLengthStatus { get; set; }

    /// <summary>
    /// 单支重(kg)
    /// </summary>
    public decimal? SourceUnitWeight { get; set; }

    /// <summary>
    /// 领料支数
    /// </summary>
    public int? InputQuantity { get; set; }

    /// <summary>
    /// 领料重量
    /// </summary>
    public decimal? InputWeight { get; set; }

    /// <summary>
    /// 现有效原料支数
    /// </summary>
    public int? CurrentValidQty { get; set; }

    /// <summary>
    /// 现有效原料重量
    /// </summary>
    public decimal? CurrentValidWeight { get; set; }

    // ========== 导航属性 ==========

    /// <summary>
    /// 工序组列表
    /// </summary>
    public List<ProcessGroup> ProcessGroups { get; set; } = new();

    /// <summary>
    /// 来源库存批次关联（合并投料）
    /// </summary>
    public List<ProductionBatchInventory> ProductionBatchInventories { get; set; } = new();
}
