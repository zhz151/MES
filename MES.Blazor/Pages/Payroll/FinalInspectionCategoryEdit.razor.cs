using Microsoft.AspNetCore.Components;
using MudBlazor;
using MES.Blazor.Services;
using MES.Blazor.Services.Payroll;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;

namespace MES.Blazor.Pages.Payroll;

/// <summary>
/// 成检计件类别定义 + 维档同页编辑（2026-09-03 引入）。
/// 上区成检项目(InspectionItem 单选) + 基准价/结算单位/启停/备注；
/// 下区按成检维度集（PieceRateInspectionDimensionKeys，区间维 4 + 等值维 4）分区折叠加档行，
/// 本地即时检查同维区间重叠（检验支数为整数档）/等值取值重复（红点提示），
/// 同成检项目启用唯一由服务端权威校验回显（保存时）。
/// </summary>
public partial class FinalInspectionCategoryEdit
{
    [Parameter] public int? Id { get; set; }

    [Inject] private PieceRateFinalInspectionCategoryService Service { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private bool _isEdit => Id.HasValue;
    private bool _loading = true;
    private bool _saving;

    // ========== 选项源 ==========
    private List<PieceRateCategoryOptionItemDto> _items = new();
    private List<PieceRateCategoryOptionItemDto> _units = new();
    private List<PieceRateCategoryOptionItemDto> _lengthStatuses = new();
    private List<PieceRateCategoryOptionItemDto> _states = new();
    private List<string> _grades = new();

    // ========== 类别定义状态 ==========
    private string? _itemKey;
    private decimal _basePrice;
    private string? _unit;
    private bool _isActive = true;
    private string? _remark;

    // ========== 维度档行 ==========
    private sealed class TierRow
    {
        public string DimensionKey { get; init; } = string.Empty;
        public string? RangeText { get; set; }   // 区间维原文（如 >54 / 41-54 / 5001-7500；长度档量纲 mm）
        public string? MatchValue { get; set; }  // 等值维取值
        public decimal Ratio { get; set; } = 1;
        public bool IsActive { get; set; } = true;
    }

    private readonly List<TierRow> _rows = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadOptionsAsync();
        if (_isEdit)
            await LoadDetailAsync();
        _loading = false;
    }

