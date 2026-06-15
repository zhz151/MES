using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MES.Blazor.Services;
using MES.Core.DTOs;
using MudBlazor;

namespace MES.Blazor.Pages.Equipment;

public enum RepairExecStep { Scan, Review, Success }

public partial class RepairExecute : IDisposable
{
    [Inject] private ScanService ScanService { get; set; } = null!;
    [Inject] private RepairOrderService RepairOrderService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    // 步骤
    private RepairExecStep _step = RepairExecStep.Scan;

    // 扫码
    private bool _isCameraStarted;
    private string _manualInput = string.Empty;
    private string? _resolveError;

    // 设备信息
    private ScanEquipmentResolveResultDto? _equipment;

    // 维修工单
    private List<RepairOrderListDto> _pendingOrders = new();
    private RepairOrderListDto? _activeOrder;
    private string? _errorMessage;
    private string? _submitError;
    private string? _successMessage;
    private bool _isProcessing;

    // 维修人
    private string _repairPerson = string.Empty;

    // 完成维修表单
    private string? _repairCategory;
    private readonly List<string> _repairCategories = new() { "厂内维修", "外协维修", "换模" };
    private string _repairContent = string.Empty;
    private string _sparePartUsed = string.Empty;
    private string _otherPersonInput = string.Empty;
    private List<string> _otherRepairPersons = new();

    protected override async Task OnInitializedAsync()
    {
        // 从 localStorage 自动读取当前登录用户名
        try
        {
            var name = await JS.InvokeAsync<string>("eval", "(function(){ try { return localStorage.getItem('userFullName') || localStorage.getItem('userName') || ''; } catch(e){ return ''; } })()");
            _repairPerson = name ?? string.Empty;
        }
        catch
        {
            _repairPerson = string.Empty;
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
                "repair-exec-scan-video", "repair-exec-scan-canvas",
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

        var response = await ScanService.ResolveEquipmentAsync(code);
        if (!response.Success || response.Data == null)
        {
            _resolveError = response.Message ?? "未找到该设备，请确认设备编码正确";
            StateHasChanged();
            return;
        }

        _equipment = response.Data;
        await LoadPendingOrders();
    }

    private async Task LoadPendingOrders()
    {
        if (_equipment == null) return;

        _errorMessage = null;
        _pendingOrders.Clear();
        _activeOrder = null;

        var response = await RepairOrderService.GetPendingByEquipmentAsync(_equipment.EquipmentId);
        if (!response.Success || response.Data == null)
        {
            _errorMessage = response.Message ?? "查询待维修工单失败";
            _step = RepairExecStep.Review;
            StateHasChanged();
            return;
        }

        _pendingOrders = response.Data;

        // 如果有维修中的工单，自动选中第一个作为活跃工单
        var inProgress = _pendingOrders.FirstOrDefault(o => o.RepairStatus == "InProgress");
        if (inProgress != null)
        {
            _activeOrder = inProgress;
        }

        _step = RepairExecStep.Review;
        StateHasChanged();
    }

    private void SelectActiveOrder(RepairOrderListDto order)
    {
        _activeOrder = order;
        _repairCategory = null;
        _repairContent = string.Empty;
        _sparePartUsed = string.Empty;
        _otherRepairPersons.Clear();
        _submitError = null;
    }

    private async Task StartRepair(RepairOrderListDto order)
    {
        if (string.IsNullOrWhiteSpace(_repairPerson))
        {
            Snackbar.Add("无法获取当前用户信息，请重新登录", Severity.Error);
            return;
        }

        _isProcessing = true;
        StateHasChanged();

        try
        {
            var request = new StartRepairRequest
            {
                RepairPerson = _repairPerson
            };

            var response = await RepairOrderService.StartRepairAsync(order.Id, request);
            if (response.Success && response.Data != null)
            {
                _successMessage = "维修已开始";
                _step = RepairExecStep.Success;
            }
            else
            {
                Snackbar.Add(response.Message ?? "开始维修失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"开始维修失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isProcessing = false;
            StateHasChanged();
        }
    }

    private void AddOtherPerson()
    {
        var trimmed = _otherPersonInput.Trim();
        if (!string.IsNullOrEmpty(trimmed) && !_otherRepairPersons.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            _otherRepairPersons.Add(trimmed);
        }
        _otherPersonInput = string.Empty;
    }

    private void RemoveOtherPerson(string person)
    {
        _otherRepairPersons.Remove(person);
    }

    private void ToggleCategory(string category)
    {
        _repairCategory = _repairCategory == category ? null : category;
    }

    private async Task SubmitComplete()
    {
        if (_activeOrder == null || string.IsNullOrWhiteSpace(_repairContent)) return;

        _isProcessing = true;
        _submitError = null;
        StateHasChanged();

        try
        {
            var request = new CompleteRepairRequest
            {
                RepairCategory = _repairCategory,
                RepairContent = _repairContent,
                SparePartUsed = string.IsNullOrWhiteSpace(_sparePartUsed) ? null : _sparePartUsed,
                OtherRepairPersons = _otherRepairPersons.Count > 0 ? _otherRepairPersons : null
            };

            var response = await RepairOrderService.CompleteRepairAsync(_activeOrder.Id, request);
            if (response.Success && response.Data != null)
            {
                _successMessage = "维修已完成";
                _step = RepairExecStep.Success;
            }
            else
            {
                _submitError = response.Message ?? "完成维修失败，请重试";
            }
        }
        catch (Exception ex)
        {
            _submitError = $"完成维修失败: {ex.Message}";
        }
        finally
        {
            _isProcessing = false;
            StateHasChanged();
        }
    }

    private void Rescan()
    {
        _step = RepairExecStep.Scan;
        _equipment = null;
        _resolveError = null;
        _manualInput = string.Empty;
        _pendingOrders.Clear();
        _activeOrder = null;
        _errorMessage = null;
    }

    private void ResetAndScanAgain()
    {
        _activeOrder = null;
        _successMessage = null;
        Rescan();
    }

    private void GoBack()
    {
        Navigation.NavigateTo("/");
    }

    private string GetOrderBorderStyle(string? status)
    {
        var color = status == "InProgress" ? "#ff9800" : "#9e9e9e";
        return $"border-left: 3px solid {color};";
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
