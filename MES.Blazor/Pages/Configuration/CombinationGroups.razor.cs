using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Blazor.Services;
using MES.Blazor.Shared;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Pages.Configuration;

public partial class CombinationGroups
{
    [Inject] private CombinationGroupService Service { get; set; } = null!;
    [Inject] private SectionFlowCategoryService CategoryService { get; set; } = null!;
    [Inject] private SectionParagraphConfigService ParagraphService { get; set; } = null!;
    [Inject] private ProcessDefinitionService ProcessService { get; set; } = null!;
    [Inject] private StandardWorkDayService WorkDayService { get; set; } = null!;

    private List<CombinationGroupDto> _items = new();
    private List<SectionFlowCategorySettingDto> _categoryOptions = new();
    private List<SectionParagraphConfigDto> _paragraphOptions = new();
    private List<ProcessInfoDto> _processOptions = new();
    private List<SectionInfoDto> _sectionOptions = new();
    private static readonly (string Value, string Text)[] _productStatusOptions = new[]
    {
        (ProductStatuses.RoughTube, "荒管"),
        (ProductStatuses.InProgress, "在制"),
        (ProductStatuses.Finished, "成品"),
    };
    private bool _isLoading;
    private bool _isSaving;
    private int _nextTempId = -1;

    // ========== 列筛选（内存模式） ==========
    private List<CombinationGroupDto> _visibleItems = new();
    private Dictionary<string, HashSet<string>> _columnFilters = new();

    // ========== 编辑状态 ==========
    private HashSet<int> _editingIds = new();
    private Dictionary<int, CombinationGroupDto> _editCache = new();

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(LoadOptionsAsync(), LoadDataAsync());
    }

    private async Task LoadOptionsAsync()
    {
        var catTask = CategoryService.GetSettingsAsync();
        var parTask = ParagraphService.GetSettingsAsync();
        var procTask = ProcessService.GetEnabledProcessesAsync();
        var sectTask = WorkDayService.GetEnabledSectionsAsync();
        await Task.WhenAll(catTask, parTask, procTask, sectTask);
        if (catTask.Result.Success && catTask.Result.Data != null)
            _categoryOptions = catTask.Result.Data;
        if (parTask.Result.Success && parTask.Result.Data != null)
            _paragraphOptions = parTask.Result.Data;
        if (procTask.Result.Success && procTask.Result.Data != null)
            _processOptions = procTask.Result.Data;
        if (sectTask.Result.Success && sectTask.Result.Data != null)
            _sectionOptions = sectTask.Result.Data;
    }

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        try
        {
            var result = await Service.GetListAsync();
            if (result.Success && result.Data != null)
            {
                _items = result.Data;
                ApplyColumnFilters();
            }
            else
                Snackbar.Add(result.Message ?? "获取数据失败", Severity.Error);
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

    // ========== 列筛选（内存过滤，三维：工序组/工段/产类） ==========

    private void OnColumnFilterChanged(string fieldKey, HashSet<string> selectedValues)
    {
        if (selectedValues.Count > 0)
            _columnFilters[fieldKey] = selectedValues;
        else
            _columnFilters.Remove(fieldKey);
        ApplyColumnFilters();
        StateHasChanged();
    }

    private void ApplyColumnFilters()
    {
        _visibleItems = _items.Where(item =>
            MatchesFilter("ProcessGroupName", item.ProcessGroupName) &&
            MatchesFilter("SectionName", item.SectionName) &&
            MatchesFilter("ProductStatus", item.ProductStatus)
        ).ToList();
    }

    private bool MatchesFilter(string field, string? value)
    {
        if (!_columnFilters.TryGetValue(field, out var selected) || selected.Count == 0) return true;
        return value != null && selected.Contains(value);
    }

    // ========== 编辑 ==========

    private void StartEdit(CombinationGroupDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new CombinationGroupDto
        {
            Id = item.Id,
            ProcessGroupName = item.ProcessGroupName,
            SectionName = item.SectionName,
            ProductStatus = item.ProductStatus,
            FlowCategoryId = item.FlowCategoryId,
            ParagraphName = item.ParagraphName,
        };
    }

    private void CancelEdit(CombinationGroupDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private void AddNewItem()
    {
        var tempId = _nextTempId--;
        _items.Add(new CombinationGroupDto
        {
            Id = tempId,
            ProcessGroupName = _processOptions.FirstOrDefault()?.ProcessKey ?? "",
            SectionName = _sectionOptions.FirstOrDefault()?.SectionKey ?? "",
            ProductStatus = ProductStatuses.RoughTube,
            FlowCategoryId = null,
            ParagraphName = null,
        });
        StartEdit(_items[^1]);
        ApplyColumnFilters();
        StateHasChanged();
    }

    private async Task SaveEdit(CombinationGroupDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.ProcessGroupName)) errors.Add("工序组不能为空");
        if (string.IsNullOrWhiteSpace(cache.SectionName)) errors.Add("工段不能为空");
        if (string.IsNullOrWhiteSpace(cache.ProductStatus)) errors.Add("产类不能为空");
        if (!cache.FlowCategoryId.HasValue) errors.Add("归属流转类别不能为空");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        try
        {
            var result = await Service.SaveAsync(cache);
            if (result.Success)
            {
                if (item.Id < 0)
                {
                    _editingIds.Remove(item.Id);
                    _editCache.Remove(item.Id);
                    await LoadDataAsync();
                }
                else
                {
                    item.ProcessGroupName = cache.ProcessGroupName;
                    item.SectionName = cache.SectionName;
                    item.ProductStatus = cache.ProductStatus;
                    item.FlowCategoryId = cache.FlowCategoryId;
                    item.FlowCategoryName = _categoryOptions.FirstOrDefault(c => c.Id == cache.FlowCategoryId)?.CategoryName;
                    item.ParagraphName = cache.ParagraphName;
                    _editingIds.Remove(item.Id);
                    _editCache.Remove(item.Id);
                    ApplyColumnFilters();
                }
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

    private async Task DeleteItem(CombinationGroupDto item)
    {
        if (item.Id < 0)
        {
            _items.Remove(item);
            _editingIds.Remove(item.Id);
            _editCache.Remove(item.Id);
            ApplyColumnFilters();
            StateHasChanged();
            return;
        }

        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除组合\"{ProcessDisplayHelper.GetProcessNameText(item.ProcessGroupName)} / {SectionDisplayHelper.GetSectionNameText(item.SectionName)} / {GetProductStatusDisplay(item.ProductStatus)}\" 吗？",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        try
        {
            var result = await Service.DeleteAsync(item.Id);
            if (result.Success)
            {
                _items.Remove(item);
                ApplyColumnFilters();
                Snackbar.Add("删除成功", Severity.Success);
                StateHasChanged();
            }
            else
            {
                Snackbar.Add(result.Message ?? "删除失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"删除失败: {ex.Message}", Severity.Error);
        }
    }

    private string GetCategoryDisplay(int? id)
        => id.HasValue ? (_categoryOptions.FirstOrDefault(c => c.Id == id.Value)?.CategoryName ?? "-") : "-";

    private string GetParagraphDisplay(string? paragraphName)
        => string.IsNullOrEmpty(paragraphName) ? "-" : paragraphName;

    private static string GetProcessDisplay(string key)
        => ProcessKeys.ToChinese(key) ?? key;

    private static string GetSectionDisplay(string key)
        => SectionKeys.ToChinese(key) ?? key;

    private static string GetProductStatusDisplay(string productStatus)
        => DisplayHelper.GetCombinationProductStatusText(productStatus);
}
