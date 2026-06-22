using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Blazor.Helpers;
using MES.Blazor.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.Quality;

public partial class FlaringTests
{
    private MudTable<FlaringTestDto>? table;
    private List<FlaringTestDto> _pageItems = new();
    private int _totalCount;
    private int _currentPage = 1, _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty, _dateFrom = string.Empty, _dateTo = string.Empty;
    private string sortColumn = "inspectiondate";
    private bool sortDescending = true;

    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns => _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "InspectionDate",    Label = "检验日期",     SortKey = "inspectiondate", FilterType = "date", Width = "110" },
        new() { Key = "Inspector",         Label = "检验员",       SortKey = "inspector", FilterType = "string", Width = "80" },
        new() { Key = "FurnaceNo",         Label = "生产编号",     SortKey = "furnaceno", FilterType = "string", Width = "100" },
        new() { Key = "Grade",             Label = "牌号",         SortKey = "grade", FilterType = "string", Width = "100" },
        new() { Key = "Specification",     Label = "规格",         SortKey = "specification", FilterType = "string", Width = "100" },
        new() { Key = "SampleNo",          Label = "试样编号",     SortKey = "sampleno", Width = "80" },
        new() { Key = "SampleSize",        Label = "试样尺寸(mm)", SortKey = "samplesize", FilterType = "string", Width = "120" },
        new() { Key = "InspectionStandard",Label = "检验标准",     SortKey = "inspectionstandard", FilterType = "string", Width = "120" },
        new() { Key = "MandrelTaper",      Label = "顶心锥度",     SortKey = "mandreltaper", FilterType = "string", Width = "100" },
        new() { Key = "FlaredDiameter",    Label = "扩后外径(mm)", SortKey = "flareddiameter", Width = "110" },
        new() { Key = "FlaringRate",       Label = "扩口率(%)",    SortKey = "flaringrate", Width = "100" },
        new() { Key = "Observation",       Label = "观察",         SortKey = "observation", FilterType = "string", Width = "150" },
        new() { Key = "Judgment",          Label = "判定",         SortKey = "judgment", FilterType = "string", Width = "80" },
        new() { Key = "UpdatedTime",       Label = "更新日期",     SortKey = "updatedtime", Width = "120" },
    };

    private async Task<TableData<FlaringTestDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            if (_isFirstLoad) { state.Page = _restoredPageIndex; _isFirstLoad = false; }
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "inspectiondate";
            DateTime? df = DateTime.TryParse(_dateFrom, out var d) ? d : null;
            DateTime? dt = DateTime.TryParse(_dateTo, out var dd) ? dd : null;
            var result = await FlaringTestService.GetAllAsync(
                pageIndex: state.Page + 1, pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy, isDescending: sortDescending,
                inspectionDateFrom: df, inspectionDateTo: dt,
                filters: SerializeFilters());
            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items; _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;
            }
            else { _pageItems = new(); _totalCount = 0; }
        }
        catch { _pageItems = new(); _totalCount = 0; }
        return new TableData<FlaringTestDto> { Items = _pageItems, TotalItems = _totalCount };
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
            var result = await FlaringTestService.GetFilterContextsAsync();
            if (result.Success && result.Data != null)
            {
                _filterContextOptions.Clear();
                foreach (var kvp in result.Data)
                    _filterContextOptions[kvp.Key] = kvp.Value.Select(v => new ExcelFilterOption { Value = v, Display = v, Count = 0 }).ToList();
                foreach (var col in _allColumns.Where(c => c.FilterType == "enum" && c.EnumOptions != null && !_filterContextOptions.ContainsKey(c.Key)))
                    _filterContextOptions[col.Key] = col.EnumOptions!.Select(e => new ExcelFilterOption { Value = e.Value, Display = e.Display, Count = 0 }).ToList();
            }
        }
        catch { }
    }

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0) _columnFilters[fieldKey] = selectedValues;
        else _columnFilters.Remove(fieldKey);
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnSearchChanged(string value)
    { _searchKeyword = value ?? string.Empty; await SavePageStateAsync(); if (table != null) await table.ReloadServerData(); }

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
    private async Task SaveColumnPrefs() => await ColumnPrefs.SaveAsync("flaring-test", null, _allColumns);
    private async Task ResetColumnDisplay()
    { _allColumns = GetAllColumnDefs(); await SaveColumnPrefs(); }
    private async Task MoveColumnUp(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnDown(ColumnDef col) => await SaveColumnPrefs();

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("flaring-test", null);
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
        var ss = await PageState.LoadAsync("flaring-test");
        if (ss != null)
        {
            sortColumn = ss.SortBy ?? "inspectiondate"; sortDescending = ss.IsDescending;
            _searchKeyword = ss.Keyword ?? string.Empty; _restoredPageIndex = Math.Max(0, ss.PageIndex - 1);
            if (ss.Extras?.ContainsKey("dateFrom") == true) _dateFrom = ss.Extras["dateFrom"];
            if (ss.Extras?.ContainsKey("dateTo") == true) _dateTo = ss.Extras["dateTo"];
            if (ss.Extras?.ContainsKey("columnFilters") == true)
                try { _columnFilters = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(ss.Extras["columnFilters"])?.ToDictionary(kv => kv.Key, kv => new HashSet<string>(kv.Value)) ?? new(); } catch { }
        }
        if (ss != null && table != null) await table.ReloadServerData();
        await LoadFilterContextsAsync();
    }

    // ========== 内联编辑 ==========
    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string InspectionDate { get; set; } = "";
        public string? Inspector { get; set; }
        public string? FurnaceNo { get; set; }
        public string? Grade { get; set; }
        public string? Specification { get; set; }
        public int? SampleNo { get; set; }
        public string? SampleSize { get; set; }
        public string? InspectionStandard { get; set; }
        public string? MandrelTaper { get; set; }
        public decimal? FlaredDiameter { get; set; }
        public decimal? FlaringRate { get; set; }
        public string? Observation { get; set; }
        public string? Judgment { get; set; }
    }

    private void StartEdit(FlaringTestDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new EditCache
        {
            InspectionDate = item.InspectionDate.ToString("yyyy-MM-dd"),
            Inspector = item.Inspector, FurnaceNo = item.FurnaceNo, Grade = item.Grade,
            Specification = item.Specification, SampleNo = item.SampleNo, SampleSize = item.SampleSize,
            InspectionStandard = item.InspectionStandard, MandrelTaper = item.MandrelTaper,
            FlaredDiameter = item.FlaredDiameter, FlaringRate = item.FlaringRate,
            Observation = item.Observation, Judgment = item.Judgment
        };
    }

    private void CancelEdit(FlaringTestDto item)
    { _editingIds.Remove(item.Id); _editCache.Remove(item.Id); }

    private async Task SaveEdit(FlaringTestDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;
        if (!DateTime.TryParse(cache.InspectionDate, out var date)) { Snackbar.Add("检验日期格式无效", Severity.Error); return; }
        _isSaving = true; StateHasChanged();
        try
        {
            var result = await FlaringTestService.UpdateAsync(item.Id, new UpdateFlaringTestRequest
            {
                InspectionDate = date, Inspector = cache.Inspector, FurnaceNo = cache.FurnaceNo,
                Grade = cache.Grade, Specification = cache.Specification, SampleNo = cache.SampleNo,
                SampleSize = cache.SampleSize, InspectionStandard = cache.InspectionStandard,
                MandrelTaper = cache.MandrelTaper, FlaredDiameter = cache.FlaredDiameter,
                FlaringRate = cache.FlaringRate, Observation = cache.Observation, Judgment = cache.Judgment
            });
            if (result.Success && result.Data != null)
            {
                item.InspectionDate = result.Data.InspectionDate; item.Inspector = result.Data.Inspector;
                item.FurnaceNo = result.Data.FurnaceNo; item.Grade = result.Data.Grade;
                item.Specification = result.Data.Specification; item.SampleNo = result.Data.SampleNo;
                item.SampleSize = result.Data.SampleSize; item.InspectionStandard = result.Data.InspectionStandard;
                item.MandrelTaper = result.Data.MandrelTaper;
                item.FlaredDiameter = result.Data.FlaredDiameter; item.FlaringRate = result.Data.FlaringRate;
                item.Observation = result.Data.Observation; item.Judgment = result.Data.Judgment;
                item.UpdatedTime = result.Data.UpdatedTime;
                _editingIds.Remove(item.Id); _editCache.Remove(item.Id);
                Snackbar.Add("更新成功", Severity.Success);
            }
            else Snackbar.Add(result.Message ?? "更新失败", Severity.Error);
        }
        catch (Exception ex) { Snackbar.Add($"更新失败: {ex.Message}", Severity.Error); }
        finally { _isSaving = false; StateHasChanged(); }
    }

    // ========== 单元格渲染 ==========
    private bool IsCellEditable(string key) => key switch
    {
        "InspectionDate" or "Inspector" or "FurnaceNo" or "Grade" or "Specification"
            or "SampleNo" or "SampleSize" or "InspectionStandard"
            or "MandrelTaper" or "FlaredDiameter" or "FlaringRate"
            or "Observation" or "Judgment" => true,
        _ => false
    };

    private RenderFragment RenderCell(FlaringTestDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing ? _editCache.GetValueOrDefault(item.Id) : null;
        switch (col.Key)
        {
            case "InspectionDate":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Value", cache.InspectionDate);
                    builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => cache.InspectionDate = v));
                    builder.AddAttribute(3, "Class", "compact-input"); builder.AddAttribute(4, "Placeholder", "yyyy-MM-dd");
                    builder.CloseComponent();
                }
                else builder.AddContent(0, item.InspectionDate.ToString("yyyy-MM-dd"));
                break;
            case "Inspector": RenderStr(builder, isEditing, cache?.Inspector, v => { if (cache != null) cache.Inspector = v; }, item.Inspector); break;
            case "FurnaceNo": RenderStr(builder, isEditing, cache?.FurnaceNo, v => { if (cache != null) cache.FurnaceNo = v; }, item.FurnaceNo); break;
            case "Grade": RenderStr(builder, isEditing, cache?.Grade, v => { if (cache != null) cache.Grade = v; }, item.Grade); break;
            case "Specification": RenderStr(builder, isEditing, cache?.Specification, v => { if (cache != null) cache.Specification = v; }, item.Specification); break;
            case "SampleNo": RenderInt(builder, isEditing, cache?.SampleNo, v => { if (cache != null) cache.SampleNo = v; }, item.SampleNo); break;
            case "SampleSize": RenderStr(builder, isEditing, cache?.SampleSize, v => { if (cache != null) cache.SampleSize = v; }, item.SampleSize); break;
            case "InspectionStandard": RenderStr(builder, isEditing, cache?.InspectionStandard, v => { if (cache != null) cache.InspectionStandard = v; }, item.InspectionStandard); break;
            case "MandrelTaper": RenderStr(builder, isEditing, cache?.MandrelTaper, v => { if (cache != null) cache.MandrelTaper = v; }, item.MandrelTaper); break;
            case "FlaredDiameter": RenderDecimal(builder, isEditing, cache?.FlaredDiameter, v => { if (cache != null) cache.FlaredDiameter = v; }, item.FlaredDiameter); break;
            case "FlaringRate": RenderDecimal(builder, isEditing, cache?.FlaringRate, v => { if (cache != null) cache.FlaringRate = v; }, item.FlaringRate); break;
            case "Observation": RenderStr(builder, isEditing, cache?.Observation, v => { if (cache != null) cache.Observation = v; }, item.Observation); break;
            case "Judgment": RenderStr(builder, isEditing, cache?.Judgment, v => { if (cache != null) cache.Judgment = v; }, item.Judgment); break;
            case "UpdatedTime": builder.AddContent(0, item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm")); break;
            default: builder.AddContent(0, ""); break;
        }
    };

    private void RenderStr(RenderTreeBuilder builder, bool isEditing, string? cacheVal, Action<string?> setter, string? display)
    {
        if (isEditing)
        {
            builder.OpenComponent<MudTextField<string>>(0);
            builder.AddAttribute(1, "Value", cacheVal);
            builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, setter));
            builder.AddAttribute(3, "Class", "compact-input");
            builder.CloseComponent();
        }
        else builder.AddContent(0, display);
    }

    private void RenderInt(RenderTreeBuilder builder, bool isEditing, int? cacheVal, Action<int?> setter, int? display)
    {
        if (isEditing)
        {
            builder.OpenComponent<MudNumericField<int?>>(0);
            builder.AddAttribute(1, "Value", cacheVal);
            builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<int?>(this, setter));
            builder.AddAttribute(3, "Class", "compact-input"); builder.AddAttribute(4, "HideSpinButtons", true);
            builder.CloseComponent();
        }
        else builder.AddContent(0, display?.ToString());
    }

    private void RenderDecimal(RenderTreeBuilder builder, bool isEditing, decimal? cacheVal, Action<decimal?> setter, decimal? display)
    {
        if (isEditing)
        {
            builder.OpenComponent<MudNumericField<decimal?>>(0);
            builder.AddAttribute(1, "Value", cacheVal);
            builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, setter));
            builder.AddAttribute(3, "Class", "compact-input"); builder.AddAttribute(4, "HideSpinButtons", true);
            builder.CloseComponent();
        }
        else builder.AddContent(0, display?.ToString("F2"));
    }

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        if (!string.IsNullOrEmpty(_dateFrom)) extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrEmpty(_dateTo)) extras["dateTo"] = _dateTo;
        await PageState.SaveAsync("flaring-test", new PageState
        {
            SortBy = sortColumn, IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage, Extras = extras
        });
    }

    private void NavigateToCreate() => Navigation.NavigateTo("/quality/flaring-test/create");

    private async Task DeleteItem(FlaringTestDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除生产编号 \"{item.FurnaceNo}\" 的扩口检验记录吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除", ["Color"] = Color.Error
        });
        var dr = await dialog.Result;
        if (!dr.Canceled)
        {
            try
            {
                var result = await FlaringTestService.DeleteAsync(item.Id);
                if (result.Success) { Snackbar.Add("删除成功", Severity.Success); if (table != null) await table.ReloadServerData(); }
                else Snackbar.Add(result.Message ?? "删除失败", Severity.Error);
            }
            catch (Exception ex) { Snackbar.Add($"删除失败: {ex.Message}", Severity.Error); }
        }
    }
}
