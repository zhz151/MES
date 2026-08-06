using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Shared.Constants;
using System.Text.Json;

namespace MES.Blazor.Pages.Materials;

public partial class SubcontractReturnItems : IAsyncDisposable
{
    private MudTable<SubcontractReturnItemListDto>? table;
    private List<SubcontractReturnItemListDto> _pageItems = new();
    private int _totalCount;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _currentPage = 1;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    // ========== 分页汇总 ==========
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "RequiredQuantity", "RequiredWeight", "ReturnedQuantity", "ReturnedWeight",
    };

    // ========== 选中行 ==========
    private bool _allSelected;
    private bool allSelected
    {
        get => _allSelected;
        set
        {
            if (_allSelected == value) return;
            _allSelected = value;
            if (_allSelected)
            {
                foreach (var item in _pageItems)
                    selectedIds.Add(item.Id);
            }
            else
            {
                selectedIds.Clear();
            }
        }
    }
    private HashSet<int> selectedIds = new();

    private string sortColumn = "Id";
    private bool sortDescending = true;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns => _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs()
    {
        return new List<ColumnDef>
        {
            new() { Key = "OrderNo",             Label = "委外单号",       SortKey = "orderno",           FilterType = "string", Width = "130" },
            new() { Key = "SupplierName",        Label = "供应商",         SortKey = "suppliername",       FilterType = "string", Width = "120" },
            new() { Key = "SourceWorkOrderNo",   Label = "来源工单号",     SortKey = "sourceworkorderno",  FilterType = "string", Width = "130" },
            new() { Key = "PlantGrade",          Label = "牌号",           SortKey = "plantgrade",         FilterType = "string", Width = "100" },
            new() { Key = "ProcessSpecification", Label = "规格",          SortKey = "processspecification", FilterType = "string", Width = "120" },
            new() { Key = "UnitWeight",          Label = "单重(kg)",       SortKey = "unitweight",                             Width = "80" },
            new() { Key = "RequiredQuantity",    Label = "需求支数",       SortKey = "requiredquantity",                       Width = "80" },
            new() { Key = "RequiredWeight",      Label = "需求重量(kg)",   SortKey = "requiredweight",                         Width = "100" },
            new() { Key = "ReturnDeadline",      Label = "截止回收日",     SortKey = "returndeadline",     FilterType = "date",  Width = "110" },
            new() { Key = "ReturnedQuantity",    Label = "回收支数",       SortKey = "returnedquantity",                       Width = "80" },
            new() { Key = "ReturnedWeight",      Label = "回收重量(kg)",   SortKey = "returnedweight",                         Width = "100" },
            new() { Key = "ProcessStatus",       Label = "执行状态",       SortKey = "processstatus",      FilterType = "enum",  Width = "100",
                EnumOptions = DisplayHelper.GetEnumFilterOptions<SubcontractOrderStatus>() },
        };
    }

    protected override async Task OnInitializedAsync()
    {
        // 列定义与偏好加载
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("subcontract_return_items", null);
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

        // 恢复筛选/状态
        var state = await PageState.LoadAsync("subcontract_return_items");
        if (state != null)
        {
            sortColumn = state.SortBy ?? "Id";
            sortDescending = state.IsDescending;
            _searchKeyword = state.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, state.PageIndex - 1);
            if (state.Extras?.ContainsKey("columnFilters") == true)
            {
                try
                {
                    var raw = state.Extras["columnFilters"];
                    var dict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(raw);
                    if (dict != null)
                        _columnFilters = dict.ToDictionary(kv => kv.Key, kv => new HashSet<string>(kv.Value));
                }
                catch { _columnFilters = new(); }
            }
        }

        // 加载筛选上下文
        await LoadFilterContextsAsync();
    }

    private async Task LoadFilterContextsAsync()
    {
        if (_filterContextOptions.Count > 0) return;

        var resp = await SubcontractService.GetReturnItemFilterContextsAsync();
        if (resp?.Data != null)
        {
            // 枚举列：使用 EnumOptions 的 Display 中文值
            var enumCols = _allColumns
                .Where(c => c.FilterType == "enum" && c.EnumOptions != null)
                .ToDictionary(c => c.Key, c => c.EnumOptions!);

            foreach (var kvp in resp.Data)
            {
                if (enumCols.TryGetValue(kvp.Key, out var enumOpts))
                {
                    // 映射：Value 保持英文（实际筛选值），Display 显示中文
                    var optDict = enumOpts.ToDictionary(e => e.Value, e => e.Display);
                    _filterContextOptions[kvp.Key] = kvp.Value.Select(v => new ExcelFilterOption
                    {
                        Value = v,
                        Display = optDict.GetValueOrDefault(v, v),
                        Count = 0
                    }).ToList();
                }
                else
                {
                    _filterContextOptions[kvp.Key] = kvp.Value.Select(v => new ExcelFilterOption
                    {
                        Value = v,
                        Display = v,
                        Count = 0
                    }).ToList();
                }
            }
        }

        // 枚举列选项兜底（API 未返回时）
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

    private async Task<TableData<SubcontractReturnItemListDto>> LoadDataFromServer(TableState tableState)
    {
        if (_isFirstLoad)
        {
            _isFirstLoad = false;
            if (_restoredPageIndex > 0)
            {
                tableState.Page = _restoredPageIndex;
            }
        }
        _pageSize = tableState.PageSize == 0 ? 10 : tableState.PageSize;
        _currentPage = tableState.Page + 1;

        var query = new QueryParams
        {
            PageIndex = _currentPage,
            PageSize = _pageSize,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Filters = SerializeFilters()
        };

        var resp = await SubcontractService.GetReturnItemListAsync(query, null);
        if (resp?.Data != null)
        {
            _pageItems = resp.Data.Items;
            _totalCount = resp.Data.TotalCount;
        }
        else
        {
            _pageItems = new();
            _totalCount = 0;
        }

        ComputePageSums();
        await SaveState();
        return new TableData<SubcontractReturnItemListDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
    }

    // ========== 分页汇总计算 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(SubcontractReturnItemListDto)
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
                    _pageSums[col.Key] = sum.ToString("G29");
                }
                else if (type == typeof(int?))
                {
                    var sum = _pageItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal?))
                {
                    var sum = _pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[col.Key] = sum.ToString("G29");
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

    private List<FilterDescriptor>? SerializeFilters()
    {
        if (_columnFilters.Count == 0) return null;
        var list = new List<FilterDescriptor>();
        foreach (var kv in _columnFilters)
        {
            if (kv.Value.Count > 0)
                list.Add(new FilterDescriptor { Field = kv.Key, Operator = "in", Values = kv.Value.ToList() });
        }
        return list.Count > 0 ? list : null;
    }

    private async Task SaveState()
    {
        await PageState.SaveAsync("subcontract_return_items", new PageState
        {
            PageIndex = _currentPage,
            Keyword = _searchKeyword,
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Extras = new Dictionary<string, string>
            {
                ["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()))
            }
        });
    }

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value;
        _currentPage = 1;
        if (table != null) await table.ReloadServerData();
    }

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的子项", Severity.Warning);
            return;
        }
        try
        {
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var ids = selectedIds.ToArray();
            var request = new OrderPrintBatchRequest { Ids = ids, Columns = _visibleColumns.Select(c => c.ToPrintColumnDef()).ToList() };
            var apiUrl = $"{Navigation.BaseUri}{ApiEndpoints.Subcontract}/return-items/print-selected-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    private void ToggleSort(string colKey)
    {
        if (sortColumn == colKey)
            sortDescending = !sortDescending;
        else
        {
            sortColumn = colKey;
            sortDescending = true;
        }
        if (table != null) table.ReloadServerData();
    }

    private async Task OnColumnToggle()
    {
        await SaveColumnPrefs();
        if (table != null) await table.ReloadServerData();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("subcontract_return_items", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        if (table != null) await table.ReloadServerData();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        var idx = _allColumns.IndexOf(col);
        if (idx <= 0) return;
        (_allColumns[idx - 1], _allColumns[idx]) = (_allColumns[idx], _allColumns[idx - 1]);
        await SaveColumnPrefs();
        StateHasChanged();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        var idx = _allColumns.IndexOf(col);
        if (idx < 0 || idx >= _allColumns.Count - 1) return;
        (_allColumns[idx + 1], _allColumns[idx]) = (_allColumns[idx], _allColumns[idx + 1]);
        await SaveColumnPrefs();
        StateHasChanged();
    }

    private async void OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);

        _currentPage = 1;
        await SaveState();
        if (table != null) await table.ReloadServerData();
    }

    private RenderFragment RenderCell(SubcontractReturnItemListDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "OrderNo":
                builder.AddContent(0, item.OrderNo);
                break;
            case "SupplierName":
                builder.AddContent(0, item.SupplierName);
                break;
            case "SourceWorkOrderNo":
                builder.AddContent(0, item.SourceWorkOrderNo);
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade);
                break;
            case "ProcessSpecification":
                builder.AddContent(0, item.ProcessSpecification);
                break;
            case "UnitWeight":
                builder.AddContent(0, item.UnitWeight?.ToString("G29"));
                break;
            case "RequiredQuantity":
                builder.AddContent(0, item.RequiredQuantity);
                break;
            case "RequiredWeight":
                builder.AddContent(0, item.RequiredWeight?.ToString("G29"));
                break;
            case "ReturnDeadline":
                builder.AddContent(0, item.ReturnDeadline?.ToString("yyyy-MM-dd"));
                break;
            case "ReturnedQuantity":
                builder.AddContent(0, item.ReturnedQuantity);
                break;
            case "ReturnedWeight":
                builder.AddContent(0, item.ReturnedWeight.ToString("G29"));
                break;
            case "ProcessStatus":
                var ps = item.ProcessStatus;
                var psColor = ps.HasValue ? DisplayHelper.GetSubcontractOrderStatusColor(ps.Value) : Color.Default;
                builder.OpenComponent(0, typeof(MudChip));
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", psColor);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)((b) => b.AddContent(0, item.ProcessStatusDisplay)));
                builder.CloseComponent();
                break;
            default:
                builder.AddContent(0, "-");
                break;
        }
    };

    public async ValueTask DisposeAsync()
    {
        await SaveState();
    }
}
