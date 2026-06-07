using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Pages.Scheduling;

public partial class ColdRollPlans
{
    private MudTable<ColdRollPlanRowDto>? table;
    private List<ColdRollPlanRowDto> _allItems = new();
    private List<ColdRollPlanRowDto> _pageItems = new();
    private string _searchKeyword = string.Empty;
    private bool _isSimplifiedView = false;
    private int _pageSize = 10000;

    // ========== 排序状态 ==========
    private string sortColumn = "ShortDisplay";
    private bool sortDescending = false;

    // ========== 工段筛选 ==========
    private string? _selectedSection;
    private static readonly string[] _sectionTabs = new[]
    {
        "全部", "60冷轧", "50冷轧", "30冷轧", "20冷轧", "三辊冷轧", "冷拔"
    };

    // ========== Tab 汇总数据 ==========
    private int _tabSpecCount;
    private decimal _tabTotalWeight;
    private int _tabKeyBatchCount;
    private decimal _tabKeyBatchWeight;

    // ========== 列定义 ==========
    private static readonly List<ColumnDef> _detailColumns = GetDetailColumnDefs();
    private static readonly List<ColumnDef> _simplifiedColumns = GetSimplifiedColumnDefs();
    private List<ColumnDef> _visibleColumns => _isSimplifiedView ? _simplifiedColumns : _detailColumns;

    // ========== 视图切换 ==========
    private async Task OnSimplifiedViewChanged(bool val)
    {
        _isSimplifiedView = val;
        sortColumn = _isSimplifiedView ? "ShortDisplay" : "ShortDisplay";
        sortDescending = false;
        if (table != null)
            await table.ReloadServerData();
    }

