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

namespace MES.Blazor.Pages.Quality;

public partial class ProcessInspections
{
    private MudTable<ProcessInspectionDto>? table;
    private List<ProcessInspectionDto> _pageItems = new();
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
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;

    // 排序状态
    private string sortColumn = "inspectiondate";
    private bool sortDescending = true;

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "Quantity", "Weight", "QualifiedQuantity", "QualifiedWeight",
        "QualifiedConcessionQuantity", "DefectReworkQuantity",
        "DefectWarehouseQuantity", "DefectScrapQuantity"
    };

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private int _totalTableWidth =>
        _visibleColumns.Sum(c => int.TryParse(c.Width, out var w) ? w : 100) + 40 + 90;

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // G1: 生产批次
        new() { Key = "BatchNo",               Label = "生产编号",   SortKey = "batchno", FilterType = "string", Width = "120",
            GroupKey = 1, GroupName = "G1 生产批次" },
        new() { Key = "ProcessName",           Label = "工序名称",   SortKey = "processname", FilterType = "string", Width = "120",
            GroupKey = 1, GroupName = "G1 生产批次" },
        new() { Key = "ManufacturingSpec",     Label = "制造规格",   SortKey = "manufacturingspec", FilterType = "string", Width = "120",
            GroupKey = 1, GroupName = "G1 生产批次" },
        new() { Key = "SectionName",           Label = "工段名称",   SortKey = "sectionname", FilterType = "string", Width = "120",
            GroupKey = 1, GroupName = "G1 生产批次" },
        new() { Key = "SequenceNumber",        Label = "执行序号",   SortKey = "sequencenumber", Width = "45",
            GroupKey = 1, GroupName = "G1 生产批次" },

        // G2: 检验执行
        new() { Key = "InspectionDate",       Label = "检验日期",   SortKey = "inspectiondate", FilterType = "date", Width = "120",
            GroupKey = 2, GroupName = "G2 检验执行" },
        new() { Key = "EquipmentName",         Label = "设备名称",   SortKey = "equipmentname", FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "G2 检验执行" },
        new() { Key = "Inspector",             Label = "检验员",     SortKey = "inspector", FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "G2 检验执行" },
        new() { Key = "Shift",                 Label = "班次",       SortKey = "shift", FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "G2 检验执行" },
        new() { Key = "InspectionItem",        Label = "检验项目",   SortKey = "inspectionitem", FilterType = "string", Width = "120",
            GroupKey = 2, GroupName = "G2 检验执行" },

        // G3: 检验结果
        new() { Key = "Quantity",              Label = "检验支数",   SortKey = "quantity", Width = "80",
            GroupKey = 3, GroupName = "G3 检验结果" },
        new() { Key = "Weight",                Label = "检验重量",   SortKey = "weight", Width = "80",
            GroupKey = 3, GroupName = "G3 检验结果" },
        new() { Key = "QualifiedQuantity",           Label = "合格支数",     SortKey = "qualifiedquantity", Width = "80",
            GroupKey = 3, GroupName = "G3 检验结果" },
        new() { Key = "QualifiedWeight",             Label = "合格重量",     SortKey = "qualifiedweight", Width = "80",
            GroupKey = 3, GroupName = "G3 检验结果" },

        // G4: 不合格处理
        new() { Key = "QualifiedConcessionQuantity", Label = "让步放行支",   SortKey = "qualifiedconcessionquantity", Width = "80",
            GroupKey = 4, GroupName = "G4 不合格处理" },
        new() { Key = "ConcessionRemark",            Label = "让步说明",     SortKey = "concessionremark", FilterType = "string", Width = "120",
            GroupKey = 4, GroupName = "G4 不合格处理" },
        new() { Key = "DefectReworkQuantity",        Label = "次品返整支",   SortKey = "defectreworkquantity", Width = "80",
            GroupKey = 4, GroupName = "G4 不合格处理" },
        new() { Key = "DefectWarehouseQuantity", Label = "次品入库支",   SortKey = "defectwarehousequantity", Width = "80",
            GroupKey = 4, GroupName = "G4 不合格处理" },
        new() { Key = "DefectScrapQuantity",   Label = "次品报废支",   SortKey = "defectscrapquantity", Width = "80",
            GroupKey = 4, GroupName = "G4 不合格处理" },
        new() { Key = "DefectDescription",     Label = "次品情况描述", SortKey = "defectdescription", FilterType = "string", Width = "120",
            GroupKey = 4, GroupName = "G4 不合格处理" },

        // G5: 辅助信息
        new() { Key = "SourceUnit",            Label = "来料单位",   SortKey = "sourceunit", FilterType = "string", Width = "120",
            GroupKey = 5, GroupName = "G5 辅助信息" },
        new() { Key = "TagNo",                 Label = "挂牌号",     SortKey = "tagno", FilterType = "string", Width = "120",
            GroupKey = 5, GroupName = "G5 辅助信息" },
        new() { Key = "PlantGrade",            Label = "工厂牌号",   SortKey = "plantgrade", FilterType = "string", Width = "120",
            GroupKey = 5, GroupName = "G5 辅助信息" },
        new() { Key = "Remark",                Label = "备注",       SortKey = "remark", FilterType = "string", Width = "120",
            GroupKey = 5, GroupName = "G5 辅助信息" },
        new() { Key = "DataSource",            Label = "数据来源",   SortKey = "datasource", FilterType = "enum", Width = "80",
            GroupKey = 5, GroupName = "G5 辅助信息",
            EnumOptions = new() { new("SCAN", "扫码"), new("MANUAL", "手动") } },
        new() { Key = "UpdatedTime",           Label = "更新日期",   SortKey = "updatedtime", Width = "120",
            GroupKey = 5, GroupName = "G5 辅助信息" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<ProcessInspectionDto>> LoadDataFromServer(TableState state)
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

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "inspectiondate";
            var filtersJson = SerializeFilters();

            DateTime? dateFrom = null;
            DateTime? dateTo = null;
            if (DateTime.TryParse(_dateFrom, out var df)) dateFrom = df;
            if (DateTime.TryParse(_dateTo, out var dt)) dateTo = dt;

            var result = await ProcessInspectionService.GetAllAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
                inspectionDateFrom: dateFrom,
                inspectionDateTo: dateTo,
                filters: filtersJson);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;
                ComputePageSums();
            }
            else
            {
                _pageItems = new();
                _totalCount = 0;
                _pageSums.Clear();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
        }

        return new TableData<ProcessInspectionDto>
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
            var result = await ProcessInspectionService.GetFilterContextsAsync();
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

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnUp(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnDown(ColumnDef col) => await SaveColumnPrefs();

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("process-inspection", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("process-inspection", null);
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
        var savedState = await PageState.LoadAsync("processinspections");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "inspectiondate";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
            if (savedState.Extras?.ContainsKey("dateFrom") == true)
                _dateFrom = savedState.Extras["dateFrom"];
            if (savedState.Extras?.ContainsKey("dateTo") == true)
                _dateTo = savedState.Extras["dateTo"];
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

        // 加载筛选上下文（ExcelFilter 下拉选项）
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#process-inspection-list-table");
        }
        catch { }

        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#process-inspection-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 导航 ==========

    private void NavigateToCreate() => Navigation.NavigateTo("/quality/process-inspection/create");

    private void ViewBatch(int batchId) => Navigation.NavigateTo($"/batches/{batchId}");

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string InspectionDate { get; set; } = "";
        public string? EquipmentName { get; set; }
        public string? Inspector { get; set; }
        public string? Shift { get; set; }
        public int? Quantity { get; set; }
        public decimal? Weight { get; set; }
        public string? InspectionItem { get; set; }
        public int? QualifiedQuantity { get; set; }
        public decimal? QualifiedWeight { get; set; }
        public int? QualifiedConcessionQuantity { get; set; }
        public string? ConcessionRemark { get; set; }
        public int? DefectReworkQuantity { get; set; }
        public int? DefectWarehouseQuantity { get; set; }
        public int? DefectScrapQuantity { get; set; }
        public string? DefectDescription { get; set; }
        public string? SourceUnit { get; set; }
        public string? TagNo { get; set; }
        public string? PlantGrade { get; set; }
        public string? Remark { get; set; }
    }

    private void StartEdit(ProcessInspectionDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new EditCache
        {
            InspectionDate = item.InspectionDate.ToString("yyyy-MM-dd"),
            EquipmentName = item.EquipmentName,
            Inspector = item.Inspector,
            Shift = item.Shift,
            Quantity = item.Quantity,
            Weight = item.Weight,
            InspectionItem = item.InspectionItem,
            QualifiedQuantity = item.QualifiedQuantity,
            QualifiedWeight = item.QualifiedWeight,
            QualifiedConcessionQuantity = item.QualifiedConcessionQuantity,
            ConcessionRemark = item.ConcessionRemark,
            DefectReworkQuantity = item.DefectReworkQuantity,
            DefectWarehouseQuantity = item.DefectWarehouseQuantity,
            DefectScrapQuantity = item.DefectScrapQuantity,
            DefectDescription = item.DefectDescription,
            SourceUnit = item.SourceUnit,
            TagNo = item.TagNo,
            PlantGrade = item.PlantGrade,
            Remark = item.Remark
        };
    }

    private void CancelEdit(ProcessInspectionDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(ProcessInspectionDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        if (!DateTime.TryParse(cache.InspectionDate, out var inspectionDate))
        {
            Snackbar.Add("检验日期格式无效", Severity.Error);
            return;
        }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateProcessInspectionRequest
            {
                InspectionDate = inspectionDate,
                EquipmentName = cache.EquipmentName,
                Inspector = cache.Inspector,
                Shift = cache.Shift,
                Quantity = cache.Quantity,
                Weight = cache.Weight,
                InspectionItem = cache.InspectionItem,
                QualifiedQuantity = cache.QualifiedQuantity,
                QualifiedWeight = cache.QualifiedWeight,
                QualifiedConcessionQuantity = cache.QualifiedConcessionQuantity,
                ConcessionRemark = cache.ConcessionRemark,
                DefectReworkQuantity = cache.DefectReworkQuantity,
                DefectWarehouseQuantity = cache.DefectWarehouseQuantity,
                DefectScrapQuantity = cache.DefectScrapQuantity,
                DefectDescription = cache.DefectDescription,
                SourceUnit = cache.SourceUnit,
                TagNo = cache.TagNo,
                PlantGrade = cache.PlantGrade,
                Remark = cache.Remark
            };

            var result = await ProcessInspectionService.UpdateAsync(item.Id, request);
            if (result.Success)
            {
                _editingIds.Remove(item.Id);
                _editCache.Remove(item.Id);
                Snackbar.Add("更新成功", Severity.Success);
                if (table != null) await table.ReloadServerData();
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

    // ========== 删除 ==========

    private async Task DeleteItem(ProcessInspectionDto item)
    {
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除工序 \"{item.ProcessName}\" 的过程检验记录吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await ProcessInspectionService.DeleteAsync(item.Id);
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
        "InspectionDate" or "EquipmentName" or "Inspector" or "Shift"
            or "Quantity" or "Weight" or "InspectionItem"
            or "QualifiedQuantity" or "QualifiedWeight"
            or "QualifiedConcessionQuantity" or "ConcessionRemark"
            or "DefectReworkQuantity" or "DefectWarehouseQuantity" or "DefectScrapQuantity"
            or "DefectDescription" or "SourceUnit" or "TagNo" or "PlantGrade" or "Remark" => true,
        _ => false
    };

    private RenderFragment RenderCell(ProcessInspectionDto item, ColumnDef col) => builder =>
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
            case "ProcessName":
                builder.AddContent(0, item.ProcessName);
                break;
            case "ManufacturingSpec":
                builder.AddContent(0, DisplayHelper.FormatSpecification(item.ManufacturingSpec));
                break;
            case "SectionName":
                builder.AddContent(0, item.SectionName);
                break;
            case "SequenceNumber":
                builder.AddContent(0, item.SequenceNumber);
                break;
            case "InspectionDate":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.InspectionDate);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.InspectionDate = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "Placeholder", "yyyy-MM-dd");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.InspectionDate.ToString("yyyy-MM-dd"));
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
            case "Inspector":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.Inspector);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.Inspector = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Inspector);
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
                    builder.AddContent(0, DisplayHelper.FormatNullableDecimalAsInt(item.Weight));
                }
                break;
            case "InspectionItem":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.InspectionItem);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.InspectionItem = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.InspectionItem);
                }
                break;
            case "QualifiedQuantity":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.QualifiedQuantity);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.QualifiedQuantity = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableInt(item.QualifiedQuantity));
                }
                break;
            case "QualifiedWeight":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<decimal?>>(0);
                    builder.AddAttribute(1, "Value", cache.QualifiedWeight);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, v => cache.QualifiedWeight = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.AddAttribute(5, "Format", "G29");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableDecimalAsInt(item.QualifiedWeight));
                }
                break;
            case "QualifiedConcessionQuantity":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.QualifiedConcessionQuantity);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.QualifiedConcessionQuantity = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableInt(item.QualifiedConcessionQuantity));
                }
                break;
            case "ConcessionRemark":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.ConcessionRemark);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.ConcessionRemark = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.ConcessionRemark);
                }
                break;
            case "DefectReworkQuantity":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.DefectReworkQuantity);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.DefectReworkQuantity = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableInt(item.DefectReworkQuantity));
                }
                break;
            case "DefectWarehouseQuantity":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.DefectWarehouseQuantity);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.DefectWarehouseQuantity = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableInt(item.DefectWarehouseQuantity));
                }
                break;
            case "DefectScrapQuantity":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.DefectScrapQuantity);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.DefectScrapQuantity = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableInt(item.DefectScrapQuantity));
                }
                break;
            case "DefectDescription":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.DefectDescription);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.DefectDescription = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.DefectDescription);
                }
                break;
            case "SourceUnit":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.SourceUnit);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.SourceUnit = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.SourceUnit);
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

    // ========== 打印 ==========

    private List<PrintColumnDef> GetPrintColumnDefs() =>
        _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();

    private async Task PrintSelected()
    {
        if (!selectedIds.Any()) return;
        var apiUrl = $"{Http.BaseAddress}api/process-inspection/print-batch-file";
        var request = new ProcessInspectionPrintBatchRequest
        {
            Ids = selectedIds.ToArray(),
            Columns = GetPrintColumnDefs()
        };
        var json = JsonSerializer.Serialize(request);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }

    private async Task PrintAll()
    {
        var apiUrl = $"{Http.BaseAddress}api/process-inspection/print-all-file";
        var request = new ProcessInspectionPrintAllRequest
        {
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            SortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "inspectiondate",
            IsDescending = sortDescending,
            InspectionDateFrom = DateTime.TryParse(_dateFrom, out var df) ? df : null,
            InspectionDateTo = DateTime.TryParse(_dateTo, out var dt) ? dt : null,
            Columns = GetPrintColumnDefs()
        };
        var json = JsonSerializer.Serialize(request);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }

    // ========== 分页汇总（B33） ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(ProcessInspectionDto)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);

        foreach (var key in _summableColumnKeys)
        {
            if (!props.TryGetValue(key, out var prop)) continue;
            var type = prop.PropertyType;
            try
            {
                if (type == typeof(decimal?))
                {
                    var sum = _pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[key] = ((int)sum).ToString();
                }
                else if (type == typeof(int?))
                {
                    var sum = _pageItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[key] = sum.ToString();
                }
            }
            catch { }
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        return _pageSums.GetValueOrDefault(col.Key, "");
    }

    // ========== 分组标题栏 ==========

    private List<GroupHeaderInfo> GetGroupHeaders()
    {
        var result = new List<GroupHeaderInfo>();
        // 选择列起始占位符（必须最前，对应 JS 遍历的第一个 <th> checkbox，gk=0）
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0, GroupName = "", TotalWidth = 40, ColumnCount = 0, CssClass = ""
        });
        int? lastKey = null;
        int totalWidth = 0;
        var groupKey = 0;
        var groupName = "";
        var count = 0;

        foreach (var col in _visibleColumns)
        {
            var gk = col.GroupKey ?? 0;
            if (gk != lastKey && lastKey.HasValue)
            {
                result.Add(new GroupHeaderInfo
                {
                    GroupKey = groupKey,
                    GroupName = groupName,
                    TotalWidth = totalWidth,
                    ColumnCount = count,
                    CssClass = GetHeaderGroupCss(groupKey, true)
                });
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
        // 操作列尾随占位符（必须最后，对应 JS 遍历的最后一个 <th> 操作列，gk=0）
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0, GroupName = "", TotalWidth = 90, ColumnCount = 0, CssClass = ""
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
            5 => "col-g5",
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
            5 => "col-g5-cell",
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
        if (!string.IsNullOrEmpty(_dateFrom))
            extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrEmpty(_dateTo))
            extras["dateTo"] = _dateTo;
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("processinspections", state);
    }

    private class GroupHeaderInfo
    {
        public int GroupKey { get; set; }
        public string GroupName { get; set; } = "";
        public int TotalWidth { get; set; }
        public int ColumnCount { get; set; }
        public string CssClass { get; set; } = "";
    }
}
