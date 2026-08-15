using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Models;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.Scheduling;

public partial class RawMaterialLockPlanAndExecution
{
    private MudTable<RawMaterialLockPlanAndExecutionDto>? table;
    private List<RawMaterialLockPlanAndExecutionDto> _allItems = new();
    private List<RawMaterialLockPlanAndExecutionDto> _filteredItems = new();
    private HashSet<RawMaterialLockPlanAndExecutionDto> _selectedItems = new();

    private void SelectAllItems(bool selected)
    {
        if (selected)
            _selectedItems = new HashSet<RawMaterialLockPlanAndExecutionDto>(_filteredItems);
        else
            _selectedItems.Clear();
    }

    private void ToggleSelection(RawMaterialLockPlanAndExecutionDto item, bool selected)
    {
        if (selected)
            _selectedItems.Add(item);
        else
            _selectedItems.Remove(item);
    }

    // 汇总数据
    private bool _showSummaryCard;          // 汇总卡片显隐（默认折叠）
    private int _totalOrderCount;
    private decimal _totalWeight;
    private decimal _pendingWeight;
    private int _purchaseCount;             // 成购（外购成品）单数：成品计划量>成品到货量 的行数
    private decimal _purchaseWeight;        // 成购重量 = Σ(成品计划量 − 成品到货量)

    // 汇总交叉矩阵（原料锁定备注 × 主号计划性）：单数 + 待投料重量 + 成购单数/重量
    private readonly record struct MatrixCell(int Count, decimal PendingWeight, int PurchaseCount, decimal PurchaseWeight);
    private readonly Dictionary<string, MatrixCell> _summaryMatrix = new();

