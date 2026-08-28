using MES.Core.Helpers;

namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 「在产在检-错疑待料」卡片行：取工单执行状况读模型（WorkOrderExecutionSummary）中
/// 主号-关注（ScheduleStage）=1 主号完成 / 3 生产执行 / 4 成品检验 三档（已过投料期的「实时关注」档位），
/// 分别统计「理论原料未至」（TotalMissingWeight &gt; 0）与「工单到料未投」（PendingInputWeight &gt; 0）的工单数 + 累计重量。
/// </summary>
public class InProductionInspectionDoubtItemDto
{
    /// <summary>主号-关注（1=主号完成 3=生产执行 4=成品检验）</summary>
    public int ScheduleStage { get; set; }

    public string ScheduleStageText => IntStatusDisplayHelper.GetScheduleStageText(ScheduleStage);

    /// <summary>理论原料未至-工单数（TotalMissingWeight &gt; 0 的工单行数）</summary>
    public int MissingOrderCount { get; set; }

    /// <summary>理论原料未至-累计重量（kg，TotalMissingWeight &gt; 0 各行缺口之和）</summary>
    public decimal MissingWeight { get; set; }

    /// <summary>工单到料未投-工单数（PendingInputWeight &gt; 0 的工单行数）</summary>
    public int PendingInputOrderCount { get; set; }

    /// <summary>工单到料未投-累计重量（kg，PendingInputWeight &gt; 0 各行未投量之和）</summary>
    public decimal PendingInputWeight { get; set; }
}
