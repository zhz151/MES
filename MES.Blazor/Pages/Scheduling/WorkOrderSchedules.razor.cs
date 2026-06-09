using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MES.Core.Models;
using System.Text.Json;

namespace MES.Blazor.Pages.Scheduling;

public partial class WorkOrderSchedules
{
    private MudTable<WorkOrderScheduleDto>? table;
    private List<WorkOrderScheduleDto> _pageItems = new();
    private int _totalCount;
    private int _restoredPageIndex;
    private int _currentPageIndex = 1;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    // 排序状态
    private string sortColumn = "WorkOrderNo";
    private bool sortDescending = true;

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

    private static List<ColumnDef> GetAllColumnDefs()
    {
        // G1: 工单基础数据
        var g1 = new List<ColumnDef>
        {
            new() { Key = "WorkOrderNo",             Label = "工单号",          SortKey = "WorkOrderNo",             FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "Salesman",                Label = "业务员",          SortKey = "Salesman",                FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "CustomerName",            Label = "往来单位",        SortKey = "CustomerName",            FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SignDate",                Label = "订单日期",        SortKey = "SignDate",                Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DeliveryDate",            Label = "交货日期",        SortKey = "DeliveryDate",            Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DelayPenalty",            Label = "延期罚款",        SortKey = "DelayPenalty",            FilterType = "boolean", Width = "120", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SettlementMethod",        Label = "结算方式",        SortKey = "SettlementMethod",        FilterType = "enum", Width = "120", EnumOptions = new() { new("Weighing","过磅"), new("WeighingNegative","过磅-负"), new("Theoretical","理算") }, Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SalesOrderNo",            Label = "订单号",          SortKey = "SalesOrderNo",            FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionMainNo",        Label = "主号",            SortKey = "ProductionMainNo",        FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionSubNo",         Label = "次号",            SortKey = "ProductionSubNo",         FilterType = "string", Width = "120", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MaterialName",            Label = "物料名称",        SortKey = "MaterialName",            FilterType = "enum", Width = "120", EnumOptions = new() { new("SeamlessPipe","无缝管"), new("WeldedPipe","焊管") }, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DeliveryState",           Label = "交货状态",        SortKey = "DeliveryState",           FilterType = "enum", Width = "120", EnumOptions = new() { new("SolutionAnnealedAndPickled","固溶酸洗"), new("SolutionAnnealedAndPickledUTube","固溶酸洗-U型管"), new("SolutionAnnealedAndPickledExternalPolished","固溶酸洗-外抛光"), new("SolutionAnnealedAndPickledInternalPolished","固溶酸洗-内抛光"), new("SolutionAnnealedAndPickledBothPolished","固溶酸洗-内外抛光"), new("SolutionAnnealedAndPickledCoiled","固溶酸洗-盘管"), new("Bright","光亮"), new("BrightUTube","光亮-U型管"), new("BrightCoiled","光亮-盘管"), new("Hard","硬态") }, Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "PlantGrade",              Label = "工厂牌号",        SortKey = "PlantGrade",              FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "Specification",           Label = "规格",            SortKey = "Specification",           FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "LengthStatus",            Label = "长度状态",        SortKey = "LengthStatus",            FilterType = "enum", Width = "120", EnumOptions = new() { new("Fixed","定尺"), new("Range","范围尺"), new("NonFixed","非定尺") }, Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MinLength",               Label = "最小长度",        SortKey = "MinLength",               Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MaxLength",               Label = "最大长度",        SortKey = "MaxLength",               Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalQuantity",           Label = "总支数",          SortKey = "TotalQuantity",           Width = "80", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalWeight",             Label = "总重量",          SortKey = "TotalWeight",             Width = "80", GroupKey = 1, GroupName = "基础数据" },
        };

        // G7: 有效流转
        var g7 = new List<ColumnDef>
        {
            new() { Key = "FlowOutputRatio",        Label = "流转成品比",     SortKey = "FlowOutputRatio",        Width = "80",                             GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowStatus",             Label = "有效流转状态",    SortKey = "FlowStatus",             FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未投料"), new("1","部分"), new("2","满足") }, GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowTotalBatchCount",    Label = "总批次数",       SortKey = "FlowTotalBatchCount",    Width = "80",                              GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowIncompleteBatchCount",Label = "未完成批数",    SortKey = "FlowIncompleteBatchCount",Width = "80",                              GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "MainNoFlowOutputRatio",  Label = "有效主号流转比", SortKey = "MainNoFlowOutputRatio",   Width = "80", Visible = false,       GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "MainNoFlowStatus",       Label = "有效主号状态",   SortKey = "MainNoFlowStatus",       FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未计划"), new("1","部分"), new("2","满足") }, Visible = false, GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowMaxRemainingWorkDays", Label = "最大剩余工量(天)",SortKey = "FlowMaxRemainingWorkDays", Width = "80",                         GroupKey = 7, GroupName = "有效流转" },
        };

