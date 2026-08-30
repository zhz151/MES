using MES.Core.Constants;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.WorkOrder;
using MudBlazor;

namespace MES.Blazor.Pages;

public partial class Index
{
    // ========== 卡片 1：工单执行 ==========
    private bool _isLoadingCard1;
    private List<WorkOrderStageRow> _rows = new();
    private string _footerTotal = "";
    private string _footerUrgentTotal = "";

    // ========== 卡片 2：批次生产（直接采用「批次计划」中的「段落流转分析」列表） ==========
    private bool _isLoadingCard2;
    private List<SectionParagraphFlowAnalysisDto> _paragraphFlowAllItems = new();
    private List<SectionParagraphFlowAnalysisDto> _paragraphFlowItems = new();
    private string _flowSortColumn = "Category";
    private bool _flowSortDescending = false;
    private decimal? _flowTotalPending;
    private decimal? _flowTotalPlanFlowQuantity;
    private decimal? _flowTotalPlanKeyWeight;

    // ========== 卡片 3：质量检验 ==========
    private bool _isLoadingCard3;
    private int _card3AwaitingMaterialCount;
    private decimal _card3AwaitingMaterialWeight;
    private int _card3AwaitingInspectionCount;
    private decimal _card3AwaitingInspectionWeight;
    private int _card3InInspectionCount;
    private decimal _card3InInspectionWeight;
    private int _card3AwaitingMaterialUrgentCount;
    private decimal _card3AwaitingMaterialUrgentWeight;
    private int _card3AwaitingInspectionUrgentCount;
    private decimal _card3AwaitingInspectionUrgentWeight;
    private int _card3InInspectionUrgentCount;
    private decimal _card3InInspectionUrgentWeight;
    private int _card3PendingCheckBatches;     // 不合格报告 - 待处理批次
    private int _card3ProcessingNcrs;           // 不合格报告 - 处理中（Pending + Processing）

    protected override async Task OnInitializedAsync()
    {
        // 加载卡片 1
        _isLoadingCard1 = true;
        try
        {
            var result = await WorkOrderService.GetDashboardSummaryAsync();
            if (result?.Success == true && result.Data != null)
                BuildCrossTable(result.Data);
        }
        finally
        {
            _isLoadingCard1 = false;
        }

        // 加载卡片 2
        _isLoadingCard2 = true;
        try
        {
            var result = await ParagraphFlowSvc.GetAnalysisAsync();
            if (result.Success && result.Data != null)
            {
                _paragraphFlowAllItems = result.Data;
            }
        }
        finally
        {
            _isLoadingCard2 = false;
        }

        // 加载卡片 3
        _isLoadingCard3 = true;
        try
        {
            await LoadCard3Async();
        }
        finally
        {
            _isLoadingCard3 = false;
        }

        ApplyFlowSort();
    }

    // ========== 卡片 1 逻辑 ==========

    private void BuildCrossTable(List<WorkOrderExecutionDashboardItem> items)
    {
        var stageGroups = items
            .GroupBy(x => x.ScheduleStage)
            .ToDictionary(g => g.Key);

        var stages = new (int Stage, string Name, string Url)[]
        {
            (1, "原料锁定", "/raw-material-lock-plan"),
            (2, "生产在产", "/batch-plans"),
            (3, "成品检验", "/final-inspection-plan"),
        };

        _rows = new List<WorkOrderStageRow>();
        var grandTotalCount = 0;
        var grandTotalWeight = 0m;
        var grandUrgentCount = 0;
        var grandUrgentWeight = 0m;

        foreach (var (stage, name, url) in stages)
        {
            var totalCount = 0;
            var totalWeight = 0m;
            var urgentCount = 0;
            var urgentWeight = 0m;

            if (stageGroups.TryGetValue(stage, out var stageItems))
            {
                totalCount = stageItems.Sum(x => x.OrderCount);
                totalWeight = stageItems.Sum(x => x.TotalWeight);

                urgentCount = stageItems
                    .Where(x => UrgencyLevelKeys.IsUrgent(x.UrgencyLevel))
                    .Sum(x => x.OrderCount);
                urgentWeight = stageItems
                    .Where(x => UrgencyLevelKeys.IsUrgent(x.UrgencyLevel))
                    .Sum(x => x.TotalWeight);
            }

            _rows.Add(new WorkOrderStageRow
            {
                StageName = name,
                NavigateUrl = url,
                TotalText = $"{totalCount}/{FormatWeight(totalWeight)}",
                UrgentTotalText = $"{urgentCount}/{FormatWeight(urgentWeight)}",
            });

            grandTotalCount += totalCount;
            grandTotalWeight += totalWeight;
            grandUrgentCount += urgentCount;
            grandUrgentWeight += urgentWeight;
        }

        _footerTotal = $"{grandTotalCount}/{FormatWeight(grandTotalWeight)}";
        _footerUrgentTotal = $"{grandUrgentCount}/{FormatWeight(grandUrgentWeight)}";
    }

