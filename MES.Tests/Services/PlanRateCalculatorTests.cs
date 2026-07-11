using FluentAssertions;
using MES.Core.Enums;
using MES.Data.Entities;
using MES.Services;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.WorkOrder;

namespace MES.Tests.Services;

/// <summary>
/// 用料计划满足率/状态计算器测试
/// </summary>
public class PlanRateCalculatorTests
{
    // ========== Helper：快速构建 WorkOrder ==========

    private static MES.Data.Entities.WorkOrder.WorkOrder MakeWo(LengthStatus lengthStatus, int totalQty = 100, decimal totalWeight = 10000m)
    {
        return new MES.Data.Entities.WorkOrder.WorkOrder
        {
            WorkOrderNo = "WO-TEST",
            LengthStatus = lengthStatus,
            TotalQuantity = totalQty,
            TotalWeight = totalWeight,
            RowVersion = new byte[8],
            CreatedBy = "tester",
        };
    }

    // ========== 无计划 ==========

    [Fact]
    public void ComputeWorkOrderRate_空计划_返回NotPlanned()
    {
        var wo = MakeWo(LengthStatus.Fixed);

        (decimal rate, int status) = PlanRateCalculator.ComputeWorkOrderRate(wo, [], [], [], []);

        rate.Should().Be(0);
        status.Should().Be((int)MaterialPlanStatus.NotPlanned);
    }

    // ========== 定尺（Fixed）= 按支数计算 ==========

    [Fact]
    public void ComputeWorkOrderRate_半成品采购定尺_按支数乘倍率计算()
    {
        var wo = MakeWo(LengthStatus.Fixed, totalQty: 100);

        var semiPlans = new List<PurchaseSemiPlan>
        {
            new() { RequiredPieces = 50, InputMultiple = 2, RequiredWeight = 5000m }, // 50*2=100 → 100/100*100=100%
            new() { RequiredPieces = 10, InputMultiple = 2, RequiredWeight = 1000m }, // 10*2=20 → +20% → 120% 总分
        };

        (decimal rate, int status) = PlanRateCalculator.ComputeWorkOrderRate(wo, semiPlans, [], [], []);

        // (50*2 + 10*2) / 100 * 100 = 120
        rate.Should().Be(120);
        status.Should().Be((int)MaterialPlanStatus.Excess);
    }

    [Fact]
    public void ComputeWorkOrderRate_成品采购定尺_按支数直接计算()
    {
        var wo = MakeWo(LengthStatus.Fixed, totalQty: 100);

        var finishPlans = new List<PurchaseFinishedPlan>
        {
            new() { RequiredPiece = 80, RequiredWeight = 8000m },
            new() { RequiredPiece = 20, RequiredWeight = 2000m },
        };

        (decimal rate, int status) = PlanRateCalculator.ComputeWorkOrderRate(wo, [], finishPlans, [], []);

        // (80 + 20) / 100 * 100 = 100
        rate.Should().Be(100);
        status.Should().Be((int)MaterialPlanStatus.TheoreticalSatisfied);
    }

    [Fact]
    public void ComputeWorkOrderRate_库存使用定尺_按支数乘倍率计算()
    {
        var wo = MakeWo(LengthStatus.Fixed, totalQty: 50);

        var invPlans = new List<InventoryPlan>
        {
            new() { UsedQuantity = 25, InputMultiple = 2, UsedWeight = 5000m }, // 25*2=50 → 50/50*100=100%
        };

        (decimal rate, int status) = PlanRateCalculator.ComputeWorkOrderRate(wo, [], [], invPlans, []);

        rate.Should().Be(100);
        status.Should().Be((int)MaterialPlanStatus.TheoreticalSatisfied);
    }

