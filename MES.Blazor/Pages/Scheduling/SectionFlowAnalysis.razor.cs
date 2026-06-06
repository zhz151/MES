using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Blazor.Models;
using MES.Blazor.Services;
using MES.Core.DTOs;
using System.Text.Json;

namespace MES.Blazor.Pages.Scheduling;

public partial class SectionFlowAnalysis
{
    private MudTable<SectionFlowAnalysisDto>? table;
    private List<SectionFlowAnalysisDto> _allItems = new();
    private List<SectionFlowAnalysisDto> _filteredItems = new();
    private bool _isLoading;

    // ========== 页面状态持久化 ==========
    private int _restoredPageIndex;
    private int _currentPageIndex = 1;
    private string _searchKeyword = string.Empty;

    // ========== 排序状态 ==========
    private string sortColumn = "Category";
    private bool sortDescending = false;

    // ========== 列定义 ==========
    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _visibleColumns =>
        _allColumns.Where(c => c.Visible).ToList();

    private static List<ColumnDef> GetAllColumnDefs()
    {
        return new List<ColumnDef>
        {
            new() { Key = "Category",        Label = "段落类别",   SortKey = "category",        FilterType = "string", IsRequired = true },
            new() { Key = "PendingTotal",    Label = "段落待产总量", SortKey = "pendingtotal",  FilterType = "number" },
            new() { Key = "SustainableDays", Label = "可持续天数",  SortKey = "sustainabledays",FilterType = "number" },
            new() { Key = "StatusJudgment",  Label = "状态判定",   SortKey = "statusjudgment",  FilterType = "string", IsRequired = true },
        };
    }

    // ========== 生命周期 ==========

    protected override async Task OnInitializedAsync()
    {
        _allColumns = GetAllColumnDefs();

        // 恢复列显隐偏好
        var saved = await ColumnPrefs.LoadAsync("section-flow-analysis", null);
        if (saved.Count > 0)
        {
            foreach (var s in saved)
            {
                var match = _allColumns.FirstOrDefault(c => c.Key == s.Key);
                if (match != null) match.Visible = s.Visible;
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
        var savedState = await PageState.LoadAsync("section-flow-analysis");
        if (savedState != null)
        {
            sortColumn = savedState.SortBy ?? "Category";
            sortDescending = savedState.IsDescending;
            _searchKeyword = savedState.Keyword ?? "";
            _restoredPageIndex = savedState.PageIndex;
        }

        await LoadDataAsync();
    }

    // ========== 数据加载 ==========

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        try
        {
            var result = await Service.GetAnalysisAsync();
            if (result.Success && result.Data != null)
            {
                _allItems = result.Data;
            }
            else
            {
                Snackbar.Add(result.Message ?? "获取数据失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }

        ApplyFiltersAndSort();
    }

    // ========== 搜索 ==========

    private async Task OnSearchChanged(string value)
    {
        _searchKeyword = value ?? string.Empty;
        ApplyFiltersAndSort();
        await SavePageStateAsync();
    }

    // ========== 筛选和排序 ==========

    private void ApplyFiltersAndSort()
    {
        var filtered = _allItems.AsEnumerable();

        // 关键词搜索
        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            var kw = _searchKeyword.Trim();
            filtered = filtered.Where(x =>
                (x.CategoryCode?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true) ||
                (x.CategoryName?.Contains(kw, StringComparison.OrdinalIgnoreCase) == true));
        }

        // 内存排序
        filtered = sortColumn switch
        {
            "Category" => sortDescending
                ? filtered.OrderByDescending(x => x.CategoryCode)
                : filtered.OrderBy(x => x.CategoryCode),
            "PendingTotal" => sortDescending
                ? filtered.OrderByDescending(x => x.PendingTotal)
                : filtered.OrderBy(x => x.PendingTotal),
            "SustainableDays" => sortDescending
                ? filtered.OrderByDescending(x => x.SustainableDays)
                : filtered.OrderBy(x => x.SustainableDays),
            "StatusJudgment" => sortDescending
                ? filtered.OrderByDescending(x => x.StatusJudgment)
                : filtered.OrderBy(x => x.StatusJudgment),
            _ => filtered.OrderBy(x => x.CategoryCode)
        };

        _filteredItems = filtered.ToList();
    }

    private async Task ToggleSort(string key)
    {
        if (sortColumn == key)
            sortDescending = !sortDescending;
        else
        {
            sortColumn = key;
            sortDescending = false;
        }
        ApplyFiltersAndSort();
        await SavePageStateAsync();
    }

    // ========== 列显隐 ==========

    private async Task OnColumnToggle(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task MoveColumnUp(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task MoveColumnDown(ColumnDef col)
    {
        await SaveColumnPrefs();
    }

    private async Task SaveColumnPrefs()
    {
        await ColumnPrefs.SaveAsync("section-flow-analysis", null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns = GetAllColumnDefs();
        await SaveColumnPrefs();
        StateHasChanged();
    }

    // ========== 持久化 ==========

    private async Task SavePageStateAsync()
    {
        var state = new PageState
        {
            SortBy = sortColumn,
            IsDescending = sortDescending,
            Keyword = string.IsNullOrWhiteSpace(_searchKeyword) ? null : _searchKeyword,
            PageIndex = _currentPageIndex,
        };
        await PageState.SaveAsync("section-flow-analysis", state);
    }

    // ========== 显示辅助 ==========

    private static string RenderInt(decimal? val)
    {
        return val.HasValue ? ((int)val.Value).ToString() : "-";
    }

    private static string RenderDecimal(decimal? val)
    {
        return val.HasValue ? val.Value.ToString("F1") : "-";
    }

    private static Color GetStatusColor(string? status)
    {
        return status switch
        {
            "偏少" => Color.Error,
            "过多" => Color.Warning,
            "正常" => Color.Success,
            _ => Color.Default
        };
    }
}
