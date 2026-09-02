using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using System.Text.Json;
using MES.Shared.Constants;
using MES.Core.Helpers;

namespace MES.Blazor.Pages.Batches;

public partial class PicklingOutRecords
{
    private MudTable<PicklingOutRecordDto>? table;
    private List<PicklingOutRecordDto> _pageItems = new();
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
    private int _currentPageIndex;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private int _loadVersion;
    private bool _resetToFirstPage;

    [Inject] private EmployeeService EmployeeService { get; set; } = null!;

    // 操作人员工下拉（全量启用员工，实名制）
    private List<EmployeeDto> _allEmployees = new();
    private Dictionary<string, EmployeeDto> _empByDisplay = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>按「姓名(工号)」或纯姓名反查员工；null 安全（Dictionary 键为 null 会抛 ArgumentNullException）</summary>
    private EmployeeDto? ResolveEmp(string? key)
        => key != null && _empByDisplay.TryGetValue(key, out var emp) ? emp : null;

    /// <summary>加载全量启用员工（不做工段过滤，避免过度收窄阻断录入）；登记「姓名(工号)」与纯姓名两种回显键</summary>
    private async Task LoadOperatorsAsync()
    {
        var resp = await EmployeeService.GetBySectionAsync(null);
        _allEmployees = resp.Success && resp.Data != null ? resp.Data : new();
        _empByDisplay = new Dictionary<string, EmployeeDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in _allEmployees)
        {
            if (!string.IsNullOrWhiteSpace(e.Name))
            {
                _empByDisplay[$"{e.Name}({e.Code})"] = e;
                if (!_empByDisplay.ContainsKey(e.Name))
                    _empByDisplay[e.Name] = e;
            }
        }
    }

    /// <summary>操作人下拉选项：按当前行「工段 + 工序组」双条件过滤；员工工序组空=通配；双条件无候选回退仅工段（防阻断）</summary>
    private List<EmployeeDto> GetRowOperatorOptions(string? sectionName, string? processName)
    {
        var bySection = string.IsNullOrWhiteSpace(sectionName)
            ? _allEmployees
            : _allEmployees.Where(e => OperatorMatchHelper.MatchesSection(e, sectionName)).ToList();
        if (string.IsNullOrWhiteSpace(processName))
            return bySection;
        var byBoth = bySection.Where(e => OperatorMatchHelper.MatchesProcessGroup(e, processName)).ToList();
        return byBoth.Count > 0 ? byBoth : bySection;
    }

    /// <summary>把「姓名(工号)、姓名(工号)」字符串反查为员工列表（多人，后端以「、」分隔）</summary>
    private List<EmployeeDto> ParseOperators(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new();
        var list = new List<EmployeeDto>();
        foreach (var seg in OperatorNameHelper.Split(text))
        {
            var emp = ResolveEmp(seg);
            if (emp != null && !list.Contains(emp)) list.Add(emp);
        }
        return list;
    }

    /// <summary>员工多选列表 → 「姓名(工号)、」分隔字符串（与后端 OperatorNameHelper.Split 约定一致）</summary>
    private string? FormatOperators(IEnumerable<EmployeeDto> list)
    {
        var arr = list.Where(e => e != null)
            .Select(e => $"{e.Name}({e.Code})")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return arr.Count == 0 ? null : string.Join("、", arr);
    }

    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;

    private string sortColumn = "completedate";
    private bool sortDescending = true;

    // ========== 内联编辑 ==========
    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();

    private class EditCache
    {
        public string? CompleteDateStr { get; set; }
        public string? Remark { get; set; }
        public string? Operator { get; set; }
        public ShiftType? Shift { get; set; }

        public string? OriginalCompleteDateStr { get; set; }
        public string? OriginalRemark { get; set; }
        public string? OriginalOperator { get; set; }
        public ShiftType? OriginalShift { get; set; }
    }

    // ========== 分页汇总 ==========
    private Dictionary<string, string> _pageSums = new();

    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "Quantity",
        "Weight"
    };

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private const string StorageKey = "pickling-out-records";

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // G1: 入缸信息
        new() { Key = "BatchNo",           Label = "批次号",       SortKey = "batchno",             FilterType = "string", Width = "120", GroupKey = 1, GroupName = "入缸信息" },
        new() { Key = "TagNo",             Label = "挂牌号",       SortKey = "tagno",               FilterType = "string", Width = "100", GroupKey = 1, GroupName = "入缸信息" },
        new() { Key = "ProcessName",       Label = "工序名称",     SortKey = "processname",         FilterType = "string", Width = "100", GroupKey = 1, GroupName = "入缸信息" },
        new() { Key = "SectionName",       Label = "工段名称",     SortKey = "sectionname",         FilterType = "string", Width = "100", GroupKey = 1, GroupName = "入缸信息" },
        new() { Key = "PlantGrade",        Label = "工厂牌号",     SortKey = "plantgrade",          FilterType = "string", Width = "120", GroupKey = 1, GroupName = "入缸信息" },
        new() { Key = "ManufacturingSpec", Label = "制造规格",     SortKey = "manufacturingspec",   FilterType = "string", Width = "120", GroupKey = 1, GroupName = "入缸信息" },
        new() { Key = "EquipmentName",     Label = "设备名称",     SortKey = "equipmentname",       FilterType = "string", Width = "100", GroupKey = 1, GroupName = "入缸信息" },
        new() { Key = "Quantity",          Label = "加工支数",     SortKey = "quantity",                                       Width = "80",  GroupKey = 1, GroupName = "入缸信息" },
        new() { Key = "Weight",            Label = "加工重量(kg)", SortKey = "weight",                                         Width = "80",  GroupKey = 1, GroupName = "入缸信息" },
        new() { Key = "ProductStatus",     Label = "产类",         SortKey = "productstatus",       FilterType = "string", Width = "80",  GroupKey = 1, GroupName = "入缸信息" },
        // G2: 完工信息
        new() { Key = "CompleteDate",      Label = "完工日期",     SortKey = "completedate",        FilterType = "date",   Width = "120", GroupKey = 2, GroupName = "完工信息" },
        new() { Key = "Shift",             Label = "班次",         SortKey = "shift",               FilterType = "enum",   Width = "80",  GroupKey = 2, GroupName = "完工信息",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<ShiftType>() },
        new() { Key = "Operator",          Label = "操作人",       SortKey = "operator",            FilterType = "string", Width = "160",  GroupKey = 2, GroupName = "完工信息" },
        new() { Key = "Remark",            Label = "备注",         SortKey = "remark",              FilterType = "string", Width = "120", GroupKey = 2, GroupName = "完工信息" },
        new() { Key = "DataSource",        Label = "数据来源",     SortKey = "datasource",          FilterType = "enum",   Width = "80",  GroupKey = 2, GroupName = "完工信息",
            EnumOptions = DisplayHelper.GetDataSourceOptions() },
        new() { Key = "UpdatedTime",       Label = "更新时间",     SortKey = "updatedtime",                                 Width = "120", GroupKey = 2, GroupName = "完工信息" },
    };

    // ========== 分页汇总计算 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(PicklingOutRecordDto)
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
        if (_pageSums.TryGetValue(col.Key, out var sum))
            return sum;
        return "-";
    }

    // ========== 内联编辑 ==========

    private void StartEdit(PicklingOutRecordDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new EditCache
        {
            CompleteDateStr = item.CompleteDate.ToString("yyyy-MM-dd"),
            Remark = item.Remark,
            Operator = item.Operator,
            Shift = item.Shift,
            OriginalCompleteDateStr = item.CompleteDate.ToString("yyyy-MM-dd"),
            OriginalRemark = item.Remark,
            OriginalOperator = item.Operator,
            OriginalShift = item.Shift
        };
    }

    private async Task SaveEdit(PicklingOutRecordDto item)
    {
        var cache = _editCache.GetValueOrDefault(item.Id);
        if (cache == null) return;

        DateTime? completeDate = null;
        if (!string.IsNullOrWhiteSpace(cache.CompleteDateStr))
        {
            if (!DateTime.TryParse(cache.CompleteDateStr, out var dt))
            {
                Snackbar.Add("完工日期格式无效，请使用 yyyy-MM-dd 格式", Severity.Warning);
                return;
            }
            completeDate = dt;
        }

        var request = new UpdatePicklingOutRecordRequest
        {
            CompleteDate = completeDate,
            Remark = cache.Remark,
            Operator = cache.Operator,
            Shift = cache.Shift
        };

        var result = await PicklingService.UpdateOutRecordAsync(item.Id, request);
        if (result.Success)
        {
            Snackbar.Add("保存成功", Severity.Success);
            _editingIds.Remove(item.Id);
            _editCache.Remove(item.Id);
            if (table != null) await table.ReloadServerData();
            await LoadFilterContextsAsync();
        }
        else
        {
            Snackbar.Add($"保存失败: {result.Message}", Severity.Error);
        }
    }

    private void CancelEdit(PicklingOutRecordDto item)
    {
        var cache = _editCache.GetValueOrDefault(item.Id);
        if (cache != null)
        {
            item.Operator = cache.OriginalOperator;
            item.Shift = cache.OriginalShift;
        }
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<PicklingOutRecordDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
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

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "completedate";
            var filtersJson = SerializeFilters();

            var result = await PicklingService.GetOutRecordsPagedAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
                completeDateFrom: DateTime.TryParse(_dateFrom, out var df) ? df : null,
                completeDateTo: DateTime.TryParse(_dateTo, out var dt) ? dt : null,
                filters: filtersJson
            );

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<PicklingOutRecordDto> { Items = _pageItems, TotalItems = _totalCount };

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPageIndex = result.Data.PageIndex;
                ComputePageSums();
            }
            else
            {
                _pageItems = new();
                _totalCount = 0;
            }

            await SavePageStateAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
        }

        return new TableData<PicklingOutRecordDto>
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
            var result = await PicklingService.GetOutRecordFilterContextsAsync();
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
                Display = kvp.Key switch
                {
                    "SectionName" or "CurrentSectionName" or "NextSectionName" or "PendingSectionName" => SectionDisplayHelper.GetSectionNameText(v),
                    "ProcessName" or "ProcessGroupName" or "CurrentGroupName" or "NextProcess" => ProcessDisplayHelper.GetProcessNameText(v),
                    "ProductStatus" => DisplayHelper.GetProductStatusText(v),
                    _ => v
                },
                Count = 0
            }).ToList();
        }

        // 补充枚举列筛选选项
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
        selectedIds.Clear();
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

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync(StorageKey, null, _allColumns);
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
        await LoadOperatorsAsync();
        _allColumns = GetAllColumnDefs();

        var saved = await ColumnPrefs.LoadAsync(StorageKey, null);
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
        var savedState = await PageState.LoadAsync("picklingoutrecords");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "completedate";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = savedState.PageIndex;
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
            if (savedState.Extras?.TryGetValue("dateFrom", out var dateFrom) == true)
                _dateFrom = dateFrom ?? string.Empty;
            if (savedState.Extras?.TryGetValue("dateTo", out var dateTo) == true)
                _dateTo = dateTo ?? string.Empty;
        }

        if (savedState != null && table != null)
            await table.ReloadServerData();

        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#pickling-out-records-list-table");
        }
        catch { }

        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#pickling-out-records-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(PicklingOutRecordDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "BatchNo":
                builder.AddContent(0, item.BatchNo ?? "");
                break;
            case "ProcessName":
                builder.AddContent(0, ProcessDisplayHelper.GetProcessNameText(item.ProcessName));
                break;
            case "SectionName":
                builder.AddContent(0, SectionDisplayHelper.GetSectionNameText(item.SectionName ?? ""));
                break;
            case "ManufacturingSpec":
                builder.AddContent(0, item.ManufacturingSpec ?? "");
                break;
            case "CompleteDate":
                if (_editingIds.Contains(item.Id))
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", _editCache[item.Id].CompleteDateStr);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => _editCache[item.Id].CompleteDateStr = v ?? ""));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "Placeholder", "yyyy-MM-dd");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.CompleteDate.ToString("yyyy-MM-dd"));
                }
                break;
            case "EquipmentName":
                builder.AddContent(0, item.EquipmentName ?? "");
                break;
            case "Operator":
                if (_editingIds.Contains(item.Id))
                {
                    // 操作工多选：MudSelect<EmployeeDto> MultiSelection（checkbox 一次勾选多人）。
                    // ⚠️ MudBlazor 6.19.1 MudAutocomplete 不支持 MultiSelection（误传会 unboxing 崩溃），
                    // 故用 MudSelect 原生多选 + PopoverClass 宽下拉；显示只显姓名，存储仍「姓名(工号)」。
                    builder.OpenComponent<MudSelect<EmployeeDto>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Size", Size.Small);
                    builder.AddAttribute(4, "MultiSelection", true);
                    builder.AddAttribute(5, "SelectedValues", ParseOperators(_editCache[item.Id].Operator));
                    builder.AddAttribute(6, "SelectedValuesChanged", EventCallback.Factory.Create<IEnumerable<EmployeeDto>>(this, list => _editCache[item.Id].Operator = FormatOperators(list)));
                    builder.AddAttribute(7, "ToStringFunc", (Func<EmployeeDto, string>)(e => e?.Name ?? ""));
                    builder.AddAttribute(8, "PopoverClass", "operator-select-popover");
                    builder.AddAttribute(9, "Class", "compact-input operator-input");
                    builder.AddAttribute(10, "Placeholder", "选择操作工");
                    builder.AddAttribute(11, "MultiSelectionDelimiter", "、");
                    builder.AddAttribute(12, "ChildContent", (RenderFragment)(b =>
                    {
                        foreach (var opt in GetRowOperatorOptions(item.SectionName, item.ProcessName))
                        {
                            b.OpenComponent<MudSelectItem<EmployeeDto>>(0);
                            b.AddAttribute(1, "Value", opt);
                            b.AddAttribute(2, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, opt.Name)));
                            b.CloseComponent();
                        }
                    }));
                    builder.CloseComponent();
                }
                else
                {
                    // 非编辑态只显纯姓名（去掉工号）
                    builder.AddContent(0, OperatorNameHelper.ToNamesOnly(item.Operator));
                }
                break;
            case "Shift":
                if (_editingIds.Contains(item.Id))
                {
                    builder.OpenComponent<MudSelect<ShiftType>>(0);
                    builder.AddAttribute(1, "Value", _editCache[item.Id].Shift ?? default(ShiftType));
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<ShiftType>(this, v => _editCache[item.Id].Shift = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
                    {
                        foreach (var opt in DisplayHelper.GetEnumOptions<ShiftType>())
                        {
                            b.OpenComponent<MudSelectItem<ShiftType>>(0);
                            b.AddAttribute(1, "Value", Enum.Parse<ShiftType>(opt.Value));
                            b.AddAttribute(2, "Text", opt.Display);
                            b.AddAttribute(3, "ChildContent", (RenderFragment)(b2 =>
                                b2.AddContent(0, opt.Display)));
                            b.CloseComponent();
                        }
                    }));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.GetShiftTypeText(item.Shift));
                }
                break;
            case "Quantity":
                builder.AddContent(0, DisplayHelper.FormatNullableInt(item.Quantity));
                break;
            case "Weight":
                builder.AddContent(0, $"{(int)(item.Weight ?? 0)}");
                break;
            case "ProductStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetProductStatusColor(item.ProductStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetProductStatusText(item.ProductStatus))));
                builder.CloseComponent();
                break;
            case "TagNo":
                builder.AddContent(0, item.TagNo ?? "");
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade ?? "");
                break;
            case "Remark":
                if (_editingIds.Contains(item.Id))
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", _editCache[item.Id].Remark);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => _editCache[item.Id].Remark = v ?? ""));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Remark ?? "");
                }
                break;
            case "DataSource":
                var dsText = item.DataSource switch
                {
                    "SCAN" => "扫码",
                    "MANUAL" => "手动",
                    _ => item.DataSource ?? ""
                };
                builder.AddContent(0, dsText);
                break;
            case "UpdatedTime":
                builder.AddContent(0, item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    // ========== 打印 ==========

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的记录", Severity.Warning);
            return;
        }

        var columns = _visibleColumns
            .Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label })
            .ToList();

        var request = new PicklingOutRecordPrintBatchRequest { Ids = selectedIds.ToArray(), Columns = columns };
        var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.Pickling}/out-records/print-selected-file";
        var json = JsonSerializer.Serialize(request);
        Snackbar.Add("正在生成PDF...", Severity.Info);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }

    // ========== 分组渲染 ==========

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
            if (lastKey.HasValue && gk != lastKey.Value)
            {
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

        // 操作列占位（90px）
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0,
            GroupName = "",
            TotalWidth = 90,
            ColumnCount = 0,
            CssClass = ""
        });

        return result;
    }

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1",
            2 => "col-g2",
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
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    // ========== 删除 ==========

    private async Task DeleteItem(PicklingOutRecordDto item)
    {
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("确认删除", new DialogParameters
        {
            ["ContentText"] = $"确定要删除 {item.BatchNo} 在 {item.CompleteDate:yyyy-MM-dd} 的完工记录吗？",
            ["ConfirmText"] = "删除"
        });

        var result = await dialog.Result;
        if (result.Canceled) return;

        var response = await PicklingService.DeleteOutRecordAsync(item.Id);
        if (response.Success)
        {
            Snackbar.Add("删除成功，入缸状态已恢复为浸泡中", Severity.Success);
            if (table != null) await table.ReloadServerData();
            await LoadFilterContextsAsync();
        }
        else
        {
            Snackbar.Add(response.Message, Severity.Error);
        }
    }

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
            PageIndex = _currentPageIndex,
            Extras = extras
        };
        await PageState.SaveAsync("picklingoutrecords", state);
    }
}
