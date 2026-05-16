using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;

using MES.Services.Printing;

namespace MES.Services;

public class MaintenanceOrderService : IMaintenanceOrderService
{
    private readonly AppDbContext _context;

    public MaintenanceOrderService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<MaintenanceOrderListDto>> GetPagedAsync(MaintenanceOrderQueryParams query)
    {
        // 先 JOIN Equipment 表，使设备字段可用于筛选和排序
        var baseQuery = from m in _context.MaintenanceOrders
                        join e in _context.Equipment on m.EquipmentId equals e.Id
                        select new { Order = m, Equipment = e };

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            baseQuery = baseQuery.Where(x =>
                x.Order.MaintOrderNo.Contains(kw) ||
                x.Equipment.EquipmentName.Contains(kw) ||
                x.Equipment.EquipmentCode.Contains(kw) ||
                x.Equipment.Location.Contains(kw) ||
                (x.Order.Executor != null && x.Order.Executor.Contains(kw)) ||
                (x.Order.ExecutionSummary != null && x.Order.ExecutionSummary.Contains(kw)) ||
                (x.Order.Remark != null && x.Order.Remark.Contains(kw)));
        }

        if (query.EquipmentId.HasValue)
            baseQuery = baseQuery.Where(x => x.Order.EquipmentId == query.EquipmentId.Value);

        var totalCount = await baseQuery.CountAsync();

        // Apply sorting (含设备关联字段)
        baseQuery = (query.SortBy?.ToLower(), query.IsDescending) switch
        {
            ("maintorderno", true) => baseQuery.OrderByDescending(x => x.Order.MaintOrderNo),
            ("maintorderno", false) => baseQuery.OrderBy(x => x.Order.MaintOrderNo),
            ("actualdate", true) => baseQuery.OrderByDescending(x => x.Order.ActualDate),
            ("actualdate", false) => baseQuery.OrderBy(x => x.Order.ActualDate),
            ("equipmentname", true) => baseQuery.OrderByDescending(x => x.Equipment.EquipmentName),
            ("equipmentname", false) => baseQuery.OrderBy(x => x.Equipment.EquipmentName),
            ("equipmentcode", true) => baseQuery.OrderByDescending(x => x.Equipment.EquipmentCode),
            ("equipmentcode", false) => baseQuery.OrderBy(x => x.Equipment.EquipmentCode),
            ("location", true) => baseQuery.OrderByDescending(x => x.Equipment.Location),
            ("location", false) => baseQuery.OrderBy(x => x.Equipment.Location),
            ("executor", true) => baseQuery.OrderByDescending(x => x.Order.Executor ?? ""),
            ("executor", false) => baseQuery.OrderBy(x => x.Order.Executor ?? ""),
            _ when query.IsDescending => baseQuery.OrderByDescending(x => x.Order.Id),
            _ => baseQuery.OrderBy(x => x.Order.Id)
        };

        var items = await baseQuery
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(x => new MaintenanceOrderListDto
            {
                Id = x.Order.Id,
                MaintOrderNo = x.Order.MaintOrderNo,
                EquipmentId = x.Order.EquipmentId,
                EquipmentName = x.Equipment.EquipmentName,
                EquipmentCode = x.Equipment.EquipmentCode,
                Location = x.Equipment.Location,
                ActualDate = x.Order.ActualDate,
                Executor = x.Order.Executor,
                ExecutionSummary = x.Order.ExecutionSummary,
                Remark = x.Order.Remark
            })
            .ToListAsync();

