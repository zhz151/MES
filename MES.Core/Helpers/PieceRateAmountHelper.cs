using MES.Core.Constants;
using MES.Core.Enums;

namespace MES.Core.Helpers;

/// <summary>
/// 计件工资「结算单价 → 整行金额」折算共享单源（2026-09-04 试算/结算共用引入）。
/// 口径说明：工资 = 单价 × 数量，数量维由结算单位 PieceRateUnitKeys 决定——元/吨→重量(kg/1000)、
/// 元/千米→长度(支×mm/1e6=km)、元/支→支数、元/头→头数（无类别用，返回 null）。
/// 原为 PieceRateCollector.AmountForUnit 等私有实现，抽为 Core 共享防试算端点与结算采集双通道漂移；
/// 采集器改调本助手（行为零变化，回归测试兜底）。不四舍五入，累加保留精度，显示层 ToString("G29")。
/// </summary>
public static class PieceRateAmountHelper
{
    /// <summary>范围尺/非定尺/解析失败 长度折算兜底（mm，业务规约 2026-09-03：6000 = 6m 常规管长）</summary>
    public const decimal FallbackLengthMm = 6000m;

    /// <summary>取文本首个数字段（仅数字字符，兼容 "9150mm" / "6000" / "11036 mm"）</summary>
    public static decimal? TryParseFirstNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var num = string.Concat(text.Where(char.IsDigit).Take(12));
        return decimal.TryParse(num, out var v) ? v : null;
    }

    /// <summary>成检单支长（mm）：定尺读 FixedLength 文本首段数字，范围尺/非定尺/解析失败按 6000 兜底</summary>
    public static decimal? ResolveLengthMm(string? fixedLength, string? lengthStatus)
    {
        if (string.Equals(lengthStatus, nameof(LengthStatus.Fixed), StringComparison.OrdinalIgnoreCase))
        {
            var mm = TryParseFirstNumber(fixedLength);
            if (mm.HasValue) return mm.Value;
        }
        return FallbackLengthMm;
    }

    /// <summary>试算长度缺省值（试算端点/试算前端共用单源）：长度状态为范围尺/非定尺（中英皆可）→ 6000mm；其余（Fixed/空）→ null。</summary>
    public static decimal? DefaultTrialLengthMm(string? lengthStatus)
    {
        if (string.IsNullOrWhiteSpace(lengthStatus)) return null;
        var s = lengthStatus.Trim();
        var parsed = EnumHelper.TryParse<LengthStatus>(s)
            ?? (Enum.TryParse(s, ignoreCase: true, out LengthStatus e) ? e : (LengthStatus?)null);
        if (parsed == LengthStatus.Range || parsed == LengthStatus.NonFixed) return FallbackLengthMm;
        return null;
    }

    /// <summary>结算单价 → 行总金额（不四舍五入，累加保留精度，显示层 G29）。缺数量/长度返回 null。</summary>
    public static decimal? AmountForUnit(string unit, decimal unitPrice,
        decimal? weightKg, int? quantity, decimal? lengthMm)
    {
        return PieceRateUnitKeys.GetQuantityDimension(unit) switch
        {
            PieceRateUnitKeys.QuantityDimension.Weight =>
                weightKg.HasValue ? weightKg.Value / 1000m * unitPrice : null,          // 元/吨：kg/1000 × 价
            PieceRateUnitKeys.QuantityDimension.Meters =>
                quantity.HasValue && lengthMm.HasValue
                    ? quantity.Value * lengthMm.Value / 1_000_000m * unitPrice : null,  // 元/千米：支×mm/1e6 = km × 价
            PieceRateUnitKeys.QuantityDimension.PieceCount =>
                quantity.HasValue ? quantity.Value * unitPrice : null,                   // 元/支：支数 × 价
            _ => null                                                                     // 元/头 无类别用
        };
    }
}
