namespace MES.Core.DTOs;

/// <summary>
/// 批量打印请求
/// </summary>
public class OrderPrintBatchRequest
{
    /// <summary>
    /// 订单ID列表
    /// </summary>
    public int[] Ids { get; set; } = Array.Empty<int>();
}

/// <summary>
/// 打印全部筛选请求
/// </summary>
public class OrderPrintAllRequest
{
    /// <summary>
    /// 模糊搜索关键字
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// 技术要求状态：Edited / NotEdited
    /// </summary>
    public string? TechnicalStatus { get; set; }

    /// <summary>
    /// 订单状态：Pending / Confirmed 可用逗号分隔多选
    /// </summary>
    public string? OrderStatus { get; set; }

    /// <summary>
    /// 排序列名
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// 是否降序
    /// </summary>
    public bool IsDescending { get; set; }
}
