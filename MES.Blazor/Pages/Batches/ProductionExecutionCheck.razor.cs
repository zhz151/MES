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
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using MES.Core.Constants;
using System.Text.Json;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.Batches;

/// <summary>
/// 生产执行核查（2026-09-08 由批次首页拆出）：承载「错疑-生产批次执行」聚合卡（即原「批次-错疑执行」，4 类错疑批次数 + 领料重量合计，默认折叠、展开时懒加载），
/// 点选卡片联动筛选页内自足精简批次列表；列表只读（仅查看详情，不做删除/编辑）。
/// 后端零改动：列表复用 api/batch/list 通用 Filters(in) 通道，聚合复用 api/batch/doubt-execution-summary。
/// </summary>
public partial class ProductionExecutionCheck
{
    private MudTable<ProductionBatchListDto>? table;
    private List<ProductionBatchListDto> _pageItems = new();
    private int _totalCount;
    private HashSet<int> selectedIds = new();
    private bool allSelected
    {
        get => _pageItems.Any() && _pageItems.All(i => selectedIds.Contains(i.Id));
        set
        {
            if (value)
            {
                foreach (var item in _pageItems)
                    selectedIds.Add(item.Id);
            }
            else
            {
                selectedIds.Clear();
            }
            StateHasChanged();
        }
    }
    private int _currentPageIndex;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private int _loadVersion;
    private bool _resetToFirstPage;

    // 排序
    private string sortColumn = "batchno";
    private bool sortDescending = true;

    // ========== 「错疑-生产批次执行」聚合卡（默认折叠；点开时才懒加载错疑汇总，点选写入列筛选联动下方列表） ==========
    private bool _showDoubtExecutionCard = false;
    private List<BatchDoubtExecutionItemDto>? _doubtExecutionItems;
    private bool _doubtExecutionLoading;

    // 由卡片联动写入的列筛选字段（用于「取消筛选」按钮；仅清除本卡片触发的筛选，不误伤列头手动筛选）
    private readonly HashSet<string> _doubtActiveFilterFields = new();
    private bool HasActiveDoubtFilter => _doubtActiveFilterFields.Any(f => _columnFilters.ContainsKey(f));
    private string _doubtLabel => _doubtActiveFilterFields
        .Where(f => _columnFilters.ContainsKey(f))
        .Select(f => f switch
        {
            "ExecutionMatch" => "匹配工单",
            "FlowJudgment" => "工段流转",
            "ProcessInspectionNeedAdjust" => "有效投料",
            "CutDoubt" => "成品切割",
            _ => f
        })
        .FirstOrDefault() ?? "";

