using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Blazor.Shared;
using MES.Core.DTOs;
using MES.Core.Models;
using System.Reflection;
using System.Text.Json;

namespace MES.Blazor.Pages.Warehouse;

public partial class InboundHistory
{
    [Parameter]
    public string? Code { get; set; }

    // ========== 仓库 ==========
    private int? _warehouseId;
    private string _warehouseName = string.Empty;
    private List<WarehouseDto> _warehouses = new();

    // ========== 工单删除通知 ==========
    private List<NotificationDto>? _workOrderNotices;

    // ========== 数据与筛选 ==========
    private List<InventoryBatchDto> _pageItems = new();
    private int _totalCount;
    private string _searchKeyword = string.Empty;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    // B33 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "InitialQuantity", "InitialWeight", "Meters"
    };
    private string _lastResolvedWarehouseCode = string.Empty;

    // 排序状态
    private string sortColumn = "InboundDate";
    private bool sortDescending = true;

    // ========== ExcelFilter 状态 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // 编辑状态
    private int? _savingItemId;
    private int? _editingRowId;
    private MudTable<InventoryBatchDto>? _table;

    /// <summary>内联编辑中按 item.Id 暂存的入库日期字符串</summary>
    private Dictionary<int, string> _editDateStrings = new();

    private void ClearEditState()
    {
        _editingRowId = null;
        _editDateStrings.Clear();
    }

    // ========== 多选 ==========
    private HashSet<InventoryBatchDto> _selectedItems = new();
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
                    _selectedItems.Add(item);
            }
            else
            {
                _selectedItems.Clear();
            }
            StateHasChanged();
        }
    }

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();
    private readonly List<(string Value, string Text)> _inboundSourceOptions = new()
    {
        ("Purchase", "外购"),
        ("Subcontract", "委外"),
        ("ReturnIn", "退货入库"),
        ("ProductionInbound", "生产入库"),
        ("InspectionInbound", "检验入库"),
        ("TransferIn", "移库入库"),
        ("Other", "其它"),
    };

    private readonly List<(string Value, string Text)> _lengthStatusOptions = new()
    {
        ("Fixed", "定尺"),
        ("Range", "范围尺"),
    };

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "BatchNo",             Label = "仓库批次", SortKey = "BatchNo", FilterType = "string", Width = "120" },
        new() { Key = "InboundDate",         Label = "入库日期", SortKey = "InboundDate",    IsRequired = true, Width = "120" },
        new() { Key = "InboundSource",       Label = "来源",     SortKey = "InboundSource", FilterType = "enum", Width = "120",
            EnumOptions = new() { new("Purchase", "外购"), new("Subcontract", "委外"), new("ReturnIn", "退货入库"), new("ProductionInbound", "生产入库"), new("InspectionInbound", "检验入库"), new("TransferIn", "移库入库"), new("Other", "其它") } },
        new() { Key = "SourceOrderNo",       Label = "物料单号", SortKey = "SourceOrderNo", FilterType = "string", Width = "120" },
        new() { Key = "MaterialType",        Label = "物料",     SortKey = "MaterialType",    IsRequired = true, FilterType = "string", Width = "120" },
        new() { Key = "SourceName",          Label = "来料单位", SortKey = "SourceName", FilterType = "string", Width = "120" },
        new() { Key = "SurfaceCondition",    Label = "物料状态", SortKey = "SurfaceCondition", FilterType = "string", Width = "120" },
        new() { Key = "LocationArea",        Label = "区域", SortKey = "LocationArea", FilterType = "string", Width = "120" },
        new() { Key = "LocationRack",        Label = "框架", SortKey = "LocationRack", FilterType = "string", Width = "120" },
        new() { Key = "HeatNo",              Label = "炉号",     SortKey = "HeatNo", FilterType = "string", Width = "120" },
        new() { Key = "PlantGrade",          Label = "工厂牌号", SortKey = "PlantGrade",      IsRequired = true, FilterType = "string", Width = "120" },
        new() { Key = "Specification",       Label = "名义规格", SortKey = "Specification",   IsRequired = true, FilterType = "string", Width = "120" },
        new() { Key = "LengthStatus",        Label = "长度状态", SortKey = "LengthStatus", FilterType = "enum", Width = "120",
            EnumOptions = new() { new("Fixed", "定尺"), new("Range", "范围尺"), new("NonFixed", "非定尺") } },
        new() { Key = "MinLength",           Label = "最小长度", SortKey = "MinLength", FilterType = null, Width = "80" },
        new() { Key = "MaxLength",           Label = "最大长度", SortKey = "MaxLength", FilterType = null, Width = "80" },
        new() { Key = "InitialQuantity",     Label = "支数",     SortKey = "InitialQuantity", IsRequired = true, FilterType = null, Width = "80" },
        new() { Key = "InitialWeight",       Label = "重量(kg)", SortKey = "InitialWeight",   IsRequired = true, FilterType = null, Width = "80" },
        new() { Key = "UnitWeight",          Label = "单支重",   SortKey = "UnitWeight", FilterType = null, Width = "80" },
        new() { Key = "Meters",              Label = "米数", SortKey = "Meters", FilterType = null, Width = "80" },
        new() { Key = "Remark",              Label = "备注", SortKey = "Remark", FilterType = "string", Width = "120" },
        new() { Key = "IsLinkedToWorkOrder", Label = "关联工单", SortKey = "IsLinkedToWorkOrder", FilterType = "boolean", Width = "120",
            BoolTrueLabel = "是", BoolFalseLabel = "否" },
        new() { Key = "WorkOrderNo",         Label = "工单号",   SortKey = "WorkOrderNo", FilterType = "string", Width = "120" },
        new() { Key = "SalesOrderNo",        Label = "订单号",   SortKey = "SalesOrderNo", FilterType = "string", Width = "120" },
        new() { Key = "OrderItemIds",        Label = "项次", SortKey = "OrderItemIds", FilterType = "string", Width = "120" },
        new() { Key = "ProductionBatchNo",   Label = "生产批号", SortKey = "ProductionBatchNo", FilterType = "string", Width = "120" },
        new() { Key = "ActualSpecification", Label = "实际规格", SortKey = "ActualSpecification", FilterType = "string", Width = "120" },
        new() { Key = "ActualOuterDiameter", Label = "外径", SortKey = "ActualOuterDiameter", FilterType = null, Width = "80" },
        new() { Key = "ActualWallThickness", Label = "壁厚", SortKey = "ActualWallThickness", FilterType = null, Width = "80" },
        new() { Key = "DefectReason",        Label = "次品原因", SortKey = "DefectReason", FilterType = "string", Width = "120" },
        new() { Key = "LiabilityType",       Label = "责任类型", SortKey = "LiabilityType", FilterType = "string", Width = "120" },
        new() { Key = "OriginalSupplier",    Label = "原始来料", SortKey = "OriginalSupplier", FilterType = "string", Width = "120" },
        new() { Key = "TagNo",               Label = "挂牌号", SortKey = "TagNo", FilterType = "string", Width = "120" },
        new() { Key = "DefectRemark",        Label = "次品备注", SortKey = "DefectRemark", FilterType = "string", Width = "120" },
    };

    private static void ApplyWarehouseDefaults(List<ColumnDef> cols, string whCode)
    {
        foreach (var c in cols)
        {
            c.IsApplicable = true;
            c.Visible = true;
        }

        switch (whCode)
        {
            case "RAW":
                SetNotApplicable(cols, "MinLength");
                SetNotApplicable(cols, "MaxLength");
                SetNotApplicable(cols, "Meters");
                SetNotApplicable(cols, "ActualSpecification");
                SetNotApplicable(cols, "ActualOuterDiameter");
                SetNotApplicable(cols, "ActualWallThickness");
                SetNotApplicable(cols, "ProductionBatchNo");
                SetNotApplicable(cols, "DefectReason");
                SetNotApplicable(cols, "LiabilityType");
                SetNotApplicable(cols, "OriginalSupplier");
                SetNotApplicable(cols, "TagNo");
                SetNotApplicable(cols, "DefectRemark");
                break;
            case "FG":
                SetNotApplicable(cols, "DefectReason");
                SetNotApplicable(cols, "LiabilityType");
                SetNotApplicable(cols, "OriginalSupplier");
                SetNotApplicable(cols, "TagNo");
                SetNotApplicable(cols, "DefectRemark");
                break;
            case "DEFECT":
                SetNotApplicable(cols, "Meters");
                SetNotApplicable(cols, "ActualSpecification");
                SetNotApplicable(cols, "ActualOuterDiameter");
                SetNotApplicable(cols, "ActualWallThickness");
                SetNotApplicable(cols, "SourceOrderNo");
                break;
            case "WIP":
                SetNotApplicable(cols, "IsLinkedToWorkOrder");
                SetNotApplicable(cols, "WorkOrderNo");
                SetNotApplicable(cols, "SalesOrderNo");
                SetNotApplicable(cols, "OrderItemIds");
                SetNotApplicable(cols, "DefectReason");
                SetNotApplicable(cols, "LiabilityType");
                SetNotApplicable(cols, "OriginalSupplier");
                SetNotApplicable(cols, "TagNo");
                SetNotApplicable(cols, "DefectRemark");
                SetNotApplicable(cols, "Meters");
                SetNotApplicable(cols, "SourceOrderNo");
                break;
        }
    }

    private static void SetNotApplicable(List<ColumnDef> cols, string key)
    {
        var c = cols.FirstOrDefault(x => x.Key == key);
        if (c != null)
        {
            c.IsApplicable = false;
            c.Visible = false;
        }
    }

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        var whCode = Code?.ToUpperInvariant() ?? "";
        await ColumnPrefs.SaveAsync("inbound_history", whCode, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        ApplyWarehouseDefaults(_allColumns, Code?.ToUpperInvariant() ?? "");
        await SaveColumnPrefs();
        if (_table != null) await _table.ReloadServerData();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<InventoryBatchDto>> LoadDataFromServer(TableState state)
    {
        try
        {
            _pageSize = state.PageSize;
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "InboundDate";
            var filtersJson = SerializeFilters();

            // 恢复持久化的页码（MudTable 初始化时始终传 page=0）
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            var query = new InventoryQueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                SortBy = sortBy,
                IsDescending = sortDescending,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                WarehouseId = _warehouseId,
                OnlyWithStock = false,
            };

            var result = await InventoryService.GetPagedAsync(query, filtersJson);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;
                ComputePageSums();

                // 清理已删除项
                _selectedItems.RemoveWhere(i => !_pageItems.Any(x => x.Id == i.Id));
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
            _pageSums.Clear();
        }

        return new TableData<InventoryBatchDto>
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
            var result = await InventoryService.GetInventoryFilterContextsAsync();
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

        // InboundSource 列显示中文
        if (_filterContextOptions.TryGetValue("InboundSource", out var sourceOptions))
        {
            foreach (var opt in sourceOptions)
            {
                opt.Display = DisplayHelper.GetInboundSourceText(opt.Value);
            }
        }

        // LengthStatus 列显示中文
        if (_filterContextOptions.TryGetValue("LengthStatus", out var lengthOptions))
        {
            foreach (var opt in lengthOptions)
            {
                opt.Display = DisplayHelper.GetLengthStatusText(opt.Value);
            }
        }

        // IsLinkedToWorkOrder 列显示中文
        if (_filterContextOptions.TryGetValue("IsLinkedToWorkOrder", out var linkedOptions))
        {
            foreach (var opt in linkedOptions)
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

    // ========== ExcelFilter 回调 ==========

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        await SavePageStateAsync();
        if (_table != null) await _table.ReloadServerData();
    }


    // ========== 排序 ==========

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
        if (_table != null) await _table.ReloadServerData();
    }

    // ========== 搜索 ==========

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        ClearEditState();
        _selectedItems.Clear();
        await SavePageStateAsync();
        if (_table != null) await _table.ReloadServerData();
    }

    // ========== 编辑器类型推断 ==========

    private static string GetEditorType(string key) => key switch
    {
        "InboundSource" => "select",
        "LengthStatus" => "select",
        "IsLinkedToWorkOrder" => "bool",
        "InitialQuantity" => "int",
        "InitialWeight" => "decimal",
        "InboundDate" => "date",
        "UnitWeight" or "MinLength" or "MaxLength" or "Meters"
            or "ActualOuterDiameter" or "ActualWallThickness" => "nullableDecimal",
        _ => "text"
    };

    // ========== 内联编辑渲染 ==========

    private RenderFragment RenderInlineEditor(InventoryBatchDto item, ColumnDef col) => builder =>
    {
        // 批次号只读
        if (col.Key == "BatchNo")
        {
            builder.AddContent(0, item.BatchNo);
            return;
        }

        switch (GetEditorType(col.Key))
        {
            case "select":
                var options = col.Key == "LengthStatus" ? _lengthStatusOptions : _inboundSourceOptions;
                var selVal = GetCellStringValue(item, col.Key);
                builder.OpenComponent<MudSelect<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Value", selVal);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => SetCellStringValue(item, col.Key, v)));
                builder.AddAttribute(5, "ChildContent", (RenderFragment)(b2 =>
                {
                    foreach (var opt in options)
                    {
                        b2.OpenComponent<MudSelectItem<string>>(0);
                        b2.AddAttribute(1, "Value", opt.Value);
                        b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, opt.Text)));
                        b2.CloseComponent();
                    }
                }));
                builder.CloseComponent();
                break;

            case "bool":
                builder.OpenComponent<MudSwitch<bool>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Color", Color.Primary);
                builder.AddAttribute(3, "Value", item.IsLinkedToWorkOrder);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<bool>(this, v =>
                {
                    item.IsLinkedToWorkOrder = v;
                    if (!v)
                    {
                        item.WorkOrderNo = null;
                        item.SalesOrderNo = null;
                        item.OrderItemIds = null;
                    }
                }));
                builder.CloseComponent();
                break;

            case "int":
                builder.OpenComponent<MudNumericField<int>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "HideSpinButtons", true);
                builder.AddAttribute(3, "Value", item.InitialQuantity);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<int>(this, v => item.InitialQuantity = v));
                builder.CloseComponent();
                break;

            case "decimal":
                builder.OpenComponent<MudNumericField<decimal>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "HideSpinButtons", true);
                builder.AddAttribute(3, "Format", "G29");
                builder.AddAttribute(4, "Value", item.InitialWeight);
                builder.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<decimal>(this, v => item.InitialWeight = v));
                builder.CloseComponent();
                break;

            case "nullableDecimal":
                builder.OpenComponent<MudNumericField<decimal?>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "HideSpinButtons", true);
                builder.AddAttribute(3, "Format", "G29");
                builder.AddAttribute(3, "Value", GetCellDecimalValue(item, col.Key));
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, v => SetCellDecimalValue(item, col.Key, v)));
                builder.CloseComponent();
                break;

            case "date":
                if (!_editDateStrings.ContainsKey(item.Id))
                    _editDateStrings[item.Id] = item.InboundDate.ToString("yyyy-MM-dd");
                var dateVal = _editDateStrings[item.Id];
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Value", dateVal);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v =>
                {
                    _editDateStrings[item.Id] = v ?? item.InboundDate.ToString("yyyy-MM-dd");
                    if (DateTime.TryParse(v, out var dt))
                        item.InboundDate = dt;
                }));
                builder.CloseComponent();
                break;

            default: // text
                var txtVal = GetCellStringValue(item, col.Key);
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Dense", true);
                builder.AddAttribute(2, "Variant", Variant.Outlined);
                builder.AddAttribute(3, "Value", txtVal);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => SetCellStringValue(item, col.Key, v)));
                builder.CloseComponent();
                break;
        }
    };

    // ========== 只读单元格渲染 ==========

    private RenderFragment RenderCell(InventoryBatchDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "BatchNo":
                builder.AddContent(0, item.BatchNo);
                break;
            case "InboundDate":
                builder.AddContent(0, item.InboundDate.ToString("yyyy-MM-dd"));
                break;
            case "InboundSource":
                builder.AddContent(0, DisplayHelper.GetInboundSourceText(item.InboundSource));
                break;
            case "MaterialType":
                builder.AddContent(0, item.MaterialType);
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade);
                break;
            case "Specification":
                builder.AddContent(0, item.Specification);
                break;
            case "SourceName":
                builder.AddContent(0, TruncateText(item.SourceName));
                break;
            case "LocationArea":
                builder.AddContent(0, TruncateText(item.LocationArea));
                break;
            case "InitialQuantity":
                builder.AddContent(0, item.InitialQuantity);
                break;
            case "InitialWeight":
                builder.AddContent(0, ((int)item.InitialWeight).ToString());
                break;
            case "UnitWeight":
                if (item.UnitWeight.HasValue)
                    builder.AddContent(0, item.UnitWeight.Value.ToString("G29"));
                break;
            case "MinLength":
                if (item.MinLength.HasValue)
                    builder.AddContent(0, item.MinLength.Value.ToString("G29"));
                break;
            case "MaxLength":
                if (item.MaxLength.HasValue)
                    builder.AddContent(0, item.MaxLength.Value.ToString("G29"));
                break;
            case "Meters":
                if (item.Meters.HasValue)
                    builder.AddContent(0, ((int)item.Meters.Value).ToString());
                break;
            case "ActualOuterDiameter":
                if (item.ActualOuterDiameter.HasValue)
                    builder.AddContent(0, item.ActualOuterDiameter.Value.ToString("G29"));
                break;
            case "ActualWallThickness":
                if (item.ActualWallThickness.HasValue)
                    builder.AddContent(0, item.ActualWallThickness.Value.ToString("G29"));
                break;
            case "IsLinkedToWorkOrder":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", item.IsLinkedToWorkOrder ? Color.Success : Color.Default);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetYesNoText(item.IsLinkedToWorkOrder))));
                builder.CloseComponent();
                break;
            case "SourceOrderNo":
                if (!string.IsNullOrEmpty(item.SourceOrderNo))
                    builder.AddContent(0, item.SourceOrderNo);
                break;
            case "LengthStatus":
                if (!string.IsNullOrEmpty(item.LengthStatus))
                    builder.AddContent(0, DisplayHelper.GetLengthStatusText(item.LengthStatus));
                break;
            default:
                var val = GetCellStringValue(item, col.Key);
                if (!string.IsNullOrEmpty(val))
                    builder.AddContent(0, val);
                break;
        }
    };

    // ========== 编辑状态管理 ==========

    private void StartEdit(InventoryBatchDto item)
    {
        _editingRowId = item.Id;
        _editDateStrings[item.Id] = item.InboundDate.ToString("yyyy-MM-dd");
    }

    private void CancelEdit()
    {
        ClearEditState();
    }

    // ========== 辅助方法 ==========

    private static string TruncateText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length > 4 ? text[..4] + "…" : text;
    }

    // ========== Getter/Setter 辅助 ==========

    private string? GetCellStringValue(InventoryBatchDto item, string key) => key switch
    {
        "MaterialType" => item.MaterialType,
        "InboundSource" => item.InboundSource,
        "SourceName" => item.SourceName,
        "HeatNo" => item.HeatNo,
        "PlantGrade" => item.PlantGrade,
        "Specification" => item.Specification,
        "LengthStatus" => item.LengthStatus,
        "SurfaceCondition" => item.SurfaceCondition,
        "LocationArea" => item.LocationArea,
        "LocationRack" => item.LocationRack,
        "Remark" => item.Remark,
        "ActualSpecification" => item.ActualSpecification,
        "ProductionBatchNo" => item.ProductionBatchNo,
        "DefectReason" => item.DefectReason,
        "LiabilityType" => item.LiabilityType,
        "OriginalSupplier" => item.OriginalSupplier,
        "TagNo" => item.TagNo,
        "DefectRemark" => item.DefectRemark,
        "WorkOrderNo" => item.WorkOrderNo,
        "SalesOrderNo" => item.SalesOrderNo,
        "OrderItemIds" => item.OrderItemIds,
        "SourceOrderNo" => item.SourceOrderNo,
        _ => null
    };

    private void SetCellStringValue(InventoryBatchDto item, string key, string? value)
    {
        switch (key)
        {
            case "MaterialType": item.MaterialType = value ?? ""; break;
            case "InboundSource": item.InboundSource = value ?? ""; break;
            case "SourceName": item.SourceName = value ?? ""; break;
            case "HeatNo": item.HeatNo = value; break;
            case "PlantGrade": item.PlantGrade = value ?? ""; break;
            case "Specification": item.Specification = value ?? ""; break;
            case "LengthStatus": item.LengthStatus = value; break;
            case "SurfaceCondition": item.SurfaceCondition = value; break;
            case "LocationArea": item.LocationArea = value; break;
            case "LocationRack": item.LocationRack = value; break;
            case "Remark": item.Remark = value; break;
            case "ActualSpecification": item.ActualSpecification = value; break;
            case "ProductionBatchNo": item.ProductionBatchNo = value; break;
            case "DefectReason": item.DefectReason = value; break;
            case "LiabilityType": item.LiabilityType = value; break;
            case "OriginalSupplier": item.OriginalSupplier = value; break;
            case "TagNo": item.TagNo = value; break;
            case "DefectRemark": item.DefectRemark = value; break;
            case "WorkOrderNo": item.WorkOrderNo = value; break;
            case "SalesOrderNo": item.SalesOrderNo = value; break;
            case "OrderItemIds": item.OrderItemIds = value; break;
            case "SourceOrderNo": item.SourceOrderNo = value; break;
        }
    }

    private decimal? GetCellDecimalValue(InventoryBatchDto item, string key) => key switch
    {
        "UnitWeight" => item.UnitWeight,
        "MinLength" => item.MinLength,
        "MaxLength" => item.MaxLength,
        "Meters" => item.Meters,
        "ActualOuterDiameter" => item.ActualOuterDiameter,
        "ActualWallThickness" => item.ActualWallThickness,
        _ => null
    };

    private void SetCellDecimalValue(InventoryBatchDto item, string key, decimal? value)
    {
        switch (key)
        {
            case "UnitWeight": item.UnitWeight = value; break;
            case "MinLength": item.MinLength = value; break;
            case "MaxLength": item.MaxLength = value; break;
            case "Meters": item.Meters = value; break;
            case "ActualOuterDiameter": item.ActualOuterDiameter = value; break;
            case "ActualWallThickness": item.ActualWallThickness = value; break;
        }
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        var whResult = await WarehouseService.GetAllAsync(true);
        if (whResult.Success && whResult.Data != null)
            _warehouses = whResult.Data;

        if (!string.IsNullOrEmpty(Code))
            await ResolveWarehouse();

        // 恢复排序/筛选状态
        var savedState = await PageState.LoadAsync("inboundhistory");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "InboundDate";
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
        if (savedState != null && _table != null)
            await _table.ReloadServerData();

        // 加载筛选上下文
        await LoadFilterContextsAsync();

        await CheckWorkOrderNotices();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(Code))
        {
            var prevCode = _lastResolvedWarehouseCode;
            await ResolveWarehouse();
            // 仅在仓库代码实际变更时重新加载数据（OnInitializedAsync 已完成首次加载）
            if (!string.Equals(prevCode, _lastResolvedWarehouseCode, StringComparison.OrdinalIgnoreCase))
            {
                ClearEditState();
                if (_table != null) await _table.ReloadServerData();
            }
        }
        await CheckWorkOrderNotices();
    }

    private async Task ResolveWarehouse()
    {
        var whCode = Code?.ToUpperInvariant() ?? "";

        // 切换仓库时清空该仓库的筛选状态（首次加载不清空，由 OnInitializedAsync 恢复持久化状态）
        if (!string.IsNullOrEmpty(_lastResolvedWarehouseCode) &&
            !string.Equals(_lastResolvedWarehouseCode, whCode, StringComparison.OrdinalIgnoreCase))
        {
            _searchKeyword = string.Empty;
            _columnFilters.Clear();
        }
        _lastResolvedWarehouseCode = whCode;
        _selectedItems.Clear();
        var wh = _warehouses.FirstOrDefault(w => w.Code.Equals(whCode, StringComparison.OrdinalIgnoreCase));
        if (wh != null)
        {
            _warehouseId = wh.Id;
            _warehouseName = wh.Name;
        }
        else
        {
            _warehouseId = null;
            _warehouseName = Code ?? "";
        }

        // 初始化列定义
        _allColumns = GetAllColumnDefs();
        ApplyWarehouseDefaults(_allColumns, whCode);

        // 加载用户自定义列偏好
        var saved = await ColumnPrefs.LoadAsync("inbound_history", whCode);
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

        // 不适用列强制隐藏
        foreach (var c in _allColumns)
        {
            if (!c.IsApplicable) c.Visible = false;
        }

        // 重新加载筛选上下文
        await LoadFilterContextsAsync();
    }

    // ========== 行保存（更改） ==========

    private async Task SaveRow(InventoryBatchDto item)
    {
        // 验证
        var errors = new List<string>();
        if (item.InitialQuantity < 1) errors.Add("支数必须大于0");
        if (item.InitialWeight <= 0) errors.Add("重量必须大于0");

        // 验证入库日期
        DateTime parsedDate = item.InboundDate;
        if (_editDateStrings.TryGetValue(item.Id, out var dateStr))
        {
            if (!DateTime.TryParse(dateStr, out parsedDate))
                errors.Add("入库日期无效，请按 yyyy-MM-dd 格式输入");
        }

        if (errors.Any())
        {
            Snackbar.Add(string.Join("\n", errors), Severity.Error);
            return;
        }

        _savingItemId = item.Id;
        StateHasChanged();

        try
        {
            var request = new UpdateInventoryBatchRequest
            {
                BatchNo = item.BatchNo,
                MaterialType = item.MaterialType,
                PlantGrade = item.PlantGrade,
                Specification = item.Specification,
                InboundSource = item.InboundSource,
                SourceName = item.SourceName,
                InboundDate = parsedDate,
                HeatNo = string.IsNullOrEmpty(item.HeatNo) ? null : item.HeatNo,
                ProductionBatchNo = string.IsNullOrEmpty(item.ProductionBatchNo) ? null : item.ProductionBatchNo,
                LengthStatus = string.IsNullOrEmpty(item.LengthStatus) ? null : item.LengthStatus,
                MinLength = item.MinLength,
                MaxLength = item.MaxLength,
                InitialQuantity = item.InitialQuantity,
                InitialWeight = item.InitialWeight,
                UnitWeight = item.UnitWeight,
                Meters = item.Meters,
                SurfaceCondition = string.IsNullOrEmpty(item.SurfaceCondition) ? null : item.SurfaceCondition,
                LocationArea = string.IsNullOrEmpty(item.LocationArea) ? null : item.LocationArea,
                LocationRack = string.IsNullOrEmpty(item.LocationRack) ? null : item.LocationRack,
                Remark = string.IsNullOrEmpty(item.Remark) ? null : item.Remark,
                ActualSpecification = string.IsNullOrEmpty(item.ActualSpecification) ? null : item.ActualSpecification,
                ActualOuterDiameter = item.ActualOuterDiameter,
                ActualWallThickness = item.ActualWallThickness,
                DefectReason = string.IsNullOrEmpty(item.DefectReason) ? null : item.DefectReason,
                LiabilityType = string.IsNullOrEmpty(item.LiabilityType) ? null : item.LiabilityType,
                OriginalSupplier = string.IsNullOrEmpty(item.OriginalSupplier) ? null : item.OriginalSupplier,
                TagNo = string.IsNullOrEmpty(item.TagNo) ? null : item.TagNo,
                DefectRemark = string.IsNullOrEmpty(item.DefectRemark) ? null : item.DefectRemark,
                IsLinkedToWorkOrder = item.IsLinkedToWorkOrder,
                WorkOrderNo = string.IsNullOrEmpty(item.WorkOrderNo) ? null : item.WorkOrderNo,
                SalesOrderNo = string.IsNullOrEmpty(item.SalesOrderNo) ? null : item.SalesOrderNo,
                OrderItemIds = string.IsNullOrEmpty(item.OrderItemIds) ? null : item.OrderItemIds,
                SourceOrderNo = string.IsNullOrEmpty(item.SourceOrderNo) ? null : item.SourceOrderNo,
            };

            var result = await InventoryService.UpdateInventoryBatchAsync(item.Id, request);
            if (result.Success)
            {
                Snackbar.Add("更改成功", Severity.Success);
                _editingRowId = null;
                _editDateStrings.Remove(item.Id);
                if (_table != null) await _table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "更改失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"网络错误: {ex.Message}", Severity.Error);
        }
        finally
        {
            _savingItemId = null;
            StateHasChanged();
        }
    }

    // ========== 删除 ==========

    private async Task ConfirmDelete(InventoryBatchDto item)
    {
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("确认",
            new DialogParameters
            {
                { "ContentText", $"确认物理删除批次「{item.BatchNo}」？\n删除后将同时删除关联的出库记录，且不可恢复！" },
                { "ConfirmText", "确认删除" },
                { "Color", Color.Error }
            });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        try
        {
            var result = await InventoryService.HardDeleteInventoryBatchAsync(item.Id);
            if (result.Success)
            {
                Snackbar.Add($"批次「{item.BatchNo}」已删除", Severity.Success);
                ClearEditState();
                if (_table != null) await _table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "删除失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"网络错误: {ex.Message}", Severity.Error);
        }
    }

    // ========== 打印 ==========

    private async Task PrintSelected()
    {
        if (!_selectedItems.Any())
        {
            Snackbar.Add("请先选择要打印的入库记录", Severity.Warning);
            return;
        }
        try
        {
            var columns = _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();
            var ids = _selectedItems.Select(i => i.Id).ToArray();
            var result = await InventoryService.PrintInventorySelectedAsync(new InventoryPrintSelectedRequest
            {
                Ids = ids,
                Columns = columns
            });
            if (result.Success && result.Data != null)
                await JS.InvokeVoidAsync("openPdf", result.Data);
            else
                Snackbar.Add(result.Message ?? "打印失败", Severity.Error);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    private async Task PrintAll()
    {
        try
        {
            var columns = _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();
            var result = await InventoryService.PrintInventoryAllAsync(new InventoryPrintAllRequest
            {
                Keyword = string.IsNullOrEmpty(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortColumn,
                IsDescending = sortDescending,
                WarehouseId = _warehouseId ?? 0,
                OnlyWithStock = false,
                Columns = columns
            });
            if (result.Success && result.Data != null)
                await JS.InvokeVoidAsync("openPdf", result.Data);
            else
                Snackbar.Add(result.Message ?? "打印失败", Severity.Error);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    // ========== 工单删除通知检查 ==========

    private async Task CheckWorkOrderNotices()
    {
        try
        {
            var result = await NotificationService.GetByTypeAsync("WorkOrderDeleted");
            if (result.Success && result.Data != null)
                _workOrderNotices = result.Data;
        }
        catch
        {
            // 通知检查失败不影响主页面
        }
    }

    private async Task DismissWorkOrderNotices()
    {
        await NotificationService.MarkAllByTypeAsReadAsync("WorkOrderDeleted");
        _workOrderNotices = null;
    }

    private void GoBack() => Navigation.NavigateTo(!string.IsNullOrEmpty(Code) ? $"/warehouse/{Code.ToLowerInvariant()}" : "/warehouse");

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
        await PageState.SaveAsync("inboundhistory", state);
    }

    private void ComputePageSums()
    {
        _pageSums.Clear();
        var props = typeof(InventoryBatchDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var key in _summableColumnKeys)
        {
            var prop = props.FirstOrDefault(p => p.Name == key);
            if (prop == null) continue;
            decimal sum = 0;
            foreach (var item in _pageItems)
            {
                var val = prop.GetValue(item);
                if (val == null) continue;
                sum += Convert.ToDecimal(val);
            }
            _pageSums[key] = ((int)sum).ToString();
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        return _pageSums.GetValueOrDefault(col.Key, "");
    }
}
