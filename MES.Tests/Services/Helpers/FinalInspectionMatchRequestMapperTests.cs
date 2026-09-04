using FluentAssertions;
using MES.Core.Enums;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Quality;
using MES.Services.Payroll;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// 成检记录 → 计件计价请求 共享映射 FinalInspectionMatchRequestMapper 纯函数测试（2026-09-04 引入）。
/// 结算采集（PieceRateCollector 成检源）与「按成检记录模拟测算」共用本映射（防双通道漂移）；
/// 此处逐字段断言 Spec→OD/WT、LengthStatus+FixedLength→Length（Range/NonFixed 按 6000 兜底）、
/// Quantity→InspectionCount、Weight→WeightKg、PlantGrade=批次牌号、EquipmentName=记录设备号、
/// 不设 SpecialState（成检记录无取数源，与结算一致）。
/// </summary>
public class FinalInspectionMatchRequestMapperTests
{
    private static ProductionBatch NewBatch(string? spec = "219*8",
        string? lengthStatus = nameof(LengthStatus.Fixed), string? plantGrade = "304")
        => new()
        {
            // 实体字段 NRT 为非空 string；测试允许传 null 模拟「规格缺失/无批次」用例
            Specification = spec!,
            LengthStatus = lengthStatus!,
            PlantGrade = plantGrade!
        };

    private static FinalInspection NewInspection(InspectionItem item = InspectionItem.Ultrasonic,
        string? fixedLength = "9150mm", int? quantity = 80, int? weight = 3000,
        string? equipmentName = "超声波探伤机", string? operatorName = null)
        => new()
        {
            InspectionItem = item,
            InspectionDate = new DateTime(2026, 9, 1),
            BatchNo = "BATCH-TRIAL",
            ProductionBatchId = 1,
            FixedLength = fixedLength,
            Quantity = quantity,
            Weight = weight,
            EquipmentName = equipmentName,
            Operator = operatorName
        };

    [Fact]
    public void BuildRequest_定尺_逐字段完整映射()
    {
        var batch = NewBatch(spec: "219*8", lengthStatus: nameof(LengthStatus.Fixed), plantGrade: "304");
        var inspection = NewInspection(fixedLength: "9150mm", quantity: 80, weight: 3000,
            equipmentName: "超声波探伤机");

        var req = FinalInspectionMatchRequestMapper.BuildRequest(inspection, batch);

        req.ItemKey.Should().Be(nameof(InspectionItem.Ultrasonic));
        req.LengthStatus.Should().Be(nameof(LengthStatus.Fixed));
        req.Length.Should().Be(9150m);            // Fixed 读 FixedLength 首段数字
        req.InspectionCount.Should().Be(80);      // Quantity → 检验支数
        req.WeightKg.Should().Be(3000m);          // Weight → 检验重量 kg
        req.OuterDiameter.Should().Be(219m);      // Spec "219*8"
        req.WallThickness.Should().Be(8m);
        req.PlantGrade.Should().Be("304");        // 批次牌号
        req.EquipmentName.Should().Be("超声波探伤机");
        req.SpecialState.Should().BeNull();       // 不设特殊制造状态（与结算源一致）
    }

    [Fact]
    public void BuildRequest_非定尺与范围尺_长度按6000兜底()
    {
        // NonFixed：即使记录填了定尺文本，仍按 6000 折算（口径=结算源）
        var nonFixed = NewBatch(lengthStatus: nameof(LengthStatus.NonFixed));
        var insp = NewInspection(fixedLength: "10000mm");
        FinalInspectionMatchRequestMapper.BuildRequest(insp, nonFixed).Length.Should().Be(6000m);

        // Range：同样 6000
        var range = NewBatch(lengthStatus: nameof(LengthStatus.Range));
        FinalInspectionMatchRequestMapper.BuildRequest(insp, range).Length.Should().Be(6000m);
    }

    [Fact]
    public void BuildRequest_批次缺失或规格空_OD壁厚为空_长度兜底()
    {
        // batch 为 null：规格/长度状态/牌号均空
        var noBatch = FinalInspectionMatchRequestMapper.BuildRequest(NewInspection(), null);
        noBatch.LengthStatus.Should().BeNull();
        noBatch.PlantGrade.Should().BeNull();
        noBatch.OuterDiameter.Should().BeNull();
        noBatch.WallThickness.Should().BeNull();
        noBatch.Length.Should().Be(6000m);        // 无状态 → 6000 兜底

        // 批次在但规格空 → OD/WT null，状态 Range → 长度 6000
        var noSpec = NewBatch(spec: null, lengthStatus: nameof(LengthStatus.Range));
        var req = FinalInspectionMatchRequestMapper.BuildRequest(NewInspection(), noSpec);
        req.OuterDiameter.Should().BeNull();
        req.WallThickness.Should().BeNull();
        req.Length.Should().Be(6000m);
    }
}
