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
using MES.Core.DTOs.ProductionStandard;
using System.Text.Json;

namespace MES.Blazor.Pages.ProductionStandard;

public partial class GradeChemicalCompositions
{
    private MudTable<GradeChemicalCompositionDto>? table;
    private List<GradeChemicalCompositionDto> _pageItems = new();
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

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "StandardGrade",        Label = "标准牌号",      SortKey = "standardgrade",          FilterType = "string", IsRequired = true },
        new() { Key = "StandardGradeCategory",Label = "标准牌号类别",  SortKey = "standardgradecategory",  FilterType = "string" },
        new() { Key = "Carbon",               Label = "C",             SortKey = "carbon",                 FilterType = "string" },
        new() { Key = "Silicon",              Label = "Si",            SortKey = "silicon",                FilterType = "string" },
        new() { Key = "Manganese",            Label = "Mn",            SortKey = "manganese",              FilterType = "string" },
        new() { Key = "Phosphorus",           Label = "P",             SortKey = "phosphorus",             FilterType = "string" },
        new() { Key = "Sulfur",               Label = "S",             SortKey = "sulfur",                 FilterType = "string" },
        new() { Key = "Nickel",               Label = "Ni",            SortKey = "nickel",                 FilterType = "string" },
        new() { Key = "Chromium",             Label = "Cr",            SortKey = "chromium",               FilterType = "string" },
        new() { Key = "Molybdenum",           Label = "Mo",            SortKey = "molybdenum",             FilterType = "string" },
        new() { Key = "Copper",               Label = "Cu",            SortKey = "copper",                 FilterType = "string" },
        new() { Key = "Nitrogen",             Label = "N",             SortKey = "nitrogen",               FilterType = "string" },
        new() { Key = "Niobium",              Label = "Nb",            SortKey = "niobium",                FilterType = "string" },
        new() { Key = "Titanium",             Label = "Ti",            SortKey = "titanium",               FilterType = "string" },
        new() { Key = "Iron",                 Label = "Fe",            SortKey = "iron",                   FilterType = "string" },
        new() { Key = "Aluminum",             Label = "Al",            SortKey = "aluminum",               FilterType = "string" },
        new() { Key = "Tungsten",             Label = "W",             SortKey = "tungsten",               FilterType = "string" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<GradeChemicalCompositionDto>> LoadDataFromServer(TableState state)
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

            var result = await GradeChemicalCompositionService.GetPagedAsync(query);

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

        return new TableData<GradeChemicalCompositionDto>
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
            var result = await GradeChemicalCompositionService.GetFilterContextsAsync();
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
        await ColumnPrefs.SaveAsync("grade_chemical_compositions", null, _allColumns);
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
        var saved = await ColumnPrefs.LoadAsync("grade_chemical_compositions", null);
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

        var savedState = await PageState.LoadAsync("grade_chemical_compositions");
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
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#grade-chemical-compositions-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 单元格渲染 ==========

    private string? GetCellRawValue(GradeChemicalCompositionDto item, string key) => key switch
    {
        "StandardGrade" => item.StandardGrade,
        "StandardGradeCategory" => item.StandardGradeCategory,
        "Carbon" => item.Carbon,
        "Silicon" => item.Silicon,
        "Manganese" => item.Manganese,
        "Phosphorus" => item.Phosphorus,
        "Sulfur" => item.Sulfur,
        "Nickel" => item.Nickel,
        "Chromium" => item.Chromium,
        "Molybdenum" => item.Molybdenum,
        "Copper" => item.Copper,
        "Nitrogen" => item.Nitrogen,
        "Niobium" => item.Niobium,
        "Titanium" => item.Titanium,
        "Iron" => item.Iron,
        "Aluminum" => item.Aluminum,
        "Tungsten" => item.Tungsten,
        _ => null
    };

    private void NavigateToCreate() => Navigation.NavigateTo("/grade-chemical-compositions/create");

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string StandardGrade { get; set; } = string.Empty;
        public string? StandardGradeCategory { get; set; }
        public string? Carbon { get; set; }
        public string? Silicon { get; set; }
        public string? Manganese { get; set; }
        public string? Phosphorus { get; set; }
        public string? Sulfur { get; set; }
        public string? Nickel { get; set; }
        public string? Chromium { get; set; }
        public string? Molybdenum { get; set; }
        public string? Copper { get; set; }
        public string? Nitrogen { get; set; }
        public string? Niobium { get; set; }
        public string? Titanium { get; set; }
        public string? Iron { get; set; }
        public string? Aluminum { get; set; }
        public string? Tungsten { get; set; }
    }

    private void StartEdit(GradeChemicalCompositionDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            StandardGrade = item.StandardGrade,
            StandardGradeCategory = item.StandardGradeCategory,
            Carbon = item.Carbon,
            Silicon = item.Silicon,
            Manganese = item.Manganese,
            Phosphorus = item.Phosphorus,
            Sulfur = item.Sulfur,
            Nickel = item.Nickel,
            Chromium = item.Chromium,
            Molybdenum = item.Molybdenum,
            Copper = item.Copper,
            Nitrogen = item.Nitrogen,
            Niobium = item.Niobium,
            Titanium = item.Titanium,
            Iron = item.Iron,
            Aluminum = item.Aluminum,
            Tungsten = item.Tungsten
        };
    }

    private void CancelEdit(GradeChemicalCompositionDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(GradeChemicalCompositionDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.StandardGrade)) errors.Add("标准牌号不能为空");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateGradeChemicalCompositionRequest
            {
                StandardGrade = cache.StandardGrade,
                StandardGradeCategory = cache.StandardGradeCategory,
                Carbon = cache.Carbon,
                Silicon = cache.Silicon,
                Manganese = cache.Manganese,
                Phosphorus = cache.Phosphorus,
                Sulfur = cache.Sulfur,
                Nickel = cache.Nickel,
                Chromium = cache.Chromium,
                Molybdenum = cache.Molybdenum,
                Copper = cache.Copper,
                Nitrogen = cache.Nitrogen,
                Niobium = cache.Niobium,
                Titanium = cache.Titanium,
                Iron = cache.Iron,
                Aluminum = cache.Aluminum,
                Tungsten = cache.Tungsten
            };

            var result = await GradeChemicalCompositionService.UpdateAsync(item.Id, request);
            if (result.Success)
            {
                item.StandardGrade = cache.StandardGrade;
                item.StandardGradeCategory = cache.StandardGradeCategory;
                item.Carbon = cache.Carbon;
                item.Silicon = cache.Silicon;
                item.Manganese = cache.Manganese;
                item.Phosphorus = cache.Phosphorus;
                item.Sulfur = cache.Sulfur;
                item.Nickel = cache.Nickel;
                item.Chromium = cache.Chromium;
                item.Molybdenum = cache.Molybdenum;
                item.Copper = cache.Copper;
                item.Nitrogen = cache.Nitrogen;
                item.Niobium = cache.Niobium;
                item.Titanium = cache.Titanium;
                item.Iron = cache.Iron;
                item.Aluminum = cache.Aluminum;
                item.Tungsten = cache.Tungsten;

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

    private async Task DeleteItem(GradeChemicalCompositionDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除牌号化学成分 \"{item.StandardGrade}\" 吗？",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await GradeChemicalCompositionService.DeleteAsync(item.Id);
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

    private RenderFragment RenderCell(GradeChemicalCompositionDto item, ColumnDef col) => builder =>
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
            case "Carbon":
            case "Silicon":
            case "Manganese":
            case "Phosphorus":
            case "Sulfur":
            case "Nickel":
            case "Chromium":
            case "Molybdenum":
            case "Copper":
            case "Nitrogen":
            case "Niobium":
            case "Titanium":
            case "Iron":
            case "Aluminum":
            case "Tungsten":
                RenderElementCell(item, col.Key, cache)(builder);
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    private RenderFragment RenderElementCell(GradeChemicalCompositionDto item, string key, EditCache? cache) => builder =>
    {
        var val = key switch
        {
            "Carbon" => item.Carbon,
            "Silicon" => item.Silicon,
            "Manganese" => item.Manganese,
            "Phosphorus" => item.Phosphorus,
            "Sulfur" => item.Sulfur,
            "Nickel" => item.Nickel,
            "Chromium" => item.Chromium,
            "Molybdenum" => item.Molybdenum,
            "Copper" => item.Copper,
            "Nitrogen" => item.Nitrogen,
            "Niobium" => item.Niobium,
            "Titanium" => item.Titanium,
            "Iron" => item.Iron,
            "Aluminum" => item.Aluminum,
            "Tungsten" => item.Tungsten,
            _ => null
        };
        var cacheVal = key switch
        {
            "Carbon" => cache?.Carbon,
            "Silicon" => cache?.Silicon,
            "Manganese" => cache?.Manganese,
            "Phosphorus" => cache?.Phosphorus,
            "Sulfur" => cache?.Sulfur,
            "Nickel" => cache?.Nickel,
            "Chromium" => cache?.Chromium,
            "Molybdenum" => cache?.Molybdenum,
            "Copper" => cache?.Copper,
            "Nitrogen" => cache?.Nitrogen,
            "Niobium" => cache?.Niobium,
            "Titanium" => cache?.Titanium,
            "Iron" => cache?.Iron,
            "Aluminum" => cache?.Aluminum,
            "Tungsten" => cache?.Tungsten,
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
                    case "Carbon": cache!.Carbon = v; break;
                    case "Silicon": cache!.Silicon = v; break;
                    case "Manganese": cache!.Manganese = v; break;
                    case "Phosphorus": cache!.Phosphorus = v; break;
                    case "Sulfur": cache!.Sulfur = v; break;
                    case "Nickel": cache!.Nickel = v; break;
                    case "Chromium": cache!.Chromium = v; break;
                    case "Molybdenum": cache!.Molybdenum = v; break;
                    case "Copper": cache!.Copper = v; break;
                    case "Nitrogen": cache!.Nitrogen = v; break;
                    case "Niobium": cache!.Niobium = v; break;
                    case "Titanium": cache!.Titanium = v; break;
                    case "Iron": cache!.Iron = v; break;
                    case "Aluminum": cache!.Aluminum = v; break;
                    case "Tungsten": cache!.Tungsten = v; break;
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
        await PageState.SaveAsync("grade_chemical_compositions", state);
    }
}
