using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MudBlazor;

namespace MES.Blazor.Pages.Equipment;

public enum RepairStep { Scan, Form, Success }

public partial class EquipmentRepair : IDisposable
{
    [Inject] private ScanService ScanService { get; set; } = null!;
    [Inject] private RepairOrderService RepairOrderService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    // 步骤
    private RepairStep _step = RepairStep.Scan;

    // 扫码
    private bool _isCameraStarted;
    private string _manualInput = string.Empty;
    private string? _resolveError;

    // 设备信息
    private ScanEquipmentResolveResultDto? _equipment;

    // 报修单
    private string _reportPerson = string.Empty;
    private string? _selectedFaultType;
    private string _faultDescription = string.Empty;
    private string? _repairOrderNo;
    private string? _submitError;
    private bool _submitting;

    // 故障类型列表
    private static readonly string[] _faultTypes =
    [
        "机械故障", "电气故障", "液压故障",
        "气动故障", "仪表故障", "其它"
    ];

    protected override async Task OnInitializedAsync()
    {
        // 从 localStorage 自动读取当前登录用户名
        try
        {
            var name = await JS.InvokeAsync<string>("eval", "(function(){ try { return localStorage.getItem('userFullName') || localStorage.getItem('userName') || ''; } catch(e){ return ''; } })()");
            _reportPerson = name ?? string.Empty;
        }
        catch
        {
            _reportPerson = string.Empty;
        }

        // 支持从查询参数 ?code=xxx 自动解析设备
        var uri = new Uri(Navigation.Uri);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var code = query["code"];
        if (!string.IsNullOrWhiteSpace(code))
        {
            await ResolveCode(code);
        }
    }

    private async Task StartCamera()
    {
        _isCameraStarted = true;
        _resolveError = null;
        StateHasChanged();

        try
        {
            var result = await JS.InvokeAsync<ScanResult>("window.startScanner",
                "repair-scan-video", "repair-scan-canvas",
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

        // 调用后端解析
        var response = await ScanService.ResolveEquipmentAsync(code);
        if (!response.Success || response.Data == null)
        {
            _resolveError = response.Message ?? "未找到该设备，请确认设备编码正确";
            StateHasChanged();
            return;
        }

        _equipment = response.Data;
        _selectedFaultType = null;
        _faultDescription = string.Empty;
        _submitError = null;
        _step = RepairStep.Form;
        StateHasChanged();
    }

    private void SelectFaultType(string faultType)
    {
        _selectedFaultType = faultType;
    }

    private async Task SubmitRepair()
    {
        if (_equipment == null || string.IsNullOrEmpty(_selectedFaultType)) return;

        _submitting = true;
        _submitError = null;
        StateHasChanged();

        try
        {
            var request = new CreateRepairOrderRequest
            {
                EquipmentId = _equipment.EquipmentId,
                FaultType = _selectedFaultType,
                FaultDescription = string.IsNullOrWhiteSpace(_faultDescription)
                    ? _selectedFaultType
                    : $"{_selectedFaultType}：{_faultDescription}",
                ReportPerson = _reportPerson,
                ReportTime = DateTime.Now,
                Priority = nameof(MES.Core.Enums.RepairPriority.Normal),
            };

            var response = await RepairOrderService.CreateAsync(request);
            if (response.Success && response.Data != null)
            {
                _repairOrderNo = response.Data.RepairOrderNo;
                _step = RepairStep.Success;
            }
            else
            {
                _submitError = response.Message ?? "提交失败，请重试";
            }
        }
        catch (Exception ex)
        {
            _submitError = $"提交失败: {ex.Message}";
        }
        finally
        {
            _submitting = false;
            StateHasChanged();
        }
    }

    private void Rescan()
    {
        _step = RepairStep.Scan;
        _equipment = null;
        _resolveError = null;
        _manualInput = string.Empty;
    }

    private void ResetAndScanAgain()
    {
        _repairOrderNo = null;
        Rescan();
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

/// <summary>
/// 扫码结果（匹配 scanner.js 返回值）
/// </summary>
public class ScanResult
{
    public bool success { get; set; }
    public string? data { get; set; }
    public string? error { get; set; }
}
