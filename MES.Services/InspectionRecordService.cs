using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;

using MES.Services.Printing;

namespace MES.Services;

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

    public async Task<InspectionRecordListDto> GetByIdAsync(int id)
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
        return await ToDtoAsync(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.InspectionRecords
            .FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) throw new BusinessException("点检记录不存在");

        _context.InspectionRecords.Remove(entity);
        await _context.SaveChangesAsync();
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
