using FluentAssertions;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Quality;
using MES.Services.Payroll;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// 产量记录 → 生产计件计价请求 共享映射 ProductionMatchRequestMapper 纯函数测试（2026-09-04 引入）。
/// 结算采集（PieceRateCollector 生产源 4 类）与「按产量记录模拟测算」共用本映射（防双通道漂移）；
/// 此处逐字段断言 4 源归一（Section→Key、Spec→OD/WT、PlantGrade 空回退批、Stage 接线）、
/// 生产记录切行 Cut 接线（Length=FinishedCutLength、FixedLengthCount=批 ItemDetails 去重定尺种数、
/// 光亮批 DeliveryState 光亮系 → SpecialState=Bright，仅 Cut 喂）及 Length 三级取数接线
/// （非切行：Fixed → ItemDetails 最长定尺长、多定尺取最长；Range/NonFixed/无定尺 → 6000 兜底，2026-09-04 拍板）。
/// </summary>
public class ProductionMatchRequestMapperTests
{
    private static ProductionBatch NewBatch(string? spec = "219*8",
        string? lengthStatus = nameof(LengthStatus.Fixed), string? deliveryState = null,
        string? plantGrade = "304", string? itemDetails = null)
        => new()
        {
            // 实体 NRT 字段测试允许传 null（模拟规格缺失/无批次/非光亮）
            Specification = spec!,
            LengthStatus = lengthStatus!,
            DeliveryState = deliveryState!,
            PlantGrade = plantGrade!,
            ItemDetails = itemDetails,
            BatchNo = "BATCH-CUT"
        };

    private static ProductionRecord NewProductionRecord(string? sectionName = SectionKeys.Cut,
        string? manufacturingSpec = null, string? plantGrade = null, string? processName = "CutProcess",
        decimal? finishedCutLength = null, decimal? weight = 1000, int? quantity = 50,
        string? remark = null, string? equipmentName = "切割机",
        decimal? cuttingMultiple = null, int? faceCutCount = null)
        => new()
        {
            SectionName = sectionName!,
            ProcessName = processName!,
            ManufacturingSpec = manufacturingSpec,
            PlantGrade = plantGrade,
            FinishedCutLength = finishedCutLength,
            Weight = weight,
            Quantity = quantity,
            Remark = remark,
            EquipmentName = equipmentName,
            CuttingMultiple = cuttingMultiple,
            FaceCutCount = faceCutCount
        };

    [Fact]
    public void BuildFromProductionRecord_非Cut行_批次规格牌号回退_FixedLengthCount与SpecialState不喂()
    {
        var batch = NewBatch(spec: "219*8", lengthStatus: nameof(LengthStatus.Fixed), deliveryState: nameof(DeliveryState.Bright));
        // 普通行（非 Cut）：批次 Fixed 但 ItemDetails 缺省 → Length 三级取数第 3 级 6000 兜底；
        // FixedLengthCount/SpecialState 仍仅 Cut 行接线，非 Cut 不喂
        var rec = NewProductionRecord(
            sectionName: SectionKeys.Pickle,
            manufacturingSpec: null, plantGrade: null, remark: "冷拔减壁", finishedCutLength: 9150);
        var req = ProductionMatchRequestMapper.BuildFromProductionRecord(rec, batch);

        req.SectionName.Should().Be(SectionKeys.Pickle);
        req.ProcessName.Should().Be("CutProcess");
        req.Stage.Should().BeNull();
        req.OuterDiameter.Should().Be(219m);          // spec 空回退批 Specification "219*8"
        req.WallThickness.Should().Be(8m);
        req.PlantGrade.Should().Be("304");            // 记录牌号空回退批牌号
        req.Remark.Should().Be("冷拔减壁");
        req.EquipmentName.Should().Be("切割机");
        req.Length.Should().Be(6000m);                // 非 Cut 三级取数：批次 Fixed 但无定尺明细 → 6000 兜底
        req.FixedLengthCount.Should().BeNull();
        req.SpecialState.Should().BeNull();
        req.CutRate.Should().Be(1m);                  // 断切率 = 断切倍数（空默认 1），所有生产记录统一喂
    }

    [Fact]
    public void BuildFromProductionRecord_非Cut行_三级取数_批次Fixed单一定尺按批()
    {
        var batch = NewBatch(spec: "219*8", lengthStatus: nameof(LengthStatus.Fixed), itemDetails: "5,9150mm,30支;");
        var rec = NewProductionRecord(sectionName: SectionKeys.Pickle, manufacturingSpec: null);
        var req = ProductionMatchRequestMapper.BuildFromProductionRecord(rec, batch);

        req.Length.Should().Be(9150m);                // 第 2 级：Fixed 批 ItemDetails 定尺长
        req.FixedLengthCount.Should().BeNull();       // 非 Cut 不统计定尺种数
        req.SpecialState.Should().BeNull();
    }

