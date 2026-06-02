using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Helpers;

namespace MES.Services.Configuration;

/// <summary>
/// 交货状态附加天数服务
/// </summary>
public class StandardWorkDayDeliveryStateService : IStandardWorkDayDeliveryStateService
{
    private readonly AppDbContext _context;

    public StandardWorkDayDeliveryStateService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<StandardWorkDayDeliveryStateDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.StandardWorkDayDeliveryStates
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
                    w.DeliveryState.Contains(keyword) ||
                    (w.Remark != null && w.Remark.Contains(keyword)));
            }
        }

        // 通用筛选
        queryable = queryable.ApplyFilters(query.Filters);

        // 排序
        var sortBy = string.IsNullOrEmpty(query.SortBy)
            ? "DeliveryState"
            : query.SortBy;
        queryable = queryable.ApplySort(sortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(w => new StandardWorkDayDeliveryStateDto
            {
                Id = w.Id,
                DeliveryState = w.DeliveryState,
                ExtraDays = w.ExtraDays,
                PlantGradePrefix = w.PlantGradePrefix,
                Remark = w.Remark
            })
            .ToListAsync();

        return new PagedResult<StandardWorkDayDeliveryStateDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<StandardWorkDayDeliveryStateDto?> GetByIdAsync(int id)
    {
        var entity = await _context.StandardWorkDayDeliveryStates
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);

        if (entity == null)
            throw new BusinessException("交货状态附加天数配置不存在");

        return new StandardWorkDayDeliveryStateDto
        {
            Id = entity.Id,
            DeliveryState = entity.DeliveryState,
            ExtraDays = entity.ExtraDays,
            PlantGradePrefix = entity.PlantGradePrefix,
            Remark = entity.Remark
        };
    }

    public async Task<bool> SaveAsync(StandardWorkDayDeliveryStateDto dto)
    {
        if (dto.Id > 0)
        {
            var entity = await _context.StandardWorkDayDeliveryStates
                .FirstOrDefaultAsync(w => w.Id == dto.Id);

            if (entity == null)
                throw new BusinessException("交货状态附加天数配置不存在");

            entity.DeliveryState = dto.DeliveryState;
            entity.ExtraDays = dto.ExtraDays;
            entity.PlantGradePrefix = dto.PlantGradePrefix;
            entity.Remark = dto.Remark;
        }
        else
        {
            var entity = new StandardWorkDayDeliveryState
            {
                DeliveryState = dto.DeliveryState,
                ExtraDays = dto.ExtraDays,
                PlantGradePrefix = dto.PlantGradePrefix,
                Remark = dto.Remark
            };
            _context.StandardWorkDayDeliveryStates.Add(entity);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.StandardWorkDayDeliveryStates
            .FirstOrDefaultAsync(w => w.Id == id);

        if (entity == null)
            throw new BusinessException("交货状态附加天数配置不存在");

        _context.StandardWorkDayDeliveryStates.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 获取交货状态附加天数映射：key=DeliveryState(枚举名), value=ExtraDays
    /// 含默认配置（key=""）
    /// </summary>
    public async Task<Dictionary<string, double>> GetDeliveryStateExtraDaysMapAsync()
    {
        return await _context.StandardWorkDayDeliveryStates
            .AsNoTracking()
            .Where(w => w.PlantGradePrefix == null)
            .ToDictionaryAsync(w => w.DeliveryState, w => w.ExtraDays);
    }
}
