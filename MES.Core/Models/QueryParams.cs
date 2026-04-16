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
    [Range(1, 100, ErrorMessage = "每页条数必须在1-100之间")]
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
    /// 计算跳过的记录数
    /// </summary>
    public int Skip => (PageIndex - 1) * PageSize;
}