using System.Text.Json;
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

namespace MES.Blazor.Pages.Quality;

public partial class QualityProcessTracking
{
    private MudTable<QualityProcessTrackingDto>? table;
    private List<QualityProcessTrackingDto> _pageItems = new();
    private int _totalCount;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    private string sortColumn = "receivedate";
    private bool sortDescending = true;

    // 选中状态（打印选中用）
    private HashSet<int> _selectedIds = new();

    // ExcelFilter 筛选
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // 列定义
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    // ========== 列定义 ==========

    private static List<ColumnDef> GetAllColumnDefs()
    {
        // G1: 批次信息
        var g1 = new List<ColumnDef>
        {
            new() { Key = "BatchNo",              Label = "生产编号",       SortKey = "batchno",              FilterType = "string",  Width = "120", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "ManufacturingItem",     Label = "制造物品",       SortKey = "manufacturingitem",    FilterType = "enum",    Width = "120", GroupKey = 1, GroupName = "批次信息",
                EnumOptions = new() { new("OrderFinishedProduct","订单成品"), new("PreparedMaterial","备料成品"), new("SurplusStock","余库料"), new("IntermediateProduct","中间品"), new("SpecialDeliveryStatus","特定交态成品") } },
            new() { Key = "PlantGrade",            Label = "工厂牌号",       SortKey = "plantgrade",            FilterType = "string",  Width = "120", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "Specification",         Label = "规格",           SortKey = "specification",         FilterType = "string",  Width = "120", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "LengthStatus",          Label = "长度状态",       SortKey = "lengthstatus",          FilterType = "enum",    Width = "100", GroupKey = 1, GroupName = "批次信息",
                EnumOptions = new() { new("Fixed","定尺"), new("Range","范围尺"), new("NonFixed","非定尺") } },
            new() { Key = "TagNo",                 Label = "挂牌号",         SortKey = "tagno",                 FilterType = "string",  Width = "120", GroupKey = 1, GroupName = "批次信息", Visible = false },
            new() { Key = "WorkOrderNo",           Label = "工单号",         SortKey = "workorderno",           FilterType = "string",  Width = "120", GroupKey = 1, GroupName = "批次信息", Visible = false },
            new() { Key = "SalesOrderNo",          Label = "订单号",         SortKey = "salesorderno",          FilterType = "string",  Width = "120", GroupKey = 1, GroupName = "批次信息", Visible = false },
            new() { Key = "FurnaceNo",             Label = "炉号",           SortKey = "furnaceno",             FilterType = "string",  Width = "120", GroupKey = 1, GroupName = "批次信息", Visible = false },
            new() { Key = "SourceUnit",            Label = "来料单位",       SortKey = "sourceunit",            FilterType = "string",  Width = "120", GroupKey = 1, GroupName = "批次信息", Visible = false },
            new() { Key = "ProductionType",        Label = "生产类型",       SortKey = "productiontype",        FilterType = "enum",    Width = "120", GroupKey = 1, GroupName = "批次信息", Visible = false,
                EnumOptions = new() { new("RoughTube","荒管生产"), new("InProcess","在制生产"), new("Inventory","库存"), new("OutsourcedPurchased","外购"), new("Rework","返整"), new("Subcontract","委外生产"), new("ExternalProcessing","对外加工") } },
            new() { Key = "Salesman",              Label = "业务员",         SortKey = "salesman",              FilterType = "string",  Width = "100", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "DeliveryState",         Label = "交货状态",       SortKey = "deliverystate",         FilterType = "enum",    Width = "120", GroupKey = 1, GroupName = "批次信息",
                EnumOptions = new() { new("SolutionAnnealedAndPickled","固溶酸洗"), new("SolutionAnnealedAndPickledUTube","固溶酸洗-U型管"), new("SolutionAnnealedAndPickledExternalPolished","固溶酸洗-外抛光"), new("SolutionAnnealedAndPickledInternalPolished","固溶酸洗-内抛光"), new("SolutionAnnealedAndPickledBothPolished","固溶酸洗-内外抛光"), new("SolutionAnnealedAndPickledCoiled","固溶酸洗-盘管"), new("Bright","光亮"), new("BrightUTube","光亮-U型管"), new("BrightCoiled","光亮-盘管"), new("Hard","硬态") } },
            new() { Key = "ProductionWeight",      Label = "生产重量(kg)",  SortKey = "productionweight",      FilterType = "number",  Width = "80",  GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "ProductionCutQuantity", Label = "生产支数",       SortKey = "productioncutquantity", FilterType = "number",  Width = "80",  GroupKey = 1, GroupName = "批次信息" },
        };

        // G2: 检验来料
        var g2 = new List<ColumnDef>
        {
            new() { Key = "ReceiveDate",           Label = "到料日期",       SortKey = "receivedate",           FilterType = "date",    Width = "120", GroupKey = 2, GroupName = "检验来料" },
            new() { Key = "Shift",                 Label = "班次",           SortKey = "shift",                 FilterType = "string",  Width = "120", GroupKey = 2, GroupName = "检验来料" },
            new() { Key = "Checker",               Label = "确认人",         SortKey = "checker",               FilterType = "string",  Width = "120", GroupKey = 2, GroupName = "检验来料" },
            new() { Key = "IsForceCompleted",      Label = "强制完成",       SortKey = "isforcecompleted",      FilterType = "boolean", Width = "100", GroupKey = 2, GroupName = "检验来料", BoolTrueLabel = "是", BoolFalseLabel = "否" },
        };

        // G3: 各项检验的日期
        var g3 = new List<ColumnDef>
        {
            new() { Key = "InspectionCount",              Label = "检测项数",      SortKey = "inspectioncount",         FilterType = "number", Width = "80",  GroupKey = 3, GroupName = "各项检验的日期" },
            new() { Key = "PmiDate",                      Label = "PMI检验",       SortKey = "pmidate",                 FilterType = "date",   Width = "120", GroupKey = 3, GroupName = "各项检验的日期" },
            new() { Key = "VisualDate",                   Label = "表检",          SortKey = "visualdate",              FilterType = "date",   Width = "120", GroupKey = 3, GroupName = "各项检验的日期" },
            new() { Key = "DimensionDate",                Label = "尺寸",          SortKey = "dimensiondate",           FilterType = "date",   Width = "120", GroupKey = 3, GroupName = "各项检验的日期" },
            new() { Key = "EndoscopyDate",                Label = "内窥",          SortKey = "endoscopydate",           FilterType = "date",   Width = "120", GroupKey = 3, GroupName = "各项检验的日期" },
            new() { Key = "HydroDate",                    Label = "水压",          SortKey = "hydrodate",               FilterType = "date",   Width = "120", GroupKey = 3, GroupName = "各项检验的日期" },
            new() { Key = "UnderwaterPneumaticDate",      Label = "水下气压",      SortKey = "underwaterpneumaticdate", FilterType = "date",   Width = "120", GroupKey = 3, GroupName = "各项检验的日期" },
            new() { Key = "EddyCurrentDate",               Label = "涡流",          SortKey = "eddycurrentdate",         FilterType = "date",   Width = "120", GroupKey = 3, GroupName = "各项检验的日期" },
            new() { Key = "UltrasonicDate",                Label = "超声波",        SortKey = "ultrasonicdate",          FilterType = "date",   Width = "120", GroupKey = 3, GroupName = "各项检验的日期" },
            new() { Key = "PortColoringDate",              Label = "端口着色",      SortKey = "portcoloringdate",        FilterType = "date",   Width = "120", GroupKey = 3, GroupName = "各项检验的日期" },
        };

        // G4: 检验的数量信息
        var g4 = new List<ColumnDef>
        {
            new() { Key = "TotalQuantity",              Label = "检验支数",       SortKey = "totalquantity",           FilterType = "number", Width = "80", GroupKey = 4, GroupName = "检验的数量信息" },
            new() { Key = "QualifiedQuantity",           Label = "合格支数",       SortKey = "qualifiedquantity",       FilterType = "number", Width = "80", GroupKey = 4, GroupName = "检验的数量信息" },
            new() { Key = "DefectReworkQuantity",        Label = "返整支数",       SortKey = "defectreworkquantity",    FilterType = "number", Width = "80", GroupKey = 4, GroupName = "检验的数量信息" },
            new() { Key = "DefectWarehouseQuantity",     Label = "不合格入库",     SortKey = "defectwarehousequantity", FilterType = "number", Width = "80", GroupKey = 4, GroupName = "检验的数量信息" },
            new() { Key = "DefectScrapQuantity",         Label = "报废支数",       SortKey = "defectscrapquantity",     FilterType = "number", Width = "80", GroupKey = 4, GroupName = "检验的数量信息" },
        };

        // G5: 入库的信息
        var g5 = new List<ColumnDef>
        {
            new() { Key = "InboundDate",         Label = "入库日期",    SortKey = "inbounddate",       FilterType = "date",   Width = "120", GroupKey = 5, GroupName = "入库的信息" },
            new() { Key = "InboundQuantity",     Label = "入库支数",    SortKey = "inboundquantity",   FilterType = "number", Width = "80",  GroupKey = 5, GroupName = "入库的信息" },
            new() { Key = "InboundWeight",       Label = "入库重量",    SortKey = "inboundweight",     FilterType = "number", Width = "80",  GroupKey = 5, GroupName = "入库的信息" },
        };

        // G6: 执行状态
        var g6 = new List<ColumnDef>
        {
            new() { Key = "QualityStatus", Label = "执行状态", SortKey = "qualitystatus", FilterType = "enum", Width = "120", GroupKey = 6, GroupName = "执行状态",
                EnumOptions = new() { new(){ Value = "待检验", Display = "待检验" }, new(){ Value = "检验中", Display = "检验中" }, new(){ Value = "完成检验", Display = "完成检验" }, new(){ Value = "异常完成", Display = "异常完成" } } },
        };

        var all = new List<ColumnDef>();
        all.AddRange(g1);
        all.AddRange(g2);
        all.AddRange(g3);
        all.AddRange(g4);
        all.AddRange(g5);
        all.AddRange(g6);
        return all;
    }

