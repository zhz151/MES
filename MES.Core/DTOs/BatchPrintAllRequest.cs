namespace MES.Core.DTOs;

/// <summary>
/// 打印全部批次请求
/// </summary>
public class BatchPrintAllRequest
{
    public string? Keyword { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? Status { get; set; }
    public string? TagNo { get; set; }
    public string? BatchNo { get; set; }
    public string? SalesOrderNo { get; set; }
    public string? ProductionMainNo { get; set; }
    public string? ProductionSubNo { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 打印选中批次请求
/// </summary>
public class BatchPrintSelectedRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}
