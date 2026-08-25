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
using MES.Shared.Constants;

namespace MES.Blazor.Pages.Configuration;

public partial class Employees
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

    // ========== 检验项目多选辅助（InspectionItems 存逗号分隔枚举名串） ==========

    private readonly List<(string Key, string Text)> _inspectionItemOptions =
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

    private async Task PrintAll()
    {
        try
        {
            var request = new EmployeePrintAllRequest
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortColumn,
                IsDescending = sortDescending,
                Columns = GetPrintColumnDefs()
            };
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.Employee}/print-all-file";
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

    // 默认列顺序=用户定稿：部门 岗位 工号 姓名 生产工段 组类 过程检验 成检到料 成检项目 启用（HR 三备注默认隐藏）
    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "Department",    Label = "部门",         SortKey = "department",     FilterType = "string" },
        new() { Key = "Position",      Label = "岗位",         SortKey = "position",       FilterType = "string" },
        new() { Key = "Code",          Label = "工号",         SortKey = "code",           FilterType = "string", IsRequired = true },
        new() { Key = "Name",          Label = "姓名",         SortKey = "name",           FilterType = "string", IsRequired = true },
        new() { Key = "SectionName",   Label = "生产工段",     SortKey = "sectionname",    FilterType = "string" },
        new() { Key = "GroupName",     Label = "组类",         SortKey = "groupname",      FilterType = "string" },
        new() { Key = "ProcessInspectionItems",   Label = "过程检验",   SortKey = "processinspectionitems",   FilterType = "boolean" },
        new() { Key = "MaterialReceiveCheckItems", Label = "成检到料", SortKey = "materialreceivecheckitems", FilterType = "boolean" },
        new() { Key = "InspectionItems", Label = "成检项目",    SortKey = "inspectionitems", FilterType = "string" },
        new() { Key = "IsActive",      Label = "启用",         SortKey = "isactive",       FilterType = "boolean" },
        new() { Key = "PositionRemark", Label = "岗位备注",     SortKey = "positionremark", FilterType = "string", Visible = false },
        new() { Key = "SalaryMode",    Label = "工资结算模式", SortKey = "salarymode",     FilterType = "string", Visible = false },
        new() { Key = "SalaryRemark",  Label = "工资结算备注", SortKey = "salaryremark",   FilterType = "string", Visible = false },
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
                    "InspectionItems" => Enum.TryParse<InspectionItem>(v, out var item) ? DisplayHelper.GetInspectionItemText(item) : v,
                    "ProcessInspectionItems" => v == "True" ? "是" : "否",
                    "MaterialReceiveCheckItems" => v == "True" ? "是" : "否",
                    "IsActive" => v == "True" ? "启用" : "停用",
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
    private const string ColumnPrefsVersion = "v3";

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

    protected override async Task OnInitializedAsync()
    {
        await LoadSectionOptionsAsync();
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
        public string? SalaryMode { get; set; }
        public string? SalaryRemark { get; set; }
        public string? SectionName { get; set; }
        public string? GroupName { get; set; }
        public bool? ProcessInspectionItems { get; set; }
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
            SectionName = item.SectionName,
            GroupName = item.GroupName,
            ProcessInspectionItems = item.ProcessInspectionItems,
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
                SectionName = cache.SectionName,
                GroupName = cache.GroupName,
                ProcessInspectionItems = cache.ProcessInspectionItems,
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
