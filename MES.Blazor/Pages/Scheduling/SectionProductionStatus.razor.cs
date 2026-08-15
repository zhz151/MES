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

public partial class SectionProductionStatus
{
    private MudTable<SectionProductionStatusDto>? table;
    private List<SectionProductionStatusDto> _allItems = new();
    private List<SectionProductionStatusDto> _filteredItems = new();
    private bool _isLoading;

    // ========== 页面状态持久化 ==========
    private int _restoredPageIndex;
    private int _currentPageIndex = 1;
    private int _pageSize = 25;
    private string _searchKeyword = string.Empty;

    // ========== 排序状态 ==========
    private string sortColumn = "ProcessGroupName";
    private bool sortDescending = false;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();
    // B18: 分页汇总
    private Dictionary<string, string> _pageSums = new();
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
            new() { Key = "ProcessGroupName", Label = "工序组", SortKey = "processgroupname", FilterType = "string" },
            new() { Key = "SectionName",      Label = "工段",   SortKey = "sectionname",      FilterType = "string" },
            new() { Key = "ProductStatus",    Label = "产类",   SortKey = "productstatus",    FilterType = "string" },
            new() { Key = "InProduction",     Label = "生产中", SortKey = "inproduction",     FilterType = "number" },
            new() { Key = "PendingProduction",Label = "待产量", SortKey = "pendingproduction",FilterType = "number" },
            new() { Key = "Total",            Label = "汇总量", SortKey = "total",            FilterType = "number" },
            new() { Key = "PlanFlowQuantity", Label = "计划流转量", SortKey = "planflowquantity", FilterType = "number" },
            new() { Key = "PlanKeyWeight",    Label = "重点批重量", SortKey = "plankeyweight",    FilterType = "number" },
        };
    }

    // ========== 生命周期 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();

        // 恢复排序/搜索/筛选状态
        var savedState = await PageState.LoadAsync("section-production-status");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "ProcessGroupName";
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

            // 恢复列显隐
            if (savedState.Extras?.ContainsKey("columnVisibility") == true)
            {
                try
                {
                    var raw = savedState.Extras["columnVisibility"];
                    var visibleKeys = JsonSerializer.Deserialize<List<string>>(raw);
                    if (visibleKeys != null)
                    {
                        var visibleSet = new HashSet<string>(visibleKeys);
                        foreach (var col in _allColumns)
                            col.Visible = visibleSet.Contains(col.Key);
                    }
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

        await LoadDataAsync();

        if (savedState != null && table != null)
            await table.ReloadServerData();
    }

    // ========== 数据加载 ==========

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        try
        {
            var result = await Service.GetStatusAsync();
            // 页面加载只呈现汇总量非空的数据行（全维度空行的(工序组,工段,产类)无业务意义）
            _allItems = result?.Success == true && result.Data != null
                ? result.Data.Where(x => x.Total.HasValue).ToList()
                : new List<SectionProductionStatusDto>();

            if (result?.Success != true)
                Snackbar.Add(result?.Message ?? "获取数据失败", Severity.Error);

            BuildFilterContextOptions();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载数据时发生错误: {ex.Message}", Severity.Error);
            _allItems = new List<SectionProductionStatusDto>();
        }
        finally
        {
            ApplyFiltersAndSort();
            _isLoading = false;
        }
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
                        Display = col.Key switch
                        {
                            "SectionName" or "CurrentSectionName" or "NextSectionName" or "PendingSectionName" => SectionDisplayHelper.GetSectionNameText(val!),
                            "ProcessName" or "ProcessGroupName" or "CurrentGroupName" or "NextProcess" => ProcessDisplayHelper.GetProcessNameText(val!),
                            "ProductStatus" => DisplayHelper.GetCombinationProductStatusText(val!),
                            _ => val!
                        },
                        Count = _allItems.Count(x => string.Equals(GetStringValue(x, col.Key), val, StringComparison.OrdinalIgnoreCase))
                    })
                    .ToList();
                _filterContextOptions[col.Key] = options;
            }
        }
    }

    private static string? GetStringValue(SectionProductionStatusDto item, string key) => key switch
    {
        "ProcessGroupName" => item.ProcessGroupName,
        "SectionName" => item.SectionName,
        "ProductStatus" => item.ProductStatus,
        _ => null
    };

    // ========== 搜索/筛选/排序 ==========

    private void ApplyFiltersAndSort()
    {
        var query = _allItems.AsEnumerable();

        // 关键字搜索
        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            var kw = _searchKeyword.Trim();
            query = query.Where(x =>
                x.ProcessGroupName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                (x.SectionName != null && (SectionDisplayHelper.GetSectionNameText(x.SectionName).Contains(kw, StringComparison.OrdinalIgnoreCase) || x.SectionName.Contains(kw, StringComparison.OrdinalIgnoreCase))));
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

                // 两者都选或都没选 = 不过滤；只选一个则过滤
                if (hasNotNull && !hasNull)
                    query = query.Where(x => GetDecimalValue(x, kvp.Key).HasValue);
                else if (hasNull && !hasNotNull)
                    query = query.Where(x => !GetDecimalValue(x, kvp.Key).HasValue);
            }
            else if (col.FilterType == "string" && col.Key == "ProcessGroupName")
            {
                query = query.Where(x => kvp.Value.Contains(x.ProcessGroupName ?? "", StringComparer.OrdinalIgnoreCase));
            }
            else if (col.FilterType == "string" && col.Key == "SectionName")
            {
                query = query.Where(x => kvp.Value.Contains(x.SectionName ?? "", StringComparer.OrdinalIgnoreCase));
            }
            else if (col.FilterType == "string" && col.Key == "ProductStatus")
            {
                query = query.Where(x => kvp.Value.Contains(x.ProductStatus ?? "", StringComparer.OrdinalIgnoreCase));
            }
        }

        // 排序
        query = sortColumn switch
        {
            "ProcessGroupName" => sortDescending
                ? query.OrderByDescending(x => x.ProcessGroupName)
                : query.OrderBy(x => x.ProcessGroupName),
            "SectionName" => sortDescending
                ? query.OrderByDescending(x => x.SectionName)
                : query.OrderBy(x => x.SectionName),
            "ProductStatus" => sortDescending
                ? query.OrderByDescending(x => x.ProductStatus)
                : query.OrderBy(x => x.ProductStatus),
            "InProduction" => sortDescending
                ? query.OrderByDescending(x => x.InProduction)
                : query.OrderBy(x => x.InProduction),
            "PendingProduction" => sortDescending
                ? query.OrderByDescending(x => x.PendingProduction)
                : query.OrderBy(x => x.PendingProduction),
            "Total" => sortDescending
                ? query.OrderByDescending(x => x.Total)
                : query.OrderBy(x => x.Total),
            "PlanFlowQuantity" => sortDescending
                ? query.OrderByDescending(x => x.PlanFlowQuantity)
                : query.OrderBy(x => x.PlanFlowQuantity),
            "PlanKeyWeight" => sortDescending
                ? query.OrderByDescending(x => x.PlanKeyWeight)
                : query.OrderBy(x => x.PlanKeyWeight),
            _ => query.OrderBy(x => x.ProcessGroupName)
        };

        _filteredItems = query.ToList();
        ComputePageSums();
    }

    private static decimal? GetDecimalValue(SectionProductionStatusDto item, string key) => key switch
    {
        "InProduction" => item.InProduction,
        "PendingProduction" => item.PendingProduction,
        "Total" => item.Total,
        "PlanFlowQuantity" => item.PlanFlowQuantity,
        "PlanKeyWeight" => item.PlanKeyWeight,
        _ => null
    };

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

    // ========== 列显隐 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SavePageStateAsync();
    }

    private async Task ResetColumnDisplay()
    {
        foreach (var col in _allColumns)
            col.Visible = true;
        await SavePageStateAsync();
        ApplyFiltersAndSort();
        StateHasChanged();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        var idx = _allColumns.IndexOf(col);
        if (idx > 0)
        {
            _allColumns.RemoveAt(idx);
            _allColumns.Insert(idx - 1, col);
        }
        await SavePageStateAsync();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        var idx = _allColumns.IndexOf(col);
        if (idx < _allColumns.Count - 1)
        {
            _allColumns.RemoveAt(idx);
            _allColumns.Insert(idx + 1, col);
        }
        await SavePageStateAsync();
    }

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
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

    // ========== 每页行数切换 ==========

    private async Task OnRowsPerPageChanged(int size)
    {
        _pageSize = size;
        ApplyFiltersAndSort(); // 内部 ComputePageSums 重算当前页汇总
        StateHasChanged();
        await SavePageStateAsync();
    }

    // ========== 状态持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>
        {
            ["pageSize"] = _pageSize.ToString(),
            ["columnVisibility"] = JsonSerializer.Serialize(_allColumns.Where(c => c.Visible).Select(c => c.Key).ToList())
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
        await PageState.SaveAsync("section-production-status", state);
    }

    // ========== 单元格渲染 ==========

    private static string RenderCellValue(decimal? value)
    {
        return value.HasValue ? ((int)value.Value).ToString() : "-";
    }

    private RenderFragment RenderCell(SectionProductionStatusDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "ProcessGroupName":
                builder.AddContent(0, ProcessDisplayHelper.GetProcessNameText(item.ProcessGroupName));
                break;
            case "SectionName":
                builder.AddContent(0, SectionDisplayHelper.GetSectionNameText(item.SectionName));
                break;
            case "ProductStatus":
                builder.AddContent(0, DisplayHelper.GetCombinationProductStatusText(item.ProductStatus));
                break;
            case "InProduction":
                builder.AddContent(0, RenderCellValue(item.InProduction));
                break;
            case "PendingProduction":
                builder.AddContent(0, RenderCellValue(item.PendingProduction));
                break;
            case "Total":
                builder.AddContent(0, RenderCellValue(item.Total));
                break;
            case "PlanFlowQuantity":
                builder.AddContent(0, RenderCellValue(item.PlanFlowQuantity));
                break;
            case "PlanKeyWeight":
                builder.AddContent(0, RenderCellValue(item.PlanKeyWeight));
                break;
        }
    };

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

        _pageSums["InProduction"] = ((int)pageItems.Sum(x => x.InProduction ?? 0m)).ToString();
        _pageSums["PendingProduction"] = ((int)pageItems.Sum(x => x.PendingProduction ?? 0m)).ToString();
        _pageSums["Total"] = ((int)pageItems.Sum(x => x.Total ?? 0m)).ToString();
        _pageSums["PlanFlowQuantity"] = ((int)pageItems.Sum(x => x.PlanFlowQuantity ?? 0m)).ToString();
        _pageSums["PlanKeyWeight"] = ((int)pageItems.Sum(x => x.PlanKeyWeight ?? 0m)).ToString();
    }

    private string RenderFooterCell(ColumnDef col)
    {
        if (_pageSums.TryGetValue(col.Key, out var sum))
            return sum;
        return "-";
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 分页导航/页大小切换后重算当前页汇总（pager 操作只改 CurrentPage/RowsPerPage，不触发 ApplyFiltersAndSort）
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

        var request = new SectionProductionStatusPrintRequest
        {
            Title = "工段待在产量",
            Items = printItems,
            Columns = _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList()
        };

        var apiUrl = $"{Http.BaseAddress}api/section-production-status/print-file";
        var json = JsonSerializer.Serialize(request);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }

    private static object ResolvePrintValue(SectionProductionStatusDto item, ColumnDef col)
    {
        if (col.DisplayConverter != null)
            return col.DisplayConverter(GetRawPropertyValue(item, col.Key)) ?? "";

        if (col.FilterType == "boolean")
        {
            var raw = GetRawPropertyValue(item, col.Key);
            if (raw is bool b) return b ? col.BoolTrueLabel : col.BoolFalseLabel;
            return raw?.ToString() ?? "-";
        }

        return GetRawPropertyValue(item, col.Key);
    }

    private static object GetRawPropertyValue(SectionProductionStatusDto item, string key) => key switch
    {
        "ProcessGroupName" => ProcessDisplayHelper.GetProcessNameText(item.ProcessGroupName),
        "SectionName" => SectionDisplayHelper.GetSectionNameText(item.SectionName),
        "ProductStatus" => DisplayHelper.GetCombinationProductStatusText(item.ProductStatus),
        "InProduction" => item.InProduction.HasValue ? ((int)item.InProduction.Value).ToString() : "-",
        "PendingProduction" => item.PendingProduction.HasValue ? ((int)item.PendingProduction.Value).ToString() : "-",
        "Total" => item.Total.HasValue ? ((int)item.Total.Value).ToString() : "-",
        "PlanFlowQuantity" => item.PlanFlowQuantity.HasValue ? ((int)item.PlanFlowQuantity.Value).ToString() : "-",
        "PlanKeyWeight" => item.PlanKeyWeight.HasValue ? ((int)item.PlanKeyWeight.Value).ToString() : "-",
        _ => ""
    };

}
