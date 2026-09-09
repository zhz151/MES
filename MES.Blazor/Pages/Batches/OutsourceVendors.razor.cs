using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Blazor.Shared;
using MES.Core.Constants;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Shared;
using MES.Core.Models;
using MES.Shared.Constants;
using System.Text.Json;

namespace MES.Blazor.Pages.Batches;

public partial class OutsourceVendors
{
    private MudTable<OutsourceVendorProfileDto>? table;
    private List<OutsourceVendorProfileDto> _pageItems = new();
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

    private string sortColumn = "VendorCode";
    private bool sortDescending = true;

    // ========== 分页汇总 ==========
    private Dictionary<string, string> _pageSums = new();

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    // 列偏好版本键：每次默认显隐/列名变化递增，强制老用户按新默认重新加载（col_prefs_outsource-vendors_v2）
    // v2：①/② 两分组（① 基本信息 8 列 + ② 往来信息 5 统计列）；默认显 单位名/工段/本厂/状态 + 5 统计列，隐藏 编码/联系人/电话/备注
    private const string ColumnPrefsVersion = "v2";

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    // ========== ① 基本信息 / ② 往来信息 分组列标题栏（仿供应商管理） ==========
    // 选择列 40px + 可见列宽和 + 操作列 100px
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
    // 累计/本年委外 单数+吨+万；本年回收 吨+万；委外未回收 吨+万；本年退回 仅吨（非正常退回无金额）
    private static readonly Dictionary<string, (string? CountField, string? WeightField, string? AmountField)> _statFieldMap = new()
    {
        ["TotalOrdering"] = ("TotalOrderCount", "TotalWeight", "TotalAmount"),
        ["YearOrdering"] = ("YearOrderCount", "YearWeight", "YearAmount"),
        ["YearRecovered"] = (null, "YearRecoveredWeight", "YearRecoveredAmount"),
        ["Pending"] = (null, "PendingWeight", "PendingAmount"),
        ["YearReturn"] = (null, "YearReturnWeight", null),
    };

    /// <summary>委外工段全部可选档（英文 key + 中文显示），供列筛/编辑下拉</summary>
    private static List<EnumOption> SectionOptions() => SectionKeys.All
        .Select(k => new EnumOption(k, SectionKeys.ToChinese(k) ?? k))
        .ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // ========== ① 基本信息（实体列，可排序/筛选/内联编辑） ==========
        // v2：编码/联系人/联系电话/备注 默认隐藏（可在列显隐中打开）
        new() { Key = "VendorCode",    Label = "编码",       SortKey = "vendorcode",    FilterType = "string",  Width = "90",  GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "VendorName",    Label = "委外单位名", SortKey = "vendorname",    FilterType = "string",  Width = "160", GroupKey = 1, GroupName = "① 基本信息" },
        new() { Key = "SectionName",   Label = "委外工段",   SortKey = "sectionname",   FilterType = "enum",    Width = "120", GroupKey = 1, GroupName = "① 基本信息", EnumOptions = SectionOptions() },
        new() { Key = "IsWorkshop",    Label = "本厂",       SortKey = "isworkshop",    FilterType = "boolean", Width = "110", GroupKey = 1, GroupName = "① 基本信息", BoolTrueLabel = "是", BoolFalseLabel = "外协" },
        new() { Key = "ContactPerson", Label = "联系人",     SortKey = "contactperson", FilterType = "string",  Width = "110", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "ContactPhone",  Label = "联系电话",   SortKey = "contactphone",  FilterType = "string",  Width = "130", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "Remark",        Label = "备注",       SortKey = "remark",        FilterType = "string",  Width = "200", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "IsActive",      Label = "状态",       SortKey = "isactive",      FilterType = "boolean", Width = "80",  GroupKey = 1, GroupName = "① 基本信息", BoolTrueLabel = "启用", BoolFalseLabel = "停用" },
        // ========== ② 往来信息（只读聚合数值列：工段委外统计；出单列 单数+吨+万、回收/未回收 吨+万、退回 仅吨；不可排序/筛选——SortKey=null 即只读标记） ==========
        new() { Key = "TotalOrdering", Label = "累计委外",           SortKey = null, FilterType = null, Width = "200", GroupKey = 2, GroupName = "② 往来信息" },
        new() { Key = "YearOrdering",  Label = "本年委外",           SortKey = null, FilterType = null, Width = "200", GroupKey = 2, GroupName = "② 往来信息" },
        new() { Key = "YearRecovered", Label = "本年回收[扣除退回]", SortKey = null, FilterType = null, Width = "200", GroupKey = 2, GroupName = "② 往来信息" },
        new() { Key = "Pending",       Label = "委外未回收",         SortKey = null, FilterType = null, Width = "180", GroupKey = 2, GroupName = "② 往来信息" },
        new() { Key = "YearReturn",    Label = "本年退回",           SortKey = null, FilterType = null, Width = "160", GroupKey = 2, GroupName = "② 往来信息" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<OutsourceVendorProfileDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "vendorcode";
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

            var result = await OutsourceVendorSvc.GetPagedAsync(query);

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<OutsourceVendorProfileDto> { Items = _pageItems, TotalItems = _totalCount };

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
                _pageSums.Clear();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
            _pageSums.Clear();
        }

