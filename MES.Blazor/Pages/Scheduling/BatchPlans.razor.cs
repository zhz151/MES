using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MES.Core.Models;
using System.Text.Json;

namespace MES.Blazor.Pages.Scheduling;

public partial class BatchPlans
{
    private MudTable<BatchPlanDto>? table;
    private List<BatchPlanDto> _pageItems = new();
    private int _totalCount;
    private int _restoredPageIndex;
    private int _currentPageIndex = 1;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    // 排序状态
    private string sortColumn = "BatchNo";
    private bool sortDescending = true;

    // ========== 工段筛选 ==========
    private string? _selectedSection;
    private static readonly string[] _sectionTabs = new[]
    {
        "全部", "60冷轧", "50冷轧", "30冷轧", "20冷轧", "三辊冷轧", "冷拔",
        "油管断", "去油", "固溶", "矫直", "断切", "酸洗", "外抛光", "外点磨",
        "过程检验", "成品检验"
    };

    // ========== Tab 汇总数据 ==========
    private int _tabBatchCount;
    private decimal _tabTotalWeight;
    private int _tabKeyBatchCount;
    private decimal _tabKeyBatchWeight;

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
        "CurrentValidWeight", "MinLength", "MaxLength",
    };

    private static List<ColumnDef> GetAllColumnDefs()
    {
        // G1: 批次信息
        var g1 = new List<ColumnDef>
        {
            new() { Key = "BatchNo",              Label = "生产编号",   SortKey = "BatchNo",              FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "TagNo",                Label = "挂牌号",     SortKey = "TagNo",                FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "PlantGrade",            Label = "原料钢号",   SortKey = "PlantGrade",            FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次信息" },
            new() { Key = "CurrentValidWeight",    Label = "重量(kg)",   SortKey = "CurrentValidWeight",    Width = "80",  GroupKey = 1, GroupName = "批次信息" },
        };

        // G2: 关联工单信息
        var g2 = new List<ColumnDef>
        {
            new() { Key = "WorkOrderNo",           Label = "工单号",     SortKey = "WorkOrderNo",           FilterType = "string", Width = "120", GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "Salesman",              Label = "业务员",     SortKey = "Salesman",              FilterType = "string", Width = "100", GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "DeliveryDate",          Label = "交货日期",   SortKey = "DeliveryDate",          Width = "110", GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "DeliveryState",         Label = "交货状态",   SortKey = "DeliveryState",         FilterType = "enum", Width = "120", EnumOptions = new() { new("SolutionAnnealedAndPickled","固溶酸洗"), new("SolutionAnnealedAndPickledUTube","固溶酸洗-U型管"), new("SolutionAnnealedAndPickledExternalPolished","固溶酸洗-外抛光"), new("SolutionAnnealedAndPickledInternalPolished","固溶酸洗-内抛光"), new("SolutionAnnealedAndPickledBothPolished","固溶酸洗-内外抛光"), new("SolutionAnnealedAndPickledCoiled","固溶酸洗-盘管"), new("Bright","光亮"), new("BrightUTube","光亮-U型管"), new("BrightCoiled","光亮-盘管"), new("Hard","硬态") }, GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "Specification",         Label = "成品规格",   SortKey = "Specification",         FilterType = "string", Width = "120", GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "LengthStatus",          Label = "长度状态",   SortKey = "LengthStatus",          FilterType = "enum", Width = "100", EnumOptions = new() { new("Fixed","定尺"), new("Range","范围尺"), new("NonFixed","非定尺") }, GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "MinLength",             Label = "最小长度",   SortKey = "MinLength",             Width = "80",  GroupKey = 2, GroupName = "关联工单" },
            new() { Key = "MaxLength",             Label = "最大长度",   SortKey = "MaxLength",             Width = "80",  GroupKey = 2, GroupName = "关联工单" },
        };

        // G3: 状态跟踪
        var g3 = new List<ColumnDef>
        {
            new() { Key = "CurrentExecDate",        Label = "执行截止日",   SortKey = "CurrentExecDate",        Width = "110", GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "CurrentSectionName",      Label = "截止工段",     SortKey = "CurrentSectionName",      FilterType = "string", Width = "100", GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "PendingProcess",         Label = "待在产执行工序", Width = "130",                       GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "PendingSectionName",     Label = "执行工段",      Width = "120",                      GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "PendingSpec",            Label = "执行规格",      Width = "120",                      GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "PendingEquipment",       Label = "在轧设备",      Width = "120",                      GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "IsKeyBatch",                  Label = "重点生产批次",  FilterType = "boolean", Width = "120", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 3, GroupName = "状态跟踪" },
        };

        // G4: 批次关注
        var g4 = new List<ColumnDef>
        {
            new() { Key = "UrgencyLevel",               Label = "工单紧急性",    SortKey = "UrgencyLevel",               FilterType = "string", Width = "110", GroupKey = 4, GroupName = "批次关注" },
            new() { Key = "ScheduleStage",               Label = "计划状态",     SortKey = "ScheduleStage",               FilterType = "enum", Width = "110", EnumOptions = new() { new("0","工单完成"), new("1","原料锁定"), new("2","生产执行"), new("3","成品检验") }, GroupKey = 4, GroupName = "批次关注" },
            new() { Key = "ProductionAttentionProcess",  Label = "生产关注工序",  SortKey = "ProductionAttentionProcess",  FilterType = "string", Width = "130", GroupKey = 4, GroupName = "批次关注" },
        };

        var all = new List<ColumnDef>();
        all.AddRange(g2);
        all.AddRange(g4);
        all.AddRange(g1);
        all.AddRange(g3);
        return all;
    }

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(BatchPlanDto)
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
                else if (type == typeof(decimal))
                {
                    var sum = _pageItems.Sum(item => (decimal)(prop.GetValue(item) ?? 0m));
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
                else if (type == typeof(int?))
                {
                    var sum = _pageItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal?))
                {
                    var sum = _pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[col.Key] = ((int)sum).ToString();
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

    // ========== 工段筛选 ==========

    private async Task OnSectionTabChanged(string? section)
    {
        _selectedSection = section;
        if (table != null) await table.ReloadServerData();
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<BatchPlanDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;

        if (_isFirstLoad)
        {
            state.Page = _restoredPageIndex;
            _isFirstLoad = false;
        }

        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "BatchNo";
            var filtersJson = SerializeFilters();

            // 添加工段筛选作为额外筛选条件
            var allFilters = new List<FilterDescriptor>();
            if (filtersJson != null)
            {
                var existing = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson);
                if (existing != null) allFilters.AddRange(existing);
            }

            if (!string.IsNullOrEmpty(_selectedSection))
            {
                // 工段筛选：Service 层根据 Tab 类型执行不同逻辑（冷轧类=工序+冷轧拔工段，其它=工段匹配）
                allFilters.Add(new FilterDescriptor
                {
                    Field = "__SectionTab",
                    Operator = "contains",
                    Value = _selectedSection
                });
            }

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending,
                Filters = allFilters.Count > 0 ? allFilters : null
            };

            var result = await BatchPlanSvc.GetPagedAsync(query);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPageIndex = state.Page + 1;

                // 计算字段排序（内存排序）
                ApplyClientSideSort();

                ComputePageSums();

                // 读取 Tab 汇总数据
                if (result.Data.Extras != null)
                {
                    _tabBatchCount = GetExtraInt(result.Data.Extras, "batchCount");
                    _tabTotalWeight = GetExtraDecimal(result.Data.Extras, "totalWeight");
                    _tabKeyBatchCount = GetExtraInt(result.Data.Extras, "keyBatchCount");
                    _tabKeyBatchWeight = GetExtraDecimal(result.Data.Extras, "keyBatchWeight");
                }

                await SavePageStateAsync();
            }
            else
            {
                _pageItems = new();
                _totalCount = 0;
                ClearTabSummaries();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
            ClearTabSummaries();
        }

        return new TableData<BatchPlanDto>
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

    // ========== 筛选上下文加载 ==========

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await BatchPlanSvc.GetFilterContextsAsync();
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

        // 枚举列：用 EnumOptions 的中文显示名替换 API 返回的原始值
        foreach (var col in _allColumns.Where(c => c.FilterType == "enum" && c.EnumOptions != null))
        {
            if (_filterContextOptions.TryGetValue(col.Key, out var options))
            {
                var enumMap = col.EnumOptions!.ToDictionary(e => e.Value, e => e.Display);
                foreach (var opt in options.Where(o => enumMap.ContainsKey(o.Value)))
                    opt.Display = enumMap[opt.Value];
            }
        }

        // 补充枚举列筛选选项（API 无返回值时）
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

    // ========== 列显隐事件 ==========

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
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
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

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();

        var savedState = await PageState.LoadAsync("batchplans");
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

            if (savedState.Extras?.ContainsKey("selectedSection") == true)
            {
                _selectedSection = savedState.Extras["selectedSection"];
                if (_selectedSection == "全部") _selectedSection = null;
            }
        }

        if (savedState != null && table != null)
            await table.ReloadServerData();

        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("initGroupHeaders", "#batch-plan-list-table");
    }

    // ========== Tab 汇总辅助方法 ==========

    private static int GetExtraInt(Dictionary<string, object> extras, string key)
    {
        if (extras.TryGetValue(key, out var val) && val is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Number)
            return je.GetInt32();
        return 0;
    }

    private static decimal GetExtraDecimal(Dictionary<string, object> extras, string key)
    {
        if (extras.TryGetValue(key, out var val) && val is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Number)
            return je.GetDecimal();
        return 0m;
    }

    private void ClearTabSummaries()
    {
        _tabBatchCount = 0;
        _tabTotalWeight = 0m;
        _tabKeyBatchCount = 0;
        _tabKeyBatchWeight = 0m;
    }

    // ========== 计算字段排序（内存排序） ==========

    /// <summary>无 SortKey 的计算字段：客户端内存排序</summary>
    private static readonly HashSet<string> _clientSortableKeys = new()
    {
        "IsKeyBatch", "PendingProcess", "PendingSectionName", "PendingSpec", "PendingEquipment"
    };

    private void ApplyClientSideSort()
    {
        if (!_clientSortableKeys.Contains(sortColumn)) return;

        _pageItems = sortDescending
            ? _pageItems.OrderByDescending(x => GetSortValue(x, sortColumn)).ToList()
            : _pageItems.OrderBy(x => GetSortValue(x, sortColumn)).ToList();
    }

    private static object? GetSortValue(BatchPlanDto item, string key) => key switch
    {
        "IsKeyBatch" => item.IsKeyBatch,
        "PendingProcess" => item.PendingProcess,
        "PendingSectionName" => item.PendingSectionName,
        "PendingSpec" => item.PendingSpec,
        "PendingEquipment" => item.PendingEquipment,
        _ => null
    };

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(BatchPlanDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            // G1
            case "BatchNo":
                builder.AddContent(0, item.BatchNo);
                break;
            case "TagNo":
                builder.AddContent(0, item.TagNo ?? "-");
                break;
            case "PlantGrade":
                builder.AddContent(0, item.PlantGrade);
                break;
            case "CurrentValidWeight":
                builder.AddContent(0, item.CurrentValidWeight.HasValue ? ((int)item.CurrentValidWeight.Value).ToString() : "-");
                break;

            // G2
            case "WorkOrderNo":
                builder.AddContent(0, item.WorkOrderNo);
                break;
            case "Salesman":
                builder.AddContent(0, item.Salesman ?? "-");
                break;
            case "DeliveryDate":
                builder.AddContent(0, item.DeliveryDate.ToString("yyyy-MM-dd"));
                break;
            case "DeliveryState":
                builder.AddContent(0, DisplayHelper.GetDeliveryStateText(item.DeliveryState));
                break;
            case "Specification":
                builder.AddContent(0, item.Specification);
                break;
            case "LengthStatus":
                builder.AddContent(0, DisplayHelper.GetLengthStatusText(item.LengthStatus));
                break;
            case "MinLength":
                builder.AddContent(0, item.MinLength.HasValue ? ((int)item.MinLength.Value).ToString() : "-");
                break;
            case "MaxLength":
                builder.AddContent(0, item.MaxLength.HasValue ? ((int)item.MaxLength.Value).ToString() : "-");
                break;

            // G3
            case "CurrentExecDate":
                builder.AddContent(0, item.CurrentExecDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "CurrentSectionName":
                builder.AddContent(0, item.CurrentSectionName ?? "-");
                break;
            case "PendingProcess":
                builder.AddContent(0, item.PendingProcess ?? "-");
                break;
            case "PendingSectionName":
                builder.AddContent(0, item.PendingSectionName ?? "-");
                break;
            case "PendingSpec":
                builder.AddContent(0, item.PendingSpec ?? "-");
                break;
            case "PendingEquipment":
                builder.AddContent(0, item.PendingEquipment ?? "-");
                break;

            // G4
            case "UrgencyLevel":
                var urgencyColor = item.UrgencyLevel switch
                {
                    "A+急" => Color.Error,
                    "A急" => Color.Warning,
                    "B顺" => Color.Info,
                    "C缓" => Color.Default,
                    "D缓" => Color.Default,
                    "E停" => Color.Default,
                    _ => Color.Default
                };
                if (item.UrgencyLevel != null)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", urgencyColor);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.UrgencyLevel)));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
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
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", stageColor);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, stageText)));
                builder.CloseComponent();
                break;
            case "ProductionAttentionProcess":
                builder.AddContent(0, item.ScheduleStage == 2
                    ? (item.ProductionAttentionProcess ?? "收尾-成检")
                    : "-");
                break;
            case "IsKeyBatch":
                if (item.IsKeyBatch)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Error);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "是")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "否");
                }
                break;
        }
    };

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));

        extras["columnVisibility"] = JsonSerializer.Serialize(_allColumns.Where(c => c.Visible).Select(c => c.Key).ToList());

        extras["selectedSection"] = _selectedSection ?? "全部";

        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPageIndex,
            Extras = extras
        };
        await PageState.SaveAsync("batchplans", state);
    }
}
