using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Payroll;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Payroll;
using MES.Services.Helpers;

namespace MES.Services.Payroll;

/// <summary>
/// 生产计件类别服务（2026-09-02 重构引入，替代旧 PieceRateStandardService）——工资结算上下文。
/// 类别 = 工段(必选单选) × 工序/产类/作业阶段(可空多选，空=全选) + 基准价 + 结算单位；
/// 维度系数在子表档行（无例外价）。结算单价 = 类别.BasePrice × 命中档 Ratio 连乘。
/// 保存时对同工段启用类别跑「禁止交集」+ 档内区间重叠/等值去重校验；
/// 匹配 SQL 仅下推 SectionKey== &amp;&amp; IsActive，JSON 键约束 ToList 后内存 OrdinalIgnoreCase 比较。
/// </summary>
public class PieceRateProductionCategoryService : IPieceRateProductionCategoryService
{
    private readonly AppDbContext _context;
    private readonly ISectionNameDisplayService _sectionNameDisplay;
    private readonly IProcessDefinitionService _processDefinitionService;

    public PieceRateProductionCategoryService(
        AppDbContext context,
        ISectionNameDisplayService sectionNameDisplay,
        IProcessDefinitionService processDefinitionService)
    {
        _context = context;
        _sectionNameDisplay = sectionNameDisplay;
        _processDefinitionService = processDefinitionService;
    }

    // ==================== 分页查询 ====================

    public async Task<PagedResult<PieceRateProductionCategoryListItemDto>> GetPagedAsync(
        PieceRateProductionCategoryQueryParams query)
    {
        var queryable = _context.PieceRateProductionCategories.AsNoTracking()
            .Include(c => c.Tiers)
            .AsQueryable();

        // SQL 可下推的精确筛选
        if (!string.IsNullOrEmpty(query.SectionKey))
            queryable = queryable.Where(c => c.SectionKey == query.SectionKey);
        if (!string.IsNullOrEmpty(query.Unit))
            queryable = queryable.Where(c => c.Unit == query.Unit);
        if (query.IsActive.HasValue)
            queryable = queryable.Where(c => c.IsActive == query.IsActive.Value);

        // 列级筛选（布尔/数值/日期列反射可下推；显示派生列忽略）
        queryable = queryable.ApplyFilters(query.Filters);

        var entities = await queryable.ToListAsync();

        var sectionMap = await _sectionNameDisplay.GetSectionNameMapAsync();
        var processMap = await _processDefinitionService.GetProcessNameMapAsync();

        // 先映射全量（排序/模糊搜索需依赖派生列 DisplayName/TierCount）
        var items = entities.Select(e => MapListItem(e, sectionMap, processMap)).ToList();

        // 关键字在内存过滤（自动组合名含全部中文约束名；另含 Remark/英文工段 Key）——分类行数少，量级可忽略
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            items = items
                .Where(i => ContainsText(i.DisplayName, kw)
                            || ContainsText(i.SectionKey, kw)
                            || ContainsText(i.Remark, kw))
                .ToList();
        }

        // 全字段排序（默认 CreatedTime 降序）
        SortItems(items, query.SortBy, query.IsDescending);

        var totalCount = items.Count;
        var pageItems = items.Skip(query.Skip).Take(query.PageSize).ToList();

        return new PagedResult<PieceRateProductionCategoryListItemDto>
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

    public async Task<PieceRateProductionCategoryDetailDto?> GetDetailAsync(int id)
    {
        var entity = await _context.PieceRateProductionCategories.AsNoTracking()
            .Include(c => c.Tiers)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null) return null;

        var sectionMap = await _sectionNameDisplay.GetSectionNameMapAsync();
        var processMap = await _processDefinitionService.GetProcessNameMapAsync();

        var dto = MapListItem(entity, sectionMap, processMap);
        var detail = new PieceRateProductionCategoryDetailDto
        {
            Id = dto.Id,
            SectionKey = dto.SectionKey,
            SectionKeyChinese = dto.SectionKeyChinese,
            ProcessKeys = dto.ProcessKeys,
            ProductStatusKeys = dto.ProductStatusKeys,
            StageKeys = dto.StageKeys,
            DisplayName = dto.DisplayName,
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
            .OrderBy(t => PieceRateDimensionIndex(t.DimensionKey))
            .ThenBy(t => t.Id)
            .Select(t => ToTierDto(t))
            .ToList();
        return detail;
    }

