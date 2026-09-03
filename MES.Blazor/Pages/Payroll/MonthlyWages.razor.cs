using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Components;
using MES.Blazor.Helpers;
using MES.Blazor.Services;
using MES.Blazor.Services.Payroll;
using MES.Blazor.Shared;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Helpers;
using MES.Shared.Constants;

namespace MES.Blazor.Pages.Payroll;

/// <summary>
/// 每日工资双表月视图页（2026-09-03）：
/// /payroll/wages/non-piece = 非计件工资（Hourly 计小时 + Daily 计日）
/// /payroll/wages/piece      = 个人计件工资（PieceIndividual 个人计件）
/// 仿考勤表网格：单元格=每日工资额；打开月份默认显示已保存快照（未保存月直接显示引擎草稿）；
/// 「引擎重算」按现行口径覆盖网格可编辑值，「保存本月」整月落库（Amount&gt;0 存、空删）。
/// 页面级授权 [Authorize(SalaryView)] 声明在 .razor @attribute；编辑写入由 _canEdit（SalaryEditor/Full/Admin）门控。
/// </summary>
public partial class MonthlyWages : IDisposable
{
    [Parameter] public string? Group { get; set; }

    [Inject] private PayrollDailyWageService WageSvc { get; set; } = null!;
    [Inject] private DictValueDefinitionService DictValueDefService { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;

    // 排序列标识
    private const string SortCode = "code";
    private const string SortName = "name";
    private const string SortCategory = "category";
    private const string SortPosition = "position";

    private PayrollWageGroup _group = PayrollWageGroup.NonPiece;
    private bool _parametersLoaded;
    private bool _canEdit;

    private string _title => _group.GetTitle();

    private int _year = DateTime.Today.Year;
    private int _month = DateTime.Today.Month;
    private int _daysInMonth => DateTime.DaysInMonth(_year, _month);

    // 年份/月份/日期循环项：一律 @foreach 渲染，避开 @for 闭包捕获（同考勤表教训）
    private List<int> _years => Enumerable.Range(2024, DateTime.Today.Year + 2 - 2024 + 1).ToList();
    private List<int> _months => Enumerable.Range(1, 12).ToList();
    private List<int> _days => Enumerable.Range(1, _daysInMonth).ToList();
    private string _keyword = string.Empty;
    private bool _loading;

    // 月份加载并发序号：切月/切组响应乱序防护（同考勤表）
    private int _loadSeq;

    // 当月是否已有该组保存快照（决定打开默认显示已保存值还是引擎草稿）
    private bool _hasSaved;
    private List<string> _warnings = new();

    // 岗位类别 / 岗位 下拉选项（参数表 enabled-values 优先，常量类兜底）
    private List<(string Key, string Text)> _positionCategoryOptions = BuildPositionCategoryOptions();
    private List<(string Key, string Text)> _positionOptions = BuildPositionOptions();

    private string _positionCategoryFilter = string.Empty;
    private string _positionFilter = string.Empty;

    private string _sortColumn = SortCode;
    private bool _sortAsc = true;

    private List<DailyWageEmployeeRowDto> _employees = new();
    private List<DailyWageEmployeeRowDto> _filteredEmployees = new();

    // 单元格编辑缓冲（已保存值 或 引擎草稿 或 用户改动），null = 当日 0 元（不落库）
    private Dictionary<int, Dictionary<int, decimal?>> _cellValues = new();

    private static List<(string Key, string Text)> BuildPositionCategoryOptions() =>
        PositionCategoryKeys.All.Select(k => (k, DictValueDisplayHelper.GetText(DictValueDefaults.PositionCategoryKey, k) ?? k)).ToList();

    private static List<(string Key, string Text)> BuildPositionOptions() =>
        PositionKeys.All.Select(k => (k, DictValueDisplayHelper.GetText(DictValueDefaults.PositionKey, k) ?? k)).ToList();

    protected override async Task OnInitializedAsync()
    {
        // 订阅字典显示映射注入事件（同考勤表）：参数表 OverrideMap 注入晚于页面首次渲染
        DictValueDisplayHelper.OverrideMapChanged += OnDictOverrideMapChanged;
        if (DictValueDisplayHelper.OverrideMap != null)
            await ApplyDictOverrideMapAsync();
        else
            await LoadDictOptionsAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        var group = PayrollWageGroups.ParseKey(Group);
        var state = await AuthProvider.GetAuthenticationStateAsync();
        var user = state.User;
        _canEdit = user.IsInRole(Roles.Menus.SalaryEditor)
                   || user.IsInRole(Roles.Menus.SalaryFull)
                   || user.IsInRole(Roles.Admin);
        if (!_parametersLoaded || group != _group)
        {
            _group = group;
            _parametersLoaded = true;
            await LoadMonthAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                await Js.InvokeVoidAsync("enableAttendanceKeyNav");
            }
            catch (JSException)
            {
                Snackbar.Add("工资网格键盘导航未加载，请强制刷新（Ctrl+Shift+R）后重试", Severity.Warning);
            }
        }
    }

