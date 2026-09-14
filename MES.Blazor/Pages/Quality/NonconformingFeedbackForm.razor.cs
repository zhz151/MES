using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Blazor.Services;
using MES.Blazor.Shared;
using MES.Core.Constants;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Quality;
using MES.Core.Enums;
using MES.Core.Interfaces.Quality;

namespace MES.Blazor.Pages.Quality;

public partial class NonconformingFeedbackForm : IAsyncDisposable
{
    [Parameter] public int Id { get; set; }

    private static int MaxAttachments => NonconformingFeedbackService.MaxAttachmentCount;

    /// <summary>客户端压缩最长边（px）与 JPEG 质量</summary>
    private const int MaxImageSize = 1600;
    private const double ImageQuality = 0.8;

    /// <summary>
    /// JS 互操作目标不存在时的统一提示。
    /// 典型成因：浏览器仍在运行「attachment.js 加入之前」加载的旧版页面/SW 缓存，
    /// 而 Blazor WASM 只在应用启动时解析一次 index.html 的 &lt;script&gt; 标签，此后重进页面也不会重新加载。
    /// </summary>
    private const string StaleClientHint = "页面脚本未加载（页面版本过旧），请关闭本页重新打开，或按 Ctrl+Shift+R 强制刷新后再试";

    private bool _isEditMode;
    private bool _isSaving;
    private bool _isUploading;
    private string _errorMessage = string.Empty;

    /// <summary>非致命提示（如「单据已保存但部分照片上传失败」）——与硬失败分开显示，避免红色误读</summary>
    private string _warningMessage = string.Empty;

    private NonconformingFeedbackDto _form = new();
    private string _reportDate = DateTime.Today.ToString("yyyy-MM-dd");
    private string _batchNo = string.Empty;
    private string _processName = string.Empty;
    private string? _manufacturingSpec;
    private string? _sectionName;

    /// <summary>来源类型（<see cref="NonconformingFeedbackSourceType"/> 枚举名，库存英文 Key）</summary>
    private string _sourceType = nameof(NonconformingFeedbackSourceType.ProductionSection);

    /// <summary>检验项目（仅「成品检验」来源必填）</summary>
    private InspectionItem? _inspectionItem;

    /// <summary>该批次是否存在「附加成检」工序（带出批次后填）——成品检验档据此推导预检/终检</summary>
    private bool _hasAdditionalFinalInspection;

    private List<NonconformingFeedbackProcessGroupOption> _processGroupOptions = new();
    private List<string> _specOptions = new();
    private List<string> _sectionOptions = new();

    // 不合格重量：留空自动按支数比计算；用户手改后不再覆盖
    private bool _defectWeightManual;

    // ========== 照片 ==========
    private ElementReference _fileInput;
    private readonly List<PhotoItem> _photos = new();

    private class PhotoItem
    {
        /// <summary>已保存附件的Id（为空表示待上传）</summary>
        public int? AttachmentId { get; set; }
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "image/jpeg";
        /// <summary>base64（不含 data: 前缀）</summary>
        public string Base64 { get; set; } = "";
        public string PreviewUrl { get; set; } = "";
    }

    private class CompressedImage
    {
        [JsonPropertyName("data")] public string Data { get; set; } = "";
        [JsonPropertyName("fileName")] public string FileName { get; set; } = "";
        [JsonPropertyName("contentType")] public string ContentType { get; set; } = "";
    }

    protected override async Task OnInitializedAsync()
    {
        _isEditMode = Id > 0;
        await LoadEmployeesAsync();
        if (_isEditMode)
        {
            await LoadExistingAsync();
        }
    }

