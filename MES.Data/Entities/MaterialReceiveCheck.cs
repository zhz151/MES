namespace MES.Data.Entities;

/// <summary>
/// 检验到料 — 所有工序完成后料到成品检验处，标记批次完成
/// </summary>
public class MaterialReceiveCheck : BaseEntity
{
    /// <summary>
    /// 关联生产批次ID
    /// </summary>
    public int ProductionBatchId { get; set; }

    /// <summary>
    /// 到料日期
    /// </summary>
    public DateTime ReceiveDate { get; set; }

    /// <summary>
    /// 到料数量（支数）
    /// </summary>
    public int? ReceivedQuantity { get; set; }

    /// <summary>
    /// 到料重量(kg)
    /// </summary>
    public decimal? ReceivedWeight { get; set; }

    /// <summary>
    /// 班次（白班/中班/夜班）
    /// </summary>
    public string? Shift { get; set; }

    /// <summary>
    /// 确认人
    /// </summary>
    public string? Checker { get; set; }

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
    /// 所属生产批次
    /// </summary>
    public ProductionBatch ProductionBatch { get; set; } = null!;
}
