using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Enums;
using MES.Core.Constants;
using MES.Core.Helpers;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.WorkOrder;
using System.Text.Json;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.WorkOrders;

public partial class MaterialPlanOverview
{
    private MudTable<WorkOrderListDto>? table;
    private List<WorkOrderListDto> _pageItems = new();
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new() { "TotalWeight", "TotalItemCount", "TotalQuantity" };
    private int _totalCount;
    private string errorMessage = string.Empty;
    // 选中状态
    private bool _allSelected;
    private bool allSelected
    {
        get => _allSelected;
        set
        {
            if (_allSelected == value) return;
            _allSelected = value;
            if (value)
            {
                foreach (var item in _pageItems)
                    selectedWorkOrderIds.Add(item.Id);
            }
            else
            {
                selectedWorkOrderIds.Clear();
            }
            StateHasChanged();
        }
    }
    private HashSet<int> selectedWorkOrderIds = new();
    private int _currentPage = 1;
    private int _pageSize = 10;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _loadVersion;
    private bool _resetToFirstPage;
    private bool _isArrowNavSetup;
    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;
    private string _deliveryDateFrom = string.Empty;
    private string _deliveryDateTo = string.Empty;

    private string sortColumn = "CreatedTime";
    private bool sortDescending = true;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // 计划类型筛选
    private bool includeSemi = true;
    private bool includeFinish = true;
    private bool includeInventory = true;
    private bool includeRework = true;
    private bool includePiercing = true;
    private bool includeInProcessRework = true;
    private bool includeInMainWorkOrder = true;
    private bool anyPlanTypeSelected => includeSemi || includeFinish || includeInventory || includeRework || includePiercing || includeInProcessRework || includeInMainWorkOrder;

    // ========== 列定义 ==========

