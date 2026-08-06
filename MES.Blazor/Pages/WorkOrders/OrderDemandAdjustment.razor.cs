using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Constants;
using MES.Core.Helpers;
using MES.Core.Models;
using MES.Core.Enums;
using MES.Core.DTOs.WorkOrder;
using MES.Core.DTOs.Shared;
using System.Reflection;
using System.Text.Json;

namespace MES.Blazor.Pages.WorkOrders;

public partial class OrderDemandAdjustment
{
    private MudTable<OrderDemandAdjustmentDto>? table;
    private List<OrderDemandAdjustmentDto> _pageItems = new();
    private int _totalCount;
    private int _restoredPageIndex;
    private int _currentPageIndex = 1;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;

    // 选中行（WorkOrderId）
    private HashSet<int> _selectedIds = new();

    // 方向键导航
    private bool _isArrowNavSetup;

    // B33 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "TotalQuantity", "TotalWeight"
    };

    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;

    // 排序状态
    private string sortColumn = "ScheduleStage";
    private bool sortDescending = true;

    // ========== 行选中 ==========

    private void SelectAllItems(bool selected)
    {
        if (selected)
        {
            foreach (var item in _pageItems)
                _selectedIds.Add(item.WorkOrderId);
        }
        else
        {
            _selectedIds.Clear();
        }
    }

    private void ToggleSelection(OrderDemandAdjustmentDto item, bool selected)
    {
        if (selected)
            _selectedIds.Add(item.WorkOrderId);
        else
            _selectedIds.Remove(item.WorkOrderId);
    }

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

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
            new() { Key = "SettlementMethod",        Label = "结算方式",        SortKey = "SettlementMethod",        FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetEnumFilterOptions<SettlementMethod>(), Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SalesOrderNo",            Label = "订单号",          SortKey = "SalesOrderNo",            FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionMainNo",        Label = "主号",            SortKey = "ProductionMainNo",        FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionSubNo",         Label = "次号",            SortKey = "ProductionSubNo",         FilterType = "string", Width = "120", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MaterialName",            Label = "钢管制造",        SortKey = "MaterialName",            FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetEnumFilterOptions<PipeManufacturingType>(), GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DeliveryState",           Label = "交货状态",        SortKey = "DeliveryState",           FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>(), Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "PlantGrade",              Label = "工厂牌号",        SortKey = "PlantGrade",              FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "Specification",           Label = "规格",            SortKey = "Specification",           FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "LengthStatus",            Label = "长度状态",        SortKey = "LengthStatus",            FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetEnumFilterOptions<LengthStatus>(), Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalQuantity",           Label = "总支数",          SortKey = "TotalQuantity",           Width = "80", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalWeight",             Label = "总重量",          SortKey = "TotalWeight",             Width = "80", GroupKey = 1, GroupName = "基础数据" },
        };

        // G12: 实时关注
        var g12 = new List<ColumnDef>
        {
            new() { Key = "ScheduleStage",           Label = "关注状态",      SortKey = "ScheduleStage",           FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetScheduleStageOptions(), GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "TotalRemainingWorkDays",  Label = "剩余总工量(天)",SortKey = "TotalRemainingWorkDays",  Width = "80",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "CapacityWorkDays",        Label = "产能工量(天)", SortKey = "CapacityWorkDays",        Width = "80",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "UrgencyLevel",            Label = "工单计划性",    SortKey = "UrgencyLevel",            FilterType = "string", Width = "120",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "EstimatedProcessCompletionDate",Label = "工艺预计完成日",SortKey = "EstimatedProcessCompletionDate", Width = "120",                  GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "DaysDiffFromDelivery",    Label = "交期相差天数",  SortKey = "DaysDiffFromDelivery",    Width = "80",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "RawMaterialLockRemark",   Label = "原锁备注",     SortKey = "RawMaterialLockRemark",   FilterType = "string", Width = "120",                             GroupKey = 12, GroupName = "实时关注" },
        };

        // G7: 有效流转
        var g7 = new List<ColumnDef>
        {
            new() { Key = "FlowOutputRatio",          Label = "流转成品比(%)",          SortKey = "FlowOutputRatio",          Width = "100", GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowStatus",               Label = "有效流转状态",           SortKey = "FlowStatus",               FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetFlowStatusOptions(), GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "MainNoFlowOutputRatio",    Label = "有效主号流转比(%)",     SortKey = "MainNoFlowOutputRatio",    Width = "100", GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "MainNoFlowStatus",          Label = "有效主号状态",          SortKey = "MainNoFlowStatus",          FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetMainNoFlowStatusOptions(), GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowTotalBatchCount",       Label = "总批次数",              SortKey = "FlowTotalBatchCount",       Width = "80", GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowIncompleteBatchCount",  Label = "未完成批数",            SortKey = "FlowIncompleteBatchCount",  Width = "80", GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowMaxRemainingWorkDays",  Label = "最大剩余工量(天)",      SortKey = "FlowMaxRemainingWorkDays",  Width = "100", GroupKey = 7, GroupName = "有效流转" },
        };

        // G13: 工单需求调整（手工编辑）
        var g13 = new List<ColumnDef>
        {
            new() { Key = "IsUrging",      Label = "催单",  SortKey = "IsUrging",      FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "工单需求调整" },
            new() { Key = "IsBatchDelivery",          Label = "分批交货",      SortKey = "IsBatchDelivery",          FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "工单需求调整" },
            new() { Key = "IsPaused",                  Label = "工单暂停",      SortKey = "IsPaused",                  FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "工单需求调整" },
            new() { Key = "AdjustmentRemark",          Label = "需求调整备注",  SortKey = "AdjustmentRemark",          FilterType = "string", Width = "200", GroupKey = 13, GroupName = "工单需求调整" },
        };

        var all = new List<ColumnDef>();
        all.AddRange(g1);
        all.AddRange(g12);
        all.AddRange(g7);
        all.AddRange(g13);
        return all;
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<OrderDemandAdjustmentDto>> LoadDataFromServer(TableState state)
    {
        // 保持 RowsPerPage 与用户选择同步，避免排序/筛选后复位
        _pageSize = state.PageSize;

        // 恢复持久化的页码（MudTable 初始化时始终传 page=0）
        if (_isFirstLoad)
        {
            state.Page = _restoredPageIndex;
            _isFirstLoad = false;
        }

        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "ScheduleStage";
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

            var result = await DemandAdjustmentService.GetPagedAsync(
                query,
                dateFrom: DateTime.TryParse(_dateFrom, out var dFrom) ? dFrom : null,
                dateTo: DateTime.TryParse(_dateTo, out var dTo) ? dTo : null);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                ComputePageSums();
                _totalCount = result.Data.TotalCount;
                _currentPageIndex = state.Page + 1;
                await SavePageStateAsync();
            }
            else
            {
                _pageItems = new();
                _pageSums.Clear();
                _totalCount = 0;
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
        }

        return new TableData<OrderDemandAdjustmentDto>
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

    // ========== 筛选上下文加载（ExcelFilter 下拉选项） ==========

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await DemandAdjustmentService.GetFilterContextsAsync();
            if (result.Success && result.Data != null)
            {
                BuildFilterContextOptions(result.Data);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载筛选上下文失败: {ex.Message}", Severity.Warning);
        }
    }

    private void BuildFilterContextOptions(Dictionary<string, List<string>> filterContexts)
    {
        _filterContextOptions.Clear();
        foreach (var kvp in filterContexts)
        {
            _filterContextOptions[kvp.Key] = kvp.Value.Select(v => new ExcelFilterOption
            {
                Value = v,
                Display = kvp.Key switch
                {
                    "UrgencyLevel" => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, v) ?? v,
                    "RawMaterialLockRemark" => RawMaterialLockRemarkKeys.ToChinese(v) ?? v,
                    _ => v
                },
                Count = 0
            }).ToList();
        }

        // DelayPenalty 列显示中文
        if (_filterContextOptions.TryGetValue("DelayPenalty", out var delayOptions))
        {
            foreach (var opt in delayOptions)
                opt.Display = opt.Value == "True" ? "是" : "否";
        }

        // 补充枚举列筛选选项（后端不返回枚举列 DISTINCT 值）
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

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
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

    private async Task OnDateFromChanged(string value)
    {
        _dateFrom = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnDateToChanged(string value)
    {
        _dateTo = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 内联编辑 ==========

    private async Task ToggleUrging(OrderDemandAdjustmentDto item)
    {
        item.IsUrging = !item.IsUrging;
        await SaveUrgingAsync(item);
    }

    private async Task OnAdjustmentRemarkChanged(OrderDemandAdjustmentDto item, string? newValue)
    {
        item.AdjustmentRemark = newValue;
        await SaveUrgingAsync(item);
    }

    private async Task SaveUrgingAsync(OrderDemandAdjustmentDto item)
    {
        try
        {
            var result = await DemandAdjustmentService.SaveUrgingAsync(item.WorkOrderId, item.IsUrging, item.IsBatchDelivery, item.IsPaused, item.AdjustmentRemark);
            if (result.Success)
            {
                Snackbar.Add("保存成功", Severity.Success);
            }
            else
            {
                Snackbar.Add(result.Message ?? "保存失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"保存失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 分组标题栏同步 ==========

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 分组标题栏：测量实际列宽 + 同步滚动
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#order-demand-adjustment-list-table");
        }
        catch { }

        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#order-demand-adjustment-list-table"))
                _isArrowNavSetup = false;
        }
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

    // ========== 分组 CSS ==========

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1",
            7 => "col-g7",
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
            7 => "col-g7-cell",
            12 => "col-g12-cell",
            13 => "col-g13-cell",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();

        // 恢复排序/筛选/列显隐状态
        var savedState = await PageState.LoadAsync("order-demand-adjustment");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "ScheduleStage";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _dateFrom = savedState.Extras?.ContainsKey("dateFrom") == true ? savedState.Extras["dateFrom"] ?? string.Empty : string.Empty;
            _dateTo = savedState.Extras?.ContainsKey("dateTo") == true ? savedState.Extras["dateTo"] ?? string.Empty : string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);

            // 恢复列显隐
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

            // 恢复列筛选
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

        // 状态恢复后重新加载表格数据
        if (savedState != null && table != null)
            await table.ReloadServerData();

        // 加载筛选上下文
        await LoadFilterContextsAsync();
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(OrderDemandAdjustmentDto item, ColumnDef col) => builder =>
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
                builder.AddContent(0, DisplayHelper.GetPipeManufacturingTypeText(item.MaterialName));
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
            case "TotalQuantity":
                builder.AddContent(0, item.TotalQuantity);
                break;
            case "TotalWeight":
                builder.AddContent(0, ((int)item.TotalWeight).ToString());
                break;
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
                builder.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, item.UrgencyLevel) ?? "-");
                break;
            case "EstimatedProcessCompletionDate":
                builder.AddContent(0, item.EstimatedProcessCompletionDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "DaysDiffFromDelivery":
                builder.AddContent(0, item.DaysDiffFromDelivery.HasValue ? $"{item.DaysDiffFromDelivery}天" : "-");
                break;
            case "RawMaterialLockRemark":
                builder.AddContent(0, RawMaterialLockRemarkKeys.ToChinese(item.RawMaterialLockRemark) ?? "-");
                break;
            case "FlowOutputRatio":
                builder.AddContent(0, $"{item.FlowOutputRatio.ToString("F1")}%");
                break;
            case "FlowStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetFlowStatusColor(item.FlowStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetFlowStatusText(item.FlowStatus))));
                builder.CloseComponent();
                break;
            case "MainNoFlowOutputRatio":
                builder.AddContent(0, $"{item.MainNoFlowOutputRatio.ToString("F1")}%");
                break;
            case "MainNoFlowStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetMainNoFlowStatusColor(item.MainNoFlowStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetMainNoFlowStatusText(item.MainNoFlowStatus))));
                builder.CloseComponent();
                break;
            case "FlowTotalBatchCount":
                builder.AddContent(0, item.FlowTotalBatchCount);
                break;
            case "FlowIncompleteBatchCount":
                builder.AddContent(0, item.FlowIncompleteBatchCount);
                break;
            case "FlowMaxRemainingWorkDays":
                builder.AddContent(0, $"{item.FlowMaxRemainingWorkDays}天");
                break;
            case "IsUrging":
                // 内联编辑：Switch 切换催单状态
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "style", "display:flex; align-items:center; gap:4px;");
                builder.OpenComponent<MudSwitch<bool>>(2);
                builder.AddAttribute(3, "Value", item.IsUrging);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<bool>(this, async v =>
                {
                    item.IsUrging = v;
                    await SaveUrgingAsync(item);
                }));
                builder.AddAttribute(5, "Color", Color.Primary);
                builder.AddAttribute(6, "Dense", true);
                builder.CloseComponent();
                builder.AddContent(7, item.IsUrging ? "是" : "否");
                builder.CloseElement();
                break;
            case "IsBatchDelivery":
                // 内联编辑：Switch 切换分批交货
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "style", "display:flex; align-items:center; gap:4px;");
                builder.OpenComponent<MudSwitch<bool>>(2);
                builder.AddAttribute(3, "Value", item.IsBatchDelivery);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<bool>(this, async v =>
                {
                    item.IsBatchDelivery = v;
                    await SaveUrgingAsync(item);
                }));
                builder.AddAttribute(5, "Color", Color.Primary);
                builder.AddAttribute(6, "Dense", true);
                builder.CloseComponent();
                builder.AddContent(7, item.IsBatchDelivery ? "是" : "否");
                builder.CloseElement();
                break;
            case "IsPaused":
                // 内联编辑：Switch 切换暂停状态
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "style", "display:flex; align-items:center; gap:4px;");
                builder.OpenComponent<MudSwitch<bool>>(2);
                builder.AddAttribute(3, "Value", item.IsPaused);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<bool>(this, async v =>
                {
                    item.IsPaused = v;
                    await SaveUrgingAsync(item);
                }));
                builder.AddAttribute(5, "Color", Color.Error);
                builder.AddAttribute(6, "Dense", true);
                builder.CloseComponent();
                builder.AddContent(7, item.IsPaused ? "是" : "否");
                builder.CloseElement();
                break;
            case "AdjustmentRemark":
                // 内联编辑：文本框
                builder.OpenComponent<MudTextField<string?>>(0);
                builder.AddAttribute(1, "Value", item.AdjustmentRemark);
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string?>(this, async v =>
                {
                    await OnAdjustmentRemarkChanged(item, v);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Immediate", true);
                builder.AddAttribute(5, "DebounceInterval", (double)800);
                builder.AddAttribute(6, "Class", "inline-edit-textfield");
                builder.AddAttribute(7, "Style", "min-width:120px;");
                builder.CloseComponent();
                break;
        }
    };

    // ========== 颜色 ==========

    private static Color GetFlowStatusColor(int status) => status switch
    {
        0 => Color.Default,
        1 => Color.Warning,
        2 => Color.Success,
        _ => Color.Default
    };

    private static Color GetMainNoFlowStatusColor(int status) => status switch
    {
        0 => Color.Default,
        1 => Color.Warning,
        2 => Color.Success,
        _ => Color.Default
    };

    // ========== 打印 ==========

    private async Task PrintAll()
    {
        try
        {
            var printColumns = _visibleColumns
                .Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label })
                .ToList();

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "ScheduleStage";
            var request = new
            {
                keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy,
                isDescending = sortDescending,
                signDateFrom = DateTime.TryParse(_dateFrom, out var dFrom) ? dFrom.ToString("yyyy-MM-dd") : null,
                signDateTo = DateTime.TryParse(_dateTo, out var dTo) ? dTo.ToString("yyyy-MM-dd") : null,
                columns = printColumns
            };

            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/order-demand-adjustment/print-all-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task PrintSelected()
    {
        if (_selectedIds.Count == 0)
        {
            Snackbar.Add("请先选择要打印的行", Severity.Warning);
            return;
        }

        try
        {
            var printColumns = _visibleColumns
                .Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label })
                .ToList();

            // 只打印当前页面选中的行
            var selectedItems = _pageItems.Where(i => _selectedIds.Contains(i.WorkOrderId)).ToList();

            var printItems = selectedItems.Select(item =>
            {
                var dict = new Dictionary<string, object>();
                foreach (var col in _visibleColumns)
                {
                    dict[col.Key] = ResolvePrintValue(item, col.Key);
                }
                return dict;
            }).ToList();

            var request = new OrderDemandAdjustmentPrintRequest
            {
                Title = "工单需求调整",
                Items = printItems,
                Columns = printColumns
            };

            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/order-demand-adjustment/print-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private static object ResolvePrintValue(OrderDemandAdjustmentDto item, string key) => key switch
    {
        // 枚举→中文
        "MaterialName" => DisplayHelper.GetPipeManufacturingTypeText(item.MaterialName) ?? "",
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState) ?? "",
        "LengthStatus" => DisplayHelper.GetLengthStatusText(item.LengthStatus) ?? "",
        "SettlementMethod" => DisplayHelper.GetSettlementMethodText(item.SettlementMethod) ?? "",
        "DelayPenalty" => item.DelayPenaltyText,
        "ScheduleStage" => item.ScheduleStageText,
        "FlowStatus" => DisplayHelper.GetFlowStatusText(item.FlowStatus),
        "MainNoFlowStatus" => DisplayHelper.GetMainNoFlowStatusText(item.MainNoFlowStatus),
        "IsUrging" => item.IsUrging ? "是" : "否",
        "IsBatchDelivery" => item.IsBatchDelivery ? "是" : "否",
        "IsPaused" => item.IsPaused ? "是" : "否",
        "AdjustmentRemark" => item.AdjustmentRemark ?? "",
        _ => GetRawPropertyValue(item, key)
    };

    private static object GetRawPropertyValue(OrderDemandAdjustmentDto item, string key) => (key switch
    {
        "WorkOrderNo" => item.WorkOrderNo ?? "",
        "Salesman" => item.Salesman ?? "",
        "CustomerName" => item.CustomerName ?? "",
        "SignDate" => item.SignDate,
        "DeliveryDate" => item.DeliveryDate,
        "SalesOrderNo" => item.SalesOrderNo ?? "",
        "ProductionMainNo" => item.ProductionMainNo ?? "",
        "ProductionSubNo" => item.ProductionSubNo ?? "",
        "PlantGrade" => item.PlantGrade ?? "",
        "Specification" => item.Specification ?? "",
        "TotalQuantity" => item.TotalQuantity,
        "TotalWeight" => item.TotalWeight,
        "TotalRemainingWorkDays" => item.TotalRemainingWorkDays,
        "CapacityWorkDays" => item.CapacityWorkDays,
        "UrgencyLevel" => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, item.UrgencyLevel) ?? "",
        "EstimatedProcessCompletionDate" => item.EstimatedProcessCompletionDate,
        "DaysDiffFromDelivery" => item.DaysDiffFromDelivery,
        "RawMaterialLockRemark" => RawMaterialLockRemarkKeys.ToChinese(item.RawMaterialLockRemark) ?? "",
        "FlowOutputRatio" => item.FlowOutputRatio,
        "MainNoFlowOutputRatio" => item.MainNoFlowOutputRatio,
        "FlowTotalBatchCount" => item.FlowTotalBatchCount,
        "FlowIncompleteBatchCount" => item.FlowIncompleteBatchCount,
        "FlowMaxRemainingWorkDays" => item.FlowMaxRemainingWorkDays,
        _ => ""
    })!;

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(_dateFrom)) extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrWhiteSpace(_dateTo)) extras["dateTo"] = _dateTo;
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));

        // 列显隐持久化
        extras["columnVisibility"] = JsonSerializer.Serialize(_allColumns.Where(c => c.Visible).Select(c => c.Key).ToList());

        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPageIndex,
            Extras = extras
        };
        await PageState.SaveAsync("order-demand-adjustment", state);
    }

    private void ComputePageSums()
    {
        _pageSums.Clear();
        var props = typeof(OrderDemandAdjustmentDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var key in _summableColumnKeys)
        {
            var prop = props.FirstOrDefault(p => p.Name == key);
            if (prop == null) continue;
            decimal sum = 0;
            foreach (var item in _pageItems)
            {
                var val = prop.GetValue(item);
                if (val == null) continue;
                sum += Convert.ToDecimal(val);
            }
            _pageSums[key] = ((int)sum).ToString();
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        return _pageSums.GetValueOrDefault(col.Key, "");
    }
}
