namespace MES.Core.Constants;

/// <summary>
/// 生产计件类别「约束集合」成员表的 ConstraintType 取值（2026-09-02 约束集合实体化引入）。
/// 主表以关系成员表 PieceRateProductionCategoryKeys 表达工序/产类/作业阶段三约束：
/// 无成员行 = 该维全选；有行 = 仅限这些英文 Key（OrdinalIgnoreCase 匹配）。
/// </summary>
public static class PieceRateConstraintTypes
{
    /// <summary>工序约束（Key ∈ ProcessDefinition 工序域）</summary>
    public const string Process = "Process";

    /// <summary>产类约束（Key ∈ ProductStatuses 产类域）</summary>
    public const string ProductStatus = "ProductStatus";

    /// <summary>作业阶段约束（Key ∈ PieceRateStageKeys 阶段域；不含「无阶段」——空成员即全域含普通报工）</summary>
    public const string Stage = "Stage";

    /// <summary>全部合法 ConstraintType 值</summary>
    public static readonly string[] All = [Process, ProductStatus, Stage];

    /// <summary>是否为合法 ConstraintType</summary>
    public static bool IsKey(string? value)
        => !string.IsNullOrEmpty(value) && All.Contains(value, StringComparer.Ordinal);
}
