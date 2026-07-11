namespace MES.Data.Entities.Batch;

/// <summary>
/// 委外回收 — 委外加工完成后回收记录
/// </summary>
public class OutsourceRecovery : BaseEntity
{
    /// <summary>
    /// 关联工段委外ID
    /// </summary>
    public int SectionOutsourceId { get; set; }

    /// <summary>
    /// 回收日期
    /// </summary>
    public DateTime RecoveryDate { get; set; }

    /// <summary>
    /// 回收数量（支数）
    /// </summary>
    public int? RecoveryQuantity { get; set; }

    /// <summary>
    /// 回收重量(kg)
    /// </summary>
    public decimal? RecoveryWeight { get; set; }

    /// <summary>
    /// 未加工支数
    /// </summary>
    public int? UnprocessedQuantity { get; set; }

    /// <summary>
    /// 未加工重量(kg)
    /// </summary>
    public decimal? UnprocessedWeight { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 数据来源（SCAN=扫码报工，MANUAL=手动录入），默认 MANUAL
    /// </summary>
    public string? DataSource { get; set; }

    // ========== 导航属性 ==========

    /// <summary>
    /// 所属工段委外记录
    /// </summary>
    public SectionOutsource SectionOutsource { get; set; } = null!;
}
