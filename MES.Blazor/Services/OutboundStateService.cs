using MES.Core.DTOs;

namespace MES.Blazor.Services;

/// <summary>
/// 在库存页面（选择批次）和出库页面（确认出库）之间共享状态
/// </summary>
public class OutboundStateService
{
    /// <summary>
    /// 选中的库存批次列表
    /// </summary>
    public List<InventoryBatchDto> SelectedItems { get; set; } = new();

    /// <summary>
    /// 来源仓库编码
    /// </summary>
    public string WarehouseCode { get; set; } = string.Empty;

    /// <summary>
    /// 来源仓库名称
    /// </summary>
    public string WarehouseName { get; set; } = string.Empty;

    /// <summary>
    /// 来源仓库ID
    /// </summary>
    public int WarehouseId { get; set; }

    public void Clear()
    {
        SelectedItems.Clear();
        WarehouseCode = string.Empty;
        WarehouseName = string.Empty;
        WarehouseId = 0;
    }
}
