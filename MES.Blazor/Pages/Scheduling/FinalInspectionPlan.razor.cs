using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Core.Enums;
using MES.Core.Constants;
using MES.Core.Helpers;
using MES.Blazor.Services;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.Scheduling;

public partial class FinalInspectionPlan
{
    private MudTable<FinalInspectionPlanDto>? table;
    private List<FinalInspectionPlanDto> _allItems = new();
    private List<FinalInspectionPlanDto> _filteredItems = new();
    private List<FinalInspectionPlanDto> _filteredAllItems = new();
    private int _totalCount;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    // 排序
    private string sortColumn = "BatchNo";
    private bool sortDescending;

    // 四档 Tab
    private string? _selectedTab = "待到料";
    private static readonly string[] _tabs = { "全部", "待到料", "待检验", "检验中", "完成检验待入库" };

    // Tab 汇总
    private int _tabCount;
    private decimal _tabTotalWeight;
    private int _urgentAPlusCount;
    private decimal _urgentAPlusWeight;
    private int _urgentACount;
    private decimal _urgentAWeight;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "ProductionCutQuantity",
        "ProductionWeight",
        "InspectionCount",
        "TotalQuantity",
        "QualifiedQuantity",
        "DefectReworkQuantity",
        "DefectWarehouseQuantity",
        "DefectScrapQuantity"
    };
    private int _lastSummedPage = -1;
    private int _lastSummedCount = -1;
    private int _lastSummedPageSize = -1;

    // 选中行
    private HashSet<FinalInspectionPlanDto> _selectedItems = new();

    // ========== 显示类汇总卡片（仿批次计划，工具栏「显示类汇总」按钮切换显隐，懒加载） ==========
    private bool _showSummaryCard;
    private bool _isLoadingSummary;
    private List<FinalInspectionPlanSummaryRowDto> _summaryRows = new();

    private void SelectAllItems(bool selected)
    {
        if (selected)
            _selectedItems = new HashSet<FinalInspectionPlanDto>(_filteredAllItems);
        else
            _selectedItems.Clear();
    }

    private void ToggleSelection(FinalInspectionPlanDto item, bool selected)
    {
        if (selected)
            _selectedItems.Add(item);
        else
            _selectedItems.Remove(item);
    }

    // ========== 列定义 ==========

    private static List<ColumnDef> GetAllColumnDefs()
    {
        // G1: 批次信息 + 关联工单（合并，参照成检追踪；成检类型取批次「成检附加」，预成检时是否交付态/制造状态显 "-"）
        // 列顺序按业务约定：生产编号/成检类型/是否交付态/生产类型/制造物品/制造状态/交货状态/工厂牌号/规格/长度状态/生产支数/生产重量/炉号/来料单位/工单号/订单号/主号/业务员/最终用户
        var g1 = new List<ColumnDef>
        {
            new() { Key = "BatchNo",              Label = "生产编号",   SortKey = "BatchNo",              FilterType = "string", Width = "130", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "InspectionType",       Label = "成检类型",   SortKey = "InspectionType",       FilterType = "enum",   Width = "100", EnumOptions = DisplayHelper.GetEnumFilterOptions<InspectionType>(), GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "IsDeliveryStatus",     Label = "是否交付态", SortKey = "IsDeliveryStatus",     FilterType = "string", Width = "90",  GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "ProductionType",       Label = "生产类型",   SortKey = "ProductionType",       FilterType = "enum",   Width = "100", EnumOptions = DisplayHelper.GetEnumFilterOptions<ProductionType>(), DisplayConverter = v => v is ProductionType pt ? DisplayHelper.GetProductionTypeText(pt) : DisplayHelper.GetProductionTypeText(v as string), GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "ManufacturingItem",    Label = "制造物品",   SortKey = "ManufacturingItem",    FilterType = "enum",   Width = "90",  EnumOptions = DisplayHelper.GetEnumFilterOptions<MaterialType>(), GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "ManufacturingStatus",  Label = "制造状态",   SortKey = "ManufacturingStatus",  FilterType = "enum",   Width = "110", EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>(), GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "DeliveryState",        Label = "交货状态",   SortKey = "DeliveryState",        FilterType = "enum",   Width = "100", EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>(), GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "PlantGrade",           Label = "工厂牌号",   SortKey = "PlantGrade",           FilterType = "string", Width = "100", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "Specification",        Label = "规格",       SortKey = "Specification",        FilterType = "string", Width = "110", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "LengthStatus",         Label = "长度状态",   SortKey = "LengthStatus",         FilterType = "enum",   Width = "90",  EnumOptions = DisplayHelper.GetEnumFilterOptions<LengthStatus>(), DisplayConverter = v => v is LengthStatus ls ? DisplayHelper.GetLengthStatusText(ls) : DisplayHelper.GetLengthStatusText(v as string), GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "ProductionCutQuantity",Label = "生产支数",   SortKey = "ProductionCutQuantity", Width = "80",  GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "ProductionWeight",     Label = "生产重量(kg)", SortKey = "ProductionWeight",  Width = "90",  GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "SourceHeatNo",         Label = "炉号",       SortKey = "SourceHeatNo",         FilterType = "string", Width = "110", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "SourceName",           Label = "来料单位",   SortKey = "SourceName",           FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "WorkOrderNo",          Label = "工单号",     SortKey = "WorkOrderNo",          FilterType = "string", Width = "110", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "SalesOrderNo",         Label = "订单号",     SortKey = "SalesOrderNo",         FilterType = "string", Width = "110", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "ProductionMainNo",     Label = "主号",       SortKey = "ProductionMainNo",     FilterType = "string", Width = "110", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "Salesman",             Label = "业务员",     SortKey = "Salesman",             FilterType = "string", Width = "90",  GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "EndCustomer",          Label = "最终用户",   SortKey = "EndCustomer",          FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次信息" },
        };

        // G2: 排程信息
        var g2 = new List<ColumnDef>
        {
            new() { Key = "ScheduleStage",         Label = "计划状态",   SortKey = "ScheduleStage",         FilterType = "enum", Width = "110", EnumOptions = new List<EnumOption> { new("-1","无此工单") }.Concat(DisplayHelper.GetScheduleStageOptions()).ToList(), DisplayConverter = v => v is int s ? s switch { -1 => "无此工单", _ => IntStatusDisplayHelper.GetScheduleStageText(s) } : null, GroupKey = 2, GroupName = "排程信息" },
            new() { Key = "UrgencyLevel",          Label = "紧急程度",   SortKey = "UrgencyLevel",          FilterType = "enum", Width = "90",  EnumOptions = _urgencyOptions.Select(o => new EnumOption(o.Value, o.Text)).ToList(), GroupKey = 2, GroupName = "排程信息" },
        };

        // G3: 成检状态
        var g3 = new List<ColumnDef>
        {
            new() { Key = "KanbanStage",           Label = "成检阶段",   FilterType = "enum", Width = "100", EnumOptions = new() { new("待到料","待到料"), new("待检验","待检验"), new("检验中","检验中"), new("完成检验待入库","完成检验待入库") }, GroupKey = 3, GroupName = "成检状态" },
            new() { Key = "ReceiveDate",           Label = "到料日期",   SortKey = "ReceiveDate",           Width = "110", GroupKey = 3, GroupName = "成检状态" },
            new() { Key = "MaxInspectionDate",     Label = "最晚检验",   SortKey = "MaxInspectionDate",     Width = "110", GroupKey = 3, GroupName = "成检状态" },
        };

        // G4: 技术要求检验项（ProductRequirement 成品检验组；表检+尺寸恒必检；与「完成检验待入库」判定同源）
        var g4 = new List<ColumnDef>
        {
            new() { Key = "ReqCount",              Label = "必检项数",   Width = "80",  GroupKey = 4, GroupName = "技术要求检验项" },
            new() { Key = "ReqPmi",                Label = "PMI检验",   FilterType = "boolean", Width = "90", BoolTrueLabel = "是", BoolFalseLabel = "-", GroupKey = 4, GroupName = "技术要求检验项" },
            new() { Key = "ReqVisual",             Label = "表检",      FilterType = "boolean", Width = "90", BoolTrueLabel = "是", BoolFalseLabel = "-", GroupKey = 4, GroupName = "技术要求检验项" },
            new() { Key = "ReqDimension",          Label = "尺寸",      FilterType = "boolean", Width = "90", BoolTrueLabel = "是", BoolFalseLabel = "-", GroupKey = 4, GroupName = "技术要求检验项" },
            new() { Key = "ReqEndoscopy",          Label = "内窥",      FilterType = "boolean", Width = "90", BoolTrueLabel = "是", BoolFalseLabel = "-", GroupKey = 4, GroupName = "技术要求检验项" },
            new() { Key = "ReqHydro",              Label = "水压",      FilterType = "boolean", Width = "90", BoolTrueLabel = "是", BoolFalseLabel = "-", GroupKey = 4, GroupName = "技术要求检验项" },
            new() { Key = "ReqUnderwater",         Label = "水下气压",  FilterType = "boolean", Width = "90", BoolTrueLabel = "是", BoolFalseLabel = "-", GroupKey = 4, GroupName = "技术要求检验项" },
            new() { Key = "ReqEddy",               Label = "涡流",      FilterType = "boolean", Width = "90", BoolTrueLabel = "是", BoolFalseLabel = "-", GroupKey = 4, GroupName = "技术要求检验项" },
            new() { Key = "ReqUltrasonic",         Label = "超声波",    FilterType = "boolean", Width = "90", BoolTrueLabel = "是", BoolFalseLabel = "-", GroupKey = 4, GroupName = "技术要求检验项" },
            new() { Key = "ReqPortColoring",       Label = "端口着色",  FilterType = "boolean", Width = "90", BoolTrueLabel = "是", BoolFalseLabel = "-", GroupKey = 4, GroupName = "技术要求检验项" },
        };

        // G5: 各项检验的日期
        var g5 = new List<ColumnDef>
        {
            new() { Key = "InspectionCount",      Label = "检测项数",   Width = "80",  GroupKey = 5, GroupName = "各项检验的日期" },
            new() { Key = "PmiDate",              Label = "PMI检验",   SortKey = "PmiDate",              Width = "110", GroupKey = 5, GroupName = "各项检验的日期" },
            new() { Key = "VisualDate",           Label = "表检",      SortKey = "VisualDate",           Width = "110", GroupKey = 5, GroupName = "各项检验的日期" },
            new() { Key = "DimensionDate",        Label = "尺寸",      SortKey = "DimensionDate",        Width = "110", GroupKey = 5, GroupName = "各项检验的日期" },
            new() { Key = "EndoscopyDate",        Label = "内窥",      SortKey = "EndoscopyDate",        Width = "110", GroupKey = 5, GroupName = "各项检验的日期" },
            new() { Key = "HydroDate",            Label = "水压",      SortKey = "HydroDate",            Width = "110", GroupKey = 5, GroupName = "各项检验的日期" },
            new() { Key = "UnderwaterPneumaticDate", Label = "水下气压", SortKey = "UnderwaterPneumaticDate", Width = "110", GroupKey = 5, GroupName = "各项检验的日期" },
            new() { Key = "EddyCurrentDate",      Label = "涡流",      SortKey = "EddyCurrentDate",      Width = "110", GroupKey = 5, GroupName = "各项检验的日期" },
            new() { Key = "UltrasonicDate",       Label = "超声波",    SortKey = "UltrasonicDate",       Width = "110", GroupKey = 5, GroupName = "各项检验的日期" },
            new() { Key = "PortColoringDate",     Label = "端口着色",  SortKey = "PortColoringDate",     Width = "110", GroupKey = 5, GroupName = "各项检验的日期" },
        };

        // G6: 检验的数量信息
        var g6 = new List<ColumnDef>
        {
            new() { Key = "TotalQuantity",         Label = "检验支数",   SortKey = "TotalQuantity",         Width = "80",  GroupKey = 6, GroupName = "检验的数量信息" },
            new() { Key = "QualifiedQuantity",      Label = "理论合格支", SortKey = "QualifiedQuantity",      Width = "80",  GroupKey = 6, GroupName = "检验的数量信息" },
            new() { Key = "DefectReworkQuantity",   Label = "返整支数",   SortKey = "DefectReworkQuantity",   Width = "80",  GroupKey = 6, GroupName = "检验的数量信息" },
            new() { Key = "DefectWarehouseQuantity",Label = "不合格入库", SortKey = "DefectWarehouseQuantity",Width = "80",  GroupKey = 6, GroupName = "检验的数量信息" },
            new() { Key = "DefectScrapQuantity",    Label = "报废支数",   SortKey = "DefectScrapQuantity",    Width = "80",  GroupKey = 6, GroupName = "检验的数量信息" },
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

    // ========== 分页汇总 ==========

    /// <summary>
    /// 渲染期惰性重算当前页汇总。⚠️ MudBlazor 6.19.1 没有 CurrentPageChanged 事件（旧绑定被
    /// CaptureUnmatchedValues 静默吞掉、永不触发）；翻页只触发 MudTable 自身 StateHasChanged，
    /// 不触发父组件 OnAfterRenderAsync。故在 FooterContent 渲染时按「页码/每页行数/数据量」签名惰性重算，
    /// 保证页脚汇总与实际显示行一致。
    /// </summary>
    private void EnsurePageSumsComputed()
    {
        var page = table?.CurrentPage ?? 0;
        var rowsPerPage = table?.RowsPerPage ?? _pageSize;
        if (rowsPerPage <= 0) rowsPerPage = _pageSize;
        var count = _filteredItems.Count;
        if (page == _lastSummedPage && count == _lastSummedCount && rowsPerPage == _lastSummedPageSize)
            return;
        _lastSummedPage = page;
        _lastSummedCount = count;
        _lastSummedPageSize = rowsPerPage;
        ComputePageSums();
    }

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_filteredItems.Count == 0) return;

        // 按当前页显示行汇总（Items 模式，取 MudTable 当前页切片）
        var page = table?.CurrentPage ?? 0;
        var rowsPerPage = table?.RowsPerPage ?? _pageSize;
        if (rowsPerPage <= 0) rowsPerPage = _pageSize;
        var pageItems = _filteredItems.Skip(page * rowsPerPage).Take(rowsPerPage).ToList();
        if (pageItems.Count == 0) return;

        var props = typeof(FinalInspectionPlanDto)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);

        foreach (var col in _visibleColumns.Where(c => _summableColumnKeys.Contains(c.Key)))
        {
            if (!props.TryGetValue(col.Key, out var prop)) continue;

            var type = prop.PropertyType;
            try
            {
                if (type == typeof(decimal?))
                {
                    var sum = pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
                else if (type == typeof(int?))
                {
                    var sum = pageItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(int))
                {
                    var sum = pageItems.Sum(item => (int)(prop.GetValue(item) ?? 0));
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

    // ========== 字典下拉选项（配置表动态加载，失败兜底静态 KeyToChinese）==========
    // 列定义 GetPlanColumnDefs 为 static，故选项字段也须 static
    private static List<(string Value, string Text)> _urgencyOptions =
        UrgencyLevelKeys.KeyToChinese.Select(kv => (kv.Key, kv.Value)).ToList();

    private async Task LoadDictOptionsAsync()
    {
        var urgency = await DictValueDefinitionService.GetEnabledValuesAsync(DictValueDefaults.UrgencyLevelKey);
        if (urgency.Success && urgency.Data is { Count: > 0 })
            _urgencyOptions = urgency.Data.Select(t => (t.Value, t.DisplayName)).ToList();
    }

    // ========== 生命周期 ==========

    protected override async Task OnInitializedAsync()
    {
        await LoadDictOptionsAsync();

        _allColumns = GetAllColumnDefs();

        var savedState = await PageState.LoadAsync("final-inspection-plan");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "BatchNo";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;

            if (savedState.Extras?.ContainsKey("columnVisibility") == true)
            {
                try
                {
                    var raw = savedState.Extras["columnVisibility"];
                    var visibleKeys = JsonSerializer.Deserialize<List<string>>(raw);
                    if (visibleKeys != null)
                    {
                        var visibleSet = new HashSet<string>(visibleKeys);
                        foreach (var col in _allColumns)
                            col.Visible = visibleSet.Contains(col.Key);
                    }
                }
                catch { }
            }

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

            if (savedState.Extras?.ContainsKey("selectedTab") == true)
            {
                _selectedTab = savedState.Extras["selectedTab"];
                if (_selectedTab == "全部") _selectedTab = null;
            }
        }

        await LoadDataAsync();
    }

    private void OnRowsPerPageChanged(int size)
    {
        _pageSize = size;
        ApplyFiltersAndSort();
        StateHasChanged();
        _ = SavePageStateAsync();
    }

    // ========== 分组标题栏同步 ==========

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 分组标题栏：测量实际列宽 + 同步滚动
        await JS.InvokeVoidAsync("initGroupHeaders", "#final-inspection-plan-table");

        // 分页导航/页大小切换后重算当前页汇总（pager 操作只改 CurrentPage/RowsPerPage，不触发 ApplyFiltersAndSort）
        if (table != null && _filteredItems.Count > 0)
        {
            var page = table.CurrentPage;
            var count = _filteredItems.Count;
            var rowsPerPage = table.RowsPerPage;
            if (page != _lastSummedPage || count != _lastSummedCount || rowsPerPage != _lastSummedPageSize)
            {
                _lastSummedPage = page;
                _lastSummedCount = count;
                _lastSummedPageSize = rowsPerPage;
                ComputePageSums();
                StateHasChanged();
            }
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _allItems = await KanbanSvc.GetKanbanAsync();
            UpdateTabSummary();
            BuildFilterOptionsFromData();
            ApplyFiltersAndSort();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _allItems = new();
        }
    }

    // ========== 筛选上下文构建（内存数据驱动） ==========

    private void BuildFilterOptionsFromData()
    {
        _filterContextOptions.Clear();

        foreach (var col in _allColumns.Where(c => c.FilterType != null))
        {
            if (col.FilterType == "enum" && col.EnumOptions != null)
            {
                _filterContextOptions[col.Key] = col.EnumOptions.Select(e => new ExcelFilterOption
                {
                    Value = e.Value,
                    Display = e.Display,
                    Count = 0
                }).ToList();
            }
            else if (col.FilterType == "string")
            {
                var distinct = _allItems
                    .Select(x => GetFilterValue(x, col.Key))
                    .Where(v => v != null)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(v => v)
                    .Select(v => new ExcelFilterOption { Value = v!, Display = v!, Count = 0 })
                    .ToList();
                _filterContextOptions[col.Key] = distinct;
            }
            else if (col.FilterType == "boolean")
            {
                _filterContextOptions[col.Key] = new List<ExcelFilterOption>
                {
                    new() { Value = "True", Display = col.BoolTrueLabel ?? "是", Count = 0 },
                    new() { Value = "False", Display = col.BoolFalseLabel ?? "否", Count = 0 }
                };
            }
        }
    }

    private static string? GetFilterValue(FinalInspectionPlanDto item, string key) => key switch
    {
        "BatchNo" => item.BatchNo,
        "InspectionType" => item.InspectionType?.ToString(),
        "IsDeliveryStatus" => item.IsDeliveryStatusDisplay,
        "ProductionType" => item.ProductionType?.ToString(),
        "ManufacturingItem" => item.ManufacturingItem?.ToString(),
        "ManufacturingStatus" => item.ManufacturingStatus?.ToString(),
        "DeliveryState" => item.DeliveryState?.ToString(),
        "PlantGrade" => item.PlantGrade,
        "Specification" => item.Specification,
        "LengthStatus" => item.LengthStatus.HasValue ? item.LengthStatus.Value.ToString() : null,
        "SourceHeatNo" => item.SourceHeatNo,
        "SourceName" => item.SourceName,
        "WorkOrderNo" => item.WorkOrderNo,
        "SalesOrderNo" => item.SalesOrderNo,
        "ProductionMainNo" => item.ProductionMainNo,
        "Salesman" => item.Salesman,
        "EndCustomer" => item.EndCustomer,
        "ScheduleStage" => item.ScheduleStage.ToString(),
        "UrgencyLevel" => item.UrgencyLevel,
        "KanbanStage" => item.KanbanStage,
        // G4: 技术要求检验项（boolean 列按约定返回 "True"/"False"）
        "ReqPmi" => item.ReqPmi ? "True" : "False",
        "ReqVisual" => item.ReqVisual ? "True" : "False",
        "ReqDimension" => item.ReqDimension ? "True" : "False",
        "ReqEndoscopy" => item.ReqEndoscopy ? "True" : "False",
        "ReqHydro" => item.ReqHydro ? "True" : "False",
        "ReqUnderwater" => item.ReqUnderwater ? "True" : "False",
        "ReqEddy" => item.ReqEddy ? "True" : "False",
        "ReqUltrasonic" => item.ReqUltrasonic ? "True" : "False",
        "ReqPortColoring" => item.ReqPortColoring ? "True" : "False",
        _ => null
    };

    // ========== Tab 切换 ==========

    private async Task OnTabChanged(string? tab)
    {
        _selectedTab = tab == "全部" ? null : tab;
        UpdateTabSummary();
        await SavePageStateAsync();
        ApplyFiltersAndSort();
        StateHasChanged();
    }

    private void UpdateTabSummary()
    {
        var filtered = _selectedTab == null
            ? _allItems
            : _allItems.Where(x => x.KanbanStage == _selectedTab).ToList();

        _tabCount = filtered.Count;
        _tabTotalWeight = filtered.Sum(x => x.ProductionWeight ?? 0);

        // 急单统计仅计「计划状态=成品检验(4)」的批次（用户口径：不需按全部统计）
        var urgentScope = filtered.Where(x => x.ScheduleStage == 4).ToList();
        _urgentAPlusCount = urgentScope.Count(x => x.UrgencyLevel == UrgencyLevelKeys.APlusUrgent);
        _urgentAPlusWeight = urgentScope.Where(x => x.UrgencyLevel == UrgencyLevelKeys.APlusUrgent).Sum(x => x.ProductionWeight ?? 0);
        _urgentACount = urgentScope.Count(x => x.UrgencyLevel == UrgencyLevelKeys.AUrgent);
        _urgentAWeight = urgentScope.Where(x => x.UrgencyLevel == UrgencyLevelKeys.AUrgent).Sum(x => x.ProductionWeight ?? 0);
    }

    // ========== ExcelFilter 事件 ==========

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        await SavePageStateAsync();
        ApplyFiltersAndSort();
        StateHasChanged();
    }

    // ========== 数据加载（从 _allItems 中筛选+排序） ==========

    private void ApplyFiltersAndSort()
    {
        // 从 _allItems 中过滤
        var filtered = _selectedTab == null
            ? _allItems.ToList()
            : _allItems.Where(x => x.KanbanStage == _selectedTab).ToList();

        // 1. 关键词搜索
        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            var kw = _searchKeyword;
            filtered = filtered.Where(x =>
                (x.BatchNo != null && x.BatchNo.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PlantGrade != null && x.PlantGrade.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.Specification != null && x.Specification.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.SourceHeatNo != null && x.SourceHeatNo.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.SourceName != null && x.SourceName.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.WorkOrderNo != null && x.WorkOrderNo.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.SalesOrderNo != null && x.SalesOrderNo.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.ProductionMainNo != null && x.ProductionMainNo.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.Salesman != null && x.Salesman.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.EndCustomer != null && x.EndCustomer.Contains(kw, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        // 2. ExcelFilter 列筛选
        if (_columnFilters.Count > 0)
        {
            filtered = filtered.Where(x => _columnFilters.All(f =>
            {
                var val = GetFilterValue(x, f.Key);
                return val != null && f.Value.Contains(val);
            })).ToList();
        }

        // 3. 排序
        filtered = ApplySorting(filtered, sortColumn, sortDescending);

        // 4. 赋全量数据（MudTable Items 模式自动分页）
        _filteredAllItems = filtered.ToList();
        _filteredItems = _filteredAllItems;
        _totalCount = _filteredItems.Count;
        ComputePageSums();
    }

    private static List<FinalInspectionPlanDto> ApplySorting(List<FinalInspectionPlanDto> items, string sortBy, bool desc)
    {
        var query = sortBy.ToLower() switch
        {
            "batchno" => items.OrderBy(x => x.BatchNo ?? ""),
            "inspectiontype" => items.OrderBy(x => x.InspectionType),
            "isdeliverystatus" => items.OrderBy(x => x.IsDeliveryStatusDisplay ?? ""),
            "productiontype" => items.OrderBy(x => x.ProductionType),
            "manufacturingitem" => items.OrderBy(x => x.ManufacturingItem),
            "manufacturingstatus" => items.OrderBy(x => x.ManufacturingStatus),
            "deliverystate" => items.OrderBy(x => x.DeliveryState),
            "plantgrade" => items.OrderBy(x => x.PlantGrade ?? ""),
            "specification" => items.OrderBy(x => x.Specification ?? ""),
            "lengthstatus" => items.OrderBy(x => x.LengthStatus.HasValue ? DisplayHelper.GetLengthStatusText(x.LengthStatus.Value) : ""),
            "productioncutquantity" => items.OrderBy(x => x.ProductionCutQuantity),
            "productionweight" => items.OrderBy(x => x.ProductionWeight),
            "sourceheatno" => items.OrderBy(x => x.SourceHeatNo ?? ""),
            "sourcename" => items.OrderBy(x => x.SourceName ?? ""),
            "workorderno" => items.OrderBy(x => x.WorkOrderNo ?? ""),
            "salesorderno" => items.OrderBy(x => x.SalesOrderNo ?? ""),
            "productionmainno" => items.OrderBy(x => x.ProductionMainNo ?? ""),
            "salesman" => items.OrderBy(x => x.Salesman ?? ""),
            "endcustomer" => items.OrderBy(x => x.EndCustomer ?? ""),
            "schedulestage" => items.OrderBy(x => x.ScheduleStage),
            "urgencylevel" => items.OrderBy(x => x.UrgencyLevel ?? ""),
            "receivedate" => items.OrderBy(x => x.ReceiveDate),
            "maxinspectiondate" => items.OrderBy(x => x.MaxInspectionDate),
            "pmidate" => items.OrderBy(x => x.PmiDate),
            "visualdate" => items.OrderBy(x => x.VisualDate),
            "dimensiondate" => items.OrderBy(x => x.DimensionDate),
            "endoscopydate" => items.OrderBy(x => x.EndoscopyDate),
            "hydrodate" => items.OrderBy(x => x.HydroDate),
            "underwaterpneumaticdate" => items.OrderBy(x => x.UnderwaterPneumaticDate),
            "eddycurrentdate" => items.OrderBy(x => x.EddyCurrentDate),
            "ultrasonicdate" => items.OrderBy(x => x.UltrasonicDate),
            "portcoloringdate" => items.OrderBy(x => x.PortColoringDate),
            "reqcount" => items.OrderBy(x => x.ReqCount),
            "reqpmi" => items.OrderBy(x => x.ReqPmi),
            "reqvisual" => items.OrderBy(x => x.ReqVisual),
            "reqdimension" => items.OrderBy(x => x.ReqDimension),
            "reqendoscopy" => items.OrderBy(x => x.ReqEndoscopy),
            "reqhydro" => items.OrderBy(x => x.ReqHydro),
            "requnderwater" => items.OrderBy(x => x.ReqUnderwater),
            "reqeddy" => items.OrderBy(x => x.ReqEddy),
            "requltrasonic" => items.OrderBy(x => x.ReqUltrasonic),
            "reqportcoloring" => items.OrderBy(x => x.ReqPortColoring),
            "totalquantity" => items.OrderBy(x => x.TotalQuantity),
            "qualifiedquantity" => items.OrderBy(x => x.QualifiedQuantity),
            "defectreworkquantity" => items.OrderBy(x => x.DefectReworkQuantity),
            "defectwarehousequantity" => items.OrderBy(x => x.DefectWarehouseQuantity),
            "defectscrapquantity" => items.OrderBy(x => x.DefectScrapQuantity),
            _ => items.OrderBy(x => x.BatchNo ?? "")
        };
        return desc ? query.Reverse().ToList() : query.ToList();
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
        ApplyFiltersAndSort();
        StateHasChanged();
    }

    // ========== 搜索 ==========

    private async Task OnSearchChanged(string? value)
    {
        _searchKeyword = value ?? string.Empty;
        await SavePageStateAsync();
        ApplyFiltersAndSort();
        StateHasChanged();
    }

    // ========== 列显隐 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SavePageStateAsync();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        var idx = _allColumns.IndexOf(col);
        if (idx > 0)
        {
            _allColumns.RemoveAt(idx);
            _allColumns.Insert(idx - 1, col);
        }
        await SavePageStateAsync();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        var idx = _allColumns.IndexOf(col);
        if (idx < _allColumns.Count - 1)
        {
            _allColumns.RemoveAt(idx);
            _allColumns.Insert(idx + 1, col);
        }
        await SavePageStateAsync();
    }

    private void ResetColumnDisplay()
    {
        foreach (var col in _allColumns)
            col.Visible = true;
    }

    // ========== 分组 CSS ==========

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1",
            2 => "col-g2",
            3 => "col-g3",
            4 => "col-g4",
            5 => "col-g5",
            6 => "col-g6",
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
            6 => "col-g6-cell",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
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

    // ========== 列值渲染 ==========

    private RenderFragment RenderCell(FinalInspectionPlanDto item, ColumnDef col) => builder =>
    {
        builder.OpenElement(0, "span");
        switch (col.Key)
        {
            case "BatchNo":
                builder.AddContent(0, item.BatchNo ?? "-");
                break;
            case "InspectionType":
                builder.AddContent(0, item.InspectionTypeDisplay ?? "-");
                break;
            case "IsDeliveryStatus":
                builder.AddContent(0, item.IsDeliveryStatusDisplay ?? "-");
                break;
            case "ProductionType":
                builder.AddContent(0, item.ProductionTypeDisplay ?? "-");
                break;
            case "ManufacturingItem":
                builder.AddContent(0, DisplayHelper.GetMaterialTypeText(item.ManufacturingItem?.ToString()));
                break;
            case "ManufacturingStatus":
                builder.AddContent(0, item.ManufacturingStatusDisplay ?? "-");
                break;
            case "DeliveryState":
                builder.AddContent(0, DisplayHelper.GetDeliveryStateText(item.DeliveryState?.ToString()));
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade ?? "-");
                break;
            case "Specification":
                builder.AddContent(0, item.Specification ?? "-");
                break;
            case "LengthStatus":
                builder.AddContent(0, item.LengthStatus.HasValue ? DisplayHelper.GetLengthStatusText(item.LengthStatus.Value) : "-");
                break;
            case "ProductionCutQuantity":
                builder.AddContent(0, item.ProductionCutQuantity > 0 ? item.ProductionCutQuantity.ToString() : "-");
                break;
            case "ProductionWeight":
                builder.AddContent(0, item.ProductionWeight is > 0 ? ((int)item.ProductionWeight.Value).ToString("G29") : "-");
                break;
            case "SourceHeatNo":
                builder.AddContent(0, item.SourceHeatNo ?? "-");
                break;
            case "SourceName":
                builder.AddContent(0, item.SourceName ?? "-");
                break;
            case "WorkOrderNo":
                builder.AddContent(0, item.WorkOrderNo ?? "-");
                break;
            case "SalesOrderNo":
                builder.AddContent(0, item.SalesOrderNo ?? "-");
                break;
            case "ProductionMainNo":
                builder.AddContent(0, item.ProductionMainNo ?? "-");
                break;
            case "Salesman":
                builder.AddContent(0, item.Salesman ?? "-");
                break;
            case "EndCustomer":
                builder.AddContent(0, item.EndCustomer ?? "-");
                break;
            case "ScheduleStage":
                var stageColor = item.ScheduleStage switch
                {
                    -1 => Color.Error,
                    0 => Color.Error,       // 主号暂停
                    1 => Color.Success,     // 主号完成
                    2 => Color.Warning,     // 原料锁定
                    3 => Color.Info,        // 生产执行
                    4 => Color.Primary,     // 成品检验
                    _ => Color.Default
                };
                var stageText = item.ScheduleStage switch
                {
                    -1 => "无此工单",
                    _ => IntStatusDisplayHelper.GetScheduleStageText(item.ScheduleStage)
                };
                builder.CloseElement(); // close span
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", stageColor);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, stageText)));
                builder.CloseComponent();
                return; // skip closing span below
            case "UrgencyLevel":
                var urgencyColor = item.UrgencyLevel switch
                {
                    UrgencyLevelKeys.APlusUrgent => Color.Error,
                    UrgencyLevelKeys.AUrgent => Color.Warning,
                    UrgencyLevelKeys.BOrder => Color.Info,
                    UrgencyLevelKeys.CSlow => Color.Default,
                    UrgencyLevelKeys.DSlow => Color.Default,
                    _ => Color.Default
                };
                if (item.UrgencyLevel != null)
                {
                    builder.CloseElement(); // close span
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", urgencyColor);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey,item.UrgencyLevel))));
                    builder.CloseComponent();
                    return; // skip closing span below
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "KanbanStage":
                builder.AddContent(0, item.KanbanStage);
                break;
            case "ReceiveDate":
                builder.AddContent(0, item.ReceiveDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "MaxInspectionDate":
                builder.AddContent(0, item.MaxInspectionDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            // G4: 技术要求检验项
            case "ReqCount":
                builder.AddContent(0, item.ReqCount.ToString());
                break;
            case "ReqPmi":
                builder.AddContent(0, item.ReqPmi ? "是" : "-");
                break;
            case "ReqVisual":
                builder.AddContent(0, item.ReqVisual ? "是" : "-");
                break;
            case "ReqDimension":
                builder.AddContent(0, item.ReqDimension ? "是" : "-");
                break;
            case "ReqEndoscopy":
                builder.AddContent(0, item.ReqEndoscopy ? "是" : "-");
                break;
            case "ReqHydro":
                builder.AddContent(0, item.ReqHydro ? "是" : "-");
                break;
            case "ReqUnderwater":
                builder.AddContent(0, item.ReqUnderwater ? "是" : "-");
                break;
            case "ReqEddy":
                builder.AddContent(0, item.ReqEddy ? "是" : "-");
                break;
            case "ReqUltrasonic":
                builder.AddContent(0, item.ReqUltrasonic ? "是" : "-");
                break;
            case "ReqPortColoring":
                builder.AddContent(0, item.ReqPortColoring ? "是" : "-");
                break;
            // G5: 各项检验的日期
            case "InspectionCount":
                builder.AddContent(0, item.InspectionCount.ToString());
                break;
            case "PmiDate":
                builder.AddContent(0, item.PmiDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "VisualDate":
                builder.AddContent(0, item.VisualDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "DimensionDate":
                builder.AddContent(0, item.DimensionDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "EndoscopyDate":
                builder.AddContent(0, item.EndoscopyDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "HydroDate":
                builder.AddContent(0, item.HydroDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "UnderwaterPneumaticDate":
                builder.AddContent(0, item.UnderwaterPneumaticDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "EddyCurrentDate":
                builder.AddContent(0, item.EddyCurrentDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "UltrasonicDate":
                builder.AddContent(0, item.UltrasonicDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "PortColoringDate":
                builder.AddContent(0, item.PortColoringDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            // G6: 检验的数量信息
            case "TotalQuantity":
                builder.AddContent(0, item.TotalQuantity.ToString());
                break;
            case "QualifiedQuantity":
                builder.AddContent(0, item.QualifiedQuantity.ToString());
                break;
            case "DefectReworkQuantity":
                builder.AddContent(0, item.DefectReworkQuantity.ToString());
                break;
            case "DefectWarehouseQuantity":
                builder.AddContent(0, item.DefectWarehouseQuantity.ToString());
                break;
            case "DefectScrapQuantity":
                builder.AddContent(0, item.DefectScrapQuantity.ToString());
                break;
            default:
                builder.AddContent(0, "-");
                break;
        }
        builder.CloseElement();
    };
    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));

        extras["columnVisibility"] = JsonSerializer.Serialize(_allColumns.Where(c => c.Visible).Select(c => c.Key).ToList());
        extras["selectedTab"] = _selectedTab ?? "全部";

        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = 1,
            Extras = extras
        };
        await PageState.SaveAsync("final-inspection-plan", state);
    }

    // ========== 显示类汇总卡片 ==========

    private void ToggleSummaryCard()
    {
        _showSummaryCard = !_showSummaryCard;
        if (_showSummaryCard && _summaryRows.Count == 0)
            _ = LoadSummaryAsync();
    }

    private async Task LoadSummaryAsync()
    {
        try
        {
            _isLoadingSummary = true;
            StateHasChanged();
            _summaryRows = await KanbanSvc.GetSummaryAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"汇总加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoadingSummary = false;
            StateHasChanged();
        }
    }

    /// <summary>汇总单元格文本：批次数/生产支数/生产重量(kg) 三合一（如 "5批/1000支/8000kg"）；全 0 显 "-" 防视觉污染</summary>
    private static string RenderSummaryCell(int count, int quantity, decimal weight)
        => count == 0 && quantity == 0 && weight == 0
            ? "-"
            : $"{count}批/{quantity}支/{weight.ToString("G29")}kg";

    /// <summary>打印「待检批支重汇总」卡片（前端 printRawHtml 直接打印 DOM 表格）</summary>
    private async Task PrintSummaryTable()
    {
        try
        {
            var html = await JS.InvokeAsync<string>("getTableHtml", "#final-inspection-plan-summary-table");
            if (!string.IsNullOrEmpty(html))
                await JS.InvokeVoidAsync("printRawHtml", html, "成检计划-待检批支重汇总");
            else
                Snackbar.Add("未找到可打印的汇总表格", Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 打印 ==========

    private async Task PrintSelected()
    {
        if (_selectedItems.Count == 0) return;

        var printItems = _selectedItems.Select(item =>
        {
            var dict = new Dictionary<string, object>();
            foreach (var col in _visibleColumns)
                dict[col.Key] = ResolvePrintValue(item, col);
            return dict;
        }).ToList();

        var request = new FinalInspectionPlanPrintRequest
        {
            Title = "成检计划",
            Items = printItems,
            Columns = _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList()
        };

        var apiUrl = $"{Http.BaseAddress}api/final-inspection-plan/print-file";
        var json = JsonSerializer.Serialize(request);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }

    private static object ResolvePrintValue(FinalInspectionPlanDto item, ColumnDef col)
    {
        if (col.DisplayConverter != null)
            return col.DisplayConverter(GetRawPropertyValue(item, col.Key)) ?? "";

        if (col.FilterType == "boolean")
        {
            var raw = GetRawPropertyValue(item, col.Key);
            if (raw is bool b) return b ? col.BoolTrueLabel : col.BoolFalseLabel;
            return raw?.ToString() ?? "-";
        }

        return GetRawPropertyValue(item, col.Key)!;
    }

    private static object? GetRawPropertyValue(FinalInspectionPlanDto item, string key) => key switch
    {
        "BatchNo" => item.BatchNo ?? "",
        "InspectionType" => item.InspectionTypeDisplay ?? "",
        "IsDeliveryStatus" => item.IsDeliveryStatusDisplay ?? "",
        "ProductionType" => item.ProductionTypeDisplay ?? "",
        "ManufacturingItem" => item.ManufacturingItemDisplay ?? "",
        "ManufacturingStatus" => item.ManufacturingStatusDisplay ?? "",
        "DeliveryState" => item.DeliveryStateDisplay ?? "",
        "PlantGrade" => item.PlantGrade ?? "",
        "Specification" => item.Specification ?? "",
        "LengthStatus" => item.LengthStatus.HasValue ? DisplayHelper.GetLengthStatusText(item.LengthStatus.Value) : "",
        "ProductionCutQuantity" => item.ProductionCutQuantity,
        "ProductionWeight" => item.ProductionWeight,
        "SourceHeatNo" => item.SourceHeatNo ?? "",
        "SourceName" => item.SourceName ?? "",
        "WorkOrderNo" => item.WorkOrderNo ?? "",
        "SalesOrderNo" => item.SalesOrderNo ?? "",
        "ProductionMainNo" => item.ProductionMainNo ?? "",
        "Salesman" => item.Salesman ?? "",
        "EndCustomer" => item.EndCustomer ?? "",
        "ScheduleStage" => item.ScheduleStage,
        "UrgencyLevel" => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, item.UrgencyLevel) ?? "",
        "KanbanStage" => item.KanbanStage,
        "ReceiveDate" => item.ReceiveDate,
        "MaxInspectionDate" => item.MaxInspectionDate,
        "ReqCount" => item.ReqCount,
        "ReqPmi" => item.ReqPmi ? "是" : "-",
        "ReqVisual" => item.ReqVisual ? "是" : "-",
        "ReqDimension" => item.ReqDimension ? "是" : "-",
        "ReqEndoscopy" => item.ReqEndoscopy ? "是" : "-",
        "ReqHydro" => item.ReqHydro ? "是" : "-",
        "ReqUnderwater" => item.ReqUnderwater ? "是" : "-",
        "ReqEddy" => item.ReqEddy ? "是" : "-",
        "ReqUltrasonic" => item.ReqUltrasonic ? "是" : "-",
        "ReqPortColoring" => item.ReqPortColoring ? "是" : "-",
        "InspectionCount" => item.InspectionCount,
        "PmiDate" => item.PmiDate,
        "VisualDate" => item.VisualDate,
        "DimensionDate" => item.DimensionDate,
        "EndoscopyDate" => item.EndoscopyDate,
        "HydroDate" => item.HydroDate,
        "UnderwaterPneumaticDate" => item.UnderwaterPneumaticDate,
        "EddyCurrentDate" => item.EddyCurrentDate,
        "UltrasonicDate" => item.UltrasonicDate,
        "PortColoringDate" => item.PortColoringDate,
        "TotalQuantity" => item.TotalQuantity,
        "QualifiedQuantity" => item.QualifiedQuantity,
        "DefectReworkQuantity" => item.DefectReworkQuantity,
        "DefectWarehouseQuantity" => item.DefectWarehouseQuantity,
        "DefectScrapQuantity" => item.DefectScrapQuantity,
        _ => ""
    };
}
