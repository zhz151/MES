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
using MES.Core.Exceptions;
using MES.Services.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace MES.Services.Configuration;

/// <summary>
/// 日产估算服务
/// </summary>
public class DailyOutputEstimateService : IDailyOutputEstimateService
{
    private readonly AppDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;

    public DailyOutputEstimateService(AppDbContext context, IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// 日产估算为全局配置，变更影响所有工单的产能工量/剩余工量/紧急性，
    /// 保存/删除后全量刷新执行读模型（级联订单读模型）与用料计划总览读模型。
    ///
    /// ⚠️ 不能直接注入 IWorkOrderExecutionService / IWorkOrderListSummaryRefreshService：
    /// 二者均依赖 IDailyOutputEstimateService（循环），故经 IServiceScopeFactory 运行时懒解析
    /// （与本仓库 WorkOrderExecutionService 解析 IOrderService 的模式一致）。
    /// 不抛异常，避免影响配置保存主流程。
    /// </summary>
    private async Task RefreshReadModelsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var workOrderExecutionService = scope.ServiceProvider.GetRequiredService<IWorkOrderExecutionService>();
            await workOrderExecutionService.RefreshAllAsync();

            var listSummaryService = scope.ServiceProvider.GetRequiredService<IWorkOrderListSummaryRefreshService>();
            var salesOrderNos = await _context.WorkOrders
                .AsNoTracking()
                .Select(wo => wo.SalesOrderNo)
                .Distinct()
                .ToListAsync();
            foreach (var soNo in salesOrderNos)
            {
                if (!string.IsNullOrWhiteSpace(soNo))
                    await listSummaryService.RefreshBySalesOrderAsync(soNo);
            }
        }
        catch (Exception ex)
        {
            // 全局配置改动属低频操作，刷新失败不影响保存结果
            System.Diagnostics.Debug.WriteLine($"读模型刷新失败: {ex.Message}");
        }
    }

    public async Task<PagedResult<DailyOutputEstimateDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.Set<DailyOutputEstimate>().AsNoTracking();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(x => x.Remark != null && x.Remark.Contains(kw));
        }

        queryable = queryable.ApplyFilters(query.Filters);
        queryable = string.IsNullOrWhiteSpace(query.SortBy)
            ? queryable.OrderBy(x => x.MinOuterDiameter)
            : queryable.ApplySort(query.SortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => new DailyOutputEstimateDto
            {
                Id = e.Id,
                MinOuterDiameter = e.MinOuterDiameter,
                DailyOutputTons = e.DailyOutputTons,
                Remark = e.Remark,
            })
            .ToListAsync();

        return new PagedResult<DailyOutputEstimateDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<DailyOutputEstimateDto?> GetByIdAsync(int id)
    {
        var entity = await _context.Set<DailyOutputEstimate>().FindAsync(id);
        if (entity == null) return null;
        return new DailyOutputEstimateDto
        {
            Id = entity.Id,
            MinOuterDiameter = entity.MinOuterDiameter,
            DailyOutputTons = entity.DailyOutputTons,
            Remark = entity.Remark,
        };
    }

    public async Task<bool> SaveAsync(DailyOutputEstimateDto dto)
    {
        if (dto.Id > 0)
        {
            var entity = await _context.Set<DailyOutputEstimate>().FindAsync(dto.Id)
                ?? throw new BusinessException("日产估算配置不存在");
            entity.MinOuterDiameter = dto.MinOuterDiameter;
            entity.DailyOutputTons = dto.DailyOutputTons;
            entity.Remark = dto.Remark;
        }
        else
        {
            var entity = new DailyOutputEstimate
            {
                MinOuterDiameter = dto.MinOuterDiameter,
                DailyOutputTons = dto.DailyOutputTons,
                Remark = dto.Remark,
            };
            _context.Set<DailyOutputEstimate>().Add(entity);
        }
        var saved = await _context.SaveChangesAsync() > 0;
        if (saved) await RefreshReadModelsAsync();
        return saved;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.Set<DailyOutputEstimate>().FindAsync(id)
            ?? throw new BusinessException("日产估算配置不存在");
        _context.Set<DailyOutputEstimate>().Remove(entity);
        var saved = await _context.SaveChangesAsync() > 0;
        if (saved) await RefreshReadModelsAsync();
        return saved;
    }

    public async Task<List<DailyOutputEstimateDto>> GetAllAsync()
    {
        return await _context.Set<DailyOutputEstimate>()
            .AsNoTracking()
            .OrderByDescending(e => e.MinOuterDiameter)
            .Select(e => new DailyOutputEstimateDto
            {
                Id = e.Id,
                MinOuterDiameter = e.MinOuterDiameter,
                DailyOutputTons = e.DailyOutputTons,
                Remark = e.Remark,
            })
            .ToListAsync();
    }
}
