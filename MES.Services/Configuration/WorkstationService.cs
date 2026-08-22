using Microsoft.EntityFrameworkCore;
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
using MES.Core.Constants;
using MES.Core.Exceptions;
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
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Core.Enums;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services.Configuration;

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
            .Select(w => new
            {
                w.Id,
                w.Code,
                w.Name,
                w.EquipmentName,
                w.SectionName,
                w.ReportType,
                w.IsActive
            })
            .ToListAsync();

        var dtos = items.Select(w => new WorkstationDto
        {
            Id = w.Id,
            Code = w.Code,
            Name = w.Name,
            EquipmentName = w.EquipmentName,
            SectionName = w.SectionName,
            ReportType = Enum.Parse<ReportTemplateType>(w.ReportType),
            IsActive = w.IsActive
        }).ToList();

        return new PagedResult<WorkstationDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<WorkstationDto?> GetByCodeAsync(string code)
    {
        var entity = await _context.Workstations
            .Where(ws => ws.Code == code && ws.IsActive)
            .Select(ws => new
            {
                ws.Id,
                ws.Code,
                ws.Name,
                ws.EquipmentName,
                ws.SectionName,
                ws.ReportType,
                ws.IsActive
            })
            .FirstOrDefaultAsync();

        if (entity == null) return null;

        return new WorkstationDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            EquipmentName = entity.EquipmentName,
            SectionName = entity.SectionName,
            ReportType = Enum.Parse<ReportTemplateType>(entity.ReportType),
            IsActive = entity.IsActive
        };
    }

    public async Task<bool> SaveAsync(WorkstationDto dto)
    {
        // 工段必须是标准工段英文 Key（对齐 SectionKeys），防止手输/历史中文等非法值污染存储
        if (!SectionKeys.IsKey(dto.SectionName))
            throw new BusinessException($"工段必须是标准工段，当前值「{SectionKeys.ToChinese(dto.SectionName)}」不合法，请从工段下拉选择");

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
            entity.ReportType = dto.ReportType.ToString();
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
                ReportType = dto.ReportType.ToString(),
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

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new QueryParams { PageIndex = 1, PageSize = int.MaxValue };
        var all = await GetPagedAsync(query);
        var selected = all.Items.Where(i => ids.Contains(i.Id)).ToList();
        return WorkstationPrintHelper.GenerateBatchPdf(selected, columns);
    }

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? null! : sortBy,
            IsDescending = isDescending
        };
        var result = await GetPagedAsync(query);
        return WorkstationPrintHelper.GenerateBatchPdf(result.Items, columns);
    }
}
