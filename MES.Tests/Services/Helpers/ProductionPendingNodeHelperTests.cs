using FluentAssertions;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Data.Entities.Batch;
using MES.Data.Entities.WorkOrder;
using MES.Services.Helpers;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// 生产执行 8 节点待量共享计算器测试（纯内存，不依赖 DbContext）：
/// 节点表单源锁定（顺序/Key/工段映射）、在产/在途分段不变式（Σ段 == 节点总量）、
/// 命中口径旁证（未到达工序组 → 在途；工序组含目标工段才计；已越过/成检/完成 → 不计）、
/// 段内稳定序、Totals/ApplyTo 出口语义。
/// </summary>
public class ProductionPendingNodeHelperTests
{
    private static ProcessGroup Pg(string processName, int seq,
        int? coldRollDraw = null, int? outerPolish = null, int? inspection = null,
        int? straighten = null, int? pickle = null)
        => new()
        {
            ProcessName = processName,
            SequenceNumber = seq,
            ColdRollDraw = coldRollDraw,
            OuterPolish = outerPolish,
            Inspection = inspection,
            Straighten = straighten,
            Pickle = pickle,
        };

    private static ProductionBatch Batch(int id, string batchNo, BatchStatus status, decimal? weight,
        string? currentGroup, string? currentSection, bool? sectionCompleted, params ProcessGroup[] groups)
        => new()
        {
            Id = id,
            BatchNo = batchNo,
            Status = status,
            CurrentValidWeight = weight == null ? null : (int)weight.Value,
            CurrentGroupName = currentGroup,
            CurrentSectionName = currentSection,
            CurrentSectionCompleted = sectionCompleted,
            ManufacturingItem = InventoryMaterialTypes.OrderFinished,
            WorkOrderNo = "WO-1",
            SalesOrderNo = "SO001",
            ProductionMainNo = "G100",
            OrderItemIds = "1",
            Salesman = "业务员",
            MaterialName = "无缝管",
            SettlementMethod = "电汇",
            StandardCode = "GB/T13296",
            DeliveryState = "Fixed",
            LengthStatus = "Fixed",
            PlantGrade = "304",
            Specification = "219*8",
            TechnicalRequirements = "无",
            ProcessGroups = groups.ToList(),
        };

    private static decimal Total(Dictionary<string, ProductionPendingNodeHelper.NodePending> map, string key)
        => map[key].TotalKg;

    // ===================== 节点表单源锁定 =====================

    [Fact]
    public void NodeDefs_顺序与键集锁定()
    {
        ProductionPendingNodeHelper.NodeDefs.Select(d => d.Key).Should().Equal(
            "RoughTubeProcessing", "InProcessRepair", "ColdRoll60", "ColdRoll50",
            "ColdRoll30", "ColdRoll20", "ThreeRollColdRoll", "ColdDraw");

        ProductionPendingNodeHelper.NodeDefs.Select(d => d.Label).Should().Equal(
            "荒管处理", "在制修检", "60冷轧", "50冷轧", "30冷轧", "20冷轧", "三辊冷轧", "冷拔");

        // Key == 工序组名（叶子 Key 直接复用工序组 Key）
        ProductionPendingNodeHelper.NodeDefs.Should().OnlyContain(d => d.Key == d.ProcessName);

        // 目标工段：荒管处理→外抛光、在制修检→检验，其余 6 项→冷轧拔
        ProductionPendingNodeHelper.NodeDefs[0].SectionName.Should().Be(SectionKeys.OuterPolish);
        ProductionPendingNodeHelper.NodeDefs[1].SectionName.Should().Be(SectionKeys.Inspection);
        ProductionPendingNodeHelper.NodeDefs.Skip(2)
            .Should().OnlyContain(d => d.SectionName == SectionKeys.ColdRollDraw);
    }

    // ===================== ActiveBatches =====================

    [Fact]
    public void ActiveBatches_排除成检与完成批次()
    {
        var batches = new List<ProductionBatch>
        {
            Batch(1, "B1", BatchStatus.InProgress, 100m, null, null, null),
            Batch(2, "B2", BatchStatus.InFinalInspection, 100m, null, null, null),
            Batch(3, "B3", BatchStatus.Completed, 100m, null, null, null),
            Batch(4, "B4", BatchStatus.None, 100m, null, null, null),
        };

        ProductionPendingNodeHelper.ActiveBatches(batches).Select(b => b.BatchNo)
            .Should().Equal("B1", "B4");
    }