    public void Dispose() => DictValueDisplayHelper.OverrideMapChanged -= OnDictOverrideMapChanged;

    private void OnDictOverrideMapChanged() => _ = InvokeAsync(ApplyDictOverrideMapAsync);

    private async Task ApplyDictOverrideMapAsync()
    {
        await LoadDictOptionsAsync();
        StateHasChanged();
    }

    private async Task LoadDictOptionsAsync()
    {
        try
        {
            var pos = await DictValueDefService.GetEnabledValuesAsync(DictValueDefaults.PositionKey);
            if (pos.Success && pos.Data != null && pos.Data.Count > 0)
                _positionOptions = pos.Data.Select(x => (x.Value, x.DisplayName)).ToList();
            else
                _positionOptions = BuildPositionOptions();
        }
        catch { _positionOptions = BuildPositionOptions(); }

        try
        {
            var cat = await DictValueDefService.GetEnabledValuesAsync(DictValueDefaults.PositionCategoryKey);
            if (cat.Success && cat.Data != null && cat.Data.Count > 0)
                _positionCategoryOptions = cat.Data.Select(x => (x.Value, x.DisplayName)).ToList();
            else
                _positionCategoryOptions = BuildPositionCategoryOptions();
        }
        catch { _positionCategoryOptions = BuildPositionCategoryOptions(); }
    }

    private async Task LoadMonthAsync()
    {
        var seq = ++_loadSeq;
        _loading = true;
        try
        {
            var result = await WageSvc.GetMonthAsync(_year, _month, _group.GetKey(), _keyword);
            if (seq != _loadSeq)
                return; // 已有更新的切月/切组请求，丢弃过期响应
            if (result.Success && result.Data != null)
            {
                _employees = result.Data.Employees;
                _hasSaved = result.Data.HasSaved;
                _warnings = result.Data.Warnings;
                _cellValues = new Dictionary<int, Dictionary<int, decimal?>>();
                foreach (var emp in _employees)
                {
                    var days = new Dictionary<int, decimal?>();
                    for (var d = 1; d <= _daysInMonth; d++)
                    {
                        // 打开默认：已保存月显示已保存快照（保存后改单价/改产量不影响已存）；
                        // 未保存月直接显示引擎自动带出草稿；工资一律四舍五入到整元（RoundYuan）
                        var saved = emp.DaySavedAmount.TryGetValue(d, out var sv) ? sv : null;
                        var engine = emp.DayEngineAmount.TryGetValue(d, out var ev) ? ev : null;
                        days[d] = DisplayHelper.RoundYuan(_hasSaved ? saved : engine) is { } w && w > 0m ? w : null;
                    }
                    _cellValues[emp.EmployeeId] = days;
                }
            }
            else
            {
                Snackbar.Add(result.Message ?? "加载每日工资失败", Severity.Error);
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
                ApplyFilterAndSort();
                StateHasChanged();
            }
        }
    }

    // ========== 引擎重算 ==========

