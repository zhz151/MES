using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Core.Interfaces.Payroll;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Payroll;
using MES.Data.Entities.Quality;
using MES.Services.Helpers;

namespace MES.Services.Payroll;

/// <summary>
/// 成检计件类别服务（2026-09-03 引入）——工资结算上下文，对标生产计件类别（PieceRateProductionCategoryService）。
/// 类别 = 成检项目(InspectionItem 单选) + 基准价 + 结算单位；无工序/产类/作业阶段约束（无 Key 子表）。
/// 维度系数在子表档行（无例外价）。结算单价 = 类别.BasePrice × 命中档 Ratio 连乘；
/// 某维配档但记录值不落任何档 → 该维系数 1（仅无命中启用类别才返回 null=未定价）。
/// 同一成检项目同时仅一条启用类别（Save 显式校验 + DB 过滤唯一索引 UK_FinalInspectionCategory_Item_Active 兜底）。
/// 维度 Key 域 = PieceRateInspectionDimensionKeys（Length 量纲 mm，全长度状态参与：Fixed=实际定尺长、
/// Range/NonFixed 取数缺省按 6000 折算；InspectionCount 为整数支数档）。
/// </summary>
public class PieceRateFinalInspectionCategoryService : IPieceRateFinalInspectionCategoryService
{
    private readonly AppDbContext _context;

    public PieceRateFinalInspectionCategoryService(AppDbContext context)
    {
        _context = context;
    }

    // ==================== 分页查询 ====================

    public async Task<PagedResult<PieceRateFinalInspectionCategoryListItemDto>> GetPagedAsync(
        PieceRateFinalInspectionCategoryQueryParams query)
    {
        var queryable = _context.PieceRateFinalInspectionCategories.AsNoTracking()
            .Include(c => c.Tiers)
            .AsQueryable();

        // SQL 可下推的精确筛选
        if (!string.IsNullOrEmpty(query.ItemKey))
            queryable = queryable.Where(c => c.ItemKey == query.ItemKey);
        if (!string.IsNullOrEmpty(query.Unit))
            queryable = queryable.Where(c => c.Unit == query.Unit);
        if (query.IsActive.HasValue)
            queryable = queryable.Where(c => c.IsActive == query.IsActive.Value);

        // 列级筛选（布尔/数值/日期列反射可下推）
        queryable = queryable.ApplyFilters(query.Filters);

        var entities = await queryable.ToListAsync();

        // 先映射全量（模糊搜索依赖派生列 ItemKeyChinese）
        var items = entities.Select(MapListItem).ToList();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            items = items
                .Where(i => ContainsText(i.ItemKeyChinese, kw)
                            || ContainsText(i.ItemKey, kw)
                            || ContainsText(i.Remark, kw))
                .ToList();
        }

        // 全字段排序（默认 CreatedTime 降序）
        SortItems(items, query.SortBy, query.IsDescending);

        var totalCount = items.Count;
        var pageItems = items.Skip(query.Skip).Take(query.PageSize).ToList();

        return new PagedResult<PieceRateFinalInspectionCategoryListItemDto>
        {
            Items = pageItems,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    private static bool ContainsText(string? field, string keyword)
        => !string.IsNullOrEmpty(field) &&
           field.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    // ==================== 详情 ====================

    public async Task<PieceRateFinalInspectionCategoryDetailDto?> GetDetailAsync(int id)
    {
        var entity = await _context.PieceRateFinalInspectionCategories.AsNoTracking()
            .Include(c => c.Tiers)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null) return null;

        var dto = MapListItem(entity);
        var detail = new PieceRateFinalInspectionCategoryDetailDto
        {
            Id = dto.Id,
            ItemKey = dto.ItemKey,
            ItemKeyChinese = dto.ItemKeyChinese,
            BasePrice = dto.BasePrice,
            Unit = dto.Unit,
            UnitChinese = dto.UnitChinese,
            IsActive = dto.IsActive,
            TierCount = dto.TierCount,
            Remark = dto.Remark,
            UpdatedTime = dto.UpdatedTime,
            CreatedTime = dto.CreatedTime
        };
        detail.Tiers = entity.Tiers
            .OrderBy(t => DimensionIndex(t.DimensionKey))
            .ThenBy(t => t.Id)
            .Select(ToTierDto)
            .ToList();
        return detail;
    }

