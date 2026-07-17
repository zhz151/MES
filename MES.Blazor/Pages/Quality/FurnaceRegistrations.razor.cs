using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Services;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Blazor.Models;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using System.Text.Json;

namespace MES.Blazor.Pages.Quality;

public partial class FurnaceRegistrations
{
    private MudTable<FurnaceRegistrationDto>? table;
    private List<FurnaceRegistrationDto> _pageItems = new();
    private int _totalCount;
    private bool _isArrowNavSetup;
    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;

    // 排序状态
    private string sortColumn = "furnacenumber";
    private bool sortDescending = false;

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new() { "Quantity", "Weight" };

    // ========== ExcelFilter 状态 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<EnumOption> GetRawMaterialTypeOptions() => DisplayHelper.GetEnumFilterOptions<MaterialType>();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "IncomingDate",     Label = "来料日期",     SortKey = "incomingdate", FilterType = "date", Width = "120" },
        new() { Key = "RawMaterialUnit",  Label = "原料单位",     SortKey = "rawmaterialunit", FilterType = "string", Width = "120", IsRequired = true },
        new() { Key = "RawMaterialType",  Label = "原料类型",     SortKey = "rawmaterialtype", FilterType = "enum", Width = "120", IsRequired = true, EnumOptions = GetRawMaterialTypeOptions() },
        new() { Key = "RegisteredGrade",  Label = "登记牌号",     SortKey = "registeredgrade", FilterType = "string", Width = "120", IsRequired = true },
        new() { Key = "RelatedPlantGrade",Label = "关联工厂牌号", SortKey = "relatedplantgrade", FilterType = "string", Width = "120" },
        new() { Key = "FurnaceNumber",    Label = "炉号",         SortKey = "furnacenumber", FilterType = "string", Width = "120", IsRequired = true },
        new() { Key = "Specification",    Label = "规格",         SortKey = "specification", FilterType = "string", Width = "120" },
        new() { Key = "Quantity",         Label = "支数",         SortKey = "quantity",        FilterType = "number", Width = "80" },
        new() { Key = "Weight",           Label = "重量",         SortKey = "weight",          FilterType = "number", Width = "80" },
        new() { Key = "Carbon",           Label = "C",            SortKey = "carbon",          FilterType = "number", Width = "80" },
        new() { Key = "Silicon",          Label = "Si",           SortKey = "silicon",         FilterType = "number", Width = "80" },
        new() { Key = "Manganese",        Label = "Mn",           SortKey = "manganese",       FilterType = "number", Width = "80" },
        new() { Key = "Phosphorus",       Label = "P",            SortKey = "phosphorus",      FilterType = "number", Width = "80" },
        new() { Key = "Sulfur",           Label = "S",            SortKey = "sulfur",          FilterType = "number", Width = "80" },
        new() { Key = "Nickel",           Label = "Ni",           SortKey = "nickel",          FilterType = "number", Width = "80" },
        new() { Key = "Chromium",         Label = "Cr",           SortKey = "chromium",        FilterType = "number", Width = "80" },
        new() { Key = "Molybdenum",       Label = "Mo",           SortKey = "molybdenum",      FilterType = "number", Width = "80" },
        new() { Key = "Copper",           Label = "Cu",           SortKey = "copper",          FilterType = "number", Width = "80" },
        new() { Key = "Nitrogen",         Label = "N",            SortKey = "nitrogen",        FilterType = "number", Width = "80" },
        new() { Key = "Niobium",          Label = "Nb",           SortKey = "niobium",         FilterType = "number", Width = "80" },
        new() { Key = "Titanium",         Label = "Ti",           SortKey = "titanium",        FilterType = "number", Width = "80" },
        new() { Key = "Iron",             Label = "Fe",           SortKey = "iron",            FilterType = "number", Width = "80" },
        new() { Key = "Aluminum",         Label = "Al",           SortKey = "aluminum",        FilterType = "number", Width = "80" },
        new() { Key = "Tungsten",         Label = "W",            SortKey = "tungsten",         FilterType = "number", Width = "80" },
        new() { Key = "PREN",             Label = "PREN腐蚀当量",  SortKey = "pren",             FilterType = "number", Width = "80" },
        new() { Key = "Remark",           Label = "备注",         SortKey = "remark", FilterType = "string", Width = "120" },
        new() { Key = "UpdatedTime",      Label = "更新日期",   SortKey = "updatedtime", Width = "120" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<FurnaceRegistrationDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            // 首次加载覆盖页码（MudTable 初始化时始终传 page=0）
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "furnacenumber";
            var filters = SerializeFilters();

            DateTime? dateFrom = null;
            DateTime? dateTo = null;
            if (DateTime.TryParse(_dateFrom, out var df)) dateFrom = df;
            if (DateTime.TryParse(_dateTo, out var dt)) dateTo = dt;

            var result = await FurnaceRegistrationService.GetAllAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
                incomingDateFrom: dateFrom,
                incomingDateTo: dateTo,
                filters: filters);

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
        }

        return new TableData<FurnaceRegistrationDto>
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
            var result = await FurnaceRegistrationService.GetFilterContextsAsync();
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

        // RawMaterialType 列显示中文
        if (_filterContextOptions.TryGetValue("RawMaterialType", out var rawMatOptions))
        {
            foreach (var opt in rawMatOptions)
            {
                opt.Display = DisplayHelper.GetMaterialTypeText(opt.Value);
            }
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

    // ========== 内联编辑 ==========

    private HashSet<int> selectedIds = new();
    private bool _allSelected;
    private bool allSelected
    {
        get => _allSelected;
        set
        {
            if (_allSelected == value) return;
            _allSelected = value;
            if (value) { foreach (var item in _pageItems) selectedIds.Add(item.Id); }
            else { selectedIds.Clear(); }
            StateHasChanged();
        }
    }

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public DateTime IncomingDate { get; set; }
        public string RawMaterialUnit { get; set; } = "";
        public string RawMaterialType { get; set; } = "";
        public string RegisteredGrade { get; set; } = "";
        public string? RelatedPlantGrade { get; set; }
        public string FurnaceNumber { get; set; } = "";
        public string? Specification { get; set; }
        public int? Quantity { get; set; }
        public decimal? Weight { get; set; }
        public decimal? Carbon { get; set; }
        public decimal? Silicon { get; set; }
        public decimal? Manganese { get; set; }
        public decimal? Phosphorus { get; set; }
        public decimal? Sulfur { get; set; }
        public decimal? Nickel { get; set; }
        public decimal? Chromium { get; set; }
        public decimal? Molybdenum { get; set; }
        public decimal? Copper { get; set; }
        public decimal? Nitrogen { get; set; }
        public decimal? Niobium { get; set; }
        public decimal? Titanium { get; set; }
        public decimal? Iron { get; set; }
        public decimal? Aluminum { get; set; }
        public decimal? Tungsten { get; set; }
        public decimal? PREN { get; set; }
        public string? Remark { get; set; }
    }

    private void StartEdit(FurnaceRegistrationDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new EditCache
        {
            IncomingDate = item.IncomingDate,
            RawMaterialUnit = item.RawMaterialUnit,
            RawMaterialType = item.RawMaterialType.ToString(),
            RegisteredGrade = item.RegisteredGrade,
            RelatedPlantGrade = item.RelatedPlantGrade,
            FurnaceNumber = item.FurnaceNumber,
            Specification = item.Specification,
            Quantity = item.Quantity,
            Weight = item.Weight,
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
            PREN = item.PREN,
            Remark = item.Remark
        };
    }

    private void CancelEdit(FurnaceRegistrationDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(FurnaceRegistrationDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        if (string.IsNullOrWhiteSpace(cache.FurnaceNumber))
        {
            Snackbar.Add("炉号不能为空", Severity.Error);
            return;
        }
        if (string.IsNullOrWhiteSpace(cache.RawMaterialUnit))
        {
            Snackbar.Add("原料单位不能为空", Severity.Error);
            return;
        }
        if (string.IsNullOrWhiteSpace(cache.RawMaterialType))
        {
            Snackbar.Add("原料类型不能为空", Severity.Error);
            return;
        }
        if (string.IsNullOrWhiteSpace(cache.RegisteredGrade))
        {
            Snackbar.Add("登记牌号不能为空", Severity.Error);
            return;
        }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateFurnaceRegistrationRequest
            {
                IncomingDate = cache.IncomingDate,
                RawMaterialUnit = cache.RawMaterialUnit,
                RawMaterialType = Enum.TryParse<MaterialType>(cache.RawMaterialType, true, out var rm) ? rm : default,
                RegisteredGrade = cache.RegisteredGrade,
                RelatedPlantGrade = cache.RelatedPlantGrade,
                FurnaceNumber = cache.FurnaceNumber,
                Specification = cache.Specification,
                Quantity = cache.Quantity,
                Weight = cache.Weight,
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
                PREN = cache.PREN,
                Remark = cache.Remark
            };

            var result = await FurnaceRegistrationService.UpdateAsync(item.Id, request);
            if (result.Success && result.Data != null)
            {
                item.IncomingDate = result.Data.IncomingDate;
                item.RawMaterialUnit = result.Data.RawMaterialUnit;
                item.RawMaterialType = result.Data.RawMaterialType;
                item.RegisteredGrade = result.Data.RegisteredGrade;
                item.RelatedPlantGrade = result.Data.RelatedPlantGrade;
                item.FurnaceNumber = result.Data.FurnaceNumber;
                item.Specification = result.Data.Specification;
                item.Quantity = result.Data.Quantity;
                item.Weight = result.Data.Weight;
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
                item.Remark = result.Data.Remark;
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
        await ColumnPrefs.SaveAsync("furnace-registration", null, _allColumns);
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
        var saved = await ColumnPrefs.LoadAsync("furnace-registration", null);
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
        var savedState = await PageState.LoadAsync("furnace-registration");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "furnacenumber";
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

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && table != null)
            await table.ReloadServerData();

        // 加载筛选上下文
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#furnace-registration-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== ExcelFilter 回调 ==========

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);

        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }


    // ========== 搜索 ==========

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

    // ========== 单元格原始值/显示值 ==========

    private string? GetCellRawValue(FurnaceRegistrationDto item, string key) => key switch
    {
        "IncomingDate" => item.IncomingDate.ToString("yyyy-MM-dd"),
        "RawMaterialUnit" => item.RawMaterialUnit,
        "RawMaterialType" => DisplayHelper.GetMaterialTypeText(item.RawMaterialType),
        "RegisteredGrade" => item.RegisteredGrade,
        "RelatedPlantGrade" => item.RelatedPlantGrade,
        "FurnaceNumber" => item.FurnaceNumber,
        "Specification" => item.Specification,
        "Quantity" => item.Quantity?.ToString(),
        "Weight" => DisplayHelper.FormatNullableDecimalAsInt(item.Weight),
        "Carbon" => item.Carbon?.ToString("G29"),
        "Silicon" => item.Silicon?.ToString("G29"),
        "Manganese" => item.Manganese?.ToString("G29"),
        "Phosphorus" => item.Phosphorus?.ToString("G29"),
        "Sulfur" => item.Sulfur?.ToString("G29"),
        "Nickel" => item.Nickel?.ToString("G29"),
        "Chromium" => item.Chromium?.ToString("G29"),
        "Molybdenum" => item.Molybdenum?.ToString("G29"),
        "Copper" => item.Copper?.ToString("G29"),
        "Nitrogen" => item.Nitrogen?.ToString("G29"),
        "Niobium" => item.Niobium?.ToString("G29"),
        "Titanium" => item.Titanium?.ToString("G29"),
        "Iron" => item.Iron?.ToString("G29"),
        "Aluminum" => item.Aluminum?.ToString("G29"),
        "Tungsten" => item.Tungsten?.ToString("G29"),
        "PREN" => item.PREN?.ToString("G29"),
        "Remark" => item.Remark,
        "UpdatedTime" => item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        _ => null
    };

    private string? GetCellDisplayText(FurnaceRegistrationDto item, string key) => key switch
    {
        _ => GetCellRawValue(item, key) ?? ""
    };

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
        await PageState.SaveAsync("furnace-registration", state);
    }

    private void NavigateToCreate() => Navigation.NavigateTo("/quality/furnace/create");
    // ========== 打印 ==========

    private List<PrintColumnDef> GetPrintColumnDefs() =>
        _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();

    private async Task PrintSelected()
    {
        if (!selectedIds.Any()) return;
        var apiUrl = $"{Http.BaseAddress}api/furnace-registration/print-batch-file";
        var request = new FurnaceRegistrationPrintBatchRequest
        {
            Ids = selectedIds.ToArray(),
            Columns = GetPrintColumnDefs()
        };
        var json = JsonSerializer.Serialize(request);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }

    private async Task PrintAll()
    {
        var apiUrl = $"{Http.BaseAddress}api/furnace-registration/print-all-file";

        DateTime? dateFrom = null;
        DateTime? dateTo = null;
        if (DateTime.TryParse(_dateFrom, out var df)) dateFrom = df;
        if (DateTime.TryParse(_dateTo, out var dt)) dateTo = dt;

        var request = new FurnaceRegistrationPrintAllRequest
        {
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            SortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "furnacenumber",
            IsDescending = sortDescending,
            Columns = GetPrintColumnDefs(),
            IncomingDateFrom = dateFrom,
            IncomingDateTo = dateTo
        };
        var json = JsonSerializer.Serialize(request);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }

    private async Task DeleteItem(FurnaceRegistrationDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除炉号 \"{item.FurnaceNumber}\" 的记录吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await FurnaceRegistrationService.DeleteAsync(item.Id);
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

    private RenderFragment RenderCell(FurnaceRegistrationDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing ? _editCache.GetValueOrDefault(item.Id) : null;

        switch (col.Key)
        {
            case "IncomingDate":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Size", Size.Small);
                    builder.AddAttribute(4, "Class", "compact-input");
                    builder.AddAttribute(5, "Value", cache.IncomingDate.ToString("yyyy-MM-dd"));
                    builder.AddAttribute(6, "ValueChanged", EventCallback.Factory.Create<string?>(this, v =>
                    {
                        if (DateTime.TryParse(v, out var dt)) cache.IncomingDate = dt;
                    }));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.IncomingDate.ToString("yyyy-MM-dd"));
                }
                break;
            case "RawMaterialUnit":
                RenderEditField(builder, isEditing, cache?.RawMaterialUnit, v => { if (cache != null) cache.RawMaterialUnit = v ?? ""; }, item.RawMaterialUnit);
                break;
            case "RawMaterialType":
                RenderEditField(builder, isEditing, cache?.RawMaterialType, v => { if (cache != null) cache.RawMaterialType = v ?? ""; }, DisplayHelper.GetMaterialTypeText(item.RawMaterialType));
                break;
            case "RegisteredGrade":
                RenderEditField(builder, isEditing, cache?.RegisteredGrade, v => { if (cache != null) cache.RegisteredGrade = v ?? ""; }, item.RegisteredGrade);
                break;
            case "RelatedPlantGrade":
                RenderEditField(builder, isEditing, cache?.RelatedPlantGrade, v => { if (cache != null) cache.RelatedPlantGrade = v; }, item.RelatedPlantGrade);
                break;
            case "FurnaceNumber":
                RenderEditField(builder, isEditing, cache?.FurnaceNumber, v => { if (cache != null) cache.FurnaceNumber = v ?? ""; }, item.FurnaceNumber);
                break;
            case "Specification":
                RenderEditField(builder, isEditing, cache?.Specification, v => { if (cache != null) cache.Specification = v; }, item.Specification);
                break;
            case "Quantity":
                RenderQuantityCell(builder, isEditing, cache, item);
                break;
            case "Weight":
                RenderWeightCell(builder, isEditing, cache, item);
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
            case "PREN":
                RenderDecimalCell(builder, isEditing, cache, item, col.Key);
                break;
            case "Remark":
                RenderEditField(builder, isEditing, cache?.Remark, v => { if (cache != null) cache.Remark = v; }, item.Remark);
                break;
            case "UpdatedTime":
                builder.AddContent(0, item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    private void RenderQuantityCell(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, bool isEditing, EditCache? cache, FurnaceRegistrationDto item)
    {
        if (isEditing && cache != null)
        {
            builder.OpenComponent<MudNumericField<int?>>(0);
            builder.AddAttribute(1, "Dense", true);
            builder.AddAttribute(2, "Variant", Variant.Outlined);
            builder.AddAttribute(3, "Size", Size.Small);
            builder.AddAttribute(4, "Class", "compact-input");
            builder.AddAttribute(5, "HideSpinButtons", true);
            builder.AddAttribute(6, "Value", cache.Quantity);
            builder.AddAttribute(7, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.Quantity = v));
            builder.CloseComponent();
        }
        else
        {
            builder.AddContent(0, item.Quantity?.ToString());
        }
    }

    private void RenderWeightCell(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, bool isEditing, EditCache? cache, FurnaceRegistrationDto item)
    {
        if (isEditing && cache != null)
        {
            builder.OpenComponent<MudNumericField<decimal?>>(0);
            builder.AddAttribute(1, "Dense", true);
            builder.AddAttribute(2, "Variant", Variant.Outlined);
            builder.AddAttribute(3, "Size", Size.Small);
            builder.AddAttribute(4, "Class", "compact-input");
            builder.AddAttribute(5, "Format", "G29");
            builder.AddAttribute(6, "HideSpinButtons", true);
            builder.AddAttribute(7, "Value", cache.Weight);
            builder.AddAttribute(8, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, v => cache.Weight = v));
            builder.CloseComponent();
        }
        else
        {
            builder.AddContent(0, DisplayHelper.FormatNullableDecimalAsInt(item.Weight));
        }
    }

    private void RenderDecimalCell(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, bool isEditing, EditCache? cache, FurnaceRegistrationDto item, string key)
    {
        decimal? editValue = key switch
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
            "PREN" => cache?.PREN,
            _ => null
        };

        if (isEditing && cache != null)
        {
            builder.OpenComponent<MudNumericField<decimal?>>(0);
            builder.AddAttribute(1, "Dense", true);
            builder.AddAttribute(2, "Variant", Variant.Outlined);
            builder.AddAttribute(3, "Size", Size.Small);
            builder.AddAttribute(4, "Class", "compact-input");
            builder.AddAttribute(5, "Format", "G29");
            builder.AddAttribute(6, "HideSpinButtons", true);
            builder.AddAttribute(7, "Value", editValue);
            builder.AddAttribute(8, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, v =>
            {
                if (cache == null) return;
                switch (key)
                {
                    case "Carbon": cache.Carbon = v; break;
                    case "Silicon": cache.Silicon = v; break;
                    case "Manganese": cache.Manganese = v; break;
                    case "Phosphorus": cache.Phosphorus = v; break;
                    case "Sulfur": cache.Sulfur = v; break;
                    case "Nickel": cache.Nickel = v; break;
                    case "Chromium": cache.Chromium = v; break;
                    case "Molybdenum": cache.Molybdenum = v; break;
                    case "Copper": cache.Copper = v; break;
                    case "Nitrogen": cache.Nitrogen = v; break;
                    case "Niobium": cache.Niobium = v; break;
                    case "Titanium": cache.Titanium = v; break;
                    case "Iron": cache.Iron = v; break;
                    case "Aluminum": cache.Aluminum = v; break;
                    case "Tungsten": cache.Tungsten = v; break;
                    case "PREN": cache.PREN = v; break;
                }
            }));
            builder.CloseComponent();
        }
        else
        {
            var display = key switch
            {
                "Carbon" => item.Carbon?.ToString("G29"),
                "Silicon" => item.Silicon?.ToString("G29"),
                "Manganese" => item.Manganese?.ToString("G29"),
                "Phosphorus" => item.Phosphorus?.ToString("G29"),
                "Sulfur" => item.Sulfur?.ToString("G29"),
                "Nickel" => item.Nickel?.ToString("G29"),
                "Chromium" => item.Chromium?.ToString("G29"),
                "Molybdenum" => item.Molybdenum?.ToString("G29"),
                "Copper" => item.Copper?.ToString("G29"),
                "Nitrogen" => item.Nitrogen?.ToString("G29"),
                "Niobium" => item.Niobium?.ToString("G29"),
                "Titanium" => item.Titanium?.ToString("G29"),
                "Iron" => item.Iron?.ToString("G29"),
                "Aluminum" => item.Aluminum?.ToString("G29"),
                "Tungsten" => item.Tungsten?.ToString("G29"),
                "PREN" => item.PREN?.ToString("G29"),
                _ => null
            };
            builder.AddContent(0, display);
        }
    }

    private void RenderEditField(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, bool isEditing, string? editValue, Action<string?> setter, string? displayValue)
    {
        if (isEditing)
        {
            builder.OpenComponent<MudTextField<string>>(0);
            builder.AddAttribute(1, "Dense", true);
            builder.AddAttribute(2, "Variant", Variant.Outlined);
            builder.AddAttribute(3, "Size", Size.Small);
            builder.AddAttribute(4, "Class", "compact-input");
            builder.AddAttribute(5, "Value", editValue);
            builder.AddAttribute(6, "ValueChanged", EventCallback.Factory.Create<string?>(this, setter));
            builder.CloseComponent();
        }
        else
        {
            builder.AddContent(0, displayValue);
        }
    }

    // ========== 分页汇总（B33） ==========
    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;
        var props = typeof(FurnaceRegistrationDto)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);
        foreach (var key in _summableColumnKeys)
        {
            if (!props.TryGetValue(key, out var prop)) continue;
            var type = prop.PropertyType;
            try
            {
                if (type == typeof(decimal?))
                {
                    var sum = _pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[key] = ((int)sum).ToString();
                }
                else if (type == typeof(int?))
                {
                    var sum = _pageItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[key] = sum.ToString();
                }
            }
            catch { }
        }
    }
    private string RenderFooterCell(ColumnDef col)
    {
        return _pageSums.GetValueOrDefault(col.Key, "");
    }
}
