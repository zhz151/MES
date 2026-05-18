using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Data;

namespace MES.Services;

/// <summary>
/// 扫码执行服务实现
/// </summary>
public class ScanService : IScanService
{
    private readonly AppDbContext _context;

    /// <summary>
    /// ProcessGroup 属性名 → 工段中文名映射
    /// </summary>
    private static readonly Dictionary<string, string> SectionNameMap = new()
    {
        ["ColdRollDraw"] = "冷轧拔",
        ["OilPipeCut"] = "油管断",
        ["Degrease"] = "去油",
        ["Solution"] = "固溶",
        ["Straighten"] = "矫直",
        ["Cut"] = "断切",
        ["ThicknessMeasure"] = "侧壁",
        ["Pickle"] = "酸洗",
        ["OuterPolish"] = "外抛光",
        ["InnerGrinding"] = "内修磨",
        ["OuterSpotGrinding"] = "外点磨",
        ["Inspection"] = "检验",
        ["WeldingHead"] = "打焊头",
        ["Lubrication"] = "润滑",
        ["Warehouse"] = "入库",
    };

    /// <summary>
    /// ProcessGroup 中涉及的字段列表（对应15个工段）
    /// </summary>
    private static readonly string[] SectionFields =
    [
        "ColdRollDraw", "OilPipeCut", "Degrease", "Solution", "Straighten",
        "Cut", "ThicknessMeasure", "Pickle", "OuterPolish", "InnerGrinding",
        "OuterSpotGrinding", "Inspection", "WeldingHead", "Lubrication", "Warehouse"
    ];

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
            "ColdRollDraw" => group.ColdRollDraw,
            "OilPipeCut" => group.OilPipeCut,
            "Degrease" => group.Degrease,
            "Solution" => group.Solution,
            "Straighten" => group.Straighten,
            "Cut" => group.Cut,
            "ThicknessMeasure" => group.ThicknessMeasure,
            "Pickle" => group.Pickle,
            "OuterPolish" => group.OuterPolish,
            "InnerGrinding" => group.InnerGrinding,
            "OuterSpotGrinding" => group.OuterSpotGrinding,
            "Inspection" => group.Inspection,
            "WeldingHead" => group.WeldingHead,
            "Lubrication" => group.Lubrication,
            "Warehouse" => group.Warehouse,
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
