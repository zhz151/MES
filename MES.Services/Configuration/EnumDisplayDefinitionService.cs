using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
/// 枚举显示配置服务：管理 C# 强类型枚举的中文显示名与排序（不改值域）。
/// 显示名配置表优先 → EnumHelper 静态字典兜底；GetDisplayMapAsync 返回完整映射供前端/后端填充覆盖。
/// </summary>
public class EnumDisplayDefinitionService : IEnumDisplayDefinitionService
{
    private const string MapCacheKey = "EnumDisplayDefinition:Map";
    private const string OptionsCacheKey = "EnumDisplayDefinition:Options";

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public EnumDisplayDefinitionService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PagedResult<EnumDisplayDefinitionDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.EnumDisplayDefinitions
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
                    w.EnumKey.Contains(keyword) ||
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
            .Select(w => new EnumDisplayDefinitionDto
            {
                Id = w.Id,
                EnumKey = w.EnumKey,
                Value = w.Value,
                DisplayName = w.DisplayName,
                DisplayOrder = w.DisplayOrder,
                Remark = w.Remark
            })
            .ToListAsync();

        return new PagedResult<EnumDisplayDefinitionDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<EnumDisplayDefinitionDto?> GetByIdAsync(int id)
    {
        var entity = await _context.EnumDisplayDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);

        if (entity == null)
            throw new BusinessException("枚举显示配置不存在");

