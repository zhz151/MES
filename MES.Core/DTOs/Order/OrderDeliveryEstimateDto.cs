namespace MES.Core.DTOs.Order;

/// <summary>
/// 订单交期预估（业务总况两小表数据源，2026-08-23 用户决策）
/// 单数按订单号（订单级）、重量按订单总重量（合同重量 kg）统计；
/// 延期判定（订单级）：预计完成日 &gt; 交期截止（DeliveryEnd）。
/// </summary>
public class OrderDeliveryEstimateDto
{
    /// <summary>两张小表（表1 订单完成预估 / 表2 延期交货订单预估）</summary>
    public List<OrderDeliveryEstimateTableDto> Tables { get; set; } = new();

    /// <summary>生成时间</summary>
    public DateTime GeneratedTime { get; set; }
}

/// <summary>
/// 单张小表：名称 + 7 个日期桶（绝对日期样式：≤今日 / 区间 / ≥尾，桶边界走 DateBucket 配置）
/// </summary>
public class OrderDeliveryEstimateTableDto
{
    /// <summary>表名（订单(整单)完成预估 / 风险-已延期订单(整单)）</summary>
    public string Name { get; set; } = null!;

    /// <summary>7 个桶标签（绝对日期区间，两表共用同一组）</summary>
    public List<string> BucketLabels { get; set; } = new();

    /// <summary>每桶统计（单数 + 吨数）</summary>
    public List<OrderDeliveryBucketDto> Buckets { get; set; } = new();
}

/// <summary>
/// 单桶统计值
/// </summary>
public class OrderDeliveryBucketDto
{
    /// <summary>单数（订单号去重数）</summary>
    public int Count { get; set; }

    /// <summary>重量（吨，kg 已换算）</summary>
    public decimal Weight { get; set; }

    /// <summary>「急中急」单数（桶内延期罚款=是的订单子集，订单级 HasDelayPenalty；仅表2 统计）</summary>
    public int UrgentCount { get; set; }

    /// <summary>「急中急」重量（吨，kg 已换算；仅表2 统计）</summary>
    public decimal UrgentWeight { get; set; }
}
