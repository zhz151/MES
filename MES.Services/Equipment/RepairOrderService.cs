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
using MES.Core.Enums;
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
using MES.Data.Entities;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Equipment;

using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services.Equipment;

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
                (x.Order.SparePartUsed != null && x.Order.SparePartUsed.Contains(kw)) ||
                (x.Order.RepairCategory != null && x.Order.RepairCategory.Contains(kw)) ||
                (x.Order.OtherRepairPersons != null && x.Order.OtherRepairPersons.Contains(kw)));
        }

        if (query.EquipmentId.HasValue)
            baseQuery = baseQuery.Where(x => x.Order.EquipmentId == query.EquipmentId.Value);

        if (query.RepairStatus.HasValue)
            baseQuery = baseQuery.Where(x => x.Order.RepairStatus == query.RepairStatus.Value.ToString());

        if (query.Priority.HasValue)
            baseQuery = baseQuery.Where(x => x.Order.Priority == query.Priority.Value.ToString());

        if (query.ReportTimeFrom.HasValue)
            baseQuery = baseQuery.Where(x => x.Order.ReportTime >= query.ReportTimeFrom.Value);

        if (query.ReportTimeTo.HasValue)
            baseQuery = baseQuery.Where(x => x.Order.ReportTime <= query.ReportTimeTo.Value);

        // 处理 JOIN 匿名类型 { Order, Equipment } 上的字段筛选：
        // ApplyFilters 通过反射在匿名类型上找不到业务字段属性（只有 Order/Equipment），
        // 故 Equipment 关联字段与 RepairOrder 全部字段均需手动处理
        if (query.Filters != null)
        {
            // Equipment 关联字段（EquipmentName/EquipmentCode/EquipmentLocation 来自 Equipment 表）
            var equipmentFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EquipmentName", "EquipmentCode", "EquipmentLocation" };
            // RepairOrder 表自身 string 字段（枚举字段 Priority/RepairStatus 实体存枚举英文名）
            var repairStringFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "RepairOrderNo", "FaultDescription", "FaultType", "Priority", "RepairStatus",
                "ReportPerson", "RepairPerson", "RepairCategory", "RepairContent",
                "SparePartUsed", "OtherRepairPersons"
            };
            // RepairOrder 表自身日期字段
            var repairDateFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "ReportTime", "RepairStartTime", "RepairEndTime" };

            foreach (var f in query.Filters.ToList())
            {
                if (string.IsNullOrWhiteSpace(f.Field)) continue;
                var op = f.Operator?.ToLowerInvariant() ?? "contains";
                var handled = false;

                if (equipmentFields.Contains(f.Field))
                {
                    // EquipmentLocation 在匿名类型中对应 Location，需要转换
                    var fieldName = f.Field.Equals("EquipmentLocation", StringComparison.OrdinalIgnoreCase) ? "Location" : f.Field;
                    if (op == "in" && f.Values?.Count > 0)
                    {
                        var values = f.Values;
                        baseQuery = baseQuery.Where(x => values.Contains(EF.Property<string>(x.Equipment, fieldName)));
                        handled = true;
                    }
                    else if (op == "contains" && !string.IsNullOrEmpty(f.Value))
                    {
                        var val = f.Value;
                        baseQuery = baseQuery.Where(x => EF.Property<string>(x.Equipment, fieldName).Contains(val));
                        handled = true;
                    }
                    else if (op == "equals" && !string.IsNullOrEmpty(f.Value))
                    {
                        var val = f.Value;
                        baseQuery = baseQuery.Where(x => EF.Property<string>(x.Equipment, fieldName) == val);
                        handled = true;
                    }
                }
                else if (repairStringFields.Contains(f.Field))
                {
                    if (op == "in" && f.Values?.Count > 0)
                    {
                        var values = f.Values;
                        baseQuery = baseQuery.Where(x => values.Contains(EF.Property<string>(x.Order, f.Field)));
                        handled = true;
                    }
                    else if (op == "contains" && !string.IsNullOrEmpty(f.Value))
                    {
                        var val = f.Value;
                        baseQuery = baseQuery.Where(x => EF.Property<string>(x.Order, f.Field).Contains(val));
                        handled = true;
                    }
                    else if (op == "equals" && !string.IsNullOrEmpty(f.Value))
                    {
                        var val = f.Value;
                        baseQuery = baseQuery.Where(x => EF.Property<string>(x.Order, f.Field) == val);
                        handled = true;
                    }
                }
                else if (repairDateFields.Contains(f.Field))
                {
                    // 日期按「精确到天」匹配（与 QueryableExtensions 的 DateTime in 分支一致）
                    if (op == "in" && f.Values?.Count > 0)
                    {
                        var dates = f.Values
                            .Select(v => DateTime.TryParse(v, out var dt) ? (DateTime?)dt.Date : null)
                            .Where(v => v.HasValue)
                            .Select(v => v!.Value)
                            .ToList();
                        if (dates.Count > 0)
                        {
                            if (f.Field.Equals("ReportTime", StringComparison.OrdinalIgnoreCase))
                                baseQuery = baseQuery.Where(x => dates.Contains(EF.Property<DateTime>(x.Order, "ReportTime").Date));
                            else
                                baseQuery = baseQuery.Where(x => dates.Contains(EF.Property<DateTime?>(x.Order, f.Field)!.Value.Date));
                            handled = true;
                        }
                    }
                    else if (op == "equals" && !string.IsNullOrEmpty(f.Value) && DateTime.TryParse(f.Value, out var eqDate))
                    {
                        if (f.Field.Equals("ReportTime", StringComparison.OrdinalIgnoreCase))
                            baseQuery = baseQuery.Where(x => EF.Property<DateTime>(x.Order, "ReportTime").Date == eqDate.Date);
                        else
                            baseQuery = baseQuery.Where(x => EF.Property<DateTime?>(x.Order, f.Field)!.Value.Date == eqDate.Date);
                        handled = true;
                    }
                }

                if (handled) query.Filters.Remove(f);
            }
        }
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
            ("repaircategory", true) => baseQuery.OrderByDescending(x => x.Order.RepairCategory ?? ""),
            ("repaircategory", false) => baseQuery.OrderBy(x => x.Order.RepairCategory ?? ""),
            ("repairstarttime", true) => baseQuery.OrderByDescending(x => x.Order.RepairStartTime),
            ("repairstarttime", false) => baseQuery.OrderBy(x => x.Order.RepairStartTime),
            ("repairendtime", true) => baseQuery.OrderByDescending(x => x.Order.RepairEndTime),
            ("repairendtime", false) => baseQuery.OrderBy(x => x.Order.RepairEndTime),
            ("repaircontent", true) => baseQuery.OrderByDescending(x => x.Order.RepairContent ?? ""),
            ("repaircontent", false) => baseQuery.OrderBy(x => x.Order.RepairContent ?? ""),
            ("sparepartused", true) => baseQuery.OrderByDescending(x => x.Order.SparePartUsed ?? ""),
            ("sparepartused", false) => baseQuery.OrderBy(x => x.Order.SparePartUsed ?? ""),
            ("otherrepairpersons", true) => baseQuery.OrderByDescending(x => x.Order.OtherRepairPersons ?? ""),
            ("otherrepairpersons", false) => baseQuery.OrderBy(x => x.Order.OtherRepairPersons ?? ""),
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
                Priority = Enum.Parse<RepairPriority>(x.Order.Priority),
                RepairStatus = x.Order.RepairStartTime != null
                    ? (x.Order.RepairEndTime != null ? RepairOrderStatus.Completed : RepairOrderStatus.InProgress)
                    : RepairOrderStatus.Pending,
                ReportPerson = x.Order.ReportPerson,
                ReportTime = x.Order.ReportTime,
                RepairPerson = x.Order.RepairPerson,
                RepairCategory = x.Order.RepairCategory,
                RepairStartTime = x.Order.RepairStartTime,
                RepairEndTime = x.Order.RepairEndTime,
                RepairContent = x.Order.RepairContent,
                SparePartUsed = x.Order.SparePartUsed,
                OtherRepairPersons = x.Order.OtherRepairPersons
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
            Priority = request.Priority.ToString(),
            RepairStatus = DeriveRepairStatus(request.RepairStartTime, request.RepairEndTime).ToString(),
            ReportPerson = request.ReportPerson,
            ReportTime = request.ReportTime,
            RepairPerson = request.RepairPerson,
            RepairCategory = request.RepairCategory,
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
        // 同步更新设备运行状态
        await EquipmentStatusCalculator.RecalculateRunningStatusAsync(_context, entity.EquipmentId);
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
        if (request.Priority.HasValue) entity.Priority = request.Priority.Value.ToString();
        if (request.ReportPerson != null) entity.ReportPerson = request.ReportPerson;
        if (request.ReportTime.HasValue) entity.ReportTime = request.ReportTime.Value;
        if (request.RepairPerson != null) entity.RepairPerson = request.RepairPerson;
        if (request.RepairCategory != null) entity.RepairCategory = request.RepairCategory;
        if (request.RepairStartTime.HasValue) entity.RepairStartTime = request.RepairStartTime.Value;
        if (request.RepairEndTime.HasValue) entity.RepairEndTime = request.RepairEndTime.Value;
        if (request.RepairContent != null) entity.RepairContent = request.RepairContent;
        if (request.SparePartUsed != null) entity.SparePartUsed = request.SparePartUsed;
        if (request.OtherRepairPersons != null) entity.OtherRepairPersons = request.OtherRepairPersons;

        // 根据字段完整度自动重算状态
        entity.RepairStatus = DeriveRepairStatus(entity.RepairStartTime, entity.RepairEndTime).ToString();

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
        // 同步更新设备运行状态
        await EquipmentStatusCalculator.RecalculateRunningStatusAsync(_context, entity.EquipmentId);
        return await ToDtoAsync(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.RepairOrders
            .FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) throw new BusinessException("维修工单不存在");

        var equipmentId = entity.EquipmentId;
        _context.RepairOrders.Remove(entity);
        await _context.SaveChangesAsync();

        // 回退设备最近维修日期快照：删除后按剩余维修单 RepairEndTime 最大值重算，不再残留已删记录的日期
        var equipment = await _context.Equipment.FirstOrDefaultAsync(e => e.Id == equipmentId);
        if (equipment != null)
        {
            var lastRepairDate = (await _context.RepairOrders
                .AsNoTracking()
                .Where(r => r.EquipmentId == equipmentId && r.RepairEndTime != null)
                .Select(r => (DateTime?)r.RepairEndTime)
                .ToListAsync()).Max();
            equipment.LastRepairDate = lastRepairDate;
            await _context.SaveChangesAsync();
        }

        // 同步更新设备运行状态
        await EquipmentStatusCalculator.RecalculateRunningStatusAsync(_context, equipmentId);
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var query = from r in _context.RepairOrders.AsNoTracking()
                    join e in _context.Equipment.AsNoTracking() on r.EquipmentId equals e.Id
                    select new
                    {
                        r.RepairOrderNo,
                        r.FaultDescription,
                        r.FaultType,
                        r.ReportPerson,
                        r.ReportTime,
                        r.RepairPerson,
                        r.RepairCategory,
                        r.RepairStartTime,
                        r.RepairEndTime,
                        r.RepairContent,
                        r.SparePartUsed,
                        r.OtherRepairPersons,
                        e.EquipmentName,
                        e.EquipmentCode,
                        Location = e.Location
                    };

        var all = await query.ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["RepairOrderNo"] = all.Select(x => x.RepairOrderNo).Distinct().OrderBy(x => x).ToList(),
            ["EquipmentName"] = all.Select(x => x.EquipmentName).Distinct().OrderBy(x => x).ToList(),
            ["EquipmentCode"] = all.Select(x => x.EquipmentCode).Distinct().OrderBy(x => x).ToList(),
            ["EquipmentLocation"] = all.Where(x => x.Location != null).Select(x => x.Location!).Distinct().OrderBy(x => x).ToList(),
            ["FaultDescription"] = all.Select(x => x.FaultDescription).Distinct().OrderBy(x => x).ToList(),
            ["FaultType"] = all.Where(x => x.FaultType != null).Select(x => x.FaultType!).Distinct().OrderBy(x => x).ToList(),
            ["ReportPerson"] = all.Select(x => x.ReportPerson).Distinct().OrderBy(x => x).ToList(),
            ["ReportTime"] = all.Select(x => x.ReportTime.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            ["RepairPerson"] = all.Where(x => x.RepairPerson != null).Select(x => x.RepairPerson!).Distinct().OrderBy(x => x).ToList(),
            ["RepairCategory"] = all.Where(x => x.RepairCategory != null).Select(x => x.RepairCategory!).Distinct().OrderBy(x => x).ToList(),
            ["RepairStartTime"] = all.Where(x => x.RepairStartTime != null).Select(x => x.RepairStartTime!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            ["RepairEndTime"] = all.Where(x => x.RepairEndTime != null).Select(x => x.RepairEndTime!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            ["RepairContent"] = all.Where(x => x.RepairContent != null).Select(x => x.RepairContent!).Distinct().OrderBy(x => x).ToList(),
            ["SparePartUsed"] = all.Where(x => x.SparePartUsed != null).Select(x => x.SparePartUsed!).Distinct().OrderBy(x => x).ToList(),
            ["OtherRepairPersons"] = all.Where(x => x.OtherRepairPersons != null).Select(x => x.OtherRepairPersons!).Distinct().OrderBy(x => x).ToList(),
        };
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

    public async Task<List<RepairOrderListDto>> GetPendingByEquipmentAsync(int equipmentId)
    {
        var query = from r in _context.RepairOrders
                    join e in _context.Equipment on r.EquipmentId equals e.Id
                    where r.EquipmentId == equipmentId
                          && r.RepairEndTime == null // 未完成的工单（Pending 或 InProgress）
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
                        Priority = Enum.Parse<RepairPriority>(r.Priority),
                        RepairStatus = r.RepairStartTime != null
                            ? RepairOrderStatus.InProgress
                            : RepairOrderStatus.Pending,
                        ReportPerson = r.ReportPerson,
                        ReportTime = r.ReportTime,
                        RepairPerson = r.RepairPerson,
                        RepairCategory = r.RepairCategory,
                        RepairStartTime = r.RepairStartTime,
                        RepairEndTime = r.RepairEndTime,
                        RepairContent = r.RepairContent,
                        SparePartUsed = r.SparePartUsed,
                        OtherRepairPersons = r.OtherRepairPersons
                    };

        return await query.ToListAsync();
    }

    public async Task<RepairOrderListDto> StartRepairAsync(int id, StartRepairRequest request)
    {
        var entity = await _context.RepairOrders
            .FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) throw new BusinessException("维修工单不存在");
        if (entity.RepairStartTime != null) throw new BusinessException("该工单已开始维修");
        if (entity.RepairEndTime != null) throw new BusinessException("该工单已完成维修");

        entity.RepairPerson = request.RepairPerson;
        entity.RepairStartTime = DateTime.Now;
        entity.RepairStatus = nameof(RepairOrderStatus.InProgress);

        await _context.SaveChangesAsync();
        // 同步更新设备运行状态
        await EquipmentStatusCalculator.RecalculateRunningStatusAsync(_context, entity.EquipmentId);
        return await ToDtoAsync(entity);
    }

    public async Task<RepairOrderListDto> CompleteRepairAsync(int id, CompleteRepairRequest request)
    {
        var entity = await _context.RepairOrders
            .FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) throw new BusinessException("维修工单不存在");
        if (entity.RepairStartTime == null) throw new BusinessException("该工单尚未开始维修，请先开始维修");
        if (entity.RepairEndTime != null) throw new BusinessException("该工单已完成维修");

        entity.RepairCategory = request.RepairCategory;
        entity.RepairContent = request.RepairContent;
        entity.SparePartUsed = request.SparePartUsed;
        entity.RepairEndTime = DateTime.Now;
        entity.RepairStatus = nameof(RepairOrderStatus.Completed);

        // 多人协作：辅助维修人单独存储（不再合并到 RepairPerson）
        if (request.OtherRepairPersons is { Count: > 0 })
        {
            var distinct = request.OtherRepairPersons
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            entity.OtherRepairPersons = distinct.Count > 0 ? string.Join(",", distinct) : null;
        }

        // 回写设备最近维修日期
        var equipment = await _context.Equipment
            .FirstOrDefaultAsync(e => e.Id == entity.EquipmentId);
        if (equipment != null)
        {
            if (!equipment.LastRepairDate.HasValue || entity.RepairEndTime.Value > equipment.LastRepairDate.Value)
                equipment.LastRepairDate = entity.RepairEndTime.Value;
        }

        await _context.SaveChangesAsync();
        // 同步更新设备运行状态
        await EquipmentStatusCalculator.RecalculateRunningStatusAsync(_context, entity.EquipmentId);
        return await ToDtoAsync(entity);
    }

    private static RepairOrderStatus DeriveRepairStatus(DateTime? startTime, DateTime? endTime)
    {
        if (endTime != null) return RepairOrderStatus.Completed;
        if (startTime != null) return RepairOrderStatus.InProgress;
        return RepairOrderStatus.Pending;
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
            Priority = Enum.Parse<RepairPriority>(entity.Priority),
            RepairStatus = DeriveRepairStatus(entity.RepairStartTime, entity.RepairEndTime),
            ReportPerson = entity.ReportPerson,
            ReportTime = entity.ReportTime,
            RepairPerson = entity.RepairPerson,
            RepairCategory = entity.RepairCategory,
            RepairStartTime = entity.RepairStartTime,
            RepairEndTime = entity.RepairEndTime,
            RepairContent = entity.RepairContent,
            SparePartUsed = entity.SparePartUsed,
            OtherRepairPersons = entity.OtherRepairPersons
        };
    }
}
