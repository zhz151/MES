using FluentAssertions;
using MES.Core.Enums;
using MES.Data.Entities;
using MES.Services.Mapping;

namespace MES.Tests.Services;

/// <summary>
/// DtoMapper 单元测试：验证 Entity → DTO 映射，重点关注含业务逻辑的 CalculateUnitWeight
/// </summary>
public class DtoMapperTests
{
    /// <summary>
    /// 构建一个最小 WorkOrder
    /// </summary>
    private static WorkOrder MakeWo(string spec = "219*8",
        LengthStatus lengthStatus = LengthStatus.Fixed,
        decimal odNeg = 0.5m, decimal odPos = 0.5m,
        decimal wtNeg = 0.5m, decimal wtPos = 0.5m,
        decimal? maxLength = 6000m)
    {
        return new WorkOrder
        {
            WorkOrderNo = "WO-TEST",
            SalesOrderNo = "SO-TEST",
            ProductionMainNo = "M-TEST",
            OrderItemIds = "1",
            SignDate = DateTime.Today,
            Salesman = "测试",
            DeliveryDate = DateTime.Today.AddMonths(1),
            DelayPenalty = false,
            StandardCode = "GB/T 8163",
            PlantGrade = "Q345B",
            Specification = spec,
            OuterDiameterNegative = odNeg,
            OuterDiameterPositive = odPos,
            WallThicknessNegative = wtNeg,
            WallThicknessPositive = wtPos,
            LengthStatus = lengthStatus,
            TotalQuantity = 100,
            TotalMeters = 1000m,
            TotalWeight = 10000m,
            TotalItemCount = 1,
            RowVersion = new byte[8],
            CreatedBy = "tester",
            MaxLength = maxLength,
        };
    }

    // ========== StandardDto / 纯字段映射 ==========

