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
using MES.Core.Interfaces.Quality;

namespace MES.Blazor.Pages.Quality;

public partial class InspectionPatrolForm : IAsyncDisposable
{
    [Parameter] public int Id { get; set; }

    private static int MaxAttachments => InspectionPatrolService.MaxAttachmentPerType;

    /// <summary>客户端压缩最长边（px）与 JPEG 质量</summary>
    private const int MaxImageSize = 1600;
    private const double ImageQuality = 0.8;

    /// <summary>页面脚本版本过旧（attachment.js 未加载）时的统一提示</summary>
    private const string StaleClientHint = "页面脚本未加载（页面版本过旧），请关闭本页重新打开，或按 Ctrl+Shift+R 强制刷新后再试";

    private bool _isEditMode;
    private bool _isSaving;
    private bool _isUploading;
    private string _errorMessage = string.Empty;

    /// <summary>非致命提示（如「单据已保存但部分照片上传失败」）——与硬失败分开显示，避免红色误读</summary>
    private string _warningMessage = string.Empty;

    private InspectionPatrolDto _form = new();
    private string _patrolDate = DateTime.Today.ToString("yyyy-MM-dd");
    private string _batchNo = string.Empty;
    private string _processName = string.Empty;
    private string? _manufacturingSpec;
    private string _sectionName = string.Empty;

    private List<InspectionPatrolProcessGroupOption> _processGroupOptions = new();
    private List<string> _specOptions = new();
    private List<string> _sectionOptions = new();

    /// <summary>在产单位/车间 候选（委外单位档案驱动，允许手填）</summary>
    private InspectionPatrolPositionOptionsDto? _positionOptions;

    /// <summary>在产单位为委外单位：置灰「在产设备号/在产操作人」（委外无本厂设备与操作人）</summary>
    private bool _isOutsourced;

    // ========== 巡检明细 ==========
    private readonly List<ItemRow> _items = new();

    private class ItemRow
    {
        public string ItemName { get; set; } = "";
        public string? Result { get; set; }
        public string? Remark { get; set; }
    }

