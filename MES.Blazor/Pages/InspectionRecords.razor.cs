using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Blazor.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages;

public partial class InspectionRecords
{
    private MudTable<InspectionRecordListDto>? table;
    private List<InspectionRecordListDto> _pageItems = new();
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
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    private string sortColumn = "id";
    private bool sortDescending = true;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "RecordNo",      Label = "记录号",   SortKey = "recordno", FilterType = "string" },
        new() { Key = "EquipmentName", Label = "设备名称",   SortKey = "equipmentname", FilterType = "string" },
        new() { Key = "EquipmentCode", Label = "设备编号",   SortKey = "equipmentcode", FilterType = "string" },
        new() { Key = "Location",       Label = "所在区域",   SortKey = "location", FilterType = "string" },
        new() { Key = "ActualDate",    Label = "实际日期",   SortKey = "actualdate", FilterType = "date" },
        new() { Key = "Inspector",     Label = "点检人",     SortKey = "inspector", FilterType = "string" },
        new() { Key = "ExecutionSummary", Label = "执行简述", SortKey = "executionsummary", FilterType = "string" },
        new() { Key = "Remark",        Label = "备注", SortKey = "remark", FilterType = "string" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<InspectionRecordListDto>> LoadDataFromServer(TableState state)
    {
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "id";
            var filtersJson = SerializeFilters();

            var result = await InspectionRecordService.GetPagedAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
                filters: filtersJson
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

        return new TableData<InspectionRecordListDto>
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
            var result = await InspectionRecordService.GetFilterContextsAsync();
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

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnUp(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnDown(ColumnDef col) => await SaveColumnPrefs();

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("inspection-records", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("inspection-records", null);
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

        // 从 PageState 恢复排序/筛选状态
        var savedState = await PageState.LoadAsync("inspectionrecords");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "id";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
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

        // 加载筛选上下文（ExcelFilter 下拉选项），完成后由表格触发首次数据加载
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#inspection-records-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 导航 ==========

    private void NavigateToCreate() => Navigation.NavigateTo("/inspection-records/create");

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, InspectionEditCache> _editCache = new();

    private class InspectionEditCache
    {
        public string? ActualDateText { get; set; }
        public string? Inspector { get; set; }
        public string? ExecutionSummaryText { get; set; }
        public string? Remark { get; set; }
    }

    private void StartEdit(InspectionRecordListDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new InspectionEditCache
        {
            ActualDateText = item.ActualDate?.ToString("yyyy-MM-dd"),
            Inspector = item.Inspector,
            ExecutionSummaryText = item.ExecutionSummary ?? "",
            Remark = item.Remark
        };
    }

    private void CancelEdit(int id)
    {
        _editingIds.Remove(id);
        _editCache.Remove(id);
    }

    private async Task SaveEdit(int id)
    {
        if (!_editCache.TryGetValue(id, out var cache)) return;

        var errors = new List<string>();

        DateTime? actualDate = null;
        if (!string.IsNullOrWhiteSpace(cache.ActualDateText))
        {
            if (DateTime.TryParse(cache.ActualDateText, out var parsedActual))
                actualDate = parsedActual;
            else
                errors.Add("实际日期格式无效");
        }

        if (errors.Any())
        {
            Snackbar.Add(string.Join("；", errors), Severity.Error);
            return;
        }

        try
        {
            var request = new UpdateInspectionRequest
            {
                ActualDate = actualDate,
                Inspector = cache.Inspector,
                ExecutionSummary = cache.ExecutionSummaryText,
                Remark = cache.Remark
            };

            var result = await InspectionRecordService.UpdateAsync(id, request);
            if (result.Success)
            {
                Snackbar.Add("保存成功", Severity.Success);
                _editingIds.Remove(id);
                _editCache.Remove(id);
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
    }

    // ========== 删除 ==========

    private async Task DeleteItem(InspectionRecordListDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除点检记录 \"{item.RecordNo}\" 吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await InspectionRecordService.DeleteAsync(item.Id);
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

    private RenderFragment RenderCell(InspectionRecordListDto item, ColumnDef col) => builder =>
    {
        if (_editingIds.Contains(item.Id) && _editCache.TryGetValue(item.Id, out var cache))
        {
            if (col.Key is "RecordNo" or "EquipmentName" or "EquipmentCode" or "Location")
            {
                RenderReadonlyCell(item, col)(builder);
                return;
            }
            RenderEditCell(cache, col)(builder);
            return;
        }

        switch (col.Key)
        {
            case "RecordNo":
                builder.AddContent(0, item.RecordNo);
                break;
            case "EquipmentName":
                builder.AddContent(0, item.EquipmentName);
                break;
            case "EquipmentCode":
                builder.AddContent(0, item.EquipmentCode);
                break;
            case "Location":
                builder.AddContent(0, item.Location);
                break;
            case "ActualDate":
                builder.AddContent(0, item.ActualDate?.ToString("yyyy-MM-dd"));
                break;
            case "Inspector":
                builder.AddContent(0, item.Inspector);
                break;
            case "ExecutionSummary":
                builder.AddContent(0, item.ExecutionSummary);
                break;
            case "Remark":
                builder.AddContent(0, item.Remark);
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    private RenderFragment RenderReadonlyCell(InspectionRecordListDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "RecordNo":
                builder.AddContent(0, item.RecordNo);
                break;
            case "EquipmentName":
                builder.AddContent(0, item.EquipmentName);
                break;
            case "EquipmentCode":
                builder.AddContent(0, item.EquipmentCode);
                break;
            case "Location":
                builder.AddContent(0, item.Location);
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    private RenderFragment RenderEditCell(InspectionEditCache cache, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "ActualDate":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.ActualDateText);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.ActualDateText = v));
                builder.AddAttribute(6, "Placeholder", "yyyy-MM-dd");
                builder.CloseComponent();
                break;
            case "Inspector":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.Inspector);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.Inspector = v));
                builder.CloseComponent();
                break;
            case "ExecutionSummary":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.ExecutionSummaryText);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.ExecutionSummaryText = v));
                builder.CloseComponent();
                break;
            case "Remark":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.Remark);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.Remark = v));
                builder.CloseComponent();
                break;
        }
    };

    // ========== 打印方法 ==========

    private List<PrintColumnDef> GetPrintColumnDefs()
    {
        return _visibleColumns.Select(c => new PrintColumnDef
        {
            Key = c.Key,
            Label = c.Label
        }).ToList();
    }

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的点检记录", Severity.Warning);
            return;
        }
        try
        {
            var ids = selectedIds.ToArray();
            var columns = GetPrintColumnDefs();
            var result = await InspectionRecordService.PrintBatchAsync(ids, columns);
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
            var columns = GetPrintColumnDefs();
            var query = new InspectionRecordQueryParams
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortColumn,
                IsDescending = sortDescending
            };
            var result = await InspectionRecordService.PrintAllAsync(query, columns);
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
        await PageState.SaveAsync("inspectionrecords", state);
    }
}
