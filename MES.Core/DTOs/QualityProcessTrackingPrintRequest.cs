namespace MES.Core.DTOs;

/// <summary>
/// 成检追踪批量打印请求
/// </summary>
public class QualityProcessTrackingPrintBatchRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 成检追踪全部打印请求
/// </summary>
public class QualityProcessTrackingPrintAllRequest
{
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
}
