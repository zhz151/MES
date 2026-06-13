using Microsoft.AspNetCore.Components;
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

namespace MES.Blazor.Pages.Batches;

public partial class PicklingOutRecords
{
    private MudTable<PicklingOutRecordDto>? table;
    private List<PicklingOutRecordDto> _pageItems = new();
    private int _totalCount;
    private bool _isArrowNavSetup;
    private int _currentPage;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    private string sortColumn = "completedate";
    private bool sortDescending = true;

    // ========== 内联编辑 ==========
    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();

    private class EditCache
    {
        public string? CompleteDateStr { get; set; }
        public string? Remark { get; set; }

        public string? OriginalCompleteDateStr { get; set; }
        public string? OriginalRemark { get; set; }
    }

    // ========== 分页汇总 ==========
    private Dictionary<string, string> _pageSums = new();

    private static readonly HashSet<string> _summableColumnKeys = new()
    {
    };

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private const string StorageKey = "pickling-out-records";

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "BatchNo",             Label = "生产编号",     SortKey = "batchno",          FilterType = "string" },
        new() { Key = "ProcessName",         Label = "工序名称",     SortKey = "processname",      FilterType = "string" },
        new() { Key = "SectionName",         Label = "工段名称",     SortKey = "sectionname",      FilterType = "string" },
        new() { Key = "ManufacturingSpec",   Label = "制造规格",     SortKey = "manufacturingspec", FilterType = "string" },
        new() { Key = "CompleteDate",        Label = "完工日期",     SortKey = "completedate",     FilterType = "date" },
        new() { Key = "TagNo",               Label = "挂牌号",       SortKey = "tagno",            FilterType = "string" },
        new() { Key = "PlantGrade",          Label = "工厂牌号",     SortKey = "plantgrade",       FilterType = "string" },
        new() { Key = "Remark",              Label = "备注",         SortKey = "remark",           FilterType = "string" },
        new() { Key = "DataSource",          Label = "数据来源",     SortKey = "datasource",       FilterType = "enum",
            EnumOptions = new() { new("SCAN", "扫码"), new("MANUAL", "手动") } },
        new() { Key = "UpdatedTime",         Label = "更新时间",     SortKey = "updatedtime",      FilterType = "date" },
    };

    // ========== 分页汇总计算 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(PicklingOutRecordDto)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);

        foreach (var col in _visibleColumns.Where(c => _summableColumnKeys.Contains(c.Key)))
        {
            if (!props.TryGetValue(col.Key, out var prop)) continue;

            var type = prop.PropertyType;
            try
            {
                if (type == typeof(int))
                {
                    var sum = _pageItems.Sum(item => (int)(prop.GetValue(item) ?? 0));
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal))
                {
                    var sum = _pageItems.Sum(item => (decimal)(prop.GetValue(item) ?? 0m));
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
                else if (type == typeof(int?))
                {
                    var sum = _pageItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal?))
                {
                    var sum = _pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
            }
            catch { }
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        if (_pageSums.TryGetValue(col.Key, out var sum))
            return sum;
        return "-";
    }

    // ========== 内联编辑 ==========

    private void StartEdit(PicklingOutRecordDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new EditCache
        {
            CompleteDateStr = item.CompleteDate.ToString("yyyy-MM-dd"),
            Remark = item.Remark,
            OriginalCompleteDateStr = item.CompleteDate.ToString("yyyy-MM-dd"),
            OriginalRemark = item.Remark
        };
    }

    private async Task SaveEdit(PicklingOutRecordDto item)
    {
        var cache = _editCache.GetValueOrDefault(item.Id);
        if (cache == null) return;

        DateTime? completeDate = null;
        if (!string.IsNullOrWhiteSpace(cache.CompleteDateStr))
        {
            if (!DateTime.TryParse(cache.CompleteDateStr, out var dt))
            {
                Snackbar.Add("完工日期格式无效，请使用 yyyy-MM-dd 格式", Severity.Warning);
                return;
            }
            completeDate = dt;
        }

        var request = new UpdatePicklingOutRecordRequest
        {
            CompleteDate = completeDate,
            Remark = cache.Remark
        };

        var result = await PicklingService.UpdateOutRecordAsync(item.Id, request);
        if (result.Success)
        {
            Snackbar.Add("保存成功", Severity.Success);
            _editingIds.Remove(item.Id);
            _editCache.Remove(item.Id);
            if (table != null) await table.ReloadServerData();
        }
        else
        {
            Snackbar.Add($"保存失败: {result.Message}", Severity.Error);
        }
    }

    private void CancelEdit(PicklingOutRecordDto item)
    {
        var cache = _editCache.GetValueOrDefault(item.Id);
        if (cache != null)
        {
            // 恢复原始值
        }
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<PicklingOutRecordDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "completedate";
            var filtersJson = SerializeFilters();

            var result = await PicklingService.GetOutRecordsPagedAsync(
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
                ComputePageSums();
            }
            else
            {
                _pageItems = new();
                _totalCount = 0;
            }

            await SavePageStateAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
        }

        return new TableData<PicklingOutRecordDto>
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
            var result = await PicklingService.GetOutRecordFilterContextsAsync();
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
        await ColumnPrefs.SaveAsync(StorageKey, null, _allColumns);
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

        var saved = await ColumnPrefs.LoadAsync(StorageKey, null);
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
        var savedState = await PageState.LoadAsync("picklingoutrecords");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "completedate";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = savedState.PageIndex;
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
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#pickling-out-records-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(PicklingOutRecordDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "BatchNo":
                builder.AddContent(0, item.BatchNo ?? "");
                break;
            case "ProcessName":
                builder.AddContent(0, item.ProcessName ?? "");
                break;
            case "SectionName":
                builder.AddContent(0, item.SectionName ?? "");
                break;
            case "ManufacturingSpec":
                builder.AddContent(0, item.ManufacturingSpec ?? "");
                break;
            case "CompleteDate":
                if (_editingIds.Contains(item.Id))
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Size", Size.Small);
                    builder.AddAttribute(4, "Value", _editCache[item.Id].CompleteDateStr);
                    builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => _editCache[item.Id].CompleteDateStr = v ?? ""));
                    builder.AddAttribute(6, "Placeholder", "yyyy-MM-dd");
                    builder.AddAttribute(7, "Style", "width:110px;");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.CompleteDate.ToString("yyyy-MM-dd"));
                }
                break;
            case "TagNo":
                builder.AddContent(0, item.TagNo ?? "");
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade ?? "");
                break;
            case "Remark":
                if (_editingIds.Contains(item.Id))
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Size", Size.Small);
                    builder.AddAttribute(4, "Value", _editCache[item.Id].Remark);
                    builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => _editCache[item.Id].Remark = v ?? ""));
                    builder.AddAttribute(7, "Style", "width:120px;");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Remark ?? "");
                }
                break;
            case "DataSource":
                var dsText = item.DataSource switch
                {
                    "SCAN" => "扫码",
                    "MANUAL" => "手动",
                    _ => item.DataSource ?? ""
                };
                builder.AddContent(0, dsText);
                break;
            case "UpdatedTime":
                builder.AddContent(0, item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    // ========== 删除 ==========

    private async Task DeleteItem(PicklingOutRecordDto item)
    {
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("确认删除", new DialogParameters
        {
            ["ContentText"] = $"确定要删除 {item.BatchNo} 在 {item.CompleteDate:yyyy-MM-dd} 的完工记录吗？",
            ["ConfirmText"] = "删除"
        });

        var result = await dialog.Result;
        if (result.Canceled) return;

        var response = await PicklingService.DeleteOutRecordAsync(item.Id);
        if (response.Success)
        {
            Snackbar.Add("删除成功，入缸状态已恢复为浸泡中", Severity.Success);
            if (table != null) await table.ReloadServerData();
        }
        else
        {
            Snackbar.Add(response.Message, Severity.Error);
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
        await PageState.SaveAsync("picklingoutrecords", state);
    }
}
