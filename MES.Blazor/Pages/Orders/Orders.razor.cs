using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Helpers;
using MES.Blazor.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.Orders;

public partial class Orders
{
    private MudTable<SalesOrderListDto>? table;
    private List<SalesOrderListDto> _pageItems = new();
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new() { "TotalContractWeight", "ItemCount" };
    private int _totalCount;
    private HashSet<int> selectedOrderIds = new();
    private bool _isArrowNavSetup;
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
                    selectedOrderIds.Add(item.Id);
            }
            else
            {
                selectedOrderIds.Clear();
            }
            StateHasChanged();
        }
    }
    private int _currentPage = 1;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;

    private string sortColumn = "signdate";
    private bool sortDescending = true;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "ordernumber",   Label = "订单号",   SortKey = "ordernumber",   FilterType = "string", Width = "120" },
        new() { Key = "signdate",      Label = "签订日期", SortKey = "signdate",     FilterType = "date", Width = "120" },
        new() { Key = "salesman",      Label = "业务员",   SortKey = "salesman",     FilterType = "string", Width = "120" },
        new() { Key = "customername",  Label = "客户名称", SortKey = "customername", FilterType = "string", Width = "120" },
        new() { Key = "endcustomer",   Label = "最终客户", SortKey = "endcustomer",  FilterType = "string", Width = "120" },
        new() { Key = "deliverystart", Label = "交期起始", SortKey = "deliverystart", FilterType = "date", Width = "120" },
        new() { Key = "deliveryend",   Label = "交期截止", SortKey = "deliveryend",  FilterType = "date", Width = "120" },
        new() { Key = "hasdelaypenalty", Label = "延期罚款", SortKey = "hasdelaypenalty", FilterType = "boolean", Width = "60", BoolTrueLabel = "是", BoolFalseLabel = "否" },
        new() { Key = "totalcontractweight", Label = "订单总重量", SortKey = "totalcontractweight", Width = "80" },
        new() { Key = "itemcount", Label = "含项次数", SortKey = "itemcount", Width = "80" },
        new() { Key = "notech",        Label = "技术要求", SortKey = "hastechnicalrequirement", FilterType = "boolean", Width = "120", BoolTrueLabel = "已编辑", BoolFalseLabel = "未编辑" },
        new() { Key = "status",        Label = "状态",     SortKey = "status", FilterType = "enum", Width = "120",
               EnumOptions = new List<EnumOption> { new("Pending", "待处理"), new("Confirmed", "已确认") },
               DisplayConverter = v => v is SalesOrderStatus s ? DisplayHelper.GetSalesOrderStatusText(s) : "-" },
        new() { Key = "lastchangedate",Label = "变更日期", SortKey = "lastchangedate", FilterType = "date", Width = "120" },
    };

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;
        var props = typeof(SalesOrderListDto)
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
        return "";
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<SalesOrderListDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            // 首次加载覆盖页码（MudTable 初始化时始终传 page=0）
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "signdate";
            var filtersJson = SerializeFilters();

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending
            };

            if (!string.IsNullOrEmpty(filtersJson))
            {
                try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson); }
                catch { }
            }

            var result = await OrderService.GetPagedAsync(query);

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
        await SavePageStateAsync();

        return new TableData<SalesOrderListDto>
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
            var result = await OrderService.GetFilterContextsAsync();
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
            var key = kvp.Key.ToLower(); // backend returns PascalCase, columns use lowercase
            _filterContextOptions[key] = kvp.Value.Select(v => new ExcelFilterOption
            {
                Value = v,
                Display = v,
                Count = 0
            }).ToList();
        }

        // Status 列显示中文
        if (_filterContextOptions.TryGetValue("status", out var statusOptions))
        {
            foreach (var opt in statusOptions)
            {
                opt.Display = opt.Value switch
                {
                    "Pending" => "待处理",
                    "Confirmed" => "已确认",
                    _ => opt.Value
                };
            }
        }

        // HasDelayPenalty 显示 是/否
        if (_filterContextOptions.TryGetValue("hasdelaypenalty", out var dpOptions))
        {
            foreach (var opt in dpOptions)
            {
                opt.Display = opt.Value == "True" ? "是" : "否";
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

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("orders", null, _allColumns);
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

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("orders", null);
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
        var savedState = await PageState.LoadAsync("orders");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "signdate";
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

        // 恢复页码
        if (savedState != null)
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && table != null)
            await table.ReloadServerData();
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#orders-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(SalesOrderListDto order, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "ordernumber":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => ViewOrder(order.Id)));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, order.OrderNumber)));
                builder.CloseComponent();
                break;
            case "signdate":
                builder.AddContent(0, order.SignDate.ToString("yyyy-MM-dd"));
                break;
            case "salesman":
                builder.AddContent(0, order.Salesman);
                break;
            case "customername":
                builder.AddContent(0, order.CustomerName);
                break;
            case "endcustomer":
                builder.AddContent(0, order.EndCustomer);
                break;
            case "deliverystart":
                builder.AddContent(0, order.DeliveryStart?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "deliveryend":
                builder.AddContent(0, order.DeliveryEnd?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "hasdelaypenalty":
                builder.AddContent(0, DisplayHelper.GetYesNoText(order.HasDelayPenalty));
                break;
            case "totalcontractweight":
                builder.AddContent(0, order.TotalContractWeight.ToString());
                break;
            case "itemcount":
                builder.AddContent(0, order.ItemCount);
                break;
            case "notech":
                if (order.HasTechnicalRequirement)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Success);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "已编辑")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Error);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "未编辑")));
                    builder.CloseComponent();
                    if (order.FirstOrderItemId.HasValue)
                    {
                        builder.OpenComponent<MudIconButton>(0);
                        builder.AddAttribute(1, "Icon", Icons.Material.Filled.Edit);
                        builder.AddAttribute(2, "Size", Size.Small);
                        builder.AddAttribute(3, "Color", Color.Warning);
                        builder.AddAttribute(4, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => ViewTechnicalRequirement(order.Id)));
                        builder.CloseComponent();
                    }
                }
                break;
            case "status":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetStatusColor(order.Status));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetStatusText(order.Status))));
                builder.CloseComponent();
                break;
            case "lastchangedate":
                builder.AddContent(0, order.LastChangeDate?.ToString("yyyy-MM-dd HH:mm") ?? "-");
                break;
        }
    };

    // ========== GetCellRawValue / GetCellDisplayText（用于 ExcelFilter 旧模式，保留引用） ==========

    private string? GetCellRawValue(SalesOrderListDto item, string key) => key switch
    {
        "ordernumber" => item.OrderNumber,
        "signdate" => item.SignDate.ToString("yyyy-MM-dd"),
        "salesman" => item.Salesman,
        "customername" => item.CustomerName,
        "endcustomer" => item.EndCustomer,
        "deliverystart" => item.DeliveryStart?.ToString("yyyy-MM-dd"),
        "deliveryend" => item.DeliveryEnd?.ToString("yyyy-MM-dd"),
        "hasdelaypenalty" => item.HasDelayPenalty.ToString(),
        "totalcontractweight" => item.TotalContractWeight.ToString(),
        "itemcount" => item.ItemCount.ToString(),
        "notech" => item.HasTechnicalRequirement.ToString(),
        "status" => item.Status.ToString(),
        "lastchangedate" => item.LastChangeDate?.ToString("yyyy-MM-dd HH:mm"),
        _ => null
    };

    private string? GetCellDisplayText(SalesOrderListDto item, string key) => key switch
    {
        "hasdelaypenalty" => DisplayHelper.GetYesNoText(item.HasDelayPenalty),
        "notech" => item.HasTechnicalRequirement ? "已编辑" : "未编辑",
        "status" => GetStatusText(item.Status),
        _ => GetCellRawValue(item, key)
    };

    // ========== 业务操作 ==========

    private void NavigateToCreate() => Navigation.NavigateTo("/orders/create");
    private void ViewOrder(int id) => Navigation.NavigateTo($"/orders/{id}");
    private void EditOrder(int id) => Navigation.NavigateTo($"/orders/{id}");
    private void ViewTechnicalRequirement(int orderId) => Navigation.NavigateTo($"/orders/{orderId}/requirements");

    private async Task ConfirmOrder(SalesOrderListDto order)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要将订单 \"{order.OrderNumber}\" 确认为正式合同吗？\n\n确认后状态将变为\"已确认\"。",
            ["ConfirmText"] = "确认",
            ["Color"] = Color.Primary
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        try
        {
            var updateRequest = new UpdateSalesOrderRequest
            {
                Status = "Confirmed",
                RowVersion = order.RowVersion ?? Array.Empty<byte>()
            };

            var result = await OrderService.UpdateAsync(order.Id, updateRequest);

            if (result.Success)
            {
                Snackbar.Add($"订单 \"{order.OrderNumber}\" 已确认为正式合同", Severity.Success);
                if (table != null) await table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "确认失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"确认失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task CancelOrder(SalesOrderListDto order)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要取消订单 \"{order.OrderNumber}\" 吗？\n\n取消后订单将被永久删除，不可恢复！",
            ["ConfirmText"] = "确认取消",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        try
        {
            var result = await OrderService.DeleteAsync(order.Id);
            if (result.Success)
            {
                Snackbar.Add($"订单 \"{order.OrderNumber}\" 已取消", Severity.Success);
                if (table != null) await table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "取消失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"取消失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 辅助方法 ==========

    private Color GetStatusColor(SalesOrderStatus status) => DisplayHelper.GetSalesOrderStatusColor(status);
    private string GetStatusText(SalesOrderStatus status) => DisplayHelper.GetSalesOrderStatusText(status);

    // ========== 打印方法 ==========

    private async Task PrintSingleOrder(int id)
    {
        try
        {
            var request = new OrderPrintBatchRequest { Ids = new[] { id } };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/order/print-file";
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
        if (!selectedOrderIds.Any())
        {
            Snackbar.Add("请先选择要打印的订单", Severity.Warning);
            return;
        }
        try
        {
            var request = new OrderPrintBatchRequest { Ids = selectedOrderIds.ToArray() };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/order/print-file";
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
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("orders", state);
    }
}
