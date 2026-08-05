using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs.Shared;
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

    // 打印选中集合
    private HashSet<FixedLengthWorkOrderListDto> _selectedItems = new();

    // B33: 分页汇总（按当前页显示行求和）
    // 可汇总列：G1 需求支数 / G3 切后支数 / G4 成检支数列 / G5 入库支数列；
    // G6 主号级聚合值同主号多行重复，且为冗余重复值，不参与求和。
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        // G1 基础数据
        "PlannedQuantity",
        // G3 成品切割
        "CutQuantity",
        // G4 成检数据
        "ArrivedQuantity", "CutArrivedQuantity", "NonCutArrivedQuantity",
        "DefectQuantity", "QualifiedQuantity", "QualifiedSurplus",
        // G5 成品入库
        "InboundQuantity", "InboundSurplus"
    };

    // 汇总缓存标记：分页导航/页大小切换/筛选后仅重算一次
    private int _lastSummedPage = -1;
    private int _lastSummedCount = -1;
    private int _lastSummedPageSize = -1;

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
            new() { Key = "ScheduleStage",      Label = "关注状态",     SortKey = "ScheduleStage",     FilterType = "enum", Width = "120", EnumOptions = new() { new("0","主号暂停"), new("1","主号完成"), new("2","原料锁定"), new("3","生产执行"), new("4","成品检验") }, GroupKey = 2, GroupName = "计划状态" },
            new() { Key = "UrgencyLevel",       Label = "工单计划性",   SortKey = "UrgencyLevel",      FilterType = "string", Width = "120", GroupKey = 2, GroupName = "计划状态" },
        };

        // G3: 成品切割执行
        var g3 = new List<ColumnDef>
        {
            new() { Key = "CutDeadline",        Label = "切割截止日",   SortKey = "CutDeadline",       Width = "120", GroupKey = 3, GroupName = "成品切割" },
            new() { Key = "CutQuantity",        Label = "切后支数",     SortKey = "CutQuantity",       Width = "80",  GroupKey = 3, GroupName = "成品切割" },
        };

        // G4: 成检数据
        var g4 = new List<ColumnDef>
        {
            new() { Key = "InspectionDeadline", Label = "成检截止日",   SortKey = "InspectionDeadline",Width = "120", GroupKey = 4, GroupName = "成检数据" },
            new() { Key = "ArrivedQuantity",    Label = "到料总支",     SortKey = "ArrivedQuantity",   Width = "80",  GroupKey = 4, GroupName = "成检数据" },
            new() { Key = "CutArrivedQuantity", Label = "成切到料支",   SortKey = "CutArrivedQuantity",Width = "80",  GroupKey = 4, GroupName = "成检数据" },
            new() { Key = "NonCutArrivedQuantity", Label = "非成切到料支", SortKey = "NonCutArrivedQuantity", Width = "80", GroupKey = 4, GroupName = "成检数据" },
            new() { Key = "DefectQuantity",     Label = "次品支数",     SortKey = "DefectQuantity",    Width = "80",  GroupKey = 4, GroupName = "成检数据" },
            new() { Key = "QualifiedQuantity",  Label = "合格支数",     SortKey = "QualifiedQuantity", Width = "80",  GroupKey = 4, GroupName = "成检数据" },
            new() { Key = "QualifiedSurplus",   Label = "合格盈缺",     SortKey = "QualifiedSurplus",  Width = "80",  GroupKey = 4, GroupName = "成检数据" },
        };

        // G5: 成品入库
        var g5 = new List<ColumnDef>
        {
            new() { Key = "InboundDeadline",  Label = "入库截止日",   SortKey = "InboundDeadline",  Width = "120", GroupKey = 5, GroupName = "成品入库" },
            new() { Key = "InboundQuantity",  Label = "入库支数",     SortKey = "InboundQuantity",  Width = "80",  GroupKey = 5, GroupName = "成品入库" },
            new() { Key = "InboundSurplus",   Label = "入库盈缺",     SortKey = "InboundSurplus",   Width = "80",  GroupKey = 5, GroupName = "成品入库" },
            new() { Key = "InboundDoubt",     Label = "入库存疑",     SortKey = "InboundDoubt",     FilterType = "enum", Width = "90", EnumOptions = new() { new("正常","正常"), new("疑问","疑问") }, GroupKey = 5, GroupName = "成品入库" },
        };

        // G6: 主号数据及现况分析（主号级）
        var g6 = new List<ColumnDef>
        {
            new() { Key = "MainNoTotalRequirement", Label = "需求计划总", SortKey = "MainNoTotalRequirement", Width = "80", Visible = false, GroupKey = 6, GroupName = "主号数据及现况分析" },
            new() { Key = "MainNoTotalInput",       Label = "理论成品总", SortKey = "MainNoTotalInput",       Width = "80", Visible = false, GroupKey = 6, GroupName = "主号数据及现况分析" },
            new() { Key = "MainNoNoCutQty",        Label = "免切理论支", SortKey = "MainNoNoCutQty",        Width = "80", Visible = false, GroupKey = 6, GroupName = "主号数据及现况分析" },
            new() { Key = "MainNoNeedCutUncutQty", Label = "待切理论支", SortKey = "MainNoNeedCutUncutQty", Width = "80", Visible = false, GroupKey = 6, GroupName = "主号数据及现况分析" },
            new() { Key = "MainNoCutTheoretical",   Label = "已切理论支", SortKey = "MainNoCutTheoretical",  Width = "80", Visible = false, GroupKey = 6, GroupName = "主号数据及现况分析" },
            new() { Key = "MainNoCutActual",        Label = "实切支数",   SortKey = "MainNoCutActual",       Width = "80", Visible = false, GroupKey = 6, GroupName = "主号数据及现况分析" },
            new() { Key = "MainNoCutRationality",   Label = "切割偏差判定", SortKey = "MainNoCutRationality", FilterType = "enum", Width = "90", EnumOptions = new() { new("正常","正常"), new("异常","异常"), new("略","略") }, GroupKey = 6, GroupName = "主号数据及现况分析" },
            new() { Key = "EstimatedLossQty",      Label = "预计损耗支",   SortKey = "EstimatedLossQty",     Width = "80", Visible = false, GroupKey = 6, GroupName = "主号数据及现况分析" },
            new() { Key = "MainNoCurrentInput",     Label = "当前理论产出", SortKey = "MainNoCurrentInput",   Width = "80", Visible = false, GroupKey = 6, GroupName = "主号数据及现况分析" },
            new() { Key = "MainNoDefect",           Label = "次品支数",   SortKey = "MainNoDefect",          Width = "80", Visible = false, GroupKey = 6, GroupName = "主号数据及现况分析" },
            new() { Key = "TotalSurplus",           Label = "盈亏支数",   SortKey = "TotalSurplus",          Width = "80", GroupKey = 6, GroupName = "主号数据及现况分析" },
            new() { Key = "TotalSurplusStatus",     Label = "盈亏状态",   SortKey = "TotalSurplusStatus",    FilterType = "enum", Width = "100", EnumOptions = new() { new("合理","合理"), new("缺少","缺少"), new("略","略") }, GroupKey = 6, GroupName = "主号数据及现况分析" },
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
                    var sum = pageItems.Sum(item => (int)(prop.GetValue(item) ?? 0));
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal))
                {
                    var sum = pageItems.Sum(item => (decimal)(prop.GetValue(item) ?? 0m));
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
                (x.TotalSurplusStatus.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.MainNoCutRationality.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.InboundDoubt.Contains(kw, StringComparison.OrdinalIgnoreCase)));
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
            "InspectionDeadline" => sortDescending ? query.OrderByDescending(x => x.InspectionDeadline) : query.OrderBy(x => x.InspectionDeadline),
            "ArrivedQuantity" => sortDescending ? query.OrderByDescending(x => x.ArrivedQuantity) : query.OrderBy(x => x.ArrivedQuantity),
            "CutArrivedQuantity" => sortDescending ? query.OrderByDescending(x => x.CutArrivedQuantity) : query.OrderBy(x => x.CutArrivedQuantity),
            "NonCutArrivedQuantity" => sortDescending ? query.OrderByDescending(x => x.NonCutArrivedQuantity) : query.OrderBy(x => x.NonCutArrivedQuantity),
            "DefectQuantity" => sortDescending ? query.OrderByDescending(x => x.DefectQuantity) : query.OrderBy(x => x.DefectQuantity),
            "QualifiedQuantity" => sortDescending ? query.OrderByDescending(x => x.QualifiedQuantity) : query.OrderBy(x => x.QualifiedQuantity),
            "QualifiedSurplus" => sortDescending ? query.OrderByDescending(x => x.QualifiedSurplus) : query.OrderBy(x => x.QualifiedSurplus),
            "InboundDeadline" => sortDescending ? query.OrderByDescending(x => x.InboundDeadline) : query.OrderBy(x => x.InboundDeadline),
            "InboundQuantity" => sortDescending ? query.OrderByDescending(x => x.InboundQuantity) : query.OrderBy(x => x.InboundQuantity),
            "InboundSurplus" => sortDescending ? query.OrderByDescending(x => x.InboundSurplus) : query.OrderBy(x => x.InboundSurplus),
            "InboundDoubt" => sortDescending ? query.OrderByDescending(x => x.InboundDoubt) : query.OrderBy(x => x.InboundDoubt),
            "MainNoTotalRequirement" => sortDescending ? query.OrderByDescending(x => x.MainNoTotalRequirement) : query.OrderBy(x => x.MainNoTotalRequirement),
            "MainNoTotalInput" => sortDescending ? query.OrderByDescending(x => x.MainNoTotalInput) : query.OrderBy(x => x.MainNoTotalInput),
            "MainNoNoCutQty" => sortDescending ? query.OrderByDescending(x => x.MainNoNoCutQty) : query.OrderBy(x => x.MainNoNoCutQty),
            "MainNoNeedCutUncutQty" => sortDescending ? query.OrderByDescending(x => x.MainNoNeedCutUncutQty) : query.OrderBy(x => x.MainNoNeedCutUncutQty),
            "MainNoCutTheoretical" => sortDescending ? query.OrderByDescending(x => x.MainNoCutTheoretical) : query.OrderBy(x => x.MainNoCutTheoretical),
            "MainNoCutActual" => sortDescending ? query.OrderByDescending(x => x.MainNoCutActual) : query.OrderBy(x => x.MainNoCutActual),
            "EstimatedLossQty" => sortDescending ? query.OrderByDescending(x => x.EstimatedLossQty) : query.OrderBy(x => x.EstimatedLossQty),
            "MainNoCurrentInput" => sortDescending ? query.OrderByDescending(x => x.MainNoCurrentInput) : query.OrderBy(x => x.MainNoCurrentInput),
            "MainNoDefect" => sortDescending ? query.OrderByDescending(x => x.MainNoDefect) : query.OrderBy(x => x.MainNoDefect),
            "TotalSurplus" => sortDescending ? query.OrderByDescending(x => x.TotalSurplus) : query.OrderBy(x => x.TotalSurplus),
            "TotalSurplusStatus" => sortDescending ? query.OrderByDescending(x => x.TotalSurplusStatus) : query.OrderBy(x => x.TotalSurplusStatus),
            "MainNoCutRationality" => sortDescending ? query.OrderByDescending(x => x.MainNoCutRationality) : query.OrderBy(x => x.MainNoCutRationality),
            _ => sortDescending ? query.OrderByDescending(x => x.WorkOrderNo) : query.OrderBy(x => x.WorkOrderNo)
        };

        _filteredItems = query.ToList();

        // 筛选/排序/搜索后回到第一页，避免停留在旧页码产生空页
        if (table != null)
            table.CurrentPage = 0;

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

        // 分页导航/页大小切换后重算当前页汇总（pager 操作只改 CurrentPage/RowsPerPage，不触发 ApplyFiltersAndSort）
        if (table != null && !_isLoading && _filteredItems.Count > 0)
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
                builder.AddContent(0, FormatQuantity(item.PlannedQuantity));
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
                builder.AddContent(0, FormatQuantity(item.CutQuantity));
                break;

            // G4 成检数据
            case "InspectionDeadline":
                builder.AddContent(0, item.InspectionDeadline?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "ArrivedQuantity":
                builder.AddContent(0, FormatQuantity(item.ArrivedQuantity));
                break;
            case "CutArrivedQuantity":
                builder.AddContent(0, FormatQuantity(item.CutArrivedQuantity));
                break;
            case "NonCutArrivedQuantity":
                builder.AddContent(0, FormatQuantity(item.NonCutArrivedQuantity));
                break;
            case "DefectQuantity":
                builder.AddContent(0, FormatQuantity(item.DefectQuantity));
                break;
            case "QualifiedQuantity":
                builder.AddContent(0, FormatQuantity(item.QualifiedQuantity));
                break;
            case "QualifiedSurplus":
                builder.AddContent(0, FormatSurplus(item.QualifiedSurplus));
                break;

            // G5 成品入库
            case "InboundDeadline":
                builder.AddContent(0, item.InboundDeadline?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "InboundQuantity":
                builder.AddContent(0, FormatQuantity(item.InboundQuantity));
                break;
            case "InboundSurplus":
                builder.AddContent(0, FormatSurplus(item.InboundSurplus));
                break;
            case "InboundDoubt":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetInboundDoubtColor(item.InboundDoubt));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.InboundDoubt)));
                builder.CloseComponent();
                break;

            // G6 主号数据及现况分析
            case "MainNoTotalRequirement":
                builder.AddContent(0, FormatQuantity(item.MainNoTotalRequirement));
                break;
            case "MainNoTotalInput":
                builder.AddContent(0, FormatQuantity(item.MainNoTotalInput));
                break;
            case "MainNoNoCutQty":
                builder.AddContent(0, FormatQuantity(item.MainNoNoCutQty));
                break;
            case "MainNoNeedCutUncutQty":
                builder.AddContent(0, FormatQuantity(item.MainNoNeedCutUncutQty));
                break;
            case "MainNoCutTheoretical":
                builder.AddContent(0, FormatQuantity(item.MainNoCutTheoretical));
                break;
            case "MainNoCutActual":
                builder.AddContent(0, FormatQuantity(item.MainNoCutActual));
                break;
            case "MainNoCutRationality":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetCutRationalityColor(item.MainNoCutRationality));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.MainNoCutRationality)));
                builder.CloseComponent();
                break;
            case "EstimatedLossQty":
                builder.AddContent(0, FormatQuantity(item.EstimatedLossQty));
                break;
            case "MainNoCurrentInput":
                builder.AddContent(0, FormatQuantity(item.MainNoCurrentInput));
                break;
            case "MainNoDefect":
                builder.AddContent(0, FormatQuantity(item.MainNoDefect));
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

    /// <summary>单元格对齐：数值类字段居中，其它字段靠左</summary>
    private static string GetAlignClass(ColumnDef col) => col.Key switch
    {
        "Length" or "PlannedQuantity" or
        "CutQuantity" or
        "ArrivedQuantity" or "CutArrivedQuantity" or "NonCutArrivedQuantity" or
        "DefectQuantity" or "QualifiedQuantity" or "QualifiedSurplus" or
        "InboundQuantity" or "InboundSurplus" or
        "MainNoTotalRequirement" or "MainNoTotalInput" or "MainNoNoCutQty" or
        "MainNoNeedCutUncutQty" or "MainNoCutTheoretical" or "MainNoCutActual" or
        "EstimatedLossQty" or "MainNoCurrentInput" or "MainNoDefect" or "TotalSurplus" => "text-center",
        _ => ""
    };

    /// <summary>执行量类数值显示：0 表示该环节未发生，显示 "-" 减少视觉污染</summary>
    private static string FormatQuantity(int value) => value == 0 ? "-" : value.ToString();

    /// <summary>盈缺支数显示（正数 +N，负数 -N，零显示 0）</summary>
    private static string FormatSurplus(int value) => value > 0 ? $"+{value}" : value.ToString();

    private static Color GetScheduleStageColor(int stage) => stage switch
    {
        0 => Color.Error,       // 主号暂停
        1 => Color.Success,     // 主号完成（闭环）
        2 => Color.Warning,     // 原料锁定（待料）
        3 => Color.Info,        // 生产执行
        4 => Color.Primary,     // 成品检验
        _ => Color.Default
    };

    private static Color GetSurplusStatusColor(string status) => status switch
    {
        "合理" => Color.Success,
        "缺少" => Color.Error,
        "略" => Color.Default,
        _ => Color.Default
    };

    private static Color GetCutRationalityColor(string status) => status switch
    {
        "正常" => Color.Success,
        "异常" => Color.Error,
        "略" => Color.Default,
        _ => Color.Default
    };

    private static Color GetInboundDoubtColor(string status) => status switch
    {
        "正常" => Color.Success,
        "疑问" => Color.Warning,
        _ => Color.Default
    };

    // ========== 选择列 ==========

    private void SelectAllItems(bool selected)
    {
        if (selected)
            _selectedItems = new HashSet<FixedLengthWorkOrderListDto>(_filteredItems);
        else
            _selectedItems.Clear();
    }

    private void ToggleSelection(FixedLengthWorkOrderListDto item, bool selected)
    {
        if (selected)
            _selectedItems.Add(item);
        else
            _selectedItems.Remove(item);
    }

    // ========== 打印 ==========

    /// <summary>打印全部（当前筛选后全部行）</summary>
    private async Task PrintAll()
    {
        if (_filteredItems.Count == 0)
        {
            Snackbar.Add("当前无数据可打印", Severity.Warning);
            return;
        }
        await PrintItems(_filteredItems, "定尺工单定尺数据（全部）");
    }

    /// <summary>打印选中行</summary>
    private async Task PrintSelected()
    {
        if (_selectedItems.Count == 0)
        {
            Snackbar.Add("请先选择要打印的行", Severity.Warning);
            return;
        }
        await PrintItems(_selectedItems.ToList(), "定尺工单定尺数据（选中）");
    }

    private async Task PrintItems(List<FixedLengthWorkOrderListDto> items, string title)
    {
        try
        {
            var printColumns = _visibleColumns
                .Select(c => new PrintColumnDef
                {
                    Key = c.Key,
                    Label = c.Label,
                    Width = int.TryParse(c.Width, out var w) ? w : 100
                })
                .ToList();

            // DTO → 字典，枚举字段预先解析为中文显示文本（Mode B ⓪）
            var printItems = items.Select(item =>
            {
                var dict = new Dictionary<string, object>();
                foreach (var col in _visibleColumns)
                    dict[col.Key] = ResolvePrintValue(item, col.Key);
                return dict;
            }).ToList();

            var request = new FixedLengthWorkOrderPrintRequest
            {
                Title = title,
                Items = printItems,
                Columns = printColumns
            };

            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/fixed-length-work-order/print-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private static object ResolvePrintValue(FixedLengthWorkOrderListDto item, string key) => key switch
    {
        // 枚举/状态字段 → 中文显示文本
        "DeliveryState" => item.DeliveryStateDisplay,
        "ScheduleStage" => item.ScheduleStageText,
        "MainNoCutRationality" => item.MainNoCutRationality,
        "TotalSurplusStatus" => item.TotalSurplusStatus,
        "InboundDoubt" => item.InboundDoubt,
        // 其余字段原样输出（TablePrintHelper 自动处理 decimal→G29、DateTime→yyyy-MM-dd 等）
        _ => GetRawPropertyValue(item, key)!
    };

    private static object? GetRawPropertyValue(FixedLengthWorkOrderListDto item, string key) => key switch
    {
        // G1 基础数据
        "WorkOrderNo" => item.WorkOrderNo ?? "",
        "Length" => item.Length,
        "PlannedQuantity" => item.PlannedQuantity,
        "Salesman" => item.Salesman ?? "",
        "CustomerName" => item.CustomerName ?? "",
        "SignDate" => item.SignDate,
        "DeliveryDate" => item.DeliveryDate,
        "SalesOrderNo" => item.SalesOrderNo ?? "",
        "ProductionMainNo" => item.ProductionMainNo ?? "",
        "ProductionSubNo" => item.ProductionSubNo ?? "",
        "PlantGrade" => item.PlantGrade ?? "",
        "Specification" => item.Specification ?? "",
        // G2 计划状态
        "UrgencyLevel" => item.UrgencyLevel ?? "",
        // G3 成品切割
        "CutDeadline" => item.CutDeadline,
        "CutQuantity" => item.CutQuantity,
        // G4 成检数据
        "InspectionDeadline" => item.InspectionDeadline,
        "ArrivedQuantity" => item.ArrivedQuantity,
        "CutArrivedQuantity" => item.CutArrivedQuantity,
        "NonCutArrivedQuantity" => item.NonCutArrivedQuantity,
        "DefectQuantity" => item.DefectQuantity,
        "QualifiedQuantity" => item.QualifiedQuantity,
        "QualifiedSurplus" => item.QualifiedSurplus,
        // G5 成品入库
        "InboundDeadline" => item.InboundDeadline,
        "InboundQuantity" => item.InboundQuantity,
        "InboundSurplus" => item.InboundSurplus,
        // G6 主号数据及现况分析
        "MainNoTotalRequirement" => item.MainNoTotalRequirement,
        "MainNoTotalInput" => item.MainNoTotalInput,
        "MainNoNoCutQty" => item.MainNoNoCutQty,
        "MainNoNeedCutUncutQty" => item.MainNoNeedCutUncutQty,
        "MainNoCutTheoretical" => item.MainNoCutTheoretical,
        "MainNoCutActual" => item.MainNoCutActual,
        "EstimatedLossQty" => item.EstimatedLossQty,
        "MainNoCurrentInput" => item.MainNoCurrentInput,
        "MainNoDefect" => item.MainNoDefect,
        "TotalSurplus" => item.TotalSurplus,
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
            PageIndex = 1,
            Extras = extras
        };
        await PageState.SaveAsync("fixedlengthview", state);
    }
}