    // ========== 照片 ==========
    private ElementReference _patrolFileInput;
    private ElementReference _rectFileInput;
    private readonly List<PhotoItem> _patrolPhotos = new();
    private readonly List<PhotoItem> _rectPhotos = new();

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
        await LoadPositionOptionsAsync();
        await LoadEmployeesAsync();
        if (_isEditMode)
            await LoadExistingAsync();
        else if (_items.Count == 0)
            AddItemRow();
    }

    private async Task LoadPositionOptionsAsync()
    {
        var resp = await PatrolService.GetPositionOptionsAsync();
        if (resp.Success && resp.Data != null)
            _positionOptions = resp.Data;
    }

    private async Task LoadExistingAsync()
    {
        var response = await PatrolService.GetByIdAsync(Id);
        if (!response.Success || response.Data == null)
        {
            Snackbar.Add($"加载失败: {response.Message}", Severity.Error);
            return;
        }

        var dto = response.Data;
        _form = dto;
        _patrolDate = dto.PatrolDate.ToString("yyyy-MM-dd");
        _batchNo = dto.BatchNo;
        _processName = dto.ProcessName;
        _manufacturingSpec = dto.ManufacturingSpec;
        _sectionName = dto.SectionName;

        // 存量存档为纯姓名，回填选中项供下拉显示（档案里已无此人则包临时实例，仍能看到原文本）
        _inspectorEmp = ToEmployeeSelection(dto.Inspector);
        _operatorEmp = ToEmployeeSelection(dto.ProductionOperator);

        // 已有单：仅按存量「在产单位」置灰，不抹掉存量设备号/操作人（存量数据保真）
        RefreshOutsourcedState(clearWhenOutsourced: false);

        foreach (var it in dto.Items)
            _items.Add(new ItemRow { ItemName = it.ItemName, Result = it.Result, Remark = it.Remark });
        if (_items.Count == 0) AddItemRow();

        // 带出批次工序组选项
        if (!string.IsNullOrWhiteSpace(dto.BatchNo))
        {
            var lookup = await PatrolService.LookupBatchAsync(dto.BatchNo);
            if (lookup.Success && lookup.Data != null)
            {
                _form.WorkOrderNo = lookup.Data.WorkOrderNo;
                _form.PlantGrade = lookup.Data.PlantGrade;
                _processGroupOptions = lookup.Data.ProcessGroups;
                UpdateSpecOptions();
                UpdateSectionOptions();
            }
        }

        // 加载已有照片预览（按类型分列）
        foreach (var att in dto.Attachments)
        {
            var target = att.PhotoType == InspectionPatrolPhotoTypes.Rectification ? _rectPhotos : _patrolPhotos;
            var bytes = await PatrolService.GetAttachmentBytesAsync(Id, att.Id);
            if (bytes == null) continue;
            var base64 = Convert.ToBase64String(bytes);
            string? url;
            try
            {
                url = await JS.InvokeAsync<string?>("MES.base64ToObjectUrl", base64, att.ContentType);
            }
            catch (JSException)
            {
                Snackbar.Add(StaleClientHint, Severity.Warning);
                break;
            }
            if (string.IsNullOrEmpty(url)) continue;
            target.Add(new PhotoItem
            {
                AttachmentId = att.Id,
                FileName = att.FileName,
                ContentType = att.ContentType,
                Base64 = base64,
                PreviewUrl = url
            });
        }
    }

    // ========== 巡检人 / 在产操作人 自动补全（员工档案驱动） ==========

    /// <summary>全量启用员工（巡检人与在产操作人共用一份，页面初始化时加载一次）</summary>
    private List<EmployeeDto> _employees = new();

    /// <summary>巡检人选中项</summary>
    private EmployeeDto? _inspectorEmp;

    /// <summary>在产操作人选中项</summary>
    private EmployeeDto? _operatorEmp;

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
    private IEnumerable<EmployeeDto> FilterEmployees(string? keyword)
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
        return result.Take(50);
    }

    private Task<IEnumerable<EmployeeDto>> FilterInspectorAsync(string? keyword)
        => Task.FromResult(FilterEmployees(keyword));

    private Task<IEnumerable<EmployeeDto>> FilterOperatorAsync(string? keyword)
        => Task.FromResult(FilterEmployees(keyword));

    private void OnInspectorSelected(EmployeeDto? emp)
    {
        _inspectorEmp = emp;
        if (emp != null) _form.Inspector = emp.Name;
    }

    private void OnOperatorSelected(EmployeeDto? emp)
    {
        _operatorEmp = emp;
        if (emp != null) _form.ProductionOperator = emp.Name;
    }

    /// <summary>巡检人手输通道：文本与当前选中项一致=下拉回填（忽略），否则视为档案外手输并脱离选中态</summary>
    private void OnInspectorTextChanged(string? text)
    {
        if (_inspectorEmp != null && string.Equals(EmployeeDisplay(_inspectorEmp), text, StringComparison.Ordinal))
            return;
        _inspectorEmp = null;
        _form.Inspector = text ?? string.Empty;
    }

    /// <summary>在产操作人手输通道：同巡检人</summary>
    private void OnOperatorTextChanged(string? text)
    {
        if (_operatorEmp != null && string.Equals(EmployeeDisplay(_operatorEmp), text, StringComparison.Ordinal))
            return;
        _operatorEmp = null;
        _form.ProductionOperator = string.IsNullOrWhiteSpace(text) ? null : text;
    }

    // ========== 在产单位候选（委外单位档案驱动，允许手填） ==========

    private static IEnumerable<string> SearchOption(List<string>? options, string? keyword)
    {
        if (options == null || options.Count == 0) return Enumerable.Empty<string>();
        if (string.IsNullOrWhiteSpace(keyword)) return options.Take(50);
        var kw = keyword.Trim();
        return options.Where(o => o.Contains(kw, StringComparison.OrdinalIgnoreCase)).Take(50);
    }

    private Task<IEnumerable<string>> SearchOptionAsync(List<string>? options, string? keyword)
        => Task.FromResult(SearchOption(options, keyword));

    /// <summary>在产单位/车间 值变更：重算委外标记，委外时清空 在产设备名/在产操作人</summary>
    private void OnProductionUnitChanged(string? value)
    {
        _form.ProductionUnit = value;
        RefreshOutsourcedState(clearWhenOutsourced: true);
    }

    /// <summary>按「是否命中委外单位档案」判定是否委外在产</summary>
    private bool IsOutsourcedUnit(string? unit)
        => !string.IsNullOrWhiteSpace(unit)
           && _positionOptions?.ProductionUnits.Any(
                  u => string.Equals(u, unit.Trim(), StringComparison.OrdinalIgnoreCase)) == true;

    /// <summary>
    /// 重算委外标记。clearWhenOutsourced=true 时清空 在产设备号/在产操作人
    /// （用户改单位、批次带出时用）；false 时只置灰不改数据（加载已有单时用，避免抹掉存量值）。
    /// </summary>
    private void RefreshOutsourcedState(bool clearWhenOutsourced)
    {
        _isOutsourced = IsOutsourcedUnit(_form.ProductionUnit);
        if (_isOutsourced && clearWhenOutsourced)
        {
            _form.EquipmentName = null;
            _form.ProductionOperator = null;
            _operatorEmp = null;
        }
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

        var response = await PatrolService.LookupBatchAsync(_batchNo.Trim());
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
            return;
        }

        _batchNo = response.Data.BatchNo;
        _form.ProductionBatchId = response.Data.ProductionBatchId;
        _form.WorkOrderNo = response.Data.WorkOrderNo;
        _form.PlantGrade = response.Data.PlantGrade;

        // 批次在产信息带出：委外单位 → 在产单位/车间；厂内在产设备名称 → 在产设备号。带出后仍可手改。
        if (!string.IsNullOrWhiteSpace(response.Data.CurrentOutsource))
            _form.ProductionUnit = response.Data.CurrentOutsource;
        if (!string.IsNullOrWhiteSpace(response.Data.CurrentEquipmentName))
            _form.EquipmentName = response.Data.CurrentEquipmentName;
        RefreshOutsourcedState(clearWhenOutsourced: true);

        _processGroupOptions = response.Data.ProcessGroups;

        // 保持已选工序（若仍存在），否则默认第一道工序
        if (_processGroupOptions.All(pg => pg.ProcessName != _processName))
        {
            _processName = _processGroupOptions.FirstOrDefault()?.ProcessName ?? string.Empty;
            _manufacturingSpec = null;
            _sectionName = string.Empty;
        }
        UpdateSpecOptions();
        UpdateSectionOptions();
        Snackbar.Add("已带出批次信息", Severity.Success);
    }

    private void OnProcessNameChanged(string? value)
    {
        _processName = value ?? string.Empty;
        _manufacturingSpec = null;
        _sectionName = string.Empty;
        UpdateSpecOptions();
        UpdateSectionOptions();
    }

    private void OnManufacturingSpecChanged(string? value)
    {
        _manufacturingSpec = value;
        _sectionName = string.Empty;
        UpdateSectionOptions();
    }

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

    // ========== 巡检明细 ==========

    private void AddItemRow() => _items.Add(new ItemRow());

    private void RemoveItemRow(ItemRow row) => _items.Remove(row);

    // ========== 整改 ==========

    private void OnNeedRectificationChanged(bool value)
    {
        _form.NeedRectification = value;
        if (!value)
        {
            _form.RectificationDescription = null;
            _form.VerificationResult = null;
            _form.IsClosed = false;
        }
    }

    // ========== 照片 ==========

    private async Task OpenFilePicker(ElementReference input)
    {
        try
        {
            await JS.InvokeVoidAsync("MES.clickElement", input);
        }
        catch (JSException)
        {
            Snackbar.Add(StaleClientHint, Severity.Warning);
        }
    }

    private Task OnPatrolFilesSelected(ChangeEventArgs e) => ReadFilesAsync(_patrolFileInput, _patrolPhotos);
    private Task OnRectFilesSelected(ChangeEventArgs e) => ReadFilesAsync(_rectFileInput, _rectPhotos);

    private async Task ReadFilesAsync(ElementReference input, List<PhotoItem> photos)
    {
        if (photos.Count >= MaxAttachments)
        {
            Snackbar.Add($"最多上传 {MaxAttachments} 张照片", Severity.Warning);
            return;
        }

        _isUploading = true;
        try
        {
            var results = await JS.InvokeAsync<List<CompressedImage>>(
                "MES.readCompressedFiles", input, MaxImageSize, ImageQuality);

            foreach (var r in results)
            {
                if (photos.Count >= MaxAttachments)
                {
                    Snackbar.Add($"超出上限，仅保留前 {MaxAttachments} 张", Severity.Warning);
                    break;
                }
                if (string.IsNullOrEmpty(r.Data)) continue;
                var contentType = string.IsNullOrWhiteSpace(r.ContentType) ? "image/jpeg" : r.ContentType;
                var url = await JS.InvokeAsync<string?>("MES.base64ToObjectUrl", r.Data, contentType);
                if (string.IsNullOrEmpty(url)) continue;
                photos.Add(new PhotoItem
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

    private async Task RemovePhotoAsync(PhotoItem photo, List<PhotoItem> photos)
    {
        try
        {
            if (photo.AttachmentId.HasValue)
            {
                var dialog = DialogService.Show<ConfirmDialog>("确认", new DialogParameters
                {
                    ["ContentText"] = $"确定要删除照片「{photo.FileName}」吗？",
                    ["ConfirmText"] = "确认删除",
                    ["Color"] = Color.Error
                });
                var dr = await dialog.Result;
                // dr 为 null（对话框被环境销毁/未返回结果）时按「取消」处理，避免 NRE
                if (dr is null || dr.Canceled) return;

                var result = await PatrolService.DeleteAttachmentAsync(Id, photo.AttachmentId.Value);
                if (!result.Success)
                {
                    Snackbar.Add(result.Message ?? "删除照片失败", Severity.Error);
                    return;
                }
            }
            photos.Remove(photo);
            Snackbar.Add("照片已删除", Severity.Success);
        }
        catch (Exception ex)
        {
            // 异常必须落成可见反馈：本方法被 ScanPhotoPicker.OnDeleteExisting 直接 await，
            // 未捕获会冒泡到 MudIconButton.OnClickHandler，按钮表现为「点了没反应」
            Snackbar.Add($"删除照片失败: {ex.Message}", Severity.Error);
        }
    }

    // ========== 保存 ==========

    private async Task Save()
    {
        _errorMessage = string.Empty;
        _warningMessage = string.Empty;

        var errors = new List<string>();
        if (!DateTime.TryParse(_patrolDate, out var patrolDate))
            errors.Add("巡检日期格式无效（请用 yyyy-MM-dd）");
        if (string.IsNullOrWhiteSpace(_form.Inspector)) errors.Add("巡检人不能为空");
        if (string.IsNullOrWhiteSpace(_batchNo)) errors.Add("生产编号不能为空");
        if (string.IsNullOrWhiteSpace(_processName)) errors.Add("工序名称不能为空");
        if (string.IsNullOrWhiteSpace(_sectionName)) errors.Add("工段名称不能为空");

        var validItems = _items.Where(i => !string.IsNullOrWhiteSpace(i.ItemName)).ToList();
        if (validItems.Count == 0) errors.Add("请至少填写一条巡检明细（巡检项不能为空）");

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
            var itemRequests = validItems.Select(i => new InspectionPatrolItemRequest
            {
                ItemName = i.ItemName.Trim(),
                Result = string.IsNullOrWhiteSpace(i.Result) ? null : i.Result.Trim(),
                Remark = string.IsNullOrWhiteSpace(i.Remark) ? null : i.Remark.Trim()
            }).ToList();

            InspectionPatrolDto? saved;
            if (_isEditMode)
            {
                var result = await PatrolService.UpdateAsync(Id, new UpdateInspectionPatrolRequest
                {
                    PatrolDate = patrolDate,
                    Inspector = _form.Inspector,
                    BatchNo = _batchNo.Trim(),
                    ProductionBatchId = _form.ProductionBatchId,
                    ProcessName = _processName,
                    ManufacturingSpec = _manufacturingSpec,
                    SectionName = _sectionName,
                    ProductionUnit = string.IsNullOrWhiteSpace(_form.ProductionUnit) ? null : _form.ProductionUnit.Trim(),
                    EquipmentName = string.IsNullOrWhiteSpace(_form.EquipmentName) ? null : _form.EquipmentName.Trim(),
                    ProductionOperator = string.IsNullOrWhiteSpace(_form.ProductionOperator) ? null : _form.ProductionOperator.Trim(),
                    Items = itemRequests,
                    NeedRectification = _form.NeedRectification,
                    RectificationDescription = _form.RectificationDescription,
                    VerificationResult = _form.VerificationResult,
                    IsClosed = _form.IsClosed
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
                var result = await PatrolService.CreateAsync(new CreateInspectionPatrolRequest
                {
                    PatrolDate = patrolDate,
                    Inspector = _form.Inspector.Trim(),
                    DataSource = "MANUAL",
                    BatchNo = _batchNo.Trim(),
                    ProductionBatchId = _form.ProductionBatchId,
                    ProcessName = _processName,
                    ManufacturingSpec = _manufacturingSpec,
                    SectionName = _sectionName,
                    ProductionUnit = string.IsNullOrWhiteSpace(_form.ProductionUnit) ? null : _form.ProductionUnit.Trim(),
                    EquipmentName = string.IsNullOrWhiteSpace(_form.EquipmentName) ? null : _form.EquipmentName.Trim(),
                    ProductionOperator = string.IsNullOrWhiteSpace(_form.ProductionOperator) ? null : _form.ProductionOperator.Trim(),
                    Items = itemRequests,
                    NeedRectification = _form.NeedRectification,
                    RectificationDescription = _form.RectificationDescription,
                    VerificationResult = _form.VerificationResult,
                    IsClosed = _form.IsClosed
                });
                if (!result.Success || result.Data == null)
                {
                    _errorMessage = result.Message ?? "创建失败";
                    Snackbar.Add($"创建失败: {_errorMessage}", Severity.Error);
                    return;
                }
                saved = result.Data;
            }

            // 上传待上传照片（整改验证照片仅在「涉及整改」时提交）
            var uploadFailed = 0;
            var pending = _patrolPhotos.Where(p => !p.AttachmentId.HasValue)
                .Select(p => (Photo: p, Type: InspectionPatrolPhotoTypes.Patrol)).ToList();
            if (_form.NeedRectification)
                pending.AddRange(_rectPhotos.Where(p => !p.AttachmentId.HasValue)
                    .Select(p => (Photo: p, Type: InspectionPatrolPhotoTypes.Rectification)));

            foreach (var (photo, type) in pending)
            {
                try
                {
                    var bytes = Convert.FromBase64String(photo.Base64);
                    var up = await PatrolService.UploadAttachmentAsync(saved.Id, type, bytes, photo.FileName, photo.ContentType);
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
                _warningMessage = $"巡检单已保存，但有 {uploadFailed} 张照片上传失败。请再点一次「保存」重试，或稍后进入编辑页补传。";
                Snackbar.Add(_warningMessage, Severity.Warning);
                return;
            }

            Snackbar.Add("保存成功", Severity.Success);

            Navigation.NavigateTo("/quality/inspection-patrol");
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

    private void GoBack() => Navigation.NavigateTo("/quality/inspection-patrol");

    public async ValueTask DisposeAsync()
    {
        try { await JS.InvokeVoidAsync("MES.revokeAllObjectUrls"); } catch { }
    }
}
