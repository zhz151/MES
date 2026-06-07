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

public partial class OutsourceRecoveries
{
    private MudTable<OutsourceRecoveryDto>? table;
    private List<OutsourceRecoveryDto> _pageItems = new();
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

    private string sortColumn = "recoverydate";
    private bool sortDescending = true;

    // ========== 分页汇总 ==========
    private Dictionary<string, string> _pageSums = new();

    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "SendQuantity", "SendWeight",
        "RecoveryQuantity", "RecoveryWeight",
        "UnprocessedQuantity", "UnprocessedWeight",
    };

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private const string StorageKey = "outsource-recoveries";

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "BatchNo",             Label = "生产编号",     SortKey = "batchno",                FilterType = "string" },
        new() { Key = "OutsourceVendor",     Label = "委外单位",     SortKey = "outsourcevendor",        FilterType = "string" },
        new() { Key = "ProcessName",         Label = "工序名称",     SortKey = "processname",            FilterType = "string" },
        new() { Key = "SectionName",         Label = "工段名称",     SortKey = "sectionname",            FilterType = "string" },
        new() { Key = "ManufacturingSpec",   Label = "制造规格",     SortKey = "manufacturingspec",      FilterType = "string" },
        new() { Key = "OutsourceSpec",       Label = "委外规格",     SortKey = "outsourcespec",          FilterType = "string" },
        new() { Key = "SendQuantity",        Label = "发出支数",     SortKey = "sendquantity" },
        new() { Key = "SendWeight",          Label = "发出重量",     SortKey = "sendweight" },
        new() { Key = "TagNo",               Label = "挂牌号",       SortKey = "tagno",                  FilterType = "string" },
        new() { Key = "PlantGrade",          Label = "工厂牌号",     SortKey = "plantgrade",             FilterType = "string" },
        new() { Key = "RecoveryDate",        Label = "回收日期",     SortKey = "recoverydate",           FilterType = "date" },
        new() { Key = "RecoveryQuantity",    Label = "正常回收(支)", SortKey = "recoveryquantity" },
        new() { Key = "RecoveryWeight",      Label = "正常回收(重)", SortKey = "recoveryweight" },
        new() { Key = "UnprocessedQuantity", Label = "非正常回收(支)", SortKey = "unprocessedquantity" },
        new() { Key = "UnprocessedWeight",   Label = "非正常回收(重)", SortKey = "unprocessedweight" },
        new() { Key = "Remark",              Label = "备注",          SortKey = "remark",                 FilterType = "string" },
        new() { Key = "CreatedTime",         Label = "创建时间",     SortKey = "createdtime",            FilterType = "date" },
    };

    // ========== 分页汇总计算 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(OutsourceRecoveryDto)
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
            catch
            {
                // ignore individual column sum errors
            }
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        if (_pageSums.TryGetValue(col.Key, out var sum))
            return sum;
        return "-";
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<OutsourceRecoveryDto>> LoadDataFromServer(TableState state)
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

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "recoverydate";
            var filtersJson = SerializeFilters();

            var result = await SectionOutsourceService.GetRecoveriesPagedAsync(
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

        return new TableData<OutsourceRecoveryDto>
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
            var result = await SectionOutsourceService.GetRecoveryFilterContextsAsync();
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
        _allSelected = false;
        selectedIds.Clear();
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
        var savedState = await PageState.LoadAsync("outsourcerecoveries");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "recoverydate";
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
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#outsource-recoveries-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(OutsourceRecoveryDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "BatchNo":
                builder.AddContent(0, item.BatchNo ?? "");
                break;
            case "OutsourceVendor":
                builder.AddContent(0, item.OutsourceVendor ?? "");
                break;
            case "ProcessName":
                builder.AddContent(0, item.ProcessName ?? "");
                break;
            case "SectionName":
                builder.AddContent(0, item.SectionName ?? "");
                break;
            case "ManufacturingSpec":
                builder.AddContent(0, DisplayHelper.FormatSpecification(item.ManufacturingSpec ?? ""));
                break;
            case "OutsourceSpec":
                builder.AddContent(0, item.OutsourceSpec ?? "");
                break;
            case "SendQuantity":
                builder.AddContent(0, DisplayHelper.FormatNullableInt(item.SendQuantity));
                break;
            case "SendWeight":
                builder.AddContent(0, $"{(int)(item.SendWeight ?? 0)}");
                break;
            case "TagNo":
                builder.AddContent(0, item.TagNo ?? "");
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade ?? "");
                break;
            case "RecoveryDate":
                builder.AddContent(0, item.RecoveryDate.ToString("yyyy-MM-dd"));
                break;
            case "RecoveryQuantity":
                builder.AddContent(0, item.RecoveryQuantity ?? 0);
                break;
            case "RecoveryWeight":
                builder.AddContent(0, $"{(int)(item.RecoveryWeight ?? 0)}");
                break;
            case "UnprocessedQuantity":
                builder.AddContent(0, item.UnprocessedQuantity ?? 0);
                break;
            case "UnprocessedWeight":
                builder.AddContent(0, $"{(int)(item.UnprocessedWeight ?? 0)}");
                break;
            case "Remark":
                builder.AddContent(0, item.Remark ?? "");
                break;
            case "CreatedTime":
                builder.AddContent(0, item.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    // ========== GetCellRawValue / GetCellDisplayText ==========

    private string? GetCellRawValue(OutsourceRecoveryDto item, string key) => key switch
    {
        "BatchNo" => item.BatchNo,
        "OutsourceVendor" => item.OutsourceVendor,
        "ProcessName" => item.ProcessName,
        "SectionName" => item.SectionName,
        "ManufacturingSpec" => item.ManufacturingSpec,
        "OutsourceSpec" => item.OutsourceSpec,
        "SendQuantity" => item.SendQuantity?.ToString("G29"),
        "SendWeight" => $"{(int)(item.SendWeight ?? 0)}",
        "TagNo" => item.TagNo,
        "PlantGrade" => item.PlantGrade,
        "RecoveryDate" => item.RecoveryDate.ToString("yyyy-MM-dd"),
        "RecoveryQuantity" => item.RecoveryQuantity?.ToString("G29"),
        "RecoveryWeight" => $"{(int)(item.RecoveryWeight ?? 0)}",
        "UnprocessedQuantity" => item.UnprocessedQuantity?.ToString("G29"),
        "UnprocessedWeight" => $"{(int)(item.UnprocessedWeight ?? 0)}",
        "Remark" => item.Remark,
        "CreatedTime" => item.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        _ => null
    };

    // ========== 删除 ==========

    private async Task DeleteItem(OutsourceRecoveryDto item)
    {
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("确认删除", new DialogParameters
        {
            ["ContentText"] = $"确定要删除 {item.RecoveryDate:yyyy-MM-dd} 的回收记录吗？",
            ["ConfirmText"] = "删除"
        });

        var result = await dialog.Result;
        if (result.Canceled) return;

        var response = await SectionOutsourceService.DeleteRecoveryAsync(item.Id);
        if (response.Success)
        {
            Snackbar.Add("删除成功", Severity.Success);
            if (table != null) await table.ReloadServerData();
        }
        else
        {
            Snackbar.Add(response.Message, Severity.Error);
        }
    }

    // ========== 打印 ==========

    private async Task PrintSelected()
    {
        if (!selectedIds.Any()) return;

        var columns = _visibleColumns
            .Where(c => c.Key != "CreatedTime")
            .Select(c => new PrintColumnDef
            {
                Key = c.Key,
                Label = c.Label
            })
            .ToList();

        var result = await SectionOutsourceService.PrintRecoverySelectedAsync(selectedIds.ToArray(), columns);
        if (result.Success)
            await JS.InvokeVoidAsync("openPdf", result.Data);
        else
            Snackbar.Add(result.Message, Severity.Error);
    }

    private async Task PrintAll()
    {
        var columns = _visibleColumns
            .Where(c => c.Key != "CreatedTime")
            .Select(c => new PrintColumnDef
            {
                Key = c.Key,
                Label = c.Label
            })
            .ToList();

        var result = await SectionOutsourceService.PrintRecoveryAllAsync(
            keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword.Trim(),
            sortBy: sortColumn,
            isDescending: sortDescending,
            columns: columns,
            recoveryDateFrom: null,
            recoveryDateTo: null);
        if (result.Success)
            await JS.InvokeVoidAsync("openPdf", result.Data);
        else
            Snackbar.Add(result.Message, Severity.Error);
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
        await PageState.SaveAsync("outsourcerecoveries", state);
    }
}
