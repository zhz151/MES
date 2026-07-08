using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Helpers;
using MES.Blazor.Shared;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.Quality;

[Authorize(Roles = Roles.Policies.QualityRead)]
public partial class Ncrs
{
    [Inject] private NcrService NcrService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private PageStateService PageState { get; set; } = null!;
    [Inject] private ColumnPrefsService ColumnPrefs { get; set; } = null!;

    private MudTable<NcrDto>? table;
    private List<NcrDto> _pageItems = new();
    private int _totalCount;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty;
    private string sortColumn = "reportdate";
    private bool sortDescending = true;
    private bool _isFirstLoad = true;
    private int _restoredPageIndex;

    // 待处理卡片
    private List<NcrPendingCheckDto> _pendingItems = new();
    private bool _showPending = false;

    // 筛选
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // 列定义
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new()
    {
        "DefectiveQuantity"
    };

    // 扩展常量
    private const string PageType = "ncrs";

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // G1: 问题反馈
        new() { Key = "ReportDate",          Label = "反馈日期",    SortKey = "reportdate",       FilterType = "date",   Width = "100",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "ReportDepartment",     Label = "反馈部门",    SortKey = "reportdepartment",  FilterType = "string", Width = "100",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "Reporter",             Label = "反馈人",      SortKey = "reporter",          FilterType = "string", Width = "80",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "PipeCategory",         Label = "钢管类别",    SortKey = "pipecategory",      FilterType = "enum",   Width = "100",
               GroupKey = 1, GroupName = "G1 问题反馈",
               EnumOptions = new List<EnumOption>
               {
                   new("TubeBlank", "荒管"), new("Intermediate", "中间品"), new("SurplusInventory", "余库料"),
                   new("CriticalFinished", "临界成品"), new("OrderFinished", "订单成品"), new("SpecialDelivery", "特定交态成品"),
               } },
        new() { Key = "BatchNo",              Label = "生产编号",    SortKey = "batchno",           FilterType = "string", Width = "120",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "WorkOrderNo",          Label = "工单号",      SortKey = "workorderno",       FilterType = "string", Width = "120",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "PlantGrade",           Label = "牌号",        SortKey = "plantgrade",        FilterType = "string", Width = "80",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "Specification",        Label = "规格",        SortKey = "specification",     FilterType = "string", Width = "100",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "DefectiveQuantity",    Label = "不合格支数",  SortKey = "defectivequantity",                      Width = "80",
               GroupKey = 1, GroupName = "G1 问题反馈" },
        new() { Key = "ProblemDescription",   Label = "问题描述",    SortKey = "problemdescription",FilterType = "string", Width = "150",
               GroupKey = 1, GroupName = "G1 问题反馈" },

        // G2: 不合格品处置
        new() { Key = "DisposalMethod",       Label = "处置方式",    SortKey = "disposalmethod",     FilterType = "enum",  Width = "100",
               GroupKey = 2, GroupName = "G2 不合格品处置",
               EnumOptions = new List<EnumOption>
               {
                   new("Rework", "返整"), new("WarehouseEntry", "入库"), new("Scrap", "报废"),
               } },
        new() { Key = "DisposalIsCompleted",  Label = "处置完结",    SortKey = "disposaliscompleted", FilterType = "boolean", Width = "70",
               GroupKey = 2, GroupName = "G2 不合格品处置",
               BoolTrueLabel = "是", BoolFalseLabel = "否" },
        new() { Key = "DisposalCompleteDate", Label = "处置完结日期",SortKey = "disposalcompletedate", FilterType = "date",  Width = "100",
               GroupKey = 2, GroupName = "G2 不合格品处置" },

        // G3: 原因分析
        new() { Key = "Severity",             Label = "严重程度",    SortKey = "severity",           FilterType = "enum",   Width = "80",
               GroupKey = 3, GroupName = "G3 原因分析",
               EnumOptions = new List<EnumOption> { new("Critical", "严重"), new("General", "一般") } },
        new() { Key = "RootCauseAnalysis",    Label = "原因分析",    SortKey = "rootcauseanalysis",   FilterType = "string", Width = "150",
               GroupKey = 3, GroupName = "G3 原因分析" },
        new() { Key = "AnalysisConfirmer",    Label = "分析确认人",  SortKey = "analysisconfirmer",   FilterType = "string", Width = "100",
               GroupKey = 3, GroupName = "G3 原因分析" },
        new() { Key = "AnalysisConfirmDate",  Label = "确认日期",    SortKey = "analysisconfirmdate", FilterType = "date",   Width = "100",
               GroupKey = 3, GroupName = "G3 原因分析" },

