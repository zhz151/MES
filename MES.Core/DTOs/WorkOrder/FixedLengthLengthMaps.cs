namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 定尺工单长度映射（一次查询构建，供批量匹配计算与回填复用）
/// </summary>
public class FixedLengthLengthMaps
{
    /// <summary>按工单号（Order+主号+次号）→ 定尺长度集合</summary>
    public Dictionary<string, HashSet<decimal>> ByWorkOrderNo { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>按「订单号|主号」（NormalizeMainKey 归一）→ 定尺长度集合</summary>
    public Dictionary<string, HashSet<decimal>> ByMainKey { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
