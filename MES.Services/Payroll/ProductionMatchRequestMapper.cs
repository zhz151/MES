using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Quality;

namespace MES.Services.Payroll;

/// <summary>
/// 产量记录 → 生产计件计价请求 共享映射（2026-09-04 引入，防双通道漂移单源）。
/// 结算采集（<see cref="PieceRateCollector"/> 4 类产量源）与「按产量记录模拟测算」均只经本映射构建请求，
/// 保证试算口径与月结工资完全一致。4 源：生产记录 / 去油酸洗入缸(InTank) / 完工(OutTank) / 过程检验。
/// 统一归一：SectionName → SectionKeys.ToKey、规格 → OD/WT（SpecificationParser）、PlantGrade 空回退批次。
/// 生产记录特有：
///  - 切行(Cut)接线：Length = 本行 FinishedCutLength（正式切割长）、FixedLengthCount = 批次 ItemDetails 去重定尺长度种数
///    （仅定尺批，非定尺不填 → 引擎该维系数 1 天然兜底）；
///  - 光亮接线：Cut 行所属批次交货状态为光亮系(DeliveryState ∈ Bright/BrightUTube/BrightCoiled) → SpecialState=Bright
///    （现行类别仅「成品断切 Cut」配了 Bright ×1.35 档；非 Cut 不喂，避免误伤未来新增档）。
/// </summary>
public static class ProductionMatchRequestMapper
{
    /// <summary>光亮交货状态白名单（与 DeliveryState 枚举一致，OrdinalIgnoreCase）</summary>
    private static readonly HashSet<string> BrightDeliveryStates = new(
        new[] { nameof(DeliveryState.Bright), nameof(DeliveryState.BrightUTube), nameof(DeliveryState.BrightCoiled) },
        StringComparer.OrdinalIgnoreCase);

    /// <summary>内部生产记录（普通报工，无作业阶段）；切行补 Length/FixedLengthCount 并接光亮 SpecialState。</summary>
    public static PieceRateProductionMatchRequest BuildFromProductionRecord(
        ProductionRecord record, ProductionBatch? batch)
    {
        var spec = !string.IsNullOrWhiteSpace(record.ManufacturingSpec)
            ? record.ManufacturingSpec
            : batch?.Specification;
        var sectionKey = SectionKeys.ToKey(record.SectionName) ?? record.SectionName;

        var request = new PieceRateProductionMatchRequest
        {
            SectionName = sectionKey,
            ProcessName = record.ProcessName,
            ProductStatus = record.ProductStatus,
            PlantGrade = !string.IsNullOrWhiteSpace(record.PlantGrade) ? record.PlantGrade : batch?.PlantGrade,
            EquipmentName = record.EquipmentName,
            Remark = record.Remark,
            OuterDiameter = spec == null ? null : SpecificationParser.ParseOuterDiameter(spec),
            WallThickness = spec == null ? null : SpecificationParser.ParseWallThickness(spec)
        };

        // ---- 切行接线（定尺计划口径）：Length = 本行正式切割长；FixedLengthCount = 批 ItemDetails 去重定尺长度种数。
        // 缺值/非定尺批 → 两维不填 → 引擎跳过该维（系数 1），天然兜底 ----
        if (string.Equals(sectionKey, SectionKeys.Cut, StringComparison.OrdinalIgnoreCase))
        {
            request.Length = record.FinishedCutLength;
            if (batch != null
                && string.Equals(batch.LengthStatus, nameof(LengthStatus.Fixed), StringComparison.OrdinalIgnoreCase))
            {
                request.FixedLengthCount = BatchItemDetailsParser.CountDistinctLengthsMm(batch.ItemDetails);
            }
            // ---- 光亮接线：Cut 行所属批次为光亮交货 → SpecialState=Bright（×1.35）----
            if (IsBrightDelivery(batch?.DeliveryState))
                request.SpecialState = PieceRateStateKeys.Bright;
        }

        return request;
    }

    /// <summary>去油/酸洗入缸（生产类别 · Stage=InTank；只用记录自身制造规格，不回退批）。</summary>
    public static PieceRateProductionMatchRequest BuildFromPicklingIn(PicklingInRecord record)
        => new()
        {
            SectionName = SectionKeys.ToKey(record.SectionName) ?? record.SectionName,
            ProcessName = record.ProcessName,
            ProductStatus = record.ProductStatus,
            Stage = PieceRateStageKeys.InTank,
            PlantGrade = record.PlantGrade,
            EquipmentName = record.EquipmentName,
            OuterDiameter = record.ManufacturingSpec == null ? null : SpecificationParser.ParseOuterDiameter(record.ManufacturingSpec),
            WallThickness = record.ManufacturingSpec == null ? null : SpecificationParser.ParseWallThickness(record.ManufacturingSpec)
        };

    /// <summary>去油/酸洗完工（生产类别 · Stage=OutTank；冗余字段自入缸复制冻结）。</summary>
    public static PieceRateProductionMatchRequest BuildFromPicklingOut(PicklingOutRecord record)
        => new()
        {
            SectionName = SectionKeys.ToKey(record.SectionName) ?? record.SectionName,
            ProcessName = record.ProcessName,
            ProductStatus = record.ProductStatus,
            Stage = PieceRateStageKeys.OutTank,
            PlantGrade = record.PlantGrade,
            EquipmentName = record.EquipmentName,
            OuterDiameter = record.ManufacturingSpec == null ? null : SpecificationParser.ParseOuterDiameter(record.ManufacturingSpec),
            WallThickness = record.ManufacturingSpec == null ? null : SpecificationParser.ParseWallThickness(record.ManufacturingSpec)
        };

    /// <summary>过程检验（生产类别 · Inspection 工段，无作业阶段）；牌号/规格空回退批次。</summary>
    public static PieceRateProductionMatchRequest BuildFromProcessInspection(
        ProcessInspection inspection, ProductionBatch? batch)
    {
        var spec = !string.IsNullOrWhiteSpace(inspection.ManufacturingSpec)
            ? inspection.ManufacturingSpec
            : batch?.Specification;
        return new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.ToKey(inspection.SectionName) ?? inspection.SectionName,
            ProcessName = inspection.ProcessName,
            ProductStatus = inspection.ProductStatus,
            PlantGrade = !string.IsNullOrWhiteSpace(inspection.PlantGrade) ? inspection.PlantGrade : batch?.PlantGrade,
            EquipmentName = inspection.EquipmentName,
            OuterDiameter = spec == null ? null : SpecificationParser.ParseOuterDiameter(spec),
            WallThickness = spec == null ? null : SpecificationParser.ParseWallThickness(spec)
        };
    }

    /// <summary>批次交货状态是否为光亮系（Bright/BrightUTube/BrightCoiled，OrdinalIgnoreCase）</summary>
    public static bool IsBrightDelivery(string? deliveryState)
        => !string.IsNullOrWhiteSpace(deliveryState)
           && BrightDeliveryStates.Contains(deliveryState!.Trim());
}
