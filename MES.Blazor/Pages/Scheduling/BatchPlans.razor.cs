using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Core.Enums;
using MES.Blazor.Services;
using MES.Core.Models;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.Scheduling;

public partial class BatchPlans
{
    private MudTable<BatchPlanDto>? table;
    private List<BatchPlanDto> _filteredItems = new();
    private int _totalCount;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;
    private bool _isPlanning;
    private HashSet<BatchPlanDto> _selectedItems = new();
    private List<BatchPlanDto> _filteredAllItems = new();

    private void SelectAllItems(bool selected)
    {
        if (selected)
            _selectedItems = new HashSet<BatchPlanDto>(_filteredAllItems);
        else
            _selectedItems.Clear();
    }

    private void ToggleSelection(BatchPlanDto item, bool selected)
    {
        if (selected)
            _selectedItems.Add(item);
        else
            _selectedItems.Remove(item);
    }

    // 排序状态
    private string sortColumn = "BatchNo";
    private bool sortDescending = true;

    // ========== 工段筛选 ==========
    private string? _selectedSection;
    private static readonly string[] _sectionTabs = new[]
    {
        "全部", "60冷轧", "50冷轧", "30冷轧", "20冷轧", "三辊冷轧", "冷拔",
        "油管断", "去油", "固溶", "矫直", "断切", "酸洗", "外抛光", "外点磨",
        "荒管检", "在制检", "成品检验"
    };

    // ========== Tab 汇总数据 ==========
    private int _tabBatchCount;
    private decimal _tabTotalWeight;
    private int _tabFlowBatchCount;
    private decimal _tabFlowBatchWeight;
    private int _tabKeyBatchCount;
    private decimal _tabKeyBatchWeight;

    // ========== 产量目标 + 初始化 ==========
    private Dictionary<string, decimal> _dailyTargets = new();

    private async Task LoadDailyTargetsAsync()
    {
        try
        {
            var targets = await BatchPlanTargetSvc.GetAllAsync();
            foreach (var t in targets)
            {
                if (_dailyTargets.ContainsKey(t.SectionName))
                    _dailyTargets[t.SectionName] = t.DailyTarget;
            }
        }
        catch
        {
            // 加载失败不阻塞页面，保持默认 0
        }
    }

