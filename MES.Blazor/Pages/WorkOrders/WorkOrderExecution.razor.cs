using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.WorkOrder;
using System.Text.Json;

namespace MES.Blazor.Pages.WorkOrders;

public partial class WorkOrderExecution
{
    private MudTable<WorkOrderExecutionSummaryDto>? table;
    private List<WorkOrderExecutionSummaryDto> _pageItems = new();
    private int _totalCount;
    private bool _isArrowNavSetup;
    private int _restoredPageIndex;
    private int _currentPageIndex = 1;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;

    // 排序状态
    private string sortColumn = "LastRefreshTime";
    private bool sortDescending = true;

    // 最后刷新时间
    private DateTime? lastRefreshTime;

    // 刷新状态
    private bool _isRefreshing;

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
            new() { Key = "SignDate",                Label = "订单日期",        SortKey = "SignDate",                FilterType = "date", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DeliveryDate",            Label = "交货日期",        SortKey = "DeliveryDate",            FilterType = "date", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DelayPenalty",            Label = "延期罚款",        SortKey = "DelayPenalty",            FilterType = "boolean", Width = "120", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SettlementMethod",        Label = "结算方式",        SortKey = "SettlementMethod",        FilterType = "enum", Width = "120", EnumOptions = new() { new("Weighing","过磅"), new("WeighingNegative","过磅-负"), new("Theoretical","理算") }, Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SalesOrderNo",            Label = "订单号",          SortKey = "SalesOrderNo",            FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionMainNo",        Label = "主号",            SortKey = "ProductionMainNo",        FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionSubNo",         Label = "次号",            SortKey = "ProductionSubNo",         FilterType = "string", Width = "120", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MaterialName",            Label = "钢管制造",        SortKey = "MaterialName",            FilterType = "enum", Width = "120", EnumOptions = new() { new("SeamlessPipe","无缝管"), new("WeldedPipe","焊管") }, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DeliveryState",           Label = "交货状态",        SortKey = "DeliveryState",           FilterType = "enum", Width = "120", EnumOptions = new() { new("SolutionAnnealedAndPickled","固溶酸洗"), new("SolutionAnnealedAndPickledUTube","固溶酸洗-U型管"), new("SolutionAnnealedAndPickledExternalPolished","固溶酸洗-外抛光"), new("SolutionAnnealedAndPickledInternalPolished","固溶酸洗-内抛光"), new("SolutionAnnealedAndPickledBothPolished","固溶酸洗-内外抛光"), new("SolutionAnnealedAndPickledCoiled","固溶酸洗-盘管"), new("Bright","光亮"), new("BrightUTube","光亮-U型管"), new("BrightCoiled","光亮-盘管"), new("Hard","硬态"), new("SolidSolutionStraightening","固溶矫直") }, Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "PlantGrade",              Label = "工厂牌号",        SortKey = "PlantGrade",              FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "Specification",           Label = "规格",            SortKey = "Specification",           FilterType = "string", Width = "120", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "LengthStatus",            Label = "长度状态",        SortKey = "LengthStatus",            FilterType = "enum", Width = "120", EnumOptions = new() { new("Fixed","定尺"), new("Range","范围尺"), new("NonFixed","非定尺") }, Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MinLength",               Label = "最小长度",        SortKey = "MinLength",               Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MaxLength",               Label = "最大长度",        SortKey = "MaxLength",               Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalItemCount",          Label = "含项次数",        SortKey = "TotalItemCount",          Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalQuantity",           Label = "总支数",          SortKey = "TotalQuantity",           Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalMeters",             Label = "总米数",          SortKey = "TotalMeters",             Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalWeight",             Label = "总重量",          SortKey = "TotalWeight",             Width = "80", Visible = false, GroupKey = 1, GroupName = "基础数据" },
        };

        // G3: 用料计划
        var g2 = new List<ColumnDef>
        {
            new() { Key = "MainNoMaterialPlanStatus",Label = "主号计划状态",    SortKey = "MainNoMaterialPlanStatus", FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未计划"), new("1","部分"), new("2","理论满足"), new("3","满足"), new("4","超量") }, GroupKey = 3, GroupName = "用料计划" },
            new() { Key = "MainNoMaterialPlanRate",  Label = "主号满足率(%)",   SortKey = "MainNoMaterialPlanRate",  Width = "80",                               GroupKey = 3, GroupName = "用料计划" },
            new() { Key = "TheoreticalCutoffDate",    Label = "理论截止投料日",  SortKey = "TheoreticalCutoffDate",   Width = "120", FilterType = "date",        GroupKey = 3, GroupName = "用料计划" },
            new() { Key = "MaterialPlanStatus",      Label = "用料计划状态",    SortKey = "MaterialPlanStatus",      FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未计划"), new("1","部分"), new("2","理论满足"), new("3","满足"), new("4","超量") }, GroupKey = 3, GroupName = "用料计划" },
            new() { Key = "MaterialPlanCoveredCount", Label = "料态种数",       SortKey = "MaterialPlanCoveredCount", Width = "80",                               GroupKey = 3, GroupName = "用料计划" },
            new() { Key = "MaterialPlanProportion",   Label = "用料占比",       SortKey = "MaterialPlanProportion",   Width = "120",                             GroupKey = 3, GroupName = "用料计划" },
            new() { Key = "TotalPlanWeight",         Label = "计划投料总重",    SortKey = "TotalPlanWeight",         Width = "100",                             GroupKey = 3, GroupName = "用料计划" },
            new() { Key = "TotalAvailableWeight",     Label = "现可投料总重",    SortKey = "TotalAvailableWeight",     Width = "100",                             GroupKey = 3, GroupName = "用料计划" },
            new() { Key = "TotalMissingWeight",       Label = "理论缺失总料重",  SortKey = "TotalMissingWeight",       Width = "100",                             GroupKey = 3, GroupName = "用料计划" },
            new() { Key = "ActualInputWeight",        Label = "实际已投料量",    SortKey = "ActualInputWeight",        Width = "100",                             GroupKey = 3, GroupName = "用料计划" },
            new() { Key = "ActualMainNoInputStatus",  Label = "实投主号状态",    SortKey = "ActualMainNoInputStatus",  Width = "100",                             GroupKey = 3, GroupName = "用料计划" },
            new() { Key = "PlanInputConsistency",     Label = "计划实投一致性",  SortKey = "PlanInputConsistency",     Width = "100",                             GroupKey = 3, GroupName = "用料计划" },
        };

        // G14: 返整执行数据
        var g6 = new List<ColumnDef>
        {
            new() { Key = "ReworkInputEndDate",          Label = "返整投料截止日", SortKey = "ReworkInputEndDate",      Width = "120", GroupKey = 14, GroupName = "返整执行" },
            new() { Key = "ReworkBatchCount",            Label = "返整批次数",     SortKey = "ReworkBatchCount",        Width = "80",                          GroupKey = 14, GroupName = "返整执行" },
            new() { Key = "ReworkInputQuantity",         Label = "返整投料支数",   SortKey = "ReworkInputQuantity",    Width = "80", Visible = false,       GroupKey = 14, GroupName = "返整执行" },
            new() { Key = "ReworkInputWeight",           Label = "返整投料重量",   SortKey = "ReworkInputWeight",      Width = "80", Visible = false,       GroupKey = 14, GroupName = "返整执行" },
            new() { Key = "ReworkTheoreticalOutputQty",  Label = "返整理论成品支", SortKey = "ReworkTheoreticalOutputQty",  Width = "80",                  GroupKey = 14, GroupName = "返整执行" },
            new() { Key = "ReworkTheoreticalOutputWeight",Label = "返整理论成品重",SortKey = "ReworkTheoreticalOutputWeight", Width = "80",               GroupKey = 14, GroupName = "返整执行" },
        };

        // G11: 原始投料
        var g3 = new List<ColumnDef>
        {
            new() { Key = "InputStartDate",          Label = "原始投料起始日",  SortKey = "InputStartDate",          Width = "120", GroupKey = 11, GroupName = "原始投料" },
            new() { Key = "InputEndDate",            Label = "原始投料截止日",  SortKey = "InputEndDate",            Width = "120", GroupKey = 11, GroupName = "原始投料" },
            new() { Key = "TotalBatchCount",         Label = "原始批次数",     SortKey = "TotalBatchCount",         Width = "80",                              GroupKey = 11, GroupName = "原始投料" },
            new() { Key = "InputQuantity",           Label = "原始总支数",      SortKey = "InputQuantity",           Width = "80", Visible = false,       GroupKey = 11, GroupName = "原始投料" },
            new() { Key = "InputWeight",             Label = "原始总重量",      SortKey = "InputWeight",             Width = "80", Visible = false,       GroupKey = 11, GroupName = "原始投料" },
            new() { Key = "TheoreticalOutputQty",    Label = "原始理论成品支",   SortKey = "TheoreticalOutputQty",    Width = "80", Visible = false,       GroupKey = 11, GroupName = "原始投料" },
            new() { Key = "TheoreticalOutputWeight", Label = "原始理论成品重",   SortKey = "TheoreticalOutputWeight", Width = "80", Visible = false,       GroupKey = 11, GroupName = "原始投料" },
            new() { Key = "InputOutputRatio",        Label = "原始成品比",     SortKey = "InputOutputRatio",        Width = "80",                             GroupKey = 11, GroupName = "原始投料" },
            new() { Key = "InputStatus",             Label = "原始投料状态",    SortKey = "InputStatus",             FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未投料"), new("1","部分"), new("2","满足") }, GroupKey = 11, GroupName = "原始投料" },
            new() { Key = "MainNoInputRatio",        Label = "原始主号比",     SortKey = "MainNoInputRatio",        Width = "80", Visible = false,       GroupKey = 11, GroupName = "原始投料" },
            new() { Key = "MainNoInputStatus",       Label = "原始主号状态",    SortKey = "MainNoInputStatus",       FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未投料"), new("1","部分"), new("2","满足") }, Visible = false, GroupKey = 11, GroupName = "原始投料" },
        };

        // G13: 合格流转
        var g4 = new List<ColumnDef>
        {
            new() { Key = "ValidBatchCount",         Label = "有效批次数",     SortKey = "ValidBatchCount",         Width = "80",                              GroupKey = 13, GroupName = "合格流转" },
            new() { Key = "ValidInputQuantity",      Label = "有效流转总支数",  SortKey = "ValidInputQuantity",      Width = "80", Visible = false,       GroupKey = 13, GroupName = "合格流转" },
            new() { Key = "ValidInputWeight",        Label = "有效流转总重量",  SortKey = "ValidInputWeight",        Width = "80", Visible = false,       GroupKey = 13, GroupName = "合格流转" },
            new() { Key = "ValidOutputQty",          Label = "流转成品支数",   SortKey = "ValidOutputQty",          Width = "80", Visible = false,       GroupKey = 13, GroupName = "合格流转" },
            new() { Key = "ValidOutputWeight",       Label = "流转成品重量",   SortKey = "ValidOutputWeight",       Width = "80", Visible = false,       GroupKey = 13, GroupName = "合格流转" },
        };

        // G12: 有效流转
        var g7 = new List<ColumnDef>
        {
            new() { Key = "FlowOutputRatio",        Label = "流转成品比",     SortKey = "FlowOutputRatio",        Width = "80",                             GroupKey = 12, GroupName = "有效流转" },
            new() { Key = "FlowStatus",             Label = "有效流转状态",    SortKey = "FlowStatus",             FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未投料"), new("1","部分"), new("2","满足") }, GroupKey = 12, GroupName = "有效流转" },
            new() { Key = "MainNoFlowRatio",        Label = "有效主号流转比", SortKey = "MainNoFlowOutputRatio",         Width = "80", Visible = false,       GroupKey = 12, GroupName = "有效流转" },
            new() { Key = "MainNoFlowStatus",       Label = "有效主号状态",   SortKey = "MainNoFlowStatus",       FilterType = "enum", Width = "120", EnumOptions = new() { new("0","未计划"), new("1","部分"), new("2","满足") }, Visible = false, GroupKey = 12, GroupName = "有效流转" },
            new() { Key = "FlowTotalBatchCount",    Label = "总批次数",      SortKey = "FlowTotalBatchCount",    Width = "80",                              GroupKey = 12, GroupName = "有效流转" },
            new() { Key = "FlowIncompleteBatchCount",Label = "未完成批数",    SortKey = "FlowIncompleteBatchCount",Width = "80",                            GroupKey = 12, GroupName = "有效流转" },
            new() { Key = "FlowMaxRemainingWorkDays", Label = "最大剩余工量(天)",SortKey = "FlowMaxRemainingWorkDays", Width = "80",                         GroupKey = 12, GroupName = "有效流转" },
        };

        // G19: 过程不合格
        var g8 = new List<ColumnDef>
        {
            new() { Key = "DefectiveRawQty",         Label = "原料不合格支数", SortKey = "DefectiveRawQty",         Width = "80",                              GroupKey = 19, GroupName = "过程不合格" },
            new() { Key = "DefectiveRawWeight",      Label = "原料不合格重量", SortKey = "DefectiveRawWeight",      Width = "80", Visible = false,       GroupKey = 19, GroupName = "过程不合格" },
            new() { Key = "DefectiveOutputQty",      Label = "影响成品支数",   SortKey = "DefectiveOutputQty",      Width = "80",                           GroupKey = 19, GroupName = "过程不合格" },
            new() { Key = "DefectiveOutputWeight",   Label = "影响成品重量",   SortKey = "DefectiveOutputWeight",   Width = "80", Visible = false,       GroupKey = 19, GroupName = "过程不合格" },
            new() { Key = "DefectiveRatio",          Label = "不合格占比",     SortKey = "DefectiveRatio",          Width = "80",                                GroupKey = 19, GroupName = "过程不合格" },
        };

        // G20: 成检不合格
        var g9 = new List<ColumnDef>
        {
            new() { Key = "InspectionStartDate",     Label = "成检起始日",    SortKey = "InspectionStartDate",    Width = "120", GroupKey = 20, GroupName = "成检不合格" },
            new() { Key = "InspectionEndDate",       Label = "成检截止日",    SortKey = "InspectionEndDate",      Width = "120", GroupKey = 20, GroupName = "成检不合格" },
            new() { Key = "InspectionDefectQty",     Label = "成检不合格支数", SortKey = "InspectionDefectQty",    Width = "80",                          GroupKey = 20, GroupName = "成检不合格" },
            new() { Key = "InspectionDefectWeight",  Label = "成检不合格重量", SortKey = "InspectionDefectWeight", Width = "80", Visible = false,       GroupKey = 20, GroupName = "成检不合格" },
            new() { Key = "InspectionDefectRatio",   Label = "成检不合格占比", SortKey = "InspectionDefectRatio",  Width = "80",                          GroupKey = 20, GroupName = "成检不合格" },
        };

        // G18: 汇总不合格
        var g10 = new List<ColumnDef>
        {
            new() { Key = "GeneralDefectWeight",     Label = "一般问题重",     SortKey = "GeneralDefectWeight",     Width = "80",                          GroupKey = 18, GroupName = "汇总不合格" },
            new() { Key = "GeneralDefectRatio",      Label = "一般问题占比",   SortKey = "GeneralDefectRatio",      Width = "80",                          GroupKey = 18, GroupName = "汇总不合格" },
            new() { Key = "SeriousDefectWeight",     Label = "严重问题重",     SortKey = "SeriousDefectWeight",    Width = "80", Visible = false,       GroupKey = 18, GroupName = "汇总不合格" },
            new() { Key = "SeriousDefectRatio",      Label = "严重问题占比",   SortKey = "SeriousDefectRatio",      Width = "80",                          GroupKey = 18, GroupName = "汇总不合格" },
            new() { Key = "ScrapWeight",             Label = "成检报废重量",   SortKey = "ScrapWeight",            Width = "80", Visible = false,       GroupKey = 18, GroupName = "汇总不合格" },
            new() { Key = "ScrapRatio",              Label = "成检报废占比",   SortKey = "ScrapRatio",              Width = "80",                          GroupKey = 18, GroupName = "汇总不合格" },
        };

        // G15: 成品入库
        var g11 = new List<ColumnDef>
        {
            new() { Key = "WarehousingStartDate",    Label = "入库起始日",    SortKey = "WarehousingStartDate",    Width = "120", GroupKey = 15, GroupName = "成品入库" },
            new() { Key = "WarehousingEndDate",      Label = "入库截止日",    SortKey = "WarehousingEndDate",      Width = "120", GroupKey = 15, GroupName = "成品入库" },
            new() { Key = "WarehousingTotalQty",     Label = "入库总支数",    SortKey = "WarehousingTotalQty",     Width = "80",                        GroupKey = 15, GroupName = "成品入库" },
            new() { Key = "WarehousingTotalWeight",  Label = "入库总重量",    SortKey = "WarehousingTotalWeight",  Width = "80",                        GroupKey = 15, GroupName = "成品入库" },
            new() { Key = "WoWarehousingStatus",     Label = "工单入库状态",    SortKey = "WoWarehousingStatus",     FilterType = "enum", Width = "120", EnumOptions = new() { new("0","无入库"), new("1","入库部分"), new("2","入库完结") }, GroupKey = 15, GroupName = "成品入库" },
            new() { Key = "MainNoWarehousingStatus", Label = "主号入库状态",  SortKey = "MainNoWarehousingStatus", FilterType = "enum", Width = "120", EnumOptions = new() { new("0","无入库"), new("1","入库部分"), new("2","入库完结") }, Visible = false, GroupKey = 15, GroupName = "成品入库" },
            new() { Key = "OrderWarehousingStatus",  Label = "订单入库状态",  SortKey = "OrderWarehousingStatus", FilterType = "enum", Width = "120", EnumOptions = new() { new("0","无入库"), new("1","入库部分"), new("2","入库完结") }, Visible = false, GroupKey = 15, GroupName = "成品入库" },
        };

        // G16: 实时关注
        var g12 = new List<ColumnDef>
        {
            new() { Key = "ScheduleStage",           Label = "关注状态",      SortKey = "ScheduleStage",           FilterType = "enum", Width = "120", EnumOptions = new() { new("0","工单完成"), new("1","原料锁定"), new("2","生产执行"), new("3","成品检验") }, GroupKey = 16, GroupName = "实时关注" },
            new() { Key = "TotalRemainingWorkDays",  Label = "工艺剩余总工量(天)",SortKey = "TotalRemainingWorkDays",  Width = "80",                        GroupKey = 16, GroupName = "实时关注" },
            new() { Key = "CapacityWorkDays",         Label = "产能工量(天)",  SortKey = "CapacityWorkDays",         Width = "80",                        GroupKey = 16, GroupName = "实时关注" },
            new() { Key = "UrgencyLevel",            Label = "工单计划性",    SortKey = "UrgencyLevel",            FilterType = "string", Width = "120",                              GroupKey = 16, GroupName = "实时关注" },
            new() { Key = "EstimatedProcessCompletionDate",Label = "预计完成日",SortKey = "EstimatedProcessCompletionDate", Width = "120",                  GroupKey = 16, GroupName = "实时关注" },
            new() { Key = "DaysDiffFromDelivery",    Label = "交期相差天数",  SortKey = "DaysDiffFromDelivery",    Width = "80",                              GroupKey = 16, GroupName = "实时关注" },
            new() { Key = "RawMaterialLockRemark",   Label = "原锁备注",     SortKey = "RawMaterialLockRemark",   FilterType = "string", Width = "120",                             GroupKey = 16, GroupName = "实时关注" },
        };

        // G2: 工单需求调整
        var g13 = new List<ColumnDef>
        {
            new() { Key = "IsUrging",              Label = "催单",             SortKey = "IsUrging", FilterType = "boolean", Width = "80", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 2, GroupName = "工单需求调整" },
            new() { Key = "IsBatchDelivery",       Label = "分批交货",         SortKey = "IsBatchDelivery", FilterType = "boolean", Width = "80", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 2, GroupName = "工单需求调整" },
            new() { Key = "IsPaused",               Label = "暂停",             SortKey = "IsPaused", FilterType = "boolean", Width = "80", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 2, GroupName = "工单需求调整" },
            new() { Key = "AdjustmentRemark",       Label = "调整备注",         SortKey = "AdjustmentRemark", FilterType = "string", Width = "120", GroupKey = 2, GroupName = "工单需求调整" },
        };

        // G17: 在产节点待量
        var g14 = new List<ColumnDef>
        {
            new() { Key = "PendingSectionRoughTube",       Label = "荒管处理待量(kg)",   SortKey = "PendingSectionRoughTube",       Width = "90",  GroupKey = 17, GroupName = "在产节点待量" },
            new() { Key = "PendingSectionWarehouseFix",    Label = "在制修检待量(kg)",   SortKey = "PendingSectionWarehouseFix",    Width = "90",  GroupKey = 17, GroupName = "在产节点待量" },
            new() { Key = "PendingSection60Roll",          Label = "60冷轧待量(kg)",     SortKey = "PendingSection60Roll",          Width = "90",  GroupKey = 17, GroupName = "在产节点待量" },
            new() { Key = "PendingSection50Roll",          Label = "50冷轧待量(kg)",     SortKey = "PendingSection50Roll",          Width = "90",  GroupKey = 17, GroupName = "在产节点待量" },
            new() { Key = "PendingSection30Roll",          Label = "30冷轧待量(kg)",     SortKey = "PendingSection30Roll",          Width = "90",  GroupKey = 17, GroupName = "在产节点待量" },
            new() { Key = "PendingSection20Roll",          Label = "20冷轧待量(kg)",     SortKey = "PendingSection20Roll",          Width = "90",  GroupKey = 17, GroupName = "在产节点待量" },
            new() { Key = "PendingSectionThreeRoll",       Label = "三辊冷轧待量(kg)",   SortKey = "PendingSectionThreeRoll",       Width = "90",  GroupKey = 17, GroupName = "在产节点待量" },
            new() { Key = "PendingSectionDrawBench",       Label = "冷拔待量(kg)",       SortKey = "PendingSectionDrawBench",       Width = "90",  GroupKey = 17, GroupName = "在产节点待量" },
            new() { Key = "DeformedProcessCompleted",      Label = "变形工序完成",       SortKey = "DeformedProcessCompleted",      FilterType = "boolean", Width = "100", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 17, GroupName = "在产节点待量" },
            new() { Key = "ProductionAttentionProcess",    Label = "生产关注工序",       SortKey = "ProductionAttentionProcess",    FilterType = "string", Width = "100", GroupKey = 17, GroupName = "在产节点待量" },
            new() { Key = "ProductionFlowProperty",        Label = "生产流转性",         SortKey = "ProductionFlowProperty", FilterType = "string", Width = "100", GroupKey = 17, GroupName = "在产节点待量" },
            new() { Key = "MaxBatchRemainingWorkDays",   Label = "最大剩余工量(天)", SortKey = "MaxBatchRemainingWorkDays", FilterType = "string", Width = "80",  GroupKey = 17, GroupName = "在产节点待量" },
            new() { Key = "MainNoAttentionProcess",      Label = "主号关注工序",    SortKey = "MainNoAttentionProcess",    FilterType = "string", Width = "120", GroupKey = 17, GroupName = "在产节点待量" },
        };

        // G4~G10: 7 种用料计划执行状况
        // G4: 圆棒穿孔
        var g15 = new List<ColumnDef>
        {
            new() { Key = "PiercingPlanWeight",        Label = "穿孔计划量(kg)",    SortKey = "PiercingPlanWeight",        Width = "80",  GroupKey = 4, GroupName = "圆棒穿孔" },
            new() { Key = "PiercingSubOutWeight",      Label = "穿孔委外量(kg)",    SortKey = "PiercingSubOutWeight",      Width = "80",  GroupKey = 4, GroupName = "圆棒穿孔" },
            new() { Key = "PiercingSubStatus",         Label = "穿孔委外状态",      SortKey = "PiercingSubStatus",         Width = "100", GroupKey = 4, GroupName = "圆棒穿孔" },
            new() { Key = "PiercingSubInWeight",       Label = "穿孔回收量(kg)",    SortKey = "PiercingSubInWeight",       Width = "80",  GroupKey = 4, GroupName = "圆棒穿孔" },
            new() { Key = "PiercingSubPendingWeight",  Label = "穿孔待回收(kg)",    SortKey = "PiercingSubPendingWeight",  Width = "80",  GroupKey = 4, GroupName = "圆棒穿孔" },
            new() { Key = "PiercingReturnStatus",      Label = "穿孔回收状态",      SortKey = "PiercingReturnStatus",      Width = "100", GroupKey = 4, GroupName = "圆棒穿孔" },
        };
        // G5: 荒管采购
        var g16 = new List<ColumnDef>
        {
            new() { Key = "SemiPlanWeight",            Label = "荒管计划量(kg)",    SortKey = "SemiPlanWeight",            Width = "80",  GroupKey = 5, GroupName = "荒管采购" },
            new() { Key = "SemiOrderWeight",           Label = "荒管采购量(kg)",    SortKey = "SemiOrderWeight",           Width = "80",  GroupKey = 5, GroupName = "荒管采购" },
            new() { Key = "SemiOrderStatus",           Label = "荒管采购状态",      SortKey = "SemiOrderStatus",           Width = "100", GroupKey = 5, GroupName = "荒管采购" },
            new() { Key = "SemiInWeight",              Label = "荒管到货量(kg)",    SortKey = "SemiInWeight",              Width = "80",  GroupKey = 5, GroupName = "荒管采购" },
            new() { Key = "SemiPendingWeight",         Label = "荒管待货(kg)",      SortKey = "SemiPendingWeight",         Width = "80",  GroupKey = 5, GroupName = "荒管采购" },
            new() { Key = "SemiInStatus",              Label = "荒管到货状态",      SortKey = "SemiInStatus",              Width = "100", GroupKey = 5, GroupName = "荒管采购" },
        };
        // G6: 成品采购
        var g17 = new List<ColumnDef>
        {
            new() { Key = "FinishPlanWeight",          Label = "成品计划量(kg)",    SortKey = "FinishPlanWeight",          Width = "80",  GroupKey = 6, GroupName = "成品采购" },
            new() { Key = "FinishOrderWeight",         Label = "成品采购量(kg)",    SortKey = "FinishOrderWeight",         Width = "80",  GroupKey = 6, GroupName = "成品采购" },
            new() { Key = "FinishOrderStatus",         Label = "成品采购状态",      SortKey = "FinishOrderStatus",         Width = "100", GroupKey = 6, GroupName = "成品采购" },
            new() { Key = "FinishInWeight",            Label = "成品到货量(kg)",    SortKey = "FinishInWeight",            Width = "80",  GroupKey = 6, GroupName = "成品采购" },
            new() { Key = "FinishPendingWeight",       Label = "成品待货(kg)",      SortKey = "FinishPendingWeight",       Width = "80",  GroupKey = 6, GroupName = "成品采购" },
            new() { Key = "FinishInStatus",            Label = "成品到货状态",      SortKey = "FinishInStatus",            Width = "100", GroupKey = 6, GroupName = "成品采购" },
        };
        // G7: 库存使用
        var g18 = new List<ColumnDef>
        {
            new() { Key = "InventoryPlanWeight",       Label = "库存计划量(kg)",    SortKey = "InventoryPlanWeight",       Width = "80",  GroupKey = 7, GroupName = "库存使用" },
            new() { Key = "InventoryOutWeight",        Label = "库存出库量(kg)",    SortKey = "InventoryOutWeight",        Width = "80",  GroupKey = 7, GroupName = "库存使用" },
            new() { Key = "InventoryOutStatus",        Label = "库存出库状态",      SortKey = "InventoryOutStatus",        Width = "100", GroupKey = 7, GroupName = "库存使用" },
        };
        // G8: 库料改制
        var g19 = new List<ColumnDef>
        {
            new() { Key = "ReworkPlanWeight",          Label = "改制计划量(kg)",    SortKey = "ReworkPlanWeight",          Width = "80",  GroupKey = 8, GroupName = "库料改制" },
            new() { Key = "ReworkPlanInputWeight",     Label = "改制投料量(kg)",    SortKey = "ReworkPlanInputWeight",     Width = "80",  GroupKey = 8, GroupName = "库料改制" },
            new() { Key = "ReworkPlanInputStatus",     Label = "改制投料状态",      SortKey = "ReworkPlanInputStatus",     Width = "100", GroupKey = 8, GroupName = "库料改制" },
        };
        // G9: 在产改制
        var g20 = new List<ColumnDef>
        {
            new() { Key = "InProcessReworkPlanWeight",      Label = "产改计划量(kg)",  SortKey = "InProcessReworkPlanWeight",      Width = "80",  GroupKey = 9, GroupName = "在产改制" },
            new() { Key = "InProcessReworkInputWeight",     Label = "产改投料量(kg)",  SortKey = "InProcessReworkInputWeight",     Width = "80",  GroupKey = 9, GroupName = "在产改制" },
            new() { Key = "InProcessReworkInputStatus",     Label = "产改投料状态",    SortKey = "InProcessReworkInputStatus",     Width = "100", GroupKey = 9, GroupName = "在产改制" },
        };
        // G10: 在产主工单
        var g21 = new List<ColumnDef>
        {
            new() { Key = "InMainPlanWeight",           Label = "主工单计划量(kg)",  SortKey = "InMainPlanWeight",           Width = "80",  GroupKey = 10, GroupName = "在产主工单" },
            new() { Key = "InMainInputWeight",          Label = "主工单投料量(kg)",  SortKey = "InMainInputWeight",          Width = "80",  GroupKey = 10, GroupName = "在产主工单" },
            new() { Key = "InMainInputStatus",          Label = "主工单投料状态",    SortKey = "InMainInputStatus",          Width = "100", GroupKey = 10, GroupName = "在产主工单" },
        };

        var all = new List<ColumnDef>();
        // 按前端显示顺序排列
        all.AddRange(g1);   // 1  基础数据
        all.AddRange(g13);  // 2  工单需求调整
        all.AddRange(g2);   // 3  用料计划
        all.AddRange(g15);  // 4  圆棒穿孔
        all.AddRange(g16);  // 5  荒管采购
        all.AddRange(g17);  // 6  成品采购
        all.AddRange(g18);  // 7  库存使用
        all.AddRange(g19);  // 8  库料改制
        all.AddRange(g20);  // 9  在产改制
        all.AddRange(g21);  // 10 在产主工单
        all.AddRange(g3);   // 11 原始投料
        all.AddRange(g7);   // 12 有效流转
        all.AddRange(g4);   // 13 合格流转
        all.AddRange(g6);   // 14 返整执行
        all.AddRange(g11);  // 15 成品入库
        all.AddRange(g12);  // 16 实时关注
        all.AddRange(g14);  // 17 在产节点待量
        all.AddRange(g10);  // 18 汇总不合格
        all.AddRange(g8);   // 19 过程不合格
        all.AddRange(g9);   // 20 成检不合格
        return all;
    }

    // ========== 分页汇总 ==========

    /// <summary>当前页汇总值（列Key → 格式化后的汇总文本）</summary>
    private Dictionary<string, string> _pageSums = new();

    /// <summary>可汇总的数值列（支数/米数/重量）</summary>
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        // 支数 (int)
        "TotalItemCount", "TotalQuantity",
        "TotalBatchCount", "InputQuantity",
        "ValidBatchCount", "ValidInputQuantity",
        "ReworkBatchCount", "ReworkInputQuantity",
        "FlowTotalBatchCount", "FlowIncompleteBatchCount",
        "DefectiveRawQty", "InspectionDefectQty",
        "WarehousingTotalQty",
        // 支数 (decimal in DTO)
        "ValidOutputQty", "ReworkTheoreticalOutputQty",
        "DefectiveOutputQty",
        // 米数
        "TotalMeters",
        // 重量
        "TotalWeight",
        "InputWeight", "TheoreticalOutputWeight",
        "ValidInputWeight", "ValidOutputWeight",
        "ReworkInputWeight", "ReworkTheoreticalOutputWeight",
        "DefectiveRawWeight", "DefectiveOutputWeight",
        "InspectionDefectWeight",
        "GeneralDefectWeight", "SeriousDefectWeight", "ScrapWeight",
        "WarehousingTotalWeight",
        // G17: 在产节点待量
        "PendingSectionRoughTube", "PendingSectionWarehouseFix",
        "PendingSection60Roll", "PendingSection50Roll",
        "PendingSection30Roll", "PendingSection20Roll",
        "PendingSectionThreeRoll", "PendingSectionDrawBench",
        // G4~G10: 用料计划执行重量
        "PiercingPlanWeight", "PiercingSubOutWeight", "PiercingSubInWeight", "PiercingSubPendingWeight",
        "SemiPlanWeight", "SemiOrderWeight", "SemiInWeight", "SemiPendingWeight",
        "FinishPlanWeight", "FinishOrderWeight", "FinishInWeight", "FinishPendingWeight",
        "InventoryPlanWeight", "InventoryOutWeight",
        "ReworkPlanWeight", "ReworkPlanInputWeight",
        "InProcessReworkPlanWeight", "InProcessReworkInputWeight",
        "InMainPlanWeight", "InMainInputWeight",
    };

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(WorkOrderExecutionSummaryDto)
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
                    _pageSums[col.Key] = ((int)sum).ToString(); // §10.7: 列表页显示为整数
                }
                else if (type == typeof(int?))
                {
                    var sum = _pageItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[col.Key] = sum.ToString();
                }
                else if (type == typeof(decimal?))
                {
                    var sum = _pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[col.Key] = ((int)sum).ToString(); // §10.7: 列表页显示为整数
                }
            }
            catch
            {
                // ignore individual column sum errors
            }
        }
    }

    // ========== 行选中 ==========
    private HashSet<int> _selectedIds = new();

    private void SelectAllItems(bool selected)
    {
        if (selected)
        {
            foreach (var item in _pageItems)
                _selectedIds.Add(item.WorkOrderId);
        }
        else
        {
            foreach (var item in _pageItems)
                _selectedIds.Remove(item.WorkOrderId);
        }
    }

    private void ToggleSelection(WorkOrderExecutionSummaryDto item, bool selected)
    {
        if (selected)
            _selectedIds.Add(item.WorkOrderId);
        else
            _selectedIds.Remove(item.WorkOrderId);
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<WorkOrderExecutionSummaryDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        // 恢复持久化的页码（MudTable 初始化时始终传 page=0）
        if (_isFirstLoad)
        {
            state.Page = _restoredPageIndex;
            _isFirstLoad = false;
        }

        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "LastRefreshTime";
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

            var result = await WorkOrderExecutionService.GetPagedAsync(query,
                signDateFrom: DateTime.TryParse(_dateFrom, out var dFrom) ? dFrom : null,
                signDateTo: DateTime.TryParse(_dateTo, out var dTo) ? dTo : null);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;

                _totalCount = result.Data.TotalCount;
                _currentPageIndex = state.Page + 1;
                lastRefreshTime = _pageItems.Select(i => i.LastRefreshTime).DefaultIfEmpty().Max();
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

        return new TableData<WorkOrderExecutionSummaryDto>
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
            var result = await WorkOrderExecutionService.GetFilterContextsAsync();
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
                Display = v,
                Count = 0
            }).ToList();
        }

        // DelayPenalty 列显示中文
        if (_filterContextOptions.TryGetValue("DelayPenalty", out var delayOptions))
        {
            foreach (var opt in delayOptions)
            {
                opt.Display = opt.Value == "True" ? "是" : "否";
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
        if (table != null) await table.ReloadServerData();
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
        StateHasChanged();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
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

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnDateFromChanged(string value)
    {
        _dateFrom = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnDateToChanged(string value)
    {
        _dateTo = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 数据加载操作 ==========

    private async Task LoadAllDataAsync()
    {
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "LastRefreshTime";
            var query = new QueryParams
            {
                PageIndex = 1,
                PageSize = 100000,
                SortBy = sortBy,
                IsDescending = sortDescending,
                Keyword = _searchKeyword
            };

            var result = await WorkOrderExecutionService.GetPagedAsync(query,
                signDateFrom: DateTime.TryParse(_dateFrom, out var dFrom) ? dFrom : null,
                signDateTo: DateTime.TryParse(_dateTo, out var dTo) ? dTo : null);
            if (result.Success && result.Data != null)
            {
                _totalCount = result.Data.TotalCount;
                lastRefreshTime = result.Data.Items.Select(i => i.LastRefreshTime).DefaultIfEmpty().Max();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
        }
        if (table != null) await table.ReloadServerData();
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();

        // 恢复排序/筛选/列显隐状态
        var savedState = await PageState.LoadAsync("workorderexecution");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "LastRefreshTime";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _dateFrom = savedState.Extras?.ContainsKey("dateFrom") == true ? savedState.Extras["dateFrom"] ?? string.Empty : string.Empty;
            _dateTo = savedState.Extras?.ContainsKey("dateTo") == true ? savedState.Extras["dateTo"] ?? string.Empty : string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);

            // 恢复列显隐
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

            // 确保新字段始终可见（兼容旧保存状态不包含这些列）
            foreach (var col in _allColumns)
            {
                if (col.Key is "MaxBatchRemainingWorkDays" or "MainNoAttentionProcess")
                    col.Visible = true;
            }

            // 恢复列筛选
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

        // 加载筛选上下文
        await LoadFilterContextsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#workorder-execution-list-table"))
                _isArrowNavSetup = false;
        }

        // 分组标题栏：测量实际列宽 + 同步滚动
        await JS.InvokeVoidAsync("initGroupHeaders", "#workorder-execution-list-table");
    }

    // ========== 单元格渲染 ==========

    /// <summary>渲染页脚汇总值</summary>
    private string RenderFooterCell(ColumnDef col)
    {
        if (_pageSums.TryGetValue(col.Key, out var sum))
            return sum;
        return "-";
    }

    private RenderFragment RenderCell(WorkOrderExecutionSummaryDto item, ColumnDef col) => builder =>
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
                builder.AddContent(0, DisplayHelper.GetLengthStatusText(item.LengthStatus));
                break;
            case "MinLength":
                builder.AddContent(0, item.MinLength?.ToString("G29") ?? "-");
                break;
            case "MaxLength":
                builder.AddContent(0, item.MaxLength?.ToString("G29") ?? "-");
                break;
            case "TotalItemCount":
                builder.AddContent(0, item.TotalItemCount);
                break;
            case "TotalQuantity":
                builder.AddContent(0, item.TotalQuantity);
                break;
            case "TotalMeters":
                builder.AddContent(0, ((int)item.TotalMeters).ToString()); // §10.7: 列表页整数
                break;
            case "TotalWeight":
                builder.AddContent(0, ((int)item.TotalWeight).ToString()); // §10.7: 列表页整数
                break;
            case "MaterialPlanStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanStatusColor(item.MaterialPlanStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.MaterialPlanStatusText)));
                builder.CloseComponent();
                break;
            case "MainNoMaterialPlanRate":
                builder.AddContent(0, item.MainNoMaterialPlanRate > 0 ? $"{item.MainNoMaterialPlanRate:F1}%" : "-");
                break;
            case "MainNoMaterialPlanStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanStatusColor(item.MainNoMaterialPlanStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.MainNoMaterialPlanStatusText)));
                builder.CloseComponent();
                break;
            case "MaterialPlanProportion":
                builder.AddContent(0, item.MaterialPlanProportion ?? "-");
                break;
            case "TheoreticalCutoffDate":
                builder.AddContent(0, item.TheoreticalCutoffDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "MaterialPlanCoveredCount":
                builder.AddContent(0, item.MaterialPlanCoveredCount);
                break;
            case "TotalPlanWeight":
            case "TotalAvailableWeight":
            case "TotalMissingWeight":
            case "ActualInputWeight":
                var weightVal = (decimal?)typeof(WorkOrderExecutionSummaryDto).GetProperty(col.Key)?.GetValue(item) ?? 0m;
                builder.AddContent(0, weightVal > 0 ? ((int)weightVal).ToString() : "-");
                break;
            case "ActualMainNoInputStatus":
                builder.AddContent(0, item.ActualMainNoInputStatusText ?? "-");
                break;
            case "PlanInputConsistency":
                builder.AddContent(0, item.PlanInputConsistencyText ?? "-");
                break;

            // ========== G4: 圆棒穿孔 ==========
            case "PiercingPlanWeight":
            case "PiercingSubOutWeight":
            case "PiercingSubInWeight":
            case "PiercingSubPendingWeight":
                var piercingWt = (decimal?)typeof(WorkOrderExecutionSummaryDto).GetProperty(col.Key)?.GetValue(item) ?? 0m;
                builder.AddContent(0, piercingWt > 0 ? ((int)piercingWt).ToString() : "-");
                break;
            case "PiercingSubStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanExecutionStatusColor(item.PiercingSubStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.PiercingSubStatusText)));
                builder.CloseComponent();
                break;
            case "PiercingReturnStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanExecutionStatusColor(item.PiercingReturnStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.PiercingReturnStatusText)));
                builder.CloseComponent();
                break;

            // ========== G5: 荒管采购 ==========
            case "SemiPlanWeight":
            case "SemiOrderWeight":
            case "SemiInWeight":
            case "SemiPendingWeight":
                var semiWt = (decimal?)typeof(WorkOrderExecutionSummaryDto).GetProperty(col.Key)?.GetValue(item) ?? 0m;
                builder.AddContent(0, semiWt > 0 ? ((int)semiWt).ToString() : "-");
                break;
            case "SemiOrderStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanExecutionStatusColor(item.SemiOrderStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.SemiOrderStatusText)));
                builder.CloseComponent();
                break;
            case "SemiInStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanExecutionStatusColor(item.SemiInStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.SemiInStatusText)));
                builder.CloseComponent();
                break;

            // ========== G6: 成品采购 ==========
            case "FinishPlanWeight":
            case "FinishOrderWeight":
            case "FinishInWeight":
            case "FinishPendingWeight":
                var finishWt = (decimal?)typeof(WorkOrderExecutionSummaryDto).GetProperty(col.Key)?.GetValue(item) ?? 0m;
                builder.AddContent(0, finishWt > 0 ? ((int)finishWt).ToString() : "-");
                break;
            case "FinishOrderStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanExecutionStatusColor(item.FinishOrderStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.FinishOrderStatusText)));
                builder.CloseComponent();
                break;
            case "FinishInStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanExecutionStatusColor(item.FinishInStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.FinishInStatusText)));
                builder.CloseComponent();
                break;

            // ========== G7: 库存使用 ==========
            case "InventoryPlanWeight":
            case "InventoryOutWeight":
                var invWt = (decimal?)typeof(WorkOrderExecutionSummaryDto).GetProperty(col.Key)?.GetValue(item) ?? 0m;
                builder.AddContent(0, invWt > 0 ? ((int)invWt).ToString() : "-");
                break;
            case "InventoryOutStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanExecutionStatusColor(item.InventoryOutStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.InventoryOutStatusText)));
                builder.CloseComponent();
                break;

            // ========== G8: 库料改制 ==========
            case "ReworkPlanWeight":
            case "ReworkPlanInputWeight":
                var reworkWt = (decimal?)typeof(WorkOrderExecutionSummaryDto).GetProperty(col.Key)?.GetValue(item) ?? 0m;
                builder.AddContent(0, reworkWt > 0 ? ((int)reworkWt).ToString() : "-");
                break;
            case "ReworkPlanInputStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanExecutionStatusColor(item.ReworkPlanInputStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.ReworkPlanInputStatusText)));
                builder.CloseComponent();
                break;

            // ========== G9: 在产改制 ==========
            case "InProcessReworkPlanWeight":
            case "InProcessReworkInputWeight":
                var iprWt = (decimal?)typeof(WorkOrderExecutionSummaryDto).GetProperty(col.Key)?.GetValue(item) ?? 0m;
                builder.AddContent(0, iprWt > 0 ? ((int)iprWt).ToString() : "-");
                break;
            case "InProcessReworkInputStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanExecutionStatusColor(item.InProcessReworkInputStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.InProcessReworkInputStatusText)));
                builder.CloseComponent();
                break;

            // ========== G10: 在产主工单 ==========
            case "InMainPlanWeight":
            case "InMainInputWeight":
                var inMainWt = (decimal?)typeof(WorkOrderExecutionSummaryDto).GetProperty(col.Key)?.GetValue(item) ?? 0m;
                builder.AddContent(0, inMainWt > 0 ? ((int)inMainWt).ToString() : "-");
                break;
            case "InMainInputStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanExecutionStatusColor(item.InMainInputStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.InMainInputStatusText)));
                builder.CloseComponent();
                break;

            case "ReworkInputEndDate":
                builder.AddContent(0, item.ReworkInputEndDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "ReworkBatchCount":
                builder.AddContent(0, item.ReworkBatchCount);
                break;
            case "ReworkInputQuantity":
                builder.AddContent(0, item.ReworkInputQuantity);
                break;
            case "ReworkInputWeight":
                builder.AddContent(0, ((int)item.ReworkInputWeight).ToString()); // §10.7
                break;
            case "ReworkTheoreticalOutputQty":
                builder.AddContent(0, ((int)item.ReworkTheoreticalOutputQty).ToString()); // §10.7
                break;
            case "ReworkTheoreticalOutputWeight":
                builder.AddContent(0, ((int)item.ReworkTheoreticalOutputWeight).ToString()); // §10.7
                break;
            case "FlowOutputRatio":
                builder.AddContent(0, item.FlowOutputRatio > 0 ? $"{item.FlowOutputRatio:F1}%" : "-");
                break;
            case "FlowStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetInputStatusColor(item.FlowStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.FlowStatusText)));
                builder.CloseComponent();
                break;
            case "MainNoFlowRatio":
                builder.AddContent(0, item.MainNoFlowOutputRatio > 0 ? $"{item.MainNoFlowOutputRatio:F1}%" : "-");
                break;
            case "MainNoFlowStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetValidMainNoStatusColor(item.MainNoFlowStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.MainNoFlowStatusText)));
                builder.CloseComponent();
                break;
            case "InputStartDate":
                builder.AddContent(0, item.InputStartDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "InputEndDate":
                builder.AddContent(0, item.InputEndDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "TotalBatchCount":
                builder.AddContent(0, item.TotalBatchCount);
                break;
            case "InputQuantity":
                builder.AddContent(0, item.InputQuantity);
                break;
            case "InputWeight":
                builder.AddContent(0, ((int)item.InputWeight).ToString()); // §10.7
                break;
            case "TheoreticalOutputQty":
                builder.AddContent(0, ((int)item.TheoreticalOutputQty).ToString()); // §10.7
                break;
            case "TheoreticalOutputWeight":
                builder.AddContent(0, ((int)item.TheoreticalOutputWeight).ToString()); // §10.7
                break;
            case "InputOutputRatio":
                builder.AddContent(0, item.InputOutputRatio > 0 ? $"{item.InputOutputRatio:F1}%" : "-");
                break;
            case "InputStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetInputStatusColor(item.InputStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.InputStatusText)));
                builder.CloseComponent();
                break;
            case "MainNoInputRatio":
                builder.AddContent(0, item.MainNoInputOutputRatio > 0 ? $"{item.MainNoInputOutputRatio:F1}%" : "-");
                break;
            case "MainNoInputStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetInputStatusColor(item.MainNoInputStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.MainNoInputStatusText)));
                builder.CloseComponent();
                break;
            case "ValidBatchCount":
                builder.AddContent(0, item.ValidBatchCount);
                break;
            case "ValidInputQuantity":
                builder.AddContent(0, item.ValidInputQuantity);
                break;
            case "ValidInputWeight":
                builder.AddContent(0, ((int)item.ValidInputWeight).ToString()); // §10.7
                break;
            case "ValidOutputQty":
                builder.AddContent(0, ((int)item.ValidOutputQty).ToString()); // §10.7
                break;
            case "ValidOutputWeight":
                builder.AddContent(0, ((int)item.ValidOutputWeight).ToString()); // §10.7
                break;

            // ========== G19: 过程不合格 ==========
            case "DefectiveRawQty":
                builder.AddContent(0, item.DefectiveRawQty > 0 ? item.DefectiveRawQty.ToString() : "-");
                break;
            case "DefectiveRawWeight":
                builder.AddContent(0, item.DefectiveRawWeight > 0 ? ((int)item.DefectiveRawWeight).ToString() : "-"); // §10.7
                break;
            case "DefectiveOutputQty":
                builder.AddContent(0, item.DefectiveOutputQty > 0 ? ((int)item.DefectiveOutputQty).ToString() : "-"); // §10.7
                break;
            case "DefectiveOutputWeight":
                builder.AddContent(0, item.DefectiveOutputWeight > 0 ? ((int)item.DefectiveOutputWeight).ToString() : "-"); // §10.7
                break;
            case "DefectiveRatio":
                builder.AddContent(0, item.DefectiveRatio > 0 ? $"{item.DefectiveRatio:F1}%" : "-");
                break;

            // ========== G20: 成检不合格 ==========
            case "InspectionStartDate":
                builder.AddContent(0, item.InspectionStartDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "InspectionEndDate":
                builder.AddContent(0, item.InspectionEndDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "InspectionDefectQty":
                builder.AddContent(0, item.InspectionDefectQty > 0 ? item.InspectionDefectQty.ToString() : "-");
                break;
            case "InspectionDefectWeight":
                builder.AddContent(0, item.InspectionDefectWeight > 0 ? ((int)item.InspectionDefectWeight).ToString() : "-"); // §10.7
                break;
            case "InspectionDefectRatio":
                builder.AddContent(0, item.InspectionDefectRatio > 0 ? $"{item.InspectionDefectRatio:F1}%" : "-");
                break;

            // ========== G18: 汇总不合格 ==========
            case "GeneralDefectWeight":
                builder.AddContent(0, item.GeneralDefectWeight > 0 ? ((int)item.GeneralDefectWeight).ToString() : "-"); // §10.7
                break;
            case "GeneralDefectRatio":
                builder.AddContent(0, item.GeneralDefectRatio > 0 ? $"{item.GeneralDefectRatio:F1}%" : "-");
                break;
            case "SeriousDefectWeight":
                builder.AddContent(0, item.SeriousDefectWeight > 0 ? ((int)item.SeriousDefectWeight).ToString() : "-"); // §10.7
                break;
            case "SeriousDefectRatio":
                builder.AddContent(0, item.SeriousDefectRatio > 0 ? $"{item.SeriousDefectRatio:F1}%" : "-");
                break;
            case "ScrapWeight":
                builder.AddContent(0, item.ScrapWeight > 0 ? ((int)item.ScrapWeight).ToString() : "-"); // §10.7
                break;
            case "ScrapRatio":
                builder.AddContent(0, item.ScrapRatio > 0 ? $"{item.ScrapRatio:F1}%" : "-");
                break;

            // ========== G15: 成品入库 ==========
            case "WarehousingStartDate":
                builder.AddContent(0, item.WarehousingStartDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "WarehousingEndDate":
                builder.AddContent(0, item.WarehousingEndDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "WarehousingTotalQty":
                builder.AddContent(0, item.WarehousingTotalQty > 0 ? item.WarehousingTotalQty.ToString() : "-");
                break;
            case "WarehousingTotalWeight":
                builder.AddContent(0, item.WarehousingTotalWeight > 0 ? ((int)item.WarehousingTotalWeight).ToString() : "-"); // §10.7
                break;
            case "WoWarehousingStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetWarehousingStatusColor(item.WoWarehousingStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.WoWarehousingStatusText)));
                builder.CloseComponent();
                break;
            case "MainNoWarehousingStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetWarehousingStatusColor(item.MainNoWarehousingStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.MainNoWarehousingStatusText)));
                builder.CloseComponent();
                break;
            case "OrderWarehousingStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetWarehousingStatusColor(item.OrderWarehousingStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.OrderWarehousingStatusText)));
                builder.CloseComponent();
                break;
            case "FlowTotalBatchCount":
                builder.AddContent(0, item.FlowTotalBatchCount);
                break;
            case "FlowIncompleteBatchCount":
                builder.AddContent(0, item.FlowIncompleteBatchCount);
                break;
            case "FlowMaxRemainingWorkDays":
                builder.AddContent(0, item.FlowMaxRemainingWorkDays > 0 ? $"{item.FlowMaxRemainingWorkDays}天" : "-");
                break;
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

            // ========== G17: 在产节点待量 ==========
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
            case "MaxBatchRemainingWorkDays":
                builder.AddContent(0, item.MaxBatchRemainingWorkDays.HasValue ? $"{item.MaxBatchRemainingWorkDays}天" : "-");
                break;
            case "MainNoAttentionProcess":
                builder.AddContent(0, item.MainNoAttentionProcess ?? "-");
                break;

            // ========== G2: 工单需求调整 ==========
            case "IsUrging":
                builder.AddContent(0, item.IsUrging ? "是" : "否");
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

            // ========== 生产流转性 ==========
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
                        "略" => Color.Default,
                        _ => Color.Default
                    };
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", color);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, flowProp)));
                    builder.CloseComponent();
                }
                break;
        }
    };

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

    // ========== 颜色 ==========

    // ========== 分组 CSS class ==========

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
            11 => "col-g11",
            12 => "col-g12",
            13 => "col-g13",
            14 => "col-g14",
            15 => "col-g15",
            16 => "col-g16",
            17 => "col-g17",
            18 => "col-g18",
            19 => "col-g19",
            20 => "col-g20",
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
            11 => "col-g11-cell",
            12 => "col-g12-cell",
            13 => "col-g13-cell",
            14 => "col-g14-cell",
            15 => "col-g15-cell",
            16 => "col-g16-cell",
            17 => "col-g17-cell",
            18 => "col-g18-cell",
            19 => "col-g19-cell",
            20 => "col-g20-cell",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    private static Color GetPlanStatusColor(MaterialPlanStatus status) => status switch
    {
        MaterialPlanStatus.NotPlanned => Color.Default,
        MaterialPlanStatus.Partial => Color.Warning,
        MaterialPlanStatus.TheoreticalSatisfied => Color.Info,
        MaterialPlanStatus.Satisfied => Color.Success,
        MaterialPlanStatus.Excess => Color.Error,
        _ => Color.Default
    };

    private static Color GetInputStatusColor(int status) => status switch
    {
        0 => Color.Default,
        1 => Color.Warning,
        2 => Color.Success,
        _ => Color.Default
    };

    private static Color GetValidMainNoStatusColor(int status) => status switch
    {
        0 => Color.Default,
        1 => Color.Warning,
        2 => Color.Success,
        _ => Color.Default
    };

    private static Color GetWarehousingStatusColor(int status) => status switch
    {
        0 => Color.Default,
        1 => Color.Warning,
        2 => Color.Success,
        _ => Color.Default
    };

    // G4~G10 计划执行状态颜色（5档：0无计划 1未执行 2部分 3已完成 4异常）
    private static Color GetPlanExecutionStatusColor(int status) => status switch
    {
        0 => Color.Default,
        1 => Color.Default,
        2 => Color.Warning,
        3 => Color.Success,
        4 => Color.Error,
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

        if (!string.IsNullOrWhiteSpace(_dateFrom)) extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrWhiteSpace(_dateTo)) extras["dateTo"] = _dateTo;

        // 列显隐持久化
        extras["columnVisibility"] = JsonSerializer.Serialize(_allColumns.Where(c => c.Visible).Select(c => c.Key).ToList());

        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPageIndex,
            Extras = extras
        };
        await PageState.SaveAsync("workorderexecution", state);
    }

    // ========== 打印 ==========

    private async Task PrintAll()
    {
        try
        {
            var printColumns = _visibleColumns
                .Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label })
                .ToList();

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "LastRefreshTime";
            var request = new
            {
                keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy,
                isDescending = sortDescending,
                signDateFrom = DateTime.TryParse(_dateFrom, out var dFrom) ? dFrom.ToString("yyyy-MM-dd") : null,
                signDateTo = DateTime.TryParse(_dateTo, out var dTo) ? dTo.ToString("yyyy-MM-dd") : null,
                columns = printColumns
            };

            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/workorder-execution/print-all-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task PrintSelected()
    {
        if (_selectedIds.Count == 0)
        {
            Snackbar.Add("请先选择要打印的行", Severity.Warning);
            return;
        }

        try
        {
            var printColumns = _visibleColumns
                .Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label })
                .ToList();

            var selectedItems = _pageItems.Where(i => _selectedIds.Contains(i.WorkOrderId)).ToList();

            var printItems = selectedItems.Select(item =>
            {
                var dict = new Dictionary<string, object>();
                foreach (var col in _visibleColumns)
                {
                    dict[col.Key] = ResolvePrintValue(item, col.Key);
                }
                return dict;
            }).ToList();

            var request = new WorkOrderExecutionPrintRequest
            {
                Title = "工单执行状况",
                Items = printItems,
                Columns = printColumns
            };

            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/workorder-execution/print-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private static object ResolvePrintValue(WorkOrderExecutionSummaryDto item, string key) => key switch
    {
        // 枚举→中文
        "SettlementMethod" => DisplayHelper.GetSettlementMethodText(item.SettlementMethod) ?? "",
        "MaterialName" => DisplayHelper.GetPipeManufacturingTypeText(item.MaterialName) ?? "",
        "DeliveryState" => DisplayHelper.GetDeliveryStateText(item.DeliveryState) ?? "",
        "LengthStatus" => DisplayHelper.GetLengthStatusText(item.LengthStatus) ?? "",
        // Bool→中文
        "DelayPenalty" => item.DelayPenaltyText,
        "IsUrging" => item.IsUrging ? "是" : "否",
        "IsBatchDelivery" => item.IsBatchDelivery ? "是" : "否",
        "IsPaused" => item.IsPaused ? "是" : "否",
        "DeformedProcessCompleted" => item.DeformedProcessCompleted ? "是" : "否",
        // 状态 int→中文
        "MaterialPlanStatus" => item.MaterialPlanStatusText,
        "MainNoMaterialPlanStatus" => item.MainNoMaterialPlanStatusText,
        "InputStatus" => item.InputStatusText,
        "MainNoInputStatus" => item.MainNoInputStatusText,
        "FlowStatus" => item.FlowStatusText,
        "MainNoFlowStatus" => item.MainNoFlowStatusText,
        "WoWarehousingStatus" => item.WoWarehousingStatusText,
        "MainNoWarehousingStatus" => item.MainNoWarehousingStatusText,
        "OrderWarehousingStatus" => item.OrderWarehousingStatusText,
        "ScheduleStage" => item.ScheduleStageText,
        // G4~G10 状态字段
        "PiercingSubStatus" => item.PiercingSubStatusText,
        "PiercingReturnStatus" => item.PiercingReturnStatusText,
        "SemiOrderStatus" => item.SemiOrderStatusText,
        "SemiInStatus" => item.SemiInStatusText,
        "FinishOrderStatus" => item.FinishOrderStatusText,
        "FinishInStatus" => item.FinishInStatusText,
        "InventoryOutStatus" => item.InventoryOutStatusText,
        "ReworkPlanInputStatus" => item.ReworkPlanInputStatusText,
        "InProcessReworkInputStatus" => item.InProcessReworkInputStatusText,
        "InMainInputStatus" => item.InMainInputStatusText,
        _ => GetRawPropertyValue(item, key)
    };

    private static object GetRawPropertyValue(WorkOrderExecutionSummaryDto item, string key) => (key switch
    {
        "WorkOrderNo" => item.WorkOrderNo ?? "",
        "Salesman" => item.Salesman ?? "",
        "CustomerName" => item.CustomerName ?? "",
        "SignDate" => item.SignDate,
        "DeliveryDate" => item.DeliveryDate,
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
        "MaterialPlanCoveredCount" => item.MaterialPlanCoveredCount,
        "MaterialPlanProportion" => item.MaterialPlanProportion ?? "",
        "TotalPlanWeight" => item.TotalPlanWeight,
        "TotalAvailableWeight" => item.TotalAvailableWeight,
        "TotalMissingWeight" => item.TotalMissingWeight,
        "ActualInputWeight" => item.ActualInputWeight,
        "ActualMainNoInputStatus" => item.ActualMainNoInputStatusText ?? "",
        "PlanInputConsistency" => item.PlanInputConsistencyText ?? "",
        "ReworkInputEndDate" => item.ReworkInputEndDate,
        "ReworkBatchCount" => item.ReworkBatchCount,
        "ReworkInputQuantity" => item.ReworkInputQuantity,
        "ReworkInputWeight" => item.ReworkInputWeight,
        "ReworkTheoreticalOutputQty" => item.ReworkTheoreticalOutputQty,
        "ReworkTheoreticalOutputWeight" => item.ReworkTheoreticalOutputWeight,
        "FlowOutputRatio" => item.FlowOutputRatio,
        "MainNoFlowOutputRatio" => item.MainNoFlowOutputRatio,
        "FlowTotalBatchCount" => item.FlowTotalBatchCount,
        "FlowIncompleteBatchCount" => item.FlowIncompleteBatchCount,
        "FlowMaxRemainingWorkDays" => item.FlowMaxRemainingWorkDays,
        "InputStartDate" => item.InputStartDate,
        "InputEndDate" => item.InputEndDate,
        "TotalBatchCount" => item.TotalBatchCount,
        "InputQuantity" => item.InputQuantity,
        "InputWeight" => item.InputWeight,
        "TheoreticalOutputQty" => item.TheoreticalOutputQty,
        "TheoreticalOutputWeight" => item.TheoreticalOutputWeight,
        "InputOutputRatio" => item.InputOutputRatio,
        "MainNoInputRatio" => item.MainNoInputOutputRatio,
        "ValidBatchCount" => item.ValidBatchCount,
        "ValidInputQuantity" => item.ValidInputQuantity,
        "ValidInputWeight" => item.ValidInputWeight,
        "ValidOutputQty" => item.ValidOutputQty,
        "ValidOutputWeight" => item.ValidOutputWeight,
        "DefectiveRawQty" => item.DefectiveRawQty,
        "DefectiveRawWeight" => item.DefectiveRawWeight,
        "DefectiveOutputQty" => item.DefectiveOutputQty,
        "DefectiveOutputWeight" => item.DefectiveOutputWeight,
        "DefectiveRatio" => item.DefectiveRatio,
        "InspectionStartDate" => item.InspectionStartDate,
        "InspectionEndDate" => item.InspectionEndDate,
        "InspectionDefectQty" => item.InspectionDefectQty,
        "InspectionDefectWeight" => item.InspectionDefectWeight,
        "InspectionDefectRatio" => item.InspectionDefectRatio,
        "GeneralDefectWeight" => item.GeneralDefectWeight,
        "GeneralDefectRatio" => item.GeneralDefectRatio,
        "SeriousDefectWeight" => item.SeriousDefectWeight,
        "SeriousDefectRatio" => item.SeriousDefectRatio,
        "ScrapWeight" => item.ScrapWeight,
        "ScrapRatio" => item.ScrapRatio,
        "WarehousingStartDate" => item.WarehousingStartDate,
        "WarehousingEndDate" => item.WarehousingEndDate,
        "WarehousingTotalQty" => item.WarehousingTotalQty,
        "WarehousingTotalWeight" => item.WarehousingTotalWeight,
        "TotalRemainingWorkDays" => item.TotalRemainingWorkDays,
        "CapacityWorkDays" => item.CapacityWorkDays,
        "UrgencyLevel" => item.UrgencyLevel ?? "",
        "EstimatedProcessCompletionDate" => item.EstimatedProcessCompletionDate,
        "DaysDiffFromDelivery" => item.DaysDiffFromDelivery,
        "RawMaterialLockRemark" => item.RawMaterialLockRemark ?? "",
        "AdjustmentRemark" => item.AdjustmentRemark ?? "",
        "PendingSectionRoughTube" => item.PendingSectionRoughTube,
        "PendingSectionWarehouseFix" => item.PendingSectionWarehouseFix,
        "PendingSection60Roll" => item.PendingSection60Roll,
        "PendingSection50Roll" => item.PendingSection50Roll,
        "PendingSection30Roll" => item.PendingSection30Roll,
        "PendingSection20Roll" => item.PendingSection20Roll,
        "PendingSectionThreeRoll" => item.PendingSectionThreeRoll,
        "PendingSectionDrawBench" => item.PendingSectionDrawBench,
        "ProductionAttentionProcess" => item.ProductionAttentionProcess ?? "",
        "ProductionFlowProperty" => item.ProductionFlowProperty ?? "",
        "MaxBatchRemainingWorkDays" => item.MaxBatchRemainingWorkDays,
        "MainNoAttentionProcess" => item.MainNoAttentionProcess ?? "",
        "MainNoFlowRatio" => item.MainNoFlowOutputRatio,
        // G4~G10: 7 种用料计划执行状况
        "PiercingPlanWeight" => item.PiercingPlanWeight,
        "PiercingSubOutWeight" => item.PiercingSubOutWeight,
        "PiercingSubStatus" => item.PiercingSubStatus,
        "PiercingSubInWeight" => item.PiercingSubInWeight,
        "PiercingSubPendingWeight" => item.PiercingSubPendingWeight,
        "PiercingReturnStatus" => item.PiercingReturnStatus,
        "SemiPlanWeight" => item.SemiPlanWeight,
        "SemiOrderWeight" => item.SemiOrderWeight,
        "SemiOrderStatus" => item.SemiOrderStatus,
        "SemiInWeight" => item.SemiInWeight,
        "SemiPendingWeight" => item.SemiPendingWeight,
        "SemiInStatus" => item.SemiInStatus,
        "FinishPlanWeight" => item.FinishPlanWeight,
        "FinishOrderWeight" => item.FinishOrderWeight,
        "FinishOrderStatus" => item.FinishOrderStatus,
        "FinishInWeight" => item.FinishInWeight,
        "FinishPendingWeight" => item.FinishPendingWeight,
        "FinishInStatus" => item.FinishInStatus,
        "InventoryPlanWeight" => item.InventoryPlanWeight,
        "InventoryOutWeight" => item.InventoryOutWeight,
        "InventoryOutStatus" => item.InventoryOutStatus,
        "ReworkPlanWeight" => item.ReworkPlanWeight,
        "ReworkPlanInputWeight" => item.ReworkPlanInputWeight,
        "ReworkPlanInputStatus" => item.ReworkPlanInputStatus,
        "InProcessReworkPlanWeight" => item.InProcessReworkPlanWeight,
        "InProcessReworkInputWeight" => item.InProcessReworkInputWeight,
        "InProcessReworkInputStatus" => item.InProcessReworkInputStatus,
        "InMainPlanWeight" => item.InMainPlanWeight,
        "InMainInputWeight" => item.InMainInputWeight,
        "InMainInputStatus" => item.InMainInputStatus,
        _ => ""
    })!;

private async Task HandleRefreshAll()
{
    _isRefreshing = true;
    try
    {
        var result = await WorkOrderExecutionService.RefreshAllAsync();
        if (result?.Success == true)
        {
            lastRefreshTime = DateTime.Now;
            if (table != null) await table.ReloadServerData();
        }
        else
        {
            Snackbar.Add(result?.Message ?? "刷新失败", Severity.Error);
        }
    }
    catch (Exception ex)
    {
        Snackbar.Add($"刷新失败: {ex.Message}", Severity.Error);
    }
    finally
    {
        _isRefreshing = false;
    }
}
}
