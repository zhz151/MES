using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Components;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MES.Core.Models;
using System.Text.Json;

namespace MES.Blazor.Pages.Scheduling;

public partial class ColdRollPlans
{
    private MudTable<ColdRollPlanRowDto>? table;
    private List<ColdRollPlanRowDto> _allItems = new();
    private List<ColdRollPlanRowDto> _rawAllItems = new(); // 明细原始数据（简化视图展开排程用）
    private List<ColdRollPlanRowDto> _pageItems = new();
    private int _totalCount;
    private int _restoredPageIndex;
    private int _currentPageIndex = 1;
    private bool _isFirstLoad = true;
    private bool _isSimplifiedView = false;
    private string _searchKeyword = string.Empty;
    private int _pageSize = 10;

    // ========== 排序状态 ==========
    private string sortColumn = "ShortDisplay";
    private bool sortDescending = false;

    // ========== 排程模式 ==========
    private bool _isSchedulingMode = false;
    private bool _scheduleDataLoaded = false;
    private DateTime? _scheduleUpdatedTime;
    private readonly Dictionary<string, ScheduleEditData> _scheduleEdits = new();

    // ========== 排程汇总 ==========
    private bool _showScheduleSummary = false;
    private List<ColdRollScheduleSummaryDto> _scheduleSummaryData = new();
    private bool _summaryLoading = false;
    private int? _summaryMaxDiff = 7; // null=全部, n=原工量差<=n

    // ========== 列筛选 ==========
    private readonly Dictionary<string, HashSet<string>> _columnFilters = new();
    private readonly Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    private class ScheduleEditData
    {
        public string MachineNo { get; set; } = "";
        public string CompletionType { get; set; } = "None";
        public string RollType { get; set; } = "None";
    }

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
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns
    {
        get => _allColumns.Where(c => c.Visible).ToList();
    }

    /// <summary>根据当前视图重建列定义</summary>
    private void RebuildColumnDefs()
    {
        _allColumns = _isSimplifiedView ? GetSimplifiedColumnDefs() : GetDetailColumnDefs();
        foreach (var col in _allColumns)
            col.Visible = true;
    }

    // ========== 视图切换 ==========
    private async Task OnSimplifiedViewChanged(bool val)
    {
        _isSimplifiedView = val;
        RebuildColumnDefs();
        await RestoreColumnVisibilityAsync();
        sortColumn = "ShortDisplay";
        sortDescending = false;
        _scheduleDataLoaded = false; // 切换视图后重新加载排程数据（key 不同）
        _scheduleEdits.Clear();
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
    [Inject] private ColdRollSpecScheduleService ScheduleSvc { get; set; } = default!;
    [Inject] private BatchPlanService BatchPlanSvc { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private PageStateService PageState { get; set; } = default!;
    protected override async Task OnInitializedAsync()
    {
        RebuildColumnDefs();

        var savedState = await PageState.LoadAsync("coldrollplans");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "ShortDisplay";
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

            if (savedState.Extras?.ContainsKey("isSimplifiedView") == true)
            {
                _isSimplifiedView = savedState.Extras["isSimplifiedView"] == "true";
                if (_isSimplifiedView)
                    RebuildColumnDefs();
            }

            if (savedState.Extras?.ContainsKey("selectedSection") == true)
            {
                _selectedSection = savedState.Extras["selectedSection"];
                if (_selectedSection == "全部") _selectedSection = null;
            }

            if (savedState.Extras?.ContainsKey("columnFilters") == true)
            {
                try
                {
                    var raw = savedState.Extras["columnFilters"];
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                            _columnFilters[kvp.Key] = new HashSet<string>(kvp.Value.Split(',', StringSplitOptions.RemoveEmptyEntries));
                    }
                }
                catch { }
            }

        }

        if (savedState != null && table != null)
            await table.ReloadServerData();
    }

    // ========== 打印 ==========
    private async Task OnPrint()
    {
        await JS.InvokeVoidAsync("window.print");
    }