    [Fact]
    public void ComputeWorkOrderRate_圆棒穿孔定尺_按支数乘倍率计算()
    {
        var wo = MakeWo(LengthStatus.Fixed, totalQty: 100);

        var piercingPlans = new List<RoundBarPiercingPlan>
        {
            new() { RequiredPieces = 40, InputMultiple = 2, RequiredWeight = 4000m }, // 40*2=80 → 80/100*100=80%
        };

        (decimal rate, int status) = PlanRateCalculator.ComputeWorkOrderRate(wo, [], [], [], piercingPlans);

        rate.Should().Be(80);
        status.Should().Be((int)MaterialPlanStatus.Partial);
    }

    // ========== 非定尺（非 Fixed）= 按重量计算 ==========

    [Fact]
    public void ComputeWorkOrderRate_半成品采购非定尺_按重量计算()
    {
        var wo = MakeWo(LengthStatus.Range, totalWeight: 10000m);

        var semiPlans = new List<PurchaseSemiPlan>
        {
            new() { RequiredWeight = 6000m },
        };

        (decimal rate, int status) = PlanRateCalculator.ComputeWorkOrderRate(wo, semiPlans, [], [], []);

        // 6000 / 10000 * 100 = 60
        rate.Should().Be(60);
        status.Should().Be((int)MaterialPlanStatus.Partial);
    }

    [Fact]
    public void ComputeWorkOrderRate_库存使用非定尺_按重量计算()
    {
        var wo = MakeWo(LengthStatus.NonFixed, totalWeight: 8000m);

        var invPlans = new List<InventoryPlan>
        {
            new() { UsedWeight = 5000m },
            new() { UsedWeight = 3000m },
        };

        (decimal rate, int status) = PlanRateCalculator.ComputeWorkOrderRate(wo, [], [], invPlans, []);

        // (5000 + 3000) / 8000 * 100 = 100
        rate.Should().Be(100);
        status.Should().Be((int)MaterialPlanStatus.TheoreticalSatisfied);
    }

    // ========== 多种计划叠加 ==========

    [Fact]
    public void ComputeWorkOrderRate_多种计划叠加_满足率求和()
    {
        var wo = MakeWo(LengthStatus.Fixed, totalQty: 100);

        var semiPlans = new List<PurchaseSemiPlan>
        {
            new() { RequiredPieces = 50, InputMultiple = 1, RequiredWeight = 5000m }, // 50/100*100=50%
        };
        var finishPlans = new List<PurchaseFinishedPlan>
        {
            new() { RequiredPiece = 30, RequiredWeight = 3000m }, // 30/100*100=30%
        };

        (decimal rate, int status) = PlanRateCalculator.ComputeWorkOrderRate(wo, semiPlans, finishPlans, [], []);

        // 50 + 30 = 80
        rate.Should().Be(80);
        status.Should().Be((int)MaterialPlanStatus.Partial);
    }

    [Fact]
    public void ComputeWorkOrderRate_多种计划叠加_上限999()
    {
        var wo = MakeWo(LengthStatus.Fixed, totalQty: 100);

        // 每个计划给 500%，共 2000%，但上限 999
        var finishPlans = new List<PurchaseFinishedPlan>
        {
            new() { RequiredPiece = 500, RequiredWeight = 50000m }, // 500%
        };
        var semiPlans = new List<PurchaseSemiPlan>
        {
            new() { RequiredPieces = 500, InputMultiple = 1, RequiredWeight = 50000m }, // 500%
        };
        var piercingPlans = new List<RoundBarPiercingPlan>
        {
            new() { RequiredPieces = 500, InputMultiple = 1, RequiredWeight = 50000m }, // 500%
        };
        var invPlans = new List<InventoryPlan>
        {
            new() { UsedQuantity = 500, InputMultiple = 1, UsedWeight = 50000m }, // 500%
        };

        (decimal rate, int status) = PlanRateCalculator.ComputeWorkOrderRate(wo, semiPlans, finishPlans, invPlans, piercingPlans);

        rate.Should().Be(999);
        status.Should().Be((int)MaterialPlanStatus.Excess);
    }

