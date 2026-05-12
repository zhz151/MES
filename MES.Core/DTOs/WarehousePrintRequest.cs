namespace MES.Core.DTOs;

/// <summary>
/// 打印列定义（Key=属性名, Label=显示名）
/// </summary>
public class PrintColumnDef
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
}

/// <summary>
/// 仓库库存/入库历史 打印全部请求
/// </summary>
public class InventoryPrintAllRequest
{
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; }
    public int WarehouseId { get; set; }
    public bool OnlyWithStock { get; set; } = true;
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 仓库库存/入库历史 打印选中请求
/// </summary>
public class InventoryPrintSelectedRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 出库历史 打印全部请求
/// </summary>
public class OutboundPrintAllRequest
{
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; }
    public int? WarehouseId { get; set; }
    public List<PrintColumnDef> Columns { get; set; } = new();
}

/// <summary>
/// 出库历史 打印选中请求
/// </summary>
public class OutboundPrintSelectedRequest
{
    public long[] Ids { get; set; } = Array.Empty<long>();
    public List<PrintColumnDef> Columns { get; set; } = new();
}
