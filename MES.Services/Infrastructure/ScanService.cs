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
using MES.Core.Constants;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Batch;
using MES.Data.Entities.WorkOrder;

namespace MES.Services.Infrastructure;

/// <summary>
/// 扫码执行服务实现
/// </summary>
public class ScanService : IScanService
{
    private readonly AppDbContext _context;

    /// <summary>
    /// ProcessGroup 属性名 → 工段中文名映射（从 SectionDefs 引用）
    /// </summary>
    private static readonly Dictionary<string, string> SectionNameMap = SectionDefs.PropertyToName;

    /// <summary>
    /// ProcessGroup 中涉及的属性名字段列表（从 SectionDefs 引用）
    /// </summary>
    private static readonly string[] SectionFields = SectionDefs.PropertyNames;

    public ScanService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ScanResolveResultDto> ResolveAsync(string batchNo, int processGroupId)
    {
        var batch = await FindBatchAsync(batchNo);

        var group = await _context.ProcessGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(pg => pg.Id == processGroupId && pg.ProductionBatchId == batch.Id)
            ?? throw new BusinessException($"未找到工序组（ID={processGroupId}）");

        // 查询该批次最大工序组序号，用于判断是否成品
        var maxSequenceNumber = await _context.ProcessGroups
            .Where(pg => pg.ProductionBatchId == batch.Id)
            .MaxAsync(pg => (int?)pg.SequenceNumber) ?? 0;

        var result = BuildResult(batch, group);
        result.IsFinished = IsFinishedManufacturingItem(batch.ManufacturingItem)
            && group.SequenceNumber >= maxSequenceNumber;
        return result;
    }

    /// <summary>
    /// 判断制造物品是否属于'成品'类别（中文含"成品"的3类）
    /// </summary>
    private static bool IsFinishedManufacturingItem(string? manufacturingItem) => manufacturingItem switch
    {
        nameof(MaterialType.OrderFinished) => true,
        nameof(MaterialType.Finished) => true,
        nameof(MaterialType.SpecialDeliveryStatus) => true,
        _ => false
    };

    public async Task<ScanBatchResolveResultDto> GetBatchProcessGroupsAsync(string batchNo)
    {
        var batch = await FindBatchAsync(batchNo);

        var groups = await _context.ProcessGroups
            .AsNoTracking()
            .Where(pg => pg.ProductionBatchId == batch.Id)
            .OrderBy(pg => pg.SequenceNumber)
            .ToListAsync();

        var groupOptions = groups.Select(g => new ProcessGroupOption
        {
            Id = g.Id,
            SequenceNumber = g.SequenceNumber,
            ProcessName = g.ProcessName,
            ManufacturingSpec = g.ManufacturingSpec,
            SectionNames = GetAvailableSectionNames(g)
        }).ToList();

        return new ScanBatchResolveResultDto
        {
            BatchNo = batch.BatchNo,
            Status = batch.Status,
            PlantGrade = batch.PlantGrade,
            Specification = batch.Specification,
            TagNo = batch.TagNo,
            ProductionType = batch.ProductionType,
            ProcessGroups = groupOptions
        };
    }

    /// <summary>
    /// 获取工序组中非 null 工段的中文名称列表
    /// </summary>
    private static List<string> GetAvailableSectionNames(ProcessGroup group)
    {
        var names = new List<string>();
        foreach (var field in SectionFields)
        {
            var value = GetSectionValue(group, field);
            if (value.HasValue)
            {
                var name = SectionNameMap.GetValueOrDefault(field, field);
                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            }
        }
        return names;
    }

    public async Task<ScanEquipmentResolveResultDto> ResolveEquipmentAsync(string equipmentCode)
    {
        var equipment = await _context.Equipment
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EquipmentCode == equipmentCode)
            ?? throw new BusinessException($"未找到设备：{equipmentCode}");

