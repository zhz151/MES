using MES.Core.Enums;

namespace MES.Core.DTOs.Warehouse;

/// <summary>
/// 待发货订单成品 DTO — 成品库中可发货的库存项
/// </summary>
public class PendingDeliveryItemDto
{
    // ========== 仓库信息 ==========
    /// <summary>库存批次（仓库批次号）</summary>
    public string InventoryBatchNo { get; set; } = null!;
    /// <summary>物料</summary>
    public MaterialType MaterialType { get; set; }
    /// <summary>来源</summary>
    public InboundSource InboundSource { get; set; }
    /// <summary>来料单位</summary>
    public string SourceName { get; set; } = null!;
    /// <summary>生产批号</summary>
    public string? ProductionBatchNo { get; set; }
    /// <summary>炉号</summary>
    public string? HeatNo { get; set; }
    /// <summary>工厂牌号</summary>
    public string PlantGrade { get; set; } = null!;
    /// <summary>名义规格</summary>
    public string Specification { get; set; } = null!;
    /// <summary>长度状态</summary>
    public string? LengthStatus { get; set; }
    /// <summary>最小长度(mm)</summary>
    public decimal? MinLength { get; set; }
    /// <summary>最大长度(mm)</summary>
    public decimal? MaxLength { get; set; }
    /// <summary>剩余支数</summary>
    public int RemainingQuantity { get; set; }
    /// <summary>剩余重量(kg)</summary>
    public decimal RemainingWeight { get; set; }
    /// <summary>米数</summary>
    public decimal? Meters { get; set; }
    /// <summary>剩余米数（仅成品库使用）</summary>
    public decimal? RemainingMeters { get; set; }
    /// <summary>入库日期</summary>
    public DateTime InboundDate { get; set; }

    // ========== 订单关联信息（从 SalesOrder + OrderItem 关联） ==========
    /// <summary>订单号</summary>
    public string? SalesOrderNo { get; set; }
    /// <summary>项次</summary>
    public string? OrderItemIds { get; set; }
    /// <summary>工单号</summary>
    public string? WorkOrderNo { get; set; }
    /// <summary>客户名称</summary>
    public string? CustomerName { get; set; }
    /// <summary>业务员</summary>
    public string? Salesman { get; set; }
    /// <summary>最终客户</summary>
    public string? EndCustomer { get; set; }
    /// <summary>产品标准</summary>
    public string? ProductStandard { get; set; }
    /// <summary>交货状态</summary>
    public string? DeliveryStatus { get; set; }
    /// <summary>标准牌号</summary>
    public string? StandardGrade { get; set; }
}
