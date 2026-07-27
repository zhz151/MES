using System.ComponentModel.DataAnnotations;
using MES.Core.Enums;

namespace MES.Core.DTOs.Warehouse;

/// <summary>
/// 批量入库请求
/// </summary>
public class BatchInboundRequest
{
    /// <summary>
    /// 仓库（必须，非公共）
    /// </summary>
    [Required]
    public int WarehouseId { get; set; }

    /// <summary>
    /// 明细行
    /// </summary>
    [Required, MinLength(1)]
    public List<InboundRow> Rows { get; set; } = new();

    // ========== 以下为公共字段，填写后自动应用到所有行 ==========

    public InboundSource? InboundSource { get; set; }
    public string? SourceName { get; set; }
    public DateTime? InboundDate { get; set; }
    public MaterialType? MaterialType { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public string? HeatNo { get; set; }
    public string? ProductionBatchNo { get; set; }
    public string? LengthStatus { get; set; }
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public decimal? UnitWeight { get; set; }
    public decimal? Meters { get; set; }
    public string? ActualSpecification { get; set; }
    public DeliveryState? SurfaceCondition { get; set; }
    public string? LocationArea { get; set; }
    public string? LocationRack { get; set; }
    public string? DefectReason { get; set; }
    public string? LiabilityType { get; set; }
    public string? OriginalSupplier { get; set; }
    public string? TagNo { get; set; }
    public string? DefectRemark { get; set; }
    public bool? IsLinkedToWorkOrder { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? SalesOrderNo { get; set; }
    public string? OrderItemIds { get; set; }
    public string? SourceOrderNo { get; set; }
    public int? SourceOrderSequence { get; set; }
}

/// <summary>
/// 单条入库行
/// </summary>
public class InboundRow
{
    [Range(1, int.MaxValue, ErrorMessage = "支数必须大于0")]
    public int InitialQuantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "重量必须大于等于0")]
    public decimal InitialWeight { get; set; }

    // 原有可覆盖字段
    public string? LengthStatus { get; set; }
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public decimal? UnitWeight { get; set; }
    public decimal? Meters { get; set; }
    public DeliveryState? SurfaceCondition { get; set; }
    public string? LocationArea { get; set; }
    public string? LocationRack { get; set; }
    public string? Remark { get; set; }

    // 次品相关（行级覆盖）
    public string? DefectReason { get; set; }
    public string? LiabilityType { get; set; }
    public string? OriginalSupplier { get; set; }
    public string? TagNo { get; set; }
    public string? DefectRemark { get; set; }

    // 原仅公共字段，现支持行级覆盖（row ?? common 回退）
    public MaterialType? MaterialType { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public string? HeatNo { get; set; }
    public InboundSource? InboundSource { get; set; }
    public string? SourceName { get; set; }
    public string? ProductionBatchNo { get; set; }
    public string? ActualSpecification { get; set; }
    public string? SalesOrderNo { get; set; }
    public string? OrderItemIds { get; set; }
    public bool? IsLinkedToWorkOrder { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? SourceOrderNo { get; set; }
    public int? SourceOrderSequence { get; set; }
}

/// <summary>
/// 批量入库结果
/// </summary>
public class BatchInboundResult
{
    public int SuccessCount { get; set; }
    public List<string> BatchNos { get; set; } = new();
}
