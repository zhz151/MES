using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Services;
using MES.Blazor.Services.Payroll;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Helpers;

namespace MES.Blazor.Pages.Payroll;

[Authorize]
public partial class Attendance : IDisposable
{
    [Inject] private AttendanceService AttendanceSvc { get; set; } = null!;
    [Inject] private DictValueDefinitionService DictValueDefService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;

    // 排序列标识
    private const string SortCode = "code";
    private const string SortName = "name";
    private const string SortCategory = "category";
    private const string SortPosition = "position";

    private int _year = DateTime.Today.Year;
    private int _month = DateTime.Today.Month;
    private int _daysInMonth => DateTime.DaysInMonth(_year, _month);

    // 年份/月份/日期下拉或格子循环项：一律用 @foreach 渲染，
    // 避开 @for 循环变量闭包捕获（lambda/ValueChanged 捕获共享 d，全部变成循环结束值 → 输入写错日期/下拉全显示末项）
    private List<int> _years => Enumerable.Range(2024, DateTime.Today.Year + 2 - 2024 + 1).ToList();
    private List<int> _months => Enumerable.Range(1, 12).ToList();
    private List<int> _days => Enumerable.Range(1, _daysInMonth).ToList();
    private string _keyword = string.Empty;
    private bool _loading;

    // 月份加载并发序号：切月/翻页会产生多个并发的 LoadMonthAsync，响应可能乱序返回，
    // 旧月份请求晚到时若不丢弃会覆盖当前月份数据 → 界面标签月份与实际表格数据错位。
    private int _loadSeq;

    // 岗位类别 / 岗位 下拉选项（参数表 enabled-values 优先，常量类兜底）
    private List<(string Key, string Text)> _positionCategoryOptions = BuildPositionCategoryOptions();
    private List<(string Key, string Text)> _positionOptions = BuildPositionOptions();

    // 筛选状态（空串 = 全部）
    private string _positionCategoryFilter = string.Empty;
    private string _positionFilter = string.Empty;

    // 排序状态（默认按工号升序）
    private string _sortColumn = SortCode;
    private bool _sortAsc = true;

    private List<AttendanceEmployeeRowDto> _employees = new();
    private List<AttendanceEmployeeRowDto> _filteredEmployees = new();
    private Dictionary<int, Dictionary<int, decimal?>> _cellValues = new();

    private static List<(string Key, string Text)> BuildPositionCategoryOptions() =>
        PositionCategoryKeys.All.Select(k => (k, DictValueDisplayHelper.GetText(DictValueDefaults.PositionCategoryKey, k) ?? k)).ToList();

    private static List<(string Key, string Text)> BuildPositionOptions() =>
        PositionKeys.All.Select(k => (k, DictValueDisplayHelper.GetText(DictValueDefaults.PositionKey, k) ?? k)).ToList();