        // G4: 责任人及处理
        new() { Key = "ResponsibilityCategory", Label = "责任类别",  SortKey = "responsibilitycategory", FilterType = "enum", Width = "110",
               GroupKey = 4, GroupName = "G4 责任人及处理",
               EnumOptions = new List<EnumOption>
               {
                   new("ProductionInternal", "生产-厂内"), new("ProductionOutsource", "生产-外协"),
                   new("MaterialTubeBlank", "原料-荒管"), new("MaterialPurchased", "原料-外购成品"),
                   new("MaterialSurplus", "原料-余库料"),
               } },
        new() { Key = "ResponsibleDept",      Label = "责任部门",    SortKey = "responsibledept",     FilterType = "string", Width = "120",
               GroupKey = 4, GroupName = "G4 责任人及处理" },
        new() { Key = "ResponsiblePerson",    Label = "责任人",      SortKey = "responsibleperson",   FilterType = "string", Width = "80",
               GroupKey = 4, GroupName = "G4 责任人及处理" },
        new() { Key = "PersonIsCompleted",    Label = "追责完结",    SortKey = "personiscompleted",   FilterType = "boolean", Width = "70",
               GroupKey = 4, GroupName = "G4 责任人及处理",
               BoolTrueLabel = "是", BoolFalseLabel = "否" },

        // G5: 纠正预防措施及结果验证
        new() { Key = "CorrectiveAction",     Label = "纠正预防措施",SortKey = "correctiveaction",    FilterType = "string", Width = "150",
               GroupKey = 5, GroupName = "G5 纠正预防措施" },
        new() { Key = "ActionPlanner",        Label = "计划人",      SortKey = "actionplanner",       FilterType = "string", Width = "80",
               GroupKey = 5, GroupName = "G5 纠正预防措施" },
        new() { Key = "ActionVerifier",       Label = "验证人",      SortKey = "actionverifier",      FilterType = "string", Width = "80",
               GroupKey = 5, GroupName = "G5 纠正预防措施" },
        new() { Key = "VerifyResult",         Label = "验证结论",    SortKey = "verifyresult",        FilterType = "enum",   Width = "100",
               GroupKey = 5, GroupName = "G5 纠正预防措施",
               EnumOptions = new List<EnumOption>
               {
                   new("Passed", "通过"), new("NeedsRectification", "需整改"), new("NotApplicable", "不适用"),
               } },
        new() { Key = "ActionResult",         Label = "结果判定",    SortKey = "actionresult",        FilterType = "string", Width = "120",
               GroupKey = 5, GroupName = "G5 纠正预防措施" },

        // 状态
        new() { Key = "Status",               Label = "状态",        SortKey = "status",              FilterType = "enum",   Width = "80",
               EnumOptions = new List<EnumOption>
               {
                   new("Processing", "处理中"), new("Closed", "已关闭"),
               } },