    private static PieceRateFinalInspectionCategoryTierDto ToTierDto(PieceRateFinalInspectionCategoryTier t)
    {
        var dto = new PieceRateFinalInspectionCategoryTierDto
        {
            Id = t.Id,
            DimensionKey = t.DimensionKey,
            DimensionKeyChinese = PieceRateInspectionDimensionKeys.ToChinese(t.DimensionKey),
            RangeText = t.RangeText,
            MinValue = t.MinValue,
            MaxValue = t.MaxValue,
            MinInt = t.MinInt,
            MaxInt = t.MaxInt,
            MatchValue = t.MatchValue,
            Ratio = t.Ratio,
            IsActive = t.IsActive
        };
        // 等值维优先展示取值文本；区间维展示原文
        if (dto.RangeText == null && dto.MatchValue != null)
            dto.RangeText = dto.MatchValue;
        return dto;
    }

    // ==================== 下拉选项 ====================

    public async Task<PieceRateFinalInspectionCategoryOptionsDto> GetOptionsAsync()
    {
        var items = EnumHelper.GetDisplayOptions<InspectionItem>()
            .Select(o => new PieceRateCategoryOptionItemDto { Key = o.Value, Name = o.DisplayName })
            .ToList();

        var lengthStatuses = EnumHelper.GetDisplayOptions<LengthStatus>()
            .Select(o => new PieceRateCategoryOptionItemDto { Key = o.Value, Name = o.DisplayName })
            .ToList();

        var units = PieceRateUnitKeys.All
            .Select(k => new PieceRateCategoryOptionItemDto { Key = k, Name = PieceRateUnitKeys.ToChinese(k) ?? k })
            .ToList();

        var states = PieceRateStateKeys.All
            .Select(k => new PieceRateCategoryOptionItemDto { Key = k, Name = PieceRateStateKeys.ToChinese(k) ?? k })
            .ToList();

        var grades = await _context.StandardGradeMappings.AsNoTracking()
            .Select(x => x.PlantGrade)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return new PieceRateFinalInspectionCategoryOptionsDto
        {
            Items = items,
            Units = units,
            LengthStatuses = lengthStatuses,
            States = states,
            Grades = grades
        };
    }

    // ==================== 保存 ====================

    public async Task<PieceRateFinalInspectionCategoryDetailDto> SaveAsync(
        int? id, PieceRateFinalInspectionCategorySaveRequest request)
    {
        var itemKey = NormalizeItemKey(request.ItemKey);
        if (itemKey == null)
            throw new BusinessException($"无效的成检项目: {request.ItemKey}");

        if (request.BasePrice <= 0)
            throw new BusinessException("基准价必须大于0");
        var unit = request.Unit?.Trim();
        if (string.IsNullOrEmpty(unit) || !PieceRateUnitKeys.IsKey(unit))
            throw new BusinessException($"无效的结算单位: {request.Unit}");

        if (id.HasValue && id.Value <= 0) id = null;
        PieceRateFinalInspectionCategory entity;
        if (id.HasValue)
        {
            entity = await _context.PieceRateFinalInspectionCategories
                .Include(c => c.Tiers)
                .FirstOrDefaultAsync(c => c.Id == id.Value)
                ?? throw new BusinessException($"类别不存在: Id={id}");
        }
        else
        {
            entity = new PieceRateFinalInspectionCategory { Tiers = new List<PieceRateFinalInspectionCategoryTier>() };
            _context.PieceRateFinalInspectionCategories.Add(entity);
        }

        entity.ItemKey = itemKey;
        entity.BasePrice = request.BasePrice;
        entity.Unit = unit;
        entity.IsActive = request.IsActive;
        entity.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();

        // 档行整组替换
        var newTiers = BuildTiers(request.Tiers);
        ValidateTierOverlapAndDuplicates(newTiers);
        foreach (var old in entity.Tiers.ToList())
            _context.PieceRateFinalInspectionCategoryTiers.Remove(old);
        entity.Tiers.Clear();
        entity.Tiers.AddRange(newTiers);

        // 同成检项目启用唯一（过滤唯一索引兜底；停用时跳过）
        if (entity.IsActive)
            await EnsureNoDuplicateActiveItemAsync(entity, id);

        await _context.SaveChangesAsync();
        return await GetDetailAsync(entity.Id)
            ?? throw new BusinessException("保存后读取失败");
    }