    [Fact]
    public void ToDto_ProductionStandard_映射所有字段()
    {
        var entity = new ProductionStandard
        {
            Id = 1,
            StandardCode = "GB/T 8163",
            StandardName = "流体管",
            Remark = "备注",
            SortOrder = 1,
            IsActive = true,
        };

        var dto = entity.ToDto();

        dto.Id.Should().Be(1);
        dto.StandardCode.Should().Be("GB/T 8163");
        dto.StandardName.Should().Be("流体管");
        dto.Remark.Should().Be("备注");
        dto.SortOrder.Should().Be(1);
        dto.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ToDto_GradeMapping_映射所有字段()
    {
        var entity = new StandardGradeMapping
        {
            Id = 1,
            StandardGrade = "Q345B",
            PlantGrade = "345B",
            Density = 7.85m,
            HeatTreatment = "正火",
            SpecialNote = "注意",
            Remark = "备注",
        };

        var dto = entity.ToDto();

        dto.StandardGrade.Should().Be("Q345B");
        dto.PlantGrade.Should().Be("345B");
        dto.Density.Should().Be(7.85m);
        dto.HeatTreatment.Should().Be("正火");
        dto.SpecialMaterial.Should().BeFalse();
        dto.SpecialNote.Should().Be("注意");
    }

    [Fact]
    public void ToDto_CustomerProfile_映射所有字段()
    {
        var entity = new CustomerProfile
        {
            Id = 1,
            CustomerCode = "C001",
            Salesman = "张三",
            CustomerUnit = "客户A",
            EndCustomer = "最终用户",
            ContactPerson = "李四",
            ContactPhone = "13800138000",
            Address = "北京",
            Status = CustomerStatus.Active,
            Remark = "重要客户",
        };

        var dto = entity.ToDto();

        dto.CustomerCode.Should().Be("C001");
        dto.Salesman.Should().Be("张三");
        dto.CustomerUnit.Should().Be("客户A");
        dto.EndCustomer.Should().Be("最终用户");
        dto.ContactPerson.Should().Be("李四");
        dto.ContactPhone.Should().Be("13800138000");
        dto.Address.Should().Be("北京");
        dto.Status.Should().Be(CustomerStatus.Active);
        dto.Remark.Should().Be("重要客户");
    }

    // ========== WorkOrder.ToListDto ==========

    [Fact]
    public void ToListDto_WorkOrder_Status和MaterialPlanStatus转为int()
    {
        var entity = MakeWo();
        entity.Status = WorkOrderStatus.Confirmed;
        entity.MaterialPlanStatus = MaterialPlanStatus.Partial;
        entity.MaterialPlanRate = 85;

        var dto = entity.ToListDto();

        dto.Status.Should().Be((int)WorkOrderStatus.Confirmed);
        dto.MaterialPlanStatus.Should().Be((int)MaterialPlanStatus.Partial);
        dto.MaterialPlanRate.Should().Be(85);
    }

    // ========== CalculateUnitWeight (通过 ToDetailDto) ==========

    [Fact]
    public void ToDetailDto_规格为空_UnitWeight为null()
    {
        var entity = MakeWo(spec: "");

        var dto = entity.ToDetailDto();

        dto.UnitWeight.Should().BeNull();
    }

    [Fact]
    public void ToDetailDto_定尺有MaxLength_正确计算UnitWeight()
    {
        // spec=219*8, ODneg=0.5, ODpos=0.5, WTneg=0.5, WTpos=0.5
        // odActual = 219 - 0.5*0.5 + 0.5*0.5 = 219
        // wtActual = 8 - 0.5*0.5 + 0.5*0.5 = 8
        // weightPerMeter = (219-8)*8*0.02466 = 41.62608
        // maxLength = 6000 (Fixed)
        // unitWeight = 41.62608 * 6000 / 1000 = 249.75648 → Math.Round(,3) = 249.756
        var entity = MakeWo(spec: "219*8", maxLength: 6000m);

        var dto = entity.ToDetailDto();

        dto.UnitWeight.Should().Be(249.756m);
    }

    [Fact]
    public void ToDetailDto_定尺MaxLength为null_默认4500()
    {
        var entity = MakeWo(spec: "219*8", maxLength: null);

        var dto = entity.ToDetailDto();

        // 41.62608 * 4500 / 1000 = 187.31736 → 187.317
        dto.UnitWeight.Should().Be(187.317m);
    }

    [Fact]
    public void ToDetailDto_非定尺_使用4500mm()
    {
        var entity = MakeWo(spec: "219*8", lengthStatus: LengthStatus.Range, maxLength: 8000m);

        var dto = entity.ToDetailDto();

        // 非 Fixed 不取 MaxLength，固定 4500
        dto.UnitWeight.Should().Be(187.317m);
    }

    [Fact]
    public void ToDetailDto_非定尺NonFixed_使用4500mm()
    {
        var entity = MakeWo(spec: "219*8", lengthStatus: LengthStatus.NonFixed, maxLength: 8000m);

        var dto = entity.ToDetailDto();

        dto.UnitWeight.Should().Be(187.317m);
    }

    [Fact]
    public void ToDetailDto_规格格式错误_UnitWeight为null()
    {
        // 没有 "*" 分隔 → ParseWallThickness 返回 null
        var entity = MakeWo(spec: "219");

        var dto = entity.ToDetailDto();

        dto.UnitWeight.Should().BeNull();
    }

    [Fact]
    public void ToDetailDto_OD和WT为0_UnitWeight为null()
    {
        // spec=0*0, 解析后 OD=0, WT=0
        var entity = MakeWo(spec: "0*0");

        var dto = entity.ToDetailDto();

        dto.UnitWeight.Should().BeNull();
    }

    [Fact]
    public void ToDetailDto_公差导致OD无效_UnitWeight为null()
    {
        // odActual = 10 - 0.5*100 + 0.5*0 = 10 - 50 = -40 ≤ 0 → null
        var entity = MakeWo(spec: "10*5", odNeg: 100m, odPos: 0m);

        var dto = entity.ToDetailDto();

        dto.UnitWeight.Should().BeNull();
    }

    [Fact]
    public void ToDetailDto_公差为0_正确计算()
    {
        // odActual = 60 - 0 + 0 = 60
        // wtActual = 5 - 0 + 0 = 5
        // weightPerMeter = (60-5)*5*0.02466 = 6.7815
        // unitWeight = 6.7815 * 6000 / 1000 = 40.689 → 40.689
        var entity = MakeWo(spec: "60*5", odNeg: 0m, odPos: 0m, wtNeg: 0m, wtPos: 0m, maxLength: 6000m);

        var dto = entity.ToDetailDto();

        dto.UnitWeight.Should().Be(40.689m);
    }

    [Fact]
    public void ToDetailDto_负公差非对称_正确计算()
    {
        // odActual = 60 - 0.5*0.3 + 0.5*0.7 = 60 - 0.15 + 0.35 = 60.2
        // wtActual = 5 - 0.5*0.2 + 0.5*0.4 = 5 - 0.1 + 0.2 = 5.1
        // weightPerMeter = (60.2-5.1)*5.1*0.02466 = 55.1*5.1*0.02466 = 6.9297066
        // unitWeight = 6.9297066 * 6000 / 1000 = 41.5782396 → 41.578
        var entity = MakeWo(spec: "60*5",
            odNeg: 0.3m, odPos: 0.7m,
            wtNeg: 0.2m, wtPos: 0.4m,
            maxLength: 6000m);

        var dto = entity.ToDetailDto();

        dto.UnitWeight.Should().Be(41.578m);
    }

    // ========== ProcessGroupDto ==========

    [Fact]
    public void ToGroupDto_映射所有字段()
    {
        var entity = new ProcessGroup
        {
            Id = 1,
            ProductionBatchId = 10,
            SequenceNumber = 2,
            ProcessName = "60冷轧",
            ManufacturingSpec = "219*8",
            ManufacturingMultiple = 1,
            ColdRollDraw = 3,
            OilPipeCut = null,
        };

        var dto = entity.ToGroupDto();

        dto.Id.Should().Be(1);
        dto.ProductionBatchId.Should().Be(10);
        dto.SequenceNumber.Should().Be(2);
        dto.ProcessName.Should().Be("60冷轧");
        dto.ColdRollDraw.Should().Be(3);
        dto.OilPipeCut.Should().BeNull();
    }

    // ========== Inventory/Outbound ==========

    [Fact]
    public void ToDto_OutboundRecord_OutboundType转为字符串()
    {
        var entity = new OutboundRecord
        {
            InventoryBatchId = 1,
            OutboundType = OutboundType.SalesOut,
            SourceOrderNo = "SO001",
            TargetCompany = "客户A",
            OutboundQuantity = 10,
            OutboundWeight = 1000m,
            OutboundDate = DateTime.Today,
            CreatedBy = "user1",
        };

        var dto = entity.ToDto();

        dto.OutboundType.Should().Be("SalesOut");
        dto.SourceOrderNo.Should().Be("SO001");
    }

    [Fact]
    public void ToDto_InventoryBatch_映射布尔字段()
    {
        var entity = new InventoryBatch
        {
            BatchNo = "CK001",
            IsLinkedToWorkOrder = true,
        };

        var dto = entity.ToDto();

        dto.IsLinkedToWorkOrder.Should().BeTrue();
    }
}
