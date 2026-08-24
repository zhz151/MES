using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Core.Enums;
using MES.Core.Constants;
using MES.Core.Helpers;
using MES.Blazor.Services;
using MES.Core.Models;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.Scheduling;

public partial class BatchPlans
{
    private MudTable<BatchPlanDto>? table;
    private List<BatchPlanDto> _filteredItems = new();
    private int _totalCount;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;
    private bool _isPlanning;
    private HashSet<BatchPlanDto> _selectedItems = new();
    private List<BatchPlanDto> _filteredAllItems = new();

    private void SelectAllItems(bool selected)
    {
        if (selected)
            _selectedItems = new HashSet<BatchPlanDto>(_filteredAllItems);
        else
            _selectedItems.Clear();
    }

    private void ToggleSelection(BatchPlanDto item, bool selected)
    {
        if (selected)
            _selectedItems.Add(item);
        else
            _selectedItems.Remove(item);
    }

    // 排序状态
    private string sortColumn = "BatchNo";
    private bool sortDescending = true;

    // ========== 工段筛选 ==========
    private string? _selectedSection;
    // 工段 Tab = "全部" + 共享工段列表（BatchPlanSectionTabs，与汇总表 GetSummaryAsync 归桶口径一致）
    private static readonly string[] _sectionTabs =
        new[] { "全部" }.Concat(BatchPlanSectionTabs.All).ToArray();

    // ========== Tab 汇总数据 ==========
    private int _tabBatchCount;
    private decimal _tabTotalWeight;
    private int _tabFlowBatchCount;
    private decimal _tabFlowBatchWeight;
    private int _tabKeyBatchCount;
    private decimal _tabKeyBatchWeight;

    // ========== 跨工段汇总卡片（仿原锁计划，默认折叠） ==========
    private bool _showSummaryCard;
    private bool _isLoadingSummary;
    private List<BatchPlanSummaryRowDto> _summaryRows = new();

    // ========== 实时委外在产折叠卡片（按在产单位×工段二维表，懒加载） ==========
    private bool _showOutsourcePendingCard;
    private bool _isLoadingOutsourcePending;
    private BatchPlanOutsourcePendingDto _outsourcePendingData = new();

    // ========== 段落流转分析折叠查询（纯表，无可持续天数字段，懒加载） ==========
    private bool _showParagraphCard;
    private bool _isLoadingParagraph;
    private List<SectionParagraphFlowAnalysisDto> _paragraphRows = new();

    // ========== 工段流转分析折叠查询（纯表，无可持续天数字段，懒加载） ==========
    private bool _showFlowCard;
    private bool _isLoadingFlow;
    private List<SectionFlowAnalysisDto> _flowRows = new();

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // "空值"筛选哨兵（与排程计划页一致：GetFilterValue 对空值返回哨兵，筛选选项 Value=哨兵）
    private const string FilterNull = "__EXCEL_FILTER_NULL__";
    // "非空"筛选哨兵（number 列筛选选项：非空/空）
    private const string FilterNotNull = "__EXCEL_FILTER_NOT_NULL__";

