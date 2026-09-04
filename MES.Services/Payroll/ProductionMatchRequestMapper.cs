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
///  - 断切率接线（2026-09-04）：CutRate = 生产记录「断切倍数」CuttingMultiple，空默认 1（OilPipeCut 断切档）；
///  - 切行(Cut)接线：Length = 本行 FinishedCutLength（正式切割长）、FixedLengthCount = 批次 ItemDetails 去重定尺长度种数
///    （仅定尺批，非定尺不填 → 引擎该维系数 1 天然兜底）；
///  - 光亮接线：Cut 行所属批次交货状态为光亮系(DeliveryState ∈ Bright/BrightUTube/BrightCoiled) → SpecialState=Bright
///    （现行类别仅「成品断切 Cut」配了 Bright ×1.35 档；非 Cut 不喂，避免误伤未来新增档）。
///  - Length 三级取数接线（2026-09-04 拍板）：第 1 级 = Cut 行本行 FinishedCutLength（正式切割长）；非切行
///    第 2 级 = 所属批次 LengthStatus=Fixed 时 ItemDetails 最长定尺长(mm，多定尺批取最长一种)、第 3 级 =
///    批次 Range/NonFixed 或定尺信息缺失 → 6000 兜底（FallbackLengthMm，与成检 ResolveLengthMm 同口径）。
///    补线后冷拔/去油/酸洗/矫直等配 Length 档的类别不再恒按系数 1 结算。
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
            WallThickness = spec == null ? null : SpecificationParser.ParseWallThickness(spec),
            // 断切率维 = 生产记录「断切倍数」；空默认 1（仅命中类别配有 CutRate 档时才参与连乘，无档零影响）
            CutRate = record.CuttingMultiple ?? 1m
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
        // ---- 非切行 Length 三级取数：第2级 = 批次 Fixed → ItemDetails 最长定尺长(mm，多定尺取最长)；第3级 = Range/NonFixed/无定尺 → 6000 兜底 ----
        else
        {
            request.Length = ResolveBatchLengthMm(batch);
        }

        return request;
    }

    /// <summary>去油/酸洗入缸（生产类别 · Stage=InTank；只用记录自身制造规格，不回退批）。Length 三级取数同非切行口径。</summary>
    public static PieceRateProductionMatchRequest BuildFromPicklingIn(PicklingInRecord record, ProductionBatch? batch)
        => new()
        {
            SectionName = SectionKeys.ToKey(record.SectionName) ?? record.SectionName,
            ProcessName = record.ProcessName,
            ProductStatus = record.ProductStatus,
            Stage = PieceRateStageKeys.InTank,
            PlantGrade = record.PlantGrade,
            EquipmentName = record.EquipmentName,
            OuterDiameter = record.ManufacturingSpec == null ? null : SpecificationParser.ParseOuterDiameter(record.ManufacturingSpec),
            WallThickness = record.ManufacturingSpec == null ? null : SpecificationParser.ParseWallThickness(record.ManufacturingSpec),
            Length = ResolveBatchLengthMm(batch)
        };

    /// <summary>去油/酸洗完工（生产类别 · Stage=OutTank；冗余字段自入缸复制冻结）。Length 三级取数同非切行口径。</summary>
    public static PieceRateProductionMatchRequest BuildFromPicklingOut(PicklingOutRecord record, ProductionBatch? batch)
        => new()
        {
            SectionName = SectionKeys.ToKey(record.SectionName) ?? record.SectionName,
            ProcessName = record.ProcessName,
            ProductStatus = record.ProductStatus,
            Stage = PieceRateStageKeys.OutTank,
            PlantGrade = record.PlantGrade,
            EquipmentName = record.EquipmentName,
            OuterDiameter = record.ManufacturingSpec == null ? null : SpecificationParser.ParseOuterDiameter(record.ManufacturingSpec),
            WallThickness = record.ManufacturingSpec == null ? null : SpecificationParser.ParseWallThickness(record.ManufacturingSpec),
            Length = ResolveBatchLengthMm(batch)
        };

    /// <summary>过程检验（生产类别 · Inspection 工段，无作业阶段）；牌号/规格空回退批次。</summary>
    public static PieceRateProductionMatchRequest BuildFromProcessInspection(
        ProcessInspection inspection, ProductionBatch? batch)
    {
        var spec = !string.IsNullOrWhiteSpace(inspection.ManufacturingSpec)
            ? inspection.ManufacturingSpec
            : batch?.Specification;
        var sectionKey = SectionKeys.ToKey(inspection.SectionName) ?? inspection.SectionName;
        return new PieceRateProductionMatchRequest
        {
            SectionName = sectionKey,
            ProcessName = inspection.ProcessName,
            ProductStatus = inspection.ProductStatus,
            PlantGrade = !string.IsNullOrWhiteSpace(inspection.PlantGrade) ? inspection.PlantGrade : batch?.PlantGrade,
            EquipmentName = inspection.EquipmentName,
            OuterDiameter = spec == null ? null : SpecificationParser.ParseOuterDiameter(spec),
            WallThickness = spec == null ? null : SpecificationParser.ParseWallThickness(spec),
            // Length 三级取数；过程检验落在 Cut 工段不喂（Cut 仅认成品切割行 FinishedCutLength，过程检非切割动作）
            Length = string.Equals(sectionKey, SectionKeys.Cut, StringComparison.OrdinalIgnoreCase)
                ? null
                : ResolveBatchLengthMm(batch)
        };
    }

    /// <summary>Length 三级取数第 2/3 级：批次 LengthStatus=Fixed → ItemDetails 最长定尺长(mm，多定尺取最长)；
    /// Range/NonFixed、定尺信息缺失或批次为 null → 6000 兜底（FallbackLengthMm，与成检 ResolveLengthMm 同口径）。</summary>
    private static decimal ResolveBatchLengthMm(ProductionBatch? batch)
    {
        if (batch != null
            && string.Equals(batch.LengthStatus, nameof(LengthStatus.Fixed), StringComparison.OrdinalIgnoreCase))
        {
            var length = BatchItemDetailsParser.MaxLengthMm(batch.ItemDetails);
            if (length.HasValue) return length.Value;
        }
        return PieceRateAmountHelper.FallbackLengthMm;
    }

    /// <summary>批次交货状态是否为光亮系（Bright/BrightUTube/BrightCoiled，OrdinalIgnoreCase）</summary>
    public static bool IsBrightDelivery(string? deliveryState)
        => !string.IsNullOrWhiteSpace(deliveryState)
           && BrightDeliveryStates.Contains(deliveryState!.Trim());
}