    // 矩阵列：主号计划性五档（不含 EPaused 暂停档）
    private static readonly string[] _urgencyColumns =
    [
        UrgencyLevelKeys.APlusUrgent, UrgencyLevelKeys.AUrgent,
        UrgencyLevelKeys.BOrder, UrgencyLevelKeys.CSlow, UrgencyLevelKeys.DSlow,
    ];

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "TotalItemCount", "TotalQuantity", "TotalMeters", "TotalWeight",
        "TotalPlanWeight", "TotalAvailableWeight", "TotalMissingWeight", "ActualInputWeight",
        "PiercingPlanWeight", "PiercingSubOutWeight", "PiercingSubInWeight", "PiercingSubPendingWeight",
        "SemiPlanWeight", "SemiOrderWeight", "SemiInWeight", "SemiPendingWeight",
        "FinishPlanWeight", "FinishOrderWeight", "FinishInWeight", "FinishPendingWeight",
        "InventoryPlanWeight", "InventoryOutWeight",
        "ReworkPlanWeight", "ReworkPlanInputWeight",
        "InProcessReworkPlanWeight", "InProcessReworkInputWeight",
    };
    private int _lastSummedPage = -1;
    private int _lastSummedCount = -1;
    private int _lastSummedPageSize = -1;

    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;

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
            new() { Key = "EndCustomer",             Label = "最终客户",        SortKey = "EndCustomer",             FilterType = "string", Width = "120", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SignDate",                Label = "订单日期",        SortKey = "SignDate",                Width = "120", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DeliveryDate",            Label = "交货日期",        SortKey = "DeliveryDate",            Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DelayPenalty",            Label = "延期罚款",        SortKey = "DelayPenalty",            FilterType = "boolean", Width = "120", BoolTrueLabel = "是", BoolFalseLabel = "否", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SettlementMethod",        Label = "结算方式",        SortKey = "SettlementMethod",        FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetEnumFilterOptions<SettlementMethod>(), Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SalesOrderNo",            Label = "订单号",          SortKey = "SalesOrderNo",            FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionMainNo",        Label = "主号",            SortKey = "ProductionMainNo",        FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionSubNo",         Label = "次号",            SortKey = "ProductionSubNo",         FilterType = "string", Width = "120", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MaterialName",            Label = "钢管制造",        SortKey = "MaterialName",            FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetEnumFilterOptions<PipeManufacturingType>(), Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DeliveryState",           Label = "交货状态",        SortKey = "DeliveryState",           FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>(), GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "PlantGrade",              Label = "工厂牌号",        SortKey = "PlantGrade",              FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "Specification",           Label = "规格",            SortKey = "Specification",           FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "LengthStatus",            Label = "长度状态",        SortKey = "LengthStatus",            FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetEnumFilterOptions<LengthStatus>(), GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MinLength",               Label = "最小长度",        SortKey = "MinLength",               Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MaxLength",               Label = "最大长度",        SortKey = "MaxLength",               Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalItemCount",          Label = "总项数",          SortKey = "TotalItemCount",          Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalQuantity",           Label = "总支数",          SortKey = "TotalQuantity",           Width = "80", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalMeters",             Label = "总米数",          SortKey = "TotalMeters",             Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalWeight",             Label = "总重量",          SortKey = "TotalWeight",             Width = "80", GroupKey = 1, GroupName = "基础数据" },
        };

        // G2: 工单需求调整
        var g2 = new List<ColumnDef>
        {
            new() { Key = "IsUrging",             Label = "催单",           SortKey = "IsUrging",             FilterType = "boolean", Width = "80",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 2, GroupName = "工单需求调整" },
            new() { Key = "IsBatchDelivery",      Label = "分批交货",       SortKey = "IsBatchDelivery",      FilterType = "boolean", Width = "80",  BoolTrueLabel = "是", BoolFalseLabel = "否", Visible = false, GroupKey = 2, GroupName = "工单需求调整" },
            new() { Key = "IsPaused",             Label = "工单暂停",       SortKey = "IsPaused",             FilterType = "boolean", Width = "80",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 2, GroupName = "工单需求调整" },
            new() { Key = "AdjustmentRemark",     Label = "调整备注",       SortKey = "AdjustmentRemark",     FilterType = "string",  Width = "200", Visible = false, GroupKey = 2, GroupName = "工单需求调整" },
        };

        // G4: 用料计划及执行实况
        var g4 = new List<ColumnDef>
        {
            // 主号级（放组首，主号- 前缀，整组默认隐藏）
            new() { Key = "MainNoMaterialPlanStatus",Label = "主号-用料计划",   SortKey = "MainNoMaterialPlanStatus", FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetMaterialPlanStatusOptions(), Visible = false, GroupKey = 4, GroupName = "用料计划及执行实况", Level = ColumnLevel.MainNo },
            new() { Key = "MainNoMaterialPlanRate",  Label = "主号-计划满足率(%)", SortKey = "MainNoMaterialPlanRate", Width = "80",  Visible = false, GroupKey = 4, GroupName = "用料计划及执行实况", Level = ColumnLevel.MainNo },
            new() { Key = "MainNoPlanExecutionStatus", Label = "主号-计划执行状态", SortKey = "MainNoPlanExecutionStatus", FilterType = "enum", EnumOptions = DisplayHelper.GetMainNoPlanExecutionStatusOptions(), Width = "110", Visible = false, GroupKey = 4, GroupName = "用料计划及执行实况", Level = ColumnLevel.MainNo },
            new() { Key = "ActualMainNoInputStatus",  Label = "主号-实投状态",   SortKey = "ActualMainNoInputStatus",  FilterType = "enum", EnumOptions = DisplayHelper.GetFlowStatusOptions(), Width = "100", Visible = false, GroupKey = 4, GroupName = "用料计划及执行实况", Level = ColumnLevel.MainNo },
            // 工单级（整组默认隐藏）
            new() { Key = "MaterialPlanStatus",      Label = "工单用料计划",    SortKey = "MaterialPlanStatus",      FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetMaterialPlanStatusOptions(), Visible = false, GroupKey = 4, GroupName = "用料计划及执行实况" },
            new() { Key = "MaterialPlanCoveredCount", Label = "料态种数",       SortKey = "MaterialPlanCoveredCount", Width = "80",       Visible = false, GroupKey = 4, GroupName = "用料计划及执行实况" },
            new() { Key = "MaterialPlanProportion",   Label = "用料占比",       SortKey = "MaterialPlanProportion",   Width = "120",                             Visible = false, GroupKey = 4, GroupName = "用料计划及执行实况" },
            new() { Key = "TheoreticalCutoffDate",    Label = "理论截止投料日",  SortKey = "TheoreticalCutoffDate",   Width = "120",                             Visible = false, GroupKey = 4, GroupName = "用料计划及执行实况" },
            new() { Key = "TotalPlanWeight",         Label = "计划投料总重",    SortKey = "TotalPlanWeight",         Width = "100",     Visible = false, GroupKey = 4, GroupName = "用料计划及执行实况" },
            new() { Key = "CutoffArrivalDate",         Label = "截止到料日",     SortKey = "CutoffArrivalDate",       Width = "120",                             Visible = false, GroupKey = 4, GroupName = "用料计划及执行实况" },
            new() { Key = "TotalAvailableWeight",     Label = "现可投料总重",    SortKey = "TotalAvailableWeight",     Width = "100",     Visible = false, GroupKey = 4, GroupName = "用料计划及执行实况" },
            new() { Key = "TotalMissingWeight",       Label = "理论缺失总料重",  SortKey = "TotalMissingWeight",       Width = "100",     Visible = false, GroupKey = 4, GroupName = "用料计划及执行实况" },
            new() { Key = "ActualInputWeight",        Label = "实际已投料量",    SortKey = "ActualInputWeight",        Width = "100", Visible = false, GroupKey = 4, GroupName = "用料计划及执行实况" },
            new() { Key = "PlanInputConsistency",     Label = "到料实投一致性",  SortKey = "PlanInputConsistency",     FilterType = "enum", EnumOptions = DisplayHelper.GetPlanInputConsistencyOptions(), Width = "140", Visible = false, GroupKey = 4, GroupName = "用料计划及执行实况", HighlightCssClass = " col-header-consistency" },
        };

        // G5: 圆棒穿孔（默认全隐）
        var g5 = new List<ColumnDef>
        {
            new() { Key = "PiercingPlanWeight",        Label = "穿孔计划量(kg)",    SortKey = "PiercingPlanWeight",        Width = "80",  Visible = false, GroupKey = 5, GroupName = "圆棒穿孔" },
            new() { Key = "PiercingSubOutWeight",      Label = "穿孔委外量(kg)",    SortKey = "PiercingSubOutWeight",      Width = "80",  Visible = false, GroupKey = 5, GroupName = "圆棒穿孔" },
            new() { Key = "PiercingSubStatus",         Label = "穿孔委外状态",      SortKey = "PiercingSubStatus",         FilterType = "enum", EnumOptions = DisplayHelper.GetPlanExecutionStatusOptions(), Width = "100", Visible = false, GroupKey = 5, GroupName = "圆棒穿孔" },
            new() { Key = "PiercingSubInWeight",       Label = "穿孔回收量(kg)",    SortKey = "PiercingSubInWeight",       Width = "80",  Visible = false, GroupKey = 5, GroupName = "圆棒穿孔" },
            new() { Key = "PiercingSubPendingWeight",  Label = "穿孔待回收(kg)",    SortKey = "PiercingSubPendingWeight",  Width = "80",  Visible = false, GroupKey = 5, GroupName = "圆棒穿孔" },
            new() { Key = "PiercingReturnStatus",      Label = "穿孔回收状态",      SortKey = "PiercingReturnStatus",      FilterType = "enum", EnumOptions = DisplayHelper.GetPlanExecutionStatusOptions(), Width = "100", Visible = false, GroupKey = 5, GroupName = "圆棒穿孔" },
        };
        // G6: 荒管采购（默认全隐）
        var g6 = new List<ColumnDef>
        {
            new() { Key = "SemiPlanWeight",            Label = "荒管计划量(kg)",    SortKey = "SemiPlanWeight",            Width = "80",  Visible = false, GroupKey = 6, GroupName = "荒管采购" },
            new() { Key = "SemiOrderWeight",           Label = "荒管采购量(kg)",    SortKey = "SemiOrderWeight",           Width = "80",  Visible = false, GroupKey = 6, GroupName = "荒管采购" },
            new() { Key = "SemiOrderStatus",           Label = "荒管采购状态",      SortKey = "SemiOrderStatus",           FilterType = "enum", EnumOptions = DisplayHelper.GetPlanExecutionStatusOptions(), Width = "100", Visible = false, GroupKey = 6, GroupName = "荒管采购" },
            new() { Key = "SemiInWeight",              Label = "荒管到货量(kg)",    SortKey = "SemiInWeight",              Width = "80",  Visible = false, GroupKey = 6, GroupName = "荒管采购" },
            new() { Key = "SemiPendingWeight",         Label = "荒管待货(kg)",      SortKey = "SemiPendingWeight",         Width = "80",  Visible = false, GroupKey = 6, GroupName = "荒管采购" },
            new() { Key = "SemiInStatus",              Label = "荒管到货状态",      SortKey = "SemiInStatus",              FilterType = "enum", EnumOptions = DisplayHelper.GetPlanExecutionStatusOptions(), Width = "100", Visible = false, GroupKey = 6, GroupName = "荒管采购" },
        };
        // G7: 成品采购（默认全隐）
        var g7 = new List<ColumnDef>
        {
            new() { Key = "FinishPlanWeight",          Label = "成品计划量(kg)",    SortKey = "FinishPlanWeight",          Width = "80",  Visible = false, GroupKey = 7, GroupName = "成品采购" },
            new() { Key = "FinishOrderWeight",         Label = "成品采购量(kg)",    SortKey = "FinishOrderWeight",         Width = "80",  Visible = false, GroupKey = 7, GroupName = "成品采购" },
            new() { Key = "FinishOrderStatus",         Label = "成品采购状态",      SortKey = "FinishOrderStatus",         FilterType = "enum", EnumOptions = DisplayHelper.GetPlanExecutionStatusOptions(), Width = "100", Visible = false, GroupKey = 7, GroupName = "成品采购" },
            new() { Key = "FinishInWeight",            Label = "成品到货量(kg)",    SortKey = "FinishInWeight",            Width = "80",  Visible = false, GroupKey = 7, GroupName = "成品采购" },
            new() { Key = "FinishPendingWeight",       Label = "成品待货(kg)",      SortKey = "FinishPendingWeight",       Width = "80",  Visible = false, GroupKey = 7, GroupName = "成品采购" },
            new() { Key = "FinishInStatus",            Label = "成品到货状态",      SortKey = "FinishInStatus",            FilterType = "enum", EnumOptions = DisplayHelper.GetPlanExecutionStatusOptions(), Width = "100", Visible = false, GroupKey = 7, GroupName = "成品采购" },
        };
        // G8: 库存使用（默认全隐）
        var g8 = new List<ColumnDef>
        {
            new() { Key = "InventoryPlanWeight",       Label = "库存计划量(kg)",    SortKey = "InventoryPlanWeight",       Width = "80",  Visible = false, GroupKey = 8, GroupName = "库存使用" },
            new() { Key = "InventoryOutWeight",        Label = "库存出库量(kg)",    SortKey = "InventoryOutWeight",        Width = "80",  Visible = false, GroupKey = 8, GroupName = "库存使用" },
            new() { Key = "InventoryOutStatus",        Label = "库存出库状态",      SortKey = "InventoryOutStatus",        FilterType = "enum", EnumOptions = DisplayHelper.GetPlanExecutionStatusOptions(), Width = "100", Visible = false, GroupKey = 8, GroupName = "库存使用" },
        };
        // G9: 库料改制（默认全隐）
        var g9 = new List<ColumnDef>
        {
            new() { Key = "ReworkPlanWeight",          Label = "改制计划量(kg)",    SortKey = "ReworkPlanWeight",          Width = "80",  Visible = false, GroupKey = 9, GroupName = "库料改制" },
            new() { Key = "ReworkPlanInputWeight",     Label = "改制投料量(kg)",    SortKey = "ReworkPlanInputWeight",     Width = "80",  Visible = false, GroupKey = 9, GroupName = "库料改制" },
            new() { Key = "ReworkPlanInputStatus",     Label = "改制投料状态",      SortKey = "ReworkPlanInputStatus",     FilterType = "enum", EnumOptions = DisplayHelper.GetPlanExecutionStatusOptions(), Width = "100", Visible = false, GroupKey = 9, GroupName = "库料改制" },
        };
        // G10: 在产改制（默认全隐）
        var g10 = new List<ColumnDef>
        {
            new() { Key = "InProcessReworkPlanWeight",      Label = "产改计划量(kg)",  SortKey = "InProcessReworkPlanWeight",      Width = "80",  Visible = false, GroupKey = 10, GroupName = "在产改制" },
            new() { Key = "InProcessReworkInputWeight",     Label = "产改投料量(kg)",  SortKey = "InProcessReworkInputWeight",     Width = "80",  Visible = false, GroupKey = 10, GroupName = "在产改制" },
            new() { Key = "InProcessReworkInputStatus",     Label = "产改投料状态",    SortKey = "InProcessReworkInputStatus",     FilterType = "enum", EnumOptions = DisplayHelper.GetPlanExecutionStatusOptions(), Width = "100", Visible = false, GroupKey = 10, GroupName = "在产改制" },
        };

        // （旧投料数据组已废弃，由 G4 用料计划及执行实况取代）

        // G7 有效流转组已废弃（由 G4 用料计划执行实况 + 实时关注取代）

        // G3: 实时关注（整体汇整，置于明细之前，整组主号级：主号- 前缀；组顺序与工单执行状况一致）
        var g3 = new List<ColumnDef>
        {
            new() { Key = "ScheduleStage",           Label = "主号-关注",      SortKey = "ScheduleStage",           FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetScheduleStageOptions(), Visible = false, GroupKey = 3, GroupName = "实时关注", Level = ColumnLevel.MainNo },
            new() { Key = "UrgencyLevel",            Label = "主号-计划性",    SortKey = "UrgencyLevel",            FilterType = "string", Width = "120",                              GroupKey = 3, GroupName = "实时关注", Level = ColumnLevel.MainNo },
            new() { Key = "EstimatedProcessCompletionDate",Label = "主号-预计完成日",SortKey = "EstimatedProcessCompletionDate", Width = "120", Visible = false, GroupKey = 3, GroupName = "实时关注", Level = ColumnLevel.MainNo },
            new() { Key = "DaysDiffFromDelivery",    Label = "主号-交期相差天数",  SortKey = "DaysDiffFromDelivery",  Width = "80", Visible = false, GroupKey = 3, GroupName = "实时关注", Level = ColumnLevel.MainNo },
            new() { Key = "TotalRemainingWorkDays",  Label = "主号-剩余总工量(天)",SortKey = "TotalRemainingWorkDays",  Width = "80", Visible = false, GroupKey = 3, GroupName = "实时关注", Level = ColumnLevel.MainNo },
            new() { Key = "CapacityWorkDays",         Label = "主号-产能工量(天)",  SortKey = "CapacityWorkDays",     Width = "80", Visible = false, GroupKey = 3, GroupName = "实时关注", Level = ColumnLevel.MainNo },
            new() { Key = "RawMaterialLockRemark",   Label = "主号-原锁备注", SortKey = "RawMaterialLockRemark",   FilterType = "string", Width = "120",                             GroupKey = 3, GroupName = "实时关注", Level = ColumnLevel.MainNo },
        };

        // G13: 实际生产总流转（生产执行进度，主号- 前缀列为主号级；列顺序与工单执行状况一致；默认全隐）
        var g13 = new List<ColumnDef>
        {
            new() { Key = "MainNoFlowStatus",         Label = "主号-流转状态",   SortKey = "MainNoFlowStatus",         FilterType = "enum",   Width = "110", EnumOptions = DisplayHelper.GetMainNoFlowStatusOptions(), Visible = false, GroupKey = 13, GroupName = "实际生产总流转", Level = ColumnLevel.MainNo },
            new() { Key = "MainNoFlowOutputRatio",    Label = "主号-流转比",     SortKey = "MainNoFlowOutputRatio",    FilterType = "number", Width = "80",  Visible = false, GroupKey = 13, GroupName = "实际生产总流转", Level = ColumnLevel.MainNo },
            new() { Key = "FlowStatus",               Label = "工单流转状态",   SortKey = "FlowStatus",               FilterType = "enum",   Width = "110", EnumOptions = DisplayHelper.GetFlowStatusOptions(), Visible = false, GroupKey = 13, GroupName = "实际生产总流转" },
            new() { Key = "FlowOutputRatio",          Label = "工单流转比",     SortKey = "FlowOutputRatio",          FilterType = "number", Width = "80",  Visible = false, GroupKey = 13, GroupName = "实际生产总流转" },
            new() { Key = "FlowTotalBatchCount",      Label = "总批次数",        SortKey = "FlowTotalBatchCount",      FilterType = "number", Width = "80",  Visible = false, GroupKey = 13, GroupName = "实际生产总流转" },
            new() { Key = "FlowIncompleteBatchCount", Label = "未完成批数",      SortKey = "FlowIncompleteBatchCount", FilterType = "number", Width = "80",  Visible = false, GroupKey = 13, GroupName = "实际生产总流转" },
            new() { Key = "FlowMaxRemainingWorkDays", Label = "最大剩余工量(天)", SortKey = "FlowMaxRemainingWorkDays", FilterType = "number", Width = "90",  Visible = false, GroupKey = 13, GroupName = "实际生产总流转" },
        };

        // G15: 预执行（页面操作标记）——操作列整列高亮（靛蓝底），与数据列区分
        var g15 = new List<ColumnDef>
        {
            new() { Key = "IsPreInput",                  Label = "执行",          SortKey = "IsPreInput",                    FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 15, GroupName = "预执行", HighlightCssClass = " col-edit-actions" },
            new() { Key = "BudgetInputDate",             Label = "预算投料日",    SortKey = "BudgetInputDate",               Width = "130", GroupKey = 15, GroupName = "预执行", HighlightCssClass = " col-edit-actions" },
        };

        var all = new List<ColumnDef>();
        // 组顺序与工单执行状况读模型一致：基础数据 → 工单需求调整 → 实时关注 → 用料计划及执行实况 → 圆棒穿孔 → 荒管采购 → 成品采购 → 库存使用 → 库料改制 → 在产改制 → 实际生产总流转 → 预执行（原锁页特有）
        all.AddRange(g1);   // 1  基础数据
        all.AddRange(g2);   // 2  工单需求调整
        all.AddRange(g3);   // 3  实时关注
        all.AddRange(g4);   // 4  用料计划及执行实况
        all.AddRange(g5);   // 5  圆棒穿孔
        all.AddRange(g6);   // 6  荒管采购
        all.AddRange(g7);   // 7  成品采购
        all.AddRange(g8);   // 8  库存使用
        all.AddRange(g9);   // 9  库料改制
        all.AddRange(g10);  // 10 在产改制
        all.AddRange(g13);  // 13 实际生产总流转
        all.AddRange(g15);  // 15 预执行
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
                    var sum = pageItems.Sum(item => (int)(prop.GetValue(item) ?? 0));
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal))
                {
                    var sum = pageItems.Sum(item => (decimal)(prop.GetValue(item) ?? 0m));
                    _pageSums[col.Key] = ((int)sum).ToString();
                }
                else if (type == typeof(int?))
                {
                    var sum = pageItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal?))
                {
                    var sum = pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
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
                SortBy = "ScheduleStage",
                IsDescending = true
            };
            var result = await RawMaterialLockPlanService.GetPagedAsync(query);
            if (result.Success && result.Data != null)
            {
                _allItems = result.Data.Items ?? new();
                RecalculateSummary();
            }
            else
            {
                _allItems = new();
                RecalculateSummary();
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

    // ========== 汇总计算 ==========

    private void ToggleSummaryCard() => _showSummaryCard = !_showSummaryCard;

    private void RecalculateSummary()
    {
        _totalOrderCount = _allItems.Count;
        _totalWeight = _allItems.Sum(x => x.TotalWeight);

        // 成购（外购成品）：成品计划量 − 成品到货量 = 未到货量（缺口口径，外购由供应商生产、本厂不投料）
        Func<RawMaterialLockPlanAndExecutionDto, decimal> purchaseCalc = x =>
            Math.Max(0m, x.FinishPlanWeight - x.FinishInWeight);
        _purchaseWeight = _allItems.Sum(purchaseCalc);
        _purchaseCount = _allItems.Count(x => x.FinishPlanWeight > x.FinishInWeight);

        // 待投料分档口径（均扣除外购成品，与订单总览 R1 同口径）：
        //   A 质量补料：投料已满足但产出不足（质量损失），补料按流转比缺口折算 = (总重−成购)×1.1×(1−流转比/100)
        //   C 执行计划 / D 完善计划 等：正常缺料 = (总重−成购)×1.1 − 已投料
        Func<RawMaterialLockPlanAndExecutionDto, decimal> pendingCalc = x =>
            RawMaterialLockRemarkKeys.ToKey(x.RawMaterialLockRemark) == RawMaterialLockRemarkKeys.QualityReplenish
                ? Math.Max(0m, (x.TotalWeight - purchaseCalc(x)) * 1.1m * (1m - x.FlowOutputRatio / 100m))
                : Math.Max(0m, (x.TotalWeight - purchaseCalc(x)) * 1.1m - x.InputWeight);

        _pendingWeight = _allItems.Sum(pendingCalc);

        // 交叉矩阵：原料锁定备注 × 主号计划性（每工单行独立归桶）
        _summaryMatrix.Clear();
        foreach (var item in _allItems)
        {
            var remarkKey = RawMaterialLockRemarkKeys.ToKey(item.RawMaterialLockRemark) ?? "";
            var urgencyKey = UrgencyLevelKeys.ToKey(item.UrgencyLevel) ?? "";
            var key = $"{remarkKey}|{urgencyKey}";
            var cell = _summaryMatrix.GetValueOrDefault(key);
            var purchaseWeight = Math.Max(0m, item.FinishPlanWeight - item.FinishInWeight);
            _summaryMatrix[key] = new MatrixCell(
                cell.Count + 1,
                cell.PendingWeight + pendingCalc(item),
                cell.PurchaseCount + (purchaseWeight > 0 ? 1 : 0),
                cell.PurchaseWeight + purchaseWeight);
        }
    }

    private MatrixCell GetMatrixCell(string remarkKey, string urgencyKey)
        => _summaryMatrix.GetValueOrDefault($"{remarkKey}|{urgencyKey}");

    private MatrixCell GetMatrixRowTotal(string remarkKey)
    {
        var cell = new MatrixCell(0, 0m, 0, 0m);
        foreach (var u in _urgencyColumns)
        {
            var c = GetMatrixCell(remarkKey, u);
            cell = new MatrixCell(
                cell.Count + c.Count, cell.PendingWeight + c.PendingWeight,
                cell.PurchaseCount + c.PurchaseCount, cell.PurchaseWeight + c.PurchaseWeight);
        }
        return cell;
    }

    private MatrixCell GetMatrixColumnTotal(string urgencyKey)
    {
        var cell = new MatrixCell(0, 0m, 0, 0m);
        foreach (var r in RawMaterialLockRemarkKeys.All)
        {
            var c = GetMatrixCell(r, urgencyKey);
            cell = new MatrixCell(
                cell.Count + c.Count, cell.PendingWeight + c.PendingWeight,
                cell.PurchaseCount + c.PurchaseCount, cell.PurchaseWeight + c.PurchaseWeight);
        }
        return cell;
    }

    private MatrixCell GetMatrixGrandTotal()
    {
        var cell = new MatrixCell(0, 0m, 0, 0m);
        foreach (var r in RawMaterialLockRemarkKeys.All)
        {
            var c = GetMatrixRowTotal(r);
            cell = new MatrixCell(
                cell.Count + c.Count, cell.PendingWeight + c.PendingWeight,
                cell.PurchaseCount + c.PurchaseCount, cell.PurchaseWeight + c.PurchaseWeight);
        }
        return cell;
    }

    // 待投料矩阵格：单数 + 待投料重量
    private static string FormatMatrixCell(MatrixCell cell)
        => cell.Count > 0 ? $"{cell.Count} 单 / {cell.PendingWeight / 1000m:F1}吨" : "-";

    // 成购矩阵格：仅外购成品未到（PurchaseCount>0）时显示单数 + 成购重量
    private static string FormatPurchaseCell(MatrixCell cell)
        => cell.PurchaseCount > 0 ? $"{cell.PurchaseCount} 单 / {cell.PurchaseWeight / 1000m:F1}吨" : "-";

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
                        Display = col.Key switch
                        {
                            "UrgencyLevel" => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, val) ?? val!,
                            "RawMaterialLockRemark" => RawMaterialLockRemarkKeys.ToChinese(val) ?? val!,
                            _ => val!
                        },
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
        "SettlementMethod" => item.SettlementMethod.ToString(),
        "MaterialName" => item.MaterialName,
        "DeliveryState" => item.DeliveryState.ToString(),
        "LengthStatus" => item.LengthStatus.ToString(),
        "EndCustomer" => item.EndCustomer,
        // 枚举/档位列：筛选选项 Value 均为档位数字，GetFilterValue 必须返回数字字符串
        "MaterialPlanStatus" => ((int)item.MaterialPlanStatus).ToString(),
        "MainNoMaterialPlanStatus" => ((int)item.MainNoMaterialPlanStatus).ToString(),
        "MainNoPlanExecutionStatus" => item.MainNoPlanExecutionStatus.ToString(),
        "ActualMainNoInputStatus" => item.ActualMainNoInputStatus.ToString(),
        "PlanInputConsistency" => item.PlanInputConsistency.ToString(),
        "PiercingSubStatus" => item.PiercingSubStatus.ToString(),
        "PiercingReturnStatus" => item.PiercingReturnStatus.ToString(),
        "SemiOrderStatus" => item.SemiOrderStatus.ToString(),
        "SemiInStatus" => item.SemiInStatus.ToString(),
        "FinishOrderStatus" => item.FinishOrderStatus.ToString(),
        "FinishInStatus" => item.FinishInStatus.ToString(),
        "InventoryOutStatus" => item.InventoryOutStatus.ToString(),
        "ReworkPlanInputStatus" => item.ReworkPlanInputStatus.ToString(),
        "InProcessReworkInputStatus" => item.InProcessReworkInputStatus.ToString(),
        "InputStatus" => item.InputStatus.ToString(),
        "MainNoInputStatus" => item.MainNoInputStatus.ToString(),
        "FlowStatus" => item.FlowStatus.ToString(),
        "MainNoFlowStatus" => item.MainNoFlowStatus.ToString(),
        "ScheduleStage" => item.ScheduleStage.ToString(),
        "UrgencyLevel" => item.UrgencyLevel,
        "RawMaterialLockRemark" => item.RawMaterialLockRemark,
        "AdjustmentRemark" => item.AdjustmentRemark,
        "DelayPenalty" => item.DelayPenalty ? "True" : "False",
        "IsUrging" => item.IsUrging ? "True" : "False",
        "IsBatchDelivery" => item.IsBatchDelivery ? "True" : "False",
        "IsPaused" => item.IsPaused ? "True" : "False",
        "IsPreInput" => item.IsPreInput ? "True" : "False",
        // G13 number 列（非空/空筛选）
        "MainNoFlowOutputRatio" => item.MainNoFlowOutputRatio.ToString(),
        "FlowOutputRatio" => item.FlowOutputRatio.ToString(),
        "FlowTotalBatchCount" => item.FlowTotalBatchCount.ToString(),
        "FlowIncompleteBatchCount" => item.FlowIncompleteBatchCount.ToString(),
        "FlowMaxRemainingWorkDays" => item.FlowMaxRemainingWorkDays.ToString(),
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
                (x.EndCustomer?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.PlantGrade?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.Specification?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.ProductionMainNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.ProductionSubNo?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (DisplayHelper.GetSettlementMethodText(x.SettlementMethod).Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.MaterialName?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (DisplayHelper.GetDeliveryStateText(x.DeliveryState).Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (DisplayHelper.GetLengthStatusText(x.LengthStatus).Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.MaterialPlanProportion?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.UrgencyLevel?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.RawMaterialLockRemark?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.AdjustmentRemark?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true));
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
            else if (col.FilterType == "number")
            {
                // number 列仅提供"非空/空"筛选（FilterNotNull/FilterNull 常量）
                var wantNotNull = kvp.Value.Contains(FilterNotNull);
                var wantNull = kvp.Value.Contains(FilterNull);
                query = query.Where(x =>
                {
                    var val = GetFilterValue(x, kvp.Key);
                    return (wantNotNull && val != null) || (wantNull && val == null);
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
            "EndCustomer" => sortDescending ? query.OrderByDescending(x => x.EndCustomer) : query.OrderBy(x => x.EndCustomer),
            "MaterialPlanStatus" => sortDescending ? query.OrderByDescending(x => x.MaterialPlanStatus) : query.OrderBy(x => x.MaterialPlanStatus),
            "MainNoMaterialPlanRate" => sortDescending ? query.OrderByDescending(x => x.MainNoMaterialPlanRate) : query.OrderBy(x => x.MainNoMaterialPlanRate),
            "MainNoMaterialPlanStatus" => sortDescending ? query.OrderByDescending(x => x.MainNoMaterialPlanStatus) : query.OrderBy(x => x.MainNoMaterialPlanStatus),
            "MainNoPlanExecutionStatus" => sortDescending ? query.OrderByDescending(x => x.MainNoPlanExecutionStatus) : query.OrderBy(x => x.MainNoPlanExecutionStatus),
            "MaterialPlanCoveredCount" => sortDescending ? query.OrderByDescending(x => x.MaterialPlanCoveredCount) : query.OrderBy(x => x.MaterialPlanCoveredCount),
            "MaterialPlanProportion" => sortDescending ? query.OrderByDescending(x => x.MaterialPlanProportion) : query.OrderBy(x => x.MaterialPlanProportion),
            "TheoreticalCutoffDate" => sortDescending ? query.OrderByDescending(x => x.TheoreticalCutoffDate) : query.OrderBy(x => x.TheoreticalCutoffDate),
            "TotalPlanWeight" => sortDescending ? query.OrderByDescending(x => x.TotalPlanWeight) : query.OrderBy(x => x.TotalPlanWeight),
            "CutoffArrivalDate" => sortDescending ? query.OrderByDescending(x => x.CutoffArrivalDate) : query.OrderBy(x => x.CutoffArrivalDate),
            "TotalAvailableWeight" => sortDescending ? query.OrderByDescending(x => x.TotalAvailableWeight) : query.OrderBy(x => x.TotalAvailableWeight),
            "TotalMissingWeight" => sortDescending ? query.OrderByDescending(x => x.TotalMissingWeight) : query.OrderBy(x => x.TotalMissingWeight),
            "ActualInputWeight" => sortDescending ? query.OrderByDescending(x => x.InputWeight) : query.OrderBy(x => x.InputWeight),
            "PlanInputConsistency" => sortDescending ? query.OrderByDescending(x => x.PlanInputConsistency) : query.OrderBy(x => x.PlanInputConsistency),
            // G5 圆棒穿孔
            "PiercingPlanWeight" => sortDescending ? query.OrderByDescending(x => x.PiercingPlanWeight) : query.OrderBy(x => x.PiercingPlanWeight),
            "PiercingSubOutWeight" => sortDescending ? query.OrderByDescending(x => x.PiercingSubOutWeight) : query.OrderBy(x => x.PiercingSubOutWeight),
            "PiercingSubStatus" => sortDescending ? query.OrderByDescending(x => x.PiercingSubStatus) : query.OrderBy(x => x.PiercingSubStatus),
            "PiercingSubInWeight" => sortDescending ? query.OrderByDescending(x => x.PiercingSubInWeight) : query.OrderBy(x => x.PiercingSubInWeight),
            "PiercingSubPendingWeight" => sortDescending ? query.OrderByDescending(x => x.PiercingSubPendingWeight) : query.OrderBy(x => x.PiercingSubPendingWeight),
            "PiercingReturnStatus" => sortDescending ? query.OrderByDescending(x => x.PiercingReturnStatus) : query.OrderBy(x => x.PiercingReturnStatus),
            // G6 荒管采购
            "SemiPlanWeight" => sortDescending ? query.OrderByDescending(x => x.SemiPlanWeight) : query.OrderBy(x => x.SemiPlanWeight),
            "SemiOrderWeight" => sortDescending ? query.OrderByDescending(x => x.SemiOrderWeight) : query.OrderBy(x => x.SemiOrderWeight),
            "SemiOrderStatus" => sortDescending ? query.OrderByDescending(x => x.SemiOrderStatus) : query.OrderBy(x => x.SemiOrderStatus),
            "SemiInWeight" => sortDescending ? query.OrderByDescending(x => x.SemiInWeight) : query.OrderBy(x => x.SemiInWeight),
            "SemiPendingWeight" => sortDescending ? query.OrderByDescending(x => x.SemiPendingWeight) : query.OrderBy(x => x.SemiPendingWeight),
            "SemiInStatus" => sortDescending ? query.OrderByDescending(x => x.SemiInStatus) : query.OrderBy(x => x.SemiInStatus),
            // G7 成品采购
            "FinishPlanWeight" => sortDescending ? query.OrderByDescending(x => x.FinishPlanWeight) : query.OrderBy(x => x.FinishPlanWeight),
            "FinishOrderWeight" => sortDescending ? query.OrderByDescending(x => x.FinishOrderWeight) : query.OrderBy(x => x.FinishOrderWeight),
            "FinishOrderStatus" => sortDescending ? query.OrderByDescending(x => x.FinishOrderStatus) : query.OrderBy(x => x.FinishOrderStatus),
            "FinishInWeight" => sortDescending ? query.OrderByDescending(x => x.FinishInWeight) : query.OrderBy(x => x.FinishInWeight),
            "FinishPendingWeight" => sortDescending ? query.OrderByDescending(x => x.FinishPendingWeight) : query.OrderBy(x => x.FinishPendingWeight),
            "FinishInStatus" => sortDescending ? query.OrderByDescending(x => x.FinishInStatus) : query.OrderBy(x => x.FinishInStatus),
            // G8 库存使用
            "InventoryPlanWeight" => sortDescending ? query.OrderByDescending(x => x.InventoryPlanWeight) : query.OrderBy(x => x.InventoryPlanWeight),
            "InventoryOutWeight" => sortDescending ? query.OrderByDescending(x => x.InventoryOutWeight) : query.OrderBy(x => x.InventoryOutWeight),
            "InventoryOutStatus" => sortDescending ? query.OrderByDescending(x => x.InventoryOutStatus) : query.OrderBy(x => x.InventoryOutStatus),
            // G9 库料改制
            "ReworkPlanWeight" => sortDescending ? query.OrderByDescending(x => x.ReworkPlanWeight) : query.OrderBy(x => x.ReworkPlanWeight),
            "ReworkPlanInputWeight" => sortDescending ? query.OrderByDescending(x => x.ReworkPlanInputWeight) : query.OrderBy(x => x.ReworkPlanInputWeight),
            "ReworkPlanInputStatus" => sortDescending ? query.OrderByDescending(x => x.ReworkPlanInputStatus) : query.OrderBy(x => x.ReworkPlanInputStatus),
            // G10 在产改制
            "InProcessReworkPlanWeight" => sortDescending ? query.OrderByDescending(x => x.InProcessReworkPlanWeight) : query.OrderBy(x => x.InProcessReworkPlanWeight),
            "InProcessReworkInputWeight" => sortDescending ? query.OrderByDescending(x => x.InProcessReworkInputWeight) : query.OrderBy(x => x.InProcessReworkInputWeight),
            "InProcessReworkInputStatus" => sortDescending ? query.OrderByDescending(x => x.InProcessReworkInputStatus) : query.OrderBy(x => x.InProcessReworkInputStatus),
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
        await ColumnPrefs.SaveAsync("rawmateriallockplan", null, _allColumns);
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

    // ========== 预执行内联操作 ==========

    private async Task TogglePreInput(RawMaterialLockPlanAndExecutionDto item, bool newValue)
    {
        var ids = new List<int> { item.WorkOrderId };
        var result = await RawMaterialLockPlanService.SetPreExecuteFlagsAsync(ids, newValue);
        if (result.Success)
        {
            item.IsPreInput = newValue;
            RecalculateSummary();
            ApplyFiltersAndSort();
            await SavePageStateAsync();
        }
        else
        {
            Snackbar.Add(result.Message ?? "操作失败", Severity.Error);
        }
    }

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

            var request = new RawMaterialLockPlanPrintRequest
            {
                Title = "原锁计划",
                Items = printItems,
                Columns = printColumns
            };

            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/raw-material-lock-plan/print-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private static object ResolvePrintValue(RawMaterialLockPlanAndExecutionDto item, string key) => key switch
    {
        // G1: 枚举→中文
        "MaterialName" => DisplayHelper.GetPipeManufacturingTypeText(item.MaterialName) ?? "",
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState) ?? "",
        "LengthStatus" => DisplayHelper.GetLengthStatusText(item.LengthStatus) ?? "",
        "SettlementMethod" => DisplayHelper.GetSettlementMethodText(item.SettlementMethod) ?? "",
        // G2: 状态枚举
        "MaterialPlanStatus" => DisplayHelper.GetMaterialPlanStatusText(item.MaterialPlanStatus),
        "MainNoMaterialPlanStatus" => DisplayHelper.GetMaterialPlanStatusText(item.MainNoMaterialPlanStatus),
        // G3: 投料状态
        "InputStatus" => DisplayHelper.GetFlowStatusText(item.InputStatus),
        "MainNoInputStatus" => DisplayHelper.GetFlowStatusText(item.MainNoInputStatus),
        // G7: 流转状态
        "FlowStatus" => DisplayHelper.GetFlowStatusText(item.FlowStatus),
        "MainNoFlowStatus" => DisplayHelper.GetMainNoFlowStatusText(item.MainNoFlowStatus),
        // G3: 关注状态
        "ScheduleStage" => item.ScheduleStageText,
        // G4: 用料计划执行状态
        "MainNoPlanExecutionStatus" => item.MainNoPlanExecutionStatusText,
        "ActualMainNoInputStatus" => item.ActualMainNoInputStatusText,
        "PlanInputConsistency" => item.PlanInputConsistencyText,
        // G5~G10: 用料执行状态
        "PiercingSubStatus" => item.PiercingSubStatusText,
        "PiercingReturnStatus" => item.PiercingReturnStatusText,
        "SemiOrderStatus" => item.SemiOrderStatusText,
        "SemiInStatus" => item.SemiInStatusText,
        "FinishOrderStatus" => item.FinishOrderStatusText,
        "FinishInStatus" => item.FinishInStatusText,
        "InventoryOutStatus" => item.InventoryOutStatusText,
        "ReworkPlanInputStatus" => item.ReworkPlanInputStatusText,
        "InProcessReworkInputStatus" => item.InProcessReworkInputStatusText,
        // 非枚举字段原样输出（TablePrintHelper 自动处理 bool→"是/否"、DateTime→"yyyy-MM-dd" 等）
        _ => GetRawPropertyValue(item, key)!
    };

    private static object? GetRawPropertyValue(RawMaterialLockPlanAndExecutionDto item, string key)
    {
        // 大多数字段可直接通过 DTO 属性读取
        return key switch
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
            "MainNoMaterialPlanRate" => item.MainNoMaterialPlanRate,
            "MaterialPlanProportion" => item.MaterialPlanProportion ?? "",
            "MaterialPlanCoveredCount" => item.MaterialPlanCoveredCount,
            "TheoreticalCutoffDate" => item.TheoreticalCutoffDate,
            "CutoffArrivalDate" => item.CutoffArrivalDate,
            "TotalPlanWeight" => item.TotalPlanWeight,
            "TotalAvailableWeight" => item.TotalAvailableWeight,
            "TotalMissingWeight" => item.TotalMissingWeight,
            "ActualInputWeight" => item.ActualInputWeight,
            "EndCustomer" => item.EndCustomer ?? "",
            // G5 圆棒穿孔
            "PiercingPlanWeight" => item.PiercingPlanWeight,
            "PiercingSubOutWeight" => item.PiercingSubOutWeight,
            "PiercingSubInWeight" => item.PiercingSubInWeight,
            "PiercingSubPendingWeight" => item.PiercingSubPendingWeight,
            // G6 荒管采购
            "SemiPlanWeight" => item.SemiPlanWeight,
            "SemiOrderWeight" => item.SemiOrderWeight,
            "SemiInWeight" => item.SemiInWeight,
            "SemiPendingWeight" => item.SemiPendingWeight,
            // G7 成品采购
            "FinishPlanWeight" => item.FinishPlanWeight,
            "FinishOrderWeight" => item.FinishOrderWeight,
            "FinishInWeight" => item.FinishInWeight,
            "FinishPendingWeight" => item.FinishPendingWeight,
            // G8 库存使用
            "InventoryPlanWeight" => item.InventoryPlanWeight,
            "InventoryOutWeight" => item.InventoryOutWeight,
            // G9 库料改制
            "ReworkPlanWeight" => item.ReworkPlanWeight,
            "ReworkPlanInputWeight" => item.ReworkPlanInputWeight,
            // G10 在产改制
            "InProcessReworkPlanWeight" => item.InProcessReworkPlanWeight,
            "InProcessReworkInputWeight" => item.InProcessReworkInputWeight,
            "InputStartDate" => item.InputStartDate,
            "InputEndDate" => item.InputEndDate,
            "TotalBatchCount" => item.TotalBatchCount,
            "InputQuantity" => item.InputQuantity,
            "InputWeight" => item.InputWeight,
            "TheoreticalOutputQty" => item.TheoreticalOutputQty,
            "TheoreticalOutputWeight" => item.TheoreticalOutputWeight,
            "InputOutputRatio" => item.InputOutputRatio,
            "MainNoInputOutputRatio" => item.MainNoInputOutputRatio,
            "FlowOutputRatio" => item.FlowOutputRatio,
            "FlowMaxRemainingWorkDays" => item.FlowMaxRemainingWorkDays,
            "FlowTotalBatchCount" => item.FlowTotalBatchCount,
            "FlowIncompleteBatchCount" => item.FlowIncompleteBatchCount,
            "TotalRemainingWorkDays" => item.TotalRemainingWorkDays,
            "CapacityWorkDays" => item.CapacityWorkDays,
            "UrgencyLevel" => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, item.UrgencyLevel) ?? "",
            "EstimatedProcessCompletionDate" => item.EstimatedProcessCompletionDate,
            "DaysDiffFromDelivery" => item.DaysDiffFromDelivery,
            "RawMaterialLockRemark" => RawMaterialLockRemarkKeys.ToChinese(item.RawMaterialLockRemark) ?? "",
            "IsUrging" => item.IsUrging,
            "IsBatchDelivery" => item.IsBatchDelivery,
            "IsPaused" => item.IsPaused,
            "AdjustmentRemark" => item.AdjustmentRemark ?? "",
            "IsPreInput" => item.IsPreInput,
            "BudgetInputDate" => item.BudgetInputDate,
            _ => ""
        };
    }

    private async Task OnBudgetInputDateChanged(RawMaterialLockPlanAndExecutionDto item, DateTime newDate)
    {
        var ids = new List<int> { item.WorkOrderId };
        var result = await RawMaterialLockPlanService.SetPreExecuteFlagsAsync(ids, null, newDate);
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
            3 => "col-g3",
            4 => "col-g4",
            5 => "col-g5",
            6 => "col-g6",
            7 => "col-g7",
            8 => "col-g8",
            9 => "col-g9",
            10 => "col-g10",
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
            3 => "col-g3-cell",
            4 => "col-g4-cell",
            5 => "col-g5-cell",
            6 => "col-g6-cell",
            7 => "col-g7-cell",
            8 => "col-g8-cell",
            9 => "col-g9-cell",
            10 => "col-g10-cell",
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
        var savedPrefs = await ColumnPrefs.LoadAsync("rawmateriallockplan", null);
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
        var savedState = await PageState.LoadAsync("rawmateriallockplan");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "ScheduleStage";
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
        await JS.InvokeVoidAsync("initGroupHeaders", "#raw-material-lock-plan-list-table");

        // 分页导航/页大小切换后重算当前页汇总（pager 操作只改 CurrentPage/RowsPerPage，不触发 ApplyFiltersAndSort）
        if (table != null && _filteredItems.Count > 0)
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
                builder.AddContent(0, DisplayHelper.GetPipeManufacturingTypeText(item.MaterialName));
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
                builder.AddContent(0, DisplayHelper.GetWorkOrderLengthStatusText(item.LengthStatus, item.MinLength, item.MaxLength));
                break;
            case "EndCustomer":
                builder.AddContent(0, item.EndCustomer ?? "-");
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
            case "MaterialPlanStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanStatusColor(item.MaterialPlanStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetMaterialPlanStatusText(item.MaterialPlanStatus))));
                builder.CloseComponent();
                break;
            case "MaterialPlanProportion":
                builder.AddContent(0, item.MaterialPlanProportion ?? "-");
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
            case "MainNoPlanExecutionStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetMainNoPlanExecutionStatusColor(item.MainNoPlanExecutionStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.MainNoPlanExecutionStatusText)));
                builder.CloseComponent();
                break;
            case "ActualMainNoInputStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetInputStatusColor(item.MainNoInputStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.ActualMainNoInputStatusText)));
                builder.CloseComponent();
                break;
            case "MaterialPlanCoveredCount":
                builder.AddContent(0, item.MaterialPlanCoveredCount);
                break;
            case "TheoreticalCutoffDate":
                builder.AddContent(0, item.TheoreticalCutoffDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "TotalPlanWeight":
                builder.AddContent(0, item.TotalPlanWeight > 0 ? ((int)item.TotalPlanWeight).ToString() : "-");
                break;
            case "CutoffArrivalDate":
                builder.AddContent(0, item.CutoffArrivalDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "TotalAvailableWeight":
                builder.AddContent(0, item.TotalAvailableWeight > 0 ? ((int)item.TotalAvailableWeight).ToString() : "-");
                break;
            case "TotalMissingWeight":
                builder.AddContent(0, item.TotalMissingWeight > 0 ? ((int)item.TotalMissingWeight).ToString() : "-");
                break;
            case "ActualInputWeight":
                builder.AddContent(0, item.ActualInputWeight > 0 ? ((int)item.ActualInputWeight).ToString() : "-");
                break;
            case "PlanInputConsistency":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanInputConsistencyColor(item.PlanInputConsistency));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.PlanInputConsistencyText)));
                builder.CloseComponent();
                break;

            // G5~G10: 用料计划执行
            case "PiercingPlanWeight":
            case "SemiPlanWeight":
            case "FinishPlanWeight":
            case "InventoryPlanWeight":
            case "ReworkPlanWeight":
            case "InProcessReworkPlanWeight":
                builder.AddContent(0, GetWeightText(item, col.Key));
                break;
            case "PiercingSubOutWeight":
            case "SemiOrderWeight":
            case "FinishOrderWeight":
            case "InventoryOutWeight":
            case "ReworkPlanInputWeight":
            case "InProcessReworkInputWeight":
                builder.AddContent(0, GetWeightText(item, col.Key));
                break;
            case "PiercingSubInWeight":
            case "PiercingSubPendingWeight":
            case "SemiInWeight":
            case "SemiPendingWeight":
            case "FinishInWeight":
            case "FinishPendingWeight":
                builder.AddContent(0, GetWeightText(item, col.Key));
                break;
            case "PiercingSubStatus":
            case "PiercingReturnStatus":
            case "SemiOrderStatus":
            case "SemiInStatus":
            case "FinishOrderStatus":
            case "FinishInStatus":
            case "InventoryOutStatus":
            case "ReworkPlanInputStatus":
            case "InProcessReworkInputStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanExecutionStatusColor(GetStatusInt(item, col.Key)));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetPlanExecutionStatusText(item, col.Key))));
                builder.CloseComponent();
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
                builder.AddAttribute(2, "Color", DisplayHelper.GetInputStatusColor(item.InputStatus));
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
                builder.AddAttribute(2, "Color", DisplayHelper.GetInputStatusColor(item.MainNoInputStatus));
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
                builder.AddAttribute(2, "Color", DisplayHelper.GetInputStatusColor(item.FlowStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, GetFlowStatusText(item.FlowStatus))));
                builder.CloseComponent();
                break;
            case "MainNoFlowOutputRatio":
                builder.AddContent(0, item.MainNoFlowOutputRatio > 0 ? $"{item.MainNoFlowOutputRatio:F1}%" : "-");
                break;
            case "MainNoFlowStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetInputStatusColor(item.MainNoFlowStatus));
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

            // G12
            case "ScheduleStage":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetScheduleStageColor(item.ScheduleStage));
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
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", DisplayHelper.GetUrgencyColor(item.UrgencyLevel));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey, item.UrgencyLevel) ?? "-")));
                builder.CloseComponent();
                break;
            case "EstimatedProcessCompletionDate":
                builder.AddContent(0, item.EstimatedProcessCompletionDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "DaysDiffFromDelivery":
                builder.AddContent(0, item.DaysDiffFromDelivery.HasValue ? $"{item.DaysDiffFromDelivery}天" : "-");
                break;
            case "RawMaterialLockRemark":
                builder.AddContent(0, RawMaterialLockRemarkKeys.ToChinese(item.RawMaterialLockRemark) ?? "-");
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
        }
    };

    // ========== 文本辅助 ==========

    private static string GetMaterialPlanStatusText(MaterialPlanStatus status) =>
        DisplayHelper.GetMaterialPlanStatusText(status);

    private static string GetInputStatusText(int status) =>
        DisplayHelper.GetFlowStatusText(status);

    private static string GetFlowStatusText(int status) =>
        DisplayHelper.GetFlowStatusText(status);

    private static string GetValidMainNoStatusText(int status) =>
        DisplayHelper.GetMainNoFlowStatusText(status);

    // ========== G5~G10 重量/状态渲染辅助 ==========

    private static decimal GetWeightValue(RawMaterialLockPlanAndExecutionDto item, string key) => key switch
    {
        "PiercingPlanWeight" => item.PiercingPlanWeight,
        "PiercingSubOutWeight" => item.PiercingSubOutWeight,
        "PiercingSubInWeight" => item.PiercingSubInWeight,
        "PiercingSubPendingWeight" => item.PiercingSubPendingWeight,
        "SemiPlanWeight" => item.SemiPlanWeight,
        "SemiOrderWeight" => item.SemiOrderWeight,
        "SemiInWeight" => item.SemiInWeight,
        "SemiPendingWeight" => item.SemiPendingWeight,
        "FinishPlanWeight" => item.FinishPlanWeight,
        "FinishOrderWeight" => item.FinishOrderWeight,
        "FinishInWeight" => item.FinishInWeight,
        "FinishPendingWeight" => item.FinishPendingWeight,
        "InventoryPlanWeight" => item.InventoryPlanWeight,
        "InventoryOutWeight" => item.InventoryOutWeight,
        "ReworkPlanWeight" => item.ReworkPlanWeight,
        "ReworkPlanInputWeight" => item.ReworkPlanInputWeight,
        "InProcessReworkPlanWeight" => item.InProcessReworkPlanWeight,
        "InProcessReworkInputWeight" => item.InProcessReworkInputWeight,
        _ => 0m
    };

    private static string GetWeightText(RawMaterialLockPlanAndExecutionDto item, string key)
    {
        var v = GetWeightValue(item, key);
        return v > 0 ? ((int)v).ToString() : "-";
    }

    private static int GetStatusInt(RawMaterialLockPlanAndExecutionDto item, string key) => key switch
    {
        "PiercingSubStatus" => item.PiercingSubStatus,
        "PiercingReturnStatus" => item.PiercingReturnStatus,
        "SemiOrderStatus" => item.SemiOrderStatus,
        "SemiInStatus" => item.SemiInStatus,
        "FinishOrderStatus" => item.FinishOrderStatus,
        "FinishInStatus" => item.FinishInStatus,
        "InventoryOutStatus" => item.InventoryOutStatus,
        "ReworkPlanInputStatus" => item.ReworkPlanInputStatus,
        "InProcessReworkInputStatus" => item.InProcessReworkInputStatus,
        _ => 0
    };

    private static string GetPlanExecutionStatusText(RawMaterialLockPlanAndExecutionDto item, string key) =>
        IntStatusDisplayHelper.GetPlanExecutionStatusText(GetStatusInt(item, key));

    // ========== 颜色 ==========

    private static Color GetPlanStatusColor(MaterialPlanStatus status) => status switch
    {
        MaterialPlanStatus.NotPlanned => Color.Default,
        MaterialPlanStatus.Partial => Color.Warning,
        MaterialPlanStatus.TheoreticalSatisfied => Color.Info,
        MaterialPlanStatus.Satisfied => Color.Success,
        MaterialPlanStatus.Excess => Color.Default,
        _ => Color.Default
    };

    /// <summary>用料计划执行状态颜色（G4~G10 共用：0无计划 1未执行 2部分 3已完成 4异常）</summary>
    private static Color GetPlanExecutionStatusColor(int status) => status switch
    {
        0 => Color.Default,
        1 => Color.Default,
        2 => Color.Warning,
        3 => Color.Success,
        4 => Color.Error,
        _ => Color.Default
    };

    /// <summary>主号计划执行状态颜色（0无计划 1未执行 2执行中 3计划落实）</summary>
    private static Color GetMainNoPlanExecutionStatusColor(int status) => status switch
    {
        0 => Color.Default,
        1 => Color.Default,
        2 => Color.Warning,
        3 => Color.Success,
        _ => Color.Default
    };

    /// <summary>到料实投一致性颜色（0一致 1待投 2疑问-到料少投 3疑问-到料超投 4/5错误系 6略）</summary>
    private static Color GetPlanInputConsistencyColor(int c) => c switch
    {
        0 => Color.Success,
        1 => Color.Info,
        2 => Color.Warning,
        3 => Color.Warning,
        4 => Color.Error,
        5 => Color.Error,
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
        await PageState.SaveAsync("rawmateriallockplan", state);
    }
}