    // ========== 分页汇总 ==========
    private Dictionary<string, string> _pageSums = new();

    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "CutQuantity",
        "TheoreticalOutputQty", "TheoreticalOutputWeight", "ProcessInspectionTheoreticalQty",
    };

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // ===== G1: 批次与工单（原「批次与执行」+「关联工单」合并；2026-09-08 裁剪 当前工序/当前工段/截止执行日/工段完工/订单号/主号） =====
        new() { Key = "BatchNo",            Label = "生产编号", SortKey = "batchno", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次与工单" },
        new() { Key = "Status",             Label = "状态",     SortKey = "status", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "批次与工单",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<BatchStatus>() },
        new() { Key = "WorkOrderNo",        Label = "工单号",   SortKey = "workorderno", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次与工单" },
        new() { Key = "ScheduleStage",      Label = "工单关注", SortKey = "ScheduleStage", FilterType = "enum", Width = "100", GroupKey = 1, GroupName = "批次与工单",
            EnumOptions = new()
            {
                new("0", "主号暂停"), new("1", "主号完成"), new("2", "原料锁定"), new("3", "生产执行"), new("4", "成品检验"),
                new("略", "略"), new("-1", "无此工单")
            } },
        new() { Key = "TagNo",              Label = "挂牌号",   SortKey = "tagno", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次与工单", Visible = false },
        new() { Key = "ProductionSubNo",    Label = "次号",     SortKey = "productionsubno", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次与工单", Visible = false },

        // ===== G2: 执行核查（4 灯：匹配工单/工段流转/投料需调整/成切存疑；与错疑聚合卡同源，默认必显） =====
        new() { Key = "ExecutionMatch", Label = "匹配工单", SortKey = "ExecutionMatch", FilterType = "enum", Width = "100", GroupKey = 2, GroupName = "执行核查",
            EnumOptions = new() { new("Error", "错误"), new(ProductionFlowKeys.Normal, "正常") } },
        new() { Key = "FlowJudgment", Label = "工段流转", SortKey = "FlowJudgment", FilterType = "enum", Width = "90", GroupKey = 2, GroupName = "执行核查",
            EnumOptions = new() { new(ProductionFlowKeys.Normal, "正常"), new(ProductionFlowKeys.Doubt, "疑问") } },
        new() { Key = "ProcessInspectionNeedAdjust", Label = "投料需调整", SortKey = null, FilterType = "enum", Width = "90", GroupKey = 2, GroupName = "执行核查",
            EnumOptions = new() { new("True", "是"), new("False", "-") } },
        new() { Key = "CutDoubt",       Label = "成切存疑", SortKey = null, FilterType = "enum", Width = "90", GroupKey = 2, GroupName = "执行核查",
            EnumOptions = DisplayHelper.GetEnumFilterOptions<CutDoubtType>() },

        // ===== G3: 理论产出对照（2026-09-08 收束为错疑缘由数据对照组；同日再调整：列名精简 + 组名贴合实际 + 取消过程检成重列） =====
        // 目的：为 G2「投料需调整」「成切存疑」两灯提供行级对照量（支数口径）
        //   · 过程检理论成支（过程检侧合格支/重量口径折算成支）→ 投料需调整分子侧对照；仅批次停留于过程检验工段时点有值，
        //     一旦成切执行/推进至成检即随 CurrentGroupName 越过后自然为空（考察窗口=生产前中段）
        //   · 现理论成支（基于现有效 CurrentValidQty×制几率）/ 理论成品重（现有效×折扣）→ 投料需调整分母 & 成切存疑分母基准
        //   · 成切需求/执行/支数 → 成切存疑灯行级缘由（缺记录/数量偏差）
        // 2026-09-08 裁剪 领料支/重、现有效原料支/重、缺陷-返整量、缺陷-纯次品量、过程检成重（重量侧折算源，判灯不依赖，详情/批次页可见）
        new() { Key = "ProcessInspectionTheoreticalQty", Label = "过程检理论成支", SortKey = null, Width = "100", GroupKey = 3, GroupName = "理论产出对照" },
        new() { Key = "TheoreticalOutputQty",    Label = "现理论成支", SortKey = "theoreticaloutputqty", Width = "80", GroupKey = 3, GroupName = "理论产出对照" },
        new() { Key = "TheoreticalOutputWeight", Label = "理论成品重", SortKey = "theoreticaloutputweight", Width = "80", GroupKey = 3, GroupName = "理论产出对照" },
        new() { Key = "CutRequirement", Label = "成切需求", SortKey = null, FilterType = "enum", Width = "90", GroupKey = 3, GroupName = "理论产出对照",
            EnumOptions = DisplayHelper.GetBoolOptions() },
        new() { Key = "CutExecution",   Label = "成切执行", SortKey = null, FilterType = "enum", Width = "90", GroupKey = 3, GroupName = "理论产出对照",
            EnumOptions = DisplayHelper.GetBoolOptions() },
        new() { Key = "CutQuantity",    Label = "成切支数", SortKey = null, Width = "90", GroupKey = 3, GroupName = "理论产出对照" },
    };

    // ========== 分页汇总计算 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(ProductionBatchListDto)
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

    // ========== 服务端数据加载 ==========

    private async Task<TableData<ProductionBatchListDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
            // 首次加载覆盖页码（MudTable 初始化时始终传 page=0）
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            if (_resetToFirstPage)
            {
                state.Page = 0;
                _resetToFirstPage = false;
            }

            var sortCol = _allColumns.FirstOrDefault(c => c.Key == sortColumn);
            var sortBy = sortCol?.SortKey ?? sortColumn ?? "batchno";
            var filtersJson = SerializeFilters();

            var query = new BatchQueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                SortBy = sortBy,
                IsDescending = sortDescending,
            };

            if (!string.IsNullOrEmpty(filtersJson))
            {
                try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson); }
                catch { }
            }

            var result = await BatchService.GetPagedAsync(query);

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<ProductionBatchListDto> { Items = _pageItems, TotalItems = _totalCount };

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPageIndex = result.Data.PageIndex;
                ComputePageSums();
            }
            else
            {
                _pageItems = new();
                _totalCount = 0;
            }

            await SavePageStateAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
        }

        return new TableData<ProductionBatchListDto>
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
            var result = await BatchService.GetFilterContextsAsync();
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
                Display = kvp.Key switch
                {
                    "SectionName" or "CurrentSectionName" or "NextSectionName" or "PendingSectionName" => SectionDisplayHelper.GetSectionNameText(v),
                    "ProcessName" or "ProcessGroupName" or "CurrentGroupName" or "NextProcess" => ProcessDisplayHelper.GetProcessNameText(v),
                    _ => v
                },
                Count = 0
            }).ToList();
        }

        // 枚举列显示中文标签
        foreach (var col in _allColumns)
        {
            if (col.FilterType == "enum" && col.EnumOptions != null && _filterContextOptions.TryGetValue(col.Key, out var options))
            {
                var displayMap = col.EnumOptions.ToDictionary(e => e.Value, e => e.Display);
                foreach (var opt in options)
                {
                    if (displayMap.TryGetValue(opt.Value, out var display))
                        opt.Display = display;
                }
            }
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
                _filterContextOptions[col.Key] = DisplayHelper.GetBoolFilterOptions(col);
            }
        }
    }

    // ========== ExcelFilter / 卡片联动 事件 ==========

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues?.Any() == true)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);

        // 卡片联动筛选状态同步：联动字段被手动清空（列头 ExcelFilter 取消勾选）时，联动标记一并失效
        if (_doubtActiveFilterFields.Contains(fieldKey) && selectedValues?.Any() != true)
            _doubtActiveFilterFields.Remove(fieldKey);

        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 「错疑-生产批次执行」聚合卡 ==========

    private void ToggleDoubtExecutionCard()
    {
        _showDoubtExecutionCard = !_showDoubtExecutionCard;
        if (_showDoubtExecutionCard && _doubtExecutionItems == null && !_doubtExecutionLoading)
            _ = LoadDoubtExecutionAsync();
    }

    private async Task LoadDoubtExecutionAsync()
    {
        _doubtExecutionLoading = true;
        try
        {
            var result = await BatchService.GetDoubtExecutionSummaryAsync();
            if (result.Success && result.Data != null)
                _doubtExecutionItems = result.Data;
            else
                Snackbar.Add(result?.Message ?? "获取批次错疑执行统计失败", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"获取批次错疑执行统计失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _doubtExecutionLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>卡片「批次数量」显示：例「10批/1500Kg」；无批次 → "-"</summary>
    private static string FormatDoubtExecutionCell(BatchDoubtExecutionItemDto item) =>
        item.BatchCount > 0 ? $"{item.BatchCount}批/{item.InputWeight.ToString("G29")}Kg" : "-";

    /// <summary>点击卡片「批次数量」→ 写入对应列筛选并刷新列表（与列头 ExcelFilter 同通道，可手动清除）</summary>
    private async Task ApplyDoubtExecutionLink(BatchDoubtExecutionItemDto item)
    {
        HashSet<string> values = item.DoubtType switch
        {
            BatchDoubtExecutionType.MatchOrder => new() { "Error" },
            BatchDoubtExecutionType.FlowDoubt => new() { ProductionFlowKeys.Doubt },
            BatchDoubtExecutionType.NeedAdjust => new() { "True" },
            BatchDoubtExecutionType.CutDoubt => new() { "QuantityMismatch", "MissingRecords" },
            _ => new()
        };
        if (values.Count == 0) return;
        var fieldKey = item.DoubtType switch
        {
            BatchDoubtExecutionType.MatchOrder => "ExecutionMatch",
            BatchDoubtExecutionType.FlowDoubt => "FlowJudgment",
            BatchDoubtExecutionType.NeedAdjust => "ProcessInspectionNeedAdjust",
            BatchDoubtExecutionType.CutDoubt => "CutDoubt",
            _ => ""
        };
        if (string.IsNullOrEmpty(fieldKey)) return;
        _doubtActiveFilterFields.Add(fieldKey);
        await OnColumnFilterChanged(fieldKey, values);
    }

    /// <summary>取消卡片联动筛选：仅清除由本卡片触发的列筛选（不误伤列头手动筛选），刷新列表</summary>
    private async Task CancelDoubtExecutionFilterAsync()
    {
        var toRemove = _doubtActiveFilterFields.Where(f => _columnFilters.ContainsKey(f)).ToList();
        if (toRemove.Count == 0) return;
        foreach (var f in toRemove)
            _columnFilters.Remove(f);
        _doubtActiveFilterFields.Clear();
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
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

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("batchExecutionCheck_v6", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
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

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("batchExecutionCheck_v6", null);
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

        // 从 PageState 恢复排序/筛选状态
        var savedState = await PageState.LoadAsync("batchExecutionCheck");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "batchno";
            sortDescending = savedState.IsDescending;
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

        // 状态恢复后重新加载表格数据（首次渲染时 ServerData 可能已用默认值加载）
        if (savedState != null && table != null)
            await table.ReloadServerData();
        await LoadFilterContextsAsync();

        // 聚合卡默认折叠；首次进入不加载（展开时才懒加载，见 ToggleDoubtExecutionCard）
        if (_showDoubtExecutionCard && _doubtExecutionItems == null && !_doubtExecutionLoading)
            await LoadDoubtExecutionAsync();
    }

    // ========== 导航 ==========

    private void ViewDetail(int id) => Navigation.NavigateTo($"/batches/{id}");

    // ========== 单元格渲染 ==========

    private static string GetColumnValue(ProductionBatchListDto item, string key) => key switch
    {
        "TagNo" => item.TagNo ?? "",
        "WorkOrderNo" => item.WorkOrderNo,
        "ProductionSubNo" => item.ProductionSubNo ?? "",
        "CutRequirement" => item.CutRequirementDisplay,
        "CutExecution" => item.CutExecutionDisplay ?? "",
        "CutQuantity" => item.CutQuantity?.ToString("G29") ?? "",
        "CutDoubt" => item.CutDoubtDisplay ?? "",
        "ProcessInspectionNeedAdjust" => item.ProcessInspectionNeedAdjust switch { true => "是", false => "-", null => "-" },
        "TheoreticalOutputQty" => DisplayHelper.FormatNullableInt(item.TheoreticalOutputQty),
        "TheoreticalOutputWeight" => DisplayHelper.FormatNullableInt(item.TheoreticalOutputWeight),
        "ProcessInspectionTheoreticalQty" => DisplayHelper.FormatNullableInt(item.ProcessInspectionTheoreticalQty),
        "ExecutionMatch" => item.ExecutionMatch == "Error" ? "错误" : "正常",
        "FlowJudgment" => item.FlowJudgment == ProductionFlowKeys.Doubt ? "疑问" : "正常",
        "ScheduleStage" => item.ScheduleStage switch
        {
            null => "略",
            -1 => "无此工单",
            int s => DisplayHelper.GetScheduleStageText(s)
        },
        "Status" => DisplayHelper.GetBatchStatusText(item.Status),
        "BatchNo" => item.BatchNo,
        _ => ""
    };

    private RenderFragment RenderCell(ProductionBatchListDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "BatchNo":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => ViewDetail(item.Id)));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.BatchNo)));
                builder.CloseComponent();
                break;
            case "Status":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetBatchStatusColor(item.Status));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetBatchStatusText(item.Status))));
                builder.CloseComponent();
                break;
            case "ProcessInspectionNeedAdjust":
                if (item.ProcessInspectionNeedAdjust == true)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Warning);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "是")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "CutRequirement":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", item.CutRequirement ? Color.Success : Color.Default);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.CutRequirementDisplay)));
                builder.CloseComponent();
                break;
            case "CutExecution":
                if (item.CutExecution.HasValue)
                {
                    var ce = item.CutExecution.Value;
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", ce ? Color.Success : Color.Warning);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, ce ? "是" : "否")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "略");
                }
                break;
            case "CutDoubt":
                if (item.CutDoubt.HasValue)
                {
                    var cd = item.CutDoubt.Value;
                    var cdColor = cd switch
                    {
                        CutDoubtType.QuantityMismatch => Color.Error,
                        CutDoubtType.MissingRecords => Color.Warning,
                        CutDoubtType.Normal => Color.Success,
                        _ => Color.Default
                    };
                    var cdText = cd switch
                    {
                        CutDoubtType.QuantityMismatch => "疑问-数量",
                        CutDoubtType.MissingRecords => "疑问-缺少",
                        CutDoubtType.Normal => "正常",
                        _ => "略"
                    };
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", cdColor);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, cdText)));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "略");
                }
                break;
            case "ScheduleStage":
                if (item.ScheduleStage is >= 0)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", DisplayHelper.GetScheduleStageColor(item.ScheduleStage.Value));
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DisplayHelper.GetScheduleStageText(item.ScheduleStage.Value))));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.ScheduleStage == -1 ? "无此工单" : "略");
                }
                break;
            case "ExecutionMatch":
                if (item.ExecutionMatch == "Error")
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Error);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "错误")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.ExecutionMatch == "Error" ? "错误" : "正常");
                }
                break;
            case "FlowJudgment":
                if (item.FlowJudgment == ProductionFlowKeys.Doubt)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Error);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "疑问")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.FlowJudgment == ProductionFlowKeys.Doubt ? "疑问" : "正常");
                }
                break;
            default:
                var val = GetColumnValue(item, col.Key);
                builder.AddContent(0, val);
                break;
        }
    };

    // ========== 打印 ==========

    private List<PrintColumnDef> GetPrintColumnDefs()
    {
        return _visibleColumns.Select(c => new PrintColumnDef
        {
            Key = c.Key,
            Label = c.Label
        }).ToList();
    }

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的批次", Severity.Warning);
            return;
        }

        // 列过多时各列被压缩到单字符放不下的宽度 → QuestPDF 布局冲突；A4 可显示列数上限 35 列（与后端 TablePrintHelper.MaxPrintColumns 同步），超限提前拦截并页面内警示
        const int MaxPrintColumns = 35;
        if (_visibleColumns.Count > MaxPrintColumns)
        {
            Snackbar.Add($"当前可见列过多（{_visibleColumns.Count} 列，打印上限 {MaxPrintColumns} 列），请通过列显隐精简后再打印", Severity.Warning);
            return;
        }

        try
        {
            var ids = selectedIds.ToArray();
            var request = new BatchPrintSelectedRequest
            {
                Ids = ids,
                Columns = GetPrintColumnDefs()
            };
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.Batch}/print-selected-file";
            var json = JsonSerializer.Serialize(request);
            Snackbar.Add("正在生成PDF...", Severity.Info);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 分组渲染 ==========

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
            if (lastKey.HasValue && gk != lastKey.Value)
            {
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

        // 操作列占位，对齐表格最右侧的操作按钮列（无 col-g 类，JS 按 gk=0 单独测量）
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0,
            GroupName = "",
            TotalWidth = 80,
            ColumnCount = 0,
            CssClass = ""
        });

        return result;
    }

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var gk = groupKey ?? 0;
        var cls = gk > 0 && gk % 3 == 1 ? "col-g3"
            : gk > 0 && gk % 3 == 2 ? "col-g4"
            : gk > 0 ? "col-g5"
            : "";
        if (isGroupStart && gk > 1) cls += " col-group-start";
        return cls;
    }

    private static string GetCellGroupCss(int? groupKey, bool isGroupStart)
    {
        var gk = groupKey ?? 0;
        var cls = gk > 0 && gk % 3 == 1 ? "col-g3-cell"
            : gk > 0 && gk % 3 == 2 ? "col-g4-cell"
            : gk > 0 ? "col-g5-cell"
            : "";
        if (isGroupStart && gk > 1) cls += " col-group-start-cell";
        return cls;
    }

    /// <summary>单元格对齐：数值类字段数据格居中，文本类字段靠左（表头保持原样）。</summary>
    private static string GetAlignClass(ColumnDef col) => col.Key switch
    {
        "CutQuantity" or
        "TheoreticalOutputQty" or "TheoreticalOutputWeight" or "ProcessInspectionTheoreticalQty" => "text-center",
        _ => ""
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#production-execution-check-list-table");
        }
        catch { }
    }

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
            PageIndex = _currentPageIndex,
            Extras = extras
        };
        await PageState.SaveAsync("batchExecutionCheck", state);
    }
}
