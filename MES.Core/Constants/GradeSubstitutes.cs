namespace MES.Core.Constants;

/// <summary>
/// 工厂牌号替代映射（高级可替低级）：key=低级, value=高级
/// 用于用料计划库存筛选和批次工厂牌号验证
/// </summary>
public static class GradeSubstitutes
{
    /// <summary>
    /// 牌号替代映射（大小写不敏感）
    /// key=低级牌号, value=可替代的高级牌号
    /// </summary>
    public static readonly Dictionary<string, string> Mapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["30400"] = "304L0",
        ["31600"] = "316L0",
        ["316H0"] = "31600",
        ["34700"] = "347H0",
        ["22051"] = "22052"
    };

    /// <summary>
    /// 验证仓库工厂牌号是否可以替代工单工厂牌号（高代低）
    /// </summary>
    /// <param name="plantGrade">工单工厂牌号（基准）</param>
    /// <param name="sourcePlantGrade">仓库来源工厂牌号</param>
    /// <returns>true=可替代（一致或高代低），false=不可替代</returns>
    public static bool IsSubstitutable(string? plantGrade, string? sourcePlantGrade)
    {
        if (string.IsNullOrWhiteSpace(plantGrade) || string.IsNullOrWhiteSpace(sourcePlantGrade))
            return true; // 任一为空则跳过验证

        if (string.Equals(plantGrade, sourcePlantGrade, StringComparison.OrdinalIgnoreCase))
            return true; // 一致

        // 检查是否高代低：sourcePlantGrade 是否可替代 plantGrade
        if (Mapping.TryGetValue(plantGrade, out var higherGrade) &&
            string.Equals(higherGrade, sourcePlantGrade, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
