using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Blazor.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.WorkOrders;

public partial class WorkOrderExecution
{
    private MudTable<WorkOrderExecutionSummaryDto>? table;
    private List<WorkOrderExecutionSummaryDto> _pageItems = new();
    private int _totalCount;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _pageSize = 10;
    private bool isRefreshing = false;
    private string _searchKeyword = string.Empty;

    // 排序状态
    private string sortColumn = "LastRefreshTime";
    private bool sortDescending = true;

    // 最后刷新时间
    private DateTime? lastRefreshTime;

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
            new() { Key = "WorkOrderNo",             Label = "工单号",          SortKey = "WorkOrderNo",             FilterType = "string", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "Salesman",                Label = "业务员",          SortKey = "Salesman",                FilterType = "string", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "CustomerName",            Label = "往来单位",        SortKey = "CustomerName",            FilterType = "string", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SignDate",                Label = "订单日期",        SortKey = "SignDate",                GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DeliveryDate",            Label = "交货日期",        SortKey = "DeliveryDate",            GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DelayPenalty",            Label = "延期罚款",        SortKey = "DelayPenalty",            FilterType = "boolean", BoolTrueLabel = "是", BoolFalseLabel = "否", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SettlementMethod",        Label = "结算方式",        SortKey = "SettlementMethod",        FilterType = "string", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "SalesOrderNo",            Label = "订单号",          SortKey = "SalesOrderNo",            FilterType = "string", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionMainNo",        Label = "主号",            SortKey = "ProductionMainNo",        FilterType = "string", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "ProductionSubNo",         Label = "次号",            SortKey = "ProductionSubNo",         FilterType = "string", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MaterialName",            Label = "物料名称",        SortKey = "MaterialName",            FilterType = "string", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "DeliveryState",           Label = "交货状态",        SortKey = "DeliveryState",           FilterType = "string", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "PlantGrade",              Label = "工厂牌号",        SortKey = "PlantGrade",              FilterType = "string", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "Specification",           Label = "规格",            SortKey = "Specification",           FilterType = "string", GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "LengthStatus",            Label = "长度状态",        SortKey = "LengthStatus",            FilterType = "string", Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MinLength",               Label = "最小长度",        SortKey = "MinLength",               Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "MaxLength",               Label = "最大长度",        SortKey = "MaxLength",               Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalItemCount",          Label = "含项次数",        SortKey = "TotalItemCount",          Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalQuantity",           Label = "总支数",          SortKey = "TotalQuantity",           Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalMeters",             Label = "总米数",          SortKey = "TotalMeters",             Visible = false, GroupKey = 1, GroupName = "基础数据" },
            new() { Key = "TotalWeight",             Label = "总重量",          SortKey = "TotalWeight",             Visible = false, GroupKey = 1, GroupName = "基础数据" },
        };

        // G2: 用料计划
        var g2 = new List<ColumnDef>
        {
            new() { Key = "LatestPlanDate",          Label = "计划截止日",      SortKey = "LatestPlanDate",          GroupKey = 2, GroupName = "用料计划" },
            new() { Key = "MaterialPlanRate",        Label = "满足率(%)",      SortKey = "MaterialPlanRate",                               GroupKey = 2, GroupName = "用料计划" },
            new() { Key = "MaterialPlanStatus",      Label = "用料计划状态",    SortKey = "MaterialPlanStatus",      FilterType = "enum", EnumOptions = new() { new("0","未计划"), new("1","部分"), new("2","理论满足"), new("3","满足"), new("4","超量") }, GroupKey = 2, GroupName = "用料计划" },
            new() { Key = "MainNoMaterialPlanRate",  Label = "主号满足率(%)",   SortKey = "MainNoMaterialPlanRate",  Visible = false,       GroupKey = 2, GroupName = "用料计划" },
            new() { Key = "MainNoMaterialPlanStatus",Label = "主号计划状态",    SortKey = "MainNoMaterialPlanStatus", FilterType = "enum", EnumOptions = new() { new("0","未计划"), new("1","部分"), new("2","理论满足"), new("3","满足"), new("4","超量") }, Visible = false, GroupKey = 2, GroupName = "用料计划" },
        };

        // G5: 物料执行实时信息
        var g5 = new List<ColumnDef>
        {
            new() { Key = "PendingRoughTubeQty",        Label = "待回荒管支",     SortKey = "PendingRoughTubeQty",                               GroupKey = 5, GroupName = "物料执行" },
            new() { Key = "PendingRoughTubeWeight",     Label = "待回荒管重",     SortKey = "PendingRoughTubeWeight",                            GroupKey = 5, GroupName = "物料执行" },
            new() { Key = "PendingOutsourceFinishQty",  Label = "待回外购成支",   SortKey = "PendingOutsourceFinishQty",                         GroupKey = 5, GroupName = "物料执行" },
            new() { Key = "PendingOutsourceFinishWeight",Label = "待回外购成重",  SortKey = "PendingOutsourceFinishWeight",                      GroupKey = 5, GroupName = "物料执行" },
            new() { Key = "TheoreticalFinishQty",        Label = "理论成品支",    SortKey = "TheoreticalFinishQty",                              GroupKey = 5, GroupName = "物料执行" },
            new() { Key = "TheoreticalFinishWeight",     Label = "理论成品重",    SortKey = "TheoreticalFinishWeight",                           GroupKey = 5, GroupName = "物料执行" },
        };

        // G6: 返整执行数据
        var g6 = new List<ColumnDef>
        {
            new() { Key = "ReworkInputEndDate",          Label = "返整投料截止日", SortKey = "ReworkInputEndDate",      GroupKey = 6, GroupName = "返整执行" },
            new() { Key = "ReworkBatchCount",            Label = "返整批次数",     SortKey = "ReworkBatchCount",                               GroupKey = 6, GroupName = "返整执行" },
            new() { Key = "ReworkInputQuantity",         Label = "返整投料支数",   SortKey = "ReworkInputQuantity",    Visible = false,       GroupKey = 6, GroupName = "返整执行" },
            new() { Key = "ReworkInputWeight",           Label = "返整投料重量",   SortKey = "ReworkInputWeight",      Visible = false,       GroupKey = 6, GroupName = "返整执行" },
            new() { Key = "ReworkTheoreticalOutputQty",  Label = "返整理论成品支", SortKey = "ReworkTheoreticalOutputQty",                      GroupKey = 6, GroupName = "返整执行" },
            new() { Key = "ReworkTheoreticalOutputWeight",Label = "返整理论成品重",SortKey = "ReworkTheoreticalOutputWeight",                   GroupKey = 6, GroupName = "返整执行" },
        };

        // G3: 原始投料
        var g3 = new List<ColumnDef>
        {
            new() { Key = "InputStartDate",          Label = "原始投料起始日",  SortKey = "InputStartDate",          GroupKey = 3, GroupName = "原始投料" },
            new() { Key = "InputEndDate",            Label = "原始投料截止日",  SortKey = "InputEndDate",            GroupKey = 3, GroupName = "原始投料" },
            new() { Key = "TotalBatchCount",         Label = "原始批次数",     SortKey = "TotalBatchCount",                                 GroupKey = 3, GroupName = "原始投料" },
            new() { Key = "InputQuantity",           Label = "原始总支数",      SortKey = "InputQuantity",           Visible = false,       GroupKey = 3, GroupName = "原始投料" },
            new() { Key = "InputWeight",             Label = "原始总重量",      SortKey = "InputWeight",             Visible = false,       GroupKey = 3, GroupName = "原始投料" },
            new() { Key = "TheoreticalOutputQty",    Label = "原始理论成品支",   SortKey = "TheoreticalOutputQty",    Visible = false,       GroupKey = 3, GroupName = "原始投料" },
            new() { Key = "TheoreticalOutputWeight", Label = "原始理论成品重",   SortKey = "TheoreticalOutputWeight", Visible = false,       GroupKey = 3, GroupName = "原始投料" },
            new() { Key = "InputOutputRatio",        Label = "原始成品比",     SortKey = "InputOutputRatio",                                GroupKey = 3, GroupName = "原始投料" },
            new() { Key = "InputStatus",             Label = "原始投料状态",    SortKey = "InputStatus",             FilterType = "enum", EnumOptions = new() { new("0","未投料"), new("1","部分"), new("2","满足") }, GroupKey = 3, GroupName = "原始投料" },
            new() { Key = "MainNoInputRatio",        Label = "原始主号比",     SortKey = "MainNoInputRatio",        Visible = false,       GroupKey = 3, GroupName = "原始投料" },
            new() { Key = "MainNoInputStatus",       Label = "原始主号状态",    SortKey = "MainNoInputStatus",       FilterType = "enum", EnumOptions = new() { new("0","未投料"), new("1","部分"), new("2","满足") }, Visible = false, GroupKey = 3, GroupName = "原始投料" },
        };

        // G4: 合格流转
        var g4 = new List<ColumnDef>
        {
            new() { Key = "ValidBatchCount",         Label = "有效批次数",     SortKey = "ValidBatchCount",                                 GroupKey = 4, GroupName = "合格流转" },
            new() { Key = "ValidInputQuantity",      Label = "有效流转总支数",  SortKey = "ValidInputQuantity",      Visible = false,       GroupKey = 4, GroupName = "合格流转" },
            new() { Key = "ValidInputWeight",        Label = "有效流转总重量",  SortKey = "ValidInputWeight",        Visible = false,       GroupKey = 4, GroupName = "合格流转" },
            new() { Key = "ValidOutputQty",          Label = "流转成品支数",   SortKey = "ValidOutputQty",          Visible = false,       GroupKey = 4, GroupName = "合格流转" },
            new() { Key = "ValidOutputWeight",       Label = "流转成品重量",   SortKey = "ValidOutputWeight",       Visible = false,       GroupKey = 4, GroupName = "合格流转" },
        };

        // G7: 有效流转
        var g7 = new List<ColumnDef>
        {
            new() { Key = "FlowOutputRatio",        Label = "流转成品比",     SortKey = "FlowOutputRatio",                                GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowStatus",             Label = "有效流转状态",    SortKey = "FlowStatus",             FilterType = "enum", EnumOptions = new() { new("0","未投料"), new("1","部分"), new("2","满足") }, GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "MainNoFlowRatio",        Label = "有效主号流转比", SortKey = "MainNoFlowOutputRatio",         Visible = false,       GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "MainNoFlowStatus",       Label = "有效主号状态",   SortKey = "MainNoFlowStatus",       FilterType = "enum", EnumOptions = new() { new("0","未计划"), new("1","部分"), new("2","满足") }, Visible = false, GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowTotalBatchCount",    Label = "总批次数",      SortKey = "FlowTotalBatchCount",                                 GroupKey = 7, GroupName = "有效流转" },
            new() { Key = "FlowIncompleteBatchCount",Label = "未完成批数",    SortKey = "FlowIncompleteBatchCount",                            GroupKey = 7, GroupName = "有效流转" },
        };

        // G8: 过程不合格
        var g8 = new List<ColumnDef>
        {
            new() { Key = "DefectiveRawQty",         Label = "原料不合格支数", SortKey = "DefectiveRawQty",                                 GroupKey = 8, GroupName = "过程不合格" },
            new() { Key = "DefectiveRawWeight",      Label = "原料不合格重量", SortKey = "DefectiveRawWeight",      Visible = false,       GroupKey = 8, GroupName = "过程不合格" },
            new() { Key = "DefectiveOutputQty",      Label = "影响成品支数",   SortKey = "DefectiveOutputQty",                              GroupKey = 8, GroupName = "过程不合格" },
            new() { Key = "DefectiveOutputWeight",   Label = "影响成品重量",   SortKey = "DefectiveOutputWeight",   Visible = false,       GroupKey = 8, GroupName = "过程不合格" },
            new() { Key = "DefectiveRatio",          Label = "不合格占比",     SortKey = "DefectiveRatio",                                   GroupKey = 8, GroupName = "过程不合格" },
        };

        // G9: 成检不合格
        var g9 = new List<ColumnDef>
        {
            new() { Key = "InspectionStartDate",     Label = "成检起始日",    SortKey = "InspectionStartDate",    GroupKey = 9, GroupName = "成检不合格" },
            new() { Key = "InspectionEndDate",       Label = "成检截止日",    SortKey = "InspectionEndDate",      GroupKey = 9, GroupName = "成检不合格" },
            new() { Key = "InspectionDefectQty",     Label = "成检不合格支数", SortKey = "InspectionDefectQty",                             GroupKey = 9, GroupName = "成检不合格" },
            new() { Key = "InspectionDefectWeight",  Label = "成检不合格重量", SortKey = "InspectionDefectWeight",  Visible = false,       GroupKey = 9, GroupName = "成检不合格" },
            new() { Key = "InspectionDefectRatio",   Label = "成检不合格占比", SortKey = "InspectionDefectRatio",                          GroupKey = 9, GroupName = "成检不合格" },
        };

        // G10: 汇总不合格
        var g10 = new List<ColumnDef>
        {
            new() { Key = "GeneralDefectWeight",     Label = "一般问题重",     SortKey = "GeneralDefectWeight",                             GroupKey = 10, GroupName = "汇总不合格" },
            new() { Key = "GeneralDefectRatio",      Label = "一般问题占比",   SortKey = "GeneralDefectRatio",                              GroupKey = 10, GroupName = "汇总不合格" },
            new() { Key = "SeriousDefectWeight",     Label = "严重问题重",     SortKey = "SeriousDefectWeight",    Visible = false,       GroupKey = 10, GroupName = "汇总不合格" },
            new() { Key = "SeriousDefectRatio",      Label = "严重问题占比",   SortKey = "SeriousDefectRatio",                              GroupKey = 10, GroupName = "汇总不合格" },
            new() { Key = "ScrapWeight",             Label = "成检报废重量",   SortKey = "ScrapWeight",            Visible = false,       GroupKey = 10, GroupName = "汇总不合格" },
            new() { Key = "ScrapRatio",              Label = "成检报废占比",   SortKey = "ScrapRatio",                                       GroupKey = 10, GroupName = "汇总不合格" },
        };

        // G11: 成品入库
        var g11 = new List<ColumnDef>
        {
            new() { Key = "WarehousingStartDate",    Label = "入库起始日",    SortKey = "WarehousingStartDate",    GroupKey = 11, GroupName = "成品入库" },
            new() { Key = "WarehousingEndDate",      Label = "入库截止日",    SortKey = "WarehousingEndDate",      GroupKey = 11, GroupName = "成品入库" },
            new() { Key = "WarehousingTotalQty",     Label = "入库总支数",    SortKey = "WarehousingTotalQty",                           GroupKey = 11, GroupName = "成品入库" },
            new() { Key = "WarehousingTotalWeight",  Label = "入库总重量",    SortKey = "WarehousingTotalWeight",                        GroupKey = 11, GroupName = "成品入库" },
            new() { Key = "WoWarehousingStatus",     Label = "工单入库状态",    SortKey = "WoWarehousingStatus",     FilterType = "enum", EnumOptions = new() { new("0","无入库"), new("1","入库部分"), new("2","入库完结") }, GroupKey = 11, GroupName = "成品入库" },
            new() { Key = "MainNoWarehousingStatus", Label = "主号入库状态",  SortKey = "MainNoWarehousingStatus", FilterType = "enum", EnumOptions = new() { new("0","无入库"), new("1","入库部分"), new("2","入库完结") }, Visible = false, GroupKey = 11, GroupName = "成品入库" },
            new() { Key = "OrderWarehousingStatus",  Label = "订单入库状态",  SortKey = "OrderWarehousingStatus", FilterType = "enum", EnumOptions = new() { new("0","无入库"), new("1","入库部分"), new("2","入库完结") }, Visible = false, GroupKey = 11, GroupName = "成品入库" },
        };

        // G12: 实时关注
        var g12 = new List<ColumnDef>
        {
            new() { Key = "ScheduleStage",           Label = "关注状态",      SortKey = "ScheduleStage",           FilterType = "enum", EnumOptions = new() { new("0","无需排产"), new("1","原料锁定"), new("2","生产执行"), new("3","成品检验") }, GroupKey = 12, GroupName = "实时关注" },
        };

        var all = new List<ColumnDef>();
        all.AddRange(g1);
        all.AddRange(g2);
        all.AddRange(g5);
        all.AddRange(g3);
        all.AddRange(g4);
        all.AddRange(g6);
        all.AddRange(g7);
        all.AddRange(g8);
        all.AddRange(g9);
        all.AddRange(g10);
        all.AddRange(g11);
        all.AddRange(g12);
        return all;
    }

    // ========== 服务端数据加载 ==========

    private async Task<TableData<WorkOrderExecutionSummaryDto>> LoadDataFromServer(TableState state)
    {
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

            var result = await WorkOrderExecutionService.GetPagedAsync(query);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;
                lastRefreshTime = _pageItems.Select(i => i.LastRefreshTime).DefaultIfEmpty().Max();
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
            {
                opt.Display = opt.Value == "True" ? "是" : "否";
            }
        }

        // 枚举字符串字段（DB 存储英文枚举值，筛选下拉显示中文）
        if (_filterContextOptions.TryGetValue("SettlementMethod", out var settlementOptions))
        {
            foreach (var opt in settlementOptions)
                opt.Display = DisplayHelper.GetSettlementMethodText(opt.Value);
        }
        if (_filterContextOptions.TryGetValue("MaterialName", out var materialOptions))
        {
            foreach (var opt in materialOptions)
                opt.Display = DisplayHelper.GetMaterialNameText(opt.Value);
        }
        if (_filterContextOptions.TryGetValue("DeliveryState", out var deliveryOptions))
        {
            foreach (var opt in deliveryOptions)
                opt.Display = DisplayHelper.GetDeliveryStateText(opt.Value);
        }
        if (_filterContextOptions.TryGetValue("LengthStatus", out var lengthOptions))
        {
            foreach (var opt in lengthOptions)
                opt.Display = DisplayHelper.GetLengthStatusText(opt.Value);
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

            var result = await WorkOrderExecutionService.GetPagedAsync(query);
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

    // ========== 即时更新 ==========

    private async Task RefreshAll()
    {
        isRefreshing = true;
        try
        {
            var result = await WorkOrderExecutionService.RefreshAllAsync();
            if (result.Success)
            {
                Snackbar.Add($"刷新完成，共{result.Data?.RefreshedCount ?? 0}条", Severity.Success);
            }
            else
            {
                Snackbar.Add(result.Message ?? "刷新失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"刷新失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            isRefreshing = false;
        }
        await LoadAllDataAsync();
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
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#workorder-execution-table"))
                _isArrowNavSetup = false;
        }

    }

    // ========== 单元格渲染 ==========

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
            case "TotalItemCount":
                builder.AddContent(0, item.TotalItemCount);
                break;
            case "TotalQuantity":
                builder.AddContent(0, item.TotalQuantity);
                break;
            case "TotalMeters":
                builder.AddContent(0, item.TotalMeters.ToString("G29"));
                break;
            case "TotalWeight":
                builder.AddContent(0, Math.Round(item.TotalWeight).ToString("F0"));
                break;
            case "LatestPlanDate":
                builder.AddContent(0, item.LatestPlanDate?.ToString("yyyy-MM-dd") ?? "-");
                break;
            case "MaterialPlanRate":
                builder.AddContent(0, item.MaterialPlanRate > 0 ? $"{item.MaterialPlanRate}%" : "-");
                break;
            case "MaterialPlanStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanStatusColor(item.MaterialPlanStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.MaterialPlanStatusText)));
                builder.CloseComponent();
                break;
            case "MainNoMaterialPlanRate":
                builder.AddContent(0, item.MainNoMaterialPlanRate > 0 ? $"{item.MainNoMaterialPlanRate}%" : "-");
                break;
            case "MainNoMaterialPlanStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetPlanStatusColor(item.MainNoMaterialPlanStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.MainNoMaterialPlanStatusText)));
                builder.CloseComponent();
                break;
            case "PendingRoughTubeQty":
                builder.AddContent(0, item.PendingRoughTubeQty > 0 ? item.PendingRoughTubeQty.ToString() : "-");
                break;
            case "PendingRoughTubeWeight":
                builder.AddContent(0, item.PendingRoughTubeWeight > 0 ? Math.Round(item.PendingRoughTubeWeight).ToString("F0") : "-");
                break;
            case "PendingOutsourceFinishQty":
                builder.AddContent(0, item.PendingOutsourceFinishQty > 0 ? item.PendingOutsourceFinishQty.ToString() : "-");
                break;
            case "PendingOutsourceFinishWeight":
                builder.AddContent(0, item.PendingOutsourceFinishWeight > 0 ? Math.Round(item.PendingOutsourceFinishWeight).ToString("F0") : "-");
                break;
            case "TheoreticalFinishQty":
                builder.AddContent(0, item.TheoreticalFinishQty > 0 ? item.TheoreticalFinishQty.ToString("G29") : "-");
                break;
            case "TheoreticalFinishWeight":
                builder.AddContent(0, item.TheoreticalFinishWeight > 0 ? Math.Round(item.TheoreticalFinishWeight).ToString("F0") : "-");
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
                builder.AddContent(0, Math.Round(item.ReworkInputWeight).ToString("F0"));
                break;
            case "ReworkTheoreticalOutputQty":
                builder.AddContent(0, item.ReworkTheoreticalOutputQty.ToString("G29"));
                break;
            case "ReworkTheoreticalOutputWeight":
                builder.AddContent(0, Math.Round(item.ReworkTheoreticalOutputWeight).ToString("F0"));
                break;
            case "FlowOutputRatio":
                builder.AddContent(0, item.FlowOutputRatio > 0 ? $"{item.FlowOutputRatio}%" : "-");
                break;
            case "FlowStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetInputStatusColor(item.FlowStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.FlowStatusText)));
                builder.CloseComponent();
                break;
            case "MainNoFlowRatio":
                builder.AddContent(0, item.MainNoFlowOutputRatio > 0 ? $"{item.MainNoFlowOutputRatio}%" : "-");
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
                builder.AddContent(0, Math.Round(item.InputWeight).ToString("F0"));
                break;
            case "TheoreticalOutputQty":
                builder.AddContent(0, item.TheoreticalOutputQty.ToString("G29"));
                break;
            case "TheoreticalOutputWeight":
                builder.AddContent(0, Math.Round(item.TheoreticalOutputWeight).ToString("F0"));
                break;
            case "InputOutputRatio":
                builder.AddContent(0, item.InputOutputRatio > 0 ? $"{item.InputOutputRatio}%" : "-");
                break;
            case "InputStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetInputStatusColor(item.InputStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.InputStatusText)));
                builder.CloseComponent();
                break;
            case "MainNoInputRatio":
                builder.AddContent(0, item.MainNoInputOutputRatio > 0 ? $"{item.MainNoInputOutputRatio}%" : "-");
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
                builder.AddContent(0, Math.Round(item.ValidInputWeight).ToString("F0"));
                break;
            case "ValidOutputQty":
                builder.AddContent(0, item.ValidOutputQty.ToString("G29"));
                break;
            case "ValidOutputWeight":
                builder.AddContent(0, Math.Round(item.ValidOutputWeight).ToString("F0"));
                break;

            // ========== G8: 过程不合格 ==========
            case "DefectiveRawQty":
                builder.AddContent(0, item.DefectiveRawQty > 0 ? item.DefectiveRawQty.ToString() : "-");
                break;
            case "DefectiveRawWeight":
                builder.AddContent(0, item.DefectiveRawWeight > 0 ? Math.Round(item.DefectiveRawWeight).ToString("F0") : "-");
                break;
            case "DefectiveOutputQty":
                builder.AddContent(0, item.DefectiveOutputQty > 0 ? item.DefectiveOutputQty.ToString("G29") : "-");
                break;
            case "DefectiveOutputWeight":
                builder.AddContent(0, item.DefectiveOutputWeight > 0 ? Math.Round(item.DefectiveOutputWeight).ToString("F0") : "-");
                break;
            case "DefectiveRatio":
                builder.AddContent(0, item.DefectiveRatio > 0 ? $"{item.DefectiveRatio}%" : "-");
                break;

            // ========== G9: 成检不合格 ==========
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
                builder.AddContent(0, item.InspectionDefectWeight > 0 ? Math.Round(item.InspectionDefectWeight).ToString("F0") : "-");
                break;
            case "InspectionDefectRatio":
                builder.AddContent(0, item.InspectionDefectRatio > 0 ? $"{item.InspectionDefectRatio}%" : "-");
                break;

            // ========== G10: 汇总不合格 ==========
            case "GeneralDefectWeight":
                builder.AddContent(0, item.GeneralDefectWeight > 0 ? Math.Round(item.GeneralDefectWeight).ToString("F0") : "-");
                break;
            case "GeneralDefectRatio":
                builder.AddContent(0, item.GeneralDefectRatio > 0 ? $"{item.GeneralDefectRatio}%" : "-");
                break;
            case "SeriousDefectWeight":
                builder.AddContent(0, item.SeriousDefectWeight > 0 ? Math.Round(item.SeriousDefectWeight).ToString("F0") : "-");
                break;
            case "SeriousDefectRatio":
                builder.AddContent(0, item.SeriousDefectRatio > 0 ? $"{item.SeriousDefectRatio}%" : "-");
                break;
            case "ScrapWeight":
                builder.AddContent(0, item.ScrapWeight > 0 ? Math.Round(item.ScrapWeight).ToString("F0") : "-");
                break;
            case "ScrapRatio":
                builder.AddContent(0, item.ScrapRatio > 0 ? $"{item.ScrapRatio}%" : "-");
                break;

            // ========== G11: 成品入库 ==========
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
                builder.AddContent(0, item.WarehousingTotalWeight > 0 ? Math.Round(item.WarehousingTotalWeight).ToString("F0") : "-");
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
            case "ScheduleStage":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetScheduleStageColor(item.ScheduleStage));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.ScheduleStageText)));
                builder.CloseComponent();
                break;
        }
    };

    // ========== 颜色 ==========

    // ========== 分组 CSS class ==========

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1",
            2 => "col-g2",
            5 => "col-g5",
            3 => "col-g3",
            4 => "col-g4",
            6 => "col-g6",
            7 => "col-g7",
            8 => "col-g8",
            9 => "col-g9",
            10 => "col-g10",
            11 => "col-g11",
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
            4 => "col-g4-cell",
            6 => "col-g6-cell",
            7 => "col-g7-cell",
            8 => "col-g8-cell",
            9 => "col-g9-cell",
            10 => "col-g10-cell",
            11 => "col-g11-cell",
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

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

        // 列显隐持久化
        extras["columnVisibility"] = JsonSerializer.Serialize(_allColumns.Where(c => c.Visible).Select(c => c.Key).ToList());

        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("workorderexecution", state);
    }
}
