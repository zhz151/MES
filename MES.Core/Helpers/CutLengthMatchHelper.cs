using MES.Core.Enums;

namespace MES.Core.Helpers;

/// <summary>
/// 定尺切割长度匹配辅助类：纯判定 + 中文显示统一出口
/// </summary>
public static class CutLengthMatchHelper
{
    /// <summary>
    /// 定尺切割长度匹配纯判定。
    /// 完全匹配：长度 ∈ 本工单号定尺长度集合；主号匹配：长度仅 ∈ 订单+主号定尺长度集合；
    /// 其余（长度空/≤0/两者皆不中/集合为空）→ null（不适用）。
    /// 适用性（成品切割+定尺+非预成切）由调用方先行把关。
    /// </summary>
    public static CutLengthMatchType? Match(HashSet<decimal>? workOrderLengths, HashSet<decimal>? mainNoLengths, decimal? length)
    {
        if (!length.HasValue || length <= 0) return null;
        if (workOrderLengths?.Contains(length.Value) == true) return CutLengthMatchType.FullMatch;
        if (mainNoLengths?.Contains(length.Value) == true) return CutLengthMatchType.MainNoMatch;
        return null;
    }

    /// <summary>定尺切割长度匹配标识 → 中文；null（不适用）→ 空串</summary>
    public static string GetText(CutLengthMatchType? match) => match switch
    {
        CutLengthMatchType.FullMatch => "完全匹配",
        CutLengthMatchType.MainNoMatch => "主号匹配",
        _ => string.Empty
    };
}
