namespace MES.Core.Constants;

/// <summary>
/// 计件类别页专用导入器的模板/导入类型（2026-09-02）。
/// Category = 类别基础定义（定位命中类别→更新主属性+三约束，绝不清档）；Tier = 维度档系数（定位命中类别→整组替换该类别 Tiers）。
/// 定位键 = 工段 × 三约束集合归一组（空=该维全选）。
/// </summary>
public static class PieceRateImportKinds
{
    /// <summary>类别定义模板/导入（只动类别主属性 + 三约束成员，不动档行）</summary>
    public const string Category = "category";

    /// <summary>维度档系数模板/导入（整组替换定位类别的 Tiers）</summary>
    public const string Tier = "tier";

    /// <summary>合法类型（OrdinalIgnoreCase 判定）</summary>
    public static readonly string[] All = [Category, Tier];

    private static readonly HashSet<string> KeySet = new(All, StringComparer.Ordinal);

    /// <summary>是否为合法导入类型</summary>
    public static bool IsValid(string? kind)
        => !string.IsNullOrEmpty(kind) && KeySet.Contains(kind!);
}