    [Fact]
    public void BuildFromProductionRecord_非Cut行_三级取数_多定尺批取最长()
    {
        var batch = NewBatch(spec: "219*8", lengthStatus: nameof(LengthStatus.Fixed), itemDetails: "5,6000mm,30支;5,9150mm,20支;");
        var rec = NewProductionRecord(sectionName: SectionKeys.Pickle, manufacturingSpec: null);
        var req = ProductionMatchRequestMapper.BuildFromProductionRecord(rec, batch);

        req.Length.Should().Be(9150m);                // 多定尺 {6000,9150} → 取最长 9150（拍板）
    }

    [Fact]
    public void BuildFromProductionRecord_非Cut行_三级取数_批次Range按6000()
    {
        var batch = NewBatch(spec: "219*8", lengthStatus: nameof(LengthStatus.Range), itemDetails: "5,9150mm,30支;");
        var rec = NewProductionRecord(sectionName: SectionKeys.Pickle, manufacturingSpec: null);
        var req = ProductionMatchRequestMapper.BuildFromProductionRecord(rec, batch);

        req.Length.Should().Be(6000m);                // 第 3 级：Range 批 → 6000（即使 ItemDetails 有值）
    }

    [Fact]
    public void BuildFromProductionRecord_断切率接线_如实按断切倍数()
    {
        // 油管断切记录断切倍数如实落 CutRate 档（空默认 1 → 1 档打折，见 PieceRateCollector 月结口径）
        var req = ProductionMatchRequestMapper.BuildFromProductionRecord(
            NewProductionRecord(sectionName: SectionKeys.OilPipeCut, cuttingMultiple: 3m), null);
        req.SectionName.Should().Be(SectionKeys.OilPipeCut);
        req.CutRate.Should().Be(3m);                  // 如实喂入 → 引擎落 CutRate 3 档

        var blank = ProductionMatchRequestMapper.BuildFromProductionRecord(
            NewProductionRecord(sectionName: SectionKeys.OilPipeCut, cuttingMultiple: null), null);
        blank.CutRate.Should().Be(1m);                // 空 → 默认 1 → 落 CutRate 1 档
    }

    [Fact]
    public void BuildFromProductionRecord_Cut行定尺批_接Length与FixedLengthCount与光亮()
    {
        var batch = NewBatch(
            spec: "219*8",
            lengthStatus: nameof(LengthStatus.Fixed),
            deliveryState: nameof(DeliveryState.Bright),
            itemDetails: "5,9150mm,30支;5,6000mm,20支;");
        var rec = NewProductionRecord(
            sectionName: SectionKeys.Cut,
            manufacturingSpec: "219*8", plantGrade: "304",
            finishedCutLength: 9150);
        var req = ProductionMatchRequestMapper.BuildFromProductionRecord(rec, batch);

        req.SectionName.Should().Be(SectionKeys.Cut);
        req.Length.Should().Be(9150m);                // 本行正式切割长
        req.FixedLengthCount.Should().Be(2);          // ItemDetails 去重定尺种数 {9150, 6000}
        req.SpecialState.Should().Be(PieceRateStateKeys.Bright); // 光亮批 → Bright
        req.OuterDiameter.Should().Be(219m);
        req.WallThickness.Should().Be(8m);
    }

    [Fact]
    public void BuildFromProductionRecord_Cut行非光亮批_SpecialState空()
    {
        var plain = NewBatch(lengthStatus: nameof(LengthStatus.Fixed), deliveryState: null);
        ProductionMatchRequestMapper.BuildFromProductionRecord(
            NewProductionRecord(sectionName: SectionKeys.Cut, manufacturingSpec: "219*8"), plain)
            .SpecialState.Should().BeNull();
    }

    [Fact]
    public void BuildFromProductionRecord_Cut行非Fixed批_FixedLengthCount空_光亮UTube仍接线()
    {
        var range = NewBatch(lengthStatus: nameof(LengthStatus.Range), deliveryState: nameof(DeliveryState.Bright), itemDetails: "5,9150mm,30支;");
        var req = ProductionMatchRequestMapper.BuildFromProductionRecord(
            NewProductionRecord(sectionName: SectionKeys.Cut, manufacturingSpec: "219*8"), range);
        req.FixedLengthCount.Should().BeNull();       // 仅 Fixed 批统计定尺种数
        req.SpecialState.Should().Be(PieceRateStateKeys.Bright); // 光亮系（UTube）仍接

        var utube = NewBatch(lengthStatus: nameof(LengthStatus.Fixed), deliveryState: nameof(DeliveryState.BrightUTube));
        ProductionMatchRequestMapper.BuildFromProductionRecord(
            NewProductionRecord(sectionName: SectionKeys.Cut, manufacturingSpec: "219*8"), utube)
            .SpecialState.Should().Be(PieceRateStateKeys.Bright);
    }

