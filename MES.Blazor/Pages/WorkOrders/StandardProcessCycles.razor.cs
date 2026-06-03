using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.WorkOrders;

public partial class StandardProcessCycles
{
    private MudTable<StandardProcessCycleDto>? table;
    private List<StandardProcessCycleDto> _pageItems = new();
    private int _totalCount;
    private string _searchKeyword = string.Empty;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;

    // 排序状态
    private string sortColumn = "PlantGrade";
    private bool sortDescending = true;

    // 选中
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

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "PlantGrade",     Label = "工厂牌号",   SortKey = "plantgrade",     FilterType = "string", IsRequired = true },
        new() { Key = "RawMaterialType",Label = "原料类型",   SortKey = "rawmaterialtype",FilterType = "string", IsRequired = true },
        new() { Key = "RawSpec",        Label = "原料规格",   SortKey = "rawspec",        FilterType = "string", IsRequired = true },
        new() { Key = "ProductSpec",    Label = "成品规格",   SortKey = "productspec",    FilterType = "string", IsRequired = true },
        new() { Key = "DeliveryState",  Label = "交货状态",   SortKey = "deliverystate",  FilterType = "string", IsRequired = true },
        new() { Key = "StandardCycleDays", Label = "周期(天)",SortKey = "standardcycledays", FilterType = null, IsRequired = true },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<StandardProcessCycleDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "plantgrade";
            var filters = SerializeFilters();

            // 首次加载覆盖页码（MudTable 初始化时始终传 page=0）
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

            var result = await StandardProcessCycleService.GetPagedAsync(query);

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

        return new TableData<StandardProcessCycleDto>
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

    // ========== 筛选上下文加载 ==========

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await StandardProcessCycleService.GetFilterContextsAsync();
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
        await ColumnPrefs.SaveAsync("standard_process_cycles", null, _allColumns);
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
        var saved = await ColumnPrefs.LoadAsync("standard_process_cycles", null);
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
        var savedState = await PageState.LoadAsync("standard_process_cycles");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "PlantGrade";
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
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#standard-process-cycles-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 单元格渲染 ==========

    private string? GetCellRawValue(StandardProcessCycleDto item, string key) => key switch
    {
        "PlantGrade" => item.PlantGrade,
        "RawMaterialType" => item.RawMaterialType,
        "RawSpec" => item.RawSpec,
        "ProductSpec" => item.ProductSpec,
        "DeliveryState" => item.DeliveryState,
        "StandardCycleDays" => item.StandardCycleDays.ToString(),
        _ => null
    };

    private void NavigateToCreate() => Navigation.NavigateTo("/standard-process-cycles/create");

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string PlantGrade { get; set; } = string.Empty;
        public string RawMaterialType { get; set; } = string.Empty;
        public string RawSpec { get; set; } = string.Empty;
        public string ProductSpec { get; set; } = string.Empty;
        public string DeliveryState { get; set; } = string.Empty;
        public int StandardCycleDays { get; set; }
    }

    private void StartEdit(StandardProcessCycleDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            PlantGrade = item.PlantGrade,
            RawMaterialType = item.RawMaterialType,
            RawSpec = item.RawSpec,
            ProductSpec = item.ProductSpec,
            DeliveryState = item.DeliveryState,
            StandardCycleDays = item.StandardCycleDays
        };
    }

    private void CancelEdit(StandardProcessCycleDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(StandardProcessCycleDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.PlantGrade)) errors.Add("工厂牌号不能为空");
        if (string.IsNullOrWhiteSpace(cache.RawMaterialType)) errors.Add("原料类型不能为空");
        if (string.IsNullOrWhiteSpace(cache.RawSpec)) errors.Add("原料规格不能为空");
        if (string.IsNullOrWhiteSpace(cache.ProductSpec)) errors.Add("成品规格不能为空");
        if (string.IsNullOrWhiteSpace(cache.DeliveryState)) errors.Add("交货状态不能为空");
        if (cache.StandardCycleDays <= 0) errors.Add("周期天数必须大于0");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateStandardProcessCycleRequest
            {
                PlantGrade = cache.PlantGrade,
                RawMaterialType = cache.RawMaterialType,
                RawSpec = cache.RawSpec,
                ProductSpec = cache.ProductSpec,
                DeliveryState = cache.DeliveryState,
                StandardCycleDays = cache.StandardCycleDays
            };

            var result = await StandardProcessCycleService.UpdateAsync(item.Id, request);
            if (result.Success)
            {
                item.PlantGrade = cache.PlantGrade;
                item.RawMaterialType = cache.RawMaterialType;
                item.RawSpec = cache.RawSpec;
                item.ProductSpec = cache.ProductSpec;
                item.DeliveryState = cache.DeliveryState;
                item.StandardCycleDays = cache.StandardCycleDays;

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

    private async Task DeleteItem(StandardProcessCycleDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除\"{item.PlantGrade}/{item.RawSpec}/{item.ProductSpec}\" 的标准工艺生产周期吗？",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await StandardProcessCycleService.DeleteAsync(item.Id);
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

    private RenderFragment RenderCell(StandardProcessCycleDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing && _editCache.TryGetValue(item.Id, out var c) ? c : null;

        switch (col.Key)
        {
            case "PlantGrade":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.PlantGrade);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.PlantGrade = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.PlantGrade);
                }
                break;
            case "RawMaterialType":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.RawMaterialType);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.RawMaterialType = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.RawMaterialType);
                }
                break;
            case "RawSpec":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.RawSpec);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.RawSpec = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.RawSpec);
                }
                break;
            case "ProductSpec":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.ProductSpec);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.ProductSpec = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.ProductSpec);
                }
                break;
            case "DeliveryState":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.DeliveryState);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.DeliveryState = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.DeliveryState);
                }
                break;
            case "StandardCycleDays":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "HideSpinButtons", true);
                    builder.AddAttribute(4, "Value", cache.StandardCycleDays);
                    builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<int>(this, v => cache.StandardCycleDays = v));
                    builder.AddAttribute(6, "Class", "compact-input");
                    builder.AddAttribute(7, "Min", 1);
                    builder.AddAttribute(8, "Max", 365);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.StandardCycleDays.ToString());
                }
                break;
            default:
                builder.AddContent(0, "");
                break;
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
        await PageState.SaveAsync("standard_process_cycles", state);
    }
}
