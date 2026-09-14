using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Order;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.Scheduling;
using MES.Data;
using MES.Data.Entities.WorkOrder;

namespace MES.Services.Order;

/// <summary>
/// 订单进度树查询服务（只读）。聚合口径全部复用既有计算：
/// 1) 主号枚举 / 完结判定(ScheduleStage==1) / 原料锁定备注 / 生产8节点待量 / 头部签订交货延期等字段：取 WorkOrderExecutionSummary 快照
///    （该表需经工单执行页「即时更新」刷新，故属快照口径）；
/// 2) 主号头的标准牌号/产品标准与整单含项次数：实时 join OrderItem（按 OrderItemIds=Sequence 逗号分隔），规格/长度/交货/支数/重量仍取快照；
/// 3) 成品检验 3 档：复用 IFinalInspectionPlanService.GetKanbanAsync（看板前 3 档，按 ProductionBatchId 去重
///    取首行，口径同工单执行看板 Stage3；第 4 档「完成检验待入库」不放入树）；
/// 4) 订单成品入库 3 档：实时读 InventoryBatch(MaterialType=OrderFinished 可交付成品，排除 SpecialDeliveryStatus
///    非交付态) + OutboundRecords(OutboundType=SalesOut)，口径同 OrderService 成品数据聚合；
/// 5) 完结主号补充分支（用户拍板「投料+产出」两维度）：「投料」= Σ 该主号生产批次的工艺卡领料重
///    InputWeight，排除「返整/委外生产/对外加工」三种生产类型（不限制造物品），批次自身带订单号+主号（零 join）；
///    「产出」全部按仓库实收 —— 在制品入库(在制品库 WIP 余库料 Surplus)/次品入库(次品库 DEFECT 6 类物料)/备料成品
///    (成品库 FG 备料成品 Finished)，三者经 InventoryBatch.ProductionBatchNo 反查 ProductionBatch 取订单号+主号后
///    聚合 InitialWeight（这些入库行自身不带订单号）；次品叶另附同叶退货出库量（次品库 OutboundType=ReturnOut）。
/// </summary>
public class OrderProgressQueryService : IOrderProgressQueryService
{
    private readonly AppDbContext _context;
    private readonly IFinalInspectionPlanService _finalInspectionPlan;

    public OrderProgressQueryService(AppDbContext context, IFinalInspectionPlanService finalInspectionPlan)
    {
        _context = context;
        _finalInspectionPlan = finalInspectionPlan;
    }

    /// <summary>生产执行分支 8 在产节点：叶子 Key/中文标签/实体待量字段取值</summary>
    private static readonly (string Key, string Label, Func<WorkOrderExecutionSummary, decimal?> Value)[] ProductionNodeDefs =
    [
        ("RoughTubeProcessing", "荒管处理", r => r.PendingSectionRoughTube),
        ("InProcessRepair", "在制修检", r => r.PendingSectionWarehouseFix),
        ("ColdRoll60", "60冷轧", r => r.PendingSection60Roll),
        ("ColdRoll50", "50冷轧", r => r.PendingSection50Roll),
        ("ColdRoll30", "30冷轧", r => r.PendingSection30Roll),
        ("ColdRoll20", "20冷轧", r => r.PendingSection20Roll),
        ("ThreeRollColdRoll", "三辊冷轧", r => r.PendingSectionThreeRoll),
        ("ColdDraw", "冷拔", r => r.PendingSectionDrawBench),
    ];