    // ========== 列显隐 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SavePageStateAsync();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        await SavePageStateAsync();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        await SavePageStateAsync();
    }

    private async Task RestoreColumnVisibilityAsync()
    {
        var savedState = await PageState.LoadAsync("coldrollplans");
        if (savedState?.Extras?.ContainsKey("columnVisibility") == true)
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
    }

    // ========== 排程模式 ==========

    /// <summary>获取行唯一键（明细模式）</summary>
    private static string GetRowKey(ColdRollPlanRowDto row)
        => $"{row.ProcessType}|{row.BilletSpec}|{row.RollingSpec}|{row.IsFinished}";

    /// <summary>获取行唯一键（简化模式）</summary>
    private static string GetSimplifiedRowKey(ColdRollPlanRowDto row)
        => $"{row.ProcessType}|{row.ShortDisplay}|{row.IsFinished}";

    /// <summary>获取行排程编辑数据（自动创建条目到字典，确保 @bind-Value 绑定稳定）</summary>
    private ScheduleEditData GetEdit(ColdRollPlanRowDto row)
    {
        var key = _isSimplifiedView ? GetSimplifiedRowKey(row) : GetRowKey(row);
        if (!_scheduleEdits.TryGetValue(key, out var edit))
        {
            edit = new ScheduleEditData { MachineNo = row.MachineNo ?? "" };
            _scheduleEdits[key] = edit;
        }
        return edit;
    }

    /// <summary>加载已有排程数据到 _scheduleEdits</summary>
    private async Task LoadExistingScheduleDataAsync(List<ColdRollPlanRowDto> items)
    {
        if (!items.Any()) return;

        _scheduleEdits.Clear();

        var existing = await ScheduleSvc.GetAllAsync();
        _scheduleUpdatedTime = existing.Count > 0
            ? existing.Max(e => e.UpdatedTime)
            : DateTime.Now;
        var existingLookup = existing.ToDictionary(
            e => $"{e.ProcessType}|{e.BilletSpec}|{e.RollingSpec}|{e.IsFinished}",
            StringComparer.OrdinalIgnoreCase);

        if (_isSimplifiedView)
        {
            foreach (var item in items)
            {
                var simKey = GetSimplifiedRowKey(item);
                var matchingDetails = _rawAllItems
                    .Where(x => GetSimplifiedRowKey(x) == simKey).ToList();

                ScheduleEditData? edit = null;
                foreach (var detail in matchingDetails)
                {
                    var dk = GetRowKey(detail);
                    if (existingLookup.TryGetValue(dk, out var sched))
                    {
                        edit = new ScheduleEditData
                        {
                            MachineNo = sched.MachineNo ?? "",
                            CompletionType = sched.CompletionType,
                            RollType = sched.RollType,
                        };
                        break;
                    }
                }
                _scheduleEdits[simKey] = edit ?? new ScheduleEditData
                {
                    MachineNo = item.MachineNo ?? "",
                };
            }
        }
        else
        {
            foreach (var item in items)
            {
                var key = GetRowKey(item);
                if (existingLookup.TryGetValue(key, out var schedule))
                {
                    _scheduleEdits[key] = new ScheduleEditData
                    {
                        MachineNo = schedule.MachineNo ?? "",
                        CompletionType = schedule.CompletionType,
                        RollType = schedule.RollType,
                    };;
                }
                else
                {
                    _scheduleEdits[key] = new ScheduleEditData
                    {
                        MachineNo = item.MachineNo ?? "",
                    };
                }
            }
        }
    }

    /// <summary>将排程值回填到 DTO 项（支持排序/筛选/文本显示）</summary>
    private void ApplyScheduleToItems()
    {
        foreach (var item in _allItems)
        {
            var key = _isSimplifiedView ? GetSimplifiedRowKey(item) : GetRowKey(item);
            if (_scheduleEdits.TryGetValue(key, out var edit))
            {
                item.CompletionType = edit.CompletionType;
                item.RollType = edit.RollType;
                item.SchedMachineNo = edit.MachineNo;
            }
        }
    }

    /// <summary>进入排程模式</summary>
    private async Task EnterSchedulingModeAsync()
    {
        if (!_allItems.Any()) return;

        if (_scheduleEdits.Count == 0)
            await LoadExistingScheduleDataAsync(_allItems);

        _isSchedulingMode = true;
        StateHasChanged();
    }

    /// <summary>保存排程</summary>
    private async Task SaveScheduleAsync()
    {
        if (!_scheduleEdits.Any())
        {
            _isSchedulingMode = false;
            return;
        }

        try
        {
            // 简化模式：展开到明细级别
            var toSave = _scheduleEdits;
            if (_isSimplifiedView)
            {
                toSave = new Dictionary<string, ScheduleEditData>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in _scheduleEdits)
                {
                    var parts = kvp.Key.Split('|');
                    var simProcessType = parts[0];
                    var simShortDisplay = parts[1];
                    var simIsFinished = bool.Parse(parts[2]);

                    var matchingDetails = _rawAllItems.Where(x =>
                        x.ProcessType == simProcessType &&
                        x.ShortDisplay == simShortDisplay &&
                        x.IsFinished == simIsFinished).ToList();

                    foreach (var detail in matchingDetails)
                    {
                        toSave[GetRowKey(detail)] = kvp.Value;
                    }
                }
            }

            var toSaveList = new List<ColdRollSpecScheduleDto>();

            // 1. 加载全部现有排程记录（保留未在当前视图中的排程数据，避免 Tab 筛选导致数据丢失）
            var existingAll = await ScheduleSvc.GetAllAsync();
            var existingKeys = new HashSet<string>(existingAll.Select(e =>
                $"{e.ProcessType}|{e.BilletSpec}|{e.RollingSpec}|{e.IsFinished}"),
                StringComparer.OrdinalIgnoreCase);

            // 2. 当前编辑的视图数据
            var editedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in toSave)
            {
                editedKeys.Add(kvp.Key);
                var parts = kvp.Key.Split('|');
                toSaveList.Add(new ColdRollSpecScheduleDto
                {
                    ProcessType = parts[0],
                    BilletSpec = parts[1],
                    RollingSpec = parts[2],
                    IsFinished = bool.Parse(parts[3]),
                    MachineNo = string.IsNullOrWhiteSpace(kvp.Value.MachineNo) ? null : kvp.Value.MachineNo,
                    CompletionType = kvp.Value.CompletionType,
                    RollType = kvp.Value.RollType,
                });
            }

            // 3. 合并未编辑的现有记录（不在编辑范围内的保留原值）
            foreach (var existing in existingAll)
            {
                var key = $"{existing.ProcessType}|{existing.BilletSpec}|{existing.RollingSpec}|{existing.IsFinished}";
                if (!editedKeys.Contains(key))
                    toSaveList.Add(existing);
            }

            await ScheduleSvc.SaveAllAsync(toSaveList);

            _scheduleUpdatedTime = DateTime.Now;
            _isSchedulingMode = false;
            _scheduleEdits.Clear();
            _scheduleDataLoaded = false; // 保存后重新加载排程数据
            Snackbar.Add("排程已保存", Severity.Success);
            if (table != null)
                await table.ReloadServerData();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"保存排程失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>取消排程</summary>
    private void CancelSchedule()
    {
        _isSchedulingMode = false;
        _scheduleEdits.Clear();
        StateHasChanged();
    }

    // ========== 排程汇总 ==========

    private async Task ToggleScheduleSummaryAsync()
    {
        if (_showScheduleSummary)
        {
            _showScheduleSummary = false;
            return;
        }

        try
        {
            _summaryLoading = true;
            _scheduleSummaryData = await BatchPlanSvc.GetFlowSummaryAsync(_selectedSection, _summaryMaxDiff);
            _showScheduleSummary = true;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载排程汇总失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _summaryLoading = false;
        }
    }

    private async Task OnSummaryMaxDiffChanged(int? maxDiff)
    {
        _summaryMaxDiff = maxDiff;
        if (_showScheduleSummary)
        {
            _summaryLoading = true;
            _scheduleSummaryData = await BatchPlanSvc.GetFlowSummaryAsync(_selectedSection, _summaryMaxDiff);
            _summaryLoading = false;
        }
    }

    /// <summary>完工要求中文显示（无计划时返回空）</summary>
    private static string GetCompletionTypeText(string ct) => DisplayHelper.GetCompletionTypeText(ct);

    /// <summary>排程类型中文显示（无计划时返回空）</summary>
    private static string GetRollTypeText(string rollType) => DisplayHelper.GetRollTypeText(rollType);

    // ========== Tab 切换 ==========
    private async Task OnSectionTabChanged(string? section)
    {
        _selectedSection = section;
        _scheduleDataLoaded = false;
        _scheduleEdits.Clear();
        if (table != null)
            await table.ReloadServerData();
    }

    // ========== 搜索 ==========
    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        await SavePageStateAsync();
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
        await SavePageStateAsync();
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

        // 关键词搜索（所有文本字段）
        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            var kw = _searchKeyword.Trim();
            filtered = filtered.Where(x =>
                (x.ProcessType?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.BilletSpec?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.RollingSpec?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.MergeDisplay?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.ShortDisplay?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.MachineNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.CompletionType?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.RollType?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.SchedMachineNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true));
        }

        // ExcelFilter 列筛选
        foreach (var kvp in _columnFilters)
        {
            var filterValues = kvp.Value;
            if (filterValues.Count == 0) continue;
            filtered = filtered.Where(x => filterValues.Contains(GetFilterValue(x, kvp.Key)));
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
            "MachineNo" => sortDescending
                ? filtered.OrderByDescending(x => x.MachineNo ?? "")
                : filtered.OrderBy(x => x.MachineNo ?? ""),
            "CompletionType" => sortDescending
                ? filtered.OrderByDescending(x => x.CompletionType ?? "")
                : filtered.OrderBy(x => x.CompletionType ?? ""),
            "RollType" => sortDescending
                ? filtered.OrderByDescending(x => x.RollType ?? "")
                : filtered.OrderBy(x => x.RollType ?? ""),
            "SchedMachineNo" => sortDescending
                ? filtered.OrderByDescending(x => x.SchedMachineNo ?? "")
                : filtered.OrderBy(x => x.SchedMachineNo ?? ""),
            _ => filtered.OrderBy(x => x.ProcessType)
        };

        _pageItems = filtered.ToList();
    }

    // ========== 加载数据 ==========
    private async Task<TableData<ColdRollPlanRowDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;

        if (_isFirstLoad)
        {
            state.Page = _restoredPageIndex;
            _isFirstLoad = false;
        }

        try
        {
            var data = await ColdRollSvc.GetPlanAsync(_selectedSection);
            _rawAllItems = data;     // 保存原始明细数据（简化排程展开用）
            _allItems = data;

            // 简化视图时按外径跨度聚合
            if (_isSimplifiedView)
                _allItems = BuildSimplifiedView();

            // 首次加载时预填排程数据
            if (!_scheduleDataLoaded && _allItems.Any())
            {
                await LoadExistingScheduleDataAsync(_allItems);
                _scheduleDataLoaded = true;
            }

            // 将排程值回填到 DTO 项（支持排序/筛选）
            ApplyScheduleToItems();

            // 构建筛选上下文
            BuildFilterOptionsFromData();

            ApplyFiltersAndSort();

            // 统计数据
            _tabSpecCount = _allItems.Count;
            _tabTotalWeight = _allItems.Sum(r => r.WeightTotal);
            _tabKeyBatchCount = _allItems.Sum(r => r.KeyBatchCount);
            _tabKeyBatchWeight = _allItems.Sum(r => r.WeightProdUrgent + r.WeightWaitNearUrgent);

            // 客户端分页
            _totalCount = _pageItems.Count;
            var skip = state.Page * state.PageSize;
            var pageData = _pageItems.Skip(skip).Take(state.PageSize).ToList();
            _pageItems = pageData;
            _currentPageIndex = state.Page + 1;

            // 计算页脚小计（当前页）
            ComputePageSums();

            await SavePageStateAsync();

            return new TableData<ColdRollPlanRowDto>
            {
                Items = _pageItems,
                TotalItems = _totalCount,
            };
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载冷轧计划数据失败: {ex.Message}", Severity.Error);
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
        var g1 = new List<ColumnDef>
        {
            new() { Key = "ShortDisplay",  Label = "简化",       Width = "100", GroupKey = 1, GroupName = "规格信息" },
            new() { Key = "ProcessType",   Label = "冷轧类型",   Width = "100", GroupKey = 1, GroupName = "规格信息" },
            new() { Key = "BilletSpec",    Label = "轧坯规格",   Width = "120", GroupKey = 1, GroupName = "规格信息" },
            new() { Key = "RollingSpec",   Label = "轧制规格",   Width = "120", GroupKey = 1, GroupName = "规格信息" },
            new() { Key = "IsFinished",    Label = "是否成品",   Width = "100", GroupKey = 1, GroupName = "规格信息" },
            new() { Key = "MergeDisplay",  Label = "合并",       Width = "180", GroupKey = 1, GroupName = "规格信息" },
        };

        var g2 = new List<ColumnDef>
        {
            new() { Key = "MachineNo",          Label = "在轧设备号",     Width = "120", GroupKey = 2, GroupName = "近日在轧" },
            new() { Key = "WeightProd",         Label = "近日在轧",        Width = "100", GroupKey = 2, GroupName = "近日在轧" },
            new() { Key = "WeightProdUrgent",   Label = "近日在轧(急管)", Width = "120", GroupKey = 2, GroupName = "近日在轧" },
        };

        var g3 = new List<ColumnDef>
        {
            new() { Key = "WeightWaitNear",       Label = "近日待轧",        Width = "100", GroupKey = 3, GroupName = "近日待轧" },
            new() { Key = "WeightWaitNearUrgent", Label = "近日待轧(急管)",  Width = "120", GroupKey = 3, GroupName = "近日待轧" },
        };

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

        var g5 = new List<ColumnDef>
        {
            new() { Key = "WeightTotal",    Label = "工艺总量",  Width = "100", GroupKey = 5, GroupName = "合计" },
        };

        var g6 = new List<ColumnDef>
        {
            new() { Key = "CompletionType", Label = "在轧要求", Width = "90",  GroupKey = 6, GroupName = "排程设置", FilterType = "enum", EnumOptions = new() { new EnumOption("All","全量"), new EnumOption("Urgent","急单"), new EnumOption("Partial1","部分(1)"), new EnumOption("Partial2","部分(2)"), new EnumOption("Partial3","部分(3)") } },
            new() { Key = "RollType",       Label = "待轧要求", Width = "90",  GroupKey = 6, GroupName = "排程设置", FilterType = "enum", EnumOptions = new() { new EnumOption("All","全量"), new EnumOption("Urgent","急单"), new EnumOption("Partial1","部分(1)"), new EnumOption("Partial2","部分(2)"), new EnumOption("Partial3","部分(3)"), new EnumOption("Subsequent","后续") } },
            new() { Key = "SchedMachineNo", Label = "待轧设备号", Width = "100", GroupKey = 6, GroupName = "排程设置", FilterType = "string" },
        };

        return g1.Concat(g2).Concat(g3).Concat(g4).Concat(g5).Concat(g6).ToList();
    }

    private static List<ColumnDef> GetSimplifiedColumnDefs()
    {
        var g1 = new List<ColumnDef>
        {
            new() { Key = "ShortDisplay",  Label = "外径跨度",  Width = "100", GroupKey = 1, GroupName = "规格信息" },
            new() { Key = "ProcessType",   Label = "冷轧类型",  Width = "100", GroupKey = 1, GroupName = "规格信息" },
            new() { Key = "IsFinished",    Label = "是否成品",  Width = "100", GroupKey = 1, GroupName = "规格信息" },
        };

        var g2 = new List<ColumnDef>
        {
            new() { Key = "MachineNo",          Label = "在轧设备号",     Width = "120", GroupKey = 2, GroupName = "近日在轧" },
            new() { Key = "WeightProd",         Label = "近日在轧",        Width = "100", GroupKey = 2, GroupName = "近日在轧" },
            new() { Key = "WeightProdUrgent",   Label = "近日在轧(急管)", Width = "120", GroupKey = 2, GroupName = "近日在轧" },
        };

        var g3 = new List<ColumnDef>
        {
            new() { Key = "WeightWaitNear",       Label = "近日待轧",        Width = "100", GroupKey = 3, GroupName = "近日待轧" },
            new() { Key = "WeightWaitNearUrgent", Label = "近日待轧(急管)",  Width = "120", GroupKey = 3, GroupName = "近日待轧" },
        };

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

        var g5 = new List<ColumnDef>
        {
            new() { Key = "WeightTotal",    Label = "工艺总量",  Width = "100", GroupKey = 5, GroupName = "合计" },
        };

        var g6 = new List<ColumnDef>
        {
            new() { Key = "CompletionType", Label = "在轧要求", Width = "90",  GroupKey = 6, GroupName = "排程设置", FilterType = "enum", EnumOptions = new() { new EnumOption("All","全量"), new EnumOption("Urgent","急单"), new EnumOption("Partial1","部分(1)"), new EnumOption("Partial2","部分(2)"), new EnumOption("Partial3","部分(3)") } },
            new() { Key = "RollType",       Label = "待轧要求", Width = "90",  GroupKey = 6, GroupName = "排程设置", FilterType = "enum", EnumOptions = new() { new EnumOption("All","全量"), new EnumOption("Urgent","急单"), new EnumOption("Partial1","部分(1)"), new EnumOption("Partial2","部分(2)"), new EnumOption("Partial3","部分(3)"), new EnumOption("Subsequent","后续") } },
            new() { Key = "SchedMachineNo", Label = "待轧设备号", Width = "100", GroupKey = 6, GroupName = "排程设置", FilterType = "string" },
        };

        return g1.Concat(g2).Concat(g3).Concat(g4).Concat(g5).Concat(g6).ToList();
    }

    // ========== 单元格渲染 ==========
    private string RenderCell(ColdRollPlanRowDto item, ColumnDef col)
    {
        if (col.Key == "CompletionType") return GetCompletionTypeText(item.CompletionType);
        if (col.Key == "RollType") return GetRollTypeText(item.RollType);
        if (col.Key == "SchedMachineNo") return item.SchedMachineNo ?? "";

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
            "MachineNo" => item.MachineNo ?? "",
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

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        extras["columnVisibility"] = JsonSerializer.Serialize(_allColumns.Where(c => c.Visible).Select(c => c.Key).ToList());
        extras["isSimplifiedView"] = _isSimplifiedView ? "true" : "false";
        extras["selectedSection"] = _selectedSection ?? "全部";

        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(
                _columnFilters.ToDictionary(k => k.Key, k => string.Join(",", k.Value)));

        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPageIndex,
            Extras = extras
        };
        await PageState.SaveAsync("coldrollplans", state);
    }

    // ========== ExcelFilter 筛选 ==========

    /// <summary>从全量数据构建每列的筛选上下文选项</summary>
    private void BuildFilterOptionsFromData()
    {
        _filterContextOptions.Clear();
        foreach (var col in _visibleColumns.Where(c => c.FilterType != null))
        {
            // 有 EnumOptions 的列直接使用预设选项
            if (col.EnumOptions?.Any() == true)
            {
                _filterContextOptions[col.Key] = col.EnumOptions.Select(e => new ExcelFilterOption
                {
                    Value = e.Value,
                    Display = e.Display,
                }).ToList();
                continue;
            }

            // 普通列从数据中提取唯一值
            var values = new HashSet<string>();
            foreach (var item in _allItems)
            {
                var val = GetFilterValue(item, col.Key);
                if (!string.IsNullOrEmpty(val))
                    values.Add(val);
            }
            _filterContextOptions[col.Key] = values
                .OrderBy(x => x)
                .Select(v => new ExcelFilterOption { Value = v, Display = v })
                .ToList();
        }
    }

    /// <summary>获取列筛选值（返回原始枚举值，与 EnumOptions 的 Value 匹配）</summary>
    private static string GetFilterValue(ColdRollPlanRowDto item, string key)
    {
        return key switch
        {
            "CompletionType" => item.CompletionType ?? "",
            "RollType" => item.RollType ?? "",
            "SchedMachineNo" => item.SchedMachineNo ?? "",
            _ => "",
        };
    }

    /// <summary>列筛选变更处理</summary>
    private async Task OnColumnFilterChanged(string fieldKey, IEnumerable<string>? selectedValues)
    {
        if (selectedValues == null || !selectedValues.Any())
            _columnFilters.Remove(fieldKey);
        else
            _columnFilters[fieldKey] = new HashSet<string>(selectedValues);

        await SavePageStateAsync();
        if (table != null)
            await table.ReloadServerData();
    }
}
