namespace MES.Core.DTOs.Quality;

/// <summary>
/// 自动填充检验数据请求
/// </summary>
public class AutoFillInspectionRequest
{
    /// <summary>需要填充的子项列表（需提供 HeatNo + ProductionBatchNo）</summary>
    public List<AutoFillInspectionItem> Items { get; set; } = new();
}

/// <summary>
/// 单条自动填充项
/// </summary>
public class AutoFillInspectionItem
{
    /// <summary>序号（对应子表 SeqNo）</summary>
    public int SeqNo { get; set; }

    /// <summary>炉号（用于匹配化学分析和成品检验）</summary>
    public string? HeatNo { get; set; }

    /// <summary>生产批号（用于匹配拉伸检验和成品检验）</summary>
    public string? ProductionBatchNo { get; set; }
}
