using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Shared.Constants;
using MES.Core.DTOs.WorkOrder;
using MES.Core.DTOs.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.WorkOrders;

public partial class WorkOrders : IAsyncDisposable
{
    private MudTable<WorkOrderListItemDto>? table;
    private List<WorkOrderListItemDto> _pageItems = new();
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new() { "TotalQuantity", "TotalWeight", "TotalItemCount" };
    private int _totalCount;
    private List<WorkOrderListItemDto>? _pendingOrders;
    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;
    private string _deliveryDateFrom = string.Empty;
    private string _deliveryDateTo = string.Empty;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _pageSize = 10;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _loadVersion;
    private bool _resetToFirstPage;
    private bool _isAdmin;

    // ========== 通知定时轮询 ==========
    private CancellationTokenSource? _pollingCts;

    // 排序状态
    private string sortColumn = "SignDate";
    private bool sortDescending = true;

    // 选中
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
                    selectedSalesOrderNos.Add(item.SalesOrderNo);
            }
            else
            {
                selectedSalesOrderNos.Clear();
            }
            StateHasChanged();
        }
    }
    private HashSet<string> selectedSalesOrderNos = new();

    /// <summary>按当前页实际选中状态重算全选复选框（单选/翻页/筛选后保持视觉一致）</summary>
    private void UpdateAllSelectedState()
    {
        _allSelected = _pageItems.Any() && _pageItems.All(i => selectedSalesOrderNos.Contains(i.SalesOrderNo));
    }

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "WorkOrderNo",       Label = "工单号",   SortKey = "WorkOrderNo",       FilterType = "string", Width = "120" },
        new() { Key = "SalesOrderNo",      Label = "订单号",   SortKey = "SalesOrderNo",      FilterType = "string", Width = "120" },
        new() { Key = "ProductionMainNo",  Label = "主号",     SortKey = "ProductionMainNo",  FilterType = "string", Width = "120" },
        new() { Key = "ProductionSubNo",   Label = "次号",     SortKey = "ProductionSubNo",   FilterType = "string", Width = "120" },
        new() { Key = "SignDate",          Label = "签订日期", SortKey = "SignDate", FilterType = "date", Width = "120" },
        new() { Key = "Salesman",          Label = "业务员",   SortKey = "Salesman",          FilterType = "string", Width = "120" },
        new() { Key = "EndCustomer",       Label = "最终客户", SortKey = "EndCustomer",       FilterType = "string", Width = "120" },
        new() { Key = "DeliveryDate",      Label = "交货日期", SortKey = "DeliveryDate", FilterType = "date", Width = "120" },
        new() { Key = "DelayPenalty",      Label = "延期罚款", SortKey = "DelayPenalty",      FilterType = "boolean", Width = "60", BoolTrueLabel = "是", BoolFalseLabel = "否" },
        new() { Key = "SettlementMethod",  Label = "结算方式", SortKey = "SettlementMethod",  FilterType = "enum", Width = "120",
               EnumOptions = DisplayHelper.GetEnumFilterOptions<SettlementMethod>() },
        new() { Key = "PlantGrade",        Label = "工厂牌号", SortKey = "PlantGrade",        FilterType = "string", Width = "120" },
        new() { Key = "MaterialName",      Label = "钢管制造", SortKey = "MaterialName",      FilterType = "enum", Width = "120",
               EnumOptions = DisplayHelper.GetEnumFilterOptions<PipeManufacturingType>() },
        new() { Key = "Specification",     Label = "规格",     SortKey = "Specification",     FilterType = "string", Width = "120" },
        new() { Key = "LengthStatus",      Label = "长度状态", SortKey = "LengthStatus",      FilterType = "enum", Width = "120",
               EnumOptions = DisplayHelper.GetEnumFilterOptions<LengthStatus>() },
        new() { Key = "MinLength",         Label = "最小长度", SortKey = "MinLength", Width = "80" },
        new() { Key = "MaxLength",         Label = "最大长度", SortKey = "MaxLength", Width = "80" },
        new() { Key = "TotalQuantity",     Label = "总支数",   SortKey = "TotalQuantity", Width = "80" },
        new() { Key = "TotalWeight",       Label = "总重量",   SortKey = "TotalWeight", Width = "80" },
        new() { Key = "DeliveryState",     Label = "交货状态", SortKey = "DeliveryState",    FilterType = "enum", Width = "120",
               EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>() },
        new() { Key = "TotalItemCount",    Label = "含项次数", SortKey = "TotalItemCount", Width = "80" },
        new() { Key = "Status",            Label = "状态",     SortKey = "Status",            FilterType = "enum", Width = "120",
               EnumOptions = DisplayHelper.GetEnumFilterOptions<WorkOrderStatus>() },
        new() { Key = "CreatedBy",         Label = "创建人",   SortKey = "CreatedBy",         FilterType = "string", Width = "100", Visible = false },
        new() { Key = "CreatedTime",       Label = "创建时间", SortKey = "CreatedTime",       FilterType = "date", Width = "130", Visible = false },
        new() { Key = "UpdatedBy",         Label = "更新人",   SortKey = "UpdatedBy",         FilterType = "string", Width = "100", Visible = false },
        new() { Key = "UpdatedTime",       Label = "更新时间", SortKey = "UpdatedTime",       FilterType = "date", Width = "130", Visible = false },
    };

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;
        var props = typeof(WorkOrderListItemDto)
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

    private async Task<TableData<WorkOrderListItemDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
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

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "SignDate";
            var filtersJson = SerializeFilters();

            var result = await WorkOrderService.GetPagedAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
                filters: filtersJson,
                dateFrom: DateTime.TryParse(_dateFrom, out var dFrom) ? dFrom : null,
                dateTo: DateTime.TryParse(_dateTo, out var dTo) ? dTo : null,
                deliveryDateFrom: DateTime.TryParse(_deliveryDateFrom, out var ddf) ? ddf : null,
                deliveryDateTo: DateTime.TryParse(_deliveryDateTo, out var ddt) ? ddt : null);

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<WorkOrderListItemDto> { Items = _pageItems, TotalItems = _totalCount };

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
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
        }

        ComputePageSums();
        UpdateAllSelectedState();
        await SavePageStateAsync();

        return new TableData<WorkOrderListItemDto>
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
            var result = await WorkOrderService.GetFilterContextsAsync();
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
                Display = v,
                Count = 0
            }).ToList();
        }

        // Status 列显示中文
        if (_filterContextOptions.TryGetValue("Status", out var statusOptions))
        {
            foreach (var opt in statusOptions)
            {
                opt.Display = opt.Value switch
                {
                    "1" => "已确定",
                    "2" => "待修正",
                    _ => opt.Value
                };
            }
        }

        // DelayPenalty 列显示中文
        if (_filterContextOptions.TryGetValue("DelayPenalty", out var delayOptions))
        {
            foreach (var opt in delayOptions)
            {
                opt.Display = opt.Value == "True" ? "是" : "否";
            }
        }

        // SettlementMethod 列显示中文
        if (_filterContextOptions.TryGetValue("SettlementMethod", out var settlementOptions))
        {
            foreach (var opt in settlementOptions)
            {
                opt.Display = opt.Value == "Theoretical" ? "理算" : "过磅";
            }
        }

        // MaterialName 列显示中文
        if (_filterContextOptions.TryGetValue("MaterialName", out var materialOptions))
        {
            foreach (var opt in materialOptions)
            {
                opt.Display = opt.Value == "SeamlessPipe" ? "无缝管" : "焊管";
            }
        }

        // LengthStatus 列显示中文
        if (_filterContextOptions.TryGetValue("LengthStatus", out var lengthOptions))
        {
            foreach (var opt in lengthOptions)
            {
                opt.Display = opt.Value switch
                {
                    "Fixed" => "定尺",
                    "Range" => "范围尺",
                    "Multiple" => "倍尺",
                    "Unlimited" => "不限",
                    _ => opt.Value
                };
            }
        }

        // DeliveryState 列显示中文
        if (_filterContextOptions.TryGetValue("DeliveryState", out var deliveryOptions))
        {
            foreach (var opt in deliveryOptions)
            {
                opt.Display = DisplayHelper.GetDeliveryStateText(opt.Value);
            }
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

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("workorders", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        if (table != null) await table.ReloadServerData();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(WorkOrderListItemDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "WorkOrderNo":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => Navigation.NavigateTo($"/workorders/{item.Id}")));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.WorkOrderNo)));
                builder.CloseComponent();
                break;
            case "SalesOrderNo":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => NavigateToTrace(item.SalesOrderNo)));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.SalesOrderNo)));
                builder.CloseComponent();
                break;
            case "ProductionMainNo":
                builder.AddContent(0, item.ProductionMainNo);
                break;
            case "ProductionSubNo":
                builder.AddContent(0, item.ProductionSubNo ?? "-");
                break;
            case "SignDate":
                builder.AddContent(0, item.SignDate.ToString("yyyy-MM-dd"));
                break;
            case "Salesman":
                builder.AddContent(0, item.Salesman);
                break;
            case "EndCustomer":
                builder.AddContent(0, item.EndCustomer);
                break;
            case "DeliveryDate":
                builder.AddContent(0, item.DeliveryDate.ToString("yyyy-MM-dd"));
                break;
            case "DelayPenalty":
                builder.AddContent(0, DisplayHelper.GetYesNoText(item.DelayPenalty));
                break;
            case "SettlementMethod":
                builder.AddContent(0, DisplayHelper.GetSettlementMethodText(item.SettlementMethod));
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade);
                break;
            case "MaterialName":
                builder.AddContent(0, DisplayHelper.GetPipeManufacturingTypeText(item.PipeManufacturingType));
                break;
            case "Specification":
                builder.AddContent(0, item.Specification);
                break;
            case "LengthStatus":
                builder.AddContent(0, DisplayHelper.GetWorkOrderLengthStatusText(item.LengthStatus, item.MinLength, item.MaxLength));
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
            case "DeliveryState":
                builder.AddContent(0, DisplayHelper.GetDeliveryStateText(item.DeliveryState));
                break;
            case "TotalItemCount":
                builder.AddContent(0, item.TotalItemCount);
                break;
            case "Status":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetWorkOrderStatusColor(item.Status));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetWorkOrderStatusText(item.Status))));
                builder.CloseComponent();
                break;
            case "CreatedBy":
                builder.AddContent(0, string.IsNullOrEmpty(item.CreatedBy) ? "-" : item.CreatedBy);
                break;
            case "CreatedTime":
                builder.AddContent(0, item.CreatedTime == default ? "-" : item.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            case "UpdatedBy":
                builder.AddContent(0, string.IsNullOrEmpty(item.UpdatedBy) ? "-" : item.UpdatedBy);
                break;
            case "UpdatedTime":
                builder.AddContent(0, item.UpdatedTime == default ? "-" : item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
        }
    };

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        // 初始化列定义
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("workorders", null);
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

        // 恢复排序/筛选状态
        var savedState = await PageState.LoadAsync("workorders");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "SignDate";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _dateFrom = savedState.Extras?.ContainsKey("dateFrom") == true ? savedState.Extras["dateFrom"] ?? string.Empty : string.Empty;
            _dateTo = savedState.Extras?.ContainsKey("dateTo") == true ? savedState.Extras["dateTo"] ?? string.Empty : string.Empty;
            _deliveryDateFrom = savedState.Extras?.ContainsKey("deliveryDateFrom") == true ? savedState.Extras["deliveryDateFrom"] ?? string.Empty : string.Empty;
            _deliveryDateTo = savedState.Extras?.ContainsKey("deliveryDateTo") == true ? savedState.Extras["deliveryDateTo"] ?? string.Empty : string.Empty;
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
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
        }

        // 检查管理员权限
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        _isAdmin = user.IsInRole(Roles.Admin);

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && table != null)
            await table.ReloadServerData();

        // 加载筛选上下文
        await LoadFilterContextsAsync();

        await CheckNotifications();
        await LoadPendingOrders();

        // 启动通知定时轮询（后台运行，不阻塞初始化）
        _ = StartNotificationPollingAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#workorders-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 通知提醒条 ==========

    private List<NotificationDto>? _orderChangeNotices;
    private List<NotificationDto>? _orderDeletedNotices;

    private async Task CheckNotifications()
    {
        try
        {
            var changeTask = NotificationService.GetByTypeAsync("OrderChanged");
            var deletedTask = NotificationService.GetByTypeAsync("OrderDeleted");
            await Task.WhenAll(changeTask, deletedTask);

            var changeResult = await changeTask;
            if (changeResult.Success && changeResult.Data != null)
                _orderChangeNotices = changeResult.Data;
            else
                _orderChangeNotices = null;

            var deletedResult = await deletedTask;
            if (deletedResult.Success && deletedResult.Data != null)
                _orderDeletedNotices = deletedResult.Data;
            else
                _orderDeletedNotices = null;
        }
        catch
        {
            _orderChangeNotices = null;
            _orderDeletedNotices = null;
        }
    }

    private async Task DismissOrderChangeNotices()
    {
        var result = await NotificationService.MarkAllByTypeAsReadAsync("OrderChanged");
        if (result.Success)
        {
            _orderChangeNotices = null;
            Snackbar.Add("提醒已忽略", Severity.Success);
        }
    }

    private async Task DismissOrderDeletedNotices()
    {
        var result = await NotificationService.MarkAllByTypeAsReadAsync("OrderDeleted");
        if (result.Success)
        {
            _orderDeletedNotices = null;
            Snackbar.Add("提醒已忽略", Severity.Success);
        }
    }

    // ========== 待生成工单 ==========

    private async Task LoadPendingOrders()
    {
        try
        {
            var result = await WorkOrderService.GetPendingOrdersAsync();
            if (result.Success && result.Data != null)
            {
                _pendingOrders = result.Data;
            }
            else
            {
                _pendingOrders = null;
            }
        }
        catch
        {
            _pendingOrders = null;
        }
    }

    // ========== 导航 ==========

    private void NavigateToGenerate(string salesOrderNo, string status)
    {
        Navigation.NavigateTo($"/workorders/generate?salesOrderNo={Uri.EscapeDataString(salesOrderNo)}&status={status}");
    }

    private void NavigateToRegenerate(string salesOrderNo, string status)
    {
        Navigation.NavigateTo($"/workorders/generate?salesOrderNo={Uri.EscapeDataString(salesOrderNo)}&status={status}&regenerate=true");
    }

    private void NavigateToUpdate(string salesOrderNo, string status)
    {
        Navigation.NavigateTo($"/workorders/generate?salesOrderNo={Uri.EscapeDataString(salesOrderNo)}&status={status}&update=true");
    }

    private void NavigateToTrace(string salesOrderNo)
    {
        Navigation.NavigateTo($"/workorders/relation?salesOrderNo={Uri.EscapeDataString(salesOrderNo)}");
    }

    // ========== 物理删除 ==========

    private async Task DeleteWorkOrder(int workOrderId, string workOrderNo)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认删除", new DialogParameters
        {
            ["ContentText"] = $"确定要永久删除工单 \"{workOrderNo}\" 吗？\n\n此操作不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        try
        {
            var result = await WorkOrderService.DeleteAsync(workOrderId);
            if (result.Success)
            {
                Snackbar.Add($"工单 \"{workOrderNo}\" 已删除", Severity.Success);
                if (table != null) await table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "删除失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"删除失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 打印 ==========

    private async Task PrintSelected()
    {
        if (!selectedSalesOrderNos.Any()) return;
        try
        {
            var salesOrderNos = selectedSalesOrderNos.ToArray();
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.WorkOrder}/order-print-batch-file";
            var json = JsonSerializer.Serialize(salesOrderNos);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>打印选中列表（按当前可见列渲染列表 PDF，Mode A 前端已准备数据）</summary>
    private async Task PrintSelectedList()
    {
        if (!selectedSalesOrderNos.Any())
        {
            Snackbar.Add("请先选择要打印的工单", Severity.Warning);
            return;
        }

        // 列过多时各列被压缩到单字符放不下的宽度 → QuestPDF 布局冲突；A4 可显示列数上限 35 列（与后端 TablePrintHelper.MaxPrintColumns 同步），超限提前拦截并页面内警示
        const int MaxPrintColumns = 35;
        if (_visibleColumns.Count > MaxPrintColumns)
        {
            Snackbar.Add($"当前可见列过多（{_visibleColumns.Count} 列，打印上限 {MaxPrintColumns} 列），请通过列显隐精简后再打印", Severity.Warning);
            return;
        }

        try
        {
            var selectedItems = _pageItems
                .Where(i => selectedSalesOrderNos.Contains(i.SalesOrderNo))
                .Select(item =>
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var col in _visibleColumns)
                        dict[col.Key] = GetPrintValue(item, col);
                    return dict;
                }).ToList();

            var request = new WorkOrderPrintListRequest
            {
                Title = "工单列表",
                Items = selectedItems,
                Columns = GetPrintColumnDefs()
            };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.WorkOrder}/order-print-list-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>当前可见列 → 打印列定义（Key/Label 对应当前列显隐与顺序）</summary>
    private List<PrintColumnDef> GetPrintColumnDefs() =>
        _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();

    /// <summary>按列取表格显示文本（对齐 RenderCell 口径：日期格式/枚举中文/布尔/数值）</summary>
    private object GetPrintValue(WorkOrderListItemDto item, ColumnDef col) => col.Key switch
    {
        "WorkOrderNo" => item.WorkOrderNo,
        "SalesOrderNo" => item.SalesOrderNo,
        "ProductionMainNo" => item.ProductionMainNo,
        "ProductionSubNo" => item.ProductionSubNo ?? "-",
        "SignDate" => item.SignDate.ToString("yyyy-MM-dd"),
        "Salesman" => item.Salesman,
        "EndCustomer" => item.EndCustomer ?? "-",
        "DeliveryDate" => item.DeliveryDate.ToString("yyyy-MM-dd"),
        "DelayPenalty" => DisplayHelper.GetYesNoText(item.DelayPenalty),
        "SettlementMethod" => DisplayHelper.GetSettlementMethodText(item.SettlementMethod),
        "PlantGrade" => item.PlantGrade,
        "MaterialName" => DisplayHelper.GetPipeManufacturingTypeText(item.PipeManufacturingType),
        "Specification" => item.Specification,
        "LengthStatus" => DisplayHelper.GetWorkOrderLengthStatusText(item.LengthStatus, item.MinLength, item.MaxLength),
        "MinLength" => item.MinLength?.ToString("G29") ?? "-",
        "MaxLength" => item.MaxLength?.ToString("G29") ?? "-",
        "TotalQuantity" => item.TotalQuantity.ToString(),
        "TotalWeight" => ((int)item.TotalWeight).ToString(),
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState),
        "TotalItemCount" => item.TotalItemCount.ToString(),
        "Status" => DisplayHelper.GetWorkOrderStatusText(item.Status),
        "CreatedBy" => string.IsNullOrEmpty(item.CreatedBy) ? "-" : item.CreatedBy,
        "CreatedTime" => item.CreatedTime == default ? "-" : item.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        "UpdatedBy" => string.IsNullOrEmpty(item.UpdatedBy) ? "-" : item.UpdatedBy,
        "UpdatedTime" => item.UpdatedTime == default ? "-" : item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        _ => "-"
    };

    // ========== 通知定时轮询（每 2 分钟） ==========

    private async Task StartNotificationPollingAsync()
    {
        _pollingCts = new CancellationTokenSource();
        try
        {
            while (!_pollingCts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(2), _pollingCts.Token);
                try
                {
                    await InvokeAsync(async () =>
                    {
                        await CheckNotifications();
                        await LoadPendingOrders();
                        StateHasChanged();
                    });
                }
                catch
                {
                    // 单次轮询异常不影响下一轮
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 组件销毁时正常取消
        }
    }

    public ValueTask DisposeAsync()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        return ValueTask.CompletedTask;
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
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("workorders", state);
    }
}
