using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Materials;

/// <summary>
/// 圆钢穿孔汇总：待穿孔（圆钢缺少量）明细单行（行=工单，按工单号聚合）
/// 数据源=圆棒穿孔计划需求 − 已下委外量（尚未决定穿孔单位，故无委外单号/序号/委外单位列）；
/// 列结构对齐「荒管待购」：仅缺少量，不暴露需求/已下委外；规格只保留穿孔规格
/// </summary>
public class SubcontractPiercingPendingDto
{
    /// <summary>工单号</summary>
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>钢种（工厂牌号，该工单计划多值合并）</summary>
    public string PlantGrade { get; set; } = "";

    /// <summary>穿孔规格（该工单计划多值合并）</summary>
    public string PiercingSpec { get; set; } = "";

    /// <summary>缺少量(kg) = Max(0, 圆棒穿孔计划需求 - 已下委外量)</summary>
    public decimal MissingWeight { get; set; }

    // ========== 工单实时关注（按工单号关联工单执行状况读模型，无记录 null → 前端 "-"） ==========
    /// <summary>工单关注（ScheduleStage：0主号暂停/1主号完成/2原料锁定/3生产执行/4成品检验）</summary>
    public int? ExecutionScheduleStage { get; set; }

    /// <summary>原锁执行备注（RawMaterialLockRemark Key）</summary>
    public string? ExecutionRawMaterialLockRemark { get; set; }

    /// <summary>工单计划性（UrgencyLevel Key）</summary>
    public string? ExecutionUrgencyLevel { get; set; }
}

/// <summary>
/// 圆钢穿孔汇总：在穿孔 单元格（委外单位×加工规格）
/// </summary>
public class SubcontractPiercingInProgressCellDto
{
    /// <summary>在穿孔量(kg) = Max(0, 需求重量 - 净回收重量)</summary>
    public decimal TotalWeight { get; set; }
}

/// <summary>
/// 圆钢穿孔汇总：在穿孔 行（委外单位）
/// </summary>
public class SubcontractPiercingInProgressRowDto
{
    /// <summary>委外单位（"合计"为合计行）</summary>
    public string SupplierName { get; set; } = null!;

    /// <summary>加工规格 → 单元格值</summary>
    public Dictionary<string, SubcontractPiercingInProgressCellDto> Cells { get; set; } = new();

    /// <summary>合计列</summary>
    public SubcontractPiercingInProgressCellDto Total { get; set; } = new();
}

/// <summary>
/// 圆钢穿孔汇总：在穿孔 结果
/// </summary>
public class SubcontractPiercingInProgressResultDto
{
    /// <summary>加工规格动态列（按字典序）</summary>
    public List<string> Specifications { get; set; } = new();

    /// <summary>行（含末行"合计"）</summary>
    public List<SubcontractPiercingInProgressRowDto> Rows { get; set; } = new();
}

/// <summary>
/// 圆钢穿孔汇总：月度 单月值（发/回）
/// </summary>
public class SubcontractPiercingMonthlyValueDto
{
    /// <summary>发(kg) = 该月下单的子项需求重量（按下单日期分月）</summary>
    public decimal SendWeight { get; set; }

    /// <summary>回(kg) = 净回收重量（Max(0, 回收量 - 退货量)）</summary>
    public decimal RecoverWeight { get; set; }
}

/// <summary>
/// 圆钢穿孔汇总：月度 行（委外单位）
/// </summary>
public class SubcontractPiercingMonthlyRowDto
{
    /// <summary>委外单位（"合计"为合计行）</summary>
    public string SupplierName { get; set; } = null!;

    /// <summary>1月~12月（下标 0-11 对应 MonthLabels）</summary>
    public List<SubcontractPiercingMonthlyValueDto> Months { get; set; } = new();

    /// <summary>合计列（12月发/回各自求和）</summary>
    public SubcontractPiercingMonthlyValueDto Total { get; set; } = new();

    /// <summary>现在穿(kg) = 未完成子项的在穿孔量（Max(0, 需求-净回收)），不分加工规格</summary>
    public decimal NowPiercing { get; set; }
}

/// <summary>
/// 圆钢穿孔汇总：月度 结果
/// </summary>
public class SubcontractPiercingMonthlyResultDto
{
    /// <summary>月份标签（yyyy-MM，1月~12月）</summary>
    public List<string> MonthLabels { get; set; } = new();

    /// <summary>行（含末行"合计"）</summary>
    public List<SubcontractPiercingMonthlyRowDto> Rows { get; set; } = new();
}
