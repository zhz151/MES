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
using Microsoft.AspNetCore.Components.Rendering;

namespace MES.Blazor.Pages.Batches;

public partial class SectionOutsources
{
    private MudTable<SectionOutsourceDto>? table;
    private List<SectionOutsourceDto> _pageItems = new();
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
    private int _currentPageIndex;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    private string sortColumn = "createdtime";
    private bool sortDescending = true;

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
        new() { Key = "BatchNo",             Label = "生产编号",     SortKey = "batchno",             FilterType = "string", Width = "120" },
        new() { Key = "ProcessName",         Label = "工序名称",     SortKey = "processname",         FilterType = "string", Width = "120" },
        new() { Key = "ManufacturingSpec",   Label = "制造规格",     SortKey = "manufacturingspec",   FilterType = "string", Width = "120" },
        new() { Key = "SectionName",         Label = "工段名称",     SortKey = "sectionname",         FilterType = "string", Width = "120" },
        new() { Key = "SequenceNumber",      Label = "组内序号",     SortKey = "sequencenumber", Width = "45" },
        new() { Key = "OutsourceVendor",     Label = "委外单位",     SortKey = "outsourcevendor",     FilterType = "string", Width = "120" },
        new() { Key = "SendOutDate",         Label = "发出日期",     SortKey = "sendoutdate",         FilterType = "date", Width = "120" },
        new() { Key = "SendQuantity",        Label = "发出支数",     SortKey = "sendquantity", Width = "80" },
        new() { Key = "SendWeight",          Label = "发出重量",     SortKey = "sendweight", Width = "80" },
        new() { Key = "Status",              Label = "状态",         SortKey = "status",              FilterType = "enum", Width = "120",
            EnumOptions = new() { new("PendingRecovery", "待回收"), new("Recovered", "已回收"), new("InProgress", "在轧") } },
        new() { Key = "TagNo",               Label = "挂牌号",       SortKey = "tagno",               FilterType = "string", Width = "120" },
        new() { Key = "PlantGrade",          Label = "工厂牌号",     SortKey = "plantgrade",          FilterType = "string", Width = "120" },
        new() { Key = "OutsourceSpec",       Label = "委外规格",     SortKey = "outsourcespec",       FilterType = "string", Width = "120" },
        new() { Key = "ExpectedReturnDate",  Label = "要求收回日期", SortKey = "expectedreturndate",  FilterType = "date", Width = "120" },
        new() { Key = "IsUrgent",            Label = "紧急",         SortKey = "isurgent",            FilterType = "boolean", BoolTrueLabel = "是", BoolFalseLabel = "否", Width = "60" },
        new() { Key = "TotalRecoveredQuantity",     Label = "正常回收(支)",  SortKey = "totalrecoveredquantity", Width = "80" },
        new() { Key = "TotalRecoveredWeight",       Label = "正常回收(重)",  SortKey = "totalrecoveredweight", Width = "80" },
        new() { Key = "TotalUnprocessedQuantity",   Label = "非正常回收(支)", SortKey = "totalunprocessedquantity", Width = "80" },
        new() { Key = "TotalUnprocessedWeight",     Label = "非正常回收(重)", SortKey = "totalunprocessedweight", Width = "80" },
        new() { Key = "ActualRecoveryDate",  Label = "实际回收日期", SortKey = "actualrecoverydate",  FilterType = "date", Width = "120" },
        new() { Key = "Remark",              Label = "备注",         SortKey = "remark",              FilterType = "string", Width = "120" },
        new() { Key = "CreatedTime",         Label = "创建时间",     SortKey = "createdtime", Width = "120" },
        new() { Key = "UpdatedTime",         Label = "更新时间",     SortKey = "updatedtime", Width = "120" },
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
                filters: filtersJson);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPageIndex = result.Data.PageIndex;
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
            foreach (var col in _allColumns)
            {
                var savedCol = saved.FirstOrDefault(c => c.Key == col.Key);
                if (savedCol != null)
                {
                    col.Visible = savedCol.Visible;
                }
            }
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
            case "ProcessName":
                builder.AddContent(0, item.ProcessName);
                break;
            case "ManufacturingSpec":
                builder.AddContent(0, DisplayHelper.FormatSpecification(item.ManufacturingSpec ?? ""));
                break;
            case "SectionName":
                builder.AddContent(0, item.SectionName);
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

            case "Remark":
                if (isEditing && cache != null)
                    RenderEditTextField(builder, cache.Remark ?? "", v => cache.Remark = v);
                else
                    builder.AddContent(0, item.Remark ?? "");
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
            ["ContentText"] = $"确定要删除 \"{item.OutsourceVendor} - {item.ProcessName}/{item.SectionName}\" 的委外记录吗？",
            ["ConfirmText"] = "删除"
        });

        var result = await dialog.Result;
        if (result.Canceled) return;

        var response = await SectionOutsourceService.DeleteAsync(item.Id);
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
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的记录", Severity.Warning);
            return;
        }

        var columns = _visibleColumns
            .Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label })
            .ToList();

        var result = await SectionOutsourceService.PrintSelectedAsync(selectedIds.ToArray(), columns);
        if (result.Success)
            await JS.InvokeVoidAsync("openPdf", result.Data);
        else
            Snackbar.Add(result.Message, Severity.Error);
    }

    private async Task PrintAll()
    {
        var columns = _visibleColumns
            .Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label })
            .ToList();

        var result = await SectionOutsourceService.PrintAllAsync(
            keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword.Trim(),
            sortBy: sortColumn,
            isDescending: sortDescending,
            columns: columns);
        if (result.Success)
            await JS.InvokeVoidAsync("openPdf", result.Data);
        else
            Snackbar.Add(result.Message, Severity.Error);
    }

    private void NavigateToCreate() => Navigation.NavigateTo("/section-outsources/create");

    private void NavigateToBatchRecovery()
    {
        if (!selectedIds.Any()) return;
        var ids = string.Join(",", selectedIds);
        Navigation.NavigateTo($"/section-outsources/create-recovery?ids={ids}");
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
            PageIndex = _currentPageIndex,
            Extras = extras
        };
        await PageState.SaveAsync("sectionoutsources", state);
    }
}
