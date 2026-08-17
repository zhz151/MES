using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.DTOs.Configuration;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Configuration;
using MES.Data;
using MES.Data.Entities.Configuration;
using System.Text.RegularExpressions;

namespace MES.Services.Configuration;

/// <summary>
/// 质量证明书打印配置服务：管理页眉企业信息/标题/页脚说明/字体字号（Key→Value 键值对），
/// 数据库全局共享（仿 ProcessCardStyleDefinition 模式）。
/// </summary>
public class CertificatePrintSettingService : ICertificatePrintSettingService
{
    private const string MapCacheKey = "CertificatePrintSetting:Map";

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public CertificatePrintSettingService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<CertificatePrintSettingDto>> GetAllAsync()
    {
        return await _context.CertificatePrintSettings
            .AsNoTracking()
            .OrderBy(x => x.Key)
            .Select(x => new CertificatePrintSettingDto
            {
                Id = x.Id,
                Key = x.Key,
                Value = x.Value,
                DisplayName = x.DisplayName,
                Remark = x.Remark
            })
            .ToListAsync();
    }

    /// <summary>配置映射：Key → Value（打印链路覆盖企业信息/页脚/字体用），IMemoryCache 5 分钟。</summary>
    public async Task<Dictionary<string, string>> GetSettingMapAsync()
    {
        return (await _cache.GetOrCreateAsync(MapCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var rows = await GetAllAsync();
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
                map[row.Key] = row.Value;
            return map;
        }))!;
    }

    /// <summary>批量新增/更新（锚点 Key），校验后写入并清缓存，返回写入行数。</summary>
    public async Task<int> SaveAllAsync(List<CertificatePrintSettingDto> items)
    {
        if (items == null || items.Count == 0)
            throw new BusinessException("配置列表不能为空");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dto in items)
        {
            if (string.IsNullOrWhiteSpace(dto.Key))
                throw new BusinessException("配置键不能为空");
            if (!IsValidKey(dto.Key))
                throw new BusinessException($"配置键「{dto.Key}」格式不正确：须字母开头，仅含字母/数字/下划线");
            if (string.IsNullOrWhiteSpace(dto.Value))
                throw new BusinessException($"配置「{dto.Key}」值不能为空");
            if (string.IsNullOrWhiteSpace(dto.DisplayName))
                throw new BusinessException($"配置「{dto.Key}」显示名不能为空");
            if (dto.Key.Length > 50 || dto.DisplayName.Length > 50)
                throw new BusinessException("配置键/显示名不能超过 50 字符");
            if (dto.Value.Length > 500)
                throw new BusinessException("配置值不能超过 500 字符");

            if (!seen.Add(dto.Key))
                throw new BusinessException($"配置列表中存在重复锚点：{dto.Key}");
        }

        var existing = await _context.CertificatePrintSettings.ToListAsync();
        var byKey = existing.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var dto in items)
        {
            if (byKey.TryGetValue(dto.Key, out var entity))
            {
                // 锚点行已存在：仅更新可调字段（Value/DisplayName/Remark）
                entity.Value = dto.Value;
                entity.DisplayName = dto.DisplayName;
                entity.Remark = dto.Remark;
            }
            else
            {
                _context.CertificatePrintSettings.Add(new CertificatePrintSetting
                {
                    Key = dto.Key,
                    Value = dto.Value,
                    DisplayName = dto.DisplayName,
                    Remark = dto.Remark
                });
            }
        }

        var written = await _context.SaveChangesAsync();
        _cache.Remove(MapCacheKey);
        return written;
    }

    /// <summary>配置键格式校验：字母开头，仅含字母/数字/下划线</summary>
    private static bool IsValidKey(string key)
        => Regex.IsMatch(key, "^[A-Za-z][A-Za-z0-9_]*$");
}
