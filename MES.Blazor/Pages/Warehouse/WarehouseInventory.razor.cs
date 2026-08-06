using System.Reflection;
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
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Rendering;
using MES.Core.Enums;
using MES.Core.Constants;
using MES.Core.Helpers;

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
    private bool _isArrowNavSetup;
    private int _pageSize = 10;
    private string _lastResolvedWarehouseCode = string.Empty;
    private List<WarehouseDto> warehouses = new();
    private List<NotificationDto>? _warehouseWorkOrderChangedNotices;

    // 当前仓库信息
    private string warehouseCode = string.Empty;
    private string warehouseName = string.Empty;
    private int warehouseId;

    // 出库模式
    private bool _outboundMode;

    // 关键字搜索
    private string _searchKeyword = string.Empty;

    // 日期搜索
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;

    // 排序状态
    private string sortColumn = "InboundDate";
    private bool sortDescending = true;

    // B33 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "InitialQuantity", "InitialWeight", "Meters", "RemainingMeters", "RemainingQuantity", "RemainingWeight"
    };

    // ========== ExcelFilter 状态 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // 列定义
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    // 多选
    private HashSet<InventoryBatchDto> _selectedItems = new();

    // ========== 待出库用料计划通知 ==========
    private List<PendingPlanBatchDto> _pendingPlanBatches = new();

    // ========== 列定义 ==========

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "BatchNo",             Label = "仓库批次", SortKey = "BatchNo", FilterType = "string", Width = "120" },
        new() { Key = "InboundDate",         Label = "入库日期", SortKey = "InboundDate", FilterType = "date", Width = "120" },
        new() { Key = "InboundSource",       Label = "来源",     SortKey = "InboundSource", FilterType = "string", Width = "120" },
        new() { Key = "SourceOrderNo",       Label = "来源单号", SortKey = "SourceOrderNo", FilterType = "string", Width = "120" },
        new() { Key = "MaterialType",        Label = "物料类型", SortKey = "MaterialType", FilterType = "string", Width = "120" },
        new() { Key = "SourceName",          Label = "来料单位", SortKey = "SourceName", FilterType = "string", Width = "120" },
        new() { Key = "ManufacturingStatus",    Label = "制造状态", SortKey = "ManufacturingStatus", FilterType = "enum", Width = "120",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>() },
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
        new() { Key = "RemainingMeters",     Label = "剩余米数", SortKey = "RemainingMeters", Width = "80" },
        new() { Key = "Remark",              Label = "备注", SortKey = "Remark", FilterType = "string", Width = "120" },
        new() { Key = "RemainingQuantity",   Label = "剩余支数", SortKey = "RemainingQuantity", Width = "80" },
        new() { Key = "RemainingWeight",     Label = "剩余重量", SortKey = "RemainingWeight", Width = "80" },
        new() { Key = "IsLinkedToWorkOrder", Label = "关联工单", SortKey = "IsLinkedToWorkOrder", FilterType = "boolean", Width = "120", BoolTrueLabel = "是", BoolFalseLabel = "否" },
        new() { Key = "WorkOrderNo",         Label = "工单号",   SortKey = "WorkOrderNo", FilterType = "string", Width = "120" },
        new() { Key = "SalesOrderNo",        Label = "订单号",   SortKey = "SalesOrderNo", FilterType = "string", Width = "120" },
        new() { Key = "OrderItemIds",        Label = "项次", SortKey = "OrderItemIds", FilterType = "string", Width = "120" },
        new() { Key = "ProductionBatchNo",   Label = "生产批号", SortKey = "ProductionBatchNo", FilterType = "string", Width = "120" },
        new() { Key = "ActualSpecification", Label = "实际规格", SortKey = "ActualSpecification", FilterType = "string", Width = "120" },
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
                SetNotApplicable(cols, "RemainingMeters");
                SetNotApplicable(cols, "ActualSpecification");
                SetNotApplicable(cols, "ProductionBatchNo");
                SetNotApplicable(cols, "DefectReason");
                SetNotApplicable(cols, "LiabilityType");
                SetNotApplicable(cols, "OriginalSupplier");
                SetNotApplicable(cols, "TagNo");
                SetNotApplicable(cols, "DefectRemark");
                SetNotApplicable(cols, "SalesOrderNo");
                SetNotApplicable(cols, "OrderItemIds");
                AssignGroups(cols, whCode);
                break;
            case "FG":
                SetNotApplicable(cols, "DefectReason");
                SetNotApplicable(cols, "LiabilityType");
                SetNotApplicable(cols, "OriginalSupplier");
                SetNotApplicable(cols, "TagNo");
                SetNotApplicable(cols, "DefectRemark");
                AssignGroups(cols, whCode);
                break;
            case "DEFECT":
                SetNotApplicable(cols, "Meters");
                SetNotApplicable(cols, "RemainingMeters");
                SetNotApplicable(cols, "ActualSpecification");
                AssignGroups(cols, whCode);
                break;
            case "WIP":
                SetNotApplicable(cols, "SourceName");
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
                SetNotApplicable(cols, "RemainingMeters");
                SetNotApplicable(cols, "SourceOrderNo");
                AssignGroups(cols, whCode);
                break;
            default:
                AssignGroups(cols, whCode);
                break;
        }
    }

    private static void AssignGroups(List<ColumnDef> cols, string whCode)
    {
        // G1 来源信息
        SetGroup(cols, "BatchNo", 1, "来源信息");
        SetGroup(cols, "InboundDate", 1, "来源信息");
        SetGroup(cols, "InboundSource", 1, "来源信息");
        SetGroup(cols, "SourceOrderNo", 1, "来源信息");
        SetGroup(cols, "ProductionBatchNo", 1, "来源信息");
        SetGroup(cols, "TagNo", 1, "来源信息");

        // G2 订单信息
        SetGroup(cols, "SalesOrderNo", 2, "订单信息");
        SetGroup(cols, "OrderItemIds", 2, "订单信息");
        SetGroup(cols, "WorkOrderNo", 2, "订单信息");
        SetGroup(cols, "IsLinkedToWorkOrder", 2, "订单信息");

        // G3 物料信息
        SetGroup(cols, "MaterialType", 3, "物料信息");
        SetGroup(cols, "PlantGrade", 3, "物料信息");
        SetGroup(cols, "Specification", 3, "物料信息");
        SetGroup(cols, "SourceName", 3, "物料信息");
        SetGroup(cols, "ManufacturingStatus", 3, "物料信息");
        SetGroup(cols, "HeatNo", 3, "物料信息");
        SetGroup(cols, "ActualSpecification", 3, "物料信息");

        // G4 长度信息
        SetGroup(cols, "LengthStatus", 4, "长度信息");
        SetGroup(cols, "MinLength", 4, "长度信息");
        SetGroup(cols, "MaxLength", 4, "长度信息");

        // G5 库存计量
        SetGroup(cols, "InitialQuantity", 5, "库存计量");
        SetGroup(cols, "InitialWeight", 5, "库存计量");
        SetGroup(cols, "UnitWeight", 5, "库存计量");
        SetGroup(cols, "Meters", 5, "库存计量");
        SetGroup(cols, "RemainingMeters", 5, "库存计量");
        SetGroup(cols, "RemainingQuantity", 5, "库存计量");
        SetGroup(cols, "RemainingWeight", 5, "库存计量");

        // G6 库位管理
        SetGroup(cols, "LocationArea", 6, "库位管理");
        SetGroup(cols, "LocationRack", 6, "库位管理");
        SetGroup(cols, "Remark", 6, "库位管理");

        // G7 次品信息（仅次品库可见）
        SetGroup(cols, "DefectReason", 7, "次品信息");
        SetGroup(cols, "LiabilityType", 7, "次品信息");
        SetGroup(cols, "OriginalSupplier", 7, "次品信息");
        SetGroup(cols, "DefectRemark", 7, "次品信息");

        SortByGroup(cols);
    }

    private static void SortByGroup(List<ColumnDef> cols)
    {
        var sorted = cols.OrderBy(c => c.GroupKey ?? int.MaxValue).ToList();
        cols.Clear();
        cols.AddRange(sorted);
    }

    private static void SetGroup(List<ColumnDef> cols, string key, int groupKey, string groupName)
    {
        var c = cols.FirstOrDefault(x => x.Key == key);
        if (c != null)
        {
            c.GroupKey = groupKey;
            c.GroupName = groupName;
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
                builder.AddContent(0, DisplayHelper.GetMaterialTypeText(item.MaterialType));
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
                builder.AddContent(0, DisplayHelper.GetLengthStatusText(item.LengthStatus?.ToString()));
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
                builder.AddContent(0, ((int)item.InitialWeight).ToString());
                break;
            case "Meters":
                if (item.Meters.HasValue)
                    builder.AddContent(0, ((int)item.Meters.Value).ToString());
                break;
            case "RemainingMeters":
                if (item.RemainingMeters.HasValue)
                    builder.AddContent(0, ((int)item.RemainingMeters.Value).ToString());
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
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, ((int)item.RemainingWeight).ToString())));
                builder.CloseComponent();
                break;
            case "UnitWeight":
                if (item.UnitWeight.HasValue)
                    builder.AddContent(0, item.UnitWeight.Value.ToString("G29"));
                break;
            case "ActualSpecification":
                builder.AddContent(0, item.ActualSpecification);
                break;
            case "ManufacturingStatus":
                builder.AddContent(0, item.ManufacturingStatusDisplay);
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
                if (!string.IsNullOrEmpty(item.LiabilityType))
                    builder.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.LiabilityTypeKey, item.LiabilityType) ?? item.LiabilityType);
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
            _pageSize = state.PageSize;
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "InboundDate";
            var filtersJson = SerializeFilters();

            // 恢复持久化的页码（MudTable 初始化时始终传 page=0）
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
            }

            var query = new InventoryQueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                SortBy = sortBy,
                IsDescending = sortDescending,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                WarehouseId = warehouseId,
                OnlyWithStock = true,
                InboundDateFrom = DateTime.TryParse(_dateFrom, out var df) ? df : null,
                InboundDateTo = DateTime.TryParse(_dateTo, out var dt) ? dt : null,
            };

            var result = await InventoryService.GetPagedAsync(query, filtersJson);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;

                // 如果持久化的页码超出实际数据页，回退到第1页
                if (_isFirstLoad && _pageItems.Count == 0 && _totalCount > 0 && _restoredPageIndex > 0)
                {
                    _restoredPageIndex = 0;
                    _currentPage = 1;
                    state.Page = 0;
                    query.PageIndex = 1;
                    var retryResult = await InventoryService.GetPagedAsync(query, filtersJson);
                    if (retryResult.Success && retryResult.Data != null)
                    {
                        _pageItems = retryResult.Data.Items;
                        _totalCount = retryResult.Data.TotalCount;
                    }
                }

                _isFirstLoad = false;
                ComputePageSums();

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

        // InboundSource 列显示中文
        if (_filterContextOptions.TryGetValue("InboundSource", out var sourceOptions))
        {
            foreach (var opt in sourceOptions)
            {
                opt.Display = DisplayHelper.GetInboundSourceText(opt.Value);
            }
        }

        // LengthStatus 列显示中文并过滤非法值
        if (_filterContextOptions.TryGetValue("LengthStatus", out var lengthOptions))
        {
            lengthOptions.RemoveAll(opt => !Enum.TryParse<LengthStatus>(opt.Value, out _));
            foreach (var opt in lengthOptions)
            {
                opt.Display = DisplayHelper.GetLengthStatusText(opt.Value);
            }
        }

        // ManufacturingStatus 列显示中文并过滤非法值
        if (_filterContextOptions.TryGetValue("ManufacturingStatus", out var surfaceOptions))
        {
            surfaceOptions.RemoveAll(opt => !Enum.TryParse<DeliveryState>(opt.Value, out _));
            foreach (var opt in surfaceOptions)
            {
                opt.Display = DisplayHelper.GetDeliveryStateText(opt.Value);
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

        // 按仓库代码过滤 MaterialType 筛选选项（仅显示该仓库允许的物料类型）
        if (!string.IsNullOrEmpty(warehouseCode) &&
            _filterContextOptions.TryGetValue("MaterialType", out var materialOptions))
        {
            var allowedTypes = MES.Core.Constants.InventoryMaterialTypes.GetAllowedTypes(warehouseCode);
            if (allowedTypes != null)
            {
                var allowedTypeNames = allowedTypes.Select(t => t.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                materialOptions.RemoveAll(opt => !allowedTypeNames.Contains(opt.Value));
            }
        }

        // MaterialType 筛选选项中文显示
        if (_filterContextOptions.TryGetValue("MaterialType", out var materialOptionsDisplay))
        {
            foreach (var opt in materialOptionsDisplay)
                opt.Display = DisplayHelper.GetMaterialTypeText(opt.Value);
        }

        // LiabilityType 列显示中文（配置表优先，兜底 LiabilityTypeKeys）
        if (_filterContextOptions.TryGetValue("LiabilityType", out var liabOptions))
        {
            foreach (var opt in liabOptions)
                opt.Display = DictValueDisplayHelper.GetText(DictValueDefaults.LiabilityTypeKey, opt.Value) ?? opt.Value;
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

    // ========== B23 分组列标题栏 ==========

    private int _totalTableWidth =>
        40 + _visibleColumns.Sum(c => GetColWidth(c.Key));

    private List<GroupHeaderInfo> _groupHeaders => GetGroupHeaders();

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
        int? lastKey = null; int totalWidth = 0;
        var groupKey = 0; var groupName = ""; var count = 0;
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
                totalWidth = 0; count = 0;
            }
            groupKey = gk; groupName = col.GroupName ?? "";
            totalWidth += GetColWidth(col.Key);
            count++; lastKey = gk;
        }
        if (count > 0)
            result.Add(new GroupHeaderInfo
            {
                GroupKey = groupKey,
                GroupName = groupName,
                TotalWidth = totalWidth,
                ColumnCount = count,
                CssClass = GetHeaderGroupCss(groupKey, true)
            });
        return result;
    }

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch { 1 => "col-g1", 2 => "col-g2", 3 => "col-g3", 4 => "col-g4", 5 => "col-g5", 6 => "col-g6", 7 => "col-g7", _ => "" };
        if (isGroupStart && groupKey > 1) cls += " col-group-start";
        return cls;
    }

    private static string GetCellGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch { 1 => "col-g1-cell", 2 => "col-g2-cell", 3 => "col-g3-cell", 4 => "col-g4-cell", 5 => "col-g5-cell", 6 => "col-g6-cell", 7 => "col-g7-cell", _ => "" };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    private static int GetColWidth(string key) => key switch
    {
        "BatchNo" => 120,
        "InboundDate" => 120,
        "InboundSource" => 120,
        "SourceOrderNo" => 120,
        "ProductionBatchNo" => 120,
        "SalesOrderNo" => 120,
        "OrderItemIds" => 120,
        "WorkOrderNo" => 120,
        "IsLinkedToWorkOrder" => 120,
        "MaterialType" => 120,
        "PlantGrade" => 120,
        "Specification" => 120,
        "SourceName" => 120,
        "ManufacturingStatus" => 120,
        "HeatNo" => 120,
        "ActualSpecification" => 120,
        "LengthStatus" => 120,
        "MinLength" => 80,
        "MaxLength" => 80,
        "InitialQuantity" => 80,
        "InitialWeight" => 80,
        "UnitWeight" => 80,
        "Meters" => 80,
        "RemainingMeters" => 80,
        "RemainingQuantity" => 80,
        "RemainingWeight" => 80,
        "LocationArea" => 120,
        "LocationRack" => 120,
        "Remark" => 120,
        "DefectReason" => 120,
        "LiabilityType" => 120,
        "OriginalSupplier" => 120,
        "TagNo" => 120,
        "DefectRemark" => 120,
        _ => 100
    };


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

    // ========== 日期搜索 ==========

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

    private bool _initialized;

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
                if (savedState.Extras?.ContainsKey("dateFrom") == true)
                    _dateFrom = savedState.Extras["dateFrom"] ?? string.Empty;
                if (savedState.Extras?.ContainsKey("dateTo") == true)
                    _dateTo = savedState.Extras["dateTo"] ?? string.Empty;
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

            // 注意：筛选上下文已在 ResolveWarehouse() 中加载
            await LoadPendingPlanBatches(); // 自动检查待出库用料计划
            await CheckWorkOrderChangedNotificationsAsync();
            _initialized = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 分组标题栏宽度同步
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#warehouse-inventory-table-wrapper");
        }
        catch { }

        // 方向键导航
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#warehouse-inventory-table-wrapper"))
                _isArrowNavSetup = false;
        }

        // 首次初始化完成后，table 引用已建立，重新加载数据确保 warehouseId 已正确设置
        if (_initialized && table != null)
        {
            _initialized = false;
            await table.ReloadServerData();
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(Code))
        {
            var prevCode = warehouseCode;
            await ResolveWarehouse();
            // 仅在仓库代码实际变更时重新加载数据
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

        Navigation.NavigateTo($"/warehouse/outbound/{warehouseCode.ToLowerInvariant()}");
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
            var request = new InventoryPrintSelectedRequest
            {
                Ids = ids,
                Columns = columns
            };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/inventory/print-stock-selected-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    private async Task PrintAll()
    {
        try
        {
            var columns = _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();
            var request = new InventoryPrintAllRequest
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortColumn,
                IsDescending = sortDescending,
                WarehouseId = warehouseId,
                OnlyWithStock = true,
                InboundDateFrom = DateTime.TryParse(_dateFrom, out var df) ? df : null,
                InboundDateTo = DateTime.TryParse(_dateTo, out var dt) ? dt : null,
                Columns = columns
            };
            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/inventory/print-stock-all-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex) { Snackbar.Add($"打印失败: {ex.Message}", Severity.Error); }
    }

    // ========== 导航 ==========

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

    // ========== 待出库用料计划通知（页面加载时执行） ==========

    private async Task LoadPendingPlanBatches()
    {
        if (warehouseId <= 0) return;

        try
        {
            var result = await MaterialPlanService.GetPendingPlanBatchesAsync(warehouseId);
            if (result.Success && result.Data != null)
            {
                _pendingPlanBatches = result.Data;
            }
            else
            {
                _pendingPlanBatches.Clear();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"查询待出库计划失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 工单内容变更通知（按仓库过滤） ==========

    /// <summary>从通知标题中提取工单号，格式："工单 {workOrderNo} 内容已变更"</summary>
    private static string? ExtractWorkOrderNoFromTitle(string? title)
    {
        if (string.IsNullOrEmpty(title)) return null;
        const string prefix = "工单 ";
        const string suffix = " 内容已变更";
        if (title.StartsWith(prefix) && title.EndsWith(suffix))
            return title[prefix.Length..^suffix.Length];
        return null;
    }

    private async Task CheckWorkOrderChangedNotificationsAsync()
    {
        if (warehouseId <= 0) return; // 只在详情页执行

        try
        {
            // 1. 获取所有 WorkOrderChanged 通知
            var result = await NotificationService.GetByTypeAsync("WorkOrderChanged");
            if (!result.Success || result.Data is not { Count: > 0 })
            {
                _warehouseWorkOrderChangedNotices = null;
                return;
            }

            // 2. 获取当前仓库关联的所有工单号
            var woNosResult = await InventoryService.GetWorkOrderNosByWarehouseAsync(warehouseId);
            if (!woNosResult.Success || woNosResult.Data is not { Count: > 0 })
            {
                _warehouseWorkOrderChangedNotices = null;
                return;
            }
            var warehouseWoNos = woNosResult.Data.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 3. 交叉过滤：只保留当前仓库有关联工单的通知
            _warehouseWorkOrderChangedNotices = result.Data
                .Where(n => ExtractWorkOrderNoFromTitle(n.Title) is string woNo && warehouseWoNos.Contains(woNo))
                .ToList();

            if (_warehouseWorkOrderChangedNotices.Count == 0)
                _warehouseWorkOrderChangedNotices = null;
        }
        catch
        {
            _warehouseWorkOrderChangedNotices = null;
        }
    }

    private async Task DismissWorkOrderChangedNotices()
    {
        if (_warehouseWorkOrderChangedNotices is not { Count: > 0 }) return;

        foreach (var notice in _warehouseWorkOrderChangedNotices)
            await NotificationService.MarkAsReadAsync(notice.Id);

        _warehouseWorkOrderChangedNotices = null;
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(_dateFrom)) extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrEmpty(_dateTo)) extras["dateTo"] = _dateTo;
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = Math.Max(0, _currentPage - 1), // 保存 0-indexed，与 MudTable state.Page 对齐
            Extras = extras
        };
        await PageState.SaveAsync("warehouseinventory", state);
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
