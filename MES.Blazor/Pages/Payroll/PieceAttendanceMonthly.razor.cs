using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Blazor.Services.Payroll;
using MES.Blazor.Shared;
using MES.Core.DTOs.Payroll;
using MES.Core.Helpers;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.Payroll;

/// <summary>
/// 靠工计件月结页（2026-09-03，/payroll/attendance-monthly）：
/// 靠工工资（月）= 靠工岗位当月平均小时工资 × 本人当月实出勤小时 × 靠工系数；
/// 平均小时工资 = 选中岗位（个人计件 + 集体计件并集）当月计件总工资 ÷ 同批岗位计件人员总出勤小时（分子分母各自合并）。
/// 员工集合 = 当前在册靠工计件员工 ∪ 当月已有月结快照员工（停用/换模式后历史月仍可见可改）。
/// 打开月份默认显示已保存快照（金额冻结）；未保存月直接显示引擎草稿。
/// 重算两种模式（与集体月结/每日工资两表同款，见 PayrollFullRecalcDialogs）：
/// 「引擎重算」仅覆盖在册靠工员工为引擎草稿（历史快照员工保留已存避免误清）；
/// 「全量重算（清历史）」额外把历史快照员工当月清空（保存后其当月旧记录删除、彻底离场，双重确认防误操作）。
/// 「保存本月」整月落库（Amount&gt;0 存、空删）。
/// 页面级授权 [Authorize(SalaryView)] 声明在 .razor @attribute；编辑写入由 _canEdit（SalaryEditor/Full/Admin）门控。
/// </summary>
public partial class PieceAttendanceMonthly : IDisposable
{
    [Inject] private PayrollAttendanceService AttendanceSvc { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
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

    private AttendanceWageMonthDto? _data;

    // 当月是否已有保存快照（决定打开默认显示引擎草稿还是已保存值）
    private bool _hasSaved;
    private List<string> _warnings = new();

    // 金额编辑缓冲（已保存值 或 引擎草稿 或 用户改动），null = 该员工当月 0 元（不落库）
    private Dictionary<int, decimal?> _amountById = new();

    private IEnumerable<AttendanceWageRowDto> Rows => _data?.Rows ?? Enumerable.Empty<AttendanceWageRowDto>();

    private int MembersCount => Rows.Count();

    protected override Task OnInitializedAsync()
    {
        // 订阅字典显示映射注入事件：MainLayout 注入 OverrideMap 晚于页面首次渲染，
        // 事件回调 StateHasChanged 使岗位文本按参数表中文重渲染
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
            var result = await AttendanceSvc.GetMonthAsync(_year, _month);
            if (seq != _loadSeq)
                return; // 已有更新的切月请求，丢弃过期响应
            if (result.Success && result.Data != null)
            {
                _data = result.Data;
                _hasSaved = result.Data.HasSaved;
                _warnings = result.Data.Warnings;
                _amountById = new Dictionary<int, decimal?>();
                foreach (var m in result.Data.Rows)
                {
                    // 打开默认：已保存月显示已保存快照（保存后改产/改薪不影响已存）；
                    // 未保存月直接显示引擎自动带出草稿（历史快照员工无引擎草稿 → null）；
                    // 工资一律四舍五入到整元（RoundYuan），null 保持 null（该员工当月无值）
                    var saved = m.SavedAmount;
                    var engine = m.EngineAmount;
                    _amountById[m.EmployeeId] = (_hasSaved ? saved : engine) is { } src
                        ? DisplayHelper.RoundYuan(src)
                        : null;
                }
            }
            else
            {
                Snackbar.Add(result.Message ?? "加载靠工月结失败", Severity.Error);
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

    // ========== 靠工岗位中文显示辅助 ==========

    /// <summary>靠工岗位逗号串 → 中文（"、" 连接，逐项经 DisplayHelper.GetPositionText）</summary>
    private static string FormatPositions(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return string.Join("、", value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => DisplayHelper.GetPositionText(s.Trim())));
    }

    private static bool HasPositions(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Split(',', StringSplitOptions.RemoveEmptyEntries).Any(s => !string.IsNullOrWhiteSpace(s.Trim()));

    // ========== 引擎重算 ==========

    /// <summary>
    /// 按引擎草稿覆盖网格：仅覆盖「引擎覆盖」员工（当前在册靠工员工），
    /// 历史快照并入的员工无引擎值，保留其已保存值避免误清（其快照仅供回溯查看/修改）。
    /// </summary>
    private async Task RecalcAsync()
    {
        var dialog = DialogService.Show<ConfirmDialog>("引擎重算确认", new DialogParameters
        {
            ["ContentText"] = $"将按现行单价与当月产量源/出勤重新计算 {_year}年{_month}月各靠工员工参照时薪与月得，"
                              + "并覆盖网格为草稿（已保存快照不受影响，需点「保存本月」才覆盖落库）。确定重算？",
            ["ConfirmText"] = "确定重算",
            ["Color"] = Color.Warning
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        var covered = 0;
        foreach (var m in Rows.Where(m => m.EngineCovered))
        {
            _amountById[m.EmployeeId] = m.EngineAmount is { } src ? DisplayHelper.RoundYuan(src) : null;
            covered++;
        }
        Snackbar.Add($"已按引擎草稿覆盖网格（{covered} 名靠工员工），可再手工修改后保存", Severity.Info);
        StateHasChanged();
    }

    /// <summary>
    /// 全量重算（清历史，模式 2）：把当月所有显示员工按引擎覆盖——
    /// 在册靠工员工（EngineCovered）→ 引擎草稿；历史快照员工引擎无草稿 → 当月清空。
    /// 点「保存本月」后历史快照员工当月旧月结记录被删除、下回加载不再并入显示（彻底离场）。
    /// ⚠️ 破坏性：历史快照是结算时实结并冻结，误清不可找回 → 双重确认（复用 PayrollFullRecalcDialogs）防误操作。
    /// </summary>
    private async Task RecalcFullAsync()
    {
        if (MembersCount == 0) return;
        var covered = Rows.Count(m => m.EngineCovered);
        var history = Rows.Count(m => !m.EngineCovered);

        if (!await PayrollFullRecalcDialogs.ConfirmFullRecalcAsync(
                DialogService, "靠工计件月结", _year, _month, covered, history))
            return;

        foreach (var m in Rows)
        {
            // 历史快照员工 → 清空当月；在册靠工员工 → 引擎草稿（RoundYuan，0 清空沿用 SetAmount 语义）
            if (!m.EngineCovered) { _amountById[m.EmployeeId] = null; continue; }
            _amountById[m.EmployeeId] = m.EngineAmount is { } src ? DisplayHelper.RoundYuan(src) : null;
        }

        Snackbar.Add(history > 0
            ? $"已全量覆盖：在册 {covered} 人→引擎草稿，历史快照 {history} 人→清空；点「保存本月」生效（将删除其当月旧记录）"
            : $"已覆盖 {covered} 名在册靠工员工为引擎草稿，点「保存本月」生效", Severity.Warning);
        StateHasChanged();
    }

    // ========== 单元格 ==========

    private string GetAmountText(int employeeId) =>
        _amountById.TryGetValue(employeeId, out var v) && v.HasValue ? v.Value.ToString("G29") : "";

    private void OnAmountChanged(int employeeId, ChangeEventArgs e)
    {
        var raw = (e.Value as string)?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            SetAmount(employeeId, null);
            return;
        }
        if (decimal.TryParse(raw, out var v))
        {
            if (v < 0)
            {
                Snackbar.Add("实得金额不可为负", Severity.Warning);
                return; // 不改值，重渲染回退输入框为原值
            }
            // 工资按整元：四舍五入到元后落库；0 = 该员工当月 0 元（等同清空，不落库）
            var yuan = DisplayHelper.RoundYuan(v);
            SetAmount(employeeId, yuan > 0 ? yuan : null);
        }
        else
        {
            Snackbar.Add("请输入有效的金额数字", Severity.Warning);
        }
    }

    private void SetAmount(int employeeId, decimal? value)
    {
        _amountById[employeeId] = value;
        StateHasChanged();
    }

    // 全员实得金额合计（当前网格可编辑值）
    private decimal TotalAmount =>
        Rows.Sum(m => _amountById.TryGetValue(m.EmployeeId, out var v) ? v ?? 0m : 0m);

    // ========== 保存 / 月份导航 ==========

    private async Task SaveMonthAsync()
    {
        if (MembersCount == 0)
        {
            Snackbar.Add("没有可保存的靠工员工", Severity.Warning);
            return;
        }

        var entries = new List<AttendanceWageEntryDto>();
        foreach (var m in Rows)
        {
            var amount = _amountById.TryGetValue(m.EmployeeId, out var v) ? v : null;
            entries.Add(new AttendanceWageEntryDto { EmployeeId = m.EmployeeId, Amount = amount is > 0m ? amount : null });
        }

        var result = await AttendanceSvc.SaveMonthAsync(new SaveAttendanceWageDto
        {
            Year = _year,
            Month = _month,
            Entries = entries
        });
        if (result.Success)
            Snackbar.Add($"已保存 {_year}年{_month}月 {entries.Count} 名靠工员工的月结", Severity.Success);
        else
            Snackbar.Add(result.Message ?? "保存失败", Severity.Error);

        // 保存成功后重载：默认显示已保存快照，与引擎草稿解耦（改产/改薪/改出勤不再影响已存）
        await LoadMonthAsync();
    }

    private void OnYearChanged(int v) { _year = v; _ = LoadMonthAsync(); }
    private void OnMonthChanged(int v) { _month = v; _ = LoadMonthAsync(); }
    private void PrevMonth() { var d = new DateTime(_year, _month, 1).AddMonths(-1); _year = d.Year; _month = d.Month; _ = LoadMonthAsync(); }
    private void NextMonth() { var d = new DateTime(_year, _month, 1).AddMonths(1); _year = d.Year; _month = d.Month; _ = LoadMonthAsync(); }
}
