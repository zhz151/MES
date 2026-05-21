using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;

using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services;

public class RepairOrderService : IRepairOrderService
{
    private readonly AppDbContext _context;

    public RepairOrderService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<RepairOrderListDto>> GetPagedAsync(RepairOrderQueryParams query)
    {
        // 先 JOIN Equipment 表，使设备字段可用于筛选和排序
        var baseQuery = from r in _context.RepairOrders
                        join e in _context.Equipment on r.EquipmentId equals e.Id
                        select new { Order = r, Equipment = e };

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            baseQuery = baseQuery.Where(x =>
                x.Order.RepairOrderNo.Contains(kw) ||
                x.Order.FaultDescription.Contains(kw) ||
                x.Order.ReportPerson.Contains(kw) ||
                x.Equipment.EquipmentName.Contains(kw) ||
                x.Equipment.EquipmentCode.Contains(kw) ||
                x.Equipment.Location.Contains(kw) ||
                (x.Order.RepairPerson != null && x.Order.RepairPerson.Contains(kw)) ||
                (x.Order.FaultType != null && x.Order.FaultType.Contains(kw)) ||
                x.Order.Priority.Contains(kw) ||
                x.Order.RepairStatus.Contains(kw) ||
                (x.Order.RepairContent != null && x.Order.RepairContent.Contains(kw)) ||
                (x.Order.SparePartUsed != null && x.Order.SparePartUsed.Contains(kw)));
        }

        if (query.EquipmentId.HasValue)
            baseQuery = baseQuery.Where(x => x.Order.EquipmentId == query.EquipmentId.Value);

        if (!string.IsNullOrEmpty(query.RepairStatus))
            baseQuery = baseQuery.Where(x => x.Order.RepairStatus == query.RepairStatus);

        if (!string.IsNullOrEmpty(query.Priority))
            baseQuery = baseQuery.Where(x => x.Order.Priority == query.Priority);

        if (query.ReportTimeFrom.HasValue)
            baseQuery = baseQuery.Where(x => x.Order.ReportTime >= query.ReportTimeFrom.Value);

        if (query.ReportTimeTo.HasValue)
            baseQuery = baseQuery.Where(x => x.Order.ReportTime <= query.ReportTimeTo.Value);

        baseQuery = baseQuery.ApplyFilters(query.Filters);

        var totalCount = await baseQuery.CountAsync();

        // Apply sorting (含设备关联字段)
        baseQuery = (query.SortBy?.ToLower(), query.IsDescending) switch
        {
            ("repairorderno", true) => baseQuery.OrderByDescending(x => x.Order.RepairOrderNo),
            ("repairorderno", false) => baseQuery.OrderBy(x => x.Order.RepairOrderNo),
            ("reporttime", true) => baseQuery.OrderByDescending(x => x.Order.ReportTime),
            ("reporttime", false) => baseQuery.OrderBy(x => x.Order.ReportTime),
            ("equipmentname", true) => baseQuery.OrderByDescending(x => x.Equipment.EquipmentName),
            ("equipmentname", false) => baseQuery.OrderBy(x => x.Equipment.EquipmentName),
            ("equipmentcode", true) => baseQuery.OrderByDescending(x => x.Equipment.EquipmentCode),
            ("equipmentcode", false) => baseQuery.OrderBy(x => x.Equipment.EquipmentCode),
            ("location", true) => baseQuery.OrderByDescending(x => x.Equipment.Location),
            ("location", false) => baseQuery.OrderBy(x => x.Equipment.Location),
            ("faultdescription", true) => baseQuery.OrderByDescending(x => x.Order.FaultDescription),
            ("faultdescription", false) => baseQuery.OrderBy(x => x.Order.FaultDescription),
            ("faulttype", true) => baseQuery.OrderByDescending(x => x.Order.FaultType ?? ""),
            ("faulttype", false) => baseQuery.OrderBy(x => x.Order.FaultType ?? ""),
            ("priority", true) => baseQuery.OrderByDescending(x => x.Order.Priority),
            ("priority", false) => baseQuery.OrderBy(x => x.Order.Priority),
            ("repairstatus", true) => baseQuery.OrderByDescending(x => x.Order.RepairStatus),
            ("repairstatus", false) => baseQuery.OrderBy(x => x.Order.RepairStatus),
            ("reportperson", true) => baseQuery.OrderByDescending(x => x.Order.ReportPerson),
            ("reportperson", false) => baseQuery.OrderBy(x => x.Order.ReportPerson),
            ("repairperson", true) => baseQuery.OrderByDescending(x => x.Order.RepairPerson ?? ""),
            ("repairperson", false) => baseQuery.OrderBy(x => x.Order.RepairPerson ?? ""),
            ("repairstarttime", true) => baseQuery.OrderByDescending(x => x.Order.RepairStartTime),
            ("repairstarttime", false) => baseQuery.OrderBy(x => x.Order.RepairStartTime),
            ("repairendtime", true) => baseQuery.OrderByDescending(x => x.Order.RepairEndTime),
            ("repairendtime", false) => baseQuery.OrderBy(x => x.Order.RepairEndTime),
            ("repaircontent", true) => baseQuery.OrderByDescending(x => x.Order.RepairContent ?? ""),
            ("repaircontent", false) => baseQuery.OrderBy(x => x.Order.RepairContent ?? ""),
            ("sparepartused", true) => baseQuery.OrderByDescending(x => x.Order.SparePartUsed ?? ""),
            ("sparepartused", false) => baseQuery.OrderBy(x => x.Order.SparePartUsed ?? ""),
            _ when query.IsDescending => baseQuery.OrderByDescending(x => x.Order.ReportTime),
            _ => baseQuery.OrderBy(x => x.Order.ReportTime)
        };

