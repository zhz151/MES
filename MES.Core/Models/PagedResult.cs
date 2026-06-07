namespace MES.Core.Models;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new List<T>();

    public int TotalCount { get; set; }

    public int PageIndex { get; set; }

    public int PageSize { get; set; }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;

    /// <summary>
    /// 扩展数据字典，用于返回分页数据之外的附加信息（如 Tab 汇总等）
    /// </summary>
    public Dictionary<string, object>? Extras { get; set; }
}