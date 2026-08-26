using System.ComponentModel.DataAnnotations;

namespace MES.Core.Models;

/// <summary>
/// 通用分页查询参数
/// </summary>
public class QueryParams
{
    /// <summary>
    /// 页码（从1开始）
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "页码必须大于0")]
    public int PageIndex { get; set; } = 1;

    /// <summary>
    /// 每页条数
    /// </summary>
    [Range(1, 100000, ErrorMessage = "每页条数必须在1-100000之间")]
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// 搜索关键字
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// 排序字段
    /// </summary>
    public string SortBy { get; set; } = "CreatedTime";

    /// <summary>
    /// 是否降序
    /// </summary>
    public bool IsDescending { get; set; } = true;

    /// <summary>
    /// 每列独立筛选条件列表
    /// </summary>
    public List<FilterDescriptor>? Filters { get; set; }

    /// <summary>
    /// 到料日期范围筛选-开始（仅检验到料使用）
    /// </summary>
    public DateTime? ReceiveDateFrom { get; set; }

    /// <summary>
    /// 到料日期范围筛选-结束（仅检验到料使用）
    /// </summary>
    public DateTime? ReceiveDateTo { get; set; }

    /// <summary>
    /// 执行日期范围筛选-开始（仅生产记录使用）
    /// </summary>
    public DateTime? ExecDateFrom { get; set; }

    /// <summary>
    /// 执行日期范围筛选-结束（仅生产记录使用）
    /// </summary>
    public DateTime? ExecDateTo { get; set; }

    /// <summary>
    /// 检验日期范围筛选-开始（仅过程检验使用）
    /// </summary>
    public DateTime? InspectionDateFrom { get; set; }

    /// <summary>
    /// 检验日期范围筛选-结束（仅过程检验使用）
    /// </summary>
    public DateTime? InspectionDateTo { get; set; }

    /// <summary>
    /// 发出日期范围筛选-开始（仅工段委外使用）
    /// </summary>
    public DateTime? SendOutDateFrom { get; set; }

    /// <summary>
    /// 发出日期范围筛选-结束（仅工段委外使用）
    /// </summary>
    public DateTime? SendOutDateTo { get; set; }

    /// <summary>
    /// 实际回收日期范围筛选-开始（仅工段委外使用）
    /// </summary>
    public DateTime? ActualRecoveryDateFrom { get; set; }

    /// <summary>
    /// 实际回收日期范围筛选-结束（仅工段委外使用）
    /// </summary>
    public DateTime? ActualRecoveryDateTo { get; set; }

    /// <summary>
    /// 回收日期范围筛选-开始（仅委外回收使用）
    /// </summary>
    public DateTime? RecoveryDateFrom { get; set; }

    /// <summary>
    /// 回收日期范围筛选-结束（仅委外回收使用）
    /// </summary>
    public DateTime? RecoveryDateTo { get; set; }

    /// <summary>
    /// 入缸日期范围筛选-开始（仅去油酸洗使用）
    /// </summary>
    public DateTime? InDateFrom { get; set; }

    /// <summary>
    /// 入缸日期范围筛选-结束（仅去油酸洗使用）
    /// </summary>
    public DateTime? InDateTo { get; set; }

    /// <summary>
    /// 完工日期范围筛选-开始（仅去油酸洗使用）
    /// </summary>
    public DateTime? CompleteDateFrom { get; set; }

    /// <summary>
    /// 完工日期范围筛选-结束（仅去油酸洗使用）
    /// </summary>
    public DateTime? CompleteDateTo { get; set; }

    /// <summary>
    /// 来料日期范围筛选-开始（仅炉号登记使用）
    /// </summary>
    public DateTime? IncomingDateFrom { get; set; }

    /// <summary>
    /// 来料日期范围筛选-结束（仅炉号登记使用）
    /// </summary>
    public DateTime? IncomingDateTo { get; set; }

    /// <summary>
    /// 反馈日期范围筛选-开始（仅NCR使用）
    /// </summary>
    public DateTime? ReportDateFrom { get; set; }

    /// <summary>
    /// 反馈日期范围筛选-结束（仅NCR使用）
    /// </summary>
    public DateTime? ReportDateTo { get; set; }

    /// <summary>
    /// 入库日期范围筛选-开始（仅订单成品(实时库存)使用，原「待发货项」）
    /// </summary>
    public DateTime? InboundDateFrom { get; set; }

    /// <summary>
    /// 入库日期范围筛选-结束（仅订单成品(实时库存)使用，原「待发货项」）
    /// </summary>
    public DateTime? InboundDateTo { get; set; }

    /// <summary>
    /// 计算跳过的记录数
    /// </summary>
    public int Skip => (PageIndex - 1) * PageSize;
}