    private static PieceRateProductionCategoryTierDto ToTierDto(PieceRateProductionCategoryTier t)
    {
        var dto = new PieceRateProductionCategoryTierDto
        {
            Id = t.Id,
            DimensionKey = t.DimensionKey,
            DimensionKeyChinese = PieceRateDimensionKeys.ToChinese(t.DimensionKey),
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

    public async Task<PieceRateProductionCategoryOptionsDto> GetOptionsAsync()
    {
        var sectionMap = await _sectionNameDisplay.GetSectionNameMapAsync();
        var processMap = await _processDefinitionService.GetProcessNameMapAsync();

        // 启用工段（StandardWorkDay IsEnabled；同一 SectionKey 多行取通用行）
        var sections = (await _context.StandardWorkDays.AsNoTracking()
                .Where(w => w.IsEnabled && w.SectionKey != null)
                .OrderBy(w => w.DisplayOrder)
                .ToListAsync())
            .GroupBy(w => w.SectionKey!, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(x => x.PlantGradePrefix == null ? 0 : 1).ThenBy(x => x.DisplayOrder).First())
            .Select(w => new PieceRateCategoryOptionItemDto
            {
                Key = w.SectionKey!,
                Name = sectionMap.TryGetValue(w.SectionKey!, out var cn) ? cn : w.SectionKey!
            })
            .ToList();

        // 启用工序（ProcessDefinition IsEnabled）
        var processes = (await _context.ProcessDefinitions.AsNoTracking()
                .Where(w => w.IsEnabled && w.ProcessKey != null)
                .OrderBy(w => w.DisplayOrder)
                .Select(w => w.ProcessKey!)
                .ToListAsync())
            .Select(key => new PieceRateCategoryOptionItemDto
            {
                Key = key,
                Name = processMap.TryGetValue(key, out var cn) ? cn : key
            })
            .ToList();

        var products = ProductStatuses.All
            .Select(k => new PieceRateCategoryOptionItemDto
            {
                Key = k,
                Name = ProductStatuses.ToChinese(k) ?? k
            })
            .ToList();

        var stages = PieceRateStageKeys.All
            .Select(k => new PieceRateCategoryOptionItemDto
            {
                Key = k,
                Name = PieceRateStageKeys.ToChinese(k) ?? k
            })
            .ToList();

        var units = PieceRateUnitKeys.All
            .Select(k => new PieceRateCategoryOptionItemDto
            {
                Key = k,
                Name = PieceRateUnitKeys.ToChinese(k) ?? k
            })
            .ToList();

        var states = PieceRateStateKeys.All
            .Select(k => new PieceRateCategoryOptionItemDto
            {
                Key = k,
                Name = PieceRateStateKeys.ToChinese(k) ?? k
            })
            .ToList();

        var grades = await _context.StandardGradeMappings.AsNoTracking()
            .Select(x => x.PlantGrade)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return new PieceRateProductionCategoryOptionsDto
        {
            Sections = sections,
            Processes = processes,
            ProductStatuses = products,
            Stages = stages,
            Units = units,
            States = states,
            Grades = grades
        };
    }

    // ==================== 保存 ====================

    public async Task<PieceRateProductionCategoryDetailDto> SaveAsync(int? id, PieceRateProductionCategorySaveRequest request)
    {
        var sectionKey = SectionKeys.ToKey(request.SectionKey);
        if (sectionKey == null)
            throw new BusinessException($"无效的工段: {request.SectionKey}");

        if (request.BasePrice <= 0)
            throw new BusinessException("基准价必须大于0");
        var unit = request.Unit?.Trim();
        if (string.IsNullOrEmpty(unit) || !PieceRateUnitKeys.IsKey(unit))
            throw new BusinessException($"无效的结算单位: {request.Unit}");

        // 三键约束集合归一：翻译中文→Key → 校验合法 → 显式全列表归一为 null（空=全选）
        var normalizedProcesses = await NormalizeProcessKeysAsync(request.ProcessKeys);
        var normalizedProducts = NormalizeFixedKeys(request.ProductStatusKeys,
            ProductStatuses.All, ProductStatuses.ToKey, "产类");
        var normalizedStages = NormalizeFixedKeys(request.StageKeys,
            PieceRateStageKeys.All, PieceRateStageKeys.ToKey, "作业阶段");

        if (id.HasValue && id.Value <= 0) id = null;
        PieceRateProductionCategory entity;
        if (id.HasValue)
        {
            entity = await _context.PieceRateProductionCategories
                .Include(c => c.Tiers)
                .FirstOrDefaultAsync(c => c.Id == id.Value)
                ?? throw new BusinessException($"类别不存在: Id={id}");
        }
        else
        {
            entity = new PieceRateProductionCategory { Tiers = new List<PieceRateProductionCategoryTier>() };
            _context.PieceRateProductionCategories.Add(entity);
        }

        entity.SectionKey = sectionKey;
        entity.ProcessKeys = normalizedProcesses;
        entity.ProductStatusKeys = normalizedProducts;
        entity.StageKeys = normalizedStages;
        entity.BasePrice = request.BasePrice;
        entity.Unit = unit;
        entity.IsActive = request.IsActive;
        entity.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();

        // 档行整组替换
        var newTiers = BuildTiers(request.Tiers);
        ValidateTierOverlapAndDuplicates(newTiers);
        foreach (var old in entity.Tiers.ToList())
            _context.PieceRateProductionCategoryTiers.Remove(old);
        entity.Tiers.Clear();
        entity.Tiers.AddRange(newTiers);

        // 同工段启用类别禁止交集（当前若停用则跳过）
        if (entity.IsActive)
        {
            await EnsureNoCoverageOverlapAsync(entity, id);
        }

        await _context.SaveChangesAsync();
        return await GetDetailAsync(entity.Id) ?? throw new BusinessException("保存后读取失败");
    }

    /// <summary>归一化工序键集：翻译中文→Key、校验、全列归一（DB 工序域 = ProcessDefinition ∪ 常量域）</summary>
    private async Task<string?> NormalizeProcessKeysAsync(IEnumerable<string>? input)
    {
        var keys = new List<string>();
        foreach (var raw in input ?? [])
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var key = ProcessKeys.ToKey(raw) ?? raw.Trim();
            keys.Add(key);
        }
        var domain = (await _context.ProcessDefinitions.AsNoTracking()
                .Where(w => !string.IsNullOrEmpty(w.ProcessKey))
                .Select(w => w.ProcessKey!)
                .ToListAsync())
            .Concat(ProcessKeys.All)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var key in keys)
        {
            if (!domain.Contains(key, StringComparer.OrdinalIgnoreCase))
                throw new BusinessException($"无效的工序: {key}");
        }
        return PieceRateJsonKeys.SerializeNormalized(keys, domain);
    }

