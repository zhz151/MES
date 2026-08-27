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
using MES.Core.Constants;
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
    private int _loadVersion;
    private bool _resetToFirstPage;
    private string _searchKeyword = string.Empty;

    // ========== 圆钢穿孔汇总折叠卡片（懒加载） ==========
    private bool _showPiercingPending;
    private bool _isLoadingPiercingPending;
    private List<SubcontractPiercingPendingDto> _piercingPendingItems = new();

    private bool _showPiercingInProgress;
    private bool _isLoadingPiercingInProgress;
    private SubcontractPiercingInProgressResultDto? _piercingInProgressData;

    private bool _showPiercingMonthly;
    private bool _isLoadingPiercingMonthly;
    private SubcontractPiercingMonthlyResultDto? _piercingMonthlyData;

    // ========== 分页汇总 ==========
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "RequiredQuantity", "RequiredWeight", "ReturnedQuantity", "ReturnedWeight",
        "ReturnQuantity", "ReturnWeight",
    };

    // ========== 选中行 ==========
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
    private HashSet<int> selectedIds = new();

    private string sortColumn = "Id";
    private bool sortDescending = true;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // 空值筛选哨兵（与 ExcelFilter 组件/后端 Service 的 "__EXCEL_FILTER_NULL__" 一致）
    private const string FilterNull = "__EXCEL_FILTER_NULL__";

    // ========== 列管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns => _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs()
    {
        // G1: 委外信息
        var g1 = new List<ColumnDef>
        {
            new() { Key = "OrderNo",             Label = "委外单号",       SortKey = "orderno",           FilterType = "string", Width = "130", GroupKey = 1, GroupName = "委外信息" },
            new() { Key = "Sequence",            Label = "序号",           SortKey = "sequence",                                 Width = "60",  GroupKey = 1, GroupName = "委外信息" },
            new() { Key = "SupplierName",        Label = "供应商",         SortKey = "suppliername",       FilterType = "string", Width = "120", GroupKey = 1, GroupName = "委外信息" },
            new() { Key = "OrderDate",           Label = "下单日期",       SortKey = "orderdate",           FilterType = "date",  Width = "110", GroupKey = 1, GroupName = "委外信息" },
            new() { Key = "SourceWorkOrderNo",   Label = "来源工单号",     SortKey = "sourceworkorderno",  FilterType = "string", Width = "130", GroupKey = 1, GroupName = "委外信息" },
            new() { Key = "PlantGrade",          Label = "牌号",           SortKey = "plantgrade",         FilterType = "string", Width = "100", GroupKey = 1, GroupName = "委外信息" },
            new() { Key = "ProcessSpecification", Label = "规格",          SortKey = "processspecification", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "委外信息" },
            new() { Key = "UnitWeight",          Label = "单重(kg)",       SortKey = "unitweight",                             Width = "80",  GroupKey = 1, GroupName = "委外信息" },
            new() { Key = "RequiredQuantity",    Label = "需求支数",       SortKey = "requiredquantity",                       Width = "80",  GroupKey = 1, GroupName = "委外信息" },
            new() { Key = "RequiredWeight",      Label = "需求重量(kg)",   SortKey = "requiredweight",                         Width = "100", GroupKey = 1, GroupName = "委外信息" },
            new() { Key = "RequiredArrivalDate", Label = "要求到货日",     SortKey = "requiredarrivaldate", FilterType = "date", Width = "110", GroupKey = 1, GroupName = "委外信息" },
            new() { Key = "Remark",              Label = "委外备注",       SortKey = "remark",           FilterType = "string", Width = "120", GroupKey = 1, GroupName = "委外信息", Visible = false },
        };

        // G2: 工单实时关注（从工单执行状况读模型按来源工单号关联，无记录默认 "-"）
        var g2Exec = new List<ColumnDef>
        {
            new() { Key = "ExecutionScheduleStage",         Label = "工单关注",     SortKey = "executionschedulestage",         FilterType = "enum",   Width = "100", GroupKey = 2, GroupName = "工单实时关注",
                EnumOptions = new List<EnumOption> { new(FilterNull, "空值") }.Concat(DisplayHelper.GetScheduleStageOptions()).ToList() },
            new() { Key = "ExecutionRawMaterialLockRemark", Label = "原锁执行备注", SortKey = "executionrawmateriallockremark", FilterType = "string", Width = "130", GroupKey = 2, GroupName = "工单实时关注" },
            new() { Key = "ExecutionUrgencyLevel",          Label = "计划性",       SortKey = "executionurgencylevel",          FilterType = "string", Width = "100", GroupKey = 2, GroupName = "工单实时关注" },
            new() { Key = "ExecutionTheoreticalCutoffDate", Label = "理论截止投料日", SortKey = "executiontheoreticalcutoffdate", FilterType = "date",   Width = "120", GroupKey = 2, GroupName = "工单实时关注" },
        };

        // G3: 执行状态
        var g2 = new List<ColumnDef>
        {
            new() { Key = "ProcessStatus",       Label = "执行状态",       SortKey = "processstatus",      FilterType = "enum",  Width = "100", GroupKey = 3, GroupName = "执行状态",
                EnumOptions = DisplayHelper.GetEnumFilterOptions<SubcontractOrderStatus>() },
            new() { Key = "ReturnDeadline",      Label = "截止回收日",     SortKey = "returndeadline",     FilterType = "date",  Width = "110", GroupKey = 3, GroupName = "执行状态" },
            new() { Key = "ReturnedQuantity",    Label = "回收支数",       SortKey = "returnedquantity",                       Width = "80",  GroupKey = 3, GroupName = "执行状态" },
            new() { Key = "ReturnedWeight",      Label = "回收重量(kg)",   SortKey = "returnedweight",                         Width = "100", GroupKey = 3, GroupName = "执行状态" },
            new() { Key = "ReturnQuantity",      Label = "退货量",                                                   Width = "100", GroupKey = 3, GroupName = "执行状态" },
            new() { Key = "IsForceCompleted",    Label = "属强制完成",     SortKey = "isforcecompleted",   FilterType = "enum",  Width = "100", GroupKey = 3, GroupName = "执行状态",
                EnumOptions = DisplayHelper.GetBoolOptions() },
        };

        var all = new List<ColumnDef>();
        all.AddRange(g1);
        all.AddRange(g2Exec);
        all.AddRange(g2);
        return all;
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 分组标题栏：测量 MudTable 实际列宽 + 同步滚动对齐（列显隐/排序/滚动条变化时 ResizeObserver 自动重同步）
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#return-item-list-table");
        }
        catch { }
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

        // 工单实时关注组：计划性 / 原锁执行备注 筛选选项显示中文（后端 DISTINCT 返回英文 Key）
        if (_filterContextOptions.TryGetValue("ExecutionUrgencyLevel", out var execUrgencyOptions))
        {
            foreach (var opt in execUrgencyOptions)
                opt.Display = DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, opt.Value) ?? opt.Value;
        }
        if (_filterContextOptions.TryGetValue("ExecutionRawMaterialLockRemark", out var execLockOptions))
        {
            foreach (var opt in execLockOptions)
                opt.Display = DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, opt.Value) ?? opt.Value;
        }

        // 空值选项统一显示「空值」（哨兵 "__EXCEL_FILTER_NULL__"，须在各项中文映射之后执行）
        foreach (var options in _filterContextOptions.Values)
        {
            foreach (var opt in options.Where(o => o.Value == FilterNull))
                opt.Display = "空值";
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
        var version = ++_loadVersion;

        if (_resetToFirstPage)
        {
            tableState.Page = 0;
            _resetToFirstPage = false;
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

        // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
        if (version != _loadVersion)
            return new TableData<SubcontractReturnItemListDto> { Items = _pageItems, TotalItems = _totalCount };

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
            if (kv.Value.Count == 0) continue;
            // 空值哨兵 → IncludeNull=true（内存层 val==null 匹配）；仅勾选空值时用 isnull 操作符
            var hasNull = kv.Value.Contains(FilterNull);
            var actualValues = kv.Value.Where(v => v != FilterNull).ToList();
            if (hasNull)
            {
                if (actualValues.Count > 0)
                    list.Add(new FilterDescriptor { Field = kv.Key, Operator = "in", Values = actualValues, IncludeNull = true });
                else
                    list.Add(new FilterDescriptor { Field = kv.Key, Operator = "isnull", IncludeNull = true });
            }
            else
            {
                list.Add(new FilterDescriptor { Field = kv.Key, Operator = "in", Values = actualValues });
            }
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
        _resetToFirstPage = true;
        _currentPage = 1;
        if (table != null) await table.ReloadServerData();
    }

    // ========== 圆钢穿孔汇总折叠卡片（懒加载） ==========

    private async Task TogglePiercingPending()
    {
        _showPiercingPending = !_showPiercingPending;
        if (_showPiercingPending && _piercingPendingItems.Count == 0) await LoadPiercingPendingAsync();
    }

    private async Task TogglePiercingInProgress()
    {
        _showPiercingInProgress = !_showPiercingInProgress;
        if (_showPiercingInProgress && _piercingInProgressData == null) await LoadPiercingInProgressAsync();
    }

    private async Task TogglePiercingMonthly()
    {
        _showPiercingMonthly = !_showPiercingMonthly;
        if (_showPiercingMonthly && _piercingMonthlyData == null) await LoadPiercingMonthlyAsync();
    }

    private async Task LoadPiercingPendingAsync()
    {
        _isLoadingPiercingPending = true;
        StateHasChanged();
        try
        {
            var result = await SubcontractService.GetPiercingPendingAsync();
            _piercingPendingItems = result.Success && result.Data != null ? result.Data : new List<SubcontractPiercingPendingDto>();
        }
        catch (Exception ex) { Snackbar.Add($"圆钢待穿孔数据加载失败: {ex.Message}", Severity.Error); }
        finally { _isLoadingPiercingPending = false; StateHasChanged(); }
    }

    private async Task LoadPiercingInProgressAsync()
    {
        _isLoadingPiercingInProgress = true;
        StateHasChanged();
        try
        {
            var result = await SubcontractService.GetPiercingInProgressAsync();
            _piercingInProgressData = result.Success && result.Data != null ? result.Data : null;
        }
        catch (Exception ex) { Snackbar.Add($"圆钢在穿孔数据加载失败: {ex.Message}", Severity.Error); }
        finally { _isLoadingPiercingInProgress = false; StateHasChanged(); }
    }

    private async Task LoadPiercingMonthlyAsync()
    {
        _isLoadingPiercingMonthly = true;
        StateHasChanged();
        try
        {
            var result = await SubcontractService.GetPiercingMonthlyAsync();
            _piercingMonthlyData = result.Success && result.Data != null ? result.Data : null;
        }
        catch (Exception ex) { Snackbar.Add($"圆钢月度穿孔数据加载失败: {ex.Message}", Severity.Error); }
        finally { _isLoadingPiercingMonthly = false; StateHasChanged(); }
    }

    // ========== 圆钢穿孔汇总格式化 ==========

    /// <summary>吨(t) 格式化：kg/1000 保留 1 位，0 值留空</summary>
    private static string FormatTon(decimal kg) => kg > 0 ? (kg / 1000m).ToString("F1") : string.Empty;

    /// <summary>kg 取整显示，0 值留空（同荒管待购 FormatPendingWeight）</summary>
    private static string FormatKg(decimal kg) => kg > 0 ? ((int)kg).ToString() : string.Empty;

    /// <summary>月度单元格格式化：「发X/回Y」（t），0 值留空</summary>
    private static string FormatSendRecoverText(decimal send, decimal rec)
    {
        if (send <= 0 && rec <= 0) return string.Empty;
        var parts = new List<string>();
        if (send > 0) parts.Add("发" + (send / 1000m).ToString("F1"));
        if (rec > 0) parts.Add("回" + (rec / 1000m).ToString("F1"));
        return string.Join("/", parts);
    }

    // ========== 圆钢穿孔汇总打印（前端 printRawHtml 直接打印 DOM 表格） ==========

    private async Task PrintTableAsync(string tableId, string title)
    {
        try
        {
            var html = await JS.InvokeAsync<string>("getTableHtml", tableId);
            if (!string.IsNullOrEmpty(html))
                await JS.InvokeVoidAsync("printRawHtml", html, title);
            else
                Snackbar.Add("未找到可打印的汇总表格", Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private Task PrintPiercingPendingTable() => PrintTableAsync("#sc-piercing-pending-table", "圆钢待穿孔");
    private Task PrintPiercingInProgressTable() => PrintTableAsync("#sc-piercing-in-progress-table", "圆钢在穿孔");
    private Task PrintPiercingMonthlyTable() => PrintTableAsync("#sc-piercing-monthly-table", "圆钢月度穿孔数据");

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
            case "Sequence":
                builder.AddContent(0, item.Sequence);
                break;
            case "OrderNo":
                builder.AddContent(0, item.OrderNo);
                break;
            case "SupplierName":
                builder.AddContent(0, item.SupplierName);
                break;
            case "OrderDate":
                builder.AddContent(0, item.OrderDate.ToString("yyyy-MM-dd"));
                break;
            case "SourceWorkOrderNo":
                builder.AddContent(0, item.SourceWorkOrderNo);
                break;
            case "ExecutionScheduleStage":
                if (item.ExecutionScheduleStage.HasValue)
                {
                    builder.OpenComponent(0, typeof(MudChip));
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", DisplayHelper.GetScheduleStageColor(item.ExecutionScheduleStage.Value));
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)((b) => b.AddContent(0, IntStatusDisplayHelper.GetScheduleStageText(item.ExecutionScheduleStage.Value))));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "ExecutionRawMaterialLockRemark":
                builder.AddContent(0, string.IsNullOrEmpty(item.ExecutionRawMaterialLockRemark) ? "-" : (DictValueDisplayHelper.GetText(DictValueDefaults.RawMaterialLockRemarkKey, item.ExecutionRawMaterialLockRemark) ?? "-"));
                break;
            case "ExecutionUrgencyLevel":
                builder.AddContent(0, string.IsNullOrEmpty(item.ExecutionUrgencyLevel) ? "-" : (DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, item.ExecutionUrgencyLevel) ?? "-"));
                break;
            case "ExecutionTheoreticalCutoffDate":
                builder.AddContent(0, item.ExecutionTheoreticalCutoffDate?.ToString("yyyy-MM-dd") ?? "-");
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
            case "RequiredArrivalDate":
                builder.AddContent(0, item.RequiredArrivalDate?.ToString("yyyy-MM-dd"));
                break;
            case "Remark":
                builder.AddContent(0, item.Remark ?? "-");
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
            case "ReturnQuantity":
                builder.AddContent(0, item.ReturnQuantity == 0 && item.ReturnWeight == 0m
                    ? "-"
                    : $"{item.ReturnQuantity}支/{item.ReturnWeight.ToString("G29")}kg");
                break;
            case "IsForceCompleted":
                builder.AddContent(0, item.IsForceCompleted ? "是" : "-");
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

    // ========== 分组渲染 ==========

    private class GroupHeaderInfo
    {
        public int GroupKey { get; init; }
        public string GroupName { get; init; } = "";
        public int TotalWidth { get; init; }
        public int ColumnCount { get; init; }
        public string CssClass { get; init; } = "";
    }

    private List<GroupHeaderInfo> GetGroupHeaders()
    {
        var result = new List<GroupHeaderInfo>();

        // 选择列占位（40px），对齐表格最左侧的 checkbox 列
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0,
            GroupName = "",
            TotalWidth = 40,
            ColumnCount = 0,
            CssClass = ""
        });

        int? lastKey = null;
        int totalWidth = 0;
        var groupKey = 0;
        var groupName = "";
        var count = 0;

        foreach (var col in _visibleColumns)
        {
            var gk = col.GroupKey ?? 0;
            if (lastKey.HasValue && gk != lastKey.Value)
            {
                if (count > 0)
                {
                    result.Add(new GroupHeaderInfo
                    {
                        GroupKey = groupKey,
                        GroupName = groupName,
                        TotalWidth = totalWidth,
                        ColumnCount = count,
                        CssClass = GetHeaderGroupCss(groupKey, true)
                    });
                }
                totalWidth = 0;
                count = 0;
            }
            groupKey = gk;
            groupName = col.GroupName ?? "";
            totalWidth += int.TryParse(col.Width, out var w) ? w : 100;
            count++;
            lastKey = gk;
        }
        if (count > 0)
        {
            result.Add(new GroupHeaderInfo
            {
                GroupKey = groupKey,
                GroupName = groupName,
                TotalWidth = totalWidth,
                ColumnCount = count,
                CssClass = GetHeaderGroupCss(groupKey, true)
            });
        }

        return result;
    }

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1",
            2 => "col-g2",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start";
        return cls;
    }

    private static string GetCellGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1-cell",
            2 => "col-g2-cell",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    public async ValueTask DisposeAsync()
    {
        await SaveState();
    }
}