    private async Task LoadExistingAsync()
    {
        var response = await FeedbackService.GetByIdAsync(Id);
        if (!response.Success || response.Data == null)
        {
            Snackbar.Add($"加载失败: {response.Message}", Severity.Error);
            return;
        }

        var dto = response.Data;
        _form = dto;
        _reportDate = dto.ReportDate.ToString("yyyy-MM-dd");
        _batchNo = dto.BatchNo;
        _processName = dto.ProcessName;
        _manufacturingSpec = dto.ManufacturingSpec;
        _sectionName = dto.SectionName;
        _sourceType = string.IsNullOrWhiteSpace(dto.SourceType)
            ? nameof(NonconformingFeedbackSourceType.ProductionSection)
            : dto.SourceType;
        _inspectionItem = dto.InspectionItem;
        // 已存重量视为手工值，避免重算覆盖
        _defectWeightManual = dto.DefectWeight.HasValue;

        // 存量存档为纯姓名，回填选中项供下拉显示（档案里已无此人则包临时实例，仍能看到原文本）
        _reporterEmp = ToEmployeeSelection(dto.Reporter);

        // 带出批次工序组选项
        if (!string.IsNullOrWhiteSpace(dto.BatchNo))
        {
            var lookup = await FeedbackService.LookupBatchAsync(dto.BatchNo);
            if (lookup.Success && lookup.Data != null)
            {
                _form.WorkOrderNo = lookup.Data.WorkOrderNo;
                _form.PlantGrade = lookup.Data.PlantGrade;
                _processGroupOptions = lookup.Data.ProcessGroups;
                _hasAdditionalFinalInspection = lookup.Data.HasAdditionalFinalInspection;
                UpdateSpecOptions();
                UpdateSectionOptions();
            }
        }

        // 加载已有照片预览
        foreach (var att in dto.Attachments)
        {
            var bytes = await FeedbackService.GetAttachmentBytesAsync(Id, att.Id);
            if (bytes == null) continue;
            var base64 = Convert.ToBase64String(bytes);
            string? url;
            try
            {
                url = await JS.InvokeAsync<string?>("MES.base64ToObjectUrl", base64, att.ContentType);
            }
            catch (JSException)
            {
                // 脚本不可用 → 预览无法生成，提示后停止（后续照片同样无法预览）
                Snackbar.Add(StaleClientHint, Severity.Warning);
                break;
            }
            if (string.IsNullOrEmpty(url)) continue;
            _photos.Add(new PhotoItem
            {
                AttachmentId = att.Id,
                FileName = att.FileName,
                ContentType = att.ContentType,
                Base64 = base64,
                PreviewUrl = url
            });
        }
    }

    // ========== 反馈人自动补全 ==========

    /// <summary>全量启用员工（页面初始化时加载一次）</summary>
    private List<EmployeeDto> _employees = new();

    /// <summary>反馈人选中项</summary>
    private EmployeeDto? _reporterEmp;

    /// <summary>加载全量启用员工（与生产记录/过程检验同款；空筛选自动忽略=不按工段收窄）</summary>
    private async Task LoadEmployeesAsync()
    {
        var resp = await EmployeeService.GetBySectionAsync(null);
        _employees = resp.Success && resp.Data != null ? resp.Data : new List<EmployeeDto>();
        if (_employees.Count == 0)
            Snackbar.Add("员工档案候选加载失败，请联系管理员检查员工档案", Severity.Warning);
    }

    /// <summary>下拉显示文本：只显姓名（null 安全）</summary>
    private static string EmployeeDisplay(EmployeeDto? e) => e?.Name ?? string.Empty;