        return new TableData<OutsourceVendorProfileDto>
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
            var result = await OutsourceVendorSvc.GetFilterContextsAsync();
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

        // 委外工段列显示中文（英文 key → SectionKeys 中文）
        if (_filterContextOptions.TryGetValue("SectionName", out var sectionOpts))
        {
            foreach (var opt in sectionOpts)
                opt.Display = SectionKeys.ToChinese(opt.Value) ?? opt.Value;
        }

        // 本厂、启用列显示中文（本厂→是，外协→外协供筛选用）
        if (_filterContextOptions.TryGetValue("IsWorkshop", out var wsOpts))
        {
            foreach (var opt in wsOpts)
                opt.Display = opt.Value == "True" ? "是" : "外协";
        }
        if (_filterContextOptions.TryGetValue("IsActive", out var activeOpts))
        {
            foreach (var opt in activeOpts)
                opt.Display = opt.Value == "True" ? "启用" : "停用";
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

    // ========== 分页汇总（仿供应商管理：仅对 ② 往来信息 数值列做页内合计） ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(OutsourceVendorProfileDto)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);
        foreach (var col in _visibleColumns)
        {
            if (col.GroupKey != 2) continue;
            if (!_statFieldMap.TryGetValue(col.Key, out var map)) continue;

            var weight = map.WeightField != null
                ? _pageItems.Sum(item => (decimal)(props[map.WeightField].GetValue(item) ?? 0m))
                : 0m;

            // 有金额或单数 → 组合文本（吨/万）；仅重量（本年退回）→ 只显吨
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

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("outsource-vendors", ColumnPrefsVersion, _allColumns);
    }

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        await SaveColumnPrefs();
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
        var saved = await ColumnPrefs.LoadAsync("outsource-vendors", ColumnPrefsVersion);
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
        var savedState = await PageState.LoadAsync("outsource-vendors");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "VendorCode";
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

        if (savedState != null && table != null)
            await table.ReloadServerData();

        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // ①/② 分组标题栏：与表格横向滚动联动（对齐各分组起止位置）
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#outsource-vendors-list-table");
        }
        catch { }
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#outsource-vendors-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string VendorName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public bool IsWorkshop { get; set; }
        public string ContactPerson { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? Remark { get; set; }
    }

