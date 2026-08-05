using MES.Core.Enums;

namespace MES.Core.DTOs.Order;

public class SourceOrderValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Warnings { get; set; } = new();
    public string? ExpectedWorkOrderNo { get; set; }
    /// <summary>物料分类（MaterialType 枚举名，来源单号的物料分类）</summary>
    public string? MaterialCategory { get; set; }
    /// <summary>厂内钢种（来源单号的工厂牌号）</summary>
    public string? PlantGrade { get; set; }
    /// <summary>规格（来源单号的名义规格）</summary>
    public string? Specification { get; set; }
    /// <summary>供应商名称</summary>
    public string? SupplierName { get; set; }
    /// <summary>订单号（生产批次查询时填充）</summary>
    public string? SalesOrderNo { get; set; }
    /// <summary>项次（生产批次查询时填充）</summary>
    public string? OrderItemIds { get; set; }
    /// <summary>炉号（生产批次查询时填充）</summary>
    public string? HeatNo { get; set; }
    /// <summary>制造状态/交货状态（生产批次查询时填充）</summary>
    public string? ManufacturingStatus { get; set; }
}

public class SourceOrderValidationRequest
{
    public string SourceOrderNo { get; set; } = null!;
    public InboundSource InboundSource { get; set; }
    /// <summary>委外来源序号（SubcontractReturnItem.Sequence），采购可忽略</summary>
    public int? SourceOrderSequence { get; set; }
}

/// <summary>
/// 生产批号验证请求
/// </summary>
public class ProductionBatchValidationRequest
{
    /// <summary>生产批号</summary>
    public string ProductionBatchNo { get; set; } = null!;
}
