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

namespace MES.Blazor.Pages.Quality;

public partial class ChemicalValidationRules
{
    private MudTable<ChemicalValidationRuleDto>? table;
    private List<ChemicalValidationRuleDto> _pageItems = new();
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

    private string sortColumn = "plantgrade";
    private bool sortDescending = false;

    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "PlantGrade",   Label = "工厂牌号",   SortKey = "plantgrade", FilterType = "string" },
        new() { Key = "CMin",         Label = "C-",         SortKey = "cmin", FilterType = "string" },
        new() { Key = "CMax",         Label = "C+",         SortKey = "cmax", FilterType = "string" },
        new() { Key = "SiMin",        Label = "Si-",        SortKey = "simin", FilterType = "string" },
        new() { Key = "SiMax",        Label = "Si+",        SortKey = "simax", FilterType = "string" },
        new() { Key = "MnMin",        Label = "Mn-",        SortKey = "mnmin", FilterType = "string" },
        new() { Key = "MnMax",        Label = "Mn+",        SortKey = "mnmax", FilterType = "string" },
        new() { Key = "PMin",         Label = "P-",         SortKey = "pmin", FilterType = "string" },
        new() { Key = "PMax",         Label = "P+",         SortKey = "pmax", FilterType = "string" },
        new() { Key = "SMin",         Label = "S-",         SortKey = "smin", FilterType = "string" },
        new() { Key = "SMax",         Label = "S+",         SortKey = "smax", FilterType = "string" },
        new() { Key = "NiMin",        Label = "Ni-",        SortKey = "nimin", FilterType = "string" },
        new() { Key = "NiMax",        Label = "Ni+",        SortKey = "nimax", FilterType = "string" },
        new() { Key = "CrMin",        Label = "Cr-",        SortKey = "crmin", FilterType = "string" },
        new() { Key = "CrMax",        Label = "Cr+",        SortKey = "crmax", FilterType = "string" },
        new() { Key = "MoMin",        Label = "Mo-",        SortKey = "momin", FilterType = "string" },
        new() { Key = "MoMax",        Label = "Mo+",        SortKey = "momax", FilterType = "string" },
        new() { Key = "CuMin",        Label = "Cu-",        SortKey = "cumin", FilterType = "string" },
        new() { Key = "CuMax",        Label = "Cu+",        SortKey = "cumax", FilterType = "string" },
        new() { Key = "NMin",         Label = "N-",         SortKey = "nmin", FilterType = "string" },
        new() { Key = "NMax",         Label = "N+",         SortKey = "nmax", FilterType = "string" },
        new() { Key = "NbMin",        Label = "Nb-",        SortKey = "nbmin", FilterType = "string" },
        new() { Key = "NbMax",        Label = "Nb+",        SortKey = "nbmax", FilterType = "string" },
        new() { Key = "TiMin",        Label = "Ti-",        SortKey = "timin", FilterType = "string" },
        new() { Key = "TiMax",        Label = "Ti+",        SortKey = "timax", FilterType = "string" },
        new() { Key = "FeMin",        Label = "Fe-",        SortKey = "femin", FilterType = "string" },
        new() { Key = "FeMax",        Label = "Fe+",        SortKey = "femax", FilterType = "string" },
        new() { Key = "AlMin",        Label = "Al-",        SortKey = "almin", FilterType = "string" },
        new() { Key = "AlMax",        Label = "Al+",        SortKey = "almax", FilterType = "string" },
        new() { Key = "WMin",         Label = "W-",         SortKey = "wmin", FilterType = "string" },
        new() { Key = "WMax",         Label = "W+",         SortKey = "wmax", FilterType = "string" },
        new() { Key = "PRENMin",      Label = "PREN腐蚀当量-", SortKey = "prenmin", FilterType = "string" },
        new() { Key = "CreatedTime",  Label = "创建日期",   SortKey = "createdtime" },
        new() { Key = "UpdatedTime",  Label = "更新日期",   SortKey = "updatedtime" },
    };

    private async Task<TableData<ChemicalValidationRuleDto>> LoadDataFromServer(TableState state)
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

            var result = await ChemicalValidationRuleService.GetAllAsync(
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

        return new TableData<ChemicalValidationRuleDto>
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

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await ChemicalValidationRuleService.GetFilterContextsAsync();
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

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string PlantGrade { get; set; } = "";
        public string? CMin { get; set; }
        public string? CMax { get; set; }
        public string? SiMin { get; set; }
        public string? SiMax { get; set; }
        public string? MnMin { get; set; }
        public string? MnMax { get; set; }
        public string? PMin { get; set; }
        public string? PMax { get; set; }
        public string? SMin { get; set; }
        public string? SMax { get; set; }
        public string? NiMin { get; set; }
        public string? NiMax { get; set; }
        public string? CrMin { get; set; }
        public string? CrMax { get; set; }
        public string? MoMin { get; set; }
        public string? MoMax { get; set; }
        public string? CuMin { get; set; }
        public string? CuMax { get; set; }
        public string? NMin { get; set; }
        public string? NMax { get; set; }
        public string? NbMin { get; set; }
        public string? NbMax { get; set; }
        public string? TiMin { get; set; }
        public string? TiMax { get; set; }
        public string? FeMin { get; set; }
        public string? FeMax { get; set; }
        public string? AlMin { get; set; }
        public string? AlMax { get; set; }
        public string? WMin { get; set; }
        public string? WMax { get; set; }
        public string? PRENMin { get; set; }
    }

    private void StartEdit(ChemicalValidationRuleDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new EditCache
        {
            PlantGrade = item.PlantGrade,
            CMin = item.CMin, CMax = item.CMax,
            SiMin = item.SiMin, SiMax = item.SiMax,
            MnMin = item.MnMin, MnMax = item.MnMax,
            PMin = item.PMin, PMax = item.PMax,
            SMin = item.SMin, SMax = item.SMax,
            NiMin = item.NiMin, NiMax = item.NiMax,
            CrMin = item.CrMin, CrMax = item.CrMax,
            MoMin = item.MoMin, MoMax = item.MoMax,
            CuMin = item.CuMin, CuMax = item.CuMax,
            NMin = item.NMin, NMax = item.NMax,
            NbMin = item.NbMin, NbMax = item.NbMax,
            TiMin = item.TiMin, TiMax = item.TiMax,
            FeMin = item.FeMin, FeMax = item.FeMax,
            AlMin = item.AlMin, AlMax = item.AlMax,
            WMin = item.WMin, WMax = item.WMax,
            PRENMin = item.PRENMin
        };
    }

    private void CancelEdit(ChemicalValidationRuleDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(ChemicalValidationRuleDto item)
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
            var request = new UpdateChemicalValidationRuleRequest
            {
                PlantGrade = cache.PlantGrade,
                CMin = cache.CMin, CMax = cache.CMax,
                SiMin = cache.SiMin, SiMax = cache.SiMax,
                MnMin = cache.MnMin, MnMax = cache.MnMax,
                PMin = cache.PMin, PMax = cache.PMax,
                SMin = cache.SMin, SMax = cache.SMax,
                NiMin = cache.NiMin, NiMax = cache.NiMax,
                CrMin = cache.CrMin, CrMax = cache.CrMax,
                MoMin = cache.MoMin, MoMax = cache.MoMax,
                CuMin = cache.CuMin, CuMax = cache.CuMax,
                NMin = cache.NMin, NMax = cache.NMax,
                NbMin = cache.NbMin, NbMax = cache.NbMax,
                TiMin = cache.TiMin, TiMax = cache.TiMax,
                FeMin = cache.FeMin, FeMax = cache.FeMax,
                AlMin = cache.AlMin, AlMax = cache.AlMax,
                WMin = cache.WMin, WMax = cache.WMax,
                PRENMin = cache.PRENMin
            };

            var result = await ChemicalValidationRuleService.UpdateAsync(item.Id, request);
            if (result.Success && result.Data != null)
            {
                var d = result.Data;
                item.PlantGrade = d.PlantGrade;
                item.CMin = d.CMin; item.CMax = d.CMax;
                item.SiMin = d.SiMin; item.SiMax = d.SiMax;
                item.MnMin = d.MnMin; item.MnMax = d.MnMax;
                item.PMin = d.PMin; item.PMax = d.PMax;
                item.SMin = d.SMin; item.SMax = d.SMax;
                item.NiMin = d.NiMin; item.NiMax = d.NiMax;
                item.CrMin = d.CrMin; item.CrMax = d.CrMax;
                item.MoMin = d.MoMin; item.MoMax = d.MoMax;
                item.CuMin = d.CuMin; item.CuMax = d.CuMax;
                item.NMin = d.NMin; item.NMax = d.NMax;
                item.NbMin = d.NbMin; item.NbMax = d.NbMax;
                item.TiMin = d.TiMin; item.TiMax = d.TiMax;
                item.FeMin = d.FeMin; item.FeMax = d.FeMax;
                item.AlMin = d.AlMin; item.AlMax = d.AlMax;
                item.WMin = d.WMin; item.WMax = d.WMax;
                item.PRENMin = d.PRENMin;
                item.UpdatedTime = d.UpdatedTime;

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

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("chemical-validate", null, _allColumns);
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

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("chemical-validate", null);
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

        var savedState = await PageState.LoadAsync("chemicalvalidationrules");
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

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && table != null)
            await table.ReloadServerData();

        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#chemical-validate-list-table"))
                _isArrowNavSetup = false;
        }
    }

    private void NavigateToCreate() => Navigation.NavigateTo("/quality/chemical-validate/create");

    private string? GetCellRawValue(ChemicalValidationRuleDto item, string key) => key switch
    {
        "PlantGrade" => item.PlantGrade,
        "CMin" => item.CMin, "CMax" => item.CMax,
        "SiMin" => item.SiMin, "SiMax" => item.SiMax,
        "MnMin" => item.MnMin, "MnMax" => item.MnMax,
        "PMin" => item.PMin, "PMax" => item.PMax,
        "SMin" => item.SMin, "SMax" => item.SMax,
        "NiMin" => item.NiMin, "NiMax" => item.NiMax,
        "CrMin" => item.CrMin, "CrMax" => item.CrMax,
        "MoMin" => item.MoMin, "MoMax" => item.MoMax,
        "CuMin" => item.CuMin, "CuMax" => item.CuMax,
        "NMin" => item.NMin, "NMax" => item.NMax,
        "NbMin" => item.NbMin, "NbMax" => item.NbMax,
        "TiMin" => item.TiMin, "TiMax" => item.TiMax,
        "FeMin" => item.FeMin, "FeMax" => item.FeMax,
        "AlMin" => item.AlMin, "AlMax" => item.AlMax,
        "WMin" => item.WMin, "WMax" => item.WMax,
        "PRENMin" => item.PRENMin,
        "CreatedTime" => item.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        "UpdatedTime" => item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        _ => null
    };

    private RenderFragment RenderCell(ChemicalValidationRuleDto item, ColumnDef col) => builder =>
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
            case "CMin":
                RenderEditField(builder, isEditing, cache?.CMin, v => { if (cache != null) cache.CMin = v; }, item.CMin);
                break;
            case "CMax":
                RenderEditField(builder, isEditing, cache?.CMax, v => { if (cache != null) cache.CMax = v; }, item.CMax);
                break;
            case "SiMin":
                RenderEditField(builder, isEditing, cache?.SiMin, v => { if (cache != null) cache.SiMin = v; }, item.SiMin);
                break;
            case "SiMax":
                RenderEditField(builder, isEditing, cache?.SiMax, v => { if (cache != null) cache.SiMax = v; }, item.SiMax);
                break;
            case "MnMin":
                RenderEditField(builder, isEditing, cache?.MnMin, v => { if (cache != null) cache.MnMin = v; }, item.MnMin);
                break;
            case "MnMax":
                RenderEditField(builder, isEditing, cache?.MnMax, v => { if (cache != null) cache.MnMax = v; }, item.MnMax);
                break;
            case "PMin":
                RenderEditField(builder, isEditing, cache?.PMin, v => { if (cache != null) cache.PMin = v; }, item.PMin);
                break;
            case "PMax":
                RenderEditField(builder, isEditing, cache?.PMax, v => { if (cache != null) cache.PMax = v; }, item.PMax);
                break;
            case "SMin":
                RenderEditField(builder, isEditing, cache?.SMin, v => { if (cache != null) cache.SMin = v; }, item.SMin);
                break;
            case "SMax":
                RenderEditField(builder, isEditing, cache?.SMax, v => { if (cache != null) cache.SMax = v; }, item.SMax);
                break;
            case "NiMin":
                RenderEditField(builder, isEditing, cache?.NiMin, v => { if (cache != null) cache.NiMin = v; }, item.NiMin);
                break;
            case "NiMax":
                RenderEditField(builder, isEditing, cache?.NiMax, v => { if (cache != null) cache.NiMax = v; }, item.NiMax);
                break;
            case "CrMin":
                RenderEditField(builder, isEditing, cache?.CrMin, v => { if (cache != null) cache.CrMin = v; }, item.CrMin);
                break;
            case "CrMax":
                RenderEditField(builder, isEditing, cache?.CrMax, v => { if (cache != null) cache.CrMax = v; }, item.CrMax);
                break;
            case "MoMin":
                RenderEditField(builder, isEditing, cache?.MoMin, v => { if (cache != null) cache.MoMin = v; }, item.MoMin);
                break;
            case "MoMax":
                RenderEditField(builder, isEditing, cache?.MoMax, v => { if (cache != null) cache.MoMax = v; }, item.MoMax);
                break;
            case "CuMin":
                RenderEditField(builder, isEditing, cache?.CuMin, v => { if (cache != null) cache.CuMin = v; }, item.CuMin);
                break;
            case "CuMax":
                RenderEditField(builder, isEditing, cache?.CuMax, v => { if (cache != null) cache.CuMax = v; }, item.CuMax);
                break;
            case "NMin":
                RenderEditField(builder, isEditing, cache?.NMin, v => { if (cache != null) cache.NMin = v; }, item.NMin);
                break;
            case "NMax":
                RenderEditField(builder, isEditing, cache?.NMax, v => { if (cache != null) cache.NMax = v; }, item.NMax);
                break;
            case "NbMin":
                RenderEditField(builder, isEditing, cache?.NbMin, v => { if (cache != null) cache.NbMin = v; }, item.NbMin);
                break;
            case "NbMax":
                RenderEditField(builder, isEditing, cache?.NbMax, v => { if (cache != null) cache.NbMax = v; }, item.NbMax);
                break;
            case "TiMin":
                RenderEditField(builder, isEditing, cache?.TiMin, v => { if (cache != null) cache.TiMin = v; }, item.TiMin);
                break;
            case "TiMax":
                RenderEditField(builder, isEditing, cache?.TiMax, v => { if (cache != null) cache.TiMax = v; }, item.TiMax);
                break;
            case "FeMin":
                RenderEditField(builder, isEditing, cache?.FeMin, v => { if (cache != null) cache.FeMin = v; }, item.FeMin);
                break;
            case "FeMax":
                RenderEditField(builder, isEditing, cache?.FeMax, v => { if (cache != null) cache.FeMax = v; }, item.FeMax);
                break;
            case "AlMin":
                RenderEditField(builder, isEditing, cache?.AlMin, v => { if (cache != null) cache.AlMin = v; }, item.AlMin);
                break;
            case "AlMax":
                RenderEditField(builder, isEditing, cache?.AlMax, v => { if (cache != null) cache.AlMax = v; }, item.AlMax);
                break;
            case "WMin":
                RenderEditField(builder, isEditing, cache?.WMin, v => { if (cache != null) cache.WMin = v; }, item.WMin);
                break;
            case "WMax":
                RenderEditField(builder, isEditing, cache?.WMax, v => { if (cache != null) cache.WMax = v; }, item.WMax);
                break;
            case "PRENMin":
                RenderEditField(builder, isEditing, cache?.PRENMin, v => { if (cache != null) cache.PRENMin = v; }, item.PRENMin);
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

    private async Task DeleteItem(ChemicalValidationRuleDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除工厂牌号 \"{item.PlantGrade}\" 的验证规则吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await ChemicalValidationRuleService.DeleteAsync(item.Id);
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

    private async Task PrintSelected()
    {
        if (!selectedIds.Any()) { Snackbar.Add("请先选择要打印的牌号验证记录", Severity.Warning); return; }
        await JS.InvokeVoidAsync("printTable", "#chemical-validate-list-table", "牌号验证（选中记录）");
    }

    private async Task PrintAll()
    {
        if (!_pageItems.Any())
        {
            Snackbar.Add("没有可打印的数据", Severity.Warning);
            return;
        }
        var html = BuildPrintHtml(_pageItems);
        await JS.InvokeVoidAsync("printRawHtml", html, "牌号验证");
    }

    private string BuildPrintHtml(IEnumerable<ChemicalValidationRuleDto> items)
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

    private string GetCellPrintValue(ChemicalValidationRuleDto item, ColumnDef col) => col.Key switch
    {
        "PlantGrade" => item.PlantGrade,
        "CMin" => item.CMin, "CMax" => item.CMax,
        "SiMin" => item.SiMin, "SiMax" => item.SiMax,
        "MnMin" => item.MnMin, "MnMax" => item.MnMax,
        "PMin" => item.PMin, "PMax" => item.PMax,
        "SMin" => item.SMin, "SMax" => item.SMax,
        "NiMin" => item.NiMin, "NiMax" => item.NiMax,
        "CrMin" => item.CrMin, "CrMax" => item.CrMax,
        "MoMin" => item.MoMin, "MoMax" => item.MoMax,
        "CuMin" => item.CuMin, "CuMax" => item.CuMax,
        "NMin" => item.NMin, "NMax" => item.NMax,
        "NbMin" => item.NbMin, "NbMax" => item.NbMax,
        "TiMin" => item.TiMin, "TiMax" => item.TiMax,
        "FeMin" => item.FeMin, "FeMax" => item.FeMax,
        "AlMin" => item.AlMin, "AlMax" => item.AlMax,
        "WMin" => item.WMin, "WMax" => item.WMax,
        "PRENMin" => item.PRENMin,
        "CreatedTime" => item.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        "UpdatedTime" => item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        _ => ""
    };

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
        await PageState.SaveAsync("chemicalvalidationrules", state);
    }
}
