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

public class EquipmentService : IEquipmentService
{
    private readonly AppDbContext _context;

    public EquipmentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<EquipmentListDto>> GetPagedAsync(EquipmentQueryParams query)
    {
        var q = _context.Equipment
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            q = q.Where(e =>
                e.EquipmentCode.Contains(kw) ||
                e.EquipmentName.Contains(kw) ||
                (e.ModelNumber != null && e.ModelNumber.Contains(kw)) ||
                (e.TechnicalParams != null && e.TechnicalParams.Contains(kw)) ||
                (e.Manufacturer != null && e.Manufacturer.Contains(kw)) ||
                (e.Location != null && e.Location.Contains(kw)) ||
                (e.RelatedSection != null && e.RelatedSection.Contains(kw)) ||
                (e.Remark != null && e.Remark.Contains(kw)) ||
                (e.InspectionPerson != null && e.InspectionPerson.Contains(kw)) ||
                (e.MaintPerson != null && e.MaintPerson.Contains(kw)) ||
                e.LifecycleStatus.Contains(kw) ||
                e.UsageType.Contains(kw));
        }

        if (!string.IsNullOrEmpty(query.LifecycleStatus))
            q = q.Where(e => e.LifecycleStatus == query.LifecycleStatus);
        if (!string.IsNullOrEmpty(query.UsageType))
            q = q.Where(e => e.UsageType == query.UsageType);
        if (!string.IsNullOrEmpty(query.Location))
            q = q.Where(e => e.Location == query.Location);
        if (!string.IsNullOrEmpty(query.RelatedSection))
            q = q.Where(e => e.RelatedSection == query.RelatedSection);

        var filterRunningStatus = query.RunningStatus;
        var filterInspectionStatus = query.InspectionStatus;
        var filterMaintStatus = query.MaintStatus;

        q = q.ApplyFilters(query.Filters);
        q = q.ApplySort(query.SortBy ?? "equipmentcode", query.IsDescending);

        var totalCount = await q.CountAsync();
        var entities = await q
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        var items = await ComputeStatusesAsync(entities);

        if (!string.IsNullOrEmpty(filterRunningStatus))
            items = items.Where(e => e.RunningStatus == filterRunningStatus).ToList();
        if (!string.IsNullOrEmpty(filterInspectionStatus))
            items = items.Where(e => e.InspectionStatus == filterInspectionStatus).ToList();
        if (!string.IsNullOrEmpty(filterMaintStatus))
            items = items.Where(e => e.MaintStatus == filterMaintStatus).ToList();

        return new PagedResult<EquipmentListDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<EquipmentListDto>> GetAllListAsync()
    {
        var entities = await _context.Equipment
            .AsNoTracking()
            .ToListAsync();

        return await ComputeStatusesAsync(entities);
    }

    public async Task<List<EquipmentListDto>> GetAllAsync()
    {
        var entities = await _context.Equipment
            .AsNoTracking()
            .Where(e => e.LifecycleStatus != nameof(LifecycleStatus.Scrapped))
            .OrderBy(e => e.EquipmentCode)
            .ToListAsync();

        return await ComputeStatusesAsync(entities);
    }

    public async Task<EquipmentDetailDto> GetByIdAsync(int id)
    {
        var entity = await _context.Equipment
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);
        if (entity == null) throw new BusinessException("设备不存在");

