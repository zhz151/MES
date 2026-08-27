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
using MES.Services.Helpers;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace MES.Services.Configuration;

/// <summary>
/// 业务参数配置服务实现
/// </summary>
public class ConfigParameterService : IConfigParameterService
{
    private readonly AppDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;

    public ConfigParameterService(AppDbContext context, IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _scopeFactory = scopeFactory;
    }

    public async Task<PagedResult<ConfigParameterDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.ConfigParameters
            .AsNoTracking()
            .AsQueryable();

        // 模糊搜索
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            queryable = queryable.Where(c =>
                c.Category.Contains(kw) ||
                (c.CategoryDisplay != null && c.CategoryDisplay.Contains(kw)) ||
                c.ParamKey.Contains(kw) ||
                (c.Remark != null && c.Remark.Contains(kw)));
        }

        // 筛选
        if (query.Filters is { Count: > 0 })
            queryable = queryable.ApplyFilters(query.Filters);

        // 排序
        var sortBy = string.IsNullOrEmpty(query.SortBy) ? "CreatedTime" : query.SortBy;
        queryable = queryable.ApplySort(sortBy, query.IsDescending);

        // 分页
        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(c => new ConfigParameterDto
            {
                Id = c.Id,
                Category = c.Category,
                CategoryDisplay = c.CategoryDisplay,
                Context = c.Context,
                ParamKey = c.ParamKey,
                ParamValue = c.ParamValue,
                Remark = c.Remark
            })
            .ToListAsync();

        return new PagedResult<ConfigParameterDto>
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<ConfigParameterDto?> GetByIdAsync(int id)
    {
        var entity = await _context.ConfigParameters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity == null)
            throw new BusinessException("参数配置不存在");

        return new ConfigParameterDto
        {
            Id = entity.Id,
            Category = entity.Category,
            CategoryDisplay = entity.CategoryDisplay,
            Context = entity.Context,
            ParamKey = entity.ParamKey,
            ParamValue = entity.ParamValue,
            Remark = entity.Remark
        };
    }

    public async Task<bool> SaveAsync(ConfigParameterDto dto)
    {
        if (dto.Id > 0)
        {
            var entity = await _context.ConfigParameters
                .FirstOrDefaultAsync(c => c.Id == dto.Id);
            if (entity == null)
                throw new BusinessException("参数配置不存在");

            entity.Category = dto.Category;
            entity.CategoryDisplay = dto.CategoryDisplay;
            entity.Context = dto.Context;
            entity.ParamKey = dto.ParamKey;
            entity.ParamValue = dto.ParamValue;
            entity.Remark = dto.Remark;
        }
        else
        {
            var entity = new ConfigParameter
            {
                Category = dto.Category,
                CategoryDisplay = dto.CategoryDisplay,
                Context = dto.Context,
                ParamKey = dto.ParamKey,
                ParamValue = dto.ParamValue,
                Remark = dto.Remark
            };
            _context.ConfigParameters.Add(entity);
        }

        await _context.SaveChangesAsync();
        await RefreshMaterialPlanToleranceSnapshotAsync(dto.Category);
        await RefreshReadModelsIfAffectedAsync(dto.Category);
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.ConfigParameters
            .FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null)
            throw new BusinessException("参数配置不存在");

        var category = entity.Category;
        _context.ConfigParameters.Remove(entity);
        await _context.SaveChangesAsync();
        await RefreshMaterialPlanToleranceSnapshotAsync(category);
        await RefreshReadModelsIfAffectedAsync(category);
        return true;
    }

    public async Task<Dictionary<string, decimal>> GetConfigMapAsync(string category)
    {
        return await _context.ConfigParameters
            .AsNoTracking()
            .Where(c => c.Category == category)
            .ToDictionaryAsync(c => c.ParamKey, c => c.ParamValue, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var rows = await _context.ConfigParameters
            .AsNoTracking()
            .Select(c => new { c.Context, c.CategoryDisplay, c.ParamKey, c.Remark })
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["Context"] = rows.Select(c => c.Context).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct().OrderBy(x => x).ToList(),
            ["CategoryDisplay"] = rows.Select(c => c.CategoryDisplay).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct().OrderBy(x => x).ToList(),
            ["ParamKey"] = rows.Select(c => c.ParamKey).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList(),
            ["Remark"] = rows.Select(c => c.Remark).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct().OrderBy(x => x).ToList()
        };
    }

    private const string MaterialPlanToleranceCategory = "MaterialPlanTolerance";

    /// <summary>
    /// MaterialPlanTolerance 类目写操作后刷新 MaterialPlanToleranceProvider 静态快照，
    /// 使到料实投一致性容差改配置表保存即生效（与 DictValueDefinitionService.RefreshStaticSnapshotAsync 同模式）。
    /// </summary>
    private async Task RefreshMaterialPlanToleranceSnapshotAsync(string? category)
    {
        if (!string.Equals(category, MaterialPlanToleranceCategory, StringComparison.OrdinalIgnoreCase))
            return;
        var map = await GetConfigMapAsync(MaterialPlanToleranceCategory);
        MaterialPlanToleranceProvider.Apply(map.GetValueOrDefault("InputConsistencyTolerance"));
    }

    /// <summary>
    /// 被物化读模型实时读取的配置类目（工单执行读模型/用料计划总览/订单读模型）。
    /// 此类目变更后物化数据会过期，须全量重算（配置变更低频，代价可接受）。
    /// </summary>
    private static readonly HashSet<string> ReadModelAffectingCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "WarehouseThreshold", "WorkOrderDays", "UrgencyThreshold", "ProcessingDiscount",
        "MaterialPlanStatus", "MaterialPlanRatio", "DefaultValue", "MaterialPlanTolerance"
    };

    /// <summary>
    /// 类目影响物化读模型时全量重算：工单执行状况（RefreshAllAsync）级联订单读模型，
    /// 并按全部销售订单重刷用料计划总览。
    ///
    /// ⚠️ 不能直接注入 IWorkOrderExecutionService / IWorkOrderListSummaryRefreshService：
    /// 二者均依赖 IConfigParameterService（循环），故经 IServiceScopeFactory 运行时懒解析
    /// （与 DailyOutputEstimateService.RefreshReadModelsAsync 同模式）。失败不影响保存主流程。
    /// </summary>
    private async Task RefreshReadModelsIfAffectedAsync(string? category)
    {
        if (string.IsNullOrWhiteSpace(category) || !ReadModelAffectingCategories.Contains(category))
            return;
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
            _context.ChangeTracker.Clear();
            // 全局配置改动属低频操作，刷新失败不影响保存结果
            System.Diagnostics.Debug.WriteLine($"读模型刷新失败: {ex.Message}");
        }
    }
}