        var items = await baseQuery
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(x => new RepairOrderListDto
            {
                Id = x.Order.Id,
                RepairOrderNo = x.Order.RepairOrderNo,
                EquipmentId = x.Order.EquipmentId,
                EquipmentName = x.Equipment.EquipmentName,
                EquipmentCode = x.Equipment.EquipmentCode,
                EquipmentLocation = x.Equipment.Location,
                FaultDescription = x.Order.FaultDescription,
                FaultType = x.Order.FaultType,
                Priority = x.Order.Priority,
                RepairStatus = x.Order.RepairStartTime != null
                    ? (x.Order.RepairEndTime != null ? "Completed" : "InProgress")
                    : "Pending",
                ReportPerson = x.Order.ReportPerson,
                ReportTime = x.Order.ReportTime,
                RepairPerson = x.Order.RepairPerson,
                RepairStartTime = x.Order.RepairStartTime,
                RepairEndTime = x.Order.RepairEndTime,
                RepairContent = x.Order.RepairContent,
                SparePartUsed = x.Order.SparePartUsed
            })
            .ToListAsync();

        return new PagedResult<RepairOrderListDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<RepairOrderListDto>> GetAllListAsync()
    {
        var baseQuery = from r in _context.RepairOrders
                        join e in _context.Equipment on r.EquipmentId equals e.Id
                        orderby r.ReportTime descending
                        select new RepairOrderListDto
                        {
                            Id = r.Id,
                            RepairOrderNo = r.RepairOrderNo,
                            EquipmentId = r.EquipmentId,
                            EquipmentName = e.EquipmentName,
                            EquipmentCode = e.EquipmentCode,
                            EquipmentLocation = e.Location,
                            FaultDescription = r.FaultDescription,
                            FaultType = r.FaultType,
                            Priority = r.Priority,
                            RepairStatus = r.RepairStartTime != null
                                ? (r.RepairEndTime != null ? "Completed" : "InProgress")
                                : "Pending",
                            ReportPerson = r.ReportPerson,
                            ReportTime = r.ReportTime,
                            RepairPerson = r.RepairPerson,
                            RepairStartTime = r.RepairStartTime,
                            RepairEndTime = r.RepairEndTime,
                            RepairContent = r.RepairContent,
                            SparePartUsed = r.SparePartUsed
                        };

        return await baseQuery.ToListAsync();
    }