    protected override async Task OnInitializedAsync()
    {
        // 订阅字典显示映射注入事件：MainLayout 注入 OverrideMap 晚于页面首次渲染，
        // 事件回调重建筛选下拉选项并 StateHasChanged，使页面按参数表中文显示
        DictValueDisplayHelper.OverrideMapChanged += OnDictOverrideMapChanged;
        if (DictValueDisplayHelper.OverrideMap != null)
            await ApplyDictOverrideMapAsync();
        else
            await LoadDictOptionsAsync();

        await LoadMonthAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 首次渲染后启用方向键单元格导航 + 聚焦全选（JS 定义在必加载的 table-nav.js）。
        // try-catch：若浏览器缓存旧脚本导致函数缺失，降级提示刷新，不阻断页面渲染
        if (firstRender)
        {
            try
            {
                await Js.InvokeVoidAsync("enableAttendanceKeyNav");
            }
            catch (JSException)
            {
                Snackbar.Add("考勤键盘导航未加载，请强制刷新（Ctrl+Shift+R）后重试", Severity.Warning);
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
            var result = await AttendanceSvc.GetMonthAsync(_year, _month, _keyword);
            if (seq != _loadSeq)
                return; // 已有更新的切月请求，丢弃过期响应
            if (result.Success && result.Data != null)
            {
                _employees = result.Data.Employees;
                _cellValues = new Dictionary<int, Dictionary<int, decimal?>>();
                foreach (var emp in _employees)
                {
                    var days = new Dictionary<int, decimal?>();
                    for (var d = 1; d <= _daysInMonth; d++)
                        days[d] = emp.DayHours.TryGetValue(d, out var v) ? v : null;
                    _cellValues[emp.EmployeeId] = days;
                }
            }
            else
            {
                Snackbar.Add(result.Message ?? "加载考勤失败", Severity.Error);
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
                // 切月是 fire-and-forget 调用：await 返回后的状态更新不在渲染管线内，
                // 必须显式 StateHasChanged，否则画面停留在上一次加载的数据（数据滞后一个月显示）。
                StateHasChanged();
            }
        }
    }

    // 按岗位类别/岗位筛选 + 排序列排序，产出显示行
    private void ApplyFilterAndSort()
    {
        IEnumerable<AttendanceEmployeeRowDto> query = _employees;
        if (!string.IsNullOrWhiteSpace(_positionCategoryFilter))
            query = query.Where(e => string.Equals(e.PositionCategory, _positionCategoryFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(_positionFilter))
            query = query.Where(e => string.Equals(e.Position, _positionFilter, StringComparison.OrdinalIgnoreCase));

        var list = query.ToList();
        Comparison<AttendanceEmployeeRowDto> cmp = _sortColumn switch
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

    private decimal? GetCellValue(int empId, int day) =>
        _cellValues.TryGetValue(empId, out var days) && days.TryGetValue(day, out var v) ? v : null;

    // 格子显示文本（G29 去零；空 = 未出勤）。原生 input value 绑定用，打字时浏览器原生处理零 Blazor 渲染
    private string GetCellText(int empId, int day) =>
        GetCellValue(empId, day) is { } v ? v.ToString("G29") : "";

    // 原生 input 失焦/回车才提交一次（避免 MudNumericField 每敲一键触发全页 StateHasChanged 重渲染 1440 格子）
    private void OnCellChanged(int empId, int day, ChangeEventArgs e)
    {
        var raw = (e.Value as string)?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            SetCellValue(empId, day, null);
            return;
        }
        if (decimal.TryParse(raw, out var v) && v is >= 0 and <= 24)
            SetCellValue(empId, day, v);
        else
            Snackbar.Add($"出勤小时必须在 0~24 之间", Severity.Warning);
    }

    private void SetCellValue(int empId, int day, decimal? value)
    {
        if (!_cellValues.TryGetValue(empId, out var days))
        {
            days = new Dictionary<int, decimal?>();
            _cellValues[empId] = days;
        }
        days[day] = value is < 0 or > 24 ? null : value;
        StateHasChanged();
    }

    private int AttendanceDays(int empId) =>
        _cellValues.TryGetValue(empId, out var days) ? days.Count(kv => kv.Value is > 0) : 0;

    private decimal TotalHours(int empId) =>
        _cellValues.TryGetValue(empId, out var days) ? days.Values.Where(v => v.HasValue).Sum(v => v!.Value) : 0m;

    // 当日合计只统计当前筛选出的行（筛选便于查看某类员工的当日合计）
    private decimal DayTotal(int day) =>
        _filteredEmployees.Sum(e => GetCellValue(e.EmployeeId, day) ?? 0m);

    private async Task SaveMonthAsync()
    {
        if (_employees.Count == 0)
        {
            Snackbar.Add("没有可保存的员工", Severity.Warning);
            return;
        }

        var entries = new List<AttendanceEntryDto>();
        foreach (var emp in _employees)
        {
            for (var d = 1; d <= _daysInMonth; d++)
                entries.Add(new AttendanceEntryDto { EmployeeId = emp.EmployeeId, Day = d, WorkHours = GetCellValue(emp.EmployeeId, d) });
        }

        var result = await AttendanceSvc.SaveMonthAsync(new SaveAttendanceDto { Year = _year, Month = _month, Entries = entries });
        if (result.Success)
            Snackbar.Add($"已保存 {_employees.Count} 名员工的 {_month} 月考勤", Severity.Success);
        else
            Snackbar.Add(result.Message ?? "保存失败", Severity.Error);
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
