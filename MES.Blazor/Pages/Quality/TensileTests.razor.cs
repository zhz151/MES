using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Models;
using MES.Blazor.Helpers;
using MES.Blazor.Shared;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Shared;
using System.Text.Json;

namespace MES.Blazor.Pages.Quality;

public partial class TensileTests
{
    private MudTable<TensileTestDto>? table;
    private List<TensileTestDto> _pageItems = new();
    private int _totalCount;
    private int _currentPage = 1, _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private string _searchKeyword = string.Empty, _dateFrom = string.Empty, _dateTo = string.Empty;
    private string sortColumn = "inspectiondate";
    private bool sortDescending = true;

    // ========== 打印选中 ==========
    private HashSet<int> selectedIds = new();
    private bool _allSelected;
    private bool allSelected
    {
        get => _allSelected;
        set
        {
            if (_allSelected == value) return;
            _allSelected = value;
            if (value) { foreach (var item in _pageItems) selectedIds.Add(item.Id); }
            else { selectedIds.Clear(); }
            StateHasChanged();
        }
    }

    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns => _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "InspectionDate",     Label = "检验日期",     SortKey = "inspectiondate", FilterType = "date", Width = "110" },
        new() { Key = "Inspector",          Label = "检验员",       SortKey = "inspector", FilterType = "string", Width = "80" },
        new() { Key = "FurnaceNo",          Label = "生产编号",     SortKey = "furnaceno", FilterType = "string", Width = "100" },
        new() { Key = "Grade",              Label = "牌号",         SortKey = "grade", FilterType = "string", Width = "100" },
        new() { Key = "Specification",      Label = "规格",         SortKey = "specification", FilterType = "string", Width = "100" },
        new() { Key = "SampleNo",           Label = "试样编号",     SortKey = "sampleno", Width = "80" },
        new() { Key = "SampleSize",         Label = "试样尺寸(mm)", SortKey = "samplesize", FilterType = "string", Width = "120" },
        new() { Key = "InspectionStandard", Label = "检验标准",     SortKey = "inspectionstandard", FilterType = "string", Width = "120" },
        new() { Key = "OriginalGaugeLength",Label = "原始标距(mm)", SortKey = "originalgaugelength", Width = "100" },
        new() { Key = "FinalGaugeLength",   Label = "断后标距(mm)", SortKey = "finalgaugelength", Width = "100" },
        new() { Key = "TensileStrength",    Label = "抗拉强度(MPa)", SortKey = "tensilestrength", Width = "100" },
        new() { Key = "YieldStrengthRp02",  Label = "屈服强度Rp0.2",SortKey = "yieldstrengthrp02", Width = "100" },
        new() { Key = "YieldStrengthRp1",   Label = "屈服强度Rp1",  SortKey = "yieldstrengthrp1", Width = "100" },
        new() { Key = "Elongation",         Label = "延伸率(%)",    SortKey = "elongation", Width = "80" },
        new() { Key = "Judgment",           Label = "判定",         SortKey = "judgment", FilterType = "string", Width = "80" },
        new() { Key = "UpdatedTime",        Label = "更新日期",     SortKey = "updatedtime", Width = "120" },
    };

    private async Task<TableData<TensileTestDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            if (_isFirstLoad) { state.Page = _restoredPageIndex; _isFirstLoad = false; }
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "inspectiondate";
            DateTime? df = DateTime.TryParse(_dateFrom, out var d) ? d : null;
            DateTime? dt = DateTime.TryParse(_dateTo, out var dd) ? dd : null;
            var result = await TensileTestService.GetAllAsync(
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
        return new TableData<TensileTestDto> { Items = _pageItems, TotalItems = _totalCount };
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
            var result = await TensileTestService.GetFilterContextsAsync();
            if (result.Success && result.Data != null)
            {
                _filterContextOptions.Clear();
                foreach (var kvp in result.Data)
                    _filterContextOptions[kvp.Key] = kvp.Value.Select(v => new ExcelFilterOption { Value = v, Display = v, Count = 0 }).ToList();
                foreach (var col in _allColumns.Where(c => c.FilterType == "enum" && c.EnumOptions != null && !_filterContextOptions.ContainsKey(c.Key)))
                    _filterContextOptions[col.Key] = col.EnumOptions!.Select(e => new ExcelFilterOption { Value = e.Value, Display = e.Display, Count = 0 }).ToList();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载筛选上下文失败: {ex.Message}", Severity.Warning);
        }
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
    private async Task SaveColumnPrefs() => await ColumnPrefs.SaveAsync("tensile-test", null, _allColumns);
    private async Task ResetColumnDisplay()
    { _allColumns = GetAllColumnDefs(); await SaveColumnPrefs(); }
    private async Task MoveColumnUp(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnDown(ColumnDef col) => await SaveColumnPrefs();

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("tensile-test", null);
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
        var ss = await PageState.LoadAsync("tensile-test");
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
        public decimal? OriginalGaugeLength { get; set; }
        public decimal? FinalGaugeLength { get; set; }
        public decimal? TensileStrength { get; set; }
        public decimal? YieldStrengthRp02 { get; set; }
        public decimal? YieldStrengthRp1 { get; set; }
        public decimal? Elongation { get; set; }
        public string? Judgment { get; set; }
    }

    private void StartEdit(TensileTestDto item)
    {
        _editingIds.Add(item.Id);
        _editCache[item.Id] = new EditCache
        {
            InspectionDate = item.InspectionDate.ToString("yyyy-MM-dd"),
            Inspector = item.Inspector,
            FurnaceNo = item.FurnaceNo,
            Grade = item.Grade,
            Specification = item.Specification,
            SampleNo = item.SampleNo,
            SampleSize = item.SampleSize,
            InspectionStandard = item.InspectionStandard,
            OriginalGaugeLength = item.OriginalGaugeLength,
            FinalGaugeLength = item.FinalGaugeLength,
            TensileStrength = item.TensileStrength,
            YieldStrengthRp02 = item.YieldStrengthRp02,
            YieldStrengthRp1 = item.YieldStrengthRp1,
            Elongation = item.Elongation,
            Judgment = item.Judgment
        };
    }

    private void CancelEdit(TensileTestDto item)
    { _editingIds.Remove(item.Id); _editCache.Remove(item.Id); }

    private async Task SaveEdit(TensileTestDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;
        if (!DateTime.TryParse(cache.InspectionDate, out var date)) { Snackbar.Add("检验日期格式无效", Severity.Error); return; }
        _isSaving = true; StateHasChanged();
        try
        {
            var result = await TensileTestService.UpdateAsync(item.Id, new UpdateTensileTestRequest
            {
                InspectionDate = date,
                Inspector = cache.Inspector,
                FurnaceNo = cache.FurnaceNo,
                Grade = cache.Grade,
                Specification = cache.Specification,
                SampleNo = cache.SampleNo,
                SampleSize = cache.SampleSize,
                InspectionStandard = cache.InspectionStandard,
                OriginalGaugeLength = cache.OriginalGaugeLength,
                FinalGaugeLength = cache.FinalGaugeLength,
                TensileStrength = cache.TensileStrength,
                YieldStrengthRp02 = cache.YieldStrengthRp02,
                YieldStrengthRp1 = cache.YieldStrengthRp1,
                Elongation = cache.Elongation,
                Judgment = cache.Judgment
            });
            if (result.Success && result.Data != null)
            {
                item.InspectionDate = result.Data.InspectionDate; item.Inspector = result.Data.Inspector;
                item.FurnaceNo = result.Data.FurnaceNo; item.Grade = result.Data.Grade;
                item.Specification = result.Data.Specification; item.SampleNo = result.Data.SampleNo;
                item.SampleSize = result.Data.SampleSize; item.InspectionStandard = result.Data.InspectionStandard;
                item.OriginalGaugeLength = result.Data.OriginalGaugeLength; item.FinalGaugeLength = result.Data.FinalGaugeLength;
                item.TensileStrength = result.Data.TensileStrength; item.YieldStrengthRp02 = result.Data.YieldStrengthRp02;
                item.YieldStrengthRp1 = result.Data.YieldStrengthRp1; item.Elongation = result.Data.Elongation;
                item.Judgment = result.Data.Judgment; item.UpdatedTime = result.Data.UpdatedTime;
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
            or "OriginalGaugeLength" or "FinalGaugeLength" or "TensileStrength"
            or "YieldStrengthRp02" or "YieldStrengthRp1" or "Elongation" or "Judgment" => true,
        _ => false
    };

    private RenderFragment RenderCell(TensileTestDto item, ColumnDef col) => builder =>
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
            case "OriginalGaugeLength": RenderDecimal(builder, isEditing, cache?.OriginalGaugeLength, v => { if (cache != null) cache.OriginalGaugeLength = v; }, item.OriginalGaugeLength); break;
            case "FinalGaugeLength": RenderDecimal(builder, isEditing, cache?.FinalGaugeLength, v => { if (cache != null) cache.FinalGaugeLength = v; }, item.FinalGaugeLength); break;
            case "TensileStrength": RenderDecimal(builder, isEditing, cache?.TensileStrength, v => { if (cache != null) cache.TensileStrength = v; }, item.TensileStrength); break;
            case "YieldStrengthRp02": RenderDecimal(builder, isEditing, cache?.YieldStrengthRp02, v => { if (cache != null) cache.YieldStrengthRp02 = v; }, item.YieldStrengthRp02); break;
            case "YieldStrengthRp1": RenderDecimal(builder, isEditing, cache?.YieldStrengthRp1, v => { if (cache != null) cache.YieldStrengthRp1 = v; }, item.YieldStrengthRp1); break;
            case "Elongation": RenderDecimal(builder, isEditing, cache?.Elongation, v => { if (cache != null) cache.Elongation = v; }, item.Elongation); break;
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
        else builder.AddContent(0, display?.ToString());
    }

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        if (!string.IsNullOrEmpty(_dateFrom)) extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrEmpty(_dateTo)) extras["dateTo"] = _dateTo;
        await PageState.SaveAsync("tensile-test", new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        });
    }

    private void NavigateToCreate() => Navigation.NavigateTo("/quality/tensile-test/create");

    private async Task DeleteItem(TensileTestDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除生产编号 \"{item.FurnaceNo}\" 的室温拉伸检验记录吗？\n\n删除后数据将不可恢复！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dr = await dialog.Result;
        if (!dr.Canceled)
        {
            try
            {
                var result = await TensileTestService.DeleteAsync(item.Id);
                if (result.Success) { Snackbar.Add("删除成功", Severity.Success); if (table != null) await table.ReloadServerData(); }
                else Snackbar.Add(result.Message ?? "删除失败", Severity.Error);
            }
            catch (Exception ex) { Snackbar.Add($"删除失败: {ex.Message}", Severity.Error); }
        }
    }

    // ========== 打印 ==========

    private List<PrintColumnDef> GetPrintColumnDefs() =>
        _visibleColumns.Select(c => new PrintColumnDef { Key = c.Key, Label = c.Label }).ToList();

    private async Task PrintSelected()
    {
        if (!selectedIds.Any()) return;
        var apiUrl = $"{Http.BaseAddress}api/tensile-test/print-batch-file";
        var request = new TensileTestPrintBatchRequest { Ids = selectedIds.ToArray(), Columns = GetPrintColumnDefs() };
        var json = JsonSerializer.Serialize(request);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }

    private async Task PrintAll()
    {
        var apiUrl = $"{Http.BaseAddress}api/tensile-test/print-all-file";
        var request = new TensileTestPrintAllRequest
        {
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            SortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "inspectiondate",
            IsDescending = sortDescending,
            InspectionDateFrom = DateTime.TryParse(_dateFrom, out var df) ? df : null,
            InspectionDateTo = DateTime.TryParse(_dateTo, out var dt) ? dt : null,
            Columns = GetPrintColumnDefs()
        };
        var json = JsonSerializer.Serialize(request);
        await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
    }
}
