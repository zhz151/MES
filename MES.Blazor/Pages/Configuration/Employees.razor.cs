using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Shared;
using System.Text.Json;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.Configuration;

public partial class Employees : IDisposable
{
    [Inject] private HttpClient Http { get; set; } = null!;
    private MudTable<EmployeeDto>? table;
    private List<EmployeeDto> _pageItems = new();
    private int _totalCount;
    private string _searchKeyword = string.Empty;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _loadVersion;
    private bool _resetToFirstPage;
    private int _pageSize = 10;

    // 排序状态
    private string sortColumn = "Code";
    private bool sortDescending = false;

    // ========== ExcelFilter 列头筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // 工段下拉选项（从参数表加载启用工段，失败降级为预置 26 工段）
    private List<(string Key, string Text)> _sectionOptions = new();

    // 工序组下拉选项（从工序定义加载启用工序，失败降级为预置 ProcessKeys）
    private List<(string Key, string Text)> _processOptions = new();

    // 岗位下拉选项（PositionKeys 兜底，显示中文经 DictValueDisplayHelper 配置表优先）
    private List<(string Key, string Text)> _positionOptions = BuildPositionOptions();

    // 岗位类别下拉选项（PositionCategoryKeys 兜底，显示中文经 DictValueDisplayHelper 配置表优先）
    private List<(string Key, string Text)> _positionCategoryOptions = BuildPositionCategoryOptions();

    // 工资结算模式下拉选项（枚举，显示中文经 DisplayHelper.GetEnumOptions 配置表排序优先）
    private List<(SalaryMode Value, string Display)> _salaryModeOptions = BuildSalaryModeOptions();

    // 靠工岗位下拉选项（「计件活岗」动态候选，从后端 piece-positions 拉取；显示中文经 DisplayHelper.GetPositionText）
    private List<(string Key, string Text)> _attendancePositionOptions = new();

    // 字段初始化器在组件构造时执行，此时 DictValueDisplayHelper.OverrideMap 尚未注入（MainLayout 晚于页面渲染）；
    // MainLayout 注入后经 OverrideMapChanged 事件调用 ApplyDictOverrideMapAsync 重建，使选项按参数表中文显示
    private static List<(string Key, string Text)> BuildPositionOptions() =>
        PositionKeys.All.Select(k => (k, DictValueDisplayHelper.GetText(DictValueDefaults.PositionKey, k) ?? k)).ToList();

    private static List<(string Key, string Text)> BuildPositionCategoryOptions() =>
        PositionCategoryKeys.All.Select(k => (k, DictValueDisplayHelper.GetText(DictValueDefaults.PositionCategoryKey, k) ?? k)).ToList();

    private static List<(SalaryMode Value, string Display)> BuildSalaryModeOptions() =>
        DisplayHelper.GetEnumOptions<SalaryMode>()
            .Where(o => Enum.TryParse<SalaryMode>(o.Value, out _))
            .Select(o => (Enum.Parse<SalaryMode>(o.Value), o.Display))
            .ToList();

    // ========== 工段多选辅助（SectionName 存逗号分隔英文 Key 串） ==========

