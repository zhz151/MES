using MES.Core.DTOs.Scheduling;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace MES.Blazor.Shared;

public partial class WorkOrderLoadOverview : ComponentBase
{
    [Inject] private Services.ProductionOverviewService OverviewService { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    [Parameter] public string Title { get; set; } = "负载总览";

    private ProductionOverviewDto? _data;
    private bool _loading;
    private string? _errorMessage;
    private DateTime _lastRefresh;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _loading = true;
        _errorMessage = null;
        try
        {
            var response = await OverviewService.GetOverviewAsync();
            if (response.Success && response.Data != null)
            {
                _data = response.Data;
                _lastRefresh = response.Data.GeneratedTime;
            }
            else
            {
                _errorMessage = response.Message ?? "获取数据失败";
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"加载失败: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task PrintTable()
    {
        var html = await JS.InvokeAsync<string>("getTableHtml", "#workorder-overview-table");
        if (!string.IsNullOrEmpty(html))
            await JS.InvokeVoidAsync("printRawHtml", html, Title);
        else
            Console.Error.WriteLine("WorkOrderLoadOverview: 未找到可打印的表格");
    }

    private static string FormatTons(decimal? tons)
    {
        return tons.HasValue ? $"{tons.Value:0}吨" : "-";
    }

    private static string FormatDays(int? days)
    {
        return days.HasValue ? $"{days.Value}天" : "-";
    }

    private static string FormatDate(DateTime? date)
    {
        return date.HasValue ? date.Value.ToString("M/d") : "-";
    }

    private static string FormatBucketTons(decimal tons)
    {
        return tons > 0 ? $"{tons:0}" : "-";
    }
}