    // ========== 状态边界（Fixed） ==========

    [Theory]
    [InlineData(0, MaterialPlanStatus.NotPlanned)]     // =0
    [InlineData(99, MaterialPlanStatus.Partial)]        // <100
    [InlineData(100, MaterialPlanStatus.TheoreticalSatisfied)] // 100-101
    [InlineData(101, MaterialPlanStatus.TheoreticalSatisfied)] // 100-101
    [InlineData(102, MaterialPlanStatus.Satisfied)]     // 102-110
    [InlineData(110, MaterialPlanStatus.Satisfied)]     // 102-110
    [InlineData(111, MaterialPlanStatus.Excess)]        // >110
    public void CalculateOverallStatus_Fixed边界值(int totalRate, MaterialPlanStatus expected)
    {
        var wo = MakeWo(LengthStatus.Fixed, totalQty: 100);

        var finishPlans = new List<PurchaseFinishedPlan>
        {
            new() { RequiredPiece = totalRate, RequiredWeight = totalRate * 100m },
        };

        (decimal rate, int status) = PlanRateCalculator.ComputeWorkOrderRate(wo, [], finishPlans, [], []);

        rate.Should().Be(totalRate);
        status.Should().Be((int)expected);
    }

    // ========== 状态边界（非 Fixed） ==========

    [Theory]
    [InlineData(0, MaterialPlanStatus.NotPlanned)]     // =0
    [InlineData(99, MaterialPlanStatus.Partial)]        // <100
    [InlineData(100, MaterialPlanStatus.TheoreticalSatisfied)] // 100-104
    [InlineData(104, MaterialPlanStatus.TheoreticalSatisfied)] // 100-104
    [InlineData(105, MaterialPlanStatus.Satisfied)]     // 105-120
    [InlineData(120, MaterialPlanStatus.Satisfied)]     // 105-120
    [InlineData(121, MaterialPlanStatus.Excess)]        // >120
    public void CalculateOverallStatus_非Fixed边界值(int totalRate, MaterialPlanStatus expected)
    {
        var wo = MakeWo(LengthStatus.NonFixed, totalQty: 100);

        var finishPlans = new List<PurchaseFinishedPlan>
        {
            new() { RequiredPiece = totalRate, RequiredWeight = totalRate * 100m },
        };

        (decimal rate, int status) = PlanRateCalculator.ComputeWorkOrderRate(wo, [], finishPlans, [], []);

        rate.Should().Be(totalRate);
        status.Should().Be((int)expected);
    }

    // ========== 零值保护 ==========

    [Fact]
    public void ComputeWorkOrderRate_工单数量为0_返回0()
    {
        var wo = MakeWo(LengthStatus.Fixed, totalQty: 0);

        var finishPlans = new List<PurchaseFinishedPlan>
        {
            new() { RequiredPiece = 50, RequiredWeight = 5000m },
        };

        (decimal rate, _) = PlanRateCalculator.ComputeWorkOrderRate(wo, [], finishPlans, [], []);

        rate.Should().Be(0);
    }

    [Fact]
    public void ComputeWorkOrderRate_改制库存和普通库存分别计算()
    {
        var wo = MakeWo(LengthStatus.Fixed, totalQty: 100);

        var regularInv = new List<InventoryPlan>
        {
            new() { UsedQuantity = 50, InputMultiple = 1, UsedWeight = 5000m, ReworkType = null }, // 50%
        };
        var reworkInv = new List<InventoryPlan>
        {
            new() { UsedQuantity = 30, InputMultiple = 1, UsedWeight = 3000m, ReworkType = ReworkType.EmptyDrawing }, // 30%
        };

        // 合并进 inventoryPlans
        var allInv = regularInv.Concat(reworkInv).ToList();

        (decimal rate, _) = PlanRateCalculator.ComputeWorkOrderRate(wo, [], [], allInv, []);

        rate.Should().Be(80);
    }
}
