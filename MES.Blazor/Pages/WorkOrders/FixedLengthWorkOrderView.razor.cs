using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs.WorkOrder;
using System.Text.Json;

namespace MES.Blazor.Pages.WorkOrders;

/// <summary>
/// 定尺工单定尺数据（联通视图，列表模式，主号级按长度实时聚合）
/// </summary>
public partial class FixedLengthWorkOrderView
{
    private MudTable<FixedLengthWorkOrderListDto>? table;
    private List<FixedLengthWorkOrderListDto> _allItems = new();
    private List<FixedLengthWorkOrderListDto> _filteredItems = new();
    private bool _isLoading;

    // B33: 分页汇总（仅「需求支数」为真正逐行可累加字段；
    // 切割/成检/总现况均为主号级聚合值，同主号多行重复，不可求和）
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "PlannedQuantity"
    };

    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    // 排序状态
    private string sortColumn = "WorkOrderNo";
    private bool sortDescending = false;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs()
    {
        var deliveryStateOptions = new List<EnumOption>
        {
            new("SolutionAnnealedAndPickled", "固溶酸洗"),
            new("SolutionAnnealedAndPickledUTube", "固溶酸洗-U型管"),
            new("SolutionAnnealedAndPickledExternalPolished", "固溶酸洗-外抛光"),
            new("SolutionAnnealedAndPickledInternalPolished", "固溶酸洗-内抛光"),
            new("SolutionAnnealedAndPickledBothPolished", "固溶酸洗-内外抛光"),
            new("SolutionAnnealedAndPickledCoiled", "固溶酸洗-盘管"),
            new("Bright", "光亮"),
            new("BrightUTube", "光亮-U型管"),
            new("BrightCoiled", "光亮-盘管"),
            new("Hard", "硬态"),
            new("SolidSolutionStraightening", "固溶矫直")
        };

        // G1: 基础数据
        var g1 = new List<ColumnDef>
        {
            new() { Key = "WorkOrderNo",        Label = "工单号",       SortKey = "WorkOrderNo",       FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "Length",             Label = "长度(mm)",     SortKey = "Length",            Width = "80",  GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "PlannedQuantity",    Label = "需求支数",     SortKey = "PlannedQuantity",   Width = "80",  GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "Salesman",           Label = "业务员",       SortKey = "Salesman",          FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "CustomerName",       Label = "往来单位",     SortKey = "CustomerName",      FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SignDate",           Label = "订单日期",     SortKey = "SignDate",          Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DeliveryDate",       Label = "交货日期",     SortKey = "DeliveryDate",      Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SalesOrderNo",       Label = "订单号",       SortKey = "SalesOrderNo",      FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionMainNo",   Label = "主号",         SortKey = "ProductionMainNo",  FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionSubNo",    Label = "次号",         SortKey = "ProductionSubNo",   FilterType = "string", Width = "120", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DeliveryState",      Label = "交货状态",     SortKey = "DeliveryState",     FilterType = "enum", Width = "120", EnumOptions = deliveryStateOptions, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "PlantGrade",         Label = "工厂牌号",     SortKey = "PlantGrade",        FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "Specification",      Label = "规格",         SortKey = "Specification",     FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
        };

        // G2: 计划状态
        var g2 = new List<ColumnDef>
        {
            new() { Key = "ScheduleStage",      Label = "关注状态",     SortKey = "ScheduleStage",     FilterType = "enum", Width = "120", EnumOptions = new() { new("0","工单完成"), new("1","原料锁定"), new("2","生产执行"), new("3","成品检验") }, GroupKey = 2, GroupName = "计划状态" },
            new() { Key = "UrgencyLevel",       Label = "工单计划性",   SortKey = "UrgencyLevel",      FilterType = "string", Width = "120", GroupKey = 2, GroupName = "计划状态" },
        };

        // G3: 成品切割执行
        var g3 = new List<ColumnDef>
        {
            new() { Key = "CutDeadline",        Label = "切割截止日",   SortKey = "CutDeadline",       Width = "120", GroupKey = 3, GroupName = "成品切割" },
            new() { Key = "CutQuantity",        Label = "切割支数",     SortKey = "CutQuantity",       Width = "80",  GroupKey = 3, GroupName = "成品切割" },
            new() { Key = "CutSurplus",         Label = "切割盈缺",     SortKey = "CutSurplus",        Width = "80",  GroupKey = 3, GroupName = "成品切割" },
        };

        // G4: 成检数据
        var g4 = new List<ColumnDef>
        {
            new() { Key = "InspectionDeadline", Label = "成检截止日",   SortKey = "InspectionDeadline",Width = "120", GroupKey = 4, GroupName = "成检数据" },
            new() { Key = "ArrivedQuantity",    Label = "到料支数",     SortKey = "ArrivedQuantity",   Width = "80",  GroupKey = 4, GroupName = "成检数据" },
            new() { Key = "DefectQuantity",     Label = "次品支数",     SortKey = "DefectQuantity",    Width = "80",  GroupKey = 4, GroupName = "成检数据" },
            new() { Key = "QualifiedQuantity",  Label = "合格支数",     SortKey = "QualifiedQuantity", Width = "80",  GroupKey = 4, GroupName = "成检数据" },
            new() { Key = "QualifiedSurplus",   Label = "合格盈缺",     SortKey = "QualifiedSurplus",  Width = "80",  GroupKey = 4, GroupName = "成检数据" },
        };

        // G5: 总现况分析（主号级）
        var g5 = new List<ColumnDef>
        {
            new() { Key = "MainNoTotalRequirement", Label = "主号总需求支", SortKey = "MainNoTotalRequirement", Width = "80", Visible = false, GroupKey = 5, GroupName = "总现况分析" },
            new() { Key = "MainNoTotalInput",       Label = "主号总投料支", SortKey = "MainNoTotalInput",       Width = "80", Visible = false, GroupKey = 5, GroupName = "总现况分析" },
            new() { Key = "MainNoUncut",            Label = "主号未切总支", SortKey = "MainNoUncut",            Width = "80", Visible = false, GroupKey = 5, GroupName = "总现况分析" },
            new() { Key = "MainNoCutTheoretical",   Label = "主号切割理论总",SortKey = "MainNoCutTheoretical",  Width = "80", Visible = false, GroupKey = 5, GroupName = "总现况分析" },
            new() { Key = "MainNoCutActual",        Label = "主号切割实际总",SortKey = "MainNoCutActual",       Width = "80", Visible = false, GroupKey = 5, GroupName = "总现况分析" },
            new() { Key = "MainNoDefect",           Label = "主号成检次品总",SortKey = "MainNoDefect",          Width = "80", Visible = false, GroupKey = 5, GroupName = "总现况分析" },
            new() { Key = "TotalSurplus",           Label = "总盈亏支数",   SortKey = "TotalSurplus",          Width = "80", GroupKey = 5, GroupName = "总现况分析" },
            new() { Key = "TotalSurplusStatus",     Label = "总盈亏状态",   SortKey = "TotalSurplusStatus",    FilterType = "enum", Width = "100", EnumOptions = new() { new("合理","合理"), new("缺少","缺少"), new("略","略") }, GroupKey = 5, GroupName = "总现况分析" },
        };

        var all = new List<ColumnDef>();
        all.AddRange(g1);
        all.AddRange(g2);
        all.AddRange(g3);
        all.AddRange(g4);
        all.AddRange(g5);
        return all;
    }

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_filteredItems.Count == 0) return;

        var props = typeof(FixedLengthWorkOrderListDto)
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
                    var sum = _filteredItems.Sum(item => (int)(prop.GetValue(item) ?? 0));
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal))
                {
                    var sum = _filteredItems.Sum(item => (decimal)(prop.GetValue(item) ?? 0m));
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

    // ========== 数据加载 ==========

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        StateHasChanged();
        try
        {
            var result = await WorkOrderExecutionService.GetFixedLengthListAsync();
            if (result.Success && result.Data != null)
            {
                _allItems = result.Data;
            }
            else
            {
                _allItems = new();
                Snackbar.Add(result?.Message ?? "获取数据失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _allItems = new();
        }
        finally
        {
            _isLoading = false;
        }

        BuildFilterContextOptions();
        ApplyFiltersAndSort();
    }

    // ========== 筛选上下文构建 ==========

    private void BuildFilterContextOptions()
    {
        _filterContextOptions.Clear();

        foreach (var col in _allColumns)
        {
            if (col.FilterType == "string")
            {
                var options = _allItems
                    .Select(item => GetFilterValue(item, col.Key))
                    .Where(v => v != null)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .Select(val => new ExcelFilterOption
                    {
                        Value = val!,
                        Display = val!,
                        Count = _allItems.Count(x => string.Equals(GetFilterValue(x, col.Key), val, StringComparison.OrdinalIgnoreCase))
                    })
                    .ToList();
                _filterContextOptions[col.Key] = options;
            }
            else if (col.FilterType == "enum")
            {
                if (col.EnumOptions != null)
                {
                    _filterContextOptions[col.Key] = col.EnumOptions.Select(e => new ExcelFilterOption
                    {
                        Value = e.Value,
                        Display = e.Display,
                        Count = _allItems.Count(x => string.Equals(GetFilterValue(x, col.Key), e.Value, StringComparison.OrdinalIgnoreCase))
                    }).ToList();
                }
            }
        }
    }

    private static string? GetFilterValue(FixedLengthWorkOrderListDto item, string key) => key switch
    {
        "WorkOrderNo" => item.WorkOrderNo,
        "Salesman" => item.Salesman,
        "CustomerName" => item.CustomerName,
        "SalesOrderNo" => item.SalesOrderNo,
        "ProductionMainNo" => item.ProductionMainNo,
        "ProductionSubNo" => item.ProductionSubNo,
        "PlantGrade" => item.PlantGrade,
        "Specification" => item.Specification,
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState),
        "ScheduleStage" => item.ScheduleStage.ToString(),
        "UrgencyLevel" => item.UrgencyLevel,
        "TotalSurplusStatus" => item.TotalSurplusStatus,
        _ => null
    };

    // ========== 搜索/筛选/排序 ==========

    private void ApplyFiltersAndSort()
    {
        var query = _allItems.AsEnumerable();

        // 关键字搜索
        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            var kw = _searchKeyword.Trim();
            query = query.Where(x =>
                (x.WorkOrderNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.SalesOrderNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.Salesman?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.CustomerName?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.ProductionMainNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.ProductionSubNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.PlantGrade?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.Specification?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.DeliveryStateDisplay.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.ScheduleStageText.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.UrgencyLevel?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.TotalSurplusStatus.Contains(kw, StringComparison.OrdinalIgnoreCase)));
        }

        // 列筛选
        foreach (var kvp in _columnFilters)
        {
            if (kvp.Value.Count == 0) continue;

            var col = _allColumns.FirstOrDefault(c => c.Key == kvp.Key);
            if (col == null) continue;

            if (col.FilterType == "string" || col.FilterType == "enum")
            {
                query = query.Where(x =>
                {
                    var val = GetFilterValue(x, kvp.Key);
                    return val != null && kvp.Value.Contains(val, StringComparer.OrdinalIgnoreCase);
                });
            }
        }

        // 排序
        query = sortColumn switch
        {
            "WorkOrderNo" => sortDescending ? query.OrderByDescending(x => x.WorkOrderNo) : query.OrderBy(x => x.WorkOrderNo),
            "Length" => sortDescending ? query.OrderByDescending(x => x.Length) : query.OrderBy(x => x.Length),
            "PlannedQuantity" => sortDescending ? query.OrderByDescending(x => x.PlannedQuantity) : query.OrderBy(x => x.PlannedQuantity),
            "Salesman" => sortDescending ? query.OrderByDescending(x => x.Salesman) : query.OrderBy(x => x.Salesman),
            "CustomerName" => sortDescending ? query.OrderByDescending(x => x.CustomerName) : query.OrderBy(x => x.CustomerName),
            "SignDate" => sortDescending ? query.OrderByDescending(x => x.SignDate) : query.OrderBy(x => x.SignDate),
            "DeliveryDate" => sortDescending ? query.OrderByDescending(x => x.DeliveryDate) : query.OrderBy(x => x.DeliveryDate),
            "SalesOrderNo" => sortDescending ? query.OrderByDescending(x => x.SalesOrderNo) : query.OrderBy(x => x.SalesOrderNo),
            "ProductionMainNo" => sortDescending ? query.OrderByDescending(x => x.ProductionMainNo) : query.OrderBy(x => x.ProductionMainNo),
            "ProductionSubNo" => sortDescending ? query.OrderByDescending(x => x.ProductionSubNo) : query.OrderBy(x => x.ProductionSubNo),
            "DeliveryState" => sortDescending ? query.OrderByDescending(x => x.DeliveryState) : query.OrderBy(x => x.DeliveryState),
            "PlantGrade" => sortDescending ? query.OrderByDescending(x => x.PlantGrade) : query.OrderBy(x => x.PlantGrade),
            "Specification" => sortDescending ? query.OrderByDescending(x => x.Specification) : query.OrderBy(x => x.Specification),
            "ScheduleStage" => sortDescending ? query.OrderByDescending(x => x.ScheduleStage) : query.OrderBy(x => x.ScheduleStage),
            "UrgencyLevel" => sortDescending ? query.OrderByDescending(x => x.UrgencyLevel) : query.OrderBy(x => x.UrgencyLevel),
            "CutDeadline" => sortDescending ? query.OrderByDescending(x => x.CutDeadline) : query.OrderBy(x => x.CutDeadline),
            "CutQuantity" => sortDescending ? query.OrderByDescending(x => x.CutQuantity) : query.OrderBy(x => x.CutQuantity),
            "CutSurplus" => sortDescending ? query.OrderByDescending(x => x.CutSurplus) : query.OrderBy(x => x.CutSurplus),
            "InspectionDeadline" => sortDescending ? query.OrderByDescending(x => x.InspectionDeadline) : query.OrderBy(x => x.InspectionDeadline),
            "ArrivedQuantity" => sortDescending ? query.OrderByDescending(x => x.ArrivedQuantity) : query.OrderBy(x => x.ArrivedQuantity),
            "DefectQuantity" => sortDescending ? query.OrderByDescending(x => x.DefectQuantity) : query.OrderBy(x => x.DefectQuantity),
            "QualifiedQuantity" => sortDescending ? query.OrderByDescending(x => x.QualifiedQuantity) : query.OrderBy(x => x.QualifiedQuantity),
            "QualifiedSurplus" => sortDescending ? query.OrderByDescending(x => x.QualifiedSurplus) : query.OrderBy(x => x.QualifiedSurplus),
            "MainNoTotalRequirement" => sortDescending ? query.OrderByDescending(x => x.MainNoTotalRequirement) : query.OrderBy(x => x.MainNoTotalRequirement),
            "MainNoTotalInput" => sortDescending ? query.OrderByDescending(x => x.MainNoTotalInput) : query.OrderBy(x => x.MainNoTotalInput),
            "MainNoUncut" => sortDescending ? query.OrderByDescending(x => x.MainNoUncut) : query.OrderBy(x => x.MainNoUncut),
            "MainNoCutTheoretical" => sortDescending ? query.OrderByDescending(x => x.MainNoCutTheoretical) : query.OrderBy(x => x.MainNoCutTheoretical),
            "MainNoCutActual" => sortDescending ? query.OrderByDescending(x => x.MainNoCutActual) : query.OrderBy(x => x.MainNoCutActual),
            "MainNoDefect" => sortDescending ? query.OrderByDescending(x => x.MainNoDefect) : query.OrderBy(x => x.MainNoDefect),
            "TotalSurplus" => sortDescending ? query.OrderByDescending(x => x.TotalSurplus) : query.OrderBy(x => x.TotalSurplus),
            "TotalSurplusStatus" => sortDescending ? query.OrderByDescending(x => x.TotalSurplusStatus) : query.OrderBy(x => x.TotalSurplusStatus),
            _ => sortDescending ? query.OrderByDescending(x => x.WorkOrderNo) : query.OrderBy(x => x.WorkOrderNo)
        };

        _filteredItems = query.ToList();
        ComputePageSums();
    }

    // ========== ExcelFilter 事件 ==========

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        ApplyFiltersAndSort();
        await SavePageStateAsync();
    }

    // ========== 列显隐事件 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SavePageStateAsync();
        await SaveColumnPrefs();
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
        await SaveColumnPrefs();
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
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("fixedlengthview", null, _allColumns);
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
        ApplyFiltersAndSort();
        await SavePageStateAsync();
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        await SavePageStateAsync();
        ApplyFiltersAndSort();
        StateHasChanged();
    }

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        ApplyFiltersAndSort();
        await SavePageStateAsync();
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

        // 从 ColumnPrefsService 恢复列顺序和显隐
        var savedPrefs = await ColumnPrefs.LoadAsync("fixedlengthview", null);
        if (savedPrefs.Count > 0)
        {
            foreach (var s in savedPrefs)
            {
                var match = _allColumns.FirstOrDefault(c => c.Key == s.Key);
                if (match != null)
                    match.Visible = s.Visible;
            }
            var reordered = new List<ColumnDef>();
            foreach (var s in savedPrefs)
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
        var savedState = await PageState.LoadAsync("fixedlengthview");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "WorkOrderNo";
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

        await LoadDataAsync();
    }

    // ========== 分组标题栏同步 ==========

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("initGroupHeaders", "#fixed-length-work-order-list-table");
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(FixedLengthWorkOrderListDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            // G1 基础数据
            case "WorkOrderNo":
                builder.AddContent(0, item.WorkOrderNo);
                break;
            case "Length":
                builder.AddContent(0, item.Length.ToString("G29"));
                break;
            case "PlannedQuantity":
                builder.AddContent(0, item.PlannedQuantity);
                break;
            case "Salesman":
                builder.AddContent(0, item.Salesman);
                break;
            case "CustomerName":
                builder.AddContent(0, item.CustomerName);
                break;
            case "SignDate":
                builder.AddContent(0, item.SignDate.ToString("yyyy-MM-dd"));
                break;
            case "DeliveryDate":
                builder.AddContent(0, item.DeliveryDate.ToString("yyyy-MM-dd"));
                break;
            case "SalesOrderNo":
                builder.AddContent(0, item.SalesOrderNo);
                break;
            case "ProductionMainNo":
                builder.AddContent(0, item.ProductionMainNo);
                break;
            case "ProductionSubNo":
                builder.AddContent(0, item.ProductionSubNo ?? "-");
                break;
            case "DeliveryState":
                builder.AddContent(0, DisplayHelper.GetDeliveryStateText(item.DeliveryState));
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade);
                break;
            case "Specification":
                builder.AddContent(0, item.Specification);
                break;

            // G2 计划状态
            case "ScheduleStage":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetScheduleStageColor(item.ScheduleStage));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.ScheduleStageText)));
                builder.CloseComponent();
                break;
            case "UrgencyLevel":
                builder.AddContent(0, item.UrgencyLevel ?? "-");
                break;

            // G3 成品切割
            case "CutDeadline":
                builder.AddContent(0, item.CutDeadline?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "CutQuantity":
                builder.AddContent(0, item.CutQuantity);
                break;
            case "CutSurplus":
                builder.AddContent(0, FormatSurplus(item.CutSurplus));
                break;

            // G4 成检数据
            case "InspectionDeadline":
                builder.AddContent(0, item.InspectionDeadline?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "ArrivedQuantity":
                builder.AddContent(0, item.ArrivedQuantity);
                break;
            case "DefectQuantity":
                builder.AddContent(0, item.DefectQuantity);
                break;
            case "QualifiedQuantity":
                builder.AddContent(0, item.QualifiedQuantity);
                break;
            case "QualifiedSurplus":
                builder.AddContent(0, FormatSurplus(item.QualifiedSurplus));
                break;

            // G5 总现况分析
            case "MainNoTotalRequirement":
                builder.AddContent(0, item.MainNoTotalRequirement);
                break;
            case "MainNoTotalInput":
                builder.AddContent(0, item.MainNoTotalInput);
                break;
            case "MainNoUncut":
                builder.AddContent(0, item.MainNoUncut);
                break;
            case "MainNoCutTheoretical":
                builder.AddContent(0, item.MainNoCutTheoretical);
                break;
            case "MainNoCutActual":
                builder.AddContent(0, item.MainNoCutActual);
                break;
            case "MainNoDefect":
                builder.AddContent(0, item.MainNoDefect);
                break;
            case "TotalSurplus":
                builder.AddContent(0, FormatSurplus(item.TotalSurplus));
                break;
            case "TotalSurplusStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetSurplusStatusColor(item.TotalSurplusStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.TotalSurplusStatus)));
                builder.CloseComponent();
                break;
        }
    };

    // ========== 显示辅助 ==========

    /// <summary>盈缺支数显示（正数 +N，负数 -N，零显示 0）</summary>
    private static string FormatSurplus(int value) => value > 0 ? $"+{value}" : value.ToString();

    private static Color GetScheduleStageColor(int stage) => stage switch
    {
        0 => Color.Default,
        1 => Color.Warning,
        2 => Color.Success,
        3 => Color.Info,
        _ => Color.Default
    };

    private static Color GetSurplusStatusColor(string status) => status switch
    {
        "合理" => Color.Success,
        "缺少" => Color.Error,
        "略" => Color.Default,
        _ => Color.Default
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
            PageIndex = 1,
            Extras = extras
        };
        await PageState.SaveAsync("fixedlengthview", state);
    }
}