    /// <summary>
    /// 简化视图聚合：按 (ProcessType, ShortDisplay, IsFinished) 分组汇总
    /// </summary>
    private List<ColdRollPlanRowDto> BuildSimplifiedView()
    {
        return _allItems
            .GroupBy(x => new { x.ProcessType, x.ShortDisplay, x.IsFinished })
            .Select(g =>
            {
                var row = new ColdRollPlanRowDto
                {
                    ProcessType = g.Key.ProcessType,
                    ShortDisplay = g.Key.ShortDisplay,
                    IsFinished = g.Key.IsFinished,
                    MergeDisplay = $"{g.Key.ShortDisplay}-{(g.Key.IsFinished ? "成品" : "中间品")}",
                    BatchCount = g.Sum(x => x.BatchCount),
                    KeyBatchCount = g.Sum(x => x.KeyBatchCount),
                    WeightProd = g.Sum(x => x.WeightProd),
                    WeightProdUrgent = g.Sum(x => x.WeightProdUrgent),
                    WeightWaitNearUrgent = g.Sum(x => x.WeightWaitNearUrgent),
                    WeightToday = g.Sum(x => x.WeightToday),
                    WeightTomorrow = g.Sum(x => x.WeightTomorrow),
                    WeightDayAfter = g.Sum(x => x.WeightDayAfter),
                    WeightExt3 = g.Sum(x => x.WeightExt3),
                    WeightExt4 = g.Sum(x => x.WeightExt4),
                    WeightExt5 = g.Sum(x => x.WeightExt5),
                    WeightDistant = g.Sum(x => x.WeightDistant),
                };
                row.WeightWaitNear = row.WeightToday + row.WeightTomorrow + row.WeightDayAfter
                    + row.WeightExt3 + row.WeightExt4 + row.WeightExt5;
                row.WeightTotal = row.WeightProd + row.WeightWaitNear + row.WeightDistant;
                return row;
            })
            .ToList();
    }

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "WeightProd", "WeightProdUrgent", "WeightWaitNear", "WeightWaitNearUrgent",
        "WeightToday", "WeightTomorrow", "WeightDayAfter",
        "WeightExt3", "WeightExt4", "WeightExt5",
        "WeightDistant", "WeightTotal",
    };

    [Inject] private ColdRollPlanService ColdRollSvc { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    protected override Task OnInitializedAsync()
    {
        return Task.CompletedTask;
    }

    // ========== 打印 ==========
    private async Task OnPrint()
    {
        await JS.InvokeVoidAsync("window.print");
    }

    // ========== Tab 切换 ==========
    private async Task OnSectionTabChanged(string? section)
    {
        _selectedSection = section;
        if (table != null)
            await table.ReloadServerData();
    }

    // ========== 搜索 ==========
    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        ApplyFiltersAndSort();
        if (table != null)
            await table.ReloadServerData();
    }

    // ========== 排序 ==========
    private async Task ToggleSort(string key)
    {
        if (sortColumn == key)
            sortDescending = !sortDescending;
        else
        {
            sortColumn = key;
            sortDescending = false;
        }
        ApplyFiltersAndSort();
        if (table != null)
            await table.ReloadServerData();
    }

    private string GetSortIcon(string key)
    {
        if (sortColumn != key) return "";
        return sortDescending ? " ▼" : " ▲";
    }

    // ========== 筛选和排序 ==========
    private void ApplyFiltersAndSort()
    {
        var filtered = _allItems.AsEnumerable();

        // 关键词搜索
        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            var kw = _searchKeyword.Trim();
            filtered = filtered.Where(x =>
                (x.ProcessType?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.BilletSpec?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.RollingSpec?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.MergeDisplay?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.ShortDisplay?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.IsFinished && kw.Contains("成品")) ||
                (!x.IsFinished && kw.Contains("中间品")));
        }

        // 内存排序
        filtered = sortColumn switch
        {
            "ProcessType" => sortDescending
                ? filtered.OrderByDescending(x => x.ProcessType)
                : filtered.OrderBy(x => x.ProcessType),
            "BilletSpec" => sortDescending
                ? filtered.OrderByDescending(x => x.BilletSpec)
                : filtered.OrderBy(x => x.BilletSpec),
            "RollingSpec" => sortDescending
                ? filtered.OrderByDescending(x => x.RollingSpec)
                : filtered.OrderBy(x => x.RollingSpec),
            "IsFinished" => sortDescending
                ? filtered.OrderByDescending(x => x.IsFinished)
                : filtered.OrderBy(x => x.IsFinished),
            "MergeDisplay" => sortDescending
                ? filtered.OrderByDescending(x => x.MergeDisplay)
                : filtered.OrderBy(x => x.MergeDisplay),
            "ShortDisplay" => sortDescending
                ? filtered.OrderByDescending(x => x.ShortDisplay)
                : filtered.OrderBy(x => x.ShortDisplay),
            "WeightProd" => sortDescending
                ? filtered.OrderByDescending(x => x.WeightProd)
                : filtered.OrderBy(x => x.WeightProd),
            "WeightProdUrgent" => sortDescending
                ? filtered.OrderByDescending(x => x.WeightProdUrgent)
                : filtered.OrderBy(x => x.WeightProdUrgent),
            "WeightWaitNear" => sortDescending
                ? filtered.OrderByDescending(x => x.WeightWaitNear)
                : filtered.OrderBy(x => x.WeightWaitNear),
            "WeightWaitNearUrgent" => sortDescending
                ? filtered.OrderByDescending(x => x.WeightWaitNearUrgent)
                : filtered.OrderBy(x => x.WeightWaitNearUrgent),
            "WeightToday" => sortDescending
                ? filtered.OrderByDescending(x => x.WeightToday)
                : filtered.OrderBy(x => x.WeightToday),
            "WeightTomorrow" => sortDescending
                ? filtered.OrderByDescending(x => x.WeightTomorrow)
                : filtered.OrderBy(x => x.WeightTomorrow),
            "WeightDayAfter" => sortDescending
                ? filtered.OrderByDescending(x => x.WeightDayAfter)
                : filtered.OrderBy(x => x.WeightDayAfter),
            "WeightExt3" => sortDescending
                ? filtered.OrderByDescending(x => x.WeightExt3)
                : filtered.OrderBy(x => x.WeightExt3),
            "WeightExt4" => sortDescending
                ? filtered.OrderByDescending(x => x.WeightExt4)
                : filtered.OrderBy(x => x.WeightExt4),
            "WeightExt5" => sortDescending
                ? filtered.OrderByDescending(x => x.WeightExt5)
                : filtered.OrderBy(x => x.WeightExt5),
            "WeightDistant" => sortDescending
                ? filtered.OrderByDescending(x => x.WeightDistant)
                : filtered.OrderBy(x => x.WeightDistant),
            "WeightTotal" => sortDescending
                ? filtered.OrderByDescending(x => x.WeightTotal)
                : filtered.OrderBy(x => x.WeightTotal),
            _ => filtered.OrderBy(x => x.ProcessType)
        };

        _pageItems = filtered.ToList();
    }

    // ========== 加载数据 ==========
    private async Task<TableData<ColdRollPlanRowDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            var data = await ColdRollSvc.GetPlanAsync(_selectedSection);
            _allItems = data;

            // 简化视图时按外径跨度聚合
            if (_isSimplifiedView)
                _allItems = BuildSimplifiedView();

            ApplyFiltersAndSort();

            // 统计数据（从列表行总计直接计算，与展示数据完全一致）
            _tabSpecCount = _allItems.Count;
            _tabTotalWeight = _allItems.Sum(r => r.WeightTotal);
            _tabKeyBatchCount = _allItems.Sum(r => r.KeyBatchCount);
            _tabKeyBatchWeight = _allItems.Sum(r => r.WeightProdUrgent + r.WeightWaitNearUrgent);

            // 计算页脚小计（全量）
            ComputePageSums();

            return new TableData<ColdRollPlanRowDto>
            {
                Items = _pageItems,
                TotalItems = _pageItems.Count,
            };
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载冷轧看板数据失败: {ex.Message}", Severity.Error);
            return new TableData<ColdRollPlanRowDto>
            {
                Items = new List<ColdRollPlanRowDto>(),
                TotalItems = 0,
            };
        }
    }

    // ========== 列定义 ==========
    private static List<ColumnDef> GetDetailColumnDefs()
    {
        // 组1: 规格信息
        var g1 = new List<ColumnDef>
        {
            new() { Key = "ShortDisplay",  Label = "简化",       Width = "100", GroupKey = 1, GroupName = "规格信息" },
            new() { Key = "ProcessType",   Label = "冷轧类型",   Width = "100", GroupKey = 1, GroupName = "规格信息" },
            new() { Key = "BilletSpec",    Label = "轧坯规格",   Width = "120", GroupKey = 1, GroupName = "规格信息" },
            new() { Key = "RollingSpec",   Label = "轧制规格",   Width = "120", GroupKey = 1, GroupName = "规格信息" },
            new() { Key = "IsFinished",    Label = "是否成品",   Width = "100", GroupKey = 1, GroupName = "规格信息" },
            new() { Key = "MergeDisplay",  Label = "合并",       Width = "180", GroupKey = 1, GroupName = "规格信息" },
        };

        // 组2: 近日在轧
        var g2 = new List<ColumnDef>
        {
            new() { Key = "WeightProd",         Label = "近日在轧",        Width = "100", GroupKey = 2, GroupName = "近日在轧" },
            new() { Key = "WeightProdUrgent",   Label = "近日在轧(急管)", Width = "120", GroupKey = 2, GroupName = "近日在轧" },
        };

        // 组3: 近日待轧
        var g3 = new List<ColumnDef>
        {
            new() { Key = "WeightWaitNear",       Label = "近日待轧",        Width = "100", GroupKey = 3, GroupName = "近日待轧" },
            new() { Key = "WeightWaitNearUrgent", Label = "近日待轧(急管)",  Width = "120", GroupKey = 3, GroupName = "近日待轧" },
        };

        // 组4: 待轧分布
        var g4 = new List<ColumnDef>
        {
            new() { Key = "WeightToday",    Label = "待轧今日", Width = "90",  GroupKey = 4, GroupName = "待轧分布" },
            new() { Key = "WeightTomorrow", Label = "待轧明日", Width = "90",  GroupKey = 4, GroupName = "待轧分布" },
            new() { Key = "WeightDayAfter", Label = "待轧后日", Width = "90",  GroupKey = 4, GroupName = "待轧分布" },
            new() { Key = "WeightExt3",     Label = "待轧延3",  Width = "80",  GroupKey = 4, GroupName = "待轧分布" },
            new() { Key = "WeightExt4",     Label = "待轧延4",  Width = "80",  GroupKey = 4, GroupName = "待轧分布" },
            new() { Key = "WeightExt5",     Label = "待轧延5",  Width = "80",  GroupKey = 4, GroupName = "待轧分布" },
            new() { Key = "WeightDistant",  Label = "远日量",    Width = "90",  GroupKey = 4, GroupName = "待轧分布" },
        };

        // 组5: 合计
        var g5 = new List<ColumnDef>
        {
            new() { Key = "WeightTotal",    Label = "工艺总量",  Width = "100", GroupKey = 5, GroupName = "合计" },
        };

        return g1.Concat(g2).Concat(g3).Concat(g4).Concat(g5).ToList();
    }

    private static List<ColumnDef> GetSimplifiedColumnDefs()
    {
        // 简化视图：以外径范围聚合，不展示具体规格
        var g1 = new List<ColumnDef>
        {
            new() { Key = "ShortDisplay",  Label = "外径跨度",  Width = "100", GroupKey = 1, GroupName = "规格信息" },
            new() { Key = "ProcessType",   Label = "冷轧类型",  Width = "100", GroupKey = 1, GroupName = "规格信息" },
            new() { Key = "IsFinished",    Label = "是否成品",  Width = "100", GroupKey = 1, GroupName = "规格信息" },
        };

        // 组2: 近日在轧
        var g2 = new List<ColumnDef>
        {
            new() { Key = "WeightProd",         Label = "近日在轧",        Width = "100", GroupKey = 2, GroupName = "近日在轧" },
            new() { Key = "WeightProdUrgent",   Label = "近日在轧(急管)", Width = "120", GroupKey = 2, GroupName = "近日在轧" },
        };

        // 组3: 近日待轧
        var g3 = new List<ColumnDef>
        {
            new() { Key = "WeightWaitNear",       Label = "近日待轧",        Width = "100", GroupKey = 3, GroupName = "近日待轧" },
            new() { Key = "WeightWaitNearUrgent", Label = "近日待轧(急管)",  Width = "120", GroupKey = 3, GroupName = "近日待轧" },
        };

        // 组4: 待轧分布
        var g4 = new List<ColumnDef>
        {
            new() { Key = "WeightToday",    Label = "待轧今日", Width = "90",  GroupKey = 4, GroupName = "待轧分布" },
            new() { Key = "WeightTomorrow", Label = "待轧明日", Width = "90",  GroupKey = 4, GroupName = "待轧分布" },
            new() { Key = "WeightDayAfter", Label = "待轧后日", Width = "90",  GroupKey = 4, GroupName = "待轧分布" },
            new() { Key = "WeightExt3",     Label = "待轧延3",  Width = "80",  GroupKey = 4, GroupName = "待轧分布" },
            new() { Key = "WeightExt4",     Label = "待轧延4",  Width = "80",  GroupKey = 4, GroupName = "待轧分布" },
            new() { Key = "WeightExt5",     Label = "待轧延5",  Width = "80",  GroupKey = 4, GroupName = "待轧分布" },
            new() { Key = "WeightDistant",  Label = "远日量",    Width = "90",  GroupKey = 4, GroupName = "待轧分布" },
        };

        // 组5: 合计
        var g5 = new List<ColumnDef>
        {
            new() { Key = "WeightTotal",    Label = "工艺总量",  Width = "100", GroupKey = 5, GroupName = "合计" },
        };

        return g1.Concat(g2).Concat(g3).Concat(g4).Concat(g5).ToList();
    }

    // ========== 单元格渲染 ==========
    private string RenderCell(ColdRollPlanRowDto item, ColumnDef col)
    {
        return col.Key switch
        {
            "IsFinished" => item.IsFinished ? "成品" : "中间品",
            "WeightProd" or "WeightProdUrgent" or "WeightWaitNear" or "WeightWaitNearUrgent"
                or "WeightToday" or "WeightTomorrow" or "WeightDayAfter"
                or "WeightExt3" or "WeightExt4" or "WeightExt5"
                or "WeightDistant" or "WeightTotal" => GetWeightDisplay(col, item),
            _ => GetStringValue(item, col.Key),
        };
    }

    /// <summary>
    /// 获取单元格额外样式：急管列红色高亮
    /// </summary>
    private static string GetCellExtraClass(ColumnDef col)
    {
        return col.Key switch
        {
            "WeightProdUrgent" or "WeightWaitNearUrgent" => "urgent-cell",
            _ => "",
        };
    }

    private static string GetWeightDisplay(ColumnDef col, ColdRollPlanRowDto item)
    {
        var val = col.Key switch
        {
            "WeightProd" => item.WeightProd,
            "WeightProdUrgent" => item.WeightProdUrgent,
            "WeightWaitNear" => item.WeightWaitNear,
            "WeightWaitNearUrgent" => item.WeightWaitNearUrgent,
            "WeightToday" => item.WeightToday,
            "WeightTomorrow" => item.WeightTomorrow,
            "WeightDayAfter" => item.WeightDayAfter,
            "WeightExt3" => item.WeightExt3,
            "WeightExt4" => item.WeightExt4,
            "WeightExt5" => item.WeightExt5,
            "WeightDistant" => item.WeightDistant,
            "WeightTotal" => item.WeightTotal,
            _ => 0m,
        };
        return val == 0 ? "" : ((int)val).ToString();
    }

    private static string GetStringValue(ColdRollPlanRowDto item, string key)
    {
        return key switch
        {
            "ProcessType" => item.ProcessType,
            "BilletSpec" => item.BilletSpec,
            "RollingSpec" => item.RollingSpec,
            "MergeDisplay" => item.MergeDisplay,
            "ShortDisplay" => item.ShortDisplay,
            _ => "",
        };
    }

    // ========== 分组标题 ==========
    private List<(string GroupName, int TotalWidth, string CssClass)> GetGroupHeaders()
    {
        var groups = new List<(string GroupName, int TotalWidth, string CssClass)>();
        var currentGroup = (Name: "", Width: 0);
        var groupIndex = 0;

        foreach (var col in _visibleColumns)
        {
            if (col.GroupKey == null) continue;
            var colWidth = int.TryParse(col.Width, out var w) ? w : 100;
            var groupName = col.GroupName ?? "";

            if (groupName != currentGroup.Name)
            {
                if (currentGroup.Width > 0)
                {
                    var cssClass = groupIndex % 2 == 0 ? "col-group-even" : "col-group-odd";
                    groups.Add((currentGroup.Name, currentGroup.Width, cssClass));
                    groupIndex++;
                }
                currentGroup = (groupName, colWidth);
            }
            else
            {
                currentGroup.Width += colWidth;
            }
        }

        if (currentGroup.Width > 0)
        {
            var cssClass = groupIndex % 2 == 0 ? "col-group-even" : "col-group-odd";
            groups.Add((currentGroup.Name, currentGroup.Width, cssClass));
        }

        return groups;
    }

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var css = isGroupStart ? "col-group-start " : "";
        var evenOdd = (groupKey ?? 0) % 2 == 0 ? "col-group-even" : "col-group-odd";
        return css + evenOdd;
    }

    private static string GetCellGroupCss(int? groupKey, bool isGroupStart)
    {
        var css = isGroupStart ? "col-group-start " : "";
        var evenOdd = (groupKey ?? 0) % 2 == 0 ? "col-cell-even" : "col-cell-odd";
        return css + evenOdd;
    }

    // ========== 页脚汇总 ==========
    private void ComputePageSums()
    {
        _pageSums = new Dictionary<string, string>();
        if (_pageItems.Count == 0) return;

        foreach (var key in _summableColumnKeys)
        {
            decimal sum = 0;
            foreach (var item in _pageItems)
            {
                sum += key switch
                {
                    "WeightProd" => item.WeightProd,
                    "WeightProdUrgent" => item.WeightProdUrgent,
                    "WeightWaitNear" => item.WeightWaitNear,
                    "WeightWaitNearUrgent" => item.WeightWaitNearUrgent,
                    "WeightToday" => item.WeightToday,
                    "WeightTomorrow" => item.WeightTomorrow,
                    "WeightDayAfter" => item.WeightDayAfter,
                    "WeightExt3" => item.WeightExt3,
                    "WeightExt4" => item.WeightExt4,
                    "WeightExt5" => item.WeightExt5,
                    "WeightDistant" => item.WeightDistant,
                    "WeightTotal" => item.WeightTotal,
                    _ => 0m,
                };
            }
            _pageSums[key] = sum == 0 ? "" : ((int)sum).ToString();
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        return _pageSums.GetValueOrDefault(col.Key, "");
    }
}
