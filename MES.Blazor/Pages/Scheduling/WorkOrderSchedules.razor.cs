using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Models;
using MES.Core.Constants;
using MES.Core.Helpers;
using MES.Core.Enums;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.WorkOrder;
using System.Text.Json;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.Scheduling;

public partial class WorkOrderSchedules
{
    private MudTable<WorkOrderScheduleDto>? table;
    private List<WorkOrderScheduleDto> _allItems = new();
    private List<WorkOrderScheduleDto> _filteredItems = new();

    // 排序状态
    private string sortColumn = "WorkOrderNo";
    private bool sortDescending = true;

    private HashSet<WorkOrderScheduleDto> _selectedItems = new();

    private void SelectAllItems(bool selected)
    {
        if (selected)
            _selectedItems = new HashSet<WorkOrderScheduleDto>(_filteredItems);
        else
            _selectedItems.Clear();
    }

    private void ToggleSelection(WorkOrderScheduleDto item, bool selected)
    {
        if (selected)
            _selectedItems.Add(item);
        else
            _selectedItems.Remove(item);
    }

    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "TotalItemCount", "TotalQuantity", "TotalMeters", "TotalWeight",
        "FlowTotalBatchCount", "FlowIncompleteBatchCount",
        "PendingSectionRoughTube", "PendingSectionWarehouseFix",
        "PendingSection60Roll", "PendingSection50Roll",
        "PendingSection30Roll", "PendingSection20Roll",
        "PendingSectionThreeRoll", "PendingSectionDrawBench",
    };
    private int _lastSummedPage = -1;
    private int _lastSummedCount = -1;
    private int _lastSummedPageSize = -1;

    // ========== 字典下拉选项（配置表动态加载，失败兜底静态 KeyToChinese）==========
    private List<(string Value, string Text)> _urgencyOptions =
        UrgencyLevelKeys.KeyToChinese.Select(kv => (kv.Key, kv.Value)).ToList();
    private List<(string Value, string Text)> _productionFlowOptions =
        ProductionFlowKeys.KeyToChinese.Select(kv => (kv.Key, kv.Value)).ToList();

    private async Task LoadDictOptionsAsync()
    {
        var urgency = await DictValueDefinitionService.GetEnabledValuesAsync(DictValueDefaults.UrgencyLevelKey);
        if (urgency.Success && urgency.Data is { Count: > 0 })
            _urgencyOptions = urgency.Data.Select(t => (t.Value, t.DisplayName)).ToList();

        var flow = await DictValueDefinitionService.GetEnabledValuesAsync(DictValueDefaults.ProductionFlowKey);
        if (flow.Success && flow.Data is { Count: > 0 })
            _productionFlowOptions = flow.Data.Select(t => (t.Value, t.DisplayName)).ToList();
    }

    // 非空/空筛选常量
    private const string FilterNotNull = "__NOT_NULL__";
    private const string FilterNull = "__EXCEL_FILTER_NULL__";

    // Plan 字段键名（G15 工单计划），用于空值筛选
    private static readonly HashSet<string> _planFieldKeys = new()
    {
        "PlanScheduleStage", "PlanUrgencyLevel",
        "PlanProductionAttentionProcess", "PlanProductionFlowProperty"
    };

    private static List<ColumnDef> GetAllColumnDefs()
    {
        // G1: 工单基础数据（顺序/显隐对齐工单执行状况）
        var g1 = new List<ColumnDef>
        {
            new() { Key = "WorkOrderNo",             Label = "工单号",          SortKey = "WorkOrderNo",             FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "Salesman",                Label = "业务员",          SortKey = "Salesman",                FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "CustomerName",            Label = "往来单位",        SortKey = "CustomerName",            FilterType = "string", Width = "120", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "EndCustomer",             Label = "最终客户",        SortKey = "EndCustomer",             FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SignDate",                Label = "订单日期",        SortKey = "SignDate",                FilterType = "date", Width = "120", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DeliveryDate",            Label = "交货日期",        SortKey = "DeliveryDate",            FilterType = "date", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DelayPenalty",            Label = "延期罚款",        SortKey = "DelayPenalty",            FilterType = "boolean", Width = "120", BoolTrueLabel = "是", BoolFalseLabel = "否", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SettlementMethod",        Label = "结算方式",        SortKey = "SettlementMethod",        FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetEnumFilterOptions<SettlementMethod>(), Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SalesOrderNo",            Label = "订单号",          SortKey = "SalesOrderNo",            FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionMainNo",        Label = "主号",            SortKey = "ProductionMainNo",        FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionSubNo",         Label = "次号",            SortKey = "ProductionSubNo",         FilterType = "string", Width = "120", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MaterialName",            Label = "钢管制造",        SortKey = "MaterialName",            FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetEnumFilterOptions<PipeManufacturingType>(), Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DeliveryState",           Label = "交货状态",        SortKey = "DeliveryState",           FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>(), GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "PlantGrade",              Label = "工厂牌号",        SortKey = "PlantGrade",              FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "Specification",           Label = "规格",            SortKey = "Specification",           FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "LengthStatus",            Label = "长度状态",        SortKey = "LengthStatus",            FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetEnumFilterOptions<LengthStatus>(), GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MinLength",               Label = "最小长度",        SortKey = "MinLength",               FilterType = "number", Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MaxLength",               Label = "最大长度",        SortKey = "MaxLength",               FilterType = "number", Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalItemCount",          Label = "含项次数",        SortKey = "TotalItemCount",          FilterType = "number", Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalQuantity",           Label = "总支数",          SortKey = "TotalQuantity",           FilterType = "number", Width = "80", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalMeters",             Label = "总米数",          SortKey = "TotalMeters",             FilterType = "number", Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalWeight",             Label = "总重量",          SortKey = "TotalWeight",             FilterType = "number", Width = "80", GroupKey = 1, GroupName = "基础数据" },
        };

        // G13: 实际生产总流转（整组默认隐藏）
        var g7 = new List<ColumnDef>
        {
            new() { Key = "MainNoFlowStatus",       Label = "主号-流转状态",  SortKey = "MainNoFlowStatus",       FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetMainNoFlowStatusOptions(), Visible = false, GroupKey = 13, GroupName = "实际生产总流转", Level = ColumnLevel.MainNo },
            new() { Key = "MainNoFlowOutputRatio",  Label = "主号-流转比",    SortKey = "MainNoFlowOutputRatio",   FilterType = "number", Width = "80", Visible = false, GroupKey = 13, GroupName = "实际生产总流转", Level = ColumnLevel.MainNo },
            new() { Key = "FlowStatus",             Label = "工单流转状态",   SortKey = "FlowStatus",             FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetFlowStatusOptions(), Visible = false, GroupKey = 13, GroupName = "实际生产总流转" },
            new() { Key = "FlowOutputRatio",        Label = "工单流转比",     SortKey = "FlowOutputRatio",        FilterType = "number", Width = "80", Visible = false, GroupKey = 13, GroupName = "实际生产总流转" },
            new() { Key = "FlowTotalBatchCount",    Label = "总批次数",       SortKey = "FlowTotalBatchCount",    FilterType = "number", Width = "80", Visible = false, GroupKey = 13, GroupName = "实际生产总流转" },
            new() { Key = "FlowIncompleteBatchCount",Label = "未完成批数",    SortKey = "FlowIncompleteBatchCount",FilterType = "number", Width = "80", Visible = false, GroupKey = 13, GroupName = "实际生产总流转" },
            new() { Key = "FlowMaxRemainingWorkDays", Label = "最大剩余工量(天)",SortKey = "FlowMaxRemainingWorkDays", FilterType = "number", Width = "80", Visible = false, GroupKey = 13, GroupName = "实际生产总流转" },
        };

        // G3: 实时关注（整组默认隐藏，顺序/显隐/标签对齐工单执行状况，整组主号级）
        var g12 = new List<ColumnDef>
        {
            new() { Key = "ScheduleStage",           Label = "主号-关注",     SortKey = "ScheduleStage",           FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetScheduleStageOptions(), Visible = false, GroupKey = 3, GroupName = "实时关注", Level = ColumnLevel.MainNo },
            new() { Key = "UrgencyLevel",            Label = "主号-计划性",   SortKey = "UrgencyLevel",            FilterType = "string", Width = "120", Visible = false, GroupKey = 3, GroupName = "实时关注", Level = ColumnLevel.MainNo },
            new() { Key = "EstimatedProcessCompletionDate",Label = "主号-预计完成日",SortKey = "EstimatedProcessCompletionDate", FilterType = "date", Width = "120", Visible = false, GroupKey = 3, GroupName = "实时关注", Level = ColumnLevel.MainNo },
            new() { Key = "DaysDiffFromDelivery",    Label = "主号-交期相差天数",SortKey = "DaysDiffFromDelivery",  FilterType = "number", Width = "80", Visible = false, GroupKey = 3, GroupName = "实时关注", Level = ColumnLevel.MainNo },
            new() { Key = "TotalRemainingWorkDays",  Label = "主号-剩余总工量(天)",SortKey = "TotalRemainingWorkDays",  FilterType = "number", Width = "80", Visible = false, GroupKey = 3, GroupName = "实时关注", Level = ColumnLevel.MainNo },
            new() { Key = "CapacityWorkDays",        Label = "主号-产能工量(天)",SortKey = "CapacityWorkDays",      FilterType = "number", Width = "80", Visible = false, GroupKey = 3, GroupName = "实时关注", Level = ColumnLevel.MainNo },
            new() { Key = "RawMaterialLockRemark",   Label = "主号-原锁备注", SortKey = "RawMaterialLockRemark",   FilterType = "string", Width = "120", Visible = false, GroupKey = 3, GroupName = "实时关注", Level = ColumnLevel.MainNo },
        };

        // G2: 工单需求调整（整组默认隐藏，对齐工单执行状况）
        var g13 = new List<ColumnDef>
        {
            new() { Key = "IsUrging",      Label = "催单",  SortKey = "IsUrging",      FilterType = "boolean", Width = "80", BoolTrueLabel = "是", BoolFalseLabel = "否", Visible = false, GroupKey = 2, GroupName = "工单需求调整" },
            new() { Key = "IsBatchDelivery",          Label = "分批交货",      SortKey = "IsBatchDelivery",          FilterType = "boolean", Width = "80", BoolTrueLabel = "是", BoolFalseLabel = "否", Visible = false, GroupKey = 2, GroupName = "工单需求调整" },
            new() { Key = "IsPaused",                  Label = "暂停",          SortKey = "IsPaused",                  FilterType = "boolean", Width = "80", BoolTrueLabel = "是", BoolFalseLabel = "否", Visible = false, GroupKey = 2, GroupName = "工单需求调整" },
            new() { Key = "AdjustmentRemark",         Label = "调整备注",      SortKey = "AdjustmentRemark",         FilterType = "string", Width = "120", Visible = false, GroupKey = 2, GroupName = "工单需求调整" },
        };

        // G18: 在产节点待量（整组默认隐藏）
        var g14 = new List<ColumnDef>
        {
            new() { Key = "MainNoAttentionProcess",      Label = "主号-关注工序",   SortKey = "MainNoAttentionProcess",    FilterType = "string", Width = "120", Visible = false, GroupKey = 18, GroupName = "在产节点待量", Level = ColumnLevel.MainNo },
            new() { Key = "PendingSectionRoughTube",       Label = "荒管处理待量(kg)",   SortKey = "PendingSectionRoughTube",       FilterType = "number", Width = "90",  Visible = false, GroupKey = 18, GroupName = "在产节点待量" },
            new() { Key = "PendingSectionWarehouseFix",    Label = "在制修检待量(kg)",   SortKey = "PendingSectionWarehouseFix",    FilterType = "number", Width = "90",  Visible = false, GroupKey = 18, GroupName = "在产节点待量" },
            new() { Key = "PendingSection60Roll",          Label = "60冷轧待量(kg)",     SortKey = "PendingSection60Roll",          FilterType = "number", Width = "90",  Visible = false, GroupKey = 18, GroupName = "在产节点待量" },
            new() { Key = "PendingSection50Roll",          Label = "50冷轧待量(kg)",     SortKey = "PendingSection50Roll",          FilterType = "number", Width = "90",  Visible = false, GroupKey = 18, GroupName = "在产节点待量" },
            new() { Key = "PendingSection30Roll",          Label = "30冷轧待量(kg)",     SortKey = "PendingSection30Roll",          FilterType = "number", Width = "90",  Visible = false, GroupKey = 18, GroupName = "在产节点待量" },
            new() { Key = "PendingSection20Roll",          Label = "20冷轧待量(kg)",     SortKey = "PendingSection20Roll",          FilterType = "number", Width = "90",  Visible = false, GroupKey = 18, GroupName = "在产节点待量" },
            new() { Key = "PendingSectionThreeRoll",       Label = "三辊冷轧待量(kg)",   SortKey = "PendingSectionThreeRoll",       FilterType = "number", Width = "90",  Visible = false, GroupKey = 18, GroupName = "在产节点待量" },
            new() { Key = "PendingSectionDrawBench",       Label = "冷拔待量(kg)",       SortKey = "PendingSectionDrawBench",       FilterType = "number", Width = "90",  Visible = false, GroupKey = 18, GroupName = "在产节点待量" },
            new() { Key = "DeformedProcessCompleted",      Label = "变形工序完成",       SortKey = "DeformedProcessCompleted",      FilterType = "boolean", Width = "100", Visible = false, BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 18, GroupName = "在产节点待量" },
            new() { Key = "ProductionAttentionProcess",    Label = "生产关注工序",       SortKey = "ProductionAttentionProcess",    FilterType = "string", Width = "100", Visible = false, GroupKey = 18, GroupName = "在产节点待量" },
            new() { Key = "ProductionFlowProperty",        Label = "生产流转性",         SortKey = "ProductionFlowProperty",        FilterType = "string", Width = "100", Visible = false, GroupKey = 18, GroupName = "在产节点待量" },
            new() { Key = "MaxBatchRemainingWorkDays",   Label = "最大剩余工量(天)", SortKey = "MaxBatchRemainingWorkDays", FilterType = "number", Width = "80",  Visible = false, GroupKey = 18, GroupName = "在产节点待量" },
        };

        // G15: 工单计划（薄表 — 手工可编辑）
        var g15 = new List<ColumnDef>
        {
            new() { Key = "ConsistencyStatus",              Label = "实时一致性",  SortKey = "ConsistencyStatus",          FilterType = "enum", Width = "100", EnumOptions = new() { new("一致","一致"), new("进度调整","进度调整"), new("错误","错误") }, GroupKey = 15, GroupName = "工单计划" },
            new() { Key = "PlanScheduleStage",               Label = "工单状态",     SortKey = "PlanScheduleStage",          FilterType = "enum", Width = "100", EnumOptions = DisplayHelper.GetPlanScheduleStageOptions(), GroupKey = 15, GroupName = "工单计划" },
            new() { Key = "PlanUrgencyLevel",                Label = "紧急性",       SortKey = "PlanUrgencyLevel",           FilterType = "string", Width = "100", GroupKey = 15, GroupName = "工单计划" },
            new() { Key = "PlanProductionAttentionProcess",  Label = "生产关注",     SortKey = "PlanProductionAttentionProcess", FilterType = "string", Width = "120", GroupKey = 15, GroupName = "工单计划" },
            new() { Key = "PlanProductionFlowProperty",      Label = "流转性",       SortKey = "PlanProductionFlowProperty",  FilterType = "string", Width = "100", GroupKey = 15, GroupName = "工单计划" },
        };

        var all = new List<ColumnDef>();
        all.AddRange(g1);   // G1 基础数据
        all.AddRange(g13);  // G2 工单需求调整
        all.AddRange(g12);  // G3 实时关注
        all.AddRange(g7);   // G13 实际生产总流转
        all.AddRange(g14);  // G18 在产节点待量
        all.AddRange(g15);  // G15 工单计划
        return all;
    }

    // ========== 分页汇总 ==========

    /// <summary>
    /// 渲染期惰性重算当前页汇总。⚠️ MudBlazor 6.19.1 没有 CurrentPageChanged 事件（旧绑定被
    /// CaptureUnmatchedValues 静默吞掉、永不触发）；翻页只触发 MudTable 自身 StateHasChanged，
    /// 不触发父组件 OnAfterRenderAsync。故在 FooterContent 渲染时按「页码/每页行数/数据量」签名惰性重算，
    /// 保证页脚汇总与实际显示行一致。
    /// </summary>
    private void EnsurePageSumsComputed()
    {
        var page = table?.CurrentPage ?? 0;
        var rowsPerPage = table?.RowsPerPage ?? _pageSize;
        if (rowsPerPage <= 0) rowsPerPage = _pageSize;
        var count = _filteredItems.Count;
        if (page == _lastSummedPage && count == _lastSummedCount && rowsPerPage == _lastSummedPageSize)
            return;
        _lastSummedPage = page;
        _lastSummedCount = count;
        _lastSummedPageSize = rowsPerPage;
        ComputePageSums();
    }

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_filteredItems.Count == 0) return;

        // 按当前页显示行汇总（Items 模式，取 MudTable 当前页切片）
        var page = table?.CurrentPage ?? 0;
        var rowsPerPage = table?.RowsPerPage ?? _pageSize;
        if (rowsPerPage <= 0) rowsPerPage = _pageSize;
        var pageItems = _filteredItems.Skip(page * rowsPerPage).Take(rowsPerPage).ToList();
        if (pageItems.Count == 0) return;

        var props = typeof(WorkOrderScheduleDto)
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
                    var sum = pageItems.Sum(item => (int)(prop.GetValue(item) ?? 0));
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal))
                {
                    var sum = pageItems.Sum(item => (decimal)(prop.GetValue(item) ?? 0m));
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
                else if (type == typeof(int?))
                {
                    var sum = pageItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal?))
                {
                    var sum = pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
            }
            catch
            {
                // ignore individual column sum errors
            }
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        if (_pageSums.TryGetValue(col.Key, out var sum))
            return sum;
        return "-";
    }

    // ========== 数据加载 ==========

    private async Task LoadDataAsync()
    {
        try
        {
            var query = new QueryParams
            {
                PageIndex = 1,
                PageSize = 50000,
                SortBy = "WorkOrderNo",
                IsDescending = false
            };
            var result = await WorkOrderScheduleSvc.GetPagedAsync(query);
            if (result.Success && result.Data != null)
            {
                _allItems = result.Data.Items ?? new();
            }
            else
            {
                _allItems = new();
                Snackbar.Add(result?.Message ?? "获取数据失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _allItems = new();
        }

        BuildFilterContextOptions();
        ApplyFiltersAndSort();
    }

    // ========== 筛选上下文构建 ==========

    private void BuildFilterContextOptions()
    {
        _filterContextOptions.Clear();

        foreach (var col in _allColumns)
        {
            if (col.FilterType == "number")
            {
                _filterContextOptions[col.Key] = new List<ExcelFilterOption>
                {
                    new() { Value = FilterNotNull, Display = "非空", Count = _allItems.Count(x => GetFilterValue(x, col.Key) != null) },
                    new() { Value = FilterNull,    Display = "空",   Count = _allItems.Count(x => GetFilterValue(x, col.Key) == null) },
                };
            }
            else if (col.FilterType == "string")
            {
                var options = _allItems
                    .Select(item => GetFilterValue(item, col.Key))
                    .Where(v => v != null && v != FilterNull)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .Select(val => new ExcelFilterOption
                    {
                        Value = val!,
                        Display = (col.Key switch
                        {
                            "SectionName" or "CurrentSectionName" or "NextSectionName" or "PendingSectionName" => SectionDisplayHelper.GetSectionNameText(val!),
                            "ProcessName" or "ProcessGroupName" or "CurrentGroupName" or "NextProcess" or "PendingProcess" or "ProductionAttentionProcess" or "MainNoAttentionProcess" or "PlanProductionAttentionProcess" => ProcessDisplayHelper.GetProcessNameText(val!),
                            "UrgencyLevel" or "PlanUrgencyLevel" => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey,val!),
                            "ProductionFlowProperty" or "PlanProductionFlowProperty" => DictValueDisplayHelper.GetText(DictValueDefaults.ProductionFlowKey,val!),
                            "RawMaterialLockRemark" => DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, val!),
                            _ => val!
                        }) ?? val!,
                        Count = _allItems.Count(x => string.Equals(GetFilterValue(x, col.Key), val, StringComparison.OrdinalIgnoreCase))
                    })
                    .ToList();

                // Plan 字段增加"空值"筛选选项
                if (_planFieldKeys.Contains(col.Key))
                {
                    var nullCount = _allItems.Count(x => GetFilterValue(x, col.Key) == FilterNull);
                    options.Insert(0, new ExcelFilterOption
                    {
                        Value = FilterNull,
                        Display = "-",
                        Count = nullCount
                    });
                }

                _filterContextOptions[col.Key] = options;
            }
            else if (col.FilterType == "enum")
            {
                if (col.EnumOptions != null)
                {
                    var options = col.EnumOptions.Select(e => new ExcelFilterOption
                    {
                        Value = e.Value,
                        Display = e.Display,
                        Count = _allItems.Count(x => string.Equals(GetFilterValue(x, col.Key), e.Value, StringComparison.OrdinalIgnoreCase))
                    }).ToList();

                    // Plan 字段增加"空值"筛选选项
                    if (_planFieldKeys.Contains(col.Key))
                    {
                        var nullCount = _allItems.Count(x => GetFilterValue(x, col.Key) == FilterNull);
                        options.Insert(0, new ExcelFilterOption
                        {
                            Value = FilterNull,
                            Display = "-",
                            Count = nullCount
                        });
                    }

                    _filterContextOptions[col.Key] = options;
                }
            }
            else if (col.FilterType == "boolean")
            {
                _filterContextOptions[col.Key] = new List<ExcelFilterOption>
                {
                    new() { Value = "True", Display = col.BoolTrueLabel ?? "是", Count = _allItems.Count(x => GetFilterValue(x, col.Key) == "True") },
                    new() { Value = "False", Display = col.BoolFalseLabel ?? "否", Count = _allItems.Count(x => GetFilterValue(x, col.Key) == "False") },
                };
            }
        }
    }

    private static string? GetFilterValue(WorkOrderScheduleDto item, string key) => key switch
    {
        "WorkOrderNo" => item.WorkOrderNo,
        "Salesman" => item.Salesman,
        "CustomerName" => item.CustomerName,
        "EndCustomer" => item.EndCustomer,
        "SalesOrderNo" => item.SalesOrderNo,
        "ProductionMainNo" => item.ProductionMainNo,
        "ProductionSubNo" => item.ProductionSubNo,
        "PlantGrade" => item.PlantGrade,
        "Specification" => item.Specification,
        "MinLength" => item.MinLength?.ToString(),
        "MaxLength" => item.MaxLength?.ToString(),
        "TotalItemCount" => item.TotalItemCount.ToString(),
        "TotalQuantity" => item.TotalQuantity.ToString(),
        "TotalMeters" => item.TotalMeters.ToString(),
        "TotalWeight" => item.TotalWeight.ToString(),
        "SettlementMethod" => DisplayHelper.GetSettlementMethodText(item.SettlementMethod),
        "MaterialName" => item.MaterialName,
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState),
        "LengthStatus" => DisplayHelper.GetLengthStatusText(item.LengthStatus),
        "FlowStatus" => item.FlowStatus.ToString(),
        "MainNoFlowStatus" => item.MainNoFlowStatus.ToString(),
        "FlowOutputRatio" => item.FlowOutputRatio.ToString(),
        "MainNoFlowOutputRatio" => item.MainNoFlowOutputRatio.ToString(),
        "FlowTotalBatchCount" => item.FlowTotalBatchCount.ToString(),
        "FlowIncompleteBatchCount" => item.FlowIncompleteBatchCount.ToString(),
        "FlowMaxRemainingWorkDays" => item.FlowMaxRemainingWorkDays.ToString(),
        "ScheduleStage" => item.ScheduleStage.ToString(),
        "UrgencyLevel" => item.UrgencyLevel,
        "RawMaterialLockRemark" => item.RawMaterialLockRemark,
        "TotalRemainingWorkDays" => item.TotalRemainingWorkDays?.ToString(),
        "CapacityWorkDays" => item.CapacityWorkDays?.ToString(),
        "DaysDiffFromDelivery" => item.DaysDiffFromDelivery?.ToString(),
        "AdjustmentRemark" => item.AdjustmentRemark,
        "DelayPenalty" => item.DelayPenalty ? "True" : "False",
        "IsUrging" => item.IsUrging ? "True" : "False",
        "IsBatchDelivery" => item.IsBatchDelivery ? "True" : "False",
        "IsPaused" => item.IsPaused ? "True" : "False",
        "DeformedProcessCompleted" => item.DeformedProcessCompleted switch { true => "True", false => "False", null => null },
        "PendingSectionRoughTube" => item.PendingSectionRoughTube?.ToString(),
        "PendingSectionWarehouseFix" => item.PendingSectionWarehouseFix?.ToString(),
        "PendingSection60Roll" => item.PendingSection60Roll?.ToString(),
        "PendingSection50Roll" => item.PendingSection50Roll?.ToString(),
        "PendingSection30Roll" => item.PendingSection30Roll?.ToString(),
        "PendingSection20Roll" => item.PendingSection20Roll?.ToString(),
        "PendingSectionThreeRoll" => item.PendingSectionThreeRoll?.ToString(),
        "PendingSectionDrawBench" => item.PendingSectionDrawBench?.ToString(),
        "ProductionAttentionProcess" => item.ProductionAttentionProcess,
        "ProductionFlowProperty" => item.ProductionFlowProperty,
        "MaxBatchRemainingWorkDays" => item.MaxBatchRemainingWorkDays?.ToString(),
        "MainNoAttentionProcess" => item.MainNoAttentionProcess,
        "ConsistencyStatus" => item.ConsistencyStatus,
        "PlanScheduleStage" => item.PlanScheduleStage?.ToString() ?? FilterNull,
        "PlanUrgencyLevel" => item.PlanUrgencyLevel ?? FilterNull,
        "PlanProductionAttentionProcess" => item.PlanProductionAttentionProcess ?? FilterNull,
        "PlanProductionFlowProperty" => item.PlanProductionFlowProperty ?? FilterNull,
        _ => null
    };

    // ========== 搜索/筛选/排序 ==========

    private void ApplyFiltersAndSort()
    {
        var query = _allItems.AsEnumerable();

        // 关键字搜索
        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            var kw = _searchKeyword.Trim();
            query = query.Where(x =>
                (x.WorkOrderNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.SalesOrderNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.Salesman?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.CustomerName?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.EndCustomer?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.PlantGrade?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.Specification?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.ProductionMainNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.ProductionSubNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (DisplayHelper.GetSettlementMethodText(x.SettlementMethod).Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.MaterialName?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (DisplayHelper.GetDeliveryStateText(x.DeliveryState).Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (DisplayHelper.GetLengthStatusText(x.LengthStatus).Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.UrgencyLevel?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.RawMaterialLockRemark?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.AdjustmentRemark?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.ProductionAttentionProcess?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.ProductionFlowProperty?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.MainNoAttentionProcess?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true));
        }

        // 列筛选
        foreach (var kvp in _columnFilters)
        {
            if (kvp.Value.Count == 0) continue;

            var col = _allColumns.FirstOrDefault(c => c.Key == kvp.Key);
            if (col == null) continue;

            if (col.FilterType == "string")
            {
                query = query.Where(x =>
                {
                    var val = GetFilterValue(x, kvp.Key);
                    return val != null && kvp.Value.Contains(val, StringComparer.OrdinalIgnoreCase);
                });
            }
            else if (col.FilterType == "enum" || col.FilterType == "boolean")
            {
                query = query.Where(x =>
                {
                    var val = GetFilterValue(x, kvp.Key);
                    return val != null && kvp.Value.Contains(val, StringComparer.OrdinalIgnoreCase);
                });
            }
            else if (col.FilterType == "number")
            {
                // 数值列仅支持"非空/空"筛选（BuildFilterContextOptions 只构建这两个选项）
                query = query.Where(x =>
                {
                    var val = GetFilterValue(x, kvp.Key);
                    return val != null
                        ? kvp.Value.Contains(FilterNotNull)
                        : kvp.Value.Contains(FilterNull);
                });
            }
        }

        // 排序
        query = sortColumn switch
        {
            "WorkOrderNo" => sortDescending ? query.OrderByDescending(x => x.WorkOrderNo) : query.OrderBy(x => x.WorkOrderNo),
            "Salesman" => sortDescending ? query.OrderByDescending(x => x.Salesman) : query.OrderBy(x => x.Salesman),
            "CustomerName" => sortDescending ? query.OrderByDescending(x => x.CustomerName) : query.OrderBy(x => x.CustomerName),
            "EndCustomer" => sortDescending ? query.OrderByDescending(x => x.EndCustomer) : query.OrderBy(x => x.EndCustomer),
            "SignDate" => sortDescending ? query.OrderByDescending(x => x.SignDate) : query.OrderBy(x => x.SignDate),
            "DeliveryDate" => sortDescending ? query.OrderByDescending(x => x.DeliveryDate) : query.OrderBy(x => x.DeliveryDate),
            "DelayPenalty" => sortDescending ? query.OrderByDescending(x => x.DelayPenalty) : query.OrderBy(x => x.DelayPenalty),
            "SettlementMethod" => sortDescending ? query.OrderByDescending(x => x.SettlementMethod) : query.OrderBy(x => x.SettlementMethod),
            "SalesOrderNo" => sortDescending ? query.OrderByDescending(x => x.SalesOrderNo) : query.OrderBy(x => x.SalesOrderNo),
            "ProductionMainNo" => sortDescending ? query.OrderByDescending(x => x.ProductionMainNo) : query.OrderBy(x => x.ProductionMainNo),
            "ProductionSubNo" => sortDescending ? query.OrderByDescending(x => x.ProductionSubNo) : query.OrderBy(x => x.ProductionSubNo),
            "MaterialName" => sortDescending ? query.OrderByDescending(x => x.MaterialName) : query.OrderBy(x => x.MaterialName),
            "DeliveryState" => sortDescending ? query.OrderByDescending(x => x.DeliveryState) : query.OrderBy(x => x.DeliveryState),
            "PlantGrade" => sortDescending ? query.OrderByDescending(x => x.PlantGrade) : query.OrderBy(x => x.PlantGrade),
            "Specification" => sortDescending ? query.OrderByDescending(x => x.Specification) : query.OrderBy(x => x.Specification),
            "LengthStatus" => sortDescending ? query.OrderByDescending(x => x.LengthStatus) : query.OrderBy(x => x.LengthStatus),
            "MinLength" => sortDescending ? query.OrderByDescending(x => x.MinLength) : query.OrderBy(x => x.MinLength),
            "MaxLength" => sortDescending ? query.OrderByDescending(x => x.MaxLength) : query.OrderBy(x => x.MaxLength),
            "TotalItemCount" => sortDescending ? query.OrderByDescending(x => x.TotalItemCount) : query.OrderBy(x => x.TotalItemCount),
            "TotalQuantity" => sortDescending ? query.OrderByDescending(x => x.TotalQuantity) : query.OrderBy(x => x.TotalQuantity),
            "TotalMeters" => sortDescending ? query.OrderByDescending(x => x.TotalMeters) : query.OrderBy(x => x.TotalMeters),
            "TotalWeight" => sortDescending ? query.OrderByDescending(x => x.TotalWeight) : query.OrderBy(x => x.TotalWeight),
            "FlowOutputRatio" => sortDescending ? query.OrderByDescending(x => x.FlowOutputRatio) : query.OrderBy(x => x.FlowOutputRatio),
            "FlowStatus" => sortDescending ? query.OrderByDescending(x => x.FlowStatus) : query.OrderBy(x => x.FlowStatus),
            "MainNoFlowOutputRatio" => sortDescending ? query.OrderByDescending(x => x.MainNoFlowOutputRatio) : query.OrderBy(x => x.MainNoFlowOutputRatio),
            "MainNoFlowStatus" => sortDescending ? query.OrderByDescending(x => x.MainNoFlowStatus) : query.OrderBy(x => x.MainNoFlowStatus),
            "FlowTotalBatchCount" => sortDescending ? query.OrderByDescending(x => x.FlowTotalBatchCount) : query.OrderBy(x => x.FlowTotalBatchCount),
            "FlowIncompleteBatchCount" => sortDescending ? query.OrderByDescending(x => x.FlowIncompleteBatchCount) : query.OrderBy(x => x.FlowIncompleteBatchCount),
            "FlowMaxRemainingWorkDays" => sortDescending ? query.OrderByDescending(x => x.FlowMaxRemainingWorkDays) : query.OrderBy(x => x.FlowMaxRemainingWorkDays),
            "ScheduleStage" => sortDescending ? query.OrderByDescending(x => x.ScheduleStage) : query.OrderBy(x => x.ScheduleStage),
            "TotalRemainingWorkDays" => sortDescending ? query.OrderByDescending(x => x.TotalRemainingWorkDays) : query.OrderBy(x => x.TotalRemainingWorkDays),
            "CapacityWorkDays" => sortDescending ? query.OrderByDescending(x => x.CapacityWorkDays) : query.OrderBy(x => x.CapacityWorkDays),
            "UrgencyLevel" => sortDescending ? query.OrderByDescending(x => x.UrgencyLevel) : query.OrderBy(x => x.UrgencyLevel),
            "EstimatedProcessCompletionDate" => sortDescending ? query.OrderByDescending(x => x.EstimatedProcessCompletionDate) : query.OrderBy(x => x.EstimatedProcessCompletionDate),
            "DaysDiffFromDelivery" => sortDescending ? query.OrderByDescending(x => x.DaysDiffFromDelivery) : query.OrderBy(x => x.DaysDiffFromDelivery),
            "RawMaterialLockRemark" => sortDescending ? query.OrderByDescending(x => x.RawMaterialLockRemark) : query.OrderBy(x => x.RawMaterialLockRemark),
            "IsUrging" => sortDescending ? query.OrderByDescending(x => x.IsUrging) : query.OrderBy(x => x.IsUrging),
            "IsBatchDelivery" => sortDescending ? query.OrderByDescending(x => x.IsBatchDelivery) : query.OrderBy(x => x.IsBatchDelivery),
            "IsPaused" => sortDescending ? query.OrderByDescending(x => x.IsPaused) : query.OrderBy(x => x.IsPaused),
            "AdjustmentRemark" => sortDescending ? query.OrderByDescending(x => x.AdjustmentRemark) : query.OrderBy(x => x.AdjustmentRemark),
            "PendingSectionRoughTube" => sortDescending ? query.OrderByDescending(x => x.PendingSectionRoughTube) : query.OrderBy(x => x.PendingSectionRoughTube),
            "PendingSectionWarehouseFix" => sortDescending ? query.OrderByDescending(x => x.PendingSectionWarehouseFix) : query.OrderBy(x => x.PendingSectionWarehouseFix),
            "PendingSection60Roll" => sortDescending ? query.OrderByDescending(x => x.PendingSection60Roll) : query.OrderBy(x => x.PendingSection60Roll),
            "PendingSection50Roll" => sortDescending ? query.OrderByDescending(x => x.PendingSection50Roll) : query.OrderBy(x => x.PendingSection50Roll),
            "PendingSection30Roll" => sortDescending ? query.OrderByDescending(x => x.PendingSection30Roll) : query.OrderBy(x => x.PendingSection30Roll),
            "PendingSection20Roll" => sortDescending ? query.OrderByDescending(x => x.PendingSection20Roll) : query.OrderBy(x => x.PendingSection20Roll),
            "PendingSectionThreeRoll" => sortDescending ? query.OrderByDescending(x => x.PendingSectionThreeRoll) : query.OrderBy(x => x.PendingSectionThreeRoll),
            "PendingSectionDrawBench" => sortDescending ? query.OrderByDescending(x => x.PendingSectionDrawBench) : query.OrderBy(x => x.PendingSectionDrawBench),
            "DeformedProcessCompleted" => sortDescending ? query.OrderByDescending(x => x.DeformedProcessCompleted) : query.OrderBy(x => x.DeformedProcessCompleted),
            "ProductionAttentionProcess" => sortDescending ? query.OrderByDescending(x => x.ProductionAttentionProcess) : query.OrderBy(x => x.ProductionAttentionProcess),
            "ProductionFlowProperty" => sortDescending ? query.OrderByDescending(x => x.ProductionFlowProperty) : query.OrderBy(x => x.ProductionFlowProperty),
            "MaxBatchRemainingWorkDays" => sortDescending ? query.OrderByDescending(x => x.MaxBatchRemainingWorkDays) : query.OrderBy(x => x.MaxBatchRemainingWorkDays),
            "MainNoAttentionProcess" => sortDescending ? query.OrderByDescending(x => x.MainNoAttentionProcess) : query.OrderBy(x => x.MainNoAttentionProcess),
            "ConsistencyStatus" => sortDescending ? query.OrderByDescending(x => x.ConsistencyStatus) : query.OrderBy(x => x.ConsistencyStatus),
            "PlanScheduleStage" => sortDescending ? query.OrderByDescending(x => x.PlanScheduleStage) : query.OrderBy(x => x.PlanScheduleStage),
            "PlanUrgencyLevel" => sortDescending ? query.OrderByDescending(x => x.PlanUrgencyLevel) : query.OrderBy(x => x.PlanUrgencyLevel),
            "PlanProductionAttentionProcess" => sortDescending ? query.OrderByDescending(x => x.PlanProductionAttentionProcess) : query.OrderBy(x => x.PlanProductionAttentionProcess),
            "PlanProductionFlowProperty" => sortDescending ? query.OrderByDescending(x => x.PlanProductionFlowProperty) : query.OrderBy(x => x.PlanProductionFlowProperty),
            _ => sortDescending ? query.OrderByDescending(x => x.WorkOrderNo) : query.OrderBy(x => x.WorkOrderNo)
        };

        _filteredItems = query.ToList();
        ComputePageSums();
    }

    // ========== ExcelFilter 事件 ==========

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        ApplyFiltersAndSort();
        await SavePageStateAsync();
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
        ApplyFiltersAndSort();
        await SavePageStateAsync();
    }

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        ApplyFiltersAndSort();
        await SavePageStateAsync();
    }

    // ========== 分组 CSS ==========

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1",
            2 => "col-g2",
            3 => "col-g3",
            13 => "col-g13",
            18 => "col-g18",
            15 => "col-g15",
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
            13 => "col-g13-cell",
            18 => "col-g18-cell",
            15 => "col-g15-cell",
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
        await LoadDictOptionsAsync();

        _allColumns = GetAllColumnDefs();

        // 从 ColumnPrefsService 恢复列顺序和显隐
        var savedPrefs = await ColumnPrefs.LoadAsync("workorderschedules", null);
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

        // 从 PageState 恢复排序/筛选状态（列显隐/顺序由 ColumnPrefs 管理）
        var savedState = await PageState.LoadAsync("workorderschedules");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "WorkOrderNo";
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
        }

        await LoadDataAsync();
    }

    // ========== 分组标题栏同步 ==========

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 分组标题栏：测量实际列宽 + 同步滚动
        await JS.InvokeVoidAsync("initGroupHeaders", "#workorder-schedule-list-table");

        // 分页导航/页大小切换后重算当前页汇总（pager 操作只改 CurrentPage/RowsPerPage，不触发 ApplyFiltersAndSort）
        if (table != null && _filteredItems.Count > 0)
        {
            var page = table.CurrentPage;
            var count = _filteredItems.Count;
            var rowsPerPage = table.RowsPerPage;
            if (page != _lastSummedPage || count != _lastSummedCount || rowsPerPage != _lastSummedPageSize)
            {
                _lastSummedPage = page;
                _lastSummedCount = count;
                _lastSummedPageSize = rowsPerPage;
                ComputePageSums();
                StateHasChanged();
            }
        }
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(WorkOrderScheduleDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "WorkOrderNo":
                builder.AddContent(0, item.WorkOrderNo);
                break;
            case "Salesman":
                builder.AddContent(0, item.Salesman);
                break;
            case "CustomerName":
                builder.AddContent(0, item.CustomerName);
                break;
            case "EndCustomer":
                builder.AddContent(0, item.EndCustomer ?? "-");
                break;
            case "SignDate":
                builder.AddContent(0, item.SignDate.ToString("yyyy-MM-dd"));
                break;
            case "DeliveryDate":
                builder.AddContent(0, item.DeliveryDate.ToString("yyyy-MM-dd"));
                break;
            case "DelayPenalty":
                builder.AddContent(0, item.DelayPenaltyText);
                break;
            case "SettlementMethod":
                builder.AddContent(0, DisplayHelper.GetSettlementMethodText(item.SettlementMethod));
                break;
            case "SalesOrderNo":
                builder.AddContent(0, item.SalesOrderNo);
                break;
            case "ProductionMainNo":
                builder.AddContent(0, item.ProductionMainNo);
                break;
            case "ProductionSubNo":
                builder.AddContent(0, item.ProductionSubNo ?? "-");
                break;
            case "MaterialName":
                builder.AddContent(0, DisplayHelper.GetPipeManufacturingTypeText(item.MaterialName));
                break;
            case "DeliveryState":
                builder.AddContent(0, DisplayHelper.GetDeliveryStateText(item.DeliveryState));
                break;
            case "LengthStatus":
                builder.AddContent(0, DisplayHelper.GetWorkOrderLengthStatusText(item.LengthStatus, item.MinLength, item.MaxLength));
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade);
                break;
            case "Specification":
                builder.AddContent(0, item.Specification);
                break;
            case "MinLength":
                builder.AddContent(0, item.MinLength?.ToString("G29") ?? "-");
                break;
            case "MaxLength":
                builder.AddContent(0, item.MaxLength?.ToString("G29") ?? "-");
                break;
            case "TotalItemCount":
                builder.AddContent(0, item.TotalItemCount.ToString());
                break;
            case "TotalQuantity":
                builder.AddContent(0, item.TotalQuantity);
                break;
            case "TotalMeters":
                builder.AddContent(0, ((int)item.TotalMeters).ToString());
                break;
            case "TotalWeight":
                builder.AddContent(0, ((int)item.TotalWeight).ToString());
                break;

            // G7
            case "FlowOutputRatio":
                builder.AddContent(0, item.FlowOutputRatio > 0 ? $"{item.FlowOutputRatio:F1}%" : "-");
                break;
            case "FlowStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetInputStatusColor(item.FlowStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetFlowStatusText(item.FlowStatus))));
                builder.CloseComponent();
                break;
            case "MainNoFlowOutputRatio":
                builder.AddContent(0, item.MainNoFlowOutputRatio > 0 ? $"{item.MainNoFlowOutputRatio:F1}%" : "-");
                break;
            case "MainNoFlowStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetInputStatusColor(item.MainNoFlowStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetValidMainNoStatusText(item.MainNoFlowStatus))));
                builder.CloseComponent();
                break;
            case "FlowTotalBatchCount":
                builder.AddContent(0, item.FlowTotalBatchCount > 0 ? item.FlowTotalBatchCount.ToString() : "-");
                break;
            case "FlowIncompleteBatchCount":
                builder.AddContent(0, item.FlowIncompleteBatchCount > 0 ? item.FlowIncompleteBatchCount.ToString() : "-");
                break;
            case "FlowMaxRemainingWorkDays":
                builder.AddContent(0, item.FlowMaxRemainingWorkDays > 0 ? $"{item.FlowMaxRemainingWorkDays}天" : "-");
                break;

            // G12
            case "ScheduleStage":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetScheduleStageColor(item.ScheduleStage));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.ScheduleStageText)));
                builder.CloseComponent();
                break;
            case "TotalRemainingWorkDays":
                builder.AddContent(0, item.TotalRemainingWorkDays.HasValue ? $"{item.TotalRemainingWorkDays}天" : "-");
                break;
            case "CapacityWorkDays":
                builder.AddContent(0, item.CapacityWorkDays.HasValue ? $"{item.CapacityWorkDays}天" : "-");
                break;
            case "UrgencyLevel":
                builder.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey,item.UrgencyLevel) ?? "-");
                break;
            case "EstimatedProcessCompletionDate":
                builder.AddContent(0, item.EstimatedProcessCompletionDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "DaysDiffFromDelivery":
                builder.AddContent(0, item.DaysDiffFromDelivery.HasValue ? $"{item.DaysDiffFromDelivery}天" : "-");
                break;
            case "RawMaterialLockRemark":
                builder.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, item.RawMaterialLockRemark) ?? "-");
                break;

            // G13
            case "IsUrging":
                builder.AddContent(0, item.UrgingText);
                break;
            case "IsBatchDelivery":
                builder.AddContent(0, item.IsBatchDelivery ? "是" : "否");
                break;
            case "IsPaused":
                builder.AddContent(0, item.IsPaused ? "是" : "否");
                break;
            case "AdjustmentRemark":
                builder.AddContent(0, item.AdjustmentRemark ?? "-");
                break;

            // G14: 在产节点待量
            case "PendingSectionRoughTube":
                builder.AddContent(0, item.PendingSectionRoughTube.HasValue ? ((int)item.PendingSectionRoughTube.Value).ToString() : "-");
                break;
            case "PendingSectionWarehouseFix":
                builder.AddContent(0, item.PendingSectionWarehouseFix.HasValue ? ((int)item.PendingSectionWarehouseFix.Value).ToString() : "-");
                break;
            case "PendingSection60Roll":
                builder.AddContent(0, item.PendingSection60Roll.HasValue ? ((int)item.PendingSection60Roll.Value).ToString() : "-");
                break;
            case "PendingSection50Roll":
                builder.AddContent(0, item.PendingSection50Roll.HasValue ? ((int)item.PendingSection50Roll.Value).ToString() : "-");
                break;
            case "PendingSection30Roll":
                builder.AddContent(0, item.PendingSection30Roll.HasValue ? ((int)item.PendingSection30Roll.Value).ToString() : "-");
                break;
            case "PendingSection20Roll":
                builder.AddContent(0, item.PendingSection20Roll.HasValue ? ((int)item.PendingSection20Roll.Value).ToString() : "-");
                break;
            case "PendingSectionThreeRoll":
                builder.AddContent(0, item.PendingSectionThreeRoll.HasValue ? ((int)item.PendingSectionThreeRoll.Value).ToString() : "-");
                break;
            case "PendingSectionDrawBench":
                builder.AddContent(0, item.PendingSectionDrawBench.HasValue ? ((int)item.PendingSectionDrawBench.Value).ToString() : "-");
                break;
            case "DeformedProcessCompleted":
                builder.AddContent(0, item.DeformedProcessCompleted switch { true => "是", false => "否", null => "略" });
                break;
            case "ProductionAttentionProcess":
                builder.AddContent(0, ProcessDisplayHelper.GetProcessNameText(item.ProductionAttentionProcess ?? "-"));
                break;
            case "ProductionFlowProperty":
                var flowProp = item.ProductionFlowProperty;
                if (!string.IsNullOrEmpty(flowProp))
                {
                    var color = flowProp switch
                    {
                        ProductionFlowKeys.Paused => Color.Error,
                        ProductionFlowKeys.Normal => Color.Success,
                        ProductionFlowKeys.Waiting => Color.Warning,
                        ProductionFlowKeys.Doubt => Color.Info,
                        _ => Color.Default
                    };
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", color);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.ProductionFlowKey,flowProp))));
                    builder.CloseComponent();
                }
                break;
            case "MaxBatchRemainingWorkDays":
                builder.AddContent(0, item.MaxBatchRemainingWorkDays.HasValue ? $"{item.MaxBatchRemainingWorkDays}天" : "-");
                break;
            case "MainNoAttentionProcess":
                builder.AddContent(0, ProcessDisplayHelper.GetProcessNameText(item.MainNoAttentionProcess ?? "-"));
                break;

            // ========== G15: 工单计划（内联编辑） ==========
            case "ConsistencyStatus":
                var cs = item.ConsistencyStatus;
                var csColor = cs switch
                {
                    "一致" => Color.Success,
                    "进度调整" => Color.Info,
                    "错误" => Color.Error,
                    _ => Color.Default
                };
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", csColor);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, cs ?? "-")));
                builder.CloseComponent();
                break;

            case "PlanScheduleStage":
                builder.OpenComponent<MudSelect<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanScheduleStage?.ToString() ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanScheduleStage = string.IsNullOrEmpty(v) ? null : int.Parse(v);
                    await SavePlanAsync(item);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "Class", "compact-select");
                builder.AddAttribute(6, "ChildContent", (RenderFragment)(b2 =>
                {
                    b2.OpenComponent<MudSelectItem<string>>(0);
                    b2.AddAttribute(1, "Value", "");
                    b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, "-")));
                    b2.CloseComponent();
                    foreach (var opt in DisplayHelper.GetPlanScheduleStageOptions())
                    {
                        b2.OpenComponent<MudSelectItem<string>>(0);
                        b2.AddAttribute(1, "Value", opt.Value);
                        b2.AddAttribute(2, "Text", opt.Display);
                        b2.AddAttribute(3, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, opt.Display)));
                        b2.CloseComponent();
                    }
                }));
                builder.CloseComponent();
                break;

            case "PlanUrgencyLevel":
                builder.OpenComponent<MudSelect<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanUrgencyLevel ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanUrgencyLevel = string.IsNullOrEmpty(v) ? null : v;
                    await SavePlanAsync(item);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "Class", "compact-select");
                builder.AddAttribute(6, "ChildContent", (RenderFragment)(b2 =>
                {
                    b2.OpenComponent<MudSelectItem<string>>(0);
                    b2.AddAttribute(1, "Value", "");
                    b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, "-")));
                    b2.CloseComponent();
                    foreach (var opt in _urgencyOptions)
                    {
                        b2.OpenComponent<MudSelectItem<string>>(0);
                        b2.AddAttribute(1, "Value", opt.Value);
                        b2.AddAttribute(2, "Text", opt.Text);
                        b2.AddAttribute(3, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, opt.Text)));
                        b2.CloseComponent();
                    }
                }));
                builder.CloseComponent();
                break;

            case "PlanProductionAttentionProcess":
                builder.OpenComponent<MudSelect<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanProductionAttentionProcess ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    var oldVal = item.PlanProductionAttentionProcess;
                    item.PlanProductionAttentionProcess = string.IsNullOrEmpty(v) ? null : v;
                    if (!ValidateAttentionProcess(item, oldVal)) return;
                    await SavePlanAsync(item);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "Class", "compact-select");
                builder.AddAttribute(6, "ChildContent", (RenderFragment)(b2 =>
                {
                    b2.OpenComponent<MudSelectItem<string>>(0);
                    b2.AddAttribute(1, "Value", "");
                    b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, "-")));
                    b2.CloseComponent();
                    if (_filterContextOptions.TryGetValue("PlanProductionAttentionProcess", out var attentionOpts))
                    {
                        foreach (var opt in attentionOpts.Where(x => x.Value != FilterNull))
                        {
                            b2.OpenComponent<MudSelectItem<string>>(0);
                            b2.AddAttribute(1, "Value", opt.Value);
                            b2.AddAttribute(2, "Text", opt.Display);
                            b2.AddAttribute(3, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, opt.Display)));
                            b2.CloseComponent();
                        }
                    }
                }));
                builder.CloseComponent();
                break;

            case "PlanProductionFlowProperty":
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "style", item.PlanProductionFlowProperty == ProductionFlowKeys.Doubt
                    ? "background-color:#ffebee;border-radius:4px;padding:2px;"
                    : "");
                builder.OpenComponent<MudSelect<string>>(2);
                builder.AddAttribute(3, "Value", item.PlanProductionFlowProperty ?? "");
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanProductionFlowProperty = string.IsNullOrEmpty(v) ? null : v;
                    await SavePlanAsync(item);
                }));
                builder.AddAttribute(5, "Dense", true);
                builder.AddAttribute(6, "Variant", Variant.Text);
                builder.AddAttribute(7, "Class", "compact-select");
                builder.AddAttribute(8, "ChildContent", (RenderFragment)(b2 =>
                {
                    b2.OpenComponent<MudSelectItem<string>>(0);
                    b2.AddAttribute(1, "Value", "");
                    b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, "-")));
                    b2.CloseComponent();
                    foreach (var opt in _productionFlowOptions)
                    {
                        b2.OpenComponent<MudSelectItem<string>>(0);
                        b2.AddAttribute(1, "Value", opt.Value);
                        b2.AddAttribute(2, "Text", opt.Text);
                        b2.AddAttribute(3, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, opt.Text)));
                        b2.CloseComponent();
                    }
                }));
                builder.CloseComponent();
                builder.CloseElement();
                break;
        }
    };

    // ========== 文本辅助 ==========

    private static string GetFlowStatusText(int status) =>
        DisplayHelper.GetFlowStatusText(status);

    private static string GetValidMainNoStatusText(int status) =>
        DisplayHelper.GetMainNoFlowStatusText(status);

    // ========== 颜色 ==========

    // ========== 计划安排 ==========

    private async Task OnPlanAllAsync()
    {
        var confirmed = await DialogService.ShowMessageBox(
            "计划安排",
            "确认将当前查询范围内所有工单的工单计划值设为系统值，并删除不匹配的 Plan 行？",
            yesText: "确认",
            cancelText: "取消");
        if (confirmed != true) return;

        try
        {
            var filtersJson = SerializeFilters();

            var query = new QueryParams
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortColumn,
                IsDescending = sortDescending,
                PageSize = 5000,
            };
            if (filtersJson != null)
            {
                query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson);
            }

            var result = await WorkOrderScheduleSvc.PlanScheduleAllAsync(query);
            if (result.Success)
            {
                Snackbar.Add("计划安排成功，已同步系统值并清理多余记录", Severity.Success);
                await LoadDataAsync();
            }
            else
            {
                Snackbar.Add($"计划安排失败: {result.Message}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"计划安排失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task OnPlanAdjustmentKeepAsync()
    {
        var confirmed = await DialogService.ShowMessageBox(
            "进度调整保留计划",
            "确认将当前查询范围内所有工单的工单计划值设为系统值（工单状态/紧急性/流转性），并删除不匹配的 Plan 行？\n实时一致性为「进度调整」的工单，其薄表「生产关注」字段将保留不覆盖。",
            yesText: "确认",
            cancelText: "取消");
        if (confirmed != true) return;

        try
        {
            var filtersJson = SerializeFilters();

            var query = new QueryParams
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortColumn,
                IsDescending = sortDescending,
                PageSize = 5000,
            };
            if (filtersJson != null)
            {
                query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson);
            }

            var result = await WorkOrderScheduleSvc.PlanScheduleKeepAdjustmentAsync(query);
            if (result.Success)
            {
                Snackbar.Add("进度调整保留计划成功，已同步系统值（进度调整工单的生产关注已保留）并清理多余记录", Severity.Success);
                await LoadDataAsync();
            }
            else
            {
                Snackbar.Add($"进度调整保留计划失败: {result.Message}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"进度调整保留计划失败: {ex.Message}", Severity.Error);
        }
    }

    private string? SerializeFilters()
    {
        if (_columnFilters.Count == 0) return null;
        var descriptors = new List<FilterDescriptor>();
        foreach (var kvp in _columnFilters)
        {
            if (kvp.Value.Count == 0) continue;
            descriptors.Add(new FilterDescriptor
            {
                Field = kvp.Key,
                Operator = "in",
                Values = kvp.Value.ToList()
            });
        }
        return descriptors.Count > 0 ? JsonSerializer.Serialize(descriptors) : null;
    }

    // ========== 工单计划保存 ==========

    /// <summary>
    /// 校验手工调整"生产关注"的工序：该工序在"在产节点待量"中对应待量必须 &gt; 0。
    /// 待量 ≤0（含 null，后端仅 &gt;0 才存值）或工序非 8 个待量工序之一 → 视为手动调整错误，回滚选择并提示。
    /// </summary>
    private bool ValidateAttentionProcess(WorkOrderScheduleDto item, string? oldVal)
    {
        var v = item.PlanProductionAttentionProcess;
        if (string.IsNullOrEmpty(v)) return true; // 清空/空值放行

        var key = ProcessKeys.ToKey(v) ?? v;
        decimal? pending = key switch
        {
            ProcessKeys.RoughTubeProcessing => item.PendingSectionRoughTube,
            ProcessKeys.InProcessRepair => item.PendingSectionWarehouseFix,
            ProcessKeys.ColdRoll60 => item.PendingSection60Roll,
            ProcessKeys.ColdRoll50 => item.PendingSection50Roll,
            ProcessKeys.ColdRoll30 => item.PendingSection30Roll,
            ProcessKeys.ColdRoll20 => item.PendingSection20Roll,
            ProcessKeys.ThreeRollColdRoll => item.PendingSectionThreeRoll,
            ProcessKeys.ColdDraw => item.PendingSectionDrawBench,
            _ => null,
        };

        if (pending is null || pending <= 0m)
        {
            item.PlanProductionAttentionProcess = oldVal;
            Snackbar.Add($"手动调整存在错误：\"{ProcessDisplayHelper.GetProcessNameText(v)}\" 在产节点待量为 0，请确认后重试", Severity.Error);
            StateHasChanged();
            return false;
        }

        return true;
    }

    private async Task SavePlanAsync(WorkOrderScheduleDto item)
    {
        var request = new SaveWorkOrderPlanRequest
        {
            WorkOrderId = item.WorkOrderId,
            ScheduleStage = item.PlanScheduleStage,
            UrgencyLevel = item.PlanUrgencyLevel,
            ProductionAttentionProcess = item.PlanProductionAttentionProcess,
            ProductionFlowProperty = item.PlanProductionFlowProperty,
        };

        var result = await WorkOrderScheduleSvc.SavePlanAsync(request);
        if (result.Success)
        {
            Snackbar.Add("保存成功", Severity.Success);
            await LoadDataAsync();
        }
        else
        {
            Snackbar.Add($"保存失败: {result.Message}", Severity.Error);
        }
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
                    dict[col.Key] = ResolvePrintValue(item, col.Key);
                }
                return dict;
            }).ToList();

            var request = new WorkOrderSchedulePrintRequest
            {
                Title = "工单计划",
                Items = printItems,
                Columns = printColumns
            };

            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.WorkOrderSchedule}/print-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private static object ResolvePrintValue(WorkOrderScheduleDto item, string key) => key switch
    {
        // G1: 枚举→中文
        "MaterialName" => DisplayHelper.GetPipeManufacturingTypeText(item.MaterialName) ?? "",
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState) ?? "",
        "LengthStatus" => DisplayHelper.GetLengthStatusText(item.LengthStatus) ?? "",
        "SettlementMethod" => DisplayHelper.GetSettlementMethodText(item.SettlementMethod) ?? "",
        // G7: 流转状态
        "FlowStatus" => DisplayHelper.GetFlowStatusText(item.FlowStatus),
        "MainNoFlowStatus" => DisplayHelper.GetMainNoFlowStatusText(item.MainNoFlowStatus),
        // G12: 关注状态
        "ScheduleStage" => DisplayHelper.GetScheduleStageText(item.ScheduleStage),
        // G15: 覆盖字段（排程计划覆盖档位，4 档）
        "PlanScheduleStage" => item.PlanScheduleStage.HasValue ? DisplayHelper.GetPlanScheduleStageText(item.PlanScheduleStage.Value) : "未知",
        "ConsistencyStatus" => item.ConsistencyStatus ?? "",
        // 非枚举字段原样输出
        _ => GetRawPropertyValue(item, key)
    };

    private static object GetRawPropertyValue(WorkOrderScheduleDto item, string key)
    {
        return (key switch
        {
            "WorkOrderNo" => item.WorkOrderNo ?? "",
            "Salesman" => item.Salesman ?? "",
            "CustomerName" => item.CustomerName ?? "",
            "EndCustomer" => item.EndCustomer ?? "",
            "SignDate" => item.SignDate,
            "DeliveryDate" => item.DeliveryDate,
            "DelayPenalty" => item.DelayPenalty,
            "SalesOrderNo" => item.SalesOrderNo ?? "",
            "ProductionMainNo" => item.ProductionMainNo ?? "",
            "ProductionSubNo" => item.ProductionSubNo ?? "",
            "PlantGrade" => item.PlantGrade ?? "",
            "Specification" => item.Specification ?? "",
            "MinLength" => item.MinLength,
            "MaxLength" => item.MaxLength,
            "TotalItemCount" => item.TotalItemCount,
            "TotalQuantity" => item.TotalQuantity,
            "TotalMeters" => item.TotalMeters,
            "TotalWeight" => item.TotalWeight,
            "FlowOutputRatio" => item.FlowOutputRatio,
            "FlowTotalBatchCount" => item.FlowTotalBatchCount,
            "FlowIncompleteBatchCount" => item.FlowIncompleteBatchCount,
            "FlowMaxRemainingWorkDays" => item.FlowMaxRemainingWorkDays,
            "MainNoFlowOutputRatio" => item.MainNoFlowOutputRatio,
            "TotalRemainingWorkDays" => item.TotalRemainingWorkDays,
            "CapacityWorkDays" => item.CapacityWorkDays,
            "UrgencyLevel" => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey,item.UrgencyLevel) ?? "",
            "EstimatedProcessCompletionDate" => item.EstimatedProcessCompletionDate,
            "DaysDiffFromDelivery" => item.DaysDiffFromDelivery,
            "RawMaterialLockRemark" => DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, item.RawMaterialLockRemark) ?? "",
            "IsUrging" => item.IsUrging,
            "IsBatchDelivery" => item.IsBatchDelivery,
            "IsPaused" => item.IsPaused,
            "AdjustmentRemark" => item.AdjustmentRemark ?? "",
            "PendingSectionRoughTube" => item.PendingSectionRoughTube,
            "PendingSectionWarehouseFix" => item.PendingSectionWarehouseFix,
            "PendingSection60Roll" => item.PendingSection60Roll,
            "PendingSection50Roll" => item.PendingSection50Roll,
            "PendingSection30Roll" => item.PendingSection30Roll,
            "PendingSection20Roll" => item.PendingSection20Roll,
            "PendingSectionThreeRoll" => item.PendingSectionThreeRoll,
            "PendingSectionDrawBench" => item.PendingSectionDrawBench,
            "DeformedProcessCompleted" => item.DeformedProcessCompleted,
            "ProductionAttentionProcess" => ProcessDisplayHelper.GetProcessNameText(item.ProductionAttentionProcess),
            "ProductionFlowProperty" => DictValueDisplayHelper.GetText(DictValueDefaults.ProductionFlowKey,item.ProductionFlowProperty) ?? "",
            "MaxBatchRemainingWorkDays" => item.MaxBatchRemainingWorkDays,
            "MainNoAttentionProcess" => ProcessDisplayHelper.GetProcessNameText(item.MainNoAttentionProcess),
            "PlanUrgencyLevel" => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey,item.PlanUrgencyLevel) ?? "",
            "PlanProductionAttentionProcess" => ProcessDisplayHelper.GetProcessNameText(item.PlanProductionAttentionProcess),
            "PlanProductionFlowProperty" => DictValueDisplayHelper.GetText(DictValueDefaults.ProductionFlowKey,item.PlanProductionFlowProperty) ?? "",
            _ => ""
        })!; // 编译器无法验证object返回值非null
    }

    // ========== 重置列显隐 ==========

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        await SavePageStateAsync();
        ApplyFiltersAndSort();
        StateHasChanged();
    }

    // ========== ColumnPrefs 持久化 ==========

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("workorderschedules", null, _allColumns);
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));



        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = 1,
            Extras = extras
        };
        await PageState.SaveAsync("workorderschedules", state);
    }
}