        // 审计
        new() { Key = "UpdatedTime",          Label = "更新日期",    SortKey = "updatedtime",         Width = "120" },
    };

    // ========== 生命周期 ==========

    protected override async Task OnInitializedAsync()
    {
        // 初始化列定义
        _allColumns = GetAllColumnDefs();

        // 恢复列偏好（合并保存的可见性/排序，不替换）
        var savedCols = await ColumnPrefs.LoadAsync(PageType, null);
        if (savedCols.Count > 0)
        {
            foreach (var s in savedCols)
            {
                var match = _allColumns.FirstOrDefault(c => c.Key == s.Key);
                if (match != null)
                    match.Visible = s.Visible;
            }
            var reordered = new List<ColumnDef>();
            foreach (var s in savedCols)
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

        // 恢复页面状态
        var savedState = await PageState.LoadAsync(PageType);
        if (savedState != null)
        {
            _searchKeyword = savedState.Keyword ?? "";
            sortColumn = savedState.SortBy ?? "reportdate";
            sortDescending = savedState.IsDescending;
            _restoredPageIndex = savedState.PageIndex;
            if (savedState.Filters?.Count > 0)
            {
                _columnFilters = savedState.Filters
                    .Where(f => f.Values?.Count > 0)
                    .ToDictionary(f => f.Field, f => new HashSet<string>(f.Values!));
            }
        }

        // 加载筛选上下文
        await Task.WhenAll(
            LoadFilterContextsAsync(),
            LoadPendingChecksAsync()
        );
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (table != null)
                await table.ReloadServerData();
            await JS.InvokeVoidAsync("initGroupHeaders", "#ncrs-list-table");
        }
    }

    // ========== 数据加载 ==========

    private async Task<TableData<NcrDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "reportdate";
            var filtersJson = SerializeFilters();

            var result = await NcrService.GetAllAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
                filters: filtersJson);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                ComputePageSums();
            }
            else
            {
                _pageItems = new();
                _totalCount = 0;
                _pageSums.Clear();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
            _pageSums.Clear();
        }

        return new TableData<NcrDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
    }

    // ========== 搜索 ==========

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 排序 ==========

    private async Task ToggleSort(string sortKey)
    {
        if (sortColumn == sortKey)
            sortDescending = !sortDescending;
        else
        {
            sortColumn = sortKey;
            sortDescending = true;
        }
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 筛选 ==========

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
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

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await NcrService.GetFilterContextsAsync();
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

        // 补充枚举列（后端不返回枚举 DISTINCT 值）
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

        // 补充布尔列
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

    // ========== 操作 ==========

    private void CreateNew()
    {
        Navigation.NavigateTo("/quality/ncr/create");
    }

    private void EditItem(int id)
    {
        Navigation.NavigateTo($"/quality/ncr/{id}");
    }

    private void PrintItem(int id)
    {
        Navigation.NavigateTo($"/quality/ncr/print/{id}");
    }

    private async Task DeleteItem(int id)
    {
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("确认删除",
            new DialogParameters { ["ContentText"] = "确定要删除该不合格品报告吗？" });
        var result = await dialog.Result;
        if (result.Canceled) return;

        var response = await NcrService.DeleteAsync(id);
        if (response.Success)
        {
            Snackbar.Add("删除成功", Severity.Success);
            if (table != null) await table.ReloadServerData();
        }
        else
        {
            Snackbar.Add($"删除失败: {response.Message}", Severity.Error);
        }
    }

    private async Task UpdateStatus(int id, NcrStatus status)
    {
        var statusText = status switch
        {
            NcrStatus.Processing => "处理中",
            NcrStatus.Closed => "已关闭",
            _ => ""
        };
        if (string.IsNullOrEmpty(statusText)) return;

        var dialog = await DialogService.ShowAsync<ConfirmDialog>("确认状态变更",
            new DialogParameters { ["ContentText"] = $"确定要将状态变更为「{statusText}」吗？" });
        var result = await dialog.Result;
        if (result.Canceled) return;

        var response = await NcrService.UpdateStatusAsync(id, status.ToString());
        if (response.Success)
        {
            Snackbar.Add($"状态已变更为: {statusText}", Severity.Success);
            if (table != null) await table.ReloadServerData();
        }
        else
        {
            Snackbar.Add($"状态变更失败: {response.Message}", Severity.Error);
        }
    }

    // ========== 列选择器 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await ColumnPrefs.SaveAsync(PageType, null, _allColumns);
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        var idx = _allColumns.IndexOf(col);
        if (idx > 0)
        {
            _allColumns.RemoveAt(idx);
            _allColumns.Insert(idx - 1, col);
        }
        await ColumnPrefs.SaveAsync(PageType, null, _allColumns);
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        var idx = _allColumns.IndexOf(col);
        if (idx < _allColumns.Count - 1)
        {
            _allColumns.RemoveAt(idx);
            _allColumns.Insert(idx + 1, col);
        }
        await ColumnPrefs.SaveAsync(PageType, null, _allColumns);
    }

    // ========== 分页汇总（B33） ==========

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;

        var props = typeof(NcrDto)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);

        foreach (var key in _summableColumnKeys)
        {
            if (!props.TryGetValue(key, out var prop)) continue;
            var type = prop.PropertyType;
            try
            {
                if (type == typeof(int) || type == typeof(int?))
                {
                    var sum = _pageItems.Sum(item =>
                    {
                        var v = prop.GetValue(item);
                        return v != null ? Convert.ToInt32(v) : 0;
                    });
                    _pageSums[key] = sum.ToString();
                }
                else if (type == typeof(decimal?) || type == typeof(decimal))
                {
                    var sum = _pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[key] = ((int)sum).ToString();
                }
            }
            catch { }
        }
    }

    private string RenderFooterCell(ColumnDef col)
    {
        return _pageSums.GetValueOrDefault(col.Key, "");
    }

    // ========== 页面状态持久化 ==========

    private async Task SavePageStateAsync()
    {
        await PageState.SaveAsync(PageType, new PageState
        {
            Keyword = _searchKeyword,
            SortBy = sortColumn,
            IsDescending = sortDescending,
            PageIndex = table?.CurrentPage ?? 0,
            Filters = _columnFilters.Count > 0
                ? _columnFilters.Select(kvp => new FilterDescriptor
                {
                    Field = kvp.Key,
                    Operator = "in",
                    Values = kvp.Value.ToList()
                }).ToList()
                : null
        });
    }

    // ========== 待处理卡片 ==========

    private async Task LoadPendingChecksAsync()
    {
        try
        {
            var result = await NcrService.GetPendingChecksAsync();
            if (result.Success && result.Data != null)
                _pendingItems = result.Data;
        }
        catch { }
    }

    private void TogglePendingChecks() => _showPending = !_showPending;

    private void CreateFromPending(NcrPendingCheckDto item)
    {
        Navigation.NavigateTo($"/quality/ncr/create?batchNo={Uri.EscapeDataString(item.BatchNo)}" +
            $"&disposalMethod={item.DisposalMethod}" +
            $"&sourceType={item.SourceType}" +
            $"&defectQty={item.DefectQuantity}" +
            $"&inspector={Uri.EscapeDataString(item.Inspector ?? "")}" +
            $"&inspectionItem={Uri.EscapeDataString(item.InspectionItem ?? "")}" +
            $"&processName={Uri.EscapeDataString(item.ProcessName ?? "")}" +
            $"&materialName={Uri.EscapeDataString(item.MaterialName ?? "")}" +
            $"&reportDate={item.ReportDate:yyyy-MM-dd}" +
            $"&defectDescription={Uri.EscapeDataString(item.DefectDescription ?? "")}");
    }

    private static string GetSourceTypeText(string sourceType) => sourceType switch
    {
        "ProcessInspection" => "过程检验",
        "FinalInspection" => "成品检验",
        _ => sourceType
    };

    private static string GetInspectionItemDisplay(string? item, string? sourceType)
    {
        if (string.IsNullOrEmpty(item)) return "";
        if (sourceType == "FinalInspection" && Enum.TryParse<InspectionItem>(item, out var enumItem))
            return DisplayHelper.GetInspectionItemText(enumItem);
        return item;
    }

    private static string GetDisposalMethodText(DisposalMethod method) => method switch
    {
        DisposalMethod.Rework => "返整",
        DisposalMethod.WarehouseEntry => "入库",
        DisposalMethod.Scrap => "报废",
        _ => method.ToString()
    };

    private static Color GetWarningColor() => Color.Warning;

    private static Color GetSourceTypeColor(string sourceType)
        => sourceType == "ProcessInspection" ? Color.Info : Color.Primary;

    private static Color GetDisposalChipColor(DisposalMethod method) => method switch
    {
        DisposalMethod.Rework => Color.Warning,
        DisposalMethod.WarehouseEntry => Color.Info,
        DisposalMethod.Scrap => Color.Error,
        _ => Color.Default
    };

    // ========== 列显示重置 ==========

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await ColumnPrefs.SaveAsync(PageType, null, _allColumns);
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
        StateHasChanged();
    }

    // ========== 分组 CSS ==========

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
        // 操作列尾随占位符（160px）
        result.Add(new GroupHeaderInfo
        {
            GroupKey = 0, GroupName = "", TotalWidth = 160, ColumnCount = 0, CssClass = ""
        });
        return result;
    }

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch
        {
            1 => "col-g1",
            2 => "col-g2",
            3 => "col-g3",
            4 => "col-g4",
            5 => "col-g5",
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
            _ => ""
        };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    // ========== 显示格式化 ==========

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(NcrDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "Status":
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Color", GetStatusColor(item.Status));
                builder.AddAttribute(2, "Size", Size.Small);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b => b.AddContent(0, GetStatusText(item.Status))));
                builder.CloseComponent();
                break;
            case "PipeCategory":
                builder.AddContent(0, GetPipeCategoryText(item.PipeCategory));
                break;
            case "DisposalMethod":
                builder.AddContent(0, GetDisposalMethodText(item.DisposalMethod));
                break;
            case "Severity":
                if (item.Severity != null)
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Color", GetSeverityColor(item.Severity));
                    builder.AddAttribute(2, "Size", Size.Small);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b => b.AddContent(0, GetSeverityText(item.Severity))));
                    builder.CloseComponent();
                }
                break;
            case "ResponsibilityCategory":
                builder.AddContent(0, GetResponsibilityCategoryText(item.ResponsibilityCategory));
                break;
            case "VerifyResult":
                builder.AddContent(0, GetVerifyResultText(item.VerifyResult));
                break;
            case "DisposalIsCompleted":
                builder.AddContent(0, item.DisposalIsCompleted ? "是" : "否");
                break;
            case "PersonIsCompleted":
                builder.AddContent(0, item.PersonIsCompleted ? "是" : "否");
                break;
            case "ReportDate":
                builder.AddContent(0, item.ReportDate.ToString("yyyy-MM-dd"));
                break;
            case "DisposalCompleteDate":
            case "AnalysisConfirmDate":
            case "ActionPlanDate":
            case "ActionVerifyDate":
            case "PersonCompleteDate":
                builder.AddContent(0, GetDateValue(item, col.Key));
                break;
            case "UpdatedTime":
                builder.AddContent(0, item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;
            default:
                var val = GetPropertyValue(item, col.Key);
                builder.AddContent(0, val ?? "");
                break;
        }
    };

    private static string? GetDateValue(NcrDto item, string key) => key switch
    {
        "DisposalCompleteDate" => item.DisposalCompleteDate?.ToString("yyyy-MM-dd"),
        "AnalysisConfirmDate" => item.AnalysisConfirmDate?.ToString("yyyy-MM-dd"),
        "ActionPlanDate" => item.ActionPlanDate?.ToString("yyyy-MM-dd"),
        "ActionVerifyDate" => item.ActionVerifyDate?.ToString("yyyy-MM-dd"),
        "PersonCompleteDate" => item.PersonCompleteDate?.ToString("yyyy-MM-dd"),
        _ => null
    };

    private static string? GetPropertyValue(NcrDto item, string key)
    {
        var prop = typeof(NcrDto).GetProperty(key);
        if (prop == null) return null;
        var val = prop.GetValue(item);
        return val?.ToString();
    }

    private static Color GetStatusColor(NcrStatus status) => status switch
    {
        NcrStatus.Pending => Color.Info,
        NcrStatus.Processing => Color.Warning,
        NcrStatus.Closed => Color.Success,
        _ => Color.Default
    };

    private string GetStatusText(NcrStatus status) => status switch
    {
        NcrStatus.Pending => "待处理",
        NcrStatus.Processing => "处理中",
        NcrStatus.Closed => "已关闭",
        _ => status.ToString()
    };

    private string GetPipeCategoryText(PipeCategory category) => category switch
    {
        PipeCategory.TubeBlank => "荒管",
        PipeCategory.Intermediate => "中间品",
        PipeCategory.SurplusInventory => "余库料",
        PipeCategory.CriticalFinished => "临界成品",
        PipeCategory.OrderFinished => "订单成品",
        PipeCategory.SpecialDelivery => "特定交态成品",
        _ => category.ToString()
    };

    private string GetDisposalMethodText(DisposalMethod? method) => method switch
    {
        DisposalMethod.Rework => "返整",
        DisposalMethod.WarehouseEntry => "入库",
        DisposalMethod.Scrap => "报废",
        _ => ""
    };

    private string GetSeverityText(SeverityLevel? severity) => severity switch
    {
        SeverityLevel.Critical => "严重",
        SeverityLevel.General => "一般",
        _ => ""
    };

    private string GetResponsibilityCategoryText(ResponsibilityCategory? category) => category switch
    {
        ResponsibilityCategory.ProductionInternal => "生产-厂内",
        ResponsibilityCategory.ProductionOutsource => "生产-外协",
        ResponsibilityCategory.MaterialTubeBlank => "原料-荒管",
        ResponsibilityCategory.MaterialPurchased => "原料-外购成品",
        ResponsibilityCategory.MaterialSurplus => "原料-余库料",
        _ => ""
    };

    private string GetVerifyResultText(VerifyResult? result) => result switch
    {
        VerifyResult.Passed => "通过",
        VerifyResult.NeedsRectification => "需整改",
        VerifyResult.NotApplicable => "不适用",
        _ => ""
    };

    private static Color GetSeverityColor(SeverityLevel? severity) => severity switch
    {
        SeverityLevel.Critical => Color.Error,
        SeverityLevel.General => Color.Warning,
        _ => Color.Default
    };

    // ========== 分组标题信息 ==========

    private class GroupHeaderInfo
    {
        public int GroupKey { get; set; }
        public string GroupName { get; set; } = "";
        public int TotalWidth { get; set; }
        public int ColumnCount { get; set; }
        public string CssClass { get; set; } = "";
    }
}
