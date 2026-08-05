using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MES.Core.Constants;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Enums;
using MES.Core.Interfaces.WorkOrder;
using MES.Data;
using MES.Data.Entities.Quality;
using MES.Data.Entities.WorkOrder;
using MES.Services.Printing;

namespace MES.Services.WorkOrder;

/// <summary>
/// 定尺工单服务（查询）
/// </summary>
public class FixedLengthWorkOrderService : IFixedLengthWorkOrderService
{
    /// <summary>产类=成品（ProductStatusHelper.Calculate 返回值）</summary>
    private const string FinishedProductStatus = "成品";

    /// <summary>IN 查询每批参数上限（SQL Server 默认 2100，留余量）</summary>
    private const int BatchInChunkSize = 1000;

    private readonly AppDbContext _context;
    private readonly ILogger<FixedLengthWorkOrderService> _logger;
    private readonly IMemoryCache _cache;

    public FixedLengthWorkOrderService(AppDbContext context, ILogger<FixedLengthWorkOrderService> logger, IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
    }

    public async Task<HashSet<decimal>> GetLengthsByMainNoAsync(string salesOrderNo, string productionMainNo)
    {
        if (string.IsNullOrWhiteSpace(salesOrderNo) || string.IsNullOrWhiteSpace(productionMainNo))
            return new HashSet<decimal>();
        return (await _context.FixedLengthWorkOrders
                .Where(f => f.SalesOrderNo == salesOrderNo && f.ProductionMainNo == productionMainNo)
                .Select(f => f.Length)
                .ToListAsync())
            .ToHashSet();
    }

