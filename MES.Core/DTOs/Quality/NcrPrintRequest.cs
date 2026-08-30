using MES.Core.DTOs.Shared;
namespace MES.Core.DTOs.Quality;

/// <summary>
/// 打印选中 NCR 报告请求
/// </summary>
public class NcrPrintSelectedRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}
