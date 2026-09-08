using MES.Core.DTOs.Shared;

namespace MES.Core.DTOs.Configuration;

public class EmployeePrintBatchRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}