    // 列偏好持久化 key（带版本号：列定义变更后自动丢弃旧偏好，强制采用新默认显隐/顺序）
    private const string ColumnPrefsKey = "materialPlanOverview_v3";

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // ========== 1 基础数据 ==========
        new() { Key = "WorkOrderNo",        Label = "工单号",     SortKey = "WorkOrderNo", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "SalesOrderNo",       Label = "订单号",     SortKey = "SalesOrderNo", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "ProductionMainNo",   Label = "主号",       SortKey = "ProductionMainNo", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "ProductionSubNo",    Label = "次号",       SortKey = "ProductionSubNo", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据", Visible = false },
        new() { Key = "Salesman",           Label = "业务员",     SortKey = "Salesman", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "EndCustomer",        Label = "最终用户",   SortKey = "EndCustomer", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "MaterialName",       Label = "钢管制造",   SortKey = "MaterialName", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "基础数据", Visible = false,
            EnumOptions = DisplayHelper.GetEnumFilterOptions<PipeManufacturingType>() },
        new() { Key = "SettlementMethod",   Label = "结算方式",   SortKey = "SettlementMethod", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "基础数据", Visible = false,
            EnumOptions = DisplayHelper.GetEnumFilterOptions<SettlementMethod>() },
        new() { Key = "DelayPenalty",       Label = "延期罚款",   SortKey = "DelayPenalty", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "基础数据", Visible = false,
            EnumOptions = DisplayHelper.GetBoolOptions() },
        new() { Key = "SignDate",           Label = "签订日期",   SortKey = "SignDate", FilterType = "date", Width = "120", GroupKey = 1, GroupName = "基础数据", Visible = false },
        new() { Key = "DeliveryDate",       Label = "交货日期",   SortKey = "DeliveryDate", FilterType = "date", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "DeliveryState",      Label = "交货状态",   SortKey = "DeliveryState", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "基础数据",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>() },
        new() { Key = "PlantGrade",         Label = "工厂牌号",   SortKey = "PlantGrade", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "Specification",      Label = "规格",       SortKey = "Specification", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "LengthStatus",       Label = "长度状态",   SortKey = "LengthStatus", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "基础数据",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<LengthStatus>() },
        new() { Key = "TotalItemCount",     Label = "含项次数",   SortKey = "TotalItemCount", Width = "80", GroupKey = 1, GroupName = "基础数据", Visible = false },
        new() { Key = "MinLength",          Label = "最小长度",   SortKey = "MinLength", Width = "80", GroupKey = 1, GroupName = "基础数据", Visible = false },
        new() { Key = "MaxLength",          Label = "最大长度",   SortKey = "MaxLength", Width = "80", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "TotalQuantity",      Label = "总支数",     SortKey = "TotalQuantity", Width = "80", GroupKey = 1, GroupName = "基础数据" },
        new() { Key = "TotalWeight",        Label = "总重量",     SortKey = "TotalWeight", Width = "80", GroupKey = 1, GroupName = "基础数据" },

        // ========== 2 实时关注（来源 WorkOrderExecutionSummary，主号级） ==========
        new() { Key = "ScheduleStage",          Label = "主号-关注",      SortKey = "ScheduleStage",          FilterType = "enum", Width = "110",
            EnumOptions = DisplayHelper.GetScheduleStageOptions(), GroupKey = 2, GroupName = "实时关注", Level = ColumnLevel.MainNo },
        new() { Key = "RawMaterialLockRemark",  Label = "主号-原锁备注",  SortKey = "RawMaterialLockRemark",  FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "实时关注", Level = ColumnLevel.MainNo },
        new() { Key = "UrgencyLevel",           Label = "主号-计划性",    SortKey = "UrgencyLevel",           FilterType = "string", Width = "110",
            GroupKey = 2, GroupName = "实时关注", Level = ColumnLevel.MainNo },

        // ========== 3 用料计划 ==========
        new() { Key = "OrderMaterialPlanStatus", Label = "订单-关联用料态", SortKey = "OrderMaterialPlanStatus", FilterType = "enum", Width = "120", GroupKey = 3, GroupName = "用料计划", Visible = false,
            EnumOptions = DisplayHelper.GetMaterialPlanStatusOptions(), Level = ColumnLevel.Order },
        new() { Key = "MainNoMaterialPlanStatus",Label = "主号-关联用料态", SortKey = "MainNoMaterialPlanStatus", FilterType = "enum", Width = "120", GroupKey = 3, GroupName = "用料计划",
            EnumOptions = DisplayHelper.GetMaterialPlanStatusOptions(), Level = ColumnLevel.MainNo },
        new() { Key = "MaterialPlanStatus",      Label = "工单用料计划",   SortKey = "MaterialPlanStatus", FilterType = "enum", Width = "120", GroupKey = 3, GroupName = "用料计划",
            EnumOptions = DisplayHelper.GetMaterialPlanStatusOptions() },
        new() { Key = "MaterialPlanRate",        Label = "工单满足率",     SortKey = "MaterialPlanRate", Width = "80", GroupKey = 3, GroupName = "用料计划" },
        new() { Key = "LatestPlanDate",          Label = "计划日期",       SortKey = "LatestPlanDate", FilterType = "date", Width = "120", GroupKey = 3, GroupName = "用料计划" },
        new() { Key = "PlanProportion",          Label = "分类用料占比",   SortKey = "MaterialPlanProportion", Width = "120", GroupKey = 3, GroupName = "用料计划" },
        new() { Key = "MaterialPlanCoveredCount",Label = "料态种数",      SortKey = "MaterialPlanCoveredCount", Width = "80", GroupKey = 3, GroupName = "用料计划", Visible = false },
        new() { Key = "LatestRequiredDate",      Label = "要求到货日",    SortKey = "LatestRequiredDate", FilterType = "date", Width = "120", GroupKey = 3, GroupName = "用料计划", Visible = false },
        new() { Key = "TheoreticalCutoffDate",  Label = "理论截止投料日", SortKey = "TheoreticalCutoffDate", FilterType = "date", Width = "120", GroupKey = 3, GroupName = "用料计划" },
        new() { Key = "MaxStandardCycle",       Label = "工单最大工艺周期", SortKey = "MaxStandardCycle", Width = "80", GroupKey = 3, GroupName = "用料计划", Visible = false },
        new() { Key = "MainNoMaxStandardCycle", Label = "主号最大工艺周期", SortKey = "MainNoMaxStandardCycle", Width = "80", GroupKey = 3, GroupName = "用料计划", Visible = false, Level = ColumnLevel.MainNo },
        new() { Key = "CapacityWorkDays",       Label = "主号产能工量",   SortKey = "CapacityWorkDays", Width = "80", GroupKey = 3, GroupName = "用料计划", Visible = false, Level = ColumnLevel.MainNo },
    };

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;
        var props = typeof(WorkOrderListDto)
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
        if (_pageSums.TryGetValue(col.Key, out var sum)) return sum;
        return "-";
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<WorkOrderListDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        // 首次加载覆盖页码（MudTable 初始化时始终传 page=0）
        if (_isFirstLoad)
        {
            state.Page = _restoredPageIndex;
            _isFirstLoad = false;
        }

