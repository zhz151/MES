namespace MES.Core.DTOs;

/// <summary>
/// 用料计划批量打印请求
/// </summary>
public class MaterialPlanBatchPrintRequest
{
    /// <summary>
    /// 工单ID列表
    /// </summary>
    public int[] WorkOrderIds { get; set; } = Array.Empty<int>();

    /// <summary>
    /// 是否包含原料采购计划
    /// </summary>
    public bool IncludeSemi { get; set; }

    /// <summary>
    /// 是否包含成品采购计划
    /// </summary>
    public bool IncludeFinish { get; set; }

    /// <summary>
    /// 是否包含库存使用计划
    /// </summary>
    public bool IncludeInventory { get; set; }

    /// <summary>
    /// 是否包含库料改制计划
    /// </summary>
    public bool IncludeRework { get; set; }

    /// <summary>
    /// 是否包含圆棒穿孔计划
    /// </summary>
    public bool IncludeRoundBarPiercing { get; set; }
}
