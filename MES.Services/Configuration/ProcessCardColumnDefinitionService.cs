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
/// 工艺卡打印列布局配置服务：管理每个打印字段的显示配置（是否启用/所属行/列顺序/列宽权重），
/// 数据库全局共享（仿 EnumDisplayDefinition 模式）。锚点 = BlockKey + FieldKey，区块顺序固定。
/// </summary>
public class ProcessCardColumnDefinitionService : IProcessCardColumnDefinitionService
{
    private const string MapCacheKey = "ProcessCardColumnDefinition:Map";

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public ProcessCardColumnDefinitionService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<ProcessCardColumnDefinitionDto>> GetAllAsync()
    {
        return await _context.ProcessCardColumnDefinitions
            .AsNoTracking()
            .OrderBy(x => x.BlockKey)
            .ThenBy(x => x.ColumnIndex)
            .Select(x => new ProcessCardColumnDefinitionDto
            {
                Id = x.Id,
                BlockKey = x.BlockKey,
                FieldKey = x.FieldKey,
                Label = x.Label,
                Visible = x.Visible,
                RowIndex = x.RowIndex,
                ColumnIndex = x.ColumnIndex,
                ColumnWeight = x.ColumnWeight
            })
            .ToListAsync();
    }

    /// <summary>
    /// 配置映射：$"{BlockKey}|{FieldKey}" → 配置 DTO（打印覆盖请求列定义用），IMemoryCache 5 分钟。
    /// </summary>
    public async Task<Dictionary<string, ProcessCardColumnDefinitionDto>> GetConfigMapAsync()
    {
        return (await _cache.GetOrCreateAsync(MapCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var rows = await GetAllAsync();
            var map = new Dictionary<string, ProcessCardColumnDefinitionDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
                map[$"{row.BlockKey}|{row.FieldKey}"] = row;
            return map;
        }))!;
    }

    /// <summary>
    /// 批量新增/更新（锚点 BlockKey+FieldKey），校验后写入并清缓存，返回写入行数。
    /// 请求列表外的存量行保留；同锚点仅在列表内唯一（重复即报错）。
    /// </summary>
    public async Task<int> SaveAllAsync(List<ProcessCardColumnDefinitionDto> items)
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
            if (dto.RowIndex < 1 || dto.ColumnIndex < 1 || dto.ColumnWeight < 1)
                throw new BusinessException($"字段「{dto.FieldKey}」所属行/列顺序/列宽权重必须为正整数");

            var key = $"{dto.BlockKey}|{dto.FieldKey}";
            if (!seen.Add(key))
                throw new BusinessException($"配置列表中存在重复锚点：{key}");
        }

        var existing = await _context.ProcessCardColumnDefinitions.ToListAsync();
        var byKey = existing.ToDictionary(
            x => $"{x.BlockKey}|{x.FieldKey}",
            StringComparer.OrdinalIgnoreCase);

        foreach (var dto in items)
        {
            var key = $"{dto.BlockKey}|{dto.FieldKey}";
            if (byKey.TryGetValue(key, out var entity))
            {
                // 锚点行已存在：仅更新可调字段（Label/Visible/RowIndex/ColumnIndex/ColumnWeight）
                entity.Label = dto.Label;
                entity.Visible = dto.Visible;
                entity.RowIndex = dto.RowIndex;
                entity.ColumnIndex = dto.ColumnIndex;
                entity.ColumnWeight = dto.ColumnWeight;
            }
            else
            {
                _context.ProcessCardColumnDefinitions.Add(new ProcessCardColumnDefinition
                {
                    BlockKey = dto.BlockKey,
                    FieldKey = dto.FieldKey,
                    Label = dto.Label,
                    Visible = dto.Visible,
                    RowIndex = dto.RowIndex,
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
