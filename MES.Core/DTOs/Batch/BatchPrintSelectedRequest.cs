using MES.Core.DTOs.Shared;
namespace MES.Core.DTOs.Batch;

/// <summary>
/// 打印选中批次请求
/// </summary>
public class BatchPrintSelectedRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}
