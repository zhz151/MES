using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs;
using System.Text.Json;

namespace MES.Blazor.Pages.Scheduling;

public partial class BatchPlans
{
    private MudTable<BatchPlanDto>? table;
    private List<BatchPlanDto> _pageItems = new();
    private int _totalCount;
    private int _restoredPageIndex;
    private int _currentPageIndex = 1;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;
    private bool _isPlanning;

    // 排序状态
    private string sortColumn = "BatchNo";
    private bool sortDescending = true;

    // ========== 工段筛选 ==========
    private string? _selectedSection;
    private static readonly string[] _sectionTabs = new[]
    {
        "全部", "60冷轧", "50冷轧", "30冷轧", "20冷轧", "三辊冷轧", "冷拔",
        "油管断", "去油", "固溶", "矫直", "断切", "酸洗", "外抛光", "外点磨",
        "过程检验", "成品检验"
    };

    // ========== Tab 汇总数据 ==========
    private int _tabBatchCount;
    private decimal _tabTotalWeight;
    private int _tabKeyBatchCount;
    private decimal _tabKeyBatchWeight;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    // 全量数据缓存
    private List<BatchPlanDto> _allItems = new();

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "CurrentValidWeight", "MinLength", "MaxLength",
    };

    private static List<ColumnDef> GetAllColumnDefs()
    {
        // G1: 批次信息
        var g1 = new List<ColumnDef>
        {
            new() { Key = "BatchNo",              Label = "生产编号",   SortKey = "BatchNo",              FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "TagNo",                Label = "挂牌号",     SortKey = "TagNo",                FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "PlantGrade",            Label = "原料钢号",   SortKey = "PlantGrade",            FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "CurrentValidWeight",    Label = "重量(kg)",   SortKey = "CurrentValidWeight",    Width = "80",  GroupKey = 1, GroupName = "批次信息" },
        };

        // G2: 关联工单信息
        var g2 = new List<ColumnDef>
        {
            new() { Key = "WorkOrderNo",           Label = "工单号",     SortKey = "WorkOrderNo",           FilterType = "string", Width = "120", GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "Salesman",              Label = "业务员",     SortKey = "Salesman",              FilterType = "string", Width = "100", GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "DeliveryDate",          Label = "交货日期",   SortKey = "DeliveryDate",          Width = "110", GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "DeliveryState",         Label = "交货状态",   SortKey = "DeliveryState",         FilterType = "enum", Width = "120", EnumOptions = new() { new("SolutionAnnealedAndPickled","固溶酸洗"), new("SolutionAnnealedAndPickledUTube","固溶酸洗-U型管"), new("SolutionAnnealedAndPickledExternalPolished","固溶酸洗-外抛光"), new("SolutionAnnealedAndPickledInternalPolished","固溶酸洗-内抛光"), new("SolutionAnnealedAndPickledBothPolished","固溶酸洗-内外抛光"), new("SolutionAnnealedAndPickledCoiled","固溶酸洗-盘管"), new("Bright","光亮"), new("BrightUTube","光亮-U型管"), new("BrightCoiled","光亮-盘管"), new("Hard","硬态") }, GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "Specification",         Label = "成品规格",   SortKey = "Specification",         FilterType = "string", Width = "120", GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "LengthStatus",          Label = "长度状态",   SortKey = "LengthStatus",          FilterType = "enum", Width = "100", EnumOptions = new() { new("Fixed","定尺"), new("Range","范围尺"), new("NonFixed","非定尺") }, GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "MinLength",             Label = "最小长度",   SortKey = "MinLength",             Width = "80",  GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "MaxLength",             Label = "最大长度",   SortKey = "MaxLength",             Width = "80",  GroupKey = 2, GroupName = "关联工单" },
        };

        // G3: 状态跟踪
        var g3 = new List<ColumnDef>
        {
            new() { Key = "CurrentExecDate",        Label = "执行截止日",   SortKey = "CurrentExecDate",        Width = "110", GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "CurrentSectionName",      Label = "截止工段",     SortKey = "CurrentSectionName",      FilterType = "string", Width = "100", GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "PendingProcess",         Label = "待在产执行工序", FilterType = "string", Width = "130", GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "PendingSectionName",     Label = "执行工段",      FilterType = "string", Width = "120", GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "PendingSpec",            Label = "执行规格",      FilterType = "string", Width = "120", GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "PendingEquipment",       Label = "在轧设备",      FilterType = "string", Width = "120", GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "ExecutionSequence",      Label = "执行序",        Width = "70",                       GroupKey = 3, GroupName = "状态跟踪" },
        };

        // G5: 冷轧排程
        var g5 = new List<ColumnDef>
        {
            new() { Key = "CurrentCR_ProcessType",  Label = "本层冷轧工序", FilterType = "string",  Width = "110", GroupKey = 5, GroupName = "冷轧排程(本层)" },
            new() { Key = "CurrentCR_BilletSpec",   Label = "本层来料规格", FilterType = "string",  Width = "110", GroupKey = 5, GroupName = "冷轧排程(本层)" },
            new() { Key = "CurrentCR_RollingSpec",  Label = "本层在轧规格", FilterType = "string",  Width = "110", GroupKey = 5, GroupName = "冷轧排程(本层)" },
            new() { Key = "CurrentCR_IsFinished",   Label = "本层末道",    FilterType = "boolean", Width = "80",  GroupKey = 5, GroupName = "冷轧排程(本层)" },
            new() { Key = "NextCR_ProcessType",     Label = "下层冷轧工序", FilterType = "string",  Width = "110", GroupKey = 6, GroupName = "冷轧排程(下层)" },
            new() { Key = "NextCR_BilletSpec",      Label = "下层来料规格", FilterType = "string",  Width = "110", GroupKey = 6, GroupName = "冷轧排程(下层)" },
            new() { Key = "NextCR_RollingSpec",     Label = "下层在轧规格", FilterType = "string",  Width = "110", GroupKey = 6, GroupName = "冷轧排程(下层)" },
            new() { Key = "NextCR_IsFinished",      Label = "下层末道",    FilterType = "boolean", Width = "80",  GroupKey = 6, GroupName = "冷轧排程(下层)" },
            new() { Key = "NextNextCR_ProcessType", Label = "下下层冷轧工序", FilterType = "string", Width = "110", GroupKey = 9, GroupName = "冷轧排程(下下层)" },
            new() { Key = "NextNextCR_BilletSpec",  Label = "下下层来料规格", FilterType = "string", Width = "110", GroupKey = 9, GroupName = "冷轧排程(下下层)" },
            new() { Key = "NextNextCR_RollingSpec", Label = "下下层在轧规格", FilterType = "string", Width = "110", GroupKey = 9, GroupName = "冷轧排程(下下层)" },
            new() { Key = "NextNextCR_IsFinished",  Label = "下下层末道",    FilterType = "boolean", Width = "80",  GroupKey = 9, GroupName = "冷轧排程(下下层)" },
            new() { Key = "CR_CompletionType",      Label = "在轧要求",    FilterType = "string",  Width = "90",  GroupKey = 7, GroupName = "冷轧排程(本层匹配)" },
            new() { Key = "CR_RollType",            Label = "待轧要求",    FilterType = "string",  Width = "90",  GroupKey = 8, GroupName = "冷轧排程(下层匹配)" },
            new() { Key = "CR_RollOrder",           Label = "顺序",        FilterType = "string",  Width = "60",  GroupKey = 8, GroupName = "冷轧排程(下层匹配)" },
            new() { Key = "CR_SchedMachineNo",      Label = "待轧设备号",   FilterType = "string", Width = "100", GroupKey = 8, GroupName = "冷轧排程(下层匹配)" },
        };

        // G4: 批次关注
        var g4 = new List<ColumnDef>
        {
            new() { Key = "UrgencyLevel",               Label = "工单紧急性",    SortKey = "UrgencyLevel",               FilterType = "string", Width = "110", GroupKey = 4, GroupName = "批次关注" },
            new() { Key = "ScheduleStage",               Label = "计划状态",     SortKey = "ScheduleStage",               FilterType = "enum", Width = "110", EnumOptions = new() { new("0","工单完成"), new("1","原料锁定"), new("2","生产执行"), new("3","成品检验") }, GroupKey = 4, GroupName = "批次关注" },
            new() { Key = "ProductionAttentionProcess",  Label = "生产关注工序",  SortKey = "ProductionAttentionProcess",  FilterType = "string", Width = "130", GroupKey = 4, GroupName = "批次关注" },
            new() { Key = "ProductionFlowProperty",     Label = "生产流转性",    SortKey = "ProductionFlowProperty",     FilterType = "string", Width = "100", GroupKey = 4, GroupName = "批次关注" },
            new() { Key = "IsKeyBatch",                  Label = "重点生产批次",  FilterType = "boolean", Width = "120", GroupKey = 4, GroupName = "批次关注" },
        };

        // G10：工单需求调整
        var g10 = new List<ColumnDef>
        {
            new() { Key = "IsUrging",              Label = "催单",         FilterType = "boolean", Width = "80",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 10, GroupName = "工单需求调整" },
            new() { Key = "IsBatchDelivery",       Label = "分批交货",     FilterType = "boolean", Width = "90",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 10, GroupName = "工单需求调整" },
            new() { Key = "IsPaused",              Label = "工单暂停",     FilterType = "boolean", Width = "90",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 10, GroupName = "工单需求调整" },
            new() { Key = "AdjustmentRemark",      Label = "调整备注",     FilterType = "string",  Width = "130", GroupKey = 10, GroupName = "工单需求调整" },
        };

        // G11：批次流转
        var g11 = new List<ColumnDef>
        {
            new() { Key = "IsFlow",                Label = "流转",        FilterType = "boolean", Width = "60",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 11, GroupName = "批次流转" },
            new() { Key = "FlowLevel",             Label = "等级",        FilterType = "string",  Width = "60",  GroupKey = 11, GroupName = "批次流转" },
            new() { Key = "FlowTarget",            Label = "流转目标",    FilterType = "string",  Width = "90",  GroupKey = 11, GroupName = "批次流转" },
            new() { Key = "FlowCRType",            Label = "冷轧类型",    FilterType = "string",  Width = "100", GroupKey = 11, GroupName = "批次流转" },
            new() { Key = "FlowExecSpec",          Label = "执行规格",    FilterType = "string",  Width = "120", GroupKey = 11, GroupName = "批次流转" },
            new() { Key = "TargetSequence",        Label = "目标序",      Width = "70",           GroupKey = 11, GroupName = "批次流转" },
        };

        // G12：执行反馈
        var g12 = new List<ColumnDef>
        {
            new() { Key = "OriginalDiff",        Label = "原工量差",   Width = "80",  GroupKey = 12, GroupName = "执行反馈" },
            new() { Key = "CurrentDiff",         Label = "现工量差",   Width = "80",  GroupKey = 12, GroupName = "执行反馈" },
            new() { Key = "IsExecuted",          Label = "是否执行",   FilterType = "boolean", Width = "80",  GroupKey = 12, GroupName = "执行反馈" },
            new() { Key = "IsCompliant",         Label = "达标",       FilterType = "boolean", Width = "70",  GroupKey = 12, GroupName = "执行反馈" },
        };

        // G13：批次计划（持久化，内联编辑）
        var g13 = new List<ColumnDef>
        {
            new() { Key = "PlanIsFlow",              Label = "流转",       FilterType = "boolean", Width = "60",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanFlowLevel",           Label = "等级",       FilterType = "string",  Width = "60",  GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanFlowTarget",          Label = "流转目标",   FilterType = "string",  Width = "90",  GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanFlowCRType",          Label = "冷轧类型",   FilterType = "string",  Width = "100", GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanFlowExecSpec",        Label = "执行规格",   FilterType = "string",  Width = "120", GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanExecutionSequence",   Label = "执行序",     Width = "70",           GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanTargetSequence",      Label = "目标序",     Width = "70",           GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "IsGrabOrder",             Label = "抢单",       FilterType = "boolean", Width = "70",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanRemark",              Label = "计划备注",   FilterType = "string",  Width = "130", GroupKey = 13, GroupName = "批次计划" },
        };

        var all = new List<ColumnDef>();
        all.AddRange(g2);
        all.AddRange(g10);
        all.AddRange(g11);
        all.AddRange(g12);
        all.AddRange(g13);
        all.AddRange(g4);
        all.AddRange(g1);
        all.AddRange(g3);
        all.AddRange(g5);
        return all;
    }

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(BatchPlanDto)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);

        foreach (var col in _visibleColumns.Where(c => _summableColumnKeys.Contains(c.Key)))
        {
            if (!props.TryGetValue(col.Key, out var prop)) continue;

            var type = prop.PropertyType;
            try
            {
                if (type == typeof(int))
                {
                    var sum = _pageItems.Sum(item => (int)(prop.GetValue(item) ?? 0));
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal))
                {
                    var sum = _pageItems.Sum(item => (decimal)(prop.GetValue(item) ?? 0m));
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
                else if (type == typeof(int?))
                {
                    var sum = _pageItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal?))
                {
                    var sum = _pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
            }
            catch { }
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        if (_pageSums.TryGetValue(col.Key, out var sum))
            return sum;
        return "-";
    }

    // ========== 工段筛选 ==========

    private async Task OnSectionTabChanged(string? section)
    {
        _selectedSection = section;
        _columnFilters.Clear();
        await LoadDataAsync();
    }

    // ========== 数据加载（全量模式） ==========

    private async Task LoadDataAsync()
    {
        try
        {
            _allItems = await BatchPlanSvc.GetAllAsync(_selectedSection);
            UpdateTabSummary();
            BuildFilterOptionsFromData();
            if (table != null) await table.ReloadServerData();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _allItems = new();
            ClearTabSummaries();
        }
    }

    private void UpdateTabSummary()
    {
        _tabBatchCount = _allItems.Count;
        _tabTotalWeight = _allItems.Sum(x => x.CurrentValidWeight ?? 0m);
        var keyBatches = _allItems.Where(x => x.IsKeyBatch).ToList();
        _tabKeyBatchCount = keyBatches.Count;
        _tabKeyBatchWeight = keyBatches.Sum(x => x.CurrentValidWeight ?? 0m);
    }

    // ========== 筛选上下文构建（内存数据驱动） ==========

    private void BuildFilterOptionsFromData()
    {
        _filterContextOptions.Clear();

        foreach (var col in _allColumns.Where(c => c.FilterType != null))
        {
            if (col.FilterType == "enum" && col.EnumOptions != null)
            {
                _filterContextOptions[col.Key] = col.EnumOptions.Select(e => new ExcelFilterOption
                {
                    Value = e.Value,
                    Display = e.Display,
                    Count = 0
                }).ToList();
            }
            else if (col.FilterType == "boolean")
            {
                _filterContextOptions[col.Key] = new List<ExcelFilterOption>
                {
                    new() { Value = "True", Display = col.BoolTrueLabel ?? "是", Count = 0 },
                    new() { Value = "False", Display = col.BoolFalseLabel ?? "否", Count = 0 }
                };
            }
            else if (col.FilterType == "string")
            {
                var distinct = _allItems
                    .Select(x => GetFilterValue(x, col.Key))
                    .Where(v => v != null)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(v => v)
                    .Select(v => new ExcelFilterOption { Value = v!, Display = v!, Count = 0 })
                    .ToList();
                _filterContextOptions[col.Key] = distinct;
            }
        }
    }

    private static string? GetFilterValue(BatchPlanDto item, string key) => key switch
    {
        "BatchNo" => item.BatchNo,
        "TagNo" => item.TagNo,
        "PlantGrade" => item.PlantGrade,
        "WorkOrderNo" => item.WorkOrderNo,
        "Salesman" => item.Salesman,
        "DeliveryState" => item.DeliveryState,
        "Specification" => item.Specification,
        "LengthStatus" => item.LengthStatus,
        "CurrentSectionName" => item.CurrentSectionName,
        "PendingProcess" => item.PendingProcess,
        "PendingSectionName" => item.PendingSectionName,
        "PendingSpec" => item.PendingSpec,
        "PendingEquipment" => item.PendingEquipment,
        "UrgencyLevel" => item.UrgencyLevel,
        "ScheduleStage" => item.ScheduleStage.ToString(),
        "ProductionAttentionProcess" => item.ProductionAttentionProcess,
        "ProductionFlowProperty" => item.ProductionFlowProperty,
        "AdjustmentRemark" => item.AdjustmentRemark,
        "FlowLevel" => item.FlowLevel.ToString(),
        "FlowTarget" => item.FlowTarget,
        "FlowCRType" => item.FlowCRType,
        "FlowExecSpec" => item.FlowExecSpec,
        "IsKeyBatch" => item.IsKeyBatch.ToString(),
        "IsFlow" => item.IsFlow.ToString(),
        "IsUrging" => item.IsUrging.ToString(),
        "IsBatchDelivery" => item.IsBatchDelivery.ToString(),
        "IsPaused" => item.IsPaused.ToString(),
        "IsGrabOrder" => item.IsGrabOrder.ToString(),
        "PlanRemark" => item.PlanRemark,
        "PlanIsFlow" => item.PlanIsFlow.ToString(),
        "PlanFlowLevel" => item.PlanFlowLevel.ToString(),
        "PlanFlowTarget" => item.PlanFlowTarget,
        "PlanFlowCRType" => item.PlanFlowCRType,
        "PlanFlowExecSpec" => item.PlanFlowExecSpec,
        "PlanExecutionSequence" => item.PlanExecutionSequence?.ToString(),
        "PlanTargetSequence" => item.PlanTargetSequence?.ToString(),
        "CurrentCR_ProcessType" => item.CurrentCR_ProcessType,
        "CurrentCR_BilletSpec" => item.CurrentCR_BilletSpec,
        "CurrentCR_RollingSpec" => item.CurrentCR_RollingSpec,
        "CurrentCR_IsFinished" => item.CurrentCR_IsFinished.ToString(),
        "NextCR_ProcessType" => item.NextCR_ProcessType,
        "NextCR_BilletSpec" => item.NextCR_BilletSpec,
        "NextCR_RollingSpec" => item.NextCR_RollingSpec,
        "NextCR_IsFinished" => item.NextCR_IsFinished.ToString(),
        "NextNextCR_ProcessType" => item.NextNextCR_ProcessType,
        "NextNextCR_BilletSpec" => item.NextNextCR_BilletSpec,
        "NextNextCR_RollingSpec" => item.NextNextCR_RollingSpec,
        "NextNextCR_IsFinished" => item.NextNextCR_IsFinished.ToString(),
        "CR_CompletionType" => item.CR_CompletionType,
        "CR_RollType" => item.CR_RollType,
        "CR_RollOrder" => item.CR_RollOrder > 0 ? item.CR_RollOrder.ToString() : null,
        "CR_SchedMachineNo" => item.CR_SchedMachineNo,
        "OriginalDiff" => item.OriginalDiff?.ToString(),
        "CurrentDiff" => item.CurrentDiff?.ToString(),
        "IsExecuted" => item.IsExecuted?.ToString(),
        "IsCompliant" => item.IsCompliant?.ToString(),
        _ => null
    };

    // ========== ExcelFilter 事件 ==========

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 列显隐事件 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SavePageStateAsync();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        var idx = _allColumns.IndexOf(col);
        if (idx > 0)
        {
            _allColumns.RemoveAt(idx);
            _allColumns.Insert(idx - 1, col);
        }
        await SavePageStateAsync();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        var idx = _allColumns.IndexOf(col);
        if (idx < _allColumns.Count - 1)
        {
            _allColumns.RemoveAt(idx);
            _allColumns.Insert(idx + 1, col);
        }
        await SavePageStateAsync();
    }

    private async Task ToggleSort(string sortKey)
    {
        if (sortColumn == sortKey)
            sortDescending = !sortDescending;
        else
        {
            sortColumn = sortKey;
            sortDescending = false;
        }
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 分组 CSS ==========

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1",
            2 => "col-g2",
            3 => "col-g3",
            4 => "col-g4",
            5 => "col-g5",
            6 => "col-g6",
            7 => "col-g7",
            8 => "col-g8",
            9 => "col-g9",
            10 => "col-g10",
            11 => "col-g11",
            12 => "col-g12",
            13 => "col-g13",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start";
        return cls;
    }

    private static string GetCellGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1-cell",
            2 => "col-g2-cell",
            3 => "col-g3-cell",
            4 => "col-g4-cell",
            5 => "col-g5-cell",
            6 => "col-g6-cell",
            7 => "col-g7-cell",
            8 => "col-g8-cell",
            9 => "col-g9-cell",
            10 => "col-g10-cell",
            11 => "col-g11-cell",
            12 => "col-g12-cell",
            13 => "col-g13-cell",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    // ========== 分组标题栏 ==========

    private class GroupHeaderInfo
    {
        public int GroupKey { get; init; }
        public string GroupName { get; init; } = "";
        public int TotalWidth { get; init; }
        public int ColumnCount { get; init; }
        public string CssClass { get; init; } = "";
    }

    private List<GroupHeaderInfo> GetGroupHeaders()
    {
        var result = new List<GroupHeaderInfo>();
        int? lastKey = null;
        int totalWidth = 0;
        var groupKey = 0;
        var groupName = "";
        var count = 0;

        foreach (var col in _visibleColumns)
        {
            var gk = col.GroupKey ?? 0;
            if (gk != lastKey && lastKey.HasValue)
            {
                result.Add(new GroupHeaderInfo
                {
                    GroupKey = groupKey,
                    GroupName = groupName,
                    TotalWidth = totalWidth,
                    ColumnCount = count,
                    CssClass = GetHeaderGroupCss(groupKey, true)
                });
                totalWidth = 0;
                count = 0;
            }
            groupKey = gk;
            groupName = col.GroupName ?? "";
            totalWidth += int.TryParse(col.Width, out var w) ? w : 100;
            count++;
            lastKey = gk;
        }
        if (count > 0)
        {
            result.Add(new GroupHeaderInfo
            {
                GroupKey = groupKey,
                GroupName = groupName,
                TotalWidth = totalWidth,
                ColumnCount = count,
                CssClass = GetHeaderGroupCss(groupKey, true)
            });
        }
        return result;
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();

        var savedState = await PageState.LoadAsync("batchplans");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "BatchNo";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = savedState.PageIndex;

            if (savedState.Extras?.ContainsKey("columnVisibility") == true)
            {
                try
                {
                    var raw = savedState.Extras["columnVisibility"];
                    var visibleKeys = JsonSerializer.Deserialize<List<string>>(raw);
                    if (visibleKeys != null)
                    {
                        var visibleSet = new HashSet<string>(visibleKeys);
                        foreach (var col in _allColumns)
                            col.Visible = visibleSet.Contains(col.Key);
                    }
                }
                catch { }
            }

            if (savedState.Extras?.ContainsKey("columnFilters") == true)
            {
                try
                {
                    var raw = savedState.Extras["columnFilters"];
                    var dict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(raw);
                    if (dict != null)
                        _columnFilters = dict.ToDictionary(kv => kv.Key, kv => new HashSet<string>(kv.Value));
                }
                catch { }
            }

            if (savedState.Extras?.ContainsKey("selectedSection") == true)
            {
                _selectedSection = savedState.Extras["selectedSection"];
                if (_selectedSection == "全部") _selectedSection = null;
            }
        }

        await LoadDataAsync();

        if (savedState != null && table != null)
            await table.ReloadServerData();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#batch-plan-list-table");
        }
        catch { }
    }

    // ========== Tab 汇总辅助方法 ==========

    private void ClearTabSummaries()
    {
        _tabBatchCount = 0;
        _tabTotalWeight = 0m;
        _tabKeyBatchCount = 0;
        _tabKeyBatchWeight = 0m;
    }

    // ========== 数据加载（从 _allItems 中筛选+排序+分页） ==========

    private async Task<TableData<BatchPlanDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;

        if (_isFirstLoad)
        {
            state.Page = _restoredPageIndex;
            _isFirstLoad = false;
        }

        // 从 _allItems 中过滤
        var filtered = _allItems.ToList();

        // 1. 关键词搜索
        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            var kw = _searchKeyword;
            filtered = filtered.Where(x =>
                (x.BatchNo != null && x.BatchNo.Contains(kw)) ||
                (x.TagNo != null && x.TagNo.Contains(kw)) ||
                (x.PlantGrade != null && x.PlantGrade.Contains(kw)) ||
                (x.WorkOrderNo != null && x.WorkOrderNo.Contains(kw)) ||
                (x.Salesman != null && x.Salesman.Contains(kw)) ||
                (x.Specification != null && x.Specification.Contains(kw)) ||
                (x.PendingProcess != null && x.PendingProcess.Contains(kw)) ||
                (x.PendingSectionName != null && x.PendingSectionName.Contains(kw)) ||
                (x.UrgencyLevel != null && x.UrgencyLevel.Contains(kw)) ||
                (x.ProductionFlowProperty != null && x.ProductionFlowProperty.Contains(kw)) ||
                (x.ProductionAttentionProcess != null && x.ProductionAttentionProcess.Contains(kw)) ||
                (x.AdjustmentRemark != null && x.AdjustmentRemark.Contains(kw)) ||
                (x.FlowTarget != null && x.FlowTarget.Contains(kw)) ||
                (x.FlowCRType != null && x.FlowCRType.Contains(kw)) ||
                (x.PlanFlowTarget != null && x.PlanFlowTarget.Contains(kw)) ||
                (x.PlanFlowCRType != null && x.PlanFlowCRType.Contains(kw)) ||
                (x.PlanFlowExecSpec != null && x.PlanFlowExecSpec.Contains(kw)) ||
                (x.PlanRemark != null && x.PlanRemark.Contains(kw)) ||
                (x.CR_CompletionType != null && x.CR_CompletionType.Contains(kw)) ||
                (x.CR_RollType != null && x.CR_RollType.Contains(kw)) ||
                (x.CR_SchedMachineNo != null && x.CR_SchedMachineNo.Contains(kw))
            ).ToList();
        }

        // 2. ExcelFilter 列筛选
        if (_columnFilters.Count > 0)
        {
            filtered = filtered.Where(x => _columnFilters.All(f =>
            {
                var val = GetFilterValue(x, f.Key);
                return val != null && f.Value.Contains(val);
            })).ToList();
        }

        _totalCount = filtered.Count;
        _currentPageIndex = state.Page + 1;

        // 3. 排序
        filtered = ApplySorting(filtered, sortColumn, sortDescending);

        // 4. 分页
        var items = filtered
            .Skip(state.Page * state.PageSize)
            .Take(state.PageSize)
            .ToList();

        _pageItems = items;
        ComputePageSums();
        await SavePageStateAsync();
        return new TableData<BatchPlanDto>
        {
            Items = items,
            TotalItems = _totalCount
        };
    }

    private static List<BatchPlanDto> ApplySorting(List<BatchPlanDto> items, string sortBy, bool desc)
    {
        var query = sortBy.ToLower() switch
        {
            "batchno" => items.OrderBy(x => x.BatchNo ?? ""),
            "tagno" => items.OrderBy(x => x.TagNo ?? ""),
            "plantgrade" => items.OrderBy(x => x.PlantGrade ?? ""),
            "currentvalidweight" => items.OrderBy(x => x.CurrentValidWeight),
            "workorderno" => items.OrderBy(x => x.WorkOrderNo ?? ""),
            "salesman" => items.OrderBy(x => x.Salesman ?? ""),
            "deliverydate" => items.OrderBy(x => x.DeliveryDate),
            "deliverystate" => items.OrderBy(x => x.DeliveryState ?? ""),
            "specification" => items.OrderBy(x => x.Specification ?? ""),
            "lengthstatus" => items.OrderBy(x => x.LengthStatus ?? ""),
            "minlength" => items.OrderBy(x => x.MinLength),
            "maxlength" => items.OrderBy(x => x.MaxLength),
            "currentexecdate" => items.OrderBy(x => x.CurrentExecDate),
            "currentsectionname" => items.OrderBy(x => x.CurrentSectionName ?? ""),
            "pendingprocess" => items.OrderBy(x => x.PendingProcess ?? ""),
            "pendingsectionname" => items.OrderBy(x => x.PendingSectionName ?? ""),
            "pendingspec" => items.OrderBy(x => x.PendingSpec ?? ""),
            "pendingequipment" => items.OrderBy(x => x.PendingEquipment ?? ""),
            "currentcr_processtype" => items.OrderBy(x => x.CurrentCR_ProcessType ?? ""),
            "currentcr_billetspec" => items.OrderBy(x => x.CurrentCR_BilletSpec ?? ""),
            "currentcr_rollingspec" => items.OrderBy(x => x.CurrentCR_RollingSpec ?? ""),
            "currentcr_isfinished" => items.OrderBy(x => x.CurrentCR_IsFinished),
            "nextcr_processtype" => items.OrderBy(x => x.NextCR_ProcessType ?? ""),
            "nextcr_billetspec" => items.OrderBy(x => x.NextCR_BilletSpec ?? ""),
            "nextcr_rollingspec" => items.OrderBy(x => x.NextCR_RollingSpec ?? ""),
            "nextcr_isfinished" => items.OrderBy(x => x.NextCR_IsFinished),
            "nextnextcr_processtype" => items.OrderBy(x => x.NextNextCR_ProcessType ?? ""),
            "nextnextcr_billetspec" => items.OrderBy(x => x.NextNextCR_BilletSpec ?? ""),
            "nextnextcr_rollingspec" => items.OrderBy(x => x.NextNextCR_RollingSpec ?? ""),
            "nextnextcr_isfinished" => items.OrderBy(x => x.NextNextCR_IsFinished),
            "cr_completiontype" => items.OrderBy(x => x.CR_CompletionType ?? ""),
            "cr_rolltype" => items.OrderBy(x => x.CR_RollType ?? ""),
            "cr_rollorder" => items.OrderBy(x => x.CR_RollOrder),
            "cr_schedmachineno" => items.OrderBy(x => x.CR_SchedMachineNo ?? ""),
            "urgencylevel" => items.OrderBy(x => x.UrgencyLevel ?? ""),
            "schedulestage" => items.OrderBy(x => x.ScheduleStage),
            "productionattentionprocess" => items.OrderBy(x => x.ProductionAttentionProcess ?? ""),
            "productionflowproperty" => items.OrderBy(x => x.ProductionFlowProperty ?? ""),
            "iskeybatch" => items.OrderBy(x => x.IsKeyBatch),
            "isurging" => items.OrderBy(x => x.IsUrging),
            "isbatchdelivery" => items.OrderBy(x => x.IsBatchDelivery),
            "ispaused" => items.OrderBy(x => x.IsPaused),
            "adjustmentremark" => items.OrderBy(x => x.AdjustmentRemark ?? ""),
            "isflow" => items.OrderBy(x => x.IsFlow),
            "flowlevel" => items.OrderBy(x => x.FlowLevel),
            "flowtarget" => items.OrderBy(x => x.FlowTarget ?? ""),
            "flowcrtype" => items.OrderBy(x => x.FlowCRType ?? ""),
            "flowexecspec" => items.OrderBy(x => x.FlowExecSpec ?? ""),
            "executionsequence" => items.OrderBy(x => x.ExecutionSequence),
            "targetsequence" => items.OrderBy(x => x.TargetSequence),
            "originaldiff" => items.OrderBy(x => x.OriginalDiff),
            "currentdiff" => items.OrderBy(x => x.CurrentDiff),
            "isexecuted" => items.OrderBy(x => x.IsExecuted),
            "iscompliant" => items.OrderBy(x => x.IsCompliant),
            "isgraborder" => items.OrderBy(x => x.IsGrabOrder),
            "planremark" => items.OrderBy(x => x.PlanRemark ?? ""),
            "planisflow" => items.OrderBy(x => x.PlanIsFlow),
            "planflowlevel" => items.OrderBy(x => x.PlanFlowLevel),
            "planflowtarget" => items.OrderBy(x => x.PlanFlowTarget ?? ""),
            "planflowcrtype" => items.OrderBy(x => x.PlanFlowCRType ?? ""),
            "planflowexecspec" => items.OrderBy(x => x.PlanFlowExecSpec ?? ""),
            "planexecutionsequence" => items.OrderBy(x => x.PlanExecutionSequence),
            "plantargetsequence" => items.OrderBy(x => x.PlanTargetSequence),
            _ => items.OrderBy(x => x.BatchNo ?? "")
        };
        return desc ? query.Reverse().ToList() : query.ToList();
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(BatchPlanDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            // G1
            case "BatchNo":
                builder.AddContent(0, item.BatchNo);
                break;
            case "TagNo":
                builder.AddContent(0, item.TagNo ?? "-");
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade);
                break;
            case "CurrentValidWeight":
                builder.AddContent(0, item.CurrentValidWeight.HasValue ? ((int)item.CurrentValidWeight.Value).ToString() : "-");
                break;

            // G2
            case "WorkOrderNo":
                builder.AddContent(0, item.WorkOrderNo);
                break;
            case "Salesman":
                builder.AddContent(0, item.Salesman ?? "-");
                break;
            case "DeliveryDate":
                builder.AddContent(0, item.DeliveryDate.ToString("yyyy-MM-dd"));
                break;
            case "DeliveryState":
                builder.AddContent(0, DisplayHelper.GetDeliveryStateText(item.DeliveryState));
                break;
            case "Specification":
                builder.AddContent(0, item.Specification);
                break;
            case "LengthStatus":
                builder.AddContent(0, DisplayHelper.GetLengthStatusText(item.LengthStatus));
                break;
            case "MinLength":
                builder.AddContent(0, item.MinLength.HasValue ? ((int)item.MinLength.Value).ToString() : "-");
                break;
            case "MaxLength":
                builder.AddContent(0, item.MaxLength.HasValue ? ((int)item.MaxLength.Value).ToString() : "-");
                break;

            // G3
            case "CurrentExecDate":
                builder.AddContent(0, item.CurrentExecDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "CurrentSectionName":
                builder.AddContent(0, item.CurrentSectionName ?? "-");
                break;
            case "PendingProcess":
                builder.AddContent(0, item.PendingProcess ?? "-");
                break;
            case "PendingSectionName":
                builder.AddContent(0, item.PendingSectionName ?? "-");
                break;
            case "PendingSpec":
                builder.AddContent(0, item.PendingSpec ?? "-");
                break;
            case "PendingEquipment":
                builder.AddContent(0, item.PendingEquipment ?? "-");
                break;
            case "ExecutionSequence":
                builder.AddContent(0, item.ExecutionSequence?.ToString() ?? "-");
                break;

            // G4
            case "UrgencyLevel":
                var urgencyColor = item.UrgencyLevel switch
                {
                    "A+急" => Color.Error,
                    "A急" => Color.Warning,
                    "B顺" => Color.Info,
                    "C缓" => Color.Default,
                    "D缓" => Color.Default,
                    "E停" => Color.Default,
                    _ => Color.Default
                };
                if (item.UrgencyLevel != null)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", urgencyColor);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.UrgencyLevel)));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "ScheduleStage":
                var stageColor = item.ScheduleStage switch
                {
                    0 => Color.Default,
                    1 => Color.Warning,
                    2 => Color.Success,
                    3 => Color.Info,
                    _ => Color.Default
                };
                var stageText = item.ScheduleStage switch
                {
                    0 => "工单完成",
                    1 => "原料锁定",
                    2 => "生产执行",
                    3 => "成品检验",
                    _ => "未知"
                };
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", stageColor);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, stageText)));
                builder.CloseComponent();
                break;
            case "ProductionAttentionProcess":
                builder.AddContent(0, item.ScheduleStage == 2
                    ? (item.ProductionAttentionProcess ?? "收尾-成检")
                    : "-");
                break;
            case "IsKeyBatch":
                if (item.IsKeyBatch)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Error);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "是")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "否");
                }
                break;

            // G6: 工单需求调整
            case "IsUrging":
                if (item.IsUrging)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Error);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "是")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "否");
                }
                break;
            case "IsBatchDelivery":
                builder.AddContent(0, item.IsBatchDelivery ? "是" : "否");
                break;
            case "IsPaused":
                if (item.IsPaused)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Warning);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "是")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "否");
                }
                break;
            case "AdjustmentRemark":
                builder.AddContent(0, item.AdjustmentRemark ?? "-");
                break;

            // G11: 批次流转
            case "IsFlow":
                if (item.IsFlow)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Primary);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "是")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "否");
                }
                break;
            case "FlowLevel":
                var levelColor = item.FlowLevel switch
                {
                    1 => Color.Error,
                    2 => Color.Warning,
                    3 => Color.Default,
                    _ => Color.Default
                };
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", levelColor);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.FlowLevel.ToString())));
                builder.CloseComponent();
                break;
            case "FlowTarget":
                builder.AddContent(0, item.FlowTarget ?? "-");
                break;
            case "FlowCRType":
                builder.AddContent(0, item.FlowCRType ?? "-");
                break;
            case "FlowExecSpec":
                builder.AddContent(0, item.FlowExecSpec ?? "-");
                break;
            case "TargetSequence":
                builder.AddContent(0, item.TargetSequence?.ToString() ?? "-");
                break;

            // G5: 冷轧排程
            case "CurrentCR_ProcessType":
                builder.AddContent(0, item.CurrentCR_ProcessType ?? "-");
                break;
            case "CurrentCR_BilletSpec":
                builder.AddContent(0, item.CurrentCR_BilletSpec ?? "-");
                break;
            case "CurrentCR_RollingSpec":
                builder.AddContent(0, item.CurrentCR_RollingSpec ?? "-");
                break;
            case "CurrentCR_IsFinished":
                builder.AddContent(0, item.CurrentCR_IsFinished ? "是" : "否");
                break;
            case "NextCR_ProcessType":
                builder.AddContent(0, item.NextCR_ProcessType ?? "-");
                break;
            case "NextCR_BilletSpec":
                builder.AddContent(0, item.NextCR_BilletSpec ?? "-");
                break;
            case "NextCR_RollingSpec":
                builder.AddContent(0, item.NextCR_RollingSpec ?? "-");
                break;
            case "NextCR_IsFinished":
                builder.AddContent(0, item.NextCR_IsFinished ? "是" : "否");
                break;
            case "NextNextCR_ProcessType":
                builder.AddContent(0, item.NextNextCR_ProcessType ?? "-");
                break;
            case "NextNextCR_BilletSpec":
                builder.AddContent(0, item.NextNextCR_BilletSpec ?? "-");
                break;
            case "NextNextCR_RollingSpec":
                builder.AddContent(0, item.NextNextCR_RollingSpec ?? "-");
                break;
            case "NextNextCR_IsFinished":
                builder.AddContent(0, item.NextNextCR_IsFinished ? "是" : "否");
                break;
            case "CR_CompletionType":
                builder.AddContent(0, string.IsNullOrEmpty(item.CR_CompletionType) || item.CR_CompletionType == "None"
                    ? "-" : DisplayHelper.GetCompletionTypeText(item.CR_CompletionType));
                break;
            case "CR_RollType":
                builder.AddContent(0, string.IsNullOrEmpty(item.CR_RollType) || item.CR_RollType == "None"
                    ? "-" : DisplayHelper.GetRollTypeText(item.CR_RollType));
                break;
            case "CR_RollOrder":
                builder.AddContent(0, item.CR_RollOrder > 0 ? item.CR_RollOrder.ToString() : "-");
                break;
            case "CR_SchedMachineNo":
                builder.AddContent(0, item.CR_SchedMachineNo ?? "-");
                break;

            // G4: 生产流转性
            case "ProductionFlowProperty":
                builder.AddContent(0, item.ProductionFlowProperty ?? "-");
                break;

            // G12: 执行反馈
            case "OriginalDiff":
                builder.AddContent(0, item.OriginalDiff?.ToString() ?? "-");
                break;
            case "CurrentDiff":
                builder.AddContent(0, item.CurrentDiff?.ToString() ?? "-");
                break;
            case "IsExecuted":
                if (item.IsExecuted == true)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Success);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "是")));
                    builder.CloseComponent();
                }
                else if (item.IsExecuted == false)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Error);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "否")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "IsCompliant":
                if (item.IsCompliant == true)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Success);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "是")));
                    builder.CloseComponent();
                }
                else if (item.IsCompliant == false)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Error);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "否")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;

            // G13: 批次计划（持久化，内联编辑）
            case "PlanIsFlow":
                builder.OpenComponent<MudSwitch<bool>>(0);
                builder.AddAttribute(1, "Value", item.PlanIsFlow);
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<bool>(this, async v =>
                {
                    item.PlanIsFlow = v;
                    await SavePlanFieldAsync(item);
                }));
                builder.AddAttribute(3, "Color", Color.Primary);
                builder.AddAttribute(4, "Dense", true);
                builder.CloseComponent();
                break;
            case "PlanFlowLevel":
                builder.OpenComponent<MudNumericField<int>>(0);
                builder.AddAttribute(1, "Value", item.PlanFlowLevel);
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int>(this, async v =>
                {
                    item.PlanFlowLevel = v;
                    await SavePlanFieldAsync(item);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "Min", 1);
                builder.AddAttribute(6, "Max", 3);
                builder.AddAttribute(7, "HideSpinButtons", true);
                builder.AddAttribute(8, "Class", "compact-select");
                builder.CloseComponent();
                break;
            case "PlanFlowTarget":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanFlowTarget ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanFlowTarget = string.IsNullOrEmpty(v) ? null : v;
                    await SavePlanFieldAsync(item);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "Class", "compact-select");
                builder.CloseComponent();
                break;
            case "PlanFlowCRType":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanFlowCRType ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanFlowCRType = string.IsNullOrEmpty(v) ? null : v;
                    await SavePlanFieldAsync(item);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "Class", "compact-select");
                builder.CloseComponent();
                break;
            case "PlanFlowExecSpec":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanFlowExecSpec ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanFlowExecSpec = string.IsNullOrEmpty(v) ? null : v;
                    await SavePlanFieldAsync(item);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "Class", "compact-select");
                builder.CloseComponent();
                break;
            case "PlanExecutionSequence":
                builder.OpenComponent<MudNumericField<int?>>(0);
                builder.AddAttribute(1, "Value", item.PlanExecutionSequence);
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, async v =>
                {
                    item.PlanExecutionSequence = v;
                    await SavePlanFieldAsync(item);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "HideSpinButtons", true);
                builder.AddAttribute(6, "Class", "compact-select");
                builder.CloseComponent();
                break;
            case "PlanTargetSequence":
                builder.OpenComponent<MudNumericField<int?>>(0);
                builder.AddAttribute(1, "Value", item.PlanTargetSequence);
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, async v =>
                {
                    item.PlanTargetSequence = v;
                    await SavePlanFieldAsync(item);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "HideSpinButtons", true);
                builder.AddAttribute(6, "Class", "compact-select");
                builder.CloseComponent();
                break;
            case "IsGrabOrder":
                builder.OpenComponent<MudSwitch<bool>>(0);
                builder.AddAttribute(1, "Value", item.IsGrabOrder);
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<bool>(this, async v =>
                {
                    item.IsGrabOrder = v;
                    await SavePlanFieldAsync(item);
                }));
                builder.AddAttribute(3, "Color", Color.Primary);
                builder.AddAttribute(4, "Dense", true);
                builder.CloseComponent();
                break;
            case "PlanRemark":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanRemark ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanRemark = string.IsNullOrEmpty(v) ? null : v;
                    await SavePlanFieldAsync(item);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "Class", "compact-select");
                builder.CloseComponent();
                break;
        }
    };

    // ========== 计划安排 ==========

    private async Task HandlePlanAllAsync()
    {
        try
        {
            _isPlanning = true;
            StateHasChanged();
            await BatchPlanScheduleSvc.PlanAllAsync(_selectedSection);
            Snackbar.Add("计划安排完成，正在刷新数据...", Severity.Success);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"计划安排失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isPlanning = false;
            StateHasChanged();
        }
    }

    private async Task SavePlanFieldAsync(BatchPlanDto item)
    {
        try
        {
            var dto = new BatchPlanScheduleDto
            {
                BatchId = item.BatchId,
                IsFlow = item.PlanIsFlow,
                FlowLevel = item.PlanFlowLevel,
                FlowTarget = item.PlanFlowTarget,
                FlowCRType = item.PlanFlowCRType,
                FlowExecSpec = item.PlanFlowExecSpec,
                ExecutionSequence = item.PlanExecutionSequence,
                TargetSequence = item.PlanTargetSequence,
                IsGrabOrder = item.IsGrabOrder,
                PlanRemark = item.PlanRemark,
            };
            await BatchPlanScheduleSvc.SaveAsync(dto);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"保存失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));

        extras["columnVisibility"] = JsonSerializer.Serialize(_allColumns.Where(c => c.Visible).Select(c => c.Key).ToList());

        extras["selectedSection"] = _selectedSection ?? "全部";

        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPageIndex,
            Extras = extras
        };
        await PageState.SaveAsync("batchplans", state);
    }
}
