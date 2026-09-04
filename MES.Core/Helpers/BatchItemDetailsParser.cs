using System.Globalization;
using System.Text.RegularExpressions;

namespace MES.Core.Helpers;

/// <summary>
/// 生产批次 ItemDetails（"项次,长度mm,支数;" 文本快照）定尺长度解析（2026-09-04 引入）。
/// 生产计件「定尺（FixedLengthCount）」维结算数据源 = 批次 ItemDetails 去重定尺长度种数（计划口径，批级固有属性，
/// 不随切割行变）；「长度（Length）」维非切行三级取数第 2 级（批次 Fixed 定尺长）取 ItemDetails 最长一种定尺长
/// （多定尺批取最长，2026-09-04 拍板）。文本来源为工单/批次生成时聚合写入（WorkOrderService.CalculateAggregates），
/// 历史存量可能含「项」后缀（"5项,14154mm,30支;"）或无（"5,14154mm,30支;"），长度一律以 "mm" 为锚点容忍解析。
/// </summary>
public static class BatchItemDetailsParser
{
    /// <summary>匹配 "14154mm" / "14154.5mm"（长度以 mm 为锚；段首项次、段尾支数不参与）</summary>
    private static readonly Regex LengthMmPattern = new(
        @"(\d+(?:\.\d+)?)\s*mm", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// 去重定尺长度种数：解析 ItemDetails 中各 "mm" 前数字并去重计数。
    /// 空/空白/无任何 mm 命中 → null（调用方不填维 → 引擎跳过该维系数 1）。
    /// </summary>
    public static int? CountDistinctLengthsMm(string? itemDetails)
    {
        if (string.IsNullOrWhiteSpace(itemDetails)) return null;

        var lengths = new HashSet<decimal>();
        foreach (Match m in LengthMmPattern.Matches(itemDetails))
        {
            if (decimal.TryParse(m.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
                lengths.Add(v);
        }
        return lengths.Count == 0 ? null : lengths.Count;
    }

    /// <summary>
    /// 最长定尺长(mm)：解析 ItemDetails 中各 "mm" 前数字并取最大者（多定尺批取最长一种，2026-09-04 拍板）。
    /// 空/空白/无任何 mm 命中 → null（调用方按 6000 兜底或跳过该维）。
    /// </summary>
    public static decimal? MaxLengthMm(string? itemDetails)
    {
        if (string.IsNullOrWhiteSpace(itemDetails)) return null;

        decimal? max = null;
        foreach (Match m in LengthMmPattern.Matches(itemDetails))
        {
            if (decimal.TryParse(m.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
                max = !max.HasValue || v > max.Value ? v : max;
        }
        return max;
    }
}
