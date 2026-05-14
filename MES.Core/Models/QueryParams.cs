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
    [Range(1, 10000, ErrorMessage = "每页条数必须在1-10000之间")]
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
    /// 计算跳过的记录数
    /// </summary>
    public int Skip => (PageIndex - 1) * PageSize;
}