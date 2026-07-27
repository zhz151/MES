using MES.Core.Enums;
using MES.Data.Entities.Batch;

namespace MES.Data.Entities.Quality;

/// <summary>
/// 成检到料 — 所有工序完成后料到成品检验处，标记批次进入成检阶段
/// </summary>
public class MaterialReceiveCheck : BaseEntity
{
    // ========== 核心数据 ==========

    /// <summary>
    /// 关联生产批次ID（UK唯一）
    /// </summary>
    public int ProductionBatchId { get; set; }

    /// <summary>
    /// 到料日期
    /// </summary>
    public DateTime ReceiveDate { get; set; }

    /// <summary>
    /// 班次（白班/中班/夜班）
    /// </summary>
    public ShiftType? Shift { get; set; }

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

    // ========== 批次冗余字段 ==========

    /// <summary>生产编号</summary>
    public string? BatchNo { get; set; }

    // ========== 工序关联（工艺卡位置跟踪） ==========

    /// <summary>
    /// 所属工序组ID（ManufacturingSpec 匹配成品规格的工序组）
    /// </summary>
    public int ProcessGroupId { get; set; }

    /// <summary>
    /// 工序名称（冗余自 ProcessGroup.ProcessName）
    /// </summary>
    public string ProcessName { get; set; } = "检验";

    /// <summary>
    /// 执行序号（冗余自 ProcessGroup.SequenceNumber）
    /// </summary>
    public int SequenceNumber { get; set; }

    // ========== 状态控制 ==========

    /// <summary>
    /// 强制完成（人控开关）
    /// </summary>
    public bool IsForceCompleted { get; set; }

    // ========== 导航属性 ==========

    /// <summary>
    /// 所属生产批次
    /// </summary>
    public ProductionBatch ProductionBatch { get; set; } = null!;

    /// <summary>
    /// 所属工序组
    /// </summary>
    public ProcessGroup ProcessGroup { get; set; } = null!;
}
