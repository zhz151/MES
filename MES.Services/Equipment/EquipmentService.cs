using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.ProductionStandard;
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
using MES.Core.Interfaces.ProductionStandard;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.ProductionStandard;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Equipment;

using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services.Equipment;

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

        // 物化状态字段现在直接存在 Equipment 表中，可直接在 DB 层筛选
        if (!string.IsNullOrEmpty(query.RunningStatus))
            q = q.Where(e => e.RunningStatus == query.RunningStatus);
        if (!string.IsNullOrEmpty(query.InspectionStatus))
            q = q.Where(e => e.InspectionStatus == query.InspectionStatus);
        if (!string.IsNullOrEmpty(query.MaintStatus))
            q = q.Where(e => e.MaintStatus == query.MaintStatus);

        // ExcelFilter 筛选（物化状态字段 ApplyFilters 可直接找到）
        q = q.ApplyFilters(query.Filters);
        q = q.ApplySort(query.SortBy ?? "equipmentcode", query.IsDescending);

        var totalCount = await q.CountAsync();

        var items = await q
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(e => new EquipmentListDto
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
                RunningStatus = e.RunningStatus,
                InspectionStatus = e.InspectionStatus,
                MaintStatus = e.MaintStatus,
                CreatedTime = e.CreatedTime,
                UpdatedTime = e.UpdatedTime,
            })
            .ToListAsync();

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
        return await _context.Equipment
            .AsNoTracking()
            .Select(e => ToDto(e))
            .ToListAsync();
    }

    public async Task<List<EquipmentListDto>> GetAllAsync()
    {
        return await _context.Equipment
            .AsNoTracking()
            .Where(e => e.LifecycleStatus != nameof(LifecycleStatus.Scrapped))
            .OrderBy(e => e.EquipmentCode)
            .Select(e => ToDto(e))
            .ToListAsync();
    }

    public async Task<EquipmentDetailDto> GetByIdAsync(int id)
    {
        var entity = await _context.Equipment
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);
        if (entity == null) throw new BusinessException("设备不存在");

        return ToDetailDto(ToDto(entity));
    }

    public async Task<EquipmentDetailDto> CreateAsync(CreateEquipmentRequest request)
    {
        var exists = await _context.Equipment
            .AnyAsync(e => e.EquipmentCode == request.EquipmentCode);
        if (exists) throw new BusinessException($"设备编号 {request.EquipmentCode} 已存在");

        var entity = new MES.Data.Entities.Equipment.Equipment
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

        // 创建后计算并持久化初始物化状态
        await EquipmentStatusCalculator.RecalculateAndSaveAsync(_context, entity.Id);

        return ToDetailDto(ToDto(entity));
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

        // 更新后重算物化状态（点检/保养参数可能已变）
        await EquipmentStatusCalculator.RecalculateAndSaveAsync(_context, entity.Id);

        return ToDetailDto(ToDto(entity));
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Equipment
            .FirstOrDefaultAsync(e => e.Id == id);
        if (entity == null) throw new BusinessException("设备不存在");

        _context.Equipment.Remove(entity);
        await _context.SaveChangesAsync();
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

    private static EquipmentListDto ToDto(MES.Data.Entities.Equipment.Equipment e) => new()
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
        RunningStatus = e.RunningStatus,
        InspectionStatus = e.InspectionStatus,
        MaintStatus = e.MaintStatus,
        CreatedTime = e.CreatedTime,
        UpdatedTime = e.UpdatedTime,
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

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var contexts = new Dictionary<string, List<string>>();

        contexts["EquipmentCode"] = await _context.Equipment
            .AsNoTracking().Where(e => e.EquipmentCode != null)
            .Select(e => e.EquipmentCode).Distinct().ToListAsync()!;
        contexts["EquipmentName"] = await _context.Equipment
            .AsNoTracking().Where(e => e.EquipmentName != null)
            .Select(e => e.EquipmentName).Distinct().ToListAsync()!;
        contexts["ModelNumber"] = await _context.Equipment
            .AsNoTracking().Where(e => e.ModelNumber != null)
            .Select(e => e.ModelNumber!).Distinct().ToListAsync()!;
        contexts["Location"] = await _context.Equipment
            .AsNoTracking().Where(e => e.Location != null)
            .Select(e => e.Location!).Distinct().ToListAsync()!;
        contexts["RelatedSection"] = await _context.Equipment
            .AsNoTracking().Where(e => e.RelatedSection != null)
            .Select(e => e.RelatedSection!).Distinct().ToListAsync()!;

        return contexts;
    }
}