    public async Task<OrderProgressTreeDto?> GetTreeAsync(string salesOrderNo)
    {
        if (string.IsNullOrWhiteSpace(salesOrderNo))
            return null;

        // 1. 主号枚举源：该订单下的全部工单执行快照行（每工单一行，含完结主号）
        var rows = await _context.Set<WorkOrderExecutionSummary>().AsNoTracking()
            .Where(e => e.SalesOrderNo == salesOrderNo)
            .ToListAsync();

        if (rows.Count == 0)
            return null;

        // 2. 成品检验实时源（看板前 3 档），按主号预分桶
        var inspectionByMain = await BuildInspectionByMainAsync(salesOrderNo);

        // 3. 成品入库实时源（OrderFinished 可交付成品 + SalesOut 出库），按主号预分桶
        var warehousing = await BuildWarehousingByMainAsync(salesOrderNo);

        // 3b. 完结主号「投料」：该主号生产批次工艺卡的领料重 Σ InputWeight（排除返整/委外生产/对外加工）
        var inputByMain = await BuildInputByMainAsync(salesOrderNo);

        // 3c. 完结主号「产出」实时源：在制品库(余料) / 次品库(6 类次品+退货) / 成品库(备料成品)
        //     均按生产批号反查订单归属后按主号聚合
        var surplusInbound = await BuildWarehouseInboundByMainAsync(
            salesOrderNo, WarehouseCodes.WorkInProgress, InventoryMaterialTypes.Surplus);
        var defectInbound = await BuildDefectInboundByMainAsync(salesOrderNo);
        var finishedStockInbound = await BuildWarehouseInboundByMainAsync(
            salesOrderNo, WarehouseCodes.FinishedGoods, InventoryMaterialTypes.Finished);

        // 4. 项次实时源：主号→(标准牌号,产品标准) + 整单去重项次数（WES 快照不含牌号/标准，须 join OrderItem）
        var itemInfo = await BuildOrderItemInfoAsync(salesOrderNo);

        var header = rows[0];
        var tree = new OrderProgressTreeDto
        {
            SalesOrderNo = salesOrderNo,
            CustomerName = header.CustomerName,
            Salesman = header.Salesman,
            SignDate = header.SignDate,
            DeliveryDate = rows.Max(r => r.DeliveryDate),
            DelayPenalty = rows.Any(r => r.DelayPenalty),
            OrderTotalWeightKg = rows.Sum(r => r.TotalWeight),
            ItemCount = itemInfo.ItemCount,
        };

        foreach (var group in rows.GroupBy(r => r.ProductionMainNo))
        {
            var items = group.ToList();
            var rep = items[0]; // 主号级字段（ScheduleStage/RawMaterialLockRemark/UrgencyLevel/主号关注工序）组内一致
            var completed = rep.ScheduleStage == 1;
            var specInfo = itemInfo.ByMain.GetValueOrDefault(group.Key);

            var node = new OrderMainProgressDto
            {
                ProductionMainNo = group.Key,
                ScheduleStage = rep.ScheduleStage,
                UrgencyLevel = MostUrgent(items),
                StandardGrade = specInfo.StandardGrade,
                ProductStandard = specInfo.ProductStandard,
                Specification = rep.Specification,
                LengthStatus = rep.LengthStatus,
                DeliveryState = rep.DeliveryState,
                QuantitySum = items.Sum(r => r.TotalQuantity),
                EstimatedCompletionDate = items.Max(r => r.EstimatedProcessCompletionDate),
                TotalWeightKg = items.Sum(r => r.TotalWeight),
            };

            if (!completed)
            {
                // 原料锁定：仅档2且备注为四类之一时携带单叶（叶即 A/B/C/D 状态，恒显示）
                if (rep.ScheduleStage == 2 && RawMaterialLockRemarkKeys.IsKey(rep.RawMaterialLockRemark))
                    node.RawMaterialLock = BuildLockBranch(items, rep.RawMaterialLockRemark!);

                // 生产执行：8 节点待量 SUM，>0 才建叶
                node.Production = BuildProductionBranch(items);

                // 成品检验：实时 3 档
                node.FinalInspection = BuildInspectionBranch(inspectionByMain, group.Key);
            }
            else
            {
                // 完结主号（ScheduleStage==1）：原料/生产/成检看板全空，改出 4 个完结专属分支（用户拍板，固定序）
                // 序1「投料」= 生产批次工艺卡 InputWeight；序2~4「产出」= 各仓库实收，经生产批号反查订单归属
                node.ProductionInput = BuildInputBranch(inputByMain.GetValueOrDefault(group.Key));
                node.SurplusInbound = BuildSingleInboundBranch(surplusInbound, group.Key, "SurplusInbound", "在制品入库");
                node.DefectInbound = BuildDefectInboundBranch(defectInbound, group.Key);
                node.FinishedStockInbound = BuildSingleInboundBranch(finishedStockInbound, group.Key, "FinishedStockInbound", "备料成品");
            }

            node.Warehousing = BuildWarehousingBranch(warehousing, group.Key);

            tree.MainNos.Add(node);
        }

        // 排序：一律按主号从小到大（真库主号 = X + 两位数字定宽，字符串升序即数值序）
        tree.MainNos = tree.MainNos
            .OrderBy(n => n.ProductionMainNo, StringComparer.Ordinal)
            .ToList();

        return tree;
    }

