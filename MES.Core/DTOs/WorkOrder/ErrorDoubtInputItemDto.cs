using MES.Core.Helpers;

namespace MES.Core.DTOs.WorkOrder;

/// <summary>
/// 「工单原锁-错疑投料」卡片明细项：取工单执行状况读模型（WorkOrderExecutionSummary）中
/// 主号-关注（ScheduleStage）=2 原料锁定，且到料实投一致性为「2 个错误 + 2 个疑问」（PlanInputConsistency ∈ {2,3,4,5}）的工单行。
/// 展示基础数据（工单号/订单号/主号/工厂牌号/规格/总重量）+ 用料计划及执行实况（计划投料总重/截止到料日/可投料总重/实际已投料量/到料实投一致性/理论原料未至）。
/// </summary>
public class ErrorDoubtInputItemDto
{
    public int WorkOrderId { get; set; }
    public string WorkOrderNo { get; set; } = null!;
    public string SalesOrderNo { get; set; } = null!;
    public string ProductionMainNo { get; set; } = null!;
    public string PlantGrade { get; set; } = null!;
    public string Specification { get; set; } = null!;
    public decimal TotalWeight { get; set; }

    /// <summary>计划投料总重（kg）</summary>
    public decimal TotalPlanWeight { get; set; }

    /// <summary>截止到料日</summary>
    public DateTime? CutoffArrivalDate { get; set; }

    /// <summary>现可投料总重（kg）</summary>
    public decimal TotalAvailableWeight { get; set; }

    /// <summary>理论缺失总料重（kg）</summary>
    public decimal TotalMissingWeight { get; set; }

    /// <summary>实际已投料量（kg）</summary>
    public decimal ActualInputWeight { get; set; }

    /// <summary>到料实投一致性（2=疑问-到料少投 3=疑问-到料超投 4=错误-无料已投 5=错误-无需投料）</summary>
    public int PlanInputConsistency { get; set; }
    public string PlanInputConsistencyText => IntStatusDisplayHelper.GetPlanInputConsistencyText(PlanInputConsistency);
}
