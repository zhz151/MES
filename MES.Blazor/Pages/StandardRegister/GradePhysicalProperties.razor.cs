using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.StandardRegister;

public partial class GradePhysicalProperties
{
    private MudTable<GradePhysicalPropertyDto>? table;
    private List<GradePhysicalPropertyDto> _pageItems = new();
    private int _totalCount;
    private string _searchKeyword = string.Empty;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;

    // 排序状态
    private string sortColumn = "StandardGrade";
    private bool sortDescending = true;

    // ========== 选择/打印 ==========
    private HashSet<int> selectedIds = new();
    private bool allSelected => _pageItems.Count > 0 && _pageItems.All(i => selectedIds.Contains(i.Id));

    private List<PrintColumnDef> GetPrintColumnDefs() =>
        _allColumns.Where(c => c.Visible).Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();

    private async Task PrintSelected()
    {
        if (!selectedIds.Any()) { Snackbar.Add("请先选择要打印的记录", Severity.Warning); return; }
        try
        {
            var request = new GradePhysicalPropertyPrintBatchRequest { Ids = selectedIds.ToArray(), Columns = GetPrintColumnDefs() };
            var apiUrl = $"{Http.BaseAddress}api/grade-physical-property/print-batch-file";
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, JsonSerializer.Serialize(request));
            Snackbar.Add("正在生成PDF...", Severity.Info);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    private async Task PrintAll()
    {
        try
        {
            var request = new GradePhysicalPropertyPrintAllRequest
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortColumn,
                IsDescending = sortDescending,
                Columns = GetPrintColumnDefs()
            };
            var apiUrl = $"{Http.BaseAddress}api/grade-physical-property/print-all-file";
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, JsonSerializer.Serialize(request));
            Snackbar.Add("正在生成PDF...", Severity.Info);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "StandardGrade",        Label = "标准牌号",            SortKey = "standardgrade",          FilterType = "string", IsRequired = true },
        new() { Key = "StandardGradeCategory",Label = "标准牌号类别",        SortKey = "standardgradecategory",  FilterType = "string" },
        new() { Key = "Density",              Label = "密度(g/cm³)",         SortKey = "density",                FilterType = "string", IsRequired = true },
        new() { Key = "HeatTreatmentTemp",    Label = "热处理温度",          SortKey = "heattreatmenttemp",      FilterType = "string" },
        new() { Key = "HardnessRockwell",     Label = "硬度(HRC/HRB)",       SortKey = "hardnessrockwell",       FilterType = "string" },
        new() { Key = "HardnessVickers",      Label = "硬度(HV)",            SortKey = "hardnessvickers",        FilterType = "string" },
        new() { Key = "HardnessBrinell",      Label = "硬度(HB)",            SortKey = "hardnessbrinell",        FilterType = "string" },
        new() { Key = "TensileStrength",      Label = "抗拉强度(Rm)",        SortKey = "tensilestrength",        FilterType = "string" },
        new() { Key = "YieldStrength02",      Label = "屈服强度(Rp0.2)",     SortKey = "yieldstrength02",        FilterType = "string" },
        new() { Key = "YieldStrength10",      Label = "屈服强度(Rp1.0)",     SortKey = "yieldstrength10",        FilterType = "string" },
        new() { Key = "Elongation",           Label = "延伸率(A%)",          SortKey = "elongation",             FilterType = "string" },
        new() { Key = "GrainSize",            Label = "晶粒度",              SortKey = "grainsize",              FilterType = "string" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<GradePhysicalPropertyDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "standardgrade";
            var filters = SerializeFilters();

            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending,
                Filters = filters
            };

            var result = await GradePhysicalPropertyService.GetPagedAsync(query);

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

        return new TableData<GradePhysicalPropertyDto>
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
            var result = await GradePhysicalPropertyService.GetFilterContextsAsync();
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
        await ColumnPrefs.SaveAsync("grade_physical_properties", null, _allColumns);
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
        var saved = await ColumnPrefs.LoadAsync("grade_physical_properties", null);
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

