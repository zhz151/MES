using FluentAssertions;
using MES.Core.Enums;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Warehouse;
using MES.Services.Helpers;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// 委外共享逻辑 SubcontractHelper 纯函数测试：
/// AggregateReturnsBySequence（按委外单/序号聚合退货量重、序号空归 0、批次未命中跳过、单号大小写不敏感）
/// 与 RecalcReturnItemStatus / SyncReturnItemFromBatches（净回收口径状态机：
/// Sent/PartialReturned/Completed/OverReceived、超额优先于完成、退货扣减、强制完成不重算）。
/// </summary>
public class SubcontractHelperTests
{
    private const decimal OverRatio = 1.1m;
    private const decimal OverDeviation = 5m;

    private static (string BatchNo, string? SourceOrderNo, int? SourceOrderSequence) Batch(
        string batchNo, string? orderNo, int? sequence)
        => (batchNo, orderNo, sequence);

    private static OutboundRecord ReturnOut(string? sourceBatchNo, int quantity, decimal weight)
        => new() { ReturnSourceBatchNo = sourceBatchNo, OutboundQuantity = quantity, OutboundWeight = weight };

    private static SubcontractReturnItem NewItem(int sequence = 1, int? requiredQty = null,
        decimal? requiredWeight = null, bool isForceCompleted = false)
        => new()
        {
            Sequence = sequence,
            RequiredQuantity = requiredQty,
            RequiredWeight = requiredWeight,
            ProcessStatus = SubcontractOrderStatus.Sent.ToString(),
            IsForceCompleted = isForceCompleted
        };

    private static InventoryBatch Batch(int? sequence, int quantity, decimal weight)
        => new() { SourceOrderSequence = sequence, InitialQuantity = quantity, InitialWeight = weight };

    // ========== AggregateReturnsBySequence ==========

    [Fact]
    public void AggregateReturnsBySequence_同批次多退货出库_按委外单及序号累加()
    {
        var batches = new List<(string BatchNo, string? SourceOrderNo, int? SourceOrderSequence)>
        {
            Batch("CK001", "WW-100", 1)
        };
        var outbounds = new List<OutboundRecord>
        {
            ReturnOut("CK001", 5, 50m),
            ReturnOut("CK001", 3, 30m)
        };

        var result = SubcontractHelper.AggregateReturnsBySequence(outbounds, batches);

        result["WW-100"][1].Should().Be((8, 80m));
        // 单号键大小写不敏感
        result["ww-100"][1].Should().Be((8, 80m));
    }

    [Fact]
    public void AggregateReturnsBySequence_来源序号为空_归入序号0()
    {
        var batches = new List<(string BatchNo, string? SourceOrderNo, int? SourceOrderSequence)>
        {
            Batch("CK002", "WW-200", null)
        };
        var outbounds = new List<OutboundRecord> { ReturnOut("CK002", 2, 20m) };

        var result = SubcontractHelper.AggregateReturnsBySequence(outbounds, batches);

        result["WW-200"].Should().ContainKey(0);
        result["WW-200"][0].Should().Be((2, 20m));
    }

    [Fact]
    public void AggregateReturnsBySequence_空单号批次被忽略()
    {
        var batches = new List<(string BatchNo, string? SourceOrderNo, int? SourceOrderSequence)>
        {
            Batch("CK003", "", 1)
        };
        var outbounds = new List<OutboundRecord> { ReturnOut("CK003", 1, 10m) };

        var result = SubcontractHelper.AggregateReturnsBySequence(outbounds, batches);

        result.Should().BeEmpty();
    }

    [Fact]
    public void AggregateReturnsBySequence_批次号未命中或为空_跳过()
    {
        var batches = new List<(string BatchNo, string? SourceOrderNo, int? SourceOrderSequence)>
        {
            Batch("CK001", "WW-100", 1)
        };
        var outbounds = new List<OutboundRecord>
        {
            ReturnOut("NO-SUCH-BATCH", 1, 10m),
            ReturnOut(null, 1, 10m),
            ReturnOut("", 1, 10m)
        };

        var result = SubcontractHelper.AggregateReturnsBySequence(outbounds, batches);

        result.Should().BeEmpty();
    }

    // ========== SyncReturnItemFromBatches / RecalcReturnItemStatus ==========

    [Fact]
    public void SyncReturnItemFromBatches_仅序号匹配批次_汇总数量重量并判完成()
    {
        var item = NewItem(requiredQty: 50, requiredWeight: 500);
        var batches = new List<InventoryBatch>
        {
            Batch(1, 40, 400m),
            Batch(1, 10, 100m),
            Batch(2, 999, 999m) // 非本序号，不应计入
        };

        SubcontractHelper.SyncReturnItemFromBatches(item, batches, 1m, 0.01m);

        item.ReturnedQuantity.Should().Be(50);
        item.ReturnedWeight.Should().Be(500);
        item.ProcessStatus.Should().Be(SubcontractOrderStatus.Completed.ToString());
    }

