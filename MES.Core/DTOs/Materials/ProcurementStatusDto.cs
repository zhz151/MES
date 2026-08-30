using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Materials;

/// <summary>
/// 工单用料计划采购执行状态
/// </summary>
public class ProcurementStatusDto
{
    /// <summary>工单号</summary>
    public string WorkOrderNo { get; set; } = null!;

    /// <summary>物料名称</summary>
    public string MaterialName { get; set; } = null!;

    /// <summary>物料分类（MaterialType 枚举名）</summary>
    public MaterialType? MaterialCategory { get; set; }

    /// <summary>工厂牌号（计划行级，同工单同分类多牌号去重后用顿号拼接）</summary>
    public string? PlantGrade { get; set; }

    /// <summary>计划总量(kg)</summary>
    public decimal PlanWeight { get; set; }

    /// <summary>已采购重量(kg)（采购卡片用）</summary>
    public decimal PurchaseWeight { get; set; }

    /// <summary>已委外重量(kg)（圆棒穿孔卡片用）</summary>
    public decimal SubcontractWeight { get; set; }

    /// <summary>缺少量(kg) = Max(0, 计划总量 - 已执行)</summary>
    public decimal MissingWeight { get; set; }

    /// <summary>状态：未采购 / 部分采购</summary>
    public string StatusText { get; set; } = null!;

    // ========== 工单实时关注（按工单号关联工单执行状况读模型，无记录 null） ==========

    /// <summary>工单关注（ScheduleStage：0主号暂停/1主号完成/2原料锁定/3生产执行/4成品检验）</summary>
    public int? ExecutionScheduleStage { get; set; }

    /// <summary>原锁执行备注（RawMaterialLockRemark Key）</summary>
    public string? ExecutionRawMaterialLockRemark { get; set; }

    /// <summary>工单计划性（UrgencyLevel Key）</summary>
    public string? ExecutionUrgencyLevel { get; set; }
}
