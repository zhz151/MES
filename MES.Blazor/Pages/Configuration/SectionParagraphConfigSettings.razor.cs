using Microsoft.AspNetCore.Components;
using MudBlazor;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Pages.Configuration;

public partial class SectionParagraphConfigSettings
{
    private MudTable<SectionParagraphConfigDto>? table;
    private List<SectionParagraphConfigDto> _items = new();
    private bool _isLoading;
    private bool _isSaving;

    // ========== 3 类筛选 Tab（段落由 3 类配置自动生成，仅参数可编辑，按类筛选查看） ==========
    private List<(string Key, string Display)> _categoryTabs = new()
    {
        ("", "全部"),
        (ParagraphCategoryTypes.Cold, "冷轧拔"),
        (ParagraphCategoryTypes.Section, "普通工段"),
        (ParagraphCategoryTypes.Fixed, "检验"),
    };
    private string? _selectedCategory;
    private List<SectionParagraphConfigDto> _visibleItems => string.IsNullOrEmpty(_selectedCategory)
        ? _items
        : _items.Where(x => x.CategoryType == _selectedCategory).ToList();

    // ========== 主表编辑状态 ==========
    private HashSet<int> _editingIds = new();
    private Dictionary<int, SectionParagraphConfigDto> _editCache = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        try
        {
            var result = await Service.GetSettingsAsync();
            if (result.Success && result.Data != null)
            {
                _items = result.Data;
            }
            else
            {
                Snackbar.Add(result.Message ?? "获取数据失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void SetCategoryFilter(string? category)
    {
        _selectedCategory = category;
    }

    // ========== 主表编辑（仅参数：日流转设定/偏少/过多天数/备注，段落由配置自动生成） ==========

    private void StartEdit(SectionParagraphConfigDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new SectionParagraphConfigDto
        {
            // 完整复制（含段落名/Key/类型/序号）：PUT 走 [ApiController] ModelState 校验，非空引用类型
            // ParagraphName 缺失会触发「The ParagraphName field is required」→ 400（2026-08-31 修复）
            Id = item.Id,
            ParagraphName = item.ParagraphName,
            ParagraphKey = item.ParagraphKey,
            CategoryType = item.CategoryType,
            DisplayOrder = item.DisplayOrder,
            DailyFlowTarget = item.DailyFlowTarget,
            LowerLimitDays = item.LowerLimitDays,
            UpperLimitDays = item.UpperLimitDays,
            Remark = item.Remark,
        };
    }

    private void CancelEdit(SectionParagraphConfigDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(SectionParagraphConfigDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        _isSaving = true;
        try
        {
            var result = await Service.SaveSettingAsync(cache);
            if (result.Success)
            {
                item.DailyFlowTarget = cache.DailyFlowTarget;
                item.LowerLimitDays = cache.LowerLimitDays;
                item.UpperLimitDays = cache.UpperLimitDays;
                item.Remark = cache.Remark;

                _editingIds.Remove(item.Id);
                _editCache.Remove(item.Id);
                Snackbar.Add("保存成功", Severity.Success);
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
            _isSaving = false;
        }
    }
}
