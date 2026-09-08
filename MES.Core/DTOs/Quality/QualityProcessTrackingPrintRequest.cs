using MES.Core.DTOs.Shared;
namespace MES.Core.DTOs.Quality;

/// <summary>
/// 成检追踪批量打印请求
/// </summary>
public class QualityProcessTrackingPrintBatchRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}

