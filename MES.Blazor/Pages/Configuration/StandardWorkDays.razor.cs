using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.Constants;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.Configuration;
using System.Text.Json;

namespace MES.Blazor.Pages.Configuration;

public partial class StandardWorkDays
{
    private MudTable<StandardWorkDayDto>? table;
    private List<StandardWorkDayDto> _pageItems = new();
    private int _totalCount;
    private string _searchKeyword = string.Empty;
    private bool _isArrowNavSetup;
    private int _currentPage = 1;
    private int _restoredPageIndex;
    private bool _isFirstLoad = true;
    private int _pageSize = 10;

    // 排序状态
    private string sortColumn = "SectionName";
    private bool sortDescending = false;

    // ========== 列选择管理 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs() => new()
    {
        new() { Key = "SectionName",     Label = "工段名称",   SortKey = "sectionname",     FilterType = null, IsRequired = true },
        new() { Key = "SectionKey",      Label = "稳定 Key",   SortKey = "sectionkey",      FilterType = null },
        new() { Key = "EnglishName",     Label = "英文名",     SortKey = "englishname",     FilterType = null },
        new() { Key = "DisplayOrder",    Label = "显示顺序",   SortKey = "displayorder",    FilterType = null, IsRequired = true },
        new() { Key = "IsEnabled",       Label = "启用",       SortKey = "isenabled",       FilterType = null },
        new() { Key = "PlantGradePrefix",Label = "牌号前缀",   SortKey = "plantgradeprefix",FilterType = null },
        new() { Key = "StandardDays",    Label = "标准天数",   SortKey = "standarddays",    FilterType = null, IsRequired = true },
        new() { Key = "Remark",          Label = "备注",       SortKey = "remark",          FilterType = null },
    };

    // ========== 服务端数据加载 ==========

    private async Task<TableData<StandardWorkDayDto>> LoadDataFromServer(TableState state)
    {
        _pageSize = state.PageSize;
        try
        {
            var sortBy = _allColumns.FirstOrDefault(c => c.Key == sortColumn)?.SortKey ?? "sectionname";

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

            var result = await StandardWorkDayService.GetPagedAsync(query);

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
        return new TableData<StandardWorkDayDto>
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

    // ========== 列选择操作 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("standard_work_days", null, _allColumns);
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
        var saved = await ColumnPrefs.LoadAsync("standard_work_days", null);
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
        var savedState = await PageState.LoadAsync("standard_work_days");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "SectionName";
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
            if (!await JS.InvokeAsync<bool>("enableTableArrowNav", "#standard-work-days-list-table"))
                _isArrowNavSetup = false;
        }
    }

    // ========== 新增 ==========

    private async Task AddNew()
    {
        // 使用一个临时负 ID 作为新增行标识
        var hash = DateTime.Now.Ticks.GetHashCode();
        var newId = hash < 0 ? hash : -hash - 1;
        var newItem = new StandardWorkDayDto
        {
            Id = newId,
            SectionName = "",
            DisplayOrder = 999,
            IsEnabled = true,
            StandardDays = 1
        };

        // 如果当前在第一页，直接加到 pageItems
        if (_currentPage == 1)
        {
            _pageItems.Insert(0, newItem);
            StartEdit(newItem);
            await InvokeAsync(StateHasChanged);
        }
        else
        {
            // 不在第一页则跳转到第一页
            _currentPage = 1;
            _restoredPageIndex = 0;
            _isFirstLoad = true;
            if (table != null) await table.ReloadServerData();
            // 重新加载后通过状态保持新增模式较复杂，提示用户手动操作
            Snackbar.Add("请在首页点击\"新建\"添加记录", Severity.Info);
        }
    }

    // ========== 内联编辑 ==========

    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();
    private bool _isSaving;

    private class EditCache
    {
        public string SectionName { get; set; } = string.Empty;
        public string? SectionKey { get; set; }
        public string? EnglishName { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string? PlantGradePrefix { get; set; }
        public double StandardDays { get; set; }
        public string? Remark { get; set; }
    }

    // 编辑中的新增行 ID
    private bool IsNewItem(int id) => id < 0;

    private void StartEdit(StandardWorkDayDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new EditCache
        {
            SectionName = item.SectionName,
            SectionKey = item.SectionKey,
            EnglishName = item.EnglishName,
            DisplayOrder = item.DisplayOrder,
            IsEnabled = item.IsEnabled,
            PlantGradePrefix = item.PlantGradePrefix,
            StandardDays = item.StandardDays,
            Remark = item.Remark
        };
    }

    private void CancelEdit(StandardWorkDayDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);

        // 如果是新增行且取消编辑，移除该行
        if (IsNewItem(item.Id))
        {
            _pageItems.Remove(item);
            StateHasChanged();
        }
    }

    private async Task SaveEdit(StandardWorkDayDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.SectionName)) errors.Add("工段名称不能为空");
        if (cache.DisplayOrder <= 0) errors.Add("显示顺序必须大于0");
        if (cache.StandardDays <= 0) errors.Add("标准天数必须大于0");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        StateHasChanged();

        try
        {
            // SectionKey 为空时按工段名称自动反查填充（历史数据/新增行保底）
            var sectionKey = cache.SectionKey;
            if (string.IsNullOrWhiteSpace(sectionKey))
            {
                sectionKey = SectionDefs.PropertyToName
                    .FirstOrDefault(kv => string.Equals(kv.Value, cache.SectionName, StringComparison.OrdinalIgnoreCase))
                    .Key;

                // 反查不到（自定义名且无预置 Key）时阻止保存，避免产生无 Key 行
                if (string.IsNullOrWhiteSpace(sectionKey))
                {
                    Snackbar.Add($"工段名\"{cache.SectionName}\"不在预置 26 工段中，无法自动映射稳定 Key；请改用预置工段名，或复用\"备用1/备用2\"槽位改名", Severity.Warning);
                    return;
                }
            }

            var dto = new StandardWorkDayDto
            {
                Id = IsNewItem(item.Id) ? 0 : item.Id,
                SectionName = cache.SectionName,
                SectionKey = sectionKey,
                EnglishName = cache.EnglishName,
                DisplayOrder = cache.DisplayOrder,
                IsEnabled = cache.IsEnabled,
                PlantGradePrefix = cache.PlantGradePrefix,
                StandardDays = cache.StandardDays,
                Remark = cache.Remark
            };

            var result = await StandardWorkDayService.SaveAsync(dto);
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

    private async Task DeleteItem(StandardWorkDayDto item)
    {
        // 新增行直接移除
        if (IsNewItem(item.Id))
        {
            _pageItems.Remove(item);
            StateHasChanged();
            return;
        }

        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除\"{item.SectionName}\" 的标准工量天数配置吗？",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await StandardWorkDayService.DeleteAsync(item.Id);
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
        await PageState.SaveAsync("standard_work_days", state);
    }
}
