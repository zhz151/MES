using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Batch;
using System.Text.Json;
using MES.Core.Enums;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.Quality;

public partial class MaterialReceiveChecks
{
    // ========== 服务端分页模式（单表查询，每次只取 1 页，同 FinalInspections） ==========

    private MudTable<MaterialReceiveCheckDto>? table;
    private List<MaterialReceiveCheckDto> _pageItems = new();
    private int _totalCount;
    private HashSet<int> selectedIds = new();
    private bool _isArrowNavSetup;
    private bool _allSelected;
    private bool allSelected
    {
        get => _allSelected;
        set
        {
            if (_allSelected == value) return;
            _allSelected = value;
            if (value)
            {
                foreach (var item in _pageItems)
                    selectedIds.Add(item.Id);
            }
            else
            {
                selectedIds.Clear();
            }
            StateHasChanged();
        }
    }
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private int _loadVersion;
    private bool _resetToFirstPage;
    private string _searchKeyword = string.Empty;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;

    private string sortColumn = "receivedate";
    private bool sortDescending = true;

    // B33: 分页汇总
    private Dictionary<string, string> _pageSums = new();
    private static readonly HashSet<string> _summableColumnKeys = new();

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列定义 ==========

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.IsApplicable && c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        // G1: 检验到料（实体数据）
        new() { Key = "BatchNo",           Label = "生产编号",   SortKey = "batchno", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "检验到料" },
        new() { Key = "TagNo",             Label = "挂牌号",     SortKey = "tagno", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "检验到料" },
        new() { Key = "HealthIssue",       Label = "校验状态",   SortKey = null, FilterType = null, Width = "100", GroupKey = 1, GroupName = "检验到料" },
        new() { Key = "ReceiveDate",       Label = "到料日期",   SortKey = "receivedate", FilterType = "date", Width = "120", GroupKey = 1, GroupName = "检验到料" },
        new() { Key = "ProcessName",       Label = "工序名称",   SortKey = "processname", FilterType = "string", Width = "100", GroupKey = 1, GroupName = "检验到料" },
        new() { Key = "SequenceNumber",    Label = "执行序",     SortKey = "sequencenumber", FilterType = "string", Width = "80", GroupKey = 1, GroupName = "检验到料" },
        new() { Key = "InspectionType",    Label = "成检类型",   SortKey = "inspectiontype", FilterType = "enum", Width = "100", GroupKey = 1, GroupName = "检验到料", EnumOptions = DisplayHelper.GetEnumFilterOptions<InspectionType>() },
        new() { Key = "Shift",             Label = "班次",        SortKey = "shift", FilterType = "enum", Width = "120", GroupKey = 1, GroupName = "检验到料", EnumOptions = DisplayHelper.GetEnumFilterOptions<ShiftType>() },
        new() { Key = "Checker",           Label = "确认人",     SortKey = "checker", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "检验到料" },
        new() { Key = "DataSource",        Label = "数据来源",   SortKey = "datasource", FilterType = "enum", Width = "80", GroupKey = 1, GroupName = "检验到料", EnumOptions = DisplayHelper.GetDataSourceOptions() },
        new() { Key = "IsForceCompleted",  Label = "强制完成",   SortKey = "isforcecompleted", FilterType = "boolean", BoolTrueLabel = "是", BoolFalseLabel = "否", Width = "60", GroupKey = 1, GroupName = "检验到料" },
        new() { Key = "Remark",            Label = "备注",        SortKey = "remark", FilterType = "string", Width = "120", GroupKey = 1, GroupName = "检验到料" },
        new() { Key = "UpdatedTime",       Label = "更新时间",   SortKey = "updatedtime", FilterType = "date", Width = "120", GroupKey = 1, GroupName = "检验到料" },

