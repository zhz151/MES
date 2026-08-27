using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Shared;
using MES.Core.Models;
using MES.Blazor.Helpers;
using MES.Blazor.Shared;
using MES.Shared.Constants;
using System.Text.Json;

namespace MES.Blazor.Pages.Quality;

public partial class Certificates
{
    private MudTable<CertificateDto>? table;
    private List<CertificateDto> _pageItems = new();
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new() { };
    private int _totalCount;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _pageSize = 10;
    private int _loadVersion;
    private bool _resetToFirstPage;
    private string _searchKeyword = string.Empty;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;

    // ========== 打印选择 ==========
    private HashSet<int> selectedIds = new();
    private bool allSelected
    {
        get => _pageItems.Any() && _pageItems.All(i => selectedIds.Contains(i.Id));
        set
        {
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

    private string sortColumn = "issuedate";
    private bool sortDescending = true;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "certificateno",  Label = "质保书编号", SortKey = "certificateno",  FilterType = "string", IsRequired = true },
        new() { Key = "issuedate",      Label = "签发日期",   SortKey = "issuedate",      FilterType = "string" },
        new() { Key = "customername",   Label = "客户名称",   SortKey = "customername",   FilterType = "string" },
        new() { Key = "productstandard",Label = "产品标准",   SortKey = "productstandard", FilterType = "string" },
        new() { Key = "productname",    Label = "产品名称",   SortKey = "productname",    FilterType = "string" },
        new() { Key = "deliverystatus", Label = "交货状态",   SortKey = "deliverystatus", FilterType = "string" },
        new() { Key = "createdby",     Label = "创建人",     SortKey = "createdby",     FilterType = "string", Width = "100", Visible = false },
        new() { Key = "createdtime",   Label = "创建时间",   SortKey = "createdtime",   FilterType = "string", Width = "130", Visible = false },
        new() { Key = "updatedby",     Label = "更新人",     SortKey = "updatedby",     FilterType = "string", Width = "100", Visible = false },
        new() { Key = "updatedtime",   Label = "更新时间",   SortKey = "updatedtime",   FilterType = "string", Width = "130", Visible = false },
    };

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;
        var props = typeof(CertificateDto)
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
        if (_pageSums.TryGetValue(col.Key, out var sum)) return sum;
        return "-";
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<CertificateDto>> LoadDataFromServer(TableState state)
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

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "issuedate";
            var filtersJson = SerializeFilters();

