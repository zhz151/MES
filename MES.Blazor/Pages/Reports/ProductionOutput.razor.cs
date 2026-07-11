using System.Text.Json;
using MudBlazor;
using Microsoft.JSInterop;
using MES.Core.DTOs.Report;
using MES.Blazor.Services;

namespace MES.Blazor.Pages.Reports;

public partial class ProductionOutput
{
    private string _dateFrom = "";
    private string _dateTo = "";
    private bool _loading;
    private bool _hasSearched;

    private List<ColumnDef> _allColumns = new();
    private List<ColumnDef> _cachedVisibleColumns = new();

    private List<DailyProductionReportRow> _rows = new();
    private DailyProductionReportRow? _summaryRow;
    private const string ColumnPrefsKey = "production_output";

    protected override void OnInitialized()
    {
        SetThisMonth();
    }

    private void SetThisMonth()
    {
        var today = DateTime.Today;
        _dateFrom = new DateTime(today.Year, today.Month, 1).ToString("yyyy-MM-dd");
        _dateTo = today.ToString("yyyy-MM-dd");
    }

    private void SetLastMonth()
    {
        var firstOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var lastMonthEnd = firstOfMonth.AddDays(-1);
        var lastMonthStart = new DateTime(lastMonthEnd.Year, lastMonthEnd.Month, 1);
        _dateFrom = lastMonthStart.ToString("yyyy-MM-dd");
        _dateTo = lastMonthEnd.ToString("yyyy-MM-dd");
    }

    private async Task LoadDataAsync()
    {
        if (!DateTime.TryParse(_dateFrom, out var fromDate))
        {
            Snackbar.Add("请输入有效的起始日期（yyyy-MM-dd）", Severity.Warning);
            return;
        }
        if (!DateTime.TryParse(_dateTo, out var toDate))
        {
            Snackbar.Add("请输入有效的结束日期（yyyy-MM-dd）", Severity.Warning);
            return;
        }
        if (fromDate > toDate)
        {
            Snackbar.Add("起始日期不能晚于结束日期", Severity.Warning);
            return;
        }

        _loading = true;
        _hasSearched = false;
        StateHasChanged();

        try
        {
            var response = await ReportService.GetDailyProductionReportAsync(fromDate, toDate);
            if (response?.Data != null)
            {
                var rows = response.Data.Rows;
                BuildColumns(response.Data.SectionColumns);

                // 汇总行独立存储，通过 FooterContent 渲染
                _summaryRow = new DailyProductionReportRow
                {
                    Date = DateTime.MinValue,
                    DisplayDate = "合计",
                    Values = new Dictionary<string, decimal>()
                };
                foreach (var col in response.Data.SectionColumns)
                {
                    _summaryRow.Values[col] = rows.Sum(r => r.Values.GetValueOrDefault(col, 0m));
                }

                _rows = rows;
                await RestoreColumnPrefsAsync();
            }
            else
            {
                _rows = new();
                _allColumns = new();
                _summaryRow = null;
                Snackbar.Add(response?.Message ?? "查询失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"查询异常: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loading = false;
            _hasSearched = true;
            StateHasChanged();
        }
    }

    private static List<ColumnDef> GetDefaultColumnDefs(IReadOnlyList<string> sectionColumns)
    {
        // 重量列宽固定 100px，标签列宽随内容
        return sectionColumns.Select(col => new ColumnDef
        {
            Key = col,
            Label = col,
            Visible = true,
            GroupKey = 1,
            GroupName = "产量项目",
            Width = col.Length > 4 ? "110" : "100"
        }).ToList();
    }

    private void BuildColumns(IReadOnlyList<string> sectionColumns)
    {
        _allColumns = GetDefaultColumnDefs(sectionColumns);
        UpdateCachedVisibleColumns();
    }

    private void UpdateCachedVisibleColumns()
    {
        _cachedVisibleColumns = _allColumns.Where(c => c.Visible).ToList();
    }

    private async Task RestoreColumnPrefsAsync()
    {
        var saved = await ColumnPrefs.LoadAsync(ColumnPrefsKey, null);
        if (saved.Count > 0)
        {
            var savedKeys = saved.Select(c => c.Key).ToHashSet();
            var newCols = _allColumns.Where(c => !savedKeys.Contains(c.Key)).ToList();

            _allColumns = saved
                .Select(s => _allColumns.FirstOrDefault(c => c.Key == s.Key))
                .Where(c => c != null)
                .Cast<ColumnDef>()
                .ToList();

            foreach (var savedCol in saved)
            {
                var match = _allColumns.FirstOrDefault(c => c.Key == savedCol.Key);
                if (match != null)
                    match.Visible = savedCol.Visible;
            }

            _allColumns.AddRange(newCols);
        }

        UpdateCachedVisibleColumns();
    }

    private async Task SaveColumnPrefsAsync()
    {
        await ColumnPrefs.SaveAsync(ColumnPrefsKey, null, _allColumns);
    }

    private async Task ResetColumnDisplay()
    {
        _allColumns.ForEach(c => c.Visible = true);
        UpdateCachedVisibleColumns();
        await SaveColumnPrefsAsync();
        Snackbar.Add("列显示已重置", Severity.Info);
    }

    private async Task OnColumnToggle(ColumnDef col)
    {
        UpdateCachedVisibleColumns();
        await SaveColumnPrefsAsync();
    }

    private async Task OnMoveUp(ColumnDef col)
    {
        UpdateCachedVisibleColumns();
        await SaveColumnPrefsAsync();
    }

    private async Task OnMoveDown(ColumnDef col)
    {
        UpdateCachedVisibleColumns();
        await SaveColumnPrefsAsync();
    }

    private async Task PrintReport()
    {
        if (_rows.Count == 0)
        {
            Snackbar.Add("暂无数据可打印", Severity.Warning);
            return;
        }

        try
        {
            var fromDate = DateTime.TryParse(_dateFrom, out var dFrom) ? dFrom.ToString("yyyy-MM-dd") : "";
            var toDate = DateTime.TryParse(_dateTo, out var dTo) ? dTo.ToString("yyyy-MM-dd") : "";

            // 传递列定义（key + visible 顺序）给后端
            var columns = _cachedVisibleColumns.Select(c => new { key = c.Key, label = c.Label }).ToList();
            var request = new
            {
                fromDate,
                toDate,
                columns
            };

            Snackbar.Add("正在生成PDF...", Severity.Info);
            var apiUrl = $"{Http.BaseAddress}api/report/daily-output/print-file";
            var json = JsonSerializer.Serialize(request);
            await JS.InvokeVoidAsync("openPdfFromApi", apiUrl, json);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }
}
