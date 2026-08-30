using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Constants;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Pages.Configuration;

/// <summary>
/// 冷轧机台数配置页：按单冷轧类型的机台数参数维护（新建/内联编辑/删除）
/// </summary>
public partial class ColdRollMachineConfigs
{
    private MudTable<ColdRollMachineConfigDto>? table;
    private List<ColdRollMachineConfigDto> _pageItems = new();
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
    private string sortColumn = "ProcessType";
    private bool sortDescending = false;

    /// <summary>机型选项（配置驱动：ProcessDefinitions 冷轧/冷拔工序，OnInitializedAsync 加载）</summary>
    private List<ProcessInfoDto> _machineTypeOptions = new();

    private async Task LoadMachineTypeOptionsAsync()
    {
        var result = await ProcessDefSvc.GetColdRollOptionsAsync();
        if (result.Success && result.Data != null)
            _machineTypeOptions = result.Data;
    }

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "ProcessType",        Label = "机型",         SortKey = "processtype",        IsRequired = true },
        new() { Key = "OwnedCount",         Label = "本厂数量",     SortKey = "ownedcount" },
        new() { Key = "MinMachines",        Label = "最小机台数",   SortKey = "minmachines" },
        new() { Key = "MaxMachines",        Label = "最大机台数",   SortKey = "maxmachines" },
        new() { Key = "EstimatedDailyOutput", Label = "估算单机日产(kg/天)", SortKey = "estimateddailyoutput" },
        new() { Key = "Remark",             Label = "备注",         SortKey = "remark" },
        new() { Key = "UpdatedTime",        Label = "更新时间",     SortKey = "updatedtime" },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<ColdRollMachineConfigDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        var version = ++_loadVersion;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "processtype";

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
                return new TableData<ColdRollMachineConfigDto> { Items = _pageItems, TotalItems = _totalCount };

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
        return new TableData<ColdRollMachineConfigDto>
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
        await ColumnPrefs.SaveAsync("cold_roll_machine_configs", null, _allColumns);
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

    // ========== 初始化 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();
        await LoadMachineTypeOptionsAsync();
        var saved = await ColumnPrefs.LoadAsync("cold_roll_machine_configs", null);
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
        var savedState = await PageState.LoadAsync("cold_roll_machine_configs");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "ProcessType";
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
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#cold-roll-machine-configs-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 新增 ==========

    private async Task AddNew()
    {
        var hash = DateTime.Now.Ticks.GetHashCode();
        var newId = hash < 0 ? hash : -hash - 1;
        var newItem = new ColdRollMachineConfigDto
        {
            Id = newId,
            ProcessType = _machineTypeOptions.FirstOrDefault()?.ProcessKey ?? "",
            OwnedCount = 0,
            MinMachines = 0,
            MaxMachines = 0,
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
        public string ProcessType { get; set; } = string.Empty;
        public int OwnedCount { get; set; }
        public int MinMachines { get; set; }
        public int MaxMachines { get; set; }
        public decimal? EstimatedDailyOutput { get; set; }
        public string? Remark { get; set; }
    }

    private bool IsNewItem(int id) => id < 0;

    private void StartEdit(ColdRollMachineConfigDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            ProcessType = item.ProcessType,
            OwnedCount = item.OwnedCount,
            MinMachines = item.MinMachines,
            MaxMachines = item.MaxMachines,
            EstimatedDailyOutput = item.EstimatedDailyOutput,
            Remark = item.Remark
        };
    }

    private void CancelEdit(ColdRollMachineConfigDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);

        if (IsNewItem(item.Id))
        {
            _pageItems.Remove(item);
            StateHasChanged();
        }
    }

    private async Task SaveEdit(ColdRollMachineConfigDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.ProcessType)) errors.Add("机型不能为空");
        if (cache.OwnedCount < 0 || cache.MinMachines < 0 || cache.MaxMachines < 0) errors.Add("机台数不能为负");
        if (cache.MinMachines > cache.MaxMachines) errors.Add("最小机台数不能大于最大机台数");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var dto = new ColdRollMachineConfigDto
            {
                Id = IsNewItem(item.Id) ? 0 : item.Id,
                ProcessType = cache.ProcessType,
                OwnedCount = cache.OwnedCount,
                MinMachines = cache.MinMachines,
                MaxMachines = cache.MaxMachines,
                EstimatedDailyOutput = cache.EstimatedDailyOutput,
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

    private async Task DeleteItem(ColdRollMachineConfigDto item)
    {
        if (IsNewItem(item.Id))
        {
            _pageItems.Remove(item);
            StateHasChanged();
            return;
        }

        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除机型 \"{ProcessDisplayHelper.GetProcessNameText(item.ProcessType)}\" 的机台数配置吗？",
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
        await PageState.SaveAsync("cold_roll_machine_configs", state);
    }
}
