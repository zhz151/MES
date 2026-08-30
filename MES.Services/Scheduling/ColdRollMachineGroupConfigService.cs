using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;
using MES.Core.DTOs.Scheduling;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Scheduling;

namespace MES.Services.Scheduling;

/// <summary>
/// 冷轧机台组配置服务 —— 冷轧工序归组参数表查询与维护（排程建议/排机估算引擎机台类型组归并输入）。
/// 供需链由 <see cref="ColdRollMachineGroupConfig.SupplyTargetGroupKey"/> 显式表达（2026-08-29 方案 A，组角色字段已移除）：
/// 配置了供给目标组 = 供给方、被别的组指向 = 需求方，允许多条并行链、多级链；
/// 强不变量为「链合法性」——供给目标须存在、供给链无环（破坏抛 BusinessException），不再有组角色/各恰 1；
/// 工序全局唯一归属一组（跨组重叠会导致引擎双重计数，服务层强校验）。
/// 保存/删除后失效排机估算、排程建议、机台组三处缓存（组配置影响引擎归组输出）。
/// </summary>
public class ColdRollMachineGroupConfigService : IColdRollMachineGroupConfigService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IProcessDefinitionService _processDefService;

    public ColdRollMachineGroupConfigService(
        AppDbContext context,
        IMemoryCache cache,
        IProcessDefinitionService processDefService)
    {
        _context = context;
        _cache = cache;
        _processDefService = processDefService;
    }

    public async Task<List<ColdRollMachineGroupConfigDto>> GetAllAsync()
    {
        return await _context.ColdRollMachineGroupConfigs
            .AsNoTracking()
            .OrderBy(g => g.DisplayOrder)
            .Select(g => ToDto(g))
            .ToListAsync();
    }

    public async Task<PagedResult<ColdRollMachineGroupConfigDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.ColdRollMachineGroupConfigs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(g =>
                g.GroupKey.Contains(kw) ||
                g.DisplayName.Contains(kw) ||
                (g.Remark != null && g.Remark.Contains(kw)));
        }

        queryable = ApplySort(queryable, query.SortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(g => ToDto(g))
            .ToListAsync();

        return new PagedResult<ColdRollMachineGroupConfigDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<bool> SaveAsync(ColdRollMachineGroupConfigDto dto)
    {
        ValidateDto(dto);

        // 组 Key 唯一校验提前（链校验的 ToDictionary 假定 GroupKey 唯一，重复会导致字典崩溃而非业务异常）
        var dup = await _context.ColdRollMachineGroupConfigs
            .AnyAsync(x => x.GroupKey == dto.GroupKey && x.Id != dto.Id);
        if (dup)
            throw new BusinessException($"组 Key「{dto.GroupKey}」已存在");

        var supplyTarget = string.IsNullOrWhiteSpace(dto.SupplyTargetGroupKey) ? null : dto.SupplyTargetGroupKey.Trim();
        dto.SupplyTargetGroupKey = supplyTarget;

        var effective = await BuildEffectiveRowsAsync(dto.Id, dto);
        ValidateChainInvariant(effective);
        await ValidateProcessKeysAsync(dto.Id, dto);

        if (dto.Id > 0)
        {
            var entity = await _context.ColdRollMachineGroupConfigs
                .FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (entity == null)
                throw new BusinessException("机台组配置不存在");

            entity.GroupKey = dto.GroupKey;
            entity.DisplayName = dto.DisplayName;
            entity.ProcessKeys = JoinKeys(dto.ProcessKeys);
            entity.DisplayOrder = dto.DisplayOrder;
            entity.SupplyTargetGroupKey = supplyTarget;
            entity.Remark = dto.Remark;
        }
        else
        {
            _context.ColdRollMachineGroupConfigs.Add(new ColdRollMachineGroupConfig
            {
                GroupKey = dto.GroupKey,
                DisplayName = dto.DisplayName,
                ProcessKeys = JoinKeys(dto.ProcessKeys),
                DisplayOrder = dto.DisplayOrder,
                SupplyTargetGroupKey = supplyTarget,
                Remark = dto.Remark,
            });
        }

        await _context.SaveChangesAsync();

        // 组配置变更 → 失效机台组缓存 + 排机估算 + 排程建议（引擎归组/拆档/流转角色输出全部受影响）
        InvalidateEngineCaches();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.ColdRollMachineGroupConfigs
            .FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            throw new BusinessException("机台组配置不存在");

        // 删除前强校验：删除本行后供需链仍合法（供给目标存在/无环；工序唯一归属不受删除影响——本行整组删除）
        var remaining = await _context.ColdRollMachineGroupConfigs
            .AsNoTracking()
            .Where(x => x.Id != id)
            .ToListAsync();
        ValidateChainInvariant(remaining);

        _context.ColdRollMachineGroupConfigs.Remove(entity);
        await _context.SaveChangesAsync();

        InvalidateEngineCaches();
        return true;
    }

    // ========== 私有方法 ==========

    private void ValidateDto(ColdRollMachineGroupConfigDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.GroupKey))
            throw new BusinessException("组 Key 不能为空");
        if (dto.GroupKey.Length > 50)
            throw new BusinessException("组 Key 不能超过 50 字符");
        if (!IsValidKey(dto.GroupKey))
            throw new BusinessException($"组 Key「{dto.GroupKey}」格式不正确：须字母开头，仅含字母/数字/下划线");

        if (string.IsNullOrWhiteSpace(dto.DisplayName))
            throw new BusinessException("组显示名不能为空");

        if (!string.IsNullOrWhiteSpace(dto.SupplyTargetGroupKey))
        {
            if (dto.SupplyTargetGroupKey.Length > 50)
                throw new BusinessException("供给目标组 Key 不能超过 50 字符");
            if (!IsValidKey(dto.SupplyTargetGroupKey))
                throw new BusinessException($"供给目标组 Key「{dto.SupplyTargetGroupKey}」格式不正确：须字母开头，仅含字母/数字/下划线");
        }
    }

    /// <summary>计算「应用本行后」的全表有效行集合（新增=现有+新行；更新=现有替换本行；删除=现有-本行）</summary>
    private async Task<List<ColdRollMachineGroupConfig>> BuildEffectiveRowsAsync(int id, ColdRollMachineGroupConfigDto dto)
    {
        var rows = await _context.ColdRollMachineGroupConfigs.AsNoTracking().ToListAsync();
        var effective = rows.Where(r => r.Id != id).ToList();

        // 用 DTO 当前值构成本行（新增：全新行；更新：本行新值）
        effective.Add(new ColdRollMachineGroupConfig
        {
            Id = id,
            GroupKey = dto.GroupKey,
            DisplayName = dto.DisplayName,
            ProcessKeys = JoinKeys(dto.ProcessKeys),
            DisplayOrder = dto.DisplayOrder,
            SupplyTargetGroupKey = string.IsNullOrWhiteSpace(dto.SupplyTargetGroupKey) ? null : dto.SupplyTargetGroupKey,
            Remark = dto.Remark,
        });
        return effective;
    }

    /// <summary>
    /// 供需链合法性校验（2026-08-29 方案 A，组角色字段已移除，链完全由 SupplyTargetGroupKey 显式表达）：
    /// ① 凡配置了供给目标组的行，目标组必须存在（可指向任何组含 None 组——多级链末端承接端）；
    /// ② 供给链无环。
    /// 工序全局唯一归属仍由 ValidateProcessKeysAsync 单独强校验。
    /// </summary>
    private static void ValidateChainInvariant(List<ColdRollMachineGroupConfig> rows)
    {
        var groupByKey = rows.ToDictionary(r => r.GroupKey, StringComparer.OrdinalIgnoreCase);

        // ① 配置了供给目标组的行：目标须存在（含 None 组亦可——被指向即成为需求承接端）
        foreach (var r in rows.Where(r => !string.IsNullOrEmpty(r.SupplyTargetGroupKey)))
        {
            var target = r.SupplyTargetGroupKey!;
            if (!groupByKey.TryGetValue(target, out _))
                throw new BusinessException($"组「{r.GroupKey}」的供给目标组「{target}」不存在，请检查");
        }

        // ② 无环：沿 SupplyTarget 链走，命中已访问组即抛错（A→B→A / A→A）
        foreach (var start in rows.Where(r => !string.IsNullOrEmpty(r.SupplyTargetGroupKey)))
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ColdRollMachineGroupConfig? cur = start;
            while (cur != null)
            {
                if (!visited.Add(cur.GroupKey))
                    throw new BusinessException($"供给链存在环（涉及组「{cur.GroupKey}」），请检查供给目标配置");
                var nextKey = cur.SupplyTargetGroupKey;
                cur = string.IsNullOrEmpty(nextKey) ? null : groupByKey.GetValueOrDefault(nextKey);
            }
        }
    }

    /// <summary>工序校验：非空、全部 ∈ 已启用的冷轧/冷拔工序集、组内无重复、跨组无工序重叠（OrdinalIgnoreCase）</summary>
    private async Task ValidateProcessKeysAsync(int id, ColdRollMachineGroupConfigDto dto)
    {
        var keys = NormalizeKeys(dto.ProcessKeys);
        if (keys.Count == 0)
            throw new BusinessException("组内工序不能为空（须至少选择一个冷轧/冷拔工序）");

        // 仅允许已启用的冷轧/冷拔工序归组（2026-08-29 收紧：禁用工序不参与归组/机台数配置/工段 Tab）
        var options = await _processDefService.GetColdRollOrDrawOptionsAsync();
        var enabledColdRollSet = options
            .Select(o => o.ProcessKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalid = keys.Where(k => !enabledColdRollSet.Contains(k)).ToList();
        if (invalid.Count > 0)
            throw new BusinessException($"工序 [{string.Join(",", invalid)}] 不是已启用的冷轧/冷拔工序，无法归组");

        // 跨组重叠：其他组已归属的工序不可再归入本组（防引擎双重计数）
        var otherKeys = await _context.ColdRollMachineGroupConfigs
            .AsNoTracking()
            .Where(g => g.Id != id)
            .Select(g => g.ProcessKeys)
            .ToListAsync();
        var owned = otherKeys
            .SelectMany(raw => SplitRawKeys(raw))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var overlap = keys.Where(k => owned.Contains(k)).ToList();
        if (overlap.Count > 0)
            throw new BusinessException($"工序 [{string.Join(",", overlap)}] 已归属其他机台组（工序全局唯一归属一组），请调整");
    }

    /// <summary>DTO List&lt;string&gt; → 实体逗号串（Trim + 去空 + 组内去重，OrdinalIgnoreCase）</summary>
    private static string JoinKeys(List<string>? keys)
        => string.Join(",", NormalizeKeys(keys));

    /// <summary>逗号串 → List&lt;string&gt;（Trim + 去空 + 去重，OrdinalIgnoreCase）</summary>
    private static List<string> SplitRawKeys(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>DTO List&lt;string&gt; 归一（Trim + 去空 + 去重，OrdinalIgnoreCase）</summary>
    private static List<string> NormalizeKeys(List<string>? keys)
        => keys?
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
           ?? new List<string>();

    private void InvalidateEngineCaches()
    {
        _cache.Remove(ColdRollPlanService.MachineGroupCacheKey);
        _cache.Remove(ColdRollPlanService.MachineEstimateCacheKey);
        _cache.Remove(ColdRollPlanService.ScheduleSuggestionCacheKey);
    }

    /// <summary>
    /// 稳定 Key 格式校验：字母或数字开头，仅含字母/数字/下划线。
    /// 组 Key 放宽允许数字开头（种子预置 5060/2030 等业务组名），否则存量组无法保存更新。
    /// </summary>
    private static bool IsValidKey(string key)
        => System.Text.RegularExpressions.Regex.IsMatch(key, "^[A-Za-z0-9][A-Za-z0-9_]*$");

    private static IQueryable<ColdRollMachineGroupConfig> ApplySort(IQueryable<ColdRollMachineGroupConfig> query, string? sortBy, bool isDescending)
    {
        var key = string.IsNullOrEmpty(sortBy) ? "displayorder" : sortBy.ToLowerInvariant();
        return key switch
        {
            "groupkey" => isDescending ? query.OrderByDescending(g => g.GroupKey) : query.OrderBy(g => g.GroupKey),
            "displayname" => isDescending ? query.OrderByDescending(g => g.DisplayName) : query.OrderBy(g => g.DisplayName),
            "supplytargetgroupkey" => isDescending ? query.OrderByDescending(g => g.SupplyTargetGroupKey) : query.OrderBy(g => g.SupplyTargetGroupKey),
            "updatedtime" => isDescending ? query.OrderByDescending(g => g.UpdatedTime) : query.OrderBy(g => g.UpdatedTime),
            _ => isDescending ? query.OrderByDescending(g => g.DisplayOrder) : query.OrderBy(g => g.DisplayOrder),
        };
    }

    private static ColdRollMachineGroupConfigDto ToDto(ColdRollMachineGroupConfig entity)
    {
        return new ColdRollMachineGroupConfigDto
        {
            Id = entity.Id,
            GroupKey = entity.GroupKey,
            DisplayName = entity.DisplayName,
            ProcessKeys = SplitRawKeys(entity.ProcessKeys),
            DisplayOrder = entity.DisplayOrder,
            SupplyTargetGroupKey = entity.SupplyTargetGroupKey,
            Remark = entity.Remark,
            UpdatedTime = entity.UpdatedTime.DateTime,
        };
    }
}
