namespace MES.Data.Entities;

/// <summary>
/// 去油/酸洗完工记录 — 出缸+冲洗完成后登记，关联入缸记录（PicklingInRecord）
/// </summary>
public class PicklingOutRecord : BaseEntity
{
    /// <summary>
    /// 关联入缸记录ID
    /// </summary>
    public int PicklingInRecordId { get; set; }

    /// <summary>
    /// 完工日期（出缸+冲洗日期）
    /// </summary>
    public DateTime CompleteDate { get; set; }

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
    /// 所属入缸记录
    /// </summary>
    public PicklingInRecord PicklingInRecord { get; set; } = null!;
}
