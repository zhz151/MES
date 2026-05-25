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

namespace MES.Blazor.Pages;

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

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "WorkOrderNo",             Label = "工单号",          SortKey = "WorkOrderNo",             FilterType = "string" },
        new() { Key = "Salesman",                Label = "业务员",          SortKey = "Salesman",                FilterType = "string" },
        new() { Key = "CustomerName",            Label = "往来单位",        SortKey = "CustomerName",            FilterType = "string" },
        new() { Key = "SignDate",                Label = "订单日期",        SortKey = "SignDate", FilterType = "date" },
        new() { Key = "DeliveryDate",            Label = "交货日期",        SortKey = "DeliveryDate", FilterType = "date" },
        new() { Key = "DelayPenalty",            Label = "延期罚款",        SortKey = "DelayPenalty",            FilterType = "boolean", BoolTrueLabel = "是", BoolFalseLabel = "否" },
        new() { Key = "SettlementMethod",        Label = "结算方式",        SortKey = "SettlementMethod",        FilterType = "string", Visible = false },
        new() { Key = "SalesOrderNo",            Label = "订单号",          SortKey = "SalesOrderNo",            FilterType = "string" },
        new() { Key = "ProductionMainNo",        Label = "主号",            SortKey = "ProductionMainNo",        FilterType = "string" },
        new() { Key = "ProductionSubNo",         Label = "次号",            SortKey = "ProductionSubNo",         FilterType = "string", Visible = false },
        new() { Key = "MaterialName",            Label = "物料名称",        SortKey = "MaterialName",            FilterType = "string" },
        new() { Key = "DeliveryState",           Label = "交货状态",        SortKey = "DeliveryState",           FilterType = "string", Visible = false },
        new() { Key = "PlantGrade",              Label = "工厂牌号",        SortKey = "PlantGrade",              FilterType = "string" },
        new() { Key = "Specification",           Label = "规格",            SortKey = "Specification",           FilterType = "string" },
        new() { Key = "LengthStatus",            Label = "长度状态",        SortKey = "LengthStatus",            FilterType = "string", Visible = false },
        new() { Key = "MinLength",               Label = "最小长度",        SortKey = "MinLength", Visible = false },
        new() { Key = "MaxLength",               Label = "最大长度",        SortKey = "MaxLength", Visible = false },
        new() { Key = "TotalItemCount",          Label = "含项次数",        SortKey = "TotalItemCount", Visible = false },
        new() { Key = "TotalQuantity",           Label = "总支数",          SortKey = "TotalQuantity", Visible = false },
        new() { Key = "TotalMeters",             Label = "总米数",          SortKey = "TotalMeters", Visible = false },
        new() { Key = "TotalWeight",             Label = "总重量",          SortKey = "TotalWeight", Visible = false },
        new() { Key = "LatestPlanDate",          Label = "计划截止日",      SortKey = "LatestPlanDate", FilterType = "date" },
        new() { Key = "MaterialPlanRate",        Label = "满足率(%)",      SortKey = "MaterialPlanRate" },
        new() { Key = "MaterialPlanStatus",      Label = "用料计划状态" },
        new() { Key = "MainNoMaterialPlanRate",  Label = "主号满足率(%)",   SortKey = "MainNoMaterialPlanRate", Visible = false },
        new() { Key = "MainNoMaterialPlanStatus",Label = "主号计划状态", Visible = false },
        new() { Key = "InputStartDate",          Label = "投料起始日",      SortKey = "InputStartDate", FilterType = "date" },
        new() { Key = "InputEndDate",            Label = "投料截止日",      SortKey = "InputEndDate", FilterType = "date" },
        new() { Key = "TotalBatchCount",         Label = "批次数",         SortKey = "TotalBatchCount" },
        new() { Key = "InputQuantity",           Label = "投料总支数",      SortKey = "InputQuantity", Visible = false },
        new() { Key = "InputWeight",             Label = "投料总重量",      SortKey = "InputWeight", Visible = false },
        new() { Key = "TheoreticalOutputQty",    Label = "理论成品支数",   SortKey = "TheoreticalOutputQty", Visible = false },
        new() { Key = "TheoreticalOutputWeight", Label = "理论成品重量",   SortKey = "TheoreticalOutputWeight", Visible = false },
        new() { Key = "InputOutputRatio",        Label = "投料成品比",     SortKey = "InputOutputRatio" },
        new() { Key = "InputStatus",             Label = "投料状态" },
        new() { Key = "MainNoInputRatio",        Label = "主号投料比",     SortKey = "MainNoInputRatio", Visible = false },
        new() { Key = "MainNoInputStatus",       Label = "主号投料状态", Visible = false },
        new() { Key = "ValidBatchCount",         Label = "有效批次数",     SortKey = "ValidBatchCount" },
        new() { Key = "ValidInputQuantity",      Label = "有效投料总支数",  SortKey = "ValidInputQuantity", Visible = false },
        new() { Key = "ValidInputWeight",        Label = "有效投料总重量",  SortKey = "ValidInputWeight", Visible = false },
        new() { Key = "ValidOutputQty",          Label = "有效成品支数",   SortKey = "ValidOutputQty", Visible = false },
        new() { Key = "ValidOutputWeight",       Label = "有效成品重量",   SortKey = "ValidOutputWeight", Visible = false },
        new() { Key = "ValidInputOutputRatio",   Label = "有效成品比",     SortKey = "ValidInputOutputRatio" },
        new() { Key = "ValidInputStatus",        Label = "有效投料状态" },
        new() { Key = "MainNoValidInputRatio",   Label = "有效主号投料比", SortKey = "MainNoValidInputRatio", Visible = false },
        new() { Key = "MainNoValidInputStatus",  Label = "有效主号状态",   Visible = false },
    };

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
            case "ValidInputOutputRatio":
                builder.AddContent(0, item.ValidInputOutputRatio > 0 ? $"{item.ValidInputOutputRatio}%" : "-");
                break;
            case "ValidInputStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetInputStatusColor(item.ValidInputStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.ValidInputStatusText)));
                builder.CloseComponent();
                break;
            case "MainNoValidInputRatio":
                builder.AddContent(0, item.MainNoValidInputOutputRatio > 0 ? $"{item.MainNoValidInputOutputRatio}%" : "-");
                break;
            case "MainNoValidInputStatus":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", GetValidMainNoStatusColor(item.MainNoValidInputStatus));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.MainNoValidInputStatusText)));
                builder.CloseComponent();
                break;
        }
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

    private static Color GetValidMainNoStatusColor(int status) => status switch
    {
        0 => Color.Default,
        1 => Color.Warning,
        2 => Color.Success,
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