    [Fact]
    public void SyncReturnItemFromBatches_退货扣减净回收_重算为部分回收()
    {
        var item = NewItem(requiredQty: 50, requiredWeight: 500);
        var batches = new List<InventoryBatch> { Batch(1, 40, 400m) };

        SubcontractHelper.SyncReturnItemFromBatches(item, batches, 1m, 0.01m,
            new Dictionary<int, (int Quantity, decimal Weight)> { [1] = (10, 100m) });

        // 净回收 30 支 / 300kg，未达需求 → PartialReturned
        item.ProcessStatus.Should().Be(SubcontractOrderStatus.PartialReturned.ToString());
    }

    [Fact]
    public void SyncReturnItemFromBatches_扣减退货后净回收仍达需求_判完成()
    {
        var item = NewItem(requiredQty: 50, requiredWeight: 500);
        var batches = new List<InventoryBatch> { Batch(1, 60, 600m) };

        SubcontractHelper.SyncReturnItemFromBatches(item, batches, 1m, 0.01m,
            new Dictionary<int, (int Quantity, decimal Weight)> { [1] = (10, 100m) });

        item.ReturnedQuantity.Should().Be(60); // 实体仍存总回收，不扣退货
        item.ReturnedWeight.Should().Be(600);
        item.ProcessStatus.Should().Be(SubcontractOrderStatus.Completed.ToString());
    }

    [Fact]
    public void SyncReturnItemFromBatches_强制完成_不重算状态()
    {
        var item = NewItem(requiredQty: 50, requiredWeight: 500, isForceCompleted: true);
        item.ProcessStatus = SubcontractOrderStatus.Completed.ToString();

        SubcontractHelper.SyncReturnItemFromBatches(item, new List<InventoryBatch>(), 1m, 0.01m);

        // 无任何批次回收（净回收 0，若重算应落 Sent），但强制完成保持原状态
        item.ReturnedQuantity.Should().Be(0);
        item.ProcessStatus.Should().Be(SubcontractOrderStatus.Completed.ToString());
    }

    [Fact]
    public void RecalcReturnItemStatus_净回收为零_返回Sent()
    {
        var item = NewItem(requiredQty: 50, requiredWeight: 500);
        item.ReturnedQuantity = 50;
        item.ReturnedWeight = 500;

        SubcontractHelper.RecalcReturnItemStatus(item, OverRatio, OverDeviation, returnQuantity: 50, returnWeight: 500);

        item.ProcessStatus.Should().Be(SubcontractOrderStatus.Sent.ToString());
    }

    [Fact]
    public void RecalcReturnItemStatus_超额回收_优先于完成判定()
    {
        var item = NewItem(requiredQty: 60, requiredWeight: 600);
        item.ReturnedQuantity = 70;
        item.ReturnedWeight = 700;

        SubcontractHelper.RecalcReturnItemStatus(item, OverRatio, OverDeviation);

        // 数量已满足且重量超量 → 仍判 OverReceived（超额优先）
        item.ProcessStatus.Should().Be(SubcontractOrderStatus.OverReceived.ToString());
    }

    [Fact]
    public void RecalcReturnItemStatus_恰达超额阈值非严格大于_判完成()
    {
        var item = NewItem(requiredQty: 60, requiredWeight: 600);
        item.ReturnedQuantity = 66;
        item.ReturnedWeight = 660; // == 600 × 1.1，非 >，不触发超额

        SubcontractHelper.RecalcReturnItemStatus(item, OverRatio, OverDeviation);

        item.ProcessStatus.Should().Be(SubcontractOrderStatus.Completed.ToString());
    }

    [Fact]
    public void RecalcReturnItemStatus_数量需求为空_按重量判完成()
    {
        var item = NewItem(requiredWeight: 500); // RequiredQuantity = null
        item.ReturnedQuantity = 50;
        item.ReturnedWeight = 500;

        SubcontractHelper.RecalcReturnItemStatus(item, 1m, 0m);

        item.ProcessStatus.Should().Be(SubcontractOrderStatus.Completed.ToString());
    }

    [Fact]
    public void RecalcReturnItemStatus_未达需求_返回PartialReturned()
    {
        var item = NewItem(requiredQty: 50, requiredWeight: 500);
        item.ReturnedQuantity = 30;
        item.ReturnedWeight = 300;

        SubcontractHelper.RecalcReturnItemStatus(item, OverRatio, OverDeviation);

        item.ProcessStatus.Should().Be(SubcontractOrderStatus.PartialReturned.ToString());
    }
}
