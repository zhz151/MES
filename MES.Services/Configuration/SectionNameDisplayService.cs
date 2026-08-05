using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;
using MES.Core.Interfaces.Configuration;
using MES.Data;
using MES.Data.Entities.Configuration;

namespace MES.Services.Configuration;

/// <summary>
/// 工段显示名解析服务：Key（英文，存储）↔ 中文（显示）双向转换。
/// 显示名优先取配置表 StandardWorkDays.SectionName（按 SectionKey，通用行优先），
/// 缺行/禁用时兜底 SectionDefs 规范中文，保证 26 个 Key 全覆盖。
/// </summary>
public class SectionNameDisplayService : ISectionNameDisplayService
{
    private const string CacheKey = "SectionNameDisplay:Map";

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public SectionNameDisplayService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSectionNameMapAsync()
    {
        return (await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            // 分组按 OrdinalIgnoreCase，map 也必须一致（SectionKey 大小写变体视为同 Key）
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 配置表：按 SectionKey 分组，通用行（PlantGradePrefix=null）优先
            var rows = await _context.StandardWorkDays
                .AsNoTracking()
                .Where(w => w.SectionKey != null)
                .ToListAsync();

            foreach (var group in rows.GroupBy(w => w.SectionKey!, StringComparer.OrdinalIgnoreCase))
            {
                var row = group
                    .OrderBy(x => x.PlantGradePrefix == null ? 0 : 1)
                    .ThenBy(x => x.DisplayOrder)
                    .First();
                if (!string.IsNullOrEmpty(row.SectionName))
                    map[row.SectionKey!] = row.SectionName;
            }

            // 兜底：SectionDefs 规范中文，保证 26 Key 全覆盖
            foreach (var kvp in SectionKeys.KeyToChinese)
            {
                map.TryAdd(kvp.Key, kvp.Value);
            }

            return (IReadOnlyDictionary<string, string>)map;
        }))!;
    }

    public async Task<string?> ToDisplayAsync(string? keyOrName)
    {
        if (string.IsNullOrEmpty(keyOrName)) return null;
        if (SectionKeys.IsKey(keyOrName))
        {
            var map = await GetSectionNameMapAsync();
            return map.TryGetValue(keyOrName, out var cn) ? cn : SectionKeys.ToChinese(keyOrName);
        }
        // 已是中文（迁移前存量/别名）原样返回
        return keyOrName;
    }

    public Task<string?> ToKeyAsync(string? nameOrKey)
        => Task.FromResult(SectionKeys.ToKey(nameOrKey));
}
