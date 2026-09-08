using MES.Core.Models;

using MES.Core.DTOs.Shared;
namespace MES.Core.DTOs.Quality;

public class PittingCorrosionTestPrintBatchRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}

