namespace MES.Core.Helpers;

/// <summary>
/// 生产计件类别维度档（子表）纯函数规则（2026-09-02 重构引入，§4）。
/// 区间维闭区间含值；区间两两「边界点相切视为合法邻接」不拦截（与旧单表模型 RangesOverlap 口径一致，
/// 保证翻译搬移旧档行后匹配行为不漂移）；等值维取值重复拦截（OrdinalIgnoreCase）。
/// </summary>
public static class PieceRateDimensionRules
{
    /// <summary>区间严格相交判定：仅拦截真正跨段重叠；边界点相切视为合法邻接（半开衔接）。</summary>
    public static bool RangesOverlap(decimal? aMin, decimal? aMax, decimal? bMin, decimal? bMax)
    {
        if (aMax.HasValue && bMin.HasValue && aMax.Value <= bMin.Value) return false;
        if (bMax.HasValue && aMin.HasValue && bMax.Value <= aMin.Value) return false;
        return true;
    }

    /// <summary>整型区间相交判定（定尺维；边界整数相切即共享整数，视为重叠拦截——两档不可同时含同一整数）。</summary>
    public static bool RangesOverlapInt(int? aMin, int? aMax, int? bMin, int? bMax)
    {
        if (aMax.HasValue && bMin.HasValue && aMax.Value < bMin.Value) return false;
        if (bMax.HasValue && aMin.HasValue && bMax.Value < aMin.Value) return false;
        return true;
    }

    /// <summary>数值是否落入闭区间 [min, max]；缺失边界视为开侧。</summary>
    public static bool IsInRange(decimal? min, decimal? max, decimal value)
    {
        if (min.HasValue && value < min.Value) return false;
        if (max.HasValue && value > max.Value) return false;
        return true;
    }

    /// <summary>整数是否落入闭区间 [min, max]（定尺维）。</summary>
    public static bool IsInRange(int? min, int? max, int value)
    {
        if (min.HasValue && value < min.Value) return false;
        if (max.HasValue && value > max.Value) return false;
        return true;
    }

    /// <summary>区间宽度（用于多个档同时命中时的确定性取窄）：空任一侧 → 正无穷，否则 max-min。</summary>
    public static decimal SpanWidth(decimal? min, decimal? max)
    {
        if (!min.HasValue || !max.HasValue) return decimal.MaxValue;
        return max.Value - min.Value;
    }

    /// <summary>返回序列中 OrdinalIgnoreCase 重复的第一个取值；空值/空白跳过；无重复返回 null。</summary>
    public static string? FirstDuplicateOrdinalIgnoreCase(IEnumerable<string?> values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (!seen.Add(value)) return value;
        }
        return null;
    }
}