    /// <summary>
    /// 纯姓名 → 下拉选中项（档案命中用档案实例，档案外/为空则为空或临时实例）。
    /// 编辑已存单时用：档案里已无此人也要能把原文本显示出来。
    /// </summary>
    private EmployeeDto? ToEmployeeSelection(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return _employees.FirstOrDefault(e =>
                   string.Equals(e.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? new EmployeeDto { Name = name.Trim() };
    }

    /// <summary>按姓名/工号内存模糊过滤（空关键字=列全部，上限 50）</summary>
    private Task<IEnumerable<EmployeeDto>> FilterReporterAsync(string? keyword)
    {
        IEnumerable<EmployeeDto> result;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            result = _employees;
        }
        else
        {
            var kw = keyword.Trim();
            result = _employees.Where(e =>
                (e.Name?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
                || (e.Code?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        return Task.FromResult(result.Take(50));
    }

    private void OnReporterSelected(EmployeeDto? emp)
    {
        _reporterEmp = emp;
        if (emp != null) _form.Reporter = emp.Name;
    }

    /// <summary>反馈人手输通道：文本与当前选中项一致=下拉回填（忽略），否则视为档案外手输并脱离选中态</summary>
    private void OnReporterTextChanged(string? text)
    {
        if (_reporterEmp != null && string.Equals(EmployeeDisplay(_reporterEmp), text, StringComparison.Ordinal))
            return;
        _reporterEmp = null;
        _form.Reporter = text ?? string.Empty;
    }

    // ========== 生产编号带出 ==========

    private async Task OnBatchNoChanged(string value)
    {
        _batchNo = value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_batchNo)) return;
        await LookupBatchAsync();
    }

    private async Task LookupBatchAsync()
    {
        if (string.IsNullOrWhiteSpace(_batchNo))
        {
            Snackbar.Add("请先输入生产编号", Severity.Warning);
            return;
        }

        var response = await FeedbackService.LookupBatchAsync(_batchNo.Trim());
        if (!response.Success)
        {
            Snackbar.Add($"查询失败: {response.Message}", Severity.Error);
            return;
        }
        if (response.Data == null)
        {
            Snackbar.Add($"生产编号「{_batchNo}」不存在", Severity.Warning);
            _processGroupOptions = new();
            _specOptions = new();
            _sectionOptions = new();
            _hasAdditionalFinalInspection = false;
            return;
        }

        _batchNo = response.Data.BatchNo;
        _form.WorkOrderNo = response.Data.WorkOrderNo;
        _form.PlantGrade = response.Data.PlantGrade;
        _processGroupOptions = response.Data.ProcessGroups;
        _hasAdditionalFinalInspection = response.Data.HasAdditionalFinalInspection;

        // 保持已选工序（若仍存在），否则默认第一道工序
        if (_processGroupOptions.All(pg => pg.ProcessName != _processName))
        {
            _processName = _processGroupOptions.FirstOrDefault()?.ProcessName ?? string.Empty;
            _manufacturingSpec = null;
            _sectionName = null;
        }
        UpdateSpecOptions();
        UpdateSectionOptions();
        Snackbar.Add("已带出批次信息", Severity.Success);
    }

    private void OnProcessNameChanged(string? value)
    {
        _processName = value ?? string.Empty;
        _manufacturingSpec = null;
        _sectionName = null;
        UpdateSpecOptions();
        UpdateSectionOptions();
    }

    private void OnManufacturingSpecChanged(string? value)
    {
        _manufacturingSpec = value;
        _sectionName = null;
        UpdateSectionOptions();
    }

    /// <summary>切换来源类型：离开「成品检验」时清空检验项目，进入时清空工段，避免残留脏值</summary>
    private void OnSourceTypeChanged(string? value)
    {
        _sourceType = value ?? nameof(NonconformingFeedbackSourceType.ProductionSection);
        if (IsFinalInspectionSource)
        {
            _sectionName = null;
        }
        else
        {
            _inspectionItem = null;
            UpdateSectionOptions();
        }
    }

    /// <summary>是否「成品检验」来源（位置信息取 工序+检验项目，无工段）</summary>
    private bool IsFinalInspectionSource
        => _sourceType == nameof(NonconformingFeedbackSourceType.FinalInspection);

    /// <summary>
    /// 成品检验档「成检类型」推导（只读显示）：附加成检工序=终检；
    /// 批次存在附加成检时其余工序=预检；批次无附加成检则全部=终检。
    /// </summary>
    private string FinalInspectionStageText
        => _processName == ProcessKeys.AdditionalFinalInspection
            ? "终检"
            : (_hasAdditionalFinalInspection ? "预检" : "终检");

    /// <summary>按所选工序列出可用制造规格</summary>
    private void UpdateSpecOptions()
    {
        _specOptions = _processGroupOptions
            .Where(pg => pg.ProcessName == _processName && !string.IsNullOrWhiteSpace(pg.ManufacturingSpec))
            .Select(pg => pg.ManufacturingSpec!)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        if (_manufacturingSpec != null && !_specOptions.Contains(_manufacturingSpec))
            _manufacturingSpec = null;
        if (_manufacturingSpec == null && _specOptions.Count > 0)
            _manufacturingSpec = _specOptions[0];
    }

    /// <summary>按 工序名称+制造规格 定位工序组，列出其工段</summary>
    private void UpdateSectionOptions()
    {
        var pg = _processGroupOptions.FirstOrDefault(p =>
            p.ProcessName == _processName && p.ManufacturingSpec == _manufacturingSpec)
            ?? _processGroupOptions.FirstOrDefault(p => p.ProcessName == _processName);

        _sectionOptions = pg?.Sections ?? new List<string>();
        if (!string.IsNullOrEmpty(_sectionName) && !_sectionOptions.Contains(_sectionName))
            _sectionName = string.Empty;
        if (string.IsNullOrEmpty(_sectionName) && _sectionOptions.Count > 0)
            _sectionName = _sectionOptions[0];
    }

    // ========== 不合格重量自动计算 ==========

    private void OnIncomingQuantityChanged(int? value) { _form.IncomingQuantity = value; RecalcDefectWeight(); }
    private void OnIncomingWeightChanged(decimal? value) { _form.IncomingWeight = value; RecalcDefectWeight(); }
    private void OnDefectQuantityChanged(int? value) { _form.DefectQuantity = value; RecalcDefectWeight(); }

    private void OnDefectWeightChanged(int? value)
    {
        _form.DefectWeight = value;
        // 留空 → 恢复自动计算；手填 → 锁定
        _defectWeightManual = value.HasValue;
        if (!value.HasValue) RecalcDefectWeight();
    }

    /// <summary>不合格重量 = 来料重量 ÷ 来料支数 × 不合格支数，四舍五入取整</summary>
    private void RecalcDefectWeight()
    {
        if (_defectWeightManual) return;
        if (!_form.IncomingWeight.HasValue || !_form.IncomingQuantity.HasValue || _form.IncomingQuantity.Value <= 0
            || !_form.DefectQuantity.HasValue || _form.DefectQuantity.Value <= 0)
        {
            _form.DefectWeight = null;
            return;
        }
        _form.DefectWeight = (int)Math.Round(
            _form.IncomingWeight.Value / _form.IncomingQuantity.Value * _form.DefectQuantity.Value,
            MidpointRounding.AwayFromZero);
    }

    // ========== 照片 ==========

    private async Task OpenFilePicker()
    {
        try
        {
            await JS.InvokeVoidAsync("MES.clickElement", _fileInput);
        }
        catch (JSException)
        {
            // 未捕获会冒泡到 MudBaseButton.OnClickHandler → 渲染树被判定崩溃，整页空白
            Snackbar.Add(StaleClientHint, Severity.Warning);
        }
    }

    private async Task OnFilesSelected(ChangeEventArgs e)
    {
        if (_photos.Count >= MaxAttachments)
        {
            Snackbar.Add($"最多上传 {MaxAttachments} 张照片", Severity.Warning);
            return;
        }

        _isUploading = true;
        try
        {
            var results = await JS.InvokeAsync<List<CompressedImage>>(
                "MES.readCompressedFiles", _fileInput, MaxImageSize, ImageQuality);

            foreach (var r in results)
            {
                if (_photos.Count >= MaxAttachments)
                {
                    Snackbar.Add($"超出上限，仅保留前 {MaxAttachments} 张", Severity.Warning);
                    break;
                }
                if (string.IsNullOrEmpty(r.Data)) continue;
                var contentType = string.IsNullOrWhiteSpace(r.ContentType) ? "image/jpeg" : r.ContentType;
                var url = await JS.InvokeAsync<string?>("MES.base64ToObjectUrl", r.Data, contentType);
                if (string.IsNullOrEmpty(url)) continue;
                _photos.Add(new PhotoItem
                {
                    FileName = string.IsNullOrWhiteSpace(r.FileName) ? "photo.jpg" : r.FileName,
                    ContentType = contentType,
                    Base64 = r.Data,
                    PreviewUrl = url
                });
            }
        }
        catch (JSException)
        {
            Snackbar.Add(StaleClientHint, Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"读取照片失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isUploading = false;
        }
    }

    private async Task RemovePhotoAsync(PhotoItem photo)
    {
        try
        {
            // 已保存的附件：确认后立即删除服务端记录
            if (photo.AttachmentId.HasValue)
            {
                var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
                {
                    ["ContentText"] = $"确定要删除照片「{photo.FileName}」吗？",
                    ["ConfirmText"] = "确认删除",
                    ["Color"] = Color.Error
                });
                var dr = await dialog.Result;
                // dr 为 null（对话框被环境销毁/未返回结果）时按「取消」处理，避免 NRE 吞掉后续操作
                if (dr is null || dr.Canceled) return;

                var result = await FeedbackService.DeleteAttachmentAsync(Id, photo.AttachmentId.Value);
                if (!result.Success)
                {
                    Snackbar.Add(result.Message ?? "删除照片失败", Severity.Error);
                    return;
                }
            }
            _photos.Remove(photo);
            Snackbar.Add("照片已删除", Severity.Success);
        }
        catch (Exception ex)
        {
            // 异常必须落成可见反馈：未捕获会冒泡到 MudIconButton.OnClickHandler，按钮表现为「点了没反应」
            Snackbar.Add($"删除照片失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 保存 ==========

    private async Task Save()
    {
        _errorMessage = string.Empty;
        _warningMessage = string.Empty;

        var errors = new List<string>();
        if (!DateTime.TryParse(_reportDate, out var reportDate))
            errors.Add("反馈日期格式无效（请用 yyyy-MM-dd）");
        if (string.IsNullOrWhiteSpace(_form.Reporter)) errors.Add("反馈人不能为空");
        if (string.IsNullOrWhiteSpace(_batchNo)) errors.Add("生产编号不能为空");
        if (string.IsNullOrWhiteSpace(_processName)) errors.Add("工序名称不能为空");
        if (IsFinalInspectionSource)
        {
            if (_inspectionItem == null) errors.Add("检验项目不能为空");
        }
        else if (string.IsNullOrWhiteSpace(_sectionName))
        {
            errors.Add("工段名称不能为空");
        }

        if (errors.Count > 0)
        {
            var msg = string.Join("；", errors);
            Snackbar.Add(msg, Severity.Error);
            _errorMessage = msg;
            return;
        }

        _isSaving = true;
        StateHasChanged();
        try
        {
            NonconformingFeedbackDto? saved;
            if (_isEditMode)
            {
                var result = await FeedbackService.UpdateAsync(Id, new UpdateNonconformingFeedbackRequest
                {
                    ReportDate = reportDate,
                    Reporter = _form.Reporter,
                    BatchNo = _batchNo.Trim(),
                    ProcessName = _processName,
                    ManufacturingSpec = _manufacturingSpec,
                    SourceType = _sourceType,
                    SectionName = IsFinalInspectionSource ? null : _sectionName,
                    InspectionItem = IsFinalInspectionSource ? _inspectionItem : null,
                    IncomingQuantity = _form.IncomingQuantity,
                    IncomingWeight = _form.IncomingWeight,
                    DefectQuantity = _form.DefectQuantity,
                    DefectWeight = _form.DefectWeight,
                    ProblemDescription = _form.ProblemDescription
                });
                if (!result.Success || result.Data == null)
                {
                    _errorMessage = result.Message ?? "保存失败";
                    Snackbar.Add($"保存失败: {_errorMessage}", Severity.Error);
                    return;
                }
                saved = result.Data;
            }
            else
            {
                var result = await FeedbackService.CreateAsync(new CreateNonconformingFeedbackRequest
                {
                    ReportDate = reportDate,
                    Reporter = _form.Reporter.Trim(),
                    BatchNo = _batchNo.Trim(),
                    ProcessName = _processName,
                    ManufacturingSpec = _manufacturingSpec,
                    SourceType = _sourceType,
                    SectionName = IsFinalInspectionSource ? null : _sectionName,
                    InspectionItem = IsFinalInspectionSource ? _inspectionItem : null,
                    IncomingQuantity = _form.IncomingQuantity,
                    IncomingWeight = _form.IncomingWeight,
                    DefectQuantity = _form.DefectQuantity,
                    DefectWeight = _form.DefectWeight,
                    ProblemDescription = _form.ProblemDescription
                });
                if (!result.Success || result.Data == null)
                {
                    _errorMessage = result.Message ?? "创建失败";
                    Snackbar.Add($"创建失败: {_errorMessage}", Severity.Error);
                    return;
                }
                saved = result.Data;
            }

            // 上传待上传照片（逐张隔离：单张失败/异常不阻断其余，也不回滚已保存的单据）
            var uploadFailed = 0;
            foreach (var photo in _photos.Where(p => !p.AttachmentId.HasValue))
            {
                try
                {
                    var bytes = Convert.FromBase64String(photo.Base64);
                    var up = await FeedbackService.UploadAttachmentAsync(saved.Id, bytes, photo.FileName, photo.ContentType);
                    if (!up.Success || up.Data == null)
                    {
                        uploadFailed++;
                        continue;
                    }
                    // 回填附件 Id：留在本页重试时不会重复上传同一张
                    photo.AttachmentId = up.Data.Id;
                }
                catch (Exception)
                {
                    uploadFailed++;
                }
            }

            if (uploadFailed > 0)
            {
                // 不跳转：留在本页让失败可见，用户可直接再点「保存」重试（已成功的照片不会重复上传）
                _warningMessage = $"单据已保存，但有 {uploadFailed} 张照片上传失败。请再点一次「保存」重试，或稍后进入编辑页补传。";
                Snackbar.Add(_warningMessage, Severity.Warning);
                return;
            }

            Snackbar.Add("保存成功", Severity.Success);
            Navigation.NavigateTo("/quality/nonconforming-feedback");
        }
        catch (Exception ex)
        {
            _errorMessage = $"网络错误: {ex.Message}";
            Snackbar.Add(_errorMessage, Severity.Error);
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    private void GoBack() => Navigation.NavigateTo("/quality/nonconforming-feedback");

    public async ValueTask DisposeAsync()
    {
        try { await JS.InvokeVoidAsync("MES.revokeAllObjectUrls"); } catch { }
    }
}