    // ===================== 成品检验（实时） =====================
    private async Task<Dictionary<string, Dictionary<string, decimal>>> BuildInspectionByMainAsync(string salesOrderNo)
    {
        var result = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.Ordinal);
        var activeStages = new HashSet<string>(new[]
        {
            KanbanStageKeys.WaitingMaterial, KanbanStageKeys.WaitingInspection, KanbanStageKeys.Inspecting
        }, StringComparer.Ordinal);

        var kanban = (await _finalInspectionPlan.GetKanbanAsync())
            .Where(k => k.SalesOrderNo == salesOrderNo && activeStages.Contains(k.KanbanStage))
            .ToList();

        // 同批预/正式成检两行去重（GroupBy ProductionBatchId 取首行，口径同工单执行看板 Stage3）
        foreach (var group in kanban.GroupBy(k => k.ProductionBatchId))
        {
            var first = group.First();
            var main = first.ProductionMainNo ?? "";
            if (!result.TryGetValue(main, out var bucket))
            {
                bucket = new Dictionary<string, decimal>(StringComparer.Ordinal);
                result[main] = bucket;
            }
            bucket[first.KanbanStage] = bucket.GetValueOrDefault(first.KanbanStage) + (first.ProductionWeight ?? 0m);
        }
        return result;
    }

    private static MainProgressBranchDto? BuildInspectionBranch(
        Dictionary<string, Dictionary<string, decimal>> byMain, string productionMainNo)
    {
        if (!byMain.TryGetValue(productionMainNo, out var bucket))
            return null;

        var leaves = new List<MainProgressLeafDto>();
        foreach (var stage in KanbanStageKeys.All) // 前3档按看板序，第4档已过滤不入
        {
            if (!bucket.TryGetValue(stage, out var weight) || weight <= 0m)
                continue;
            leaves.Add(new MainProgressLeafDto { Key = stage, Text = stage, WeightKg = weight });
        }
        return leaves.Count > 0
            ? new MainProgressBranchDto { Key = "FinalInspection", Title = "成品检验", Leaves = leaves }
            : null;
    }

    // ===================== 订单成品入库（实时） =====================
    private async Task<Dictionary<string, (decimal Inbound, decimal Stock, decimal Outbound)>> BuildWarehousingByMainAsync(string salesOrderNo)
    {
        var result = new Dictionary<string, (decimal Inbound, decimal Stock, decimal Outbound)>(StringComparer.Ordinal);

        var finished = await (from ib in _context.InventoryBatches.AsNoTracking()
                              where ib.SalesOrderNo == salesOrderNo
                                  && ib.MaterialType == InventoryMaterialTypes.OrderFinished
                              select new { ib.Id, ib.ProductionMainNo, ib.InitialWeight, ib.RemainingWeight })
                              .ToListAsync();

        // 入库/库存按主号聚合（InventoryBatch 自带订单号+主号查询键）
        var mainByBatchId = new Dictionary<int, string>(finished.Count);
        foreach (var f in finished)
        {
            mainByBatchId[f.Id] = f.ProductionMainNo ?? "";
            var key = f.ProductionMainNo ?? "";
            var cur = result.GetValueOrDefault(key);
            result[key] = (cur.Inbound + f.InitialWeight, cur.Stock + f.RemainingWeight, cur.Outbound);
        }

        // 成品出库：仅销售出库（SalesOut），按主号聚合
        if (finished.Count > 0)
        {
            var ids = finished.Select(f => f.Id).ToList();
            var outRows = await _context.OutboundRecords.AsNoTracking()
                .Where(o => o.OutboundType == OutboundType.SalesOut && ids.Contains(o.InventoryBatchId))
                .Select(o => new { o.InventoryBatchId, o.OutboundWeight })
                .ToListAsync();

            foreach (var o in outRows)
            {
                if (!mainByBatchId.TryGetValue(o.InventoryBatchId, out var main))
                    continue;
                var cur = result.GetValueOrDefault(main);
                result[main] = (cur.Inbound, cur.Stock, cur.Outbound + o.OutboundWeight);
            }
        }

        return result;
    }

    private static MainProgressBranchDto? BuildWarehousingBranch(
        Dictionary<string, (decimal Inbound, decimal Stock, decimal Outbound)> byMain, string productionMainNo)
    {
        if (!byMain.TryGetValue(productionMainNo, out var w) || (w.Inbound <= 0m && w.Outbound <= 0m && w.Stock <= 0m))
            return null;

        var leaves = new List<MainProgressLeafDto>();
        if (w.Inbound > 0m)
            leaves.Add(new MainProgressLeafDto { Key = "Inbound", Text = "入库", WeightKg = w.Inbound });
        if (w.Outbound > 0m)
            leaves.Add(new MainProgressLeafDto { Key = "Outbound", Text = "出库", WeightKg = w.Outbound });
        if (w.Stock > 0m)
            leaves.Add(new MainProgressLeafDto { Key = "Stock", Text = "库存", WeightKg = w.Stock });
        return leaves.Count > 0
            ? new MainProgressBranchDto { Key = "Warehousing", Title = "订单成品入库", Leaves = leaves }
            : null;
    }

    // ===================== 完结主号补充（投料：生产批次工艺卡 InputWeight） =====================

    /// <summary>
    /// 完结主号「生产投料」取数：Σ 该主号生产批次的工艺卡领料重 InputWeight。
    /// 用户拍板口径——排除「返整/委外生产/对外加工」三种生产类型，其余类型全收（不限制造物品）；
    /// 同时返回同批次的 ProductionType 去重串，供分支标题后缀（如「生产投料[荒管生产+在制生产+外购]」）。
    /// 批次自身带订单号+主号，零 join。
    /// </summary>
    private async Task<Dictionary<string, (decimal Weight, string? TypeText)>> BuildInputByMainAsync(string salesOrderNo)
    {
        var rows = await _context.ProductionBatches.AsNoTracking()
            .Where(b => b.SalesOrderNo == salesOrderNo
                && (b.ProductionType == null
                    || (b.ProductionType != ProductionTypeKeys.Rework
                        && b.ProductionType != ProductionTypeKeys.Subcontract
                        && b.ProductionType != ProductionTypeKeys.ExternalProcessing)))
            .Select(b => new { b.ProductionMainNo, b.ProductionType, b.InputWeight })
            .ToListAsync();

        var result = new Dictionary<string, (decimal Weight, string? TypeText)>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in rows.GroupBy(r => r.ProductionMainNo ?? ""))
        {
            var weight = group.Sum(r => r.InputWeight ?? 0m);
            var typeText = string.Join("+", group
                .Select(r => r.ProductionType)
                .Where(t => t != null)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(k =>
                {
                    var idx = Array.IndexOf(ProductionTypeKeys.All, k!);
                    return idx < 0 ? int.MaxValue : idx;
                })
                .Select(k => ProductionTypeText(k!)));

            result[group.Key] = (weight, typeText.Length > 0 ? typeText : null);
        }
        return result;
    }

    /// <summary>生产投料单叶：投料重来自生产批次工艺卡；标题后缀该主号涉及的生产类型</summary>
    private static MainProgressBranchDto? BuildInputBranch((decimal Weight, string? TypeText) info)
    {
        return info.Weight > 0m
            ? new MainProgressBranchDto
            {
                Key = "ProductionInput",
                Title = string.IsNullOrEmpty(info.TypeText) ? "生产投料" : $"生产投料[{info.TypeText}]",
                Leaves = [new MainProgressLeafDto { Key = "Input", Text = "投料", WeightKg = info.Weight }],
            }
            : null;
    }

    /// <summary>生产类型 Key → 中文（未知 Key 原样返回）</summary>
    private static string ProductionTypeText(string key)
        => Enum.TryParse<ProductionType>(key, out var parsed) ? EnumHelper.GetDisplayName(parsed) : key;

    // ===================== 完结主号补充（次品入库：次品库实收 + 退货出库） =====================

    /// <summary>次品库叶子物料类型顺序（与 InventoryMaterialTypes.WarehouseAllowedTypes["DEFECT"] 一致）</summary>
    private static readonly string[] DefectMaterialTypeOrder =
    [
        InventoryMaterialTypes.DefectRoundBar,
        InventoryMaterialTypes.DefectRoughTube,
        InventoryMaterialTypes.DefectSemi,
        InventoryMaterialTypes.DefectFinished,
        InventoryMaterialTypes.DefectWIP,
        InventoryMaterialTypes.Scrap,
    ];

    /// <summary>
    /// 完结主号「次品入库」取数：次品库(DEFECT) 6 类物料类型的入库重，以及同批次的退货出库重
    /// （OutboundType=ReturnOut，即「先入次品库、再退货出库」的下半程）。
    /// 次品库入库行自身不带订单号+主号，只能经 InventoryBatch.ProductionBatchNo 反查 ProductionBatch 取归属；
    /// 生产批号缺失或对不上生产批次的行不计入。返回 主号→物料类型→(入库重, 退货重)。
    /// </summary>
    private async Task<Dictionary<string, Dictionary<string, (decimal Inbound, decimal Return)>>> BuildDefectInboundByMainAsync(string salesOrderNo)
    {
        var defectTypes = DefectMaterialTypeOrder; // 局部集合供 EF 参数化翻译
        var batches = await (from ib in _context.InventoryBatches.AsNoTracking()
                             join w in _context.Warehouses.AsNoTracking() on ib.WarehouseId equals w.Id
                             join pb in _context.ProductionBatches.AsNoTracking() on ib.ProductionBatchNo equals pb.BatchNo
                             where w.Code == WarehouseCodes.Defect
                                 && defectTypes.Contains(ib.MaterialType)
                                 && ib.ProductionBatchNo != null
                                 && pb.SalesOrderNo == salesOrderNo
                             select new { ib.Id, pb.ProductionMainNo, ib.MaterialType, ib.InitialWeight })
                             .ToListAsync();

        var result = new Dictionary<string, Dictionary<string, (decimal Inbound, decimal Return)>>(StringComparer.OrdinalIgnoreCase);
        var mainByBatchId = new Dictionary<int, (string Main, string MaterialType)>(batches.Count);
        foreach (var b in batches)
        {
            var main = b.ProductionMainNo ?? "";
            mainByBatchId[b.Id] = (main, b.MaterialType);
            var bucket = Bucket(result, main);
            var cur = bucket.GetValueOrDefault(b.MaterialType);
            bucket[b.MaterialType] = (cur.Inbound + b.InitialWeight, cur.Return);
        }

        if (batches.Count > 0)
        {
            var ids = batches.Select(b => b.Id).ToList();
            var outRows = await _context.OutboundRecords.AsNoTracking()
                .Where(o => o.OutboundType == OutboundType.ReturnOut && ids.Contains(o.InventoryBatchId))
                .Select(o => new { o.InventoryBatchId, o.OutboundWeight })
                .ToListAsync();

            foreach (var o in outRows)
            {
                if (!mainByBatchId.TryGetValue(o.InventoryBatchId, out var owner))
                    continue;
                var bucket = Bucket(result, owner.Main);
                var cur = bucket.GetValueOrDefault(owner.MaterialType);
                bucket[owner.MaterialType] = (cur.Inbound, cur.Return + o.OutboundWeight);
            }
        }

        return result;
    }

    /// <summary>按主号取（无则建）次品物料分桶</summary>
    private static Dictionary<string, (decimal Inbound, decimal Return)> Bucket(
        Dictionary<string, Dictionary<string, (decimal Inbound, decimal Return)>> byMain, string main)
    {
        if (!byMain.TryGetValue(main, out var bucket))
        {
            bucket = new Dictionary<string, (decimal Inbound, decimal Return)>(StringComparer.Ordinal);
            byMain[main] = bucket;
        }
        return bucket;
    }

    /// <summary>次品入库分支：6 类次品叶（入库重 + 同叶退货出库重），入库与退货均为 0 的叶省略；全空不建分支</summary>
    private static MainProgressBranchDto? BuildDefectInboundBranch(
        Dictionary<string, Dictionary<string, (decimal Inbound, decimal Return)>> byMain, string productionMainNo)
    {
        if (!byMain.TryGetValue(productionMainNo, out var bucket))
            return null;

        var leaves = new List<MainProgressLeafDto>();
        foreach (var materialType in DefectMaterialTypeOrder)
        {
            if (!bucket.TryGetValue(materialType, out var v) || (v.Inbound <= 0m && v.Return <= 0m))
                continue;
            leaves.Add(new MainProgressLeafDto
            {
                Key = materialType,
                Text = MaterialTypeText(materialType),
                WeightKg = v.Inbound,
                ReturnWeightKg = v.Return,
            });
        }
        return leaves.Count > 0
            ? new MainProgressBranchDto { Key = "DefectInbound", Title = "次品入库", Leaves = leaves }
            : null;
    }

    /// <summary>物料类型 Key → 中文（未知 Key 原样返回）</summary>
    private static string MaterialTypeText(string key)
        => Enum.TryParse<MaterialType>(key, out var parsed) ? EnumHelper.GetDisplayName(parsed) : key;

    // ===================== 完结主号补充（在制品入库/备料成品：按生产批号反查订单归属） =====================

    /// <summary>
    /// 指定仓库+物料类型的入库重量，经 InventoryBatch.ProductionBatchNo 反查 ProductionBatch 归属，
    /// 按主号聚合。在制品库(余料)/成品库(备料成品)的入库行自身不带订单号，只能按生产批号反查；
    /// 生产批号缺失或对不上生产批次的行不计入（因此当前真库该两类多为空）。
    /// </summary>
    private async Task<Dictionary<string, decimal>> BuildWarehouseInboundByMainAsync(
        string salesOrderNo, string warehouseCode, string materialType)
    {
        var rows = await (from ib in _context.InventoryBatches.AsNoTracking()
                          join w in _context.Warehouses.AsNoTracking() on ib.WarehouseId equals w.Id
                          join pb in _context.ProductionBatches.AsNoTracking() on ib.ProductionBatchNo equals pb.BatchNo
                          where w.Code == warehouseCode
                              && ib.MaterialType == materialType
                              && ib.ProductionBatchNo != null
                              && pb.SalesOrderNo == salesOrderNo
                          select new { pb.ProductionMainNo, ib.InitialWeight })
                          .ToListAsync();

        // 生产批号跨表比较后按主号分桶，内存比较用 OrdinalIgnoreCase（SQL Server 大小写不敏感）
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            var key = r.ProductionMainNo ?? "";
            result[key] = result.GetValueOrDefault(key) + r.InitialWeight;
        }
        return result;
    }

    /// <summary>单叶入库分支（余料入库/备料入库）：无值或 0 时不建分支</summary>
    private static MainProgressBranchDto? BuildSingleInboundBranch(
        Dictionary<string, decimal> byMain, string productionMainNo, string key, string title)
    {
        if (!byMain.TryGetValue(productionMainNo, out var weight) || weight <= 0m)
            return null;

        return new MainProgressBranchDto
        {
            Key = key,
            Title = title,
            Leaves = [new MainProgressLeafDto { Key = "Inbound", Text = "入库", WeightKg = weight }],
        };
    }

    // ===================== 生产执行（快照） =====================
    private static MainProgressBranchDto? BuildProductionBranch(List<WorkOrderExecutionSummary> items)
    {
        var leaves = new List<MainProgressLeafDto>();
        foreach (var (key, label, value) in ProductionNodeDefs)
        {
            var sum = items.Sum(r => value(r) ?? 0m);
            if (sum > 0m)
                leaves.Add(new MainProgressLeafDto { Key = key, Text = label, WeightKg = sum });
        }
        return leaves.Count > 0
            ? new MainProgressBranchDto { Key = "Production", Title = "生产执行[待产]", Leaves = leaves }
            : null;
    }

    // ===================== 原料锁定（快照，按类取现成字段） =====================
    private static MainProgressBranchDto BuildLockBranch(List<WorkOrderExecutionSummary> items, string remark)
    {
        // Missing(r)：复刻 WorkOrderExecutionSummaryDto.TotalMissingWeight 现可口口径
        // = Max(0, 计划投料总重 − 现可投料总重)，缺口>计划×3% 容差才取值（小缺口降噪）
        var missingSum = items.Sum(r => ComputeMissing(r));
        // Rework(r)：待返整成重（生产返整补足的量）
        var reworkSum = items.Sum(r => r.PendingReworkOutputWeight ?? 0m);

        var weight = remark switch
        {
            // 质量补料（QualityReplenish）：返整也补不足、真需外补的量
            RawMaterialLockRemarkKeys.QualityReplenish => Math.Max(0m, missingSum - reworkSum),
            // 生产返整补足（ExecuteRework）：待返整成重
            RawMaterialLockRemarkKeys.ExecuteRework => reworkSum,
            // 执行用料计划（ExecutePlan） / 完善用料计划（ImprovePlan）：缺料缺口（前者=在途未到货，后者=计划尚缺）
            _ => missingSum,
        };

        return new MainProgressBranchDto
        {
            Key = "RawMaterialLock",
            Title = "原料锁定",
            Leaves =
            [
                new MainProgressLeafDto
                {
                    Key = remark,
                    Text = RawMaterialLockRemarkKeys.ToChinese(remark) ?? remark,
                    WeightKg = weight,
                }
            ],
        };
    }

    private static decimal ComputeMissing(WorkOrderExecutionSummary r)
    {
        var plan = r.PiercingPlanWeight + r.SemiPlanWeight + r.FinishPlanWeight
            + r.InventoryPlanWeight + r.ReworkPlanWeight
            + r.InProcessReworkPlanWeight + r.InMainPlanWeight;
        var available = r.PiercingSubInWeight + r.SemiInWeight + r.FinishInWeight
            + r.InventoryOutWeight + r.ReworkPlanInputWeight
            + r.InProcessReworkInputWeight + r.InMainInputWeight;

        var missing = plan - available;
        if (missing <= 0m)
            return 0m;
        return missing > plan * MaterialPlanToleranceProvider.InputConsistencyTolerance ? missing : 0m;
    }

    // ===================== 项次规格信息（实时 join OrderItem） =====================
    /// <summary>实时取该订单被工单引用的 OrderItem：主号→(标准牌号,产品标准) + 整单去重项次数</summary>
    private async Task<OrderItemSpecInfo> BuildOrderItemInfoAsync(string salesOrderNo)
    {
        var salesOrder = await _context.SalesOrders.AsNoTracking()
            .FirstOrDefaultAsync(so => so.OrderNumber == salesOrderNo);

        if (salesOrder == null)
            return new OrderItemSpecInfo(new Dictionary<string, (string? StandardGrade, string? ProductStandard)>(StringComparer.Ordinal), 0);

        // 收集该订单下各工单合并的项次 Sequence（逗号分隔，真库实测如 "27,29,30"）
        var woRows = await _context.WorkOrders.AsNoTracking()
            .Where(wo => wo.SalesOrderNo == salesOrderNo)
            .Select(wo => new { wo.ProductionMainNo, wo.OrderItemIds })
            .ToListAsync();

        var seqSet = new HashSet<int>();
        foreach (var wo in woRows)
            foreach (var part in wo.OrderItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part, out var seq))
                    seqSet.Add(seq);

        // 分片查项次防 SQL 参数上限；Sequence 订单内唯一，按 SalesOrderId 过滤避免跨单冲突（同 WorkOrderService）
        var itemsBySeq = new Dictionary<int, (string StandardGrade, string? StandardNo)>();
        if (seqSet.Count > 0)
        {
            foreach (var chunk in seqSet.ToList().Chunk(1000))
            {
                var seqs = chunk.ToList();
                var rows = await _context.OrderItems.AsNoTracking()
                    .Where(oi => oi.SalesOrderId == salesOrder.Id && seqs.Contains(oi.Sequence))
                    .Select(oi => new { oi.Sequence, oi.StandardGrade, oi.StandardNo })
                    .ToListAsync();
                foreach (var r in rows)
                    itemsBySeq.TryAdd(r.Sequence, (r.StandardGrade, r.StandardNo));
            }
        }

        // 主号取该主号下首个被引用项次的牌号/标准（同主号一般同一规格，取首个即代表）
        var byMain = new Dictionary<string, (string? StandardGrade, string? ProductStandard)>(StringComparer.Ordinal);
        foreach (var wo in woRows)
        {
            string? grade = null;
            string? standard = null;
            foreach (var part in wo.OrderItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(part, out var seq) || !itemsBySeq.TryGetValue(seq, out var info))
                    continue;
                grade ??= info.StandardGrade;
                standard ??= info.StandardNo;
                if (grade != null && standard != null)
                    break;
            }
            byMain.TryAdd(wo.ProductionMainNo, (grade, standard));
        }

        return new OrderItemSpecInfo(byMain, itemsBySeq.Count);
    }

    /// <summary>项次规格信息：主号→牌号/标准 映射 + 整单去重项次数</summary>
    private sealed record OrderItemSpecInfo(
        Dictionary<string, (string? StandardGrade, string? ProductStandard)> ByMain,
        int ItemCount);

    // ===================== 通用 =====================
    /// <summary>取组内最紧急的 UrgencyLevel（英文 Key；按 UrgencyLevelKeys.All 顺序 A+ > A > … > E）</summary>
    private static string? MostUrgent(List<WorkOrderExecutionSummary> items)
        => items
            .Select(i => UrgencyLevelKeys.ToKey(i.UrgencyLevel))
            .Where(k => k != null)
            .OrderBy(k => UrgencyRank(k))
            .Select(k => k!)
            .FirstOrDefault();

    private static int UrgencyRank(string? urgency)
    {
        var key = UrgencyLevelKeys.ToKey(urgency);
        return key == null ? int.MaxValue : Array.IndexOf(UrgencyLevelKeys.All, key);
    }
}
