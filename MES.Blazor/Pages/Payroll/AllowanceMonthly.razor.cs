using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Blazor.Services.Payroll;
using MES.Blazor.Shared;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Helpers;
using MES.Shared.Constants;
using System.Globalization;

namespace MES.Blazor.Pages.Payroll;

/// <summary>
/// 「津贴与处罚」月度录入网格页（2026-09-04，/payroll/allowance）：
/// 行=员工（IsActive 在册 ∪ 当月已有记录），列=固定 9 个金额项目（宽表每人每月一行，EmployeeId+Year+Month 唯一）。
/// 金额强制整元：OnCellChanged 即时 RoundYuan（AwayFromZero），0/空 → null（等价未填），禁止负数（后端权威同规约）。
/// 网格复用考勤网格骨架（attendance-scroll/attendance-grid/attendance-cell-input 类名）→ 白得方向键导航 + 聚焦全选（table-nav.js）。
/// 保存=整月 upsert（全行提交；全空行服务端删除）；清空本月=提交空 Rows。
/// 页面级授权 [Authorize(SalaryView)] 声明在 .razor @attribute；编辑/清空/保存由 _canEdit（SalaryEditor/Full/Admin）门控。
/// </summary>
public partial class AllowanceMonthly : IDisposable
{
    [Inject] private PayrollAllowanceService AllowanceSvc { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;

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

    private List<AllowanceRowDto> _rows = new();
    private string _keyword = string.Empty;

    // 金额编辑缓冲：empId → (项目 Key → 金额)。0/空=null（等价未填，不落库）。
    private Dictionary<int, Dictionary<string, decimal?>> _cellValues = new();

    private List<AllowanceRowDto> FilteredRows
    {
        get
        {
            if (_rows.Count == 0) return _rows;
            var kw = _keyword.Trim();
            if (string.IsNullOrWhiteSpace(kw)) return _rows;
            return _rows.Where(r =>
                r.EmployeeCode.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || r.EmployeeName.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || (r.PositionCategory ?? string.Empty).Contains(kw, StringComparison.OrdinalIgnoreCase)
                || (r.Position ?? string.Empty).Contains(kw, StringComparison.OrdinalIgnoreCase)
                || (r.PositionRemark ?? string.Empty).Contains(kw, StringComparison.OrdinalIgnoreCase)).ToList();
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 首次渲染后启用方向键单元格导航 + 聚焦全选（JS 定义在必加载的 table-nav.js）。
        if (firstRender)
        {
            try
            {
                await Js.InvokeVoidAsync("enableAttendanceKeyNav");
            }
            catch (JSException)
            {
                Snackbar.Add("键盘导航未加载，请强制刷新（Ctrl+Shift+R）后重试", Severity.Warning);
            }
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
            var result = await AllowanceSvc.GetMonthAsync(_year, _month);
            if (seq != _loadSeq)
                return; // 已有更新的切月请求，丢弃过期响应
            if (result.Success && result.Data != null)
            {
                _rows = result.Data.Rows;
                _cellValues = new Dictionary<int, Dictionary<string, decimal?>>();
                foreach (var row in _rows)
                {
                    var cells = new Dictionary<string, decimal?>(PayrollAllowanceItems.All.Length);
                    foreach (var item in PayrollAllowanceItems.All)
                        cells[item.Key] = GetRowAmount(row, item.Key);
                    _cellValues[row.EmployeeId] = cells;
                }
            }
            else
            {
                Snackbar.Add(result.Message ?? "加载津贴与处罚失败", Severity.Error);
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

    // ========== 单元格 ==========

    private decimal? GetCellValue(int empId, string key) =>
        _cellValues.TryGetValue(empId, out var cells) && cells.TryGetValue(key, out var v) ? v : null;

    // 格子显示文本（G29 去零；空 = 未填）。原生 input value 绑定用，打字时浏览器原生处理零 Blazor 渲染
    private string GetCellText(int empId, string key) =>
        GetCellValue(empId, key) is { } v ? v.ToString("G29") : "";

    // 原生 input 失焦/回车才提交一次：整元规约（RoundYuan AwayFromZero），0/空 → null，禁止负数。
    // 与后端 SaveMonthAsync NormalizeAmount 同口径（后端仍权威二次规约）。
    private void OnCellChanged(int empId, string key, ChangeEventArgs e)
    {
        var raw = (e.Value as string)?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            SetCellValue(empId, key, null);
            return;
        }
        if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
        {
            Snackbar.Add("金额需为不小于 0 的数字", Severity.Warning);
            return;
        }
        if (v < 0)
        {
            Snackbar.Add("金额不能为负数", Severity.Warning);
            return;
        }
        var rounded = DisplayHelper.RoundYuan(v);
        SetCellValue(empId, key, rounded == 0 ? null : rounded);
    }

    private void SetCellValue(int empId, string key, decimal? value)
    {
        if (!_cellValues.TryGetValue(empId, out var cells))
        {
            cells = new Dictionary<string, decimal?>();
            _cellValues[empId] = cells;
        }
        cells[key] = value;
        StateHasChanged();
    }

    // 当前筛选显示行的某项目合计（整月口径不影响：合计按筛选行显示，便于核算）
    private decimal ColumnTotal(string key) =>
        FilteredRows.Sum(r => GetCellValue(r.EmployeeId, key) ?? 0m);

    // ========== 保存 / 清空 ==========

    private async Task SaveMonthAsync()
    {
        if (_rows.Count == 0)
        {
            Snackbar.Add("没有可保存的员工", Severity.Warning);
            return;
        }

        var inputs = new List<AllowanceRowInputDto>();
        foreach (var row in _rows)
        {
            var dto = new AllowanceRowInputDto { EmployeeId = row.EmployeeId };
            foreach (var item in PayrollAllowanceItems.All)
                SetInputAmount(dto, item.Key, GetCellValue(row.EmployeeId, item.Key));
            inputs.Add(dto);
        }

        var result = await AllowanceSvc.SaveMonthAsync(new SaveAllowanceMonthDto { Year = _year, Month = _month, Rows = inputs });
        if (result.Success)
        {
            if (result.Data > 0)
                Snackbar.Add($"已保存 {result.Data} 名员工的 {_month} 月津贴", Severity.Success);
            else
                Snackbar.Add("本月无金额变动，未产生保存", Severity.Info);
            await LoadMonthAsync();
        }
        else
        {
            Snackbar.Add(result.Message ?? "保存失败", Severity.Error);
        }
    }

    private async Task ClearMonthAsync()
    {
        var dialog = DialogService.Show<ConfirmDialog>("清空确认", new DialogParameters
        {
            ["ContentText"] = $"确定清空 {_year} 年 {_month} 月全部员工的津贴与处罚记录？此操作不可撤销。",
            ["ConfirmText"] = "清空",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        var result = await AllowanceSvc.SaveMonthAsync(new SaveAllowanceMonthDto { Year = _year, Month = _month, Rows = new List<AllowanceRowInputDto>() });
        if (result.Success)
        {
            Snackbar.Add($"已清空 {_month} 月津贴", Severity.Success);
            await LoadMonthAsync();
        }
        else
        {
            Snackbar.Add(result.Message ?? "清空失败", Severity.Error);
        }
    }

    // ========== 行↔9 金额列的 Key 读写（宽表固定列 switch，Key 与 PayrollAllowanceItems.All 对齐） ==========

    private static decimal? GetRowAmount(AllowanceRowDto row, string key) => key switch
    {
        "FullAttendanceBonus" => row.FullAttendanceBonus,
        "SeniorityBonus" => row.SeniorityBonus,
        "NightShiftAllowance" => row.NightShiftAllowance,
        "PositionAllowance" => row.PositionAllowance,
        "HighTempAllowance" => row.HighTempAllowance,
        "InjurySubsidy" => row.InjurySubsidy,
        "LeadBonus" => row.LeadBonus,
        "Penalty" => row.Penalty,
        "SocialSecurity" => row.SocialSecurity,
        _ => null,
    };

    private static void SetInputAmount(AllowanceRowInputDto dto, string key, decimal? value)
    {
        switch (key)
        {
            case "FullAttendanceBonus": dto.FullAttendanceBonus = value; break;
            case "SeniorityBonus": dto.SeniorityBonus = value; break;
            case "NightShiftAllowance": dto.NightShiftAllowance = value; break;
            case "PositionAllowance": dto.PositionAllowance = value; break;
            case "HighTempAllowance": dto.HighTempAllowance = value; break;
            case "InjurySubsidy": dto.InjurySubsidy = value; break;
            case "LeadBonus": dto.LeadBonus = value; break;
            case "Penalty": dto.Penalty = value; break;
            case "SocialSecurity": dto.SocialSecurity = value; break;
        }
    }

    // ========== 月份导航 ==========

    private void OnYearChanged(int v) { _year = v; _ = LoadMonthAsync(); }
    private void OnMonthChanged(int v) { _month = v; _ = LoadMonthAsync(); }
    private void PrevMonth() { var d = new DateTime(_year, _month, 1).AddMonths(-1); _year = d.Year; _month = d.Month; _ = LoadMonthAsync(); }
    private void NextMonth() { var d = new DateTime(_year, _month, 1).AddMonths(1); _year = d.Year; _month = d.Month; _ = LoadMonthAsync(); }
    private void OnKeywordChanged(string v) { _keyword = v ?? string.Empty; StateHasChanged(); }
}
