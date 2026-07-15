// 文件路径: MES.Core/DTOs/SaveAllOrderRequest.cs
using MES.Core.Enums;

namespace MES.Core.DTOs.Order;

/// <summary>
/// 订单批量保存请求——在一次请求中完成头更新 + 全部项次增删改
/// </summary>
public class SaveAllOrderRequest
{
    /// <summary>订单号（为 null 表示不修改）</summary>
    public string? OrderNumber { get; set; }

    /// <summary>签订日期（为 null 表示不修改）</summary>
    public DateTime? SignDate { get; set; }

    /// <summary>客户ID（为 null 表示不修改）</summary>
    public int? CustomerId { get; set; }

    /// <summary>客户名称（手动输入，为 null 表示不修改）</summary>
    public string? CustomerName { get; set; }

    /// <summary>业务员（手动输入，为 null 表示不修改）</summary>
    public string? Salesman { get; set; }

    /// <summary>最终用户（手动输入，为 null 表示不修改）</summary>
    public string? EndCustomer { get; set; }

    /// <summary>乐观并发控制版本号</summary>
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>新建的项次（Id=0，Sequence 由服务端分配）</summary>
    public List<OrderItemSaveRequest> NewItems { get; set; } = new();

    /// <summary>更新的项次（通过 Id 匹配 DB 记录）</summary>
    public List<OrderItemSaveRequest> UpdatedItems { get; set; } = new();

    /// <summary>要删除的项次 ID 列表</summary>
    public List<int> DeletedItemIds { get; set; } = new();
}

/// <summary>
/// 订单项次保存请求（同时用于新增和更新，通过 Id 区分：0=新增，>0=更新）
/// </summary>
public class OrderItemSaveRequest
{
    /// <summary>项次 ID（新增时为 0）</summary>
    public int Id { get; set; }

    /// <summary>项次号</summary>
    public int Sequence { get; set; }

    /// <summary>交货日期</summary>
    public DateTime DeliveryDate { get; set; }

    /// <summary>是否延期罚款</summary>
    public bool DelayPenalty { get; set; }

    /// <summary>结算方式</summary>
    public SettlementMethod SettlementMethod { get; set; }

    /// <summary>钢管制造类别</summary>
    public PipeManufacturingType PipeManufacturingType { get; set; }

    /// <summary>标准号</summary>
    public string StandardNo { get; set; } = null!;

    /// <summary>交货状态</summary>
    public DeliveryState DeliveryState { get; set; }

    /// <summary>标准牌号</summary>
    public string StandardGrade { get; set; } = null!;

    /// <summary>外径</summary>
    public decimal OuterDiameter { get; set; }

    /// <summary>壁厚</summary>
    public decimal WallThickness { get; set; }

    /// <summary>外径下偏差</summary>
    public decimal OuterDiameterNegative { get; set; }

    /// <summary>外径上偏差</summary>
    public decimal OuterDiameterPositive { get; set; }

    /// <summary>壁厚下偏差</summary>
    public decimal WallThicknessNegative { get; set; }

    /// <summary>壁厚上偏差</summary>
    public decimal WallThicknessPositive { get; set; }

    /// <summary>长度状态</summary>
    public LengthStatus LengthStatus { get; set; }

    /// <summary>最小长度（mm）</summary>
    public decimal? MinLength { get; set; }

    /// <summary>最大长度（mm）</summary>
    public decimal? MaxLength { get; set; }

    /// <summary>数量（支数）</summary>
    public int? Quantity { get; set; }

    /// <summary>米数</summary>
    public decimal? Meters { get; set; }

    /// <summary>合同重量</summary>
    public decimal ContractWeight { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}