        // G2: 批次信息（DTO 导航带出）
        new() { Key = "WorkOrderNo",       Label = "工单号",     SortKey = "workorderno", FilterType = "string", Width = "120", GroupKey = 2, GroupName = "批次信息" },
        new() { Key = "SalesOrderNo",      Label = "订单号",     SortKey = "salesorderno", FilterType = "string", Visible = false, Width = "120", GroupKey = 2, GroupName = "批次信息" },
        new() { Key = "ProductionMainNo",  Label = "主号",       SortKey = "productionmainno", FilterType = "string", Visible = false, Width = "120", GroupKey = 2, GroupName = "批次信息" },
        new() { Key = "ProductionType",    Label = "生产类型",   SortKey = "productiontype", FilterType = "enum", Width = "120", Visible = false, GroupKey = 2, GroupName = "批次信息", EnumOptions = DisplayHelper.GetEnumFilterOptions<ProductionType>() },
        new() { Key = "ManufacturingItem", Label = "制造物品",   SortKey = "manufacturingitem", FilterType = "enum", Width = "120", GroupKey = 2, GroupName = "批次信息", EnumOptions = new() { new("OrderFinished","订单成品"), new("Finished","备料成品"), new("Surplus","余库料"), new("SpecialDeliveryStatus","订成-非交付态") } },
        new() { Key = "ManufacturingStatus", Label = "制造状态",   SortKey = "manufacturingsstatus", FilterType = "enum", Width = "120", GroupKey = 2, GroupName = "批次信息", EnumOptions = DisplayHelper.GetEnumFilterOptions<DeliveryState>() },
        new() { Key = "IsDeliveryStatus",  Label = "是否交付态", SortKey = "isdeliverystatus", FilterType = "enum", Width = "90", GroupKey = 2, GroupName = "批次信息", EnumOptions = new() { new("是","是"), new("否","否") } },
        new() { Key = "PlantGrade",        Label = "工厂牌号",   SortKey = "plantgrade", FilterType = "string", Width = "120", GroupKey = 2, GroupName = "批次信息" },
        new() { Key = "Specification",     Label = "规格",       SortKey = "specification", FilterType = "string", Width = "120", GroupKey = 2, GroupName = "批次信息" },
        new() { Key = "LengthStatus",      Label = "长度状态",   SortKey = "lengthstatus", FilterType = "enum", Width = "100", GroupKey = 2, GroupName = "批次信息", EnumOptions = DisplayHelper.GetEnumFilterOptions<LengthStatus>() },
        new() { Key = "FurnaceNo",         Label = "炉号",       SortKey = "furnaceno", FilterType = "string", Visible = false, Width = "120", GroupKey = 2, GroupName = "批次信息" },
        new() { Key = "SourceUnit",        Label = "来料单位",   SortKey = "sourceunit", FilterType = "string", Visible = false, Width = "120", GroupKey = 2, GroupName = "批次信息" },
        new() { Key = "Salesman",          Label = "业务员",     SortKey = "salesman", FilterType = "string", Visible = false, Width = "100", GroupKey = 2, GroupName = "批次信息" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<MaterialReceiveCheckDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
            // 首次加载覆盖页码（MudTable 初始化时始终传 page=0）
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            if (_resetToFirstPage)
            {
                state.Page = 0;
                _resetToFirstPage = false;
            }

            var sortBy = _allColumns.FirstOrDefault(c => c.SortKey == sortColumn)?.SortKey ?? "receivedate";
            var filtersJson = SerializeFilters();

            DateTime? dateFrom = null;
            DateTime? dateTo = null;
            if (DateTime.TryParse(_dateFrom, out var df)) dateFrom = df;
            if (DateTime.TryParse(_dateTo, out var dt)) dateTo = dt;

            var result = await MaterialCheckService.GetAllMaterialReceiveChecksAsync(
                pageIndex: state.Page + 1,
                pageSize: state.PageSize,
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                sortBy: sortBy,
                isDescending: sortDescending,
                receiveDateFrom: dateFrom,
                receiveDateTo: dateTo,
                filters: filtersJson
            );

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<MaterialReceiveCheckDto> { Items = _pageItems, TotalItems = _totalCount };

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;
                ComputePageSums();
            }
            else
            {
                _pageItems = new();
                _totalCount = 0;
                _pageSums.Clear();
            }

