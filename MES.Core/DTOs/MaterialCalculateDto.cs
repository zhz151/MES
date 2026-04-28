namespace MES.Core.DTOs;

/// <summary>
/// 用料测算请求（前端发送测算参数，后端返回计算结果）
/// </summary>
public class MaterialCalculateRequest
{
    /// <summary>工单ID（用于获取工单信息）</summary>
    public int WorkOrderId { get; set; }

    /// <summary>调整壁厚(成品)(mm)</summary>
    public decimal AdjustedWallThickness { get; set; }

    /// <summary>成材率(%)</summary>
    public decimal YieldRate { get; set; }

    /// <summary>投料倍率(1制几)</summary>
    public int InputMultiple { get; set; }

    /// <summary>正品率(%)</summary>
    public decimal QualifiedRate { get; set; }
}

/// <summary>
/// 用料测算结果
/// </summary>
public class MaterialCalculateResult
{
    /// <summary>密度(g/cm³)</summary>
    public decimal Density { get; set; }

    /// <summary>单米重量(kg/m)</summary>
    public decimal UnitWeightPerMeter { get; set; }

    /// <summary>成品单重(kg/支)</summary>
    public decimal? UnitWeight { get; set; }

    /// <summary>原料单重(kg/支)</summary>
    public decimal? RawUnitWeight { get; set; }

    /// <summary>原料支数</summary>
    public int? RequiredPieces { get; set; }

    /// <summary>原料重量(kg)</summary>
    public decimal? RequiredWeight { get; set; }
}
