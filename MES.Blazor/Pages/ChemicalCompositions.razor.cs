using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Blazor.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages;

public partial class ChemicalCompositions
{
    private MudTable<ChemicalCompositionDto>? table;
    private List<ChemicalCompositionDto> _pageItems = new();
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
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    // 排序
    private string sortColumn = "plantgrade";
    private bool sortDescending = false;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "PlantGrade",   Label = "工厂牌号",     SortKey = "plantgrade", FilterType = "string" },
        new() { Key = "Carbon",       Label = "C",           SortKey = "carbon", FilterType = "string" },
        new() { Key = "Silicon",      Label = "Si",          SortKey = "silicon", FilterType = "string" },
        new() { Key = "Manganese",    Label = "Mn",          SortKey = "manganese", FilterType = "string" },
        new() { Key = "Phosphorus",   Label = "P",           SortKey = "phosphorus", FilterType = "string" },
        new() { Key = "Sulfur",       Label = "S",           SortKey = "sulfur", FilterType = "string" },
        new() { Key = "Nickel",       Label = "Ni",          SortKey = "nickel", FilterType = "string" },
        new() { Key = "Chromium",     Label = "Cr",          SortKey = "chromium", FilterType = "string" },
        new() { Key = "Molybdenum",   Label = "Mo",          SortKey = "molybdenum", FilterType = "string" },
        new() { Key = "Copper",       Label = "Cu",          SortKey = "copper", FilterType = "string" },
        new() { Key = "Nitrogen",     Label = "N",           SortKey = "nitrogen", FilterType = "string" },
        new() { Key = "Niobium",      Label = "Nb",          SortKey = "niobium", FilterType = "string" },
        new() { Key = "Titanium",     Label = "Ti",          SortKey = "titanium", FilterType = "string" },
        new() { Key = "Iron",         Label = "Fe",          SortKey = "iron", FilterType = "string" },
        new() { Key = "Aluminum",     Label = "Al",          SortKey = "aluminum", FilterType = "string" },
        new() { Key = "Tungsten",     Label = "W",           SortKey = "tungsten", FilterType = "string" },
        new() { Key = "PREN",         Label = "PREN腐蚀当量", SortKey = "pren", FilterType = "string" },
        new() { Key = "CreatedTime",  Label = "创建日期",   SortKey = "createdtime" },
        new() { Key = "UpdatedTime",  Label = "更新日期",   SortKey = "updatedtime" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<ChemicalCompositionDto>> LoadDataFromServer(TableState state)
    {
        try
        {
            // 首次加载覆盖页码（MudTable 初始化时始终传 page=0）
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "plantgrade";
            var filters = SerializeFilters();

            var result = await ChemicalCompositionService.GetAllAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
                filters: filters
            );

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

        return new TableData<ChemicalCompositionDto>
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
            var result = await ChemicalCompositionService.GetFilterContextsAsync();
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

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string PlantGrade { get; set; } = "";
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
        public string? PREN { get; set; }
    }

    private void StartEdit(ChemicalCompositionDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new EditCache
        {
            PlantGrade = item.PlantGrade,
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
            Tungsten = item.Tungsten,
            PREN = item.PREN
        };
    }

    private void CancelEdit(ChemicalCompositionDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(ChemicalCompositionDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        if (string.IsNullOrWhiteSpace(cache.PlantGrade))
        {
            Snackbar.Add("工厂牌号不能为空", Severity.Error);
            return;
        }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateChemicalCompositionRequest
            {
                PlantGrade = cache.PlantGrade,
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
                Tungsten = cache.Tungsten,
                PREN = cache.PREN
            };

            var result = await ChemicalCompositionService.UpdateAsync(item.Id, request);
            if (result.Success && result.Data != null)
            {
                item.PlantGrade = result.Data.PlantGrade;
                item.Carbon = result.Data.Carbon;
                item.Silicon = result.Data.Silicon;
                item.Manganese = result.Data.Manganese;
                item.Phosphorus = result.Data.Phosphorus;
                item.Sulfur = result.Data.Sulfur;
                item.Nickel = result.Data.Nickel;
                item.Chromium = result.Data.Chromium;
                item.Molybdenum = result.Data.Molybdenum;
                item.Copper = result.Data.Copper;
                item.Nitrogen = result.Data.Nitrogen;
                item.Niobium = result.Data.Niobium;
                item.Titanium = result.Data.Titanium;
                item.Iron = result.Data.Iron;
                item.Aluminum = result.Data.Aluminum;
                item.Tungsten = result.Data.Tungsten;
                item.PREN = result.Data.PREN;
                item.UpdatedTime = result.Data.UpdatedTime;

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

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("chemical-composition", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
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

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("chemical-composition", null);
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
        var savedState = await PageState.LoadAsync("chemicalcompositions");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "plantgrade";
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

        // 加载筛选上下文
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#chemical-composition-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 导航 ==========

    private void NavigateToCreate() => Navigation.NavigateTo("/quality/chemical-composition/create");

    // ========== 单元格渲染 ==========

    private string? GetCellRawValue(ChemicalCompositionDto item, string key) => key switch
    {
        "PlantGrade" => item.PlantGrade,
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
        "PREN" => item.PREN,
        "CreatedTime" => item.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        "UpdatedTime" => item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        _ => null
    };

    private RenderFragment RenderCell(ChemicalCompositionDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing ? _editCache.GetValueOrDefault(item.Id) : null;

        switch (col.Key)
        {
            case "PlantGrade":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.PlantGrade);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.PlantGrade = v ?? ""));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.PlantGrade);
                }
                break;
            case "Carbon":
                RenderEditField(builder, isEditing, cache?.Carbon, v => { if (cache != null) cache.Carbon = v; }, item.Carbon);
                break;
            case "Silicon":
                RenderEditField(builder, isEditing, cache?.Silicon, v => { if (cache != null) cache.Silicon = v; }, item.Silicon);
                break;
            case "Manganese":
                RenderEditField(builder, isEditing, cache?.Manganese, v => { if (cache != null) cache.Manganese = v; }, item.Manganese);
                break;
            case "Phosphorus":
                RenderEditField(builder, isEditing, cache?.Phosphorus, v => { if (cache != null) cache.Phosphorus = v; }, item.Phosphorus);
                break;
            case "Sulfur":
                RenderEditField(builder, isEditing, cache?.Sulfur, v => { if (cache != null) cache.Sulfur = v; }, item.Sulfur);
                break;
            case "Nickel":
                RenderEditField(builder, isEditing, cache?.Nickel, v => { if (cache != null) cache.Nickel = v; }, item.Nickel);
                break;
            case "Chromium":
                RenderEditField(builder, isEditing, cache?.Chromium, v => { if (cache != null) cache.Chromium = v; }, item.Chromium);
                break;
            case "Molybdenum":
                RenderEditField(builder, isEditing, cache?.Molybdenum, v => { if (cache != null) cache.Molybdenum = v; }, item.Molybdenum);
                break;
            case "Copper":
                RenderEditField(builder, isEditing, cache?.Copper, v => { if (cache != null) cache.Copper = v; }, item.Copper);
                break;
            case "Nitrogen":
                RenderEditField(builder, isEditing, cache?.Nitrogen, v => { if (cache != null) cache.Nitrogen = v; }, item.Nitrogen);
                break;
            case "Niobium":
                RenderEditField(builder, isEditing, cache?.Niobium, v => { if (cache != null) cache.Niobium = v; }, item.Niobium);
                break;
            case "Titanium":
                RenderEditField(builder, isEditing, cache?.Titanium, v => { if (cache != null) cache.Titanium = v; }, item.Titanium);
                break;
            case "Iron":
                RenderEditField(builder, isEditing, cache?.Iron, v => { if (cache != null) cache.Iron = v; }, item.Iron);
                break;
            case "Aluminum":
                RenderEditField(builder, isEditing, cache?.Aluminum, v => { if (cache != null) cache.Aluminum = v; }, item.Aluminum);
                break;
            case "Tungsten":
                RenderEditField(builder, isEditing, cache?.Tungsten, v => { if (cache != null) cache.Tungsten = v; }, item.Tungsten);
                break;
            case "PREN":
                RenderEditField(builder, isEditing, cache?.PREN, v => { if (cache != null) cache.PREN = v; }, item.PREN);
                break;
            case "CreatedTime":
                builder.AddContent(0, item.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            case "UpdatedTime":
                builder.AddContent(0, item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    private void RenderEditField(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, bool isEditing, string? editValue, Action<string?> setter, string? displayValue)
    {
        if (isEditing)
        {
            builder.OpenComponent<MudTextField<string>>(0);
            builder.AddAttribute(1, "Value", editValue);
            builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string?>(this, setter));
            builder.AddAttribute(3, "Class", "compact-input");
            builder.CloseComponent();
        }
        else
        {
            builder.AddContent(0, displayValue);
        }
    }

    // ========== 删除 ==========

    private async Task DeleteItem(ChemicalCompositionDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除工厂牌号 \"{item.PlantGrade}\" 的化学成分记录吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await ChemicalCompositionService.DeleteAsync(item.Id);
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

    // ========== 打印 ==========

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的化学成分记录", Severity.Warning);
            return;
        }
        await JS.InvokeVoidAsync("printTable", "#chemical-composition-list-table", "牌号化学成分（选中记录）");
    }

    private async Task PrintAll()
    {
        if (!_pageItems.Any())
        {
            Snackbar.Add("没有可打印的数据", Severity.Warning);
            return;
        }
        var html = BuildPrintHtml(_pageItems);
        await JS.InvokeVoidAsync("printRawHtml", html, "牌号化学成分");
    }

    private string BuildPrintHtml(IEnumerable<ChemicalCompositionDto> items)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<table><thead><tr>");
        foreach (var col in _visibleColumns)
        {
            sb.Append("<th>").Append(System.Net.WebUtility.HtmlEncode(col.Label)).Append("</th>");
        }
        sb.Append("</tr></thead><tbody>");
        foreach (var item in items)
        {
            sb.Append("<tr>");
            foreach (var col in _visibleColumns)
            {
                sb.Append("<td>");
                sb.Append(System.Net.WebUtility.HtmlEncode(GetCellPrintValue(item, col)));
                sb.Append("</td>");
            }
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");
        return sb.ToString();
    }

    private string GetCellPrintValue(ChemicalCompositionDto item, ColumnDef col) => col.Key switch
    {
        "PlantGrade" => item.PlantGrade,
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
        "PREN" => item.PREN,
        "CreatedTime" => item.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        "UpdatedTime" => item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        _ => ""
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
        await PageState.SaveAsync("chemicalcompositions", state);
    }
}
