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
using MES.Core.DTOs.Shared;
using System.Text.Json;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.Orders;

public partial class Customers
{
    private MudTable<CustomerProfileDto>? table;
    private List<CustomerProfileDto> _pageItems = new();
    private int _totalCount;
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
    private string _searchKeyword = string.Empty;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _loadVersion;
    private bool _resetToFirstPage;

    private string sortColumn = "CustomerCode";
    private bool sortDescending = true;

    // ========== 双日期区间（2026-09-14；与报表总览「客户往来数据」卡同口径、同规则） ==========

    private string _signDateFrom = "";   // 接单区间-开始（yyyy-MM-dd，空=不限）
    private string _signDateTo = "";     // 接单区间-结束（yyyy-MM-dd，闭区间含当天）
    private string _shipDateFrom = "";   // 发货区间-开始（yyyy-MM-dd，空=不限）
    private string _shipDateTo = "";     // 发货区间-结束（yyyy-MM-dd，闭区间含当天）

    /// <summary>接单区间生效（任一端有值）：仅 `YearOrdering` 列按区间重算，其余列置「—」</summary>
    private bool SignRangeMode => !string.IsNullOrWhiteSpace(_signDateFrom) || !string.IsNullOrWhiteSpace(_signDateTo);

    /// <summary>发货区间生效（任一端有值）：仅 `ShippedDone`/`ShippedOther` 列按区间重算，其余列置「—」</summary>
    private bool ShipRangeMode => !string.IsNullOrWhiteSpace(_shipDateFrom) || !string.IsNullOrWhiteSpace(_shipDateTo);

    /// <summary>任一时间区间生效（两区间互相独立、可叠加）：服务端只返回「激活列有数据」的客户行</summary>
    private bool AnyRangeMode => SignRangeMode || ShipRangeMode;

    /// <summary>
    /// 当前区间模式下「有数据」的列集合；返回 null 表示未启用任何区间（8 列全部正常展示、不过滤行）。
    /// 未激活列渲染为「—」防视觉污染——该列口径为自然年/全时段/当前存量，与所选区间不同源。
    /// ⚠️ 键名与后端 `CustomerService.ActiveStatsColumns` 及本页 `YearOrdering`（带 -ing）约定一致。
    /// </summary>
    private HashSet<string>? ActiveTradeColumns()
    {
        if (!AnyRangeMode) return null;
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (SignRangeMode) set.Add("YearOrdering");
        if (ShipRangeMode) { set.Add("ShippedDone"); set.Add("ShippedOther"); }
        return set;
    }

    /// <summary>该 ② 往来信息统计列在当前区间模式下是否有数据（未启用区间时恒 true）</summary>
    private bool IsTradeColumnActive(string key) => ActiveTradeColumns()?.Contains(key) ?? true;

    /// <summary>表头动态文本：仅在该维度区间生效时改名（口径已切换，防误导）</summary>
    private string TradeColumnLabel(ColumnDef col) => col.Key switch
    {
        "YearOrdering" => SignRangeMode ? "区间接单" : "本年接单",
        "ShippedDone" => ShipRangeMode ? "区间已发货(整单)" : "本年已发货(整单)",
        "ShippedOther" => ShipRangeMode ? "区间已发货(非整单)" : "本年已发货(非整单)",
        _ => col.Label
    };

    /// <summary>区间生效时的口径提示条（三分支：仅接单 / 仅发货 / 双区间）</summary>
    private string RangeHint()
    {
        if (SignRangeMode && ShipRangeMode)
            return "接单 + 发货区间模式：「区间接单」「区间已发货(整单/非整单)」按各自所选日期区间统计；其余列置「—」；只显示所选区间内有数据的客户。";
        if (SignRangeMode)
            return "接单区间模式：仅「区间接单」按所选接单日期区间统计；其余列置「—」；只显示该区间内有接单数据的客户。";
        return "发货区间模式：仅「区间已发货(整单/非整单)」按所选发货日期区间统计；其余列置「—」；只显示该区间内有发货数据的客户。";
    }

