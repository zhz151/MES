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

namespace MES.Blazor.Pages.Batches;

public partial class ProductionRecords
{
    private MudTable<ProductionRecordDto>? table;
    private List<ProductionRecordDto> _pageItems = new();
    private int _totalCount;
    private int _pageSize = 10;
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
    private int _restoredPageIndex;
    private int _currentPageIndex;
    private bool _isFirstLoad = true;

    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;

    // 排序
    private string sortColumn = "createdtime";
    private bool sortDescending = true;

    // ========== 分页汇总 ==========
    private Dictionary<string, string> _pageSums = new();

    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "Quantity", "Weight", "PostCutQuantity",
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
        // ===== Group 1: 执行信息 =====
        new() { Key = "ExecDate",          Label = "执行日期",   SortKey = "execdate", FilterType = "date", Width = "120", GroupKey = 1, GroupName = "执行信息" },
        new() { Key = "BatchNo",           Label = "生产编号",   SortKey = "batchno",           FilterType = "string", Width = "120", GroupKey = 1, GroupName = "执行信息" },
        new() { Key = "WorkOrderNo",       Label = "工单号",     SortKey = "workorderno",       FilterType = "string", Width = "120", GroupKey = 1, GroupName = "执行信息" },
        new() { Key = "SalesOrderNo",      Label = "订单号",     SortKey = "salesorderno",      FilterType = "string", Width = "120", GroupKey = 1, GroupName = "执行信息" },
        new() { Key = "ProductionMainNo",  Label = "主号",       SortKey = "productionmainno",  FilterType = "string", Width = "120", GroupKey = 1, GroupName = "执行信息" },
        new() { Key = "ProcessName",       Label = "工序名称",   SortKey = "processname",       FilterType = "string", Width = "120", GroupKey = 1, GroupName = "执行信息" },
        new() { Key = "ManufacturingSpec", Label = "制造规格",   SortKey = "manufacturingspec", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "执行信息" },
        new() { Key = "SectionName",       Label = "工段名称",   SortKey = "sectionname",       FilterType = "string", Width = "120", GroupKey = 1, GroupName = "执行信息" },
        new() { Key = "SequenceNumber",    Label = "执行序号",   SortKey = "sequencenumber", Width = "45", GroupKey = 1, GroupName = "执行信息" },

        // ===== Group 2: 产出数据 =====
        new() { Key = "EquipmentName",     Label = "设备名称",   SortKey = "equipmentname",     FilterType = "string", Width = "120", GroupKey = 2, GroupName = "产出数据" },
        new() { Key = "Operator",          Label = "操作人",     SortKey = "operator",          FilterType = "string", Width = "120", GroupKey = 2, GroupName = "产出数据" },
        new() { Key = "Shift",             Label = "班次",       SortKey = "shift",             FilterType = "string", Width = "120", GroupKey = 2, GroupName = "产出数据" },
        new() { Key = "Quantity",          Label = "加工支数",   SortKey = "quantity", Width = "80", GroupKey = 2, GroupName = "产出数据" },
        new() { Key = "Weight",            Label = "加工重量",   SortKey = "weight", Width = "80", GroupKey = 2, GroupName = "产出数据" },
        new() { Key = "ProductStatus",      Label = "产类",       SortKey = "productstatus",         FilterType = "string", Width = "80", GroupKey = 2, GroupName = "产出数据" },
        new() { Key = "IsPreCut",           Label = "预成切",     SortKey = "isprecut",             FilterType = "enum", Width = "70", GroupKey = 2, GroupName = "产出数据",
            EnumOptions = new() { new("true", "是") } },
        new() { Key = "LengthStatus",       Label = "长度状态",   SortKey = "lengthstatus",         FilterType = "string", Width = "80", GroupKey = 2, GroupName = "产出数据" },
        new() { Key = "CuttingMultiple",   Label = "断切倍数",   SortKey = "cuttingmultiple", Width = "80", GroupKey = 2, GroupName = "产出数据" },
        new() { Key = "FinishedCutLength", Label = "成品长度",   SortKey = "finishedcutlength", Width = "80", GroupKey = 2, GroupName = "产出数据" },
        new() { Key = "PostCutQuantity",   Label = "切后支数",   SortKey = "postcutquantity", Width = "80", GroupKey = 2, GroupName = "产出数据" },
        new() { Key = "FaceCutCount",      Label = "平头数",     SortKey = "facecutcount", Width = "60", GroupKey = 2, GroupName = "产出数据" },

