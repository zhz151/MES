using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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

    // ExcelFilter 筛选
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // 列定义
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    // ========== 列定义 ==========

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // G1: 批次信息
        new() { Key = "BatchNo",           Label = "批次号",      SortKey = "batchno",       FilterType = "string",  GroupKey = 1, GroupName = "批次信息" },
        new() { Key = "ManufacturingItem",  Label = "制造物品",    SortKey = "manufacturingitem", FilterType = "string",  GroupKey = 1 },
        new() { Key = "PlantGrade",         Label = "钢种",        SortKey = "plantgrade",     FilterType = "string",  GroupKey = 1 },
        new() { Key = "Specification",      Label = "规格",        SortKey = "specification",  FilterType = "string",  GroupKey = 1 },
        new() { Key = "ReceiveDate",        Label = "到料日期",    SortKey = "receivedate",    FilterType = "date",    GroupKey = 1 },
        new() { Key = "Shift",              Label = "班次",        SortKey = "shift",          FilterType = "string",  GroupKey = 1 },
        new() { Key = "Checker",            Label = "确认人",      SortKey = "checker",        FilterType = "string",  GroupKey = 1 },
        new() { Key = "TagNo",              Label = "挂牌号",      SortKey = "tagno",          FilterType = "string",  GroupKey = 1, Visible = false },
        new() { Key = "WorkOrderNo",        Label = "工单号",      SortKey = "workorderno",    FilterType = "string",  GroupKey = 1, Visible = false },
        new() { Key = "SalesOrderNo",       Label = "订单号",      SortKey = "salesorderno",   FilterType = "string",  GroupKey = 1, Visible = false },
        new() { Key = "FurnaceNo",          Label = "炉号",        SortKey = "furnaceno",      FilterType = "string",  GroupKey = 1, Visible = false },
        new() { Key = "SourceUnit",         Label = "来料单位",    SortKey = "sourceunit",     FilterType = "string",  GroupKey = 1, Visible = false },
        new() { Key = "ProductionType",     Label = "生产类型",    SortKey = "productiontype", FilterType = "string",  GroupKey = 1, Visible = false },

        // G2: 检验日期
        new() { Key = "PmiDate",                    Label = "PMI检验",       SortKey = "pmidate",                FilterType = "date",   GroupKey = 2, GroupName = "检验日期" },
        new() { Key = "VisualDate",                 Label = "表检",          SortKey = "visualdate",             FilterType = "date",   GroupKey = 2 },
        new() { Key = "DimensionDate",              Label = "尺寸",          SortKey = "dimensiondate",          FilterType = "date",   GroupKey = 2 },
        new() { Key = "EndoscopyDate",              Label = "内窥",          SortKey = "endoscopydate",          FilterType = "date",   GroupKey = 2 },
        new() { Key = "HydroDate",                  Label = "水压",          SortKey = "hydrodate",              FilterType = "date",   GroupKey = 2 },
        new() { Key = "UnderwaterPneumaticDate",    Label = "水下气压",      SortKey = "underwaterpneumaticdate",FilterType = "date",   GroupKey = 2 },
        new() { Key = "EddyCurrentDate",             Label = "涡流",          SortKey = "eddycurrentdate",         FilterType = "date",   GroupKey = 2 },
        new() { Key = "UltrasonicDate",              Label = "超声波",        SortKey = "ultrasonicdate",          FilterType = "date",   GroupKey = 2 },
        new() { Key = "PortColoringDate",            Label = "端口着色",      SortKey = "portcoloringdate",        FilterType = "date",   GroupKey = 2 },
        new() { Key = "InspectionCount",             Label = "检测项数",      SortKey = "inspectioncount",         FilterType = "number", GroupKey = 2 },

        // G3: 检验汇总
        new() { Key = "TotalQuantity",              Label = "检验支数",      SortKey = "totalquantity",           FilterType = "number", GroupKey = 3, GroupName = "检验汇总" },
        new() { Key = "QualifiedQuantity",           Label = "合格支数",      SortKey = "qualifiedquantity",       FilterType = "number", GroupKey = 3 },
        new() { Key = "DefectReworkQuantity",        Label = "返整支数",      SortKey = "defectreworkquantity",    FilterType = "number", GroupKey = 3 },
        new() { Key = "DefectWarehouseQuantity",     Label = "不合格入库",    SortKey = "defectwarehousequantity", FilterType = "number", GroupKey = 3 },
        new() { Key = "DefectScrapQuantity",         Label = "报废支数",      SortKey = "defectscrapquantity",     FilterType = "number", GroupKey = 3 },
        new() { Key = "ProductionCutQuantity",       Label = "生产支数",      SortKey = "productioncutquantity",   FilterType = "number", GroupKey = 3 },

        // G4: 成品入库
        new() { Key = "InboundQuantity",     Label = "入库支数",    SortKey = "inboundquantity",   FilterType = "number", GroupKey = 4, GroupName = "成品入库" },
        new() { Key = "InboundWeight",       Label = "入库重量",    SortKey = "inboundweight",     FilterType = "number", GroupKey = 4 },
        new() { Key = "InboundDate",         Label = "入库日期",    SortKey = "inbounddate",       FilterType = "date",   GroupKey = 4 },

        // G5: 执行状态
        new() { Key = "QualityStatus", Label = "执行状态", SortKey = "qualitystatus", FilterType = "string", GroupKey = 5, GroupName = "执行状态",
            EnumOptions = new() { new(){ Value = "待检验", Display = "待检验" }, new(){ Value = "检验中", Display = "检验中" }, new(){ Value = "完成检验", Display = "完成检验" } } },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<QualityProcessTrackingDto>> LoadDataFromServer(TableState state)
    {
        try
        {
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
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", new object[] { "#quality-process-tracking-table" }))
                _isArrowNavSetup = false;
        }
    }

    // ========== 列分组样式 ==========

    private static string GetHeaderGroupCss(int groupKey, bool isGroupStart)
    {
        return groupKey switch
        {
            1 => "col-g1",
            2 => "col-g2",
            3 => "col-g3",
            4 => "col-g4",
            5 => "col-g5",
            _ => ""
        };
    }

    private static string GetCellGroupCss(int groupKey, bool isGroupStart)
    {
        return groupKey switch
        {
            1 => "col-g1-cell",
            2 => "col-g2-cell",
            3 => "col-g3-cell",
            4 => "col-g4-cell",
            5 => "col-g5-cell",
            _ => ""
        };
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
                builder.AddContent(0, item.ManufacturingItem);
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
                builder.AddContent(0, item.ProductionType);
                break;

            case "ProductionCutQuantity":
                builder.AddContent(0, item.ProductionCutQuantity);
                break;

            case "ReceiveDate":
                builder.AddContent(0, item.ReceiveDate.ToString("yyyy-MM-dd"));
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
            case "InboundDate":
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
                    var color = item.QualityStatus switch
                    {
                        "完成检验" => Color.Success,
                        "检验中" => Color.Warning,
                        _ => Color.Default
                    };
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", color);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.QualityStatus)));
                    builder.CloseComponent();
                }
                break;

            default:
                builder.AddContent(0, "");
                break;
        }
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
