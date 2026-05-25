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

namespace MES.Blazor.Pages;

public partial class PurchaseOrders
{
    private MudTable<PurchaseOrderDto>? table;
    private List<PurchaseOrderDto> _pageItems = new();
    private int _totalCount;
    private bool isSyncing;
    private string _searchKeyword = string.Empty;
    private bool _isArrowNavSetup;
    private bool _isAdmin;
    private int _currentPage = 1;
    private int _pageSize = 10;

    // 排序状态
    private string sortColumn = "orderdate";
    private bool sortDescending = true;

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
                    selectedIds.Add(item.Id);
            }
            else
            {
                selectedIds.Clear();
            }
            StateHasChanged();
        }
    }
    private HashSet<int> selectedIds = new();

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 采购状态 & 关联异常 ==========
    private List<ProcurementStatusDto> procurementItems = new();
    private bool showProcurementStatus = false;
    private List<OrderMismatchInfo> mismatchItems = new();

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "OrderNo",             Label = "采购单号",     SortKey = "orderno", FilterType = "string" },
        new() { Key = "SourceWorkOrderNo",   Label = "来源工单号",   SortKey = "sourceworkorderno", FilterType = "string" },
        new() { Key = "WoSalesOrderNo",      Label = "订单号",       SortKey = "wosalesorderno", FilterType = "string" },
        new() { Key = "WoProductionMainNo",  Label = "主号",         SortKey = "woproductionmainno", FilterType = "string" },
        new() { Key = "WoProductionSubNo",   Label = "次号",         SortKey = "woproductionsubno", FilterType = "string" },
        new() { Key = "WoSignDate",          Label = "签订日期",     SortKey = "wosigndate", FilterType = "date" },
        new() { Key = "WoSalesman",          Label = "业务员",       SortKey = "wosalesman", FilterType = "string" },
        new() { Key = "WoEndCustomer",       Label = "最终用户",     SortKey = "woendcustomer", FilterType = "string" },
        new() { Key = "WoDeliveryDate",      Label = "交货日期",     SortKey = "wodeliverydate", FilterType = "date" },
        new() { Key = "WoDelayPenalty",      Label = "延期罚款",     SortKey = "wodelaypenalty", FilterType = "enum",
            EnumOptions = new() { new("True", "是"), new("False", "否") } },
        new() { Key = "WoSettlementMethod",  Label = "结算方式",     SortKey = "wosettlementmethod", FilterType = "enum",
            EnumOptions = new() { new("MonthlyStatement", "月结"), new("PerOrder", "单结"), new("Deposit", "定金"), new("FullPayment", "全款") } },
        new() { Key = "WoPlantGrade",        Label = "工厂牌号",     SortKey = "woplantgrade", FilterType = "string" },
        new() { Key = "WoSpecification",     Label = "成品规格",     SortKey = "wospecification", FilterType = "string" },
        new() { Key = "WoLengthStatus",      Label = "长度状态",     FilterType = "enum",
            EnumOptions = new() { new("Fixed", "定尺"), new("Range", "范围尺") } },
        new() { Key = "WoMaxLength",         Label = "最大长度",     SortKey = "womaxlength" },
        new() { Key = "WoTotalQuantity",     Label = "总支数",       SortKey = "wototalquantity" },
        new() { Key = "WoTotalWeight",       Label = "总重量",       SortKey = "wototalweight" },
        new() { Key = "WoDeliveryState",     Label = "交货状态",     FilterType = "enum",
            EnumOptions = new() { new("Raw", "原料"), new("SemiFinished", "半成品"), new("Finished", "成品") } },
        new() { Key = "WoTotalItemCount",    Label = "含项次数",     SortKey = "wototalitemcount" },
        new() { Key = "OrderDate",           Label = "下单日期",     SortKey = "orderdate", FilterType = "date" },
        new() { Key = "MaterialCategory",    Label = "物料分类",     SortKey = "materialcategory", FilterType = "string" },
        new() { Key = "PlantGrade",          Label = "厂内钢种",     SortKey = "plantgrade", FilterType = "string" },
        new() { Key = "Specification",       Label = "规格",         SortKey = "specification", FilterType = "string" },
        new() { Key = "UnitWeight",          Label = "单支重量",     SortKey = "unitweight" },
        new() { Key = "Quantity",            Label = "支数",         SortKey = "quantity" },
        new() { Key = "InputMultiple",       Label = "投料倍率",     SortKey = "inputmultiple" },
        new() { Key = "Weight",              Label = "采购重量",     SortKey = "weight" },
        new() { Key = "RequiredDate",        Label = "要求到货日",   SortKey = "requireddate", FilterType = "date" },
        new() { Key = "SupplierName",        Label = "供应商",       SortKey = "suppliername", FilterType = "string" },
        new() { Key = "Status",              Label = "状态",         FilterType = "enum",
            EnumOptions = new() { new("Open", "已下单"), new("Partial", "部分到货"), new("Completed", "已完成"), new("Cancelled", "已取消") } },
        new() { Key = "Received",            Label = "已到货" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<PurchaseOrderDto>> LoadDataFromServer(TableState state)
    {
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "orderdate";
            var filters = SerializeFilters();

            var result = await PurchaseService.GetPagedAsync(
                new QueryParams
                {
                    PageIndex = state.Page + 1,
                    PageSize = state.PageSize,
                    SortBy = sortBy,
                    IsDescending = sortDescending,
                    Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                    Filters = filters
                });

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

        return new TableData<PurchaseOrderDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
    }

    private List<FilterDescriptor>? SerializeFilters()
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
        return descriptors.Count > 0 ? descriptors : null;
    }

    // ========== 筛选上下文加载（ExcelFilter 下拉选项） ==========

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await PurchaseService.GetFilterContextsAsync();
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
                opt.Display = opt.Value switch
                {
                    "Open" => "已下单",
                    "Partial" => "部分到货",
                    "Completed" => "已完成",
                    "Cancelled" => "已取消",
                    _ => opt.Value
                };
            }
        }

        // WoDelayPenalty 列显示中文
        if (_filterContextOptions.TryGetValue("WoDelayPenalty", out var delayOptions))
        {
            foreach (var opt in delayOptions)
                opt.Display = opt.Value == "True" ? "是" : "否";
        }

        // WoSettlementMethod 列显示中文
        if (_filterContextOptions.TryGetValue("WoSettlementMethod", out var settlementOptions))
        {
            foreach (var opt in settlementOptions)
            {
                opt.Display = opt.Value switch
                {
                    "MonthlyStatement" => "月结",
                    "PerOrder" => "单结",
                    "Deposit" => "定金",
                    "FullPayment" => "全款",
                    _ => opt.Value
                };
            }
        }

        // WoLengthStatus 列显示中文
        if (_filterContextOptions.TryGetValue("WoLengthStatus", out var lengthOptions))
        {
            foreach (var opt in lengthOptions)
            {
                opt.Display = opt.Value switch
                {
                    "Fixed" => "定尺",
                    "Range" => "范围尺",
                    _ => opt.Value
                };
            }
        }

        // WoDeliveryState 列显示中文
        if (_filterContextOptions.TryGetValue("WoDeliveryState", out var deliveryOptions))
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
        await ColumnPrefs.SaveAsync("purchase_orders", null, _allColumns);
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

    private RenderFragment RenderCell(PurchaseOrderDto item, ColumnDef col) => builder =>
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
            case "SourceWorkOrderNo":
                builder.AddContent(0, item.SourceWorkOrderNo);
                break;
            case "WoSalesOrderNo":
                builder.AddContent(0, item.WoSalesOrderNo);
                break;
            case "WoProductionMainNo":
                builder.AddContent(0, item.WoProductionMainNo);
                break;
            case "WoProductionSubNo":
                builder.AddContent(0, item.WoProductionSubNo);
                break;
            case "WoSignDate":
                builder.AddContent(0, item.WoSignDate?.ToString("yyyy-MM-dd"));
                break;
            case "WoSalesman":
                builder.AddContent(0, item.WoSalesman);
                break;
            case "WoEndCustomer":
                builder.AddContent(0, item.WoEndCustomer ?? "-");
                break;
            case "WoDeliveryDate":
                builder.AddContent(0, item.WoDeliveryDate?.ToString("yyyy-MM-dd"));
                break;
            case "WoDelayPenalty":
                builder.AddContent(0, DisplayHelper.GetYesNoText(item.WoDelayPenalty));
                break;
            case "WoSettlementMethod":
                builder.AddContent(0, item.WoSettlementMethod.HasValue ? DisplayHelper.GetSettlementMethodText(item.WoSettlementMethod.Value) : "-");
                break;
            case "WoPlantGrade":
                builder.AddContent(0, item.WoPlantGrade);
                break;
            case "WoSpecification":
                builder.AddContent(0, item.WoSpecification);
                break;
            case "WoLengthStatus":
                builder.AddContent(0, item.WoLengthStatus.HasValue ? DisplayHelper.GetLengthStatusText(item.WoLengthStatus.Value) : "-");
                break;
            case "WoMaxLength":
                builder.AddContent(0, item.WoMaxLength?.ToString("G29") ?? "-");
                break;
            case "WoTotalQuantity":
                builder.AddContent(0, item.WoTotalQuantity);
                break;
            case "WoTotalWeight":
                builder.AddContent(0, item.WoTotalWeight?.ToString("G29") ?? "-");
                break;
            case "WoDeliveryState":
                builder.AddContent(0, item.WoDeliveryState.HasValue ? DisplayHelper.GetDeliveryStateText(item.WoDeliveryState.Value) : "-");
                break;
            case "WoTotalItemCount":
                builder.AddContent(0, item.WoTotalItemCount);
                break;
            case "OrderDate":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", Color.Info);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.OrderDate.ToString("yyyy-MM-dd"))));
                builder.CloseComponent();
                break;
            case "MaterialCategory":
                builder.AddContent(0, item.MaterialCategory);
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade);
                break;
            case "Specification":
                builder.AddContent(0, item.Specification);
                break;
            case "UnitWeight":
                builder.AddContent(0, item.UnitWeight?.ToString("G29") ?? "-");
                break;
            case "Quantity":
                builder.AddContent(0, item.Quantity?.ToString("G29") ?? "-");
                break;
            case "InputMultiple":
                builder.AddContent(0, item.InputMultiple?.ToString() ?? "-");
                break;
            case "Weight":
                builder.AddContent(0, item.Weight.ToString("G29"));
                break;
            case "RequiredDate":
                builder.AddContent(0, item.RequiredDate.ToString("yyyy-MM-dd"));
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
            case "Received":
                builder.AddContent(0, $"{item.ReceivedQuantity}支/{item.ReceivedWeight.ToString("G29")}kg");
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

        // 列定义与偏好加载
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("purchase_orders", null);
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
        var savedState = await PageState.LoadAsync("purchase_orders");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "orderdate";
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

        // 加载关联异常
        await LoadOrderMismatches();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#purchase-orders-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 采购状态 ==========

    private async Task LoadProcurementStatus()
    {
        try
        {
            var result = await PurchaseService.GetProcurementStatusAsync();
            if (result.Success && result.Data != null)
                procurementItems = result.Data;
            else if (!result.Success)
                Snackbar.Add($"加载用料计划执行状态失败: {result.Message}", Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载用料计划执行状态异常: {ex.Message}", Severity.Error);
        }
    }

    private void ToggleProcurementStatus() => showProcurementStatus = !showProcurementStatus;

    private async Task LoadOrderMismatches()
    {
        try
        {
            var result = await PurchaseService.GetMismatchedOrdersAsync();
            if (result.Success && result.Data != null && result.Data.Count > 0)
            {
                mismatchItems = result.Data;
                var messages = mismatchItems.Select(item =>
                    $"采购单号 {item.OrderNo} 中，来源工单号：{string.Join("；", item.MismatchedWorkOrderNos)} 已不关联采购用料计划，需修改！");
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

    // ========== 即时更新 & 同步 ==========

    private async Task SyncAllAsync(bool silent = false)
    {
        if (!silent) { isSyncing = true; StateHasChanged(); }
        try
        {
            var result = await PurchaseService.SyncAllAsync();
            if (result.Success)
            {
                if (!silent) Snackbar.Add("同步完成", Severity.Success);
                await LoadProcurementStatus();
                await LoadOrderMismatches();
                if (table != null) await table.ReloadServerData();
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
            Snackbar.Add("请先选择要打印的采购单", Severity.Warning);
            return;
        }
        try
        {
            var ids = selectedIds.ToArray();
            var result = await PurchaseService.PrintOrderBatchAsync(ids);
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
            var result = await PurchaseService.PrintOrderAllAsync(
                string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortColumn, sortDescending);
            if (result.Success && result.Data != null)
                await JS.InvokeVoidAsync("openPdf", result.Data);
            else
                Snackbar.Add(result.Message ?? "打印失败", Severity.Error);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    // ========== 导航 ==========

    private void NavigateToCreate() => Navigation.NavigateTo("/purchase-orders/create");
    private void ViewDetail(int id) => Navigation.NavigateTo($"/purchase-orders/{id}");
    private void NavigateToEdit(int id) => Navigation.NavigateTo($"/purchase-orders/{id}");

    // ========== 取消订单 ==========

    private async Task CancelOrder(PurchaseOrderDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters { ["ContentText"] = $"确定要取消采购单 \"{item.OrderNo}\" 吗？", ["ConfirmText"] = "确认取消", ["Color"] = Color.Error });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await PurchaseService.DeleteAsync(item.Id);
                if (result.Success) { Snackbar.Add("取消成功", Severity.Success); await LoadProcurementStatus(); if (table != null) await table.ReloadServerData(); }
                else { Snackbar.Add(result.Message ?? "取消失败", Severity.Error); }
            }
            catch (Exception ex) { Snackbar.Add($"取消失败: {ex.Message}", Severity.Error); }
        }
    }

    // ========== 颜色 ==========

    private static Color GetStatusColor(PurchaseOrderStatus status) => DisplayHelper.GetPurchaseOrderStatusColor(status);
    private static string GetStatusText(PurchaseOrderStatus status) => DisplayHelper.GetPurchaseOrderStatusText(status);

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
        await PageState.SaveAsync("purchase_orders", state);
    }
}
