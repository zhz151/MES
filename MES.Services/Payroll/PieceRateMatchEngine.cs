using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Data.Entities.Payroll;

namespace MES.Services.Payroll;

/// <summary>
/// 计件单价纯函数匹配引擎（2026-09-03 每日工资核算引入）。
/// 把 生产计件类别 / 成检计件类别 两条「候选类别命中 → 维档命中 Ratio 连乘 → 单价」在内存实现为纯静态函数，
/// 供每日工资引擎对当月产量源逐行定价（类别全集一次预取后逐行匹配，避免逐行 DB 查询）。
/// 匹配口径与 <see cref="PieceRateProductionCategoryService.MatchPriceAsync"/> /
/// <see cref="PieceRateFinalInspectionCategoryService.MatchPriceAsync"/> 完全一致（同一批入参应产出相同 UnitPrice，
/// 一致性由单元测试兜底）；不重构既有 Service。
/// 生产值维 ColdDrawType（冷拔类型）= MatchValue 存备注关键词，Remark.Contains 命中（PieceRateRemarkMatcher 单源）。
/// 调用方须传入「启用类别全集」（生产含 Tiers+ConstraintKeys、成检含 Tiers）。
/// </summary>
public static class PieceRateMatchEngine
{
    /// <summary>生产行命中结果（单价 = BasePrice × TotalRatio）</summary>
    public sealed class ProductionHit
    {
        public int CategoryId { get; init; }
        public string SectionKey { get; init; } = string.Empty;
        public decimal BasePrice { get; init; }
        public decimal TotalRatio { get; init; }
        public decimal UnitPrice { get; init; }
        public string Unit { get; init; } = string.Empty;
        public string? Remark { get; init; }
    }

    /// <summary>成检行命中结果（单价 = BasePrice × TotalRatio）</summary>
    public sealed class FinalInspectionHit
    {
        public int CategoryId { get; init; }
        public string ItemKey { get; init; } = string.Empty;
        public decimal BasePrice { get; init; }
        public decimal TotalRatio { get; init; }
        public decimal UnitPrice { get; init; }
        public string Unit { get; init; } = string.Empty;
        public string? Remark { get; init; }
    }

    // ==================== 生产计件匹配 ====================

    /// <summary>
    /// 生产计件单价匹配：工段+工序+产类+作业阶段 四键命中（类别已按 SectionKey 与启用过滤由调用方或本方法保证）→ 维档命中。
    /// 返回 null = 未定价（命中不到启用类别）；命中 &gt;1 抛 BusinessException（禁交集被破坏）。
    /// </summary>
    public static ProductionHit? MatchProduction(
        IReadOnlyCollection<PieceRateProductionCategory> categories,
        PieceRateProductionMatchRequest request)
    {
        var sectionKey = SectionKeys.ToKey(request.SectionName) ?? request.SectionName;
        if (string.IsNullOrEmpty(sectionKey)) return null;

        // 归一化请求键值（兼容中文存量输入；记录侧本身为英文 Key）
        var processName = NormalizeOrNull(ProcessKeys.ToKey(request.ProcessName), request.ProcessName);
        var productStatus = NormalizeOrNull(ProductStatuses.ToKey(request.ProductStatus), request.ProductStatus);
        var stage = NormalizeOrNull(PieceRateStageKeys.ToKey(request.Stage), request.Stage);
        var specialState = NormalizeOrNull(PieceRateStateKeys.ToKey(request.SpecialState), request.SpecialState);

        PieceRateProductionCategory? matched = null;
        foreach (var c in categories)
        {
            if (!c.IsActive || !string.Equals(c.SectionKey, sectionKey, StringComparison.OrdinalIgnoreCase))
                continue;
            var procs = ConstraintKeysOf(c, PieceRateConstraintTypes.Process);
            var prods = ConstraintKeysOf(c, PieceRateConstraintTypes.ProductStatus);
            var stages = ConstraintKeysOf(c, PieceRateConstraintTypes.Stage);
            if (KeysContain(procs, processName)
                && KeysContain(prods, productStatus)
                && KeysContain(stages, stage))
            {
                if (matched != null)
                    throw new BusinessException(
                        $"数据违例：工段「{SectionKeys.ToChinese(sectionKey)}」命中多个启用类别（禁交集被破坏）: "
                        + $"{matched.SectionKey} 与 {c.SectionKey}，请检查重复类别");
                matched = c;
            }
        }

        if (matched == null) return null; // 未定价

        decimal totalRatio = 1;
        foreach (var tierGroup in matched.Tiers
                     .Where(t => t.IsActive)
                     .GroupBy(t => t.DimensionKey))
        {
            var hitTier = SelectProductionHitTier(tierGroup.ToList(), tierGroup.Key, request);
            if (hitTier == null) continue;
            totalRatio *= hitTier.Ratio;
        }

        return new ProductionHit
        {
            CategoryId = matched.Id,
            SectionKey = matched.SectionKey,
            BasePrice = matched.BasePrice,
            TotalRatio = totalRatio,
            UnitPrice = matched.BasePrice * totalRatio,
            Unit = matched.Unit,
            Remark = matched.Remark
        };
    }

