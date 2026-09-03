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
/// 集体计件月结页（2026-09-03，/payroll/collective-monthly）：
/// 集体=岗位 × 月度评分 × 月结快照；成员月得 = 岗位池 × (实出勤小时×分值) ÷ Σ同岗位权重。
/// 打开月份默认显示已保存快照（金额冻结）；未保存月直接显示引擎草稿。
/// 重算两种模式（与每日工资两表同款，见 PayrollFullRecalcDialogs）：
/// 「引擎重算」仅覆盖在册集体成员为引擎草稿（历史快照员工保留已存避免误清）；
/// 「全量重算（清历史）」额外把已转出/离职的历史快照成员当月清空（保存后其当月旧记录删除、彻底离场，双重确认防误操作）。
/// 「保存本月」整月落库（Amount&gt;0 存、空删）。
/// 页面级授权 [Authorize(SalaryView)] 声明在 .razor @attribute；编辑写入由 _canEdit（SalaryEditor/Full/Admin）门控。
/// </summary>
public partial class CollectiveMonthly : IDisposable
{
    [Inject] private PayrollCollectiveService CollectiveSvc { get; set; } = null!;
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

    private CollectiveMonthDto? _data;

    // 当月是否已有保存快照（决定打开默认显示引擎草稿还是已保存值）
    private bool _hasSaved;
    private List<string> _warnings = new();

    // 金额编辑缓冲（已保存值 或 引擎草稿 或 用户改动），null = 该员工当月 0 元（不落库）
    private Dictionary<int, decimal?> _amountById = new();

    private IEnumerable<CollectiveGroupDto> Groups => _data?.Groups ?? Enumerable.Empty<CollectiveGroupDto>();

