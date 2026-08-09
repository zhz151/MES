using System.ComponentModel.DataAnnotations;
using MES.Core.Enums;

namespace MES.Core.DTOs.Warehouse;

/// <summary>
/// 入库请求
/// </summary>
public class CreateInboundRequest
{
    [Required(ErrorMessage = "仓库不能为空")]
    public int WarehouseId { get; set; }

    [Required(ErrorMessage = "物料名称不能为空")]
    public MaterialType MaterialType { get; set; }

    [Required(ErrorMessage = "钢种不能为空")]
    [StringLength(50)]
    public string PlantGrade { get; set; } = string.Empty;

    [Required(ErrorMessage = "规格不能为空")]
    [StringLength(100)]
    public string Specification { get; set; } = string.Empty;

    [Required(ErrorMessage = "入库来源不能为空")]
    public InboundSource InboundSource { get; set; }

    [Required(ErrorMessage = "来料单位不能为空")]
    [StringLength(200)]
    public string SourceName { get; set; } = string.Empty;

    [Required(ErrorMessage = "入库日期不能为空")]
    public DateTime InboundDate { get; set; }

    public string? HeatNo { get; set; }
    public string? ProductionBatchNo { get; set; }
    public LengthStatus? LengthStatus { get; set; }
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "入库支数必须大于0")]
    public int InitialQuantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "入库重量必须大于等于0")]
    public decimal InitialWeight { get; set; }

    public decimal? UnitWeight { get; set; }
    public decimal? Meters { get; set; }
    public string? ActualSpecification { get; set; }
    public DeliveryState? ManufacturingStatus { get; set; }
    public string? LocationArea { get; set; }
    public string? LocationRack { get; set; }
    public string? Remark { get; set; }
    public string? DefectReason { get; set; }
    public string? LiabilityType { get; set; }
    public string? OriginalSupplier { get; set; }
    public string? TagNo { get; set; }
    public string? DefectRemark { get; set; }

    // 工单关联
    public bool IsLinkedToWorkOrder { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? SalesOrderNo { get; set; }
    public string? OrderItemIds { get; set; }

    // 跨上下文关联
    public string? SourceOrderNo { get; set; }
    public int? SourceOrderSequence { get; set; }

    /// <summary>
    /// 是否启用定尺切割长度匹配硬校验（仅自动填充第2种「检验入库」按生产批号时由前端置 true）。
    /// true：FG 成品库 + 检验入库 + 订单类成品 + 定尺 + 生产批号非空 + 工单号非空时，
    /// 入库长度必须命中「订单号+主号」定尺长度集合，否则抛 BusinessException 禁止入库。
    /// </summary>
    public bool EnforceCutLengthMatch { get; set; }
}
