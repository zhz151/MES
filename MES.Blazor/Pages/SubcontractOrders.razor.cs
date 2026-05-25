using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Shared;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Rendering;

namespace MES.Blazor.Pages;

public partial class SubcontractOrders
{
    private MudTable<SubcontractOrderDto>? table;
    private List<SubcontractOrderDto> _pageItems = new();
    private int _totalCount;
    private HashSet<int> selectedIds = new();
    private bool _isArrowNavSetup;
    private bool _allSelected;
    private bool _isAdmin;
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
    private bool isSyncing;
    private string _searchKeyword = string.Empty;

    private string sortColumn = "OrderDate";
    private bool sortDescending = true;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    private List<ProcurementStatusDto> procurementItems = new();
    private bool showProcurementStatus = false;
    private List<OrderMismatchInfo> mismatchItems = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "OrderNo",             Label = "委外单号",     SortKey = "OrderNo",             FilterType = "string" },
        new() { Key = "OrderDate",           Label = "下单日期",     SortKey = "OrderDate", FilterType = "date" },
        new() { Key = "ProcessType",         Label = "加工类型",     SortKey = "ProcessType",         FilterType = "string" },
        new() { Key = "OutMaterialCategory", Label = "物料分类",     SortKey = "OutMaterialCategory", FilterType = "string" },
        new() { Key = "OutPlantGrade",       Label = "工厂牌号",     SortKey = "OutPlantGrade",       FilterType = "string" },
        new() { Key = "OutSpecification",    Label = "规格",         SortKey = "OutSpecification",    FilterType = "string" },
        new() { Key = "OutQuantity",         Label = "发出支数",     SortKey = "OutQuantity" },
        new() { Key = "OutWeight",           Label = "发出重量",     SortKey = "OutWeight" },
        new() { Key = "ReturnDeadline",      Label = "收回期限",     SortKey = "ReturnDeadline", FilterType = "date" },
        new() { Key = "SupplierName",        Label = "供应商",       SortKey = "SupplierName",        FilterType = "string" },
        new() { Key = "Status",              Label = "状态",         SortKey = "Status",              FilterType = "enum",
            EnumOptions = new() { new("Sent", "已发出"), new("PartialReturned", "部分收回"), new("Completed", "已完成"), new("Cancelled", "已取消") } },
        new() { Key = "Returned",            Label = "已回收" },
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

    // ========== 服务端数据加载 ==========

    private async Task<TableData<SubcontractOrderDto>> LoadDataFromServer(TableState state)
    {
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "OrderDate";
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

            var result = await SubcontractService.GetPagedAsync(query);

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

        // Status 列显示中文
        if (_filterContextOptions.TryGetValue("Status", out var statusOptions))
        {
            foreach (var opt in statusOptions)
            {
                opt.Display = GetStatusText(Enum.TryParse<SubcontractOrderStatus>(opt.Value, out var s) ? s : SubcontractOrderStatus.Sent);
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

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(SubcontractOrderDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "OrderNo":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => ViewDetail(item.Id)));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => {
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
                builder.AddContent(0, item.ProcessType);
                break;
            case "OutMaterialCategory":
                builder.AddContent(0, item.OutMaterialCategory);
                break;
            case "OutPlantGrade":
                builder.AddContent(0, item.OutPlantGrade);
                break;
            case "OutSpecification":
                builder.AddContent(0, item.OutSpecification);
                break;
            case "OutQuantity":
                builder.AddContent(0, item.OutQuantity.ToString("G29"));
                break;
            case "OutWeight":
                builder.AddContent(0, item.OutWeight.ToString("G29"));
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
            case "Returned":
                builder.AddContent(0, $"{item.InQuantity?.ToString("G29") ?? "0"}支/{item.InWeight?.ToString("G29") ?? "0"}kg");
                break;
        }
    };

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        _isAdmin = user.Claims.Any(c => c.Type == "role" && c.Value == "Admin")
                || user.IsInRole("Admin");

        await LoadProcurementStatus();

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
            if (result.Success && result.Data != null && result.Data.Count > 0)
            {
                mismatchItems = result.Data;
                var messages = mismatchItems.Select(item =>
                    $"委外单号 {item.OrderNo} 中，来源工单号：{string.Join("；", item.MismatchedWorkOrderNos)} 已不关联采购用料计划，需修改！");
                Snackbar.Add($"发现 {mismatchItems.Count} 条工单关联异常：\n{string.Join("\n", messages)}", Severity.Warning, config =>
                {
                    config.VisibleStateDuration = 20000;
                    config.Action = "忽略";
                });
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"检测工单关联异常: {ex.Message}", Severity.Error);
        }
    }

    // ========== 同步 ==========

    private async Task SyncAllAsync(bool silent = false)
    {
        if (!silent) { isSyncing = true; StateHasChanged(); }
        try
        {
            var result = await SubcontractService.SyncAllAsync();
            if (result.Success)
            {
                if (!silent) Snackbar.Add("同步完成", Severity.Success);
                if (table != null) await table.ReloadServerData();
                await LoadProcurementStatus();
                await LoadOrderMismatches();
            }
            else if (!silent)
            {
                Snackbar.Add(result.Message ?? "同步失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            if (!silent) Snackbar.Add($"同步失败: {ex.Message}", Severity.Error);
        }
        finally { if (!silent) { isSyncing = false; StateHasChanged(); } }
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
            var ids = selectedIds.ToArray();
            var result = await SubcontractService.PrintOrderBatchAsync(ids);
            if (result.Success && result.Data != null)
                await JS.InvokeVoidAsync("openPdf", result.Data);
            else
                Snackbar.Add(result.Message ?? "打印失败", Severity.Error);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    private async Task PrintAll()
    {
        try
        {
            var result = await SubcontractService.PrintOrderAllAsync(
                string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortColumn, sortDescending);
            if (result.Success && result.Data != null)
                await JS.InvokeVoidAsync("openPdf", result.Data);
            else
                Snackbar.Add(result.Message ?? "打印失败", Severity.Error);
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
                if (result.Success) { Snackbar.Add("取消成功", Severity.Success); if (table != null) await table.ReloadServerData(); await LoadProcurementStatus(); }
                else { Snackbar.Add(result.Message ?? "取消失败", Severity.Error); }
            }
            catch (Exception ex) { Snackbar.Add($"取消失败: {ex.Message}", Severity.Error); }
        }
    }

    // ========== 辅助方法 ==========

    private static Color GetStatusColor(SubcontractOrderStatus status) => status switch { SubcontractOrderStatus.Sent => Color.Info, SubcontractOrderStatus.PartialReturned => Color.Warning, SubcontractOrderStatus.Completed => Color.Success, SubcontractOrderStatus.Cancelled => Color.Default, _ => Color.Default };
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
}
