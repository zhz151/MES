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

public partial class PicklingInRecords
{
    private MudTable<PicklingInRecordDto>? table;
    private List<PicklingInRecordDto> _pageItems = new();
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

    // ========== 内联编辑 ==========
    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();

    private class EditCache
    {
        public string? EquipmentName { get; set; }
        public string? Operator { get; set; }
        public string? Shift { get; set; }
        public int? Quantity { get; set; }
        public decimal? Weight { get; set; }
        public bool IsFinished { get; set; }
        public string? Remark { get; set; }

        // 备份原始值用于取消
        public string? OriginalEquipmentName { get; set; }
        public string? OriginalOperator { get; set; }
        public string? OriginalShift { get; set; }
        public int? OriginalQuantity { get; set; }
        public decimal? OriginalWeight { get; set; }
        public bool OriginalIsFinished { get; set; }
        public string? OriginalRemark { get; set; }
    }

    // ========== 分页汇总 ==========
    private Dictionary<string, string> _pageSums = new();

    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "Quantity",
        "Weight"
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
        // G1: 去油/酸洗信息
        new() { Key = "BatchNo",             Label = "生产编号",     SortKey = "batchno",             FilterType = "string", Width = "120", GroupKey = 1, GroupName = "去油/酸洗信息" },
        new() { Key = "ProcessName",         Label = "工序名称",     SortKey = "processname",         FilterType = "string", Width = "120", GroupKey = 1, GroupName = "去油/酸洗信息" },
        new() { Key = "ManufacturingSpec",   Label = "制造规格",     SortKey = "manufacturingspec",   FilterType = "string", Width = "120", GroupKey = 1, GroupName = "去油/酸洗信息" },
        new() { Key = "SequenceNumber",      Label = "执行序号",     SortKey = "sequencenumber",      FilterType = "string", Width = "80",  GroupKey = 1, GroupName = "去油/酸洗信息" },
        new() { Key = "InDate",              Label = "入缸日期",     SortKey = "indate",              FilterType = "date",   Width = "120", GroupKey = 1, GroupName = "去油/酸洗信息" },
        new() { Key = "SectionName",         Label = "工段名称",     SortKey = "sectionname",         FilterType = "string", Width = "100", GroupKey = 1, GroupName = "去油/酸洗信息" },
        new() { Key = "EquipmentName",       Label = "设备名称",     SortKey = "equipmentname",       FilterType = "string", Width = "100", GroupKey = 1, GroupName = "去油/酸洗信息" },
        new() { Key = "Operator",            Label = "操作人",       SortKey = "operator",            FilterType = "string", Width = "80",  GroupKey = 1, GroupName = "去油/酸洗信息" },
        new() { Key = "Shift",               Label = "班次",         SortKey = "shift",               FilterType = "string", Width = "80",  GroupKey = 1, GroupName = "去油/酸洗信息" },
        new() { Key = "Quantity",            Label = "加工支数",     SortKey = "quantity",                                       Width = "80",  GroupKey = 1, GroupName = "去油/酸洗信息" },
        new() { Key = "Weight",              Label = "加工重量",     SortKey = "weight",                                         Width = "80",  GroupKey = 1, GroupName = "去油/酸洗信息" },
        new() { Key = "IsFinished",          Label = "是否成品",     SortKey = "isfinished",          FilterType = "boolean", BoolTrueLabel = "是", BoolFalseLabel = "否", Width = "80", GroupKey = 1, GroupName = "去油/酸洗信息" },
        new() { Key = "TagNo",               Label = "挂牌号",       SortKey = "tagno",               FilterType = "string", Width = "120", GroupKey = 1, GroupName = "去油/酸洗信息" },
        new() { Key = "PlantGrade",          Label = "工厂牌号",     SortKey = "plantgrade",          FilterType = "string", Width = "120", GroupKey = 1, GroupName = "去油/酸洗信息" },
        new() { Key = "Remark",              Label = "备注",         SortKey = "remark",              FilterType = "string", Width = "120", GroupKey = 1, GroupName = "去油/酸洗信息" },
        new() { Key = "DataSource",          Label = "数据来源",     SortKey = "datasource",          FilterType = "enum",   Width = "80",  GroupKey = 1, GroupName = "去油/酸洗信息",
            EnumOptions = new() { new("SCAN", "扫码"), new("MANUAL", "手动") } },
        new() { Key = "UpdatedTime",         Label = "更新时间",     SortKey = "updatedtime",                                   Width = "120", GroupKey = 1, GroupName = "去油/酸洗信息" },
        // G2: 完工信息
        new() { Key = "Status",              Label = "状态",         SortKey = "status",              FilterType = "enum",   Width = "100", GroupKey = 2, GroupName = "完工信息",
            EnumOptions = new() { new("Soaking", "浸泡中"), new("Completed", "已完工") } },
        new() { Key = "CompleteDate",        Label = "完工日期",     SortKey = "completedate",        FilterType = "date",   Width = "120", GroupKey = 2, GroupName = "完工信息" },
    };

    // ========== 分页汇总计算 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(PicklingInRecordDto)
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

    private void StartEdit(PicklingInRecordDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new EditCache
        {
            EquipmentName = item.EquipmentName,
            Operator = item.Operator,
            Shift = item.Shift,
            Quantity = item.Quantity,
            Weight = item.Weight,
            IsFinished = item.IsFinished,
            Remark = item.Remark,
            OriginalEquipmentName = item.EquipmentName,
            OriginalOperator = item.Operator,
            OriginalShift = item.Shift,
            OriginalQuantity = item.Quantity,
            OriginalWeight = item.Weight,
            OriginalIsFinished = item.IsFinished,
            OriginalRemark = item.Remark
        };
    }

    private async Task SaveEdit(PicklingInRecordDto item)
    {
        var cache = _editCache.GetValueOrDefault(item.Id);
        if (cache == null) return;

        var request = new UpdatePicklingInRecordRequest
        {
            EquipmentName = cache.EquipmentName,
            Operator = cache.Operator,
            Shift = cache.Shift,
            Quantity = cache.Quantity,
            Weight = cache.Weight,
            IsFinished = cache.IsFinished,
            Remark = cache.Remark
        };

        var result = await PicklingService.UpdateAsync(item.Id, request);
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

    private void CancelEdit(PicklingInRecordDto item)
    {
        var cache = _editCache.GetValueOrDefault(item.Id);
        if (cache != null)
        {
            item.EquipmentName = cache.OriginalEquipmentName;
            item.Operator = cache.OriginalOperator;
            item.Shift = cache.OriginalShift;
            item.Quantity = cache.OriginalQuantity;
            item.Weight = cache.OriginalWeight;
            item.IsFinished = cache.OriginalIsFinished;
            item.Remark = cache.OriginalRemark;
        }
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    // ========== 完工对话框 ==========

    private async Task OpenCompleteDialog(PicklingInRecordDto item)
    {
        var parameters = new DialogParameters
        {
            { "PicklingInRecordId", item.Id },
            { "BatchNo", item.BatchNo },
            { "SectionName", item.SectionName },
            { "InDate", item.InDate }
        };
        var dialog = await DialogService.ShowAsync<PicklingCompleteDialog>("完工登记", parameters);
        var result = await dialog.Result;
        if (!result.Canceled && table != null)
            await table.ReloadServerData();
    }

    // ========== 删除 ==========

    private async Task ConfirmDelete(PicklingInRecordDto item)
    {
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("确认删除", new DialogParameters
        {
            ["ContentText"] = $"确定要删除批次「{item.BatchNo}」的入缸记录吗？",
            ["ConfirmText"] = "删除"
        });
        var result = await dialog.Result;
        if (!result.Canceled)
        {
            var apiResult = await PicklingService.DeleteAsync(item.Id);
            if (apiResult.Success)
            {
                Snackbar.Add("删除成功", Severity.Success);
                if (table != null) await table.ReloadServerData();
            }
            else
            {
                Snackbar.Add($"删除失败: {apiResult.Message}", Severity.Error);
            }
        }
    }

    // ========== 导航 ==========

    private void NavigateToCreate()
    {
        Navigation.NavigateTo("/pickling-in-records/create");
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<PicklingInRecordDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            var sortCol = _allColumns.FirstOrDefault(c => c.Key == sortColumn);
            var sortBy = sortCol?.SortKey ?? sortColumn ?? "createdtime";
            var filtersJson = SerializeFilters();

            var result = await PicklingService.GetPagedAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
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

        return new TableData<PicklingInRecordDto>
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

    // ========== 筛选上下文加载 ==========

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await PicklingService.GetFilterContextsAsync();
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
        selectedIds.Clear();
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 列显示管理 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await ColumnPrefs.SaveAsync("pickling-in-records", null, _allColumns);
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        await ColumnPrefs.SaveAsync("pickling-in-records", null, _allColumns);
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        await ColumnPrefs.SaveAsync("pickling-in-records", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await ColumnPrefs.SaveAsync("pickling-in-records", null, _allColumns);
        if (table != null) await table.ReloadServerData();
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("pickling-in-records", null);
        if (saved.Count > 0)
        {
            // 恢复列顺序：按保存列表排序，未保存的列追加到末尾
            var ordered = saved
                .Select(s => _allColumns.FirstOrDefault(c => c.Key == s.Key))
                .Where(c => c != null)
                .Cast<ColumnDef>()
                .ToList();
            var unsaved = _allColumns.Where(c => !saved.Any(s => s.Key == c.Key)).ToList();
            foreach (var col in ordered)
                col.Visible = saved.First(s => s.Key == col.Key).Visible;
            _allColumns = ordered.Concat(unsaved).ToList();
        }

        var savedState = await PageState.LoadAsync("picklinginrecords");
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

        // 状态恢复后重新加载表格数据
        if (savedState != null && table != null)
            await table.ReloadServerData();

        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#pickling-in-records-list-table");
        }
        catch { }

        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#pickling-in-records-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 动态单元格渲染 ==========

    private RenderFragment RenderCell(PicklingInRecordDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing ? _editCache.GetValueOrDefault(item.Id) : null;

        switch (col.Key)
        {
            case "BatchNo":
                builder.AddContent(0, item.BatchNo);
                break;
            case "ProcessName":
                builder.AddContent(0, item.ProcessName);
                break;
            case "ManufacturingSpec":
                builder.AddContent(0, item.ManufacturingSpec);
                break;
            case "SequenceNumber":
                builder.AddContent(0, item.SequenceNumber);
                break;
            case "InDate":
                builder.AddContent(0, item.InDate.ToString("yyyy-MM-dd"));
                break;
            case "SectionName":
                builder.AddContent(0, item.SectionName);
                break;
            case "EquipmentName":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.EquipmentName);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.EquipmentName = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.EquipmentName);
                }
                break;
            case "Operator":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.Operator);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.Operator = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Operator);
                }
                break;
            case "Shift":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.Shift);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.Shift = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Shift);
                }
                break;
            case "Quantity":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.Quantity);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.Quantity = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableInt(item.Quantity));
                }
                break;
            case "Weight":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<decimal?>>(0);
                    builder.AddAttribute(1, "Value", cache.Weight);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, v => cache.Weight = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.AddAttribute(5, "Format", "G29");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, $"{(int)(item.Weight ?? 0)}");
                }
                break;
            case "IsFinished":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudSwitch<bool>>(0);
                    builder.AddAttribute(1, "Value", cache.IsFinished);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<bool>(this, async v =>
                    {
                        cache.IsFinished = v;
                        var request = new UpdatePicklingInRecordRequest { IsFinished = v };
                        var result = await PicklingService.UpdateAsync(item.Id, request);
                        if (!result.Success)
                            Snackbar.Add($"保存失败: {result.Message}", Severity.Error);
                    }));
                    builder.CloseComponent();
                }
                else
                {
                    if (item.IsFinished)
                    {
                        builder.OpenComponent<MudChip>(0);
                        builder.AddAttribute(1, "Size", Size.Small);
                        builder.AddAttribute(2, "Color", Color.Success);
                        builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "是")));
                        builder.CloseComponent();
                    }
                    else
                    {
                        builder.OpenComponent<MudChip>(0);
                        builder.AddAttribute(1, "Size", Size.Small);
                        builder.AddAttribute(2, "Color", Color.Default);
                        builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "否")));
                        builder.CloseComponent();
                    }
                }
                break;
            case "TagNo":
                builder.AddContent(0, item.TagNo);
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade);
                break;
            case "Status":
                if (item.Status == "Completed")
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Success);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "已完工")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Warning);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "浸泡中")));
                    builder.CloseComponent();
                }
                break;
            case "CompleteDate":
                builder.AddContent(0, item.CompleteDate?.ToString("yyyy-MM-dd"));
                break;
            case "Remark":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.Remark);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.Remark = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Remark);
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
        }
    };

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

        var request = new PicklingInRecordPrintBatchRequest { Ids = selectedIds.ToArray(), Columns = columns };
        var apiUrl = $"{Http.BaseAddress}api/pickling/print-selected-file";
        var json = JsonSerializer.Serialize(request);
        Snackbar.Add("正在生成PDF...", Severity.Info);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }

    private async Task PrintAll()
    {
        var columns = _visibleColumns
            .Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label })
            .ToList();

        var request = new PicklingInRecordPrintAllRequest
        {
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword.Trim(),
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Columns = columns
        };
        var apiUrl = $"{Http.BaseAddress}api/pickling/print-all-file";
        var json = JsonSerializer.Serialize(request);
        Snackbar.Add("正在生成PDF...", Severity.Info);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
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

        // 选择列占位（40px）
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

        // 操作列占位（90px）
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
            3 => "col-g3",
            4 => "col-g4",
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
            3 => "col-g3-cell",
            4 => "col-g4-cell",
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

        await PageState.SaveAsync("picklinginrecords", new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = _searchKeyword,
            PageIndex = _currentPageIndex > 0 ? _currentPageIndex - 1 : 0,
            Extras = extras
        });
    }
}