    /// <summary>归一化固定键集（产类/作业阶段）：翻译中文→Key、校验、全列归一</summary>
    private static string? NormalizeFixedKeys(
        IEnumerable<string>? input, string[] all, Func<string?, string?> toKey, string chineseName)
    {
        var keys = new List<string>();
        foreach (var raw in input ?? [])
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var key = toKey(raw);
            if (key == null)
                throw new BusinessException($"无效的{chineseName}: {raw}");
            keys.Add(key);
        }
        var domain = all.Distinct(StringComparer.Ordinal).ToArray();
        return PieceRateJsonKeys.SerializeNormalized(keys, domain);
    }

    /// <summary>
    /// 建档行实体（解析区间边界、等值维取 MatchValue）。等值维取值可空 = 无约束档被过滤；
    /// 空 RangeText 区间维抛出 BusinessException。
    /// </summary>
    private static List<PieceRateProductionCategoryTier> BuildTiers(
        IReadOnlyList<PieceRateProductionCategoryTierSaveRequest>? rows)
    {
        var list = new List<PieceRateProductionCategoryTier>();
        if (rows == null) return list;

        foreach (var row in rows)
        {
            if (!PieceRateDimensionKeys.IsKey(row.DimensionKey))
                throw new BusinessException($"无效的维度: {row.DimensionKey}");
            if (row.Ratio <= 0)
                throw new BusinessException($"{PieceRateDimensionKeys.ToChinese(row.DimensionKey)}档加价系数必须大于0");

            var tier = new PieceRateProductionCategoryTier
            {
                DimensionKey = row.DimensionKey,
                Ratio = row.Ratio,
                IsActive = row.IsActive
            };

            if (PieceRateDimensionKeys.IsValueDimension(row.DimensionKey))
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
                    $"{PieceRateDimensionKeys.ToChinese(row.DimensionKey)}档必须填写可解析的区间");

            tier.RangeText = row.RangeText!.Trim();
            tier.MatchValue = null;
            if (row.DimensionKey == PieceRateDimensionKeys.FixedLengthCount)
            {
                tier.MinInt = ToIntBound(min, PieceRateDimensionKeys.ToChinese(row.DimensionKey));
                tier.MaxInt = ToIntBound(max, PieceRateDimensionKeys.ToChinese(row.DimensionKey));
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
            throw new BusinessException($"{dimChinese ?? "定尺"}档必须为整数区间");
        return (int)value.Value;
    }

    /// <summary>对已建档行实体校验：区间维两两重叠（相切=合法邻接；定尺维整数共享即重叠）+ 等值维取值去重（OrdinalIgnoreCase）。仅校验启用行。</summary>
    private static void ValidateTierOverlapAndDuplicates(IReadOnlyList<PieceRateProductionCategoryTier> tiers)
    {
        var active = tiers.Where(t => t.IsActive).ToList();
        foreach (var dimGroup in active.GroupBy(t => t.DimensionKey))
        {
            var dimRows = dimGroup.ToList();

            if (PieceRateDimensionKeys.IsValueDimension(dimGroup.Key))
            {
                var dup = PieceRateDimensionRules.FirstDuplicateOrdinalIgnoreCase(
                    dimRows.Select(t => t.MatchValue));
                if (dup != null)
                    throw new BusinessException(
                        $"{PieceRateDimensionKeys.ToChinese(dimGroup.Key)}档取值重复: {dup}");
                continue;
            }

            for (var i = 0; i < dimRows.Count; i++)
            {
                for (var j = i + 1; j < dimRows.Count; j++)
                {
                    var a = dimRows[i];
                    var b = dimRows[j];
                    var overlap = dimGroup.Key == PieceRateDimensionKeys.FixedLengthCount
                        ? PieceRateDimensionRules.RangesOverlapInt(a.MinInt, a.MaxInt, b.MinInt, b.MaxInt)
                        : PieceRateDimensionRules.RangesOverlap(a.MinValue, a.MaxValue, b.MinValue, b.MaxValue);
                    if (overlap)
                        throw new BusinessException(
                            $"{PieceRateDimensionKeys.ToChinese(dimGroup.Key)}档区间重叠: 「{a.RangeText}」与「{b.RangeText}」");
                }
            }
        }
    }

    private async Task EnsureNoCoverageOverlapAsync(PieceRateProductionCategory entity, int? selfId)
    {
        var others = await _context.PieceRateProductionCategories.AsNoTracking()
            .Where(c => c.SectionKey == entity.SectionKey
                        && c.IsActive
                        && (!selfId.HasValue || c.Id != selfId.Value))
            .ToListAsync();

        var mine = CategoryCoverageRule.Create(
            entity.SectionKey,
            PieceRateJsonKeys.Deserialize(entity.ProcessKeys),
            PieceRateJsonKeys.Deserialize(entity.ProductStatusKeys),
            PieceRateJsonKeys.Deserialize(entity.StageKeys));

        foreach (var other in others)
        {
            var theirs = CategoryCoverageRule.Create(
                other.SectionKey,
                PieceRateJsonKeys.Deserialize(other.ProcessKeys),
                PieceRateJsonKeys.Deserialize(other.ProductStatusKeys),
                PieceRateJsonKeys.Deserialize(other.StageKeys));
            if (mine.Intersects(theirs))
                throw new BusinessException(
                    $"类别覆盖与既有类别冲突（禁止交集）: 「{mine.Describe()}」与「{theirs.Describe()}」");
        }
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.PieceRateProductionCategories
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new BusinessException($"类别不存在: Id={id}");
        _context.PieceRateProductionCategories.Remove(entity); // 级联删档行
        await _context.SaveChangesAsync();
    }

    // ==================== 试算匹配 ====================

    public async Task<PieceRateProductionMatchResultDto?> MatchPriceAsync(PieceRateProductionMatchRequest request)
    {
        var sectionKey = SectionKeys.ToKey(request.SectionName) ?? request.SectionName;
        if (string.IsNullOrEmpty(sectionKey))
            return null;

        // SQL 仅下推工段 + 启用
        var candidates = await _context.PieceRateProductionCategories.AsNoTracking()
            .Include(c => c.Tiers)
            .Where(c => c.SectionKey == sectionKey && c.IsActive)
            .ToListAsync();

        // 归一化请求键值（兼容中文存量输入；记录侧本身为英文 Key）
        var processName = NormalizeOrNull(ProcessKeys.ToKey(request.ProcessName), request.ProcessName);
        var productStatus = NormalizeOrNull(ProductStatuses.ToKey(request.ProductStatus), request.ProductStatus);
        var stage = NormalizeOrNull(PieceRateStageKeys.ToKey(request.Stage), request.Stage);
        var specialState = NormalizeOrNull(PieceRateStateKeys.ToKey(request.SpecialState), request.SpecialState);

        PieceRateProductionCategory? matched = null;
        foreach (var c in candidates)
        {
            var procs = PieceRateJsonKeys.Deserialize(c.ProcessKeys);
            var prods = PieceRateJsonKeys.Deserialize(c.ProductStatusKeys);
            var stages = PieceRateJsonKeys.Deserialize(c.StageKeys);
            if (PieceRateJsonKeys.ContainsKey(procs, processName)
                && PieceRateJsonKeys.ContainsKey(prods, productStatus)
                && PieceRateJsonKeys.ContainsKey(stages, stage))
            {
                if (matched != null)
                    throw new BusinessException(
                        $"数据违例：工段「{SectionKeys.ToChinese(sectionKey)}」命中多个启用类别（禁交集被破坏）: "
                        + $"{matched.SectionKey} 与 {c.SectionKey}，请检查重复类别");
                matched = c;
            }
        }

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
                DimensionKeyChinese = PieceRateDimensionKeys.ToChinese(dimKey),
                RangeText = PieceRateDimensionKeys.IsValueDimension(dimKey)
                    ? hitTier.MatchValue
                    : hitTier.RangeText,
                Ratio = hitTier.Ratio
            });
            totalRatio *= hitTier.Ratio;
        }

        var unitPrice = matched.BasePrice * totalRatio;

        var sectionMap = await _sectionNameDisplay.GetSectionNameMapAsync();
        var processMap = await _processDefinitionService.GetProcessNameMapAsync();
        var sectionCn = sectionMap.TryGetValue(matched.SectionKey, out var cn)
            ? cn
            : SectionKeys.ToChinese(matched.SectionKey) ?? matched.SectionKey;

        return new PieceRateProductionMatchResultDto
        {
            CategoryId = matched.Id,
            SectionKey = matched.SectionKey,
            SectionKeyChinese = sectionCn,
            DisplayName = BuildDisplayName(matched, sectionMap, processMap),
            BasePrice = matched.BasePrice,
            TotalRatio = totalRatio,
            UnitPrice = unitPrice,
            Unit = matched.Unit,
            UnitChinese = PieceRateUnitKeys.ToChinese(matched.Unit) ?? matched.Unit,
            Hits = hits,
            Remark = matched.Remark
        };
    }

    private static PieceRateProductionCategoryTier? SelectHitTier(
        List<PieceRateProductionCategoryTier> activeTiers, string dimKey,
        PieceRateProductionMatchRequest request)
    {
        if (PieceRateDimensionKeys.IsValueDimension(dimKey))
        {
            var value = dimKey switch
            {
                PieceRateDimensionKeys.SpecialGrade => request.PlantGrade,
                PieceRateDimensionKeys.SpecialState => NormalizeOrNull(
                    PieceRateStateKeys.ToKey(request.SpecialState), request.SpecialState),
                PieceRateDimensionKeys.Device => request.EquipmentName,
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

    private static string? NormalizeOrNull(string? key, string? original)
        => string.IsNullOrWhiteSpace(original) ? null : (key ?? original.Trim());

    // ==================== 映射 ====================

    private static PieceRateProductionCategoryListItemDto MapListItem(
        PieceRateProductionCategory entity,
        IReadOnlyDictionary<string, string> sectionMap,
        IReadOnlyDictionary<string, string> processMap)
    {
        var procs = PieceRateJsonKeys.Deserialize(entity.ProcessKeys);
        var prods = PieceRateJsonKeys.Deserialize(entity.ProductStatusKeys);
        var stages = PieceRateJsonKeys.Deserialize(entity.StageKeys);

        return new PieceRateProductionCategoryListItemDto
        {
            Id = entity.Id,
            SectionKey = entity.SectionKey,
            SectionKeyChinese = sectionMap.TryGetValue(entity.SectionKey, out var cn)
                ? cn
                : SectionKeys.ToChinese(entity.SectionKey) ?? entity.SectionKey,
            ProcessKeys = OrderDomain(procs, ProcessKeys.All).ToList(),
            ProductStatusKeys = OrderDomain(prods, ProductStatuses.All).ToList(),
            StageKeys = OrderDomain(stages, PieceRateStageKeys.All).ToList(),
            DisplayName = BuildDisplayName(entity, sectionMap, processMap),
            BasePrice = entity.BasePrice,
            Unit = entity.Unit,
            UnitChinese = PieceRateUnitKeys.ToChinese(entity.Unit) ?? entity.Unit,
            IsActive = entity.IsActive,
            TierCount = entity.Tiers.Count(t => t.IsActive),
            Remark = entity.Remark,
            UpdatedTime = entity.UpdatedTime,
            CreatedTime = entity.CreatedTime
        };
    }

    private static string BuildDisplayName(
        PieceRateProductionCategory entity,
        IReadOnlyDictionary<string, string> sectionMap,
        IReadOnlyDictionary<string, string> processMap)
    {
        var procs = PieceRateJsonKeys.Deserialize(entity.ProcessKeys);
        var prods = PieceRateJsonKeys.Deserialize(entity.ProductStatusKeys);
        var stages = PieceRateJsonKeys.Deserialize(entity.StageKeys);

        var sectionCn = sectionMap.TryGetValue(entity.SectionKey, out var cn)
            ? cn
            : SectionKeys.ToChinese(entity.SectionKey) ?? entity.SectionKey;

        // 各自有序中文集（空=全选，显示「全部」）
        var prodCn = prods.Count == 0
            ? null
            : OrderDomain(prods, ProductStatuses.All)
                .Select(k => ProductStatuses.ToChinese(k) ?? k)
                .ToArray();
        var procCn = procs.Count == 0
            ? null
            : OrderDomain(procs, ProcessKeys.All)
                .Select(k => processMap.TryGetValue(k, out var name) ? name : k)
                .ToArray();
        var stageCn = stages.Count == 0
            ? null
            : OrderDomain(stages, PieceRateStageKeys.All)
                .Select(k => PieceRateStageKeys.ToChinese(k) ?? k)
                .ToArray();

        return CategoryDisplayNameHelper.Build(sectionCn, prodCn, procCn, stageCn);
    }

    /// <summary>按键域声明顺序稳定排序（域外的键追加末尾，保持确定性）</summary>
    private static IEnumerable<string> OrderDomain(IEnumerable<string> keys, string[] domainOrder)
    {
        var orderMap = domainOrder
            .Select((k, i) => (k, i))
            .ToDictionary(x => x.k, x => x.i, StringComparer.Ordinal);
        return keys.OrderBy(k => orderMap.TryGetValue(k, out var i) ? i : int.MaxValue)
            .ThenBy(k => k, StringComparer.Ordinal);
    }

    private static int PieceRateDimensionIndex(string dimKey)
    {
        var idx = Array.IndexOf(PieceRateDimensionKeys.All, dimKey);
        return idx < 0 ? int.MaxValue : idx;
    }

    private static void SortItems(
        List<PieceRateProductionCategoryListItemDto> items,
        string sortBy, bool isDescending)
    {
        var key = (sortBy ?? "CreatedTime").ToLowerInvariant();
        IOrderedEnumerable<PieceRateProductionCategoryListItemDto>? ordered = null;
        switch (key)
        {
            case "sectionkey": ordered = Order(items, i => i.SectionKey, isDescending); break;
            case "sectionkeychinese": ordered = Order(items, i => i.SectionKeyChinese, isDescending); break;
            case "displayname": ordered = Order(items, i => i.DisplayName, isDescending); break;
            case "baseprice": ordered = Order(items, i => i.BasePrice, isDescending); break;
            case "unit": ordered = Order(items, i => i.Unit, isDescending); break;
            case "isactive": ordered = Order(items, i => i.IsActive, isDescending); break;
            case "tiercount": ordered = Order(items, i => i.TierCount, isDescending); break;
            case "remark": ordered = Order(items, i => i.Remark, isDescending); break;
            case "updatedtime": ordered = Order(items, i => i.UpdatedTime, isDescending); break;
            default: ordered = Order(items, i => i.CreatedTime, isDescending); break;
        }

        // 反射排序改动原集合顺序
        var sorted = ordered.ToList();
        items.Clear();
        items.AddRange(sorted);
    }

    private static IOrderedEnumerable<PieceRateProductionCategoryListItemDto> Order<TKey>(
        IEnumerable<PieceRateProductionCategoryListItemDto> source,
        Func<PieceRateProductionCategoryListItemDto, TKey> keySelector,
        bool isDescending)
        => isDescending
            ? source.OrderByDescending(keySelector)
            : source.OrderBy(keySelector);
}
