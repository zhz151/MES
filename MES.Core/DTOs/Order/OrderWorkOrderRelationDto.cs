using System.Collections.Generic;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Order;

/// <summary>
/// 订单的工单项次追溯关系 DTO
/// </summary>
public class OrderWorkOrderRelationDto
{
    /// <summary>
    /// 订单ID
    /// </summary>
    public int SalesOrderId { get; set; }

    /// <summary>
    /// 订单号
    /// </summary>
    public string OrderNumber { get; set; } = null!;

    /// <summary>
    /// 签订日期
    /// </summary>
    public DateTime SignDate { get; set; }

    /// <summary>
    /// 业务员
    /// </summary>
    public string Salesman { get; set; } = null!;

    /// <summary>
    /// 客户名称
    /// </summary>
    public string CustomerName { get; set; } = null!;

    /// <summary>
    /// 最终用户
    /// </summary>
    public string? EndCustomer { get; set; }

    /// <summary>
    /// 该订单下的工单列表
    /// </summary>
    public List<WorkOrderRelationDto> WorkOrders { get; set; } = new();
}

/// <summary>
/// 工单追溯关系 DTO
/// </summary>
public class WorkOrderRelationDto
{
    /// <summary>
    /// 工单ID
    /// </summary>
    public int WorkOrderId { get; set; }

    /// <summary>
    /// 工单号
    /// </summary>
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>
    /// 主号
    /// </summary>
    public string ProductionMainNo { get; set; } = null!;

    /// <summary>
    /// 次号
    /// </summary>
    public string? ProductionSubNo { get; set; }

    /// <summary>
    /// 工单状态
    /// </summary>
    public WorkOrderStatus Status { get; set; }

    /// <summary>
    /// 工单状态中文显示
    /// </summary>
    public string StatusDisplay => EnumHelper.GetDisplayName(Status);

    /// <summary>
    /// 工单状态文本
    /// </summary>
    public string StatusText { get; set; } = null!;

    /// <summary>
    /// 物料名称
    /// </summary>
    public string MaterialName { get; set; } = null!;

    /// <summary>
    /// 标准牌号（从订单项次获取，同一工单下一致）
    /// </summary>
    public string StandardGrade { get; set; } = null!;

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 规格
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
    /// 交货状态
    /// </summary>
    public DeliveryState DeliveryState { get; set; }

    /// <summary>
    /// 交货状态中文显示
    /// </summary>
    public string DeliveryStateDisplay => EnumHelper.GetDisplayName(DeliveryState);

    /// <summary>
    /// 长度状态
    /// </summary>
    public LengthStatus LengthStatus { get; set; }

    /// <summary>
    /// 长度状态中文显示
    /// </summary>
    public string LengthStatusDisplay => EnumHelper.GetDisplayName(LengthStatus);

    /// <summary>
    /// 交货日期
    /// </summary>
    public DateTime DeliveryDate { get; set; }

    /// <summary>
    /// 总数量（支数）
    /// </summary>
    public int TotalQuantity { get; set; }

    /// <summary>
    /// 总重量
    /// </summary>
    public decimal TotalWeight { get; set; }

    /// <summary>
    /// 该工单包含的原始订单项次列表
    /// </summary>
    public List<OrderItemBriefDto> OrderItems { get; set; } = new();
}

/// <summary>
/// 订单项次简要 DTO（用于追溯页面）
/// </summary>
public class OrderItemBriefDto
{
    /// <summary>
    /// 项次号
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// 标准牌号
    /// </summary>
    public string StandardGrade { get; set; } = null!;

    /// <summary>
    /// 规格
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 长度状态
    /// </summary>
    public LengthStatus LengthStatus { get; set; }

    /// <summary>
    /// 长度状态中文显示
    /// </summary>
    public string LengthStatusDisplay => EnumHelper.GetDisplayName(LengthStatus);

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
}