    public async Task<RepairOrderListDto> GetByIdAsync(int id)
    {
        var entity = await _context.RepairOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) throw new BusinessException("维修工单不存在");
        return await ToDtoAsync(entity);
    }

    public async Task<RepairOrderListDto> CreateAsync(CreateRepairOrderRequest request)
    {
        var equipment = await _context.Equipment
            .FirstOrDefaultAsync(e => e.Id == request.EquipmentId);
        if (equipment == null) throw new BusinessException("设备不存在");

        var orderNo = await GenerateOrderNoAsync("WX");

        var entity = new RepairOrder
        {
            RepairOrderNo = orderNo,
            EquipmentId = request.EquipmentId,
            FaultDescription = request.FaultDescription,
            FaultType = request.FaultType,
            Priority = request.Priority,
            RepairStatus = DeriveRepairStatus(request.RepairStartTime, request.RepairEndTime),
            ReportPerson = request.ReportPerson,
            ReportTime = request.ReportTime,
            RepairPerson = request.RepairPerson,
            RepairStartTime = request.RepairStartTime,
            RepairEndTime = request.RepairEndTime,
            RepairContent = request.RepairContent,
            SparePartUsed = request.SparePartUsed
        };

        _context.RepairOrders.Add(entity);

        // RepairEndTime 有值时回写设备最近维修日期
        if (request.RepairEndTime.HasValue)
        {
            if (!equipment.LastRepairDate.HasValue || request.RepairEndTime.Value > equipment.LastRepairDate.Value)
                equipment.LastRepairDate = request.RepairEndTime.Value;
        }

        await _context.SaveChangesAsync();
        return await ToDtoAsync(entity);
    }

    public async Task<List<RepairOrderListDto>> CreateBatchAsync(List<CreateRepairOrderRequest> requests)
    {
        if (requests.Count == 0) return new List<RepairOrderListDto>();

        var results = new List<RepairOrderListDto>();
        foreach (var request in requests)
        {
            var dto = await CreateAsync(request);
            results.Add(dto);
        }
        return results;
    }

    public async Task<RepairOrderListDto> UpdateAsync(int id, UpdateRepairOrderRequest request)
    {
        var entity = await _context.RepairOrders
            .FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) throw new BusinessException("维修工单不存在");

        if (request.FaultDescription != null) entity.FaultDescription = request.FaultDescription;
        if (request.FaultType != null) entity.FaultType = request.FaultType;
        if (request.Priority != null) entity.Priority = request.Priority;
        if (request.ReportPerson != null) entity.ReportPerson = request.ReportPerson;
        if (request.ReportTime.HasValue) entity.ReportTime = request.ReportTime.Value;
        if (request.RepairPerson != null) entity.RepairPerson = request.RepairPerson;
        if (request.RepairStartTime.HasValue) entity.RepairStartTime = request.RepairStartTime.Value;
        if (request.RepairEndTime.HasValue) entity.RepairEndTime = request.RepairEndTime.Value;
        if (request.RepairContent != null) entity.RepairContent = request.RepairContent;
        if (request.SparePartUsed != null) entity.SparePartUsed = request.SparePartUsed;

        // 根据字段完整度自动重算状态
        entity.RepairStatus = DeriveRepairStatus(entity.RepairStartTime, entity.RepairEndTime);

        // RepairEndTime 有值时回写设备最近维修日期
        if (request.RepairEndTime.HasValue)
        {
            var equipment = await _context.Equipment
                .FirstOrDefaultAsync(e => e.Id == entity.EquipmentId);
            if (equipment != null)
            {
                if (!equipment.LastRepairDate.HasValue || request.RepairEndTime.Value > equipment.LastRepairDate.Value)
                    equipment.LastRepairDate = request.RepairEndTime.Value;
            }
        }

        await _context.SaveChangesAsync();
        return await ToDtoAsync(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.RepairOrders
            .FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) throw new BusinessException("维修工单不存在");

        _context.RepairOrders.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new RepairOrderQueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue
        };
        var result = await GetPagedAsync(query);
        var selected = result.Items.Where(i => ids.Contains(i.Id)).ToList();
        return RepairOrderPrintHelper.GenerateBatchPdf(selected, columns);
    }

    public async Task<byte[]> PrintAllAsync(RepairOrderQueryParams query, List<PrintColumnDef> columns)
    {
        query.PageIndex = 1;
        query.PageSize = int.MaxValue;
        var result = await GetPagedAsync(query);
        return RepairOrderPrintHelper.GenerateBatchPdf(result.Items, columns);
    }

    private static string DeriveRepairStatus(DateTime? startTime, DateTime? endTime)
    {
        if (endTime != null) return nameof(RepairOrderStatus.Completed);
        if (startTime != null) return nameof(RepairOrderStatus.InProgress);
        return nameof(RepairOrderStatus.Pending);
    }

    private async Task<string> GenerateOrderNoAsync(string prefix)
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var pattern = $"{prefix}-{today}-";

        var maxNo = await _context.RepairOrders
            .Where(r => r.RepairOrderNo.StartsWith(pattern))
            .OrderByDescending(r => r.RepairOrderNo)
            .Select(r => r.RepairOrderNo)
            .FirstOrDefaultAsync();

        if (maxNo == null) return $"{pattern}001";

        var seq = int.Parse(maxNo[^3..]) + 1;
        return $"{pattern}{seq:D3}";
    }

    private async Task<RepairOrderListDto> ToDtoAsync(RepairOrder entity)
    {
        var equipment = await _context.Equipment
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entity.EquipmentId);

        return new RepairOrderListDto
        {
            Id = entity.Id,
            RepairOrderNo = entity.RepairOrderNo,
            EquipmentId = entity.EquipmentId,
            EquipmentName = equipment?.EquipmentName ?? "",
            EquipmentCode = equipment?.EquipmentCode,
            EquipmentLocation = equipment?.Location,
            FaultDescription = entity.FaultDescription,
            FaultType = entity.FaultType,
            Priority = entity.Priority,
            RepairStatus = DeriveRepairStatus(entity.RepairStartTime, entity.RepairEndTime),
            ReportPerson = entity.ReportPerson,
            ReportTime = entity.ReportTime,
            RepairPerson = entity.RepairPerson,
            RepairStartTime = entity.RepairStartTime,
            RepairEndTime = entity.RepairEndTime,
            RepairContent = entity.RepairContent,
            SparePartUsed = entity.SparePartUsed
        };
    }
}
