using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MES.Blazor.Helpers;
using MES.Blazor.Services;
using MES.Blazor.Shared;
using MES.Core.Constants;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Quality;
using MES.Core.Enums;
using MES.Core.Interfaces.Quality;
using MudBlazor;

namespace MES.Blazor.Pages.Tools;

/// <summary>
/// 扫码链质量报工页 — 「巡检」「不合格反馈」共用（Kind=patrol / feedback）。
///
/// 与扫码报工的差异（用户 2026-09-11 拍板）：<b>不扫工位码、不扫员工码</b>，
/// 直接扫批次码 → 选「工序 + 工段」；操作人 = 当前登录账号（服务端按登录名反查员工姓名）。
///
/// 巡检闭环规则：同「批次 + 工序组 + 工段」若存在<b>待整改</b>（涉及整改且未闭环）的巡检单，
/// 第二次扫码直接呈现第一次的内容，只补整改验证与照片；否则视为新建。
/// </summary>
public partial class ScanQuality : IDisposable
{
    /// <summary>巡检</summary>
    public const string KindPatrol = "patrol";

    /// <summary>不合格反馈</summary>
    public const string KindFeedback = "feedback";

    private const string StaleClientHint = "页面脚本未加载（页面版本过旧），请关闭本页重新打开，或按 Ctrl+Shift+R 强制刷新后再试";

    [Inject] private ScanService ScanService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    /// <summary>路由参数：patrol=巡检 / feedback=不合格反馈</summary>
    [Parameter] public string Kind { get; set; } = KindPatrol;

    private enum PageStep { ScanBatch, PickPosition, FillForm, Done }

    private PageStep _step = PageStep.ScanBatch;

    private bool IsPatrol => !string.Equals(Kind, KindFeedback, StringComparison.OrdinalIgnoreCase);
    private string PageTitle => IsPatrol ? "巡检扫码" : "不合格反馈扫码";

    // ========== 扫码 ==========
    private DotNetObjectReference<ScanQuality>? _dotNetRef;
    private bool _isCameraStarted;
    private string? _manualInput;

    // ========== 通用上下文 ==========
    /// <summary>当前登录人真实姓名（服务端解析，只读展示并作为 巡检人/反馈人/整改人）</summary>
    private string _operatorName = "";
    private string? _batchNo;
    private ScanBatchResolveResultDto? _batchGroupResult;
    private ProcessGroupOption? _selectedGroup;
    private string? _selectedSection;
    private string? _errorMessage;
    private bool _isSubmitting;

    // ========== 巡检状态 ==========
    private List<InspectionPatrolDto> _locatedPatrols = new();

    /// <summary>待整改目标（非空 = 整改回填模式；空 = 新建模式）</summary>
    private InspectionPatrolDto? _rectifyTarget;
    private bool _showLocatedPanel;

    private string _patrolDate = DateTime.Today.ToString("yyyy-MM-dd");
    private string? _productionUnit;
    private string? _equipmentName;
    private string? _productionOperator;
    private List<PatrolItemRow> _patrolItems = new();
    private bool _needRectification;
    private string? _rectificationDescription;
    private string? _verificationResult;
    private bool _isClosed;
    private InspectionPatrolPositionOptionsDto _positionOptions = new();

    private readonly List<ScanPhotoItem> _patrolPhotos = new();
    private readonly List<ScanPhotoItem> _rectPhotos = new();

    // ========== 不合格反馈状态 ==========
    private string _reportDate = DateTime.Today.ToString("yyyy-MM-dd");
    private int? _incomingQuantity;
    private decimal? _incomingWeight;
    private int? _defectQuantity;
    private int? _defectWeight;
    private string? _problemDescription;
    private readonly List<ScanPhotoItem> _feedbackPhotos = new();

    /// <summary>反馈来源类型（生产工段/过程检验/成品检验），决定位置信息取「工序+工段」还是「工序+检验项目」</summary>
    private string _sourceType = nameof(NonconformingFeedbackSourceType.ProductionSection);

