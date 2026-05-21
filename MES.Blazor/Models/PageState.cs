using System.Text.Json.Serialization;
using MES.Core.Models;

namespace MES.Blazor.Models;

/// <summary>
/// 列表页状态（排序、筛选、分页等），用于 localStorage 持久化
/// </summary>
public class PageState
{
    /// <summary>排序列名</summary>
    [JsonPropertyName("s")]
    public string? SortBy { get; set; }

    /// <summary>是否降序</summary>
    [JsonPropertyName("d")]
    public bool IsDescending { get; set; }

    /// <summary>关键字搜索</summary>
    [JsonPropertyName("k")]
    public string? Keyword { get; set; }

    /// <summary>当前页码（0-based）</summary>
    [JsonPropertyName("p")]
    public int PageIndex { get; set; }

    /// <summary>列筛选条件</summary>
    [JsonPropertyName("f")]
    public List<FilterDescriptor>? Filters { get; set; }

    /// <summary>页面自定义扩展字段（各页面特有的下拉框状态等）</summary>
    [JsonPropertyName("e")]
    public Dictionary<string, string>? Extras { get; set; }
}
