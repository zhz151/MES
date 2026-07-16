using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Interfaces.Equipment;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Materials;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.StandardRegister;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Models;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Helpers;

namespace MES.Services.Configuration;

/// <summary>
/// 标准工量天数服务
/// </summary>
public class StandardWorkDayService : IStandardWorkDayService
{
    private readonly AppDbContext _context;

    public StandardWorkDayService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<StandardWorkDayDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.StandardWorkDays
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
                    w.SectionName.Contains(keyword) ||
                    (w.PlantGradePrefix != null && w.PlantGradePrefix.Contains(keyword)) ||
                    (w.Remark != null && w.Remark.Contains(keyword)));
            }
        }

        // 通用筛选
        queryable = queryable.ApplyFilters(query.Filters);

        // 排序
        var sortBy = string.IsNullOrEmpty(query.SortBy) || query.SortBy.Equals("CreatedTime", StringComparison.OrdinalIgnoreCase)
            ? "SectionName"
            : query.SortBy;
        queryable = queryable.ApplySort(sortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(w => new StandardWorkDayDto
            {
                Id = w.Id,
                SectionName = w.SectionName,
                PlantGradePrefix = w.PlantGradePrefix,
                StandardDays = w.StandardDays,
                Remark = w.Remark
            })
            .ToListAsync();

        return new PagedResult<StandardWorkDayDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<StandardWorkDayDto?> GetByIdAsync(int id)
    {
        var entity = await _context.StandardWorkDays
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);

        if (entity == null)
            throw new BusinessException("标准工量天数配置不存在");

        return new StandardWorkDayDto
        {
            Id = entity.Id,
            SectionName = entity.SectionName,
            PlantGradePrefix = entity.PlantGradePrefix,
            StandardDays = entity.StandardDays,
            Remark = entity.Remark
        };
    }

    public async Task<bool> SaveAsync(StandardWorkDayDto dto)
    {
        if (dto.Id > 0)
        {
            // 更新
            var entity = await _context.StandardWorkDays
                .FirstOrDefaultAsync(w => w.Id == dto.Id);

            if (entity == null)
                throw new BusinessException("标准工量天数配置不存在");

            entity.SectionName = dto.SectionName;
            entity.PlantGradePrefix = dto.PlantGradePrefix;
            entity.StandardDays = dto.StandardDays;
            entity.Remark = dto.Remark;
        }
        else
        {
            // 新增
            var entity = new StandardWorkDay
            {
                SectionName = dto.SectionName,
                PlantGradePrefix = dto.PlantGradePrefix,
                StandardDays = dto.StandardDays,
                Remark = dto.Remark
            };
            _context.StandardWorkDays.Add(entity);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.StandardWorkDays
            .FirstOrDefaultAsync(w => w.Id == id);

        if (entity == null)
            throw new BusinessException("标准工量天数配置不存在");

        _context.StandardWorkDays.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 获取标准天数映射表：key=SectionName, value=StandardDays
    /// 匹配规则：先找 PlantGradePrefix 精确匹配，未找到则取通用的 null 值
    /// </summary>
    public async Task<Dictionary<string, double>> GetStandardDaysMapAsync(string? plantGrade)
    {
        var all = await _context.StandardWorkDays
            .AsNoTracking()
            .ToListAsync();

        // 按 SectionName 分组，优先取精确匹配 PlantGradePrefix
        var result = new Dictionary<string, double>();
        foreach (var group in all.GroupBy(w => w.SectionName))
        {
            // 找匹配牌号前缀的条目
            var matched = group.FirstOrDefault(w =>
                w.PlantGradePrefix != null &&
                plantGrade != null &&
                plantGrade.StartsWith(w.PlantGradePrefix));

            // 未找到则使用通用（null）条目
            matched ??= group.FirstOrDefault(w => w.PlantGradePrefix == null);

            if (matched != null)
                result[group.Key] = matched.StandardDays;
        }

        return result;
    }
}
