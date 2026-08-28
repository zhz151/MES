namespace MES.Core.DTOs.Batch;

/// <summary>
/// 「批次-错疑执行」卡片错疑类别：匹配工单/工段流转/有效投料/成品切割
/// </summary>
public enum BatchDoubtExecutionType
{
    /// <summary>匹配工单：执行匹配=错误</summary>
    MatchOrder,

    /// <summary>工段流转：流转判定=疑问</summary>
    FlowDoubt,

    /// <summary>有效投料：需调整=是</summary>
    NeedAdjust,

    /// <summary>成品切割：成切存疑=疑问-数量/疑问-缺少</summary>
    CutDoubt
}

/// <summary>
/// 「批次-错疑执行」卡片行：错疑类别 + 批次数 + 领料重量合计。
/// 统计口径：全量批次，批次数=命中条件批次数，重量=这些批次「原始投料信息组」领料重量（InputWeight）之和。
/// </summary>
public class BatchDoubtExecutionItemDto
{
    /// <summary>错疑类别</summary>
    public BatchDoubtExecutionType DoubtType { get; set; }

    /// <summary>错疑类别中文显示</summary>
    public string DoubtTypeText => DoubtType switch
    {
        BatchDoubtExecutionType.MatchOrder => "匹配工单",
        BatchDoubtExecutionType.FlowDoubt => "工段流转",
        BatchDoubtExecutionType.NeedAdjust => "有效投料",
        BatchDoubtExecutionType.CutDoubt => "成品切割",
        _ => ""
    };

    /// <summary>符合条件的批次数</summary>
    public int BatchCount { get; set; }

    /// <summary>领料重量合计（kg，InputWeight 之和）</summary>
    public decimal InputWeight { get; set; }
}
