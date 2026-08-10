using MES.Core.DTOs.Warehouse;
using MES.Core.Enums;
using MES.Core.Helpers;
namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 库存使用计划 DTO
/// </summary>
public class InventoryPlanDto
{
    /// <summary>
    /// 计划ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 关联工单ID
    /// </summary>
    public int WorkOrderId { get; set; }

    /// <summary>
    /// 计划日期
    /// </summary>
    public DateTime PlanDate { get; set; }

    /// <summary>
    /// 关联库存批次号
    /// </summary>
    public string InventoryBatchNo { get; set; } = null!;

    /// <summary>
    /// 批次号
    /// </summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>
    /// 物料名称
    /// </summary>
    public MaterialType? MaterialType { get; set; }

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 规格
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 放置区域
    /// </summary>
    public string? LocationArea { get; set; }

    /// <summary>
    /// 放置架号
    /// </summary>
    public string? LocationRack { get; set; }

    /// <summary>
    /// 投料倍率
    /// </summary>
    public int InputMultiple { get; set; }

    /// <summary>
    /// 使用模式：All=全部使用 Partial=部分使用
    /// </summary>
    public string UsageMode { get; set; } = null!;

    /// <summary>
    /// 使用支数（部分使用时填写）
    /// </summary>
    public int? UsedQuantity { get; set; }

    /// <summary>
    /// 使用重量(kg)
    /// </summary>
    public decimal UsedWeight { get; set; }

    /// <summary>
    /// 要求到位日期
    /// </summary>
    public DateTime? RequiredDate { get; set; }

    /// <summary>
    /// 计划状态值
    /// </summary>
    public InventoryPlanStatus PlanStatus { get; set; }
    public string PlanStatusDisplay => EnumHelper.GetDisplayName(PlanStatus);

    /// <summary>
    /// 计划状态文本
    /// </summary>
    public string PlanStatusText { get; set; } = null!;

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 改制类型
    /// </summary>
    public ReworkType? ReworkType { get; set; }
    public string? ReworkTypeDisplay => ReworkType.HasValue ? EnumHelper.GetDisplayName(ReworkType.Value) : null;

    /// <summary>
    /// 改制类型文本
    /// </summary>
    public string? ReworkTypeText { get; set; }

    /// <summary>工艺周期（天）</summary>
    public int StandardCycle { get; set; }

    /// <summary>
    /// 是否已生产领用出库（已出库计划不可修改/删除）
    /// </summary>
    public bool IsOutbound { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public string CreatedBy { get; set; } = null!;
}

/// <summary>
/// 创建库存使用计划请求
/// </summary>
public class CreateInventoryPlanRequest
{
    /// <summary>
    /// 关联工单ID
    /// </summary>
    public int WorkOrderId { get; set; }

    /// <summary>
    /// 计划日期
    /// </summary>
    public DateTime PlanDate { get; set; }

    /// <summary>
    /// 关联库存批次号
    /// </summary>
    public string InventoryBatchNo { get; set; } = null!;

    /// <summary>
    /// 物料名称
    /// </summary>
    public MaterialType? MaterialType { get; set; }

    /// <summary>
    /// 放置区域
    /// </summary>
    public string? LocationArea { get; set; }

    /// <summary>
    /// 放置架号
    /// </summary>
    public string? LocationRack { get; set; }

    /// <summary>
    /// 投料倍率(1制几)
    /// </summary>
    public int InputMultiple { get; set; } = 1;

    /// <summary>
    /// 使用模式：All=全部使用 Partial=部分使用
    /// </summary>
    public string UsageMode { get; set; } = "All";

    /// <summary>
    /// 使用支数（部分使用时填写）
    /// </summary>
    public int? UsedQuantity { get; set; }

    /// <summary>
    /// 使用重量(kg)
    /// </summary>
    public decimal UsedWeight { get; set; }

    /// <summary>
    /// 要求到位日期
    /// </summary>
    public DateTime? RequiredDate { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 改制类型
    /// </summary>
    public ReworkType? ReworkType { get; set; }

    /// <summary>
    /// 工序组（库料改制 ReworkType 非空时必填；库存使用可为空）
    /// </summary>
    public List<SavePlanProcessGroupItem>? ProcessGroups { get; set; }

}

/// <summary>
/// 可用库存批次 DTO（展示给用户选择）
/// </summary>
public class AvailableInventoryBatchDto
{
    /// <summary>
    /// 批次ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 批次号
    /// </summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>
    /// 物料名称
    /// </summary>
    public MaterialType? MaterialType { get; set; }

    /// <summary>
    /// 工厂牌号
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 规格
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 实际规格（原实测外径/实测壁厚已删除，实测值由实际规格解析）
    /// </summary>
    public string? ActualSpecification { get; set; }

    /// <summary>
    /// 长度状态
    /// </summary>
    public LengthStatus? LengthStatus { get; set; }

    /// <summary>
    /// 最小长度(mm)
    /// </summary>
    public decimal? MinLength { get; set; }

    /// <summary>
    /// 最大长度(mm)
    /// </summary>
    public decimal? MaxLength { get; set; }

    /// <summary>
    /// 剩余支数（物理剩余）
    /// </summary>
    public int RemainingQuantity { get; set; }

    /// <summary>
    /// 剩余重量(kg)（物理剩余）
    /// </summary>
    public decimal RemainingWeight { get; set; }

    /// <summary>
    /// 已被其他部分使用计划预留的支数（仅统计未取消且未出库的部分使用计划）
    /// </summary>
    public int ReservedQuantity { get; set; }

    /// <summary>
    /// 已被其他部分使用计划预留的重量(kg)（仅统计未取消且未出库的部分使用计划）
    /// </summary>
    public decimal ReservedWeight { get; set; }

    /// <summary>
    /// 单重(kg/支)
    /// </summary>
    public decimal? UnitWeight { get; set; }

    /// <summary>
    /// 制造状态
    /// </summary>
    public DeliveryState? ManufacturingStatus { get; set; }

    /// <summary>
    /// 放置区域
    /// </summary>
    public string? LocationArea { get; set; }

    /// <summary>
    /// 放置架号
    /// </summary>
    public string? LocationRack { get; set; }
}