    private async Task LoadOptionsAsync()
    {
        try
        {
            var result = await Service.GetOptionsAsync();
            if (!result.Success || result.Data == null)
            {
                Snackbar.Add(result.Message ?? "选项加载失败", Severity.Error);
                return;
            }
            _items = result.Data.Items;
            _units = result.Data.Units;
            _lengthStatuses = result.Data.LengthStatuses;
            _states = result.Data.States;
            _grades = result.Data.Grades;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"选项加载失败: {ex.Message}", Severity.Error);
        }
    }

    private async Task LoadDetailAsync()
    {
        try
        {
            var result = await Service.GetByIdAsync(Id!.Value);
            if (!result.Success || result.Data == null)
            {
                Snackbar.Add(result.Message ?? $"类别不存在: {Id}", Severity.Error);
                await Task.Yield();
                Navigation.NavigateTo("/payroll/final-inspection-categories");
                return;
            }
            var dto = result.Data;
            _itemKey = dto.ItemKey;
            _basePrice = dto.BasePrice;
            _unit = dto.Unit;
            _isActive = dto.IsActive;
            _remark = dto.Remark;
            foreach (var tier in dto.Tiers)
            {
                _rows.Add(new TierRow
                {
                    DimensionKey = tier.DimensionKey,
                    RangeText = PieceRateInspectionDimensionKeys.IsValueDimension(tier.DimensionKey) ? null : tier.RangeText,
                    MatchValue = PieceRateInspectionDimensionKeys.IsValueDimension(tier.DimensionKey) ? tier.MatchValue : null,
                    Ratio = tier.Ratio,
                    IsActive = tier.IsActive
                });
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 维度分区渲染辅助 ==========

    private IEnumerable<TierRow> RowsOf(string dimKey) => _rows.Where(r => r.DimensionKey == dimKey);

    private static string DimChinese(string dimKey) => PieceRateInspectionDimensionKeys.ToChinese(dimKey) ?? dimKey;

    private static bool IsIntervalDim(string dimKey) => PieceRateInspectionDimensionKeys.IsInterval(dimKey);

    /// <summary>区间维输入框 Label（检验支数/长度档特化提示文案，量纲 mm）</summary>
    private static string RangeTextLabel(string dimKey)
        => dimKey switch
        {
            PieceRateInspectionDimensionKeys.InspectionCount =>
                "检验支数闭带（整数支数，如 1-10 / 11-100 / 1001-999999；勿用 >N 开口——按含 N 解析与相邻闭带判重叠，末档上限写业务最大可达）",
            PieceRateInspectionDimensionKeys.Length =>
                "长度区间（mm，如 5001-7500 / 11001-16000 / >16000；范围尺/非定尺取数默认按 6000 命中本档）",
            _ => $"{DimChinese(dimKey)} 区间（如 >54 / 41-54）"
        };

    private void AddTier(string dimKey) => _rows.Add(new TierRow { DimensionKey = dimKey });

    private void RemoveTier(TierRow row) => _rows.Remove(row);

    /// <summary>同维「区间重叠」提示（只提示同维区间行两两真正重叠；相切邻接=合法不提示；检验支数为整数共享即重叠）</summary>
    private bool HasIntervalOverlap(string dimKey)
    {
        var intMode = PieceRateInspectionDimensionKeys.InspectionCount.Equals(dimKey, StringComparison.Ordinal);
        var rows = _rows.Where(r => r.DimensionKey == dimKey && r.IsActive).ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            var ai = TryParseRange(rows[i].RangeText);
            if (ai == null) continue;
            for (var j = i + 1; j < rows.Count; j++)
            {
                var bj = TryParseRange(rows[j].RangeText);
                if (bj == null) continue;
                if (RangesOverlap(ai.Value, bj.Value, intMode))
                    return true;
            }
        }
        return false;
    }

    /// <summary>同维「等值重复」提示（OrdinalIgnoreCase，去空白）</summary>
    private bool HasValueDuplicate(string dimKey)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in _rows.Where(r => r.DimensionKey == dimKey && r.IsActive))
        {
            if (string.IsNullOrWhiteSpace(r.MatchValue)) continue;
            if (!seen.Add(r.MatchValue!.Trim())) return true;
        }
        return false;
    }

    private bool HasDimWarning(string dimKey)
        => IsIntervalDim(dimKey) ? HasIntervalOverlap(dimKey) : HasValueDuplicate(dimKey);

    private static (decimal? Min, decimal? Max)? TryParseRange(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        if (t.StartsWith('>'))
            return decimal.TryParse(t[1..].Trim(), out var lo) ? (lo, (decimal?)null) : null;
        if (t.StartsWith('<'))
            return decimal.TryParse(t[1..].Trim(), out var hi) ? ((decimal?)null, hi) : null;
        if (decimal.TryParse(t, out var eq)) return (eq, eq);
        var dash = t.IndexOf('-');
        if (dash > 0)
        {
            var okLo = decimal.TryParse(t[..dash].Trim(), out var lo);
            var okHi = decimal.TryParse(t[(dash + 1)..].Trim(), out var hi);
            return okLo && okHi ? (lo, hi) : null;
        }
        return null;
    }

    private static bool RangesOverlap((decimal? Min, decimal? Max) a, (decimal? Min, decimal? Max) b, bool intMode)
    {
        if (intMode)
        {
            // 检验支数整数：共享整数即重叠
            if (a.Max.HasValue && b.Min.HasValue && a.Max.Value < b.Min.Value) return false;
            if (b.Max.HasValue && a.Min.HasValue && b.Max.Value < a.Min.Value) return false;
            return true;
        }
        // 小数：边界相切视为合法邻接（半开衔接）
        if (a.Max.HasValue && b.Min.HasValue && a.Max.Value <= b.Min.Value) return false;
        if (b.Max.HasValue && a.Min.HasValue && b.Max.Value <= a.Min.Value) return false;
        return true;
    }

    // ========== 提交保存 ==========

    private async Task SaveAsync()
    {
        // 提交时汇总验证（禁止 alert）
        var errors = new List<string>();
        if (string.IsNullOrEmpty(_itemKey))
            errors.Add("未选成检项目");
        if (_basePrice <= 0)
            errors.Add("基准价必须大于0");
        if (string.IsNullOrEmpty(_unit))
            errors.Add("未选结算单位");
        var rows = _rows.Where(r => !string.IsNullOrWhiteSpace(r.RangeText) || !string.IsNullOrWhiteSpace(r.MatchValue)).ToList();
        foreach (var row in rows)
        {
            if (row.Ratio <= 0)
                errors.Add($"{DimChinese(row.DimensionKey)}档系数必须大于0");
        }
        foreach (var dim in PieceRateInspectionDimensionKeys.All)
        {
            if (HasDimWarning(dim))
                errors.Add($"{DimChinese(dim)}档区间重叠或取值重复，请修正后再保存");
        }
        if (errors.Count > 0)
        {
            foreach (var e in errors.Distinct().Take(5))
                Snackbar.Add(e, Severity.Error);
            return;
        }

        _saving = true;
        try
        {
            var request = new PieceRateFinalInspectionCategorySaveRequest
            {
                ItemKey = _itemKey!,
                BasePrice = _basePrice,
                Unit = _unit!,
                IsActive = _isActive,
                Remark = string.IsNullOrWhiteSpace(_remark) ? null : _remark,
                Tiers = rows.Select(r => new PieceRateFinalInspectionCategoryTierSaveRequest
                {
                    DimensionKey = r.DimensionKey,
                    RangeText = PieceRateInspectionDimensionKeys.IsValueDimension(r.DimensionKey) ? null : r.RangeText,
                    MatchValue = PieceRateInspectionDimensionKeys.IsValueDimension(r.DimensionKey) ? r.MatchValue : null,
                    Ratio = r.Ratio,
                    IsActive = r.IsActive
                }).ToList()
            };

            var result = await Service.SaveAsync(Id, request);
            if (result.Success && result.Data != null)
            {
                Snackbar.Add(_isEdit ? "已保存" : "已创建", Severity.Success);
                Navigation.NavigateTo("/payroll/final-inspection-categories");
            }
            else
            {
                Snackbar.Add(result.Message ?? "保存失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"保存失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private void Back() => Navigation.NavigateTo("/payroll/final-inspection-categories");
}