    // ========== 列定义 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    // 全量数据缓存
    private List<BatchPlanDto> _allItems = new();

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "CurrentValidWeight", "MinLength", "MaxLength",
    };
    private int _lastSummedPage = -1;
    private int _lastSummedCount = -1;
    private int _lastSummedPageSize = -1;

    // ========== 字典下拉选项（配置表动态加载，失败兜底静态 KeyToChinese）==========
    private List<(string Value, string Text)> _flowTargetOptions =
        FlowTargetKeys.KeyToChinese.Select(kv => (kv.Key, kv.Value)).ToList();

    // ========== 批次计划等级/冷轧类型下拉选项 ==========
    /// <summary>薄表等级五档（存储 1~5，显示中文）</summary>
    private static readonly List<(int Value, string Text)> PlanFlowLevelSelectOptions = new()
    {
        (1, "急+"), (2, "急"), (3, "急-"), (4, "一般"), (5, "略"),
    };

    private async Task LoadDictOptionsAsync()
    {
        var flowTarget = await DictValueDefinitionService.GetEnabledValuesAsync(DictValueDefaults.FlowTargetKey);
        if (flowTarget.Success && flowTarget.Data is { Count: > 0 })
            _flowTargetOptions = flowTarget.Data.Select(t => (t.Value, t.DisplayName)).ToList();
    }

    /// <summary>
    /// 永久隐藏列：冷轧排程维度列（本层/下层/下下层/实时）+ 匹配结果 + 工单需求调整 + 批次基础多余字段
    /// （不在列显隐选择器显示，始终不显示；冷轧维度展示统一走 G11 判定结果 + G13 批次计划，2026-08-20 用户决策）
    /// </summary>
    private static readonly HashSet<string> _permanentlyHiddenColumnKeys = new()
    {
        // 冷轧排程(本层/下层/下下层/实时) 维度列
        "CurrentCR_ProcessType", "CurrentCR_BilletSpec", "CurrentCR_RollingSpec", "CurrentCR_IsFinished", "CurrentCR_DeformedSeqCompleted",
        "NextCR_ProcessType", "NextCR_BilletSpec", "NextCR_RollingSpec", "NextCR_IsFinished",
        "NextNextCR_ProcessType", "NextNextCR_BilletSpec", "NextNextCR_RollingSpec", "NextNextCR_IsFinished",
        "RealTimeCR_ProcessType", "RealTimeCR_BilletSpec", "RealTimeCR_RollingSpec", "RealTimeCR_IsFinished",
        // 冷轧排程(本层匹配)
        "CR_CompletionType",
        // 冷轧排程(下层匹配)
        "CR_RollType", "CR_SchedMachineNo",
        // 工单需求调整
        "IsUrging", "IsBatchDelivery", "IsPaused", "AdjustmentRemark",
        // 批次基础信息多余字段
        "MinLength", "MaxLength",
    };

    /// <summary>
    /// 获取可在列显隐选择器中切换的列（永久隐藏列不显示在列选择器）
    /// </summary>
    private List<ColumnDef> GetToggleableColumns() =>
        _allColumns.Where(c => !_permanentlyHiddenColumnKeys.Contains(c.Key)).ToList();

    private static List<ColumnDef> GetAllColumnDefs()
    {
        // G1: 批次基础信息（批次信息 + 关联工单合并，用户指定字段与顺序）
        var g1 = new List<ColumnDef>
        {
            new() { Key = "BatchNo",              Label = "生产编号",     SortKey = "BatchNo",              FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次基础信息" },
            new() { Key = "TagNo",                Label = "挂牌号",       SortKey = "TagNo",                FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次基础信息" },
            new() { Key = "PlantGrade",            Label = "原料钢号",     SortKey = "PlantGrade",            FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次基础信息" },
            new() { Key = "CurrentValidWeight",    Label = "重量(kg)",     SortKey = "CurrentValidWeight",    FilterType = "number", Width = "80",  GroupKey = 1, GroupName = "批次基础信息" },
            // 新增列（生产类型/制造物品/制造状态）置于"关联工单号"前，默认隐藏
            new() { Key = "ProductionType",        Label = "生产类型",     SortKey = "ProductionType",        FilterType = "enum", Width = "110", EnumOptions = DisplayHelper.GetEnumFilterOptions<ProductionType>(), GroupKey = 1, GroupName = "批次基础信息", DisplayConverter = v => DisplayHelper.GetProductionTypeText(v?.ToString()), Visible = false },
            new() { Key = "ManufacturingItem",     Label = "制造物品",     SortKey = "ManufacturingItem",     FilterType = "enum", Width = "110", EnumOptions = DisplayHelper.GetEnumFilterOptions<MaterialType>(), GroupKey = 1, GroupName = "批次基础信息", DisplayConverter = v => DisplayHelper.GetMaterialTypeText(v?.ToString()), Visible = false },
            new() { Key = "ManufacturingStatus",   Label = "制造状态",     SortKey = "ManufacturingStatus",   FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>(), GroupKey = 1, GroupName = "批次基础信息", DisplayConverter = v => DisplayHelper.GetDeliveryStateText(v?.ToString()), Visible = false },
            new() { Key = "WorkOrderNo",           Label = "关联工单号",   SortKey = "WorkOrderNo",           FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次基础信息" },
            // 新增列（订单号/主号/业务员/最终用户）置于"关联工单号"后，默认隐藏
            new() { Key = "SalesOrderNo",          Label = "订单号",       SortKey = "SalesOrderNo",          FilterType = "string", Width = "130", GroupKey = 1, GroupName = "批次基础信息", Visible = false },
            new() { Key = "ProductionMainNo",      Label = "主号",         SortKey = "ProductionMainNo",      FilterType = "string", Width = "100", GroupKey = 1, GroupName = "批次基础信息", Visible = false },
            new() { Key = "Salesman",              Label = "业务员",       SortKey = "Salesman",              FilterType = "string", Width = "100", GroupKey = 1, GroupName = "批次基础信息", Visible = false },
            new() { Key = "EndCustomer",           Label = "最终用户",     SortKey = "EndCustomer",           FilterType = "string", Width = "130", GroupKey = 1, GroupName = "批次基础信息", Visible = false },
            new() { Key = "DeliveryState",         Label = "交货状态",     SortKey = "DeliveryState",         FilterType = "enum", Width = "120", EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>(), GroupKey = 1, GroupName = "批次基础信息", DisplayConverter = v => v is DeliveryState dv ? DisplayHelper.GetDeliveryStateText(dv) : DisplayHelper.GetDeliveryStateText(v as string) },
            new() { Key = "DeliveryDate",          Label = "交货日期",     SortKey = "DeliveryDate",          FilterType = "number", Width = "110", GroupKey = 1, GroupName = "批次基础信息" },
            new() { Key = "Specification",         Label = "成品规格",     SortKey = "Specification",         FilterType = "string", Width = "120", GroupKey = 1, GroupName = "批次基础信息" },
            new() { Key = "LengthStatus",          Label = "长度状态",     SortKey = "LengthStatus",          FilterType = "enum", Width = "100", EnumOptions = DisplayHelper.GetEnumFilterOptions<LengthStatus>(), GroupKey = 1, GroupName = "批次基础信息", DisplayConverter = v => v is LengthStatus ls ? DisplayHelper.GetLengthStatusText(ls) : DisplayHelper.GetLengthStatusText(v as string) },
        };

        // G1 多余字段（用户决策：最小长度/最大长度 不显示，保留定义供搜索/筛选）
        var g1Hidden = new List<ColumnDef>
        {
            new() { Key = "MinLength",             Label = "最小长度",   SortKey = "MinLength",             FilterType = "number", Width = "80",  GroupKey = 1, GroupName = "批次基础信息" },
            new() { Key = "MaxLength",             Label = "最大长度",   SortKey = "MaxLength",             FilterType = "number", Width = "80",  GroupKey = 1, GroupName = "批次基础信息" },
        };

        // G3: 状态跟踪
        var g3 = new List<ColumnDef>
        {
            new() { Key = "CurrentExecDate",        Label = "执行截止日",   SortKey = "CurrentExecDate",        FilterType = "number", Width = "110", GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "PendingProcess",         Label = "执行工序",     FilterType = "string", Width = "130", GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "PendingSectionName",     Label = "待在产执行工段", FilterType = "string", Width = "120", GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "PendingSpec",            Label = "执行规格",      FilterType = "string", Width = "120", GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "PendingUnit",            Label = "在产单位",      FilterType = "string", Width = "120", GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "PendingEquipment",       Label = "在产设备",      FilterType = "string", Width = "120", GroupKey = 3, GroupName = "状态跟踪" },
            new() { Key = "ExecutionSequence",      Label = "现执行序",      FilterType = "number", Width = "70", GroupKey = 3, GroupName = "状态跟踪" },
        };

        // G5: 冷轧排程（本层/下层/下下层维度明细可列显隐切换；匹配结果 CR_* 永久隐藏）
        var g5 = new List<ColumnDef>
        {
            new() { Key = "CurrentCR_ProcessType",  Label = "本层冷轧工序", FilterType = "string",  Width = "110", GroupKey = 5, GroupName = "冷轧排程(本层)" },
            new() { Key = "CurrentCR_BilletSpec",   Label = "本层来料规格", FilterType = "string",  Width = "110", GroupKey = 5, GroupName = "冷轧排程(本层)" },
            new() { Key = "CurrentCR_RollingSpec",  Label = "本层在轧规格", FilterType = "string",  Width = "110", GroupKey = 5, GroupName = "冷轧排程(本层)" },
            new() { Key = "CurrentCR_IsFinished",   Label = "本层末道",    FilterType = "boolean", Width = "80",  GroupKey = 5, GroupName = "冷轧排程(本层)" },
            new() { Key = "CurrentCR_DeformedSeqCompleted", Label = "变形序完成", FilterType = "boolean", Width = "90", BoolTrueLabel = "完成", BoolFalseLabel = "否", GroupKey = 5, GroupName = "冷轧排程(本层)" },
            new() { Key = "NextCR_ProcessType",     Label = "下层冷轧工序", FilterType = "string",  Width = "110", GroupKey = 6, GroupName = "冷轧排程(下层)" },
            new() { Key = "NextCR_BilletSpec",      Label = "下层来料规格", FilterType = "string",  Width = "110", GroupKey = 6, GroupName = "冷轧排程(下层)" },
            new() { Key = "NextCR_RollingSpec",     Label = "下层在轧规格", FilterType = "string",  Width = "110", GroupKey = 6, GroupName = "冷轧排程(下层)" },
            new() { Key = "NextCR_IsFinished",      Label = "下层末道",    FilterType = "boolean", Width = "80",  GroupKey = 6, GroupName = "冷轧排程(下层)" },
            new() { Key = "NextNextCR_ProcessType", Label = "下下层冷轧工序", FilterType = "string", Width = "110", GroupKey = 9, GroupName = "冷轧排程(下下层)" },
            new() { Key = "NextNextCR_BilletSpec",  Label = "下下层来料规格", FilterType = "string", Width = "110", GroupKey = 9, GroupName = "冷轧排程(下下层)" },
            new() { Key = "NextNextCR_RollingSpec", Label = "下下层在轧规格", FilterType = "string", Width = "110", GroupKey = 9, GroupName = "冷轧排程(下下层)" },
            new() { Key = "NextNextCR_IsFinished",  Label = "下下层末道",    FilterType = "boolean", Width = "80",  GroupKey = 9, GroupName = "冷轧排程(下下层)" },
            new() { Key = "RealTimeCR_ProcessType", Label = "冷轧工序",     FilterType = "string",  Width = "110", GroupKey = 12, GroupName = "冷轧排程(实时)" },
            new() { Key = "RealTimeCR_BilletSpec",  Label = "来料规格",     FilterType = "string",  Width = "110", GroupKey = 12, GroupName = "冷轧排程(实时)" },
            new() { Key = "RealTimeCR_RollingSpec", Label = "在轧规格",     FilterType = "string",  Width = "110", GroupKey = 12, GroupName = "冷轧排程(实时)" },
            new() { Key = "RealTimeCR_IsFinished",  Label = "末道",         FilterType = "boolean", Width = "80",  GroupKey = 12, GroupName = "冷轧排程(实时)" },
            new() { Key = "CR_CompletionType",      Label = "在轧要求",    FilterType = "enum",   Width = "90",  GroupKey = 7, GroupName = "冷轧排程(本层匹配)", EnumOptions = DisplayHelper.GetCompletionTypeOptions(), DisplayConverter = v => DisplayHelper.GetCompletionTypeText(v as string) },
            new() { Key = "CR_RollType",            Label = "待轧要求",    FilterType = "enum",   Width = "90",  GroupKey = 8, GroupName = "冷轧排程(下层匹配)", EnumOptions = DisplayHelper.GetRollTypeOptions(), DisplayConverter = v => DisplayHelper.GetRollTypeText(v as string) },
            new() { Key = "CR_SchedMachineNo",      Label = "待轧设备号",   FilterType = "string", Width = "100", GroupKey = 8, GroupName = "冷轧排程(下层匹配)" },
        };

        // G4: 工单计划
        var g4 = new List<ColumnDef>
        {
            new() { Key = "UrgencyLevel",               Label = "工单紧急性",    SortKey = "UrgencyLevel",               FilterType = "string", Width = "110", GroupKey = 4, GroupName = "工单计划" },
            new() { Key = "ScheduleStage",               Label = "计划状态",     SortKey = "ScheduleStage",               FilterType = "enum", Width = "110", EnumOptions = new List<EnumOption> { new("-1","无此工单"), new("4","非工单") }.Concat(DisplayHelper.GetPlanScheduleStageOptions()).ToList(), GroupKey = 4, GroupName = "工单计划", DisplayConverter = v => v is int s ? s switch { -1 => "无此工单", 4 => "非工单", _ => IntStatusDisplayHelper.GetPlanScheduleStageText(s) } : null },
            new() { Key = "MainNoAttentionProcess",             Label = "主号关注工序",   SortKey = "MainNoAttentionProcess",          FilterType = "string", Width = "130", GroupKey = 4, GroupName = "工单计划" },
            new() { Key = "AttentionProcessSectionSequence",    Label = "相应工段序",   SortKey = "AttentionProcessSectionSequence", FilterType = "number", Width = "100", GroupKey = 4, GroupName = "工单计划" },
            new() { Key = "ProductionFlowProperty",             Label = "生产流转性",    SortKey = "ProductionFlowProperty",           FilterType = "string", Width = "100", GroupKey = 4, GroupName = "工单计划" },
            new() { Key = "IsKeyBatch",                  Label = "重点生产批次",  FilterType = "boolean", Width = "120", GroupKey = 4, GroupName = "工单计划" },
        };

        // G10：工单需求调整（在工单管理页面编辑，批次页展示）
        var g10 = new List<ColumnDef>
        {
            new() { Key = "IsUrging",              Label = "催单",         FilterType = "boolean", Width = "80",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 10, GroupName = "工单需求调整" },
            new() { Key = "IsBatchDelivery",       Label = "分批交货",     FilterType = "boolean", Width = "90",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 10, GroupName = "工单需求调整" },
            new() { Key = "IsPaused",              Label = "工单暂停",     FilterType = "boolean", Width = "90",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 10, GroupName = "工单需求调整" },
            new() { Key = "AdjustmentRemark",      Label = "调整备注",     FilterType = "string",  Width = "130", GroupKey = 10, GroupName = "工单需求调整" },
        };

        // G11：关联冷轧排程
        var g11 = new List<ColumnDef>
        {
            new() { Key = "IsFlow",                Label = "流转",        FilterType = "boolean", Width = "60",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 11, GroupName = "关联冷轧排程" },
            new() { Key = "FlowLevel",             Label = "等级",        FilterType = "enum",    Width = "60",  EnumOptions = DisplayHelper.GetScheduleTierOptions(), GroupKey = 11, GroupName = "关联冷轧排程" },
            new() { Key = "FlowTarget",            Label = "流转目标",    FilterType = "string",  Width = "90",  GroupKey = 11, GroupName = "关联冷轧排程" },
            new() { Key = "FlowCRType",            Label = "冷轧类型",    FilterType = "string",  Width = "100", GroupKey = 11, GroupName = "关联冷轧排程" },
            new() { Key = "OuterDiameterSpan",     Label = "外径跨度",    FilterType = "string",  Width = "90",  GroupKey = 11, GroupName = "关联冷轧排程" },
            new() { Key = "FlowExecSpec",          Label = "执行规格",    FilterType = "string",  Width = "120", GroupKey = 11, GroupName = "关联冷轧排程" },
            new() { Key = "TargetSequence",        Label = "目标序",      FilterType = "number", Width = "70",  GroupKey = 11, GroupName = "关联冷轧排程" },
        };

        // G12：执行反馈
        var g12 = new List<ColumnDef>
        {
            new() { Key = "OriginalDiff",        Label = "原工量差",   FilterType = "number", Width = "80", GroupKey = 12, GroupName = "执行反馈" },
            new() { Key = "CurrentDiff",         Label = "现工量差",   FilterType = "number", Width = "80", GroupKey = 12, GroupName = "执行反馈" },
            new() { Key = "IsExecuted",          Label = "是否执行",   FilterType = "boolean", Width = "80",  GroupKey = 12, GroupName = "执行反馈" },
            new() { Key = "IsCompliant",         Label = "达标",       FilterType = "enum", Width = "70",  EnumOptions = new() { new("达标","达标"), new("未达标","未达标") }, GroupKey = 12, GroupName = "执行反馈", DisplayConverter = v => v as string },
        };

        // G13：批次计划（持久化，内联编辑）
        var g13 = new List<ColumnDef>
        {
            new() { Key = "PlanIsFlow",              Label = "流转",       FilterType = "boolean", Width = "60",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanFlowLevel",           Label = "等级",       FilterType = "enum",    Width = "60",  EnumOptions = DisplayHelper.GetPlanFlowLevelOptions(), GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanFlowCRType",          Label = "目标工序",   FilterType = "string",  Width = "100", GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanFlowTarget",          Label = "流转位",     FilterType = "string",  Width = "90",  GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanOuterDiameterSpan",   Label = "外径跨度",   FilterType = "string",  Width = "90",  GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanFlowExecSpec",        Label = "执行规格",   FilterType = "string",  Width = "120", GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanExecutionSequence",   Label = "执行序",     FilterType = "number", Width = "70", GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanTargetSequence",      Label = "目标序",     FilterType = "number", Width = "70", GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanIsPaused",            Label = "暂停",       FilterType = "boolean", Width = "70",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "IsGrabOrder",             Label = "抢单",       FilterType = "boolean", Width = "70",  BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 13, GroupName = "批次计划" },
            new() { Key = "PlanRemark",              Label = "计划备注",   FilterType = "string",  Width = "130", GroupKey = 13, GroupName = "批次计划" },
        };

        var all = new List<ColumnDef>();
        all.AddRange(g5);
        all.AddRange(g10);
        all.AddRange(g4);
        all.AddRange(g11);
        all.AddRange(g1Hidden);
        all.AddRange(g1);   // 批次基础信息
        all.AddRange(g13);  // 批次计划（状态跟踪前）
        all.AddRange(g3);   // 状态跟踪
        all.AddRange(g12);  // 执行反馈

        // 用户决策默认隐藏（列显隐选择器仍可切换打开）：工单计划(工单紧急性/计划状态/生产流转性 默认显示)、关联冷轧排程、批次计划(执行序/目标序)、状态跟踪(现执行序)、执行反馈(原工量差)
        foreach (var c in all)
        {
            if ((c.GroupName is "工单计划" && c.Key is not ("UrgencyLevel" or "ScheduleStage" or "ProductionFlowProperty")) ||
                (c.GroupName is "关联冷轧排程") ||
                (c.Key is "PlanExecutionSequence" or "PlanTargetSequence" or "ExecutionSequence" or "OriginalDiff"))
                c.Visible = false;
        }
        return all;
    }

    // ========== 分页汇总 ==========

    /// <summary>
    /// 渲染期惰性重算当前页汇总。⚠️ MudBlazor 6.19.1 没有 CurrentPageChanged 事件（旧绑定被
    /// CaptureUnmatchedValues 静默吞掉、永不触发）；翻页只触发 MudTable 自身 StateHasChanged，
    /// 不触发父组件 OnAfterRenderAsync。故在 FooterContent 渲染时按「页码/每页行数/数据量」签名惰性重算，
    /// 保证页脚汇总与实际显示行一致。
    /// </summary>
    private void EnsurePageSumsComputed()
    {
        var page = table?.CurrentPage ?? 0;
        var rowsPerPage = table?.RowsPerPage ?? _pageSize;
        if (rowsPerPage <= 0) rowsPerPage = _pageSize;
        var count = _filteredItems.Count;
        if (page == _lastSummedPage && count == _lastSummedCount && rowsPerPage == _lastSummedPageSize)
            return;
        _lastSummedPage = page;
        _lastSummedCount = count;
        _lastSummedPageSize = rowsPerPage;
        ComputePageSums();
    }

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
        _columnFilters.Clear();
        await LoadDataAsync();
    }

    // ========== 数据加载（全量模式） ==========

    private async Task LoadDataAsync()
    {
        try
        {
            _allItems = await BatchPlanSvc.GetAllAsync(_selectedSection);
            UpdateTabSummary();
            BuildFilterOptionsFromData();
            if (table != null) { ApplyFiltersAndSort(); StateHasChanged(); }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _allItems = new();
            ClearTabSummaries();
        }
    }

    private void UpdateTabSummary()
    {
        _tabBatchCount = _allItems.Count;
        _tabTotalWeight = _allItems.Sum(x => x.CurrentValidWeight ?? 0m);
        var flowBatches = _allItems.Where(x => x.PlanIsFlow).ToList();
        _tabFlowBatchCount = flowBatches.Count;
        _tabFlowBatchWeight = flowBatches.Sum(x => x.CurrentValidWeight ?? 0m);
        // 重点批次 = 批次计划等级 == 急+（PlanFlowLevel 1），与 G13 等级列口径一致
        var keyBatches = _allItems.Where(x => x.PlanFlowLevel == 1).ToList();
        _tabKeyBatchCount = keyBatches.Count;
        _tabKeyBatchWeight = keyBatches.Sum(x => x.CurrentValidWeight ?? 0m);
    }

    // ========== 跨工段汇总卡片（仿原锁计划，默认折叠，工具栏「近日生产量数据」按钮切换显隐） ==========

    private void ToggleSummaryCard() => _showSummaryCard = !_showSummaryCard;

    private async Task LoadSummaryAsync()
    {
        try
        {
            _isLoadingSummary = true;
            StateHasChanged();
            _summaryRows = await BatchPlanSvc.GetSummaryAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"汇总加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoadingSummary = false;
            StateHasChanged();
        }
    }

    // ========== 实时委外在产折叠卡片（仿汇总卡片，懒加载） ==========

    private async Task ToggleOutsourcePendingCard()
    {
        _showOutsourcePendingCard = !_showOutsourcePendingCard;
        if (_showOutsourcePendingCard && _outsourcePendingData.Rows.Count == 0)
            await LoadOutsourcePendingAsync();
    }

    private async Task LoadOutsourcePendingAsync()
    {
        try
        {
            _isLoadingOutsourcePending = true;
            StateHasChanged();
            _outsourcePendingData = await BatchPlanSvc.GetOutsourcePendingAsync() ?? new BatchPlanOutsourcePendingDto();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"实时委外在产加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoadingOutsourcePending = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 委外在产单元格三值格式化（前端 /1000 显示 t，保留 1 位，0 值留空）：
    /// 格式「总量/[流转]/[*特急]」= 总量、其中批次计划实时流转（IsFlow=是）重量、其中批次计划等级急+（特急批）重量（* 标红）。
    /// </summary>
    private static MarkupString FormatOutsourceCell(OutsourcePendingCellDto? cell)
    {
        if (cell == null || cell.Total <= 0) return new MarkupString("");
        var sb = new System.Text.StringBuilder((cell.Total / 1000m).ToString("F1"));
        if (cell.Flow > 0) sb.Append($"/[{(cell.Flow / 1000m).ToString("F1")}]");
        if (cell.Key > 0) sb.Append($"/[<span style=\"color:#d32f2f;font-weight:600;\">*{(cell.Key / 1000m).ToString("F1")}</span>]");
        return new MarkupString(sb.ToString());
    }

    // ========== 段落/工段流转分析折叠查询（仿汇总卡片，懒加载） ==========

    private async Task ToggleParagraphCard()
    {
        _showParagraphCard = !_showParagraphCard;
        if (_showParagraphCard && _paragraphRows.Count == 0)
            await LoadParagraphAsync();
    }

    private async Task LoadParagraphAsync()
    {
        try
        {
            _isLoadingParagraph = true;
            StateHasChanged();
            var result = await SectionParagraphFlowAnalysisSvc.GetAnalysisAsync();
            _paragraphRows = result?.Success == true && result.Data != null
                ? result.Data
                : new List<SectionParagraphFlowAnalysisDto>();
            if (result?.Success != true)
                Snackbar.Add(result?.Message ?? "段落流转分析加载失败", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"段落流转分析加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoadingParagraph = false;
            StateHasChanged();
        }
    }

    private async Task ToggleFlowCard()
    {
        _showFlowCard = !_showFlowCard;
        if (_showFlowCard && _flowRows.Count == 0)
            await LoadFlowAsync();
    }

    private async Task LoadFlowAsync()
    {
        try
        {
            _isLoadingFlow = true;
            StateHasChanged();
            var result = await SectionFlowAnalysisSvc.GetAnalysisAsync();
            _flowRows = result?.Success == true && result.Data != null
                ? result.Data
                : new List<SectionFlowAnalysisDto>();
            if (result?.Success != true)
                Snackbar.Add(result?.Message ?? "工段流转分析加载失败", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"工段流转分析加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoadingFlow = false;
            StateHasChanged();
        }
    }

    // 纯表渲染辅助（与流转分析独立页口径一致）
    private static string RenderInt(decimal? val) => val.HasValue ? Math.Round(val.Value, 0).ToString() : "-";

    // 近日生产量数据重量(t) 格式化：kg /1000 显示 t（保留 1 位），0 值留空（防视觉污染，与生产记录页口径一致）
    private static string FormatT(decimal kg)
        => kg > 0 ? (kg / 1000m).ToString("F1") : string.Empty;

    private static Color GetStatusColor(string? status) => status switch
    {
        "偏少" => Color.Error,
        "过多" => Color.Warning,
        "正常" => Color.Success,
        _ => Color.Default
    };

    private static Color GetPlanFlowJudgmentColor(string? judgment) => judgment == "加速" ? Color.Warning : Color.Default;

    // ========== 筛选上下文构建（内存数据驱动） ==========

    private void BuildFilterOptionsFromData()
    {
        _filterContextOptions.Clear();

        foreach (var col in _allColumns.Where(c => c.FilterType != null))
        {
            if (col.FilterType == "enum" && col.EnumOptions != null)
            {
                var enumOptions = col.EnumOptions.Select(e => new ExcelFilterOption
                {
                    Value = e.Value,
                    Display = e.Display,
                    Count = 0
                }).ToList();
                // 达标列追加"空值"筛选（批次计划流转=否 → null）
                if (col.Key == "IsCompliant")
                    enumOptions.Insert(0, new ExcelFilterOption { Value = FilterNull, Display = "空值", Count = 0 });
                _filterContextOptions[col.Key] = enumOptions;
            }
            else if (col.FilterType == "boolean")
            {
                var boolValues = _allItems.Select(x => GetFilterValue(x, col.Key)).Where(v => v != null).ToList();
                _filterContextOptions[col.Key] = new List<ExcelFilterOption>
                {
                    new() { Value = "True", Display = col.BoolTrueLabel ?? "是", Count = boolValues.Count(v => v == "True") },
                    new() { Value = "False", Display = col.BoolFalseLabel ?? "否", Count = boolValues.Count(v => v == "False") }
                };
            }
            else if (col.FilterType == "number")
            {
                // 实际数值选项（DISTINCT 数值升序）+ 非空/空：供按具体数值筛选（工量差/执行序等），避免只有非空/空不实用
                var numericOptions = _allItems
                    .Select(x => GetFilterValue(x, col.Key))
                    .Where(v => v != null)
                    .GroupBy(v => v!)
                    .Select(g => new ExcelFilterOption { Value = g.Key, Display = g.Key, Count = g.Count() })
                    .OrderBy(o => decimal.TryParse(o.Value, out var d) ? d : decimal.MaxValue)
                    .ThenBy(o => o.Value, StringComparer.Ordinal)
                    .ToList();
                var options = new List<ExcelFilterOption>
                {
                    new() { Value = FilterNotNull, Display = "非空", Count = _allItems.Count(x => GetFilterValue(x, col.Key) != null) },
                    new() { Value = FilterNull,    Display = "空",   Count = _allItems.Count(x => GetFilterValue(x, col.Key) == null) },
                };
                options.AddRange(numericOptions);
                _filterContextOptions[col.Key] = options;
            }
            else if (col.FilterType == "string")
            {
                var distinct = _allItems
                    .Select(x => GetFilterValue(x, col.Key))
                    .Where(v => v != null && v != FilterNull)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(v => v)
                    .Select(v => new ExcelFilterOption
                    {
                        Value = v!,
                        Display = (col.Key switch
                        {
                            "SectionName" or "CurrentSectionName" or "NextSectionName" or "PendingSectionName" => SectionDisplayHelper.GetSectionNameText(v!),
                            "ProcessName" or "ProcessGroupName" or "CurrentGroupName" or "NextProcess" or "PendingProcess" or "MainNoAttentionProcess" or "PlanProductionAttentionProcess"
                                or "CurrentCR_ProcessType" or "NextCR_ProcessType" or "NextNextCR_ProcessType" or "FlowCRType" or "PlanFlowCRType" => ProcessDisplayHelper.GetProcessNameText(v!),
                            "UrgencyLevel" => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey,v!),
                            "ProductionFlowProperty" or "PlanProductionFlowProperty" => DictValueDisplayHelper.GetText(DictValueDefaults.ProductionFlowKey,v!),
                            "FlowTarget" or "PlanFlowTarget" => DictValueDisplayHelper.GetText(DictValueDefaults.FlowTargetKey,v!),
                            _ => v!
                        }) ?? v!,
                        Count = 0
                    })
                    .ToList();

                // 工单紧急性：追加"空值"筛选选项（与排程计划 Plan 字段空值模式一致）
                if (col.Key == "UrgencyLevel")
                {
                    distinct.Insert(0, new ExcelFilterOption
                    {
                        Value = FilterNull,
                        Display = "空值",
                        Count = 0
                    });
                }

                _filterContextOptions[col.Key] = distinct;
            }
        }
    }

    /// <summary>实时排程档位 → 薄表等级（V5.28 五档映射：急+→急+/急→急/急-→急-/顺·带→一般/略→略）</summary>
    private static int PlanLevelFromScheduleTier(int tier) => tier switch
    {
        1 => 1, // 急+
        2 => 2, // 急
        3 => 3, // 急-
        4 => 4, // 顺 → 一般
        5 => 4, // 带 → 一般
        _ => 5, // 略
    };

    private static string? GetFilterValue(BatchPlanDto item, string key) => key switch
    {
        "BatchNo" => item.BatchNo,
        "TagNo" => item.TagNo,
        "PlantGrade" => item.PlantGrade,
        "CurrentValidWeight" => item.CurrentValidWeight?.ToString(),
        "ProductionType" => item.ProductionType,
        "ManufacturingItem" => item.ManufacturingItem,
        "ManufacturingStatus" => item.ManufacturingStatus,
        "WorkOrderNo" => item.WorkOrderNo,
        "SalesOrderNo" => item.SalesOrderNo,
        "ProductionMainNo" => item.ProductionMainNo,
        "Salesman" => item.Salesman,
        "EndCustomer" => item.EndCustomer,
        "DeliveryDate" => item.DeliveryDate == default ? null : item.DeliveryDate.ToString("yyyy-MM-dd"),
        "DeliveryState" => item.DeliveryState.HasValue ? item.DeliveryState.Value.ToString() : null,
        "Specification" => item.Specification,
        "LengthStatus" => item.LengthStatus.HasValue ? item.LengthStatus.Value.ToString() : null,
        "MinLength" => item.MinLength?.ToString("G29"),
        "MaxLength" => item.MaxLength?.ToString("G29"),
        "CurrentSectionName" => item.CurrentSectionName,
        "CurrentExecDate" => item.CurrentExecDate?.ToString("yyyy-MM-dd"),
        "PendingProcess" => item.PendingProcess,
        "PendingSectionName" => item.PendingSectionName,
        "PendingSpec" => item.PendingSpec,
        "PendingUnit" => item.PendingUnit,
        "PendingEquipment" => item.PendingEquipment,
        "ExecutionSequence" => item.ExecutionSequence?.ToString(),
        "UrgencyLevel" => item.UrgencyLevel ?? FilterNull,
        "ScheduleStage" => item.ScheduleStage.ToString(),
        "ProductionFlowProperty" => item.ProductionFlowProperty,
        "MainNoAttentionProcess" => item.MainNoAttentionProcess,
        "AttentionProcessSectionSequence" => item.AttentionProcessSectionSequence?.ToString(),
        "AdjustmentRemark" => item.AdjustmentRemark,
        "FlowLevel" => item.ScheduleTierDisplay,
        "FlowTarget" => item.FlowTarget,
        "FlowCRType" => item.FlowCRType,
        "OuterDiameterSpan" => item.OuterDiameterSpan,
        "FlowExecSpec" => item.FlowExecSpec,
        "TargetSequence" => item.TargetSequence?.ToString(),
        "IsKeyBatch" => item.IsKeyBatch ? "True" : "False",
        "IsFlow" => item.IsFlow ? "True" : "False",
        "IsUrging" => item.IsUrging ? "True" : "False",
        "IsBatchDelivery" => item.IsBatchDelivery ? "True" : "False",
        "IsPaused" => item.IsPaused ? "True" : "False",
        "IsGrabOrder" => item.IsGrabOrder ? "True" : "False",
        "PlanRemark" => item.PlanRemark,
        "PlanIsPaused" => item.PlanIsPaused ? "True" : "False",
        "PlanIsFlow" => item.PlanIsFlow ? "True" : "False",
        "PlanFlowLevel" => item.PlanFlowLevelDisplay,
        "PlanFlowTarget" => item.PlanFlowTarget,
        "PlanFlowCRType" => item.PlanFlowCRType,
        "PlanOuterDiameterSpan" => item.PlanOuterDiameterSpan,
        "PlanFlowExecSpec" => item.PlanFlowExecSpec,
        "PlanExecutionSequence" => item.PlanExecutionSequence?.ToString(),
        "PlanTargetSequence" => item.PlanTargetSequence?.ToString(),
        "CurrentCR_ProcessType" => item.CurrentCR_ProcessType,
        "CurrentCR_BilletSpec" => item.CurrentCR_BilletSpec,
        "CurrentCR_RollingSpec" => item.CurrentCR_RollingSpec,
        "CurrentCR_IsFinished" => item.CurrentCR_IsFinished ? "True" : "False",
        "CurrentCR_DeformedSeqCompleted" => item.CurrentCR_DeformedSeqCompleted != false ? "True" : "False",
        "NextCR_ProcessType" => item.NextCR_ProcessType,
        "NextCR_BilletSpec" => item.NextCR_BilletSpec,
        "NextCR_RollingSpec" => item.NextCR_RollingSpec,
        "NextCR_IsFinished" => item.NextCR_IsFinished ? "True" : "False",
        "NextNextCR_ProcessType" => item.NextNextCR_ProcessType,
        "NextNextCR_BilletSpec" => item.NextNextCR_BilletSpec,
        "NextNextCR_RollingSpec" => item.NextNextCR_RollingSpec,
        "NextNextCR_IsFinished" => item.NextNextCR_IsFinished ? "True" : "False",
        "RealTimeCR_ProcessType" => item.RealTimeCR_ProcessType,
        "RealTimeCR_BilletSpec" => item.RealTimeCR_BilletSpec,
        "RealTimeCR_RollingSpec" => item.RealTimeCR_RollingSpec,
        "RealTimeCR_IsFinished" => item.RealTimeCR_IsFinished ? "True" : "False",
        "CR_CompletionType" => item.CR_CompletionType,
        "CR_RollType" => item.CR_RollType,
        "CR_SchedMachineNo" => item.CR_SchedMachineNo,
        "OriginalDiff" => item.OriginalDiff.ToString(),
        "CurrentDiff" => item.CurrentDiff.ToString(),
        "IsExecuted" => item.IsExecuted ? "True" : "False",
        "IsCompliant" => item.IsCompliant,
        _ => null
    };

    // ========== ExcelFilter 事件 ==========

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        await SavePageStateAsync();
        if (table != null) { ApplyFiltersAndSort(); StateHasChanged(); }
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
        StateHasChanged();
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
        StateHasChanged();
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
        if (table != null) { ApplyFiltersAndSort(); StateHasChanged(); }
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        // 永久隐藏列：重置后仍强制不显示
        foreach (var col in _allColumns)
        {
            if (_permanentlyHiddenColumnKeys.Contains(col.Key))
                col.Visible = false;
        }
        await SaveColumnPrefs();
        await SavePageStateAsync();
        if (table != null) { ApplyFiltersAndSort(); StateHasChanged(); }
    }

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        await SavePageStateAsync();
        if (table != null) { ApplyFiltersAndSort(); StateHasChanged(); }
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
            11 => "col-g11",
            12 => "col-g12",
            13 => "col-g13",
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
        await LoadDictOptionsAsync();

        _allColumns = GetAllColumnDefs();

        // 从 ColumnPrefsService 恢复列顺序和显隐
        var savedPrefs = await ColumnPrefs.LoadAsync("batchplans", null);
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

        // 永久隐藏列：强制不显示（不参与列显隐选择器，任何偏好不覆盖）
        foreach (var col in _allColumns)
        {
            if (_permanentlyHiddenColumnKeys.Contains(col.Key))
                col.Visible = false;
        }

        // 将新字段移动到正确的组内位置（追加在末尾会导致组显示错乱）
        RepositionNewColumn("OuterDiameterSpan", "FlowCRType");
        RepositionNewColumn("PlanOuterDiameterSpan", "PlanFlowTarget");
        RepositionNewColumn("ProductionFlowProperty", "MainNoAttentionProcess");
        RepositionNewColumn("AttentionProcessSectionSequence", "MainNoAttentionProcess");

        // 从 PageState 恢复排序/筛选状态（列显隐/顺序由 ColumnPrefs 管理）
        var savedState = await PageState.LoadAsync("batchplans");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "BatchNo";
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

            if (savedState.Extras?.ContainsKey("selectedSection") == true)
            {
                _selectedSection = savedState.Extras["selectedSection"];
                if (_selectedSection == "全部") _selectedSection = null;
            }
        }

        await LoadDataAsync();
        await LoadSummaryAsync();
    }

    /// <summary>
    /// 将新字段移动到指定锚点字段之后（兼容旧配置不包含此新字段时被追加到末尾）
    /// </summary>
    private void RepositionNewColumn(string newKey, string afterKey)
    {
        var newCol = _allColumns.FirstOrDefault(c => c.Key == newKey);
        if (newCol == null) return;
        var currentIdx = _allColumns.IndexOf(newCol);
        var anchorIdx = _allColumns.FindLastIndex(c => c.Key == afterKey);
        if (anchorIdx < 0) return;
        if (currentIdx == anchorIdx + 1) return; // 已在正确位置
        _allColumns.RemoveAt(currentIdx);
        var insertIdx = _allColumns.FindLastIndex(c => c.Key == afterKey);
        if (insertIdx >= 0)
            _allColumns.Insert(insertIdx + 1, newCol);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#batch-plan-list-table");
        }
        catch { }
        // 分页汇总改由 FooterContent 渲染期 EnsurePageSumsComputed 惰性重算（见该方法的注释说明）
    }

    // ========== Tab 汇总辅助方法 ==========

    private void ClearTabSummaries()
    {
        _tabBatchCount = 0;
        _tabTotalWeight = 0m;
        _tabFlowBatchCount = 0;
        _tabFlowBatchWeight = 0m;
        _tabKeyBatchCount = 0;
        _tabKeyBatchWeight = 0m;
    }

    // ========== 数据加载（从 _allItems 中筛选+排序+分页） ==========

    private void ApplyFiltersAndSort()
    {
        // 从 _allItems 中过滤
        var filtered = _allItems.ToList();

        // 1. 关键词搜索
        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            var kw = _searchKeyword;
            filtered = filtered.Where(x =>
                (x.BatchNo != null && x.BatchNo.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.TagNo != null && x.TagNo.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PlantGrade != null && x.PlantGrade.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.WorkOrderNo != null && x.WorkOrderNo.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.Salesman != null && x.Salesman.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.Specification != null && x.Specification.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PendingProcess != null && x.PendingProcess.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PendingSectionName != null && (SectionDisplayHelper.GetSectionNameText(x.PendingSectionName).Contains(kw, StringComparison.OrdinalIgnoreCase) || x.PendingSectionName.Contains(kw, StringComparison.OrdinalIgnoreCase))) ||
                (x.UrgencyLevel != null && x.UrgencyLevel.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.ProductionFlowProperty != null && x.ProductionFlowProperty.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.MainNoAttentionProcess != null && x.MainNoAttentionProcess.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.AdjustmentRemark != null && x.AdjustmentRemark.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.FlowTarget != null && x.FlowTarget.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.FlowCRType != null && x.FlowCRType.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PlanFlowTarget != null && x.PlanFlowTarget.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PlanFlowCRType != null && x.PlanFlowCRType.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PlanFlowExecSpec != null && x.PlanFlowExecSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PlanRemark != null && x.PlanRemark.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.CR_CompletionType != null && x.CR_CompletionType.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.CR_RollType != null && x.CR_RollType.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.CR_SchedMachineNo != null && x.CR_SchedMachineNo.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.DeliveryState != null && DisplayHelper.GetDeliveryStateText(x.DeliveryState.Value).Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.LengthStatus != null && DisplayHelper.GetLengthStatusText(x.LengthStatus.Value).Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.CurrentSectionName != null && (SectionDisplayHelper.GetSectionNameText(x.CurrentSectionName).Contains(kw, StringComparison.OrdinalIgnoreCase) || x.CurrentSectionName.Contains(kw, StringComparison.OrdinalIgnoreCase))) ||
                (x.PendingSpec != null && x.PendingSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PendingUnit != null && x.PendingUnit.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PendingEquipment != null && x.PendingEquipment.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.OuterDiameterSpan != null && x.OuterDiameterSpan.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PlanOuterDiameterSpan != null && x.PlanOuterDiameterSpan.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.FlowExecSpec != null && x.FlowExecSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.CurrentCR_ProcessType != null && x.CurrentCR_ProcessType.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.CurrentCR_BilletSpec != null && x.CurrentCR_BilletSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.CurrentCR_RollingSpec != null && x.CurrentCR_RollingSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.NextCR_ProcessType != null && x.NextCR_ProcessType.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.NextCR_BilletSpec != null && x.NextCR_BilletSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.NextCR_RollingSpec != null && x.NextCR_RollingSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.NextNextCR_ProcessType != null && x.NextNextCR_ProcessType.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.NextNextCR_BilletSpec != null && x.NextNextCR_BilletSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.NextNextCR_RollingSpec != null && x.NextNextCR_RollingSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.RealTimeCR_ProcessType != null && x.RealTimeCR_ProcessType.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.RealTimeCR_BilletSpec != null && x.RealTimeCR_BilletSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.RealTimeCR_RollingSpec != null && x.RealTimeCR_RollingSpec.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.CurrentCR_DeformedSeqCompleted != false ? "完成" : "否").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                (x.IsCompliant != null && x.IsCompliant.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                // 中文显示文本列（enum/int/bool 档位）：计划状态/等级/布尔是·否
                (x.ScheduleStage switch { -1 => "无此工单", 4 => "非工单", _ => IntStatusDisplayHelper.GetPlanScheduleStageText(x.ScheduleStage) }).Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                (x.ScheduleTierDisplay != null && x.ScheduleTierDisplay.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.PlanFlowLevelDisplay != null && x.PlanFlowLevelDisplay.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (x.IsKeyBatch ? "是" : "否").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                (x.IsFlow ? "是" : "否").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                (x.PlanIsFlow ? "是" : "否").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                (x.PlanIsPaused ? "是" : "否").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                (x.IsExecuted ? "是" : "否").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                (x.IsGrabOrder ? "是" : "否").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                (x.IsUrging ? "是" : "否").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                (x.IsBatchDelivery ? "是" : "否").Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                (x.IsPaused ? "是" : "否").Contains(kw, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        // 2. ExcelFilter 列筛选
        if (_columnFilters.Count > 0)
        {
            filtered = filtered.Where(x => _columnFilters.All(f =>
            {
                var col = _allColumns.FirstOrDefault(c => c.Key == f.Key);
                if (col?.FilterType == "number")
                {
                    var hasNotNull = f.Value.Contains(FilterNotNull);
                    var hasNull = f.Value.Contains(FilterNull);
                    // 勾选的具体数值（除非空/空哨兵外的实际值）
                    var actualValues = f.Value.Where(v => v != FilterNotNull && v != FilterNull).ToHashSet();
                    var val = GetFilterValue(x, f.Key);
                    if (actualValues.Count > 0)
                    {
                        if (val != null && actualValues.Contains(val)) return true;
                        if (hasNull && val == null) return true;
                        if (hasNotNull && val != null) return true;
                        return false;
                    }
                    if (hasNotNull && hasNull) return true;
                    if (hasNotNull) return val != null;
                    if (hasNull) return val == null;
                    return false;
                }
                var enumVal = GetFilterValue(x, f.Key);
                if (enumVal == null) return f.Value.Contains(FilterNull);
                return f.Value.Contains(enumVal);
            })).ToList();
        }

        // 3. 排序
        filtered = ApplySorting(filtered, sortColumn, sortDescending);

        // 4. 赋全量数据给 _filteredItems（MudTable Items 模式自动分页）
        _filteredAllItems = filtered.ToList();
        _filteredItems = _filteredAllItems;
        _totalCount = _filteredItems.Count;
        ComputePageSums();
    }

    private void OnRowsPerPageChanged(int size)
    {
        _pageSize = size;
        ApplyFiltersAndSort();
        StateHasChanged();
        _ = SavePageStateAsync();
    }

    private static List<BatchPlanDto> ApplySorting(List<BatchPlanDto> items, string sortBy, bool desc)
    {
        var query = sortBy.ToLower() switch
        {
            "batchno" => items.OrderBy(x => x.BatchNo ?? ""),
            "tagno" => items.OrderBy(x => x.TagNo ?? ""),
            "plantgrade" => items.OrderBy(x => x.PlantGrade ?? ""),
            "currentvalidweight" => items.OrderBy(x => x.CurrentValidWeight),
            "workorderno" => items.OrderBy(x => x.WorkOrderNo ?? ""),
            "salesman" => items.OrderBy(x => x.Salesman ?? ""),
            "deliverydate" => items.OrderBy(x => x.DeliveryDate),
            "deliverystate" => items.OrderBy(x => x.DeliveryState.HasValue ? DisplayHelper.GetDeliveryStateText(x.DeliveryState.Value) : ""),
            "specification" => items.OrderBy(x => x.Specification ?? ""),
            "lengthstatus" => items.OrderBy(x => x.LengthStatus.HasValue ? DisplayHelper.GetLengthStatusText(x.LengthStatus.Value) : ""),
            "minlength" => items.OrderBy(x => x.MinLength),
            "maxlength" => items.OrderBy(x => x.MaxLength),
            "currentexecdate" => items.OrderBy(x => x.CurrentExecDate),
            "pendingprocess" => items.OrderBy(x => x.PendingProcess ?? ""),
            "pendingsectionname" => items.OrderBy(x => x.PendingSectionName ?? ""),
            "pendingspec" => items.OrderBy(x => x.PendingSpec ?? ""),
            "pendingunit" => items.OrderBy(x => x.PendingUnit ?? ""),
            "pendingequipment" => items.OrderBy(x => x.PendingEquipment ?? ""),
            "currentcr_processtype" => items.OrderBy(x => x.CurrentCR_ProcessType ?? ""),
            "currentcr_billetspec" => items.OrderBy(x => x.CurrentCR_BilletSpec ?? ""),
            "currentcr_rollingspec" => items.OrderBy(x => x.CurrentCR_RollingSpec ?? ""),
            "currentcr_isfinished" => items.OrderBy(x => x.CurrentCR_IsFinished),
            "currentcr_deformedseqcompleted" => items.OrderBy(x => x.CurrentCR_DeformedSeqCompleted),
            "nextcr_processtype" => items.OrderBy(x => x.NextCR_ProcessType ?? ""),
            "nextcr_billetspec" => items.OrderBy(x => x.NextCR_BilletSpec ?? ""),
            "nextcr_rollingspec" => items.OrderBy(x => x.NextCR_RollingSpec ?? ""),
            "nextcr_isfinished" => items.OrderBy(x => x.NextCR_IsFinished),
            "nextnextcr_processtype" => items.OrderBy(x => x.NextNextCR_ProcessType ?? ""),
            "nextnextcr_billetspec" => items.OrderBy(x => x.NextNextCR_BilletSpec ?? ""),
            "nextnextcr_rollingspec" => items.OrderBy(x => x.NextNextCR_RollingSpec ?? ""),
            "nextnextcr_isfinished" => items.OrderBy(x => x.NextNextCR_IsFinished),
            "realtimecr_processtype" => items.OrderBy(x => x.RealTimeCR_ProcessType ?? ""),
            "realtimecr_billetspec" => items.OrderBy(x => x.RealTimeCR_BilletSpec ?? ""),
            "realtimecr_rollingspec" => items.OrderBy(x => x.RealTimeCR_RollingSpec ?? ""),
            "realtimecr_isfinished" => items.OrderBy(x => x.RealTimeCR_IsFinished),
            "cr_completiontype" => items.OrderBy(x => x.CR_CompletionType ?? ""),
            "cr_rolltype" => items.OrderBy(x => x.CR_RollType ?? ""),
            "cr_schedmachineno" => items.OrderBy(x => x.CR_SchedMachineNo ?? ""),
            "urgencylevel" => items.OrderBy(x => x.UrgencyLevel ?? ""),
            "schedulestage" => items.OrderBy(x => x.ScheduleStage),
            "productionflowproperty" => items.OrderBy(x => x.ProductionFlowProperty ?? ""),
            "mainnoattentionprocess" => items.OrderBy(x => x.MainNoAttentionProcess ?? ""),
            "attentionprocesssectionsequence" => items.OrderBy(x => x.AttentionProcessSectionSequence),
            "iskeybatch" => items.OrderBy(x => x.IsKeyBatch),
            "isurging" => items.OrderBy(x => x.IsUrging),
            "isbatchdelivery" => items.OrderBy(x => x.IsBatchDelivery),
            "ispaused" => items.OrderBy(x => x.IsPaused),
            "adjustmentremark" => items.OrderBy(x => x.AdjustmentRemark ?? ""),
            "isflow" => items.OrderBy(x => x.IsFlow),
            "flowlevel" => items.OrderBy(x => x.ScheduleTier),
            "flowtarget" => items.OrderBy(x => x.FlowTarget ?? ""),
            "flowcrtype" => items.OrderBy(x => x.FlowCRType ?? ""),
            "outerdiameterspan" => items.OrderBy(x => x.OuterDiameterSpan ?? ""),
            "flowexecspec" => items.OrderBy(x => x.FlowExecSpec ?? ""),
            "executionsequence" => items.OrderBy(x => x.ExecutionSequence),
            "targetsequence" => items.OrderBy(x => x.TargetSequence),
            "originaldiff" => items.OrderBy(x => x.OriginalDiff),
            "currentdiff" => items.OrderBy(x => x.CurrentDiff),
            "isexecuted" => items.OrderBy(x => x.IsExecuted),
            "iscompliant" => items.OrderBy(x => x.IsCompliant),
            "isgraborder" => items.OrderBy(x => x.IsGrabOrder),
            "planremark" => items.OrderBy(x => x.PlanRemark ?? ""),
            "planisflow" => items.OrderBy(x => x.PlanIsFlow),
            "planispaused" => items.OrderBy(x => x.PlanIsPaused),
            "planflowlevel" => items.OrderBy(x => x.PlanFlowLevel),
            "planflowtarget" => items.OrderBy(x => x.PlanFlowTarget ?? ""),
            "planflowcrtype" => items.OrderBy(x => x.PlanFlowCRType ?? ""),
            "planouterdiameterspan" => items.OrderBy(x => x.PlanOuterDiameterSpan ?? ""),
            "planflowexecspec" => items.OrderBy(x => x.PlanFlowExecSpec ?? ""),
            "planexecutionsequence" => items.OrderBy(x => x.PlanExecutionSequence),
            "plantargetsequence" => items.OrderBy(x => x.PlanTargetSequence),
            _ => items.OrderBy(x => x.BatchNo ?? "")
        };
        return desc ? query.Reverse().ToList() : query.ToList();
    }

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
            case "ProductionType":
                builder.AddContent(0, string.IsNullOrEmpty(item.ProductionType) ? "-" : DisplayHelper.GetProductionTypeText(item.ProductionType));
                break;
            case "ManufacturingItem":
                builder.AddContent(0, string.IsNullOrEmpty(item.ManufacturingItem) ? "-" : DisplayHelper.GetMaterialTypeText(item.ManufacturingItem));
                break;
            case "ManufacturingStatus":
                builder.AddContent(0, string.IsNullOrEmpty(item.ManufacturingStatus) ? "-" : DisplayHelper.GetDeliveryStateText(item.ManufacturingStatus));
                break;
            case "WorkOrderNo":
                builder.AddContent(0, item.WorkOrderNo);
                break;
            case "SalesOrderNo":
                builder.AddContent(0, item.SalesOrderNo ?? "-");
                break;
            case "ProductionMainNo":
                builder.AddContent(0, item.ProductionMainNo ?? "-");
                break;
            case "Salesman":
                builder.AddContent(0, item.Salesman ?? "-");
                break;
            case "EndCustomer":
                builder.AddContent(0, item.EndCustomer ?? "-");
                break;
            case "DeliveryDate":
                // 0001-01-01（default）按批次首页显示样式显示为空
                builder.AddContent(0, item.DeliveryDate == default ? "" : item.DeliveryDate.ToString("yyyy-MM-dd"));
                break;
            case "DeliveryState":
                builder.AddContent(0, col.DisplayConverter?.Invoke(item.DeliveryState) as string ?? (item.DeliveryState.HasValue ? DisplayHelper.GetDeliveryStateText(item.DeliveryState.Value) : "-"));
                break;
            case "Specification":
                builder.AddContent(0, item.Specification);
                break;
            case "LengthStatus":
                // 定尺分"定尺/定尺（多）"两种显示（最小长度≠最大长度 → 定尺（多）），沿用工单维度口径
                builder.AddContent(0, item.LengthStatus.HasValue
                    ? DisplayHelper.GetWorkOrderLengthStatusText(item.LengthStatus.Value, item.MinLength, item.MaxLength)
                    : "-");
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
                builder.AddContent(0, string.IsNullOrEmpty(item.CurrentSectionName) ? "-" : SectionDisplayHelper.GetSectionNameText(item.CurrentSectionName));
                break;
            case "PendingProcess":
                builder.AddContent(0, ProcessDisplayHelper.GetProcessNameText(item.PendingProcess ?? "-"));
                break;
            case "PendingSectionName":
                builder.AddContent(0, string.IsNullOrEmpty(item.PendingSectionName) ? "-" : SectionDisplayHelper.GetSectionNameText(item.PendingSectionName));
                break;
            case "PendingSpec":
                builder.AddContent(0, item.PendingSpec ?? "-");
                break;
            case "PendingUnit":
                builder.AddContent(0, item.PendingUnit ?? "-");
                break;
            case "PendingEquipment":
                builder.AddContent(0, item.PendingEquipment ?? "-");
                break;
            case "ExecutionSequence":
                builder.AddContent(0, (item.ExecutionSequence ?? 0).ToString());
                break;

            // G4
            case "UrgencyLevel":
                var urgencyColor = DisplayHelper.GetUrgencyColor(item.UrgencyLevel);
                if (item.UrgencyLevel != null)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", urgencyColor);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey,item.UrgencyLevel))));
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
                    -1 => Color.Error,
                    0 => Color.Default,
                    1 => Color.Warning,
                    2 => Color.Success,
                    3 => Color.Info,
                    4 => Color.Secondary,
                    _ => Color.Default
                };
                var stageText = item.ScheduleStage switch
                {
                    -1 => "无此工单",
                    4 => "非工单",
                    _ => IntStatusDisplayHelper.GetPlanScheduleStageText(item.ScheduleStage)
                };
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", stageColor);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, stageText)));
                builder.CloseComponent();
                break;
            case "MainNoAttentionProcess":
                builder.AddContent(0, ProcessDisplayHelper.GetProcessNameText(item.MainNoAttentionProcess ?? "-"));
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

            // G6: 工单需求调整
            case "IsUrging":
                if (item.IsUrging)
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
            case "IsBatchDelivery":
                builder.AddContent(0, item.IsBatchDelivery ? "是" : "否");
                break;
            case "IsPaused":
                if (item.IsPaused)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Warning);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "是")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "否");
                }
                break;
            case "AdjustmentRemark":
                builder.AddContent(0, item.AdjustmentRemark ?? "-");
                break;

            // G11: 关联冷轧排程
            case "IsFlow":
                if (item.IsFlow)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Primary);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "是")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "否");
                }
                break;
            case "FlowLevel":
                var levelColor = item.ScheduleTier switch
                {
                    1 => Color.Error,
                    2 => Color.Warning,
                    3 => Color.Warning,
                    4 => Color.Default,
                    5 => Color.Info,
                    _ => Color.Default
                };
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", levelColor);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.ScheduleTierDisplay)));
                builder.CloseComponent();
                break;
            case "FlowTarget":
                builder.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.FlowTargetKey, item.FlowTarget) ?? "");
                break;
            case "FlowCRType":
                builder.AddContent(0, ProcessDisplayHelper.GetProcessNameText(item.FlowCRType ?? "-"));
                break;
            case "OuterDiameterSpan":
                builder.AddContent(0, item.OuterDiameterSpan ?? "-");
                break;
            case "FlowExecSpec":
                builder.AddContent(0, item.FlowExecSpec ?? "-");
                break;
            case "TargetSequence":
                builder.AddContent(0, item.TargetSequence?.ToString() ?? "-");
                break;

            // G5: 冷轧排程
            case "CurrentCR_ProcessType":
                builder.AddContent(0, ProcessDisplayHelper.GetProcessNameText(item.CurrentCR_ProcessType ?? "-"));
                break;
            case "CurrentCR_BilletSpec":
                builder.AddContent(0, item.CurrentCR_BilletSpec ?? "-");
                break;
            case "CurrentCR_RollingSpec":
                builder.AddContent(0, item.CurrentCR_RollingSpec ?? "-");
                break;
            case "CurrentCR_IsFinished":
                builder.AddContent(0, item.CurrentCR_IsFinished ? "是" : "否");
                break;
            case "CurrentCR_DeformedSeqCompleted":
                builder.AddContent(0, item.CurrentCR_DeformedSeqCompleted != false ? "完成" : "否");
                break;
            case "NextCR_ProcessType":
                builder.AddContent(0, ProcessDisplayHelper.GetProcessNameText(item.NextCR_ProcessType ?? "-"));
                break;
            case "NextCR_BilletSpec":
                builder.AddContent(0, item.NextCR_BilletSpec ?? "-");
                break;
            case "NextCR_RollingSpec":
                builder.AddContent(0, item.NextCR_RollingSpec ?? "-");
                break;
            case "NextCR_IsFinished":
                builder.AddContent(0, item.NextCR_IsFinished ? "是" : "否");
                break;
            case "NextNextCR_ProcessType":
                builder.AddContent(0, ProcessDisplayHelper.GetProcessNameText(item.NextNextCR_ProcessType ?? "-"));
                break;
            case "NextNextCR_BilletSpec":
                builder.AddContent(0, item.NextNextCR_BilletSpec ?? "-");
                break;
            case "NextNextCR_RollingSpec":
                builder.AddContent(0, item.NextNextCR_RollingSpec ?? "-");
                break;
            case "NextNextCR_IsFinished":
                builder.AddContent(0, item.NextNextCR_IsFinished ? "是" : "否");
                break;
            case "RealTimeCR_ProcessType":
                builder.AddContent(0, ProcessDisplayHelper.GetProcessNameText(item.RealTimeCR_ProcessType ?? "-"));
                break;
            case "RealTimeCR_BilletSpec":
                builder.AddContent(0, item.RealTimeCR_BilletSpec ?? "-");
                break;
            case "RealTimeCR_RollingSpec":
                builder.AddContent(0, item.RealTimeCR_RollingSpec ?? "-");
                break;
            case "RealTimeCR_IsFinished":
                builder.AddContent(0, item.RealTimeCR_IsFinished ? "是" : "否");
                break;
            case "CR_CompletionType":
                builder.AddContent(0, string.IsNullOrEmpty(item.CR_CompletionType) || item.CR_CompletionType == "None"
                    ? "-" : col.DisplayConverter?.Invoke(item.CR_CompletionType) ?? item.CR_CompletionType);
                break;
            case "CR_RollType":
                builder.AddContent(0, string.IsNullOrEmpty(item.CR_RollType) || item.CR_RollType == "None"
                    ? "-" : col.DisplayConverter?.Invoke(item.CR_RollType) ?? item.CR_RollType);
                break;
            case "CR_SchedMachineNo":
                builder.AddContent(0, item.CR_SchedMachineNo ?? "-");
                break;

            // G4: 生产流转性
            case "ProductionFlowProperty":
                builder.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.ProductionFlowKey,item.ProductionFlowProperty) ?? "-");
                break;
            case "AttentionProcessSectionSequence":
                builder.AddContent(0, item.AttentionProcessSectionSequence?.ToString() ?? "-");
                break;

            // G12: 执行反馈
            case "OriginalDiff":
                builder.AddContent(0, item.OriginalDiff.ToString());
                break;
            case "CurrentDiff":
                builder.AddContent(0, item.CurrentDiff.ToString());
                break;
            case "IsExecuted":
                if (item.IsExecuted)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Success);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "是")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Error);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "否")));
                    builder.CloseComponent();
                }
                break;
            case "IsCompliant":
                if (item.IsCompliant == "达标")
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Success);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "达标")));
                    builder.CloseComponent();
                }
                else if (item.IsCompliant == "未达标")
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Error);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "未达标")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "-");
                }
                break;

            // G13: 批次计划（只读展示：除"抢单""计划备注"外均由服务端三规则自动生成，手工编辑会被计划安排覆盖）
            case "PlanIsFlow":
                if (item.PlanIsFlow)
                {
                    // 流转=是：高亮显示（蓝底白字）
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", Color.Primary);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, "是")));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, "否");
                }
                break;
            case "PlanFlowLevel":
                var planLevelColor = item.PlanFlowLevel switch
                {
                    1 => Color.Error,
                    2 => Color.Warning,
                    3 => Color.Warning,
                    _ => Color.Default
                };
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", planLevelColor);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.PlanFlowLevelDisplay)));
                builder.CloseComponent();
                break;
            case "PlanFlowTarget":
                builder.AddContent(0, DictValueDisplayHelper.GetText(DictValueDefaults.FlowTargetKey, item.PlanFlowTarget) ?? "");
                break;
            case "PlanFlowCRType":
                builder.AddContent(0, string.IsNullOrEmpty(item.PlanFlowCRType) ? "" : ProcessDisplayHelper.GetProcessNameText(item.PlanFlowCRType));
                break;
            case "PlanOuterDiameterSpan":
                builder.AddContent(0, item.PlanOuterDiameterSpan ?? "");
                break;
            case "PlanFlowExecSpec":
                builder.AddContent(0, item.PlanFlowExecSpec ?? "");
                break;
            case "PlanExecutionSequence":
                // 未产执行序显示 0
                builder.AddContent(0, (item.PlanExecutionSequence ?? 0).ToString());
                break;
            case "PlanTargetSequence":
                builder.AddContent(0, item.PlanTargetSequence?.ToString() ?? "");
                break;
            case "PlanIsPaused":
                builder.OpenComponent<MudSwitch<bool>>(0);
                builder.AddAttribute(1, "Value", item.PlanIsPaused);
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<bool>(this, async v =>
                {
                    item.PlanIsPaused = v;
                    await SavePlanFieldAsync(item);
                    // 读时覆盖：切回"否"需重新加载以恢复原流转字段显示（流转/等级/流转位等）
                    await LoadDataAsync();
                    // 暂停影响流转/重点统计 → 同步刷新跨工段汇总卡片
                    await LoadSummaryAsync();
                }));
                builder.AddAttribute(3, "Color", Color.Warning);
                builder.AddAttribute(4, "Dense", true);
                builder.CloseComponent();
                break;
            case "IsGrabOrder":
                builder.OpenComponent<MudSwitch<bool>>(0);
                builder.AddAttribute(1, "Value", item.IsGrabOrder);
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<bool>(this, async v =>
                {
                    item.IsGrabOrder = v;
                    await SavePlanFieldAsync(item);
                }));
                builder.AddAttribute(3, "Color", Color.Primary);
                builder.AddAttribute(4, "Dense", true);
                builder.CloseComponent();
                break;
            case "PlanRemark":
                builder.OpenComponent<MudTextField<string>>(0);
                builder.AddAttribute(1, "Value", item.PlanRemark ?? "");
                builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, async v =>
                {
                    item.PlanRemark = string.IsNullOrEmpty(v) ? null : v;
                    await SavePlanFieldAsync(item);
                }));
                // ⚠️ 受控组件必须有 ValueExpression，否则输入回弹无法编辑（MudBlazor 6.19.1 已知问题）
                builder.AddAttribute(3, "ValueExpression", () => item.PlanRemark ?? "");
                builder.AddAttribute(4, "Dense", true);
                builder.AddAttribute(5, "Variant", Variant.Outlined);
                builder.AddAttribute(6, "Size", Size.Small);
                builder.AddAttribute(7, "Class", "compact-select");
                builder.CloseComponent();
                break;
        }
    };

    // ========== 计划安排 ==========

    private async Task HandlePlanAllAsync()
    {
        try
        {
            _isPlanning = true;
            StateHasChanged();
            await BatchPlanScheduleSvc.PlanAllAsync(_selectedSection);
            Snackbar.Add("计划安排完成，正在刷新数据...", Severity.Success);
            await LoadDataAsync();
            await LoadSummaryAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"计划安排失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isPlanning = false;
            StateHasChanged();
        }
    }

    private async Task SavePlanFieldAsync(BatchPlanDto item)
    {
        try
        {
            var dto = new BatchPlanScheduleDto
            {
                BatchId = item.BatchId,
                IsPaused = item.PlanIsPaused,
                IsFlow = item.PlanIsFlow,
                FlowLevel = item.PlanFlowLevel,
                FlowTarget = item.PlanFlowTarget,
                FlowCRType = item.PlanFlowCRType,
                PlanOuterDiameterSpan = item.PlanOuterDiameterSpan,
                FlowExecSpec = item.PlanFlowExecSpec,
                ExecutionSequence = item.PlanExecutionSequence,
                TargetSequence = item.PlanTargetSequence,
                IsGrabOrder = item.IsGrabOrder,
                PlanRemark = item.PlanRemark,
            };
            await BatchPlanScheduleSvc.SaveAsync(dto);
            // 保存成功后刷新顶部汇总（流转批次/重点批次计数及重量）
            UpdateTabSummary();
            // 强制 MudTable 重新渲染 RowTemplate，确保 G12 执行反馈重算
            ApplyFiltersAndSort();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"保存失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));

        extras["selectedSection"] = _selectedSection ?? "全部";

        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = 1,
            Extras = extras
        };
        await PageState.SaveAsync("batchplans", state);
    }

    // ========== ColumnPrefs 持久化 ==========

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("batchplans", null, _allColumns);
    }

    // ========== 打印 ==========

    /// <summary>打印「近日生产量数据」卡片（前端 printRawHtml 直接打印 DOM 表格）</summary>
    private async Task PrintSummaryTable()
    {
        try
        {
            var html = await JS.InvokeAsync<string>("getTableHtml", "#batch-plan-summary-table");
            if (!string.IsNullOrEmpty(html))
                await JS.InvokeVoidAsync("printRawHtml", html, "近日生产量数据");
            else
                Snackbar.Add("未找到可打印的汇总表格", Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>打印「实时委外在产」卡片（前端 printRawHtml 直接打印 DOM 表格）</summary>
    private async Task PrintOutsourcePendingTable()
    {
        try
        {
            var html = await JS.InvokeAsync<string>("getTableHtml", "#batch-plan-outsource-pending-table");
            if (!string.IsNullOrEmpty(html))
                await JS.InvokeVoidAsync("printRawHtml", html, "实时委外在产");
            else
                Snackbar.Add("未找到可打印的委外在产表格", Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>打印「段落流转分析」折叠查询（后端 QuestPDF，无可持续天数列）</summary>
    private async Task PrintParagraphAnalysis()
    {
        try
        {
            var printColumns = new List<PrintColumnDef>
            {
                new() { Key = "Paragraph",     Label = "生产段落" },
                new() { Key = "PendingTotal",  Label = "待在产重量" },
                new() { Key = "StatusJudgment",Label = "总况判定" },
                new() { Key = "PlanFlowQuantity",Label = "计划流转量" },
                new() { Key = "PlanFlowJudgment",Label = "计划流转判定" },
                new() { Key = "PlanKeyWeight", Label = "特急批重量" },
            };
            var printItems = _paragraphRows.Select(item => new Dictionary<string, object>
            {
                ["Paragraph"] = item.ParagraphName,
                ["PendingTotal"] = RenderInt(item.PendingTotal),
                ["StatusJudgment"] = item.StatusJudgment ?? "-",
                ["PlanFlowQuantity"] = RenderInt(item.PlanFlowQuantity),
                ["PlanFlowJudgment"] = item.PlanFlowJudgment ?? "-",
                ["PlanKeyWeight"] = RenderInt(item.PlanKeyWeight),
            }).ToList();

            var request = new SectionParagraphFlowAnalysisPrintRequest
            {
                Title = "段落流转分析",
                Items = printItems,
                Columns = printColumns
            };
            var apiUrl = $"{Http.BaseAddress}api/section-paragraph-flow-analysis/print-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>打印「工段流转分析」折叠查询（后端 QuestPDF，无可持续天数列）</summary>
    private async Task PrintFlowAnalysis()
    {
        try
        {
            var printColumns = new List<PrintColumnDef>
            {
                new() { Key = "Category",      Label = "流转类别" },
                new() { Key = "PendingTotal",  Label = "待在产重量" },
                new() { Key = "StatusJudgment",Label = "总况判定" },
                new() { Key = "PlanFlowQuantity",Label = "计划流转量" },
                new() { Key = "PlanFlowJudgment",Label = "计划流转判定" },
                new() { Key = "PlanKeyWeight", Label = "特急批重量" },
            };
            var printItems = _flowRows.Select(item => new Dictionary<string, object>
            {
                ["Category"] = item.CategoryName,
                ["PendingTotal"] = RenderInt(item.PendingTotal),
                ["StatusJudgment"] = item.StatusJudgment ?? "-",
                ["PlanFlowQuantity"] = RenderInt(item.PlanFlowQuantity),
                ["PlanFlowJudgment"] = item.PlanFlowJudgment ?? "-",
                ["PlanKeyWeight"] = RenderInt(item.PlanKeyWeight),
            }).ToList();

            var request = new SectionFlowAnalysisPrintRequest
            {
                Title = "工段流转分析",
                Items = printItems,
                Columns = printColumns
            };
            var apiUrl = $"{Http.BaseAddress}api/section-flow-analysis/print-file";
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
                    dict[col.Key] = ResolvePrintValue(item, col);
                }
                return dict;
            }).ToList();

            var request = new BatchPlanPrintRequest
            {
                Title = "批次计划",
                Items = printItems,
                Columns = printColumns
            };

            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/batch-plan/print-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private static object ResolvePrintValue(BatchPlanDto item, ColumnDef col)
    {
        // 枚举列：使用 DisplayConverter
        if (col.DisplayConverter != null)
            return col.DisplayConverter(GetRawPropertyValue(item, col.Key)) ?? "";

        // 布尔列：用 BoolTrueLabel/BoolFalseLabel
        if (col.FilterType == "boolean")
        {
            var raw = GetRawPropertyValue(item, col.Key);
            if (raw is bool b)
                return b ? col.BoolTrueLabel : col.BoolFalseLabel;
            return raw?.ToString() ?? "-";
        }

        return GetRawPropertyValue(item, col.Key);
    }

    private static object GetRawPropertyValue(BatchPlanDto item, string key)
    {
        return (key switch
        {
            "BatchNo" => item.BatchNo ?? "",
            "TagNo" => item.TagNo ?? "",
            "PlantGrade" => item.PlantGrade ?? "",
            "CurrentValidWeight" => item.CurrentValidWeight,
            "ProductionType" => string.IsNullOrEmpty(item.ProductionType) ? "" : DisplayHelper.GetProductionTypeText(item.ProductionType),
            "ManufacturingItem" => string.IsNullOrEmpty(item.ManufacturingItem) ? "" : DisplayHelper.GetMaterialTypeText(item.ManufacturingItem),
            "ManufacturingStatus" => string.IsNullOrEmpty(item.ManufacturingStatus) ? "" : DisplayHelper.GetDeliveryStateText(item.ManufacturingStatus),
            "WorkOrderNo" => item.WorkOrderNo ?? "",
            "SalesOrderNo" => item.SalesOrderNo ?? "",
            "ProductionMainNo" => item.ProductionMainNo ?? "",
            "Salesman" => item.Salesman ?? "",
            "EndCustomer" => item.EndCustomer ?? "",
            "DeliveryDate" => item.DeliveryDate == default ? "" : item.DeliveryDate.ToString("yyyy-MM-dd"),
            "Specification" => item.Specification ?? "",
            "MinLength" => item.MinLength,
            "MaxLength" => item.MaxLength,
            "CurrentExecDate" => item.CurrentExecDate?.ToString("yyyy-MM-dd") ?? "",
            "CurrentSectionName" => SectionDisplayHelper.GetSectionNameText(item.CurrentSectionName),
            "PendingProcess" => ProcessDisplayHelper.GetProcessNameText(item.PendingProcess),
            "PendingSectionName" => SectionDisplayHelper.GetSectionNameText(item.PendingSectionName),
            "PendingSpec" => item.PendingSpec ?? "",
            "PendingUnit" => item.PendingUnit ?? "",
            "PendingEquipment" => item.PendingEquipment ?? "",
            "ExecutionSequence" => item.ExecutionSequence ?? 0,
            "UrgencyLevel" => DictValueDisplayHelper.GetText(DictValueDefaults.UrgencyLevelKey,item.UrgencyLevel) ?? "",
            "MainNoAttentionProcess" => ProcessDisplayHelper.GetProcessNameText(item.MainNoAttentionProcess),
            "AttentionProcessSectionSequence" => item.AttentionProcessSectionSequence,
            "IsKeyBatch" => item.IsKeyBatch,
            "IsUrging" => item.IsUrging,
            "IsBatchDelivery" => item.IsBatchDelivery,
            "IsPaused" => item.IsPaused,
            "AdjustmentRemark" => item.AdjustmentRemark ?? "",
            "IsFlow" => item.IsFlow,
            "FlowLevel" => item.ScheduleTierDisplay,
            "FlowTarget" => DictValueDisplayHelper.GetText(DictValueDefaults.FlowTargetKey,item.FlowTarget) ?? "",
            "FlowCRType" => ProcessDisplayHelper.GetProcessNameText(item.FlowCRType),
            "OuterDiameterSpan" => item.OuterDiameterSpan ?? "",
            "FlowExecSpec" => item.FlowExecSpec ?? "",
            "TargetSequence" => item.TargetSequence,
            "OriginalDiff" => item.OriginalDiff,
            "CurrentDiff" => item.CurrentDiff,
            "IsExecuted" => item.IsExecuted,
            "IsCompliant" => item.IsCompliant ?? "",
            "CurrentCR_ProcessType" => ProcessDisplayHelper.GetProcessNameText(item.CurrentCR_ProcessType),
            "CurrentCR_BilletSpec" => item.CurrentCR_BilletSpec ?? "",
            "CurrentCR_RollingSpec" => item.CurrentCR_RollingSpec ?? "",
            "CurrentCR_IsFinished" => item.CurrentCR_IsFinished,
            "CurrentCR_DeformedSeqCompleted" => item.CurrentCR_DeformedSeqCompleted != false ? "完成" : "否",
            "NextCR_ProcessType" => ProcessDisplayHelper.GetProcessNameText(item.NextCR_ProcessType),
            "NextCR_BilletSpec" => item.NextCR_BilletSpec ?? "",
            "NextCR_RollingSpec" => item.NextCR_RollingSpec ?? "",
            "NextCR_IsFinished" => item.NextCR_IsFinished,
            "NextNextCR_ProcessType" => ProcessDisplayHelper.GetProcessNameText(item.NextNextCR_ProcessType),
            "NextNextCR_BilletSpec" => item.NextNextCR_BilletSpec ?? "",
            "NextNextCR_RollingSpec" => item.NextNextCR_RollingSpec ?? "",
            "NextNextCR_IsFinished" => item.NextNextCR_IsFinished,
            "RealTimeCR_ProcessType" => ProcessDisplayHelper.GetProcessNameText(item.RealTimeCR_ProcessType),
            "RealTimeCR_BilletSpec" => item.RealTimeCR_BilletSpec ?? "",
            "RealTimeCR_RollingSpec" => item.RealTimeCR_RollingSpec ?? "",
            "RealTimeCR_IsFinished" => item.RealTimeCR_IsFinished,
            "CR_CompletionType" => DisplayHelper.GetCompletionTypeText(item.CR_CompletionType),
            "CR_RollType" => DisplayHelper.GetRollTypeText(item.CR_RollType),
            "CR_SchedMachineNo" => item.CR_SchedMachineNo ?? "",
            "PlanIsFlow" => item.PlanIsFlow,
            "PlanFlowLevel" => item.PlanFlowLevelDisplay,
            "PlanFlowTarget" => DictValueDisplayHelper.GetText(DictValueDefaults.FlowTargetKey,item.PlanFlowTarget) ?? "",
            "PlanFlowCRType" => ProcessDisplayHelper.GetProcessNameText(item.PlanFlowCRType),
            "PlanOuterDiameterSpan" => item.PlanOuterDiameterSpan ?? "",
            "PlanFlowExecSpec" => item.PlanFlowExecSpec ?? "",
            "PlanExecutionSequence" => item.PlanExecutionSequence ?? 0,
            "PlanTargetSequence" => item.PlanTargetSequence,
            "PlanIsPaused" => item.PlanIsPaused,
            "IsGrabOrder" => item.IsGrabOrder,
            "PlanRemark" => item.PlanRemark ?? "",
            "ProductionFlowProperty" => DictValueDisplayHelper.GetText(DictValueDefaults.ProductionFlowKey,item.ProductionFlowProperty) ?? "",
            _ => ""
        })!;
    }
}