    private static string FormatWeight(decimal weight)
        => Math.Round(weight / 1000m, 0, MidpointRounding.AwayFromZero).ToString("G29");

    // ========== 卡片 2 逻辑 ==========

    private void ToggleFlowSort(string key)
    {
        if (_flowSortColumn == key)
            _flowSortDescending = !_flowSortDescending;
        else
        {
            _flowSortColumn = key;
            _flowSortDescending = false;
        }
        ApplyFlowSort();
    }

    private void ApplyFlowSort()
    {
        var q = _paragraphFlowAllItems.AsEnumerable();

        q = _flowSortColumn switch
        {
            "Category" => _flowSortDescending
                ? q.OrderByDescending(x => x.DisplayOrder)
                : q.OrderBy(x => x.DisplayOrder),
            "PendingTotal" => _flowSortDescending
                ? q.OrderByDescending(x => x.PendingTotal)
                : q.OrderBy(x => x.PendingTotal),
            "StatusJudgment" => _flowSortDescending
                ? q.OrderByDescending(x => x.StatusJudgment)
                : q.OrderBy(x => x.StatusJudgment),
            "PlanFlowQuantity" => _flowSortDescending
                ? q.OrderByDescending(x => x.PlanFlowQuantity)
                : q.OrderBy(x => x.PlanFlowQuantity),
            "PlanFlowJudgment" => _flowSortDescending
                ? q.OrderByDescending(x => x.PlanFlowJudgment)
                : q.OrderBy(x => x.PlanFlowJudgment),
            "PlanKeyWeight" => _flowSortDescending
                ? q.OrderByDescending(x => x.PlanKeyWeight)
                : q.OrderBy(x => x.PlanKeyWeight),
            _ => q.OrderBy(x => x.DisplayOrder)
        };

        _paragraphFlowItems = q.ToList();
        ComputeFlowFooter();
    }

    private void ComputeFlowFooter()
    {
        _flowTotalPending = _paragraphFlowItems.Sum(x => x.PendingTotal ?? 0m);
        _flowTotalPlanFlowQuantity = _paragraphFlowItems.Sum(x => x.PlanFlowQuantity ?? 0m);
        _flowTotalPlanKeyWeight = _paragraphFlowItems.Sum(x => x.PlanKeyWeight ?? 0m);
    }

    private string FlowSortIndicator(string key)
    {
        if (_flowSortColumn != key) return "";
        return _flowSortDescending ? " ▼" : " ▲";
    }

    private static string FlowRenderInt(decimal? val)
    {
        return val.HasValue ? Math.Round(val.Value, 0).ToString() : "-";
    }

    private static Color FlowGetStatusColor(string? status)
    {
        return status switch
        {
            SustainStatusKeys.Excessive => Color.Error,
            SustainStatusKeys.Insufficient => Color.Warning,
            SustainStatusKeys.Normal => Color.Success,
            _ => Color.Default
        };
    }

    private static Color FlowGetPlanFlowJudgmentColor(string? judgment)
    {
        return judgment == PlanFlowJudgmentKeys.Accelerate ? Color.Error : Color.Default;
    }

    private static string Card3StatText(int count, decimal weightTons)
    {
        return $"{count}批/{((int)weightTons).ToString()}吨";
    }

    // ========== 卡片 3 逻辑 ==========

