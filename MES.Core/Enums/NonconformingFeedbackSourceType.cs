namespace MES.Core.Enums;

/// <summary>
/// 不合格反馈单「来源类型」— 表示这条反馈是在哪个环节填报的：
/// 生产工段（正式生产前发现疑问）/ 过程检验 / 成品检验。
/// 来源决定位置信息取「工序+工段」还是「工序+检验项目」。
/// </summary>
public enum NonconformingFeedbackSourceType
{
    /// <summary>生产工段（正式生产前由生产工段填报，位置=工序+工段）</summary>
    ProductionSection,

    /// <summary>过程检验（检验前初检或检验中难判定，位置=工序+工段）</summary>
    ProcessInspection,

    /// <summary>成品检验（位置=工序+检验项目，无工段）</summary>
    FinalInspection
}
