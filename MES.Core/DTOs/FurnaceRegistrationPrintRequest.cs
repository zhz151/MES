using MES.Core.Models;

namespace MES.Core.DTOs;

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
}
