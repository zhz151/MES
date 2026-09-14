namespace MES.Core.Enums;

/// <summary>
/// 不合格流向（5 档）——物料实际去向，由过程检验/成品检验记录带出，不合格反馈来源为空。
/// <b>不是</b>处置方式（<see cref="MES.Core.Constants.NcrDisposalKeys"/>，8 档字典，含「让步放行」「返工」
/// 这类操作前预先判定的档位与更细的「入次品库(修正改制/报废)」）。
/// 本枚举固定不字典化：流向是检验结果的客观分类，不随用户配置改名或扩展。
/// </summary>
public enum FlowDirection
{
    /// <summary>返整</summary>
    Rework,

    /// <summary>入在制库</summary>
    InProcessWarehouse,

    /// <summary>可入备库</summary>
    FinishedWarehouse,

    /// <summary>入次品库</summary>
    Scrap,

    /// <summary>退货</summary>
    Return
}
