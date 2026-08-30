using MES.Core.DTOs.Shared;

namespace MES.Core.DTOs.Warehouse;

/// <summary>
/// 仓库库存/入库历史 打印选中请求
/// </summary>
public class InventoryPrintSelectedRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
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
