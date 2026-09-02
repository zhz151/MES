namespace MES.Data.Entities.Payroll;

/// <summary>
/// 生产计件类别「约束集合」成员行（子表，2026-09-02 约束集合实体化引入，替代主表三 JSON 数组列）。
/// 类别 = 工段(主表必选单选) × 工序 × 产类 × 作业阶段，其中三约束各由若干成员行表达：
/// 某 ConstraintType 无成员行 = 该维全选（0 行=全选，禁止显式全列表与空并存）；有行 = 仅限这些英文 Key。
/// 与 PieceRateProductionCategoryTier 同为类别级联子表；每类每 ConstraintType 下 Key 不重复（UK 兜底）。
/// </summary>
public class PieceRateProductionCategoryKey : BaseEntity
{
    /// <summary>所属类别（级联删除）</summary>
    public int CategoryId { get; set; }

    /// <summary>所属类别导航</summary>
    public PieceRateProductionCategory? Category { get; set; }

    /// <summary>约束类型英文 Key（PieceRateConstraintTypes：Process/ProductStatus/Stage）</summary>
    public string ConstraintType { get; set; } = string.Empty;

    /// <summary>约束成员英文 Key（工序/产类/作业阶段 Key，OrdinalIgnoreCase 匹配）</summary>
    public string Key { get; set; } = string.Empty;
}