        var savedState = await PageState.LoadAsync("grade_physical_properties");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "StandardGrade";
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
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#grade-physical-properties-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 单元格渲染 ==========

    private string? GetCellRawValue(GradePhysicalPropertyDto item, string key) => key switch
    {
        "StandardGrade" => item.StandardGrade,
        "StandardGradeCategory" => item.StandardGradeCategory,
        "Density" => item.Density.ToString("G29"),
        "HeatTreatmentTemp" => item.HeatTreatmentTemp,
        "HardnessRockwell" => item.HardnessRockwell,
        "HardnessVickers" => item.HardnessVickers,
        "HardnessBrinell" => item.HardnessBrinell,
        "TensileStrength" => item.TensileStrength,
        "YieldStrength02" => item.YieldStrength02,
        "YieldStrength10" => item.YieldStrength10,
        "Elongation" => item.Elongation,
        "GrainSize" => item.GrainSize,
        _ => null
    };

    private void NavigateToCreate() => Navigation.NavigateTo("/grade-physical-properties/create");

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string StandardGrade { get; set; } = string.Empty;
        public string? StandardGradeCategory { get; set; }
        public decimal? Density { get; set; }
        public string? HeatTreatmentTemp { get; set; }
        public string? HardnessRockwell { get; set; }
        public string? HardnessVickers { get; set; }
        public string? HardnessBrinell { get; set; }
        public string? TensileStrength { get; set; }
        public string? YieldStrength02 { get; set; }
        public string? YieldStrength10 { get; set; }
        public string? Elongation { get; set; }
        public string? GrainSize { get; set; }
    }

    private void StartEdit(GradePhysicalPropertyDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            StandardGrade = item.StandardGrade,
            StandardGradeCategory = item.StandardGradeCategory,
            Density = item.Density,
            HeatTreatmentTemp = item.HeatTreatmentTemp,
            HardnessRockwell = item.HardnessRockwell,
            HardnessVickers = item.HardnessVickers,
            HardnessBrinell = item.HardnessBrinell,
            TensileStrength = item.TensileStrength,
            YieldStrength02 = item.YieldStrength02,
            YieldStrength10 = item.YieldStrength10,
            Elongation = item.Elongation,
            GrainSize = item.GrainSize
        };
    }

    private void CancelEdit(GradePhysicalPropertyDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(GradePhysicalPropertyDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.StandardGrade)) errors.Add("标准牌号不能为空");
        if (cache.Density == null || cache.Density <= 0) errors.Add("密度必须大于0");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateGradePhysicalPropertyRequest
            {
                StandardGrade = cache.StandardGrade,
                StandardGradeCategory = cache.StandardGradeCategory,
                Density = cache.Density,
                HeatTreatmentTemp = cache.HeatTreatmentTemp,
                HardnessRockwell = cache.HardnessRockwell,
                HardnessVickers = cache.HardnessVickers,
                HardnessBrinell = cache.HardnessBrinell,
                TensileStrength = cache.TensileStrength,
                YieldStrength02 = cache.YieldStrength02,
                YieldStrength10 = cache.YieldStrength10,
                Elongation = cache.Elongation,
                GrainSize = cache.GrainSize
            };

            var result = await GradePhysicalPropertyService.UpdateAsync(item.Id, request);
            if (result.Success)
            {
                item.StandardGrade = cache.StandardGrade;
                item.StandardGradeCategory = cache.StandardGradeCategory;
                if (cache.Density.HasValue) item.Density = cache.Density.Value;
                item.HeatTreatmentTemp = cache.HeatTreatmentTemp;
                item.HardnessRockwell = cache.HardnessRockwell;
                item.HardnessVickers = cache.HardnessVickers;
                item.HardnessBrinell = cache.HardnessBrinell;
                item.TensileStrength = cache.TensileStrength;
                item.YieldStrength02 = cache.YieldStrength02;
                item.YieldStrength10 = cache.YieldStrength10;
                item.Elongation = cache.Elongation;
                item.GrainSize = cache.GrainSize;

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

    private async Task DeleteItem(GradePhysicalPropertyDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除牌号物理性能 \"{item.StandardGrade}\" 吗？",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await GradePhysicalPropertyService.DeleteAsync(item.Id);
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

    private RenderFragment RenderCell(GradePhysicalPropertyDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing && _editCache.TryGetValue(item.Id, out var c) ? c : null;

        switch (col.Key)
        {
            case "StandardGrade":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.StandardGrade);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.StandardGrade = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.StandardGrade);
                }
                break;
            case "StandardGradeCategory":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.StandardGradeCategory);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.StandardGradeCategory = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.StandardGradeCategory);
                }
                break;
            case "Density":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<decimal?>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "HideSpinButtons", true);
                    builder.AddAttribute(4, "Format", "G29");
                    builder.AddAttribute(5, "Value", cache.Density);
                    builder.AddAttribute(6, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, v => cache.Density = v));
                    builder.AddAttribute(7, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Density.ToString("G29"));
                }
                break;
            case "HeatTreatmentTemp":
            case "HardnessRockwell":
            case "HardnessVickers":
            case "HardnessBrinell":
            case "TensileStrength":
            case "YieldStrength02":
            case "YieldStrength10":
            case "Elongation":
            case "GrainSize":
                RenderTextFieldCell(item, col.Key, cache)(builder);
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    private RenderFragment RenderTextFieldCell(GradePhysicalPropertyDto item, string key, EditCache? cache) => builder =>
    {
        var val = key switch
        {
            "HeatTreatmentTemp" => item.HeatTreatmentTemp,
            "HardnessRockwell" => item.HardnessRockwell,
            "HardnessVickers" => item.HardnessVickers,
            "HardnessBrinell" => item.HardnessBrinell,
            "TensileStrength" => item.TensileStrength,
            "YieldStrength02" => item.YieldStrength02,
            "YieldStrength10" => item.YieldStrength10,
            "Elongation" => item.Elongation,
            "GrainSize" => item.GrainSize,
            _ => null
        };
        var cacheVal = key switch
        {
            "HeatTreatmentTemp" => cache?.HeatTreatmentTemp,
            "HardnessRockwell" => cache?.HardnessRockwell,
            "HardnessVickers" => cache?.HardnessVickers,
            "HardnessBrinell" => cache?.HardnessBrinell,
            "TensileStrength" => cache?.TensileStrength,
            "YieldStrength02" => cache?.YieldStrength02,
            "YieldStrength10" => cache?.YieldStrength10,
            "Elongation" => cache?.Elongation,
            "GrainSize" => cache?.GrainSize,
            _ => null
        };

        if (cache != null)
        {
            builder.OpenComponent<MudTextField<string>>(0);
            builder.AddAttribute(1, "Dense", true);
            builder.AddAttribute(2, "Variant", Variant.Outlined);
            builder.AddAttribute(3, "Value", cacheVal);
            var capturedKey = key;
            builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v =>
            {
                switch (capturedKey)
                {
                    case "HeatTreatmentTemp": cache!.HeatTreatmentTemp = v; break;
                    case "HardnessRockwell": cache!.HardnessRockwell = v; break;
                    case "HardnessVickers": cache!.HardnessVickers = v; break;
                    case "HardnessBrinell": cache!.HardnessBrinell = v; break;
                    case "TensileStrength": cache!.TensileStrength = v; break;
                    case "YieldStrength02": cache!.YieldStrength02 = v; break;
                    case "YieldStrength10": cache!.YieldStrength10 = v; break;
                    case "Elongation": cache!.Elongation = v; break;
                    case "GrainSize": cache!.GrainSize = v; break;
                }
            }));
            builder.AddAttribute(5, "Class", "compact-input");
            builder.CloseComponent();
        }
        else
        {
            builder.AddContent(0, val);
        }
    };

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
        await PageState.SaveAsync("grade_physical_properties", state);
    }
}
