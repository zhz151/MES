using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.Scheduling;

public partial class SectionFlowAnalysis
{
    private MudTable<SectionFlowAnalysisDto>? table;
    private List<SectionFlowAnalysisDto> _allItems = new();
    private List<SectionFlowAnalysisDto> _filteredItems = new();
    private bool _isLoading;

    // ========== 页面状态持久化 ==========
    private int _restoredPageIndex;
    private int _currentPageIndex = 1;
    private int _pageSize = 25;
    private string _searchKeyword = string.Empty;

    // ========== 排序状态 ==========
    private string sortColumn = "Category";
    private bool sortDescending = false;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "PendingTotal",
    };
    private int _lastSummedPage = -1;
    private int _lastSummedCount = -1;
    private int _lastSummedPageSize = -1;

    // 非空/空筛选常量
    private const string FilterNotNull = "__NOT_NULL__";
    private const string FilterNull = "__EXCEL_FILTER_NULL__";

    // ========== 列定义 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs()
    {
        return new List<ColumnDef>
        {
            new() { Key = "Category",        Label = "段落类别",   SortKey = "category",        FilterType = "string", IsRequired = true },
            new() { Key = "PendingTotal",    Label = "段落待产总量", SortKey = "pendingtotal",  FilterType = "number" },
            new() { Key = "SustainableDays", Label = "可持续天数",  SortKey = "sustainabledays",FilterType = "number" },
            new() { Key = "StatusJudgment",  Label = "状态判定",   SortKey = "statusjudgment",  FilterType = "string", IsRequired = true },
        };
    }

    // ========== 生命周期 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();

        // 恢复列显隐偏好
        var saved = await ColumnPrefs.LoadAsync("section-flow-analysis", null);
        if (saved.Count > 0)
        {
            foreach (var s in saved)
            {
                var match = _allColumns.FirstOrDefault(c => c.Key == s.Key);
                if (match != null) match.Visible = s.Visible;
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

        // 恢复排序/搜索/筛选状态
        var savedState = await PageState.LoadAsync("section-flow-analysis");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "Category";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? "";
            _restoredPageIndex = savedState.PageIndex;

            // 恢复列筛选状态
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

            // 恢复分页行数
            if (savedState.Extras?.ContainsKey("pageSize") == true)
            {
                if (int.TryParse(savedState.Extras["pageSize"], out var ps))
                    _pageSize = ps;
            }
        }

        // 状态恢复后重新加载数据
        if (savedState != null && table != null)
            await table.ReloadServerData();

        await LoadDataAsync();
    }

    // ========== 数据加载 ==========

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        try
        {
            var result = await Service.GetAnalysisAsync();
            if (result.Success && result.Data != null)
            {
                _allItems = result.Data;
            }
            else
            {
                Snackbar.Add(result.Message ?? "获取数据失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }

        BuildFilterContextOptions();
        ApplyFiltersAndSort();
    }

    // ========== 筛选上下文构建 ==========

    private void BuildFilterContextOptions()
    {
        _filterContextOptions.Clear();

        foreach (var col in _allColumns)
        {
            if (col.FilterType == "number")
            {
                _filterContextOptions[col.Key] = new List<ExcelFilterOption>
                {
                    new() { Value = FilterNotNull, Display = "非空", Count = _allItems.Count(x => GetDecimalValue(x, col.Key).HasValue) },
                    new() { Value = FilterNull,    Display = "空",   Count = _allItems.Count(x => !GetDecimalValue(x, col.Key).HasValue) },
                };
            }
            else if (col.FilterType == "string")
            {
                var options = _allItems
                    .Select(item => GetStringValue(item, col.Key))
                    .Where(v => v != null)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .Select(val => new ExcelFilterOption
                    {
                        Value = val!,
                        Display = val!,
                        Count = _allItems.Count(x => string.Equals(GetStringValue(x, col.Key), val, StringComparison.OrdinalIgnoreCase))
                    })
                    .ToList();
                _filterContextOptions[col.Key] = options;
            }
        }
    }

    private static string? GetStringValue(SectionFlowAnalysisDto item, string key) => key switch
    {
        "Category" => $"{item.CategoryCode} {item.CategoryName}".Trim(),
        "StatusJudgment" => item.StatusJudgment,
        _ => null
    };

    private static decimal? GetDecimalValue(SectionFlowAnalysisDto item, string key) => key switch
    {
        "PendingTotal" => item.PendingTotal,
        "SustainableDays" => item.SustainableDays,
        _ => null
    };

    // ========== 搜索 ==========

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        ApplyFiltersAndSort();
        await SavePageStateAsync();
    }

    // ========== 筛选和排序 ==========

    private void ApplyFiltersAndSort()
    {
        var filtered = _allItems.AsEnumerable();

        // 关键词搜索
        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            var kw = _searchKeyword.Trim();
            filtered = filtered.Where(x =>
                (x.CategoryCode?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.CategoryName?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.StatusJudgment?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true));
        }

        // 列筛选
        foreach (var kvp in _columnFilters)
        {
            if (kvp.Value.Count == 0) continue;

            var col = _allColumns.FirstOrDefault(c => c.Key == kvp.Key);
            if (col == null) continue;

            if (col.FilterType == "number")
            {
                var hasNotNull = kvp.Value.Contains(FilterNotNull);
                var hasNull = kvp.Value.Contains(FilterNull);

                if (hasNotNull && !hasNull)
                    filtered = filtered.Where(x => GetDecimalValue(x, kvp.Key).HasValue);
                else if (hasNull && !hasNotNull)
                    filtered = filtered.Where(x => !GetDecimalValue(x, kvp.Key).HasValue);
            }
            else if (col.FilterType == "string")
            {
                filtered = filtered.Where(x =>
                {
                    var val = GetStringValue(x, kvp.Key);
                    return val != null && kvp.Value.Contains(val, StringComparer.OrdinalIgnoreCase);
                });
            }
        }

        // 内存排序
        filtered = sortColumn switch
        {
            "Category" => sortDescending
                ? filtered.OrderByDescending(x => x.CategoryCode)
                : filtered.OrderBy(x => x.CategoryCode),
            "PendingTotal" => sortDescending
                ? filtered.OrderByDescending(x => x.PendingTotal)
                : filtered.OrderBy(x => x.PendingTotal),
            "SustainableDays" => sortDescending
                ? filtered.OrderByDescending(x => x.SustainableDays)
                : filtered.OrderBy(x => x.SustainableDays),
            "StatusJudgment" => sortDescending
                ? filtered.OrderByDescending(x => x.StatusJudgment)
                : filtered.OrderBy(x => x.StatusJudgment),
            _ => filtered.OrderBy(x => x.CategoryCode)
        };

        _filteredItems = filtered.ToList();
        ComputePageSums();
    }

    private async Task ToggleSort(string key)
    {
        if (sortColumn == key)
            sortDescending = !sortDescending;
        else
        {
            sortColumn = key;
            sortDescending = false;
        }
        ApplyFiltersAndSort();
        await SavePageStateAsync();
    }

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);

        ApplyFiltersAndSort();
        await SavePageStateAsync();
    }

    // ========== 列显隐 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
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

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("section-flow-analysis", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        StateHasChanged();
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>
        {
            ["pageSize"] = _pageSize.ToString()
        };
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(
                _columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));

        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPageIndex,
            Extras = extras,
        };
        await PageState.SaveAsync("section-flow-analysis", state);
    }

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_filteredItems.Count == 0) return;

        // 按当前页显示行汇总（Items 模式，取 MudTable 当前页切片）
        var page = table?.CurrentPage ?? 0;
        var rowsPerPage = table?.RowsPerPage ?? _pageSize;
        if (rowsPerPage <= 0) rowsPerPage = _pageSize;
        var pageItems = _filteredItems.Skip(page * rowsPerPage).Take(rowsPerPage).ToList();
        if (pageItems.Count == 0) return;

        _pageSums["PendingTotal"] = ((int)pageItems.Sum(x => x.PendingTotal ?? 0m)).ToString();
    }

    private string RenderFooterCell(ColumnDef col)
    {
        if (_pageSums.TryGetValue(col.Key, out var sum))
            return sum;
        return "-";
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 分页导航/页大小切换后重算当前页汇总（pager 操作只改 CurrentPage/RowsPerPage，不触发 LoadDataAsync）
        if (table != null && !_isLoading && _filteredItems.Count > 0)
        {
            var page = table.CurrentPage;
            var count = _filteredItems.Count;
            var rowsPerPage = table.RowsPerPage;
            if (page != _lastSummedPage || count != _lastSummedCount || rowsPerPage != _lastSummedPageSize)
            {
                _lastSummedPage = page;
                _lastSummedCount = count;
                _lastSummedPageSize = rowsPerPage;
                ComputePageSums();
                StateHasChanged();
            }
        }
        await Task.CompletedTask;
    }

    // ========== 显示辅助 ==========

    private static string RenderInt(decimal? val)
    {
        return val.HasValue ? ((int)val.Value).ToString() : "-";
    }

    private static string RenderDecimal(decimal? val)
    {
        return val.HasValue ? val.Value.ToString("F1") : "-";
    }

    private static Color GetStatusColor(string? status)
    {
        return status switch
        {
            "偏少" => Color.Error,
            "过多" => Color.Warning,
            "正常" => Color.Success,
            _ => Color.Default
        };
    }

    // ========== 打印 ==========

    private async Task PrintAll()
    {
        var printItems = _filteredItems.Select(item =>
        {
            var dict = new Dictionary<string, object>();
            foreach (var col in _visibleColumns)
                dict[col.Key] = ResolvePrintValue(item, col);
            return dict;
        }).ToList();

        var request = new SectionFlowAnalysisPrintRequest
        {
            Title = "工段流转分析",
            Items = printItems,
            Columns = _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList()
        };

        var apiUrl = $"{Http.BaseAddress}api/section-flow-analysis/print-file";
        var json = JsonSerializer.Serialize(request);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }

    private static object ResolvePrintValue(SectionFlowAnalysisDto item, ColumnDef col)
    {
        if (col.DisplayConverter != null)
            return col.DisplayConverter(GetRawPropertyValue(item, col.Key)) ?? "";
        return GetRawPropertyValue(item, col.Key);
    }

    private static object GetRawPropertyValue(SectionFlowAnalysisDto item, string key) => key switch
    {
        "Category" => $"{item.CategoryCode} {item.CategoryName}".Trim(),
        "PendingTotal" => item.PendingTotal.HasValue ? ((int)item.PendingTotal.Value).ToString() : "-",
        "SustainableDays" => item.SustainableDays.HasValue ? item.SustainableDays.Value.ToString("F1") : "-",
        "StatusJudgment" => item.StatusJudgment ?? "-",
        _ => ""
    };
}