    /// <summary>
    /// 按引擎草稿覆盖网格：仅覆盖「引擎覆盖」员工（档案归口属该组的启用员工），
    /// 历史归口并入的员工无引擎值，保留其已保存值避免误清（其快照仅供回溯查看/修改）。
    /// </summary>
    private async Task RecalcAsync()
    {
        var dialog = DialogService.Show<ConfirmDialog>("引擎重算确认", new DialogParameters
        {
            ["ContentText"] = $"将按当前考勤/产量与现行单价重新计算 {_year}年{_month}月「{_title}」每日工资，"
                              + "并覆盖网格为草稿（已保存快照不受影响，需点「保存本月」才覆盖落库）。确定重算？",
            ["ConfirmText"] = "确定重算",
            ["Color"] = Color.Warning
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        foreach (var emp in _employees.Where(e => e.EngineCovered))
        {
            if (!_cellValues.TryGetValue(emp.EmployeeId, out var days))
            {
                days = new Dictionary<int, decimal?>();
                _cellValues[emp.EmployeeId] = days;
            }
            for (var d = 1; d <= _daysInMonth; d++)
                days[d] = DisplayHelper.RoundYuan(emp.DayEngineAmount.TryGetValue(d, out var ev) ? ev : null) is { } w && w > 0m ? w : null;
        }
        Snackbar.Add($"已按引擎草稿覆盖网格（{_employees.Count(e => e.EngineCovered)} 名员工），可再手工修改后保存", Severity.Info);
        ApplyFilterAndSort();
        StateHasChanged();
    }

    /// <summary>
    /// 全量重算（清历史，模式 2）：把整月所有显示员工按引擎覆盖——
    /// 在册归口（EngineCovered）→ 引擎草稿；已转出/离职的历史归口员工引擎无草稿 → 整行清空。
    /// 保存「本月」后历史归口员工当月旧记录被删除、下回加载不再并入显示（彻底离场）。
    /// ⚠️ 破坏性：历史已保存金额是转归口前按旧组真实应发/录入，误清不可找回 → 双重确认（Info 说明 + Error 终确认，复用 PayrollFullRecalcDialogs）防误操作。
    /// </summary>
    private async Task RecalcFullAsync()
    {
        if (_employees.Count == 0) return;
        var covered = _employees.Count(e => e.EngineCovered);
        var history = _employees.Count(e => !e.EngineCovered);

        if (!await PayrollFullRecalcDialogs.ConfirmFullRecalcAsync(
                DialogService, _title, _year, _month, covered, history))
            return;

        foreach (var emp in _employees)
        {
            if (!_cellValues.TryGetValue(emp.EmployeeId, out var days))
            {
                days = new Dictionary<int, decimal?>();
                _cellValues[emp.EmployeeId] = days;
            }
            for (var d = 1; d <= _daysInMonth; d++)
            {
                // 历史归口 → 清空；在册 → 引擎草稿
                if (!emp.EngineCovered) { days[d] = null; continue; }
                days[d] = DisplayHelper.RoundYuan(emp.DayEngineAmount.TryGetValue(d, out var ev) ? ev : null) is { } w && w > 0m ? w : null;
            }
        }

        Snackbar.Add(history > 0
            ? $"已全量覆盖：在册 {covered} 人→引擎草稿，历史归口 {history} 人→清空；点「保存本月」生效（将删除其当月旧记录）"
            : $"已覆盖 {covered} 名在册员工为引擎草稿，点「保存本月」生效", Severity.Warning);
        ApplyFilterAndSort();
        StateHasChanged();
    }

    // ========== 筛选 / 排序 ==========

    private void ApplyFilterAndSort()
    {
        IEnumerable<DailyWageEmployeeRowDto> query = _employees;
        if (!string.IsNullOrWhiteSpace(_positionCategoryFilter))
            query = query.Where(e => string.Equals(e.PositionCategory, _positionCategoryFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(_positionFilter))
            query = query.Where(e => string.Equals(e.Position, _positionFilter, StringComparison.OrdinalIgnoreCase));

        var list = query.ToList();
        Comparison<DailyWageEmployeeRowDto> cmp = _sortColumn switch
        {
            SortCode => (a, b) => string.Compare(a.EmployeeCode, b.EmployeeCode, StringComparison.OrdinalIgnoreCase),
            SortName => (a, b) => string.Compare(a.EmployeeName, b.EmployeeName, StringComparison.OrdinalIgnoreCase),
            SortCategory => (a, b) => string.Compare(a.PositionCategory ?? "", b.PositionCategory ?? "", StringComparison.OrdinalIgnoreCase),
            SortPosition => (a, b) => string.Compare(a.Position ?? "", b.Position ?? "", StringComparison.OrdinalIgnoreCase),
            _ => (a, b) => 0
        };
        list.Sort(cmp);
        if (!_sortAsc) list.Reverse();
        _filteredEmployees = list;
    }

    private void ToggleSort(string column)
    {
        if (_sortColumn == column)
            _sortAsc = !_sortAsc;
        else
        {
            _sortColumn = column;
            _sortAsc = true;
        }
        ApplyFilterAndSort();
    }

    private string SortIcon(string column) =>
        _sortColumn != column ? "" : (_sortAsc ? "▲" : "▼");

    private void OnPositionCategoryFilterChanged(string? v)
    {
        _positionCategoryFilter = v ?? string.Empty;
        ApplyFilterAndSort();
    }

    private void OnPositionFilterChanged(string? v)
    {
        _positionFilter = v ?? string.Empty;
        ApplyFilterAndSort();
    }

    // ========== 单元格 ==========

    private decimal? GetCellValue(int empId, int day) =>
        _cellValues.TryGetValue(empId, out var days) && days.TryGetValue(day, out var v) ? v : null;

    private string GetCellText(int empId, int day) =>
        GetCellValue(empId, day) is { } v ? v.ToString("G29") : "";

    private void OnCellChanged(int empId, int day, ChangeEventArgs e)
    {
        var raw = (e.Value as string)?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            SetCellValue(empId, day, null);
            return;
        }
        if (decimal.TryParse(raw, out var v))
        {
            if (v < 0)
            {
                Snackbar.Add("每日工资不可为负", Severity.Warning);
                return; // 不改值，重渲染回退输入框为原值
            }
            // 工资按整元：四舍五入到元后落库；0 = 该日 0 元（等同清空，不落库）
            var yuan = DisplayHelper.RoundYuan(v);
            SetCellValue(empId, day, yuan > 0 ? yuan : null);
        }
        else
        {
            Snackbar.Add("请输入有效的金额数字", Severity.Warning);
        }
    }

    private void SetCellValue(int empId, int day, decimal? value)
    {
        if (!_cellValues.TryGetValue(empId, out var days))
        {
            days = new Dictionary<int, decimal?>();
            _cellValues[empId] = days;
        }
        days[day] = value;
        StateHasChanged();
    }

    // ========== 合计 ==========

    private decimal EmployeeTotal(int empId) =>
        _cellValues.TryGetValue(empId, out var days) ? days.Values.Where(v => v.HasValue).Sum(v => v!.Value) : 0m;

    private int EmployeePaidDays(int empId) =>
        _cellValues.TryGetValue(empId, out var days) ? days.Count(kv => kv.Value is > 0) : 0;

    // 当日合计只统计当前筛选出的行
    private decimal DayTotal(int day) =>
        _filteredEmployees.Sum(e => GetCellValue(e.EmployeeId, day) ?? 0m);

    // ========== 保存 / 月份导航 ==========

    private async Task SaveMonthAsync()
    {
        if (_employees.Count == 0)
        {
            Snackbar.Add("没有可保存的员工", Severity.Warning);
            return;
        }

        var entries = new List<DailyWageEntryDto>();
        foreach (var emp in _employees)
        {
            for (var d = 1; d <= _daysInMonth; d++)
                entries.Add(new DailyWageEntryDto { EmployeeId = emp.EmployeeId, Day = d, Amount = GetCellValue(emp.EmployeeId, d) });
        }

        var result = await WageSvc.SaveMonthAsync(new SaveDailyWageDto
        {
            Year = _year,
            Month = _month,
            Group = _group,
            Entries = entries
        });
        if (result.Success)
            Snackbar.Add($"已保存 {_employees.Count} 名员工的 {_year}年{_month}月「{_title}」", Severity.Success);
        else
            Snackbar.Add(result.Message ?? "保存失败", Severity.Error);

        // 保存成功后重载：默认显示已保存快照，与引擎草稿解耦（改单价/改产量不再影响已存）
        await LoadMonthAsync();
    }

    private void OnYearChanged(int v) { _year = v; _ = LoadMonthAsync(); }
    private void OnMonthChanged(int v) { _month = v; _ = LoadMonthAsync(); }
    private void PrevMonth() { var d = new DateTime(_year, _month, 1).AddMonths(-1); _year = d.Year; _month = d.Month; _ = LoadMonthAsync(); }
    private void NextMonth() { var d = new DateTime(_year, _month, 1).AddMonths(1); _year = d.Year; _month = d.Month; _ = LoadMonthAsync(); }

    private async Task OnKeywordEnter(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await LoadMonthAsync();
    }
}
