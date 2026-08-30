using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Core.Interfaces.Configuration;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Helpers;
using System.Text.RegularExpressions;

namespace MES.Services.Configuration;

/// <summary>
/// 字典值配置服务：管理 string 存储字典字段（工段/工序/紧急度/产类/流转/关注目标/汇总行/责任类别）
/// 的中文显示名、排序、隐藏与可加值。
/// 显示名配置表优先 → 各 Keys 常量类兜底；IsEnabled=false 隐藏（下拉/筛选不出现）。
/// </summary>
public class DictValueDefinitionService : IDictValueDefinitionService
{
    private const string MapCacheKey = "DictValueDefinition:Map";

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public DictValueDefinitionService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PagedResult<DictValueDefinitionDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.DictValueDefinitions
            .AsNoTracking()
            .AsQueryable();

        // 关键字模糊搜索
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keywords = query.Keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var kw in keywords)
            {
                var keyword = kw;
                queryable = queryable.Where(w =>
                    w.DictKey.Contains(keyword) ||
                    w.Value.Contains(keyword) ||
                    w.DisplayName.Contains(keyword) ||
                    (w.Remark != null && w.Remark.Contains(keyword)));
            }
        }

        // 通用筛选
        queryable = queryable.ApplyFilters(query.Filters);

        // 排序
        var sortBy = string.IsNullOrEmpty(query.SortBy) || query.SortBy.Equals("CreatedTime", StringComparison.OrdinalIgnoreCase)
            ? "DisplayOrder"
            : query.SortBy;
        queryable = queryable.ApplySort(sortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(w => new DictValueDefinitionDto
            {
                Id = w.Id,
                DictKey = w.DictKey,
                Value = w.Value,
                DisplayName = w.DisplayName,
                DisplayOrder = w.DisplayOrder,
                IsEnabled = w.IsEnabled,
                Remark = w.Remark
            })
            .ToListAsync();

        return new PagedResult<DictValueDefinitionDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    /// <summary>
    /// 列筛选上下文：返回可筛列的 DISTINCT 值（DictKey/Value/DisplayName/IsEnabled/Remark），供前端列头 ExcelFilter 下拉加载。
    /// IsEnabled 返回 "True"/"False"，前端显示「启用/隐藏」。
    /// </summary>
    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var rows = await _context.DictValueDefinitions
            .AsNoTracking()
            .Select(x => new { x.DictKey, x.Value, x.DisplayName, x.IsEnabled, x.Remark })
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["DictKey"] = rows.Select(x => x.DictKey).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList(),
            ["Value"] = rows.Select(x => x.Value).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList(),
            ["DisplayName"] = rows.Select(x => x.DisplayName).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList(),
            ["IsEnabled"] = rows.Select(x => x.IsEnabled.ToString()).Distinct().OrderBy(x => x).ToList(),
            ["Remark"] = rows.Select(x => x.Remark).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct().OrderBy(x => x).ToList()
        };
    }

    public async Task<DictValueDefinitionDto?> GetByIdAsync(int id)
    {
        var entity = await _context.DictValueDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);

        if (entity == null)
            throw new BusinessException("字典值配置不存在");

        return new DictValueDefinitionDto
        {
            Id = entity.Id,
            DictKey = entity.DictKey,
            Value = entity.Value,
            DisplayName = entity.DisplayName,
            DisplayOrder = entity.DisplayOrder,
            IsEnabled = entity.IsEnabled,
            Remark = entity.Remark
        };
    }

    public async Task<bool> SaveAsync(DictValueDefinitionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.DictKey) || string.IsNullOrWhiteSpace(dto.Value) || string.IsNullOrWhiteSpace(dto.DisplayName))
            throw new BusinessException("字典标识、字典值与中文显示不能为空");
        if (!ContainsChinese(dto.DisplayName))
            throw new BusinessException($"字典「{dto.Value}」的中文显示「{dto.DisplayName}」必须包含汉字");

        if (dto.DictKey.Length > 50 || dto.Value.Length > 50)
            throw new BusinessException("字典标识/字典值不能超过 50 字符");
        if (!IsValidKey(dto.Value))
            throw new BusinessException($"字典值「{dto.Value}」格式不正确：须字母开头，仅含字母/数字/下划线");

        // 唯一性校验（DictKey+Value，忽略自身）
        // SQL Server 默认 collation case-insensitive，== 即忽略大小写；string.Equals(...,StringComparison) 无法被 EF 翻译
        var duplicate = await _context.DictValueDefinitions
            .AnyAsync(w => w.Id != dto.Id
                && w.DictKey == dto.DictKey
                && w.Value == dto.Value);
        if (duplicate)
            throw new BusinessException($"字典「{dto.DictKey}」的值「{dto.Value}」已存在");

        if (dto.Id > 0)
        {
            var entity = await _context.DictValueDefinitions
                .FirstOrDefaultAsync(w => w.Id == dto.Id);
            if (entity == null)
                throw new BusinessException("字典值配置不存在");

            // 锚点字段（DictKey+Value）不可改：改动会导致存储层引用的旧 Key 与配置失配，
            // 覆盖失效、下拉出现无效项。加值请新增行，而非改已有行。仅允许改中文显示、排序、启用与备注。
            if (!string.Equals(dto.DictKey, entity.DictKey, StringComparison.Ordinal)
                || !string.Equals(dto.Value, entity.Value, StringComparison.Ordinal))
                throw new BusinessException("字典标识与字典值（锚点）不可修改，仅可改中文显示、排序、启用与备注");

            entity.DisplayName = dto.DisplayName;
            entity.DisplayOrder = dto.DisplayOrder;
            entity.IsEnabled = dto.IsEnabled;
            entity.Remark = dto.Remark;
        }
        else
        {
            var entity = new DictValueDefinition
            {
                DictKey = dto.DictKey,
                Value = dto.Value,
                DisplayName = dto.DisplayName,
                DisplayOrder = dto.DisplayOrder,
                IsEnabled = dto.IsEnabled,
                Remark = dto.Remark
            };
            _context.DictValueDefinitions.Add(entity);
        }

        await _context.SaveChangesAsync();
        _cache.Remove(MapCacheKey);
        await RefreshStaticSnapshotAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.DictValueDefinitions
            .FirstOrDefaultAsync(w => w.Id == id);
        if (entity == null)
            throw new BusinessException("字典值配置不存在");

        _context.DictValueDefinitions.Remove(entity);
        await _context.SaveChangesAsync();
        _cache.Remove(MapCacheKey);
        await RefreshStaticSnapshotAsync();
        return true;
    }

    /// <summary>
    /// 全量显示映射：DictKey → Value → DisplayName。
    /// 配置表优先，静态 Keys 常量类兜底补齐未配置的值（保证全量覆盖）。
    /// </summary>
    public async Task<Dictionary<string, Dictionary<string, string>>> GetDisplayMapAsync()
    {
        return (await _cache.GetOrCreateAsync(MapCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;

            var map = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            // 配置表全量
            var rows = await _context.DictValueDefinitions
                .AsNoTracking()
                .ToListAsync();
            foreach (var row in rows)
            {
                if (!map.TryGetValue(row.DictKey, out var inner))
                {
                    inner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    map[row.DictKey] = inner;
                }
                inner[row.Value] = row.DisplayName;
            }

            // 兜底：静态 Keys 常量类未配置的值
            foreach (var kvp in DictValueDefaults.All)
            {
                if (!map.TryGetValue(kvp.Key, out var inner))
                {
                    inner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    map[kvp.Key] = inner;
                }
                foreach (var v in kvp.Value)
                {
                    inner.TryAdd(v.Key, v.Value);
                }
            }

            return map;
        }))!;
    }

    public async Task<int> RestoreDefaultsAsync(string dictKey)
    {
        if (string.IsNullOrEmpty(dictKey) || !DictValueDefaults.All.TryGetValue(dictKey, out var map))
            return 0; // 未注册字典无静态默认

        var existingValues = await _context.DictValueDefinitions
            .AsNoTracking()
            .Where(x => x.DictKey == dictKey)
            .Select(x => x.Value)
            .ToListAsync();
        var existing = existingValues.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var order = 0;
        var rows = map
            .Where(kvp => !existing.Contains(kvp.Key))
            .Select(kvp => new DictValueDefinition
            {
                DictKey = dictKey,
                Value = kvp.Key,
                DisplayName = kvp.Value,
                DisplayOrder = ++order,
                IsEnabled = true
            })
            .ToList();

        if (rows.Count == 0) return 0;

        _context.DictValueDefinitions.AddRange(rows);
        await _context.SaveChangesAsync();
        _cache.Remove(MapCacheKey);
        await RefreshStaticSnapshotAsync();
        return rows.Count;
    }

    /// <summary>
    /// 配置写操作后刷新进程内静态快照：重建 display-map 并重新注入 DictValueDisplayHelper.OverrideMap，
    /// 使后端打印/DataExchange 保存即生效，无需重启 API（与前端 MainLayout 每次加载注入对齐）。
    /// 注：需在清缓存后调用，GetDisplayMapAsync 才会重查配置表。
    /// </summary>
    private async Task RefreshStaticSnapshotAsync()
    {
        DictValueDisplayHelper.OverrideMap = await GetDisplayMapAsync();
    }

    /// <summary>
    /// 启用字典值列表：配置表 IsEnabled=true 按 DisplayOrder 升序；配置表中不存在（含被隐藏）的静态值追加末尾。
    /// </summary>
    public async Task<List<DictValueInfoDto>> GetEnabledValuesAsync(string dictKey)
    {
        if (string.IsNullOrEmpty(dictKey))
            return new List<DictValueInfoDto>();

        var rows = await _context.DictValueDefinitions
            .AsNoTracking()
            .Where(w => w.DictKey == dictKey)
            .Select(w => new { w.Value, w.DisplayName, w.DisplayOrder, w.IsEnabled })
            .ToListAsync();

        // 配置表中已存在的值（含被隐藏），静态兜底不重复追加
        var configuredValues = rows.Select(r => r.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = rows
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.DisplayOrder)
            .Select(r => new DictValueInfoDto
            {
                Value = r.Value,
                DisplayName = r.DisplayName,
                DisplayOrder = r.DisplayOrder,
                IsEnabled = r.IsEnabled
            })
            .ToList();

        // 静态兜底：配置表中不存在（含被隐藏）的内置值追加末尾，保证内置 Key 全覆盖
        if (DictValueDefaults.All.TryGetValue(dictKey, out var map))
        {
            foreach (var kvp in map)
            {
                if (configuredValues.Contains(kvp.Key)) continue;
                result.Add(new DictValueInfoDto
                {
                    Value = kvp.Key,
                    DisplayName = kvp.Value,
                    DisplayOrder = int.MaxValue,
                    IsEnabled = true
                });
            }
        }

        return result;
    }

    /// <summary>字典值格式校验：字母开头，仅含字母/数字/下划线（对齐 Keys 常量契约）</summary>
    private static bool IsValidKey(string key)
        => Regex.IsMatch(key, "^[A-Za-z][A-Za-z0-9_]*$");

    /// <summary>中文显示名校验：必须包含至少一个汉字（CJK 统一表意文字），杜绝英文/空白/纯 ASCII 显示名</summary>
    private static bool ContainsChinese(string value)
        => value.Any(c => c >= 0x4E00 && c <= 0x9FFF);
}
