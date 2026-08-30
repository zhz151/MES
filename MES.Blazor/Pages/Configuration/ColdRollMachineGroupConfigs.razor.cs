using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Blazor.Shared;
using MES.Core.Models;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Pages.Configuration;

/// <summary>
/// 冷轧机台组配置页：冷轧工序归组参数表维护（哪个工序归入哪组、组显示名、组排序、供给目标组）。
/// 引擎归组/排机估算/排程建议据此聚合机台类型，新增冷轧工序可经配置归组，无需改代码。
/// </summary>
public partial class ColdRollMachineGroupConfigs
{
    private MudTable<ColdRollMachineGroupConfigDto>? table;
    private List<ColdRollMachineGroupConfigDto> _pageItems = new();
    private int _totalCount;
    private string _searchKeyword = string.Empty;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;
    private int _loadVersion;
    private bool _resetToFirstPage;

    // 排序状态
    private string sortColumn = "DisplayOrder";
    private bool sortDescending = false;

    /// <summary>冷轧/冷拔工序选项（配置表 ProcessDefinitions 驱动，MudSelectMulti 选项）</summary>
    private List<ProcessInfoDto> _machineTypeOptions = new();

    /// <summary>全部机台组选项（供给目标组下拉选项/显示名映射）</summary>
    private List<ColdRollMachineGroupConfigDto> _groupOptions = new();

    private async Task LoadMachineTypeOptionsAsync()
    {
        var result = await ProcessDefSvc.GetColdRollOptionsAsync();
        if (result.Success && result.Data != null)
            _machineTypeOptions = result.Data;
    }