    [Fact]
    public void BuildFromPicklingIn_入缸_StageInTank_仅记录规格_Length三级取数()
    {
        var batch = NewBatch(spec: "219*8", lengthStatus: nameof(LengthStatus.Fixed), itemDetails: "5,12000mm,30支;");
        var rec = new PicklingInRecord
        {
            SectionName = "酸洗",          // 中文工段 → 归一键 Pickle
            ProcessName = "Degrease",
            ProductStatus = ProductStatuses.RoughTube,
            ManufacturingSpec = "60*3",
            PlantGrade = "304",
            EquipmentName = "酸洗槽1",
            Remark = "入缸"
        };
        var req = ProductionMatchRequestMapper.BuildFromPicklingIn(rec, batch);

        req.SectionName.Should().Be(SectionKeys.Pickle);
        req.ProcessName.Should().Be("Degrease");
        req.ProductStatus.Should().Be(ProductStatuses.RoughTube);
        req.Stage.Should().Be(PieceRateStageKeys.InTank); // 入缸端独立计酬
        req.OuterDiameter.Should().Be(60m);               // 规格仍仅取记录自身 "60*3"，不回退批 "219*8"
        req.WallThickness.Should().Be(3m);
        req.PlantGrade.Should().Be("304");
        req.Length.Should().Be(12000m);                   // Length 三级取数第 2 级：批次 Fixed 定尺长
        req.SpecialState.Should().BeNull();
    }

    [Fact]
    public void BuildFromPicklingIn_入缸_批次Range或缺失_Length按6000兜底()
    {
        var range = NewBatch(spec: "219*8", lengthStatus: nameof(LengthStatus.Range), itemDetails: "5,12000mm,30支;");
        var rec = new PicklingInRecord
        {
            SectionName = "酸洗",
            ManufacturingSpec = "60*3",
            ProductStatus = ProductStatuses.RoughTube
        };
        ProductionMatchRequestMapper.BuildFromPicklingIn(rec, range).Length.Should().Be(6000m);
        ProductionMatchRequestMapper.BuildFromPicklingIn(rec, null).Length.Should().Be(6000m); // 批次缺失 → 兜底
    }

    [Fact]
    public void BuildFromPicklingOut_完工_StageOutTank_冗余字段_Length三级取数()
    {
        var batch = NewBatch(spec: "219*8", lengthStatus: nameof(LengthStatus.Fixed), itemDetails: "5,9150mm,30支;");
        var rec = new PicklingOutRecord
        {
            SectionName = "酸洗",
            ProcessName = "Pickle",
            ProductStatus = ProductStatuses.RoughTube,
            ManufacturingSpec = "60*3",
            PlantGrade = "304",
            EquipmentName = "酸洗槽1",
            BatchNo = "BATCH-X"
        };
        var req = ProductionMatchRequestMapper.BuildFromPicklingOut(rec, batch);

        req.SectionName.Should().Be(SectionKeys.Pickle);
        req.Stage.Should().Be(PieceRateStageKeys.OutTank); // 出缸端独立计酬
        req.OuterDiameter.Should().Be(60m);
        req.WallThickness.Should().Be(3m);
        req.Length.Should().Be(9150m);                    // Length 三级取数：经入缸所属批 Fixed 定尺长
    }

    [Fact]
    public void BuildFromProcessInspection_过程检验_无阶段_规格牌号空回退批()
    {
        var batch = NewBatch(spec: "100*5", plantGrade: "316");
        var insp = new ProcessInspection
        {
            SectionName = "酸洗",
            ProcessName = "ProcessCheck",
            ProductStatus = ProductStatuses.InProgress,
            ManufacturingSpec = null,   // spec 空 → 回退批
            PlantGrade = null,          // 牌号空 → 回退批
            EquipmentName = "检验台"
        };
        var req = ProductionMatchRequestMapper.BuildFromProcessInspection(insp, batch);

        req.SectionName.Should().Be(SectionKeys.Pickle);
        req.Stage.Should().BeNull();                  // 过程检验无作业阶段
        req.OuterDiameter.Should().Be(100m);          // 批次规格回退
        req.WallThickness.Should().Be(5m);
        req.PlantGrade.Should().Be("316");            // 批次牌号回退
        req.SpecialState.Should().BeNull();

        // 批次为 null → OD/WT/牌号均空
        var noBatch = ProductionMatchRequestMapper.BuildFromProcessInspection(insp, null);
        noBatch.OuterDiameter.Should().BeNull();
        noBatch.WallThickness.Should().BeNull();
        noBatch.PlantGrade.Should().BeNull();
    }
}