        // ===== Group 3: 工艺参数 =====
        new() { Key = "SolutionTemperature", Label = "固溶温度(℃)", SortKey = "solutiontemperature", Width = "80", GroupKey = 3, GroupName = "工艺参数" },
        new() { Key = "SoakTime",           Label = "保温时间(min)", SortKey = "soaktime", Width = "80", GroupKey = 3, GroupName = "工艺参数" },

        // ===== Group 4: 追溯信息 =====
        new() { Key = "TagNo",             Label = "挂牌号",     SortKey = "tagno",             FilterType = "string", Width = "120", GroupKey = 4, GroupName = "追溯信息" },
        new() { Key = "PlantGrade",        Label = "工厂牌号",   SortKey = "plantgrade",        FilterType = "string", Width = "120", GroupKey = 4, GroupName = "追溯信息" },
        new() { Key = "Remark",            Label = "备注",       SortKey = "remark",            FilterType = "string", Width = "120", GroupKey = 4, GroupName = "追溯信息" },
        new() { Key = "DataSource",        Label = "数据来源",   SortKey = "datasource",        FilterType = "enum", Width = "80", GroupKey = 4, GroupName = "追溯信息",
            EnumOptions = new() { new("SCAN", "扫码"), new("MANUAL", "手动") } },
        new() { Key = "UpdatedTime",       Label = "更新日期",   SortKey = "updatedtime", Width = "120", GroupKey = 4, GroupName = "追溯信息" },
    };

    // ========== 分页汇总计算 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(ProductionRecordDto)
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

    private async Task<TableData<ProductionRecordDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;

        // 恢复持久化的页码（MudTable 初始化时始终传 page=0）
        if (_isFirstLoad)
        {
            state.Page = _restoredPageIndex;
            _isFirstLoad = false;
        }

        try
        {
            var filtersJson = SerializeFilters();

            // sortColumn 来自 ToggleSort(col.Key)，按 c.Key == sortColumn 匹配
            var sortCol = _allColumns.FirstOrDefault(c => c.Key == sortColumn);
            var sortBy = sortCol?.SortKey ?? sortColumn ?? "createdtime";

            var result = await ProductionRecordService.GetAllProductionRecordsAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
                execDateFrom: DateTime.TryParse(_dateFrom, out var df) ? df : null,
                execDateTo: DateTime.TryParse(_dateTo, out var dt) ? dt : null,
                filters: filtersJson
            );

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPageIndex = state.Page + 1;
                ComputePageSums();
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

        // 持久化当前页码
        await SavePageStateAsync();

        return new TableData<ProductionRecordDto>
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
            var result = await ProductionRecordService.GetFilterContextsAsync();
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
            sortDescending = true;
        }
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

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string ExecDate { get; set; } = "";
        public string? EquipmentName { get; set; }
        public string? Operator { get; set; }
        public ShiftType? Shift { get; set; }
        public int? Quantity { get; set; }
        public decimal? Weight { get; set; }
        public decimal? SolutionTemperature { get; set; }
        public int? SoakTime { get; set; }
        public bool? IsPreCut { get; set; }
        public int? FaceCutCount { get; set; }
        public decimal? CuttingMultiple { get; set; }
        public decimal? FinishedCutLength { get; set; }
        public int? PostCutQuantity { get; set; }
        public string? TagNo { get; set; }
        public string? PlantGrade { get; set; }
        public string? Remark { get; set; }
    }

    private void StartEdit(ProductionRecordDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new EditCache
        {
            ExecDate = item.ExecDate.ToString("yyyy-MM-dd"),
            EquipmentName = item.EquipmentName,
            Operator = item.Operator,
            Shift = item.Shift,
            Quantity = item.Quantity,
            Weight = item.Weight,
            SolutionTemperature = item.SolutionTemperature,
            SoakTime = item.SoakTime,
            IsPreCut = item.IsPreCut,
            FaceCutCount = item.FaceCutCount,
            CuttingMultiple = item.CuttingMultiple,
            FinishedCutLength = item.FinishedCutLength,
            PostCutQuantity = item.PostCutQuantity,
            TagNo = item.TagNo,
            PlantGrade = item.PlantGrade,
            Remark = item.Remark
        };
    }

    private void CancelEdit(ProductionRecordDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(ProductionRecordDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        if (!DateTime.TryParse(cache.ExecDate, out var execDate))
        {
            Snackbar.Add("执行日期格式无效", Severity.Error);
            return;
        }
        if (string.IsNullOrWhiteSpace(cache.PlantGrade))
        {
            Snackbar.Add("工厂牌号不能为空", Severity.Error);
            return;
        }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateProductionRecordRequest
            {
                ExecDate = execDate,
                EquipmentName = cache.EquipmentName,
                Operator = cache.Operator,
                Shift = cache.Shift,
                Quantity = cache.Quantity,
                Weight = cache.Weight,
                SolutionTemperature = cache.SolutionTemperature,
                SoakTime = cache.SoakTime,
                IsPreCut = cache.IsPreCut,
                FaceCutCount = cache.FaceCutCount,
                CuttingMultiple = cache.CuttingMultiple,
                FinishedCutLength = cache.FinishedCutLength,
                PostCutQuantity = cache.PostCutQuantity,
                TagNo = cache.TagNo,
                PlantGrade = cache.PlantGrade,
                Remark = cache.Remark
            };

            var result = await ProductionRecordService.UpdateProductionRecordAsync(item.Id, request);
            if (result.Success && result.Data != null)
            {
                item.ExecDate = result.Data.ExecDate;
                item.EquipmentName = result.Data.EquipmentName;
                item.Operator = result.Data.Operator;
                item.Shift = result.Data.Shift;
                item.Quantity = result.Data.Quantity;
                item.Weight = result.Data.Weight;
                item.SolutionTemperature = result.Data.SolutionTemperature;
                item.SoakTime = result.Data.SoakTime;
                item.FaceCutCount = result.Data.FaceCutCount;
                item.ProductStatus = result.Data.ProductStatus;
                item.IsPreCut = result.Data.IsPreCut;
                item.CuttingMultiple = result.Data.CuttingMultiple;
                item.FinishedCutLength = result.Data.FinishedCutLength;
                item.PostCutQuantity = result.Data.PostCutQuantity;
                item.TagNo = result.Data.TagNo;
                item.PlantGrade = result.Data.PlantGrade;
                item.Remark = result.Data.Remark;
                item.UpdatedTime = result.Data.UpdatedTime;

                _editingIds.Remove(item.Id);
                _editCache.Remove(item.Id);
                Snackbar.Add("更新成功", Severity.Success);
            }
            else
            {
                Snackbar.Add(result.Message ?? "更新失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"更新失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("production-records", null, _allColumns);
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
        var saved = await ColumnPrefs.LoadAsync("production-records", null);
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

        // 从 PageState 恢复排序/筛选状态
        var savedState = await PageState.LoadAsync("productionrecords");
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

        // 加载筛选上下文（ExcelFilter 下拉选项），完成后由表格触发首次数据加载
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", new object[] { "#production-records-list-table" }))
                _isArrowNavSetup = false;
        }

        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#production-records-list-table");
        }
        catch { }
    }

    // ========== 导航 ==========

    private void NavigateToCreate() => Navigation.NavigateTo("/production-records/create");

    private void ViewBatch(int batchId) => Navigation.NavigateTo($"/batches/{batchId}");

    // ========== 删除 ==========

    private async Task DeleteItem(ProductionRecordDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除工序 \"{item.ProcessName}\" 的生产记录吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await ProductionRecordService.DeleteProductionRecordAsync(item.Id);
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

    // ========== 单元格渲染 ==========

    private bool IsCellEditable(string key) => key switch
    {
        "ExecDate" or "EquipmentName" or "Operator" or "Shift"
            or "Quantity" or "Weight"
            or "SolutionTemperature" or "SoakTime"
            or "FaceCutCount"
            or "CuttingMultiple" or "FinishedCutLength"
            or "PostCutQuantity" or "TagNo" or "PlantGrade" or "Remark" => true,
        _ => false
    };

    private RenderFragment RenderCell(ProductionRecordDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing ? _editCache.GetValueOrDefault(item.Id) : null;

        switch (col.Key)
        {
            case "BatchNo":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => ViewBatch(item.ProductionBatchId)));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.BatchNo)));
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
            case "ExecDate":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.ExecDate);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.ExecDate = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "Placeholder", "yyyy-MM-dd");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.ExecDate.ToString("yyyy-MM-dd"));
                }
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
                    builder.OpenComponent<MudSelect<ShiftType>>(0);
                    builder.AddAttribute(1, "Value", cache.Shift ?? default(ShiftType));
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<ShiftType>(this, v => cache.Shift = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "ChildContent", (RenderFragment)(b =>
                    {
                        foreach (var val in Enum.GetValues<ShiftType>())
                        {
                            b.OpenComponent<MudSelectItem<ShiftType>>(0);
                            b.AddAttribute(1, "Value", val);
                            b.AddAttribute(2, "ChildContent", (RenderFragment)(b2 =>
                                b2.AddContent(0, DisplayHelper.GetShiftTypeText(val))));
                            b.CloseComponent();
                        }
                    }));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.GetShiftTypeText(item.Shift));
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
            case "SolutionTemperature":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<decimal?>>(0);
                    builder.AddAttribute(1, "Value", cache.SolutionTemperature);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, v => cache.SolutionTemperature = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.AddAttribute(5, "Format", "G29");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableDecimal(item.SolutionTemperature));
                }
                break;
            case "SoakTime":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.SoakTime);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.SoakTime = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableInt(item.SoakTime));
                }
                break;
            case "FaceCutCount":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.FaceCutCount);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.FaceCutCount = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableInt(item.FaceCutCount));
                }
                break;
            case "ProductStatus":
                {
                    var color = item.ProductStatus switch
                    {
                        "成品" => Color.Success,
                        "荒管" => Color.Primary,
                        _ => Color.Default
                    };
                    var text = item.ProductStatus ?? "在制";
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", color);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, text)));
                    builder.CloseComponent();
                }
                break;
            case "IsPreCut":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudSelect<bool?>>(0);
                    builder.AddAttribute(1, "Value", cache.IsPreCut);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<bool?>(this, v => cache.IsPreCut = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "Dense", true);
                    builder.AddAttribute(5, "Clearable", true);
                    builder.AddAttribute(6, "ChildContent", (RenderFragment)(b2 =>
                    {
                        // 空 = null（不设置 Value，默认即为 null）
                        b2.OpenComponent<MudSelectItem<bool?>>(0);
                        b2.AddContent(2, "");
                        b2.CloseComponent();
                        b2.OpenComponent<MudSelectItem<bool?>>(3);
                        b2.AddAttribute(4, "Value", true);
                        b2.AddAttribute(5, "Text", "是");
                        b2.AddContent(6, "是");
                        b2.CloseComponent();
                    }));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.IsPreCut == true ? "是" : "");
                }
                break;
            case "LengthStatus":
                builder.AddContent(0, DisplayHelper.GetLengthStatusText(item.LengthStatus));
                break;
            case "CuttingMultiple":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<decimal?>>(0);
                    builder.AddAttribute(1, "Value", cache.CuttingMultiple);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, v => cache.CuttingMultiple = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.AddAttribute(5, "Format", "G29");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableDecimal(item.CuttingMultiple));
                }
                break;
            case "FinishedCutLength":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<decimal?>>(0);
                    builder.AddAttribute(1, "Value", cache.FinishedCutLength);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, v => cache.FinishedCutLength = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.AddAttribute(5, "Format", "G29");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableDecimal(item.FinishedCutLength));
                }
                break;
            case "PostCutQuantity":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.PostCutQuantity);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.PostCutQuantity = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableInt(item.PostCutQuantity));
                }
                break;
            case "TagNo":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.TagNo);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.TagNo = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.TagNo);
                }
                break;
            case "PlantGrade":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.PlantGrade);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.PlantGrade = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.PlantGrade);
                }
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
            default:
                builder.AddContent(0, "");
                break;
        }
    };

    // ========== 打印方法 ==========

    private List<PrintColumnDef> GetPrintColumnDefs()
    {
        return _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();
    }

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的生产记录", Severity.Warning);
            return;
        }
        try
        {
            var ids = selectedIds.ToArray();
            var cols = GetPrintColumnDefs();
            var request = new ProductionRecordPrintBatchRequest { Ids = ids, Columns = cols };
            var apiUrl = $"{Http.BaseAddress}api/production-record/print-batch-file";
            var json = JsonSerializer.Serialize(request);
            Snackbar.Add("正在生成PDF...", Severity.Info);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
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
            var cols = GetPrintColumnDefs();
            var sortCol = _allColumns.FirstOrDefault(c => c.Key == sortColumn);
            var sortBy = sortCol?.SortKey ?? sortColumn ?? "createdtime";
            var request = new ProductionRecordPrintAllRequest
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending,
                ExecDateFrom = DateTime.TryParse(_dateFrom, out var df) ? df : null,
                ExecDateTo = DateTime.TryParse(_dateTo, out var dt) ? dt : null,
                Columns = cols
            };
            var apiUrl = $"{Http.BaseAddress}api/production-record/print-all-file";
            var json = JsonSerializer.Serialize(request);
            Snackbar.Add("正在生成PDF...", Severity.Info);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
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

        // 操作列占位
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
            1 => "col-g3",
            2 => "col-g4",
            3 => "col-g5",
            4 => "col-g6",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start";
        return cls;
    }

    private static string GetCellGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g3-cell",
            2 => "col-g4-cell",
            3 => "col-g5-cell",
            4 => "col-g6-cell",
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
        await PageState.SaveAsync("productionrecords", state);
    }
}
