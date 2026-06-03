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

public partial class RawMaterialLockPlanAndExecution
{
    private MudTable<RawMaterialLockPlanAndExecutionDto>? table;
    private List<RawMaterialLockPlanAndExecutionDto> _pageItems = new();
    private int _totalCount;
    private int _restoredPageIndex;
    private int _currentPageIndex = 1;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private bool _isPlanning;

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "TotalItemCount", "TotalQuantity", "TotalMeters", "TotalWeight",
        "PendingRoughTubeQty", "PendingRoughTubeWeight", "PendingOutsourceFinishQty", "PendingOutsourceFinishWeight",
        "TheoreticalFinishQty", "TheoreticalFinishWeight",
        "InputQuantity", "InputWeight", "TheoreticalOutputQty", "TheoreticalOutputWeight",
        "FlowTotalBatchCount", "FlowIncompleteBatchCount",
        "GeneralDefectWeight", "SeriousDefectWeight", "ScrapWeight",
    };

    private bool _isUpdating;
    private string _searchKeyword = string.Empty;

    // 排序状态
    private string sortColumn = "ScheduleStage";
    private bool sortDescending = true;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs()
    {
        // G1: 工单基础数据
        var g1 = new List<ColumnDef>
        {
            new() { Key = "WorkOrderNo",             Label = "工单号",          SortKey = "WorkOrderNo",             FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "Salesman",                Label = "业务员",          SortKey = "Salesman",                FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "CustomerName",            Label = "往来单位",        SortKey = "CustomerName",            FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SignDate",                Label = "订单日期",        SortKey = "SignDate",                Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DeliveryDate",            Label = "交货日期",        SortKey = "DeliveryDate",            Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DelayPenalty",            Label = "延期罚款",        SortKey = "DelayPenalty",            FilterType = "boolean", Width = "120", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SettlementMethod",        Label = "结算方式",        SortKey = "SettlementMethod",        FilterType = "enum", Width = "120", EnumOptions = new() { new("Weighing","过磅"), new("WeighingNegative","过磅-负"), new("Theoretical","理算") }, Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SalesOrderNo",            Label = "订单号",          SortKey = "SalesOrderNo",            FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionMainNo",        Label = "主号",            SortKey = "ProductionMainNo",        FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionSubNo",         Label = "次号",            SortKey = "ProductionSubNo",         FilterType = "string", Width = "120", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MaterialName",            Label = "物料名称",        SortKey = "MaterialName",            FilterType = "enum", Width = "120", EnumOptions = new() { new("SeamlessPipe","无缝管"), new("WeldedPipe","焊管") }, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DeliveryState",           Label = "交货状态",        SortKey = "DeliveryState",           FilterType = "enum", Width = "120", EnumOptions = new() { new("SolutionAnnealedAndPickled","固溶酸洗"), new("SolutionAnnealedAndPickledUTube","固溶酸洗-U型管"), new("SolutionAnnealedAndPickledExternalPolished","固溶酸洗-外抛光"), new("SolutionAnnealedAndPickledInternalPolished","固溶酸洗-内抛光"), new("SolutionAnnealedAndPickledBothPolished","固溶酸洗-内外抛光"), new("SolutionAnnealedAndPickledCoiled","固溶酸洗-盘管"), new("Bright","光亮"), new("BrightUTube","光亮-U型管"), new("BrightCoiled","光亮-盘管"), new("Hard","硬态") }, Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "PlantGrade",              Label = "工厂牌号",        SortKey = "PlantGrade",              FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "Specification",           Label = "规格",            SortKey = "Specification",           FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "LengthStatus",            Label = "长度状态",        SortKey = "LengthStatus",            FilterType = "enum", Width = "120", EnumOptions = new() { new("Fixed","定尺"), new("Range","范围尺"), new("NonFixed","非定尺") }, Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MinLength",               Label = "最小长度",        SortKey = "MinLength",               Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MaxLength",               Label = "最大长度",        SortKey = "MaxLength",               Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalItemCount",          Label = "总项数",          SortKey = "TotalItemCount",          Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalQuantity",           Label = "总支数",          SortKey = "TotalQuantity",           Width = "80", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalMeters",             Label = "总米数",          SortKey = "TotalMeters",             Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalWeight",             Label = "总重量",          SortKey = "TotalWeight",             Width = "80", GroupKey = 1, GroupName = "基础数据" },
        };

        // G2: 用料计划
        var g2 = new List<ColumnDef>
        {
            new() { Key = "LatestPlanDate",          Label = "计划截止日",      SortKey = "LatestPlanDate",          Width = "120", GroupKey = 2, GroupName = "用料计划" },
            new() { Key = "MaterialPlanRate",        Label = "满足率(%)",      SortKey = "MaterialPlanRate",        Width = "80",                              GroupKey = 2, GroupName = "用料计划" },
            new() { Key = "MaterialPlanStatus",      Label = "用料计划状态",    SortKey = "MaterialPlanStatus",      FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未计划"), new("1","部分"), new("2","理论满足"), new("3","满足"), new("4","超量") }, GroupKey = 2, GroupName = "用料计划" },
            new() { Key = "MainNoMaterialPlanRate",  Label = "主号满足率(%)",  SortKey = "MainNoMaterialPlanRate",  Width = "80", Visible = false, GroupKey = 2, GroupName = "用料计划" },
            new() { Key = "MainNoMaterialPlanStatus",Label = "主号用料计划状态",SortKey = "MainNoMaterialPlanStatus",FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未计划"), new("1","部分"), new("2","理论满足"), new("3","满足"), new("4","超量") }, Visible = false, GroupKey = 2, GroupName = "用料计划" },
            new() { Key = "ProcessCycle",            Label = "工艺周期(天)",    SortKey = "ProcessCycle",            Width = "80",                               GroupKey = 2, GroupName = "用料计划" },
        };

        // G5: 物料执行实时信息
        var g5 = new List<ColumnDef>
        {
            new() { Key = "PendingRoughTubeQty",        Label = "待回荒管支",     SortKey = "PendingRoughTubeQty",        Width = "80",                      GroupKey = 5, GroupName = "物料执行" },
            new() { Key = "PendingRoughTubeWeight",     Label = "待回荒管重",     SortKey = "PendingRoughTubeWeight",     Width = "80",                      GroupKey = 5, GroupName = "物料执行" },
            new() { Key = "PendingOutsourceFinishQty",  Label = "待回外购成支",   SortKey = "PendingOutsourceFinishQty",  Width = "80",                      GroupKey = 5, GroupName = "物料执行" },
            new() { Key = "PendingOutsourceFinishWeight",Label = "待回外购成重",  SortKey = "PendingOutsourceFinishWeight",Width = "80",                    GroupKey = 5, GroupName = "物料执行" },
            new() { Key = "TheoreticalFinishQty",        Label = "理论成品支",    SortKey = "TheoreticalFinishQty",        Width = "80",                    GroupKey = 5, GroupName = "物料执行" },
            new() { Key = "TheoreticalFinishWeight",     Label = "理论成品重",    SortKey = "TheoreticalFinishWeight",     Width = "80",                    GroupKey = 5, GroupName = "物料执行" },
        };

        // G3: 投料数据
        var g3 = new List<ColumnDef>
        {
            new() { Key = "InputStartDate",          Label = "原始投料起始日",  SortKey = "InputStartDate",          Width = "120", GroupKey = 3, GroupName = "投料数据" },
            new() { Key = "InputEndDate",            Label = "原始投料截止日",  SortKey = "InputEndDate",            Width = "120", GroupKey = 3, GroupName = "投料数据" },
            new() { Key = "TotalBatchCount",         Label = "原始批次数",     SortKey = "TotalBatchCount",         Width = "80",                              GroupKey = 3, GroupName = "投料数据" },
            new() { Key = "InputQuantity",           Label = "原始投料支数",    SortKey = "InputQuantity",           Width = "80", Visible = false, GroupKey = 3, GroupName = "投料数据" },
            new() { Key = "InputWeight",             Label = "原始投料重量",    SortKey = "InputWeight",             Width = "80", Visible = false, GroupKey = 3, GroupName = "投料数据" },
            new() { Key = "TheoreticalOutputQty",    Label = "理论产出支数",    SortKey = "TheoreticalOutputQty",    Width = "80", Visible = false, GroupKey = 3, GroupName = "投料数据" },
            new() { Key = "TheoreticalOutputWeight", Label = "理论产出重量",    SortKey = "TheoreticalOutputWeight", Width = "80", Visible = false, GroupKey = 3, GroupName = "投料数据" },
            new() { Key = "InputOutputRatio",        Label = "原始成品比",     SortKey = "InputOutputRatio",        Width = "80",                             GroupKey = 3, GroupName = "投料数据" },
            new() { Key = "InputStatus",             Label = "原始投料状态",    SortKey = "InputStatus",             FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未投料"), new("1","部分"), new("2","满足") }, GroupKey = 3, GroupName = "投料数据" },
            new() { Key = "MainNoInputOutputRatio",  Label = "主号成品比",     SortKey = "MainNoInputOutputRatio",  Width = "80", Visible = false, GroupKey = 3, GroupName = "投料数据" },
            new() { Key = "MainNoInputStatus",       Label = "主号投料状态",    SortKey = "MainNoInputStatus",       FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未投料"), new("1","部分"), new("2","满足") }, Visible = false, GroupKey = 3, GroupName = "投料数据" },
        };

        // G7: 有效流转
        var g7 = new List<ColumnDef>
        {
            new() { Key = "FlowOutputRatio",        Label = "流转成品比",     SortKey = "FlowOutputRatio",        Width = "80",                             GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowStatus",             Label = "有效流转状态",    SortKey = "FlowStatus",             FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未投料"), new("1","部分"), new("2","满足") }, GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "MainNoFlowOutputRatio",  Label = "有效主号流转比", SortKey = "MainNoFlowOutputRatio",   Width = "80", Visible = false,       GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "MainNoFlowStatus",       Label = "有效主号状态",   SortKey = "MainNoFlowStatus",       FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未计划"), new("1","部分"), new("2","满足") }, Visible = false, GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowMaxRemainingWorkDays", Label = "最大剩余工量(天)",SortKey = "FlowMaxRemainingWorkDays", Width = "80",                         GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowTotalBatchCount",        Label = "流转总批次数",   SortKey = "FlowTotalBatchCount",        Width = "80", Visible = false, GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowIncompleteBatchCount",   Label = "流转未完成批次数",SortKey = "FlowIncompleteBatchCount",   Width = "80", Visible = false, GroupKey = 7, GroupName = "有效流转" },
        };

        // G10: 汇总不合格
        var g10 = new List<ColumnDef>
        {
            new() { Key = "GeneralDefectWeight",     Label = "一般问题重",     SortKey = "GeneralDefectWeight",     Width = "80",                          GroupKey = 10, GroupName = "汇总不合格" },
            new() { Key = "GeneralDefectRatio",      Label = "一般问题占比",   SortKey = "GeneralDefectRatio",      Width = "80",                          GroupKey = 10, GroupName = "汇总不合格" },
            new() { Key = "SeriousDefectWeight",     Label = "严重问题重",     SortKey = "SeriousDefectWeight",     Width = "80", Visible = false,         GroupKey = 10, GroupName = "汇总不合格" },
            new() { Key = "SeriousDefectRatio",      Label = "严重问题占比",   SortKey = "SeriousDefectRatio",      Width = "80", Visible = false,         GroupKey = 10, GroupName = "汇总不合格" },
            new() { Key = "ScrapWeight",             Label = "成检报废重量",   SortKey = "ScrapWeight",            Width = "80",                          GroupKey = 10, GroupName = "汇总不合格" },
            new() { Key = "ScrapRatio",              Label = "成检报废占比",   SortKey = "ScrapRatio",              Width = "80",                          GroupKey = 10, GroupName = "汇总不合格" },
        };

        // G12: 实时关注
        var g12 = new List<ColumnDef>
        {
            new() { Key = "ScheduleStage",           Label = "关注状态",      SortKey = "ScheduleStage",           FilterType = "enum", Width = "120", EnumOptions = new() { new("0","无需排产"), new("1","原料锁定"), new("2","生产执行"), new("3","成品检验") }, GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "TotalRemainingWorkDays",  Label = "剩余总工量(天)",SortKey = "TotalRemainingWorkDays",  Width = "80",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "UrgencyLevel",            Label = "工单计划性",    SortKey = "UrgencyLevel",            FilterType = "string", Width = "120",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "EstimatedProcessCompletionDate",Label = "工艺预计完成日",SortKey = "EstimatedProcessCompletionDate", Width = "120",                  GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "DaysDiffFromDelivery",    Label = "交期相差天数",  SortKey = "DaysDiffFromDelivery",    Width = "80",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "RawMaterialLockRemark",   Label = "原锁备注",     SortKey = "RawMaterialLockRemark",   FilterType = "string", Width = "120",                             GroupKey = 12, GroupName = "实时关注" },
        };

        // G13: 销售催单
        var g13 = new List<ColumnDef>
        {
            new() { Key = "SalesUrging",             Label = "销售催单",      SortKey = "SalesUrging",             FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "销售催单" },
            new() { Key = "UrgingRemark",            Label = "催单备注",      SortKey = "UrgingRemark",            FilterType = "string", Width = "200", GroupKey = 13, GroupName = "销售催单" },
        };

        // G14: 执行情况
        var g14 = new List<ColumnDef>
        {
            new() { Key = "CurrentScheduleStage",        Label = "现关注状态",     SortKey = "CurrentScheduleStage",        FilterType = "enum", Width = "120", EnumOptions = new() { new("0","无需排产"), new("1","原料锁定"), new("2","生产执行"), new("3","成品检验") }, Visible = false, GroupKey = 14, GroupName = "执行情况" },
            new() { Key = "CurrentRawMaterialLockRemark", Label = "现原锁备注",    SortKey = "CurrentRawMaterialLockRemark", FilterType = "string", Width = "120", Visible = false, GroupKey = 14, GroupName = "执行情况" },
            new() { Key = "IsExecuted",                   Label = "已执行",        SortKey = "IsExecuted",                    FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 14, GroupName = "执行情况" },
        };

        // G15: 预执行（页面操作标记）
        var g15 = new List<ColumnDef>
        {
            new() { Key = "IsPreInput",                  Label = "执行",          SortKey = "IsPreInput",                    FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 15, GroupName = "预执行" },
            new() { Key = "IsMainNoMaterialComplete",    Label = "主号齐全",      SortKey = "IsMainNoMaterialComplete",      FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 15, GroupName = "预执行" },
        };

        var all = new List<ColumnDef>();
        all.AddRange(g1);
        all.AddRange(g2);
        all.AddRange(g5);
        all.AddRange(g3);
        all.AddRange(g7);
        all.AddRange(g10);
        all.AddRange(g12);
        all.AddRange(g13);
        all.AddRange(g14);
        all.AddRange(g15);
        return all;
    }

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(RawMaterialLockPlanAndExecutionDto)
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

    private async Task<TableData<RawMaterialLockPlanAndExecutionDto>> LoadDataFromServer(TableState state)
    {
        // 保持 RowsPerPage 与用户选择同步，避免排序/筛选后复位
        _pageSize = state.PageSize;

        if (_isFirstLoad)
        {
            state.Page = _restoredPageIndex;
            _isFirstLoad = false;
        }

        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "ScheduleStage";
            var filtersJson = SerializeFilters();

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending
            };
            if (filtersJson != null)
            {
                query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson);
            }

            var result = await RawMaterialLockPlanService.GetPagedAsync(query);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPageIndex = state.Page + 1;
                ComputePageSums();
                await SavePageStateAsync();
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

        return new TableData<RawMaterialLockPlanAndExecutionDto>
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
            var result = await RawMaterialLockPlanService.GetFilterContextsAsync();
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

        // DelayPenalty 列显示中文
        if (_filterContextOptions.TryGetValue("DelayPenalty", out var delayOptions))
        {
            foreach (var opt in delayOptions)
                opt.Display = opt.Value == "True" ? "是" : "否";
        }

        // 补充枚举列筛选选项
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

    // ========== 操作按钮 ==========

    private async Task PlanArrangement()
    {
        _isPlanning = true;
        try
        {
            var result = await RawMaterialLockPlanService.PlanArrangementAsync();
            if (result.Success)
            {
                Snackbar.Add($"计划安排完成，共{result.Data}条", Severity.Success);
            }
            else
            {
                Snackbar.Add(result.Message ?? "计划安排失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"计划安排失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isPlanning = false;
        }
        if (table != null) await table.ReloadServerData();
    }

    private async Task ExecuteDataUpdate()
    {
        _isUpdating = true;
        try
        {
            var result = await RawMaterialLockPlanService.ExecuteDataUpdateAsync();
            if (result.Success)
            {
                Snackbar.Add($"执行数据更新完成，共更新{result.Data}条", Severity.Success);
            }
            else
            {
                Snackbar.Add(result.Message ?? "执行数据更新失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"执行数据更新失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isUpdating = false;
        }
        if (table != null) await table.ReloadServerData();
    }

    // ========== 预执行内联操作 ==========

    private async Task TogglePreInput(RawMaterialLockPlanAndExecutionDto item, bool newValue)
    {
        var ids = new List<int> { item.WorkOrderId };
        var result = await RawMaterialLockPlanService.SetPreExecuteFlagsAsync(ids, newValue, null);
        if (result.Success)
            item.IsPreInput = newValue;
        else
            Snackbar.Add(result.Message ?? "操作失败", Severity.Error);
    }

    private async Task ToggleMainNoComplete(RawMaterialLockPlanAndExecutionDto item, bool newValue)
    {
        var ids = new List<int> { item.WorkOrderId };
        var result = await RawMaterialLockPlanService.SetPreExecuteFlagsAsync(ids, null, newValue);
        if (result.Success)
            item.IsMainNoMaterialComplete = newValue;
        else
            Snackbar.Add(result.Message ?? "操作失败", Severity.Error);
    }

    // ========== 分组 CSS ==========

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1",
            2 => "col-g2",
            5 => "col-g5",
            3 => "col-g3",
            7 => "col-g7",
            10 => "col-g10",
            12 => "col-g12",
            13 => "col-g13",
            14 => "col-g14",
            15 => "col-g15",
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
            5 => "col-g5-cell",
            3 => "col-g3-cell",
            7 => "col-g7-cell",
            10 => "col-g10-cell",
            12 => "col-g12-cell",
            13 => "col-g13-cell",
            14 => "col-g14-cell",
            15 => "col-g15-cell",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();

        var savedState = await PageState.LoadAsync("rawmateriallockplan");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "ScheduleStage";
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
        }

        if (savedState != null && table != null)
            await table.ReloadServerData();

        await LoadFilterContextsAsync();
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(RawMaterialLockPlanAndExecutionDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "WorkOrderNo":
                builder.AddContent(0, item.WorkOrderNo);
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
            case "DelayPenalty":
                builder.AddContent(0, item.DelayPenaltyText);
                break;
            case "SettlementMethod":
                builder.AddContent(0, DisplayHelper.GetSettlementMethodText(item.SettlementMethod));
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
            case "MaterialName":
                builder.AddContent(0, DisplayHelper.GetMaterialNameText(item.MaterialName));
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
            case "LengthStatus":
                builder.AddContent(0, DisplayHelper.GetLengthStatusText(item.LengthStatus));
                break;
            case "MinLength":
                builder.AddContent(0, item.MinLength?.ToString("G29") ?? "-");
                break;
            case "MaxLength":
                builder.AddContent(0, item.MaxLength?.ToString("G29") ?? "-");
                break;
            case "TotalQuantity":
                builder.AddContent(0, item.TotalQuantity);
                break;
            case "TotalItemCount":
                builder.AddContent(0, item.TotalItemCount);
                break;
            case "TotalMeters":
                builder.AddContent(0, ((int)item.TotalMeters).ToString());
                break;
            case "TotalWeight":
                builder.AddContent(0, ((int)item.TotalWeight).ToString());
                break;

            // G2
            case "LatestPlanDate":
                builder.AddContent(0, item.LatestPlanDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "MaterialPlanRate":
                builder.AddContent(0, item.MaterialPlanRate > 0 ? $"{item.MaterialPlanRate:F1}%" : "-");
                break;
            case "MaterialPlanStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanStatusColor(item.MaterialPlanStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetMaterialPlanStatusText(item.MaterialPlanStatus))));
                builder.CloseComponent();
                break;
            case "ProcessCycle":
                builder.AddContent(0, item.ProcessCycle > 0 ? $"{item.ProcessCycle}天" : "-");
                break;
            case "MainNoMaterialPlanRate":
                builder.AddContent(0, item.MainNoMaterialPlanRate > 0 ? $"{item.MainNoMaterialPlanRate:F1}%" : "-");
                break;
            case "MainNoMaterialPlanStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanStatusColor(item.MainNoMaterialPlanStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetMaterialPlanStatusText(item.MainNoMaterialPlanStatus))));
                builder.CloseComponent();
                break;

            // G5
            case "PendingRoughTubeQty":
                builder.AddContent(0, item.PendingRoughTubeQty > 0 ? item.PendingRoughTubeQty.ToString() : "-");
                break;
            case "PendingRoughTubeWeight":
                builder.AddContent(0, item.PendingRoughTubeWeight > 0 ? ((int)item.PendingRoughTubeWeight).ToString() : "-");
                break;
            case "PendingOutsourceFinishQty":
                builder.AddContent(0, item.PendingOutsourceFinishQty > 0 ? item.PendingOutsourceFinishQty.ToString() : "-");
                break;
            case "PendingOutsourceFinishWeight":
                builder.AddContent(0, item.PendingOutsourceFinishWeight > 0 ? ((int)item.PendingOutsourceFinishWeight).ToString() : "-");
                break;
            case "TheoreticalFinishQty":
                builder.AddContent(0, item.TheoreticalFinishQty > 0 ? ((int)item.TheoreticalFinishQty).ToString() : "-");
                break;
            case "TheoreticalFinishWeight":
                builder.AddContent(0, item.TheoreticalFinishWeight > 0 ? ((int)item.TheoreticalFinishWeight).ToString() : "-");
                break;

            // G3
            case "InputStartDate":
                builder.AddContent(0, item.InputStartDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "InputEndDate":
                builder.AddContent(0, item.InputEndDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "TotalBatchCount":
                builder.AddContent(0, item.TotalBatchCount);
                break;
            case "InputOutputRatio":
                builder.AddContent(0, item.InputOutputRatio > 0 ? $"{item.InputOutputRatio:F1}%" : "-");
                break;
            case "InputStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetInputStatusColor(item.InputStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetInputStatusText(item.InputStatus))));
                builder.CloseComponent();
                break;
            case "InputQuantity":
                builder.AddContent(0, item.InputQuantity > 0 ? item.InputQuantity.ToString() : "-");
                break;
            case "InputWeight":
                builder.AddContent(0, item.InputWeight > 0 ? ((int)item.InputWeight).ToString() : "-");
                break;
            case "TheoreticalOutputQty":
                builder.AddContent(0, item.TheoreticalOutputQty > 0 ? ((int)item.TheoreticalOutputQty).ToString() : "-");
                break;
            case "TheoreticalOutputWeight":
                builder.AddContent(0, item.TheoreticalOutputWeight > 0 ? ((int)item.TheoreticalOutputWeight).ToString() : "-");
                break;
            case "MainNoInputOutputRatio":
                builder.AddContent(0, item.MainNoInputOutputRatio > 0 ? $"{item.MainNoInputOutputRatio:F1}%" : "-");
                break;
            case "MainNoInputStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetInputStatusColor(item.MainNoInputStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetInputStatusText(item.MainNoInputStatus))));
                builder.CloseComponent();
                break;

            // G7
            case "FlowOutputRatio":
                builder.AddContent(0, item.FlowOutputRatio > 0 ? $"{item.FlowOutputRatio:F1}%" : "-");
                break;
            case "FlowStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetInputStatusColor(item.FlowStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetFlowStatusText(item.FlowStatus))));
                builder.CloseComponent();
                break;
            case "MainNoFlowOutputRatio":
                builder.AddContent(0, item.MainNoFlowOutputRatio > 0 ? $"{item.MainNoFlowOutputRatio:F1}%" : "-");
                break;
            case "MainNoFlowStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetInputStatusColor(item.MainNoFlowStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetValidMainNoStatusText(item.MainNoFlowStatus))));
                builder.CloseComponent();
                break;
            case "FlowMaxRemainingWorkDays":
                builder.AddContent(0, item.FlowMaxRemainingWorkDays > 0 ? $"{item.FlowMaxRemainingWorkDays}天" : "-");
                break;
            case "FlowTotalBatchCount":
                builder.AddContent(0, item.FlowTotalBatchCount > 0 ? item.FlowTotalBatchCount.ToString() : "-");
                break;
            case "FlowIncompleteBatchCount":
                builder.AddContent(0, item.FlowIncompleteBatchCount > 0 ? item.FlowIncompleteBatchCount.ToString() : "-");
                break;

            // G10
            case "GeneralDefectWeight":
                builder.AddContent(0, item.GeneralDefectWeight > 0 ? ((int)item.GeneralDefectWeight).ToString() : "-");
                break;
            case "GeneralDefectRatio":
                builder.AddContent(0, item.GeneralDefectRatio > 0 ? $"{item.GeneralDefectRatio:F1}%" : "-");
                break;
            case "SeriousDefectWeight":
                builder.AddContent(0, item.SeriousDefectWeight > 0 ? ((int)item.SeriousDefectWeight).ToString() : "-");
                break;
            case "SeriousDefectRatio":
                builder.AddContent(0, item.SeriousDefectRatio > 0 ? $"{item.SeriousDefectRatio:F1}%" : "-");
                break;
            case "ScrapWeight":
                builder.AddContent(0, item.ScrapWeight > 0 ? ((int)item.ScrapWeight).ToString() : "-");
                break;
            case "ScrapRatio":
                builder.AddContent(0, item.ScrapRatio > 0 ? $"{item.ScrapRatio:F1}%" : "-");
                break;

            // G12
            case "ScheduleStage":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetScheduleStageColor(item.ScheduleStage));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.ScheduleStageText)));
                builder.CloseComponent();
                break;
            case "TotalRemainingWorkDays":
                builder.AddContent(0, item.TotalRemainingWorkDays.HasValue ? $"{item.TotalRemainingWorkDays}天" : "-");
                break;
            case "UrgencyLevel":
                builder.AddContent(0, item.UrgencyLevel ?? "-");
                break;
            case "EstimatedProcessCompletionDate":
                builder.AddContent(0, item.EstimatedProcessCompletionDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "DaysDiffFromDelivery":
                builder.AddContent(0, item.DaysDiffFromDelivery.HasValue ? $"{item.DaysDiffFromDelivery}天" : "-");
                break;
            case "RawMaterialLockRemark":
                builder.AddContent(0, item.RawMaterialLockRemark ?? "-");
                break;

            // G13
            case "SalesUrging":
                builder.AddContent(0, item.SalesUrgingText);
                break;
            case "UrgingRemark":
                builder.AddContent(0, item.UrgingRemark ?? "-");
                break;

            // G14
            case "CurrentScheduleStage":
                builder.AddContent(0, item.CurrentScheduleStageText ?? "-");
                break;
            case "CurrentRawMaterialLockRemark":
                builder.AddContent(0, item.CurrentRawMaterialLockRemark ?? "-");
                break;
            case "IsExecuted":
                builder.AddContent(0, item.IsExecutedText);
                break;

            // G15: 预执行
            case "IsPreInput":
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "style", "display:flex; align-items:center; gap:4px;");
                builder.OpenComponent<MudSwitch<bool>>(2);
                builder.AddAttribute(3, "Value", item.IsPreInput);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<bool>(this, async v =>
                {
                    await TogglePreInput(item, v);
                }));
                builder.AddAttribute(5, "Color", Color.Primary);
                builder.AddAttribute(6, "Dense", true);
                builder.CloseComponent();
                builder.AddContent(7, item.IsPreInput ? "是" : "否");
                builder.CloseElement();
                break;
            case "IsMainNoMaterialComplete":
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "style", "display:flex; align-items:center; gap:4px;");
                builder.OpenComponent<MudSwitch<bool>>(2);
                builder.AddAttribute(3, "Value", item.IsMainNoMaterialComplete);
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<bool>(this, async v =>
                {
                    await ToggleMainNoComplete(item, v);
                }));
                builder.AddAttribute(5, "Color", Color.Primary);
                builder.AddAttribute(6, "Dense", true);
                builder.CloseComponent();
                builder.AddContent(7, item.IsMainNoMaterialComplete ? "是" : "否");
                builder.CloseElement();
                break;
        }
    };

    // ========== 文本辅助 ==========

    private static string GetMaterialPlanStatusText(int status) => status switch
    {
        0 => "未计划", 1 => "部分", 2 => "理论满足", 3 => "满足", 4 => "超量", _ => "未知"
    };

    private static string GetInputStatusText(int status) => status switch
    {
        0 => "未投料", 1 => "部分", 2 => "满足", _ => "未知"
    };

    private static string GetFlowStatusText(int status) => status switch
    {
        0 => "未投料", 1 => "部分", 2 => "满足", _ => "未知"
    };

    private static string GetValidMainNoStatusText(int status) => status switch
    {
        0 => "未计划", 1 => "部分", 2 => "满足", _ => "未知"
    };

    // ========== 颜色 ==========

    private static Color GetPlanStatusColor(int status) => status switch
    {
        0 => Color.Default,
        1 => Color.Warning,
        2 => Color.Info,
        3 => Color.Success,
        4 => Color.Error,
        _ => Color.Default
    };

    private static Color GetInputStatusColor(int status) => status switch
    {
        0 => Color.Default,
        1 => Color.Warning,
        2 => Color.Success,
        _ => Color.Default
    };

    private static Color GetScheduleStageColor(int stage) => stage switch
    {
        0 => Color.Default,
        1 => Color.Warning,
        2 => Color.Success,
        3 => Color.Info,
        _ => Color.Default
    };

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));

        extras["columnVisibility"] = JsonSerializer.Serialize(_allColumns.Where(c => c.Visible).Select(c => c.Key).ToList());

        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPageIndex,
            Extras = extras
        };
        await PageState.SaveAsync("rawmateriallockplan", state);
    }
}