        // G12: 实时关注
        var g12 = new List<ColumnDef>
        {
            new() { Key = "ScheduleStage",           Label = "关注状态",      SortKey = "ScheduleStage",           FilterType = "enum", Width = "120", EnumOptions = new() { new("0","工单完成"), new("1","原料锁定"), new("2","生产执行"), new("3","成品检验") }, GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "TotalRemainingWorkDays",  Label = "剩余总工量(天)",SortKey = "TotalRemainingWorkDays",  Width = "80",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "CapacityWorkDays",        Label = "产能工量(天)", SortKey = "CapacityWorkDays",        Width = "80",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "UrgencyLevel",            Label = "工单计划性",    SortKey = "UrgencyLevel",            FilterType = "string", Width = "120", GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "EstimatedProcessCompletionDate",Label = "工艺预计完成日",SortKey = "EstimatedProcessCompletionDate", Width = "120",           GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "DaysDiffFromDelivery",    Label = "交期相差天数",  SortKey = "DaysDiffFromDelivery",    Width = "80",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "RawMaterialLockRemark",   Label = "原锁备注",     SortKey = "RawMaterialLockRemark",   FilterType = "string", Width = "120",     GroupKey = 12, GroupName = "实时关注" },
        };

        // G13: 工单需求调整
        var g13 = new List<ColumnDef>
        {
            new() { Key = "IsUrging",      Label = "催单",  SortKey = "IsUrging",      FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "工单需求调整" },
            new() { Key = "IsBatchDelivery",          Label = "分批交货",      SortKey = "IsBatchDelivery",          FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "工单需求调整" },
            new() { Key = "IsPaused",                  Label = "工单暂停",      SortKey = "IsPaused",                  FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "工单需求调整" },
            new() { Key = "AdjustmentRemark",         Label = "需求调整备注",  SortKey = "AdjustmentRemark",         FilterType = "string", Width = "200", GroupKey = 13, GroupName = "工单需求调整" },
        };

        // G14: 在产节点待量
        var g14 = new List<ColumnDef>
        {
            new() { Key = "PendingSectionRoughTube",       Label = "荒管处理待量",  SortKey = "PendingSectionRoughTube",       Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "PendingSectionWarehouseFix",    Label = "在制修检待量",  SortKey = "PendingSectionWarehouseFix",    Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "PendingSection60Roll",          Label = "60冷轧待量",    SortKey = "PendingSection60Roll",          Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "PendingSection50Roll",          Label = "50冷轧待量",    SortKey = "PendingSection50Roll",          Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "PendingSection30Roll",          Label = "30冷轧待量",    SortKey = "PendingSection30Roll",          Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "PendingSection20Roll",          Label = "20冷轧待量",    SortKey = "PendingSection20Roll",          Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "PendingSectionThreeRoll",       Label = "三辊冷轧待量",  SortKey = "PendingSectionThreeRoll",       Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "PendingSectionDrawBench",       Label = "冷拔待量",      SortKey = "PendingSectionDrawBench",       Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "DeformedProcessCompleted",      Label = "变形完成",      SortKey = "DeformedProcessCompleted",      FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "ProductionAttentionProcess",    Label = "生产关注工序",  SortKey = "ProductionAttentionProcess",    FilterType = "string", Width = "120", GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "ProductionFlowProperty",        Label = "生产流转性",    SortKey = "ProductionFlowProperty",        FilterType = "string", Width = "100", GroupKey = 14, GroupName = "在产待量" },
        };

