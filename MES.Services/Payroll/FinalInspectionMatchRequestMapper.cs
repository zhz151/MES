using MES.Core.DTOs.Payroll;
using MES.Core.Helpers;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Quality;

namespace MES.Services.Payroll;

/// <summary>
/// 成检记录 → 计件计价请求 共享映射（2026-09-04 引入，防双通道漂移单源）。
/// 结算采集（PieceRateCollector 成检源）与「按成检记录模拟测算」均只经本映射构建请求，
/// 保证试算口径与月结工资完全一致：Spec→OD/WT、LengthStatus+FixedLength→Length
/// （Range/NonFixed 按 6000 兜底）、Quantity→InspectionCount、Weight→WeightKg、
/// PlantGrade=批次牌号、EquipmentName=记录设备号。不设 SpecialState（成检记录无此取数源，
/// 结算本就不带该维，两处一致）。
/// </summary>
public static class FinalInspectionMatchRequestMapper
{
    /// <summary>由一条成检记录（含其所属批次）构建计价请求。batch 可空时规格/长度状态/牌号相关字段按空处理。</summary>
    public static PieceRateFinalInspectionMatchRequest BuildRequest(FinalInspection inspection, ProductionBatch? batch)
    {
        var spec = batch?.Specification;
        return new PieceRateFinalInspectionMatchRequest
        {
            ItemKey = inspection.InspectionItem.ToString(),
            LengthStatus = batch?.LengthStatus,
            Length = PieceRateAmountHelper.ResolveLengthMm(inspection.FixedLength, batch?.LengthStatus),
            InspectionCount = inspection.Quantity,
            WeightKg = inspection.Weight,
            OuterDiameter = spec == null ? null : SpecificationParser.ParseOuterDiameter(spec),
            WallThickness = spec == null ? null : SpecificationParser.ParseWallThickness(spec),
            PlantGrade = batch?.PlantGrade,
            EquipmentName = inspection.EquipmentName
        };
    }
}
