using MES.Core.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace MES.Data.Entities;

/// <summary>
/// 销售项次实体
/// </summary>
public class OrderItem : BaseEntity
{
    /// <summary>
    /// 销售单ID，外键到SalesOrder
    /// </summary>
    public int SalesOrderId { get; set; }

    /// <summary>
    /// 项次号（订单内唯一）
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// 工单ID，外键到WorkOrder（预留字段，用于后续生产执行上下文集成）
    /// </summary>
    [NotMapped]
    public int? WorkOrderId { get; set; }

    /// <summary>
    /// 交货日期
    /// </summary>
    public DateTime DeliveryDate { get; set; }

    /// <summary>
    /// 是否延期违约金
    /// </summary>
    public bool DelayPenalty { get; set; }

    /// <summary>
    /// 结算方式
    /// </summary>
    public SettlementMethod SettlementMethod { get; set; }

    /// <summary>
    /// 物料名称
    /// </summary>
    public MaterialName MaterialName { get; set; }

    /// <summary>
    /// 产品标准ID，外键到ProductionStandard
    /// </summary>
    public int ProductionStandardId { get; set; }

    /// <summary>
    /// 交货状态
    /// </summary>
    public DeliveryState DeliveryState { get; set; }

    /// <summary>
    /// 标准牌号，外键到StandardGradeMapping.StandardGrade
    /// </summary>
    public string StandardGrade { get; set; } = null!;

    /// <summary>
    /// 工厂牌号（冗余）
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 密度（冗余）
    /// </summary>
    public decimal Density { get; set; }

    /// <summary>
    /// 外径
    /// </summary>
    public decimal OuterDiameter { get; set; }

    /// <summary>
    /// 壁厚
    /// </summary>
    public decimal WallThickness { get; set; }

    /// <summary>
    /// 规格（外径*壁厚，冗余）
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 外径下偏差
    /// </summary>
    public decimal OuterDiameterNegative { get; set; }

    /// <summary>
    /// 外径上偏差
    /// </summary>
    public decimal OuterDiameterPositive { get; set; }

    /// <summary>
    /// 壁厚下偏差
    /// </summary>
    public decimal WallThicknessNegative { get; set; }

    /// <summary>
    /// 壁厚上偏差
    /// </summary>
    public decimal WallThicknessPositive { get; set; }

    /// <summary>
    /// 长度状态
    /// </summary>
    public LengthStatus LengthStatus { get; set; }

    /// <summary>
    /// 最小长度（mm）
    /// </summary>
    public decimal? MinLength { get; set; }

    /// <summary>
    /// 最大长度（mm）
    /// </summary>
    public decimal? MaxLength { get; set; }

    /// <summary>
    /// 数量（支数）
    /// </summary>
    public int? Quantity { get; set; }

    /// <summary>
    /// 米数
    /// </summary>
    public decimal? Meters { get; set; }

    /// <summary>
    /// 合同重量
    /// </summary>
    public decimal ContractWeight { get; set; }

    /// <summary>
    /// 理算重量
    /// </summary>
    public decimal TheoreticalWeight { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 导航属性 - 销售单
    /// </summary>
    public SalesOrder SalesOrder { get; set; } = null!;

    /// <summary>
    /// 导航属性 - 产品标准
    /// </summary>
    public ProductionStandard ProductionStandard { get; set; } = null!;

    /// <summary>
    /// 导航属性 - 牌号对照
    /// </summary>
    public StandardGradeMapping? GradeMapping { get; set; }

    /// <summary>
    /// 导航属性 - 工单（预留，用于后续生产执行上下文集成）
    /// </summary>
    [NotMapped]
    public WorkOrder? WorkOrder { get; set; }

    /// <summary>
    /// 导航属性 - 产品要求
    /// </summary>
    public ProductRequirement? ProductRequirement { get; set; }
}