        var list = await ComputeStatusesAsync(new List<Equipment> { entity });
        return ToDetailDto(list[0]);
    }

    public async Task<EquipmentDetailDto> CreateAsync(CreateEquipmentRequest request)
    {
        var exists = await _context.Equipment
            .AnyAsync(e => e.EquipmentCode == request.EquipmentCode);
        if (exists) throw new BusinessException($"设备编号 {request.EquipmentCode} 已存在");

        var entity = new Equipment
        {
            EquipmentCode = request.EquipmentCode,
            EquipmentName = request.EquipmentName,
            ModelNumber = request.ModelNumber,
            TechnicalParams = request.TechnicalParams,
            Manufacturer = request.Manufacturer,
            InstallationDate = request.InstallationDate,
            Remark = request.Remark,
            Location = request.Location,
            RelatedSection = request.RelatedSection,
            NeedInspection = request.NeedInspection,
            InspectionPerson = request.InspectionPerson,
            InspectionCycleDays = request.InspectionCycleDays,
            CurrentInspectionStartDate = request.CurrentInspectionStartDate,
            NeedMaintenance = request.NeedMaintenance,
            MaintPerson = request.MaintPerson,
            MaintCycleDays = request.MaintCycleDays,
            CurrentMaintStartDate = request.CurrentMaintStartDate,
            LifecycleStatus = request.LifecycleStatus,
            UsageType = request.UsageType
        };

        _context.Equipment.Add(entity);
        await _context.SaveChangesAsync();

        var list = await ComputeStatusesAsync(new List<Equipment> { entity });
        return ToDetailDto(list[0]);
    }

    public async Task<EquipmentDetailDto> UpdateAsync(int id, UpdateEquipmentRequest request)
    {
        var entity = await _context.Equipment
            .FirstOrDefaultAsync(e => e.Id == id);
        if (entity == null) throw new BusinessException("设备不存在");

        if (entity.EquipmentCode != request.EquipmentCode)
        {
            var exists = await _context.Equipment
                .AnyAsync(e => e.EquipmentCode == request.EquipmentCode && e.Id != id);
            if (exists) throw new BusinessException($"设备编号 {request.EquipmentCode} 已存在");
        }

        entity.EquipmentCode = request.EquipmentCode;
        entity.EquipmentName = request.EquipmentName;
        entity.ModelNumber = request.ModelNumber ?? entity.ModelNumber;
        entity.TechnicalParams = request.TechnicalParams ?? entity.TechnicalParams;
        entity.Manufacturer = request.Manufacturer ?? entity.Manufacturer;
        entity.InstallationDate = request.InstallationDate ?? entity.InstallationDate;
        entity.Remark = request.Remark ?? entity.Remark;
        entity.Location = request.Location;
        entity.RelatedSection = request.RelatedSection ?? entity.RelatedSection;

        entity.NeedInspection = request.NeedInspection;
        entity.InspectionPerson = request.InspectionPerson ?? entity.InspectionPerson;
        entity.InspectionCycleDays = request.InspectionCycleDays;
        entity.CurrentInspectionStartDate = request.CurrentInspectionStartDate ?? entity.CurrentInspectionStartDate;

        entity.NeedMaintenance = request.NeedMaintenance;
        entity.MaintPerson = request.MaintPerson ?? entity.MaintPerson;
        entity.MaintCycleDays = request.MaintCycleDays;
        entity.CurrentMaintStartDate = request.CurrentMaintStartDate ?? entity.CurrentMaintStartDate;

        entity.LifecycleStatus = request.LifecycleStatus;
        entity.UsageType = request.UsageType;

        await _context.SaveChangesAsync();

        var list = await ComputeStatusesAsync(new List<Equipment> { entity });
        return ToDetailDto(list[0]);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Equipment
            .FirstOrDefaultAsync(e => e.Id == id);
        if (entity == null) throw new BusinessException("设备不存在");

        _context.Equipment.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private async Task<List<EquipmentListDto>> ComputeStatusesAsync(List<Equipment> entities)
    {
        if (entities.Count == 0) return new List<EquipmentListDto>();

        var ids = entities.Select(e => e.Id).ToList();
        var today = DateTime.Today;

        // 批量加载关联记录
        var inspectionByEquipment = await _context.InspectionRecords
            .AsNoTracking()
            .Where(r => ids.Contains(r.EquipmentId) && r.ActualDate != null)
            .Select(r => new { r.EquipmentId, r.ActualDate })
            .ToListAsync();

        var maintByEquipment = await _context.MaintenanceOrders
            .AsNoTracking()
            .Where(m => ids.Contains(m.EquipmentId) && m.ActualDate != null)
            .Select(m => new { m.EquipmentId, m.ActualDate })
            .ToListAsync();

        var repairByEquipment = await _context.RepairOrders
            .AsNoTracking()
            .Where(r => ids.Contains(r.EquipmentId))
            .Select(r => new { r.EquipmentId, r.RepairStartTime, r.RepairEndTime, r.CreatedTime })
            .ToListAsync();

        var inspectionLookup = inspectionByEquipment
            .GroupBy(x => x.EquipmentId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ActualDate!.Value).ToList());

        var maintLookup = maintByEquipment
            .GroupBy(x => x.EquipmentId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ActualDate!.Value).ToList());

        var repairLookup = repairByEquipment
            .GroupBy(x => x.EquipmentId)
            .ToDictionary(g => g.Key, g => g
                .OrderByDescending(x => x.RepairStartTime ?? x.RepairEndTime ?? x.CreatedTime.DateTime)
                .Select(x => (x.RepairStartTime, x.RepairEndTime))
                .ToList());

        return entities.Select(entity =>
        {
            var dto = ToListDto(entity);

            var inspectionDates = inspectionLookup.GetValueOrDefault(entity.Id) ?? new List<DateTime>();
            var maintDates = maintLookup.GetValueOrDefault(entity.Id) ?? new List<DateTime>();
            var repairs = repairLookup.GetValueOrDefault(entity.Id) ?? new List<(DateTime? RepairStartTime, DateTime? RepairEndTime)>();

            dto.InspectionStatus = ComputeTaskStatus(
                entity.NeedInspection,
                entity.CurrentInspectionStartDate,
                entity.InspectionCycleDays,
                inspectionDates,
                today);

            dto.MaintStatus = ComputeTaskStatus(
                entity.NeedMaintenance,
                entity.CurrentMaintStartDate,
                entity.MaintCycleDays,
                maintDates,
                today);

            dto.RunningStatus = ComputeRunningStatus(repairs);

            return dto;
        }).ToList();
    }

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var items = await GetPagedAsync(new EquipmentQueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue
        });
        var selected = items.Items.Where(i => ids.Contains(i.Id)).ToList();
        return EquipmentPrintHelper.GenerateBatchPdf(selected, columns);
    }

    public async Task<byte[]> PrintAllAsync(EquipmentQueryParams query, List<PrintColumnDef> columns)
    {
        query.PageIndex = 1;
        query.PageSize = int.MaxValue;
        var result = await GetPagedAsync(query);
        return EquipmentPrintHelper.GenerateBatchPdf(result.Items, columns);
    }

    private static string ComputeTaskStatus(
        bool needTask,
        DateTime? currentStartDate,
        int cycleDays,
        List<DateTime> actualDates,
        DateTime today)
    {
        if (!needTask) return nameof(EquipmentTaskStatus.NotApplicable);
        if (currentStartDate == null) return nameof(EquipmentTaskStatus.Pending);

        // 如果今天 < 起始日，说明时间窗还未开始 → 正常（此逻辑优先）
        if (today < currentStartDate) return nameof(EquipmentTaskStatus.Normal);

        var periodEnd = currentStartDate.Value.AddDays(cycleDays - 1);
        var hasRecord = actualDates.Any(ad => ad >= currentStartDate && ad <= periodEnd);

        if (hasRecord) return nameof(EquipmentTaskStatus.Normal);
        if (today > periodEnd) return nameof(EquipmentTaskStatus.Overdue);
        return nameof(EquipmentTaskStatus.Pending);
    }

    private static string ComputeRunningStatus(List<(DateTime? RepairStartTime, DateTime? RepairEndTime)> repairs)
    {
        if (repairs.Count == 0) return nameof(RunningStatus.Normal);

        var latest = repairs[0];
        if (latest.RepairEndTime != null) return nameof(RunningStatus.Normal);
        if (latest.RepairStartTime != null) return nameof(RunningStatus.InProgress);
        return nameof(RunningStatus.Pending);
    }

    private static EquipmentListDto ToListDto(Equipment e) => new()
    {
        Id = e.Id,
        EquipmentCode = e.EquipmentCode,
        EquipmentName = e.EquipmentName,
        ModelNumber = e.ModelNumber,
        TechnicalParams = e.TechnicalParams,
        Manufacturer = e.Manufacturer,
        InstallationDate = e.InstallationDate,
        Remark = e.Remark,
        Location = e.Location,
        RelatedSection = e.RelatedSection,
        NeedInspection = e.NeedInspection,
        InspectionPerson = e.InspectionPerson,
        InspectionCycleDays = e.InspectionCycleDays,
        LastInspectionDate = e.LastInspectionDate,
        CurrentInspectionStartDate = e.CurrentInspectionStartDate,
        NeedMaintenance = e.NeedMaintenance,
        MaintPerson = e.MaintPerson,
        MaintCycleDays = e.MaintCycleDays,
        LastMaintDate = e.LastMaintDate,
        CurrentMaintStartDate = e.CurrentMaintStartDate,
        LastRepairDate = e.LastRepairDate,
        LifecycleStatus = e.LifecycleStatus,
        UsageType = e.UsageType,
        CreatedTime = e.CreatedTime,
        UpdatedTime = e.UpdatedTime,
        InspectionStatus = nameof(EquipmentTaskStatus.NotApplicable),
        MaintStatus = nameof(EquipmentTaskStatus.NotApplicable),
        RunningStatus = nameof(RunningStatus.Normal)
    };

    private static EquipmentDetailDto ToDetailDto(EquipmentListDto dto)
    {
        var detail = new EquipmentDetailDto();
        foreach (var prop in typeof(EquipmentListDto).GetProperties())
        {
            prop.SetValue(detail, prop.GetValue(dto));
        }
        return detail;
    }
}