    // ==================== 成检计件匹配 ====================

    /// <summary>
    /// 成检计件单价匹配：按成检项目单选（调用方传入该项目启用类别；同项目启用唯一，&gt;1 抛数据违例）→ 8 维档命中。
    /// 返回 null = 未定价；注意 Length 缺省（Range/NonFixed 6000 折算）须由调用方在 request.Length 上先兜底。
    /// </summary>
    public static FinalInspectionHit? MatchFinalInspection(
        IReadOnlyCollection<PieceRateFinalInspectionCategory> categories,
        PieceRateFinalInspectionMatchRequest request)
    {
        var itemKey = NormalizeItemKey(request.ItemKey);
        if (itemKey == null) return null;

        var active = categories
            .Where(c => c.IsActive && string.Equals(c.ItemKey, itemKey, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (active.Count > 1)
            throw new BusinessException(
                $"数据违例：成检项目「{EnumHelper.GetDisplayName<InspectionItem>(itemKey)}」命中多个启用类别（同项目启用唯一被破坏）");
        var matched = active.FirstOrDefault();
        if (matched == null) return null; // 未定价

        decimal totalRatio = 1;
        foreach (var tierGroup in matched.Tiers
                     .Where(t => t.IsActive)
                     .GroupBy(t => t.DimensionKey))
        {
            var hitTier = SelectFinalInspectionHitTier(tierGroup.ToList(), tierGroup.Key, request);
            if (hitTier == null) continue;
            totalRatio *= hitTier.Ratio;
        }

        return new FinalInspectionHit
        {
            CategoryId = matched.Id,
            ItemKey = matched.ItemKey,
            BasePrice = matched.BasePrice,
            TotalRatio = totalRatio,
            UnitPrice = matched.BasePrice * totalRatio,
            Unit = matched.Unit,
            Remark = matched.Remark
        };
    }

    // ==================== 维档命中（生产，复刻 PieceRateProductionCategoryService） ====================

    private static PieceRateProductionCategoryTier? SelectProductionHitTier(
        List<PieceRateProductionCategoryTier> activeTiers, string dimKey,
        PieceRateProductionMatchRequest request)
    {
        if (PieceRateDimensionKeys.IsValueDimension(dimKey))
        {
            // 冷拔类型 = 备注关键词包含命中（非等值）：Remark 含 MatchValue 关键词即命中，最长词优先
            if (dimKey == PieceRateDimensionKeys.ColdDrawType)
                return PieceRateRemarkMatcher.MatchKeyword(activeTiers, request.Remark);

            var value = dimKey switch
            {
                PieceRateDimensionKeys.SpecialGrade => request.PlantGrade,
                PieceRateDimensionKeys.SpecialState => NormalizeOrNull(
                    PieceRateStateKeys.ToKey(request.SpecialState), request.SpecialState),
                PieceRateDimensionKeys.SpecialDevice => request.EquipmentName,
                _ => null
            };
            if (string.IsNullOrWhiteSpace(value)) return null;
            return activeTiers
                .Where(t => string.Equals(t.MatchValue, value, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Id)
                .FirstOrDefault();
        }

        // 区间维
        decimal? requestValue = null;
        int? requestInt = null;
        switch (dimKey)
        {
            case PieceRateDimensionKeys.OuterDiameter: requestValue = request.OuterDiameter; break;
            case PieceRateDimensionKeys.WallThickness: requestValue = request.WallThickness; break;
            case PieceRateDimensionKeys.Length: requestValue = request.Length; break;
            case PieceRateDimensionKeys.CutRate: requestValue = request.CutRate; break;
            case PieceRateDimensionKeys.FixedLengthCount: requestInt = request.FixedLengthCount; break;
        }

        if (dimKey == PieceRateDimensionKeys.FixedLengthCount)
        {
            if (!requestInt.HasValue) return null;
            var hits = activeTiers
                .Where(t => PieceRateDimensionRules.IsInRange(t.MinInt, t.MaxInt, requestInt.Value))
                .ToList();
            return PickNarrowest(hits, requestInt.Value, isInt: true);
        }

        if (!requestValue.HasValue) return null;
        var intervalHits = activeTiers
            .Where(t => PieceRateDimensionRules.IsInRange(t.MinValue, t.MaxValue, requestValue.Value))
            .ToList();
        return PickNarrowest(intervalHits, requestValue.Value, isInt: false);
    }

    // ==================== 维档命中（成检，复刻 PieceRateFinalInspectionCategoryService） ====================

    private static PieceRateFinalInspectionCategoryTier? SelectFinalInspectionHitTier(
        List<PieceRateFinalInspectionCategoryTier> activeTiers, string dimKey,
        PieceRateFinalInspectionMatchRequest request)
    {
        if (PieceRateInspectionDimensionKeys.IsValueDimension(dimKey))
        {
            var value = dimKey switch
            {
                PieceRateInspectionDimensionKeys.LengthStatus => NormalizeLengthStatus(request.LengthStatus),
                PieceRateInspectionDimensionKeys.SpecialGrade => request.PlantGrade,
                PieceRateInspectionDimensionKeys.SpecialState => NormalizeOrNull(
                    PieceRateStateKeys.ToKey(request.SpecialState), request.SpecialState),
                PieceRateInspectionDimensionKeys.SpecialDevice => request.EquipmentName,
                _ => null
            };
            if (string.IsNullOrWhiteSpace(value)) return null;
            return activeTiers
                .Where(t => string.Equals(t.MatchValue, value, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Id)
                .FirstOrDefault();
        }

        // 区间维
        decimal? requestValue = null;
        int? requestInt = null;
        switch (dimKey)
        {
            case PieceRateInspectionDimensionKeys.OuterDiameter: requestValue = request.OuterDiameter; break;
            case PieceRateInspectionDimensionKeys.WallThickness: requestValue = request.WallThickness; break;
            case PieceRateInspectionDimensionKeys.Length: requestValue = request.Length; break;
            case PieceRateInspectionDimensionKeys.InspectionCount: requestInt = request.InspectionCount; break;
        }

        if (dimKey == PieceRateInspectionDimensionKeys.InspectionCount)
        {
            if (!requestInt.HasValue) return null;
            var hits = activeTiers
                .Where(t => PieceRateDimensionRules.IsInRange(t.MinInt, t.MaxInt, requestInt.Value))
                .ToList();
            return PickNarrowest(hits, requestInt.Value, isInt: true);
        }

        if (!requestValue.HasValue) return null;
        var intervalHits = activeTiers
            .Where(t => PieceRateDimensionRules.IsInRange(t.MinValue, t.MaxValue, requestValue.Value))
            .ToList();
        return PickNarrowest(intervalHits, requestValue.Value, isInt: false);
    }

    // ==================== 维档命中取窄（复刻两 Service PickNarrowest） ====================

    private static PieceRateProductionCategoryTier? PickNarrowest(
        List<PieceRateProductionCategoryTier> hits, decimal requestValue, bool isInt)
    {
        if (hits.Count == 0) return null;
        return hits
            .OrderBy(t => isInt
                ? PieceRateDimensionRules.SpanWidth(t.MinInt, t.MaxInt)
                : PieceRateDimensionRules.SpanWidth(t.MinValue, t.MaxValue))
            .ThenBy(t => t.Id)
            .First();
    }

    private static PieceRateFinalInspectionCategoryTier? PickNarrowest(
        List<PieceRateFinalInspectionCategoryTier> hits, decimal requestValue, bool isInt)
    {
        if (hits.Count == 0) return null;
        return hits
            .OrderBy(t => isInt
                ? PieceRateDimensionRules.SpanWidth(t.MinInt, t.MaxInt)
                : PieceRateDimensionRules.SpanWidth(t.MinValue, t.MaxValue))
            .ThenBy(t => t.Id)
            .First();
    }

    // ==================== 约束集合（复刻 PieceRateProductionCategoryService 私有 helper） ====================

    private static string[] ConstraintKeysOf(PieceRateProductionCategory entity, string type)
        => entity.ConstraintKeys
            .Where(k => string.Equals(k.ConstraintType, type, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(k.Key))
            .Select(k => k.Key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool KeysContain(IReadOnlyCollection<string> keys, string? value)
    {
        if (keys.Count == 0) return true;
        return value != null && keys.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    private static string? NormalizeLengthStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return EnumHelper.TryParse<LengthStatus>(raw.Trim())?.ToString();
    }

    private static string? NormalizeItemKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return EnumHelper.TryParse<InspectionItem>(raw.Trim())?.ToString();
    }

    private static string? NormalizeOrNull(string? key, string? original)
        => string.IsNullOrWhiteSpace(original) ? null : (key ?? original.Trim());
}
