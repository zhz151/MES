// 文件路径: MES.Core/DTOs/OrderItemDto.cs
using MES.Core.Enums;

namespace MES.Core.DTOs.Order;

/// <summary>
/// 订单项次 DTO
/// </summary>
public class OrderItemDto
{
    /// <summary>
    /// 项次ID
    /// </summary>
    public int Id { get; set; }

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
    /// 标准号（用于前端显示）
    /// </summary>
    public string StandardNo { get; set; } = null!;

    /// <summary>
    /// 交货状态
    /// </summary>
    public DeliveryState DeliveryState { get; set; }

    /// <summary>
    /// 标准牌号
    /// </summary>
    public string StandardGrade { get; set; } = null!;

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 密度(g/cm³)
    /// </summary>
    public decimal Density { get; set; }

    /// <summary>
    /// 外径(mm)
    /// </summary>
    public decimal OuterDiameter { get; set; }

    /// <summary>
    /// 壁厚(mm)
    /// </summary>
    public decimal WallThickness { get; set; }

    /// <summary>
    /// 规格（外径*壁厚）
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 外径下偏差(mm)
    /// </summary>
    public decimal OuterDiameterNegative { get; set; }

    /// <summary>
    /// 外径上偏差(mm)
    /// </summary>
    public decimal OuterDiameterPositive { get; set; }

    /// <summary>
    /// 壁厚下偏差(mm)
    /// </summary>
    public decimal WallThicknessNegative { get; set; }

    /// <summary>
    /// 壁厚上偏差(mm)
    /// </summary>
    public decimal WallThicknessPositive { get; set; }

    /// <summary>
    /// 长度状态
    /// </summary>
    public LengthStatus LengthStatus { get; set; }

    /// <summary>
    /// 最小长度(mm)
    /// </summary>
    public decimal? MinLength { get; set; }

    /// <summary>
    /// 最大长度(mm)
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
    /// 合同重量(kg)
    /// </summary>
    public decimal ContractWeight { get; set; }

    /// <summary>
    /// 理算重量(kg)
    /// </summary>
    public decimal TheoreticalWeight { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 创建时间（审计字段）
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 更新时间（审计字段）
    /// </summary>
    public DateTimeOffset UpdatedTime { get; set; }
}