        // G15: 工单计划（薄表 — 手工可编辑）
        var g15 = new List<ColumnDef>
        {
            new() { Key = "ConsistencyStatus",              Label = "实时一致性",  SortKey = "ConsistencyStatus",          FilterType = "boolean", Width = "100", BoolTrueLabel = "一致", BoolFalseLabel = "不一致", GroupKey = 15, GroupName = "工单计划" },
            new() { Key = "PlanScheduleStage",               Label = "工单状态",     SortKey = "PlanScheduleStage",          Width = "100", GroupKey = 15, GroupName = "工单计划" },
            new() { Key = "PlanUrgencyLevel",                Label = "紧急性",       SortKey = "PlanUrgencyLevel",           Width = "100", GroupKey = 15, GroupName = "工单计划" },
            new() { Key = "PlanProductionAttentionProcess",  Label = "生产关注",     SortKey = "PlanProductionAttentionProcess", Width = "120", GroupKey = 15, GroupName = "工单计划" },
            new() { Key = "PlanProductionFlowProperty",      Label = "流转性",       SortKey = "PlanProductionFlowProperty",  Width = "100", GroupKey = 15, GroupName = "工单计划" },
        };

        var all = new List<ColumnDef>();
        all.AddRange(g1);
        all.AddRange(g7);
        all.AddRange(g12);
        all.AddRange(g13);
        all.AddRange(g14);
        all.AddRange(g15);
        return all;
    }

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

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

    // ========== 服务端数据加载 ==========

    private async Task<TableData<WorkOrderScheduleDto>> LoadDataFromServer(TableState state)
    {
        // 保持 RowsPerPage 与用户选择同步，避免排序/筛选后复位
        _pageSize = state.PageSize;

        if (_isFirstLoad)
        {
            state.Page = _restoredPageIndex;
            _isFirstLoad = false;
        }

        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "WorkOrderNo";
            var filtersJson = SerializeFilters();

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending
            };
            if (filtersJson != null)
            {
                query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson);
            }

            var result = await WorkOrderScheduleSvc.GetPagedAsync(query);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPageIndex = state.Page + 1;
                ComputePageSums();
                await SavePageStateAsync();
            }
            else
            {
                _pageItems = new();
                _totalCount = 0;
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
        }

        return new TableData<WorkOrderScheduleDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
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

    // ========== 筛选上下文加载 ==========

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await WorkOrderScheduleSvc.GetFilterContextsAsync();
            if (result.Success && result.Data != null)
            {
                BuildFilterContextOptions(result.Data);
            }
        }
        catch { }
    }

    private void BuildFilterContextOptions(Dictionary<string, List<string>> filterContexts)
    {
        _filterContextOptions.Clear();
        foreach (var kvp in filterContexts)
        {
            _filterContextOptions[kvp.Key] = kvp.Value.Select(v => new ExcelFilterOption
            {
                Value = v,
                Display = v,
                Count = 0
            }).ToList();
        }

        // DelayPenalty 列显示中文
        if (_filterContextOptions.TryGetValue("DelayPenalty", out var delayOptions))
        {
            foreach (var opt in delayOptions)
                opt.Display = opt.Value == "True" ? "是" : "否";
        }

        // 补充枚举列筛选选项
        foreach (var col in _allColumns)
        {
            if (col.FilterType == "enum" && col.EnumOptions != null && !_filterContextOptions.ContainsKey(col.Key))
            {
                _filterContextOptions[col.Key] = col.EnumOptions.Select(e => new ExcelFilterOption
                {
                    Value = e.Value,
                    Display = e.Display,
                    Count = 0
                }).ToList();
            }
        }

        // 补充布尔列筛选选项
        foreach (var col in _allColumns)
        {
            if (col.FilterType == "boolean" && !_filterContextOptions.ContainsKey(col.Key))
            {
                _filterContextOptions[col.Key] = new List<ExcelFilterOption>
                {
                    new() { Value = "True", Display = col.BoolTrueLabel ?? "是", Count = 0 },
                    new() { Value = "False", Display = col.BoolFalseLabel ?? "否", Count = 0 }
                };
            }
        }
    }

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
        await SavePageStateAsync();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
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
            7 => "col-g7",
            12 => "col-g12",
            13 => "col-g13",
            14 => "col-g14",
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
            7 => "col-g7-cell",
            12 => "col-g12-cell",
            13 => "col-g13-cell",
            14 => "col-g14-cell",
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

        var savedState = await PageState.LoadAsync("workorderschedules");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "WorkOrderNo";
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
        }

        if (savedState != null && table != null)
            await table.ReloadServerData();

        await LoadFilterContextsAsync();
    }

    // ========== 分组标题栏同步 ==========

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 分组标题栏：测量实际列宽 + 同步滚动
        await JS.InvokeVoidAsync("initGroupHeaders", "#workorder-schedule-list-table");
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
                builder.AddContent(0, DisplayHelper.GetMaterialNameText(item.MaterialName));
                break;
            case "DeliveryState":
                builder.AddContent(0, DisplayHelper.GetDeliveryStateText(item.DeliveryState));
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade);
                break;
            case "Specification":
                builder.AddContent(0, item.Specification);
                break;
            case "LengthStatus":
                builder.AddContent(0, DisplayHelper.GetLengthStatusText(item.LengthStatus));
                break;
            case "MinLength":
                builder.AddContent(0, item.MinLength?.ToString("G29") ?? "-");
                break;
            case "MaxLength":
                builder.AddContent(0, item.MaxLength?.ToString("G29") ?? "-");
                break;
            case "TotalQuantity":
                builder.AddContent(0, item.TotalQuantity);
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
                builder.AddAttribute(2, "Color", GetInputStatusColor(item.FlowStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetFlowStatusText(item.FlowStatus))));
                builder.CloseComponent();
                break;
            case "MainNoFlowOutputRatio":
                builder.AddContent(0, item.MainNoFlowOutputRatio > 0 ? $"{item.MainNoFlowOutputRatio:F1}%" : "-");
                break;
            case "MainNoFlowStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetInputStatusColor(item.MainNoFlowStatus));
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
                builder.AddAttribute(2, "Color", GetScheduleStageColor(item.ScheduleStage));
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
                builder.AddContent(0, item.UrgencyLevel ?? "-");
                break;
            case "EstimatedProcessCompletionDate":
                builder.AddContent(0, item.EstimatedProcessCompletionDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "DaysDiffFromDelivery":
                builder.AddContent(0, item.DaysDiffFromDelivery.HasValue ? $"{item.DaysDiffFromDelivery}天" : "-");
                break;
            case "RawMaterialLockRemark":
                builder.AddContent(0, item.RawMaterialLockRemark ?? "-");
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
                builder.AddContent(0, item.DeformedProcessCompleted ? "是" : "否");
                break;
            case "ProductionAttentionProcess":
                builder.AddContent(0, item.ProductionAttentionProcess ?? "-");
                break;
            case "ProductionFlowProperty":
                var flowProp = item.ProductionFlowProperty;
                if (!string.IsNullOrEmpty(flowProp))
                {
                    var color = flowProp switch
                    {
                        "暂停" => Color.Error,
                        "正常" => Color.Success,
                        "待料" => Color.Warning,
                        "疑问" => Color.Info,
                        _ => Color.Default
                    };
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", color);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, flowProp)));
                    builder.CloseComponent();
                }
                break;

            // ========== G15: 工单计划（内联编辑） ==========
            case "ConsistencyStatus":
                var isConsistent = item.ConsistencyStatus;
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", isConsistent ? Color.Success : Color.Error);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, isConsistent ? "一致" : "不一致")));
                builder.CloseComponent();
                break;

            case "PlanScheduleStage":
                builder.OpenComponent<MudSelect<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanScheduleStage?.ToString() ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanScheduleStage = string.IsNullOrEmpty(v) ? null : int.Parse(v);
                    await SavePlanAsync(item, "PlanScheduleStage", item.PlanScheduleStage);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "Class", "compact-select");
                builder.AddAttribute(6, "ChildContent", (RenderFragment)(b2 =>
                {
                    b2.OpenComponent<MudSelectItem<string>>(0);
                    b2.AddAttribute(1, "Value", "");
                    b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, "系统值")));
                    b2.CloseComponent();
                    foreach (var (val, label) in new[] { ("0", "工单完成"), ("1", "原料锁定"), ("2", "生产执行"), ("3", "成品检验") })
                    {
                        b2.OpenComponent<MudSelectItem<string>>(0);
                        b2.AddAttribute(1, "Value", val);
                        b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, label)));
                        b2.CloseComponent();
                    }
                }));
                builder.CloseComponent();
                break;

            case "PlanUrgencyLevel":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanUrgencyLevel ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanUrgencyLevel = string.IsNullOrEmpty(v) ? null : v;
                    await SavePlanAsync(item, "PlanUrgencyLevel", item.PlanUrgencyLevel);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "Class", "compact-select");
                builder.CloseComponent();
                break;

            case "PlanProductionAttentionProcess":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanProductionAttentionProcess ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanProductionAttentionProcess = string.IsNullOrEmpty(v) ? null : v;
                    await SavePlanAsync(item, "PlanProductionAttentionProcess", item.PlanProductionAttentionProcess);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "Class", "compact-select");
                builder.CloseComponent();
                break;

            case "PlanProductionFlowProperty":
                builder.OpenComponent<MudSelect<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanProductionFlowProperty ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanProductionFlowProperty = string.IsNullOrEmpty(v) ? null : v;
                    await SavePlanAsync(item, "PlanProductionFlowProperty", item.PlanProductionFlowProperty);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "Class", "compact-select");
                builder.AddAttribute(6, "ChildContent", (RenderFragment)(b2 =>
                {
                    b2.OpenComponent<MudSelectItem<string>>(0);
                    b2.AddAttribute(1, "Value", "");
                    b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, "系统值")));
                    b2.CloseComponent();
                    foreach (var opt in new[] { "正常", "暂停", "待料", "疑问", "略" })
                    {
                        b2.OpenComponent<MudSelectItem<string>>(0);
                        b2.AddAttribute(1, "Value", opt);
                        b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, opt)));
                        b2.CloseComponent();
                    }
                }));
                builder.CloseComponent();
                break;
        }
    };

    // ========== 文本辅助 ==========

    private static string GetFlowStatusText(int status) => status switch
    {
        0 => "未投料", 1 => "部分", 2 => "满足", _ => "未知"
    };

    private static string GetValidMainNoStatusText(int status) => status switch
    {
        0 => "未计划", 1 => "部分", 2 => "满足", _ => "未知"
    };

    // ========== 颜色 ==========

    private static Color GetInputStatusColor(int status) => status switch
    {
        0 => Color.Default,
        1 => Color.Warning,
        2 => Color.Success,
        _ => Color.Default
    };

    private static Color GetScheduleStageColor(int stage) => stage switch
    {
        0 => Color.Default,
        1 => Color.Warning,
        2 => Color.Success,
        3 => Color.Info,
        _ => Color.Default
    };

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
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "WorkOrderNo";
            var filtersJson = SerializeFilters();

            var query = new QueryParams
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
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
                if (table != null) await table.ReloadServerData();
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

    // ========== 工单计划保存 ==========

    private async Task SavePlanAsync(WorkOrderScheduleDto item, string fieldName, object? value)
    {
        var request = new SaveWorkOrderPlanRequest
        {
            WorkOrderId = item.WorkOrderId,
        };

        switch (fieldName)
        {
            case "PlanScheduleStage":
                request.ScheduleStage = (int?)value;
                break;
            case "PlanUrgencyLevel":
                request.UrgencyLevel = (string?)value;
                break;
            case "PlanProductionAttentionProcess":
                request.ProductionAttentionProcess = (string?)value;
                break;
            case "PlanProductionFlowProperty":
                request.ProductionFlowProperty = (string?)value;
                break;
        }

        var result = await WorkOrderScheduleSvc.SavePlanAsync(request);
        if (result.Success)
        {
            Snackbar.Add("保存成功", Severity.Success);
        }
        else
        {
            Snackbar.Add($"保存失败: {result.Message}", Severity.Error);
        }
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));

        extras["columnVisibility"] = JsonSerializer.Serialize(_allColumns.Where(c => c.Visible).Select(c => c.Key).ToList());

        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPageIndex,
            Extras = extras
        };
        await PageState.SaveAsync("workorderschedules", state);
    }
}
