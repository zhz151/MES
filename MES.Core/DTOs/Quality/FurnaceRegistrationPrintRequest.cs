using MES.Core.Models;

using MES.Core.DTOs.Shared;
namespace MES.Core.DTOs.Quality;

public class FurnaceRegistrationPrintBatchRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}

public class FurnaceRegistrationPrintAllRequest
{
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
    public DateTime? IncomingDateFrom { get; set; }
    public DateTime? IncomingDateTo { get; set; }
}
