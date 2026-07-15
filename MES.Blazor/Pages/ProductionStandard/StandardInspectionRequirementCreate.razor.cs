using Microsoft.AspNetCore.Components;
using MudBlazor;
using MES.Blazor.Services;
using MES.Core.DTOs.ProductionStandard;

namespace MES.Blazor.Pages.ProductionStandard;

public partial class StandardInspectionRequirementCreate
{
    private MudForm? _form;
    private bool _isSaving;
    private readonly CreateStandardInspectionRequirementRequest _model = new();

    private string? ValidateStandardNo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "标准号不能为空";
        return null;
    }

    private async Task SaveAsync()
    {
        if (_isSaving) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(_model.StandardNo))
            errors.Add("标准号不能为空");
        if (errors.Count > 0)
        {
            Snackbar.Add(string.Join("；", errors), Severity.Warning);
            return;
        }

        _isSaving = true;
        StateHasChanged();

        try
        {
            var result = await StandardInspectionRequirementService.CreateAsync(_model);
            if (result.Success)
            {
                Snackbar.Add("创建成功", Severity.Success);
                Navigation.NavigateTo("/standard-inspection-requirements");
            }
            else
            {
                Snackbar.Add(result.Message ?? "创建失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"创建失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    private void Cancel()
    {
        Navigation.NavigateTo("/standard-inspection-requirements");
    }
}