    // ===================== 在产 / 在途 分段 =====================

    [Fact]
    public void Compute_未到达工序组计在途_已在本工序组计在产()
    {
        // 批次当前在 荒管处理（seq1）：荒管处理=在产（无当前工段 → 未到外抛光），60冷轧（seq2）=在途
        var batch = Batch(1, "B1", BatchStatus.InProgress, 100m,
            currentGroup: ProcessKeys.RoughTubeProcessing, currentSection: null, sectionCompleted: null,
            Pg(ProcessKeys.RoughTubeProcessing, 1, outerPolish: 5),
            Pg(ProcessKeys.ColdRoll60, 2, coldRollDraw: 1));

        var map = ProductionPendingNodeHelper.Compute(new List<ProductionBatch> { batch });

        var rough = map[ProcessKeys.RoughTubeProcessing];
        rough.TotalKg.Should().Be(100m);
        rough.InProgress.Should().ContainSingle().Which.BatchNo.Should().Be("B1");
        rough.InTransit.Should().BeEmpty();

        var roll60 = map[ProcessKeys.ColdRoll60];
        roll60.TotalKg.Should().Be(100m);
        roll60.InProgress.Should().BeEmpty();
        roll60.InTransit.Should().ContainSingle().Which.BatchNo.Should().Be("B1");
    }

    [Fact]
    public void Compute_在产与在途两段合计恒等于节点总量()
    {
        var batches = new List<ProductionBatch>
        {
            Batch(1, "B1", BatchStatus.InProgress, 100m, ProcessKeys.RoughTubeProcessing, null, null,
                Pg(ProcessKeys.RoughTubeProcessing, 1, outerPolish: 5),
                Pg(ProcessKeys.ColdRoll60, 2, coldRollDraw: 1),
                Pg(ProcessKeys.ColdDraw, 3, coldRollDraw: 1)),
            Batch(2, "B2", BatchStatus.InProgress, 250m, ProcessKeys.ColdRoll50, SectionKeys.ColdRollDraw, false,
                Pg(ProcessKeys.ColdRoll50, 1, coldRollDraw: 2)),
            Batch(3, "B3", BatchStatus.InProgress, 70m, ProcessKeys.ColdRoll60, SectionKeys.Straighten, false,
                Pg(ProcessKeys.RoughTubeProcessing, 1, outerPolish: 5),
                Pg(ProcessKeys.ColdRoll60, 2, coldRollDraw: 1, outerPolish: 3, inspection: 4)),
            Batch(4, "B4", BatchStatus.None, 33m, null, null, null,
                Pg(ProcessKeys.ColdRoll30, 1, coldRollDraw: 1)),
        };

        var map = ProductionPendingNodeHelper.Compute(batches);

        map.Keys.Should().BeEquivalentTo(ProductionPendingNodeHelper.NodeDefs.Select(d => d.Key));
        foreach (var def in ProductionPendingNodeHelper.NodeDefs)
        {
            var node = map[def.Key];
            (node.InProgress.Sum(b => b.WeightKg) + node.InTransit.Sum(b => b.WeightKg))
                .Should().Be(node.TotalKg, $"{def.Key} 的分段只是对同一命中集合分区，不增不减");
        }
    }

    [Fact]
    public void Compute_同一批次在同一节点只落一段()
    {
        var batch = Batch(1, "B1", BatchStatus.InProgress, 100m, ProcessKeys.RoughTubeProcessing, null, null,
            Pg(ProcessKeys.RoughTubeProcessing, 1, outerPolish: 5),
            Pg(ProcessKeys.ColdRoll60, 2, coldRollDraw: 1));

        var map = ProductionPendingNodeHelper.Compute(new List<ProductionBatch> { batch });

        foreach (var def in ProductionPendingNodeHelper.NodeDefs)
        {
            var node = map[def.Key];
            (node.InProgress.Count + node.InTransit.Count).Should().Be(
                node.InProgress.Concat(node.InTransit).Select(b => b.BatchId).Distinct().Count());
        }
    }

    // ===================== 荒管处理 / 在制修检：工段级到达判定 =====================