    /// <summary>检验项目（仅「成品检验」来源必填）</summary>
    private InspectionItem? _inspectionItem;

    /// <summary>是否「成品检验」来源</summary>
    private bool IsFinalInspectionSource
        => _sourceType == nameof(NonconformingFeedbackSourceType.FinalInspection);

    /// <summary>
    /// 成品检验档「成检类型」推导（只读展示）：附加成检工序=终检；
    /// 批次存在附加成检时其余工序=预检；批次无附加成检则全部=终检。
    /// </summary>
    private string FeedbackStageText
        => _selectedGroup?.ProcessName == ProcessKeys.AdditionalFinalInspection
            ? "终检"
            : (HasAdditionalFinalInspection ? "预检" : "终检");

    /// <summary>该批次是否含「附加成检」工序组</summary>
    private bool HasAdditionalFinalInspection
        => _batchGroupResult?.ProcessGroups.Any(g => g.ProcessName == ProcessKeys.AdditionalFinalInspection) == true;

    // ========== 完成页 ==========
    private string? _doneSummary;

    /// <summary>巡检照片张数上限（按类型，与后端 InspectionPatrolPhotoTypes.MaxPerType 一致）</summary>
    private static int MaxPhotoPerType => InspectionPatrolPhotoTypes.MaxPerType;

    /// <summary>不合格反馈「问题照片」张数上限（按记录，与后端 NonconformingFeedbackService 一致）</summary>
    private static int MaxPhotoPerRecord => QualityPhotoLimits.PerRecord;