    public async Task<List<FixedLengthWorkOrderListDto>> GetListAsync()
    {
        // 结果缓存：全量聚合（6 次查询 + 2 处全表拉内存）较慢，5 分钟绝对过期，
        // 与 WorkOrderExecutionService 缓存模式一致。数据源（工单/批次/记录/入库）CRUD 无统一失效入口，
        // 采用短 TTL 保证新鲜度，兼顾重复打开页面时的加载性能。
        return await _cache.GetOrCreateAsync("FixedLengthWorkOrderService:List", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await GetListCoreAsync();
        }) ?? new List<FixedLengthWorkOrderListDto>();
    }

    private async Task<List<FixedLengthWorkOrderListDto>> GetListCoreAsync()
    {
        // 1. 全部定尺工单行（工单号升序，长度降序）
        var fixedRows = await _context.FixedLengthWorkOrders
            .OrderBy(f => f.WorkOrderNo).ThenByDescending(f => f.Length)
            .ToListAsync();
        if (fixedRows.Count == 0)
            return new List<FixedLengthWorkOrderListDto>();

        var workOrderNos = fixedRows.Select(f => f.WorkOrderNo)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // 2. 读模型基础信息（每工单一行，主号下各工单基础信息一致）
        var summaries = await _context.Set<WorkOrderExecutionSummary>()
            .Where(s => workOrderNos.Contains(s.WorkOrderNo))
            .Select(s => new
            {
                s.WorkOrderNo,
                s.Salesman,
                s.CustomerName,
                s.SignDate,
                s.DeliveryDate,
                s.ProductionSubNo,
                s.DeliveryState,
                s.PlantGrade,
                s.Specification,
                s.ScheduleStage,
                s.UrgencyLevel
            })
            .ToListAsync();
        var summaryMap = summaries.ToDictionary(
            s => s.WorkOrderNo,
            StringComparer.OrdinalIgnoreCase);

        // 3. 主号级批次（全部批次最小投影，内存过滤出涉及主号）
        var involvedMainKeys = fixedRows
            .Select(f => NormalizeMainKey(f.SalesOrderNo, f.ProductionMainNo))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allBatches = await _context.ProductionBatches
            .Select(b => new { b.Id, b.SalesOrderNo, b.ProductionMainNo, b.TheoreticalOutputQty, b.CutRequirement })
            .ToListAsync();
        var batchMainKeyMap = new Dictionary<int, string>();
        var batchCutReqMap = new Dictionary<int, bool>();
        var batchesByMainKey = new Dictionary<string, List<BatchBrief>>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in allBatches)
        {
            var key = NormalizeMainKey(b.SalesOrderNo, b.ProductionMainNo);
            if (!involvedMainKeys.Contains(key)) continue;
            batchMainKeyMap[b.Id] = key;
            batchCutReqMap[b.Id] = b.CutRequirement;
            if (!batchesByMainKey.TryGetValue(key, out var list))
                batchesByMainKey[key] = list = new List<BatchBrief>();
            list.Add(new BatchBrief { Id = b.Id, TheoreticalOutputQty = b.TheoreticalOutputQty ?? 0 });
        }
        var batchIds = batchMainKeyMap.Keys.ToList();

        // 4. 主号级断切成品记录（分块查询避免 IN 参数超限）
        var cutRecords = new List<CutRecord>();
        foreach (var chunk in Chunk(batchIds, BatchInChunkSize))
        {
            cutRecords.AddRange(await _context.ProductionRecords
                .Where(r => chunk.Contains(r.ProductionBatchId)
                    && r.SectionName == SectionDefs.Cut
                    && r.ProductStatus == FinishedProductStatus
                    && r.FinishedCutLength.HasValue
                    && r.IsPreCut != true) // 预成切不计入成品切割支数
                .Select(r => new CutRecord
                {
                    ProductionBatchId = r.ProductionBatchId,
                    FinishedCutLength = r.FinishedCutLength,
                    PostCutQuantity = r.PostCutQuantity,
                    ExecDate = r.ExecDate
                })
                .ToListAsync());
        }

        // 5. 主号级成检记录（检验项目=尺寸 + 成检类型=正式成检 + 定尺长度有值）
        var inspections = new List<InspectionRecord>();
        foreach (var chunk in Chunk(batchIds, BatchInChunkSize))
        {
            inspections.AddRange(await _context.FinalInspections
                .Where(f => chunk.Contains(f.ProductionBatchId)
                    && f.InspectionItem == InspectionItem.Dimension
                    && f.InspectionType == nameof(InspectionType.FormalInspection)
                    && f.FixedLength != null)
                .Select(f => new InspectionRecord
                {
                    ProductionBatchId = f.ProductionBatchId,
                    FixedLength = f.FixedLength,
                    Quantity = f.Quantity,
                    DefectReworkQuantity = f.DefectReworkQuantity,
                    DefectWarehouseQuantity = f.DefectWarehouseQuantity,
                    DefectScrapQuantity = f.DefectScrapQuantity,
                    InspectionDate = f.InspectionDate
                })
                .ToListAsync());
        }

        // 6. 预计算主号级聚合（每主号：长度级切割/成检 + 总现况）
        var cutAggByMainKey = new Dictionary<string, Dictionary<decimal, LengthCutAgg>>(StringComparer.OrdinalIgnoreCase);
        var inspAggByMainKey = new Dictionary<string, Dictionary<decimal, LengthInspAgg>>(StringComparer.OrdinalIgnoreCase);
        var mainNoAggByKey = new Dictionary<string, MainNoAgg>(StringComparer.OrdinalIgnoreCase);

        foreach (var rec in cutRecords)
        {
            if (!batchMainKeyMap.TryGetValue(rec.ProductionBatchId, out var key)) continue;

            if (!mainNoAggByKey.TryGetValue(key, out var agg))
                mainNoAggByKey[key] = agg = new MainNoAgg();
            agg.CutBatchIds.Add(rec.ProductionBatchId);
            agg.CutActual += rec.PostCutQuantity ?? 0;

            var len = rec.FinishedCutLength ?? 0;
            if (!cutAggByMainKey.TryGetValue(key, out var dict))
                cutAggByMainKey[key] = dict = new Dictionary<decimal, LengthCutAgg>();
            if (!dict.TryGetValue(len, out var cutAgg))
                dict[len] = cutAgg = new LengthCutAgg();
            cutAgg.CutQuantity += rec.PostCutQuantity ?? 0;
            if (rec.ExecDate != default && (cutAgg.CutDeadline == null || rec.ExecDate > cutAgg.CutDeadline))
                cutAgg.CutDeadline = rec.ExecDate;
        }

        foreach (var rec in inspections)
        {
            if (!batchMainKeyMap.TryGetValue(rec.ProductionBatchId, out var key)) continue;

            if (!mainNoAggByKey.TryGetValue(key, out var agg))
                mainNoAggByKey[key] = agg = new MainNoAgg();
            agg.Defect += (rec.DefectReworkQuantity ?? 0) + (rec.DefectWarehouseQuantity ?? 0) + (rec.DefectScrapQuantity ?? 0);

            var len = ParseLength(rec.FixedLength) ?? 0;
            if (!inspAggByMainKey.TryGetValue(key, out var dict))
                inspAggByMainKey[key] = dict = new Dictionary<decimal, LengthInspAgg>();
            if (!dict.TryGetValue(len, out var inspAgg))
                dict[len] = inspAgg = new LengthInspAgg();
            inspAgg.ArrivedQuantity += rec.Quantity ?? 0;
            if (batchCutReqMap.TryGetValue(rec.ProductionBatchId, out var isCut) && isCut)
                inspAgg.CutArrivedQuantity += rec.Quantity ?? 0;
            else
                inspAgg.NonCutArrivedQuantity += rec.Quantity ?? 0;
            inspAgg.DefectQuantity += (rec.DefectReworkQuantity ?? 0) + (rec.DefectWarehouseQuantity ?? 0) + (rec.DefectScrapQuantity ?? 0);
            if (rec.InspectionDate != default && (inspAgg.InspectionDeadline == null || rec.InspectionDate > inspAgg.InspectionDeadline))
                inspAgg.InspectionDeadline = rec.InspectionDate;
        }

        // 主号总需求支（该主号下所有定尺行计划支数之和）
        var totalRequirementByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in fixedRows)
        {
            var key = NormalizeMainKey(f.SalesOrderNo, f.ProductionMainNo);
            totalRequirementByKey[key] = totalRequirementByKey.GetValueOrDefault(key) + f.PlannedQuantity;
        }

        // 6.5 仓库成品入库（主号级聚合：经 WorkOrder 推断 ProductionMainNo，与成检口径一致，不依赖工单号精确匹配）
        var inboundAggByMainKey = new Dictionary<string, Dictionary<decimal, InboundAgg>>(StringComparer.OrdinalIgnoreCase);
        var inboundBatches = await (from ib in _context.InventoryBatches
                                    join w in _context.WorkOrders on ib.WorkOrderNo equals w.WorkOrderNo
                                    where ib.WorkOrderNo != null
                                        && (ib.MaterialType == InventoryMaterialTypes.OrderFinished
                                            || ib.MaterialType == InventoryMaterialTypes.SpecialDeliveryStatus)
                                        && ib.MaxLength.HasValue
                                    select new
                                    {
                                        w.SalesOrderNo,
                                        w.ProductionMainNo,
                                        ib.MaterialType,
                                        ib.MaxLength,
                                        ib.InitialQuantity,
                                        ib.InboundDate
                                    })
                                    .ToListAsync();
        foreach (var ib in inboundBatches)
        {
            var mainKey = NormalizeMainKey(ib.SalesOrderNo, ib.ProductionMainNo);
            if (!involvedMainKeys.Contains(mainKey)) continue;
            var len = ib.MaxLength!.Value;
            if (!inboundAggByMainKey.TryGetValue(mainKey, out var inDict))
                inboundAggByMainKey[mainKey] = inDict = new Dictionary<decimal, InboundAgg>();
            if (!inDict.TryGetValue(len, out var inAgg))
                inDict[len] = inAgg = new InboundAgg();
            if (ib.MaterialType == InventoryMaterialTypes.SpecialDeliveryStatus)
            {
                inAgg.SpecialQty += ib.InitialQuantity;
                if (ib.InboundDate != default && (inAgg.SpecialDate == null || ib.InboundDate > inAgg.SpecialDate))
                    inAgg.SpecialDate = ib.InboundDate;
            }
            else
            {
                inAgg.OrderFinishedQty += ib.InitialQuantity;
                if (ib.InboundDate != default && (inAgg.OrderFinishedDate == null || ib.InboundDate > inAgg.OrderFinishedDate))
                    inAgg.OrderFinishedDate = ib.InboundDate;
            }
        }

        // 7. 组装明细行（每行 = 工单号 + 长度，切割/成检为主号级该长度聚合）
        var result = new List<FixedLengthWorkOrderListDto>(fixedRows.Count);
        foreach (var f in fixedRows)
        {
            var mainKey = NormalizeMainKey(f.SalesOrderNo, f.ProductionMainNo);

            var summary = summaryMap.GetValueOrDefault(f.WorkOrderNo);
            var mainAgg = mainNoAggByKey.GetValueOrDefault(mainKey);
            var batches = batchesByMainKey.GetValueOrDefault(mainKey);
            var cutBatchIds = mainAgg?.CutBatchIds ?? new HashSet<int>();

            var cutQty = 0;
            DateTime? cutDeadline = null;
            if (cutAggByMainKey.TryGetValue(mainKey, out var cutDict)
                && cutDict.TryGetValue(f.Length, out var cutAgg))
            {
                cutQty = cutAgg.CutQuantity;
                cutDeadline = cutAgg.CutDeadline;
            }

            var arrQty = 0;
            var cutArrQty = 0;
            var nonCutArrQty = 0;
            var defectQty = 0;
            DateTime? inspDeadline = null;
            if (inspAggByMainKey.TryGetValue(mainKey, out var inspDict)
                && inspDict.TryGetValue(f.Length, out var inspAgg))
            {
                arrQty = inspAgg.ArrivedQuantity;
                cutArrQty = inspAgg.CutArrivedQuantity;
                nonCutArrQty = inspAgg.NonCutArrivedQuantity;
                defectQty = inspAgg.DefectQuantity;
                inspDeadline = inspAgg.InspectionDeadline;
            }

            // 成品入库（主号+长度 级，与成检口径一致；物料类型按交货状态区分：U型管=SpecialDeliveryStatus，非U型管=OrderFinished）
            var isUTube = summary?.DeliveryState is "SolutionAnnealedAndPickledUTube" or "BrightUTube";
            var inboundQty = 0;
            DateTime? inboundDeadline = null;
            if (inboundAggByMainKey.TryGetValue(mainKey, out var inDict)
                && inDict.TryGetValue(f.Length, out var inAgg))
            {
                if (isUTube)
                {
                    inboundQty = inAgg.SpecialQty;
                    inboundDeadline = inAgg.SpecialDate;
                }
                else
                {
                    inboundQty = inAgg.OrderFinishedQty;
                    inboundDeadline = inAgg.OrderFinishedDate;
                }
            }

            // 总现况分析（主号级；三档划分：无需切割 + 需切未切 + 切割理论 = 总投料）
            var mainNoTotalInput = batches?.Sum(b => b.TheoreticalOutputQty) ?? 0;
            var mainNoNoCutQty = batches?
                .Where(b => batchCutReqMap.TryGetValue(b.Id, out var noCutReq) && !noCutReq)
                .Sum(b => b.TheoreticalOutputQty) ?? 0;
            var mainNoNeedCutUncutQty = batches?
                .Where(b => !cutBatchIds.Contains(b.Id)
                    && batchCutReqMap.TryGetValue(b.Id, out var needCutReq) && needCutReq)
                .Sum(b => b.TheoreticalOutputQty) ?? 0;
            var mainNoCutTheoretical = batches?
                .Where(b => cutBatchIds.Contains(b.Id)
                    && batchCutReqMap.TryGetValue(b.Id, out var cutReq) && cutReq)
                .Sum(b => b.TheoreticalOutputQty) ?? 0;

            result.Add(new FixedLengthWorkOrderListDto
            {
                WorkOrderNo = f.WorkOrderNo,
                Length = f.Length,
                PlannedQuantity = f.PlannedQuantity,
                Salesman = summary?.Salesman ?? string.Empty,
                CustomerName = summary?.CustomerName ?? string.Empty,
                SignDate = summary?.SignDate ?? default,
                DeliveryDate = summary?.DeliveryDate ?? default,
                SalesOrderNo = f.SalesOrderNo,
                ProductionMainNo = f.ProductionMainNo,
                ProductionSubNo = summary?.ProductionSubNo,
                DeliveryState = string.IsNullOrEmpty(summary?.DeliveryState)
                    ? default
                    : Enum.Parse<DeliveryState>(summary.DeliveryState),
                PlantGrade = summary?.PlantGrade ?? string.Empty,
                Specification = summary?.Specification ?? string.Empty,
                ScheduleStage = summary?.ScheduleStage ?? 0,
                UrgencyLevel = summary?.UrgencyLevel,
                CutDeadline = cutDeadline,
                CutQuantity = cutQty,
                InspectionDeadline = inspDeadline,
                ArrivedQuantity = arrQty,
                CutArrivedQuantity = cutArrQty,
                NonCutArrivedQuantity = nonCutArrQty,
                DefectQuantity = defectQty,
                InboundDeadline = inboundDeadline,
                InboundQuantity = inboundQty,
                MainNoTotalRequirement = totalRequirementByKey.GetValueOrDefault(mainKey),
                MainNoTotalInput = mainNoTotalInput,
                MainNoNoCutQty = mainNoNoCutQty,
                MainNoNeedCutUncutQty = mainNoNeedCutUncutQty,
                MainNoCutTheoretical = mainNoCutTheoretical,
                MainNoCutActual = mainAgg?.CutActual ?? 0,
                MainNoDefect = mainAgg?.Defect ?? 0
            });
        }

        return result;
    }

    /// <summary>生成打印 PDF（Mode B ⓪：前端已准备数据，枚举字段已转中文）</summary>
    public Task<byte[]> PrintFileAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        var pdfBytes = TablePrintHelper.GeneratePdf(title, items, columns);
        return Task.FromResult(pdfBytes);
    }

    /// <summary>主号聚合键（订单号+主号 大小写归一化）</summary>
    private static string NormalizeMainKey(string? salesOrderNo, string? productionMainNo) =>
        $"{salesOrderNo?.Trim().ToUpperInvariant()}|{productionMainNo?.Trim().ToUpperInvariant()}";

    private static IEnumerable<List<int>> Chunk(IReadOnlyList<int> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
        {
            var count = Math.Min(size, source.Count - i);
            var chunk = new List<int>(count);
            for (var j = 0; j < count; j++)
                chunk.Add(source[i + j]);
            yield return chunk;
        }
    }

    private static decimal? ParseLength(string? fixedLength)
    {
        if (string.IsNullOrWhiteSpace(fixedLength)) return null;
        var s = fixedLength.Trim();
        if (s.EndsWith("mm", StringComparison.OrdinalIgnoreCase))
            s = s[..^2].Trim();
        return decimal.TryParse(s, out var value) ? value : null;
    }

    private sealed class BatchBrief
    {
        public int Id { get; set; }
        public int TheoreticalOutputQty { get; set; }
    }

    private sealed class LengthCutAgg
    {
        public int CutQuantity { get; set; }
        public DateTime? CutDeadline { get; set; }
    }

    private sealed class LengthInspAgg
    {
        public int ArrivedQuantity { get; set; }
        public int CutArrivedQuantity { get; set; }
        public int NonCutArrivedQuantity { get; set; }
        public int DefectQuantity { get; set; }
        public DateTime? InspectionDeadline { get; set; }
    }

    private sealed class MainNoAgg
    {
        public HashSet<int> CutBatchIds { get; } = new();
        public int CutActual { get; set; }
        public int Defect { get; set; }
    }

    /// <summary>成品入库聚合（按物料类型区分 U型管/非U型管）</summary>
    private sealed class InboundAgg
    {
        public int OrderFinishedQty { get; set; }
        public DateTime? OrderFinishedDate { get; set; }
        public int SpecialQty { get; set; }
        public DateTime? SpecialDate { get; set; }
    }

    private class CutRecord
    {
        public int ProductionBatchId { get; set; }
        public decimal? FinishedCutLength { get; set; }
        public int? PostCutQuantity { get; set; }
        public DateTime ExecDate { get; set; }
    }

    private class InspectionRecord
    {
        public int ProductionBatchId { get; set; }
        public string? FixedLength { get; set; }
        public int? Quantity { get; set; }
        public int? DefectReworkQuantity { get; set; }
        public int? DefectWarehouseQuantity { get; set; }
        public int? DefectScrapQuantity { get; set; }
        public DateTime InspectionDate { get; set; }
    }
}