    [Fact]
    public void Compute_荒管处理_无当前工段计入在产()
    {
        var batch = Batch(1, "B1", BatchStatus.InProgress, 100m,
            ProcessKeys.RoughTubeProcessing, null, null,
            Pg(ProcessKeys.RoughTubeProcessing, 1, outerPolish: 5));

        ProductionPendingNodeHelper.Compute(new List<ProductionBatch> { batch })[ProcessKeys.RoughTubeProcessing]
            .InProgress.Should().ContainSingle();
    }

    [Fact]
    public void Compute_荒管处理_当前工段早于目标工段计入在产_晚于目标工段不计()
    {
        // 工序组内：整直=3、外抛光=5、酸洗=7
        static ProcessGroup[] Groups() =>
        [
            Pg(ProcessKeys.RoughTubeProcessing, 1, outerPolish: 5, straighten: 3, pickle: 7)
        ];

        var before = Batch(1, "B1", BatchStatus.InProgress, 100m,
            ProcessKeys.RoughTubeProcessing, SectionKeys.Straighten, null, Groups());
        ProductionPendingNodeHelper.Compute(new List<ProductionBatch> { before })[ProcessKeys.RoughTubeProcessing]
            .TotalKg.Should().Be(100m);

        var after = Batch(2, "B2", BatchStatus.InProgress, 100m,
            ProcessKeys.RoughTubeProcessing, SectionKeys.Pickle, null, Groups());
        ProductionPendingNodeHelper.Compute(new List<ProductionBatch> { after })[ProcessKeys.RoughTubeProcessing]
            .TotalKg.Should().Be(0m);
    }

    [Fact]
    public void Compute_荒管处理_批次当前已不在该工序组不计()
    {
        // 当前工序组 = 60冷轧（seq2）→ 已越过荒管处理（seq1）
        var batch = Batch(1, "B1", BatchStatus.InProgress, 100m,
            ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, false,
            Pg(ProcessKeys.RoughTubeProcessing, 1, outerPolish: 5),
            Pg(ProcessKeys.ColdRoll60, 2, coldRollDraw: 1));

        ProductionPendingNodeHelper.Compute(new List<ProductionBatch> { batch })[ProcessKeys.RoughTubeProcessing]
            .TotalKg.Should().Be(0m);
    }

    // ===================== 冷轧/冷拔：工段完成判定 + 工序组不含目标工段 =====================

    [Fact]
    public void Compute_冷轧_正在目标工段且未完成计入在产_已完成不计()
    {
        var running = Batch(1, "B1", BatchStatus.InProgress, 100m,
            ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, false,
            Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1));
        ProductionPendingNodeHelper.Compute(new List<ProductionBatch> { running })[ProcessKeys.ColdRoll60]
            .InProgress.Should().ContainSingle();