    protected override void OnInitialized()
    {
        _dotNetRef = DotNetObjectReference.Create(this);
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_operatorName.Length == 0)
        {
            var resp = await ScanService.GetCurrentOperatorNameAsync();
            _operatorName = resp.Success && !string.IsNullOrWhiteSpace(resp.Data) ? resp.Data! : "";
        }
    }

    // ========== 摄像头 ==========

    private async Task StartCamera()
    {
        try
        {
            _isCameraStarted = true;
            StateHasChanged();
            await Task.Delay(100);

            var result = await JS.InvokeAsync<ScanQualityResult>("window.startScanner",
                "quality-scan-video", "quality-scan-canvas", _dotNetRef, "OnQrCodeDetected");

            if (result is { Success: false })
            {
                Snackbar.Add(result.Error ?? "启动摄像头失败", Severity.Error);
                _isCameraStarted = false;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"摄像头错误：{ex.Message}", Severity.Error);
            _isCameraStarted = false;
            StateHasChanged();
        }
    }

    private async Task StopCamera()
    {
        try { await JS.InvokeVoidAsync("window.stopScanner"); }
        catch { }
        _isCameraStarted = false;
    }

    [JSInvokable]
    public async Task OnQrCodeDetected(string data)
    {
        try
        {
            if (_step == PageStep.ScanBatch)
                await ResolveBatchAsync(data);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"解析失败：{ex.Message}", Severity.Error);
            StateHasChanged();
        }
    }

    // ========== 步骤1：扫批次码 ==========

    private async Task ResolveManualInputAsync()
    {
        if (string.IsNullOrWhiteSpace(_manualInput)) return;
        await ResolveBatchAsync(_manualInput.Trim());
    }

    private async Task ResolveBatchAsync(string rawCode)
    {
        // 兼容「批次号|工序组ID」形式（与扫码报工一致）
        var parts = rawCode.Split('|');
        var batchNo = parts[0].Trim();
        if (string.IsNullOrWhiteSpace(batchNo)) return;

        var response = await ScanService.GetBatchProcessGroupsAsync(batchNo);
        if (!response.Success || response.Data == null)
        {
            Snackbar.Add(response.Message ?? "未识别到该生产编号", Severity.Warning);
            return;
        }

        _batchGroupResult = response.Data;
        _batchNo = response.Data.BatchNo;
        _selectedGroup = null;
        _selectedSection = null;
        _sourceType = nameof(NonconformingFeedbackSourceType.ProductionSection);
        _inspectionItem = null;
        _manualInput = null;

        // 巡检：预加载在产单位候选（委外单位档案驱动）
        if (IsPatrol)
        {
            var optResp = await ScanService.GetPatrolPositionOptionsAsync();
            _positionOptions = optResp.Success && optResp.Data != null ? optResp.Data : new();
        }

        await StopCamera();
        _step = PageStep.PickPosition;
        Snackbar.Add($"已识别批次：{_batchNo}", Severity.Success);
        StateHasChanged();
    }

    // ========== 步骤2：选工序 + 工段 ==========

    private void OnGroupChanged(ProcessGroupOption? group)
    {
        _selectedGroup = group;
        _selectedSection = null;
        StateHasChanged();
    }

    /// <summary>切换来源类型：离开「成品检验」清空检验项目，进入时清空工段（位置字段随来源互斥）</summary>
    private void OnSourceTypeChanged(string? value)
    {
        _sourceType = value ?? nameof(NonconformingFeedbackSourceType.ProductionSection);
        if (IsFinalInspectionSource) _selectedSection = null;
        else _inspectionItem = null;
        StateHasChanged();
    }

    private async Task ConfirmPositionAsync()
    {
        var missing = _selectedGroup == null
            || (IsFinalInspectionSource ? _inspectionItem == null : string.IsNullOrWhiteSpace(_selectedSection));
        if (missing)
        {
            Snackbar.Add(IsFinalInspectionSource ? "请选择工序与检验项目" : "请选择工序与工段", Severity.Warning);
            return;
        }

        if (IsPatrol)
            await LoadPatrolContextAsync();
        else
            InitFeedbackForm();

        _step = PageStep.FillForm;
        StateHasChanged();
    }

    // ========== 巡检：定位待整改单 / 新建 ==========

    private async Task LoadPatrolContextAsync()
    {
        _locatedPatrols = new List<InspectionPatrolDto>();
        _rectifyTarget = null;
        _showLocatedPanel = false;

        var resp = await ScanService.GetPatrolsByKeyAsync(_batchNo!, _selectedGroup!.Id, _selectedSection!);
        if (resp.Success && resp.Data != null)
            _locatedPatrols = resp.Data;

        // 待整改（涉及整改且未闭环）→ 呈现第一次内容供补充；否则新建
        var pending = _locatedPatrols.FirstOrDefault(p => p.NeedRectification && !p.IsClosed);
        if (pending != null)
            await LoadRectifyTargetAsync(pending.Id);
        else
            InitNewPatrolForm();
    }

    /// <summary>载入待整改单详情（含第一次的巡检明细与照片）</summary>
    private async Task LoadRectifyTargetAsync(int id)
    {
        var resp = await ScanService.GetPatrolAsync(id);
        if (!resp.Success || resp.Data == null)
        {
            Snackbar.Add(resp.Message ?? "载入巡检单失败", Severity.Error);
            InitNewPatrolForm();
            return;
        }

        _rectifyTarget = resp.Data;
        _locatedPatrols = _locatedPatrols
            .Select(p => p.Id == id ? resp.Data! : p).ToList();

        _rectPhotos.Clear();
        _patrolPhotos.Clear();
        foreach (var att in resp.Data.Attachments)
        {
            var bytes = await ScanService.GetPatrolAttachmentBytesAsync(id, att.Id);
            if (bytes == null) continue;
            var base64 = Convert.ToBase64String(bytes);
            string? url;
            try { url = await JS.InvokeAsync<string?>("MES.base64ToObjectUrl", base64, att.ContentType); }
            catch (JSException) { Snackbar.Add(StaleClientHint, Severity.Warning); break; }
            if (string.IsNullOrEmpty(url)) continue;

            var item = new ScanPhotoItem
            {
                AttachmentId = att.Id,
                FileName = att.FileName,
                ContentType = att.ContentType,
                Base64 = base64,
                PreviewUrl = url
            };
            if (att.PhotoType == InspectionPatrolPhotoTypes.Rectification)
                _rectPhotos.Add(item);
            else
                _patrolPhotos.Add(item);
        }

        // 回填本单已有的整改验证内容
        _verificationResult = resp.Data.VerificationResult;
        _isClosed = resp.Data.IsClosed;
        _errorMessage = null;
    }

    private void InitNewPatrolForm()
    {
        _rectifyTarget = null;
        _patrolDate = DateTime.Today.ToString("yyyy-MM-dd");
        _productionUnit = null;
        _equipmentName = null;
        _productionOperator = null;
        _needRectification = false;
        _rectificationDescription = null;
        _verificationResult = null;
        _isClosed = false;
        _patrolItems = new List<PatrolItemRow> { new() };
        _patrolPhotos.Clear();
        _rectPhotos.Clear();
        _errorMessage = null;
    }

    /// <summary>改选处理哪一张单：可整改的进入整改回填，其余一律按新建</summary>
    private async Task SelectRectifyTargetAsync(InspectionPatrolDto patrol)
    {
        _showLocatedPanel = false;
        if (patrol.NeedRectification && !patrol.IsClosed)
        {
            await LoadRectifyTargetAsync(patrol.Id);
        }
        else
        {
            InitNewPatrolForm();
            Snackbar.Add("该巡检单无需整改，已切换为新建", Severity.Info);
        }
        StateHasChanged();
    }

    private void SwitchToNewPatrol()
    {
        _showLocatedPanel = false;
        InitNewPatrolForm();
        StateHasChanged();
    }

    private void AddPatrolItemRow() => _patrolItems.Add(new PatrolItemRow());

    private void RemovePatrolItemRow(PatrolItemRow row) => _patrolItems.Remove(row);

    // ========== 不合格反馈：初始化 ==========

    private void InitFeedbackForm()
    {
        _reportDate = DateTime.Today.ToString("yyyy-MM-dd");
        _incomingQuantity = null;
        _incomingWeight = null;
        _defectQuantity = null;
        _defectWeight = null;
        _problemDescription = null;
        _sourceType = nameof(NonconformingFeedbackSourceType.ProductionSection);
        _inspectionItem = null;
        _feedbackPhotos.Clear();
        _errorMessage = null;
    }

    // ========== 提交 ==========

    private async Task SubmitAsync()
    {
        _errorMessage = null;
        if (!Validate()) return;

        _isSubmitting = true;
        StateHasChanged();
        try
        {
            if (IsPatrol)
                await SubmitPatrolAsync();
            else
                await SubmitFeedbackAsync();
        }
        finally
        {
            _isSubmitting = false;
            StateHasChanged();
        }
    }

    private bool Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(_batchNo)) errors.Add("生产编号不能为空");
        if (_selectedGroup == null) errors.Add("工序名称不能为空");
        // 成品检验来源无工段，改要求检验项目
        if (IsPatrol || !IsFinalInspectionSource)
        {
            if (string.IsNullOrWhiteSpace(_selectedSection)) errors.Add("工段名称不能为空");
        }
        else if (_inspectionItem == null)
        {
            errors.Add("检验项目不能为空");
        }

        if (IsPatrol)
        {
            if (!DateTime.TryParse(_patrolDate, out _)) errors.Add("巡检日期格式无效（请用 yyyy-MM-dd）");
            if (_rectifyTarget == null
                && !_patrolItems.Any(i => !string.IsNullOrWhiteSpace(i.ItemName)))
                errors.Add("请至少填写一条巡检明细（巡检项不能为空）");
            if (_rectifyTarget == null && _needRectification
                && string.IsNullOrWhiteSpace(_rectificationDescription))
                errors.Add("涉及整改时请填写整改内容");
        }
        else
        {
            if (!DateTime.TryParse(_reportDate, out _)) errors.Add("反馈日期格式无效（请用 yyyy-MM-dd）");
            if (_defectQuantity == null) errors.Add("不合格支数不能为空");
        }

        if (errors.Count == 0) return true;

        _errorMessage = string.Join("；", errors);
        Snackbar.Add(_errorMessage, Severity.Error);
        return false;
    }

    private async Task SubmitPatrolAsync()
    {
        if (_rectifyTarget != null)
        {
            var resp = await ScanService.RectifyPatrolAsync(_rectifyTarget.Id, new InspectionPatrolRectifyRequest
            {
                VerificationResult = string.IsNullOrWhiteSpace(_verificationResult) ? null : _verificationResult.Trim(),
                IsClosed = _isClosed
            });
            if (!resp.Success || resp.Data == null)
            {
                _errorMessage = resp.Message ?? "整改回填失败";
                Snackbar.Add($"提交失败：{_errorMessage}", Severity.Error);
                return;
            }

            var failed = await UploadPatrolPhotosAsync(resp.Data.Id);
            _doneSummary = $"巡检单 XJ{resp.Data.Id:D4} 整改回填成功"
                           + (failed > 0 ? $"（{failed} 张照片上传失败）" : "");
        }
        else
        {
            var date = DateTime.Parse(_patrolDate);
            var items = _patrolItems
                .Where(i => !string.IsNullOrWhiteSpace(i.ItemName))
                .Select(i => new InspectionPatrolItemRequest
                {
                    ItemName = i.ItemName.Trim(),
                    Result = string.IsNullOrWhiteSpace(i.Result) ? null : i.Result.Trim(),
                    Remark = string.IsNullOrWhiteSpace(i.Remark) ? null : i.Remark.Trim()
                }).ToList();

            var resp = await ScanService.CreatePatrolAsync(new CreateInspectionPatrolRequest
            {
                PatrolDate = date,
                Inspector = _operatorName,
                BatchNo = _batchNo!,
                ProcessGroupId = _selectedGroup!.Id,
                ProcessName = _selectedGroup.ProcessName,
                ManufacturingSpec = _selectedGroup.ManufacturingSpec,
                SectionName = _selectedSection!,
                ProductionUnit = string.IsNullOrWhiteSpace(_productionUnit) ? null : _productionUnit.Trim(),
                EquipmentName = string.IsNullOrWhiteSpace(_equipmentName) ? null : _equipmentName.Trim(),
                ProductionOperator = string.IsNullOrWhiteSpace(_productionOperator) ? null : _productionOperator.Trim(),
                Items = items,
                NeedRectification = _needRectification,
                RectificationDescription = string.IsNullOrWhiteSpace(_rectificationDescription) ? null : _rectificationDescription.Trim(),
                VerificationResult = string.IsNullOrWhiteSpace(_verificationResult) ? null : _verificationResult.Trim(),
                IsClosed = _isClosed
            });
            if (!resp.Success || resp.Data == null)
            {
                _errorMessage = resp.Message ?? "提交失败";
                Snackbar.Add($"提交失败：{_errorMessage}", Severity.Error);
                return;
            }

            var failed = await UploadPatrolPhotosAsync(resp.Data.Id);
            _doneSummary = $"巡检单 XJ{resp.Data.Id:D4} 已提交"
                           + (failed > 0 ? $"（{failed} 张照片上传失败）" : "");
        }

        await FinishAsync();
    }

    /// <summary>上传巡检相关照片：新建传「巡检照片」，整改模式补齐「整改验证照片」</summary>
    private async Task<int> UploadPatrolPhotosAsync(int patrolId)
    {
        var pending = new List<(ScanPhotoItem Photo, string Type)>();
        if (_rectifyTarget == null)
            pending.AddRange(_patrolPhotos.Where(p => !p.AttachmentId.HasValue)
                .Select(p => (p, InspectionPatrolPhotoTypes.Patrol)));
        pending.AddRange(_rectPhotos.Where(p => !p.AttachmentId.HasValue)
            .Select(p => (p, InspectionPatrolPhotoTypes.Rectification)));

        var failed = 0;
        foreach (var (photo, type) in pending)
        {
            var bytes = Convert.FromBase64String(photo.Base64);
            var resp = await ScanService.UploadPatrolAttachmentAsync(patrolId, type, bytes, photo.FileName, photo.ContentType);
            if (!resp.Success) failed++;
        }
        return failed;
    }

    private async Task SubmitFeedbackAsync()
    {
        var date = DateTime.Parse(_reportDate);
        var resp = await ScanService.CreateFeedbackAsync(new CreateNonconformingFeedbackRequest
        {
            ReportDate = date,
            Reporter = _operatorName,
            BatchNo = _batchNo!,
            ProcessGroupId = _selectedGroup!.Id,
            ProcessName = _selectedGroup.ProcessName,
            ManufacturingSpec = _selectedGroup.ManufacturingSpec,
            SourceType = _sourceType,
            SectionName = IsFinalInspectionSource ? null : _selectedSection,
            InspectionItem = IsFinalInspectionSource ? _inspectionItem : null,
            IncomingQuantity = _incomingQuantity,
            IncomingWeight = _incomingWeight,
            DefectQuantity = _defectQuantity,
            DefectWeight = _defectWeight,
            ProblemDescription = string.IsNullOrWhiteSpace(_problemDescription) ? null : _problemDescription.Trim()
        });
        if (!resp.Success || resp.Data == null)
        {
            _errorMessage = resp.Message ?? "提交失败";
            Snackbar.Add($"提交失败：{_errorMessage}", Severity.Error);
            return;
        }

        var failed = 0;
        foreach (var photo in _feedbackPhotos.Where(p => !p.AttachmentId.HasValue))
        {
            var bytes = Convert.FromBase64String(photo.Base64);
            var up = await ScanService.UploadFeedbackAttachmentAsync(resp.Data.Id, bytes, photo.FileName, photo.ContentType);
            if (!up.Success) failed++;
        }

        _doneSummary = $"不合格反馈单 FB{resp.Data.Id:D4} 已提交"
                       + (failed > 0 ? $"（{failed} 张照片上传失败）" : "");
        await FinishAsync();
    }

    private async Task FinishAsync()
    {
        await StopCamera();
        Snackbar.Add(_doneSummary ?? "提交成功", Severity.Success);
        _step = PageStep.Done;
        StateHasChanged();
    }

    // ========== 照片删除（巡检既有附件） ==========

    /// <summary>删除已落库的巡检照片（第二次扫码呈现第一次照片时允许删除）</summary>
    private async Task<bool> DeleteExistingPatrolPhotoAsync(ScanPhotoItem photo)
    {
        if (_rectifyTarget == null || !photo.AttachmentId.HasValue) return false;
        var resp = await ScanService.DeletePatrolAttachmentAsync(_rectifyTarget.Id, photo.AttachmentId.Value);
        if (!resp.Success)
        {
            Snackbar.Add(resp.Message ?? "删除照片失败", Severity.Error);
            return false;
        }
        Snackbar.Add("照片已删除", Severity.Success);
        return true;
    }

    // ========== 导航 ==========

    private void Rescan()
    {
        _step = PageStep.ScanBatch;
        _batchNo = null;
        _batchGroupResult = null;
        _selectedGroup = null;
        _selectedSection = null;
        _locatedPatrols = new List<InspectionPatrolDto>();
        _rectifyTarget = null;
        _showLocatedPanel = false;
        _patrolItems = new List<PatrolItemRow>();
        _patrolPhotos.Clear();
        _rectPhotos.Clear();
        _feedbackPhotos.Clear();
        _doneSummary = null;
        _errorMessage = null;
        _dotNetRef ??= DotNetObjectReference.Create(this);
    }

    private void GoBack() => Navigation.NavigateTo("/");

    public void Dispose()
    {
        try { _ = JS.InvokeVoidAsync("window.stopScanner"); }
        catch { }
        try { _ = JS.InvokeVoidAsync("MES.revokeAllObjectUrls"); }
        catch { }
        _dotNetRef?.Dispose();
    }

    // ========== 内部类型 ==========

    /// <summary>巡检明细行（新建时录入）</summary>
    public class PatrolItemRow
    {
        public string ItemName { get; set; } = "";
        public string? Result { get; set; }
        public string? Remark { get; set; }
    }

    /// <summary>JS 摄像头启动结果</summary>
    public class ScanQualityResult
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
    }
}
