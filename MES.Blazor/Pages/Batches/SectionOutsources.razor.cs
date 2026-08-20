using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Rendering;

namespace MES.Blazor.Pages.Batches;

public partial class SectionOutsources
{
    private MudTable<SectionOutsourceDto>? table;
    private List<SectionOutsourceDto> _pageItems = new();
    private int _totalCount;
    private HashSet<int> selectedIds = new();
    private bool _isArrowNavSetup;
    private bool allSelected
    {
        get => _pageItems.Any() && _pageItems.All(i => selectedIds.Contains(i.Id));
        set
        {
            if (value)
            {
                // 厂内（虚拟发外）记录无需回收，不参与全选
                foreach (var item in _pageItems.Where(i => !i.IsInternal))
                    selectedIds.Add(item.Id);
            }
            else
            {
                selectedIds.Clear();
            }
            StateHasChanged();
        }
    }
    private int _currentPageIndex;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;

    private string sortColumn = "createdtime";
    private bool sortDescending = true;

    // ========== 客户端排序（聚合字段无法后端排序）==========

    private static readonly HashSet<string> _clientSortableKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "totalrecoveredquantity", "totalrecoveredweight",
        "totalunprocessedquantity", "totalunprocessedweight",
        "actualrecoverydate", "recoveryremark"
    };

    private void ApplyClientSideSort()
    {
        var key = sortColumn.ToLower();
        if (!_clientSortableKeys.Contains(key)) return;

        _pageItems = key switch
        {
            "totalrecoveredquantity" => sortDescending
                ? _pageItems.OrderByDescending(i => i.TotalRecoveredQuantity ?? 0).ToList()
                : _pageItems.OrderBy(i => i.TotalRecoveredQuantity ?? 0).ToList(),
            "totalrecoveredweight" => sortDescending
                ? _pageItems.OrderByDescending(i => i.TotalRecoveredWeight ?? 0).ToList()
                : _pageItems.OrderBy(i => i.TotalRecoveredWeight ?? 0).ToList(),
            "totalunprocessedquantity" => sortDescending
                ? _pageItems.OrderByDescending(i => i.TotalUnprocessedQuantity ?? 0).ToList()
                : _pageItems.OrderBy(i => i.TotalUnprocessedQuantity ?? 0).ToList(),
            "totalunprocessedweight" => sortDescending
                ? _pageItems.OrderByDescending(i => i.TotalUnprocessedWeight ?? 0).ToList()
                : _pageItems.OrderBy(i => i.TotalUnprocessedWeight ?? 0).ToList(),
            "actualrecoverydate" => sortDescending
                ? _pageItems.OrderByDescending(i => i.ActualRecoveryDate ?? DateTime.MinValue).ToList()
                : _pageItems.OrderBy(i => i.ActualRecoveryDate ?? DateTime.MinValue).ToList(),
            "recoveryremark" => sortDescending
                ? _pageItems.OrderByDescending(i => i.RecoveryRemark ?? "").ToList()
                : _pageItems.OrderBy(i => i.RecoveryRemark ?? "").ToList(),
            _ => _pageItems
        };
    }

    // ========== 分页汇总 ==========
    private Dictionary<string, string> _pageSums = new();

    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "SendQuantity", "SendWeight",
        "TotalRecoveredQuantity", "TotalRecoveredWeight",
        "TotalUnprocessedQuantity", "TotalUnprocessedWeight",
    };

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "BatchNo",             Label = "生产编号",     SortKey = "batchno",             FilterType = "string", Width = "120", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "TagNo",               Label = "挂牌号",       SortKey = "tagno",               FilterType = "string", Width = "120", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "WorkOrderNo",         Label = "工单号",       SortKey = "workorderno",         FilterType = "string", Width = "120", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "SalesOrderNo",        Label = "订单号",       SortKey = "salesorderno",        FilterType = "string", Width = "120", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "ProductionMainNo",    Label = "主号",         SortKey = "productionmainno",    FilterType = "string", Width = "120", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "ProcessName",         Label = "工序名称",     SortKey = "processname",         FilterType = "string", Width = "120", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "SectionName",         Label = "工段名称",     SortKey = "sectionname",         FilterType = "string", Width = "120", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "PlantGrade",          Label = "工厂牌号",     SortKey = "plantgrade",          FilterType = "string", Width = "120", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "ManufacturingSpec",   Label = "制造规格",     SortKey = "manufacturingspec",   FilterType = "string", Width = "120", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "SequenceNumber",      Label = "执行序号",     SortKey = "sequencenumber", Width = "45", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "ProductStatus",       Label = "产类",         SortKey = "productstatus",       FilterType = "string", Width = "80", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "Status",              Label = "状态",         SortKey = "status",              FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "委外信息",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<SectionOutsourceStatus>() },
        new() { Key = "SendOutDate",         Label = "发出日期",     SortKey = "sendoutdate",         FilterType = "date", Width = "120", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "OutsourceVendor",     Label = "委外单位",     SortKey = "outsourcevendor",     FilterType = "string", Width = "120", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "IsInternal",          Label = "厂内",         SortKey = "isinternal",          FilterType = "boolean", BoolTrueLabel = "是", BoolFalseLabel = "否", Width = "60", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "OutsourceSpec",       Label = "委外规格",     SortKey = "outsourcespec",       FilterType = "string", Width = "120", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "SendQuantity",        Label = "发出支数",     SortKey = "sendquantity", Width = "80", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "SendWeight",          Label = "发出重量",     SortKey = "sendweight", Width = "80", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "ExpectedReturnDate",  Label = "要求收回日期", SortKey = "expectedreturndate",  FilterType = "date", Width = "120", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "IsUrgent",            Label = "紧急",         SortKey = "isurgent",            FilterType = "boolean", BoolTrueLabel = "是", BoolFalseLabel = "否", Width = "60", GroupKey = 1, GroupName = "委外信息" },
        // ----- 元信息（归属委外记录） -----
        new() { Key = "Remark",              Label = "备注",         SortKey = "remark",              FilterType = "string", Width = "120", GroupKey = 1, GroupName = "委外信息" },
        new() { Key = "DataSource",          Label = "数据来源",     SortKey = "datasource",          FilterType = "enum", Width = "80", GroupKey = 1, GroupName = "委外信息",
            EnumOptions = DisplayHelper.GetDataSourceOptions() },
        new() { Key = "UpdatedTime",         Label = "更新时间",     SortKey = "updatedtime", Width = "120", GroupKey = 1, GroupName = "委外信息" },
        // ===== 回收信息 =====
        new() { Key = "ActualRecoveryDate",  Label = "实际回收日期", SortKey = "actualrecoverydate",  FilterType = "date", Width = "120", GroupKey = 2, GroupName = "回收信息" },
        new() { Key = "TotalRecoveredQuantity",     Label = "正常回收(支)",  SortKey = "totalrecoveredquantity", Width = "80", GroupKey = 2, GroupName = "回收信息" },
        new() { Key = "TotalRecoveredWeight",       Label = "正常回收(重)",  SortKey = "totalrecoveredweight", Width = "80", GroupKey = 2, GroupName = "回收信息" },
        new() { Key = "TotalUnprocessedQuantity",   Label = "非正常回收(支)", SortKey = "totalunprocessedquantity", Width = "80", GroupKey = 2, GroupName = "回收信息" },
        new() { Key = "TotalUnprocessedWeight",     Label = "非正常回收(重)", SortKey = "totalunprocessedweight", Width = "80", GroupKey = 2, GroupName = "回收信息" },
        new() { Key = "RecoveryRemark",      Label = "回收备注",     SortKey = "recoveryremark",      FilterType = "string", Width = "120", GroupKey = 2, GroupName = "回收信息" },
    };

    // ========== 分页汇总计算 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(SectionOutsourceDto)
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

    private async Task<TableData<SectionOutsourceDto>> LoadDataFromServer(TableState state)
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

            var sortCol = _allColumns.FirstOrDefault(c => c.Key == sortColumn);
            var sortBy = sortCol?.SortKey ?? sortColumn ?? "createdtime";
            var filtersJson = SerializeFilters();

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending
            };

            if (!string.IsNullOrEmpty(filtersJson))
            {
                try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson); }
                catch { }
            }

            var result = await SectionOutsourceService.GetPagedAsync(
                pageIndex: query.PageIndex,
                pageSize: query.PageSize,
                keyword: query.Keyword,
                sortBy: query.SortBy,
                isDescending: query.IsDescending,
                sendOutDateFrom: DateTime.TryParse(_dateFrom, out var df) ? df : null,
                sendOutDateTo: DateTime.TryParse(_dateTo, out var dt) ? dt : null,
                filters: filtersJson);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPageIndex = result.Data.PageIndex;
                ApplyClientSideSort();
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

        return new TableData<SectionOutsourceDto>
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
            var result = await SectionOutsourceService.GetFilterContextsAsync();
            if (result.Success && result.Data != null)
            {
                BuildFilterContextOptions(result.Data);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载筛选上下文失败: {ex.Message}", Severity.Warning);
        }
    }

    private void BuildFilterContextOptions(Dictionary<string, List<string>> filterContexts)
    {
        _filterContextOptions.Clear();
        foreach (var kvp in filterContexts)
        {
            _filterContextOptions[kvp.Key] = kvp.Value.Select(v => new ExcelFilterOption
            {
                Value = v,
                Display = kvp.Key switch
                {
                    "SectionName" or "CurrentSectionName" or "NextSectionName" or "PendingSectionName" => SectionDisplayHelper.GetSectionNameText(v),
                    "ProcessName" or "ProcessGroupName" or "CurrentGroupName" or "NextProcess" => ProcessDisplayHelper.GetProcessNameText(v),
                    "ProductStatus" => DisplayHelper.GetProductStatusText(v),
                    _ => v
                },
                Count = 0
            }).ToList();
        }

        // IsUrgent 列显示中文
        if (_filterContextOptions.TryGetValue("IsUrgent", out var isUrgentOptions))
        {
            foreach (var opt in isUrgentOptions)
            {
                opt.Display = opt.Value == "True" ? "是" : "否";
            }
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

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues?.Any() == true)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }


    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnDateFromChanged(string value)
    {
        _dateFrom = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnDateToChanged(string value)
    {
        _dateTo = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 列显示管理 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await ColumnPrefs.SaveAsync("section-outsources", null, _allColumns);
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        await ColumnPrefs.SaveAsync("section-outsources", null, _allColumns);
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        await ColumnPrefs.SaveAsync("section-outsources", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await ColumnPrefs.SaveAsync("section-outsources", null, _allColumns);
        if (table != null) await table.ReloadServerData();
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("section-outsources", null);
        if (saved.Count > 0)
        {
            var reordered = new List<ColumnDef>();
            foreach (var savedCol in saved)
            {
                var match = _allColumns.FirstOrDefault(c => c.Key == savedCol.Key);
                if (match != null)
                {
                    match.Visible = savedCol.Visible;
                    reordered.Add(match);
                }
            }
            foreach (var col in _allColumns)
            {
                if (!reordered.Any(c => c.Key == col.Key))
                    reordered.Add(col);
            }
            _allColumns = reordered;
        }

        // 恢复排序/筛选状态
        var savedState = await PageState.LoadAsync("sectionoutsources");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "createdtime";
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
            if (savedState.Extras?.TryGetValue("dateFrom", out var dateFrom) == true)
                _dateFrom = dateFrom ?? string.Empty;
            if (savedState.Extras?.TryGetValue("dateTo", out var dateTo) == true)
                _dateTo = dateTo ?? string.Empty;
        }

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && table != null)
            await table.ReloadServerData();

        // 加载筛选上下文
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#section-outsources-list-table");
        }
        catch { }

        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#section-outsources-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public int? SendQuantity { get; set; }
        public decimal? SendWeight { get; set; }
        public string? OutsourceVendor { get; set; }
        public string? OutsourceSpec { get; set; }
        public string? ExpectedReturnDateText { get; set; }
        public bool IsUrgent { get; set; }
        public bool IsInternal { get; set; }
        public string? Remark { get; set; }
    }

    private RenderFragment RenderCell(SectionOutsourceDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing ? _editCache.GetValueOrDefault(item.Id) : null;
        var key = col.Key;

        switch (key)
        {
            case "BatchNo":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "Style", "cursor:pointer; color:#1976d2;");
                builder.AddAttribute(3, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => Navigation.NavigateTo($"/batches/{item.ProductionBatchId}")));
                builder.AddAttribute(4, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.BatchNo)));
                builder.CloseComponent();
                break;
            case "WorkOrderNo":
                builder.AddContent(0, item.WorkOrderNo);
                break;
            case "SalesOrderNo":
                builder.AddContent(0, item.SalesOrderNo);
                break;
            case "ProductionMainNo":
                builder.AddContent(0, item.ProductionMainNo);
                break;
            case "ProcessName":
                builder.AddContent(0, ProcessDisplayHelper.GetProcessNameText(item.ProcessName));
                break;
            case "ManufacturingSpec":
                builder.AddContent(0, DisplayHelper.FormatSpecification(item.ManufacturingSpec ?? ""));
                break;
            case "SectionName":
                builder.AddContent(0, SectionDisplayHelper.GetSectionNameText(item.SectionName));
                break;
            case "SequenceNumber":
                builder.AddContent(0, item.SequenceNumber);
                break;

            case "OutsourceVendor":
                if (isEditing && cache != null)
                    RenderEditTextField(builder, cache.OutsourceVendor ?? "", v => cache.OutsourceVendor = v);
                else
                    builder.AddContent(0, item.OutsourceVendor);
                break;

            case "IsInternal":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudCheckBox<bool>>(0);
                    builder.AddAttribute(1, "Value", cache.IsInternal);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<bool>(this, v => cache.IsInternal = v));
                    builder.AddAttribute(3, "Dense", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.GetYesNoText(item.IsInternal));
                }
                break;

            case "SendOutDate":
                builder.AddContent(0, item.SendOutDate.ToString("yyyy-MM-dd"));
                break;

            case "SendQuantity":
                if (isEditing && cache != null)
                    RenderEditIntField(builder, cache.SendQuantity, v => cache.SendQuantity = v);
                else
                    builder.AddContent(0, DisplayHelper.FormatNullableInt(item.SendQuantity));
                break;

            case "SendWeight":
                if (isEditing && cache != null)
                    RenderEditDecimalField(builder, cache.SendWeight, v => cache.SendWeight = v);
                else
                    builder.AddContent(0, $"{(int)(item.SendWeight ?? 0)}");
                break;

            case "Status":
                var statusColor = DisplayHelper.GetSectionOutsourceStatusColor(item.Status);
                var statusText = DisplayHelper.GetSectionOutsourceStatusText(item.Status);
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", statusColor);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, statusText)));
                builder.CloseComponent();
                break;

            case "TagNo":
                builder.AddContent(0, item.TagNo ?? "");
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade ?? "");
                break;

            case "OutsourceSpec":
                if (isEditing && cache != null)
                    RenderEditTextField(builder, cache.OutsourceSpec ?? "", v => cache.OutsourceSpec = v);
                else
                    builder.AddContent(0, item.OutsourceSpec ?? "");
                break;

            case "ExpectedReturnDate":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.ExpectedReturnDateText);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.ExpectedReturnDateText = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.AddAttribute(6, "Placeholder", "yyyy-MM-dd");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.ExpectedReturnDate?.ToString("yyyy-MM-dd") ?? "");
                }
                break;

            case "IsUrgent":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudCheckBox<bool>>(0);
                    builder.AddAttribute(1, "Value", cache.IsUrgent);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<bool>(this, v => cache.IsUrgent = v));
                    builder.AddAttribute(3, "Dense", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.GetYesNoText(item.IsUrgent));
                }
                break;

            case "ProductStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetProductStatusColor(item.ProductStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetProductStatusText(item.ProductStatus))));
                builder.CloseComponent();
                break;

            case "TotalRecoveredQuantity":
                builder.AddContent(0, item.TotalRecoveredQuantity ?? 0);
                break;
            case "TotalRecoveredWeight":
                builder.AddContent(0, $"{(int)(item.TotalRecoveredWeight ?? 0)}");
                break;
            case "TotalUnprocessedQuantity":
                builder.AddContent(0, item.TotalUnprocessedQuantity ?? 0);
                break;
            case "TotalUnprocessedWeight":
                builder.AddContent(0, $"{(int)(item.TotalUnprocessedWeight ?? 0)}");
                break;

            case "ActualRecoveryDate":
                builder.AddContent(0, item.ActualRecoveryDate?.ToString("yyyy-MM-dd") ?? "");
                break;

            case "RecoveryRemark":
                builder.AddContent(0, item.RecoveryRemark ?? "");
                break;

            case "Remark":
                if (isEditing && cache != null)
                    RenderEditTextField(builder, cache.Remark ?? "", v => cache.Remark = v);
                else
                    builder.AddContent(0, item.Remark ?? "");
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

    private void RenderEditTextField(RenderTreeBuilder builder, string value, Action<string> onChanged)
    {
        builder.OpenComponent<MudTextField<string>>(0);
        builder.AddAttribute(1, "Dense", true);
        builder.AddAttribute(2, "Variant", Variant.Outlined);
        builder.AddAttribute(3, "Value", value);
        builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, onChanged));
        builder.AddAttribute(5, "Class", "compact-input");
        builder.CloseComponent();
    }

    private void RenderEditIntField(RenderTreeBuilder builder, int? value, Action<int?> onChanged)
    {
        builder.OpenComponent<MudNumericField<int?>>(0);
        builder.AddAttribute(1, "Dense", true);
        builder.AddAttribute(2, "Variant", Variant.Outlined);
        builder.AddAttribute(3, "Value", value);
        builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<int?>(this, onChanged));
        builder.AddAttribute(5, "Class", "compact-input");
        builder.AddAttribute(6, "HideSpinButtons", true);
        builder.CloseComponent();
    }

    private void RenderEditDecimalField(RenderTreeBuilder builder, decimal? value, Action<decimal?> onChanged)
    {
        builder.OpenComponent<MudNumericField<decimal?>>(0);
        builder.AddAttribute(1, "Dense", true);
        builder.AddAttribute(2, "Variant", Variant.Outlined);
        builder.AddAttribute(3, "Value", value);
        builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, onChanged));
        builder.AddAttribute(5, "Class", "compact-input");
        builder.AddAttribute(6, "HideSpinButtons", true);
        builder.AddAttribute(7, "Format", "G29");
        builder.CloseComponent();
    }

    private void StartEdit(SectionOutsourceDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new EditCache
        {
            SendQuantity = item.SendQuantity,
            SendWeight = item.SendWeight,
            OutsourceVendor = item.OutsourceVendor,
            OutsourceSpec = item.OutsourceSpec,
            ExpectedReturnDateText = item.ExpectedReturnDate?.ToString("yyyy-MM-dd"),
            IsUrgent = item.IsUrgent,
            IsInternal = item.IsInternal,
            Remark = item.Remark
        };
    }

    private void CancelEdit(SectionOutsourceDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(SectionOutsourceDto item)
    {
        _isSaving = true;
        try
        {
            var cache = _editCache.GetValueOrDefault(item.Id);
            if (cache == null) return;

            var request = new UpdateSectionOutsourceRequest
            {
                SendQuantity = cache.SendQuantity,
                SendWeight = cache.SendWeight,
                OutsourceVendor = cache.OutsourceVendor,
                OutsourceSpec = cache.OutsourceSpec,
                ExpectedReturnDate = DateTime.TryParse(cache.ExpectedReturnDateText, out var erd) ? erd : null,
                IsUrgent = cache.IsUrgent,
                IsInternal = cache.IsInternal,
                Remark = cache.Remark
            };

            var result = await SectionOutsourceService.UpdateAsync(item.Id, request);
            if (result.Success)
            {
                Snackbar.Add("更新成功", Severity.Success);
                _editingIds.Remove(item.Id);
                _editCache.Remove(item.Id);
                if (table != null) await table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message, Severity.Error);
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    // ========== 删除 ==========

    private async Task DeleteItem(SectionOutsourceDto item)
    {
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("确认删除", new DialogParameters
        {
            ["ContentText"] = $"确定要删除 \"{item.OutsourceVendor} - {ProcessDisplayHelper.GetProcessNameText(item.ProcessName)}/{SectionDisplayHelper.GetSectionNameText(item.SectionName)}\" 的委外记录吗？",
            ["ConfirmText"] = "删除"
        });

        var result = await dialog.Result;
        if (result.Canceled) return;

        var response = await SectionOutsourceService.DeleteAsync(item.Id);
        if (response.Success)
        {
            Snackbar.Add("删除成功", Severity.Success);
            if (table != null) await table.ReloadServerData();
            await LoadFilterContextsAsync();
        }
        else
        {
            Snackbar.Add(response.Message, Severity.Error);
        }
    }

    // ========== 打印 ==========

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的记录", Severity.Warning);
            return;
        }

        var columns = _visibleColumns
            .Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label })
            .ToList();

        var request = new SectionOutsourcePrintBatchRequest { Ids = selectedIds.ToArray(), Columns = columns };
        var apiUrl = $"{Http.BaseAddress}api/section-outsource/print-selected-file";
        var json = JsonSerializer.Serialize(request);
        Snackbar.Add("正在生成PDF...", Severity.Info);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }

    private async Task PrintAll()
    {
        var columns = _visibleColumns
            .Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label })
            .ToList();

        var request = new SectionOutsourcePrintAllRequest
        {
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword.Trim(),
            SortBy = sortColumn,
            IsDescending = sortDescending,
            SendOutDateFrom = DateTime.TryParse(_dateFrom, out var df) ? df : null,
            SendOutDateTo = DateTime.TryParse(_dateTo, out var dt) ? dt : null,
            Columns = columns
        };
        var apiUrl = $"{Http.BaseAddress}api/section-outsource/print-all-file";
        var json = JsonSerializer.Serialize(request);
        Snackbar.Add("正在生成PDF...", Severity.Info);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }

    private void NavigateToCreate() => Navigation.NavigateTo("/section-outsources/create");

    private void NavigateToBatchRecovery()
    {
        // 厂内（虚拟发外）记录无需回收，从已选中集合中排除
        var pendingIds = selectedIds.Where(id => _pageItems.Any(i => i.Id == id && !i.IsInternal)).ToList();
        if (!pendingIds.Any())
        {
            Snackbar.Add("请选择待回收的委外记录（厂内记录无需回收）", Severity.Warning);
            return;
        }
        var ids = string.Join(",", pendingIds);
        Navigation.NavigateTo($"/section-outsources/create-recovery?ids={ids}");
    }

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

        // 操作列占位，对齐表格最右侧的操作按钮列
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0,
            GroupName = "",
            TotalWidth = 90,
            ColumnCount = 0,
            CssClass = ""
        });

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

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        if (!string.IsNullOrEmpty(_dateFrom)) extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrEmpty(_dateTo)) extras["dateTo"] = _dateTo;
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPageIndex,
            Extras = extras
        };
        await PageState.SaveAsync("sectionoutsources", state);
    }
}
