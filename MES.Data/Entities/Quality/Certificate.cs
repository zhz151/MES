using MES.Data.Entities.Quality;

namespace MES.Data.Entities.Quality;

/// <summary>
/// 质量证明书（主表）— 类似"订单+项次"模式的主表
/// </summary>
public class Certificate : BaseEntity
{
    /// <summary>证明书编号 = 订单号 + "-01"/"-02"...</summary>
    public string CertificateNo { get; set; } = null!;

    /// <summary>签发日期</summary>
    public DateTime IssueDate { get; set; }

    /// <summary>客户名称</summary>
    public string? CustomerName { get; set; }

    /// <summary>产品标准</summary>
    public string? ProductStandard { get; set; }

    /// <summary>产品名称</summary>
    public string? ProductName { get; set; }

    /// <summary>交货状态</summary>
    public string? DeliveryStatus { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>子项集合</summary>
    public List<CertificateItem> Items { get; set; } = new();
}