        var done = Batch(2, "B2", BatchStatus.InProgress, 100m,
            ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, true,
            Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1));
        ProductionPendingNodeHelper.Compute(new List<ProductionBatch> { done })[ProcessKeys.ColdRoll60]
            .TotalKg.Should().Be(0m);
    }

    [Fact]
    public void Compute_工序组不含目标工段不计入()
    {
        // 批次未到达 60冷轧（在途分支），但该工序组未定义冷轧拔工段 → 不计
        var batch = Batch(1, "B1", BatchStatus.InProgress, 100m,
            ProcessKeys.RoughTubeProcessing, null, null,
            Pg(ProcessKeys.RoughTubeProcessing, 1, outerPolish: 5),
            Pg(ProcessKeys.ColdRoll60, 2, coldRollDraw: null));

        ProductionPendingNodeHelper.Compute(new List<ProductionBatch> { batch })[ProcessKeys.ColdRoll60]
            .TotalKg.Should().Be(0m);
    }

    // ===================== 边界：完成批次 / 空集合 / 无工序组 =====================

    [Fact]
    public void Compute_已完成批次不参与()
    {
        var completed = Batch(1, "B1", BatchStatus.Completed, 100m,
            ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, false,
            Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1));

        var map = ProductionPendingNodeHelper.Compute(new List<ProductionBatch> { completed });

        map.Values.Should().OnlyContain(n => n.TotalKg == 0m);
    }

    [Fact]
    public void Compute_空集合与无工序组批次_返回全0且不抛()
    {
        var empty = ProductionPendingNodeHelper.Compute(new List<ProductionBatch>());
        empty.Should().HaveCount(8);
        empty.Values.Should().OnlyContain(n => n.TotalKg == 0m && n.InProgress.Count == 0 && n.InTransit.Count == 0);

        var noGroups = Batch(1, "B1", BatchStatus.InProgress, 100m,
            ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, false);
        var map = ProductionPendingNodeHelper.Compute(new List<ProductionBatch> { noGroups });
        map.Values.Should().OnlyContain(n => n.TotalKg == 0m);
    }

    [Fact]
    public void Compute_未填有效重量_按0计入名单()
    {
        var batch = Batch(1, "B1", BatchStatus.InProgress, null,
            ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, false,
            Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1));

        var node = ProductionPendingNodeHelper.Compute(new List<ProductionBatch> { batch })[ProcessKeys.ColdRoll60];

        node.TotalKg.Should().Be(0m);
        node.InProgress.Should().ContainSingle().Which.WeightKg.Should().Be(0m);
    }

    // ===================== 段内稳定序 =====================

    [Fact]
    public void Compute_段内按生产编号Ordinal升序()
    {
        var batches = new List<ProductionBatch>
        {
            Batch(1, "2603-300", BatchStatus.None, 1m, null, null, null, Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1)),
            Batch(2, "2603-100", BatchStatus.None, 1m, null, null, null, Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1)),
            Batch(3, "2603-200", BatchStatus.None, 1m, null, null, null, Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1)),
        };

        ProductionPendingNodeHelper.Compute(batches)[ProcessKeys.ColdRoll60]
            .InTransit.Select(b => b.BatchNo).Should().Equal("2603-100", "2603-200", "2603-300");
    }

    // ===================== 出口：Totals / ApplyTo =====================

    [Fact]
    public void Totals_按节点Key返回合计_大小写不敏感()
    {
        var batch = Batch(1, "B1", BatchStatus.InProgress, 100m,
            ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, false,
            Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1));

        var totals = ProductionPendingNodeHelper.Totals(ProductionPendingNodeHelper.Compute(new List<ProductionBatch> { batch }));

        totals[ProcessKeys.ColdRoll60].Should().Be(100m);
        totals["coldroll60"].Should().Be(100m); // OrdinalIgnoreCase
        totals[ProcessKeys.ColdDraw].Should().Be(0m);
    }

    [Fact]
    public void ApplyTo_零值写null_正值按节点写对应字段()
    {
        // 每节点给互不相同的重量，任何字段错位/互换都会被断言抓住
        var batches = new List<ProductionBatch>
        {
            Batch(1, "B1", BatchStatus.InProgress, 11m, ProcessKeys.ColdRoll60, SectionKeys.ColdRollDraw, false,
                Pg(ProcessKeys.ColdRoll60, 1, coldRollDraw: 1)),
            Batch(2, "B2", BatchStatus.InProgress, 22m, ProcessKeys.ColdRoll50, SectionKeys.ColdRollDraw, false,
                Pg(ProcessKeys.ColdRoll50, 1, coldRollDraw: 1)),
            Batch(3, "B3", BatchStatus.InProgress, 33m, ProcessKeys.RoughTubeProcessing, null, null,
                Pg(ProcessKeys.RoughTubeProcessing, 1, outerPolish: 5)),
            Batch(4, "B4", BatchStatus.InProgress, 44m, ProcessKeys.InProcessRepair, null, null,
                Pg(ProcessKeys.InProcessRepair, 1, inspection: 2)),
        };

        var summary = new WorkOrderExecutionSummary();
        ProductionPendingNodeHelper.ApplyTo(summary, ProductionPendingNodeHelper.Compute(batches));

        summary.PendingSectionRoughTube.Should().Be(33m);
        summary.PendingSectionWarehouseFix.Should().Be(44m);
        summary.PendingSection60Roll.Should().Be(11m);
        summary.PendingSection50Roll.Should().Be(22m);
        // 未命中的节点写 null（而非 0）
        summary.PendingSection30Roll.Should().BeNull();
        summary.PendingSection20Roll.Should().BeNull();
        summary.PendingSectionThreeRoll.Should().BeNull();
        summary.PendingSectionDrawBench.Should().BeNull();
    }
}
