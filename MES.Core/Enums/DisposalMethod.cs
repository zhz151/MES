namespace MES.Core.Enums;

/// <summary>
/// 不合格品处置方式
/// </summary>
public enum DisposalMethod
{
    /// <summary>返整</summary>
    Rework,
    /// <summary>入库</summary>
    WarehouseEntry,
    /// <summary>报废</summary>
    Scrap
}