    private int MembersCount => Groups.Sum(g => g.Members.Count);

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
            await LoadMonthAsync();
        }
    }

    public void Dispose() => DictValueDisplayHelper.OverrideMapChanged -= OnDictOverrideMapChanged;

    private void OnDictOverrideMapChanged() => _ = InvokeAsync(() =>
    {
        StateHasChanged();
        return Task.CompletedTask;
    });

    private async Task LoadMonthAsync()
    {
        var seq = ++_loadSeq;
        _loading = true;
        try
        {
            var result = await CollectiveSvc.GetMonthAsync(_year, _month);
            if (seq != _loadSeq)
                return; // 已有更新的切月请求，丢弃过期响应
            if (result.Success && result.Data != null)
            {
                _data = result.Data;
                _hasSaved = result.Data.HasSaved;
                _warnings = result.Data.Warnings;
                _amountById = new Dictionary<int, decimal?>();
                foreach (var g in result.Data.Groups)
                {
                    foreach (var m in g.Members)
                    {
                        // 打开默认：已保存月显示已保存快照（保存后改价/改产/改评分不影响已存）；
                        // 未保存月直接显示引擎自动带出草稿（历史快照员工无引擎草稿 → null）；
                        // 工资一律四舍五入到整元（RoundYuan），null 保持 null（该成员当月无值）
                        var saved = m.SavedAmount;
                        var engine = m.EngineAmount;
                        _amountById[m.EmployeeId] = (_hasSaved ? saved : engine) is { } src
                            ? DisplayHelper.RoundYuan(src)
                            : null;
                    }
                }
            }
            else
            {
                Snackbar.Add(result.Message ?? "加载集体月结失败", Severity.Error);
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

    // ========== 引擎重算 ==========

    /// <summary>
    /// 按引擎草稿覆盖网格：仅覆盖「引擎覆盖」员工（当前在册集体成员），
    /// 历史快照并入的员工无引擎值，保留其已保存值避免误清（其快照仅供回溯查看/修改）。
    /// </summary>
    private async Task RecalcAsync()
    {
        var dialog = DialogService.Show<ConfirmDialog>("引擎重算确认", new DialogParameters
        {
            ["ContentText"] = $"将按现行单价与当月考勤/评分重新计算 {_year}年{_month}月各岗位集体池并按权重分配，"
                              + "并覆盖网格为草稿（已保存快照不受影响，需点「保存本月」才覆盖落库）。确定重算？",
            ["ConfirmText"] = "确定重算",
            ["Color"] = Color.Warning
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        var covered = 0;
        foreach (var g in Groups)
        {
            foreach (var m in g.Members.Where(m => m.EngineCovered))
            {
                _amountById[m.EmployeeId] = m.EngineAmount is { } src ? DisplayHelper.RoundYuan(src) : null;
                covered++;
            }
        }
        Snackbar.Add($"已按引擎草稿覆盖网格（{covered} 名成员），可再手工修改后保存", Severity.Info);
        StateHasChanged();
    }

    /// <summary>
    /// 全量重算（清历史，模式 2）：把当月所有显示成员按引擎覆盖——
    /// 在册集体成员（EngineCovered）→ 引擎分配草稿；已转出/离职的历史快照成员引擎无草稿 → 当月清空。
    /// 点「保存本月」后历史快照成员当月旧月结记录被删除、下回加载不再并入显示（彻底离场）。
    /// ⚠️ 破坏性：历史快照是结算时按当月岗位/出勤/评分实结并冻结，误清不可找回 → 双重确认（复用 PayrollFullRecalcDialogs）防误操作。
    /// </summary>
    private async Task RecalcFullAsync()
    {
        if (MembersCount == 0) return;
        var covered = Groups.Sum(g => g.Members.Count(m => m.EngineCovered));
        var history = Groups.Sum(g => g.Members.Count(m => !m.EngineCovered));

        if (!await PayrollFullRecalcDialogs.ConfirmFullRecalcAsync(
                DialogService, "集体计件月结", _year, _month, covered, history))
            return;

        foreach (var g in Groups)
        {
            foreach (var m in g.Members)
            {
                // 历史快照成员 → 清空当月；在册集体成员 → 引擎分配草稿（RoundYuan，0 清空沿用 SetAmount 语义）
                if (!m.EngineCovered) { _amountById[m.EmployeeId] = null; continue; }
                _amountById[m.EmployeeId] = m.EngineAmount is { } src ? DisplayHelper.RoundYuan(src) : null;
            }
        }

        Snackbar.Add(history > 0
            ? $"已全量覆盖：在册 {covered} 人→引擎草稿，历史快照 {history} 人→清空；点「保存本月」生效（将删除其当月旧记录）"
            : $"已覆盖 {covered} 名在册集体成员为引擎草稿，点「保存本月」生效", Severity.Warning);
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
            // 工资按整元：四舍五入到元后落库；0 = 该成员当月 0 元（等同清空，不落库）
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

    // 一组实得金额合计（当前网格可编辑值）
    private decimal GroupTotal(CollectiveGroupDto g) =>
        g.Members.Sum(m => _amountById.TryGetValue(m.EmployeeId, out var v) ? v ?? 0m : 0m);

    // ========== 保存 / 月份导航 ==========

    private async Task SaveMonthAsync()
    {
        if (MembersCount == 0)
        {
            Snackbar.Add("没有可保存的成员", Severity.Warning);
            return;
        }

        var entries = new List<CollectiveMonthEntryDto>();
        foreach (var g in Groups)
        {
            foreach (var m in g.Members)
            {
                var amount = _amountById.TryGetValue(m.EmployeeId, out var v) ? v : null;
                entries.Add(new CollectiveMonthEntryDto { EmployeeId = m.EmployeeId, Amount = amount is > 0m ? amount : null });
            }
        }

        var result = await CollectiveSvc.SaveMonthAsync(new SaveCollectiveMonthDto
        {
            Year = _year,
            Month = _month,
            Entries = entries
        });
        if (result.Success)
            Snackbar.Add($"已保存 {_year}年{_month}月 {entries.Count} 名成员的集体月结", Severity.Success);
        else
            Snackbar.Add(result.Message ?? "保存失败", Severity.Error);

        // 保存成功后重载：默认显示已保存快照，与引擎草稿解耦（改价/改产/改评分不再影响已存）
        await LoadMonthAsync();
    }

    private void OnYearChanged(int v) { _year = v; _ = LoadMonthAsync(); }
    private void OnMonthChanged(int v) { _month = v; _ = LoadMonthAsync(); }
    private void PrevMonth() { var d = new DateTime(_year, _month, 1).AddMonths(-1); _year = d.Year; _month = d.Month; _ = LoadMonthAsync(); }
    private void NextMonth() { var d = new DateTime(_year, _month, 1).AddMonths(1); _year = d.Year; _month = d.Month; _ = LoadMonthAsync(); }
}
