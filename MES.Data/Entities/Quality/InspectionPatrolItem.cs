namespace MES.Data.Entities.Quality;

/// <summary>
/// 巡检明细 — 一条巡检单下的巡检项（一对多）。
/// 巡检项/巡检结果均为自由文本，支持一张单覆盖多个检查点。
/// </summary>
public class InspectionPatrolItem : BaseEntity
{
    /// <summary>
    /// 所属巡检单ID
    /// </summary>
    public int PatrolId { get; set; }

    /// <summary>
    /// 巡检项
    /// </summary>
    public string ItemName { get; set; } = null!;

    /// <summary>
    /// 巡检结果（自由文本）
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    public int SortOrder { get; set; }

    // ========== 导航属性 ==========

    /// <summary>
    /// 所属巡检单
    /// </summary>
    public InspectionPatrol Patrol { get; set; } = null!;
}
