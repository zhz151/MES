using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Blazor.Shared;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Pages.Configuration;

public partial class SectionFlowCategorySettings
{
    private MudTable<SectionFlowCategorySettingDto>? table;
    private List<SectionFlowCategorySettingDto> _items = new();
    private bool _isLoading;
    private bool _isSaving;

    // ========== 主表编辑状态 ==========
    private HashSet<int> _editingIds = new();
    private Dictionary<int, SectionFlowCategorySettingDto> _editCache = new();

    // ========== 子表编辑状态 ==========
    private HashSet<int> _itemEditingIds = new();
    private Dictionary<int, SectionFlowCategoryItemDto> _itemEditCache = new();

    // 新增行临时负ID
    private int _nextTempId = -1;

    // ========== 行展开状态 ==========
    private HashSet<int> _expandedIds = new();

    private void ToggleExpand(SectionFlowCategorySettingDto item)
    {
        if (!_expandedIds.Remove(item.Id))
            _expandedIds.Add(item.Id);
    }

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

    private void StartEdit(SectionFlowCategorySettingDto item)
    {
        if (!_editingIds.Add(item.Id)) return;
        _editCache[item.Id] = new SectionFlowCategorySettingDto
        {
            Id = item.Id,
            CategoryCode = item.CategoryCode,
            CategoryName = item.CategoryName,
            DailyProductionTarget = item.DailyProductionTarget,
            LowerLimitDays = item.LowerLimitDays,
            UpperLimitDays = item.UpperLimitDays,
            Remark = item.Remark,
        };
    }

    private void CancelEdit(SectionFlowCategorySettingDto item)
    {
        _editingIds.Remove(item.Id);
        _editCache.Remove(item.Id);
    }

    private async Task SaveEdit(SectionFlowCategorySettingDto item)
    {
        if (!_editCache.TryGetValue(item.Id, out var cache)) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(cache.CategoryCode)) errors.Add("编码不能为空");
        if (string.IsNullOrWhiteSpace(cache.CategoryName)) errors.Add("类别名称不能为空");
        if (errors.Any()) { Snackbar.Add(string.Join("；", errors), Severity.Warning); return; }