    private async Task LoadCard3Async()
    {
        // 成品检验：按 KanbanStage 分组统计
        try
        {
            var kanbanItems = await FinalInspectionSvc.GetKanbanAsync();

            // 待到料
            var material = kanbanItems.Where(x => x.KanbanStage == KanbanStageKeys.WaitingMaterial).ToList();
            _card3AwaitingMaterialCount = material.Count;
            _card3AwaitingMaterialWeight = material.Sum(x => x.ProductionWeight ?? 0m) / 1000m;
            _card3AwaitingMaterialUrgentCount = material.Count(x => UrgencyLevelKeys.IsUrgent(x.UrgencyLevel));
            _card3AwaitingMaterialUrgentWeight = material.Where(x => UrgencyLevelKeys.IsUrgent(x.UrgencyLevel)).Sum(x => x.ProductionWeight ?? 0m) / 1000m;

            // 待检验
            var awaiting = kanbanItems.Where(x => x.KanbanStage == KanbanStageKeys.WaitingInspection).ToList();
            _card3AwaitingInspectionCount = awaiting.Count;
            _card3AwaitingInspectionWeight = awaiting.Sum(x => x.ProductionWeight ?? 0m) / 1000m;
            _card3AwaitingInspectionUrgentCount = awaiting.Count(x => UrgencyLevelKeys.IsUrgent(x.UrgencyLevel));
            _card3AwaitingInspectionUrgentWeight = awaiting.Where(x => UrgencyLevelKeys.IsUrgent(x.UrgencyLevel)).Sum(x => x.ProductionWeight ?? 0m) / 1000m;

            // 检验中
            var inProg = kanbanItems.Where(x => x.KanbanStage == KanbanStageKeys.Inspecting).ToList();
            _card3InInspectionCount = inProg.Count;
            _card3InInspectionWeight = inProg.Sum(x => x.ProductionWeight ?? 0m) / 1000m;
            _card3InInspectionUrgentCount = inProg.Count(x => UrgencyLevelKeys.IsUrgent(x.UrgencyLevel));
            _card3InInspectionUrgentWeight = inProg.Where(x => UrgencyLevelKeys.IsUrgent(x.UrgencyLevel)).Sum(x => x.ProductionWeight ?? 0m) / 1000m;
        }
        catch
        {
            _card3AwaitingMaterialCount = 0;
            _card3AwaitingMaterialWeight = 0m;
            _card3AwaitingInspectionCount = 0;
            _card3AwaitingInspectionWeight = 0m;
            _card3InInspectionCount = 0;
            _card3InInspectionWeight = 0m;
            _card3AwaitingMaterialUrgentCount = 0;
            _card3AwaitingMaterialUrgentWeight = 0m;
            _card3AwaitingInspectionUrgentCount = 0;
            _card3AwaitingInspectionUrgentWeight = 0m;
            _card3InInspectionUrgentCount = 0;
            _card3InInspectionUrgentWeight = 0m;
        }

        // 不合格报告
        try
        {
            var pendingChecks = await NcrSvc.GetPendingChecksAsync();
            _card3PendingCheckBatches = pendingChecks?.Success == true && pendingChecks.Data != null
                ? pendingChecks.Data.Count
                : 0;
        }
        catch
        {
            _card3PendingCheckBatches = 0;
        }

        try
        {
            // 查询处理中的 NCR（状态为 Pending 或 Processing，即未关闭）
            var filterJson = System.Text.Json.JsonSerializer.Serialize(
                new[] { new MES.Core.Models.FilterDescriptor { Field = "Status", Values = new List<string> { "Pending", "Processing" }, Operator = "in" } });
            var result = await NcrSvc.GetAllAsync(pageIndex: 1, pageSize: 1, filters: filterJson);
            _card3ProcessingNcrs = result?.Success == true && result.Data != null
                ? result.Data.TotalCount
                : 0;
        }
        catch
        {
            _card3ProcessingNcrs = 0;
        }
    }

    // ========== 共用类型 ==========

    private class WorkOrderStageRow
    {
        public string StageName { get; set; } = "";
        public string NavigateUrl { get; set; } = "";
        public string TotalText { get; set; } = "";
        public string UrgentTotalText { get; set; } = "";
    }
}
