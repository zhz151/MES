using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Blazor.Services;
using MES.Core.Enums;
using MES.Core.DTOs.Quality;

namespace MES.Blazor.Pages.Quality;

public partial class NcrPrint
{
    [Inject] private NcrService NcrService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [Parameter] public int Id { get; set; }

    private NcrDto? _ncr;
    private bool _loading = true;
    private bool _error;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var response = await NcrService.GetByIdAsync(Id);
            if (response.Success && response.Data != null)
            {
                _ncr = response.Data;
            }
            else
            {
                _error = true;
                Snackbar.Add($"加载不合格报告 #{Id} 失败: {response.Message}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            _error = true;
            Snackbar.Add($"网络错误: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task Print()
    {
        await JS.InvokeVoidAsync("print");
    }

    private void GoBack()
    {
        Navigation.NavigateTo("/quality/ncr");
    }

    // ========== 枚举转文本 ==========

    private static string GetStatusText(NcrStatus status) => DisplayHelper.GetNcrStatusText(status);

    private static string GetStatusCss(NcrStatus status) => status switch
    {
        NcrStatus.Processing => "ncr-status-processing",
        NcrStatus.Closed => "ncr-status-closed",
        _ => ""
    };

    private static string GetPipeCategoryText(PipeCategory category) => DisplayHelper.GetPipeCategoryText(category);

    private static string GetDisposalMethodText(DisposalMethod? method) => method switch
    {
        DisposalMethod.Rework => "返整",
        DisposalMethod.WarehouseEntry => "入库",
        DisposalMethod.Scrap => "报废",
        _ => ""
    };

    private static string GetSeverityText(SeverityLevel? severity) => severity switch
    {
        SeverityLevel.Critical => "严重",
        SeverityLevel.General => "一般",
        _ => ""
    };

    private static string GetResponsibilityCategoryText(ResponsibilityCategory? category) => category switch
    {
        ResponsibilityCategory.ProductionInternal => "生产-厂内",
        ResponsibilityCategory.ProductionOutsource => "生产-外协",
        ResponsibilityCategory.MaterialTubeBlank => "原料-荒管",
        ResponsibilityCategory.MaterialPurchased => "原料-外购成品",
        ResponsibilityCategory.MaterialSurplus => "原料-余库料",
        _ => ""
    };

    private static string GetVerifyResultText(VerifyResult? result) => result switch
    {
        VerifyResult.Passed => "通过",
        VerifyResult.NeedsRectification => "需整改",
        VerifyResult.NotApplicable => "不适用",
        _ => ""
    };

    private static string GetYesNoText(bool value) => value ? "是" : "否";

    private static string FormatDate(DateTime? dt) => dt?.ToString("yyyy-MM-dd") ?? "";

    private static string FormatDateTime(DateTimeOffset dto) => dto.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
}