    private async Task LoadGroupOptionsAsync()
    {
        var result = await Service.GetAllAsync();
        if (result.Success && result.Data != null)
            _groupOptions = result.Data;
    }

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "GroupKey",       Label = "组Key",      SortKey = "groupkey",      IsRequired = true },
        new() { Key = "DisplayName",    Label = "组显示名",    SortKey = "displayname",   IsRequired = true },
        new() { Key = "ProcessKeys",    Label = "组内工序" },
        new() { Key = "DisplayOrder",   Label = "显示顺序",    SortKey = "displayorder" },
        new() { Key = "SupplyTargetGroupKey", Label = "供给目标组", SortKey = "supplytargetgroupkey" },
        new() { Key = "Remark",         Label = "备注" },
        new() { Key = "UpdatedTime",    Label = "更新时间",    SortKey = "updatedtime" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<ColdRollMachineGroupConfigDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "displayorder";

            // 首次加载覆盖页码
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

            var query = new QueryParams
            {
                PageIndex = state.Page + 1,
                PageSize = state.PageSize,
                Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
                SortBy = sortBy,
                IsDescending = sortDescending
            };

            var result = await Service.GetPagedAsync(query);

            // 竞态保护：丢弃过期请求结果（搜索/筛选并发时旧请求晚返回不得覆盖新结果）
            if (version != _loadVersion)
                return new TableData<ColdRollMachineGroupConfigDto> { Items = _pageItems, TotalItems = _totalCount };

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
        return new TableData<ColdRollMachineGroupConfigDto>
        {
            Items = _pageItems,
            TotalItems = _totalCount
        };
    }

    // ========== 排序和搜索 ==========

    private async Task ToggleSort(string colKey)
    {
        var sortKey = _allColumns.FirstOrDefault(c => c.Key == colKey)?.SortKey;
        if (string.IsNullOrEmpty(sortKey)) return; // 组内工序/备注列不排序
        if (sortColumn == colKey)
            sortDescending = !sortDescending;
        else
        {
            sortColumn = colKey;
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

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("cold_roll_machine_group_configs", null, _allColumns);
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        if (table != null) await table.ReloadServerData();
    }

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        await LoadMachineTypeOptionsAsync();
        await LoadGroupOptionsAsync();
        var saved = await ColumnPrefs.LoadAsync("cold_roll_machine_group_configs", null);
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

        // 恢复排序/搜索状态
        var savedState = await PageState.LoadAsync("cold_roll_machine_group_configs");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "DisplayOrder";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? string.Empty;
            _restoredPageIndex = Math.Max(0, savedState.PageIndex - 1);
        }

        if (savedState != null && table != null)
            await table.ReloadServerData();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_isArrowNavSetup)
        {
            _isArrowNavSetup = true;
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#cold-roll-machine-group-configs-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 新增 ==========

    private async Task AddNew()
    {
        var hash = DateTime.Now.Ticks.GetHashCode();
        var newId = hash < 0 ? hash : -hash - 1;
        var newItem = new ColdRollMachineGroupConfigDto
        {
            Id = newId,
            GroupKey = "",
            DisplayName = "",
            ProcessKeys = new List<string>(),
            DisplayOrder = 0,
        };

        if (_currentPage == 1)
        {
            _pageItems.Insert(0, newItem);
            StartEdit(newItem);
            await InvokeAsync(StateHasChanged);
        }
        else
        {
            _currentPage = 1;
            _restoredPageIndex = 0;
            _isFirstLoad = true;
            if (table != null) await table.ReloadServerData();
            Snackbar.Add("请在首页点击\"新建\"添加记录", Severity.Info);
        }
    }

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string GroupKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<string> ProcessKeys { get; set; } = new();
        public int DisplayOrder { get; set; }
        public string? SupplyTargetGroupKey { get; set; }
        public string? Remark { get; set; }
    }

    private bool IsNewItem(int id) => id < 0;

    private void StartEdit(ColdRollMachineGroupConfigDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            GroupKey = item.GroupKey,
            DisplayName = item.DisplayName,
            ProcessKeys = new List<string>(item.ProcessKeys),
            DisplayOrder = item.DisplayOrder,
            SupplyTargetGroupKey = item.SupplyTargetGroupKey,
            Remark = item.Remark
        };
    }

    private void CancelEdit(ColdRollMachineGroupConfigDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);

        if (IsNewItem(item.Id))
        {
            _pageItems.Remove(item);
            StateHasChanged();
        }
    }

    private async Task SaveEdit(ColdRollMachineGroupConfigDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.GroupKey)) errors.Add("组Key不能为空");
        if (string.IsNullOrWhiteSpace(cache.DisplayName)) errors.Add("组显示名不能为空");
        if (cache.ProcessKeys.Count == 0) errors.Add("组内工序不能为空（须至少选一个冷轧/冷拔工序）");
        if (cache.DisplayOrder < 0) errors.Add("显示顺序不能为负");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var dto = new ColdRollMachineGroupConfigDto
            {
                Id = IsNewItem(item.Id) ? 0 : item.Id,
                GroupKey = cache.GroupKey,
                DisplayName = cache.DisplayName,
                ProcessKeys = cache.ProcessKeys,
                DisplayOrder = cache.DisplayOrder,
                SupplyTargetGroupKey = string.IsNullOrWhiteSpace(cache.SupplyTargetGroupKey) ? null : cache.SupplyTargetGroupKey,
                Remark = cache.Remark
            };

            var result = await Service.SaveAsync(dto);
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

    private async Task DeleteItem(ColdRollMachineGroupConfigDto item)
    {
        if (IsNewItem(item.Id))
        {
            _pageItems.Remove(item);
            StateHasChanged();
            return;
        }

        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除机台组 \"{item.DisplayName}\" 吗？",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await Service.DeleteAsync(item.Id);
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
    }

    // ========== 显示辅助 ==========

    /// <summary>组内工序中文显示（Key → 中文，未知原样）</summary>
    private static string GetProcessKeysText(ColdRollMachineGroupConfigDto item)
        => string.Join("、", item.ProcessKeys.Select(ProcessDisplayHelper.GetProcessNameText));

    /// <summary>供给目标组显示（组 Key → 组显示名，未配置显示 —）</summary>
    private string GetSupplyTargetText(string? targetKey)
    {
        if (string.IsNullOrWhiteSpace(targetKey)) return "—";
        var match = _groupOptions.FirstOrDefault(g => string.Equals(g.GroupKey, targetKey, StringComparison.OrdinalIgnoreCase));
        return match == null ? targetKey : match.DisplayName;
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPage
        };
        await PageState.SaveAsync("cold_roll_machine_group_configs", state);
    }
}
