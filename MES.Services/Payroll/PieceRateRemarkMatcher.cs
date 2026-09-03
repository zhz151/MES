using MES.Data.Entities.Payroll;

namespace MES.Services.Payroll;

/// <summary>
/// 冷拔类型维（PieceRateDimensionKeys.ColdDrawType）备注关键词命中助手（2026-09-04 引入）。
/// 该维是值维家族中唯一「等值相等 → 备注包含」语义的维：档行 MatchValue 存自由文本关键词，
/// 报工备注 Remark 包含某关键词即命中该档乘系数；未命中 = 系数 1（不返回）。
/// 同一备注命中多个关键词时取【最长关键词】（更具体）定则；同长取先配行（Id 小者）。
/// 命中口径为单一来源：匹配引擎（PieceRateMatchEngine）与服务端（PieceRateProductionCategoryService）均调用本助手，
/// 防双通道漂移；一致性由单元测试兜底。
/// </summary>
internal static class PieceRateRemarkMatcher
{
    /// <summary>
    /// 在启用档行中按备注关键词包含命中。remark 空/空白 → null（=系数 1）。
    /// </summary>
    public static PieceRateProductionCategoryTier? MatchKeyword(
        IEnumerable<PieceRateProductionCategoryTier> activeTiers, string? remark)
    {
        if (string.IsNullOrWhiteSpace(remark)) return null;
        var text = remark.Trim();

        PieceRateProductionCategoryTier? best = null;
        var bestLength = -1;
        foreach (var tier in activeTiers)
        {
            var keyword = tier.MatchValue?.Trim();
            if (string.IsNullOrEmpty(keyword)) continue;
            if (!text.Contains(keyword, StringComparison.OrdinalIgnoreCase)) continue;

            // 最长关键词优先；同长取先配行（Id 小者）
            if (keyword.Length > bestLength
                || (keyword.Length == bestLength && best != null && tier.Id < best.Id))
            {
                best = tier;
                bestLength = keyword.Length;
            }
        }
        return best;
    }
}
