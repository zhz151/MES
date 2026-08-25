using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Models;
using MES.Blazor.Helpers;
using MES.Blazor.Shared;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Shared;
using System.Text.Json;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.Quality;

public partial class ChemicalAnalyses
{
    private MudTable<ChemicalAnalysisDto>? table;
    private List<ChemicalAnalysisDto> _pageItems = new();
    private int _totalCount;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private int _loadVersion;
    private bool _resetToFirstPage;
    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;

    private string sortColumn = "analysisdate";
    private bool sortDescending = true;

    // ========== 打印选中 ==========
    private HashSet<int> selectedIds = new();
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

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "AnalysisDate",    Label = "检验日期",   SortKey = "analysisdate", FilterType = "date", Width = "110" },
        new() { Key = "Analyst",         Label = "检验员",     SortKey = "analyst", FilterType = "string", Width = "80" },
        new() { Key = "FurnaceNo",       Label = "炉号",       SortKey = "furnaceno", FilterType = "string", Width = "100" },
        new() { Key = "Grade",           Label = "牌号",       SortKey = "grade", FilterType = "string", Width = "100" },
        new() { Key = "AnalysisCount",   Label = "试样编号",   SortKey = "analysiscount", Width = "80" },
        new() { Key = "AnalysisStandard",Label = "检验标准",   SortKey = "analysisstandard", FilterType = "string", Width = "120" },
        new() { Key = "C",  Label = "C%",  SortKey = "c",  Width = "80" },
        new() { Key = "Si", Label = "Si%", SortKey = "si", Width = "80" },
        new() { Key = "Mn", Label = "Mn%", SortKey = "mn", Width = "80" },
        new() { Key = "P",  Label = "P%",  SortKey = "p",  Width = "80" },
        new() { Key = "S",  Label = "S%",  SortKey = "s",  Width = "80" },
        new() { Key = "Ni", Label = "Ni%", SortKey = "ni", Width = "80" },
        new() { Key = "Cr", Label = "Cr%", SortKey = "cr", Width = "80" },
        new() { Key = "Mo", Label = "Mo%", SortKey = "mo", Width = "80" },
        new() { Key = "Cu", Label = "Cu%", SortKey = "cu", Width = "80" },
        new() { Key = "N",  Label = "N%",  SortKey = "n",  Width = "80" },
        new() { Key = "Nb", Label = "Nb%", SortKey = "nb", Width = "80" },
        new() { Key = "Ti", Label = "Ti%", SortKey = "ti", Width = "80" },
        new() { Key = "Fe", Label = "Fe%", SortKey = "fe", Width = "80" },
        new() { Key = "Al", Label = "Al%", SortKey = "al", Width = "80" },
        new() { Key = "W",  Label = "W%",  SortKey = "w",  Width = "80" },
        new() { Key = "UpdatedTime",      Label = "更新日期",   SortKey = "updatedtime", Width = "120" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<ChemicalAnalysisDto>> LoadDataFromServer(TableState state)
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

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "analysisdate";
            var filtersJson = SerializeFilters();

            DateTime? dateFrom = null;
            DateTime? dateTo = null;
            if (DateTime.TryParse(_dateFrom, out var df)) dateFrom = df;
            if (DateTime.TryParse(_dateTo, out var dt)) dateTo = dt;

            var result = await ChemicalAnalysisService.GetAllAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
                inspectionDateFrom: dateFrom,
                inspectionDateTo: dateTo,
                filters: filtersJson);

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<ChemicalAnalysisDto> { Items = _pageItems, TotalItems = _totalCount };
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

        return new TableData<ChemicalAnalysisDto>
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
            var result = await ChemicalAnalysisService.GetFilterContextsAsync();
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

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        _resetToFirstPage = true;
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

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("chemical-analysis", null, _allColumns);
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
        var saved = await ColumnPrefs.LoadAsync("chemical-analysis", null);
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

        var savedState = await PageState.LoadAsync("chemical-analysis");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "analysisdate";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
            if (savedState.Extras?.ContainsKey("dateFrom") == true)
                _dateFrom = savedState.Extras["dateFrom"];
            if (savedState.Extras?.ContainsKey("dateTo") == true)
                _dateTo = savedState.Extras["dateTo"];
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

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string AnalysisDate { get; set; } = "";
        public string? Analyst { get; set; }
        public string? FurnaceNo { get; set; }
        public string? Grade { get; set; }
        public int? AnalysisCount { get; set; }
        public string? AnalysisStandard { get; set; }
        public decimal? C { get; set; }
        public decimal? Si { get; set; }
        public decimal? Mn { get; set; }
        public decimal? P { get; set; }
        public decimal? S { get; set; }
        public decimal? Ni { get; set; }
        public decimal? Cr { get; set; }
        public decimal? Mo { get; set; }
        public decimal? Cu { get; set; }
        public decimal? N { get; set; }
        public decimal? Nb { get; set; }
        public decimal? Ti { get; set; }
        public decimal? Fe { get; set; }
        public decimal? Al { get; set; }
        public decimal? W { get; set; }
    }

    private void StartEdit(ChemicalAnalysisDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new EditCache
        {
            AnalysisDate = item.AnalysisDate.ToString("yyyy-MM-dd"),
            Analyst = item.Analyst,
            FurnaceNo = item.FurnaceNo,
            Grade = item.Grade,
            AnalysisCount = item.AnalysisCount,
            AnalysisStandard = item.AnalysisStandard,
            C = item.C,
            Si = item.Si,
            Mn = item.Mn,
            P = item.P,
            S = item.S,
            Ni = item.Ni,
            Cr = item.Cr,
            Mo = item.Mo,
            Cu = item.Cu,
            N = item.N,
            Nb = item.Nb,
            Ti = item.Ti,
            Fe = item.Fe,
            Al = item.Al,
            W = item.W
        };
    }

    private void CancelEdit(ChemicalAnalysisDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(ChemicalAnalysisDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        if (!DateTime.TryParse(cache.AnalysisDate, out var analysisDate))
        {
            Snackbar.Add("检验日期格式无效", Severity.Error);
            return;
        }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateChemicalAnalysisRequest
            {
                AnalysisDate = analysisDate,
                Analyst = cache.Analyst,
                FurnaceNo = cache.FurnaceNo,
                Grade = cache.Grade,
                AnalysisCount = cache.AnalysisCount,
                AnalysisStandard = cache.AnalysisStandard,
                C = cache.C,
                Si = cache.Si,
                Mn = cache.Mn,
                P = cache.P,
                S = cache.S,
                Ni = cache.Ni,
                Cr = cache.Cr,
                Mo = cache.Mo,
                Cu = cache.Cu,
                N = cache.N,
                Nb = cache.Nb,
                Ti = cache.Ti,
                Fe = cache.Fe,
                Al = cache.Al,
                W = cache.W
            };

            var result = await ChemicalAnalysisService.UpdateAsync(item.Id, request);
            if (result.Success && result.Data != null)
            {
                item.AnalysisDate = result.Data.AnalysisDate;
                item.Analyst = result.Data.Analyst;
                item.FurnaceNo = result.Data.FurnaceNo;
                item.Grade = result.Data.Grade;
                item.AnalysisCount = result.Data.AnalysisCount;
                item.AnalysisStandard = result.Data.AnalysisStandard;
                item.C = result.Data.C;
                item.Si = result.Data.Si;
                item.Mn = result.Data.Mn;
                item.P = result.Data.P;
                item.S = result.Data.S;
                item.Ni = result.Data.Ni;
                item.Cr = result.Data.Cr;
                item.Mo = result.Data.Mo;
                item.Cu = result.Data.Cu;
                item.N = result.Data.N;
                item.Nb = result.Data.Nb;
                item.Ti = result.Data.Ti;
                item.Fe = result.Data.Fe;
                item.Al = result.Data.Al;
                item.W = result.Data.W;
                item.UpdatedTime = result.Data.UpdatedTime;

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

    // ========== 单元格渲染 ==========

    private bool IsCellEditable(string key) => key switch
    {
        "AnalysisDate" or "Analyst" or "FurnaceNo" or "Grade"
            or "AnalysisCount" or "AnalysisStandard"
            or "C" or "Si" or "Mn" or "P" or "S"
            or "Ni" or "Cr" or "Mo" or "Cu" or "N"
            or "Nb" or "Ti" or "Fe" or "Al" or "W" => true,
        _ => false
    };

    private RenderFragment RenderCell(ChemicalAnalysisDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing ? _editCache.GetValueOrDefault(item.Id) : null;

        switch (col.Key)
        {
            case "AnalysisDate":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.AnalysisDate);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.AnalysisDate = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "Placeholder", "yyyy-MM-dd");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.AnalysisDate.ToString("yyyy-MM-dd"));
                }
                break;
            case "Analyst":
                RenderEditableString(builder, isEditing, cache?.Analyst, v => { if (cache != null) cache.Analyst = v; }, item.Analyst);
                break;
            case "FurnaceNo":
                RenderEditableString(builder, isEditing, cache?.FurnaceNo, v => { if (cache != null) cache.FurnaceNo = v; }, item.FurnaceNo);
                break;
            case "Grade":
                RenderEditableString(builder, isEditing, cache?.Grade, v => { if (cache != null) cache.Grade = v; }, item.Grade);
                break;
            case "AnalysisCount":
                RenderEditableInt(builder, isEditing, cache?.AnalysisCount, v => { if (cache != null) cache.AnalysisCount = v; }, item.AnalysisCount);
                break;
            case "AnalysisStandard":
                RenderEditableString(builder, isEditing, cache?.AnalysisStandard, v => { if (cache != null) cache.AnalysisStandard = v; }, item.AnalysisStandard);
                break;
            case "C":
            case "Si":
            case "Mn":
            case "P":
            case "S":
            case "Ni":
            case "Cr":
            case "Mo":
            case "Cu":
            case "N":
            case "Nb":
            case "Ti":
            case "Fe":
            case "Al":
            case "W":
                RenderEditableDecimal(builder, isEditing, cache, col.Key, item);
                break;
            case "UpdatedTime":
                builder.AddContent(0, item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    private void RenderEditableString(RenderTreeBuilder builder, bool isEditing, string? cacheValue, Action<string?> setter, string? displayValue)
    {
        if (isEditing)
        {
            builder.OpenComponent<MudTextField<string>>(0);
            builder.AddAttribute(1, "Value", cacheValue);
            builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, setter));
            builder.AddAttribute(3, "Class", "compact-input");
            builder.CloseComponent();
        }
        else
        {
            builder.AddContent(0, displayValue);
        }
    }

    private void RenderEditableInt(RenderTreeBuilder builder, bool isEditing, int? cacheValue, Action<int?> setter, int? displayValue)
    {
        if (isEditing)
        {
            builder.OpenComponent<MudNumericField<int?>>(0);
            builder.AddAttribute(1, "Value", cacheValue);
            builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, setter));
            builder.AddAttribute(3, "Class", "compact-input");
            builder.AddAttribute(4, "HideSpinButtons", true);
            builder.CloseComponent();
        }
        else
        {
            builder.AddContent(0, displayValue.HasValue ? displayValue.Value.ToString() : "");
        }
    }

    private void RenderEditableDecimal(RenderTreeBuilder builder, bool isEditing, EditCache? cache, string key, ChemicalAnalysisDto item)
    {
        var value = GetDecimalValue(item, key);
        if (isEditing && cache != null)
        {
            var cacheValue = GetDecimalFromCache(cache, key);
            builder.OpenComponent<MudNumericField<decimal?>>(0);
            builder.AddAttribute(1, "Value", cacheValue);
            builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, v => SetDecimalInCache(cache, key, v)));
            builder.AddAttribute(3, "Class", "compact-input");
            builder.AddAttribute(4, "HideSpinButtons", true);
            builder.AddAttribute(5, "Format", "G29");
            builder.CloseComponent();
        }
        else
        {
            builder.AddContent(0, value.HasValue ? value.Value.ToString("G29") : "");
        }
    }

    private static decimal? GetDecimalValue(ChemicalAnalysisDto item, string key) => key switch
    {
        "C" => item.C,
        "Si" => item.Si,
        "Mn" => item.Mn,
        "P" => item.P,
        "S" => item.S,
        "Ni" => item.Ni,
        "Cr" => item.Cr,
        "Mo" => item.Mo,
        "Cu" => item.Cu,
        "N" => item.N,
        "Nb" => item.Nb,
        "Ti" => item.Ti,
        "Fe" => item.Fe,
        "Al" => item.Al,
        "W" => item.W,
        _ => null
    };

    private static decimal? GetDecimalFromCache(EditCache cache, string key) => key switch
    {
        "C" => cache.C,
        "Si" => cache.Si,
        "Mn" => cache.Mn,
        "P" => cache.P,
        "S" => cache.S,
        "Ni" => cache.Ni,
        "Cr" => cache.Cr,
        "Mo" => cache.Mo,
        "Cu" => cache.Cu,
        "N" => cache.N,
        "Nb" => cache.Nb,
        "Ti" => cache.Ti,
        "Fe" => cache.Fe,
        "Al" => cache.Al,
        "W" => cache.W,
        _ => null
    };

    private static void SetDecimalInCache(EditCache cache, string key, decimal? value)
    {
        switch (key)
        {
            case "C": cache.C = value; break;
            case "Si": cache.Si = value; break;
            case "Mn": cache.Mn = value; break;
            case "P": cache.P = value; break;
            case "S": cache.S = value; break;
            case "Ni": cache.Ni = value; break;
            case "Cr": cache.Cr = value; break;
            case "Mo": cache.Mo = value; break;
            case "Cu": cache.Cu = value; break;
            case "N": cache.N = value; break;
            case "Nb": cache.Nb = value; break;
            case "Ti": cache.Ti = value; break;
            case "Fe": cache.Fe = value; break;
            case "Al": cache.Al = value; break;
            case "W": cache.W = value; break;
        }
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        if (!string.IsNullOrEmpty(_dateFrom))
            extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrEmpty(_dateTo))
            extras["dateTo"] = _dateTo;
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("chemical-analysis", state);
    }

    private void NavigateToCreate() => Navigation.NavigateTo("/quality/chemical-analysis/create");

    private async Task DeleteItem(ChemicalAnalysisDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除炉号 \"{item.FurnaceNo}\" 的化学检验记录吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await ChemicalAnalysisService.DeleteAsync(item.Id);
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

    // ========== 打印 ==========

    private List<PrintColumnDef> GetPrintColumnDefs() =>
        _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();

    private async Task PrintSelected()
    {
        if (!selectedIds.Any()) return;
        var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.ChemicalAnalysis}/print-batch-file";
        var request = new ChemicalAnalysisPrintBatchRequest
        {
            Ids = selectedIds.ToArray(),
            Columns = GetPrintColumnDefs()
        };
        var json = JsonSerializer.Serialize(request);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }

    private async Task PrintAll()
    {
        var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.ChemicalAnalysis}/print-all-file";
        var request = new ChemicalAnalysisPrintAllRequest
        {
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            SortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "analysisdate",
            IsDescending = sortDescending,
            InspectionDateFrom = DateTime.TryParse(_dateFrom, out var df) ? df : null,
            InspectionDateTo = DateTime.TryParse(_dateTo, out var dt) ? dt : null,
            Columns = GetPrintColumnDefs()
        };
        var json = JsonSerializer.Serialize(request);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }
}
