using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Payroll;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Payroll;
using MES.Data.Entities.Quality;
using MES.Services.Helpers;

namespace MES.Services.Payroll;

/// <summary>
/// 生产计件类别服务（2026-09-02 重构引入，替代旧 PieceRateStandardService）——工资结算上下文。
/// 类别 = 工段(必选单选) × 工序/产类/作业阶段(可空多选，空=全选) + 基准价 + 结算单位；
/// 维度系数在子表档行（无例外价）。结算单价 = 类别.BasePrice × 命中档 Ratio 连乘。
/// 保存时对同工段启用类别跑「禁止交集」+ 档内区间重叠/等值去重校验；
/// 匹配 SQL 仅下推 SectionKey== &amp;&amp; IsActive，约束成员表 ToList 后内存 OrdinalIgnoreCase 比较。
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
            .Include(c => c.ConstraintKeys)
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
            .Include(c => c.ConstraintKeys)
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

        // 三键约束集合归一：翻译中文→Key → 校验合法 → 显式全列表归一为空数组（空=全选，不插成员行）
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
                .Include(c => c.ConstraintKeys)
                .FirstOrDefaultAsync(c => c.Id == id.Value)
                ?? throw new BusinessException($"类别不存在: Id={id}");
        }
        else
        {
            entity = new PieceRateProductionCategory { Tiers = new List<PieceRateProductionCategoryTier>() };
            _context.PieceRateProductionCategories.Add(entity);
        }

        entity.SectionKey = sectionKey;
        entity.BasePrice = request.BasePrice;
        entity.Unit = unit;
        entity.IsActive = request.IsActive;
        entity.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();

        // 约束集合成员行整组替换（按 ConstraintType；空数组=移除全部成员行=该维全选）
        ReplaceKeys(entity, PieceRateConstraintTypes.Process, normalizedProcesses);
        ReplaceKeys(entity, PieceRateConstraintTypes.ProductStatus, normalizedProducts);
        ReplaceKeys(entity, PieceRateConstraintTypes.Stage, normalizedStages);

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

    /// <summary>归一化工序键集：翻译中文→Key、校验、全列归零（DB 工序域 = ProcessDefinition ∪ 常量域）</summary>
    private async Task<string[]> NormalizeProcessKeysAsync(IEnumerable<string>? input)
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
        return NormalizeToDomain(keys, domain);
    }

    /// <summary>归一化固定键集（产类/作业阶段）：翻译中文→Key、校验、全列归零</summary>
    private static string[] NormalizeFixedKeys(
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
        return NormalizeToDomain(keys, domain);
    }

    /// <summary>
    /// 键集归一为「落库成员数组」：空集合 → 空数组（=全选，不插成员行）；
    /// 非空集合若与 fullDomain 全等（OrdinalIgnoreCase）→ 空数组（显式全列表归一为全选，防禁交集误判）；
    /// 否则返回去重排序（Ordinal）的 Key 数组。
    /// </summary>
    private static string[] NormalizeToDomain(IEnumerable<string>? keys, IEnumerable<string> fullDomain)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (keys != null)
        {
            foreach (var key in keys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                    set.Add(key.Trim());
            }
        }

        // 空集合 = 全选
        if (set.Count == 0) return [];

        // 显式全列表 = 全选（须与空形态统一，否则「全选」与「显式全列」被禁交集误判为不相交）
        var domain = fullDomain.Where(d => !string.IsNullOrWhiteSpace(d)).ToArray();
        if (domain.Length > 0)
        {
            var domainSet = new HashSet<string>(domain, StringComparer.OrdinalIgnoreCase);
            if (domainSet.Count == set.Count && domainSet.IsSubsetOf(set))
                return [];
        }

        return set.OrderBy(k => k, StringComparer.Ordinal).ToArray();
    }

    // ==================== 约束集合成员行 helper ====================

    /// <summary>取某类某 ConstraintType 的成员 Key 数组（去重 OrdinalIgnoreCase；0 行=该维全选 → 空数组）</summary>
    private static string[] ConstraintKeysOf(PieceRateProductionCategory entity, string type)
        => entity.ConstraintKeys
            .Where(k => string.Equals(k.ConstraintType, type, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(k.Key))
            .Select(k => k.Key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>取某类某 ConstraintType 的集合（HashSet OrdinalIgnoreCase；空=全选）</summary>
    private static HashSet<string> ConstraintSetOf(PieceRateProductionCategory entity, string type)
        => new(ConstraintKeysOf(entity, type), StringComparer.OrdinalIgnoreCase);

    /// <summary>键约束是否包含某值：空集合=全选 → 恒 true；否则要求 value 非空且 OrdinalIgnoreCase 命中。</summary>
    private static bool KeysContain(IReadOnlyCollection<string> keys, string? value)
    {
        if (keys.Count == 0) return true;
        return value != null && keys.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>整组替换某类某 ConstraintType 的成员行（先移除旧同 type，再按给定 Key 数组追加；空数组=移除全部=该维全选）</summary>
    private void ReplaceKeys(PieceRateProductionCategory entity, string type, IEnumerable<string> keys)
    {
        var old = entity.ConstraintKeys
            .Where(k => string.Equals(k.ConstraintType, type, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var row in old)
            _context.PieceRateProductionCategoryKeys.Remove(row);
        entity.ConstraintKeys.RemoveAll(k => string.Equals(k.ConstraintType, type, StringComparison.OrdinalIgnoreCase));

        foreach (var key in keys)
        {
            entity.ConstraintKeys.Add(new PieceRateProductionCategoryKey
            {
                ConstraintType = type,
                Key = key
            });
        }
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
            .Include(c => c.ConstraintKeys)
            .Where(c => c.SectionKey == entity.SectionKey
                        && c.IsActive
                        && (!selfId.HasValue || c.Id != selfId.Value))
            .ToListAsync();

        var mine = CategoryCoverageRule.Create(
            entity.SectionKey,
            ConstraintSetOf(entity, PieceRateConstraintTypes.Process),
            ConstraintSetOf(entity, PieceRateConstraintTypes.ProductStatus),
            ConstraintSetOf(entity, PieceRateConstraintTypes.Stage));

        foreach (var other in others)
        {
            var theirs = CategoryCoverageRule.Create(
                other.SectionKey,
                ConstraintSetOf(other, PieceRateConstraintTypes.Process),
                ConstraintSetOf(other, PieceRateConstraintTypes.ProductStatus),
                ConstraintSetOf(other, PieceRateConstraintTypes.Stage));
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
        => await MatchByRequestAsync(request, weightKg: null, quantity: null);

    /// <summary>核心匹配计价（手动 match-price 与按产量记录点选试算共用，2026-09-04 拆出防双通道漂移）。
    /// 命中后按带量参数折算 SimulatedAmount（与月结采集同 PieceRateAmountHelper 口径、length 恒 null；
    /// 生产记录行无元/千米长度维，结算本就不折算）；手动请求无量恒 null。</summary>
    private async Task<PieceRateProductionMatchResultDto?> MatchByRequestAsync(
        PieceRateProductionMatchRequest request, decimal? weightKg, int? quantity)
    {
        var sectionKey = SectionKeys.ToKey(request.SectionName) ?? request.SectionName;
        if (string.IsNullOrEmpty(sectionKey))
            return null;

        // SQL 仅下推工段 + 启用
        var candidates = await _context.PieceRateProductionCategories.AsNoTracking()
            .Include(c => c.Tiers)
            .Include(c => c.ConstraintKeys)
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
            SimulatedAmount = PieceRateAmountHelper.AmountForUnit(matched.Unit, unitPrice, weightKg, quantity, null),
            Hits = hits,
            Remark = matched.Remark
        };
    }

    // ==================== 模拟测算（按产量记录点选计价，2026-09-04） ====================

    /// <summary>候选产量记录（全局任意记录）：产量源必选 + 关键字过滤 SQL 下推 + 分页；默认记录日期降序 + Id 降序。
    /// 关键字只下推记录本地列（操作人/设备/备注/制造规格；PicklingOut/ProcessInspection 另含自冗余批次号），
    /// 导航批次号/规格仅投影展示（避免导航列下推在 InMemory 下不可靠）。与月结采集同 Mapper 映射单源。</summary>
    public async Task<PagedResult<PieceRateProductionTrialRecordDto>> GetTrialRecordsAsync(
        PieceRateProductionTrialRecordQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.Source)
            || !Enum.TryParse<PieceRateProductionTrialSource>(query.Source.Trim(), ignoreCase: true, out var source))
            throw new BusinessException(string.IsNullOrWhiteSpace(query.Source)
                ? "请选择产量源"
                : $"无效的产量源: {query.Source}");

        var kw = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim();

        switch (source)
        {
            case PieceRateProductionTrialSource.ProductionRecord:
            {
                var q = _context.ProductionRecords.AsNoTracking()
                    .Include(r => r.ProductionBatch)
                    .AsQueryable();
                if (kw != null)
                    q = q.Where(x => (x.Operator != null && x.Operator.Contains(kw))
                                     || (x.EquipmentName != null && x.EquipmentName.Contains(kw))
                                     || (x.Remark != null && x.Remark.Contains(kw))
                                     || (x.ManufacturingSpec != null && x.ManufacturingSpec.Contains(kw)));
                var total = await q.CountAsync();
                var rows = await q
                    .OrderByDescending(x => x.ExecDate)
                    .ThenByDescending(x => x.Id)
                    .Skip(query.Skip)
                    .Take(query.PageSize)
                    .ToListAsync();
                var sectionMap = await _sectionNameDisplay.GetSectionNameMapAsync();
                return Result(source, rows.Select(r => MapTrial(r, r.ProductionBatch, sectionMap)).ToList(), total, query);
            }
            case PieceRateProductionTrialSource.PicklingIn:
            {
                var q = _context.PicklingInRecords.AsNoTracking()
                    .Include(r => r.ProductionBatch)
                    .AsQueryable();
                if (kw != null)
                    q = q.Where(x => (x.Operator != null && x.Operator.Contains(kw))
                                     || (x.EquipmentName != null && x.EquipmentName.Contains(kw))
                                     || (x.Remark != null && x.Remark.Contains(kw))
                                     || (x.ManufacturingSpec != null && x.ManufacturingSpec.Contains(kw)));
                var total = await q.CountAsync();
                var rows = await q
                    .OrderByDescending(x => x.InDate)
                    .ThenByDescending(x => x.Id)
                    .Skip(query.Skip)
                    .Take(query.PageSize)
                    .ToListAsync();
                var sectionMap = await _sectionNameDisplay.GetSectionNameMapAsync();
                return Result(source, rows.Select(r => MapTrial(r, r.ProductionBatch, sectionMap)).ToList(), total, query);
            }
            case PieceRateProductionTrialSource.PicklingOut:
            {
                var q = _context.PicklingOutRecords.AsNoTracking()
                    .AsQueryable();
                if (kw != null)
                    q = q.Where(x => (x.Operator != null && x.Operator.Contains(kw))
                                     || (x.EquipmentName != null && x.EquipmentName.Contains(kw))
                                     || (x.Remark != null && x.Remark.Contains(kw))
                                     || (x.ManufacturingSpec != null && x.ManufacturingSpec.Contains(kw))
                                     || (x.BatchNo != null && x.BatchNo.Contains(kw)));
                var total = await q.CountAsync();
                var rows = await q
                    .OrderByDescending(x => x.CompleteDate)
                    .ThenByDescending(x => x.Id)
                    .Skip(query.Skip)
                    .Take(query.PageSize)
                    .ToListAsync();
                var sectionMap = await _sectionNameDisplay.GetSectionNameMapAsync();
                return Result(source, rows.Select(r => MapTrial(r, sectionMap)).ToList(), total, query);
            }
            default:
            {
                var q = _context.ProcessInspections.AsNoTracking()
                    .Include(p => p.ProductionBatch)
                    .AsQueryable();
                if (kw != null)
                    q = q.Where(x => (x.Inspector != null && x.Inspector.Contains(kw))
                                     || (x.EquipmentName != null && x.EquipmentName.Contains(kw))
                                     || (x.Remark != null && x.Remark.Contains(kw))
                                     || (x.ManufacturingSpec != null && x.ManufacturingSpec.Contains(kw))
                                     || (x.BatchNo != null && x.BatchNo.Contains(kw)));
                var total = await q.CountAsync();
                var rows = await q
                    .OrderByDescending(x => x.InspectionDate)
                    .ThenByDescending(x => x.Id)
                    .Skip(query.Skip)
                    .Take(query.PageSize)
                    .ToListAsync();
                var sectionMap = await _sectionNameDisplay.GetSectionNameMapAsync();
                return Result(source, rows.Select(p => MapTrial(p, p.ProductionBatch, sectionMap)).ToList(), total, query);
            }
        }
    }

    private static PagedResult<PieceRateProductionTrialRecordDto> Result(
        PieceRateProductionTrialSource source, List<PieceRateProductionTrialRecordDto> items,
        int totalCount, PieceRateProductionTrialRecordQuery query)
        => new()
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };

    /// <summary>产量源中文</summary>
    private static string SourceChinese(PieceRateProductionTrialSource source)
        => PieceRateProductionTrialSourceExtensions.ToChinese(source);

    private static (string Key, string Chinese) SectionOf(
        string? rawSection, IReadOnlyDictionary<string, string> sectionMap)
    {
        var key = SectionKeys.ToKey(rawSection) ?? rawSection ?? string.Empty;
        var cn = sectionMap.TryGetValue(key, out var name)
            ? name
            : (SectionKeys.ToChinese(key) ?? key);
        return (key, cn);
    }

    private static string? ProductStatusChinese(string? key)
        => string.IsNullOrWhiteSpace(key) ? null : (ProductStatuses.ToChinese(key) ?? key);

    private static string? StageChinese(string? stageKey)
        => string.IsNullOrWhiteSpace(stageKey) ? null : (PieceRateStageKeys.ToChinese(stageKey) ?? stageKey);

    private static PieceRateProductionTrialRecordDto MapTrial(
        ProductionRecord r, ProductionBatch? batch, IReadOnlyDictionary<string, string> sectionMap)
    {
        var (key, cn) = SectionOf(r.SectionName, sectionMap);
        var spec = !string.IsNullOrWhiteSpace(r.ManufacturingSpec) ? r.ManufacturingSpec : batch?.Specification;
        return new PieceRateProductionTrialRecordDto
        {
            Id = r.Id,
            SourceKey = nameof(PieceRateProductionTrialSource.ProductionRecord),
            SourceChinese = SourceChinese(PieceRateProductionTrialSource.ProductionRecord),
            RecordDate = r.ExecDate,
            BatchNo = batch?.BatchNo,
            SectionKey = key,
            SectionKeyChinese = cn,
            ProcessName = r.ProcessName,
            ProductStatus = r.ProductStatus,
            ProductStatusChinese = ProductStatusChinese(r.ProductStatus),
            Specification = spec,
            Quantity = r.Quantity,
            Weight = r.Weight,
            Operator = r.Operator,
            EquipmentName = r.EquipmentName,
            Remark = r.Remark
        };
    }

    private static PieceRateProductionTrialRecordDto MapTrial(
        PicklingInRecord r, ProductionBatch? batch, IReadOnlyDictionary<string, string> sectionMap)
    {
        var (key, cn) = SectionOf(r.SectionName, sectionMap);
        var spec = !string.IsNullOrWhiteSpace(r.ManufacturingSpec) ? r.ManufacturingSpec : batch?.Specification;
        return new PieceRateProductionTrialRecordDto
        {
            Id = r.Id,
            SourceKey = nameof(PieceRateProductionTrialSource.PicklingIn),
            SourceChinese = SourceChinese(PieceRateProductionTrialSource.PicklingIn),
            RecordDate = r.InDate,
            BatchNo = batch?.BatchNo,
            SectionKey = key,
            SectionKeyChinese = cn,
            ProcessName = r.ProcessName,
            ProductStatus = r.ProductStatus,
            ProductStatusChinese = ProductStatusChinese(r.ProductStatus),
            StageKey = PieceRateStageKeys.InTank,
            StageChinese = StageChinese(PieceRateStageKeys.InTank),
            Specification = spec,
            Quantity = r.Quantity,
            Weight = r.Weight,
            Operator = r.Operator,
            EquipmentName = r.EquipmentName,
            Remark = r.Remark
        };
    }

    private static PieceRateProductionTrialRecordDto MapTrial(
        PicklingOutRecord r, IReadOnlyDictionary<string, string> sectionMap)
    {
        var (key, cn) = SectionOf(r.SectionName, sectionMap);
        return new PieceRateProductionTrialRecordDto
        {
            Id = r.Id,
            SourceKey = nameof(PieceRateProductionTrialSource.PicklingOut),
            SourceChinese = SourceChinese(PieceRateProductionTrialSource.PicklingOut),
            RecordDate = r.CompleteDate,
            BatchNo = r.BatchNo,
            SectionKey = key,
            SectionKeyChinese = cn,
            ProcessName = r.ProcessName,
            ProductStatus = r.ProductStatus,
            ProductStatusChinese = ProductStatusChinese(r.ProductStatus),
            StageKey = PieceRateStageKeys.OutTank,
            StageChinese = StageChinese(PieceRateStageKeys.OutTank),
            Specification = r.ManufacturingSpec,
            Quantity = r.Quantity,
            Weight = r.Weight,
            Operator = r.Operator,
            EquipmentName = r.EquipmentName,
            Remark = r.Remark
        };
    }

    private static PieceRateProductionTrialRecordDto MapTrial(
        ProcessInspection p, ProductionBatch? batch, IReadOnlyDictionary<string, string> sectionMap)
    {
        var (key, cn) = SectionOf(p.SectionName, sectionMap);
        var spec = !string.IsNullOrWhiteSpace(p.ManufacturingSpec) ? p.ManufacturingSpec : batch?.Specification;
        return new PieceRateProductionTrialRecordDto
        {
            Id = p.Id,
            SourceKey = nameof(PieceRateProductionTrialSource.ProcessInspection),
            SourceChinese = SourceChinese(PieceRateProductionTrialSource.ProcessInspection),
            RecordDate = p.InspectionDate,
            BatchNo = p.BatchNo,
            SectionKey = key,
            SectionKeyChinese = cn,
            ProcessName = p.ProcessName,
            ProductStatus = p.ProductStatus,
            ProductStatusChinese = ProductStatusChinese(p.ProductStatus),
            Specification = spec,
            Quantity = p.Quantity,
            Weight = p.Weight,
            Operator = p.Inspector,
            EquipmentName = p.EquipmentName,
            Remark = p.Remark
        };
    }

    /// <summary>模拟测算：按一条真实产量记录计价（与月结采集同 ProductionMatchRequestMapper 单源映射，
    /// 含切行定尺/光亮接线）。记录不存在抛 BusinessException；命中不到启用类别返回 null（=未定价）。</summary>
    public async Task<PieceRateProductionMatchResultDto?> MatchProductionRecordAsync(
        PieceRateProductionTrialSource source, int recordId)
    {
        switch (source)
        {
            case PieceRateProductionTrialSource.ProductionRecord:
            {
                var rec = await _context.ProductionRecords.AsNoTracking()
                    .Include(r => r.ProductionBatch)
                    .FirstOrDefaultAsync(r => r.Id == recordId)
                    ?? throw new BusinessException($"生产记录不存在: {recordId}");
                var request = ProductionMatchRequestMapper.BuildFromProductionRecord(rec, rec.ProductionBatch);
                return await MatchByRequestAsync(request, rec.Weight, rec.Quantity);
            }
            case PieceRateProductionTrialSource.PicklingIn:
            {
                var rec = await _context.PicklingInRecords.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == recordId)
                    ?? throw new BusinessException($"去油/酸洗入缸记录不存在: {recordId}");
                var request = ProductionMatchRequestMapper.BuildFromPicklingIn(rec);
                return await MatchByRequestAsync(request, rec.Weight, rec.Quantity);
            }
            case PieceRateProductionTrialSource.PicklingOut:
            {
                var rec = await _context.PicklingOutRecords.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == recordId)
                    ?? throw new BusinessException($"去油/酸洗完工记录不存在: {recordId}");
                var request = ProductionMatchRequestMapper.BuildFromPicklingOut(rec);
                return await MatchByRequestAsync(request, rec.Weight, rec.Quantity);
            }
            default:
            {
                var rec = await _context.ProcessInspections.AsNoTracking()
                    .Include(p => p.ProductionBatch)
                    .FirstOrDefaultAsync(p => p.Id == recordId)
                    ?? throw new BusinessException($"过程检验记录不存在: {recordId}");
                var request = ProductionMatchRequestMapper.BuildFromProcessInspection(rec, rec.ProductionBatch);
                return await MatchByRequestAsync(request, rec.Weight, rec.Quantity);
            }
        }
    }

    private static PieceRateProductionCategoryTier? SelectHitTier(
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
        var procs = ConstraintKeysOf(entity, PieceRateConstraintTypes.Process);
        var prods = ConstraintKeysOf(entity, PieceRateConstraintTypes.ProductStatus);
        var stages = ConstraintKeysOf(entity, PieceRateConstraintTypes.Stage);

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
        var procs = ConstraintKeysOf(entity, PieceRateConstraintTypes.Process);
        var prods = ConstraintKeysOf(entity, PieceRateConstraintTypes.ProductStatus);
        var stages = ConstraintKeysOf(entity, PieceRateConstraintTypes.Stage);

        var sectionCn = sectionMap.TryGetValue(entity.SectionKey, out var cn)
            ? cn
            : SectionKeys.ToChinese(entity.SectionKey) ?? entity.SectionKey;

        // 各自有序中文集（空=全选，显示「全部」）
        var prodCn = prods.Length == 0
            ? null
            : OrderDomain(prods, ProductStatuses.All)
                .Select(k => ProductStatuses.ToChinese(k) ?? k)
                .ToArray();
        var procCn = procs.Length == 0
            ? null
            : OrderDomain(procs, ProcessKeys.All)
                .Select(k => processMap.TryGetValue(k, out var name) ? name : k)
                .ToArray();
        var stageCn = stages.Length == 0
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
