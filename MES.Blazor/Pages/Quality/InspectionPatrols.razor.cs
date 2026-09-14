using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Blazor.Shared;
using MES.Core.DTOs.Quality;
using MES.Core.Models;
using System.Text.Json;

namespace MES.Blazor.Pages.Quality;

public partial class InspectionPatrols
{
    private MudTable<InspectionPatrolDto>? table;
    private List<InspectionPatrolDto> _pageItems = new();
    private int _totalCount;
    private int _currentPage = 1, _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private int _loadVersion;
    private bool _resetToFirstPage;
    private string _searchKeyword = string.Empty, _dateFrom = string.Empty, _dateTo = string.Empty;
    private string sortColumn = "patroldate";
    private bool sortDescending = true;

    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns => _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    /// <summary>数值列（数据格居中）</summary>
    private static readonly HashSet<string> _centerColumnKeys = new(StringComparer.Ordinal)
    { "ItemCount", "AttachmentCount" };
    private static bool IsNumericColumn(ColumnDef col) => _centerColumnKeys.Contains(col.Key);

    private const string ColumnPrefsVersion = "v1";
    private const string PageKey = "quality_inspection_patrol";

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "PatrolDate",         Label = "巡检日期",     SortKey = "patroldate", FilterType = "date", Width = "110" },
        new() { Key = "Inspector",          Label = "巡检人",       SortKey = "inspector", FilterType = "string", Width = "90" },
        new() { Key = "BatchNo",            Label = "生产编号",     SortKey = "batchno", FilterType = "string", Width = "110" },
        new() { Key = "WorkOrderNo",        Label = "工单号",       SortKey = "workorderno", FilterType = "string", Width = "110" },
        new() { Key = "ProcessName",        Label = "工序名称",     SortKey = "processname", FilterType = "string", Width = "110" },
        new() { Key = "SectionName",        Label = "工段名称",     SortKey = "sectionname", FilterType = "string", Width = "100" },
        new() { Key = "ProductStatus",      Label = "产类",         SortKey = "productstatus", FilterType = "string", Width = "80" },
        new() { Key = "PlantGrade",         Label = "工厂牌号",     SortKey = "plantgrade", FilterType = "string", Width = "110" },
        new() { Key = "ManufacturingSpec",  Label = "制造规格",     SortKey = "manufacturingspec", FilterType = "string", Width = "110" },
        new() { Key = "ProductionUnit",     Label = "在产单位/车间", SortKey = "productionunit", FilterType = "string", Width = "120" },
        new() { Key = "EquipmentName",      Label = "在产设备名",   SortKey = "equipmentname", FilterType = "string", Width = "110" },
        new() { Key = "ProductionOperator", Label = "在产操作人",   SortKey = "productionoperator", FilterType = "string", Width = "110" },
        new() { Key = "ItemCount",          Label = "巡检项数",     SortKey = "itemcount", Width = "90" },
        new() { Key = "NeedRectification",  Label = "涉及整改",     SortKey = "needrectification", Width = "90" },
        new() { Key = "IsClosed",           Label = "是否闭环",     SortKey = "isclosed", Width = "90" },
        new() { Key = "AttachmentCount",    Label = "照片",         SortKey = "attachmentcount", Width = "70" },
        new() { Key = "DataSource",         Label = "数据来源",     SortKey = "datasource", FilterType = "string", Width = "90", Visible = false },
        new() { Key = "UpdatedTime",        Label = "更新时间",     SortKey = "updatedtime", Width = "140", Visible = false },
    };

    private async Task<TableData<InspectionPatrolDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
            if (_isFirstLoad) { state.Page = _restoredPageIndex; _isFirstLoad = false; }
            if (_resetToFirstPage)
            {
                state.Page = 0;
                _resetToFirstPage = false;
            }
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "patroldate";
            DateTime? df = DateTime.TryParse(_dateFrom, out var d) ? d : null;
            DateTime? dt = DateTime.TryParse(_dateTo, out var dd) ? dd : null;
            var result = await PatrolService.GetAllAsync(
                pageIndex: state.Page + 1, pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy, isDescending: sortDescending,
                filters: SerializeFilters(),
                patrolDateFrom: df, patrolDateTo: dt);
            // 竞态保护：丢弃过期请求结果
            if (version != _loadVersion)
                return new TableData<InspectionPatrolDto> { Items = _pageItems, TotalItems = _totalCount };
            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items; _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;
            }
            else { _pageItems = new(); _totalCount = 0; }
        }
        catch { _pageItems = new(); _totalCount = 0; }
        return new TableData<InspectionPatrolDto> { Items = _pageItems, TotalItems = _totalCount };
    }

    private string? SerializeFilters()
    {
        if (_columnFilters.Count == 0) return null;
        var descriptors = _columnFilters.Where(kv => kv.Value.Count > 0)
            .Select(kv => new FilterDescriptor { Field = kv.Key, Operator = "in", Values = kv.Value.ToList() }).ToList();
        return descriptors.Count > 0 ? JsonSerializer.Serialize(descriptors) : null;
    }

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await PatrolService.GetFilterContextsAsync();
            if (result.Success && result.Data != null)
            {
                _filterContextOptions.Clear();
                foreach (var kvp in result.Data)
                    _filterContextOptions[kvp.Key] = kvp.Value.Select(v => new ExcelFilterOption { Value = v, Display = DisplayFilterValue(kvp.Key, v), Count = 0 }).ToList();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载筛选上下文失败: {ex.Message}", Severity.Warning);
        }
    }

    /// <summary>筛选下拉显示值中文化（工序/工段/产类）</summary>
    private static string DisplayFilterValue(string fieldKey, string value) => fieldKey switch
    {
        "ProcessName" => ProcessDisplayHelper.GetProcessNameText(value),
        "SectionName" => SectionDisplayHelper.GetSectionNameText(value),
        "ProductStatus" => DisplayHelper.GetProductStatusText(value),
        "DataSource" => MES.Core.Helpers.StringEnumDisplayHelper.GetDataSourceText(value),
        _ => value
    };

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0) _columnFilters[fieldKey] = selectedValues;
        else _columnFilters.Remove(fieldKey);
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnSearchChanged(string value)
    { _searchKeyword = value ?? string.Empty; _resetToFirstPage = true; await SavePageStateAsync(); if (table != null) await table.ReloadServerData(); }

    private async Task OnDateFromChanged(string value)
    { _dateFrom = value ?? string.Empty; await SavePageStateAsync(); if (table != null) await table.ReloadServerData(); }

    private async Task OnDateToChanged(string value)
    { _dateTo = value ?? string.Empty; await SavePageStateAsync(); if (table != null) await table.ReloadServerData(); }

    private async Task ToggleSort(string sortKey)
    {
        if (sortColumn == sortKey) sortDescending = !sortDescending;
        else { sortColumn = sortKey; sortDescending = false; }
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnColumnToggle(ColumnDef col) => await SaveColumnPrefs();
    private async Task SaveColumnPrefs() => await ColumnPrefs.SaveAsync(PageKey, ColumnPrefsVersion, _allColumns);
    private async Task ResetColumnDisplay()
    { _allColumns = GetAllColumnDefs(); await SaveColumnPrefs(); }
    private async Task MoveColumnUp(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnDown(ColumnDef col) => await SaveColumnPrefs();

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync(PageKey, ColumnPrefsVersion);
        if (saved.Count > 0)
        {
            foreach (var s in saved)
            { var m = _allColumns.FirstOrDefault(c => c.Key == s.Key); if (m != null) m.Visible = s.Visible; }
            var re = new List<ColumnDef>();
            foreach (var s in saved)
            { var m = _allColumns.FirstOrDefault(c => c.Key == s.Key); if (m != null && !re.Contains(m)) re.Add(m); }
            foreach (var c in _allColumns) { if (!re.Contains(c)) re.Add(c); }
            _allColumns = re;
        }
        var ss = await PageState.LoadAsync(PageKey);
        if (ss != null)
        {
            sortColumn = ss.SortBy ?? "patroldate"; sortDescending = ss.IsDescending;
            _searchKeyword = ss.Keyword ?? string.Empty; _restoredPageIndex = Math.Max(0, ss.PageIndex - 1);
            if (ss.Extras?.ContainsKey("dateFrom") == true) _dateFrom = ss.Extras["dateFrom"];
            if (ss.Extras?.ContainsKey("dateTo") == true) _dateTo = ss.Extras["dateTo"];
            if (ss.Extras?.ContainsKey("columnFilters") == true)
                try { _columnFilters = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(ss.Extras["columnFilters"])?.ToDictionary(kv => kv.Key, kv => new HashSet<string>(kv.Value)) ?? new(); } catch { }
        }
        if (ss != null && table != null) await table.ReloadServerData();
        await LoadFilterContextsAsync();
    }

    // ========== 单元格渲染 ==========
    private RenderFragment RenderCell(InspectionPatrolDto item, ColumnDef col) => builder =>
    {
        switch (col.Key)
        {
            case "PatrolDate": builder.AddContent(0, item.PatrolDate.ToString("yyyy-MM-dd")); break;
            case "Inspector": builder.AddContent(0, MES.Core.Helpers.OperatorNameHelper.ToNamesOnly(item.Inspector)); break;
            case "BatchNo": builder.AddContent(0, item.BatchNo); break;
            case "WorkOrderNo": builder.AddContent(0, item.WorkOrderNo ?? ""); break;
            case "ProcessName": builder.AddContent(0, ProcessDisplayHelper.GetProcessNameText(item.ProcessName)); break;
            case "SectionName": builder.AddContent(0, SectionDisplayHelper.GetSectionNameText(item.SectionName)); break;
            case "ProductStatus":
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", DisplayHelper.GetProductStatusColor(item.ProductStatus));
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b => b.AddContent(0, DisplayHelper.GetProductStatusText(item.ProductStatus))));
                    builder.CloseComponent();
                }
                break;
            case "PlantGrade": builder.AddContent(0, item.PlantGrade ?? ""); break;
            case "ManufacturingSpec": builder.AddContent(0, item.ManufacturingSpec ?? ""); break;
            case "ProductionUnit": builder.AddContent(0, item.ProductionUnit ?? ""); break;
            case "EquipmentName": builder.AddContent(0, item.EquipmentName ?? ""); break;
            case "ProductionOperator": builder.AddContent(0, MES.Core.Helpers.OperatorNameHelper.ToNamesOnly(item.ProductionOperator)); break;
            case "ItemCount":
                if (item.ItemCount > 0)
                {
                    // 点击直接打开查看弹窗（含巡检明细与照片大图），QualityView 用户即可用
                    builder.OpenElement(0, "span");
                    builder.AddAttribute(1, "class", "cell-link");
                    builder.AddAttribute(2, "onclick",
                        EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, () => OpenViewDialog(item.Id)));
                    builder.AddContent(3, $"{item.ItemCount} 项");
                    builder.CloseElement();
                }
                else builder.AddContent(0, "-");
                break;
            case "NeedRectification":
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", item.NeedRectification ? Color.Warning : Color.Default);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b => b.AddContent(0, item.NeedRectification ? "是" : "否")));
                    builder.CloseComponent();
                }
                break;
            case "IsClosed":
                if (!item.NeedRectification) builder.AddContent(0, "-");
                else
                {
                    builder.OpenComponent<MudChip>(0);
                    builder.AddAttribute(1, "Size", Size.Small);
                    builder.AddAttribute(2, "Color", item.IsClosed ? Color.Success : Color.Warning);
                    builder.AddAttribute(3, "ChildContent", (RenderFragment)(b => b.AddContent(0, item.IsClosed ? "已闭环" : "未闭环")));
                    builder.CloseComponent();
                }
                break;
            case "AttachmentCount":
                if (item.AttachmentCount > 0)
                {
                    builder.OpenElement(0, "span");
                    builder.AddAttribute(1, "class", "cell-link");
                    builder.AddAttribute(2, "onclick",
                        EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, () => OpenViewDialog(item.Id)));
                    builder.AddContent(3, $"{item.AttachmentCount} 张");
                    builder.CloseElement();
                }
                else builder.AddContent(0, "-");
                break;
            case "DataSource": builder.AddContent(0, MES.Core.Helpers.StringEnumDisplayHelper.GetDataSourceText(item.DataSource)); break;
            case "UpdatedTime": builder.AddContent(0, item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm")); break;
            default: builder.AddContent(0, ""); break;
        }
    };

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        if (!string.IsNullOrEmpty(_dateFrom)) extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrEmpty(_dateTo)) extras["dateTo"] = _dateTo;
        await PageState.SaveAsync(PageKey, new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        });
    }

    private void NavigateToCreate() => Navigation.NavigateTo("/quality/inspection-patrol/create");
    private void NavigateToEdit(int id) => Navigation.NavigateTo($"/quality/inspection-patrol/{id}/edit");

    /// <summary>查看弹窗（只读全字段 + 巡检明细 + 照片大图 + 打印），与 QualityEdit 门控解耦</summary>
    private async Task OpenViewDialog(int id)
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, CloseButton = true };
        var parameters = new DialogParameters { ["PatrolId"] = id };
        await DialogService.ShowAsync<InspectionPatrolViewDialog>("查看巡检单", parameters, options);
    }

    private async Task ToggleClosed(InspectionPatrolDto item)
    {
        try
        {
            var result = await PatrolService.SetClosedAsync(item.Id, !item.IsClosed);
            if (result.Success)
            {
                item.IsClosed = !item.IsClosed;
                Snackbar.Add(item.IsClosed ? "已标记为闭环" : "已取消闭环标记", Severity.Success);
                StateHasChanged();
            }
            else Snackbar.Add(result.Message ?? "操作失败", Severity.Error);
        }
        catch (Exception ex) { Snackbar.Add($"操作失败: {ex.Message}", Severity.Error); }
    }

    private async Task DeleteItem(InspectionPatrolDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除生产编号 \"{item.BatchNo}\" 的巡检单吗？\n\n关联的巡检明细与照片将一并删除，且数据不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dr = await dialog.Result;
        if (dr.Canceled) return;

        try
        {
            var result = await PatrolService.DeleteAsync(item.Id);
            if (result.Success) { Snackbar.Add("删除成功", Severity.Success); if (table != null) await table.ReloadServerData(); await LoadFilterContextsAsync(); }
            else Snackbar.Add(result.Message ?? "删除失败", Severity.Error);
        }
        catch (Exception ex) { Snackbar.Add($"删除失败: {ex.Message}", Severity.Error); }
    }
}
