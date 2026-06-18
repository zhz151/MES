using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MudBlazor;

namespace MES.Blazor.Pages.Equipment;

public enum ScanStep { Scan, SelectAction }

public partial class EquipmentScan : IDisposable
{
    [Inject] private ScanService ScanService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    // 步骤
    private ScanStep _step = ScanStep.Scan;

    // 扫码
    private bool _isCameraStarted;
    private string _manualInput = string.Empty;
    private string? _resolveError;
    private bool _isLoading;

    // 设备信息
    private ScanEquipmentResolveResultDto? _equipment;

    private async Task StartCamera()
    {
        _isCameraStarted = true;
        _resolveError = null;
        StateHasChanged();

        try
        {
            var result = await JS.InvokeAsync<ScanResult>("window.startScanner",
                "equipment-scan-video", "equipment-scan-canvas",
                DotNetObjectReference.Create(this), "OnScanResult");
            if (result?.success == false)
            {
                _resolveError = result.error ?? "启动摄像头失败";
                _isCameraStarted = false;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            _resolveError = $"启动摄像头失败: {ex.Message}";
            _isCameraStarted = false;
            StateHasChanged();
        }
    }

    [JSInvokable]
    public async Task OnScanResult(string code)
    {
        await StopCamera();
        await ResolveCode(code);
    }

    private async Task StopCamera()
    {
        try
        {
            await JS.InvokeVoidAsync("window.stopScanner");
        }
        catch { }
        _isCameraStarted = false;
    }

    private async Task ResolveManualInput()
    {
        if (string.IsNullOrWhiteSpace(_manualInput)) return;
        await ResolveCode(_manualInput.Trim());
    }

    private async Task ResolveCode(string code)
    {
        _resolveError = null;
        _equipment = null;
        _isLoading = true;
        StateHasChanged();

        var response = await ScanService.ResolveEquipmentAsync(code);
        if (!response.Success || response.Data == null)
        {
            _resolveError = response.Message ?? "未找到该设备，请确认设备编码正确";
            _isLoading = false;
            StateHasChanged();
            return;
        }

        _equipment = response.Data;
        _isLoading = false;
        _step = ScanStep.SelectAction;
        StateHasChanged();
    }

    private void GoToRepair()
    {
        if (_equipment == null) return;
        Navigation.NavigateTo($"/equipment-repair?code={Uri.EscapeDataString(_equipment.EquipmentCode)}");
    }

    private void GoToExecute()
    {
        if (_equipment == null) return;
        Navigation.NavigateTo($"/repair-execute?code={Uri.EscapeDataString(_equipment.EquipmentCode)}");
    }

    private void GoToOrderList()
    {
        Navigation.NavigateTo("/repair-orders");
    }

    private void Rescan()
    {
        _step = ScanStep.Scan;
        _equipment = null;
        _resolveError = null;
        _manualInput = string.Empty;
    }

    private void GoBack()
    {
        Navigation.NavigateTo("/");
    }

    public void Dispose()
    {
        try
        {
            _ = JS.InvokeVoidAsync("window.stopScanner");
        }
        catch { }
    }
}
