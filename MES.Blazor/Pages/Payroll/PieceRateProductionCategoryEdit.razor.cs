using Microsoft.AspNetCore.Components;
using MudBlazor;
using MES.Blazor.Services;
using MES.Blazor.Services.Payroll;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;

namespace MES.Blazor.Pages.Payroll;

/// <summary>
/// 生产计件类别定义 + 维档同页编辑（2026-09-02 两表模型）。
/// 上区四键（工段单选 + 工序/产类/阶段多选空=全选）+ 基准价/单位/启停；
/// 下区按维度分区折叠加档行，本地即时检查同维区间重叠/等值重复（红点提示），
/// 跨类别覆盖冲突由服务端权威校验回显（保存时）。
/// </summary>
public partial class PieceRateProductionCategoryEdit
{
    [Parameter] public int? Id { get; set; }

    [Inject] private PieceRateProductionCategoryService Service { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private bool _isEdit => Id.HasValue;
    private bool _loading = true;
    private bool _saving;

    // ========== 选项源 ==========
    private List<PieceRateCategoryOptionItemDto> _sections = new();
    private List<PieceRateCategoryOptionItemDto> _processes = new();
    private List<PieceRateCategoryOptionItemDto> _productStatuses = new();
    private List<PieceRateCategoryOptionItemDto> _stages = new();
    private List<PieceRateCategoryOptionItemDto> _units = new();
    private List<PieceRateCategoryOptionItemDto> _states = new();
    private List<string> _grades = new();

    // ========== 类别定义状态 ==========
    private string? _sectionKey;
    private List<string> _processKeys = new();
    private List<string> _productStatusKeys = new();
    private List<string> _stageKeys = new();
    private decimal _basePrice;
    private string? _unit;
    private bool _isActive = true;
    private string? _remark;

    // ========== 维度档行 ==========
    private sealed class TierRow
    {
        public string DimensionKey { get; init; } = string.Empty;
        public string? RangeText { get; set; }   // 区间维原文（如 >54 / 41-54 / 6-8）
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
            _sections = result.Data.Sections;
            _processes = result.Data.Processes;
            _productStatuses = result.Data.ProductStatuses;
            _stages = result.Data.Stages;
            _units = result.Data.Units;
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
                Navigation.NavigateTo("/payroll/piece-rate-categories");
                return;
            }
            var dto = result.Data;
            _sectionKey = dto.SectionKey;
            _processKeys = dto.ProcessKeys ?? new();
            _productStatusKeys = dto.ProductStatusKeys ?? new();
            _stageKeys = dto.StageKeys ?? new();
            _basePrice = dto.BasePrice;
            _unit = dto.Unit;
            _isActive = dto.IsActive;
            _remark = dto.Remark;
            foreach (var tier in dto.Tiers)
            {
                _rows.Add(new TierRow
                {
                    DimensionKey = tier.DimensionKey,
                    RangeText = PieceRateDimensionKeys.IsValueDimension(tier.DimensionKey) ? null : tier.RangeText,
                    MatchValue = PieceRateDimensionKeys.IsValueDimension(tier.DimensionKey) ? tier.MatchValue : null,
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

    private static string DimChinese(string dimKey) => PieceRateDimensionKeys.ToChinese(dimKey) ?? dimKey;

    private bool IsIntervalDim(string dimKey) => PieceRateDimensionKeys.IsInterval(dimKey);

    private void AddTier(string dimKey) => _rows.Add(new TierRow { DimensionKey = dimKey });

    private void RemoveTier(TierRow row) => _rows.Remove(row);

    /// <summary>同维「区间重叠」提示（只提示同维区间行两两真正重叠；相切邻接=合法不提示）</summary>
    private bool HasIntervalOverlap(string dimKey)
    {
        var intMode = PieceRateDimensionKeys.FixedLengthCount.Equals(dimKey, StringComparison.Ordinal);
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
            // 定尺整数：共享整数即重叠
            if (a.Max.HasValue && b.Min.HasValue && a.Max.Value < b.Min.Value) return false;
            if (b.Max.HasValue && a.Min.HasValue && b.Max.Value < a.Min.Value) return false;
            return true;
        }
        // 小数：边界相切视为合法邻接（半开衔接）
        if (a.Max.HasValue && b.Min.HasValue && a.Max.Value <= b.Min.Value) return false;
        if (b.Max.HasValue && a.Min.HasValue && b.Max.Value <= a.Min.Value) return false;
        return true;
    }

    // ========== 自动组合名实时预览 ==========

    private string PreviewName
    {
        get
        {
            var secCn = ResolveName(_sectionKey, _sections) ?? "未选工段";
            var prodCn = JoinKeysCn(_productStatusKeys, _productStatuses, "全部产类");
            var procCn = JoinKeysCn(_processKeys, _processes, "全部工序");
            var stageCn = JoinKeysCn(_stageKeys, _stages, "全部阶段");
            return $"{secCn}｜{prodCn}｜{procCn}｜{stageCn}";
        }
    }

    private static string? ResolveName(string? key, IEnumerable<PieceRateCategoryOptionItemDto> options)
    {
        if (string.IsNullOrEmpty(key)) return null;
        var hit = options.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
        return hit?.Name ?? key;
    }

    private static string JoinKeysCn(IEnumerable<string> keys, IEnumerable<PieceRateCategoryOptionItemDto> options, string allText)
    {
        var list = keys.ToList();
        if (list.Count == 0) return allText;
        var dict = options.ToDictionary(o => o.Key, o => o.Name, StringComparer.OrdinalIgnoreCase);
        return string.Join("·", list.Select(k => dict.TryGetValue(k, out var cn) ? cn : k));
    }

    // ========== MudSelect 多选闭合框中文显示 ==========
    // MudBlazor 多选闭合文字 = string.Join(", ", SelectedValues.Select(x => Converter.Set(x)))，Converter 由
    // ToStringFunc 决定（默认 x.ToString() 直出英文 Key）；必须设 ToStringFunc 映射回中文，否则显示英文。
    private string ProcessNameText(string? k) => ResolveName(k, _processes) ?? k ?? string.Empty;
    private string ProductStatusNameText(string? k) => ResolveName(k, _productStatuses) ?? k ?? string.Empty;
    private string StageNameText(string? k) => ResolveName(k, _stages) ?? k ?? string.Empty;

    // ========== 提交保存 ==========

    private async Task SaveAsync()
    {
        // 提交时汇总验证（禁止 alert）
        var errors = new List<string>();
        if (string.IsNullOrEmpty(_sectionKey))
            errors.Add("请选择工段");
        if (_basePrice <= 0)
            errors.Add("基准价必须大于0");
        if (string.IsNullOrEmpty(_unit))
            errors.Add("请选择结算单位");
        var rows = _rows.Where(r => !string.IsNullOrWhiteSpace(r.RangeText) || !string.IsNullOrWhiteSpace(r.MatchValue)).ToList();
        foreach (var row in rows)
        {
            if (row.Ratio <= 0)
                errors.Add($"{DimChinese(row.DimensionKey)}档系数必须大于0");
        }
        foreach (var dim in PieceRateDimensionKeys.All)
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
            var request = new PieceRateProductionCategorySaveRequest
            {
                SectionKey = _sectionKey!,
                ProcessKeys = _processKeys ?? new(),
                ProductStatusKeys = _productStatusKeys ?? new(),
                StageKeys = _stageKeys ?? new(),
                BasePrice = _basePrice,
                Unit = _unit!,
                IsActive = _isActive,
                Remark = string.IsNullOrWhiteSpace(_remark) ? null : _remark,
                Tiers = rows.Select(r => new PieceRateProductionCategoryTierSaveRequest
                {
                    DimensionKey = r.DimensionKey,
                    RangeText = PieceRateDimensionKeys.IsValueDimension(r.DimensionKey) ? null : r.RangeText,
                    MatchValue = PieceRateDimensionKeys.IsValueDimension(r.DimensionKey) ? r.MatchValue : null,
                    Ratio = r.Ratio,
                    IsActive = r.IsActive
                }).ToList()
            };

            var result = await Service.SaveAsync(Id, request);
            if (result.Success && result.Data != null)
            {
                Snackbar.Add(_isEdit ? "已保存" : "已创建", Severity.Success);
                Navigation.NavigateTo("/payroll/piece-rate-categories");
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

    private void Back() => Navigation.NavigateTo("/payroll/piece-rate-categories");
}
