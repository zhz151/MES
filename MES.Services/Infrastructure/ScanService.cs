using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Constants;
using MES.Data;

namespace MES.Services;

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

        return BuildResult(batch, group);
    }

    public async Task<ScanBatchResolveResultDto> GetBatchProcessGroupsAsync(string batchNo)
    {
        var batch = await FindBatchAsync(batchNo);

        var groups = await _context.ProcessGroups
            .AsNoTracking()
            .Where(pg => pg.ProductionBatchId == batch.Id)
            .OrderBy(pg => pg.SequenceNumber)
            .Select(pg => new ProcessGroupOption
            {
                Id = pg.Id,
                SequenceNumber = pg.SequenceNumber,
                ProcessName = pg.ProcessName,
                ManufacturingSpec = pg.ManufacturingSpec
            })
            .ToListAsync();

        return new ScanBatchResolveResultDto
        {
            BatchNo = batch.BatchNo,
            Status = GetStatusText(batch.Status),
            PlantGrade = batch.PlantGrade,
            Specification = batch.Specification,
            TagNo = batch.TagNo,
            ProductionType = batch.ProductionType,
            ProcessGroups = groups
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

        // 反向查找：SectionNameMap 是 PropertyName→ChineseName
        // 建立 ChineseName→PropertyName 的反向映射
        var reverseMap = SectionNameMap
            .GroupBy(kv => kv.Value, kv => kv.Key)
            .ToDictionary(g => g.Key, g => g.First());

        if (!reverseMap.TryGetValue(sectionName, out var propertyName))
            return null;

        // 找到第一个有此工段的工序组
        foreach (var group in groups)
        {
            var value = GetSectionValue(group, propertyName);
            if (value.HasValue)
            {
                return BuildResult(batch, group);
            }
        }

        return null;
    }

    private async Task<Data.Entities.ProductionBatch> FindBatchAsync(string batchNo)
    {
        return await _context.ProductionBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BatchNo == batchNo)
            ?? throw new BusinessException($"未找到批次：{batchNo}");
    }

    private static ScanResolveResultDto BuildResult(Data.Entities.ProductionBatch batch, Data.Entities.ProcessGroup group)
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

        return new ScanResolveResultDto
        {
            BatchNo = batch.BatchNo,
            Status = GetStatusText(batch.Status),
            PlantGrade = batch.PlantGrade,
            Specification = batch.Specification,
            TagNo = batch.TagNo,
            ProductionType = batch.ProductionType,
            ProcessGroupId = group.Id,
            ProcessName = group.ProcessName,
            ManufacturingSpec = group.ManufacturingSpec,
            AvailableSections = availableSections
        };
    }

    private static int? GetSectionValue(Data.Entities.ProcessGroup group, string fieldName)
    {
        return fieldName switch
        {
            nameof(Data.Entities.ProcessGroup.ColdRollDraw) => group.ColdRollDraw,
            nameof(Data.Entities.ProcessGroup.OilPipeCut) => group.OilPipeCut,
            nameof(Data.Entities.ProcessGroup.Degrease) => group.Degrease,
            nameof(Data.Entities.ProcessGroup.Solution) => group.Solution,
            nameof(Data.Entities.ProcessGroup.Straighten) => group.Straighten,
            nameof(Data.Entities.ProcessGroup.Cut) => group.Cut,
            nameof(Data.Entities.ProcessGroup.ThicknessMeasure) => group.ThicknessMeasure,
            nameof(Data.Entities.ProcessGroup.Pickle) => group.Pickle,
            nameof(Data.Entities.ProcessGroup.OuterPolish) => group.OuterPolish,
            nameof(Data.Entities.ProcessGroup.InnerGrinding) => group.InnerGrinding,
            nameof(Data.Entities.ProcessGroup.OuterSpotGrinding) => group.OuterSpotGrinding,
            nameof(Data.Entities.ProcessGroup.Inspection) => group.Inspection,
            nameof(Data.Entities.ProcessGroup.WeldingHead) => group.WeldingHead,
            nameof(Data.Entities.ProcessGroup.Lubrication) => group.Lubrication,
            nameof(Data.Entities.ProcessGroup.Warehouse) => group.Warehouse,
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
