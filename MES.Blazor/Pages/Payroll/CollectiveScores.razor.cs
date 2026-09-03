using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using MES.Blazor.Services.Payroll;
using MES.Core.DTOs.Payroll;
using MES.Core.Helpers;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.Payroll;

/// <summary>
/// 集体计件月度评分页（2026-09-03，/payroll/collective-scores）：
/// 分值 1~10（最多 1 位小数，如 8.5），评定机制业务自理，系统仅录入保存（整月 upsert，null 清空）。
/// 页面级授权 [Authorize(SalaryView)] 声明在 .razor @attribute；编辑由 _canEdit（SalaryEditor/Full/Admin）门控。
/// 员工列表=当前在册集体计件（岗位分组展示），分组卡标题用岗位字典中文。
/// </summary>
public partial class CollectiveScores : IDisposable
{
    [Inject] private PayrollCollectiveService CollectiveSvc { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private bool _parametersLoaded;
    private bool _canEdit;

    private int _year = DateTime.Today.Year;
    private int _month = DateTime.Today.Month;

    // 年份/月份循环项：一律 @foreach 渲染，避开 @for 闭包捕获（同考勤表教训）
    private List<int> _years => Enumerable.Range(2024, DateTime.Today.Year + 2 - 2024 + 1).ToList();
    private List<int> _months => Enumerable.Range(1, 12).ToList();
    private bool _loading;

    // 月份加载并发序号：切月响应乱序防护（同考勤表）
    private int _loadSeq;

    private List<CollectiveScoreRowDto> _rows = new();

    // 分值编辑缓冲（employeeId → 当前界面分值；初始=后端已存，未评分=null）
    private Dictionary<int, decimal?> _scoreById = new();

    // 按岗位分组（组间 Position 序，组内行序由后端按工号保证）；空岗位归 "(未设岗位)" 处理在 razor
    private IEnumerable<IGrouping<string, CollectiveScoreRowDto>> Groups =>
        _rows.GroupBy(r => r.Position ?? "").OrderBy(g => g.Key, StringComparer.Ordinal);

    protected override Task OnInitializedAsync()
    {
        // 订阅字典显示映射注入事件：MainLayout 注入 OverrideMap 晚于页面首次渲染，
        // 事件回调 StateHasChanged 使岗位卡标题按参数表中文重渲染
        DictValueDisplayHelper.OverrideMapChanged += OnDictOverrideMapChanged;
        return Task.CompletedTask;
    }

    protected override async Task OnParametersSetAsync()
    {
        var state = await AuthProvider.GetAuthenticationStateAsync();
        var user = state.User;
        _canEdit = user.IsInRole(Roles.Menus.SalaryEditor)
                   || user.IsInRole(Roles.Menus.SalaryFull)
                   || user.IsInRole(Roles.Admin);
        if (!_parametersLoaded)
        {
            _parametersLoaded = true;
            await LoadScoresAsync();
        }
    }

    public void Dispose() => DictValueDisplayHelper.OverrideMapChanged -= OnDictOverrideMapChanged;

    private void OnDictOverrideMapChanged() => _ = InvokeAsync(() =>
    {
        StateHasChanged();
        return Task.CompletedTask;
    });

    private async Task LoadScoresAsync()
    {
        var seq = ++_loadSeq;
        _loading = true;
        try
        {
            var result = await CollectiveSvc.GetScoresAsync(_year, _month);
            if (seq != _loadSeq)
                return; // 已有更新的切月请求，丢弃过期响应
            if (result.Success && result.Data != null)
            {
                _rows = result.Data.Rows;
                _scoreById = _rows.ToDictionary(r => r.EmployeeId, r => r.Score);
            }
            else
            {
                Snackbar.Add(result.Message ?? "加载月度评分失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            if (seq == _loadSeq)
                Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            if (seq == _loadSeq)
            {
                _loading = false;
                // 切月是 fire-and-forget 调用：await 返回后的状态更新不在渲染管线内，须显式刷新
                StateHasChanged();
            }
        }
    }

    private decimal? GetScore(int employeeId) =>
        _scoreById.TryGetValue(employeeId, out var v) ? v : null;

    private void SetScore(int employeeId, decimal? value)
    {
        // 分值 1~10 且最多 1 位小数（与后端 SaveScoresAsync 校验同口径）
        if (value is { } v && (v < 1m || v > 10m || (v * 10m) != Math.Floor(v * 10m)))
        {
            Snackbar.Add("分值须在 1~10 且最多 1 位小数", Severity.Warning);
            StateHasChanged(); // 不改值，重渲染回退输入框为原值
            return;
        }
        _scoreById[employeeId] = value;
        StateHasChanged();
    }

    private async Task SaveScoresAsync()
    {
        if (_rows.Count == 0)
        {
            Snackbar.Add("没有可保存的员工", Severity.Warning);
            return;
        }

        var entries = _rows
            .Select(r => new CollectiveScoreEntryDto { EmployeeId = r.EmployeeId, Score = GetScore(r.EmployeeId) })
            .ToList();

        var result = await CollectiveSvc.SaveScoresAsync(new SaveCollectiveScoresDto
        {
            Year = _year,
            Month = _month,
            Entries = entries
        });
        if (result.Success)
            Snackbar.Add($"已保存 {_year}年{_month}月 {entries.Count} 名员工的月度评分", Severity.Success);
        else
            Snackbar.Add(result.Message ?? "保存评分失败", Severity.Error);

        // 保存成功后重载：以数据库为准（服务端对越界/非法值拦截后返回）
        await LoadScoresAsync();
    }

    private void OnYearChanged(int v) { _year = v; _ = LoadScoresAsync(); }
    private void OnMonthChanged(int v) { _month = v; _ = LoadScoresAsync(); }
    private void PrevMonth() { var d = new DateTime(_year, _month, 1).AddMonths(-1); _year = d.Year; _month = d.Month; _ = LoadScoresAsync(); }
    private void NextMonth() { var d = new DateTime(_year, _month, 1).AddMonths(1); _year = d.Year; _month = d.Month; _ = LoadScoresAsync(); }
}
