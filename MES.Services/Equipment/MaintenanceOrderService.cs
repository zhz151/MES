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

        // 处理 JOIN 匿名类型 { Order, Equipment } 上的字段筛选：
        // ApplyFilters 通过反射在匿名类型上找不到业务字段属性（只有 Order/Equipment），
        // 故 Equipment 关联字段与 MaintenanceOrder 全部字段均需手动处理
        if (query.Filters != null)
        {
            // Equipment 关联字段（EquipmentName/EquipmentCode/Location 来自 Equipment 表）
            var equipmentFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EquipmentName", "EquipmentCode", "Location" };
            // MaintenanceOrder 表自身 string 字段
            var orderStringFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "MaintOrderNo", "Executor", "ExecutionSummary", "Remark" };
            // MaintenanceOrder 表自身日期字段
            var orderDateFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "ActualDate" };

            foreach (var f in query.Filters.ToList())
            {
                if (string.IsNullOrWhiteSpace(f.Field)) continue;
                var op = f.Operator?.ToLowerInvariant() ?? "contains";
                var handled = false;

                if (equipmentFields.Contains(f.Field))
                {
                    if (op == "in" && f.Values?.Count > 0)
                    {
                        var values = f.Values;
                        var fieldName = f.Field;
                        baseQuery = baseQuery.Where(x => values.Contains(EF.Property<string>(x.Equipment, fieldName)));
                        handled = true;
                    }
                    else if (op == "contains" && !string.IsNullOrEmpty(f.Value))
                    {
                        var val = f.Value;
                        var fieldName = f.Field;
                        baseQuery = baseQuery.Where(x => EF.Property<string>(x.Equipment, fieldName).Contains(val));
                        handled = true;
                    }
                    else if (op == "equals" && !string.IsNullOrEmpty(f.Value))
                    {
                        var val = f.Value;
                        var fieldName = f.Field;
                        baseQuery = baseQuery.Where(x => EF.Property<string>(x.Equipment, fieldName) == val);
                        handled = true;
                    }
                }
                else if (orderStringFields.Contains(f.Field))
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
                else if (orderDateFields.Contains(f.Field))
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
                            baseQuery = baseQuery.Where(x => dates.Contains(EF.Property<DateTime?>(x.Order, "ActualDate")!.Value.Date));
                            handled = true;
                        }
                    }
                    else if (op == "equals" && !string.IsNullOrEmpty(f.Value) && DateTime.TryParse(f.Value, out var eqDate))
                    {
                        baseQuery = baseQuery.Where(x => EF.Property<DateTime?>(x.Order, "ActualDate")!.Value.Date == eqDate.Date);
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
            ("executionsummary", true) => baseQuery.OrderByDescending(x => x.Order.ExecutionSummary ?? ""),
            ("executionsummary", false) => baseQuery.OrderBy(x => x.Order.ExecutionSummary ?? ""),
            ("remark", true) => baseQuery.OrderByDescending(x => x.Order.Remark ?? ""),
            ("remark", false) => baseQuery.OrderBy(x => x.Order.Remark ?? ""),
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
        // 同步更新设备保养状况
        await EquipmentStatusCalculator.RecalculateMaintStatusAsync(_context, entity.EquipmentId);
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

        entity.ActualDate = request.ActualDate ?? entity.ActualDate;
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
        // 同步更新设备保养状况
        await EquipmentStatusCalculator.RecalculateMaintStatusAsync(_context, entity.EquipmentId);
        return await ToDtoAsync(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.MaintenanceOrders
            .FirstOrDefaultAsync(m => m.Id == id);
        if (entity == null) throw new BusinessException("保养工单不存在");

        var equipmentId = entity.EquipmentId;
        _context.MaintenanceOrders.Remove(entity);
        await _context.SaveChangesAsync();

        // 回退设备最近保养日期快照：删除后按剩余保养单 ActualDate 最大值重算，不再残留已删记录的日期
        var equipment = await _context.Equipment.FirstOrDefaultAsync(e => e.Id == equipmentId);
        if (equipment != null)
        {
            var lastMaintDate = (await _context.MaintenanceOrders
                .AsNoTracking()
                .Where(m => m.EquipmentId == equipmentId && m.ActualDate != null)
                .Select(m => (DateTime?)m.ActualDate)
                .ToListAsync()).Max();
            equipment.LastMaintDate = lastMaintDate;
            await _context.SaveChangesAsync();
        }

        // 同步更新设备保养状况
        await EquipmentStatusCalculator.RecalculateMaintStatusAsync(_context, equipmentId);
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var query = from m in _context.MaintenanceOrders.AsNoTracking()
                    join e in _context.Equipment.AsNoTracking() on m.EquipmentId equals e.Id
                    select new
                    {
                        m.MaintOrderNo,
                        m.ActualDate,
                        m.Executor,
                        m.ExecutionSummary,
                        m.Remark,
                        e.EquipmentName,
                        e.EquipmentCode,
                        e.Location
                    };

        var all = await query.ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["MaintOrderNo"] = all.Select(x => x.MaintOrderNo).Distinct().OrderBy(x => x).ToList(),
            ["EquipmentName"] = all.Select(x => x.EquipmentName).Distinct().OrderBy(x => x).ToList(),
            ["EquipmentCode"] = all.Select(x => x.EquipmentCode).Distinct().OrderBy(x => x).ToList(),
            ["Location"] = all.Where(x => x.Location != null).Select(x => x.Location!).Distinct().OrderBy(x => x).ToList(),
            ["ActualDate"] = all.Where(x => x.ActualDate != null).Select(x => x.ActualDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            ["Executor"] = all.Where(x => x.Executor != null).Select(x => x.Executor!).Distinct().OrderBy(x => x).ToList(),
            ["ExecutionSummary"] = all.Where(x => x.ExecutionSummary != null).Select(x => x.ExecutionSummary!).Distinct().OrderBy(x => x).ToList(),
            ["Remark"] = all.Where(x => x.Remark != null).Select(x => x.Remark!).Distinct().OrderBy(x => x).ToList(),
        };
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
