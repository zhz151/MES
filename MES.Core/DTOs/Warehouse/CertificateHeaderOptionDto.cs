namespace MES.Core.DTOs.Warehouse;

/// <summary>
/// 质保书头选择项 — 待发货数据中 DISTINCT (订单号+客户名称+产品标准+交货状态)
/// </summary>
public class CertificateHeaderOptionDto
{
    /// <summary>订单号</summary>
    public string OrderNo { get; set; } = null!;

    /// <summary>客户名称</summary>
    public string? CustomerName { get; set; }

    /// <summary>产品标准</summary>
    public string? ProductStandard { get; set; }

    /// <summary>交货状态</summary>
    public string? DeliveryStatus { get; set; }
}
