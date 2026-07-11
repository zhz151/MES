// 文件路径: MES.Core/DTOs/SaveAllOrderResponse.cs

namespace MES.Core.DTOs.Order;

/// <summary>
/// 订单批量保存响应
/// </summary>
public class SaveAllOrderResponse
{
    /// <summary>更新后的 RowVersion（前端需保存以进行后续编辑）</summary>
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>
    /// 新创建项次的索引→ID 映射
    /// Key = NewItems 列表中的索引（0-based），Value = 数据库自增 ID
    /// 前端用此映射将 ViewModel 中的临时 Id 替换为真实 Id
    /// </summary>
    public Dictionary<int, int> NewItemIdMap { get; set; } = new();

    /// <summary>所有保存后的项次摘要（含 Sequence 分配结果）</summary>
    public List<OrderItemSaveResult> Items { get; set; } = new();
}

/// <summary>
/// 单个项次的保存结果
/// </summary>
public class OrderItemSaveResult
{
    public int Id { get; set; }
    public int Sequence { get; set; }
    public decimal Meters { get; set; }
    public decimal TheoreticalWeight { get; set; }
}