        return new PagedResult<MaintenanceOrderListDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<MaintenanceOrderListDto> GetByIdAsync(int id)
    {
        var entity = await _context.MaintenanceOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);
        if (entity == null) throw new BusinessException("保养工单不存在");
        return await ToDtoAsync(entity);
    }

    public async Task<MaintenanceOrderListDto> CreateAsync(CreateMaintenanceOrderRequest request)
    {
        var equipment = await _context.Equipment
            .FirstOrDefaultAsync(e => e.Id == request.EquipmentId);
        if (equipment == null) throw new BusinessException("设备不存在");

        var orderNo = await GenerateOrderNoAsync("BY");

        var entity = new MaintenanceOrder
        {
            MaintOrderNo = orderNo,
            EquipmentId = request.EquipmentId,
            ActualDate = request.ActualDate,
            Executor = request.Executor,
            ExecutionSummary = request.ExecutionSummary,
            Remark = request.Remark
        };

        _context.MaintenanceOrders.Add(entity);

        // 回写设备保养参数
        if (request.ActualDate.HasValue)
        {
            if (!equipment.LastMaintDate.HasValue || request.ActualDate.Value > equipment.LastMaintDate.Value)
                equipment.LastMaintDate = request.ActualDate.Value;
            if (equipment.CurrentMaintStartDate != null)
            {
                equipment.CurrentMaintStartDate = equipment.CurrentMaintStartDate.Value.AddDays(equipment.MaintCycleDays);
            }
        }

        await _context.SaveChangesAsync();
        return await ToDtoAsync(entity);
    }

    public async Task<List<MaintenanceOrderListDto>> CreateBatchAsync(List<CreateMaintenanceOrderRequest> requests)
    {
        if (requests.Count == 0) return new List<MaintenanceOrderListDto>();

        var results = new List<MaintenanceOrderListDto>();
        foreach (var request in requests)
        {
            var dto = await CreateAsync(request);
            results.Add(dto);
        }
        return results;
    }

    public async Task<MaintenanceOrderListDto> UpdateAsync(int id, UpdateMaintenanceRequest request)
    {
        var entity = await _context.MaintenanceOrders
            .FirstOrDefaultAsync(m => m.Id == id);
        if (entity == null) throw new BusinessException("保养工单不存在");

        if (request.ActualDate.HasValue) entity.ActualDate = request.ActualDate;
        if (request.Executor != null) entity.Executor = request.Executor;
        if (request.ExecutionSummary != null) entity.ExecutionSummary = request.ExecutionSummary;
        if (request.Remark != null) entity.Remark = request.Remark;

        // ActualDate 填入时回写设备保养参数
        if (request.ActualDate.HasValue)
        {
            var equipment = await _context.Equipment
                .FirstOrDefaultAsync(e => e.Id == entity.EquipmentId);
            if (equipment != null)
            {
                if (!equipment.LastMaintDate.HasValue || request.ActualDate.Value > equipment.LastMaintDate.Value)
                    equipment.LastMaintDate = request.ActualDate.Value;
                if (equipment.CurrentMaintStartDate != null)
                {
                    equipment.CurrentMaintStartDate = equipment.CurrentMaintStartDate.Value.AddDays(equipment.MaintCycleDays);
                }
                equipment.MaintPerson ??= request.Executor;
            }
        }

        await _context.SaveChangesAsync();
        return await ToDtoAsync(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.MaintenanceOrders
            .FirstOrDefaultAsync(m => m.Id == id);
        if (entity == null) throw new BusinessException("保养工单不存在");

        _context.MaintenanceOrders.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new MaintenanceOrderQueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue
        };
        var result = await GetPagedAsync(query);
        var selected = result.Items.Where(i => ids.Contains(i.Id)).ToList();
        return MaintenanceOrderPrintHelper.GenerateBatchPdf(selected, columns);
    }

    public async Task<byte[]> PrintAllAsync(MaintenanceOrderQueryParams query, List<PrintColumnDef> columns)
    {
        query.PageIndex = 1;
        query.PageSize = int.MaxValue;
        var result = await GetPagedAsync(query);
        return MaintenanceOrderPrintHelper.GenerateBatchPdf(result.Items, columns);
    }

    private async Task<string> GenerateOrderNoAsync(string prefix)
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var pattern = $"{prefix}-{today}-";

        var maxNo = await _context.MaintenanceOrders
            .Where(m => m.MaintOrderNo.StartsWith(pattern))
            .OrderByDescending(m => m.MaintOrderNo)
            .Select(m => m.MaintOrderNo)
            .FirstOrDefaultAsync();

        if (maxNo == null) return $"{pattern}001";

        var seq = int.Parse(maxNo[^3..]) + 1;
        return $"{pattern}{seq:D3}";
    }

    private async Task<MaintenanceOrderListDto> ToDtoAsync(MaintenanceOrder entity)
    {
        var equipment = await _context.Equipment
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entity.EquipmentId);

        return new MaintenanceOrderListDto
        {
            Id = entity.Id,
            MaintOrderNo = entity.MaintOrderNo,
            EquipmentId = entity.EquipmentId,
            EquipmentName = equipment?.EquipmentName ?? "",
            EquipmentCode = equipment?.EquipmentCode,
            Location = equipment?.Location,
            ActualDate = entity.ActualDate,
            Executor = entity.Executor,
            ExecutionSummary = entity.ExecutionSummary,
            Remark = entity.Remark
        };
    }
}