        _isSaving = true;
        try
        {
            var result = await Service.SaveSettingAsync(cache);
            if (result.Success)
            {
                // 更新原始 item 的值
                item.CategoryCode = cache.CategoryCode;
                item.CategoryName = cache.CategoryName;
                item.DailyProductionTarget = cache.DailyProductionTarget;
                item.LowerLimitDays = cache.LowerLimitDays;
                item.UpperLimitDays = cache.UpperLimitDays;
                item.Remark = cache.Remark;

                _editingIds.Remove(item.Id);
                _editCache.Remove(item.Id);
                if (table != null) await table.ReloadServerData();
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

    // ========== 子表渲染 ==========

    private RenderFragment RenderChildTable(SectionFlowCategorySettingDto setting) => builder =>
    {
        var items = setting.Items;
        if (items.Count == 0 && !_itemEditingIds.Any(id => items.Any(i => i.Id == id)))
        {
            // 空状态，只显示新增按钮
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "pa-4");
            builder.AddContent(2, "暂无明细，请点击下方按钮添加");
            builder.CloseElement();
        }

        builder.OpenComponent<MudTable<SectionFlowCategoryItemDto>>(10);
        builder.AddAttribute(11, "Items", items);
        builder.AddAttribute(12, "Dense", true);
        builder.AddAttribute(13, "Hover", true);
        builder.AddAttribute(14, "Elevation", 0);
        builder.AddAttribute(15, "Class", "ml-4 mt-2 child-table");

        // HeaderContent
        builder.AddAttribute(16, "HeaderContent", (RenderFragment)(hb =>
        {
            hb.OpenElement(17, "thead");
            hb.OpenElement(18, "tr");
            foreach (var col in new[] { ("工序组", ""), ("工段", ""), ("变异量系数", "mud-table-cell--right"), ("排序号", "mud-table-cell--right"), ("操作", "mud-table-cell--center") })
            {
                hb.OpenElement(19, "th");
                if (!string.IsNullOrEmpty(col.Item2))
                    hb.AddAttribute(20, "class", col.Item2);
                hb.OpenElement(21, "span");
                hb.AddAttribute(22, "class", "th-label");
                hb.AddContent(23, col.Item1);
                hb.CloseElement();
                hb.CloseElement();
            }
            hb.CloseElement();
            hb.CloseElement();
        }));

        // RowTemplate
        builder.AddAttribute(30, "RowTemplate", (RenderFragment<SectionFlowCategoryItemDto>)(item => rb =>
        {
            var itemEditing = _itemEditingIds.Contains(item.Id);
            var itemCache = itemEditing && _itemEditCache.TryGetValue(item.Id, out var ic) ? ic : null;

            // ProcessGroupName
            rb.OpenElement(100, "td");
            rb.AddContent(101, ProcessDisplayHelper.GetProcessNameText(item.ProcessGroupName));
            rb.CloseElement();

            // SectionName
            rb.OpenElement(110, "td");
            rb.AddContent(111, SectionDisplayHelper.GetSectionNameText(item.SectionName));
            rb.CloseElement();

            // Coefficient
            rb.OpenElement(120, "td");
            rb.AddAttribute(121, "class", "mud-table-cell--right");
            if (itemEditing && itemCache != null)
            {
                rb.OpenComponent<MudNumericField<decimal>>(122);
                rb.AddAttribute(123, "Dense", true);
                rb.AddAttribute(124, "Variant", Variant.Outlined);
                rb.AddAttribute(125, "HideSpinButtons", true);
                rb.AddAttribute(126, "Format", "G29");
                rb.AddAttribute(127, "Class", "compact-input");
                rb.AddAttribute(128, "Value", itemCache.Coefficient);
                rb.AddAttribute(129, "ValueChanged", EventCallback.Factory.Create<decimal>(this, v => itemCache.Coefficient = v));
                rb.CloseComponent();
            }
            else
            {
                rb.AddContent(130, item.Coefficient.ToString("G29"));
            }
            rb.CloseElement();

            // DisplayOrder
            rb.OpenElement(140, "td");
            rb.AddAttribute(141, "class", "mud-table-cell--right");
            if (itemEditing && itemCache != null)
            {
                rb.OpenComponent<MudNumericField<int>>(142);
                rb.AddAttribute(143, "Dense", true);
                rb.AddAttribute(144, "Variant", Variant.Outlined);
                rb.AddAttribute(145, "HideSpinButtons", true);
                rb.AddAttribute(146, "Class", "compact-input");
                rb.AddAttribute(147, "Value", itemCache.DisplayOrder);
                rb.AddAttribute(148, "ValueChanged", EventCallback.Factory.Create<int>(this, v => itemCache.DisplayOrder = v));
                rb.CloseComponent();
            }
            else
            {
                rb.AddContent(149, item.DisplayOrder.ToString());
            }
            rb.CloseElement();

            // 操作按钮
            rb.OpenElement(150, "td");
            rb.AddAttribute(151, "class", "mud-table-cell--center");
            rb.AddAttribute(152, "style", "white-space: nowrap;");
            if (itemEditing)
            {
                rb.OpenComponent<MudIconButton>(153);
                rb.AddAttribute(154, "Icon", Icons.Material.Filled.Check);
                rb.AddAttribute(155, "Color", Color.Success);
                rb.AddAttribute(156, "Size", Size.Small);
                rb.AddAttribute(157, "Disabled", _isSaving);
                rb.AddAttribute(158, "OnClick", EventCallback.Factory.Create<MouseEventArgs>(this, () => SaveItemEdit(item)));
                rb.CloseComponent();

                rb.OpenComponent<MudIconButton>(160);
                rb.AddAttribute(161, "Icon", Icons.Material.Filled.Close);
                rb.AddAttribute(162, "Color", Color.Default);
                rb.AddAttribute(163, "Size", Size.Small);
                rb.AddAttribute(164, "OnClick", EventCallback.Factory.Create<MouseEventArgs>(this, () => CancelItemEdit(item)));
                rb.CloseComponent();
            }
            else
            {
                rb.OpenComponent<MudIconButton>(170);
                rb.AddAttribute(171, "Icon", Icons.Material.Filled.Edit);
                rb.AddAttribute(172, "Color", Color.Info);
                rb.AddAttribute(173, "Size", Size.Small);
                rb.AddAttribute(174, "OnClick", EventCallback.Factory.Create<MouseEventArgs>(this, () => StartItemEdit(item)));
                rb.CloseComponent();

                rb.OpenComponent<MudIconButton>(180);
                rb.AddAttribute(181, "Icon", Icons.Material.Filled.Delete);
                rb.AddAttribute(182, "Color", Color.Error);
                rb.AddAttribute(183, "Size", Size.Small);
                rb.AddAttribute(184, "OnClick", EventCallback.Factory.Create<MouseEventArgs>(this, () => DeleteItem(setting, item)));
                rb.CloseComponent();
            }
            rb.CloseElement();
        }));

        builder.CloseComponent();

        // 新增按钮
        builder.OpenComponent<MudButton>(200);
        builder.AddAttribute(201, "Size", Size.Small);
        builder.AddAttribute(202, "Color", Color.Primary);
        builder.AddAttribute(203, "Class", "ml-4 mt-1 mb-2");
        builder.AddAttribute(204, "StartIcon", Icons.Material.Filled.Add);
        builder.AddAttribute(205, "OnClick", EventCallback.Factory.Create<MouseEventArgs>(this, () => AddNewItem(setting)));
        builder.AddAttribute(206, "ChildContent", (RenderFragment)(ab =>
        {
            ab.AddContent(207, "新增明细");
        }));
        builder.CloseComponent();
    };

    // ========== 子表编辑 ==========

    private void StartItemEdit(SectionFlowCategoryItemDto item)
    {
        if (!_itemEditingIds.Add(item.Id)) return;
        _itemEditCache[item.Id] = new SectionFlowCategoryItemDto
        {
            Id = item.Id,
            SettingId = item.SettingId,
            ProcessGroupName = item.ProcessGroupName,
            SectionName = item.SectionName,
            Coefficient = item.Coefficient,
            DisplayOrder = item.DisplayOrder,
        };
    }

    private void CancelItemEdit(SectionFlowCategoryItemDto item)
    {
        _itemEditingIds.Remove(item.Id);
        _itemEditCache.Remove(item.Id);
    }

    private async Task SaveItemEdit(SectionFlowCategoryItemDto item)
    {
        if (!_itemEditCache.TryGetValue(item.Id, out var cache)) return;

        _isSaving = true;
        try
        {
            var result = await Service.SaveItemAsync(item.Id, cache);
            if (result.Success)
            {
                item.Coefficient = cache.Coefficient;
                item.DisplayOrder = cache.DisplayOrder;

                _itemEditingIds.Remove(item.Id);
                _itemEditCache.Remove(item.Id);
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

    // ========== 子表删除/新增 ==========

    private async Task DeleteItem(SectionFlowCategorySettingDto setting, SectionFlowCategoryItemDto item)
    {
        // 新增行（临时负ID）直接移除
        if (item.Id < 0)
        {
            setting.Items.Remove(item);
            return;
        }

        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = $"确定要删除明细\"{ProcessDisplayHelper.GetProcessNameText(item.ProcessGroupName)} / {SectionDisplayHelper.GetSectionNameText(item.SectionName)}\" 吗？",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (!dialogResult.Canceled)
        {
            try
            {
                var result = await Service.DeleteItemAsync(item.Id);
                if (result.Success)
                {
                    setting.Items.Remove(item);
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

    private void AddNewItem(SectionFlowCategorySettingDto setting)
    {
        var tempId = _nextTempId--;
        var newItem = new SectionFlowCategoryItemDto
        {
            Id = tempId,
            SettingId = setting.Id,
            ProcessGroupName = "",
            SectionName = "",
            Coefficient = 1m,
            DisplayOrder = setting.Items.Count + 1,
        };

        setting.Items.Add(newItem);
        StartItemEdit(newItem);
        StateHasChanged();
    }
}
