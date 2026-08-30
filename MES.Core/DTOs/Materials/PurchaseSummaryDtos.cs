using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Materials;

/// <summary>
/// 采购汇总：待购（荒管/成品）单行
/// </summary>
public class PurchasePendingDto
{
    /// <summary>工单号</summary>
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>物料分类（MaterialType 枚举名）</summary>
    public MaterialType? MaterialCategory { get; set; }

    /// <summary>厂内钢种（组内多值合并）</summary>
    public string PlantGrade { get; set; } = "";

    /// <summary>规格（组内多值合并）</summary>
    public string Specification { get; set; } = "";

    /// <summary>待购量(kg) = Max(0, 计划总量 - 已采购量)</summary>
    public decimal PendingWeight { get; set; }

    // ========== 工单实时关注（按工单号关联工单执行状况读模型，无记录 null） ==========

    /// <summary>工单关注（ScheduleStage：0主号暂停/1主号完成/2原料锁定/3生产执行/4成品检验）</summary>
    public int? ExecutionScheduleStage { get; set; }

    /// <summary>原锁执行备注（RawMaterialLockRemark Key）</summary>
    public string? ExecutionRawMaterialLockRemark { get; set; }

    /// <summary>工单计划性（UrgencyLevel Key）</summary>
    public string? ExecutionUrgencyLevel { get; set; }
}

/// <summary>
/// 采购汇总：在购（荒管/成品）单元格（供应商×厂内钢种）
/// </summary>
public class PurchaseInProgressCellDto
{
    /// <summary>在购总量(kg) = 采购重量 + 退货量 - 已到货量</summary>
    public decimal TotalWeight { get; set; }

    /// <summary>急量(kg) = 计划性 A+急/A急 的在购总量</summary>
    public decimal UrgentWeight { get; set; }
}

/// <summary>
/// 采购汇总：在购（荒管/成品）行（供应商）
/// </summary>
public class PurchaseInProgressRowDto
{
    /// <summary>供应商名称（"合计"为合计行）</summary>
    public string SupplierName { get; set; } = null!;

    /// <summary>厂内钢种 → 单元格值</summary>
    public Dictionary<string, PurchaseInProgressCellDto> Cells { get; set; } = new();

    /// <summary>合计列</summary>
    public PurchaseInProgressCellDto Total { get; set; } = new();
}

/// <summary>
/// 采购汇总：在购（荒管/成品）结果
/// </summary>
public class PurchaseInProgressResultDto
{
    /// <summary>厂内钢种动态列（按字典序）</summary>
    public List<string> SteelGrades { get; set; } = new();

    /// <summary>行（含末行"合计"）</summary>
    public List<PurchaseInProgressRowDto> Rows { get; set; } = new();
}

/// <summary>
/// 采购汇总：月度（荒管/成品）单月值（购/回）
/// </summary>
public class PurchaseMonthlyValueDto
{
    /// <summary>购(kg) = 该月下单的采购重量</summary>
    public decimal BuyWeight { get; set; }

    /// <summary>回(kg) = 已到货量 - 退货量</summary>
    public decimal ReturnWeight { get; set; }
}

/// <summary>
/// 采购汇总：月度（荒管/成品）行（供应商）
/// </summary>
public class PurchaseMonthlyRowDto
{
    /// <summary>供应商名称（"合计"为合计行）</summary>
    public string SupplierName { get; set; } = null!;

    /// <summary>1月~12月（下标 0-11 对应 MonthLabels）</summary>
    public List<PurchaseMonthlyValueDto> Months { get; set; } = new();

    /// <summary>合计列（12月购/回各自求和）</summary>
    public PurchaseMonthlyValueDto Total { get; set; } = new();

    /// <summary>现在购(t) = 状态已下单+部分到货 的 采购重量+退货量-已到货量（不分厂内钢种）</summary>
    public decimal NowInProgress { get; set; }
}

/// <summary>
/// 采购汇总：月度（荒管/成品）结果
/// </summary>
public class PurchaseMonthlyResultDto
{
    /// <summary>月份标签（yyyy-MM，1月~12月）</summary>
    public List<string> MonthLabels { get; set; } = new();

    /// <summary>行（含末行"合计"）</summary>
    public List<PurchaseMonthlyRowDto> Rows { get; set; } = new();
}
