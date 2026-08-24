using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Blazor.Shared;
using MES.Core.Models;
using MES.Core.DTOs.Configuration;
using MES.Core.Helpers;
using System.Text.Json;

namespace MES.Blazor.Pages.Configuration;

public partial class EnumDisplayDefinitions
{
    private MudTable<EnumDisplayDefinitionDto>? table;
    private List<EnumDisplayDefinitionDto> _pageItems = new();
    private int _totalCount;
    private string _searchKeyword = string.Empty;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;

    // 枚举筛选
    private List<string> _enumKeys = new();
    private string? _selectedEnumKey;
    private bool _isRestoring;

    // 排序状态
    private string sortColumn = "EnumKey";
    private bool sortDescending = false;

    // ========== ExcelFilter 筛选 ==========
    private Dictionary<string, HashSet<string>> _columnFilters = new();
    private Dictionary<string, List<ExcelFilterOption>> _filterContextOptions = new();

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "EnumKey",     Label = "枚举标识", SortKey = "enumkey",     FilterType = "string", IsRequired = true },
        new() { Key = "Value",       Label = "枚举值",   SortKey = "value",       FilterType = "string", IsRequired = true },
        new() { Key = "DisplayName", Label = "中文显示", SortKey = "displayname", FilterType = "string", IsRequired = true },
        new() { Key = "DisplayOrder",Label = "显示顺序", SortKey = "displayorder",FilterType = null, IsRequired = true },
        new() { Key = "Remark",      Label = "说明",     SortKey = "remark",      FilterType = "string" },
    };

    /// <summary>枚举标识（类型名）→ 中文说明（下拉/表格展示用，纯显示辅助，不改存储值）</summary>
    private static readonly Dictionary<string, string> EnumKeyTexts = new(StringComparer.Ordinal)
    {
        ["WorkOrderStatus"] = "工单状态",
        ["MaterialPlanStatus"] = "物料计划状态",
        ["InventoryPlanStatus"] = "库存计划状态",
        ["LengthStatus"] = "长度状态",
        ["DeliveryState"] = "制造状态",
        ["SettlementMethod"] = "结算方式",
        ["SalesOrderStatus"] = "销售订单状态",
        ["PipeManufacturingType"] = "管型",
        ["ReworkType"] = "返整方式",
        ["FinishedProductType"] = "成品类型",
        ["ProductionType"] = "生产方式",
        ["OutboundType"] = "出库类型",
        ["InboundSource"] = "入库来源",
        ["CustomerStatus"] = "客户状态",
        ["RequirementType"] = "需求类型",
        ["NotificationType"] = "通知类型",
        ["BatchStatus"] = "批次状态",
        ["PurchaseOrderStatus"] = "采购单状态",
        ["SubcontractOrderStatus"] = "委外单状态",
        ["SectionOutsourceStatus"] = "工段委外状态",
        ["RepairPriority"] = "维修优先级",
        ["LifecycleStatus"] = "设备生命周期",
        ["UsageType"] = "设备用途",
        ["RunningStatus"] = "运行状态",
        ["RepairOrderStatus"] = "维修单状态",
        ["EquipmentTaskStatus"] = "设备任务状态",
        ["TaskOrderStatus"] = "任务单状态",
        ["InspectionItem"] = "检验项目",
        ["InspectionType"] = "成检类型",
        ["DisposalMethod"] = "处理方式",
        ["NcrStatus"] = "NCR 状态",
        ["PicklingStatus"] = "酸洗状态",
        ["SeverityLevel"] = "严重程度",
        ["VerifyResult"] = "验证结果",
        ["SectionStatus"] = "工段状态",
        ["ShiftType"] = "班次",
        ["MaterialType"] = "物料类型",
        ["ReportTemplateType"] = "报工模板类型",
        ["BatchInputType"] = "批次投料方式",
        ["CutDoubtType"] = "成切疑问",
        ["InspectionRequirementStage"] = "技术要求检验阶段",
    };

    private static string GetEnumKeyText(string enumKey)
        => EnumKeyTexts.TryGetValue(enumKey, out var cn) ? $"{cn}（{enumKey}）" : enumKey;

    // ========== 服务端数据加载 ==========

    private async Task<TableData<EnumDisplayDefinitionDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "enumkey";

            // 首次加载覆盖页码
            if (_isFirstLoad)
            {
                state.Page = _restoredPageIndex;
                _isFirstLoad = false;
            }

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending
            };

            var filters = new List<FilterDescriptor>();

            // 按枚举筛选（equals 精确匹配 EnumKey）
            if (!string.IsNullOrEmpty(_selectedEnumKey))
            {
                filters.Add(new FilterDescriptor { Field = "EnumKey", Operator = "equals", Value = _selectedEnumKey });
            }

            // 列头 ExcelFilter 多选（in）
            var columnFiltersJson = SerializeFilters();
            if (columnFiltersJson != null)
            {
                var descriptors = JsonSerializer.Deserialize<List<FilterDescriptor>>(columnFiltersJson);
                if (descriptors is { Count: > 0 })
                    filters.AddRange(descriptors);
            }

            if (filters.Count > 0)
                query.Filters = filters;

            var result = await EnumDisplayDefinitionService.GetPagedAsync(query);

            if (result.Success && result.Data != null)
            {
                _pageItems = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _currentPage = state.Page + 1;
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

        await SavePageStateAsync();
        return new TableData<EnumDisplayDefinitionDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
    }

    // ========== 排序和搜索 ==========

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

    private async Task OnEnumKeyChanged(string? value)
    {
        _selectedEnumKey = string.IsNullOrEmpty(value) ? null : value;
        _restoredPageIndex = 0;
        _isFirstLoad = true;
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== ExcelFilter 筛选 ==========

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
            var result = await EnumDisplayDefinitionService.GetFilterContextsAsync();
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
                Display = kvp.Key == "EnumKey" ? GetEnumKeyText(v) : v,
                Count = 0
            }).ToList();
        }
    }

    private async Task OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        await SavePageStateAsync();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("enum_display_definitions", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        if (table != null) await table.ReloadServerData();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    // ========== 恢复默认 ==========

    private async Task RestoreDefaults()
    {
        if (string.IsNullOrEmpty(_selectedEnumKey)) return;

        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要为枚举「{GetEnumKeyText(_selectedEnumKey)}」恢复缺失的默认值吗？（已存在/已改名的行不会被覆盖）",
            ["ConfirmText"] = "恢复默认",
            ["Color"] = Color.Secondary
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        _isRestoring = true;
        try
        {
            var result = await EnumDisplayDefinitionService.RestoreDefaultsAsync(_selectedEnumKey);
            if (result.Success)
            {
                Snackbar.Add(result.Data > 0 ? $"已恢复 {result.Data} 行默认值" : "无缺失默认值", Severity.Success);
                if (table != null) await table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "恢复默认失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"恢复默认失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isRestoring = false;
            StateHasChanged();
        }
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        var saved = await ColumnPrefs.LoadAsync("enum_display_definitions", null);
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

        // 枚举标识列表（EnumHelper 静态注册的 41 个枚举类型名）
        _enumKeys = EnumHelper.GetAllMappings().Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

        // 恢复排序/搜索状态
        var savedState = await PageState.LoadAsync("enum_display_definitions");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "EnumKey";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
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

        // 加载筛选上下文（ExcelFilter 下拉选项）
        await LoadFilterContextsAsync();

        if (savedState != null && table != null)
            await table.ReloadServerData();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#enum-display-definitions-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string DisplayName { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public string? Remark { get; set; }
    }

    private void StartEdit(EnumDisplayDefinitionDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            DisplayName = item.DisplayName,
            DisplayOrder = item.DisplayOrder,
            Remark = item.Remark
        };
    }

    private void CancelEdit(EnumDisplayDefinitionDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
        StateHasChanged();
    }

    private async Task SaveEdit(EnumDisplayDefinitionDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.DisplayName)) errors.Add("中文显示不能为空");
        if (cache.DisplayOrder <= 0) errors.Add("显示顺序必须大于0");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var dto = new EnumDisplayDefinitionDto
            {
                Id = item.Id,
                EnumKey = item.EnumKey,
                Value = item.Value,
                DisplayName = cache.DisplayName,
                DisplayOrder = cache.DisplayOrder,
                Remark = cache.Remark
            };

            var result = await EnumDisplayDefinitionService.SaveAsync(dto);
            if (result.Success)
            {
                _editingIds.Remove(item.Id);
                _editCache.Remove(item.Id);
                Snackbar.Add("保存成功", Severity.Success);
                if (table != null) await table.ReloadServerData();
            }
            else
            {
                Snackbar.Add(result.Message ?? "保存失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"保存失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    // ========== 删除 ==========

    private async Task DeleteItem(EnumDisplayDefinitionDto item)
    {
        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除枚举「{GetEnumKeyText(item.EnumKey)}」的值「{item.Value}」配置吗？（删除后该值显示回退到内置默认中文）",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        try
        {
            var result = await EnumDisplayDefinitionService.DeleteAsync(item.Id);
            if (result.Success)
            {
                Snackbar.Add("删除成功", Severity.Success);
                if (table != null) await table.ReloadServerData();
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
            PageIndex = _currentPage,
            Extras = extras
        };
        await PageState.SaveAsync("enum_display_definitions", state);
    }
}
