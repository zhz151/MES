using Microsoft.AspNetCore.Components;
using MudBlazor;
using MES.Blazor.Services;
using MES.Core.Models;
using MES.Blazor.Shared;
using MES.Core.DTOs.ProductionStandard;

namespace MES.Blazor.Pages.ProductionStandard;

public partial class StandardRegisterDetail
{
    [Parameter] public int Id { get; set; }

    private StandardRegisterDto _dto = new();
    private StandardRegisterDto _editDto = new();
    private List<StandardRegisterItemDto> _items = new();
    private bool _isLoading = true;
    private bool _isSubmitting;
    private bool _isEditMode;
    private bool _isCreateMode => Id == 0;

    // 复制来源
    private List<StandardRegisterDto> _allStandards = new();
    private int? _sourceStandardId;

    protected override async Task OnInitializedAsync()
    {
        if (_isCreateMode)
        {
            _isEditMode = true;
            _isLoading = false;
            await LoadAllStandards();
        }
        else
        {
            await LoadData();
        }
    }

    private async Task LoadAllStandards()
    {
        try
        {
            var result = await Svc.GetPagedAsync(new QueryParams
            {
                PageIndex = 1,
                PageSize = 500,
                SortBy = "standardno",
                IsDescending = false
            });
            if (result.Success && result.Data != null)
            {
                _allStandards = result.Data.Items;
            }
        }
        catch { }
    }

    private async Task OnSourceChanged(int? sourceId)
    {
        _sourceStandardId = sourceId;
        _items.Clear();
        if (sourceId == null) return;

        try
        {
            var result = await Svc.GetItemsAsync(sourceId.Value);
            if (result.Success && result.Data != null)
            {
                _items = result.Data.Select(i => new StandardRegisterItemDto
                {
                    StandardRegisterId = 0,
                    SeqNo = i.SeqNo,
                    InspectionCategory = i.InspectionCategory,
                    InspectionItem = i.InspectionItem,
                    IsMandatory = i.IsMandatory,
                    SamplingRequirement = i.SamplingRequirement,
                    ApplicableRange = i.ApplicableRange,
                    RefStandard = i.RefStandard,
                    DetailRequirement = i.DetailRequirement
                }).ToList();
            }
        }
        catch { }
    }

    private async Task LoadData()
    {
        _isLoading = true;
        try
        {
            var result = await Svc.GetByIdAsync(Id);
            if (result.Success && result.Data != null)
            {
                _dto = result.Data;
                CopyToEdit();
            }
            else
            {
                Snackbar.Add(result.Message ?? "加载失败", Severity.Error);
            }

            var itemsResult = await Svc.GetItemsAsync(Id);
            if (itemsResult.Success && itemsResult.Data != null)
            {
                _items = itemsResult.Data;
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

    private void CopyToEdit()
    {
        _editDto = new StandardRegisterDto
        {
            Id = _dto.Id,
            StandardNo = _dto.StandardNo,
            StandardName = _dto.StandardName,
            RefSpecification = _dto.RefSpecification,
            StandardLevel = _dto.StandardLevel,
            ManufactureMethod = _dto.ManufactureMethod,
            SteelType = _dto.SteelType,
            Remark = _dto.Remark
        };
    }

    private void EnterEditMode()
    {
        CopyToEdit();
        _isEditMode = true;
    }

    private void CancelEdit()
    {
        if (_isCreateMode)
        {
            GoBack();
            return;
        }
        _isEditMode = false;
        CopyToEdit();
    }

    private async Task SaveAll()
    {
        if (string.IsNullOrWhiteSpace(_editDto.StandardNo))
        {
            Snackbar.Add("标准号不能为空", Severity.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_editDto.StandardName))
        {
            Snackbar.Add("标准名称不能为空", Severity.Warning);
            return;
        }

        _isSubmitting = true;
        try
        {
            // 1. 保存标准号头，拿到 Id
            var headerResult = await Svc.SaveAsync(_editDto);
            if (!headerResult.Success || headerResult.Data <= 0)
            {
                Snackbar.Add(headerResult.Message ?? "保存标准号失败", Severity.Error);
                return;
            }

            var headerId = headerResult.Data;

            // 2. 保存子项目
            foreach (var item in _items)
            {
                item.StandardRegisterId = headerId;
                var itemResult = await Svc.SaveItemAsync(item);
                if (itemResult.Success && itemResult.Data > 0 && item.Id == 0)
                {
                    item.Id = itemResult.Data; // 回写新 Id，防重复创建
                }
            }

            Snackbar.Add("保存成功", Severity.Success);
            if (_isCreateMode)
            {
                Navigation.NavigateTo("/standard-registers");
            }
            else
            {
                _isEditMode = false;
                // 重新加载数据确保最新
                await LoadData();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"保存失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private void AddItem()
    {
        var maxSeq = _items.Count > 0 ? _items.Max(i => i.SeqNo) : 0;
        _items.Add(new StandardRegisterItemDto
        {
            StandardRegisterId = Id,
            SeqNo = maxSeq + 1,
            InspectionItem = ""
        });
    }

    private async Task RemoveItem(StandardRegisterItemDto item)
    {
        if (item.Id == 0)
        {
            _items.Remove(item);
            return;
        }

        var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
        {
            ["ContentText"] = "确定要删除该子项目吗？",
            ["ConfirmText"] = "确认删除",
            ["Color"] = Color.Error
        });
        var dialogResult = await dialog.Result;
        if (dialogResult.Canceled) return;

        try
        {
            var result = await Svc.DeleteItemAsync(item.Id);
            if (result.Success)
            {
                _items.Remove(item);
                Snackbar.Add("删除成功", Severity.Success);
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

    private void GoBack() => Navigation.NavigateTo("/standard-registers");
}