    private void StartEdit(OutsourceVendorProfileDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            VendorName = item.VendorName,
            SectionName = item.SectionName,
            IsWorkshop = item.IsWorkshop,
            ContactPerson = item.ContactPerson ?? "",
            ContactPhone = item.ContactPhone ?? "",
            IsActive = item.IsActive,
            Remark = item.Remark
        };
    }

    private void CancelEdit(OutsourceVendorProfileDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(OutsourceVendorProfileDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        if (string.IsNullOrWhiteSpace(cache.VendorName) || string.IsNullOrWhiteSpace(cache.SectionName))
        {
            Snackbar.Add("委外单位名与委外工段不能为空", Severity.Warning);
            return;
        }
        // 本厂车间锁定冷轧拔
        if (cache.IsWorkshop && !string.Equals(cache.SectionName, SectionKeys.ColdRollDraw, StringComparison.Ordinal))
            cache.SectionName = SectionKeys.ColdRollDraw;

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateOutsourceVendorRequest
            {
                VendorName = cache.VendorName.Trim(),
                SectionName = cache.SectionName.Trim(),
                IsWorkshop = cache.IsWorkshop,
                ContactPerson = cache.ContactPerson,
                ContactPhone = cache.ContactPhone,
                IsActive = cache.IsActive,
                Remark = cache.Remark
            };

            var result = await OutsourceVendorSvc.UpdateAsync(item.Id, request);
            if (result.Success)
            {
                item.VendorName = cache.VendorName;
                item.SectionName = cache.SectionName;
                item.IsWorkshop = cache.IsWorkshop;
                item.ContactPerson = cache.ContactPerson;
                item.ContactPhone = cache.ContactPhone;
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

    private async Task DeleteItem(OutsourceVendorProfileDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除委外单位档案 \"{item.VendorName}\"（{GetSectionCn(item.SectionName)}）吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await OutsourceVendorSvc.DeleteAsync(item.Id);
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

    private static string GetSectionCn(string key) => SectionKeys.ToChinese(key) ?? key;

    /// <summary>本厂字段显示文本：属于本厂=是，外协=空（与列表/打印一致）</summary>
    private static string GetWorkshopText(bool isWorkshop) => isWorkshop ? "是" : "";

    private RenderFragment RenderCell(OutsourceVendorProfileDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing && _editCache.TryGetValue(item.Id, out var c) ? c : null;

        // ② 委外往来统计 5 列：只读三色单元格（z单/x吨/y万，蓝单/绿吨/万橙），悬停显示完整纯文本值
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
            case "VendorCode":
                builder.AddContent(0, item.VendorCode);
                break;

            case "VendorName":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.VendorName);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.VendorName = v ?? ""));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.VendorName);
                }
                break;

            case "SectionName":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudSelect<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.SectionName);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.SectionName = v ?? ""));
                    builder.AddAttribute(5, "Class", "compact-input");
                    // 本厂车间行工段锁定冷轧拔，禁止改选
                    builder.AddAttribute(6, "Disabled", cache.IsWorkshop);
                    builder.AddAttribute(6, "ChildContent", (RenderFragment)(b2 =>
                    {
                        foreach (var k in SectionKeys.All)
                        {
                            b2.OpenComponent<MudSelectItem<string>>(0);
                            b2.AddAttribute(1, "Value", k);
                            b2.AddAttribute(2, "Text", GetSectionCn(k));
                            b2.CloseComponent();
                        }
                    }));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, GetSectionCn(item.SectionName));
                }
                break;

            case "IsWorkshop":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudSwitch<bool>>(0);
                    builder.AddAttribute(1, "Value", cache.IsWorkshop);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<bool>(this, v =>
                    {
                        cache.IsWorkshop = v;
                        // 勾选本厂车间 → 工段锁定冷轧拔（厂内虚拟发外仅冷轧拔开放）
                        if (v && !string.Equals(cache.SectionName, SectionKeys.ColdRollDraw, StringComparison.Ordinal))
                            cache.SectionName = SectionKeys.ColdRollDraw;
                    }));
                    builder.AddAttribute(3, "Dense", true);
                    builder.AddAttribute(4, "Color", Color.Success);
                    builder.CloseComponent();
                }
                else
                {
                    // 本厂字段：属于本厂显示「是」，外协留空（悬停提示 本厂/外协单位）
                    builder.OpenElement(0, "span");
                    builder.AddAttribute(1, "title", item.IsWorkshop ? "本厂" : "外协单位");
                    builder.AddContent(2, item.IsWorkshop ? "是" : "");
                    builder.CloseElement();
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

    // ========== ② 委外往来统计列渲染 ==========

    /// <summary>统计 5 列三色富文本：累计·本年委外(单数+吨+万)/本年回收·委外未回收(吨+万)/本年退回(吨)；非统计列返回 null</summary>
    private static MarkupString? RenderStatMarkup(OutsourceVendorProfileDto item, string key)
    {
        if (!ResolveStat(item, key, out var withCount, out var count, out var weight, out var amount))
            return null;
        return OrderOverviewFormatter.RenderTradeMarkup(count, weight, amount, withCount);
    }

    /// <summary>统计列纯文本（同 RenderStatMarkup 数值口径，供 tooltip/打印）；非统计列返回 null</summary>
    private static string? RenderStatText(OutsourceVendorProfileDto item, string key)
    {
        if (!ResolveStat(item, key, out var withCount, out var count, out var weight, out var amount))
            return null;
        return OrderOverviewFormatter.RenderTradeText(count, weight, amount, withCount);
    }

    /// <summary>解析统计 5 列成分（委外/累计·本年 带单数；回收/未回收 吨+万；退回 仅吨）</summary>
    private static bool ResolveStat(OutsourceVendorProfileDto item, string key, out bool withCount, out int count, out decimal weight, out decimal amount)
    {
        withCount = false; count = 0; weight = 0m; amount = 0m;
        switch (key)
        {
            case "TotalOrdering": withCount = true; count = item.TotalOrderCount; weight = item.TotalWeight; amount = item.TotalAmount; return true;
            case "YearOrdering": withCount = true; count = item.YearOrderCount; weight = item.YearWeight; amount = item.YearAmount; return true;
            case "YearRecovered": weight = item.YearRecoveredWeight; amount = item.YearRecoveredAmount; return true;
            case "Pending": weight = item.PendingWeight; amount = item.PendingAmount; return true;
            case "YearReturn": weight = item.YearReturnWeight; amount = 0m; return true;
            default: return false;
        }
    }

    /// <summary>统计列页内合计文本（同 RenderStatText 数值口径，单/吨/万 取整）</summary>
    private static string BuildStatText(bool withCount, int count, decimal weightKg, decimal amountYuan)
        => OrderOverviewFormatter.RenderTradeText(count, weightKg, amountYuan, withCount);

    // ========== 打印方法（Mode A 列表打印：按当前可见列——含 ② 往来信息 全部统计列——完整打印选中行） ==========

    /// <summary>打印选中委外单位（按当前可见列渲染列表 PDF，Mode A 前端已准备数据）</summary>
    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的委外单位", Severity.Warning);
            return;
        }
        try
        {
            // 从当前页取选中行，按可见列把每格转显示文本（保证 ② 往来信息 统计列也能完整打印）
            var selectedItems = _pageItems
                .Where(v => selectedIds.Contains(v.Id))
                .Select(item =>
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var col in _visibleColumns)
                        dict[col.Key] = GetCellDisplayText(item, col) ?? "-";
                    return dict;
                }).ToList();

            var request = new OrderPrintListRequest
            {
                Title = "委外单位列表",
                Items = selectedItems,
                Columns = GetPrintColumnDefs()
            };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Navigation.BaseUri}{ApiEndpoints.OutsourceVendorProfile}/print-list-file";
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
    private static string? GetCellDisplayText(OutsourceVendorProfileDto item, ColumnDef col)
    {
        if (col.GroupKey == 2)
            return RenderStatText(item, col.Key) ?? "-";

        return col.Key switch
        {
            "VendorCode" => item.VendorCode,
            "VendorName" => item.VendorName,
            "SectionName" => GetSectionCn(item.SectionName),
            "IsWorkshop" => GetWorkshopText(item.IsWorkshop),
            "ContactPerson" => item.ContactPerson,
            "ContactPhone" => item.ContactPhone,
            "IsActive" => item.IsActive ? "启用" : "停用",
            "Remark" => item.Remark,
            _ => "-"
        };
    }

    private void NavigateToCreate() => Navigation.NavigateTo("/outsource-vendors/create");

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
        await PageState.SaveAsync("outsource-vendors", state);
    }
}