    // ========== B33: 分页汇总 ==========
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "InspectionCount", "TotalQuantity", "QualifiedQuantity",
        "DefectReworkQuantity", "DefectWarehouseQuantity", "DefectScrapQuantity",
        "ProductionCutQuantity", "ProductionWeight",
        "InboundQuantity", "InboundWeight"
    };

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(QualityProcessTrackingDto)
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
                else if (type == typeof(decimal) || type == typeof(decimal?))
                {
                    var sum = _pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
                else if (type == typeof(int?))
                {
                    var sum = _pageItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[col.Key] = sum.ToString();
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

    // ========== 服务端数据加载 ==========

    private async Task<TableData<QualityProcessTrackingDto>> LoadDataFromServer(TableState state)
    {
        try
        {
            _pageSize = state.PageSize;
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "receivedate";
            var filtersJson = SerializeFilters();

            // 首次加载覆盖页码（MudTable 初始化时始终传 page=0）
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending
            };
            if (filtersJson != null)
                query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson);

            var result = await TrackingService.GetPagedAsync(query);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;
                ComputePageSums();
            }
            else
            {
                Snackbar.Add(result.Message ?? "数据加载失败", Severity.Warning);
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

        return new TableData<QualityProcessTrackingDto>
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

    // ========== 筛选上下文 ==========

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await TrackingService.GetFilterContextsAsync();
            if (result.Success && result.Data != null)
                BuildFilterContextOptions(result.Data);
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

        // 补充枚举/布尔列
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

    // ========== 事件处理 ==========

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task ToggleSort(string key)
    {
        if (sortColumn == key)
            sortDescending = !sortDescending;
        else
        {
            sortColumn = key;
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

    private async Task OnColumnToggle(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnUp(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnDown(ColumnDef col) => await SaveColumnPrefs();

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("quality-process-tracking", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 分组标题栏 ==========

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
        return result;
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();

        var savedState = await PageState.LoadAsync("qualityprocesstracking");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "receivedate";
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

        if (savedState != null && table != null)
            await table.ReloadServerData();

        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 分组标题栏对齐
        await JS.InvokeAsync<object>("initGroupHeaders", new object[] { "#quality-process-tracking-list-table" });

        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", new object[] { "#quality-process-tracking-table" }))
                _isArrowNavSetup = false;
        }
    }

    // ========== 列分组样式 ==========

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1", 2 => "col-g2", 3 => "col-g3",
            4 => "col-g4", 5 => "col-g5", 6 => "col-g6",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start";
        return cls;
    }

    private static string GetCellGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1-cell", 2 => "col-g2-cell", 3 => "col-g3-cell",
            4 => "col-g4-cell", 5 => "col-g5-cell", 6 => "col-g6-cell",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(QualityProcessTrackingDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            // === G1: 文本字段 ===
            case "BatchNo":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => Navigation.NavigateTo($"/batches/{item.ProductionBatchId}")));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.BatchNo)));
                builder.CloseComponent();
                break;

            case "ManufacturingItem":
                builder.AddContent(0, DisplayHelper.GetManufacturingItemText(item.ManufacturingItem));
                break;

            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade);
                break;

            case "Specification":
                builder.AddContent(0, item.Specification);
                break;

            case "Shift":
                builder.AddContent(0, item.Shift);
                break;

            case "Checker":
                builder.AddContent(0, item.Checker);
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

            case "FurnaceNo":
                builder.AddContent(0, item.FurnaceNo);
                break;

            case "SourceUnit":
                builder.AddContent(0, item.SourceUnit);
                break;

            case "ProductionType":
                builder.AddContent(0, DisplayHelper.GetProductionTypeText(item.ProductionType));
                break;

            case "Salesman":
                builder.AddContent(0, item.Salesman);
                break;

            case "DeliveryState":
                builder.AddContent(0, DisplayHelper.GetDeliveryStateText(item.DeliveryState));
                break;

            case "LengthStatus":
                builder.AddContent(0, DisplayHelper.GetLengthStatusText(item.LengthStatus));
                break;

            case "ProductionWeight":
                builder.AddContent(0, item.ProductionWeight.HasValue ? ((int)item.ProductionWeight.Value).ToString() : "-");
                break;

            case "ProductionCutQuantity":
                builder.AddContent(0, item.ProductionCutQuantity);
                break;

            case "ReceiveDate":
                builder.AddContent(0, item.ReceiveDate.ToString("yyyy-MM-dd"));
                break;

            // === G2: 检验来料 ===
            case "IsForceCompleted":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", item.IsForceCompleted ? Color.Error : Color.Default);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.IsForceCompleted ? "是" : "否")));
                builder.CloseComponent();
                break;

            // === G2: 日期字段 ===
            case "PmiDate":
            case "VisualDate":
            case "DimensionDate":
            case "EndoscopyDate":
            case "HydroDate":
            case "UnderwaterPneumaticDate":
            case "EddyCurrentDate":
            case "UltrasonicDate":
            case "PortColoringDate":
            case "MaxInspectionDate":
                {
                    var val = typeof(QualityProcessTrackingDto).GetProperty(col.Key)?.GetValue(item) as DateTime?;
                    builder.AddContent(0, val?.ToString("yyyy-MM-dd") ?? "-");
                }
                break;

            // === G3: 数字字段 ===
            case "InspectionCount":
            case "TotalQuantity":
            case "QualifiedQuantity":
            case "DefectReworkQuantity":
            case "DefectWarehouseQuantity":
            case "DefectScrapQuantity":
                {
                    var val = (int)(typeof(QualityProcessTrackingDto).GetProperty(col.Key)?.GetValue(item) ?? 0);
                    builder.AddContent(0, DisplayHelper.FormatNullableInt(val));
                }
                break;

            // === G4: 入库 ===
            case "InboundQuantity":
                builder.AddContent(0, DisplayHelper.FormatNullableInt(item.InboundQuantity));
                break;

            case "InboundWeight":
                builder.AddContent(0, DisplayHelper.FormatNullableDecimal(item.InboundWeight));
                break;

            // === G5: 执行状态 ===
            case "QualityStatus":
                {
                    var displayText = item.IsForceCompleted ? "异常完成" : item.QualityStatus;
                    var color = item.IsForceCompleted ? Color.Error :
                        item.QualityStatus switch
                        {
                            "完成检验" => Color.Success,
                            "检验中" => Color.Warning,
                            _ => Color.Default
                        };
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", color);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, displayText)));
                    builder.CloseComponent();
                }
                break;

            default:
                builder.AddContent(0, "");
                break;
        }
    };

    // ========== 打印 ==========

    private async Task PrintSelected()
    {
        if (!_selectedIds.Any()) return;
        var items = _pageItems.Where(i => _selectedIds.Contains(i.Id)).ToList();
        var html = BuildPrintHtml(items);
        await JS.InvokeVoidAsync("printRawHtml", html, "成检追踪（选中记录）");
    }

    private async Task PrintAll()
    {
        var html = BuildPrintHtml(_pageItems);
        await JS.InvokeVoidAsync("printRawHtml", html, "成检追踪");
    }

    private string BuildPrintHtml(IEnumerable<QualityProcessTrackingDto> items)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<table border='1' cellpadding='4' cellspacing='0' style='border-collapse:collapse;width:100%;font-size:12px;'>");
        sb.Append("<thead><tr>");
        foreach (var col in _visibleColumns)
            sb.Append($"<th style='background:#f0f0f0;font-weight:bold;'>{System.Net.WebUtility.HtmlEncode(col.Label)}</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var item in items)
        {
            sb.Append("<tr>");
            foreach (var col in _visibleColumns)
                sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(GetCellPrintValue(item, col))}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");
        return sb.ToString();
    }

    private string GetCellPrintValue(QualityProcessTrackingDto item, ColumnDef col) => col.Key switch
    {
        "BatchNo" => item.BatchNo ?? "",
        "ManufacturingItem" => DisplayHelper.GetManufacturingItemText(item.ManufacturingItem),
        "PlantGrade" => item.PlantGrade ?? "",
        "Specification" => item.Specification ?? "",
        "LengthStatus" => DisplayHelper.GetLengthStatusText(item.LengthStatus),
        "TagNo" => item.TagNo ?? "",
        "WorkOrderNo" => item.WorkOrderNo ?? "",
        "SalesOrderNo" => item.SalesOrderNo ?? "",
        "FurnaceNo" => item.FurnaceNo ?? "",
        "SourceUnit" => item.SourceUnit ?? "",
        "ProductionType" => DisplayHelper.GetProductionTypeText(item.ProductionType),
        "Salesman" => item.Salesman ?? "",
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState),
        "ProductionWeight" => item.ProductionWeight?.ToString("G29") ?? "",
        "ProductionCutQuantity" => item.ProductionCutQuantity.ToString(),
        "ReceiveDate" => item.ReceiveDate.ToString("yyyy-MM-dd"),
        "Shift" => item.Shift ?? "",
        "Checker" => item.Checker ?? "",
        "IsForceCompleted" => item.IsForceCompleted ? "是" : "否",
        "InspectionCount" => item.InspectionCount.ToString(),
        "PmiDate" => item.PmiDate?.ToString("yyyy-MM-dd") ?? "",
        "VisualDate" => item.VisualDate?.ToString("yyyy-MM-dd") ?? "",
        "DimensionDate" => item.DimensionDate?.ToString("yyyy-MM-dd") ?? "",
        "EndoscopyDate" => item.EndoscopyDate?.ToString("yyyy-MM-dd") ?? "",
        "HydroDate" => item.HydroDate?.ToString("yyyy-MM-dd") ?? "",
        "UnderwaterPneumaticDate" => item.UnderwaterPneumaticDate?.ToString("yyyy-MM-dd") ?? "",
        "EddyCurrentDate" => item.EddyCurrentDate?.ToString("yyyy-MM-dd") ?? "",
        "UltrasonicDate" => item.UltrasonicDate?.ToString("yyyy-MM-dd") ?? "",
        "PortColoringDate" => item.PortColoringDate?.ToString("yyyy-MM-dd") ?? "",
        "TotalQuantity" => item.TotalQuantity.ToString(),
        "QualifiedQuantity" => item.QualifiedQuantity.ToString(),
        "DefectReworkQuantity" => item.DefectReworkQuantity.ToString(),
        "DefectWarehouseQuantity" => item.DefectWarehouseQuantity.ToString(),
        "DefectScrapQuantity" => item.DefectScrapQuantity.ToString(),
        "InboundDate" => item.InboundDate?.ToString("yyyy-MM-dd") ?? "",
        "InboundQuantity" => item.InboundQuantity.ToString(),
        "InboundWeight" => item.InboundWeight?.ToString("G29") ?? "",
        "QualityStatus" => item.IsForceCompleted ? "异常完成" : item.QualityStatus ?? "",
        _ => ""
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
        await PageState.SaveAsync("qualityprocesstracking", state);
    }
}
