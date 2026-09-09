using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using MES.Core.Helpers;
using System.Text.Json;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.Materials;

public partial class Suppliers
{
    private MudTable<SupplierProfileDto>? table;
    private List<SupplierProfileDto> _pageItems = new();
    private int _totalCount;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private HashSet<int> selectedIds = new();
    private bool _isArrowNavSetup;
    private bool allSelected
    {
        get => _pageItems.Any() && _pageItems.All(i => selectedIds.Contains(i.Id));
        set
        {
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
    private int _loadVersion;
    private bool _resetToFirstPage;
    private string _searchKeyword = string.Empty;

    private string sortColumn = "SupplierCode";
    private bool sortDescending = true;

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    // 列偏好版本键：每次默认显隐/列名变化递增，强制老用户按新默认重新加载（col_prefs_suppliers_v3）
    // v3：默认隐藏 编码/联系人/联系电话/地址 四列；「已到货」改口径为本年到货[扣除退货]、「本年累计退货」更名「本年退货」
    private const string ColumnPrefsVersion = "v3";

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    // ========== ② 往来信息 分组列标题栏（仿客户管理/订单列表 B23） ==========
    // 选择列 40px + 可见列宽和 + 操作列 100px（供应商操作列含编辑/删除，宽于客户 90px）
    private int _totalTableWidth =>
        40 + _visibleColumns.Sum(c => int.TryParse(c.Width, out var w) ? w : 100) + 100;

    private List<GroupHeaderInfo> _groupHeaders => GetGroupHeaders();

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
        result.Add(new GroupHeaderInfo { GroupKey = 0, GroupName = "", TotalWidth = 40, ColumnCount = 0, CssClass = "" });

        int? lastKey = null; int totalWidth = 0;
        var groupKey = 0; var groupName = ""; var count = 0;
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
                totalWidth = 0; count = 0;
            }
            groupKey = gk; groupName = col.GroupName ?? "";
            totalWidth += int.TryParse(col.Width, out var w) ? w : 100;
            count++; lastKey = gk;
        }
        if (count > 0)
            result.Add(new GroupHeaderInfo
            {
                GroupKey = groupKey,
                GroupName = groupName,
                TotalWidth = totalWidth,
                ColumnCount = count,
                CssClass = GetHeaderGroupCss(groupKey, true)
            });

