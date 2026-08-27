namespace MES.Core.DTOs.Order;

/// <summary>
/// 订单交期预估小表 → 订单列表点击联动筛选参数
/// 前端点击小表某桶单元格时，将桶的结构化日期边界原样回传，后端按传入范围筛选，
/// 与 GetDeliveryEstimateAsync 生成桶的边界天然一致（避免跨天/配置变更错位）。
/// </summary>
public class OrderDeliveryEstimateFilterDto
{
    /// <summary>表标识（complete=订单(整单)完成预估 / delay=风险-已延期订单(整单)，对应 OrderDeliveryEstimateTableDto.Id）</summary>
    public string Table { get; set; } = "";

    /// <summary>桶日期范围起始（含；null=无下界）</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>桶日期范围结束（含；null=无上界）</summary>
    public DateTime? DateTo { get; set; }
}
