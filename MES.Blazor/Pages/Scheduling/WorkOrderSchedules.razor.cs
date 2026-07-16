using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Models;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.WorkOrder;
using System.Text.Json;

namespace MES.Blazor.Pages.Scheduling;

public partial class WorkOrderSchedules
{
    private MudTable<WorkOrderScheduleDto>? table;
    private List<WorkOrderScheduleDto> _allItems = new();
    private List<WorkOrderScheduleDto> _filteredItems = new();

    // 排序状态
    private string sortColumn = "WorkOrderNo";
    private bool sortDescending = true;

    private HashSet<WorkOrderScheduleDto> _selectedItems = new();

    private void SelectAllItems(bool selected)
    {
        if (selected)
            _selectedItems = new HashSet<WorkOrderScheduleDto>(_filteredItems);
        else
            _selectedItems.Clear();
    }

    private void ToggleSelection(WorkOrderScheduleDto item, bool selected)
    {
        if (selected)
            _selectedItems.Add(item);
        else
            _selectedItems.Remove(item);
    }

    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

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
        "TotalItemCount", "TotalQuantity", "TotalMeters", "TotalWeight",
        "FlowTotalBatchCount", "FlowIncompleteBatchCount",
        "PendingSectionRoughTube", "PendingSectionWarehouseFix",
        "PendingSection60Roll", "PendingSection50Roll",
        "PendingSection30Roll", "PendingSection20Roll",
        "PendingSectionThreeRoll", "PendingSectionDrawBench",
    };

    // 非空/空筛选常量
    private const string FilterNotNull = "__NOT_NULL__";
    private const string FilterNull = "__EXCEL_FILTER_NULL__";

    // Plan 字段键名（G15 工单计划），用于空值筛选
    private static readonly HashSet<string> _planFieldKeys = new()
    {
        "PlanScheduleStage", "PlanUrgencyLevel",
        "PlanProductionAttentionProcess", "PlanProductionFlowProperty"
    };

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
            new() { Key = "TotalQuantity",           Label = "总支数",          SortKey = "TotalQuantity",           Width = "80", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalWeight",             Label = "总重量",          SortKey = "TotalWeight",             Width = "80", GroupKey = 1, GroupName = "基础数据" },
        };

        // G7: 有效流转
        var g7 = new List<ColumnDef>
        {
            new() { Key = "FlowOutputRatio",        Label = "流转成品比",     SortKey = "FlowOutputRatio",        Width = "80",                             GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowStatus",             Label = "有效流转状态",    SortKey = "FlowStatus",             FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未投料"), new("1","部分"), new("2","满足") }, GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowTotalBatchCount",    Label = "总批次数",       SortKey = "FlowTotalBatchCount",    Width = "80",                              GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowIncompleteBatchCount",Label = "未完成批数",    SortKey = "FlowIncompleteBatchCount",Width = "80",                              GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "MainNoFlowOutputRatio",  Label = "有效主号流转比", SortKey = "MainNoFlowOutputRatio",   Width = "80", Visible = false,       GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "MainNoFlowStatus",       Label = "有效主号状态",   SortKey = "MainNoFlowStatus",       FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未计划"), new("1","部分"), new("2","满足") }, Visible = false, GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowMaxRemainingWorkDays", Label = "最大剩余工量(天)",SortKey = "FlowMaxRemainingWorkDays", Width = "80",                         GroupKey = 7, GroupName = "有效流转" },
        };

        // G12: 实时关注
        var g12 = new List<ColumnDef>
        {
            new() { Key = "ScheduleStage",           Label = "关注状态",      SortKey = "ScheduleStage",           FilterType = "enum", Width = "120", EnumOptions = new() { new("0","工单完成"), new("1","原料锁定"), new("2","生产执行"), new("3","成品检验") }, GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "TotalRemainingWorkDays",  Label = "剩余总工量(天)",SortKey = "TotalRemainingWorkDays",  Width = "80",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "CapacityWorkDays",        Label = "产能工量(天)", SortKey = "CapacityWorkDays",        Width = "80",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "UrgencyLevel",            Label = "工单计划性",    SortKey = "UrgencyLevel",            FilterType = "string", Width = "120", GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "EstimatedProcessCompletionDate",Label = "工艺预计完成日",SortKey = "EstimatedProcessCompletionDate", Width = "120",           GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "DaysDiffFromDelivery",    Label = "交期相差天数",  SortKey = "DaysDiffFromDelivery",    Width = "80",                              GroupKey = 12, GroupName = "实时关注" },
            new() { Key = "RawMaterialLockRemark",   Label = "原锁备注",     SortKey = "RawMaterialLockRemark",   FilterType = "string", Width = "120",     GroupKey = 12, GroupName = "实时关注" },
        };

        // G13: 工单需求调整
        var g13 = new List<ColumnDef>
        {
            new() { Key = "IsUrging",      Label = "催单",  SortKey = "IsUrging",      FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "工单需求调整" },
            new() { Key = "IsBatchDelivery",          Label = "分批交货",      SortKey = "IsBatchDelivery",          FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "工单需求调整" },
            new() { Key = "IsPaused",                  Label = "工单暂停",      SortKey = "IsPaused",                  FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "工单需求调整" },
            new() { Key = "AdjustmentRemark",         Label = "需求调整备注",  SortKey = "AdjustmentRemark",         FilterType = "string", Width = "200", GroupKey = 13, GroupName = "工单需求调整" },
        };

        // G14: 在产节点待量
        var g14 = new List<ColumnDef>
        {
            new() { Key = "PendingSectionRoughTube",       Label = "荒管处理待量",  SortKey = "PendingSectionRoughTube",       Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "PendingSectionWarehouseFix",    Label = "在制修检待量",  SortKey = "PendingSectionWarehouseFix",    Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "PendingSection60Roll",          Label = "60冷轧待量",    SortKey = "PendingSection60Roll",          Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "PendingSection50Roll",          Label = "50冷轧待量",    SortKey = "PendingSection50Roll",          Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "PendingSection30Roll",          Label = "30冷轧待量",    SortKey = "PendingSection30Roll",          Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "PendingSection20Roll",          Label = "20冷轧待量",    SortKey = "PendingSection20Roll",          Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "PendingSectionThreeRoll",       Label = "三辊冷轧待量",  SortKey = "PendingSectionThreeRoll",       Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "PendingSectionDrawBench",       Label = "冷拔待量",      SortKey = "PendingSectionDrawBench",       Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "DeformedProcessCompleted",      Label = "变形完成",      SortKey = "DeformedProcessCompleted",      FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "ProductionAttentionProcess",    Label = "生产关注工序",  SortKey = "ProductionAttentionProcess",    FilterType = "string", Width = "120", GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "ProductionFlowProperty",        Label = "生产流转性",    SortKey = "ProductionFlowProperty",        FilterType = "string", Width = "100", GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "MaxBatchRemainingWorkDays",   Label = "最大剩余工量(天)", SortKey = "MaxBatchRemainingWorkDays",   FilterType = "string", Width = "80",  GroupKey = 14, GroupName = "在产待量" },
            new() { Key = "MainNoAttentionProcess",      Label = "主号关注工序",   SortKey = "MainNoAttentionProcess",      FilterType = "string", Width = "120", GroupKey = 14, GroupName = "在产待量" },
        };

        // G15: 工单计划（薄表 — 手工可编辑）
        var g15 = new List<ColumnDef>
        {
            new() { Key = "ConsistencyStatus",              Label = "实时一致性",  SortKey = "ConsistencyStatus",          FilterType = "enum", Width = "100", EnumOptions = new() { new("一致","一致"), new("进度调整","进度调整"), new("值存疑","值存疑"), new("错误","错误") }, GroupKey = 15, GroupName = "工单计划" },
            new() { Key = "PlanScheduleStage",               Label = "工单状态",     SortKey = "PlanScheduleStage",          FilterType = "enum", Width = "100", EnumOptions = new() { new("0","工单完成"), new("1","原料锁定"), new("2","生产执行"), new("3","成品检验") }, GroupKey = 15, GroupName = "工单计划" },
            new() { Key = "PlanUrgencyLevel",                Label = "紧急性",       SortKey = "PlanUrgencyLevel",           FilterType = "enum", Width = "100", EnumOptions = new() { new("A+急","A+急"), new("A急","A急"), new("B急","B急"), new("C急","C急"), new("B顺","B顺"), new("普通","普通") }, GroupKey = 15, GroupName = "工单计划" },
            new() { Key = "PlanProductionAttentionProcess",  Label = "生产关注",     SortKey = "PlanProductionAttentionProcess", FilterType = "enum", Width = "120", EnumOptions = new() { new("荒管处理","荒管处理"), new("在制修检","在制修检"), new("60冷轧","60冷轧"), new("50冷轧","50冷轧"), new("30冷轧","30冷轧"), new("20冷轧","20冷轧"), new("三辊冷轧","三辊冷轧"), new("冷拔","冷拔"), new("收尾-成检","收尾-成检") }, GroupKey = 15, GroupName = "工单计划" },
            new() { Key = "PlanProductionFlowProperty",      Label = "流转性",       SortKey = "PlanProductionFlowProperty",  FilterType = "string", Width = "100", GroupKey = 15, GroupName = "工单计划" },
        };

        var all = new List<ColumnDef>();
        all.AddRange(g1);
        all.AddRange(g7);
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
        if (_filteredItems.Count == 0) return;

        var props = typeof(WorkOrderScheduleDto)
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
        try
        {
            var query = new QueryParams
            {
                PageIndex = 1,
                PageSize = 50000,
                SortBy = "WorkOrderNo",
                IsDescending = false
            };
            var result = await WorkOrderScheduleSvc.GetPagedAsync(query);
            if (result.Success && result.Data != null)
            {
                _allItems = result.Data.Items ?? new();
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
                    .Where(v => v != null && v != FilterNull)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .Select(val => new ExcelFilterOption
                    {
                        Value = val!,
                        Display = val!,
                        Count = _allItems.Count(x => string.Equals(GetFilterValue(x, col.Key), val, StringComparison.OrdinalIgnoreCase))
                    })
                    .ToList();

                // Plan 字段增加"空值"筛选选项
                if (_planFieldKeys.Contains(col.Key))
                {
                    var nullCount = _allItems.Count(x => GetFilterValue(x, col.Key) == FilterNull);
                    options.Insert(0, new ExcelFilterOption
                    {
                        Value = FilterNull,
                        Display = "空值",
                        Count = nullCount
                    });
                }

                _filterContextOptions[col.Key] = options;
            }
            else if (col.FilterType == "enum")
            {
                if (col.EnumOptions != null)
                {
                    var options = col.EnumOptions.Select(e => new ExcelFilterOption
                    {
                        Value = e.Value,
                        Display = e.Display,
                        Count = _allItems.Count(x => string.Equals(GetFilterValue(x, col.Key), e.Value, StringComparison.OrdinalIgnoreCase))
                    }).ToList();

                    // Plan 字段增加"空值"筛选选项
                    if (_planFieldKeys.Contains(col.Key))
                    {
                        var nullCount = _allItems.Count(x => GetFilterValue(x, col.Key) == FilterNull);
                        options.Insert(0, new ExcelFilterOption
                        {
                            Value = FilterNull,
                            Display = "空值",
                            Count = nullCount
                        });
                    }

                    _filterContextOptions[col.Key] = options;
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

    private static string? GetFilterValue(WorkOrderScheduleDto item, string key) => key switch
    {
        "WorkOrderNo" => item.WorkOrderNo,
        "Salesman" => item.Salesman,
        "CustomerName" => item.CustomerName,
        "SalesOrderNo" => item.SalesOrderNo,
        "ProductionMainNo" => item.ProductionMainNo,
        "ProductionSubNo" => item.ProductionSubNo,
        "PlantGrade" => item.PlantGrade,
        "Specification" => item.Specification,
        "SettlementMethod" => DisplayHelper.GetSettlementMethodText(item.SettlementMethod),
        "MaterialName" => item.MaterialName,
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState),
        "LengthStatus" => DisplayHelper.GetLengthStatusText(item.LengthStatus),
        "FlowStatus" => item.FlowStatus.ToString(),
        "MainNoFlowStatus" => item.MainNoFlowStatus.ToString(),
        "ScheduleStage" => item.ScheduleStage.ToString(),
        "UrgencyLevel" => item.UrgencyLevel,
        "RawMaterialLockRemark" => item.RawMaterialLockRemark,
        "AdjustmentRemark" => item.AdjustmentRemark,
        "DelayPenalty" => DisplayHelper.GetYesNoText(item.DelayPenalty),
        "IsUrging" => DisplayHelper.GetYesNoText(item.IsUrging),
        "IsBatchDelivery" => DisplayHelper.GetYesNoText(item.IsBatchDelivery),
        "IsPaused" => DisplayHelper.GetYesNoText(item.IsPaused),
        "DeformedProcessCompleted" => DisplayHelper.GetYesNoText(item.DeformedProcessCompleted),
        "ProductionAttentionProcess" => item.ProductionAttentionProcess,
        "ProductionFlowProperty" => item.ProductionFlowProperty,
        "MaxBatchRemainingWorkDays" => item.MaxBatchRemainingWorkDays?.ToString(),
        "MainNoAttentionProcess" => item.MainNoAttentionProcess,
        "ConsistencyStatus" => item.ConsistencyStatus,
        "PlanScheduleStage" => item.PlanScheduleStage?.ToString() ?? FilterNull,
        "PlanUrgencyLevel" => item.PlanUrgencyLevel ?? FilterNull,
        "PlanProductionAttentionProcess" => item.PlanProductionAttentionProcess ?? FilterNull,
        "PlanProductionFlowProperty" => item.PlanProductionFlowProperty ?? FilterNull,
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
                (x.ProductionMainNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.ProductionSubNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (DisplayHelper.GetSettlementMethodText(x.SettlementMethod).Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.MaterialName?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (DisplayHelper.GetDeliveryStateText(x.DeliveryState).Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (DisplayHelper.GetLengthStatusText(x.LengthStatus).Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.UrgencyLevel?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.RawMaterialLockRemark?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.AdjustmentRemark?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.ProductionAttentionProcess?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.ProductionFlowProperty?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.MainNoAttentionProcess?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true));
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
            "TotalQuantity" => sortDescending ? query.OrderByDescending(x => x.TotalQuantity) : query.OrderBy(x => x.TotalQuantity),
            "TotalWeight" => sortDescending ? query.OrderByDescending(x => x.TotalWeight) : query.OrderBy(x => x.TotalWeight),
            "FlowOutputRatio" => sortDescending ? query.OrderByDescending(x => x.FlowOutputRatio) : query.OrderBy(x => x.FlowOutputRatio),
            "FlowStatus" => sortDescending ? query.OrderByDescending(x => x.FlowStatus) : query.OrderBy(x => x.FlowStatus),
            "MainNoFlowOutputRatio" => sortDescending ? query.OrderByDescending(x => x.MainNoFlowOutputRatio) : query.OrderBy(x => x.MainNoFlowOutputRatio),
            "MainNoFlowStatus" => sortDescending ? query.OrderByDescending(x => x.MainNoFlowStatus) : query.OrderBy(x => x.MainNoFlowStatus),
            "FlowTotalBatchCount" => sortDescending ? query.OrderByDescending(x => x.FlowTotalBatchCount) : query.OrderBy(x => x.FlowTotalBatchCount),
            "FlowIncompleteBatchCount" => sortDescending ? query.OrderByDescending(x => x.FlowIncompleteBatchCount) : query.OrderBy(x => x.FlowIncompleteBatchCount),
            "FlowMaxRemainingWorkDays" => sortDescending ? query.OrderByDescending(x => x.FlowMaxRemainingWorkDays) : query.OrderBy(x => x.FlowMaxRemainingWorkDays),
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
            "PendingSectionRoughTube" => sortDescending ? query.OrderByDescending(x => x.PendingSectionRoughTube) : query.OrderBy(x => x.PendingSectionRoughTube),
            "PendingSectionWarehouseFix" => sortDescending ? query.OrderByDescending(x => x.PendingSectionWarehouseFix) : query.OrderBy(x => x.PendingSectionWarehouseFix),
            "PendingSection60Roll" => sortDescending ? query.OrderByDescending(x => x.PendingSection60Roll) : query.OrderBy(x => x.PendingSection60Roll),
            "PendingSection50Roll" => sortDescending ? query.OrderByDescending(x => x.PendingSection50Roll) : query.OrderBy(x => x.PendingSection50Roll),
            "PendingSection30Roll" => sortDescending ? query.OrderByDescending(x => x.PendingSection30Roll) : query.OrderBy(x => x.PendingSection30Roll),
            "PendingSection20Roll" => sortDescending ? query.OrderByDescending(x => x.PendingSection20Roll) : query.OrderBy(x => x.PendingSection20Roll),
            "PendingSectionThreeRoll" => sortDescending ? query.OrderByDescending(x => x.PendingSectionThreeRoll) : query.OrderBy(x => x.PendingSectionThreeRoll),
            "PendingSectionDrawBench" => sortDescending ? query.OrderByDescending(x => x.PendingSectionDrawBench) : query.OrderBy(x => x.PendingSectionDrawBench),
            "DeformedProcessCompleted" => sortDescending ? query.OrderByDescending(x => x.DeformedProcessCompleted) : query.OrderBy(x => x.DeformedProcessCompleted),
            "ProductionAttentionProcess" => sortDescending ? query.OrderByDescending(x => x.ProductionAttentionProcess) : query.OrderBy(x => x.ProductionAttentionProcess),
            "ProductionFlowProperty" => sortDescending ? query.OrderByDescending(x => x.ProductionFlowProperty) : query.OrderBy(x => x.ProductionFlowProperty),
            "MaxBatchRemainingWorkDays" => sortDescending ? query.OrderByDescending(x => x.MaxBatchRemainingWorkDays) : query.OrderBy(x => x.MaxBatchRemainingWorkDays),
            "MainNoAttentionProcess" => sortDescending ? query.OrderByDescending(x => x.MainNoAttentionProcess) : query.OrderBy(x => x.MainNoAttentionProcess),
            "ConsistencyStatus" => sortDescending ? query.OrderByDescending(x => x.ConsistencyStatus) : query.OrderBy(x => x.ConsistencyStatus),
            "PlanScheduleStage" => sortDescending ? query.OrderByDescending(x => x.PlanScheduleStage) : query.OrderBy(x => x.PlanScheduleStage),
            "PlanUrgencyLevel" => sortDescending ? query.OrderByDescending(x => x.PlanUrgencyLevel) : query.OrderBy(x => x.PlanUrgencyLevel),
            "PlanProductionAttentionProcess" => sortDescending ? query.OrderByDescending(x => x.PlanProductionAttentionProcess) : query.OrderBy(x => x.PlanProductionAttentionProcess),
            "PlanProductionFlowProperty" => sortDescending ? query.OrderByDescending(x => x.PlanProductionFlowProperty) : query.OrderBy(x => x.PlanProductionFlowProperty),
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

    // ========== 分组 CSS ==========

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1",
            7 => "col-g7",
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
            7 => "col-g7-cell",
            12 => "col-g12-cell",
            13 => "col-g13-cell",
            14 => "col-g14-cell",
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
        var savedPrefs = await ColumnPrefs.LoadAsync("workorderschedules", null);
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

        // 从 PageState 恢复排序/筛选状态（列显隐/顺序由 ColumnPrefs 管理）
        var savedState = await PageState.LoadAsync("workorderschedules");
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

        // 确保新字段始终可见
        foreach (var col in _allColumns)
        {
            if (col.Key is "MaxBatchRemainingWorkDays" or "MainNoAttentionProcess")
                col.Visible = true;
        }

        await LoadDataAsync();
    }

    // ========== 分组标题栏同步 ==========

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 分组标题栏：测量实际列宽 + 同步滚动
        await JS.InvokeVoidAsync("initGroupHeaders", "#workorder-schedule-list-table");
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(WorkOrderScheduleDto item, ColumnDef col) => builder =>
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
                builder.AddContent(0, DisplayHelper.GetPipeManufacturingTypeText(item.MaterialName));
                break;
            case "DeliveryState":
                builder.AddContent(0, DisplayHelper.GetDeliveryStateText(item.DeliveryState));
                break;
            case "LengthStatus":
                builder.AddContent(0, DisplayHelper.GetLengthStatusText(item.LengthStatus));
                break;
            case "PlantGrade":
            case "MinLength":
                builder.AddContent(0, item.MinLength?.ToString("G29") ?? "-");
                break;
            case "MaxLength":
                builder.AddContent(0, item.MaxLength?.ToString("G29") ?? "-");
                break;
            case "TotalQuantity":
                builder.AddContent(0, item.TotalQuantity);
                break;
            case "TotalWeight":
                builder.AddContent(0, ((int)item.TotalWeight).ToString());
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
            case "FlowTotalBatchCount":
                builder.AddContent(0, item.FlowTotalBatchCount > 0 ? item.FlowTotalBatchCount.ToString() : "-");
                break;
            case "FlowIncompleteBatchCount":
                builder.AddContent(0, item.FlowIncompleteBatchCount > 0 ? item.FlowIncompleteBatchCount.ToString() : "-");
                break;
            case "FlowMaxRemainingWorkDays":
                builder.AddContent(0, item.FlowMaxRemainingWorkDays > 0 ? $"{item.FlowMaxRemainingWorkDays}天" : "-");
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

            // G13
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

            // G14: 在产节点待量
            case "PendingSectionRoughTube":
                builder.AddContent(0, item.PendingSectionRoughTube.HasValue ? ((int)item.PendingSectionRoughTube.Value).ToString() : "-");
                break;
            case "PendingSectionWarehouseFix":
                builder.AddContent(0, item.PendingSectionWarehouseFix.HasValue ? ((int)item.PendingSectionWarehouseFix.Value).ToString() : "-");
                break;
            case "PendingSection60Roll":
                builder.AddContent(0, item.PendingSection60Roll.HasValue ? ((int)item.PendingSection60Roll.Value).ToString() : "-");
                break;
            case "PendingSection50Roll":
                builder.AddContent(0, item.PendingSection50Roll.HasValue ? ((int)item.PendingSection50Roll.Value).ToString() : "-");
                break;
            case "PendingSection30Roll":
                builder.AddContent(0, item.PendingSection30Roll.HasValue ? ((int)item.PendingSection30Roll.Value).ToString() : "-");
                break;
            case "PendingSection20Roll":
                builder.AddContent(0, item.PendingSection20Roll.HasValue ? ((int)item.PendingSection20Roll.Value).ToString() : "-");
                break;
            case "PendingSectionThreeRoll":
                builder.AddContent(0, item.PendingSectionThreeRoll.HasValue ? ((int)item.PendingSectionThreeRoll.Value).ToString() : "-");
                break;
            case "PendingSectionDrawBench":
                builder.AddContent(0, item.PendingSectionDrawBench.HasValue ? ((int)item.PendingSectionDrawBench.Value).ToString() : "-");
                break;
            case "DeformedProcessCompleted":
                builder.AddContent(0, item.DeformedProcessCompleted ? "是" : "否");
                break;
            case "ProductionAttentionProcess":
                builder.AddContent(0, item.ProductionAttentionProcess ?? "-");
                break;
            case "ProductionFlowProperty":
                var flowProp = item.ProductionFlowProperty;
                if (!string.IsNullOrEmpty(flowProp))
                {
                    var color = flowProp switch
                    {
                        "暂停" => Color.Error,
                        "正常" => Color.Success,
                        "待料" => Color.Warning,
                        "疑问" => Color.Info,
                        _ => Color.Default
                    };
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", color);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, flowProp)));
                    builder.CloseComponent();
                }
                break;
            case "MaxBatchRemainingWorkDays":
                builder.AddContent(0, item.MaxBatchRemainingWorkDays.HasValue ? $"{item.MaxBatchRemainingWorkDays}天" : "-");
                break;
            case "MainNoAttentionProcess":
                builder.AddContent(0, item.MainNoAttentionProcess ?? "-");
                break;

            // ========== G15: 工单计划（内联编辑） ==========
            case "ConsistencyStatus":
                var cs = item.ConsistencyStatus;
                var csColor = cs switch
                {
                    "一致" => Color.Success,
                    "进度调整" => Color.Info,
                    "值存疑" => Color.Warning,
                    "错误" => Color.Error,
                    _ => Color.Default
                };
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", csColor);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, cs ?? "-")));
                builder.CloseComponent();
                break;

            case "PlanScheduleStage":
                builder.OpenComponent<MudSelect<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanScheduleStage?.ToString() ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanScheduleStage = string.IsNullOrEmpty(v) ? null : int.Parse(v);
                    await SavePlanAsync(item);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "Class", "compact-select");
                builder.AddAttribute(6, "ChildContent", (RenderFragment)(b2 =>
                {
                    b2.OpenComponent<MudSelectItem<string>>(0);
                    b2.AddAttribute(1, "Value", "");
                    b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, "空值")));
                    b2.CloseComponent();
                    foreach (var (val, label) in new[] { ("0", "工单完成"), ("1", "原料锁定"), ("2", "生产执行"), ("3", "成品检验") })
                    {
                        b2.OpenComponent<MudSelectItem<string>>(0);
                        b2.AddAttribute(1, "Value", val);
                        b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, label)));
                        b2.CloseComponent();
                    }
                }));
                builder.CloseComponent();
                break;

            case "PlanUrgencyLevel":
                builder.OpenComponent<MudSelect<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanUrgencyLevel ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanUrgencyLevel = string.IsNullOrEmpty(v) ? null : v;
                    await SavePlanAsync(item);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "Class", "compact-select");
                builder.AddAttribute(6, "ChildContent", (RenderFragment)(b2 =>
                {
                    b2.OpenComponent<MudSelectItem<string>>(0);
                    b2.AddAttribute(1, "Value", "");
                    b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, "空值")));
                    b2.CloseComponent();
                    foreach (var opt in new[] { "A+急", "A急", "B急", "C急", "B顺", "普通" })
                    {
                        b2.OpenComponent<MudSelectItem<string>>(0);
                        b2.AddAttribute(1, "Value", opt);
                        b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, opt)));
                        b2.CloseComponent();
                    }
                }));
                builder.CloseComponent();
                break;

            case "PlanProductionAttentionProcess":
                builder.OpenComponent<MudSelect<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanProductionAttentionProcess ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanProductionAttentionProcess = string.IsNullOrEmpty(v) ? null : v;
                    await SavePlanAsync(item);
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.AddAttribute(4, "Variant", Variant.Text);
                builder.AddAttribute(5, "Class", "compact-select");
                builder.AddAttribute(6, "ChildContent", (RenderFragment)(b2 =>
                {
                    b2.OpenComponent<MudSelectItem<string>>(0);
                    b2.AddAttribute(1, "Value", "");
                    b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, "空值")));
                    b2.CloseComponent();
                    foreach (var opt in new[] { "荒管处理", "在制修检", "60冷轧", "50冷轧", "30冷轧", "20冷轧", "三辊冷轧", "冷拔", "收尾-成检" })
                    {
                        b2.OpenComponent<MudSelectItem<string>>(0);
                        b2.AddAttribute(1, "Value", opt);
                        b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, opt)));
                        b2.CloseComponent();
                    }
                }));
                builder.CloseComponent();
                break;

            case "PlanProductionFlowProperty":
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "style", item.PlanProductionFlowProperty == "疑问"
                    ? "background-color:#ffebee;border-radius:4px;padding:2px;"
                    : "");
                builder.OpenComponent<MudSelect<string>>(2);
                builder.AddAttribute(3, "Value", item.PlanProductionFlowProperty ?? "");
                builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanProductionFlowProperty = string.IsNullOrEmpty(v) ? null : v;
                    await SavePlanAsync(item);
                }));
                builder.AddAttribute(5, "Dense", true);
                builder.AddAttribute(6, "Variant", Variant.Text);
                builder.AddAttribute(7, "Class", "compact-select");
                builder.AddAttribute(8, "ChildContent", (RenderFragment)(b2 =>
                {
                    b2.OpenComponent<MudSelectItem<string>>(0);
                    b2.AddAttribute(1, "Value", "");
                    b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, "空值")));
                    b2.CloseComponent();
                    foreach (var opt in new[] { "正常", "暂停", "待料", "疑问", "略" })
                    {
                        b2.OpenComponent<MudSelectItem<string>>(0);
                        b2.AddAttribute(1, "Value", opt);
                        b2.AddAttribute(2, "ChildContent", (RenderFragment)(b3 => b3.AddContent(0, opt)));
                        b2.CloseComponent();
                    }
                }));
                builder.CloseComponent();
                builder.CloseElement();
                break;
        }
    };

    // ========== 文本辅助 ==========

    private static string GetFlowStatusText(int status) =>
        DisplayHelper.GetFlowStatusText(status);

    private static string GetValidMainNoStatusText(int status) =>
        DisplayHelper.GetMainNoFlowStatusText(status);

    // ========== 颜色 ==========

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

    // ========== 计划安排 ==========

    private async Task OnPlanAllAsync()
    {
        var confirmed = await DialogService.ShowMessageBox(
            "计划安排",
            "确认将当前查询范围内所有工单的工单计划值设为系统值，并删除不匹配的 Plan 行？",
            yesText: "确认",
            cancelText: "取消");
        if (confirmed != true) return;

        try
        {
            var filtersJson = SerializeFilters();

            var query = new QueryParams
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortColumn,
                IsDescending = sortDescending,
                PageSize = 5000,
            };
            if (filtersJson != null)
            {
                query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson);
            }

            var result = await WorkOrderScheduleSvc.PlanScheduleAllAsync(query);
            if (result.Success)
            {
                Snackbar.Add("计划安排成功，已同步系统值并清理多余记录", Severity.Success);
                await LoadDataAsync();
            }
            else
            {
                Snackbar.Add($"计划安排失败: {result.Message}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"计划安排失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task OnPlanKeepAttentionAsync()
    {
        var confirmed = await DialogService.ShowMessageBox(
            "进度保留计划",
            "确认将当前查询范围内所有工单的工单状态/紧急性/流转性设为系统值，并保留生产关注的手工调整？",
            yesText: "确认",
            cancelText: "取消");
        if (confirmed != true) return;

        try
        {
            var filtersJson = SerializeFilters();

            var query = new QueryParams
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortColumn,
                IsDescending = sortDescending,
                PageSize = 5000,
            };
            if (filtersJson != null)
            {
                query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filtersJson);
            }

            var result = await WorkOrderScheduleSvc.PlanScheduleKeepAttentionAsync(query);
            if (result.Success)
            {
                Snackbar.Add("进度保留计划成功，已同步系统值并保留生产关注", Severity.Success);
                await LoadDataAsync();
            }
            else
            {
                Snackbar.Add($"进度保留计划失败: {result.Message}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"进度保留计划失败: {ex.Message}", Severity.Error);
        }
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

    // ========== 工单计划保存 ==========

    private async Task SavePlanAsync(WorkOrderScheduleDto item)
    {
        var request = new SaveWorkOrderPlanRequest
        {
            WorkOrderId = item.WorkOrderId,
            ScheduleStage = item.PlanScheduleStage,
            UrgencyLevel = item.PlanUrgencyLevel,
            ProductionAttentionProcess = item.PlanProductionAttentionProcess,
            ProductionFlowProperty = item.PlanProductionFlowProperty,
        };

        var result = await WorkOrderScheduleSvc.SavePlanAsync(request);
        if (result.Success)
        {
            Snackbar.Add("保存成功", Severity.Success);
            await LoadDataAsync();
        }
        else
        {
            Snackbar.Add($"保存失败: {result.Message}", Severity.Error);
        }
    }

    // ========== 打印 ==========

    private async Task PrintSelected()
    {
        if (_selectedItems.Count == 0)
        {
            Snackbar.Add("请先选择要打印的行", Severity.Warning);
            return;
        }

        try
        {
            var printColumns = _visibleColumns
                .Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label })
                .ToList();

            // 将 DTO 转为字典，枚举字段预先解析为中文显示文本
            var printItems = _selectedItems.Select(item =>
            {
                var dict = new Dictionary<string, object>();
                foreach (var col in _visibleColumns)
                {
                    dict[col.Key] = ResolvePrintValue(item, col.Key);
                }
                return dict;
            }).ToList();

            var request = new WorkOrderSchedulePrintRequest
            {
                Title = "工单计划",
                Items = printItems,
                Columns = printColumns
            };

            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/workorder-schedule/print-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private static object ResolvePrintValue(WorkOrderScheduleDto item, string key) => key switch
    {
        // G1: 枚举→中文
        "MaterialName" => DisplayHelper.GetPipeManufacturingTypeText(item.MaterialName) ?? "",
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState) ?? "",
        "LengthStatus" => DisplayHelper.GetLengthStatusText(item.LengthStatus) ?? "",
        "SettlementMethod" => DisplayHelper.GetSettlementMethodText(item.SettlementMethod) ?? "",
        // G7: 流转状态
        "FlowStatus" => DisplayHelper.GetFlowStatusText(item.FlowStatus),
        "MainNoFlowStatus" => DisplayHelper.GetMainNoFlowStatusText(item.MainNoFlowStatus),
        // G12: 关注状态
        "ScheduleStage" => DisplayHelper.GetScheduleStageText(item.ScheduleStage),
        // G15: 覆盖字段
        "PlanScheduleStage" => item.PlanScheduleStage.HasValue ? DisplayHelper.GetScheduleStageText(item.PlanScheduleStage.Value) : "未知",
        "ConsistencyStatus" => item.ConsistencyStatus ?? "",
        // 非枚举字段原样输出
        _ => GetRawPropertyValue(item, key)
    };

    private static object GetRawPropertyValue(WorkOrderScheduleDto item, string key)
    {
        return (key switch
        {
            "WorkOrderNo" => item.WorkOrderNo ?? "",
            "Salesman" => item.Salesman ?? "",
            "CustomerName" => item.CustomerName ?? "",
            "SignDate" => item.SignDate,
            "DeliveryDate" => item.DeliveryDate,
            "DelayPenalty" => item.DelayPenalty,
            "SalesOrderNo" => item.SalesOrderNo ?? "",
            "ProductionMainNo" => item.ProductionMainNo ?? "",
            "ProductionSubNo" => item.ProductionSubNo ?? "",
            "PlantGrade" => item.PlantGrade ?? "",
            "Specification" => item.Specification ?? "",
            "MinLength" => item.MinLength,
            "MaxLength" => item.MaxLength,
            "TotalItemCount" => item.TotalItemCount,
            "TotalQuantity" => item.TotalQuantity,
            "TotalMeters" => item.TotalMeters,
            "TotalWeight" => item.TotalWeight,
            "FlowOutputRatio" => item.FlowOutputRatio,
            "FlowTotalBatchCount" => item.FlowTotalBatchCount,
            "FlowIncompleteBatchCount" => item.FlowIncompleteBatchCount,
            "FlowMaxRemainingWorkDays" => item.FlowMaxRemainingWorkDays,
            "MainNoFlowOutputRatio" => item.MainNoFlowOutputRatio,
            "TotalRemainingWorkDays" => item.TotalRemainingWorkDays,
            "CapacityWorkDays" => item.CapacityWorkDays,
            "UrgencyLevel" => item.UrgencyLevel ?? "",
            "EstimatedProcessCompletionDate" => item.EstimatedProcessCompletionDate,
            "DaysDiffFromDelivery" => item.DaysDiffFromDelivery,
            "RawMaterialLockRemark" => item.RawMaterialLockRemark ?? "",
            "IsUrging" => item.IsUrging,
            "IsBatchDelivery" => item.IsBatchDelivery,
            "IsPaused" => item.IsPaused,
            "AdjustmentRemark" => item.AdjustmentRemark ?? "",
            "PendingSectionRoughTube" => item.PendingSectionRoughTube,
            "PendingSectionWarehouseFix" => item.PendingSectionWarehouseFix,
            "PendingSection60Roll" => item.PendingSection60Roll,
            "PendingSection50Roll" => item.PendingSection50Roll,
            "PendingSection30Roll" => item.PendingSection30Roll,
            "PendingSection20Roll" => item.PendingSection20Roll,
            "PendingSectionThreeRoll" => item.PendingSectionThreeRoll,
            "PendingSectionDrawBench" => item.PendingSectionDrawBench,
            "DeformedProcessCompleted" => item.DeformedProcessCompleted,
            "ProductionAttentionProcess" => item.ProductionAttentionProcess ?? "",
            "ProductionFlowProperty" => item.ProductionFlowProperty ?? "",
            "MaxBatchRemainingWorkDays" => item.MaxBatchRemainingWorkDays,
            "MainNoAttentionProcess" => item.MainNoAttentionProcess ?? "",
            "PlanUrgencyLevel" => item.PlanUrgencyLevel ?? "",
            "PlanProductionAttentionProcess" => item.PlanProductionAttentionProcess ?? "",
            "PlanProductionFlowProperty" => item.PlanProductionFlowProperty ?? "",
            _ => ""
        })!; // 编译器无法验证object返回值非null
    }

    // ========== 重置列显隐 ==========

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        await SavePageStateAsync();
        ApplyFiltersAndSort();
        StateHasChanged();
    }

    // ========== ColumnPrefs 持久化 ==========

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("workorderschedules", null, _allColumns);
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
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = 1,
            Extras = extras
        };
        await PageState.SaveAsync("workorderschedules", state);
    }
}
