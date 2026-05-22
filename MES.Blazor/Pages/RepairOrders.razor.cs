using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Blazor.Shared;
using MES.Core.DTOs;
using MES.Core.Models;
using System.Text.Json;

namespace MES.Blazor.Pages;

public partial class RepairOrders
{
    private MudTable<RepairOrderListDto>? table;
    private List<RepairOrderListDto> _pageItems = new();
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
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    private string sortColumn = "reporttime";
    private bool sortDescending = true;

    // ========== ExcelFilter 状态 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 内联编辑 ==========
    private HashSet<int> _editingIds = new();
    private Dictionary<int, RepairEditCache> _editCache = new();

    private class RepairEditCache
    {
        public string FaultDescription { get; set; } = "";
        public string? FaultType { get; set; }
        public string Priority { get; set; } = "Normal";
        public string ReportPerson { get; set; } = "";
        public string ReportTimeText { get; set; } = "";
        public string? RepairPerson { get; set; }
        public string? RepairStartTimeText { get; set; }
        public string? RepairEndTimeText { get; set; }
        public string? RepairContent { get; set; }
        public string? SparePartUsed { get; set; }
    }

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "RepairOrderNo",    Label = "工单号",     SortKey = "repairorderno", FilterType = "string" },
        new() { Key = "EquipmentName",    Label = "设备名称",    SortKey = "equipmentname", FilterType = "string" },
        new() { Key = "EquipmentLocation", Label = "所在区域",   SortKey = "location", FilterType = "string" },
        new() { Key = "EquipmentCode",    Label = "设备编号",    SortKey = "equipmentcode", FilterType = "string" },
        new() { Key = "FaultDescription", Label = "故障描述",   SortKey = "faultdescription", FilterType = "string" },
        new() { Key = "FaultType",      Label = "故障类型",   SortKey = "faulttype", FilterType = "string" },
        new() { Key = "Priority",       Label = "优先级",     SortKey = "priority", FilterType = "enum",
            EnumOptions = new() { new("Normal", "普通"), new("Urgent", "紧急"), new("Emergency", "特急") } },
        new() { Key = "RepairStatus",   Label = "维修状态", FilterType = "enum",
            EnumOptions = new() { new("Pending", "待维修"), new("InProgress", "维修中"), new("Completed", "完成") } },
        new() { Key = "ReportPerson",   Label = "报修人",     SortKey = "reportperson", FilterType = "string" },
        new() { Key = "ReportTime",     Label = "报修时间",   SortKey = "reporttime", FilterType = "date" },
        new() { Key = "RepairPerson",   Label = "维修人",     SortKey = "repairperson", FilterType = "string" },
        new() { Key = "RepairStartTime",Label = "开始时间",   SortKey = "repairstarttime", FilterType = "date" },
        new() { Key = "RepairEndTime",  Label = "完成时间",   SortKey = "repairendtime", FilterType = "date" },
        new() { Key = "RepairContent",  Label = "维修内容", SortKey = "repaircontent", FilterType = "string" },
        new() { Key = "SparePartUsed",  Label = "更换备件", SortKey = "sparepartused", FilterType = "string" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<RepairOrderListDto>> LoadDataFromServer(TableState state)
    {
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "reporttime";
            var filtersJson = SerializeFilters();

            var query = new RepairOrderQueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = string.IsNullOrEmpty(sortBy) ? "reporttime" : sortBy,
                IsDescending = sortDescending
            };
            if (filtersJson != null)
                query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson);

            var result = await RepairOrderService.GetPagedAsync(query);

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

        return new TableData<RepairOrderListDto>
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
            var result = await RepairOrderService.GetFilterContextsAsync();
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

        // RepairStatus 列显示中文
        if (_filterContextOptions.TryGetValue("RepairStatus", out var statusOptions))
        {
            foreach (var opt in statusOptions)
            {
                opt.Display = DisplayHelper.GetRepairOrderStatusText(opt.Value);
            }
        }

        // Priority 列显示中文
        if (_filterContextOptions.TryGetValue("Priority", out var priorityOptions))
        {
            foreach (var opt in priorityOptions)
            {
                opt.Display = DisplayHelper.GetPriorityText(opt.Value);
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
        await ColumnPrefs.SaveAsync("repair-orders", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        if (table != null) await table.ReloadServerData();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(RepairOrderListDto item, ColumnDef col) => builder =>
    {
        if (_editingIds.Contains(item.Id) && _editCache.TryGetValue(item.Id, out var cache))
        {
            // 系统/只读字段在编辑模式下仍然只读显示
            if (col.Key is "RepairOrderNo" or "EquipmentName" or "EquipmentLocation" or "EquipmentCode" or "RepairStatus")
            {
                RenderReadonlyCell(item, col)(builder);
                return;
            }
            RenderEditCell(cache, col)(builder);
            return;
        }

        switch (col.Key)
        {
            case "RepairOrderNo":
                builder.AddContent(0, item.RepairOrderNo);
                break;
            case "EquipmentName":
                builder.AddContent(0, item.EquipmentName);
                break;
            case "EquipmentLocation":
                builder.AddContent(0, item.EquipmentLocation);
                break;
            case "EquipmentCode":
                builder.AddContent(0, item.EquipmentCode);
                break;
            case "FaultDescription":
                builder.AddContent(0, item.FaultDescription);
                break;
            case "FaultType":
                builder.AddContent(0, item.FaultType);
                break;
            case "Priority":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetPriorityColor(item.Priority));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetPriorityText(item.Priority))));
                builder.CloseComponent();
                break;
            case "RepairStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetRepairOrderStatusColor(item.RepairStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetRepairOrderStatusText(item.RepairStatus))));
                builder.CloseComponent();
                break;
            case "ReportPerson":
                builder.AddContent(0, item.ReportPerson);
                break;
            case "ReportTime":
                builder.AddContent(0, item.ReportTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            case "RepairPerson":
                builder.AddContent(0, item.RepairPerson);
                break;
            case "RepairStartTime":
                builder.AddContent(0, item.RepairStartTime?.ToString("yyyy-MM-dd HH:mm"));
                break;
            case "RepairEndTime":
                builder.AddContent(0, item.RepairEndTime?.ToString("yyyy-MM-dd HH:mm"));
                break;
            case "RepairContent":
                builder.AddContent(0, item.RepairContent);
                break;
            case "SparePartUsed":
                builder.AddContent(0, item.SparePartUsed);
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    private RenderFragment RenderReadonlyCell(RepairOrderListDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "RepairOrderNo":
                builder.AddContent(0, item.RepairOrderNo);
                break;
            case "EquipmentName":
                builder.AddContent(0, item.EquipmentName);
                break;
            case "EquipmentLocation":
                builder.AddContent(0, item.EquipmentLocation);
                break;
            case "EquipmentCode":
                builder.AddContent(0, item.EquipmentCode);
                break;
            case "RepairStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetRepairOrderStatusColor(item.RepairStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetRepairOrderStatusText(item.RepairStatus))));
                builder.CloseComponent();
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    private RenderFragment RenderEditCell(RepairEditCache cache, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "FaultDescription":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.FaultDescription);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.FaultDescription = v));
                builder.CloseComponent();
                break;
            case "FaultType":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.FaultType);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.FaultType = v));
                builder.CloseComponent();
                break;
            case "Priority":
                builder.OpenComponent<MudSelect<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.Priority);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.Priority = v));
                builder.AddAttribute(6, "ChildContent", (RenderFragment)(cb =>
                {
                    cb.OpenComponent<MudSelectItem<string>>(0);
                    cb.AddAttribute(1, "Value", "Normal");
                    cb.AddAttribute(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, "普通")));
                    cb.CloseComponent();
                    cb.OpenComponent<MudSelectItem<string>>(0);
                    cb.AddAttribute(1, "Value", "Urgent");
                    cb.AddAttribute(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, "紧急")));
                    cb.CloseComponent();
                    cb.OpenComponent<MudSelectItem<string>>(0);
                    cb.AddAttribute(1, "Value", "Emergency");
                    cb.AddAttribute(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, "特急")));
                    cb.CloseComponent();
                }));
                builder.CloseComponent();
                break;
            case "ReportPerson":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.ReportPerson);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.ReportPerson = v));
                builder.CloseComponent();
                break;
            case "ReportTime":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.ReportTimeText);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.ReportTimeText = v));
                builder.AddAttribute(6, "Placeholder", "yyyy-MM-dd HH:mm");
                builder.CloseComponent();
                break;
            case "RepairPerson":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.RepairPerson);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.RepairPerson = v));
                builder.CloseComponent();
                break;
            case "RepairStartTime":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.RepairStartTimeText);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.RepairStartTimeText = v));
                builder.AddAttribute(6, "Placeholder", "yyyy-MM-dd HH:mm");
                builder.CloseComponent();
                break;
            case "RepairEndTime":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.RepairEndTimeText);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.RepairEndTimeText = v));
                builder.AddAttribute(6, "Placeholder", "yyyy-MM-dd HH:mm");
                builder.CloseComponent();
                break;
            case "RepairContent":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.RepairContent);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.RepairContent = v));
                builder.CloseComponent();
                break;
            case "SparePartUsed":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Size", Size.Small);
                builder.AddAttribute(4, "Value", cache.SparePartUsed);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.SparePartUsed = v));
                builder.CloseComponent();
                break;
        }
    };

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("repair-orders", null);
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
        var savedState = await PageState.LoadAsync("repair-orders");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "reporttime";
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

        // 加载筛选上下文
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#repair-orders-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 导航 ==========

    private void NavigateToCreate() => Navigation.NavigateTo("/repair-orders/create");

    // ========== 内联编辑 ==========

    private void StartEdit(RepairOrderListDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new RepairEditCache
        {
            FaultDescription = item.FaultDescription,
            FaultType = item.FaultType,
            Priority = item.Priority,
            ReportPerson = item.ReportPerson,
            ReportTimeText = item.ReportTime.ToString("yyyy-MM-dd HH:mm"),
            RepairPerson = item.RepairPerson,
            RepairStartTimeText = item.RepairStartTime?.ToString("yyyy-MM-dd HH:mm"),
            RepairEndTimeText = item.RepairEndTime?.ToString("yyyy-MM-dd HH:mm"),
            RepairContent = item.RepairContent,
            SparePartUsed = item.SparePartUsed,
        };
    }

    private void CancelEdit(int id)
    {
        _editingIds.Remove(id);
        _editCache.Remove(id);
    }

    private async Task SaveEdit(int id)
    {
        if (!_editCache.TryGetValue(id, out var cache)) return;

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(cache.FaultDescription))
            errors.Add("故障描述不能为空");
        if (string.IsNullOrWhiteSpace(cache.ReportPerson))
            errors.Add("报修人不能为空");

        DateTime? reportTime = null;
        if (string.IsNullOrWhiteSpace(cache.ReportTimeText))
            errors.Add("报修时间不能为空");
        else if (!DateTime.TryParse(cache.ReportTimeText, out var parsedReport))
            errors.Add("报修时间格式无效");
        else
            reportTime = parsedReport;

        DateTime? repairStartTime = null;
        if (!string.IsNullOrWhiteSpace(cache.RepairStartTimeText))
        {
            if (DateTime.TryParse(cache.RepairStartTimeText, out var parsedStart))
                repairStartTime = parsedStart;
            else
                errors.Add("开始时间格式无效");
        }

        DateTime? repairEndTime = null;
        if (!string.IsNullOrWhiteSpace(cache.RepairEndTimeText))
        {
            if (DateTime.TryParse(cache.RepairEndTimeText, out var parsedEnd))
                repairEndTime = parsedEnd;
            else
                errors.Add("完成时间格式无效");
        }

        if (errors.Any())
        {
            Snackbar.Add(string.Join("；", errors), Severity.Error);
            return;
        }

        try
        {
            var request = new UpdateRepairOrderRequest
            {
                FaultDescription = cache.FaultDescription,
                FaultType = cache.FaultType,
                Priority = cache.Priority,
                ReportPerson = cache.ReportPerson,
                ReportTime = reportTime,
                RepairPerson = cache.RepairPerson,
                RepairStartTime = repairStartTime,
                RepairEndTime = repairEndTime,
                RepairContent = cache.RepairContent,
                SparePartUsed = cache.SparePartUsed,
            };

            var result = await RepairOrderService.UpdateAsync(id, request);
            if (result.Success)
            {
                Snackbar.Add("保存成功", Severity.Success);
                _editingIds.Remove(id);
                _editCache.Remove(id);
                if (table != null) await table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "保存失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"保存失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 删除 ==========

    private async Task DeleteItem(RepairOrderListDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除维修工单 \"{item.RepairOrderNo}\" 吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await RepairOrderService.DeleteAsync(item.Id);
                if (result.Success)
                {
                    Snackbar.Add("删除成功", Severity.Success);
                    if (table != null) await table.ReloadServerData();
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

    // ========== 打印方法 ==========

    private List<PrintColumnDef> GetPrintColumnDefs()
    {
        return _visibleColumns.Select(c => new PrintColumnDef
        {
            Key = c.Key,
            Label = c.Label
        }).ToList();
    }

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的维修工单", Severity.Warning);
            return;
        }
        try
        {
            var ids = selectedIds.ToArray();
            var columns = GetPrintColumnDefs();
            var result = await RepairOrderService.PrintBatchAsync(ids, columns);
            if (result.Success && result.Data != null)
                await JS.InvokeVoidAsync("openPdf", result.Data);
            else
                Snackbar.Add(result.Message ?? "打印失败", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task PrintAll()
    {
        try
        {
            var columns = GetPrintColumnDefs();
            var query = new RepairOrderQueryParams
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortColumn,
                IsDescending = sortDescending
            };
            var result = await RepairOrderService.PrintAllAsync(query, columns);
            if (result.Success && result.Data != null)
                await JS.InvokeVoidAsync("openPdf", result.Data);
            else
                Snackbar.Add(result.Message ?? "打印失败", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
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
        await PageState.SaveAsync("repair-orders", state);
    }
}
