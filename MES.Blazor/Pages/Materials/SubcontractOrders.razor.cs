using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Shared.Constants;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Rendering;

namespace MES.Blazor.Pages.Materials;

public partial class SubcontractOrders : IAsyncDisposable
{
    private MudTable<SubcontractOrderDto>? table;
    private List<SubcontractOrderDto> _pageItems = new();
    private int _totalCount;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private HashSet<int> selectedIds = new();
    private bool _isArrowNavSetup;
    private bool _allSelectedField;
    private bool _isAdmin;

    private bool allSelected
    {
        get => _allSelectedField;
        set
        {
            if (_allSelectedField == value) return;
            _allSelectedField = value;
            if (value)
            {
                foreach (var item in _pageItems)
                    selectedIds.Add(item.Id);
            }
            else
            {
                selectedIds.Clear();
            }
            StateHasChanged();
        }
    }
    private int _currentPage = 1;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    // 日期范围搜索
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "OutQuantity", "OutWeight", "ActualOutboundWeight", "Returned",
    };

    private string sortColumn = "OrderDate";
    private bool sortDescending = true;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    private List<ProcurementStatusDto> procurementItems = new();
    private bool showProcurementStatus = false;
    private List<OrderMismatchInfo> mismatchItems = new();

    // ========== 状态面板定时轮询 ==========
    private CancellationTokenSource? _pollingCts;

    private static List<EnumOption> GetMaterialCategoryOptions() => DisplayHelper.GetEnumFilterOptions<MaterialType>();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "OrderNo",             Label = "委外单号",     SortKey = "OrderNo",             FilterType = "string",   Width = "160" },
        new() { Key = "OrderDate",           Label = "下单日期",     SortKey = "OrderDate",           FilterType = "date",     Width = "120" },
        new() { Key = "ProcessType",         Label = "加工类型",     SortKey = "ProcessType",         Width = "100" },
        new() { Key = "OutMaterialCategory", Label = "物料分类",     SortKey = "OutMaterialCategory", FilterType = "enum",   Width = "100", EnumOptions = GetMaterialCategoryOptions() },
        new() { Key = "OutPlantGrade",       Label = "工厂牌号",     SortKey = "OutPlantGrade",       FilterType = "string",   Width = "100" },
        new() { Key = "OutSpecification",    Label = "规格",         SortKey = "OutSpecification",    FilterType = "string",   Width = "120" },
        new() { Key = "OutQuantity",         Label = "发出支数",     SortKey = "OutQuantity",                                  Width = "90"  },
        new() { Key = "OutWeight",           Label = "发出重量",     SortKey = "OutWeight",                                    Width = "90"  },
        new() { Key = "ReturnDeadline",      Label = "收回期限",     SortKey = "ReturnDeadline",      FilterType = "date",     Width = "110" },
        new() { Key = "SupplierName",        Label = "供应商",       SortKey = "SupplierName",        FilterType = "string",   Width = "150" },
        new() { Key = "Status",              Label = "状态",         SortKey = "Status",              FilterType = "enum",     Width = "100",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<SubcontractOrderStatus>() },
        new() { Key = "ActualOutboundWeight",Label = "实发量",                                                                 Width = "90"  },
        new() { Key = "Returned",            Label = "已回收",                                                                 Width = "130" },
    };

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("subcontract_orders", null, _allColumns);
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

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(SubcontractOrderDto)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);

        foreach (var col in _visibleColumns.Where(c => _summableColumnKeys.Contains(c.Key)))
        {
            // 特殊处理: "Returned" 列对应 InWeight
            if (col.Key == "Returned")
            {
                var sum = _pageItems.Sum(item => item.InWeight ?? 0m);
                _pageSums[col.Key] = ((int)sum).ToString();
                continue;
            }

            // 特殊处理: "ActualOutboundWeight" 列汇总支数+重量
            if (col.Key == "ActualOutboundWeight")
            {
                var qtySum = _pageItems.Sum(item => item.ActualOutboundQuantity ?? 0);
                var wgtSum = _pageItems.Sum(item => item.ActualOutboundWeight ?? 0m);
                _pageSums[col.Key] = $"{qtySum}支/{((int)wgtSum).ToString()}kg";
                continue;
            }

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

    private async Task<TableData<SubcontractOrderDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "OrderDate";
            var filtersJson = SerializeFilters();

            // 恢复持久化的页码（MudTable 初始化时始终传 page=0）
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

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

            DateTime? dateFrom = DateTime.TryParse(_dateFrom, out var dFrom) ? dFrom : null;
            DateTime? dateTo = DateTime.TryParse(_dateTo, out var dTo) ? dTo : null;

            var result = await SubcontractService.GetPagedAsync(query, status: null, dateFrom: dateFrom, dateTo: dateTo);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;
                ComputePageSums();
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

        return new TableData<SubcontractOrderDto>
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
            var result = await SubcontractService.GetFilterContextsAsync();
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
                opt.Display = GetStatusText(Enum.TryParse<SubcontractOrderStatus>(opt.Value, out var s) ? s : SubcontractOrderStatus.Sent);
            }
        }

        // OutMaterialCategory 列显示中文
        if (_filterContextOptions.TryGetValue("OutMaterialCategory", out var categoryOptions))
        {
            foreach (var opt in categoryOptions)
            {
                opt.Display = DisplayHelper.GetMaterialTypeText(opt.Value);
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

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(SubcontractOrderDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "OrderNo":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => ViewDetail(item.Id)));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 =>
                {
                    b2.OpenElement(4, "strong");
                    b2.AddContent(5, item.OrderNo);
                    b2.CloseElement();
                }));
                builder.CloseComponent();
                break;
            case "OrderDate":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", Color.Info);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.OrderDate.ToString("yyyy-MM-dd"))));
                builder.CloseComponent();
                break;
            case "ProcessType":
                builder.AddContent(0, "穿孔");
                break;
            case "OutMaterialCategory":
                builder.AddContent(0, DisplayHelper.GetMaterialTypeText(item.OutMaterialCategory));
                break;
            case "OutPlantGrade":
                builder.AddContent(0, item.OutPlantGrade);
                break;
            case "OutSpecification":
                builder.AddContent(0, item.OutSpecification);
                break;
            case "OutQuantity":
                builder.AddContent(0, item.OutQuantity.ToString());
                break;
            case "OutWeight":
                builder.AddContent(0, ((int)item.OutWeight).ToString());
                break;
            case "ReturnDeadline":
                builder.AddContent(0, item.ReturnDeadline?.ToString("yyyy-MM-dd"));
                break;
            case "SupplierName":
                builder.AddContent(0, item.SupplierName);
                break;
            case "Status":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetStatusColor(item.Status));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetStatusText(item.Status))));
                builder.CloseComponent();
                break;
            case "ActualOutboundWeight":
                builder.AddContent(0, item.ActualOutboundQuantity.HasValue
                    ? $"{item.ActualOutboundQuantity.Value}支/{((int)(item.ActualOutboundWeight ?? 0)).ToString()}kg"
                    : "-");
                break;
            case "Returned":
                builder.AddContent(0, $"{item.InQuantity?.ToString() ?? "0"}支/{((int)(item.InWeight ?? 0)).ToString()}kg");
                break;
        }
    };

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        _isAdmin = user.Claims.Any(c => c.Type == "role" && c.Value == "Admin")
                || user.IsInRole(Roles.Admin);

        await LoadProcurementStatus();
        await LoadOrderMismatches();

        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("subcontract_orders", null);
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
        var savedState = await PageState.LoadAsync("subcontractorders");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "OrderDate";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
            if (savedState.Extras?.ContainsKey("dateFrom") == true)
                _dateFrom = savedState.Extras["dateFrom"];
            if (savedState.Extras?.ContainsKey("dateTo") == true)
                _dateTo = savedState.Extras["dateTo"];
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

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && table != null)
            await table.ReloadServerData();

        // 加载筛选上下文
        await LoadFilterContextsAsync();

        // 启动状态面板定时轮询（后台运行，不阻塞初始化）
        _ = StartPollingAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#subcontract-orders-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 采购状态 ==========

    private async Task LoadProcurementStatus()
    {
        try
        {
            var result = await SubcontractService.GetProcurementStatusAsync();
            if (result.Success && result.Data != null)
                procurementItems = result.Data;
            else if (!result.Success)
                Snackbar.Add($"加载圆棒穿孔计划执行状态失败: {result.Message}", Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载圆棒穿孔计划执行状态异常: {ex.Message}", Severity.Error);
        }
    }

    private void ToggleProcurementStatus() => showProcurementStatus = !showProcurementStatus;

    private async Task LoadOrderMismatches()
    {
        try
        {
            var result = await SubcontractService.GetMismatchedOrdersAsync();
            if (result.Success && result.Data != null)
            {
                mismatchItems = result.Data;
            }
            else
                mismatchItems.Clear();
        }
        catch
        {
            mismatchItems.Clear();
        }
    }

    // ========== 打印 ==========

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的委外单", Severity.Warning);
            return;
        }
        try
        {
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var ids = selectedIds.ToArray();
            var request = new OrderPrintBatchRequest { Ids = ids };
            var apiUrl = $"{Navigation.BaseUri}api/subcontract/print-batch-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    private async Task PrintAll()
    {
        try
        {
            Snackbar.Add("正在生成PDF...", Severity.Info);
            DateTime? dateFrom = DateTime.TryParse(_dateFrom, out var dFrom) ? dFrom : null;
            DateTime? dateTo = DateTime.TryParse(_dateTo, out var dTo) ? dTo : null;
            var request = new OrderPrintAllRequest
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortColumn,
                IsDescending = sortDescending,
                DateFrom = dateFrom,
                DateTo = dateTo
            };
            var apiUrl = $"{Navigation.BaseUri}api/subcontract/print-all-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    // ========== 取消订单 ==========

    private async Task CancelOrder(SubcontractOrderDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters { ["ContentText"] = $"确定要取消委外单 \"{item.OrderNo}\" 吗？", ["ConfirmText"] = "确认取消", ["Color"] = Color.Error });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await SubcontractService.DeleteAsync(item.Id);
                if (result.Success) { Snackbar.Add("取消成功", Severity.Success); if (table != null) await table.ReloadServerData(); await LoadProcurementStatus(); await LoadFilterContextsAsync(); }
                else { Snackbar.Add(result.Message ?? "取消失败", Severity.Error); }
            }
            catch (Exception ex) { Snackbar.Add($"取消失败: {ex.Message}", Severity.Error); }
        }
    }

    // ========== 辅助方法 ==========

    private static Color GetStatusColor(SubcontractOrderStatus status) => status switch { SubcontractOrderStatus.Sent => Color.Info, SubcontractOrderStatus.PartialReturned => Color.Warning, SubcontractOrderStatus.Completed => Color.Success, _ => Color.Default };
    private static string GetStatusText(SubcontractOrderStatus status) => DisplayHelper.GetSubcontractOrderStatusText(status);

    private void NavigateToCreate() => Navigation.NavigateTo("/subcontract-orders/create");
    private void ViewDetail(int id) => Navigation.NavigateTo($"/subcontract-orders/{id}");
    private void NavigateToEdit(int id) => Navigation.NavigateTo($"/subcontract-orders/{id}");

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        if (!string.IsNullOrEmpty(_dateFrom)) extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrEmpty(_dateTo)) extras["dateTo"] = _dateTo;
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("subcontractorders", state);
    }

    // ========== 状态面板定时轮询（每 2 分钟） ==========

    private async Task StartPollingAsync()
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
                        await LoadProcurementStatus();
                        if (table != null) await table.ReloadServerData();
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
}
