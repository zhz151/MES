using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MES.Blazor.Services;
using MES.Blazor.Services.Payroll;
using MES.Blazor.Shared;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Payroll;
using MES.Shared.Constants;
using System.Globalization;

namespace MES.Blazor.Pages.Payroll;

/// <summary>
/// 杂辅工记录台账页（2026-09-03，/payroll/misc-work）：
/// 按月筛选的台账列表，行=一条杂辅任务登记（日期/员工/杂辅内容/小时数/金额/备注）。
/// 金额为手工录入源头（保留小数、不做整元取整）；允许同一员工同一天多条。
/// 员工选人复用「全量启用员工」下拉（EmployeeService.GetBySectionAsync(null)，按 Id 绑定）；
/// 整月数据一次拉回内存，关键词筛选（工号/姓名/内容）客户端执行，顶栏合计恒为整月口径。
/// 页面级授权 [Authorize(SalaryView)] 声明在 .razor @attribute；新增/编辑/删除由 _canEdit（SalaryEditor/Full/Admin）门控。
/// </summary>
public partial class MiscWorkMonthly
{
    [Inject] private PayrollMiscWorkService MiscSvc { get; set; } = null!;
    [Inject] private EmployeeService EmployeeSvc { get; set; } = null!;
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

    // 月份加载并发序号：切月响应乱序防护（同其它工资月页）
    private int _loadSeq;

    private MiscWorkMonthDto? _data;

    // 页内关键词筛选（工号/姓名/内容，客户端过滤，不影响整月合计）
    private string _keyword = string.Empty;

    private IEnumerable<MiscWorkRowDto> Rows => _data?.Records ?? Enumerable.Empty<MiscWorkRowDto>();

    /// <summary>按关键词过滤后的显示行（工号/姓名/内容 Contains，忽略大小写）</summary>
    private List<MiscWorkRowDto> FilteredRows
    {
        get
        {
            var kw = _keyword?.Trim();
            if (string.IsNullOrEmpty(kw)) return Rows.ToList();
            var rows = Rows.Where(r =>
                r.EmployeeCode.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || r.EmployeeName.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || r.Content.Contains(kw, StringComparison.OrdinalIgnoreCase)).ToList();
            return rows;
        }
    }

    // 全量启用员工下拉（新增行选人用）
    private List<EmployeeDto> _employees = new();

    // 新增行状态
    private bool _adding;
    private int? _newEmployeeId;
    private string _newDateText = string.Empty;
    private string _newContent = string.Empty;
    private string _newHoursText = string.Empty;
    private string _newAmountText = string.Empty;
    private string _newRemark = string.Empty;

    // 行内编辑状态：_editingIds 内行进入编辑，字段缓存 _editCache[id]
    private HashSet<int> _editingIds = new();
    private Dictionary<int, EditCache> _editCache = new();

    private class EditCache
    {
        public string WorkDateText { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string HoursText { get; set; } = string.Empty;
        public string AmountText { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
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
            await LoadEmployeesAsync();
            await LoadMonthAsync();
        }
    }

    // ========== 加载 ==========

    /// <summary>全量启用员工下拉（选人），仅成功加载一次；含 Id/Code/Name 供绑定与显示</summary>
    private async Task LoadEmployeesAsync()
    {
        var resp = await EmployeeSvc.GetBySectionAsync(null);
        _employees = resp.Success && resp.Data != null ? resp.Data : new();
    }

    private string EmployeeLabel(EmployeeDto e) => $"{e.Name}({e.Code})";

