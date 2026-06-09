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
    private List<RawMaterialLockPlanAndExecutionDto> _allItems = new();
    private List<RawMaterialLockPlanAndExecutionDto> _filteredItems = new();
    private bool _isLoading;

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

    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

    // 待成检到料批次卡片摘要
    private int _preInputCount;
    private decimal _preInputWeight;

    // 排序状态
    private string sortColumn = "ScheduleStage";
    private bool sortDescending = true;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // 非空/空筛选常量
    private const string FilterNotNull = "__NOT_NULL__";
    private const string FilterNull = "__EXCEL_FILTER_NULL__";

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
            new() { Key = "MaterialPlanProportion",   Label = "用料占比",       SortKey = "MaterialPlanProportion",   Width = "120",                             GroupKey = 2, GroupName = "用料计划" },
            new() { Key = "LatestRequiredDate",       Label = "要求到货日",      SortKey = "LatestRequiredDate",      Width = "120",                             GroupKey = 2, GroupName = "用料计划" },
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
            new() { Key = "CapacityWorkDays",         Label = "产能工量(天)",  SortKey = "CapacityWorkDays",         Width = "80",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "UrgencyLevel",            Label = "工单计划性",    SortKey = "UrgencyLevel",            FilterType = "string", Width = "120",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "EstimatedProcessCompletionDate",Label = "工艺预计完成日",SortKey = "EstimatedProcessCompletionDate", Width = "120",                  GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "DaysDiffFromDelivery",    Label = "交期相差天数",  SortKey = "DaysDiffFromDelivery",    Width = "80",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "RawMaterialLockRemark",   Label = "原锁备注",     SortKey = "RawMaterialLockRemark",   FilterType = "string", Width = "120",                             GroupKey = 12, GroupName = "实时关注" },
        };

        // G13: 订单需求调整
        var g13 = new List<ColumnDef>
        {
            new() { Key = "IsUrging",             Label = "催单",           SortKey = "IsUrging",             FilterType = "boolean", Width = "80",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "订单需求调整" },
            new() { Key = "IsBatchDelivery",      Label = "分批交货",       SortKey = "IsBatchDelivery",      FilterType = "boolean", Width = "80",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "订单需求调整" },
            new() { Key = "IsPaused",             Label = "工单暂停",       SortKey = "IsPaused",             FilterType = "boolean", Width = "80",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "订单需求调整" },
            new() { Key = "AdjustmentRemark",     Label = "调整备注",       SortKey = "AdjustmentRemark",     FilterType = "string",  Width = "200", GroupKey = 13, GroupName = "订单需求调整" },
        };

        // G15: 预执行（页面操作标记）
        var g15 = new List<ColumnDef>
        {
            new() { Key = "IsPreInput",                  Label = "执行",          SortKey = "IsPreInput",                    FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 15, GroupName = "预执行" },
            new() { Key = "BudgetInputDate",             Label = "预算投料日",    SortKey = "BudgetInputDate",               Width = "130", GroupKey = 15, GroupName = "预执行" },
            new() { Key = "ExecutionError",              Label = "执行错误",      SortKey = "ExecutionError",                FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 15, GroupName = "预执行" },
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
        all.AddRange(g15);
        return all;
    }

    // ========== 分页汇总 ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_filteredItems.Count == 0) return;

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
                    var sum = _filteredItems.Sum(item => (int)(prop.GetValue(item) ?? 0));
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal))
                {
                    var sum = _filteredItems.Sum(item => (decimal)(prop.GetValue(item) ?? 0m));
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
                else if (type == typeof(int?))
                {
                    var sum = _filteredItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal?))
                {
                    var sum = _filteredItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
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
        try
        {
            var query = new QueryParams
            {
                PageIndex = 1,
                PageSize = 50000,
                SortBy = "ScheduleStage",
                IsDescending = true
            };
            var result = await RawMaterialLockPlanService.GetPagedAsync(query);
            if (result.Success && result.Data != null)
            {
                _allItems = result.Data.Items ?? new();
                _preInputCount = _allItems.Count(x => x.IsPreInput);
                _preInputWeight = _allItems.Where(x => x.IsPreInput).Sum(x => x.TotalWeight);
            }
            else
            {
                _allItems = new();
                Snackbar.Add(result?.Message ?? "获取数据失败", Severity.Error);
                _preInputCount = 0;
                _preInputWeight = 0m;
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
            if (col.FilterType == "number")
            {
                _filterContextOptions[col.Key] = new List<ExcelFilterOption>
                {
                    new() { Value = FilterNotNull, Display = "非空", Count = _allItems.Count(x => GetFilterValue(x, col.Key) != null) },
                    new() { Value = FilterNull,    Display = "空",   Count = _allItems.Count(x => GetFilterValue(x, col.Key) == null) },
                };
            }
            else if (col.FilterType == "string")
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
                // 使用 EnumOptions 映射
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
            else if (col.FilterType == "boolean")
            {
                _filterContextOptions[col.Key] = new List<ExcelFilterOption>
                {
                    new() { Value = "True", Display = col.BoolTrueLabel ?? "是", Count = _allItems.Count(x => GetFilterValue(x, col.Key) == "True") },
                    new() { Value = "False", Display = col.BoolFalseLabel ?? "否", Count = _allItems.Count(x => GetFilterValue(x, col.Key) == "False") },
                };
            }
        }
    }

    private static string? GetFilterValue(RawMaterialLockPlanAndExecutionDto item, string key) => key switch
    {
        "WorkOrderNo" => item.WorkOrderNo,
        "Salesman" => item.Salesman,
        "CustomerName" => item.CustomerName,
        "SalesOrderNo" => item.SalesOrderNo,
        "ProductionMainNo" => item.ProductionMainNo,
        "ProductionSubNo" => item.ProductionSubNo,
        "PlantGrade" => item.PlantGrade,
        "Specification" => item.Specification,
        "SettlementMethod" => item.SettlementMethod,
        "MaterialName" => item.MaterialName,
        "DeliveryState" => item.DeliveryState,
        "LengthStatus" => item.LengthStatus,
        "MaterialPlanStatus" => item.MaterialPlanStatus.ToString(),
        "MainNoMaterialPlanStatus" => item.MainNoMaterialPlanStatus.ToString(),
        "InputStatus" => item.InputStatus.ToString(),
        "MainNoInputStatus" => item.MainNoInputStatus.ToString(),
        "FlowStatus" => item.FlowStatus.ToString(),
        "MainNoFlowStatus" => item.MainNoFlowStatus.ToString(),
        "ScheduleStage" => item.ScheduleStage.ToString(),
        "UrgencyLevel" => item.UrgencyLevel,
        "RawMaterialLockRemark" => item.RawMaterialLockRemark,
        "AdjustmentRemark" => item.AdjustmentRemark,
        "DelayPenalty" => item.DelayPenalty.ToString(),
        "IsUrging" => item.IsUrging.ToString(),
        "IsBatchDelivery" => item.IsBatchDelivery.ToString(),
        "IsPaused" => item.IsPaused.ToString(),
        "IsPreInput" => item.IsPreInput.ToString(),
        "ExecutionError" => item.ExecutionError.ToString(),
        "IsMainNoMaterialComplete" => item.IsMainNoMaterialComplete.ToString(),
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
                (x.PlantGrade?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.Specification?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.ProductionMainNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true));
        }

        // 列筛选
        foreach (var kvp in _columnFilters)
        {
            if (kvp.Value.Count == 0) continue;

            var col = _allColumns.FirstOrDefault(c => c.Key == kvp.Key);
            if (col == null) continue;

            if (col.FilterType == "string")
            {
                query = query.Where(x =>
                {
                    var val = GetFilterValue(x, kvp.Key);
                    return val != null && kvp.Value.Contains(val, StringComparer.OrdinalIgnoreCase);
                });
            }
            else if (col.FilterType == "enum" || col.FilterType == "boolean")
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
            "Salesman" => sortDescending ? query.OrderByDescending(x => x.Salesman) : query.OrderBy(x => x.Salesman),
            "CustomerName" => sortDescending ? query.OrderByDescending(x => x.CustomerName) : query.OrderBy(x => x.CustomerName),
            "SignDate" => sortDescending ? query.OrderByDescending(x => x.SignDate) : query.OrderBy(x => x.SignDate),
            "DeliveryDate" => sortDescending ? query.OrderByDescending(x => x.DeliveryDate) : query.OrderBy(x => x.DeliveryDate),
            "DelayPenalty" => sortDescending ? query.OrderByDescending(x => x.DelayPenalty) : query.OrderBy(x => x.DelayPenalty),
            "SettlementMethod" => sortDescending ? query.OrderByDescending(x => x.SettlementMethod) : query.OrderBy(x => x.SettlementMethod),
            "SalesOrderNo" => sortDescending ? query.OrderByDescending(x => x.SalesOrderNo) : query.OrderBy(x => x.SalesOrderNo),
            "ProductionMainNo" => sortDescending ? query.OrderByDescending(x => x.ProductionMainNo) : query.OrderBy(x => x.ProductionMainNo),
            "ProductionSubNo" => sortDescending ? query.OrderByDescending(x => x.ProductionSubNo) : query.OrderBy(x => x.ProductionSubNo),
            "MaterialName" => sortDescending ? query.OrderByDescending(x => x.MaterialName) : query.OrderBy(x => x.MaterialName),
            "DeliveryState" => sortDescending ? query.OrderByDescending(x => x.DeliveryState) : query.OrderBy(x => x.DeliveryState),
            "PlantGrade" => sortDescending ? query.OrderByDescending(x => x.PlantGrade) : query.OrderBy(x => x.PlantGrade),
            "Specification" => sortDescending ? query.OrderByDescending(x => x.Specification) : query.OrderBy(x => x.Specification),
            "LengthStatus" => sortDescending ? query.OrderByDescending(x => x.LengthStatus) : query.OrderBy(x => x.LengthStatus),
            "MinLength" => sortDescending ? query.OrderByDescending(x => x.MinLength) : query.OrderBy(x => x.MinLength),
            "MaxLength" => sortDescending ? query.OrderByDescending(x => x.MaxLength) : query.OrderBy(x => x.MaxLength),
            "TotalItemCount" => sortDescending ? query.OrderByDescending(x => x.TotalItemCount) : query.OrderBy(x => x.TotalItemCount),
            "TotalQuantity" => sortDescending ? query.OrderByDescending(x => x.TotalQuantity) : query.OrderBy(x => x.TotalQuantity),
            "TotalMeters" => sortDescending ? query.OrderByDescending(x => x.TotalMeters) : query.OrderBy(x => x.TotalMeters),
            "TotalWeight" => sortDescending ? query.OrderByDescending(x => x.TotalWeight) : query.OrderBy(x => x.TotalWeight),
            "LatestPlanDate" => sortDescending ? query.OrderByDescending(x => x.LatestPlanDate) : query.OrderBy(x => x.LatestPlanDate),
            "MaterialPlanRate" => sortDescending ? query.OrderByDescending(x => x.MaterialPlanRate) : query.OrderBy(x => x.MaterialPlanRate),
            "MaterialPlanStatus" => sortDescending ? query.OrderByDescending(x => x.MaterialPlanStatus) : query.OrderBy(x => x.MaterialPlanStatus),
            "MainNoMaterialPlanRate" => sortDescending ? query.OrderByDescending(x => x.MainNoMaterialPlanRate) : query.OrderBy(x => x.MainNoMaterialPlanRate),
            "MainNoMaterialPlanStatus" => sortDescending ? query.OrderByDescending(x => x.MainNoMaterialPlanStatus) : query.OrderBy(x => x.MainNoMaterialPlanStatus),
            "ProcessCycle" => sortDescending ? query.OrderByDescending(x => x.ProcessCycle) : query.OrderBy(x => x.ProcessCycle),
            "MaterialPlanProportion" => sortDescending ? query.OrderByDescending(x => x.MaterialPlanProportion) : query.OrderBy(x => x.MaterialPlanProportion),
            "LatestRequiredDate" => sortDescending ? query.OrderByDescending(x => x.LatestRequiredDate) : query.OrderBy(x => x.LatestRequiredDate),
            "PendingRoughTubeQty" => sortDescending ? query.OrderByDescending(x => x.PendingRoughTubeQty) : query.OrderBy(x => x.PendingRoughTubeQty),
            "PendingRoughTubeWeight" => sortDescending ? query.OrderByDescending(x => x.PendingRoughTubeWeight) : query.OrderBy(x => x.PendingRoughTubeWeight),
            "PendingOutsourceFinishQty" => sortDescending ? query.OrderByDescending(x => x.PendingOutsourceFinishQty) : query.OrderBy(x => x.PendingOutsourceFinishQty),
            "PendingOutsourceFinishWeight" => sortDescending ? query.OrderByDescending(x => x.PendingOutsourceFinishWeight) : query.OrderBy(x => x.PendingOutsourceFinishWeight),
            "TheoreticalFinishQty" => sortDescending ? query.OrderByDescending(x => x.TheoreticalFinishQty) : query.OrderBy(x => x.TheoreticalFinishQty),
            "TheoreticalFinishWeight" => sortDescending ? query.OrderByDescending(x => x.TheoreticalFinishWeight) : query.OrderBy(x => x.TheoreticalFinishWeight),
            "InputStartDate" => sortDescending ? query.OrderByDescending(x => x.InputStartDate) : query.OrderBy(x => x.InputStartDate),
            "InputEndDate" => sortDescending ? query.OrderByDescending(x => x.InputEndDate) : query.OrderBy(x => x.InputEndDate),
            "TotalBatchCount" => sortDescending ? query.OrderByDescending(x => x.TotalBatchCount) : query.OrderBy(x => x.TotalBatchCount),
            "InputQuantity" => sortDescending ? query.OrderByDescending(x => x.InputQuantity) : query.OrderBy(x => x.InputQuantity),
            "InputWeight" => sortDescending ? query.OrderByDescending(x => x.InputWeight) : query.OrderBy(x => x.InputWeight),
            "TheoreticalOutputQty" => sortDescending ? query.OrderByDescending(x => x.TheoreticalOutputQty) : query.OrderBy(x => x.TheoreticalOutputQty),
            "TheoreticalOutputWeight" => sortDescending ? query.OrderByDescending(x => x.TheoreticalOutputWeight) : query.OrderBy(x => x.TheoreticalOutputWeight),
            "InputOutputRatio" => sortDescending ? query.OrderByDescending(x => x.InputOutputRatio) : query.OrderBy(x => x.InputOutputRatio),
            "InputStatus" => sortDescending ? query.OrderByDescending(x => x.InputStatus) : query.OrderBy(x => x.InputStatus),
            "MainNoInputOutputRatio" => sortDescending ? query.OrderByDescending(x => x.MainNoInputOutputRatio) : query.OrderBy(x => x.MainNoInputOutputRatio),
            "MainNoInputStatus" => sortDescending ? query.OrderByDescending(x => x.MainNoInputStatus) : query.OrderBy(x => x.MainNoInputStatus),
            "FlowOutputRatio" => sortDescending ? query.OrderByDescending(x => x.FlowOutputRatio) : query.OrderBy(x => x.FlowOutputRatio),
            "FlowStatus" => sortDescending ? query.OrderByDescending(x => x.FlowStatus) : query.OrderBy(x => x.FlowStatus),
            "MainNoFlowOutputRatio" => sortDescending ? query.OrderByDescending(x => x.MainNoFlowOutputRatio) : query.OrderBy(x => x.MainNoFlowOutputRatio),
            "MainNoFlowStatus" => sortDescending ? query.OrderByDescending(x => x.MainNoFlowStatus) : query.OrderBy(x => x.MainNoFlowStatus),
            "FlowMaxRemainingWorkDays" => sortDescending ? query.OrderByDescending(x => x.FlowMaxRemainingWorkDays) : query.OrderBy(x => x.FlowMaxRemainingWorkDays),
            "FlowTotalBatchCount" => sortDescending ? query.OrderByDescending(x => x.FlowTotalBatchCount) : query.OrderBy(x => x.FlowTotalBatchCount),
            "FlowIncompleteBatchCount" => sortDescending ? query.OrderByDescending(x => x.FlowIncompleteBatchCount) : query.OrderBy(x => x.FlowIncompleteBatchCount),
            "GeneralDefectWeight" => sortDescending ? query.OrderByDescending(x => x.GeneralDefectWeight) : query.OrderBy(x => x.GeneralDefectWeight),
            "GeneralDefectRatio" => sortDescending ? query.OrderByDescending(x => x.GeneralDefectRatio) : query.OrderBy(x => x.GeneralDefectRatio),
            "SeriousDefectWeight" => sortDescending ? query.OrderByDescending(x => x.SeriousDefectWeight) : query.OrderBy(x => x.SeriousDefectWeight),
            "SeriousDefectRatio" => sortDescending ? query.OrderByDescending(x => x.SeriousDefectRatio) : query.OrderBy(x => x.SeriousDefectRatio),
            "ScrapWeight" => sortDescending ? query.OrderByDescending(x => x.ScrapWeight) : query.OrderBy(x => x.ScrapWeight),
            "ScrapRatio" => sortDescending ? query.OrderByDescending(x => x.ScrapRatio) : query.OrderBy(x => x.ScrapRatio),
            "ScheduleStage" => sortDescending ? query.OrderByDescending(x => x.ScheduleStage) : query.OrderBy(x => x.ScheduleStage),
            "TotalRemainingWorkDays" => sortDescending ? query.OrderByDescending(x => x.TotalRemainingWorkDays) : query.OrderBy(x => x.TotalRemainingWorkDays),
            "CapacityWorkDays" => sortDescending ? query.OrderByDescending(x => x.CapacityWorkDays) : query.OrderBy(x => x.CapacityWorkDays),
            "UrgencyLevel" => sortDescending ? query.OrderByDescending(x => x.UrgencyLevel) : query.OrderBy(x => x.UrgencyLevel),
            "EstimatedProcessCompletionDate" => sortDescending ? query.OrderByDescending(x => x.EstimatedProcessCompletionDate) : query.OrderBy(x => x.EstimatedProcessCompletionDate),
            "DaysDiffFromDelivery" => sortDescending ? query.OrderByDescending(x => x.DaysDiffFromDelivery) : query.OrderBy(x => x.DaysDiffFromDelivery),
            "RawMaterialLockRemark" => sortDescending ? query.OrderByDescending(x => x.RawMaterialLockRemark) : query.OrderBy(x => x.RawMaterialLockRemark),
            "IsUrging" => sortDescending ? query.OrderByDescending(x => x.IsUrging) : query.OrderBy(x => x.IsUrging),
            "IsBatchDelivery" => sortDescending ? query.OrderByDescending(x => x.IsBatchDelivery) : query.OrderBy(x => x.IsBatchDelivery),
            "IsPaused" => sortDescending ? query.OrderByDescending(x => x.IsPaused) : query.OrderBy(x => x.IsPaused),
            "AdjustmentRemark" => sortDescending ? query.OrderByDescending(x => x.AdjustmentRemark) : query.OrderBy(x => x.AdjustmentRemark),
            "IsPreInput" => sortDescending ? query.OrderByDescending(x => x.IsPreInput) : query.OrderBy(x => x.IsPreInput),
            "BudgetInputDate" => sortDescending ? query.OrderByDescending(x => x.BudgetInputDate) : query.OrderBy(x => x.BudgetInputDate),
            "ExecutionError" => sortDescending ? query.OrderByDescending(x => x.ExecutionError) : query.OrderBy(x => x.ExecutionError),
            "IsMainNoMaterialComplete" => sortDescending ? query.OrderByDescending(x => x.IsMainNoMaterialComplete) : query.OrderBy(x => x.IsMainNoMaterialComplete),
            _ => sortDescending ? query.OrderByDescending(x => x.ScheduleStage) : query.OrderBy(x => x.ScheduleStage)
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

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        ApplyFiltersAndSort();
        await SavePageStateAsync();
    }

    // ========== 预执行内联操作 ==========

    private async Task TogglePreInput(RawMaterialLockPlanAndExecutionDto item, bool newValue)
    {
        var ids = new List<int> { item.WorkOrderId };
        var result = await RawMaterialLockPlanService.SetPreExecuteFlagsAsync(ids, newValue, null);
        if (result.Success)
        {
            item.IsPreInput = newValue;
            // 更新预执行卡片摘要（增量计算，避免全量重载）
            if (newValue)
            {
                _preInputCount++;
                _preInputWeight += item.TotalWeight;
            }
            else
            {
                _preInputCount = Math.Max(0, _preInputCount - 1);
                _preInputWeight = Math.Max(0, _preInputWeight - item.TotalWeight);
            }
            ApplyFiltersAndSort();
            await SavePageStateAsync();
        }
        else
        {
            Snackbar.Add(result.Message ?? "操作失败", Severity.Error);
        }
    }

    private async Task OnBudgetInputDateChanged(RawMaterialLockPlanAndExecutionDto item, DateTime newDate)
    {
        var ids = new List<int> { item.WorkOrderId };
        var result = await RawMaterialLockPlanService.SetPreExecuteFlagsAsync(ids, null, null, newDate);
        if (result.Success)
        {
            item.BudgetInputDate = newDate;
            ApplyFiltersAndSort();
            await SavePageStateAsync();
        }
        else
        {
            Snackbar.Add(result.Message ?? "保存预算投料日失败", Severity.Error);
        }
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
            15 => "col-g15-cell",
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

        var savedState = await PageState.LoadAsync("rawmateriallockplan");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "ScheduleStage";
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
        }

        await LoadDataAsync();
    }

    // ========== 分组标题栏同步 ==========

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("initGroupHeaders", "#raw-material-lock-plan-list-table");
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
            case "MaterialPlanProportion":
                builder.AddContent(0, item.MaterialPlanProportion ?? "-");
                break;
            case "LatestRequiredDate":
                builder.AddContent(0, item.LatestRequiredDate?.ToString("yyyy-MM-dd") ?? "-");
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
            case "CapacityWorkDays":
                builder.AddContent(0, item.CapacityWorkDays.HasValue ? $"{item.CapacityWorkDays}天" : "-");
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

            // G6
            case "IsUrging":
                builder.AddContent(0, item.UrgingText);
                break;
            case "IsBatchDelivery":
                builder.AddContent(0, item.IsBatchDelivery ? "是" : "否");
                break;
            case "IsPaused":
                builder.AddContent(0, item.IsPaused ? "是" : "否");
                break;
            case "AdjustmentRemark":
                builder.AddContent(0, item.AdjustmentRemark ?? "-");
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
            case "BudgetInputDate":
                if (item.IsPreInput)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", item.BudgetInputDate?.ToString("yyyy-MM-dd") ?? "");
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                    {
                        if (DateTime.TryParseExact(v, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
                            await OnBudgetInputDateChanged(item, date);
                    }));
                    builder.AddAttribute(3, "Placeholder", "yyyy-MM-dd");
                    builder.AddAttribute(4, "Dense", true);
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "ExecutionError":
                if (item.ExecutionError)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Error);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "是")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;
            case "IsMainNoMaterialComplete":
                builder.OpenElement(0, "span");
                var completeColor = item.IsMainNoMaterialComplete ? "color:#1565C0;font-weight:bold" : "color:#999";
                builder.AddAttribute(1, "style", completeColor);
                builder.AddAttribute(2, "class", "pl-2");
                builder.AddContent(3, item.IsMainNoMaterialCompleteText);
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
            PageIndex = 1,
            Extras = extras
        };
        await PageState.SaveAsync("rawmateriallockplan", state);
    }
}
