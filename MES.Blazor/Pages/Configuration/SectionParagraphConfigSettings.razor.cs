using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MES.Blazor.Shared;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Pages.Configuration;

public partial class SectionParagraphConfigSettings
{
    private MudTable<SectionParagraphConfigDto>? table;
    private List<SectionParagraphConfigDto> _items = new();
    private bool _isLoading;
    private bool _isSaving;

    // ========== 主表编辑状态 ==========
    private HashSet<int> _editingIds = new();
    private Dictionary<int, SectionParagraphConfigDto> _editCache = new();

    // 新增行临时负ID
    private int _nextTempId = -1;

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

    // ========== 主表编辑 ==========

    private void StartEdit(SectionParagraphConfigDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new SectionParagraphConfigDto
        {
            Id = item.Id,
            ParagraphName = item.ParagraphName,
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

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.ParagraphName)) errors.Add("段落类别不能为空");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        try
        {
            var isNew = item.Id < 0;
            var result = isNew
                ? await Service.CreateSettingAsync(cache)
                : await Service.SaveSettingAsync(cache);
            if (result.Success)
            {
                if (isNew)
                {
                    // 新增成功：移除临时行并重新加载（获取真实 Id）
                    _editingIds.Remove(item.Id);
                    _editCache.Remove(item.Id);
                    await LoadDataAsync();
                }
                else
                {
                    // 更新原始 item 的值
                    item.ParagraphName = cache.ParagraphName;
                    item.DisplayOrder = cache.DisplayOrder;
                    item.DailyFlowTarget = cache.DailyFlowTarget;
                    item.LowerLimitDays = cache.LowerLimitDays;
                    item.UpperLimitDays = cache.UpperLimitDays;
                    item.Remark = cache.Remark;

                    _editingIds.Remove(item.Id);
                    _editCache.Remove(item.Id);
                    if (table != null) await table.ReloadServerData();
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

    private void AddNewSetting()
    {
        var tempId = _nextTempId--;
        var newItem = new SectionParagraphConfigDto
        {
            Id = tempId,
            ParagraphName = "",
            DisplayOrder = _items.Count + 1,
        };
        _items.Add(newItem);
        StartEdit(newItem);
        StateHasChanged();
    }

    private async Task DeleteSetting(SectionParagraphConfigDto item)
    {
        // 新增行（临时负ID）直接移除
        if (item.Id < 0)
        {
            _items.Remove(item);
            _editingIds.Remove(item.Id);
            _editCache.Remove(item.Id);
            StateHasChanged();
            return;
        }

        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除段落\"{item.ParagraphName}\" 及其全部组合归类归属吗？",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        try
        {
            var result = await Service.DeleteSettingAsync(item.Id);
            if (result.Success)
            {
                _items.Remove(item);
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
}
