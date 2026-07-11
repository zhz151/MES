namespace MES.Core.DTOs.Order;

public class SourceOrderValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Warnings { get; set; } = new();
    public string? ExpectedWorkOrderNo { get; set; }
    /// <summary>物料分类（来源单号的物料分类）</summary>
    public string? MaterialCategory { get; set; }
    /// <summary>厂内钢种（来源单号的工厂牌号）</summary>
    public string? PlantGrade { get; set; }
    /// <summary>规格（来源单号的名义规格）</summary>
    public string? Specification { get; set; }
    /// <summary>供应商名称</summary>
    public string? SupplierName { get; set; }
}

public class SourceOrderValidationRequest
{
    public string SourceOrderNo { get; set; } = null!;
    public string InboundSource { get; set; } = null!;
    /// <summary>委外来源序号（SubcontractReturnItem.Sequence），采购可忽略</summary>
    public int? SourceOrderSequence { get; set; }
}