            // 实时健康汇总（按当前筛选条件全量统计），并行拉取
            await LoadHealthSummaryAsync(dateFrom, dateTo, filtersJson);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
            _pageItems = new();
            _totalCount = 0;
        }

        return new TableData<MaterialReceiveCheckDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
    }

    // ========== 实时健康汇总（顶部通知条） ==========

    private MaterialCheckHealthSummaryDto? _healthSummary;

    private async Task LoadHealthSummaryAsync(DateTime? dateFrom, DateTime? dateTo, string? filtersJson)
    {
        try
        {
            var summary = await MaterialCheckService.GetMaterialCheckHealthSummaryAsync(
                keyword: string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                receiveDateFrom: dateFrom,
                receiveDateTo: dateTo,
                filters: filtersJson);
            _healthSummary = summary.Success ? summary.Data : null;
        }
        catch
        {
            _healthSummary = null;
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

    // ========== 筛选上下文加载（ExcelFilter 下拉选项） ==========

    private async Task LoadFilterContextsAsync()
    {
        try
        {
            var result = await MaterialCheckService.GetMaterialCheckFilterContextsAsync();
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
                Display = kvp.Key switch
                {
                    "SectionName" or "CurrentSectionName" or "NextSectionName" or "PendingSectionName" => SectionDisplayHelper.GetSectionNameText(v),
                    "ProcessName" or "ProcessGroupName" or "CurrentGroupName" or "NextProcess" => ProcessDisplayHelper.GetProcessNameText(v),
                    _ => v
                },
                Count = 0
            }).ToList();
        }

        // 枚举列：用 EnumOptions 的中文显示名替换 API 返回的原始值
        foreach (var col in _allColumns.Where(c => c.FilterType == "enum" && c.EnumOptions != null))
        {
            if (_filterContextOptions.TryGetValue(col.Key, out var options))
            {
                var enumMap = col.EnumOptions!.ToDictionary(e => e.Value, e => e.Display);
                foreach (var opt in options.Where(o => enumMap.ContainsKey(o.Value)))
                    opt.Display = enumMap[opt.Value];
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
                _filterContextOptions[col.Key] = DisplayHelper.GetBoolFilterOptions(col);
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


    private async Task ToggleSort(string? sortKey)
    {
        if (sortColumn == sortKey)
            sortDescending = !sortDescending;
        else
        {
            sortColumn = sortKey!;
            sortDescending = false;
        }
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        _resetToFirstPage = true;
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

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnUp(ColumnDef col) => await SaveColumnPrefs();
    private async Task MoveColumnDown(ColumnDef col) => await SaveColumnPrefs();

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("material-receive-checks", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        StateHasChanged();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("material-receive-checks", null);
        if (saved.Count > 0)
        {
            foreach (var s in saved)
            {
                var match = _allColumns.FirstOrDefault(c => c.Key == s.Key);
                if (match != null)
                    match.Visible = s.Visible;
            }
            var reordered = new List<ColumnDef>();
            foreach (var s in saved)
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

        // 从 PageState 恢复排序/筛选状态
        var savedState = await PageState.LoadAsync("materialchecks");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "receivedate";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
            if (savedState.Extras?.ContainsKey("dateFrom") == true)
                _dateFrom = savedState.Extras["dateFrom"];
            if (savedState.Extras?.ContainsKey("dateTo") == true)
                _dateTo = savedState.Extras["dateTo"];
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

        // 加载筛选上下文和待成检到料卡片数据（并行）
        await Task.WhenAll(
            LoadFilterContextsAsync(),
            LoadPendingMaterialChecksAsync()
        );
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            await JS.InvokeVoidAsync("initGroupHeaders", "#material-checks-list-table");
        }
        catch { }

        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#material-checks-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 待成检到料卡片 ==========

    private List<PendingMaterialCheckDto> _pendingItems = new();
    private bool _showPending = false;

    private async Task LoadPendingMaterialChecksAsync()
    {
        try
        {
            var result = await MaterialCheckService.GetPendingMaterialChecksAsync();
            if (result.Success && result.Data != null)
                _pendingItems = result.Data;
        }
        catch { }
    }

    private void TogglePendingMaterialChecks() => _showPending = !_showPending;

    // ========== 导航 ==========

    private void NavigateToCreate() => Navigation.NavigateTo("/quality/material-receive-checks/create");
    private void NavigateToCreateWithBatch(PendingMaterialCheckDto item) => Navigation.NavigateTo(
        $"/quality/material-receive-checks/create?batchNo={Uri.EscapeDataString(item.BatchNo)}&processGroupId={item.ProcessGroupId}&processGroupName={Uri.EscapeDataString(item.ProcessGroupName ?? "")}");
    private void ViewBatch(int batchId) => Navigation.NavigateTo($"/batches/{batchId}");

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    // 每个批次可重选的检验工序组选项（工序名称下拉数据源，按需懒加载）
    private Dictionary<int, List<ProcessGroupDto>> _processGroupOptions = new();

    private async Task LoadProcessGroupOptionsAsync(int batchId)
    {
        if (_processGroupOptions.ContainsKey(batchId)) return;
        try
        {
            var result = await BatchService.GetProcessGroupsAsync(batchId);
            if (result.Success && result.Data != null)
                _processGroupOptions[batchId] = result.Data;
        }
        catch { /* 加载失败仅影响下拉，不阻断编辑 */ }
    }

    private List<ProcessGroupDto> GetProcessGroupOptions(int batchId)
        => _processGroupOptions.TryGetValue(batchId, out var list) ? list : new();

    private class EditCache
    {
        public string ReceiveDate { get; set; } = string.Empty;
        public ShiftType? Shift { get; set; }
        public string? Checker { get; set; }
        public string? Remark { get; set; }

        /// <summary>当前选中工序组ID（工序名称重选用）</summary>
        public int ProcessGroupId { get; set; }
    }

    private async Task StartEdit(MaterialReceiveCheckDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            ReceiveDate = item.ReceiveDate.ToString("yyyy-MM-dd"),
            Shift = item.Shift,
            Checker = item.Checker,
            Remark = item.Remark,
            ProcessGroupId = item.ProcessGroupId
        };
        // 懒加载该批次检验工序组，供工序名称下拉重选
        await LoadProcessGroupOptionsAsync(item.ProductionBatchId);
    }

    private void CancelEdit(MaterialReceiveCheckDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(MaterialReceiveCheckDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        if (!DateTime.TryParse(cache.ReceiveDate, out var parsedDate))
        {
            Snackbar.Add("到料日期格式无效（请用 yyyy-MM-dd）", Severity.Error);
            return;
        }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateMaterialReceiveCheckRequest
            {
                ReceiveDate = parsedDate,
                Shift = cache.Shift,
                Checker = cache.Checker,
                Remark = cache.Remark,
                ProcessGroupId = cache.ProcessGroupId
            };

            var result = await MaterialCheckService.UpdateMaterialReceiveCheckAsync(item.Id, request);
            if (result.Success && result.Data != null)
            {
                item.ReceiveDate = result.Data.ReceiveDate;
                item.Shift = result.Data.Shift;
                item.Checker = result.Data.Checker;
                item.Remark = result.Data.Remark;
                // 推导值：保存即按当前工艺卡重算（含重选工序组），同步回写展示
                item.ProcessGroupId = result.Data.ProcessGroupId;
                item.ProcessName = result.Data.ProcessName;
                item.SequenceNumber = result.Data.SequenceNumber;
                item.InspectionType = result.Data.InspectionType;

                _editingIds.Remove(item.Id);
                _editCache.Remove(item.Id);
                if (table != null) await table.ReloadServerData();
                Snackbar.Add("更新成功", Severity.Success);
            }
            else
            {
                Snackbar.Add(result.Message ?? "更新失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"更新失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    // ========== 删除 ==========

    private async Task DeleteItem(MaterialReceiveCheckDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除批次 \"{item.BatchNo}\" 的成检到料记录吗？\n\n删除后批次将恢复为进行中状态！",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await MaterialCheckService.DeleteMaterialReceiveCheckAsync(item.Id);
                if (result.Success)
                {
                    Snackbar.Add("删除成功", Severity.Success);
                    if (table != null) await table.ReloadServerData();
                    await LoadPendingMaterialChecksAsync();
                    await LoadFilterContextsAsync();
                }
                else
                {
                    Snackbar.Add(result.Message ?? "删除失败", Severity.Error);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"删除失败: {ex.Message}", Severity.Error);
            }
        }
    }

    // ========== 单元格渲染 ==========

    private RenderFragment RenderCell(MaterialReceiveCheckDto item, ColumnDef col) => builder =>
    {
        var isEditing = _editingIds.Contains(item.Id);
        var cache = isEditing && _editCache.TryGetValue(item.Id, out var c) ? c : null;

        switch (col.Key)
        {
            case "BatchNo":
                builder.OpenComponent<MudLink>(0);
                builder.AddAttribute(1, "Typo", Typo.body2);
                builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create<MouseEventArgs?>(this, () => ViewBatch(item.ProductionBatchId)));
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.BatchNo)));
                builder.CloseComponent();
                break;

            case "HealthIssue":
                // 实时校验状态：正常=绿，成检类型过期=橙，工序组非检验=红
                builder.OpenComponent<MudChip>(0);
                builder.AddAttribute(1, "Size", Size.Small);
                builder.AddAttribute(2, "Color", item.HealthIssue switch
                {
                    "成检类型过期" => Color.Warning,
                    "工序组非检验" => Color.Error,
                    _ => Color.Success
                });
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b2 => b2.AddContent(0, item.HealthIssue ?? "正常")));
                builder.CloseComponent();
                break;

            case "ReceiveDate":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.ReceiveDate);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => { cache.ReceiveDate = v ?? ""; }));
                    builder.AddAttribute(5, "Placeholder", "yyyy-MM-dd");
                    builder.AddAttribute(6, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.ReceiveDate.ToString("yyyy-MM-dd"));
                }
                break;

            case "ManufacturingItem":
                builder.AddContent(0, DisplayHelper.GetMaterialTypeText(item.ManufacturingItem?.ToString()));
                break;

            case "PlantGrade":
            case "Specification":
            case "TagNo":
            case "WorkOrderNo":
            case "SalesOrderNo":
            case "ProductionMainNo":
            case "FurnaceNo":
            case "SourceUnit":
                builder.AddContent(0, typeof(MaterialReceiveCheckDto).GetProperty(col.Key)?.GetValue(item)?.ToString());
                break;

            case "ProductionType":
                builder.AddContent(0, DisplayHelper.GetProductionTypeText(item.ProductionType?.ToString()));
                break;

            case "LengthStatus":
                builder.AddContent(0, DisplayHelper.GetLengthStatusText(item.LengthStatus?.ToString()));
                break;

            case "DataSource":
                builder.AddContent(0, item.DataSource switch
                {
                    "SCAN" => "扫码",
                    "MANUAL" => "手动",
                    _ => item.DataSource
                });
                break;

            case "IsForceCompleted":
                var isCompleted = item.IsForceCompleted;
                builder.OpenComponent<MudSwitch<bool>>(0);
                builder.AddAttribute(1, "Checked", isCompleted);
                builder.AddAttribute(2, "CheckedChanged", EventCallback.Factory.Create<bool>(this, async value =>
                {
                    await MaterialCheckService.UpdateMaterialReceiveCheckAsync(item.Id, new UpdateMaterialReceiveCheckRequest { IsForceCompleted = value });
                    item.IsForceCompleted = value;
                    if (table != null) await table.ReloadServerData();
                }));
                builder.AddAttribute(3, "Dense", true);
                builder.CloseComponent();
                break;

            case "Shift":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudSelect<ShiftType>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.Shift ?? default(ShiftType));
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<ShiftType>(this, v => cache.Shift = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.AddAttribute(6, "ChildContent", (RenderFragment)(b =>
                    {
                        foreach (var opt in DisplayHelper.GetEnumOptions<ShiftType>())
                        {
                            b.OpenComponent<MudSelectItem<ShiftType>>(0);
                            b.AddAttribute(1, "Value", Enum.Parse<ShiftType>(opt.Value));
                            b.AddAttribute(2, "Text", opt.Display);
                            b.AddAttribute(3, "ChildContent", (RenderFragment)(b2 =>
                                b2.AddContent(0, opt.Display)));
                            b.CloseComponent();
                        }
                    }));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, DisplayHelper.GetShiftTypeText(item.Shift));
                }
                break;

            case "Checker":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.Checker);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.Checker = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Checker);
                }
                break;

            case "Remark":
                if (isEditing && cache != null)
                {
                    builder.OpenComponent<MudTextField<string>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.Remark);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => cache.Remark = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, item.Remark);
                }
                break;

            case "UpdatedTime":
                builder.AddContent(0, item.UpdatedTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
                break;

            case "Salesman":
                builder.AddContent(0, item.Salesman);
                break;

            case "ManufacturingStatus":
                if (item.IsLastProcessGroup)
                    builder.AddContent(0, item.ManufacturingStatusDisplay);
                else
                    builder.AddContent(0, "-");
                break;

            case "InspectionType":
                builder.AddContent(0, DisplayHelper.GetInspectionTypeText(item.InspectionType));
                break;

            case "IsDeliveryStatus":
                builder.AddContent(0, item.IsDeliveryStatus ?? "-");
                break;

            case "ProcessName":
                if (isEditing && cache != null)
                {
                    // 重选工序组：展示该批次检验工序组，联动重算执行序/成检类型
                    var pgOptions = GetProcessGroupOptions(item.ProductionBatchId);
                    builder.OpenComponent<MudSelect<int>>(0);
                    builder.AddAttribute(1, "Dense", true);
                    builder.AddAttribute(2, "Variant", Variant.Outlined);
                    builder.AddAttribute(3, "Value", cache.ProcessGroupId);
                    builder.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<int>(this, v => cache.ProcessGroupId = v));
                    builder.AddAttribute(5, "Class", "compact-input");
                    builder.AddAttribute(6, "ChildContent", (RenderFragment)(b =>
                    {
                        foreach (var pg in pgOptions)
                        {
                            b.OpenComponent<MudSelectItem<int>>(0);
                            b.AddAttribute(1, "Value", pg.Id);
                            b.AddAttribute(2, "Text", ProcessDisplayHelper.GetProcessNameText(pg.ProcessName));
                            b.AddAttribute(3, "ChildContent", (RenderFragment)(b2 =>
                                b2.AddContent(0, ProcessDisplayHelper.GetProcessNameText(pg.ProcessName))));
                            b.CloseComponent();
                        }
                    }));
                    builder.CloseComponent();
                }
                else
                {
                    builder.AddContent(0, ProcessDisplayHelper.GetProcessNameText(item.ProcessName));
                }
                break;

            case "SequenceNumber":
                builder.AddContent(0, item.SequenceNumber.ToString("G29"));
                break;

            default:
                builder.AddContent(0, "");
                break;
        }
    };

    // ========== 打印方法 ==========

    private List<PrintColumnDef> GetPrintColumnDefs()
    {
        return _visibleColumns.Select(c => new PrintColumnDef
        {
            Key = c.Key,
            Label = c.Label
        }).ToList();
    }

    private async Task PrintSelected()
    {
        if (!selectedIds.Any())
        {
            Snackbar.Add("请先选择要打印的成检到料记录", Severity.Warning);
            return;
        }
        try
        {
            var ids = selectedIds.ToArray();
            var columns = GetPrintColumnDefs();
            var request = new MaterialCheckPrintBatchRequest { Ids = ids, Columns = columns };
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.MaterialReceiveCheck}/print-batch-file";
            var json = JsonSerializer.Serialize(request);
            Snackbar.Add("正在生成PDF...", Severity.Info);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task PrintAll()
    {
        try
        {
            var columns = GetPrintColumnDefs();
            var request = new MaterialCheckPrintAllRequest
            {
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortColumn,
                IsDescending = sortDescending,
                ReceiveDateFrom = DateTime.TryParse(_dateFrom, out var df) ? df : null,
                ReceiveDateTo = DateTime.TryParse(_dateTo, out var dt) ? dt : null,
                Columns = columns,
                Filters = SerializeFilters()
            };
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.MaterialReceiveCheck}/print-all-file";
            var json = JsonSerializer.Serialize(request);
            Snackbar.Add("正在生成PDF...", Severity.Info);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var extras = new Dictionary<string, string>();
        if (_columnFilters.Count > 0)
            extras["columnFilters"] = JsonSerializer.Serialize(_columnFilters.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));
        if (!string.IsNullOrEmpty(_dateFrom))
            extras["dateFrom"] = _dateFrom;
        if (!string.IsNullOrEmpty(_dateTo))
            extras["dateTo"] = _dateTo;
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("materialchecks", state);
    }

    // ========== 分页汇总（B33） ==========
    private int _totalTableWidth =>
        _visibleColumns.Sum(c => int.TryParse(c.Width, out var w) ? w : 100) + 40 + 100;

    // ========== 分组标题栏 ==========

    private List<GroupHeaderInfo> GetGroupHeaders()
    {
        var result = new List<GroupHeaderInfo>();
        result.Add(new GroupHeaderInfo { GroupKey = 0, GroupName = "", TotalWidth = 40, ColumnCount = 0, CssClass = "" });
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
                result.Add(new GroupHeaderInfo { GroupKey = groupKey, GroupName = groupName, TotalWidth = totalWidth, ColumnCount = count, CssClass = GetHeaderGroupCss(groupKey, true) });
                totalWidth = 0; count = 0;
            }
            groupKey = gk;
            groupName = col.GroupName ?? "";
            totalWidth += int.TryParse(col.Width, out var w) ? w : 100;
            count++;
            lastKey = gk;
        }
        if (count > 0)
            result.Add(new GroupHeaderInfo { GroupKey = groupKey, GroupName = groupName, TotalWidth = totalWidth, ColumnCount = count, CssClass = GetHeaderGroupCss(groupKey, true) });
        result.Add(new GroupHeaderInfo { GroupKey = 0, GroupName = "", TotalWidth = 100, ColumnCount = 0, CssClass = "" });
        return result;
    }

    private static string GetHeaderGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch { 1 => "col-g1", 2 => "col-g2", _ => "" };
        if (isGroupStart && groupKey > 1) cls += " col-group-start";
        return cls;
    }

    private static string GetCellGroupCss(int? groupKey, bool isGroupStart)
    {
        var cls = groupKey switch { 1 => "col-g1-cell", 2 => "col-g2-cell", _ => "" };
        if (isGroupStart && groupKey > 1) cls += " col-group-start-cell";
        return cls;
    }

    private class GroupHeaderInfo
    {
        public int GroupKey { get; set; }
        public string GroupName { get; set; } = "";
        public int TotalWidth { get; set; }
        public int ColumnCount { get; set; }
        public string CssClass { get; set; } = "";
    }

    private void ComputePageSums()
    {
        _pageSums.Clear();
        if (_pageItems.Count == 0) return;
        var props = typeof(MaterialReceiveCheckDto)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);
        foreach (var key in _summableColumnKeys)
        {
            if (!props.TryGetValue(key, out var prop)) continue;
            var type = prop.PropertyType;
            try
            {
                if (type == typeof(decimal?))
                {
                    var sum = _pageItems.Sum(item => (decimal?)(prop.GetValue(item)) ?? 0m);
                    _pageSums[key] = ((int)sum).ToString();
                }
                else if (type == typeof(int?))
                {
                    var sum = _pageItems.Sum(item => (int?)(prop.GetValue(item)) ?? 0);
                    _pageSums[key] = sum.ToString();
                }
            }
            catch { }
        }
    }
    private string RenderFooterCell(ColumnDef col)
    {
        return _pageSums.GetValueOrDefault(col.Key, "");
    }
}