    private static DateTime? ParseRangeDate(string text)
        => DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd",
               System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
            ? d
            : null;

    /// <summary>应用/清除区间后回到第 1 页重载（区间模式下总条数会变化，须重置页码）</summary>
    private async Task ApplyRangeAsync()
    {
        _resetToFirstPage = true;
        if (table != null) await table.ReloadServerData();
    }

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

    // 列偏好版本键：改为「① 基本信息 / ② 往来信息」两分组后递增，强制老用户按新默认重新加载（col_prefs_customers_v2）
    private const string ColumnPrefsVersion = "v2";
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    // ========== ② 往来信息组数据/页脚一律靠左（2026-09-10 同委外单位/供应商页口径，移除居中逻辑） ==========

    // ========== ② 往来信息 分组列标题栏（仿订单列表 B23） ==========
    // 选择列 40px + 可见列宽和 + 操作列 90px
    private int _totalTableWidth =>
        40 + _visibleColumns.Sum(c => int.TryParse(c.Width, out var w) ? w : 100) + 90;

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

        // 选择列占位（40px）
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

        // 操作列占位（90px）
        result.Add(new GroupHeaderInfo { GroupKey = 0, GroupName = "", TotalWidth = 90, ColumnCount = 0, CssClass = "" });

        return result;
    }

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch { 1 => "col-g1", 2 => "col-g2", 3 => "col-g3", 4 => "col-g4", _ => "" };
        if (isGroupStart && groupKey > 1) cls += " col-group-start";
        return cls;
    }

    /// <summary>
    /// ② 往来信息表头分组强调色（2026-09-14）：待发货（整单/非整单）= 暖橙、待在产（整单未入库/扣除部分入库）= 冷紫，
    /// 用于在同一「往来信息」分组内再区分两组语义（待出货 vs 在制在产）。与报表总览「客户往来数据」卡同源配色。
    /// </summary>
    private static string GetTradeAccentCss(string key) => key switch
    {
        "StockDone" or "StockOther" => " th-accent-stock",
        "WipNone" or "WipPartial" => " th-accent-wip",
        _ => ""
    };

    private static string GetCellGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch { 1 => "col-g1-cell", 2 => "col-g2-cell", 3 => "col-g3-cell", 4 => "col-g4-cell", _ => "" };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    // ========== ② 往来信息 统计列（DTO 成分字段映射，用于分页合计） ==========

    private static readonly Dictionary<string, (string? CountField, string WeightField, string AmountField)> _statFieldMap = new()
    {
        ["TotalOrdering"] = ("TotalOrderCount", "TotalOrderWeight", "TotalOrderAmount"),
        ["YearOrdering"] = ("YearOrderCount", "YearOrderWeight", "YearOrderAmount"),
        ["ShippedDone"] = ("ShippedCompletedCount", "ShippedCompletedWeight", "ShippedCompletedAmount"),
        ["ShippedOther"] = ("ShippedOtherCount", "ShippedOtherWeight", "ShippedOtherAmount"),
        ["StockDone"] = ("StockCompletedCount", "StockCompletedWeight", "StockCompletedAmount"),
        ["StockOther"] = ("StockOtherCount", "StockOtherWeight", "StockOtherAmount"),
        ["WipNone"] = ("WipNoneCount", "WipNoneWeight", "WipNoneAmount"),
        ["WipPartial"] = ("WipPartialCount", "WipPartialWeight", "WipPartialAmount"),
    };

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // ========== ① 基本信息（默认仅显示：业务员/最终用户/状态；客户编码/客户单位/联系人/电话/地址/备注默认隐藏） ==========
        new() { Key = "CustomerCode",  Label = "客户编码", SortKey = "customercode",  FilterType = "string", IsRequired = true, Width = "120", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "Salesman",      Label = "业务员",   SortKey = "salesman",      FilterType = "string", IsRequired = true, Width = "120", GroupKey = 1, GroupName = "① 基本信息" },
        new() { Key = "CustomerUnit",  Label = "客户单位", SortKey = "customerunit",  FilterType = "string", IsRequired = true, Width = "120", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "EndCustomer",   Label = "最终用户", SortKey = "endcustomer",   FilterType = "string", Width = "120", GroupKey = 1, GroupName = "① 基本信息" },
        new() { Key = "Status",        Label = "状态",     SortKey = "status",        FilterType = "enum",   EnumOptions = DisplayHelper.GetEnumFilterOptions<CustomerStatus>(), Width = "120", GroupKey = 1, GroupName = "① 基本信息" },
        new() { Key = "ContactPerson", Label = "联系人",     SortKey = "contactperson", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "ContactPhone",  Label = "联系电话",   SortKey = "contactphone",  FilterType = "string", Width = "120", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "Address",       Label = "联系地址",   SortKey = "address",       FilterType = "string", Width = "150", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        new() { Key = "Remark",        Label = "备注",       SortKey = "remark",        FilterType = "string", Width = "120", GroupKey = 1, GroupName = "① 基本信息", Visible = false },
        // ========== ② 往来信息（只读聚合数值列：重量按吨、金额按万保留 1 位小数；不可排序/筛选——SortKey=null 即只读标记） ==========
        new() { Key = "TotalOrdering", Label = "累计接单",            SortKey = null, FilterType = null, Width = "200", GroupKey = 2, GroupName = "② 往来信息" },
        new() { Key = "YearOrdering",  Label = "本年接单",            SortKey = null, FilterType = null, Width = "200", GroupKey = 2, GroupName = "② 往来信息" },
        new() { Key = "ShippedDone",   Label = "本年已发货(整单)",    SortKey = null, FilterType = null, Width = "170", GroupKey = 2, GroupName = "② 往来信息" },
        new() { Key = "ShippedOther",  Label = "本年已发货(非整单)",  SortKey = null, FilterType = null, Width = "170", GroupKey = 2, GroupName = "② 往来信息" },
        new() { Key = "StockDone",     Label = "待发货(整单)",        SortKey = null, FilterType = null, Width = "170", GroupKey = 2, GroupName = "② 往来信息" },
        new() { Key = "StockOther",    Label = "待发货(非整单)",      SortKey = null, FilterType = null, Width = "170", GroupKey = 2, GroupName = "② 往来信息" },
        new() { Key = "WipNone",       Label = "待在产(整单未入库)",  SortKey = null, FilterType = null, Width = "190", GroupKey = 2, GroupName = "② 往来信息" },
        new() { Key = "WipPartial",    Label = "待在产(扣除部分入库)", SortKey = null, FilterType = null, Width = "190", GroupKey = 2, GroupName = "② 往来信息" },
    };

    // ========== 分页汇总（仿订单列表：仅对 ② 往来信息 数值列做页内合计） ==========

    private Dictionary<string, string> _pageSums = new();

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;
        var props = typeof(CustomerProfileDto)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);
        foreach (var col in _visibleColumns)
        {
            if (col.GroupKey != 2) continue;
            if (!_statFieldMap.TryGetValue(col.Key, out var map)) continue; // 非数值列不合计
            // 区间模式下未激活列置「—」（口径与所选区间不同源，显旧值误导）
            if (!IsTradeColumnActive(col.Key)) { _pageSums[col.Key] = "—"; continue; }

            var weightProp = props[map.WeightField];
            var amountProp = props[map.AmountField];
            var weight = _pageItems.Sum(item => (decimal)(weightProp.GetValue(item) ?? 0m));
            var amount = _pageItems.Sum(item => (decimal)(amountProp.GetValue(item) ?? 0m));

            if (map.CountField != null)
            {
                var countProp = props[map.CountField];
                var count = _pageItems.Sum(item => (int)(countProp.GetValue(item) ?? 0));
                _pageSums[col.Key] = BuildStatText(true, count, weight, amount);
            }
            else
            {
                _pageSums[col.Key] = BuildStatText(false, 0, weight, amount);
            }
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        if (_pageSums.TryGetValue(col.Key, out var sum)) return sum;
        return "-";
    }

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("customers", ColumnPrefsVersion, _allColumns);
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

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "customercode";
            var filtersJson = SerializeFilters();

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = string.IsNullOrEmpty(sortBy) ? "customercode" : sortBy,
                IsDescending = sortDescending,
                // 双日期区间（互相独立、可叠加）：传了则服务端按区间重算统计并只返回区间内有数据的客户
                SignDateFrom = ParseRangeDate(_signDateFrom),
                SignDateTo = ParseRangeDate(_signDateTo),
                ShipDateFrom = ParseRangeDate(_shipDateFrom),
                ShipDateTo = ParseRangeDate(_shipDateTo)
            };
            if (filtersJson != null)
                query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson);

            var result = await CustomerService.GetPagedAsync(query);

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<CustomerProfileDto> { Items = _pageItems, TotalItems = _totalCount };

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

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("customers", ColumnPrefsVersion);
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
                    {
                        // 仅保留当前列定义中仍存在的列（兼容旧版本存储的已删列 filter，如 HasSales）
                        var validKeys = _allColumns.Select(c => c.Key).ToHashSet();
                        _columnFilters = dict
                            .Where(kv => validKeys.Contains(kv.Key))
                            .ToDictionary(kv => kv.Key, kv => new HashSet<string>(kv.Value));
                    }
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
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#customers-list-table");
        }
        catch { }
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

    private RenderFragment RenderCell(CustomerProfileDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing && _editCache.TryGetValue(item.Id, out var c) ? c : null;

        // 区间模式下未激活的统计列置「—」（口径与所选区间不同源，显旧值误导）
        if (col.GroupKey == 2 && _statFieldMap.ContainsKey(col.Key) && !IsTradeColumnActive(col.Key))
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "style", "color:#9e9e9e");
            builder.AddContent(2, "—");
            builder.CloseElement();
            return;
        }

        // 客户业务统计 8 列：只读三色单元格（z单/x吨/y万，蓝单/绿吨/万橙），悬停显示完整纯文本值
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
                        foreach (var opt in DisplayHelper.GetEnumOptions<CustomerStatus>())
                        {
                            cb.OpenComponent<MudSelectItem<string>>(0);
                            cb.AddAttribute(1, "Value", opt.Value);
                            cb.AddAttribute(2, "Text", opt.Display);
                            cb.AddAttribute(3, "ChildContent", (RenderFragment)(b => b.AddContent(0, opt.Display)));
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

    // ========== 客户业务统计列渲染 ==========

    /// <summary>统计列三色富文本（z单/x吨/y万，取整；与报表业务总况色板一致）；非统计列返回 null</summary>
    private static MarkupString? RenderStatMarkup(CustomerProfileDto item, string key)
    {
        if (!ResolveStat(item, key, out var withCount, out var count, out var weight, out var amount))
            return null;
        return OrderOverviewFormatter.RenderTradeMarkup(count, weight, amount, withCount);
    }

    /// <summary>统计列纯文本（同 RenderStatMarkup 数值口径，供 tooltip/打印）；非统计列返回 null</summary>
    private static string? RenderStatText(CustomerProfileDto item, string key)
    {
        if (!ResolveStat(item, key, out var withCount, out var count, out var weight, out var amount))
            return null;
        return OrderOverviewFormatter.RenderTradeText(count, weight, amount, withCount);
    }

    /// <summary>解析统计 8 列成分（单数/重量kg/金额元，均为带单数列）</summary>
    private static bool ResolveStat(CustomerProfileDto item, string key, out bool withCount, out int count, out decimal weight, out decimal amount)
    {
        count = 0; weight = 0m; amount = 0m;
        switch (key)
        {
            case "TotalOrdering": count = item.TotalOrderCount; weight = item.TotalOrderWeight; amount = item.TotalOrderAmount; break;
            case "YearOrdering": count = item.YearOrderCount; weight = item.YearOrderWeight; amount = item.YearOrderAmount; break;
            case "ShippedDone": count = item.ShippedCompletedCount; weight = item.ShippedCompletedWeight; amount = item.ShippedCompletedAmount; break;
            case "ShippedOther": count = item.ShippedOtherCount; weight = item.ShippedOtherWeight; amount = item.ShippedOtherAmount; break;
            case "StockDone": count = item.StockCompletedCount; weight = item.StockCompletedWeight; amount = item.StockCompletedAmount; break;
            case "StockOther": count = item.StockOtherCount; weight = item.StockOtherWeight; amount = item.StockOtherAmount; break;
            case "WipNone": count = item.WipNoneCount; weight = item.WipNoneWeight; amount = item.WipNoneAmount; break;
            case "WipPartial": count = item.WipPartialCount; weight = item.WipPartialWeight; amount = item.WipPartialAmount; break;
            default: withCount = true; return false;
        }
        withCount = true;
        return true;
    }

    /// <summary>统计列页内合计文本（同 RenderStatText 数值口径，整单/吨/万 取整）</summary>
    private static string BuildStatText(bool withCount, int count, decimal weightKg, decimal amountYuan)
        => OrderOverviewFormatter.RenderTradeText(count, weightKg, amountYuan, withCount);

    // ========== 打印方法（Mode A 列表打印：按当前可见列——含 ② 往来信息 全部统计列——完整打印选中行） ==========

    /// <summary>打印选中客户（按当前可见列渲染列表 PDF，Mode A 前端已准备数据）</summary>
    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的客户", Severity.Warning);
            return;
        }
        try
        {
            // 从当前页取选中行，按可见列把每格转显示文本（保证 ② 往来信息 统计列也能完整打印）
            var selectedItems = _pageItems
                .Where(c => selectedIds.Contains(c.Id))
                .Select(item =>
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var col in _visibleColumns)
                        dict[col.Key] = GetCellDisplayText(item, col) ?? "-";
                    return dict;
                }).ToList();

            var request = new OrderPrintListRequest
            {
                Title = "客户列表",
                Items = selectedItems,
                Columns = GetPrintColumnDefs()
            };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.Customer}/print-list-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>当前可见列 → 打印列定义（Key/Label 对应当前列显隐与顺序；区间模式下的动态表头同步带上）</summary>
    private List<PrintColumnDef> GetPrintColumnDefs() =>
        _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = TradeColumnLabel(c) }).ToList();

    /// <summary>按列取打印显示文本：① 基本信息原样，② 往来信息走统计渲染（吨/万/单），与页面单元格口径一致（含区间模式「—」）</summary>
    private string? GetCellDisplayText(CustomerProfileDto item, ColumnDef col)
    {
        if (col.GroupKey == 2)
        {
            if (_statFieldMap.ContainsKey(col.Key) && !IsTradeColumnActive(col.Key))
                return "—";
            return RenderStatText(item, col.Key) ?? "-";
        }

        return col.Key switch
        {
            "CustomerCode" => item.CustomerCode,
            "Salesman" => item.Salesman,
            "CustomerUnit" => item.CustomerUnit,
            "EndCustomer" => item.EndCustomer,
            "Status" => item.Status == CustomerStatus.Active ? "启用" : "停用",
            "ContactPerson" => item.ContactPerson,
            "ContactPhone" => item.ContactPhone,
            "Address" => item.Address,
            "Remark" => item.Remark,
            _ => "-"
        };
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
