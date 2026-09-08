// 文件路径: MES.Core/DTOs/CustomerProfileDto.cs
using MES.Core.Enums;
using MES.Core.Helpers;
using System.Text.Json.Serialization;

namespace MES.Core.DTOs.Order;

/// <summary>
/// 客户档案 DTO
/// </summary>
public class CustomerProfileDto
{
    /// <summary>
    /// 客户ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 客户编码
    /// </summary>
    public string CustomerCode { get; set; } = string.Empty;

    /// <summary>
    /// 业务员
    /// </summary>
    public string Salesman { get; set; } = string.Empty;

    /// <summary>
    /// 客户单位
    /// </summary>
    public string CustomerUnit { get; set; } = string.Empty;

    /// <summary>
    /// 最终用户
    /// </summary>
    public string EndCustomer { get; set; } = string.Empty;

    /// <summary>
    /// 联系人
    /// </summary>
    public string? ContactPerson { get; set; }

    /// <summary>
    /// 联系电话
    /// </summary>
    public string? ContactPhone { get; set; }

    /// <summary>
    /// 联系地址
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// 客户状态
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CustomerStatus Status { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    // ========== 客户业务统计（客户管理列表 8 列，服务层按「业务员+最终用户」聚合注入；仅 GetPagedAsync 填充，其余入口为默认值） ==========

    /// <summary>累计接单 单数（合同重量&gt;0 的有效订单）</summary>
    public int TotalOrderCount { get; set; }

    /// <summary>累计接单 合同重量(kg)</summary>
    public decimal TotalOrderWeight { get; set; }

    /// <summary>累计接单 合同金额(元)</summary>
    public decimal TotalOrderAmount { get; set; }

    /// <summary>本年接单 单数</summary>
    public int YearOrderCount { get; set; }

    /// <summary>本年接单 合同重量(kg)</summary>
    public decimal YearOrderWeight { get; set; }

    /// <summary>本年接单 合同金额(元)</summary>
    public decimal YearOrderAmount { get; set; }

    /// <summary>本年已发货(整单/主号完成) 单数（落入该桶的订单数，可与其他桶重叠）</summary>
    public int ShippedCompletedCount { get; set; }

    /// <summary>本年已发货(整单/主号完成) 重量(kg)</summary>
    public decimal ShippedCompletedWeight { get; set; }

    /// <summary>本年已发货(整单/主号完成) 金额(元，结算分治：过磅按实际出库公斤、理算/过磅-负封顶合同)</summary>
    public decimal ShippedCompletedAmount { get; set; }

    /// <summary>本年已发货(非整单) 单数</summary>
    public int ShippedOtherCount { get; set; }

    /// <summary>本年已发货(非整单) 重量(kg)</summary>
    public decimal ShippedOtherWeight { get; set; }

    /// <summary>本年已发货(非整单) 金额(元)</summary>
    public decimal ShippedOtherAmount { get; set; }

    /// <summary>待发货(整单/主号完成，成品库存) 单数</summary>
    public int StockCompletedCount { get; set; }

    /// <summary>待发货(整单/主号完成，成品库存) 重量(kg)</summary>
    public decimal StockCompletedWeight { get; set; }

    /// <summary>待发货(整单/主号完成) 金额(元)</summary>
    public decimal StockCompletedAmount { get; set; }

    /// <summary>待发货(非整单) 单数</summary>
    public int StockOtherCount { get; set; }

    /// <summary>待发货(非整单) 重量(kg)</summary>
    public decimal StockOtherWeight { get; set; }

    /// <summary>待发货(非整单) 金额(元)</summary>
    public decimal StockOtherAmount { get; set; }

    /// <summary>待在产(整单未入库，成品入库量=0) 单数</summary>
    public int WipNoneCount { get; set; }

    /// <summary>待在产(整单未入库，成品入库量=0) 重量(kg=整单合同重量)</summary>
    public decimal WipNoneWeight { get; set; }

    /// <summary>待在产(整单未入库) 金额(元=整单合同金额)</summary>
    public decimal WipNoneAmount { get; set; }

    /// <summary>待在产(扣除部分入库，0&lt;入库&lt;合同) 单数</summary>
    public int WipPartialCount { get; set; }

    /// <summary>待在产(扣除部分入库，0&lt;入库&lt;合同) 重量(kg=合同−入库)</summary>
    public decimal WipPartialWeight { get; set; }

    /// <summary>待在产(扣除部分入库) 金额(元=未入库余量价值)</summary>
    public decimal WipPartialAmount { get; set; }
}