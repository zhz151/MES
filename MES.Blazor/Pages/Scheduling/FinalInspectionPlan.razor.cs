using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs;
using System.Text.Json;

namespace MES.Blazor.Pages.Scheduling;

public partial class FinalInspectionPlan
{
    private MudTable<FinalInspectionPlanDto>? table;
    private List<FinalInspectionPlanDto> _pageItems = new();
    private int _totalCount;
    private int _restoredPageIndex;
    private int _currentPageIndex = 1;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    // 排序
    private string sortColumn = "BatchNo";
    private bool sortDescending;

    // 三档 Tab
    private string? _selectedTab = "待到料";
    private static readonly string[] _tabs = { "全部", "待到料", "待检验", "检验中" };

    // Tab 汇总
    private int _tabCount;
    private decimal _tabTotalWeight;

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
        "CurrentValidWeight"
    };

    // 全量数据（加载后缓存）
    private List<FinalInspectionPlanDto> _allItems = new();

    // ========== 列定义 ==========

    private static List<ColumnDef> GetAllColumnDefs()
    {
        // G1: 批次信息
        var g1 = new List<ColumnDef>
        {
            new() { Key = "BatchNo",              Label = "生产编号",   SortKey = "BatchNo",              FilterType = "string", Width = "130", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "TagNo",                Label = "挂牌号",     SortKey = "TagNo",                FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "PlantGrade",            Label = "原料钢号",   SortKey = "PlantGrade",            FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "CurrentValidWeight",    Label = "重量(kg)",   SortKey = "CurrentValidWeight",    Width = "80",  GroupKey = 1, GroupName = "批次信息" },
        };

        // G2: 关联工单
        var g2 = new List<ColumnDef>
        {
            new() { Key = "WorkOrderNo",           Label = "工单号",     SortKey = "WorkOrderNo",           FilterType = "string", Width = "130", GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "Salesman",              Label = "业务员",     SortKey = "Salesman",              FilterType = "string", Width = "100", GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "Specification",         Label = "成品规格",   SortKey = "Specification",         FilterType = "string", Width = "130", GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "LengthStatus",          Label = "长度状态",   SortKey = "LengthStatus",          FilterType = "enum", Width = "100", EnumOptions = new() { new("Fixed","定尺"), new("Range","范围尺"), new("NonFixed","非定尺") }, GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "MinLength",             Label = "最小长度",   SortKey = "MinLength",             Width = "80",  GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "MaxLength",             Label = "最大长度",   SortKey = "MaxLength",             Width = "80",  GroupKey = 2, GroupName = "关联工单" },
        };

        // G3: 排程信息
        var g3 = new List<ColumnDef>
        {
            new() { Key = "ScheduleStage",         Label = "排程阶段",   SortKey = "ScheduleStage",         FilterType = "enum", Width = "110", EnumOptions = new() { new("0","工单完成"), new("1","原料锁定"), new("2","生产执行"), new("3","成品检验") }, GroupKey = 3, GroupName = "排程信息" },
            new() { Key = "UrgencyLevel",          Label = "紧急程度",   SortKey = "UrgencyLevel",          FilterType = "enum", Width = "90",  EnumOptions = new() { new("A+急","A+急"), new("A急","A急"), new("B顺","B顺"), new("C缓","C缓"), new("D缓","D缓") }, GroupKey = 3, GroupName = "排程信息" },
        };

        // G4: 成检状态
        var g4 = new List<ColumnDef>
        {
            new() { Key = "KanbanStage",           Label = "成检阶段",   FilterType = "enum", Width = "100", EnumOptions = new() { new("待到料","待到料"), new("待检验","待检验"), new("检验中","检验中") }, GroupKey = 4, GroupName = "成检状态" },
            new() { Key = "ReceiveDate",           Label = "到料日期",   SortKey = "ReceiveDate",           Width = "110", GroupKey = 4, GroupName = "成检状态" },
            new() { Key = "MaxInspectionDate",     Label = "最晚检验",   SortKey = "MaxInspectionDate",     Width = "110", GroupKey = 4, GroupName = "成检状态" },
        };

        var all = new List<ColumnDef>();
        all.AddRange(g1);
        all.AddRange(g2);
        all.AddRange(g3);
        all.AddRange(g4);
        return all;
    }

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

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

    // ========== 生命周期 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();

        var savedState = await PageState.LoadAsync("final-inspection-plan");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "BatchNo";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = savedState.PageIndex;

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

        if (savedState != null && table != null)
            await table.ReloadServerData();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("initGroupHeaders", "#final-inspection-plan-table");
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _allItems = await KanbanSvc.GetKanbanAsync();
            UpdateTabSummary();
            BuildFilterOptionsFromData();
            if (table != null) await table.ReloadServerData();
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
        }
    }

    private static string? GetFilterValue(FinalInspectionPlanDto item, string key) => key switch
    {
        "BatchNo" => item.BatchNo,
        "TagNo" => item.TagNo,
        "PlantGrade" => item.PlantGrade,
        "WorkOrderNo" => item.WorkOrderNo,
        "Salesman" => item.Salesman,
        "Specification" => item.Specification,
        "LengthStatus" => item.LengthStatus,
        "ScheduleStage" => item.ScheduleStage.ToString(),
        "UrgencyLevel" => item.UrgencyLevel,
        "KanbanStage" => item.KanbanStage,
        _ => null
    };

    // ========== Tab 切换 ==========

    private async Task OnTabChanged(string? tab)
    {
        _selectedTab = tab == "全部" ? null : tab;
        UpdateTabSummary();
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private void UpdateTabSummary()
    {
        var filtered = _selectedTab == null
            ? _allItems
            : _allItems.Where(x => x.KanbanStage == _selectedTab).ToList();

        _tabCount = filtered.Count;
        _tabTotalWeight = filtered.Sum(x => x.CurrentValidWeight ?? 0);
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

    // ========== 数据加载 ==========

    private async Task<TableData<FinalInspectionPlanDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;

        if (_isFirstLoad)
        {
            state.Page = _restoredPageIndex;
            _isFirstLoad = false;
        }

        // 1. Tab 筛选
        var filtered = _selectedTab == null
            ? _allItems
            : _allItems.Where(x => x.KanbanStage == _selectedTab).ToList();

        // 2. 关键词搜索
        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            var kw = _searchKeyword;
            filtered = filtered.Where(x =>
                (x.BatchNo != null && x.BatchNo.Contains(kw)) ||
                (x.TagNo != null && x.TagNo.Contains(kw)) ||
                (x.PlantGrade != null && x.PlantGrade.Contains(kw)) ||
                (x.WorkOrderNo != null && x.WorkOrderNo.Contains(kw)) ||
                (x.Specification != null && x.Specification.Contains(kw)) ||
                (x.Salesman != null && x.Salesman.Contains(kw))
            ).ToList();
        }

        // 3. ExcelFilter 列筛选
        if (_columnFilters.Count > 0)
        {
            filtered = filtered.Where(x => _columnFilters.All(f =>
            {
                var val = GetFilterValue(x, f.Key);
                return val != null && f.Value.Contains(val);
            })).ToList();
        }

        _totalCount = filtered.Count;
        _currentPageIndex = state.Page + 1;

        // 4. 排序
        filtered = ApplySorting(filtered, sortColumn, sortDescending);

        // 5. 分页
        var items = filtered
            .Skip(state.Page * state.PageSize)
            .Take(state.PageSize)
            .ToList();

        _pageItems = items;
        ComputePageSums();
        await SavePageStateAsync();
        return new TableData<FinalInspectionPlanDto>
        {
            Items = items,
            TotalItems = _totalCount
        };
    }

    private static List<FinalInspectionPlanDto> ApplySorting(List<FinalInspectionPlanDto> items, string sortBy, bool desc)
    {
        var query = sortBy.ToLower() switch
        {
            "batchno" => items.OrderBy(x => x.BatchNo ?? ""),
            "tagno" => items.OrderBy(x => x.TagNo ?? ""),
            "plantgrade" => items.OrderBy(x => x.PlantGrade ?? ""),
            "currentvalidweight" => items.OrderBy(x => x.CurrentValidWeight),
            "workorderno" => items.OrderBy(x => x.WorkOrderNo ?? ""),
            "salesman" => items.OrderBy(x => x.Salesman ?? ""),
            "specification" => items.OrderBy(x => x.Specification ?? ""),
            "lengthstatus" => items.OrderBy(x => x.LengthStatus ?? ""),
            "minlength" => items.OrderBy(x => x.MinLength),
            "maxlength" => items.OrderBy(x => x.MaxLength),
            "schedulestage" => items.OrderBy(x => x.ScheduleStage),
            "urgencylevel" => items.OrderBy(x => x.UrgencyLevel ?? ""),
            "receivedate" => items.OrderBy(x => x.ReceiveDate),
            "maxinspectiondate" => items.OrderBy(x => x.MaxInspectionDate),
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
        if (table != null) await table.ReloadServerData();
    }

    // ========== 搜索 ==========

    private async Task OnSearchChanged(string? value)
    {
        _searchKeyword = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
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

    // ========== 列值渲染 ==========

    private RenderFragment RenderCell(FinalInspectionPlanDto item, ColumnDef col) => builder =>
    {
        builder.OpenElement(0, "span");
        switch (col.Key)
        {
            case "BatchNo":
                builder.AddContent(0, item.BatchNo ?? "-");
                break;
            case "TagNo":
                builder.AddContent(0, item.TagNo ?? "-");
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade ?? "-");
                break;
            case "CurrentValidWeight":
                builder.AddContent(0, ((int)(item.CurrentValidWeight ?? 0)).ToString());
                break;
            case "WorkOrderNo":
                builder.AddContent(0, item.WorkOrderNo ?? "-");
                break;
            case "Salesman":
                builder.AddContent(0, item.Salesman ?? "-");
                break;
            case "Specification":
                builder.AddContent(0, item.Specification ?? "-");
                break;
            case "LengthStatus":
                builder.AddContent(0, DisplayHelper.GetLengthStatusText(item.LengthStatus));
                break;
            case "MinLength":
                builder.AddContent(0, item.MinLength?.ToString("G29") ?? "-");
                break;
            case "MaxLength":
                builder.AddContent(0, item.MaxLength?.ToString("G29") ?? "-");
                break;
            case "ScheduleStage":
                var stageColor = item.ScheduleStage switch
                {
                    0 => Color.Default,
                    1 => Color.Warning,
                    2 => Color.Success,
                    3 => Color.Info,
                    _ => Color.Default
                };
                var stageText = item.ScheduleStage switch
                {
                    0 => "工单完成",
                    1 => "原料锁定",
                    2 => "生产执行",
                    3 => "成品检验",
                    _ => "未知"
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
                    "A+急" => Color.Error,
                    "A急" => Color.Warning,
                    "B顺" => Color.Info,
                    "C缓" => Color.Default,
                    "D缓" => Color.Default,
                    _ => Color.Default
                };
                if (item.UrgencyLevel != null)
                {
                    builder.CloseElement(); // close span
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", urgencyColor);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.UrgencyLevel)));
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
            PageIndex = _currentPageIndex,
            Extras = extras
        };
        await PageState.SaveAsync("final-inspection-plan", state);
    }
}
