using Microsoft.AspNetCore.Components;
using MudBlazor;
using MES.Blazor.Services;
using MES.Core.DTOs.StandardRegister;

namespace MES.Blazor.Pages.StandardRegister;

public partial class FactoryInspectionRequirementCreate
{
    private MudForm? _form;
    private bool _isSaving;
    private readonly CreateFactoryInspectionRequirementRequest _model = new()
    {
        // 新增字段创建默认值：表检+尺寸默认"必检"，PMI检验+内窥+水下气压+端口着色默认"按需"
        SurfaceInspection = "必检",
        Dimension = "必检",
        PmiInspection = "按需",
        Endoscopy = "按需",
        UnderwaterPressure = "按需",
        PortColoring = "按需",
    };

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
            var result = await FactoryInspectionRequirementService.CreateAsync(_model);
            if (result.Success)
            {
                Snackbar.Add("创建成功", Severity.Success);
                Navigation.NavigateTo("/factory-inspection-requirements");
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
        Navigation.NavigateTo("/factory-inspection-requirements");
    }
}