        // 操作列占位（100px）
        result.Add(new GroupHeaderInfo { GroupKey = 0, GroupName = "", TotalWidth = 100, ColumnCount = 0, CssClass = "" });
        return result;
    }

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch { 1 => "col-g1", 2 => "col-g2", 3 => "col-g3", 4 => "col-g4", _ => "" };
        if (isGroupStart && groupKey > 1) cls += " col-group-start";
        return cls;
    }

    private static string GetCellGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch { 1 => "col-g1-cell", 2 => "col-g2-cell", 3 => "col-g3-cell", 4 => "col-g4-cell", _ => "" };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    // ========== ② 往来信息 统计列（DTO 成分字段映射，用于分页合计；null=该列不展示该成分） ==========
    // 到货/待收两列补金额（ArrivedAmount/PendingAmount，按单认领参考货款）；退货列仅吨
    private static readonly Dictionary<string, (string? CountField, string? WeightField, string? AmountField)> _statFieldMap = new()
    {
        ["TotalOrdering"] = ("TotalOrderCount", "TotalWeight", "TotalAmount"),
        ["YearOrdering"] = ("YearOrderCount", "YearWeight", "YearAmount"),
        ["Arrived"] = (null, "ArrivedWeight", "ArrivedAmount"),
        ["Pending"] = (null, "PendingWeight", "PendingAmount"),
        ["YearReturn"] = (null, "YearReturnWeight", null),
    };

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // ========== ① 基本信息（实体列，可排序/筛选/内联编辑） ==========
        // v3：编码/联系人/联系电话/地址 默认隐藏（可在列显隐中打开）
        new() { Key = "SupplierCode",     Label = "供应商编码", SortKey = "suppliercode",     FilterType = "string",  Width = "130", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "SupplierName",     Label = "供应商名称", SortKey = "suppliername",     FilterType = "string",  Width = "160", GroupKey = 1, GroupName = "① 基本信息" },
        new() { Key = "MaterialCategory", Label = "物料分类",   SortKey = "materialcategory", FilterType = "enum",    Width = "100", GroupKey = 1, GroupName = "① 基本信息", EnumOptions = DisplayHelper.GetEnumFilterOptions<MaterialType>() },
        new() { Key = "ContactPerson",    Label = "联系人",     SortKey = "contactperson",    FilterType = "string",  Width = "100", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "ContactPhone",     Label = "联系电话",   SortKey = "contactphone",     FilterType = "string",  Width = "130", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "Address",          Label = "地址",       SortKey = "address",          FilterType = "string",  Width = "200", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "Remark",           Label = "备注",       SortKey = "remark",           FilterType = "string",  Width = "200", GroupKey = 1, GroupName = "① 基本信息" },
        new() { Key = "IsActive",         Label = "状态",       SortKey = "isactive",         FilterType = "boolean", Width = "80",  GroupKey = 1, GroupName = "① 基本信息", BoolTrueLabel = "启用", BoolFalseLabel = "停用" },
        // ========== ② 往来信息（只读聚合数值列：采购+委外合并；出单列 单数+吨+万、到货/待收 吨+万、退货 仅吨；不可排序/筛选——SortKey=null 即只读标记） ==========
        new() { Key = "TotalOrdering", Label = "累计出单",          SortKey = null, FilterType = null, Width = "200", GroupKey = 2, GroupName = "② 往来信息" },
        new() { Key = "YearOrdering",  Label = "本年出单",          SortKey = null, FilterType = null, Width = "200", GroupKey = 2, GroupName = "② 往来信息" },
        new() { Key = "Arrived",       Label = "本年到货[扣除退货]", SortKey = null, FilterType = null, Width = "220", GroupKey = 2, GroupName = "② 往来信息" },
        new() { Key = "Pending",       Label = "待收货",            SortKey = null, FilterType = null, Width = "200", GroupKey = 2, GroupName = "② 往来信息" },
        new() { Key = "YearReturn",    Label = "本年退货",          SortKey = null, FilterType = null, Width = "170", GroupKey = 2, GroupName = "② 往来信息" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<SupplierProfileDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "suppliercode";
            var filtersJson = SerializeFilters();

            // 恢复持久化的页码（MudTable 初始化时始终传 page=0）
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

            var result = await SupplierService.GetPagedAsync(query);

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<SupplierProfileDto> { Items = _pageItems, TotalItems = _totalCount };

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

        return new TableData<SupplierProfileDto>
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
            var result = await SupplierService.GetFilterContextsAsync();
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

        // IsActive 列显示中文
        if (_filterContextOptions.TryGetValue("IsActive", out var isActiveOptions))
        {
            foreach (var opt in isActiveOptions)
            {
                opt.Display = opt.Value == "True" ? "启用" : "停用";
            }
        }

        // MaterialCategory 列显示中文
        if (_filterContextOptions.TryGetValue("MaterialCategory", out var materialCatOptions))
        {
            foreach (var opt in materialCatOptions)
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


    private async Task ToggleSort(string colKey)
    {
        // 只读统计列（SortKey=null）不可排序，忽略点击
        var col = _allColumns.FirstOrDefault(c => c.Key == colKey);
        if (col == null || col.SortKey == null) return;

        if (sortColumn == colKey)
            sortDescending = !sortDescending;
        else
        {
            sortColumn = colKey;
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

    // ========== 分页汇总（仿客户管理：仅对 ② 往来信息 数值列做页内合计） ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(SupplierProfileDto)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);
        foreach (var col in _visibleColumns)
        {
            if (col.GroupKey != 2) continue;
            if (!_statFieldMap.TryGetValue(col.Key, out var map)) continue;

            var weight = map.WeightField != null
                ? _pageItems.Sum(item => (decimal)(props[map.WeightField].GetValue(item) ?? 0m))
                : 0m;

            // 有金额或单数 → 组合文本（吨/万）；仅重量（本年退货）→ 只显吨
            var count = map.CountField != null
                ? _pageItems.Sum(item => (int)(props[map.CountField].GetValue(item) ?? 0))
                : 0;
            var amount = map.AmountField != null
                ? _pageItems.Sum(item => (decimal)(props[map.AmountField].GetValue(item) ?? 0m))
                : 0m;
            var withCount = map.CountField != null;
            _pageSums[col.Key] = (withCount || map.AmountField != null)
                ? BuildStatText(withCount, count, weight, amount)
                : BuildStatText(false, 0, weight, 0m);
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        if (_pageSums.TryGetValue(col.Key, out var sum))
            return sum;
        return "-";
    }

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("suppliers", ColumnPrefsVersion, _allColumns);
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
        var saved = await ColumnPrefs.LoadAsync("suppliers", ColumnPrefsVersion);
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
        var savedState = await PageState.LoadAsync("suppliers");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "SupplierCode";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
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
        }

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && table != null)
            await table.ReloadServerData();

        // 加载筛选上下文
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // ①/② 分组标题栏：与表格横向滚动联动（对齐各分组起止位置）
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#suppliers-list-table");
        }
        catch { }
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#suppliers-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string SupplierName { get; set; } = string.Empty;
        public string MaterialCategory { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? Remark { get; set; }
    }

    private void StartEdit(SupplierProfileDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            SupplierName = item.SupplierName,
            MaterialCategory = item.MaterialCategory?.ToString() ?? "",
            ContactPerson = item.ContactPerson ?? "",
            ContactPhone = item.ContactPhone ?? "",
            Address = item.Address ?? "",
            IsActive = item.IsActive,
            Remark = item.Remark
        };
    }

    private void CancelEdit(SupplierProfileDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(SupplierProfileDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateSupplierRequest
            {
                SupplierName = cache.SupplierName,
                MaterialCategory = EnumHelper.TryParse<MaterialType>(cache.MaterialCategory),
                ContactPerson = cache.ContactPerson,
                ContactPhone = cache.ContactPhone,
                Address = cache.Address,
                IsActive = cache.IsActive,
                Remark = cache.Remark
            };

            var result = await SupplierService.UpdateAsync(item.Id, request);
            if (result.Success)
            {
                item.SupplierName = cache.SupplierName;
                item.MaterialCategory = EnumHelper.TryParse<MaterialType>(cache.MaterialCategory);
                item.ContactPerson = cache.ContactPerson;
                item.ContactPhone = cache.ContactPhone;
                item.Address = cache.Address;
                item.IsActive = cache.IsActive;
                item.Remark = cache.Remark;

                _editingIds.Remove(item.Id);
                _editCache.Remove(item.Id);
                if (table != null) await table.ReloadServerData();
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

    private async Task DeleteItem(SupplierProfileDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除供应商 \"{item.SupplierName}\" 吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await SupplierService.DeleteAsync(item.Id);
                if (result.Success)
                {
                    Snackbar.Add("删除成功", Severity.Success);
                    if (table != null) await table.ReloadServerData();
                    await LoadFilterContextsAsync();
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

    private RenderFragment RenderCell(SupplierProfileDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing && _editCache.TryGetValue(item.Id, out var c) ? c : null;

        // 供应商往来统计 5 列：只读三色单元格（z单/x吨/y万，蓝单/绿吨/万橙），悬停显示完整纯文本值
        var statMarkup = RenderStatMarkup(item, col.Key);
        if (statMarkup.HasValue)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "title", RenderStatText(item, col.Key) ?? "—");
            builder.AddContent(2, statMarkup.Value);
            builder.CloseElement();
            return;
        }

        switch (col.Key)
        {
            case "SupplierCode":
                builder.AddContent(0, item.SupplierCode);
                break;

            case "SupplierName":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.SupplierName);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.SupplierName = v ?? ""));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.SupplierName);
                }
                break;

            case "MaterialCategory":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudSelect<MaterialType>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", EnumHelper.TryParse<MaterialType>(cache.MaterialCategory) ?? MaterialType.Finished);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<MaterialType>(this, v => cache.MaterialCategory = v.ToString()));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.AddAttribute(6, "ChildContent", (RenderFragment)(b2 =>
                    {
                        foreach (var opt in DisplayHelper.GetEnumOptions<MaterialType>())
                        {
                            b2.OpenComponent<MudSelectItem<MaterialType>>(0);
                            b2.AddAttribute(1, "Value", Enum.Parse<MaterialType>(opt.Value));
                            b2.AddAttribute(2, "Text", opt.Display);
                            b2.AddAttribute(3, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, opt.Display)));
                            b2.CloseComponent();
                        }
                    }));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.GetMaterialTypeText(item.MaterialCategory));
                }
                break;

            case "ContactPerson":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.ContactPerson);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.ContactPerson = v ?? ""));
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
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.ContactPhone = v ?? ""));
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
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.Address = v ?? ""));
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
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.Remark = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Remark);
                }
                break;

            case "IsActive":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudSwitch<bool>>(0);
                    builder.AddAttribute(1, "Value", cache.IsActive);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<bool>(this, v => cache.IsActive = v));
                    builder.AddAttribute(3, "Dense", true);
                    builder.AddAttribute(4, "Color", Color.Success);
                    builder.CloseComponent();
                }
                else
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", item.IsActive ? Color.Success : Color.Error);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.IsActive ? "启用" : "停用")));
                    builder.CloseComponent();
                }
                break;

            default:
                builder.AddContent(0, "");
                break;
        }
    };

    // ========== 供应商往来统计列渲染 ==========

    /// <summary>统计 5 列三色富文本：累计·本年出单(单数+吨+万)/到货·待收(吨+万)/本年退货(吨)；非统计列返回 null</summary>
    private static MarkupString? RenderStatMarkup(SupplierProfileDto item, string key)
    {
        if (!ResolveStat(item, key, out var withCount, out var count, out var weight, out var amount))
            return null;
        return OrderOverviewFormatter.RenderTradeMarkup(count, weight, amount, withCount);
    }

    /// <summary>统计列纯文本（同 RenderStatMarkup 数值口径，供 tooltip/打印）；非统计列返回 null</summary>
    private static string? RenderStatText(SupplierProfileDto item, string key)
    {
        if (!ResolveStat(item, key, out var withCount, out var count, out var weight, out var amount))
            return null;
        return OrderOverviewFormatter.RenderTradeText(count, weight, amount, withCount);
    }

    /// <summary>解析统计 5 列成分（出单列 带单数；到货/待收 吨+万；退货 仅吨）</summary>
    private static bool ResolveStat(SupplierProfileDto item, string key, out bool withCount, out int count, out decimal weight, out decimal amount)
    {
        withCount = false; count = 0; weight = 0m; amount = 0m;
        switch (key)
        {
            case "TotalOrdering": withCount = true; count = item.TotalOrderCount; weight = item.TotalWeight; amount = item.TotalAmount; return true;
            case "YearOrdering": withCount = true; count = item.YearOrderCount; weight = item.YearWeight; amount = item.YearAmount; return true;
            case "Arrived": weight = item.ArrivedWeight; amount = item.ArrivedAmount; return true;
            case "Pending": weight = item.PendingWeight; amount = item.PendingAmount; return true;
            case "YearReturn": weight = item.YearReturnWeight; amount = 0m; return true;
            default: return false;
        }
    }

    /// <summary>统计列页内合计文本（同 RenderStatText 数值口径，单/吨/万 取整）</summary>
    private static string BuildStatText(bool withCount, int count, decimal weightKg, decimal amountYuan)
        => OrderOverviewFormatter.RenderTradeText(count, weightKg, amountYuan, withCount);

    // ========== 打印方法（Mode A 列表打印：按当前可见列——含 ② 往来信息 全部统计列——完整打印选中行） ==========

    /// <summary>打印选中供应商（按当前可见列渲染列表 PDF，Mode A 前端已准备数据）</summary>
    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的供应商", Severity.Warning);
            return;
        }
        try
        {
            // 从当前页取选中行，按可见列把每格转显示文本（保证 ② 往来信息 统计列也能完整打印）
            var selectedItems = _pageItems
                .Where(s => selectedIds.Contains(s.Id))
                .Select(item =>
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var col in _visibleColumns)
                        dict[col.Key] = GetCellDisplayText(item, col) ?? "-";
                    return dict;
                }).ToList();

            var request = new OrderPrintListRequest
            {
                Title = "供应商列表",
                Items = selectedItems,
                Columns = GetPrintColumnDefs()
            };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Navigation.BaseUri}{ApiEndpoints.Supplier}/print-list-file";
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

    /// <summary>按列取打印显示文本：① 基本信息原样，② 往来信息走统计渲染（单/吨/万），与页面单元格口径一致</summary>
    private static string? GetCellDisplayText(SupplierProfileDto item, ColumnDef col)
    {
        if (col.GroupKey == 2)
            return RenderStatText(item, col.Key) ?? "-";

        return col.Key switch
        {
            "SupplierCode" => item.SupplierCode,
            "SupplierName" => item.SupplierName,
            "MaterialCategory" => DisplayHelper.GetMaterialTypeText(item.MaterialCategory),
            "ContactPerson" => item.ContactPerson,
            "ContactPhone" => item.ContactPhone,
            "Address" => item.Address,
            "IsActive" => item.IsActive ? "启用" : "停用",
            "Remark" => item.Remark,
            _ => "-"
        };
    }

    private void NavigateToCreate() => Navigation.NavigateTo("/suppliers/create");

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
        await PageState.SaveAsync("suppliers", state);
    }
}
