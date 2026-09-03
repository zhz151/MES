using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Services.Payroll;
using MES.Core.DTOs.Payroll;
using MES.Core.Helpers;
using MES.Shared.Constants;
using System.Text.Json;

namespace MES.Blazor.Pages.Payroll;

/// <summary>
/// 「月工资津贴汇总」页（2026-09-04，/payroll/monthly-summary）：
/// 员工某结算月完整应发/实发（各子页已保存金额 + 考勤派生），列序对齐《工资条及打印.xlsx》。
/// 网格只读展示实时重算行；「保存本月」整月替换快照（SalaryEdit）→ 打印读快照保证发放单冻结口径一致。
/// 顶部徽标区分「已保存/未保存」，未保存时打印禁用并提示先保存。
/// 金额列 0 留空（贴近 Excel），处罚/代缴为负值（源表正数录入、汇总存负）。
/// </summary>
public partial class MonthlySummary : IDisposable
{
    [Inject] private PayrollMonthlySummaryService SummarySvc { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private HttpClient Http { get; set; } = null!;

    // 报表列（Key=PayrollMonthlySummaryRowDto 属性名；IsMoney=金额列，0 网格留空）
    private static readonly (string Key, string Label, bool IsMoney)[] Columns =
    {
        ("AttendanceDays", "出勤天数", false),
        ("BaseWage", "本月基础工资", true),
        ("MiscWorkAmount", "本月杂辅工资", true),
        ("PositionAllowance", "岗位补贴", true),
        ("SeniorityBonus", "工龄奖", true),
        ("FullAttendanceBonus", "满勤奖", true),
        ("LeadBonus", "带班费", true),
        ("NightShiftAllowance", "夜班津贴", true),
        ("HighTempAllowance", "高温费", true),
        ("InjurySubsidy", "工伤补贴", true),
        ("Penalty", "处罚", true),
        ("SocialSecurity", "代缴社保", true),
        ("TotalPayable", "应发工资及津贴", true),
        ("TotalPaid", "实发工资及津贴", true),
    };

    private bool _parametersLoaded;
    private bool _canEdit;

    private int _year = DateTime.Today.Year;
    private int _month = DateTime.Today.Month;

    // 年份/月份循环项：一律 @foreach 渲染，避开 @for 闭包捕获（同考勤表教训）
    private List<int> _years => Enumerable.Range(2024, DateTime.Today.Year + 2 - 2024 + 1).ToList();
    private List<int> _months => Enumerable.Range(1, 12).ToList();
    private bool _loading;
    private bool _hasSaved;

    // 月份加载并发序号：切月响应乱序防护（同考勤表）
    private int _loadSeq;

    private List<PayrollMonthlySummaryRowDto> _rows = new();
    private string _keyword = string.Empty;

    private List<PayrollMonthlySummaryRowDto> FilteredRows
    {
        get
        {
            if (_rows.Count == 0) return _rows;
            var kw = _keyword.Trim();
            if (string.IsNullOrWhiteSpace(kw)) return _rows;
            return _rows.Where(r =>
                r.EmployeeCode.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || r.EmployeeName.Contains(kw, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    protected override Task OnInitializedAsync()
    {
        // 订阅字典显示映射注入事件：MainLayout 注入 OverrideMap 晚于页面首次渲染，
        // 事件回调 StateHasChanged 使岗位类别/岗位文本按参数表中文重渲染
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
            await LoadMonthAsync();
        }
    }

    public void Dispose() => DictValueDisplayHelper.OverrideMapChanged -= OnDictOverrideMapChanged;

    private void OnDictOverrideMapChanged() => _ = InvokeAsync(() =>
    {
        StateHasChanged();
        return Task.CompletedTask;
    });

    // ========== 加载 ==========

    private async Task LoadMonthAsync()
    {
        var seq = ++_loadSeq;
        _loading = true;
        try
        {
            var result = await SummarySvc.GetMonthAsync(_year, _month);
            if (seq != _loadSeq)
                return; // 已有更新的切月请求，丢弃过期响应
            if (result.Success && result.Data != null)
            {
                _rows = result.Data.Rows;
                _hasSaved = result.Data.HasSaved;
            }
            else
            {
                Snackbar.Add(result.Message ?? "加载月工资汇总失败", Severity.Error);
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

    // ========== 单元格展示 ==========

    private string CellText(PayrollMonthlySummaryRowDto row, string key) => key switch
    {
        "AttendanceDays" => row.AttendanceDays.ToString(),
        "BaseWage" => Money(row.BaseWage),
        "MiscWorkAmount" => Money(row.MiscWorkAmount),
        "PositionAllowance" => Money(row.PositionAllowance),
        "SeniorityBonus" => Money(row.SeniorityBonus),
        "FullAttendanceBonus" => Money(row.FullAttendanceBonus),
        "LeadBonus" => Money(row.LeadBonus),
        "NightShiftAllowance" => Money(row.NightShiftAllowance),
        "HighTempAllowance" => Money(row.HighTempAllowance),
        "InjurySubsidy" => Money(row.InjurySubsidy),
        "Penalty" => Money(row.Penalty),
        "SocialSecurity" => Money(row.SocialSecurity),
        "TotalPayable" => Money(row.TotalPayable),
        "TotalPaid" => Money(row.TotalPaid),
        _ => string.Empty,
    };

    private static string Money(decimal v) => v == 0 ? string.Empty : v.ToString("G29");

    private decimal ColumnTotal(string key) => FilteredRows.Sum(r =>
        key switch
        {
            "BaseWage" => r.BaseWage,
            "MiscWorkAmount" => r.MiscWorkAmount,
            "PositionAllowance" => r.PositionAllowance,
            "SeniorityBonus" => r.SeniorityBonus,
            "FullAttendanceBonus" => r.FullAttendanceBonus,
            "LeadBonus" => r.LeadBonus,
            "NightShiftAllowance" => r.NightShiftAllowance,
            "HighTempAllowance" => r.HighTempAllowance,
            "InjurySubsidy" => r.InjurySubsidy,
            "Penalty" => r.Penalty,
            "SocialSecurity" => r.SocialSecurity,
            "TotalPayable" => r.TotalPayable,
            "TotalPaid" => r.TotalPaid,
            _ => 0m,
        });

    // ========== 保存 / 打印 ==========

    private async Task SaveMonthAsync()
    {
        if (_rows.Count == 0)
        {
            Snackbar.Add("本月没有可汇总的员工", Severity.Warning);
            return;
        }

        var result = await SummarySvc.SaveMonthAsync(new SaveMonthlySummaryDto { Year = _year, Month = _month });
        if (result.Success)
        {
            Snackbar.Add(result.Data > 0
                ? $"已保存本月工资津贴汇总 {result.Data} 人（整月替换快照）"
                : "本月汇总为空，未产生快照", result.Data > 0 ? Severity.Success : Severity.Info);
            await LoadMonthAsync();
        }
        else
        {
            Snackbar.Add(result.Message ?? "保存失败", Severity.Error);
        }
    }

    /// <summary>调用 print.js openPdfFromApi：POST JSON 到 -file 端点，浏览器打开下载 PDF（JWT 从 localStorage 读取）</summary>
    private async Task PrintAsync(string endpoint)
    {
        if (!_hasSaved)
        {
            Snackbar.Add("本月尚未生成工资汇总快照，请先「保存本月」后再打印", Severity.Warning);
            return;
        }
        try
        {
            var apiUrl = $"{Http.BaseAddress}{ApiEndpoints.PayrollMonthlySummary}/{endpoint}";
            var body = JsonSerializer.Serialize(new { year = _year, month = _month });
            await Js.InvokeVoidAsync("openPdfFromApi", apiUrl, body);
            Snackbar.Add("正在生成PDF...", Severity.Info);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打印失败: {ex.Message}", Severity.Error);
        }
    }

    private Task PrintAllAsync() => PrintAsync("print-all-file");
    private Task PrintPersonalAsync() => PrintAsync("print-personal-file");

    // ========== 月份导航 ==========

    private void OnYearChanged(int v) { _year = v; _ = LoadMonthAsync(); }
    private void OnMonthChanged(int v) { _month = v; _ = LoadMonthAsync(); }
    private void PrevMonth() { var d = new DateTime(_year, _month, 1).AddMonths(-1); _year = d.Year; _month = d.Month; _ = LoadMonthAsync(); }
    private void NextMonth() { var d = new DateTime(_year, _month, 1).AddMonths(1); _year = d.Year; _month = d.Month; _ = LoadMonthAsync(); }
    private void OnKeywordChanged(string v) { _keyword = v ?? string.Empty; StateHasChanged(); }
}
