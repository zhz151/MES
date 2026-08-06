using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Configuration;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Helpers;

namespace MES.Services.Configuration;

/// <summary>
/// 工序组定义服务：配置表管理（分页/增删改）+ 显示名双向映射 + 冷轧类判定集合。
/// 显示名优先取配置表 ProcessDefinitions.ProcessName，兜底 ProcessNames 规范中文。
/// </summary>
public class ProcessDefinitionService : IProcessDefinitionService
{
    private const string MapCacheKey = "ProcessNameDisplay:Map";
    private const string ColdRollCacheKey = "ProcessDefinition:ColdRollKeys";
    private const string ColdRollOrDrawCacheKey = "ProcessDefinition:ColdRollOrDrawKeys";

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public ProcessDefinitionService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PagedResult<ProcessDefinitionDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.ProcessDefinitions
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
                    w.ProcessKey.Contains(keyword) ||
                    w.ProcessName.Contains(keyword) ||
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
            .Select(w => new ProcessDefinitionDto
            {
                Id = w.Id,
                ProcessKey = w.ProcessKey,
                ProcessName = w.ProcessName,
                DisplayOrder = w.DisplayOrder,
                IsEnabled = w.IsEnabled,
                IsColdRoll = w.IsColdRoll,
                IsColdDraw = w.IsColdDraw,
                Remark = w.Remark
            })
            .ToListAsync();

        return new PagedResult<ProcessDefinitionDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<ProcessDefinitionDto?> GetByIdAsync(int id)
    {
        var entity = await _context.ProcessDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);

        if (entity == null)
            throw new BusinessException("工序组定义不存在");

