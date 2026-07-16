using MES.Core.Enums;

namespace MES.Services;

/// <summary>
/// 钢管单重计算工具
/// 从规格 + 公差 + 长度状态计算理论单支重量（kg）
/// </summary>
public static class PipeWeightCalculator
{
    /// <summary>
    /// 计算理论单支重量
    /// </summary>
    /// <param name="specification">规格字符串（如 "219*8"）</param>
    /// <param name="outerDiameterNegative">外径负公差</param>
    /// <param name="outerDiameterPositive">外径正公差</param>
    /// <param name="wallThicknessNegative">壁厚负公差</param>
    /// <param name="wallThicknessPositive">壁厚正公差</param>
    /// <param name="lengthStatus">长度状态</param>
    /// <param name="maxLength">最大长度（mm），定尺时使用</param>
    /// <returns>单支重量（kg），保留3位小数；无法计算时返回 null</returns>
    public static decimal? CalculateUnitWeight(
        string specification,
        decimal outerDiameterNegative,
        decimal outerDiameterPositive,
        decimal wallThicknessNegative,
        decimal wallThicknessPositive,
        LengthStatus lengthStatus,
        decimal? maxLength)
    {
        if (string.IsNullOrEmpty(specification)) return null;

        var nominalOd = SpecificationParser.ParseOuterDiameter(specification);
        var nominalWt = SpecificationParser.ParseWallThickness(specification);
        if (nominalOd == null || nominalWt == null || nominalOd <= 0 || nominalWt <= 0) return null;

        var odActual = nominalOd.Value - 0.5m * outerDiameterNegative + 0.5m * outerDiameterPositive;
        var wtActual = nominalWt.Value - 0.5m * wallThicknessNegative + 0.5m * wallThicknessPositive;

        if (odActual <= 0 || wtActual <= 0) return null;

        var weightPerMeter = (odActual - wtActual) * wtActual * 0.02466m;
        var maxLengthMm = lengthStatus == LengthStatus.Fixed
            ? maxLength ?? 4500m
            : 4500m;

        return Math.Round(weightPerMeter * maxLengthMm / 1000m, 3);
    }
}