            var result = await Svc.GetAllAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
                filters: filtersJson);

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<CertificateDto> { Items = _pageItems, TotalItems = _totalCount };
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

        ComputePageSums();

        return new TableData<CertificateDto>
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
            var result = await Svc.GetFilterContextsAsync();
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
            var key = kvp.Key.ToLower();
            _filterContextOptions[key] = kvp.Value.Select(v => new ExcelFilterOption
            {
                Value = v,
                Display = v,
                Count = 0
            }).ToList();
        }

        // 交货状态列：枚举英文名 → 中文显示
        if (_filterContextOptions.TryGetValue("deliverystatus", out var deliveryOptions))
        {
            foreach (var opt in deliveryOptions)
            {
                opt.Display = DisplayHelper.GetDeliveryStateText(opt.Value);
            }
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

        // 补充布尔列筛选选项
        foreach (var col in _allColumns)
        {
            if (col.FilterType == "boolean" && !_filterContextOptions.ContainsKey(col.Key))
            {
                _filterContextOptions[col.Key] = DisplayHelper.GetBoolFilterOptions(col);
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
        _resetToFirstPage = true;
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
        await ColumnPrefs.SaveAsync("quality_certificates", null, _allColumns);
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
        var saved = await ColumnPrefs.LoadAsync("quality_certificates", null);
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

        var savedState = await PageState.LoadAsync("quality_certificates");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "issuedate";
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

        if (savedState != null)
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);

        if (savedState != null && table != null)
            await table.ReloadServerData();
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#certificates-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(CertificateDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "certificateno":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => ViewDetail(item.Id)));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.CertificateNo)));
                builder.CloseComponent();
                break;
            case "issuedate":
                builder.AddContent(0, item.IssueDate.ToString("yyyy-MM-dd"));
                break;
            case "customername":
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "table-cell-clamp-1");
                builder.AddAttribute(2, "title", item.CustomerName);
                builder.AddContent(3, item.CustomerName);
                builder.CloseElement();
                break;
            case "productstandard":
                builder.AddContent(0, item.ProductStandard);
                break;
            case "productname":
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "table-cell-clamp-1");
                builder.AddAttribute(2, "title", item.ProductName);
                builder.AddContent(3, item.ProductName);
                builder.CloseElement();
                break;
            case "deliverystatus":
                builder.AddContent(0, DisplayHelper.GetDeliveryStateText(item.DeliveryStatus));
                break;
            case "createdby":
                builder.AddContent(0, string.IsNullOrEmpty(item.CreatedBy) ? "-" : item.CreatedBy);
                break;
            case "createdtime":
                builder.AddContent(0, item.CreatedTime == default ? "-" : item.CreatedTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            case "updatedby":
                builder.AddContent(0, string.IsNullOrEmpty(item.UpdatedBy) ? "-" : item.UpdatedBy);
                break;
            case "updatedtime":
                builder.AddContent(0, item.UpdatedTime == default ? "-" : item.UpdatedTime.ToString("yyyy-MM-dd HH:mm"));
                break;
        }
    };

    /// <summary>当前可见列 → 打印列定义（Key/Label 对应当前列显隐与顺序）</summary>
    private List<PrintColumnDef> GetPrintColumnDefs() =>
        _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();

    /// <summary>按列取表格显示文本（复用 RenderCell 各分支口径，保证打印列表与页面单元格一致）</summary>
    private string? GetCellDisplayText(CertificateDto item, string key) => key switch
    {
        "certificateno" => item.CertificateNo,
        "issuedate" => item.IssueDate.ToString("yyyy-MM-dd"),
        "customername" => item.CustomerName,
        "productstandard" => item.ProductStandard,
        "productname" => item.ProductName,
        "deliverystatus" => DisplayHelper.GetDeliveryStateText(item.DeliveryStatus),
        "createdby" => string.IsNullOrEmpty(item.CreatedBy) ? "-" : item.CreatedBy,
        "createdtime" => item.CreatedTime == default ? "-" : item.CreatedTime.ToString("yyyy-MM-dd HH:mm"),
        "updatedby" => string.IsNullOrEmpty(item.UpdatedBy) ? "-" : item.UpdatedBy,
        "updatedtime" => item.UpdatedTime == default ? "-" : item.UpdatedTime.ToString("yyyy-MM-dd HH:mm"),
        _ => null
    };

    // ========== 业务操作 ==========

    private void NavigateToCreate() => Navigation.NavigateTo("/quality/certificates/create");
    private void ViewDetail(int id) => Navigation.NavigateTo($"/quality/certificates/{id}");

    // ========== 打印 ==========

    /// <summary>打开「打印设置」对话框（页眉/页脚/字体配置，全局生效）</summary>
    private async Task OpenPrintSettings()
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<CertificatePrintSettingsDialog>("打印设置", options);
        await dialog.Result;
    }

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的质量证明书", Severity.Warning);
            return;
        }
        try
        {
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var request = new CertificatePrintRequest { Ids = selectedIds.ToArray() };
            var apiUrl = $"{Navigation.BaseUri}{ApiEndpoints.Certificate}/print-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>打印选中列表（按当前可见列渲染列表 PDF，Mode A 前端已准备数据）</summary>
    private async Task PrintSelectedList()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的质量证明书", Severity.Warning);
            return;
        }
        try
        {
            // 列过多时各列被压缩到单字符放不下的宽度 → QuestPDF 布局冲突；A4 可显示列数上限 35 列（与后端 TablePrintHelper.MaxPrintColumns 同步），超限提前拦截并页面内警示
            const int MaxPrintColumns = 35;
            var visible = _visibleColumns;
            if (visible.Count > MaxPrintColumns)
            {
                Snackbar.Add($"当前可见列过多（{visible.Count} 列，打印上限 {MaxPrintColumns} 列），请通过列显隐精简后再打印", Severity.Warning);
                return;
            }

            var selectedItems = _pageItems
                .Where(o => selectedIds.Contains(o.Id))
                .Select(item =>
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var col in visible)
                        dict[col.Key] = GetCellDisplayText(item, col.Key) ?? "-";
                    return dict;
                }).ToList();

            var request = new CertificatePrintListRequest
            {
                Title = "质量证明书列表",
                Items = selectedItems,
                Columns = GetPrintColumnDefs()
            };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Navigation.BaseUri}{ApiEndpoints.Certificate}/print-list-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task DeleteItem(CertificateDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除质保书 \"{item.CertificateNo}\" 吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await Svc.DeleteAsync(item.Id);
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
        await PageState.SaveAsync("quality_certificates", state);
    }
}
