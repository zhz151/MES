using MES.Core.DTOs.Shared;
namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 工单需求调整打印请求
/// </summary>
public class OrderDemandAdjustmentPrintRequest
{
    /// <summary>标题</summary>
    public string Title { get; set; } = "工单需求调整";

    /// <summary>打印数据行（字典格式，枚举字段已解析为中文显示文本）</summary>
    public List<Dictionary<string, object>> Items { get; set; } = new();

    /// <summary>打印列定义</summary>
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 工单需求调整全部打印请求
/// </summary>
public class DemandAdjustmentPrintAllRequest
{
    /// <summary>模糊搜索关键字</summary>
    public string? Keyword { get; set; }

    /// <summary>排序列名</summary>
    public string? SortBy { get; set; }

    /// <summary>是否降序</summary>
    public bool IsDescending { get; set; }

    /// <summary>订单日期从</summary>
    public DateTime? SignDateFrom { get; set; }

    /// <summary>订单日期至</summary>
    public DateTime? SignDateTo { get; set; }

    /// <summary>打印列定义</summary>
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 保存工单需求调整请求（原定义在 Controller 中，迁移至 DTO 层）
/// </summary>
public class SaveUrgingRequest
{
    public int WorkOrderId { get; set; }
    public bool IsUrging { get; set; }
    public bool IsBatchDelivery { get; set; }
    public bool IsPaused { get; set; }
    public bool IsForceCompleted { get; set; }
    public string? AdjustmentRemark { get; set; }
}