    /// <summary>同成检项目不得存在其它启用类别（DB 过滤唯一索引 UK_FinalInspectionCategory_Item_Active 同规则）</summary>
    private async Task EnsureNoDuplicateActiveItemAsync(PieceRateFinalInspectionCategory entity, int? selfId)
    {
        var exists = await _context.PieceRateFinalInspectionCategories.AsNoTracking()
            .AnyAsync(c => c.ItemKey == entity.ItemKey
                           && c.IsActive
                           && (!selfId.HasValue || c.Id != selfId.Value));
        if (exists)
            throw new BusinessException(
                $"成检项目「{EnumHelper.GetDisplayName<InspectionItem>(entity.ItemKey)}」已存在启用类别（同项目仅一条启用，请改为编辑既有类别）");
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.PieceRateFinalInspectionCategories
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new BusinessException($"类别不存在: Id={id}");
        _context.PieceRateFinalInspectionCategories.Remove(entity); // 级联删档行
        await _context.SaveChangesAsync();
    }

    // ==================== 建档 helper ====================

    /// <summary>归一化成检项目：中文或英文（枚举名，大小写不敏感）→ InspectionItem 枚举名；非法返回 null。</summary>
    private static string? NormalizeItemKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return EnumHelper.TryParse<InspectionItem>(raw.Trim())?.ToString();
    }

    /// <summary>
    /// 建档行实体（解析区间边界、等值维取 MatchValue）。等值维取值可空 = 无约束档被过滤；
    /// 空 RangeText 区间维抛出 BusinessException。
    /// </summary>
    private static List<PieceRateFinalInspectionCategoryTier> BuildTiers(
        IReadOnlyList<PieceRateFinalInspectionCategoryTierSaveRequest>? rows)
    {
        var list = new List<PieceRateFinalInspectionCategoryTier>();
        if (rows == null) return list;

        foreach (var row in rows)
        {
            if (!PieceRateInspectionDimensionKeys.IsKey(row.DimensionKey))
                throw new BusinessException($"无效的维度: {row.DimensionKey}");
            if (row.Ratio <= 0)
                throw new BusinessException($"{PieceRateInspectionDimensionKeys.ToChinese(row.DimensionKey)}档加价系数必须大于0");

            var tier = new PieceRateFinalInspectionCategoryTier
            {
                DimensionKey = row.DimensionKey,
                Ratio = row.Ratio,
                IsActive = row.IsActive
            };

            if (PieceRateInspectionDimensionKeys.IsValueDimension(row.DimensionKey))
            {
                tier.MatchValue = string.IsNullOrWhiteSpace(row.MatchValue) ? null : row.MatchValue.Trim();
                tier.RangeText = tier.MatchValue;
                list.Add(tier);
                continue;
            }

            // 区间维：区间原文解析边界
            if (string.IsNullOrWhiteSpace(row.RangeText) ||
                !PieceRateRangeParser.TryParseRange(row.RangeText, out var min, out var max))
                throw new BusinessException(
                    $"{PieceRateInspectionDimensionKeys.ToChinese(row.DimensionKey)}档必须填写可解析的区间");

            tier.RangeText = row.RangeText!.Trim();
            tier.MatchValue = null;
            if (row.DimensionKey == PieceRateInspectionDimensionKeys.InspectionCount)
            {
                tier.MinInt = ToIntBound(min, PieceRateInspectionDimensionKeys.ToChinese(row.DimensionKey));
                tier.MaxInt = ToIntBound(max, PieceRateInspectionDimensionKeys.ToChinese(row.DimensionKey));
            }
            else
            {
                tier.MinValue = min;
                tier.MaxValue = max;
            }
            list.Add(tier);
        }
        return list;
    }

    private static int? ToIntBound(decimal? value, string? dimChinese)
    {
        if (!value.HasValue) return null;
        if (value.Value != Math.Floor(value.Value) || value.Value < int.MinValue || value.Value > int.MaxValue)
            throw new BusinessException($"{dimChinese ?? "检验支数"}档必须为整数区间");
        return (int)value.Value;
    }

    /// <summary>对已建档行实体校验：区间维两两重叠（相切=合法邻接；检验支数维整数共享即重叠）+ 等值维取值去重（OrdinalIgnoreCase）。仅校验启用行。</summary>
    private static void ValidateTierOverlapAndDuplicates(IReadOnlyList<PieceRateFinalInspectionCategoryTier> tiers)
    {
        var active = tiers.Where(t => t.IsActive).ToList();
        foreach (var dimGroup in active.GroupBy(t => t.DimensionKey))
        {
            var dimRows = dimGroup.ToList();

            if (PieceRateInspectionDimensionKeys.IsValueDimension(dimGroup.Key))
            {
                var dup = PieceRateDimensionRules.FirstDuplicateOrdinalIgnoreCase(
                    dimRows.Select(t => t.MatchValue));
                if (dup != null)
                    throw new BusinessException(
                        $"{PieceRateInspectionDimensionKeys.ToChinese(dimGroup.Key)}档取值重复: {dup}");
                continue;
            }

            for (var i = 0; i < dimRows.Count; i++)
            {
                for (var j = i + 1; j < dimRows.Count; j++)
                {
                    var a = dimRows[i];
                    var b = dimRows[j];
                    var overlap = dimGroup.Key == PieceRateInspectionDimensionKeys.InspectionCount
                        ? PieceRateDimensionRules.RangesOverlapInt(a.MinInt, a.MaxInt, b.MinInt, b.MaxInt)
                        : PieceRateDimensionRules.RangesOverlap(a.MinValue, a.MaxValue, b.MinValue, b.MaxValue);
                    if (overlap)
                        throw new BusinessException(
                            $"{PieceRateInspectionDimensionKeys.ToChinese(dimGroup.Key)}档区间重叠: 「{a.RangeText}」与「{b.RangeText}」");
                }
            }
        }
    }

    // ==================== 试算匹配 ====================

    public async Task<PieceRateFinalInspectionMatchResultDto?> MatchPriceAsync(PieceRateFinalInspectionMatchRequest request)
        => await MatchByRequestAsync(request);

    /// <summary>核心匹配计价（手动 match-price 与按成检记录试算共用，2026-09-04 拆出）</summary>
    private async Task<PieceRateFinalInspectionMatchResultDto?> MatchByRequestAsync(PieceRateFinalInspectionMatchRequest request)
    {
        var itemKey = NormalizeItemKey(request.ItemKey);
        if (itemKey == null) return null;

        // 长度兜底（与结算采集器/试算前端同口径，2026-09-04 共享单源）：Range/NonFixed 未填长度按 6000 折算，
        // 使 Length 档匹配与金额折算一致；Fixed/空状态不兜底（长度留空则元/千米金额无法折算）
        if (!request.Length.HasValue)
            request.Length = PieceRateAmountHelper.DefaultTrialLengthMm(request.LengthStatus);

        // SQL 仅下推成检项目 + 启用；同项目启用唯一（过滤唯一索引兜底）
        var candidates = await _context.PieceRateFinalInspectionCategories.AsNoTracking()
            .Include(c => c.Tiers)
            .Where(c => c.ItemKey == itemKey && c.IsActive)
            .ToListAsync();

        if (candidates.Count > 1)
            throw new BusinessException(
                $"数据违例：成检项目「{EnumHelper.GetDisplayName<InspectionItem>(itemKey)}」命中多个启用类别（同项目启用唯一被破坏）");
        var matched = candidates.FirstOrDefault();
        if (matched == null) return null; // 未定价

        // 命中档扫描：区间维落入（多档重叠取区间最窄），等值维等值命中
        var hits = new List<PieceRateProductionMatchTierHitDto>();
        decimal totalRatio = 1;
        foreach (var tierGroup in matched.Tiers
                     .Where(t => t.IsActive)
                     .GroupBy(t => t.DimensionKey))
        {
            var dimKey = tierGroup.Key;
            var hitTier = SelectHitTier(tierGroup.ToList(), dimKey, request);
            if (hitTier == null) continue;
            hits.Add(new PieceRateProductionMatchTierHitDto
            {
                DimensionKey = dimKey,
                DimensionKeyChinese = PieceRateInspectionDimensionKeys.ToChinese(dimKey),
                RangeText = PieceRateInspectionDimensionKeys.IsValueDimension(dimKey)
                    ? hitTier.MatchValue
                    : hitTier.RangeText,
                Ratio = hitTier.Ratio
            });
            totalRatio *= hitTier.Ratio;
        }

        var unitPrice = matched.BasePrice * totalRatio;
        var simulatedAmount = PieceRateAmountHelper.AmountForUnit(
            matched.Unit, unitPrice, request.WeightKg, request.InspectionCount, request.Length);

        return new PieceRateFinalInspectionMatchResultDto
        {
            CategoryId = matched.Id,
            ItemKey = matched.ItemKey,
            ItemKeyChinese = EnumHelper.GetDisplayName<InspectionItem>(matched.ItemKey),
            BasePrice = matched.BasePrice,
            TotalRatio = totalRatio,
            UnitPrice = unitPrice,
            Unit = matched.Unit,
            UnitChinese = PieceRateUnitKeys.ToChinese(matched.Unit) ?? matched.Unit,
            SimulatedAmount = simulatedAmount,
            Hits = hits,
            Remark = matched.Remark
        };
    }

    // ==================== 模拟测算（按成检记录点选，2026-09-04） ====================

    /// <summary>候选成检记录（全局任意记录）：成检项目/关键字过滤 SQL 下推 + 分页；默认检验日期降序。
    /// 关键字覆盖记录本地列（生产编号/设备/操作人），规格列仅展示（避免导航列下推在 InMemory 下不可靠）。</summary>
    public async Task<PagedResult<FinalInspectionPriceTrialRecordDto>> GetTrialRecordsAsync(
        FinalInspectionPriceTrialRecordQuery query)
    {
        var q = _context.FinalInspections.AsNoTracking()
            .Include(f => f.ProductionBatch)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.ItemKey)
            && Enum.TryParse<InspectionItem>(query.ItemKey.Trim(), ignoreCase: true, out var itemKey))
        {
            q = q.Where(f => f.InspectionItem == itemKey);
        }

        var kw = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim();
        if (kw != null)
        {
            q = q.Where(f => f.BatchNo.Contains(kw)
                             || (f.EquipmentName != null && f.EquipmentName.Contains(kw))
                             || (f.Operator != null && f.Operator.Contains(kw)));
        }

        var totalCount = await q.CountAsync();

        var rows = await q
            .OrderByDescending(f => f.InspectionDate)
            .ThenByDescending(f => f.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(f => new
            {
                f.Id,
                f.InspectionDate,
                f.InspectionItem,
                f.BatchNo,
                f.Quantity,
                f.Weight,
                f.EquipmentName,
                f.Operator,
                f.FixedLength,
                Specification = f.ProductionBatch.Specification,
                LengthStatus = f.ProductionBatch.LengthStatus
            })
            .ToListAsync();

        var items = rows.Select(x => new FinalInspectionPriceTrialRecordDto
        {
            Id = x.Id,
            InspectionDate = x.InspectionDate,
            ItemKey = x.InspectionItem.ToString(),
            ItemKeyChinese = EnumHelper.GetDisplayName(x.InspectionItem),
            BatchNo = x.BatchNo,
            Specification = x.Specification,
            LengthStatusKey = x.LengthStatus,
            LengthStatusChinese = ToLengthStatusChinese(x.LengthStatus),
            FixedLength = x.FixedLength,
            Quantity = x.Quantity,
            Weight = x.Weight,
            EquipmentName = x.EquipmentName,
            Operator = x.Operator
        }).ToList();

        return new PagedResult<FinalInspectionPriceTrialRecordDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    private static string? ToLengthStatusChinese(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return EnumHelper.TryParse<LengthStatus>(key) is { } v ? EnumHelper.GetDisplayName(v) : key;
    }

    /// <summary>模拟测算：按一条真实成检记录计价（与月结采集同 FinalInspectionMatchRequestMapper 单源映射）。
    /// 记录不存在抛 BusinessException；命中不到启用类别返回 null（=未定价）。</summary>
    public async Task<PieceRateFinalInspectionMatchResultDto?> MatchFinalInspectionRecordAsync(int recordId)
    {
        var inspection = await _context.FinalInspections.AsNoTracking()
            .Include(f => f.ProductionBatch)
            .FirstOrDefaultAsync(f => f.Id == recordId)
            ?? throw new BusinessException($"成检记录不存在: {recordId}");
        var request = FinalInspectionMatchRequestMapper.BuildRequest(inspection, inspection.ProductionBatch);
        return await MatchByRequestAsync(request);
    }

    private static PieceRateFinalInspectionCategoryTier? SelectHitTier(
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
            var intHits = activeTiers
                .Where(t => PieceRateDimensionRules.IsInRange(t.MinInt, t.MaxInt, requestInt.Value))
                .ToList();
            return PickNarrowest(intHits, requestInt.Value, isInt: true);
        }

        if (!requestValue.HasValue) return null;
        var intervalHits = activeTiers
            .Where(t => PieceRateDimensionRules.IsInRange(t.MinValue, t.MaxValue, requestValue.Value))
            .ToList();
        return PickNarrowest(intervalHits, requestValue.Value, isInt: false);
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

    private static string? NormalizeLengthStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return EnumHelper.TryParse<LengthStatus>(raw.Trim())?.ToString();
    }

    private static string? NormalizeOrNull(string? key, string? original)
        => string.IsNullOrWhiteSpace(original) ? null : (key ?? original.Trim());

    // ==================== 映射 ====================

    private static PieceRateFinalInspectionCategoryListItemDto MapListItem(PieceRateFinalInspectionCategory entity)
        => new()
        {
            Id = entity.Id,
            ItemKey = entity.ItemKey,
            ItemKeyChinese = EnumHelper.GetDisplayName<InspectionItem>(entity.ItemKey),
            BasePrice = entity.BasePrice,
            Unit = entity.Unit,
            UnitChinese = PieceRateUnitKeys.ToChinese(entity.Unit) ?? entity.Unit,
            IsActive = entity.IsActive,
            TierCount = entity.Tiers.Count(t => t.IsActive),
            Remark = entity.Remark,
            UpdatedTime = entity.UpdatedTime,
            CreatedTime = entity.CreatedTime
        };

    private static int DimensionIndex(string dimKey)
    {
        var idx = Array.IndexOf(PieceRateInspectionDimensionKeys.All, dimKey);
        return idx < 0 ? int.MaxValue : idx;
    }

    private static void SortItems(
        List<PieceRateFinalInspectionCategoryListItemDto> items,
        string sortBy, bool isDescending)
    {
        var key = (sortBy ?? "CreatedTime").ToLowerInvariant();
        IOrderedEnumerable<PieceRateFinalInspectionCategoryListItemDto>? ordered = null;
        switch (key)
        {
            case "itemkey": ordered = Order(items, i => i.ItemKey, isDescending); break;
            case "itemkeychinese": ordered = Order(items, i => i.ItemKeyChinese, isDescending); break;
            case "baseprice": ordered = Order(items, i => i.BasePrice, isDescending); break;
            case "unit": ordered = Order(items, i => i.Unit, isDescending); break;
            case "isactive": ordered = Order(items, i => i.IsActive, isDescending); break;
            case "tiercount": ordered = Order(items, i => i.TierCount, isDescending); break;
            case "remark": ordered = Order(items, i => i.Remark, isDescending); break;
            case "updatedtime": ordered = Order(items, i => i.UpdatedTime, isDescending); break;
            default: ordered = Order(items, i => i.CreatedTime, isDescending); break;
        }

        var sorted = ordered.ToList();
        items.Clear();
        items.AddRange(sorted);
    }

    private static IOrderedEnumerable<PieceRateFinalInspectionCategoryListItemDto> Order<TKey>(
        IEnumerable<PieceRateFinalInspectionCategoryListItemDto> source,
        Func<PieceRateFinalInspectionCategoryListItemDto, TKey> keySelector,
        bool isDescending)
        => isDescending
            ? source.OrderByDescending(keySelector)
            : source.OrderBy(keySelector);
}