    // 逗号串 → 多选列表（编辑态初值）
    private static List<string> SplitSections(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

    // 逗号串 → 中文显示（"、 " 连接）
    private static string FormatSections(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join("、", value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => SectionDisplayHelper.GetSectionNameText(s.Trim())));

    // ========== 工序组多选辅助（GroupName 存逗号分隔工序英文 Key 串） ==========

    // 逗号串 → 多选列表（编辑态初值）
    private static List<string> SplitProcesses(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

    // 逗号串 → 中文显示（"、" 连接，显示名参数化）
    private static string FormatProcesses(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join("、", value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => ProcessDisplayHelper.GetProcessNameText(s.Trim())));

    // ========== 检验项目多选辅助（InspectionItems 存逗号分隔枚举名串） ==========

    // 成检项目下拉（枚举配置表 EnumDisplayDefinition 优先 → EnumHelper 静态注册兜底）。
    // 字段初始化器在构造时枚举覆盖未注入（MainLayout 晚注入），故改为可重建，随 OverrideMapChanged 事件重建
    private List<(string Key, string Text)> _inspectionItemOptions = BuildInspectionItemOptions();

    private static List<(string Key, string Text)> BuildInspectionItemOptions() =>
        DisplayHelper.GetEnumOptions<InspectionItem>().Select(o => (o.Value, o.Display)).ToList();

    // 逗号串 → 多选列表（编辑态初值）
    private static List<string> SplitInspectionItems(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

    // 逗号串 → 中文显示（"、 " 连接）
    private static string FormatInspectionItems(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join("、", value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Enum.TryParse<InspectionItem>(x.Trim(), out var item) ? DisplayHelper.GetInspectionItemText(item) : x.Trim()));

    // ========== 靠工岗位多选辅助（AttendancePositions 存逗号分隔岗位英文 Key 串，仅靠工计件模式使用） ==========

    // 逗号串 → 多选列表（编辑态初值）
    private static List<string> SplitAttendancePositions(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

    // 逗号串 → 中文显示（"、" 连接）
    private static string FormatAttendancePositions(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join("、", value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => DisplayHelper.GetPositionText(s.Trim())));

    // ========== 选择/打印 ==========
    private HashSet<int> selectedIds = new();
    private bool allSelected => _pageItems.Count > 0 && _pageItems.All(i => selectedIds.Contains(i.Id));

    private void OnSelectAllChanged(bool v)
    {
        selectedIds = v ? new HashSet<int>(_pageItems.Select(i => i.Id)) : new();
        StateHasChanged();
    }

    private void OnRowSelectionChanged(int id, bool v)
    {
        if (v) selectedIds.Add(id); else selectedIds.Remove(id);
        StateHasChanged();
    }

    private List<PrintColumnDef> GetPrintColumnDefs() =>
        _allColumns.Where(c => c.Visible).Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();

    private async Task PrintSelected()
    {
        if (!selectedIds.Any()) { Snackbar.Add("请先选择要打印的记录", Severity.Warning); return; }
        try
        {
            var request = new EmployeePrintBatchRequest { Ids = selectedIds.ToArray(), Columns = GetPrintColumnDefs() };
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.Employee}/print-batch-file";
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, JsonSerializer.Serialize(request));
            Snackbar.Add("正在生成PDF...", Severity.Info);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    // ========== 二维码打印 ==========

    private async Task PrintQrCodes()
    {
        var items = _pageItems.Where(i => selectedIds.Contains(i.Id)).ToList();
        if (items.Count == 0) return;
        var codes = items.Select(i => i.Code).ToList();
        await JS.InvokeVoidAsync("MES.printQrCodes", codes);
    }

    private async Task PrintSingleQrCode(EmployeeDto item)
    {
        await JS.InvokeVoidAsync("MES.printQrCodes", new List<string> { item.Code });
    }

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    // 默认列顺序=用户定稿（2026-09-01）：工号 姓名 生产工段 工序组 成检到料 成检项目 启用 岗位类别 岗位 岗位备注 工资结算模式 工资结算备注（默认全显）
    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "Code",          Label = "工号",         SortKey = "code",           FilterType = "string", IsRequired = true },
        new() { Key = "Name",          Label = "姓名",         SortKey = "name",           FilterType = "string", IsRequired = true },
        new() { Key = "SectionName",   Label = "生产工段",     SortKey = "sectionname",    FilterType = "string" },
        new() { Key = "GroupName",     Label = "工序组",       SortKey = "groupname",      FilterType = "string" },
        new() { Key = "MaterialReceiveCheckItems", Label = "成检到料", SortKey = "materialreceivecheckitems", FilterType = "boolean" },
        new() { Key = "InspectionItems", Label = "成检项目",    SortKey = "inspectionitems", FilterType = "string" },
        new() { Key = "IsActive",      Label = "启用",         SortKey = "isactive",       FilterType = "boolean" },
        new() { Key = "Department",    Label = "岗位类别",     SortKey = "department",     FilterType = "string" },
        new() { Key = "Position",      Label = "岗位",         SortKey = "position",       FilterType = "string" },
        new() { Key = "PositionRemark", Label = "岗位备注",     SortKey = "positionremark", FilterType = "string" },
        new() { Key = "SalaryMode",    Label = "工资结算模式", SortKey = "salarymode",     FilterType = "string" },
        new() { Key = "AttendancePositions", Label = "靠工岗位", SortKey = "attendancepositions", FilterType = "string" },
        new() { Key = "AttendanceCoefficient", Label = "靠工系数", SortKey = "attendancecoefficient" },
        new() { Key = "HourlyWage",    Label = "小时工资",     SortKey = "hourlywage" },
        new() { Key = "DailyWage",     Label = "日工资",       SortKey = "dailywage" },
        new() { Key = "MonthlyWage",   Label = "月工资",       SortKey = "monthlywage" },
        new() { Key = "SalaryRemark",  Label = "工资结算备注", SortKey = "salaryremark",   FilterType = "string" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<EmployeeDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "code";

            // 首次加载覆盖页码
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

            // 列头 ExcelFilter 多选（in）
            var columnFiltersJson = SerializeFilters();
            if (columnFiltersJson != null)
            {
                var descriptors = JsonSerializer.Deserialize<List<FilterDescriptor>>(columnFiltersJson);
                if (descriptors is { Count: > 0 })
                    query.Filters = descriptors;
            }

            var result = await EmployeeService.GetPagedAsync(query);

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<EmployeeDto> { Items = _pageItems, TotalItems = _totalCount };

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
        return new TableData<EmployeeDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
    }

    // ========== 排序和搜索 ==========

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

    // ========== ExcelFilter 筛选 ==========

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

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await EmployeeService.GetFilterContextsAsync();
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
                    "SectionName" => SectionDisplayHelper.GetSectionNameText(v),
                    "GroupName" => FormatProcesses(v),
                    "InspectionItems" => Enum.TryParse<InspectionItem>(v, out var item) ? DisplayHelper.GetInspectionItemText(item) : v,
                    "MaterialReceiveCheckItems" => v == "True" ? "是" : "否",
                    "IsActive" => v == "True" ? "启用" : "停用",
                    "Department" => DisplayHelper.GetPositionCategoryText(v),
                    "Position" => DisplayHelper.GetPositionText(v),
                    "AttendancePositions" => DisplayHelper.GetPositionText(v),
                    "SalaryMode" => DisplayHelper.GetSalaryModeText(v),
                    _ => v
                },
                Count = 0
            }).ToList();
        }
    }

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    // 版本化列偏好 key：列顺序/默认显隐调整后，已保存过 localStorage 的用户也能看到新默认
    private const string ColumnPrefsVersion = "v5";

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("employees", ColumnPrefsVersion, _allColumns);
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

    // 加载启用工段下拉（从参数表，失败降级为预置 26 工段）
    private async Task LoadSectionOptionsAsync()
    {
        var r = await StandardWorkDayService.GetEnabledSectionsAsync();
        if (r.Success && r.Data != null)
            _sectionOptions = r.Data.Select(x => (x.SectionKey, SectionDisplayHelper.GetSectionNameText(x.SectionKey))).ToList();
        else
            _sectionOptions = SectionKeys.All.Select(k => (k, SectionDisplayHelper.GetSectionNameText(k))).ToList();
    }

    // 加载启用工序下拉（从工序定义，失败降级为预置 ProcessKeys）；显示文本统一走 GetProcessNameText 中文化，防配置表存英文 Key 时下拉直出英文
    private async Task LoadProcessOptionsAsync()
    {
        var r = await ProcessDefinitionService.GetEnabledProcessesAsync();
        if (r.Success && r.Data != null)
            _processOptions = r.Data.Select(x => (x.ProcessKey, ProcessDisplayHelper.GetProcessNameText(x.ProcessName))).ToList();
        else
            _processOptions = ProcessKeys.All.Select(k => (k, ProcessKeys.ToChinese(k) ?? k)).ToList();
    }

    // 岗位/岗位类别下拉 = 参数表「启用值」全量（含配置新增 Key、过滤停用、按 DisplayOrder 排序），
    // 与配置管理页「启用值」一致；失败降级 OverrideMap/常量类兜底。
    // 注：字段初始化器用 BuildXxx（常量类兜底）保证构造期有值，此处异步加载成功后覆盖。
    private async Task LoadDictOptionsAsync()
    {
        try
        {
            var pos = await DictValueDefinitionService.GetEnabledValuesAsync(DictValueDefaults.PositionKey);
            if (pos.Success && pos.Data != null && pos.Data.Count > 0)
                _positionOptions = pos.Data.Select(x => (x.Value, x.DisplayName)).ToList();
            else
                _positionOptions = BuildPositionOptions();
        }
        catch { _positionOptions = BuildPositionOptions(); }

        try
        {
            var cat = await DictValueDefinitionService.GetEnabledValuesAsync(DictValueDefaults.PositionCategoryKey);
            if (cat.Success && cat.Data != null && cat.Data.Count > 0)
                _positionCategoryOptions = cat.Data.Select(x => (x.Value, x.DisplayName)).ToList();
            else
                _positionCategoryOptions = BuildPositionCategoryOptions();
        }
        catch { _positionCategoryOptions = BuildPositionCategoryOptions(); }

        // 靠工岗位候选 = 「计件活岗」动态（服务端实时查询当前在册计件员工岗位）；失败保留现役选项
        try
        {
            var r = await EmployeeService.GetPiecePositionOptionsAsync();
            if (r.Success && r.Data != null)
                _attendancePositionOptions = r.Data.Select(k => (k, DisplayHelper.GetPositionText(k))).ToList();
        }
        catch { }
    }

    protected override async Task OnInitializedAsync()
    {
        // 订阅字典显示映射注入事件：MainLayout 注入 OverrideMap 晚于页面首次渲染，
        // 事件回调重建下拉/筛选选项并 StateHasChanged，使页面按参数表中文显示
        DictValueDisplayHelper.OverrideMapChanged += OnDictOverrideMapChanged;
        if (DictValueDisplayHelper.OverrideMap != null)
            await ApplyDictOverrideMapAsync(); // 已就绪直接应用（防时序漏报，内部含 LoadDictOptionsAsync）
        else
            await LoadDictOptionsAsync();

        await LoadSectionOptionsAsync();
        await LoadProcessOptionsAsync();
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("employees", ColumnPrefsVersion);
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

        // 恢复排序/搜索状态
        var savedState = await PageState.LoadAsync("employees");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "Code";
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

        // 加载筛选上下文（ExcelFilter 下拉选项）
        await LoadFilterContextsAsync();

        if (savedState != null && table != null)
            await table.ReloadServerData();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#employees-list-table"))
                _isArrowNavSetup = false;
        }
    }

    public void Dispose()
    {
        DictValueDisplayHelper.OverrideMapChanged -= OnDictOverrideMapChanged;
    }

    private void OnDictOverrideMapChanged()
    {
        _ = InvokeAsync(async () => await ApplyDictOverrideMapAsync());
    }

    /// <summary>
    /// 字典显示映射就绪后应用：重建岗位/岗位类别/结算模式下拉选项、重建筛选上下文（中文按参数表重算）、强制重渲染。
    /// 列表列走模板实时调用 DisplayHelper.GetXxxText，重渲染即用新映射。
    /// </summary>
    private async Task ApplyDictOverrideMapAsync()
    {
        await LoadDictOptionsAsync();
        _salaryModeOptions = BuildSalaryModeOptions();
        _inspectionItemOptions = BuildInspectionItemOptions();
        await LoadFilterContextsAsync();
        StateHasChanged();
    }

    // ========== 新增 ==========

    private async Task AddNew()
    {
        var hash = DateTime.Now.Ticks.GetHashCode();
        var newId = hash < 0 ? hash : -hash - 1;
        var newItem = new EmployeeDto
        {
            Id = newId,
            Code = "",
            Name = "",
            IsActive = true
        };

        if (_currentPage == 1)
        {
            _pageItems.Insert(0, newItem);
            StartEdit(newItem);
            await InvokeAsync(StateHasChanged);
        }
        else
        {
            _currentPage = 1;
            _restoredPageIndex = 0;
            _isFirstLoad = true;
            if (table != null) await table.ReloadServerData();
            Snackbar.Add("请在首页点击\"新建\"添加记录", Severity.Info);
        }
    }

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string? Position { get; set; }
        public string? PositionRemark { get; set; }
        public SalaryMode? SalaryMode { get; set; }
        public string? SalaryRemark { get; set; }
        public string? AttendancePositions { get; set; }
        public decimal? AttendanceCoefficient { get; set; } = 1.0m;
        public decimal? HourlyWage { get; set; }
        public decimal? DailyWage { get; set; }
        public decimal? MonthlyWage { get; set; }
        public string? SectionName { get; set; }
        public string? GroupName { get; set; }
        public bool? MaterialReceiveCheckItems { get; set; }
        public string? InspectionItems { get; set; }
        public bool IsActive { get; set; } = true;
    }

    private bool IsNewItem(int id) => id < 0;

    private void StartEdit(EmployeeDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            Code = item.Code,
            Name = item.Name,
            Department = item.Department,
            Position = item.Position,
            PositionRemark = item.PositionRemark,
            SalaryMode = item.SalaryMode,
            SalaryRemark = item.SalaryRemark,
            AttendancePositions = item.AttendancePositions,
            AttendanceCoefficient = item.AttendanceCoefficient,
            HourlyWage = item.HourlyWage,
            DailyWage = item.DailyWage,
            MonthlyWage = item.MonthlyWage,
            SectionName = item.SectionName,
            GroupName = item.GroupName,
            MaterialReceiveCheckItems = item.MaterialReceiveCheckItems,
            InspectionItems = item.InspectionItems,
            IsActive = item.IsActive
        };
    }

    private void CancelEdit(EmployeeDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);

        if (IsNewItem(item.Id))
        {
            _pageItems.Remove(item);
            StateHasChanged();
        }
    }

    private async Task SaveEdit(EmployeeDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.Code)) errors.Add("工号不能为空");
        if (string.IsNullOrWhiteSpace(cache.Name)) errors.Add("姓名不能为空");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var dto = new EmployeeDto
            {
                Id = IsNewItem(item.Id) ? 0 : item.Id,
                Code = cache.Code,
                Name = cache.Name,
                Department = cache.Department,
                Position = cache.Position,
                PositionRemark = cache.PositionRemark,
                SalaryMode = cache.SalaryMode,
                SalaryRemark = cache.SalaryRemark,
                AttendancePositions = cache.AttendancePositions,
                AttendanceCoefficient = cache.AttendanceCoefficient,
                HourlyWage = cache.HourlyWage,
                DailyWage = cache.DailyWage,
                MonthlyWage = cache.MonthlyWage,
                SectionName = cache.SectionName,
                GroupName = cache.GroupName,
                MaterialReceiveCheckItems = cache.MaterialReceiveCheckItems,
                InspectionItems = cache.InspectionItems,
                IsActive = cache.IsActive
            };

            var result = await EmployeeService.SaveAsync(dto);
            if (result.Success)
            {
                _editingIds.Remove(item.Id);
                _editCache.Remove(item.Id);
                Snackbar.Add("保存成功", Severity.Success);
                if (table != null) await table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "保存失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"保存失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    // ========== 删除 ==========

    private async Task DeleteItem(EmployeeDto item)
    {
        if (IsNewItem(item.Id))
        {
            _pageItems.Remove(item);
            StateHasChanged();
            return;
        }

        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除员工 \"{item.Name}({item.Code})\" 吗？",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await EmployeeService.DeleteAsync(item.Id);
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

    // ========== 一键补齐登录账号 ==========

    private bool _isSyncing;

    private async Task SyncAccounts()
    {
        var dialog = DialogService.Show<ConfirmDialog>("补齐登录账号", new DialogParameters
        {
            ["ContentText"] = "将为所有启用员工自动创建登录账号（用户名=工号、密码=123456、仅最小扫码权限），已存在的账号自动跳过。是否继续？",
            ["ConfirmText"] = "开始补齐",
            ["Color"] = Color.Primary
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        _isSyncing = true;
        StateHasChanged();
        try
        {
            var result = await EmployeeService.SyncAccountsAsync();
            if (result.Success)
                Snackbar.Add(result.Message ?? $"已补齐 {result.Data} 个登录账号", Severity.Success);
            else
                Snackbar.Add(result.Message ?? "补齐账号失败", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"补齐账号失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSyncing = false;
            StateHasChanged();
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
        await PageState.SaveAsync("employees", state);
    }
}
