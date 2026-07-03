using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Helpers;

namespace MES.Services.Infrastructure;

public class WorkstationService : IWorkstationService
{
    private readonly AppDbContext _context;

    public WorkstationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<WorkstationDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.Workstations
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
                    w.Code.Contains(keyword) ||
                    (w.Name != null && w.Name.Contains(keyword)) ||
                    (w.EquipmentName != null && w.EquipmentName.Contains(keyword)) ||
                    w.SectionName.Contains(keyword) ||
                    (w.ReportType != null && w.ReportType.Contains(keyword)));
            }
        }

        // 通用筛选
        queryable = queryable.ApplyFilters(query.Filters);

        // 排序
        var sortBy = string.IsNullOrEmpty(query.SortBy) || query.SortBy.Equals("CreatedTime", StringComparison.OrdinalIgnoreCase)
            ? "Code"
            : query.SortBy;
        queryable = queryable.ApplySort(sortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(w => new WorkstationDto
            {
                Id = w.Id,
                Code = w.Code,
                Name = w.Name,
                EquipmentName = w.EquipmentName,
                SectionName = w.SectionName,
                ReportType = w.ReportType,
                IsActive = w.IsActive
            })
            .ToListAsync();

        return new PagedResult<WorkstationDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<WorkstationDto?> GetByCodeAsync(string code)
    {
        return await _context.Workstations
            .Where(ws => ws.Code == code && ws.IsActive)
            .Select(ws => new WorkstationDto
            {
                Id = ws.Id,
                Code = ws.Code,
                Name = ws.Name,
                EquipmentName = ws.EquipmentName,
                SectionName = ws.SectionName,
                ReportType = ws.ReportType,
                IsActive = ws.IsActive
            })
            .FirstOrDefaultAsync();
    }

    public async Task<bool> SaveAsync(WorkstationDto dto)
    {
        if (dto.Id > 0)
        {
            // 更新
            var entity = await _context.Workstations
                .FirstOrDefaultAsync(w => w.Id == dto.Id);

            if (entity == null)
                throw new BusinessException("工位不存在");

            entity.Code = dto.Code;
            entity.Name = dto.Name;
            entity.EquipmentName = dto.EquipmentName;
            entity.SectionName = dto.SectionName;
            entity.ReportType = dto.ReportType;
            entity.IsActive = dto.IsActive;
        }
        else
        {
            // 新增
            var entity = new Workstation
            {
                Code = dto.Code,
                Name = dto.Name,
                EquipmentName = dto.EquipmentName,
                SectionName = dto.SectionName,
                ReportType = dto.ReportType,
                IsActive = dto.IsActive
            };
            _context.Workstations.Add(entity);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.Workstations
            .FirstOrDefaultAsync(w => w.Id == id);

        if (entity == null)
            throw new BusinessException("工位不存在");

        _context.Workstations.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}
