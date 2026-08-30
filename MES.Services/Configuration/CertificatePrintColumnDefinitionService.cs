using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Configuration;
using MES.Data;
using MES.Data.Entities.Configuration;
using System.Text.RegularExpressions;

namespace MES.Services.Configuration;

/// <summary>
/// 质量证明书打印列布局配置服务：管理明细表（物料/化学/检验检测）每个打印字段的显示配置
/// （启用/列顺序/列宽权重），数据库全局共享（仿 ProcessCardColumnDefinition 模式）。
/// 锚点 = BlockKey + FieldKey，区块顺序固定（Material→Chemistry→Inspection）。
/// </summary>
public class CertificatePrintColumnDefinitionService : ICertificatePrintColumnDefinitionService
{
    private const string MapCacheKey = "CertificatePrintColumnDefinition:Map";

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public CertificatePrintColumnDefinitionService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<CertificatePrintColumnDefinitionDto>> GetAllAsync()
    {
        return await _context.CertificatePrintColumnDefinitions
            .AsNoTracking()
            .OrderBy(x => x.BlockKey)
            .ThenBy(x => x.ColumnIndex)
            .Select(x => new CertificatePrintColumnDefinitionDto
            {
                Id = x.Id,
                BlockKey = x.BlockKey,
                FieldKey = x.FieldKey,
                Label = x.Label,
                LabelEn = x.LabelEn,
                Visible = x.Visible,
                ColumnIndex = x.ColumnIndex,
                ColumnWeight = x.ColumnWeight
            })
            .ToListAsync();
    }

    /// <summary>
    /// 配置映射：$"{BlockKey}|{FieldKey}" → 配置 DTO（打印链路覆盖默认列定义用），IMemoryCache 5 分钟。
    /// </summary>
    public async Task<Dictionary<string, CertificatePrintColumnDefinitionDto>> GetConfigMapAsync()
    {
        return (await _cache.GetOrCreateAsync(MapCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;

            var rows = await GetAllAsync();
            var map = new Dictionary<string, CertificatePrintColumnDefinitionDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
                map[$"{row.BlockKey}|{row.FieldKey}"] = row;
            return map;
        }))!;
    }

    /// <summary>
    /// 批量新增/更新（锚点 BlockKey+FieldKey），校验后写入并清缓存，返回写入行数。
    /// 请求列表外的存量行保留；同锚点仅在列表内唯一（重复即报错）。
    /// </summary>
    public async Task<int> SaveAllAsync(List<CertificatePrintColumnDefinitionDto> items)
    {
        if (items == null || items.Count == 0)
            throw new BusinessException("配置列表不能为空");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dto in items)
        {
            if (string.IsNullOrWhiteSpace(dto.BlockKey) || string.IsNullOrWhiteSpace(dto.FieldKey))
                throw new BusinessException("区块与字段标识不能为空");
            if (!IsValidKey(dto.BlockKey) || !IsValidKey(dto.FieldKey))
                throw new BusinessException($"字段「{dto.FieldKey}」标识格式不正确：须字母开头，仅含字母/数字/下划线");
            if (string.IsNullOrWhiteSpace(dto.Label))
                throw new BusinessException($"字段「{dto.FieldKey}」显示名不能为空");
            if (dto.BlockKey.Length > 50 || dto.FieldKey.Length > 50 || dto.Label.Length > 50)
                throw new BusinessException("区块/字段/显示名不能超过 50 字符");
            if (!string.IsNullOrEmpty(dto.LabelEn) && dto.LabelEn.Length > 50)
                throw new BusinessException($"字段「{dto.FieldKey}」英文显示名不能超过 50 字符");
            if (dto.ColumnIndex < 1 || dto.ColumnWeight < 1)
                throw new BusinessException($"字段「{dto.FieldKey}」列顺序/列宽权重必须为正整数");

            var key = $"{dto.BlockKey}|{dto.FieldKey}";
            if (!seen.Add(key))
                throw new BusinessException($"配置列表中存在重复锚点：{key}");
        }

        var existing = await _context.CertificatePrintColumnDefinitions.ToListAsync();
        var byKey = existing.ToDictionary(
            x => $"{x.BlockKey}|{x.FieldKey}",
            StringComparer.OrdinalIgnoreCase);

        foreach (var dto in items)
        {
            var key = $"{dto.BlockKey}|{dto.FieldKey}";
            if (byKey.TryGetValue(key, out var entity))
            {
                // 锚点行已存在：仅更新可调字段（Label/LabelEn/Visible/ColumnIndex/ColumnWeight）
                entity.Label = dto.Label;
                entity.LabelEn = dto.LabelEn;
                entity.Visible = dto.Visible;
                entity.ColumnIndex = dto.ColumnIndex;
                entity.ColumnWeight = dto.ColumnWeight;
            }
            else
            {
                _context.CertificatePrintColumnDefinitions.Add(new CertificatePrintColumnDefinition
                {
                    BlockKey = dto.BlockKey,
                    FieldKey = dto.FieldKey,
                    Label = dto.Label,
                    LabelEn = dto.LabelEn,
                    Visible = dto.Visible,
                    ColumnIndex = dto.ColumnIndex,
                    ColumnWeight = dto.ColumnWeight
                });
            }
        }

        var written = await _context.SaveChangesAsync();
        _cache.Remove(MapCacheKey);
        return written;
    }

    /// <summary>锚点标识格式校验：字母开头，仅含字母/数字/下划线（对齐 C# 标识符契约）</summary>
    private static bool IsValidKey(string key)
        => Regex.IsMatch(key, "^[A-Za-z][A-Za-z0-9_]*$");
}