        return new ScanEquipmentResolveResultDto
        {
            EquipmentId = equipment.Id,
            EquipmentCode = equipment.EquipmentCode,
            EquipmentName = equipment.EquipmentName,
            Location = equipment.Location,
            RelatedSection = equipment.RelatedSection,
            ModelNumber = equipment.ModelNumber,
        };
    }

    public async Task<ScanResolveResultDto?> ResolveByBatchAndSectionAsync(string batchNo, string sectionName)
    {
        var batch = await FindBatchAsync(batchNo);

        var groups = await _context.ProcessGroups
            .AsNoTracking()
            .Where(pg => pg.ProductionBatchId == batch.Id)
            .OrderBy(pg => pg.SequenceNumber)
            .ToListAsync();

        // 归一为稳定 Key（兼容中文/别名/Key 入参），作为 ProcessGroup 属性名直接使用
        var propertyName = SectionKeys.ToKey(sectionName);
        if (propertyName == null)
            return null;

        // 找到第一个有此工段的工序组
        foreach (var group in groups)
        {
            var value = GetSectionValue(group, propertyName);
            if (value.HasValue)
            {
                var result = BuildResult(batch, group);
                result.IsFinished = IsFinishedManufacturingItem(batch.ManufacturingItem)
                    && group.SequenceNumber >= groups.Max(g => g.SequenceNumber);
                return result;
            }
        }

        return null;
    }

    private async Task<ProductionBatch> FindBatchAsync(string batchNo)
    {
        return await _context.ProductionBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BatchNo == batchNo)
            ?? throw new BusinessException($"未找到批次：{batchNo}");
    }

    private static ScanResolveResultDto BuildResult(ProductionBatch batch, ProcessGroup group)
    {
        var availableSections = new List<SectionOption>();
        foreach (var field in SectionFields)
        {
            var value = GetSectionValue(group, field);
            if (value.HasValue)
            {
                availableSections.Add(new SectionOption
                {
                    SectionName = SectionNameMap.GetValueOrDefault(field, field),
                    SequenceNumber = value.Value
                });
            }
        }

        availableSections = availableSections.OrderBy(s => s.SequenceNumber).ToList();

        // 计算单支重量（总重量/总支数），用于扫码自动算重
        decimal? unitWeight = null;
        if (batch.TotalQuantity > 0)
        {
            var weight = batch.CurrentValidWeight ?? batch.TotalWeight;
            unitWeight = Math.Round(weight / batch.TotalQuantity, 4);
        }

        return new ScanResolveResultDto
        {
            BatchNo = batch.BatchNo,
            Status = batch.Status,
            PlantGrade = batch.PlantGrade,
            Specification = batch.Specification,
            TagNo = batch.TagNo,
            ProductionType = batch.ProductionType,
            ProcessGroupId = group.Id,
            ProcessName = group.ProcessName,
            ManufacturingSpec = group.ManufacturingSpec,
            AvailableSections = availableSections,
            UnitWeight = unitWeight
        };
    }

    private static int? GetSectionValue(ProcessGroup group, string fieldName)
    {
        return fieldName switch
        {
            nameof(ProcessGroup.ColdRollDraw) => group.ColdRollDraw,
            nameof(ProcessGroup.OilPipeCut) => group.OilPipeCut,
            nameof(ProcessGroup.Degrease) => group.Degrease,
            nameof(ProcessGroup.Solution) => group.Solution,
            nameof(ProcessGroup.Straighten) => group.Straighten,
            nameof(ProcessGroup.Cut) => group.Cut,
            nameof(ProcessGroup.ThicknessMeasure) => group.ThicknessMeasure,
            nameof(ProcessGroup.Pickle) => group.Pickle,
            nameof(ProcessGroup.OuterPolish) => group.OuterPolish,
            nameof(ProcessGroup.InnerGrinding) => group.InnerGrinding,
            nameof(ProcessGroup.OuterSpotGrinding) => group.OuterSpotGrinding,
            nameof(ProcessGroup.Inspection) => group.Inspection,
            nameof(ProcessGroup.WeldingHead) => group.WeldingHead,
            nameof(ProcessGroup.Lubrication) => group.Lubrication,
            nameof(ProcessGroup.Warehouse) => group.Warehouse,
            _ => null
        };
    }

    private static string GetStatusText(BatchStatus status) => status switch
    {
        BatchStatus.None => "未产",
        BatchStatus.InProgress => "在产",
        BatchStatus.Completed => "完成",
        BatchStatus.Suspended => "挂起",
        _ => status.ToString()
    };
}