        return new ProcessDefinitionDto
        {
            Id = entity.Id,
            ProcessKey = entity.ProcessKey,
            ProcessName = entity.ProcessName,
            DisplayOrder = entity.DisplayOrder,
            IsEnabled = entity.IsEnabled,
            IsColdRoll = entity.IsColdRoll,
            IsColdDraw = entity.IsColdDraw,
            Remark = entity.Remark
        };
    }

    public async Task<bool> SaveAsync(ProcessDefinitionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ProcessKey) || string.IsNullOrWhiteSpace(dto.ProcessName))
            throw new BusinessException("工序 Key 与工序名称不能为空");

        // 格式校验：字母开头，仅含字母/数字/下划线（程序识别契约，禁中文/空格/特殊字符）
        if (dto.ProcessKey.Length > 50)
            throw new BusinessException("工序 Key 不能超过 50 字符");
        if (!IsValidKey(dto.ProcessKey))
            throw new BusinessException($"工序 Key「{dto.ProcessKey}」格式不正确：须字母开头，仅含字母/数字/下划线");

        // 唯一性校验（ProcessKey 全局唯一，忽略自身）
        var duplicate = await _context.ProcessDefinitions
            .AnyAsync(w => w.Id != dto.Id && string.Equals(w.ProcessKey, dto.ProcessKey, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
            throw new BusinessException($"工序 Key「{dto.ProcessKey}」已存在");

        if (dto.Id > 0)
        {
            // 更新
            var entity = await _context.ProcessDefinitions
                .FirstOrDefaultAsync(w => w.Id == dto.Id);
            if (entity == null)
                throw new BusinessException("工序组定义不存在");

            entity.ProcessKey = dto.ProcessKey;
            entity.ProcessName = dto.ProcessName;
            entity.DisplayOrder = dto.DisplayOrder;
            entity.IsEnabled = dto.IsEnabled;
            entity.IsColdRoll = dto.IsColdRoll;
            entity.IsColdDraw = dto.IsColdDraw;
            entity.Remark = dto.Remark;
        }
        else
        {
            // 新增
            var entity = new ProcessDefinition
            {
                ProcessKey = dto.ProcessKey,
                ProcessName = dto.ProcessName,
                DisplayOrder = dto.DisplayOrder,
                IsEnabled = dto.IsEnabled,
                IsColdRoll = dto.IsColdRoll,
                IsColdDraw = dto.IsColdDraw,
                Remark = dto.Remark
            };
            _context.ProcessDefinitions.Add(entity);
        }

        await _context.SaveChangesAsync();
        InvalidateCaches();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.ProcessDefinitions
            .FirstOrDefaultAsync(w => w.Id == id);
        if (entity == null)
            throw new BusinessException("工序组定义不存在");

        _context.ProcessDefinitions.Remove(entity);
        await _context.SaveChangesAsync();
        InvalidateCaches();
        return true;
    }

    /// <summary>
    /// 获取启用工序列表：IsEnabled=true，按 DisplayOrder 升序。
    /// </summary>
    public async Task<List<ProcessInfoDto>> GetEnabledProcessesAsync()
    {
        return await _context.ProcessDefinitions
            .AsNoTracking()
            .Where(w => w.IsEnabled)
            .OrderBy(w => w.DisplayOrder)
            .Select(w => new ProcessInfoDto
            {
                ProcessKey = w.ProcessKey,
                ProcessName = w.ProcessName,
                DisplayOrder = w.DisplayOrder,
                IsEnabled = w.IsEnabled,
                IsColdRoll = w.IsColdRoll,
                IsColdDraw = w.IsColdDraw
            })
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetProcessNameMapAsync()
    {
        return (await _cache.GetOrCreateAsync(MapCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 配置表全量（不禁用过滤：存量记录可能仍引用已禁用工序）
            var rows = await _context.ProcessDefinitions
                .AsNoTracking()
                .ToListAsync();

            foreach (var row in rows)
            {
                if (!string.IsNullOrEmpty(row.ProcessKey) && !string.IsNullOrEmpty(row.ProcessName))
                    map[row.ProcessKey] = row.ProcessName;
            }

            // 兜底：ProcessNames 规范中文，保证 9 Key 全覆盖
            foreach (var kvp in ProcessKeys.KeyToChinese)
            {
                map.TryAdd(kvp.Key, kvp.Value);
            }

            return (IReadOnlyDictionary<string, string>)map;
        }))!;
    }

    public async Task<string?> ToDisplayAsync(string? keyOrName)
    {
        if (string.IsNullOrEmpty(keyOrName)) return null;
        if (ProcessKeys.IsKey(keyOrName))
        {
            var map = await GetProcessNameMapAsync();
            return map.TryGetValue(keyOrName, out var cn) ? cn : ProcessKeys.ToChinese(keyOrName);
        }
        // 已是中文（迁移前存量）原样返回
        return keyOrName;
    }

    public Task<string?> ToKeyAsync(string? nameOrKey)
        => Task.FromResult(ProcessKeys.ToKey(nameOrKey));

    public async Task<HashSet<string>> GetColdRollKeysAsync()
    {
        return (await _cache.GetOrCreateAsync(ColdRollCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var keys = await _context.ProcessDefinitions
                .AsNoTracking()
                .Where(w => w.IsColdRoll)
                .Select(w => w.ProcessKey)
                .ToListAsync();
            return new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        }))!;
    }

    public async Task<HashSet<string>> GetColdRollOrDrawKeysAsync()
    {
        return (await _cache.GetOrCreateAsync(ColdRollOrDrawCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var keys = await _context.ProcessDefinitions
                .AsNoTracking()
                .Where(w => w.IsColdRoll || w.IsColdDraw)
                .Select(w => w.ProcessKey)
                .ToListAsync();
            return new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        }))!;
    }

    private void InvalidateCaches()
    {
        _cache.Remove(MapCacheKey);
        _cache.Remove(ColdRollCacheKey);
        _cache.Remove(ColdRollOrDrawCacheKey);
    }

    /// <summary>稳定 Key 格式校验：字母开头，仅含字母/数字/下划线（程序识别契约，禁中文/空格/特殊字符）</summary>
    private static bool IsValidKey(string key)
        => System.Text.RegularExpressions.Regex.IsMatch(key, "^[A-Za-z][A-Za-z0-9_]*$");
}
