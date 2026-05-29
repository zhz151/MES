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

namespace MES.Blazor.Pages.Warehouse;

public partial class WarehouseInventory
{
    [Parameter]
    public string? Code { get; set; }

    private MudTable<InventoryBatchDto>? table;
    private List<InventoryBatchDto> _pageItems = new();
    private int _totalCount;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _lastResolvedWarehouseCode = string.Empty;
    private List<WarehouseDto> warehouses = new();

    // 当前仓库信息
    private string warehouseCode = string.Empty;
    private string warehouseName = string.Empty;
    private int warehouseId;

    // 出库模式
    private bool _outboundMode;

    // 关键字搜索
    private string _searchKeyword = string.Empty;

    // 排序状态
    private string sortColumn = "InboundDate";
    private bool sortDescending = true;

    // ========== ExcelFilter 状态 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // 列定义
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    // 多选
    private HashSet<InventoryBatchDto> _selectedItems = new();

    // ========== 列定义 ==========

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "BatchNo",             Label = "批次号",   SortKey = "BatchNo", FilterType = "string", Width = "120" },
        new() { Key = "InboundDate",         Label = "入库日期", SortKey = "InboundDate", FilterType = "date", Width = "120" },
        new() { Key = "InboundSource",       Label = "来源",     SortKey = "InboundSource", FilterType = "string", Width = "120" },
        new() { Key = "SourceOrderNo",       Label = "物料单号", SortKey = "SourceOrderNo", FilterType = "string", Width = "120" },
        new() { Key = "MaterialType",        Label = "物料",     SortKey = "MaterialType", FilterType = "string", Width = "120" },
        new() { Key = "SourceName",          Label = "来料单位", SortKey = "SourceName", FilterType = "string", Width = "120" },
        new() { Key = "SurfaceCondition",    Label = "物料状态", SortKey = "SurfaceCondition", FilterType = "string", Width = "120" },
        new() { Key = "LocationArea",        Label = "区域", SortKey = "LocationArea", FilterType = "string", Width = "120" },
        new() { Key = "LocationRack",        Label = "框架", SortKey = "LocationRack", FilterType = "string", Width = "120" },
        new() { Key = "HeatNo",              Label = "炉号",     SortKey = "HeatNo", FilterType = "string", Width = "120" },
        new() { Key = "PlantGrade",          Label = "工厂牌号", SortKey = "PlantGrade", FilterType = "string", Width = "120" },
        new() { Key = "Specification",       Label = "名义规格", SortKey = "Specification", FilterType = "string", Width = "120" },
        new() { Key = "LengthStatus",        Label = "长度状态", SortKey = "LengthStatus", FilterType = "string", Width = "120" },
        new() { Key = "MinLength",           Label = "最小长度", SortKey = "MinLength", Width = "80" },
        new() { Key = "MaxLength",           Label = "最大长度", SortKey = "MaxLength", Width = "80" },
        new() { Key = "InitialQuantity",     Label = "支数",     SortKey = "InitialQuantity", Width = "80" },
        new() { Key = "InitialWeight",       Label = "重量(kg)", SortKey = "InitialWeight", Width = "80" },
        new() { Key = "UnitWeight",          Label = "单支重",   SortKey = "UnitWeight", Width = "80" },
        new() { Key = "Meters",              Label = "米数", SortKey = "Meters", Width = "80" },
        new() { Key = "Remark",              Label = "备注", SortKey = "Remark", FilterType = "string", Width = "120" },
        new() { Key = "RemainingQuantity",   Label = "剩余支数", SortKey = "RemainingQuantity", Width = "80" },
        new() { Key = "RemainingWeight",     Label = "剩余重量", SortKey = "RemainingWeight", Width = "80" },
        new() { Key = "IsLinkedToWorkOrder", Label = "关联工单", SortKey = "IsLinkedToWorkOrder", FilterType = "boolean", Width = "120", BoolTrueLabel = "是", BoolFalseLabel = "否" },
        new() { Key = "WorkOrderNo",         Label = "工单号",   SortKey = "WorkOrderNo", FilterType = "string", Width = "120" },
        new() { Key = "SalesOrderNo",        Label = "订单号",   SortKey = "SalesOrderNo", FilterType = "string", Width = "120" },
        new() { Key = "OrderItemIds",        Label = "项次", SortKey = "OrderItemIds", FilterType = "string", Width = "120" },
        new() { Key = "ProductionBatchNo",   Label = "生产批号", SortKey = "ProductionBatchNo", FilterType = "string", Width = "120" },
        new() { Key = "ActualSpecification", Label = "实际规格", SortKey = "ActualSpecification", FilterType = "string", Width = "120" },
        new() { Key = "ActualOuterDiameter", Label = "外径", SortKey = "ActualOuterDiameter", Width = "80" },
        new() { Key = "ActualWallThickness", Label = "壁厚", SortKey = "ActualWallThickness", Width = "80" },
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

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(InventoryBatchDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "BatchNo":
                if (!string.IsNullOrEmpty(item.BatchNo))
                    builder.AddContent(0, item.BatchNo);
                break;
            case "MaterialType":
                builder.AddContent(0, item.MaterialType);
                break;
            case "InboundSource":
                builder.AddContent(0, DisplayHelper.GetInboundSourceText(item.InboundSource));
                break;
            case "SourceName":
                builder.AddContent(0, TruncateSourceName(item.SourceName));
                break;
            case "InboundDate":
                builder.AddContent(0, item.InboundDate.ToString("yyyy-MM-dd"));
                break;
            case "HeatNo":
                builder.AddContent(0, item.HeatNo);
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade);
                break;
            case "Specification":
                builder.AddContent(0, item.Specification);
                break;
            case "LengthStatus":
                builder.AddContent(0, DisplayHelper.GetLengthStatusText(item.LengthStatus));
                break;
            case "MinLength":
                if (item.MinLength.HasValue)
                    builder.AddContent(0, item.MinLength.Value.ToString("G29"));
                break;
            case "MaxLength":
                if (item.MaxLength.HasValue)
                    builder.AddContent(0, item.MaxLength.Value.ToString("G29"));
                break;
            case "InitialQuantity":
                builder.AddContent(0, item.InitialQuantity);
                break;
            case "InitialWeight":
                builder.AddContent(0, item.InitialWeight.ToString("G29"));
                break;
            case "Meters":
                if (item.Meters.HasValue)
                    builder.AddContent(0, item.Meters.Value.ToString("G29"));
                break;
            case "RemainingQuantity":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", item.RemainingQuantity > 0 ? Color.Success : Color.Default);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.RemainingQuantity)));
                builder.CloseComponent();
                break;
            case "RemainingWeight":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", item.RemainingWeight > 0 ? Color.Success : Color.Default);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.RemainingWeight.ToString("G29"))));
                builder.CloseComponent();
                break;
            case "UnitWeight":
                if (item.UnitWeight.HasValue)
                    builder.AddContent(0, item.UnitWeight.Value.ToString("G29"));
                break;
            case "ActualSpecification":
                builder.AddContent(0, item.ActualSpecification);
                break;
            case "ActualOuterDiameter":
                if (item.ActualOuterDiameter.HasValue)
                    builder.AddContent(0, item.ActualOuterDiameter.Value.ToString("G29"));
                break;
            case "ActualWallThickness":
                if (item.ActualWallThickness.HasValue)
                    builder.AddContent(0, item.ActualWallThickness.Value.ToString("G29"));
                break;
            case "SurfaceCondition":
                builder.AddContent(0, item.SurfaceCondition);
                break;
            case "LocationArea":
                builder.AddContent(0, TruncateSourceName(item.LocationArea));
                break;
            case "LocationRack":
                builder.AddContent(0, item.LocationRack);
                break;
            case "Remark":
                builder.AddContent(0, item.Remark);
                break;
            case "DefectReason":
                builder.AddContent(0, item.DefectReason);
                break;
            case "LiabilityType":
                builder.AddContent(0, item.LiabilityType);
                break;
            case "OriginalSupplier":
                builder.AddContent(0, item.OriginalSupplier);
                break;
            case "TagNo":
                builder.AddContent(0, item.TagNo);
                break;
            case "DefectRemark":
                builder.AddContent(0, item.DefectRemark);
                break;
            case "ProductionBatchNo":
                builder.AddContent(0, item.ProductionBatchNo);
                break;
            case "IsLinkedToWorkOrder":
                if (item.IsLinkedToWorkOrder)
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
                break;
            case "WorkOrderNo":
                builder.AddContent(0, item.WorkOrderNo);
                break;
            case "SalesOrderNo":
                builder.AddContent(0, item.SalesOrderNo);
                break;
            case "OrderItemIds":
                builder.AddContent(0, item.OrderItemIds);
                break;
            case "SourceOrderNo":
                if (!string.IsNullOrEmpty(item.SourceOrderNo))
                    builder.AddContent(0, item.SourceOrderNo);
                break;
        }
    };

    // ========== 来料单位截断 ==========
    private static string TruncateSourceName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        return name.Length > 4 ? name[..4] + "\u2026" : name;
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<InventoryBatchDto>> LoadDataFromServer(TableState state)
    {
        try
        {
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
                WarehouseId = warehouseId,
                OnlyWithStock = true
            };

            var result = await InventoryService.GetPagedAsync(query, filtersJson);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;

                _selectedItems.RemoveWhere(i => !_pageItems.Any(x => x.Id == i.Id));
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
        _selectedItems.Clear();
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 列管理 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("inventory", warehouseCode, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        ApplyWarehouseDefaults(_allColumns, warehouseCode);
        foreach (var c in _allColumns)
        {
            if (c.IsApplicable) c.Visible = true;
        }
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

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        var result = await WarehouseService.GetAllAsync(true);
        if (result.Success && result.Data != null)
            warehouses = result.Data;

        if (!string.IsNullOrEmpty(Code))
        {
            await ResolveWarehouse();

            // 恢复排序/筛选状态
            var savedState = await PageState.LoadAsync("warehouseinventory");
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
            if (savedState != null && table != null)
                await table.ReloadServerData();

            // 加载筛选上下文
            await LoadFilterContextsAsync();
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(Code))
        {
            var prevCode = warehouseCode;
            await ResolveWarehouse();
            // 仅在仓库代码实际变更时重新加载数据（OnInitializedAsync 已完成首次加载）
            if (!string.Equals(prevCode, warehouseCode, StringComparison.OrdinalIgnoreCase))
            {
                if (table != null) await table.ReloadServerData();
            }
        }
    }

    private async Task ResolveWarehouse()
    {
        var newCode = Code?.ToUpperInvariant() ?? "";
        warehouseCode = newCode;

        // 切换仓库时清空该仓库的筛选状态（首次加载不清空，由 OnInitializedAsync 恢复持久化状态）
        if (!string.IsNullOrEmpty(_lastResolvedWarehouseCode) &&
            !string.Equals(_lastResolvedWarehouseCode, newCode, StringComparison.OrdinalIgnoreCase))
        {
            _searchKeyword = string.Empty;
            _columnFilters.Clear();
        }
        _lastResolvedWarehouseCode = newCode;
        _outboundMode = false;
        _selectedItems.Clear();

        var wh = warehouses.FirstOrDefault(w => w.Code.Equals(warehouseCode, StringComparison.OrdinalIgnoreCase));
        if (wh != null)
        {
            warehouseId = wh.Id;
            warehouseName = wh.Name;
        }
        else
        {
            warehouseName = warehouseCode;
        }

        // 初始化列定义
        _allColumns = GetAllColumnDefs();
        ApplyWarehouseDefaults(_allColumns, warehouseCode);

        // 加载用户自定义列偏好
        var saved = await ColumnPrefs.LoadAsync("inventory", warehouseCode);
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

        foreach (var c in _allColumns)
        {
            if (!c.IsApplicable) c.Visible = false;
        }

        // 加载筛选上下文
        await LoadFilterContextsAsync();
    }

    // ========== 出库模式切换 ==========

    private void ToggleOutboundMode()
    {
        _outboundMode = !_outboundMode;
        if (!_outboundMode)
        {
            _selectedItems.Clear();
        }
    }

    private void ToggleItemSelection(InventoryBatchDto item)
    {
        if (_selectedItems.Contains(item))
            _selectedItems.Remove(item);
        else
            _selectedItems.Add(item);
        StateHasChanged();
    }

    private bool IsItemSelected(InventoryBatchDto item) => _selectedItems.Contains(item);

    // ========== 跳转到出库页面 ==========

    private void NavigateToOutbound()
    {
        if (_selectedItems.Count == 0) return;

        OutboundState.SelectedItems = _selectedItems.ToList();
        OutboundState.WarehouseCode = warehouseCode;
        OutboundState.WarehouseName = warehouseName;
        OutboundState.WarehouseId = warehouseId;

        _outboundMode = false;
        _selectedItems.Clear();

        Navigation.NavigateTo("/warehouse/outbound");
    }

    // ========== 打印 ==========

    private async Task PrintSelected()
    {
        if (!_selectedItems.Any())
        {
            Snackbar.Add("请先选择要打印的批次", Severity.Warning);
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
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortColumn,
                IsDescending = sortDescending,
                WarehouseId = warehouseId,
                OnlyWithStock = true,
                Columns = columns
            });
            if (result.Success && result.Data != null)
                await JS.InvokeVoidAsync("openPdf", result.Data);
            else
                Snackbar.Add(result.Message ?? "打印失败", Severity.Error);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    // ========== 导航 ==========

    private void NavigateToInboundFromHome() => Navigation.NavigateTo("/warehouse/inbound");
    private void NavigateToInbound() => Navigation.NavigateTo($"/warehouse/inbound/{warehouseCode.ToLowerInvariant()}");

    private void NavigateToWarehouse(string code) => Navigation.NavigateTo($"/warehouse/{code.ToLowerInvariant()}");

    private void NavigateToInboundHistory() => Navigation.NavigateTo(string.IsNullOrEmpty(Code) ? "/warehouse/inbound-history" : $"/warehouse/inbound-history/{warehouseCode.ToLowerInvariant()}");
    private void NavigateToOutboundHistory() => Navigation.NavigateTo(string.IsNullOrEmpty(Code) ? "/warehouse/outbound-history" : $"/warehouse/outbound-history/{warehouseCode.ToLowerInvariant()}");

    private static string GetCardStyle(bool isActive) => isActive ? "cursor:pointer;" : "cursor:pointer;opacity:0.5;";

    private static string GetWarehouseIcon(string code) => code.ToUpperInvariant() switch
    {
        "RAW" => Icons.Material.Filled.Inventory2,
        "FG" => Icons.Material.Filled.Inventory,
        "DEFECT" => Icons.Material.Filled.ErrorOutline,
        "WIP" => Icons.Material.Filled.PrecisionManufacturing,
        _ => Icons.Material.Filled.Warehouse
    };

    // ========== 工单号即时更新验证 ==========

    private bool _mismatchLoaded;

    private async Task LoadWarehouseMismatches()
    {
        if (warehouseId <= 0) return;

        try
        {
            var result = await InventoryService.ValidateWorkOrderNosAsync(warehouseId);
            if (result.Success && result.Data != null && result.Data.Count > 0)
            {
                var woNos = string.Join("\u3001", result.Data);
                Snackbar.Add($"入库数据中包含已不存在的工单号：{woNos}，需修改！",
                    Severity.Warning, config =>
                    {
                        config.VisibleStateDuration = 20000;
                        config.Action = "忽略";
                    });
            }
            else if (_mismatchLoaded)
            {
                Snackbar.Add("工单号验证通过，所有工单号均有效", Severity.Success, config =>
                {
                    config.VisibleStateDuration = 3000;
                    config.Action = "忽略";
                });
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"工单号验证失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _mismatchLoaded = true;
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
        await PageState.SaveAsync("warehouseinventory", state);
    }
}
