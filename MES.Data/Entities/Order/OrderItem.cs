// 文件路径: MES.Data/Entities/OrderItem.cs
using MES.Core.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace MES.Data.Entities.Order;

/// <summary>
/// 订单项次实体
/// </summary>
public class OrderItem : BaseEntity
{
    /// <summary>
    /// 销售订单ID（外键）
    /// </summary>
    public int SalesOrderId { get; set; }

    /// <summary>
    /// 订单号（从 SalesOrder 冗余，用于数据导入覆盖匹配）
    /// </summary>
    public string? OrderNumber { get; set; }

    /// <summary>
    /// 项次号（订单内唯一）
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// 交货日期
    /// </summary>
    public DateTime DeliveryDate { get; set; }

    /// <summary>
    /// 是否延期罚款
    /// </summary>
    public bool DelayPenalty { get; set; }

    /// <summary>
    /// 结算方式
    /// </summary>
    public SettlementMethod SettlementMethod { get; set; }

    /// <summary>
    /// 钢管制造类别
    /// </summary>
    public PipeManufacturingType PipeManufacturingType { get; set; }

    /// <summary>
    /// 标准号（从 StandardRegister 弱引用，无 FK 约束）
    /// </summary>
    public string? StandardNo { get; set; }

    /// <summary>
    /// 交货状态
    /// </summary>
    public DeliveryState DeliveryState { get; set; }

    /// <summary>
    /// 标准牌号
    /// </summary>
    public string StandardGrade { get; set; } = null!;

    /// <summary>
    /// 工厂牌号（冗余字段，从牌号对照表自动填充）
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 密度（冗余字段，从牌号对照表自动填充）
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
    /// 规格（外径*壁厚，冗余字段，系统自动生成）
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
    /// 理算重量（系统自动计算）
    /// </summary>
    public decimal TheoreticalWeight { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    // ========== 导航属性 ==========

    /// <summary>
    /// 所属销售订单
    /// </summary>
    public virtual SalesOrder SalesOrder { get; set; } = null!;

    /// <summary>
    /// 产品要求（一对一关系）
    /// </summary>
    public virtual ProductRequirement? ProductRequirement { get; set; }
}
