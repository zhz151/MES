using MES.Core.Enums;

namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 在产改制计划 DTO
/// </summary>
public class InProcessReworkPlanDto
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public DateTime PlanDate { get; set; }
    public int ProductionBatchId { get; set; }
    public string BatchNo { get; set; } = null!;
    public string? BatchTagNo { get; set; }
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public int InputMultiple { get; set; }
    public int? UsedQuantity { get; set; }
    public decimal UsedWeight { get; set; }
}

/// <summary>
/// 创建在产改制计划请求
/// </summary>
public class CreateInProcessReworkPlanRequest
{
    public int WorkOrderId { get; set; }
    public DateTime PlanDate { get; set; }
    public int ProductionBatchId { get; set; }
    public int InputMultiple { get; set; } = 1;
    public int? UsedQuantity { get; set; }
    public decimal UsedWeight { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? Remark { get; set; }

    /// <summary>
    /// 工序组（在产改制必填，随创建请求内算工量）
    /// </summary>
    public List<SavePlanProcessGroupItem>? ProcessGroups { get; set; }
}

/// <summary>
/// 可用在产批次 DTO（展示给用户选择）
/// </summary>
public class AvailableInProcessBatchDto
{
    /// <summary>批次ID</summary>
    public int Id { get; set; }
    /// <summary>批次号</summary>
    public string BatchNo { get; set; } = null!;
    /// <summary>挂牌号</summary>
    public string? TagNo { get; set; }
    /// <summary>工厂牌号</summary>
    public string PlantGrade { get; set; } = null!;
    /// <summary>规格</summary>
    public string Specification { get; set; } = null!;
    /// <summary>长度状态</summary>
    public LengthStatus LengthStatus { get; set; }
    /// <summary>现有效原料支数</summary>
    public int? CurrentValidQty { get; set; }
    /// <summary>现有效原料重量(kg)</summary>
    public int? CurrentValidWeight { get; set; }
    /// <summary>当前工序</summary>
    public string? CurrentGroupName { get; set; }
    /// <summary>当前工段</summary>
    public string? CurrentSectionName { get; set; }
    /// <summary>当前规格</summary>
    public string? CurrentSpec { get; set; }
    /// <summary>下个工序（未产批次=首道工序；在产批次=当前工段之后的下一工段所在工序组）</summary>
    public string? NextProcess { get; set; }
    /// <summary>下个工段</summary>
    public string? NextSectionName { get; set; }
    /// <summary>下个规格（下个工段所在工序块的制造规格；未产批次=首道工序规格，用于可用料判定）</summary>
    public string? CorrespondingSpec { get; set; }

    /// <summary>
    /// 已被其他未取消且未投料的在产改制计划预留的支数（与 CurrentValidQty 显示口径一致）
    /// </summary>
    public int ReservedQuantity { get; set; }

    /// <summary>
    /// 已被其他未取消且未投料的在产改制计划预留的重量(kg)
    /// </summary>
    public decimal ReservedWeight { get; set; }
}
