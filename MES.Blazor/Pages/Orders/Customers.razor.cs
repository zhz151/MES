using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Blazor.Shared;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Core.DTOs.Order;
using System.Text.Json;

namespace MES.Blazor.Pages.Orders;

public partial class Customers
{
    private MudTable<CustomerProfileDto>? table;
    private List<CustomerProfileDto> _pageItems = new();
    private int _totalCount;
    private HashSet<int> selectedIds = new();
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
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;

    private string sortColumn = "CustomerCode";
    private bool sortDescending = true;

    // ExcelFilter 状态
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string CustomerCode { get; set; } = string.Empty;
        public string Salesman { get; set; } = string.Empty;
        public string CustomerUnit { get; set; } = string.Empty;
        public string EndCustomer { get; set; } = string.Empty;
        public CustomerStatus Status { get; set; }
        public string? ContactPerson { get; set; }
        public string? ContactPhone { get; set; }
        public string? Address { get; set; }
        public string? Remark { get; set; }
    }

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "CustomerCode", Label = "客户编码", SortKey = "customercode", FilterType = "string", IsRequired = true, Width = "120" },
        new() { Key = "Salesman",     Label = "业务员",   SortKey = "salesman",     FilterType = "string", IsRequired = true, Width = "120" },
        new() { Key = "CustomerUnit", Label = "客户单位", SortKey = "customerunit", FilterType = "string", IsRequired = true, Width = "120" },
        new() { Key = "EndCustomer",  Label = "最终用户", SortKey = "endcustomer",  FilterType = "string", Width = "120" },
        new() { Key = "Status",       Label = "状态",     SortKey = "status",       FilterType = "enum",     EnumOptions = new() { new("Active", "启用"), new("Inactive", "停用") }, Width = "120" },
        new() { Key = "ContactPerson",Label = "联系人",     SortKey = "contactperson", FilterType = "string", Width = "120" },
        new() { Key = "ContactPhone", Label = "联系电话",   SortKey = "contactphone",  FilterType = "string", Width = "120" },
        new() { Key = "Address",      Label = "联系地址",   SortKey = "address",       FilterType = "string", Width = "150" },
        new() { Key = "Remark",       Label = "备注",       SortKey = "remark",        FilterType = "string", Width = "120" },
    };

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("customers", null, _allColumns);
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

    private async Task<TableData<CustomerProfileDto>> LoadDataFromServer(TableState state)
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

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "customercode";
            var filtersJson = SerializeFilters();

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = string.IsNullOrEmpty(sortBy) ? "customercode" : sortBy,
                IsDescending = sortDescending
            };
            if (filtersJson != null)
                query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson);

            var result = await CustomerService.GetPagedAsync(query);

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

        await SavePageStateAsync();

        return new TableData<CustomerProfileDto>
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
            var result = await CustomerService.GetFilterContextsAsync();
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
        _filterContextOptions = filterContexts.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Select(v => new ExcelFilterOption { Value = v, Display = v, Count = 0 }).ToList()
        );

        // Status 列显示中文
        if (_filterContextOptions.TryGetValue("Status", out var statusOptions))
        {
            foreach (var opt in statusOptions)
                opt.Display = opt.Value == "Active" ? "启用" : "停用";
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

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("customers", null);
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
        var savedState = await PageState.LoadAsync("customers");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "CustomerCode";
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

        // 加载筛选上下文（ExcelFilter 下拉选项）
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#customers-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 导航 ==========

    private void NavigateToCreate() => Navigation.NavigateTo("/customers/create");

    // ========== 内联编辑操作 ==========

    private void StartEdit(CustomerProfileDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            CustomerCode = item.CustomerCode,
            Salesman = item.Salesman,
            CustomerUnit = item.CustomerUnit,
            EndCustomer = item.EndCustomer,
            Status = item.Status,
            ContactPerson = item.ContactPerson,
            ContactPhone = item.ContactPhone,
            Address = item.Address,
            Remark = item.Remark
        };
    }

    private void CancelEdit(CustomerProfileDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(CustomerProfileDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.CustomerCode)) errors.Add("客户编码不能为空");
        if (string.IsNullOrWhiteSpace(cache.Salesman)) errors.Add("业务员不能为空");
        if (string.IsNullOrWhiteSpace(cache.CustomerUnit)) errors.Add("客户单位不能为空");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateCustomerRequest
            {
                CustomerCode = cache.CustomerCode,
                Salesman = cache.Salesman,
                CustomerUnit = cache.CustomerUnit,
                EndCustomer = cache.EndCustomer,
                ContactPerson = cache.ContactPerson,
                ContactPhone = cache.ContactPhone,
                Address = cache.Address,
                Status = cache.Status,
                Remark = cache.Remark
            };

            var result = await CustomerService.UpdateAsync(item.Id, request);
            if (result.Success)
            {
                // 更新列表中的缓存数据
                item.CustomerCode = cache.CustomerCode;
                item.Salesman = cache.Salesman;
                item.CustomerUnit = cache.CustomerUnit;
                item.EndCustomer = cache.EndCustomer;
                item.Status = cache.Status;
                item.ContactPerson = cache.ContactPerson;
                item.ContactPhone = cache.ContactPhone;
                item.Address = cache.Address;
                item.Remark = cache.Remark;

                _editingIds.Remove(item.Id);
                _editCache.Remove(item.Id);
                Snackbar.Add("更新成功", Severity.Success);
            }
            else
            {
                Snackbar.Add(result.Message ?? "更新失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"更新失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    // ========== 删除 ==========

    private async Task DeleteItem(CustomerProfileDto customer)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除客户 \"{customer.CustomerUnit}\" 吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await CustomerService.DeleteAsync(customer.Id);
                if (result.Success)
                {
                    Snackbar.Add("删除成功", Severity.Success);
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
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(CustomerProfileDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing && _editCache.TryGetValue(item.Id, out var c) ? c : null;

        switch (col.Key)
        {
            case "CustomerCode":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.CustomerCode);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.CustomerCode = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.CustomerCode);
                }
                break;
            case "Salesman":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.Salesman);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.Salesman = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Salesman);
                }
                break;
            case "CustomerUnit":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.CustomerUnit);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.CustomerUnit = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.CustomerUnit);
                }
                break;
            case "EndCustomer":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.EndCustomer);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.EndCustomer = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.EndCustomer);
                }
                break;
            case "Status":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudSelect<string>>(0);
                    builder.AddAttribute(1, "Value", cache.Status.ToString());
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v =>
                    {
                        cache.Status = v == CustomerStatus.Active.ToString() ? CustomerStatus.Active : CustomerStatus.Inactive;
                    }));
                    builder.AddAttribute(3, "Dense", true);
                    builder.AddAttribute(4, "Variant", Variant.Outlined);
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.AddAttribute(6, "ChildContent", (RenderFragment)(cb =>
                    {
                        foreach (var val in Enum.GetValues<CustomerStatus>())
                        {
                            cb.OpenComponent<MudSelectItem<string>>(0);
                            cb.AddAttribute(1, "Value", val.ToString());
                            cb.AddAttribute(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, DisplayHelper.GetCustomerStatusText(val))));
                            cb.CloseComponent();
                        }
                    }));
                    builder.CloseComponent();
                }
                else
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", item.Status == CustomerStatus.Active ? Color.Success : Color.Error);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.Status == CustomerStatus.Active ? "启用" : "停用")));
                    builder.CloseComponent();
                }
                break;
            case "ContactPerson":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.ContactPerson);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.ContactPerson = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.ContactPerson);
                }
                break;
            case "ContactPhone":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.ContactPhone);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.ContactPhone = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.ContactPhone);
                }
                break;
            case "Address":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.Address);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.Address = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Address);
                }
                break;
            case "Remark":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.Remark);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.Remark = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Remark);
                }
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    // ========== 打印方法 ==========

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的客户", Severity.Warning);
            return;
        }
        try
        {
            var ids = selectedIds.ToArray();
            var request = new OrderPrintBatchRequest { Ids = ids, Columns = _visibleColumns.Select(c => c.ToPrintColumnDef()).ToList() };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/customer/print-batch-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task PrintAll()
    {
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "customercode";
            var request = new OrderPrintAllRequest
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending,
                Columns = _visibleColumns.Select(c => c.ToPrintColumnDef()).ToList()
            };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/customer/print-all-file";
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
        await PageState.SaveAsync("customers", state);
    }
}