    private async Task LoadMonthAsync()
    {
        var seq = ++_loadSeq;
        _loading = true;
        try
        {
            var result = await MiscSvc.GetMonthAsync(_year, _month);
            if (seq != _loadSeq)
                return; // 已有更新的切月请求，丢弃过期响应
            if (result.Success && result.Data != null)
            {
                _data = result.Data;
                ResetEditState();
            }
            else
            {
                Snackbar.Add(result.Message ?? "加载杂辅工记录失败", Severity.Error);
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

    /// <summary>重载/切月时清空新增与行内编辑草稿</summary>
    private void ResetEditState()
    {
        _adding = false;
        _newEmployeeId = null;
        _newContent = string.Empty;
        _newHoursText = string.Empty;
        _newAmountText = string.Empty;
        _newRemark = string.Empty;
        _editingIds.Clear();
        _editCache.Clear();
    }

    private string WorkDateText(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string NumText(decimal v) => v.ToString("G29", CultureInfo.InvariantCulture);

    private bool TryParseDate(string? text, out DateTime date)
    {
        return DateTime.TryParseExact(text?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    }

    private string DefaultNewDateText()
    {
        var today = DateTime.Today;
        return today.Year == _year && today.Month == _month
            ? WorkDateText(today)
            : WorkDateText(new DateTime(_year, _month, 1));
    }

    // ========== 新增行 ==========

    private void BeginAdd()
    {
        _editingIds.Clear();
        _editCache.Clear();
        _adding = true;
        _newEmployeeId = null;
        _newDateText = DefaultNewDateText();
        _newContent = string.Empty;
        _newHoursText = string.Empty;
        _newAmountText = string.Empty;
        _newRemark = string.Empty;
        StateHasChanged();
    }

    private void CancelAdd()
    {
        _adding = false;
        StateHasChanged();
    }

    private async Task SaveNewAsync()
    {
        var (dto, message) = BuildInput(_newEmployeeId, _newDateText, _newContent, _newHoursText, _newAmountText, _newRemark, Id: 0);
        if (dto == null)
        {
            Snackbar.Add(message, Severity.Warning);
            return;
        }

        var result = await MiscSvc.SaveRecordAsync(dto);
        if (!result.Success)
        {
            Snackbar.Add(result.Message ?? "保存失败", Severity.Error);
            return;
        }

        Snackbar.Add("已新增杂辅记录", Severity.Success);
        var savedInMonth = dto.WorkDate.Year == _year && dto.WorkDate.Month == _month;
        _adding = false;
        await LoadMonthAsync();
        if (!savedInMonth)
            Snackbar.Add("该日期不在当前月份，请切换月份查看", Severity.Info);
    }

    // ========== 行内编辑 ==========

    private void StartEdit(MiscWorkRowDto row)
    {
        _adding = false;
        _editingIds.Add(row.Id);
        _editCache[row.Id] = new EditCache
        {
            WorkDateText = WorkDateText(row.WorkDate),
            Content = row.Content,
            HoursText = NumText(row.Hours),
            AmountText = NumText(row.Amount),
            Remark = row.Remark ?? string.Empty,
        };
        StateHasChanged();
    }

    private void CancelEdit(int id)
    {
        _editingIds.Remove(id);
        _editCache.Remove(id);
        StateHasChanged();
    }

    private async Task SaveEditAsync(MiscWorkRowDto row)
    {
        if (!_editCache.TryGetValue(row.Id, out var cache))
        {
            CancelEdit(row.Id);
            return;
        }
        // 编辑不改员工归属，EmployeeId 取原行
        var (dto, message) = BuildInput(row.EmployeeId, cache.WorkDateText, cache.Content, cache.HoursText,
            cache.AmountText, cache.Remark, Id: row.Id);
        if (dto == null)
        {
            Snackbar.Add(message, Severity.Warning);
            return;
        }

        var result = await MiscSvc.SaveRecordAsync(dto);
        if (!result.Success)
        {
            Snackbar.Add(result.Message ?? "保存失败", Severity.Error);
            return;
        }

        Snackbar.Add("已保存修改", Severity.Success);
        var savedInMonth = dto.WorkDate.Year == _year && dto.WorkDate.Month == _month;
        _editingIds.Remove(row.Id);
        _editCache.Remove(row.Id);
        await LoadMonthAsync();
        if (!savedInMonth)
            Snackbar.Add("该日期不在当前月份，请切换月份查看", Severity.Info);
    }

    /// <summary>把表单值组装为保存 DTO；任何一项非法返回 (null, 提示文案)</summary>
    private (MiscWorkRecordInputDto? dto, string? message) BuildInput(
        int? employeeId, string? dateText, string content, string hoursText, string amountText, string? remark, int Id)
    {
        if (!employeeId.HasValue)
            return (null, "请选择员工");
        if (!TryParseDate(dateText, out var date))
            return (null, "日期格式应为 yyyy-MM-dd");
        if (string.IsNullOrWhiteSpace(content))
            return (null, "请填写杂辅内容");
        if (!decimal.TryParse(hoursText?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var hours) || hours < 0)
            return (null, "小时数需为不小于 0 的数字");
        if (!decimal.TryParse(amountText?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount < 0)
            return (null, "杂辅工资需为不小于 0 的数字");
        return (new MiscWorkRecordInputDto
        {
            Id = Id,
            EmployeeId = employeeId.Value,
            WorkDate = date,
            Content = content.Trim(),
            Hours = hours,
            Amount = amount,
            Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim(),
        }, null);
    }

    // ========== 删除 ==========

    private async Task DeleteAsync(MiscWorkRowDto row)
    {
        var dialog = DialogService.Show<ConfirmDialog>("删除确认", new DialogParameters
        {
            ["ContentText"] = $"确定删除 {row.WorkDate.ToString("yyyy-MM-dd")} {row.EmployeeName}({row.EmployeeCode}) 的杂辅记录「{row.Content}」？",
            ["ConfirmText"] = "删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        var result = await MiscSvc.DeleteAsync(row.Id);
        if (result.Success)
        {
            Snackbar.Add("已删除", Severity.Success);
            await LoadMonthAsync();
        }
        else
        {
            Snackbar.Add(result.Message ?? "删除失败", Severity.Error);
        }
    }

    // ========== 月份导航 ==========

    private void OnYearChanged(int v) { _year = v; _ = LoadMonthAsync(); }
    private void OnMonthChanged(int v) { _month = v; _ = LoadMonthAsync(); }
    private void PrevMonth() { var d = new DateTime(_year, _month, 1).AddMonths(-1); _year = d.Year; _month = d.Month; _ = LoadMonthAsync(); }
    private void NextMonth() { var d = new DateTime(_year, _month, 1).AddMonths(1); _year = d.Year; _month = d.Month; _ = LoadMonthAsync(); }
    private void OnKeywordChanged(string v) { _keyword = v ?? string.Empty; StateHasChanged(); }
}