    private async Task SaveDailyTargetsAsync()
    {
        try
        {
            var dtos = _dailyTargets.Select(kv => new BatchPlanTargetDto
            {
                SectionName = kv.Key,
                DailyTarget = kv.Value,
            }).ToList();
            await BatchPlanTargetSvc.SaveAllAsync(dtos);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"保存产量目标失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task OnDailyTargetChanged(string section, decimal value)
    {
        _dailyTargets[section] = value;
        await SaveDailyTargetsAsync();
    }

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    // 全量数据缓存
    private List<BatchPlanDto> _allItems = new();

    // 永久隐藏字段（不在列显隐选择器中显示）
    private static readonly HashSet<string> _permanentlyHiddenColumnKeys = new()
    {
        // G5: 冷轧排程维度明细（12列）
        "CurrentCR_ProcessType", "CurrentCR_BilletSpec", "CurrentCR_RollingSpec", "CurrentCR_IsFinished",
        "NextCR_ProcessType", "NextCR_BilletSpec", "NextCR_RollingSpec", "NextCR_IsFinished",
        "NextNextCR_ProcessType", "NextNextCR_BilletSpec", "NextNextCR_RollingSpec", "NextNextCR_IsFinished",
        // G10: 工单需求调整（4列）
        "IsUrging", "IsBatchDelivery", "IsPaused", "AdjustmentRemark",
    };

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "CurrentValidWeight", "MinLength", "MaxLength",
    };

    /// <summary>
    /// 获取可在列显隐选择器中切换的列（排除永久隐藏字段）
    /// </summary>
    private List<ColumnDef> GetToggleableColumns() =>
        _allColumns.Where(c => !_permanentlyHiddenColumnKeys.Contains(c.Key)).ToList();

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
            new() { Key = "DeliveryState",         Label = "交货状态",   SortKey = "DeliveryState",         FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>(), GroupKey = 2, GroupName = "关联工单", DisplayConverter = v => v is DeliveryState dv ? DisplayHelper.GetDeliveryStateText(dv) : DisplayHelper.GetDeliveryStateText(v as string) },
            new() { Key = "Specification",         Label = "成品规格",   SortKey = "Specification",         FilterType = "string", Width = "120", GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "LengthStatus",          Label = "长度状态",   SortKey = "LengthStatus",          FilterType = "enum", Width = "100", EnumOptions = DisplayHelper.GetEnumFilterOptions<LengthStatus>(), GroupKey = 2, GroupName = "关联工单", DisplayConverter = v => v is LengthStatus ls ? DisplayHelper.GetLengthStatusText(ls) : DisplayHelper.GetLengthStatusText(v as string) },
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

        // G5: 冷轧排程（维度明细默认隐藏，仅保留匹配结果列）
        var g5 = new List<ColumnDef>
        {
            new() { Key = "CurrentCR_ProcessType",  Label = "本层冷轧工序", FilterType = "string",  Width = "110", GroupKey = 5, GroupName = "冷轧排程(本层)", Visible = false },
            new() { Key = "CurrentCR_BilletSpec",   Label = "本层来料规格", FilterType = "string",  Width = "110", GroupKey = 5, GroupName = "冷轧排程(本层)", Visible = false },
            new() { Key = "CurrentCR_RollingSpec",  Label = "本层在轧规格", FilterType = "string",  Width = "110", GroupKey = 5, GroupName = "冷轧排程(本层)", Visible = false },
            new() { Key = "CurrentCR_IsFinished",   Label = "本层末道",    FilterType = "boolean", Width = "80",  GroupKey = 5, GroupName = "冷轧排程(本层)", Visible = false },
            new() { Key = "NextCR_ProcessType",     Label = "下层冷轧工序", FilterType = "string",  Width = "110", GroupKey = 6, GroupName = "冷轧排程(下层)", Visible = false },
            new() { Key = "NextCR_BilletSpec",      Label = "下层来料规格", FilterType = "string",  Width = "110", GroupKey = 6, GroupName = "冷轧排程(下层)", Visible = false },
            new() { Key = "NextCR_RollingSpec",     Label = "下层在轧规格", FilterType = "string",  Width = "110", GroupKey = 6, GroupName = "冷轧排程(下层)", Visible = false },
            new() { Key = "NextCR_IsFinished",      Label = "下层末道",    FilterType = "boolean", Width = "80",  GroupKey = 6, GroupName = "冷轧排程(下层)", Visible = false },
            new() { Key = "NextNextCR_ProcessType", Label = "下下层冷轧工序", FilterType = "string", Width = "110", GroupKey = 9, GroupName = "冷轧排程(下下层)", Visible = false },
            new() { Key = "NextNextCR_BilletSpec",  Label = "下下层来料规格", FilterType = "string", Width = "110", GroupKey = 9, GroupName = "冷轧排程(下下层)", Visible = false },
            new() { Key = "NextNextCR_RollingSpec", Label = "下下层在轧规格", FilterType = "string", Width = "110", GroupKey = 9, GroupName = "冷轧排程(下下层)", Visible = false },
            new() { Key = "NextNextCR_IsFinished",  Label = "下下层末道",    FilterType = "boolean", Width = "80",  GroupKey = 9, GroupName = "冷轧排程(下下层)", Visible = false },
            new() { Key = "CR_CompletionType",      Label = "在轧要求",    FilterType = "enum",   Width = "90",  GroupKey = 7, GroupName = "冷轧排程(本层匹配)", EnumOptions = new() { new("All","全量"), new("Urgent","特急单"), new("Partial2","急单"), new("Partial3","含B顺") }, DisplayConverter = v => DisplayHelper.GetCompletionTypeText(v as string) },
            new() { Key = "CR_RollType",            Label = "待轧要求",    FilterType = "enum",   Width = "90",  GroupKey = 8, GroupName = "冷轧排程(下层匹配)", EnumOptions = new() { new("All","全量"), new("Urgent","特急单"), new("Partial2","急单"), new("Partial3","含B顺") }, DisplayConverter = v => DisplayHelper.GetRollTypeText(v as string) },
            new() { Key = "CR_SchedMachineNo",      Label = "待轧设备号",   FilterType = "string", Width = "100", GroupKey = 8, GroupName = "冷轧排程(下层匹配)" },
        };

        // G4: 批次关注
        var g4 = new List<ColumnDef>
        {
            new() { Key = "UrgencyLevel",               Label = "工单紧急性",    SortKey = "UrgencyLevel",               FilterType = "string", Width = "110", GroupKey = 4, GroupName = "批次关注" },
            new() { Key = "ScheduleStage",               Label = "计划状态",     SortKey = "ScheduleStage",               FilterType = "enum", Width = "110", EnumOptions = new() { new("-1","存错-无此工单"), new("0","工单完成"), new("1","原料锁定"), new("2","生产执行"), new("3","成品检验"), new("4","非工单批次") }, GroupKey = 4, GroupName = "批次关注", DisplayConverter = v => v is int s ? s switch { -1 => "存错-无此工单", 0 => "工单完成", 1 => "原料锁定", 2 => "生产执行", 3 => "成品检验", 4 => "非工单批次", _ => null } : null },
            new() { Key = "MainNoAttentionProcess",             Label = "主号关注工序",   SortKey = "MainNoAttentionProcess",          FilterType = "string", Width = "130", GroupKey = 4, GroupName = "批次关注" },
            new() { Key = "AttentionProcessSectionSequence",    Label = "相应工段序",   SortKey = "AttentionProcessSectionSequence", Width = "100", GroupKey = 4, GroupName = "批次关注" },
            new() { Key = "ProductionFlowProperty",             Label = "生产流转性",    SortKey = "ProductionFlowProperty",           FilterType = "string", Width = "100", GroupKey = 4, GroupName = "批次关注" },
            new() { Key = "IsKeyBatch",                  Label = "重点生产批次",  FilterType = "boolean", Width = "120", GroupKey = 4, GroupName = "批次关注" },
        };

        // G10：工单需求调整（默认隐藏，在工单管理页面编辑）
        var g10 = new List<ColumnDef>
        {
            new() { Key = "IsUrging",              Label = "催单",         FilterType = "boolean", Width = "80",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 10, GroupName = "工单需求调整", Visible = false },
            new() { Key = "IsBatchDelivery",       Label = "分批交货",     FilterType = "boolean", Width = "90",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 10, GroupName = "工单需求调整", Visible = false },
            new() { Key = "IsPaused",              Label = "工单暂停",     FilterType = "boolean", Width = "90",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 10, GroupName = "工单需求调整", Visible = false },
            new() { Key = "AdjustmentRemark",      Label = "调整备注",     FilterType = "string",  Width = "130", GroupKey = 10, GroupName = "工单需求调整", Visible = false },
        };

        // G11：批次流转
        var g11 = new List<ColumnDef>
        {
            new() { Key = "IsFlow",                Label = "流转",        FilterType = "boolean", Width = "60",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 11, GroupName = "批次流转" },
            new() { Key = "FlowLevel",             Label = "等级",        FilterType = "string",  Width = "60",  GroupKey = 11, GroupName = "批次流转" },
            new() { Key = "FlowTarget",            Label = "流转目标",    FilterType = "string",  Width = "90",  GroupKey = 11, GroupName = "批次流转" },
            new() { Key = "FlowCRType",            Label = "冷轧类型",    FilterType = "string",  Width = "100", GroupKey = 11, GroupName = "批次流转" },
            new() { Key = "OuterDiameterSpan",     Label = "外径跨度",    FilterType = "string",  Width = "90",  GroupKey = 11, GroupName = "批次流转" },
            new() { Key = "FlowExecSpec",          Label = "执行规格",    FilterType = "string",  Width = "120", GroupKey = 11, GroupName = "批次流转" },
            new() { Key = "TargetSequence",        Label = "目标序",      Width = "70",           GroupKey = 11, GroupName = "批次流转" },
        };

        // G12：执行反馈
        var g12 = new List<ColumnDef>
        {
            new() { Key = "OriginalDiff",        Label = "原工量差",   Width = "80",  GroupKey = 12, GroupName = "执行反馈" },
            new() { Key = "CurrentDiff",         Label = "现工量差",   Width = "80",  GroupKey = 12, GroupName = "执行反馈" },
            new() { Key = "IsExecuted",          Label = "是否执行",   FilterType = "boolean", Width = "80",  GroupKey = 12, GroupName = "执行反馈" },
            new() { Key = "IsCompliant",         Label = "达标",       FilterType = "enum", Width = "70",  EnumOptions = new() { new("达标","达标"), new("半达标","半达标"), new("未达标","未达标") }, GroupKey = 12, GroupName = "执行反馈", DisplayConverter = v => v as string },
        };

        // G13：批次计划（持久化，内联编辑）
        var g13 = new List<ColumnDef>
        {
            new() { Key = "PlanIsFlow",              Label = "流转",       FilterType = "boolean", Width = "60",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanFlowLevel",           Label = "等级",       FilterType = "string",  Width = "60",  GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanFlowTarget",          Label = "流转目标",   FilterType = "string",  Width = "90",  GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanFlowCRType",          Label = "冷轧类型",   FilterType = "string",  Width = "100", GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanOuterDiameterSpan",   Label = "外径跨度",   FilterType = "string",  Width = "90",  GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanFlowExecSpec",        Label = "执行规格",   FilterType = "string",  Width = "120", GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanExecutionSequence",   Label = "执行序",     Width = "70",           GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanTargetSequence",      Label = "目标序",     Width = "70",           GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "IsGrabOrder",             Label = "抢单",       FilterType = "boolean", Width = "70",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanRemark",              Label = "计划备注",   FilterType = "string",  Width = "130", GroupKey = 13, GroupName = "批次计划" },
        };

        var all = new List<ColumnDef>();
        all.AddRange(g5);
        all.AddRange(g10);
        all.AddRange(g4);
        all.AddRange(g11);
        all.AddRange(g2);
        all.AddRange(g1);
        all.AddRange(g3);
        all.AddRange(g13); // 右侧第2
        all.AddRange(g12); // 最右侧
        return all;
    }

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_filteredItems.Count == 0) return;

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
                    var sum = _filteredItems.Sum(item => (int)(prop.GetValue(item) ?? 0));
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal))
                {
                    var sum = _filteredItems.Sum(item => (decimal)(prop.GetValue(item) ?? 0m));
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
                else if (type == typeof(int?))
                {
                    var sum = _filteredItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal?))
                {
                    var sum = _filteredItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
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
            if (table != null) { ApplyFiltersAndSort(); StateHasChanged(); }
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
        var flowBatches = _allItems.Where(x => x.PlanIsFlow).ToList();
        _tabFlowBatchCount = flowBatches.Count;
        _tabFlowBatchWeight = flowBatches.Sum(x => x.CurrentValidWeight ?? 0m);
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
                    .Select(v => new ExcelFilterOption { Value = v!, Display = col.Key is "CurrentSectionName" or "PendingSectionName" ? SectionDisplayHelper.GetSectionNameText(v!) : v!, Count = 0 })
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
        "DeliveryState" => item.DeliveryState.HasValue ? DisplayHelper.GetDeliveryStateText(item.DeliveryState.Value) : null,
        "Specification" => item.Specification,
        "LengthStatus" => item.LengthStatus.HasValue ? DisplayHelper.GetLengthStatusText(item.LengthStatus.Value) : null,
        "CurrentSectionName" => item.CurrentSectionName,
        "PendingProcess" => item.PendingProcess,
        "PendingSectionName" => item.PendingSectionName,
        "PendingSpec" => item.PendingSpec,
        "PendingEquipment" => item.PendingEquipment,
        "UrgencyLevel" => item.UrgencyLevel,
        "ScheduleStage" => item.ScheduleStage.ToString(),
        "ProductionFlowProperty" => item.ProductionFlowProperty,
        "MaxBatchRemainingWorkDays" => item.MaxBatchRemainingWorkDays?.ToString(),
        "MainNoAttentionProcess" => item.MainNoAttentionProcess,
        "AttentionProcessSectionSequence" => item.AttentionProcessSectionSequence?.ToString(),
        "AdjustmentRemark" => item.AdjustmentRemark,
        "FlowLevel" => item.FlowLevelDisplay,
        "FlowTarget" => item.FlowTarget,
        "FlowCRType" => item.FlowCRType,
        "OuterDiameterSpan" => item.OuterDiameterSpan,
        "FlowExecSpec" => item.FlowExecSpec,
        "IsKeyBatch" => DisplayHelper.GetYesNoText(item.IsKeyBatch),
        "IsFlow" => DisplayHelper.GetYesNoText(item.IsFlow),
        "IsUrging" => DisplayHelper.GetYesNoText(item.IsUrging),
        "IsBatchDelivery" => DisplayHelper.GetYesNoText(item.IsBatchDelivery),
        "IsPaused" => DisplayHelper.GetYesNoText(item.IsPaused),
        "IsGrabOrder" => DisplayHelper.GetYesNoText(item.IsGrabOrder),
        "PlanRemark" => item.PlanRemark,
        "PlanIsFlow" => DisplayHelper.GetYesNoText(item.PlanIsFlow),
        "PlanFlowLevel" => item.PlanFlowLevelDisplay,
        "PlanFlowTarget" => item.PlanFlowTarget,
        "PlanFlowCRType" => item.PlanFlowCRType,
        "PlanOuterDiameterSpan" => item.PlanOuterDiameterSpan,
        "PlanFlowExecSpec" => item.PlanFlowExecSpec,
        "PlanExecutionSequence" => item.PlanExecutionSequence?.ToString(),
        "PlanTargetSequence" => item.PlanTargetSequence?.ToString(),
        "CurrentCR_ProcessType" => item.CurrentCR_ProcessType,
        "CurrentCR_BilletSpec" => item.CurrentCR_BilletSpec,
        "CurrentCR_RollingSpec" => item.CurrentCR_RollingSpec,
        "CurrentCR_IsFinished" => DisplayHelper.GetYesNoText(item.CurrentCR_IsFinished),
        "NextCR_ProcessType" => item.NextCR_ProcessType,
        "NextCR_BilletSpec" => item.NextCR_BilletSpec,
        "NextCR_RollingSpec" => item.NextCR_RollingSpec,
        "NextCR_IsFinished" => DisplayHelper.GetYesNoText(item.NextCR_IsFinished),
        "NextNextCR_ProcessType" => item.NextNextCR_ProcessType,
        "NextNextCR_BilletSpec" => item.NextNextCR_BilletSpec,
        "NextNextCR_RollingSpec" => item.NextNextCR_RollingSpec,
        "NextNextCR_IsFinished" => DisplayHelper.GetYesNoText(item.NextNextCR_IsFinished),
        "CR_CompletionType" => item.CR_CompletionType,
        "CR_RollType" => item.CR_RollType,
        "CR_SchedMachineNo" => item.CR_SchedMachineNo,
        "OriginalDiff" => item.OriginalDiff?.ToString(),
        "CurrentDiff" => item.CurrentDiff?.ToString(),
        "IsExecuted" => item.IsExecuted.HasValue ? DisplayHelper.GetYesNoText(item.IsExecuted.Value) : null,
        "IsCompliant" => item.IsCompliant,
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
        if (table != null) { ApplyFiltersAndSort(); StateHasChanged(); }
    }

    // ========== 列显隐事件 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SavePageStateAsync();
        await SaveColumnPrefs();
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
        await SaveColumnPrefs();
        StateHasChanged();
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
        await SaveColumnPrefs();
        StateHasChanged();
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
        if (table != null) { ApplyFiltersAndSort(); StateHasChanged(); }
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        await SavePageStateAsync();
        if (table != null) { ApplyFiltersAndSort(); StateHasChanged(); }
    }

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) { ApplyFiltersAndSort(); StateHasChanged(); }
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

        // 选择列占位（40px），对齐表格最左侧的 checkbox 列
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0,
            GroupName = "",
            TotalWidth = 40,
            ColumnCount = 0,
            CssClass = ""
        });

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

        // 从 ColumnPrefsService 恢复列顺序和显隐
        var savedPrefs = await ColumnPrefs.LoadAsync("batchplans", null);
        if (savedPrefs.Count > 0)
        {
            foreach (var s in savedPrefs)
            {
                var match = _allColumns.FirstOrDefault(c => c.Key == s.Key);
                if (match != null)
                    match.Visible = s.Visible;
            }
            var reordered = new List<ColumnDef>();
            foreach (var s in savedPrefs)
            {
                var match = _allColumns.FirstOrDefault(c => c.Key == s.Key);
                if (match != null && !reordered.Contains(match))
                    reordered.Add(match);
            }
            foreach (var c in _allColumns)
            {
                if (!reordered.Contains(c))
                    reordered.Add(c);
            }
            _allColumns = reordered;
        }

        // 确保新字段始终可见，并插入到正确位置（兼容旧保存状态不包含这些列）
        foreach (var col in _allColumns)
        {
            if (col.Key is "MaxBatchRemainingWorkDays" or "MainNoAttentionProcess" or "OuterDiameterSpan" or "PlanOuterDiameterSpan" or "ProductionFlowProperty" or "AttentionProcessSectionSequence")
                col.Visible = true;
        }

        // 将新字段移动到正确的组内位置（追加在末尾会导致组显示错乱）
        RepositionNewColumn("OuterDiameterSpan", "FlowCRType");
        RepositionNewColumn("PlanOuterDiameterSpan", "PlanFlowCRType");
        RepositionNewColumn("ProductionFlowProperty", "MainNoAttentionProcess");
        RepositionNewColumn("AttentionProcessSectionSequence", "MainNoAttentionProcess");

        // 从 PageState 恢复排序/筛选状态（列显隐/顺序由 ColumnPrefs 管理）
        var savedState = await PageState.LoadAsync("batchplans");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "BatchNo";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;

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

        // 初始化产量目标
        _dailyTargets = _sectionTabs
            .Where(t => t != "全部")
            .ToDictionary(t => t, t => 0m);

        // 从数据库加载已保存的目标
        await LoadDailyTargetsAsync();

        await LoadDataAsync();
    }

    /// <summary>
    /// 将新字段移动到指定锚点字段之后（兼容旧配置不包含此新字段时被追加到末尾）
    /// </summary>
    private void RepositionNewColumn(string newKey, string afterKey)
    {
        var newCol = _allColumns.FirstOrDefault(c => c.Key == newKey);
        if (newCol == null) return;
        var currentIdx = _allColumns.IndexOf(newCol);
        var anchorIdx = _allColumns.FindLastIndex(c => c.Key == afterKey);
        if (anchorIdx < 0) return;
        if (currentIdx == anchorIdx + 1) return; // 已在正确位置
        _allColumns.RemoveAt(currentIdx);
        var insertIdx = _allColumns.FindLastIndex(c => c.Key == afterKey);
        if (insertIdx >= 0)
            _allColumns.Insert(insertIdx + 1, newCol);
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
        _tabFlowBatchCount = 0;
        _tabFlowBatchWeight = 0m;
        _tabKeyBatchCount = 0;
        _tabKeyBatchWeight = 0m;
    }

    // ========== 数据加载（从 _allItems 中筛选+排序+分页） ==========

    private void ApplyFiltersAndSort()
    {
        // 从 _allItems 中过滤
        var filtered = _allItems.ToList();

        // 1. 关键词搜索
        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            var kw = _searchKeyword;
            filtered = filtered.Where(x =>
                (x.BatchNo != null && x.BatchNo.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.TagNo != null && x.TagNo.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PlantGrade != null && x.PlantGrade.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.WorkOrderNo != null && x.WorkOrderNo.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.Salesman != null && x.Salesman.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.Specification != null && x.Specification.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PendingProcess != null && x.PendingProcess.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PendingSectionName != null && x.PendingSectionName.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.UrgencyLevel != null && x.UrgencyLevel.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.ProductionFlowProperty != null && x.ProductionFlowProperty.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.MainNoAttentionProcess != null && x.MainNoAttentionProcess.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.AdjustmentRemark != null && x.AdjustmentRemark.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.FlowTarget != null && x.FlowTarget.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.FlowCRType != null && x.FlowCRType.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PlanFlowTarget != null && x.PlanFlowTarget.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PlanFlowCRType != null && x.PlanFlowCRType.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PlanFlowExecSpec != null && x.PlanFlowExecSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PlanRemark != null && x.PlanRemark.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.CR_CompletionType != null && x.CR_CompletionType.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.CR_RollType != null && x.CR_RollType.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.CR_SchedMachineNo != null && x.CR_SchedMachineNo.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.DeliveryState != null && DisplayHelper.GetDeliveryStateText(x.DeliveryState.Value).Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.LengthStatus != null && DisplayHelper.GetLengthStatusText(x.LengthStatus.Value).Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.CurrentSectionName != null && x.CurrentSectionName.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PendingSpec != null && x.PendingSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PendingEquipment != null && x.PendingEquipment.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.OuterDiameterSpan != null && x.OuterDiameterSpan.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PlanOuterDiameterSpan != null && x.PlanOuterDiameterSpan.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.FlowExecSpec != null && x.FlowExecSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.CurrentCR_ProcessType != null && x.CurrentCR_ProcessType.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.CurrentCR_BilletSpec != null && x.CurrentCR_BilletSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.CurrentCR_RollingSpec != null && x.CurrentCR_RollingSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.NextCR_ProcessType != null && x.NextCR_ProcessType.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.NextCR_BilletSpec != null && x.NextCR_BilletSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.NextCR_RollingSpec != null && x.NextCR_RollingSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.NextNextCR_ProcessType != null && x.NextNextCR_ProcessType.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.NextNextCR_BilletSpec != null && x.NextNextCR_BilletSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.NextNextCR_RollingSpec != null && x.NextNextCR_RollingSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.IsCompliant != null && x.IsCompliant.Contains(kw, StringComparison.OrdinalIgnoreCase))
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

        // 3. 排序
        filtered = ApplySorting(filtered, sortColumn, sortDescending);

        // 4. 赋全量数据给 _filteredItems（MudTable Items 模式自动分页）
        _filteredAllItems = filtered.ToList();
        _filteredItems = _filteredAllItems;
        _totalCount = _filteredItems.Count;
        ComputePageSums();
    }

    private void OnRowsPerPageChanged(int size)
    {
        _pageSize = size;
        ApplyFiltersAndSort();
        StateHasChanged();
        _ = SavePageStateAsync();
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
            "deliverystate" => items.OrderBy(x => x.DeliveryState.HasValue ? DisplayHelper.GetDeliveryStateText(x.DeliveryState.Value) : ""),
            "specification" => items.OrderBy(x => x.Specification ?? ""),
            "lengthstatus" => items.OrderBy(x => x.LengthStatus.HasValue ? DisplayHelper.GetLengthStatusText(x.LengthStatus.Value) : ""),
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
            "cr_schedmachineno" => items.OrderBy(x => x.CR_SchedMachineNo ?? ""),
            "urgencylevel" => items.OrderBy(x => x.UrgencyLevel ?? ""),
            "schedulestage" => items.OrderBy(x => x.ScheduleStage),
            "productionflowproperty" => items.OrderBy(x => x.ProductionFlowProperty ?? ""),
            "maxbatchremainingworkdays" => items.OrderBy(x => x.MaxBatchRemainingWorkDays),
            "mainnoattentionprocess" => items.OrderBy(x => x.MainNoAttentionProcess ?? ""),
            "attentionprocesssectionsequence" => items.OrderBy(x => x.AttentionProcessSectionSequence),
            "iskeybatch" => items.OrderBy(x => x.IsKeyBatch),
            "isurging" => items.OrderBy(x => x.IsUrging),
            "isbatchdelivery" => items.OrderBy(x => x.IsBatchDelivery),
            "ispaused" => items.OrderBy(x => x.IsPaused),
            "adjustmentremark" => items.OrderBy(x => x.AdjustmentRemark ?? ""),
            "isflow" => items.OrderBy(x => x.IsFlow),
            "flowlevel" => items.OrderBy(x => x.FlowLevel),
            "flowtarget" => items.OrderBy(x => x.FlowTarget ?? ""),
            "flowcrtype" => items.OrderBy(x => x.FlowCRType ?? ""),
            "outerdiameterspan" => items.OrderBy(x => x.OuterDiameterSpan ?? ""),
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
            "planouterdiameterspan" => items.OrderBy(x => x.PlanOuterDiameterSpan ?? ""),
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
                builder.AddContent(0, col.DisplayConverter?.Invoke(item.DeliveryState) as string ?? (item.DeliveryState.HasValue ? DisplayHelper.GetDeliveryStateText(item.DeliveryState.Value) : "-"));
                break;
            case "Specification":
                builder.AddContent(0, item.Specification);
                break;
            case "LengthStatus":
                builder.AddContent(0, col.DisplayConverter?.Invoke(item.LengthStatus) as string ?? (item.LengthStatus.HasValue ? DisplayHelper.GetLengthStatusText(item.LengthStatus.Value) : "-"));
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
                    -1 => Color.Error,
                    0 => Color.Default,
                    1 => Color.Warning,
                    2 => Color.Success,
                    3 => Color.Info,
                    4 => Color.Secondary,
                    _ => Color.Default
                };
                var stageText = item.ScheduleStage switch
                {
                    -1 => "存错-无此工单",
                    0 => "工单完成",
                    1 => "原料锁定",
                    2 => "生产执行",
                    3 => "成品检验",
                    4 => "非工单批次",
                    _ => "未知"
                };
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", stageColor);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, stageText)));
                builder.CloseComponent();
                break;
            case "MainNoAttentionProcess":
                builder.AddContent(0, item.MainNoAttentionProcess ?? "-");
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
                    3 => Color.Info,
                    4 => Color.Default,
                    5 => Color.Default,
                    _ => Color.Default
                };
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", levelColor);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.FlowLevelDisplay)));
                builder.CloseComponent();
                break;
            case "FlowTarget":
                builder.AddContent(0, item.FlowTarget ?? "-");
                break;
            case "FlowCRType":
                builder.AddContent(0, item.FlowCRType ?? "-");
                break;
            case "OuterDiameterSpan":
                builder.AddContent(0, item.OuterDiameterSpan ?? "-");
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
                    ? "-" : col.DisplayConverter?.Invoke(item.CR_CompletionType) ?? item.CR_CompletionType);
                break;
            case "CR_RollType":
                builder.AddContent(0, string.IsNullOrEmpty(item.CR_RollType) || item.CR_RollType == "None"
                    ? "-" : col.DisplayConverter?.Invoke(item.CR_RollType) ?? item.CR_RollType);
                break;
            case "CR_SchedMachineNo":
                builder.AddContent(0, item.CR_SchedMachineNo ?? "-");
                break;

            // G4: 生产流转性
            case "ProductionFlowProperty":
                builder.AddContent(0, item.ProductionFlowProperty ?? "-");
                break;
            case "AttentionProcessSectionSequence":
                builder.AddContent(0, item.AttentionProcessSectionSequence?.ToString() ?? "-");
                break;
            case "MaxBatchRemainingWorkDays":
                builder.AddContent(0, item.MaxBatchRemainingWorkDays.HasValue ? $"{item.MaxBatchRemainingWorkDays}天" : "-");
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
                if (item.IsCompliant == "达标")
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Success);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "达标")));
                    builder.CloseComponent();
                }
                else if (item.IsCompliant == "半达标")
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Warning);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "半达标")));
                    builder.CloseComponent();
                }
                else if (item.IsCompliant == "未达标")
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Error);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "未达标")));
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
                    if (v)
                    {
                        // 切到"是"：从系统推荐值自动填充（FlowLevel/FlowTarget/FlowCRType/外径跨度/FlowExecSpec及执行序/目标序）
                        item.PlanFlowLevel = item.FlowLevel;
                        item.PlanFlowTarget = item.FlowTarget;
                        item.PlanFlowCRType = item.FlowCRType;
                        item.PlanOuterDiameterSpan = item.OuterDiameterSpan;
                        item.PlanFlowExecSpec = item.FlowExecSpec;
                        item.PlanTargetSequence = item.TargetSequence;
                        item.PlanExecutionSequence = item.ExecutionSequence;
                    }
                    else
                    {
                        // 切到"否"：清空所有流转相关字段（含执行序/目标序，使 G12 执行反馈全部显示"-"）
                        item.PlanFlowLevel = 5;
                        item.PlanFlowTarget = null;
                        item.PlanFlowCRType = null;
                        item.PlanOuterDiameterSpan = null;
                        item.PlanFlowExecSpec = null;
                        item.PlanTargetSequence = null;
                        item.PlanExecutionSequence = null;
                    }
                    await SavePlanFieldAsync(item);
                }));
                builder.AddAttribute(3, "Color", Color.Primary);
                builder.AddAttribute(4, "Dense", true);
                builder.CloseComponent();
                break;
            case "PlanFlowLevel":
                if (item.PlanFlowLevel == 1)
                {
                    // 等级1时显示彩色 MudChip + 1A/1B 标识
                    var planLevelColor = Color.Error;
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", planLevelColor);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.PlanFlowLevelDisplay)));
                    builder.CloseComponent();
                }
                else
                {
                    builder.OpenComponent<MudNumericField<int>>(0);
                    builder.AddAttribute(1, "Value", item.PlanFlowLevel);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int>(this, async v =>
                    {
                        item.PlanFlowLevel = v;
                        await SavePlanFieldAsync(item);
                    }));
                    builder.AddAttribute(3, "Dense", true);
                    builder.AddAttribute(4, "Variant", Variant.Outlined);
                    builder.AddAttribute(5, "Size", Size.Small);
                    builder.AddAttribute(6, "Min", 1);
                    builder.AddAttribute(7, "Max", 5);
                    builder.AddAttribute(8, "HideSpinButtons", true);
                    builder.AddAttribute(9, "Class", $"compact-select flow-level-{item.PlanFlowLevel}");
                    builder.CloseComponent();
                }
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
                builder.AddAttribute(4, "Variant", Variant.Outlined);
                builder.AddAttribute(5, "Size", Size.Small);
                builder.AddAttribute(6, "Class", "compact-select");
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
                builder.AddAttribute(4, "Variant", Variant.Outlined);
                builder.AddAttribute(5, "Size", Size.Small);
                builder.AddAttribute(6, "Class", "compact-select");
                builder.CloseComponent();
                break;
            case "PlanOuterDiameterSpan":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanOuterDiameterSpan ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanOuterDiameterSpan = string.IsNullOrEmpty(v) ? null : v;
                    await SavePlanFieldAsync(item);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Outlined);
                builder.AddAttribute(5, "Size", Size.Small);
                builder.AddAttribute(6, "Class", "compact-select");
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
                builder.AddAttribute(4, "Variant", Variant.Outlined);
                builder.AddAttribute(5, "Size", Size.Small);
                builder.AddAttribute(6, "Class", "compact-select");
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
                builder.AddAttribute(4, "Variant", Variant.Outlined);
                builder.AddAttribute(5, "Size", Size.Small);
                builder.AddAttribute(6, "HideSpinButtons", true);
                builder.AddAttribute(7, "Class", "compact-select");
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
                builder.AddAttribute(4, "Variant", Variant.Outlined);
                builder.AddAttribute(5, "Size", Size.Small);
                builder.AddAttribute(6, "HideSpinButtons", true);
                builder.AddAttribute(7, "Class", "compact-select");
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
                builder.AddAttribute(4, "Variant", Variant.Outlined);
                builder.AddAttribute(5, "Size", Size.Small);
                builder.AddAttribute(6, "Class", "compact-select");
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
                PlanOuterDiameterSpan = item.PlanOuterDiameterSpan,
                FlowExecSpec = item.PlanFlowExecSpec,
                ExecutionSequence = item.PlanExecutionSequence,
                TargetSequence = item.PlanTargetSequence,
                IsGrabOrder = item.IsGrabOrder,
                PlanRemark = item.PlanRemark,
            };
            await BatchPlanScheduleSvc.SaveAsync(dto);
            // 保存成功后刷新顶部汇总（流转批次/重点批次计数及重量）
            UpdateTabSummary();
            // 强制 MudTable 重新渲染 RowTemplate，确保 G12 执行反馈重算
            ApplyFiltersAndSort();
            StateHasChanged();
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

        extras["selectedSection"] = _selectedSection ?? "全部";

        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = 1,
            Extras = extras
        };
        await PageState.SaveAsync("batchplans", state);
    }

    // ========== ColumnPrefs 持久化 ==========

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("batchplans", null, _allColumns);
    }

    // ========== 打印 ==========

    private async Task PrintSelected()
    {
        if (_selectedItems.Count == 0)
        {
            Snackbar.Add("请先选择要打印的行", Severity.Warning);
            return;
        }

        try
        {
            var printColumns = _visibleColumns
                .Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label })
                .ToList();

            // 将 DTO 转为字典，枚举字段预先解析为中文显示文本
            var printItems = _selectedItems.Select(item =>
            {
                var dict = new Dictionary<string, object>();
                foreach (var col in _visibleColumns)
                {
                    dict[col.Key] = ResolvePrintValue(item, col);
                }
                return dict;
            }).ToList();

            var request = new BatchPlanPrintRequest
            {
                Title = "批次计划",
                Items = printItems,
                Columns = printColumns
            };

            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/batch-plan/print-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private static object ResolvePrintValue(BatchPlanDto item, ColumnDef col)
    {
        // 枚举列：使用 DisplayConverter
        if (col.DisplayConverter != null)
            return col.DisplayConverter(GetRawPropertyValue(item, col.Key)) ?? "";

        // 布尔列：用 BoolTrueLabel/BoolFalseLabel
        if (col.FilterType == "boolean")
        {
            var raw = GetRawPropertyValue(item, col.Key);
            if (raw is bool b)
                return b ? col.BoolTrueLabel : col.BoolFalseLabel;
            return raw?.ToString() ?? "-";
        }

        return GetRawPropertyValue(item, col.Key);
    }

    private static object GetRawPropertyValue(BatchPlanDto item, string key)
    {
        return (key switch
        {
            "BatchNo" => item.BatchNo ?? "",
            "TagNo" => item.TagNo ?? "",
            "PlantGrade" => item.PlantGrade ?? "",
            "CurrentValidWeight" => item.CurrentValidWeight,
            "WorkOrderNo" => item.WorkOrderNo ?? "",
            "Salesman" => item.Salesman ?? "",
            "DeliveryDate" => item.DeliveryDate,
            "Specification" => item.Specification ?? "",
            "MinLength" => item.MinLength,
            "MaxLength" => item.MaxLength,
            "CurrentExecDate" => item.CurrentExecDate,
            "CurrentSectionName" => item.CurrentSectionName ?? "",
            "PendingProcess" => item.PendingProcess ?? "",
            "PendingSectionName" => item.PendingSectionName ?? "",
            "PendingSpec" => item.PendingSpec ?? "",
            "PendingEquipment" => item.PendingEquipment ?? "",
            "ExecutionSequence" => item.ExecutionSequence,
            "UrgencyLevel" => item.UrgencyLevel ?? "",
            "MainNoAttentionProcess" => item.MainNoAttentionProcess ?? "",
            "AttentionProcessSectionSequence" => item.AttentionProcessSectionSequence,
            "IsKeyBatch" => item.IsKeyBatch,
            "IsUrging" => item.IsUrging,
            "IsBatchDelivery" => item.IsBatchDelivery,
            "IsPaused" => item.IsPaused,
            "AdjustmentRemark" => item.AdjustmentRemark ?? "",
            "IsFlow" => item.IsFlow,
            "FlowLevel" => item.FlowLevel,
            "FlowTarget" => item.FlowTarget ?? "",
            "FlowCRType" => item.FlowCRType ?? "",
            "OuterDiameterSpan" => item.OuterDiameterSpan ?? "",
            "FlowExecSpec" => item.FlowExecSpec ?? "",
            "TargetSequence" => item.TargetSequence,
            "OriginalDiff" => item.OriginalDiff,
            "CurrentDiff" => item.CurrentDiff,
            "IsExecuted" => item.IsExecuted,
            "CurrentCR_ProcessType" => item.CurrentCR_ProcessType ?? "",
            "CurrentCR_BilletSpec" => item.CurrentCR_BilletSpec ?? "",
            "CurrentCR_RollingSpec" => item.CurrentCR_RollingSpec ?? "",
            "CurrentCR_IsFinished" => item.CurrentCR_IsFinished,
            "NextCR_ProcessType" => item.NextCR_ProcessType ?? "",
            "NextCR_BilletSpec" => item.NextCR_BilletSpec ?? "",
            "NextCR_RollingSpec" => item.NextCR_RollingSpec ?? "",
            "NextCR_IsFinished" => item.NextCR_IsFinished,
            "NextNextCR_ProcessType" => item.NextNextCR_ProcessType ?? "",
            "NextNextCR_BilletSpec" => item.NextNextCR_BilletSpec ?? "",
            "NextNextCR_RollingSpec" => item.NextNextCR_RollingSpec ?? "",
            "NextNextCR_IsFinished" => item.NextNextCR_IsFinished,
            "CR_CompletionType" => item.CR_CompletionType ?? "",
            "CR_RollType" => item.CR_RollType ?? "",
            "CR_SchedMachineNo" => item.CR_SchedMachineNo ?? "",
            "PlanIsFlow" => item.PlanIsFlow,
            "PlanFlowLevel" => item.PlanFlowLevel,
            "PlanFlowTarget" => item.PlanFlowTarget ?? "",
            "PlanFlowCRType" => item.PlanFlowCRType ?? "",
            "PlanOuterDiameterSpan" => item.PlanOuterDiameterSpan ?? "",
            "PlanFlowExecSpec" => item.PlanFlowExecSpec ?? "",
            "PlanExecutionSequence" => item.PlanExecutionSequence,
            "PlanTargetSequence" => item.PlanTargetSequence,
            "IsGrabOrder" => item.IsGrabOrder,
            "PlanRemark" => item.PlanRemark ?? "",
            "ProductionFlowProperty" => item.ProductionFlowProperty ?? "",
            "MaxBatchRemainingWorkDays" => item.MaxBatchRemainingWorkDays,
            _ => ""
        })!;
    }
}