        return new EnumDisplayDefinitionDto
        {
            Id = entity.Id,
            EnumKey = entity.EnumKey,
            Value = entity.Value,
            DisplayName = entity.DisplayName,
            DisplayOrder = entity.DisplayOrder,
            Remark = entity.Remark
        };
    }

    public async Task<bool> SaveAsync(EnumDisplayDefinitionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.EnumKey) || string.IsNullOrWhiteSpace(dto.Value) || string.IsNullOrWhiteSpace(dto.DisplayName))
            throw new BusinessException("枚举标识、枚举值与中文显示不能为空");
        if (!ContainsChinese(dto.DisplayName))
            throw new BusinessException($"枚举「{dto.Value}」的中文显示「{dto.DisplayName}」必须包含汉字");

        if (dto.EnumKey.Length > 50 || dto.Value.Length > 50)
            throw new BusinessException("枚举标识/枚举值不能超过 50 字符");
        if (!IsValidKey(dto.Value))
            throw new BusinessException($"枚举值「{dto.Value}」格式不正确：须字母开头，仅含字母/数字/下划线");

        // 唯一性校验（EnumKey+Value，忽略自身）
        // SQL Server 默认 collation case-insensitive，== 即忽略大小写；string.Equals(...,StringComparison) 无法被 EF 翻译
        var duplicate = await _context.EnumDisplayDefinitions
            .AnyAsync(w => w.Id != dto.Id
                && w.EnumKey == dto.EnumKey
                && w.Value == dto.Value);
        if (duplicate)
            throw new BusinessException($"枚举「{dto.EnumKey}」的值「{dto.Value}」已存在");

        if (dto.Id > 0)
        {
            var entity = await _context.EnumDisplayDefinitions
                .FirstOrDefaultAsync(w => w.Id == dto.Id);
            if (entity == null)
                throw new BusinessException("枚举显示配置不存在");

            // 锚点字段（EnumKey+Value）不可改：改动会导致配置行与 C# 枚举成员失配，
            // 覆盖失效且下拉出现无效选项。仅允许改中文显示、排序与备注。
            if (!string.Equals(dto.EnumKey, entity.EnumKey, StringComparison.Ordinal)
                || !string.Equals(dto.Value, entity.Value, StringComparison.Ordinal))
                throw new BusinessException("枚举标识与枚举值（锚点）不可修改，仅可改中文显示、排序与备注");

            entity.DisplayName = dto.DisplayName;
            entity.DisplayOrder = dto.DisplayOrder;
            entity.Remark = dto.Remark;
        }
        else
        {
            var entity = new EnumDisplayDefinition
            {
                EnumKey = dto.EnumKey,
                Value = dto.Value,
                DisplayName = dto.DisplayName,
                DisplayOrder = dto.DisplayOrder,
                Remark = dto.Remark
            };
            _context.EnumDisplayDefinitions.Add(entity);
        }

        await _context.SaveChangesAsync();
        _cache.Remove(MapCacheKey);
        _cache.Remove(OptionsCacheKey);
        await RefreshStaticSnapshotAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.EnumDisplayDefinitions
            .FirstOrDefaultAsync(w => w.Id == id);
        if (entity == null)
            throw new BusinessException("枚举显示配置不存在");

        _context.EnumDisplayDefinitions.Remove(entity);
        await _context.SaveChangesAsync();
        _cache.Remove(MapCacheKey);
        _cache.Remove(OptionsCacheKey);
        await RefreshStaticSnapshotAsync();
        return true;
    }

    /// <summary>
    /// 全量显示映射：EnumKey → Value → DisplayName。
    /// 配置表优先，静态 EnumHelper 兜底补齐未配置的枚举值（保证全量覆盖）。
    /// </summary>
    public async Task<Dictionary<string, Dictionary<string, string>>> GetDisplayMapAsync()
    {
        return (await _cache.GetOrCreateAsync(MapCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var map = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            // 配置表全量
            var rows = await _context.EnumDisplayDefinitions
                .AsNoTracking()
                .ToListAsync();
            foreach (var row in rows)
            {
                if (!map.TryGetValue(row.EnumKey, out var inner))
                {
                    inner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    map[row.EnumKey] = inner;
                }
                inner[row.Value] = row.DisplayName;
            }

            // 兜底：静态 EnumHelper 未配置的枚举值
            foreach (var kvp in EnumHelper.GetAllMappings())
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

    /// <summary>
    /// 全量显示选项：EnumKey → 有序 (Value/DisplayName/DisplayOrder)。
    /// 配置表行按 DisplayOrder 升序；未配置的静态兜底值按注册顺序追加到末尾。
    /// </summary>
    public async Task<Dictionary<string, List<EnumDisplayOptionDto>>> GetOptionsMapAsync()
    {
        return (await _cache.GetOrCreateAsync(OptionsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var map = new Dictionary<string, List<EnumDisplayOptionDto>>(StringComparer.OrdinalIgnoreCase);

            var rows = await _context.EnumDisplayDefinitions
                .AsNoTracking()
                .OrderBy(x => x.EnumKey)
                .ThenBy(x => x.DisplayOrder)
                .ToListAsync();
            foreach (var row in rows)
            {
                if (!map.TryGetValue(row.EnumKey, out var list))
                {
                    list = new List<EnumDisplayOptionDto>();
                    map[row.EnumKey] = list;
                }
                list.Add(new EnumDisplayOptionDto
                {
                    Value = row.Value,
                    DisplayName = row.DisplayName,
                    DisplayOrder = row.DisplayOrder
                });
            }

            // 兜底：静态 EnumHelper 未配置的枚举值追加到末尾（按注册顺序）
            foreach (var kvp in EnumHelper.GetAllMappings())
            {
                if (!map.TryGetValue(kvp.Key, out var list))
                {
                    list = new List<EnumDisplayOptionDto>();
                    map[kvp.Key] = list;
                }
                var existing = list.Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var order = list.Count + 1;
                foreach (var v in kvp.Value)
                {
                    if (!existing.Contains(v.Key))
                    {
                        list.Add(new EnumDisplayOptionDto { Value = v.Key, DisplayName = v.Value, DisplayOrder = order++ });
                    }
                }
            }

            return map;
        }))!;
    }

    public async Task<int> RestoreDefaultsAsync(string enumKey)
    {
        var defaults = EnumHelper.GetAllMappings();
        if (string.IsNullOrEmpty(enumKey) || !defaults.TryGetValue(enumKey, out var map))
            return 0; // 未注册枚举无静态默认

        var existingValues = await _context.EnumDisplayDefinitions
            .AsNoTracking()
            .Where(x => x.EnumKey == enumKey)
            .Select(x => x.Value)
            .ToListAsync();
        var existing = existingValues.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var order = 0;
        var rows = map
            .Where(kvp => !existing.Contains(kvp.Key))
            .Select(kvp => new EnumDisplayDefinition
            {
                EnumKey = enumKey,
                Value = kvp.Key,
                DisplayName = kvp.Value,
                DisplayOrder = ++order
            })
            .ToList();

        if (rows.Count == 0) return 0;

        _context.EnumDisplayDefinitions.AddRange(rows);
        await _context.SaveChangesAsync();
        _cache.Remove(MapCacheKey);
        _cache.Remove(OptionsCacheKey);
        await RefreshStaticSnapshotAsync();
        return rows.Count;
    }

    /// <summary>
    /// 配置写操作后刷新进程内静态快照：重建 display-map/options-map 并重新注入 EnumHelper，
    /// 使后端打印/DataExchange 保存即生效，无需重启 API（与前端 MainLayout 每次加载注入对齐）。
    /// 注：需在清缓存后调用，GetDisplayMapAsync/GetOptionsMapAsync 才会重查配置表。
    /// </summary>
    private async Task RefreshStaticSnapshotAsync()
    {
        var map = await GetDisplayMapAsync();
        foreach (var kvp in map)
            EnumHelper.ApplyEnumOverrides(kvp.Key, kvp.Value);

        var options = await GetOptionsMapAsync();
        foreach (var kvp in options)
        {
            var order = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var opt in kvp.Value)
                order[opt.Value] = opt.DisplayOrder;
            EnumHelper.ApplyEnumOrder(kvp.Key, order);
        }
    }

    /// <summary>枚举值名格式校验：字母开头，仅含字母/数字/下划线（对齐 C# 标识符契约）</summary>
    private static bool IsValidKey(string key)
        => Regex.IsMatch(key, "^[A-Za-z][A-Za-z0-9_]*$");

    /// <summary>中文显示名校验：必须包含至少一个汉字（CJK 统一表意文字），杜绝英文/空白/纯 ASCII 显示名</summary>
    private static bool ContainsChinese(string value)
        => value.Any(c => c >= 0x4E00 && c <= 0x9FFF);
}
