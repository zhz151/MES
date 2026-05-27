using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Helpers;
using MES.Blazor.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.Quality;

public partial class FinalInspections
{
    private MudTable<FinalInspectionDto>? table;
    private List<FinalInspectionDto> _pageItems = new();
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

    private string sortColumn = "inspectiondate";
    private bool sortDescending = true;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "InspectionItem",        Label = "检验项目",   SortKey = "inspectionitem", FilterType = "enum",
               EnumOptions = new List<EnumOption>
               {
                   new("PMIInspection", "PMI检验"),
                   new("VisualInspection", "表检"),
                   new("Dimension", "尺寸"),
                   new("Endoscopy", "内窥"),
                   new("HydrostaticPressure", "水压"),
                   new("UnderwaterPneumatic", "水下气压"),
                   new("EddyCurrent", "涡流"),
                   new("Ultrasonic", "超声波"),
                   new("PortColoring", "端口着色"),
               } },
        new() { Key = "InspectionDate",        Label = "检验日期",   SortKey = "inspectiondate", FilterType = "date" },
        new() { Key = "BatchNo",                Label = "生产编号",   SortKey = "batchno", FilterType = "string" },
        new() { Key = "MaterialName",           Label = "物料名称",   SortKey = "materialname", FilterType = "string" },
        new() { Key = "TagNo",                  Label = "挂牌号",     SortKey = "tagno", FilterType = "string" },
        new() { Key = "WorkOrderNo",            Label = "工单号",     SortKey = "workorderno", FilterType = "string" },
        new() { Key = "SalesOrderNo",           Label = "订单号",     SortKey = "salesorderno", FilterType = "string" },
        new() { Key = "SourceUnit",             Label = "来料单位",   SortKey = "sourceunit", FilterType = "string" },
        new() { Key = "FurnaceNo",              Label = "炉号",       SortKey = "furnaceno", FilterType = "string" },
        new() { Key = "PlantGrade",             Label = "工厂牌号",   SortKey = "plantgrade", FilterType = "string" },
        new() { Key = "Specification",          Label = "规格",       SortKey = "specification", FilterType = "string" },
        new() { Key = "FixedLength",            Label = "定尺长度",   SortKey = "fixedlength", FilterType = "string" },
        new() { Key = "ProductionType",         Label = "生产类型",   SortKey = "productiontype", FilterType = "string" },
        new() { Key = "EquipmentName",          Label = "设备名称",   SortKey = "equipmentname", FilterType = "string" },
        new() { Key = "Shift",                  Label = "班次",       SortKey = "shift", FilterType = "string" },
        new() { Key = "Operator",               Label = "操作员",     SortKey = "operator", FilterType = "string" },
        new() { Key = "Quantity",               Label = "检验支数",   SortKey = "quantity" },
        new() { Key = "Weight",                 Label = "检验重量",   SortKey = "weight" },
        new() { Key = "QualifiedQuantity",      Label = "合格支数",     SortKey = "qualifiedquantity" },
        new() { Key = "QualifiedWeight",        Label = "合格重量",     SortKey = "qualifiedweight" },
        new() { Key = "QualifiedConcessionQuantity", Label = "让步放行支", SortKey = "qualifiedconcessionquantity" },
        new() { Key = "ConcessionRemark",       Label = "让步说明",     SortKey = "concessionremark", FilterType = "string" },
        new() { Key = "DefectReworkQuantity",   Label = "次品返整支",   SortKey = "defectreworkquantity" },
        new() { Key = "DefectWarehouseQuantity",Label = "次品入库支",   SortKey = "defectwarehousequantity" },
        new() { Key = "DefectScrapQuantity",    Label = "次品报废支",   SortKey = "defectscrapquantity" },
        new() { Key = "DefectDescription",      Label = "次品情况描述", SortKey = "defectdescription", FilterType = "string" },
        new() { Key = "OuterDiameterRange",     Label = "外径范围",   SortKey = "outerdiameterrange", FilterType = "string" },
        new() { Key = "WallThicknessRange",     Label = "壁厚范围",   SortKey = "wallthicknessrange", FilterType = "string" },
        new() { Key = "LengthAllowanceRange",   Label = "长度余量范围", SortKey = "lengthallowancerange", FilterType = "string" },
        new() { Key = "Pressure",               Label = "压力Mpa",    SortKey = "pressure" },
        new() { Key = "HoldTime",               Label = "保压时间s",  SortKey = "holdtime" },
        new() { Key = "Remark",                 Label = "检验备注",   SortKey = "remark", FilterType = "string" },
        new() { Key = "CreatedTime",            Label = "创建日期",   SortKey = "createdtime" },
        new() { Key = "UpdatedTime",            Label = "更新日期",   SortKey = "updatedtime" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<FinalInspectionDto>> LoadDataFromServer(TableState state)
    {
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

            var result = await FinalInspectionService.GetAllAsync(
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

        return new TableData<FinalInspectionDto>
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
            var result = await FinalInspectionService.GetFilterContextsAsync();
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

        // InspectionItem 列显示中文
        if (_filterContextOptions.TryGetValue("InspectionItem", out var itemOptions))
        {
            foreach (var opt in itemOptions)
            {
                opt.Display = DisplayHelper.GetInspectionItemText(
                    Enum.TryParse<InspectionItem>(opt.Value, out var item) ? item : InspectionItem.PMIInspection);
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
        await ColumnPrefs.SaveAsync("final-inspection", null, _allColumns);
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
        var saved = await ColumnPrefs.LoadAsync("final-inspection", null);
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
        var savedState = await PageState.LoadAsync("final-inspection");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "inspectiondate";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
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
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#final-inspection-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string InspectionDate { get; set; } = "";
        public string? EquipmentName { get; set; }
        public string? Shift { get; set; }
        public string? Operator { get; set; }
        public int? Quantity { get; set; }
        public decimal? Weight { get; set; }
        public int? QualifiedQuantity { get; set; }
        public decimal? QualifiedWeight { get; set; }
        public int? QualifiedConcessionQuantity { get; set; }
        public string? ConcessionRemark { get; set; }
        public int? DefectReworkQuantity { get; set; }
        public int? DefectWarehouseQuantity { get; set; }
        public int? DefectScrapQuantity { get; set; }
        public string? DefectDescription { get; set; }
        public string? OuterDiameterRange { get; set; }
        public string? WallThicknessRange { get; set; }
        public string? LengthAllowanceRange { get; set; }
        public decimal? Pressure { get; set; }
        public int? HoldTime { get; set; }
        public string? Remark { get; set; }
    }

    private void StartEdit(FinalInspectionDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new EditCache
        {
            InspectionDate = item.InspectionDate.ToString("yyyy-MM-dd"),
            EquipmentName = item.EquipmentName,
            Shift = item.Shift,
            Operator = item.Operator,
            Quantity = item.Quantity,
            Weight = item.Weight,
            QualifiedQuantity = item.QualifiedQuantity,
            QualifiedWeight = item.QualifiedWeight,
            QualifiedConcessionQuantity = item.QualifiedConcessionQuantity,
            ConcessionRemark = item.ConcessionRemark,
            DefectReworkQuantity = item.DefectReworkQuantity,
            DefectWarehouseQuantity = item.DefectWarehouseQuantity,
            DefectScrapQuantity = item.DefectScrapQuantity,
            DefectDescription = item.DefectDescription,
            OuterDiameterRange = item.OuterDiameterRange,
            WallThicknessRange = item.WallThicknessRange,
            LengthAllowanceRange = item.LengthAllowanceRange,
            Pressure = item.Pressure,
            HoldTime = item.HoldTime,
            Remark = item.Remark
        };
    }

    private void CancelEdit(FinalInspectionDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(FinalInspectionDto item)
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
            var request = new UpdateFinalInspectionRequest
            {
                InspectionDate = inspectionDate,
                EquipmentName = cache.EquipmentName,
                Shift = cache.Shift,
                Operator = cache.Operator,
                Quantity = cache.Quantity,
                Weight = cache.Weight,
                QualifiedQuantity = cache.QualifiedQuantity,
                QualifiedWeight = cache.QualifiedWeight,
                QualifiedConcessionQuantity = cache.QualifiedConcessionQuantity,
                ConcessionRemark = cache.ConcessionRemark,
                DefectReworkQuantity = cache.DefectReworkQuantity,
                DefectWarehouseQuantity = cache.DefectWarehouseQuantity,
                DefectScrapQuantity = cache.DefectScrapQuantity,
                DefectDescription = cache.DefectDescription,
                OuterDiameterRange = cache.OuterDiameterRange,
                WallThicknessRange = cache.WallThicknessRange,
                LengthAllowanceRange = cache.LengthAllowanceRange,
                Pressure = cache.Pressure,
                HoldTime = cache.HoldTime,
                Remark = cache.Remark
            };

            var result = await FinalInspectionService.UpdateAsync(item.Id, request);
            if (result.Success && result.Data != null)
            {
                item.InspectionDate = result.Data.InspectionDate;
                item.EquipmentName = result.Data.EquipmentName;
                item.Shift = result.Data.Shift;
                item.Operator = result.Data.Operator;
                item.Quantity = result.Data.Quantity;
                item.Weight = result.Data.Weight;
                item.QualifiedQuantity = result.Data.QualifiedQuantity;
                item.QualifiedWeight = result.Data.QualifiedWeight;
                item.QualifiedConcessionQuantity = result.Data.QualifiedConcessionQuantity;
                item.ConcessionRemark = result.Data.ConcessionRemark;
                item.DefectReworkQuantity = result.Data.DefectReworkQuantity;
                item.DefectWarehouseQuantity = result.Data.DefectWarehouseQuantity;
                item.DefectScrapQuantity = result.Data.DefectScrapQuantity;
                item.DefectDescription = result.Data.DefectDescription;
                item.OuterDiameterRange = result.Data.OuterDiameterRange;
                item.WallThicknessRange = result.Data.WallThicknessRange;
                item.LengthAllowanceRange = result.Data.LengthAllowanceRange;
                item.Pressure = result.Data.Pressure;
                item.HoldTime = result.Data.HoldTime;
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

    // ========== 单元格原始值/显示值 ==========

    private string? GetCellRawValue(FinalInspectionDto item, string key) => key switch
    {
        "InspectionItem" => item.InspectionItem.ToString(),
        "InspectionDate" => item.InspectionDate.ToString("yyyy-MM-dd"),
        "BatchNo" => item.BatchNo,
        "MaterialName" => item.MaterialName,
        "TagNo" => item.TagNo,
        "WorkOrderNo" => item.WorkOrderNo,
        "SalesOrderNo" => item.SalesOrderNo,
        "SourceUnit" => item.SourceUnit,
        "FurnaceNo" => item.FurnaceNo,
        "PlantGrade" => item.PlantGrade,
        "Specification" => item.Specification,
        "FixedLength" => item.FixedLength,
        "EquipmentName" => item.EquipmentName,
        "Shift" => item.Shift,
        "Operator" => item.Operator,
        "Quantity" => item.Quantity?.ToString(),
        "Weight" => item.Weight?.ToString("G29"),
        "QualifiedQuantity" => item.QualifiedQuantity?.ToString(),
        "QualifiedWeight" => item.QualifiedWeight?.ToString("G29"),
        "QualifiedConcessionQuantity" => item.QualifiedConcessionQuantity?.ToString(),
        "ConcessionRemark" => item.ConcessionRemark,
        "DefectReworkQuantity" => item.DefectReworkQuantity?.ToString(),
        "DefectWarehouseQuantity" => item.DefectWarehouseQuantity?.ToString(),
        "DefectScrapQuantity" => item.DefectScrapQuantity?.ToString(),
        "DefectDescription" => item.DefectDescription,
        "OuterDiameterRange" => item.OuterDiameterRange,
        "WallThicknessRange" => item.WallThicknessRange,
        "LengthAllowanceRange" => item.LengthAllowanceRange,
        "Pressure" => item.Pressure?.ToString("G29"),
        "HoldTime" => item.HoldTime?.ToString(),
        "Remark" => item.Remark,
        "CreatedTime" => item.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        "UpdatedTime" => item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        _ => null
    };

    private string? GetCellDisplayText(FinalInspectionDto item, string key) => key switch
    {
        "InspectionItem" => DisplayHelper.GetInspectionItemText(item.InspectionItem),
        "MaterialName" => DisplayHelper.GetMaterialNameText(item.MaterialName),
        _ => GetCellRawValue(item, key) ?? ""
    };

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
        await PageState.SaveAsync("final-inspection", state);
    }

    private void NavigateToCreate() => Navigation.NavigateTo("/quality/final-inspection/create");

    private async Task DeleteItem(FinalInspectionDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除生产编号 \"{item.BatchNo}\" 的成品检验记录吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await FinalInspectionService.DeleteAsync(item.Id);
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

    // ========== 打印 ==========

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的成品检验记录", Severity.Warning);
            return;
        }
        await JS.InvokeVoidAsync("printTable", "#final-inspection-list-table", "成品检验（选中记录）");
    }

    private async Task PrintAll()
    {
        if (!_pageItems.Any())
        {
            Snackbar.Add("没有可打印的数据", Severity.Warning);
            return;
        }
        var html = BuildPrintHtml(_pageItems);
        await JS.InvokeVoidAsync("printRawHtml", html, "成品检验");
    }

    private string BuildPrintHtml(IEnumerable<FinalInspectionDto> items)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<table><thead><tr>");
        foreach (var col in _visibleColumns)
        {
            sb.Append("<th>").Append(System.Net.WebUtility.HtmlEncode(col.Label)).Append("</th>");
        }
        sb.Append("</tr></thead><tbody>");
        foreach (var item in items)
        {
            sb.Append("<tr>");
            foreach (var col in _visibleColumns)
            {
                sb.Append("<td>");
                sb.Append(System.Net.WebUtility.HtmlEncode(GetCellPrintValue(item, col)));
                sb.Append("</td>");
            }
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");
        return sb.ToString();
    }

    private string GetCellPrintValue(FinalInspectionDto item, ColumnDef col) => col.Key switch
    {
        "InspectionItem" => DisplayHelper.GetInspectionItemText(item.InspectionItem),
        "InspectionDate" => item.InspectionDate.ToString("yyyy-MM-dd"),
        "BatchNo" => item.BatchNo,
        "MaterialName" => DisplayHelper.GetMaterialNameText(item.MaterialName),
        "TagNo" => item.TagNo ?? "",
        "WorkOrderNo" => item.WorkOrderNo ?? "",
        "SalesOrderNo" => item.SalesOrderNo ?? "",
        "SourceUnit" => item.SourceUnit ?? "",
        "FurnaceNo" => item.FurnaceNo ?? "",
        "PlantGrade" => item.PlantGrade ?? "",
        "Specification" => item.Specification ?? "",
        "FixedLength" => item.FixedLength ?? "",
        "ProductionType" => item.ProductionType ?? "",
        "EquipmentName" => item.EquipmentName ?? "",
        "Shift" => item.Shift ?? "",
        "Operator" => item.Operator ?? "",
        "Quantity" => DisplayHelper.FormatNullableInt(item.Quantity),
        "Weight" => DisplayHelper.FormatNullableDecimal(item.Weight),
        "QualifiedQuantity" => DisplayHelper.FormatNullableInt(item.QualifiedQuantity),
        "QualifiedWeight" => DisplayHelper.FormatNullableDecimal(item.QualifiedWeight),
        "QualifiedConcessionQuantity" => DisplayHelper.FormatNullableInt(item.QualifiedConcessionQuantity),
        "ConcessionRemark" => item.ConcessionRemark ?? "",
        "DefectReworkQuantity" => DisplayHelper.FormatNullableInt(item.DefectReworkQuantity),
        "DefectWarehouseQuantity" => DisplayHelper.FormatNullableInt(item.DefectWarehouseQuantity),
        "DefectScrapQuantity" => DisplayHelper.FormatNullableInt(item.DefectScrapQuantity),
        "DefectDescription" => item.DefectDescription ?? "",
        "OuterDiameterRange" => item.OuterDiameterRange ?? "",
        "WallThicknessRange" => item.WallThicknessRange ?? "",
        "LengthAllowanceRange" => item.LengthAllowanceRange ?? "",
        "Pressure" => DisplayHelper.FormatNullableDecimal(item.Pressure),
        "HoldTime" => DisplayHelper.FormatNullableInt(item.HoldTime),
        "Remark" => item.Remark ?? "",
        "CreatedTime" => item.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        "UpdatedTime" => item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        _ => ""
    };

    // ========== 单元格渲染 ==========

    private bool IsCellEditable(string key) => key switch
    {
        "InspectionDate" or "EquipmentName" or "Shift" or "Operator"
            or "Quantity" or "Weight"
            or "QualifiedQuantity" or "QualifiedWeight"
            or "QualifiedConcessionQuantity" or "ConcessionRemark"
            or "DefectReworkQuantity" or "DefectWarehouseQuantity" or "DefectScrapQuantity"
            or "DefectDescription" or "OuterDiameterRange" or "WallThicknessRange"
            or "LengthAllowanceRange" or "Pressure" or "HoldTime" or "Remark" => true,
        _ => false
    };

    private RenderFragment RenderCell(FinalInspectionDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing ? _editCache.GetValueOrDefault(item.Id) : null;

        switch (col.Key)
        {
            case "InspectionItem":
                builder.AddContent(0, DisplayHelper.GetInspectionItemText(item.InspectionItem));
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
            case "BatchNo":
                builder.AddContent(0, item.BatchNo);
                break;
            case "MaterialName":
                builder.AddContent(0, DisplayHelper.GetMaterialNameText(item.MaterialName));
                break;
            case "TagNo":
                builder.AddContent(0, item.TagNo);
                break;
            case "WorkOrderNo":
                builder.AddContent(0, item.WorkOrderNo);
                break;
            case "SalesOrderNo":
                builder.AddContent(0, item.SalesOrderNo);
                break;
            case "SourceUnit":
                builder.AddContent(0, item.SourceUnit);
                break;
            case "FurnaceNo":
                builder.AddContent(0, item.FurnaceNo);
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade);
                break;
            case "ProductionType":
                builder.AddContent(0, item.ProductionType);
                break;
            case "Specification":
                builder.AddContent(0, item.Specification);
                break;
            case "FixedLength":
                builder.AddContent(0, item.FixedLength);
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
                    builder.AddContent(0, DisplayHelper.FormatNullableDecimal(item.Weight));
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
                    builder.AddContent(0, DisplayHelper.FormatNullableDecimal(item.QualifiedWeight));
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
            case "OuterDiameterRange":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.OuterDiameterRange);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.OuterDiameterRange = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.OuterDiameterRange);
                }
                break;
            case "WallThicknessRange":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.WallThicknessRange);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.WallThicknessRange = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.WallThicknessRange);
                }
                break;
            case "LengthAllowanceRange":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.LengthAllowanceRange);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.LengthAllowanceRange = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.LengthAllowanceRange);
                }
                break;
            case "Pressure":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<decimal?>>(0);
                    builder.AddAttribute(1, "Value", cache.Pressure);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, v => cache.Pressure = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.AddAttribute(5, "Format", "G29");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableDecimal(item.Pressure));
                }
                break;
            case "HoldTime":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudNumericField<int?>>(0);
                    builder.AddAttribute(1, "Value", cache.HoldTime);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => cache.HoldTime = v));
                    builder.AddAttribute(3, "Class", "compact-input");
                    builder.AddAttribute(4, "HideSpinButtons", true);
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.FormatNullableInt(item.HoldTime));
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
            case "CreatedTime":
                builder.AddContent(0, item.CreatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            case "UpdatedTime":
                builder.AddContent(0, item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            default:
                builder.AddContent(0, "");
                break;
        }
    };
}
