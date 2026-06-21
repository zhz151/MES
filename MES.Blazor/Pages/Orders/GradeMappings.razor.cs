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

namespace MES.Blazor.Pages.Orders;

public partial class GradeMappings
{
    private MudTable<StandardGradeMappingDto>? table;
    private List<StandardGradeMappingDto> _pageItems = new();
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
        new() { Key = "StandardGrade",  Label = "标准牌号",   SortKey = "standardgrade",  FilterType = "string", IsRequired = true },
        new() { Key = "StandardGradeCategory", Label = "标准牌号类别", SortKey = "standardgradecategory", FilterType = "string" },
        new() { Key = "PlantGrade",     Label = "工厂牌号",   SortKey = "plantgrade",     FilterType = "string", IsRequired = true },
        new() { Key = "Density",        Label = "密度(g/cm³)",SortKey = "density",        FilterType = null, IsRequired = true },
        new() { Key = "HeatTreatment",  Label = "热处理工艺", SortKey = "heattreatment",  FilterType = "string" },
        new() { Key = "SpecialMaterial",Label = "特殊材料",   SortKey = "specialmaterial",FilterType = "boolean", BoolTrueLabel = "特殊材料", BoolFalseLabel = "常规" },
        new() { Key = "SpecialNote",    Label = "特殊注意事项", SortKey = "specialnote",          FilterType = "string" },
        new() { Key = "SteelProperty",  Label = "钢性",        SortKey = "steelproperty",        FilterType = "string" },
        new() { Key = "Remark",         Label = "备注",         SortKey = "remark",              FilterType = "string" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<StandardGradeMappingDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "standardgrade";
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

            var result = await GradeMappingService.GetPagedAsync(query);

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

        return new TableData<StandardGradeMappingDto>
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
            var result = await GradeMappingService.GetFilterContextsAsync();
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

        // SpecialMaterial 列显示中文
        if (_filterContextOptions.TryGetValue("SpecialMaterial", out var specialOptions))
        {
            foreach (var opt in specialOptions)
            {
                opt.Display = opt.Value == "True" ? "特殊材料" : "常规";
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

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("grade_mappings", null, _allColumns);
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
        var saved = await ColumnPrefs.LoadAsync("grade_mappings", null);
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
        var savedState = await PageState.LoadAsync("grade_mappings");
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
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#grade-mappings-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 单元格渲染 ==========

    private string? GetCellRawValue(StandardGradeMappingDto item, string key) => key switch
    {
        "StandardGrade" => item.StandardGrade,
        "StandardGradeCategory" => item.StandardGradeCategory,
        "PlantGrade" => item.PlantGrade,
        "Density" => item.Density.ToString("G29"),
        "HeatTreatment" => item.HeatTreatment,
        "SpecialMaterial" => item.SpecialMaterial.ToString(),
        "SpecialNote" => item.SpecialNote,
        "SteelProperty" => item.SteelProperty,
        "Remark" => item.Remark,
        _ => null
    };

    private void NavigateToCreate() => Navigation.NavigateTo("/grade-mappings/create");

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string StandardGrade { get; set; } = string.Empty;
        public string? StandardGradeCategory { get; set; }
        public string PlantGrade { get; set; } = string.Empty;
        public decimal? Density { get; set; }
        public string? HeatTreatment { get; set; }
        public bool SpecialMaterial { get; set; }
        public string? SpecialNote { get; set; }
        public string? Remark { get; set; }
    }

    private void StartEdit(StandardGradeMappingDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            StandardGrade = item.StandardGrade,
            StandardGradeCategory = item.StandardGradeCategory,
            PlantGrade = item.PlantGrade,
            Density = item.Density,
            HeatTreatment = item.HeatTreatment,
            SpecialMaterial = item.SpecialMaterial,
            SpecialNote = item.SpecialNote,
            Remark = item.Remark
        };
    }

    private void CancelEdit(StandardGradeMappingDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(StandardGradeMappingDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.StandardGrade)) errors.Add("标准牌号不能为空");
        if (string.IsNullOrWhiteSpace(cache.PlantGrade)) errors.Add("工厂牌号不能为空");
        if (cache.Density == null || cache.Density <= 0) errors.Add("密度必须大于0");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateGradeMappingRequest
            {
                StandardGrade = cache.StandardGrade,
                StandardGradeCategory = cache.StandardGradeCategory,
                PlantGrade = cache.PlantGrade,
                Density = cache.Density,
                HeatTreatment = cache.HeatTreatment,
                SpecialMaterial = cache.SpecialMaterial,
                SpecialNote = cache.SpecialNote,
                Remark = cache.Remark
            };

            var result = await GradeMappingService.UpdateAsync(item.Id, request);
            if (result.Success)
            {
                item.StandardGrade = cache.StandardGrade;
                item.StandardGradeCategory = cache.StandardGradeCategory;
                item.PlantGrade = cache.PlantGrade;
                if (cache.Density.HasValue) item.Density = cache.Density.Value;
                item.HeatTreatment = cache.HeatTreatment;
                item.SpecialMaterial = cache.SpecialMaterial;
                item.SpecialNote = cache.SpecialNote;
                item.Remark = cache.Remark;

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

    private async Task DeleteItem(StandardGradeMappingDto mapping)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除牌号对照 \"{mapping.StandardGrade}\" 吗？\n\n如果该牌号已被订单使用，删除后可能导致数据问题！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await GradeMappingService.DeleteAsync(mapping.Id);
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

    private RenderFragment RenderCell(StandardGradeMappingDto item, ColumnDef col) => builder =>
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
            case "HeatTreatment":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.HeatTreatment);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.HeatTreatment = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.HeatTreatment);
                }
                break;
            case "SpecialMaterial":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudSwitch<bool>>(0);
                    builder.AddAttribute(1, "Value", cache.SpecialMaterial);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<bool>(this, v => cache.SpecialMaterial = v));
                    builder.AddAttribute(3, "Dense", true);
                    builder.AddAttribute(4, "Color", Color.Success);
                    builder.CloseComponent();
                }
                else
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", item.SpecialMaterial ? Color.Warning : Color.Default);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.SpecialMaterial ? DisplayHelper.GetRequirementTypeText(RequirementType.Special) : DisplayHelper.GetRequirementTypeText(RequirementType.Normal))));
                    builder.CloseComponent();
                }
                break;
            case "SpecialNote":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.SpecialNote);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.SpecialNote = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.SpecialNote);
                }
                break;
            case "SteelProperty":
                builder.AddContent(0, item.SteelProperty);
                break;
            case "Remark":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.Remark);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.Remark = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Remark);
                }
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    // ========== 打印方法 ==========

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的牌号", Severity.Warning);
            return;
        }
        try
        {
            var ids = selectedIds.ToArray();
            var result = await GradeMappingService.PrintGradeMappingBatchAsync(ids);
            if (result.Success && result.Data != null)
                await JS.InvokeVoidAsync("openPdf", result.Data);
            else
                Snackbar.Add(result.Message ?? "打印失败", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task PrintAll()
    {
        try
        {
            var result = await GradeMappingService.PrintGradeMappingAllAsync(
                string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortColumn, sortDescending);
            if (result.Success && result.Data != null)
                await JS.InvokeVoidAsync("openPdf", result.Data);
            else
                Snackbar.Add(result.Message ?? "打印失败", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
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
        await PageState.SaveAsync("grade_mappings", state);
    }
}
