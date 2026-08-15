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

public class InspectionRecordService : IInspectionRecordService
{
    private readonly AppDbContext _context;

    public InspectionRecordService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<InspectionRecordListDto>> GetPagedAsync(InspectionRecordQueryParams query)
    {
        // 先 JOIN Equipment 表，使设备字段可用于筛选和排序
        var baseQuery = from r in _context.InspectionRecords
                        join e in _context.Equipment on r.EquipmentId equals e.Id
                        select new { Record = r, Equipment = e };

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            baseQuery = baseQuery.Where(x =>
                x.Record.RecordNo.Contains(kw) ||
                x.Equipment.EquipmentName.Contains(kw) ||
                x.Equipment.EquipmentCode.Contains(kw) ||
                x.Equipment.Location.Contains(kw) ||
                (x.Record.Inspector != null && x.Record.Inspector.Contains(kw)) ||
                (x.Record.ExecutionSummary != null && x.Record.ExecutionSummary.Contains(kw)) ||
                (x.Record.Remark != null && x.Record.Remark.Contains(kw)));
        }

        if (query.EquipmentId.HasValue)
            baseQuery = baseQuery.Where(x => x.Record.EquipmentId == query.EquipmentId.Value);

        // 处理 JOIN 匿名类型 { Record, Equipment } 上的字段筛选：
        // ApplyFilters 通过反射在匿名类型上找不到业务字段属性（只有 Record/Equipment），
        // 故 Equipment 关联字段与 InspectionRecord 全部字段均需手动处理
        if (query.Filters != null)
        {
            // Equipment 关联字段（EquipmentName/EquipmentCode/Location 来自 Equipment 表）
            var equipmentFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EquipmentName", "EquipmentCode", "Location" };
            // InspectionRecord 表自身 string 字段
            var recordStringFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "RecordNo", "Inspector", "ExecutionSummary", "Remark" };
            // InspectionRecord 表自身日期字段
            var recordDateFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
                else if (recordStringFields.Contains(f.Field))
                {
                    if (op == "in" && f.Values?.Count > 0)
                    {
                        var values = f.Values;
                        baseQuery = baseQuery.Where(x => values.Contains(EF.Property<string>(x.Record, f.Field)));
                        handled = true;
                    }
                    else if (op == "contains" && !string.IsNullOrEmpty(f.Value))
                    {
                        var val = f.Value;
                        baseQuery = baseQuery.Where(x => EF.Property<string>(x.Record, f.Field).Contains(val));
                        handled = true;
                    }
                    else if (op == "equals" && !string.IsNullOrEmpty(f.Value))
                    {
                        var val = f.Value;
                        baseQuery = baseQuery.Where(x => EF.Property<string>(x.Record, f.Field) == val);
                        handled = true;
                    }
                }
                else if (recordDateFields.Contains(f.Field))
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
                            baseQuery = baseQuery.Where(x => dates.Contains(EF.Property<DateTime?>(x.Record, "ActualDate")!.Value.Date));
                            handled = true;
                        }
                    }
                    else if (op == "equals" && !string.IsNullOrEmpty(f.Value) && DateTime.TryParse(f.Value, out var eqDate))
                    {
                        baseQuery = baseQuery.Where(x => EF.Property<DateTime?>(x.Record, "ActualDate")!.Value.Date == eqDate.Date);
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
            ("recordno", true) => baseQuery.OrderByDescending(x => x.Record.RecordNo),
            ("recordno", false) => baseQuery.OrderBy(x => x.Record.RecordNo),
            ("actualdate", true) => baseQuery.OrderByDescending(x => x.Record.ActualDate),
            ("actualdate", false) => baseQuery.OrderBy(x => x.Record.ActualDate),
            ("equipmentname", true) => baseQuery.OrderByDescending(x => x.Equipment.EquipmentName),
            ("equipmentname", false) => baseQuery.OrderBy(x => x.Equipment.EquipmentName),
            ("equipmentcode", true) => baseQuery.OrderByDescending(x => x.Equipment.EquipmentCode),
            ("equipmentcode", false) => baseQuery.OrderBy(x => x.Equipment.EquipmentCode),
            ("location", true) => baseQuery.OrderByDescending(x => x.Equipment.Location),
            ("location", false) => baseQuery.OrderBy(x => x.Equipment.Location),
            ("inspector", true) => baseQuery.OrderByDescending(x => x.Record.Inspector ?? ""),
            ("inspector", false) => baseQuery.OrderBy(x => x.Record.Inspector ?? ""),
            ("executionsummary", true) => baseQuery.OrderByDescending(x => x.Record.ExecutionSummary ?? ""),
            ("executionsummary", false) => baseQuery.OrderBy(x => x.Record.ExecutionSummary ?? ""),
            ("remark", true) => baseQuery.OrderByDescending(x => x.Record.Remark ?? ""),
            ("remark", false) => baseQuery.OrderBy(x => x.Record.Remark ?? ""),
            _ when query.IsDescending => baseQuery.OrderByDescending(x => x.Record.Id),
            _ => baseQuery.OrderBy(x => x.Record.Id)
        };

        var items = await baseQuery
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(x => new InspectionRecordListDto
            {
                Id = x.Record.Id,
                RecordNo = x.Record.RecordNo,
                EquipmentId = x.Record.EquipmentId,
                EquipmentName = x.Equipment.EquipmentName,
                EquipmentCode = x.Equipment.EquipmentCode,
                Location = x.Equipment.Location,
                ActualDate = x.Record.ActualDate,
                Inspector = x.Record.Inspector,
                ExecutionSummary = x.Record.ExecutionSummary,
                Remark = x.Record.Remark
            })
            .ToListAsync();

        return new PagedResult<InspectionRecordListDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var records = _context.InspectionRecords.AsNoTracking();
        var equipment = _context.Equipment.AsNoTracking();

        return new Dictionary<string, List<string>>
        {
            ["RecordNo"] = await records.Select(r => r.RecordNo).Distinct().OrderBy(x => x).ToListAsync(),
            ["EquipmentName"] = await equipment.Where(e => e.EquipmentName != null).Select(e => e.EquipmentName).Distinct().OrderBy(x => x).ToListAsync(),
            ["EquipmentCode"] = await equipment.Where(e => e.EquipmentCode != null).Select(e => e.EquipmentCode).Distinct().OrderBy(x => x).ToListAsync(),
            ["Location"] = await equipment.Where(e => e.Location != null).Select(e => e.Location).Distinct().OrderBy(x => x).ToListAsync(),
            ["ActualDate"] = (await records.Where(r => r.ActualDate != null).Select(r => r.ActualDate!.Value.Date).Distinct().OrderBy(x => x).ToListAsync()).Select(d => d.ToString("yyyy-MM-dd")).ToList(),
            ["Inspector"] = await records.Where(r => r.Inspector != null).Select(r => r.Inspector!).Distinct().OrderBy(x => x).ToListAsync(),
            ["ExecutionSummary"] = await records.Where(r => r.ExecutionSummary != null).Select(r => r.ExecutionSummary!).Distinct().OrderBy(x => x).ToListAsync(),
            ["Remark"] = await records.Where(r => r.Remark != null).Select(r => r.Remark!).Distinct().OrderBy(x => x).ToListAsync(),
        };
    }

    public async Task<List<InspectionRecordListDto>> GetAllListAsync()
    {
        var baseQuery = from r in _context.InspectionRecords
                        join e in _context.Equipment on r.EquipmentId equals e.Id
                        orderby r.Id descending
                        select new InspectionRecordListDto
                        {
                            Id = r.Id,
                            RecordNo = r.RecordNo,
                            EquipmentId = r.EquipmentId,
                            EquipmentName = e.EquipmentName,
                            EquipmentCode = e.EquipmentCode,
                            Location = e.Location,
                            ActualDate = r.ActualDate,
                            Inspector = r.Inspector,
                            ExecutionSummary = r.ExecutionSummary,
                            Remark = r.Remark
                        };

        return await baseQuery.ToListAsync();
    }

    public async Task<InspectionRecordListDto?> GetByIdAsync(int id)
    {
        var entity = await _context.InspectionRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) throw new BusinessException("点检记录不存在");
        return await ToDtoAsync(entity);
    }

    public async Task<InspectionRecordListDto> CreateAsync(CreateInspectionRecordRequest request)
    {
        var equipment = await _context.Equipment
            .FirstOrDefaultAsync(e => e.Id == request.EquipmentId);
        if (equipment == null) throw new BusinessException("设备不存在");

        var recordNo = await GenerateRecordNoAsync("DJ");

        var entity = new InspectionRecord
        {
            RecordNo = recordNo,
            EquipmentId = request.EquipmentId,
            ActualDate = request.ActualDate,
            Inspector = request.Inspector,
            ExecutionSummary = request.ExecutionSummary,
            Remark = request.Remark
        };

        _context.InspectionRecords.Add(entity);

        // 回写设备点检参数
        if (request.ActualDate.HasValue)
        {
            if (!equipment.LastInspectionDate.HasValue || request.ActualDate.Value > equipment.LastInspectionDate.Value)
                equipment.LastInspectionDate = request.ActualDate.Value;
            if (equipment.CurrentInspectionStartDate != null)
            {
                // 推进到下个周期
                equipment.CurrentInspectionStartDate = equipment.CurrentInspectionStartDate.Value.AddDays(equipment.InspectionCycleDays);
            }
        }

        await _context.SaveChangesAsync();
        // 同步更新设备点检状况
        await EquipmentStatusCalculator.RecalculateInspectionStatusAsync(_context, entity.EquipmentId);
        return await ToDtoAsync(entity);
    }

    public async Task<List<InspectionRecordListDto>> CreateBatchAsync(List<CreateInspectionRecordRequest> requests)
    {
        if (requests.Count == 0) return new List<InspectionRecordListDto>();

        var results = new List<InspectionRecordListDto>();
        foreach (var request in requests)
        {
            var dto = await CreateAsync(request);
            results.Add(dto);
        }
        return results;
    }

    public async Task<InspectionRecordListDto> UpdateAsync(int id, UpdateInspectionRequest request)
    {
        var entity = await _context.InspectionRecords
            .FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) throw new BusinessException("点检记录不存在");

        entity.ActualDate = request.ActualDate ?? entity.ActualDate;
        if (request.Inspector != null) entity.Inspector = request.Inspector;
        if (request.ExecutionSummary != null) entity.ExecutionSummary = request.ExecutionSummary;
        if (request.Remark != null) entity.Remark = request.Remark;

        // ActualDate 有值时回写设备点检参数
        if (request.ActualDate.HasValue)
        {
            var equipment = await _context.Equipment
                .FirstOrDefaultAsync(e => e.Id == entity.EquipmentId);
            if (equipment != null)
            {
                if (!equipment.LastInspectionDate.HasValue || request.ActualDate.Value > equipment.LastInspectionDate.Value)
                    equipment.LastInspectionDate = request.ActualDate.Value;
                if (equipment.CurrentInspectionStartDate != null)
                {
                    equipment.CurrentInspectionStartDate = equipment.CurrentInspectionStartDate.Value.AddDays(equipment.InspectionCycleDays);
                }
                equipment.InspectionPerson ??= request.Inspector;
            }
        }

        await _context.SaveChangesAsync();
        // 同步更新设备点检状况
        await EquipmentStatusCalculator.RecalculateInspectionStatusAsync(_context, entity.EquipmentId);
        return await ToDtoAsync(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.InspectionRecords
            .FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) throw new BusinessException("点检记录不存在");

        _context.InspectionRecords.Remove(entity);
        await _context.SaveChangesAsync();
        // 同步更新设备点检状况
        await EquipmentStatusCalculator.RecalculateInspectionStatusAsync(_context, entity.EquipmentId);
    }

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new InspectionRecordQueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue
        };
        var result = await GetPagedAsync(query);
        var selected = result.Items.Where(i => ids.Contains(i.Id)).ToList();
        return InspectionRecordPrintHelper.GenerateBatchPdf(selected, columns);
    }

    public async Task<byte[]> PrintAllAsync(InspectionRecordQueryParams query, List<PrintColumnDef> columns)
    {
        query.PageIndex = 1;
        query.PageSize = int.MaxValue;
        var result = await GetPagedAsync(query);
        return InspectionRecordPrintHelper.GenerateBatchPdf(result.Items, columns);
    }

    private async Task<string> GenerateRecordNoAsync(string prefix)
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var pattern = $"{prefix}-{today}-";

        var maxNo = await _context.InspectionRecords
            .Where(r => r.RecordNo.StartsWith(pattern))
            .OrderByDescending(r => r.RecordNo)
            .Select(r => r.RecordNo)
            .FirstOrDefaultAsync();

        if (maxNo == null) return $"{pattern}001";

        var seq = int.Parse(maxNo[^3..]) + 1;
        return $"{pattern}{seq:D3}";
    }

    private async Task<InspectionRecordListDto> ToDtoAsync(InspectionRecord entity)
    {
        var equipment = await _context.Equipment
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entity.EquipmentId);

        return new InspectionRecordListDto
        {
            Id = entity.Id,
            RecordNo = entity.RecordNo,
            EquipmentId = entity.EquipmentId,
            EquipmentName = equipment?.EquipmentName ?? "",
            EquipmentCode = equipment?.EquipmentCode,
            Location = equipment?.Location,
            ActualDate = entity.ActualDate,
            Inspector = entity.Inspector,
            ExecutionSummary = entity.ExecutionSummary,
            Remark = entity.Remark
        };
    }
}