        if (_resetToFirstPage)
        {
            state.Page = 0;
            _resetToFirstPage = false;
        }

        try
        {
            errorMessage = string.Empty;
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "CreatedTime";
            var filtersJson = SerializeFilters();
            var planTypeFilter = BuildPlanTypeFilter();

            var result = await WorkOrderService.GetPagedWithPlansAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
                filters: filtersJson,
                planTypeFilter: planTypeFilter,
                signDateFrom: DateTime.TryParse(_dateFrom, out var dFrom) ? dFrom : null,
                signDateTo: DateTime.TryParse(_dateTo, out var dTo) ? dTo : null,
                deliveryDateStart: DateTime.TryParse(_deliveryDateFrom, out var ddf) ? ddf : null,
                deliveryDateEnd: DateTime.TryParse(_deliveryDateTo, out var ddt) ? ddt : null
            );

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<WorkOrderListDto> { Items = _pageItems, TotalItems = _totalCount };

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;
            }
            else
            {
                _pageItems = new();
                _totalCount = 0;
                errorMessage = result?.Message ?? "查询失败";
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"加载异常: {ex.Message}";
            Snackbar.Add(errorMessage, Severity.Error);
            _pageItems = new();
            _totalCount = 0;
        }

        ComputePageSums();
        await SavePageStateAsync();

        return new TableData<WorkOrderListDto>
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

    private string? BuildPlanTypeFilter()
    {
        var planTypes = new List<string>();
        if (includePiercing) planTypes.Add("piercing");
        if (includeSemi) planTypes.Add("semi");
        if (includeFinish) planTypes.Add("finish");
        if (includeInventory) planTypes.Add("inventory");
        if (includeRework) planTypes.Add("rework");
        if (includeInProcessRework) planTypes.Add("inprocess");
        if (includeInMainWorkOrder) planTypes.Add("inmain");
        return planTypes.Count < 7 ? string.Join(",", planTypes) : null;
    }

    // ========== 筛选上下文加载（ExcelFilter 下拉选项） ==========

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await WorkOrderService.GetFilterContextsAsync();
            if (result.Success && result.Data != null)
            {
                _filterContextOptions.Clear();
                foreach (var kvp in result.Data)
                {
                    _filterContextOptions[kvp.Key] = kvp.Value.Select(v => new ExcelFilterOption
                    {
                        Value = v,
                        Display = kvp.Key switch
                        {
                            "UrgencyLevel" => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, v) ?? v,
                            "RawMaterialLockRemark" => DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, v) ?? v,
                            _ => v
                        },
                        Count = 0
                    }).ToList();
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
                        _filterContextOptions[col.Key] = DisplayHelper.GetBoolFilterOptions(col);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载筛选上下文失败: {ex.Message}", Severity.Warning);
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
        _resetToFirstPage = true;
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

    private async Task OnDeliveryDateFromChanged(string value)
    {
        _deliveryDateFrom = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnDeliveryDateToChanged(string value)
    {
        _deliveryDateTo = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 计划类型筛选 ==========

    private async Task OnPlanTypeFilterChangedAsync(bool value, int planType)
    {
        switch (planType)
        {
            case 1: includeSemi = value; break;
            case 2: includeFinish = value; break;
            case 3: includeInventory = value; break;
            case 4: includeRework = value; break;
            case 5: includePiercing = value; break;
            case 6: includeInProcessRework = value; break;
            case 7: includeInMainWorkOrder = value; break;
        }
        selectedWorkOrderIds.Clear();
        _allSelected = false;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnUp(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnDown(ColumnDef col) => await SaveColumnPrefs();

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync(ColumnPrefsKey, null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();

        var saved = await ColumnPrefs.LoadAsync(ColumnPrefsKey, null);
        if (saved.Count > 0)
        {
            foreach (var s in saved)
            {
                var match = _allColumns.FirstOrDefault(c => c.Key == s.Key);
                if (match != null)
                    match.Visible = s.Visible;
            }
            var reordered = new List<ColumnDef>();
            foreach (var s in saved)
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

        // 从 PageState 恢复排序/筛选状态
        var savedState = await PageState.LoadAsync("materialplan");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "CreatedTime";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _dateFrom = savedState.Extras?.ContainsKey("dateFrom") == true ? savedState.Extras["dateFrom"] ?? string.Empty : string.Empty;
            _dateTo = savedState.Extras?.ContainsKey("dateTo") == true ? savedState.Extras["dateTo"] ?? string.Empty : string.Empty;
            _deliveryDateFrom = savedState.Extras?.ContainsKey("deliveryDateFrom") == true ? savedState.Extras["deliveryDateFrom"] ?? string.Empty : string.Empty;
            _deliveryDateTo = savedState.Extras?.ContainsKey("deliveryDateTo") == true ? savedState.Extras["deliveryDateTo"] ?? string.Empty : string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
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
            // 恢复计划类型筛选状态
            if (savedState.Extras?.ContainsKey("includePiercing") == true)
                bool.TryParse(savedState.Extras["includePiercing"], out includePiercing);
            if (savedState.Extras?.ContainsKey("includeSemi") == true)
                bool.TryParse(savedState.Extras["includeSemi"], out includeSemi);
            if (savedState.Extras?.ContainsKey("includeFinish") == true)
                bool.TryParse(savedState.Extras["includeFinish"], out includeFinish);
            if (savedState.Extras?.ContainsKey("includeInventory") == true)
                bool.TryParse(savedState.Extras["includeInventory"], out includeInventory);
            if (savedState.Extras?.ContainsKey("includeRework") == true)
                bool.TryParse(savedState.Extras["includeRework"], out includeRework);
            if (savedState.Extras?.ContainsKey("includeInProcessRework") == true)
                bool.TryParse(savedState.Extras["includeInProcessRework"], out includeInProcessRework);
            if (savedState.Extras?.ContainsKey("includeInMainWorkOrder") == true)
                bool.TryParse(savedState.Extras["includeInMainWorkOrder"], out includeInMainWorkOrder);
        }

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && table != null)
            await table.ReloadServerData();

        // 加载筛选上下文（ExcelFilter 下拉选项）
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#material-plan-overview-list-table"))
                _isArrowNavSetup = false;
        }

        // 分组标题栏：测量实际列宽 + 同步滚动
        await JS.InvokeVoidAsync("initGroupHeaders", "#material-plan-overview-list-table");
    }

    // ========== 选中 ==========

    private void ToggleSelectWorkOrder(int id, bool selected)
    {
        if (selected)
            selectedWorkOrderIds.Add(id);
        else
            selectedWorkOrderIds.Remove(id);

        _allSelected = _pageItems.Any() && _pageItems.All(i => selectedWorkOrderIds.Contains(i.Id));
        StateHasChanged();
    }

    // ========== 导航 ==========

    private void NavigateToMaterialPlan(int id)
    {
        Navigation.NavigateTo($"/workorders/{id}/material-plan");
    }

    // ========== 辅助方法 ==========

    private Color GetStatusColor(MaterialPlanStatus status)
    {
        return status switch
        {
            MaterialPlanStatus.NotPlanned => Color.Default,
            MaterialPlanStatus.Partial => Color.Warning,
            MaterialPlanStatus.Satisfied => Color.Success,
            MaterialPlanStatus.Excess => Color.Default,
            _ => Color.Default
        };
    }

    private string GetStatusText(MaterialPlanStatus status) => DisplayHelper.GetMaterialPlanStatusText(status);

    // "超量"采用深色底白字 Chip（其余档位默认色）
    private static string GetStatusChipClass(MaterialPlanStatus status)
        => status == MaterialPlanStatus.Excess ? "chip-dark" : "";

    // ========== 单元格原始值/显示值 ==========

    private string? GetCellRawValue(WorkOrderListDto item, string key) => key switch
    {
        "WorkOrderNo" => item.WorkOrderNo,
        "SalesOrderNo" => item.SalesOrderNo,
        "ProductionMainNo" => item.ProductionMainNo,
        "ProductionSubNo" => item.ProductionSubNo,
        "SignDate" => item.SignDate.ToString("yyyy-MM-dd"),
        "Salesman" => item.Salesman,
        "EndCustomer" => item.EndCustomer,
        "DeliveryDate" => item.DeliveryDate.ToString("yyyy-MM-dd"),
        "DelayPenalty" => DisplayHelper.GetYesNoText(item.DelayPenalty),
        "SettlementMethod" => DisplayHelper.GetSettlementMethodText(item.SettlementMethod),
        "PlantGrade" => item.PlantGrade,
        "Specification" => item.Specification,
        "MaterialName" => DisplayHelper.GetPipeManufacturingTypeText(item.PipeManufacturingType),
        "LengthStatus" => DisplayHelper.GetWorkOrderLengthStatusText(item.LengthStatus, item.MinLength, item.MaxLength),
        "MaxLength" => item.MaxLength?.ToString("G29"),
        "MinLength" => item.MinLength?.ToString("G29"),
        "TotalQuantity" => item.TotalQuantity.ToString("G29"),
        "TotalWeight" => ((int)item.TotalWeight).ToString(),
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState),
        "TotalItemCount" => item.TotalItemCount.ToString("G29"),
        "LatestPlanDate" => item.LatestPlanDate?.ToString("yyyy-MM-dd"),
        "MaterialPlanStatus" => DisplayHelper.GetMaterialPlanStatusText(item.MaterialPlanStatus),
        "MaterialPlanRate" => $"{item.MaterialPlanRate:F1}%",
        "MainNoMaterialPlanStatus" => GetStatusText(item.MainNoMaterialPlanStatus),
        "OrderMaterialPlanStatus" => GetStatusText(item.OrderMaterialPlanStatus),
        "MaterialPlanCoveredCount" => item.MaterialPlanCoveredCount.ToString(),
        "LatestRequiredDate" => item.LatestRequiredDate?.ToString("yyyy-MM-dd"),
        "MainNoMaxStandardCycle" => item.MainNoMaxStandardCycle.ToString(),
        "CapacityWorkDays" => item.CapacityWorkDays?.ToString() ?? "-",
        "TheoreticalCutoffDate" => item.TheoreticalCutoffDate?.ToString("yyyy-MM-dd"),
        _ => null
    };

    private string? GetCellDisplayText(WorkOrderListDto item, string key) => key switch
    {
        "DelayPenalty" => DisplayHelper.GetYesNoText(item.DelayPenalty),
        "SettlementMethod" => DisplayHelper.GetSettlementMethodText(item.SettlementMethod),
        "MaterialName" => DisplayHelper.GetPipeManufacturingTypeText(item.PipeManufacturingType),
        "LengthStatus" => DisplayHelper.GetWorkOrderLengthStatusText(item.LengthStatus, item.MinLength, item.MaxLength),
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState),
        "MaterialPlanStatus" => DisplayHelper.GetMaterialPlanStatusText(item.MaterialPlanStatus),
        "MainNoMaterialPlanStatus" => GetStatusText(item.MainNoMaterialPlanStatus),
        "OrderMaterialPlanStatus" => GetStatusText(item.OrderMaterialPlanStatus),
        _ => GetCellRawValue(item, key) ?? ""
    };

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

    // ========== 分组 CSS class ==========

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1",
            2 => "col-g2",
            3 => "col-g3",
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
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(WorkOrderListDto wo, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "WorkOrderNo":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => NavigateToMaterialPlan(wo.Id)));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, wo.WorkOrderNo)));
                builder.CloseComponent();
                break;
            case "SalesOrderNo":
                builder.AddContent(0, wo.SalesOrderNo);
                break;
            case "ProductionMainNo":
                builder.AddContent(0, wo.ProductionMainNo);
                break;
            case "ProductionSubNo":
                builder.AddContent(0, wo.ProductionSubNo);
                break;
            case "SignDate":
                builder.AddContent(0, wo.SignDate.ToString("yyyy-MM-dd"));
                break;
            case "Salesman":
                builder.AddContent(0, wo.Salesman);
                break;
            case "EndCustomer":
                builder.AddContent(0, wo.EndCustomer ?? "-");
                break;
            case "DeliveryDate":
                builder.AddContent(0, wo.DeliveryDate.ToString("yyyy-MM-dd"));
                break;
            case "DelayPenalty":
                builder.AddContent(0, DisplayHelper.GetYesNoText(wo.DelayPenalty));
                break;
            case "SettlementMethod":
                builder.AddContent(0, DisplayHelper.GetSettlementMethodText(wo.SettlementMethod));
                break;
            case "PlantGrade":
                builder.AddContent(0, wo.PlantGrade);
                break;
            case "Specification":
                builder.AddContent(0, wo.Specification);
                break;
            case "MaterialName":
                builder.AddContent(0, DisplayHelper.GetPipeManufacturingTypeText(wo.PipeManufacturingType));
                break;
            case "LengthStatus":
                builder.AddContent(0, DisplayHelper.GetWorkOrderLengthStatusText(wo.LengthStatus, wo.MinLength, wo.MaxLength));
                break;
            case "MaxLength":
                builder.AddContent(0, wo.MaxLength?.ToString("G29") ?? "-");
                break;
            case "MinLength":
                builder.AddContent(0, wo.MinLength?.ToString("G29") ?? "-");
                break;
            case "TotalQuantity":
                builder.AddContent(0, wo.TotalQuantity);
                break;
            case "TotalWeight":
                builder.AddContent(0, ((int)wo.TotalWeight).ToString());
                break;
            case "DeliveryState":
                builder.AddContent(0, DisplayHelper.GetDeliveryStateText(wo.DeliveryState));
                break;
            case "TotalItemCount":
                builder.AddContent(0, wo.TotalItemCount);
                break;
            case "ScheduleStage":
                if (wo.ScheduleStage.HasValue)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", DisplayHelper.GetScheduleStageColor(wo.ScheduleStage.Value));
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, IntStatusDisplayHelper.GetScheduleStageText(wo.ScheduleStage))));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "UrgencyLevel":
                if (!string.IsNullOrEmpty(wo.UrgencyLevel))
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", DisplayHelper.GetUrgencyColor(wo.UrgencyLevel));
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, wo.UrgencyLevel))));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "RawMaterialLockRemark":
                builder.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, wo.RawMaterialLockRemark) ?? "-");
                break;
            case "LatestPlanDate":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", Color.Info);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, wo.LatestPlanDate?.ToString("yyyy-MM-dd") ?? "-")));
                builder.CloseComponent();
                break;
            case "MaterialPlanStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetStatusColor(wo.MaterialPlanStatus));
                builder.AddAttribute(3, "Class", GetStatusChipClass(wo.MaterialPlanStatus));
                builder.AddAttribute(4, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetStatusText(wo.MaterialPlanStatus))));
                builder.CloseComponent();
                break;
            case "MaterialPlanRate":
                builder.AddContent(0, $"{wo.MaterialPlanRate:F1}%");
                break;
            case "PlanProportion":
                if (!string.IsNullOrEmpty(wo.PlanProportionText))
                {
                    builder.OpenComponent<MudText>(0);
                    builder.AddAttribute(1, "Typo", Typo.caption);
                    builder.AddAttribute(2, "Class", "text-wrap");
                    builder.AddAttribute(3, "Style", "max-width:180px;");
                    builder.AddAttribute(4, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, wo.PlanProportionText)));
                    builder.CloseComponent();
                }
                else
                {
                    builder.OpenComponent<MudText>(0);
                    builder.AddAttribute(1, "Typo", Typo.caption);
                    builder.AddAttribute(2, "Color", Color.Secondary);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "-")));
                    builder.CloseComponent();
                }
                break;
            case "MainNoMaterialPlanStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetStatusColor(wo.MainNoMaterialPlanStatus));
                builder.AddAttribute(3, "Class", GetStatusChipClass(wo.MainNoMaterialPlanStatus));
                builder.AddAttribute(4, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetStatusText(wo.MainNoMaterialPlanStatus))));
                builder.CloseComponent();
                break;
            case "OrderMaterialPlanStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetStatusColor(wo.OrderMaterialPlanStatus));
                builder.AddAttribute(3, "Class", GetStatusChipClass(wo.OrderMaterialPlanStatus));
                builder.AddAttribute(4, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetStatusText(wo.OrderMaterialPlanStatus))));
                builder.CloseComponent();
                break;
            case "MaxStandardCycle":
                builder.AddContent(0, wo.MaxStandardCycle > 0 ? $"{wo.MaxStandardCycle}天" : "-");
                break;
            case "MainNoMaxStandardCycle":
                builder.AddContent(0, wo.MainNoMaxStandardCycle > 0 ? $"{wo.MainNoMaxStandardCycle}天" : "-");
                break;
            case "CapacityWorkDays":
                builder.AddContent(0, wo.CapacityWorkDays.HasValue ? $"{wo.CapacityWorkDays}天" : "-");
                break;
            case "TheoreticalCutoffDate":
                if (wo.TheoreticalCutoffDate.HasValue)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Warning);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, wo.TheoreticalCutoffDate.Value.ToString("yyyy-MM-dd"))));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "MaterialPlanCoveredCount":
                builder.AddContent(0, wo.MaterialPlanCoveredCount > 0 ? $"{wo.MaterialPlanCoveredCount}/7" : "-");
                break;
            case "LatestRequiredDate":
                if (wo.LatestRequiredDate.HasValue)
                    builder.AddContent(0, wo.LatestRequiredDate.Value.ToString("yyyy-MM-dd"));
                else
                    builder.AddContent(0, "-");
                break;
        }
    };

    // ========== 批量打印 ==========

    private async Task PrintSelectedPlans()
    {
        if (!selectedWorkOrderIds.Any() || !anyPlanTypeSelected) return;

        var request = new MaterialPlanBatchPrintRequest
        {
            WorkOrderIds = selectedWorkOrderIds.ToArray(),
            IncludeSemi = includeSemi,
            IncludeFinish = includeFinish,
            IncludeInventory = includeInventory,
            IncludeRework = includeRework,
            IncludeRoundBarPiercing = includePiercing,
            IncludeInProcessRework = includeInProcessRework,
            IncludeInMainWorkOrder = includeInMainWorkOrder
        };

        try
        {
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.MaterialPlan}/print/batch";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        if (!string.IsNullOrWhiteSpace(_dateFrom)) extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrWhiteSpace(_dateTo)) extras["dateTo"] = _dateTo;
        if (!string.IsNullOrWhiteSpace(_deliveryDateFrom)) extras["deliveryDateFrom"] = _deliveryDateFrom;
        if (!string.IsNullOrWhiteSpace(_deliveryDateTo)) extras["deliveryDateTo"] = _deliveryDateTo;
        extras["includePiercing"] = includePiercing.ToString();
        extras["includeSemi"] = includeSemi.ToString();
        extras["includeFinish"] = includeFinish.ToString();
        extras["includeInventory"] = includeInventory.ToString();
        extras["includeRework"] = includeRework.ToString();
        extras["includeInProcessRework"] = includeInProcessRework.ToString();
        extras["includeInMainWorkOrder"] = includeInMainWorkOrder.ToString();
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("materialplan", state);
    }
}
