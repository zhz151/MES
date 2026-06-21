using MES.Core.Enums;

namespace MES.Core.DTOs;

/// <summary>
/// 工单生成项次 DTO
/// </summary>
public class OrderItemForWorkOrderDto
{
    /// <summary>
    /// 订单项次ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 订单号
    /// </summary>
    public string OrderNumber { get; set; } = null!;

    /// <summary>
    /// 项次号
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// 物料名称
    /// </summary>
    public MaterialName MaterialName { get; set; }

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
    /// 标准号
    /// </summary>
    public string StandardNo { get; set; } = null!;

    /// <summary>
    /// 交货状态
    /// </summary>
    public DeliveryState DeliveryState { get; set; }

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 规格
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 外径-外径负公差
    /// </summary>
    public decimal OuterDiameterNegative { get; set; }

    /// <summary>
    /// 外径+外径正公差
    /// </summary>
    public decimal OuterDiameterPositive { get; set; }

    /// <summary>
    /// 壁厚-壁厚负公差
    /// </summary>
    public decimal WallThicknessNegative { get; set; }

    /// <summary>
    /// 壁厚+壁厚正公差
    /// </summary>
    public decimal WallThicknessPositive { get; set; }

    /// <summary>
    /// 长度状态
    /// </summary>
    public LengthStatus LengthStatus { get; set; }

    /// <summary>
    /// 最小长度
    /// </summary>
    public decimal? MinLength { get; set; }

    /// <summary>
    /// 最大长度
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
    /// 技术要求类型（Normal/Special）
    /// </summary>
    public string RequirementType { get; set; } = null!;

    /// <summary>
    /// 系统生成的主号（可修改）
    /// </summary>
    public string SuggestedMainNo { get; set; } = null!;

    /// <summary>
    /// 原主号（待修正状态时显示，只读）
    /// </summary>
    public string? OriginalMainNo { get; set; }

    /// <summary>
    /// 原次号（待修正状态时显示，只读）
    /// </summary>
    public string? OriginalSubNo { get; set; }
}