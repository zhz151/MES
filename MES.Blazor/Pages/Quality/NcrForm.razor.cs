using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Blazor.Services;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Shared.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Configuration;

namespace MES.Blazor.Pages.Quality;

[Authorize(Roles = Roles.Policies.QualityRead)]
public partial class NcrForm
{
    [Inject] private NcrService NcrService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private DictValueDefinitionService DictValueDefinitionService { get; set; } = null!;

    [Parameter] public int Id { get; set; }

    // 从卡片点击传入的查询参数
    [SupplyParameterFromQuery] public string? batchNo { get; set; }
    [SupplyParameterFromQuery] public string? disposalMethod { get; set; }
    [SupplyParameterFromQuery] public string? sourceType { get; set; }
    [SupplyParameterFromQuery] public int? defectQty { get; set; }
    [SupplyParameterFromQuery] public int? defectWeight { get; set; }
    [SupplyParameterFromQuery] public string? inspector { get; set; }
    [SupplyParameterFromQuery] public string? inspectionItem { get; set; }
    [SupplyParameterFromQuery] public string? processName { get; set; }
    [SupplyParameterFromQuery] public string? materialName { get; set; }
    [SupplyParameterFromQuery] public string? reportDate { get; set; }
    [SupplyParameterFromQuery] public string? defectDescription { get; set; }

    private MudForm? form;
    private CreateNcrRequest _formData = new();
    private bool _isEditMode;
    private bool _isSaving;
    private NcrStatus _currentStatus;

    // 责任类别字典下拉（配置表动态加载）与「新增责任类型」输入
    private List<DictValueInfoDto> _responsibilityOptions = new();
    private string _newResponsibilityName = "";

    // 待处理卡片
    private List<NcrPendingCheckDto> _pendingItems = new();
    private bool _showPending = true;

    // 日期字符串绑定（禁止 MudDatePicker）
    private string _reportDate = DateTime.Today.ToString("yyyy-MM-dd");
    private string _disposalCompleteDate = "";
    private string _analysisConfirmDate = "";
    private string _operationDate = "";
    private string _actionPlanDate = "";
    private string _actionVerifyDate = "";
    private string _personCompleteDate = "";

    protected override async Task OnInitializedAsync()
    {
        _isEditMode = Id > 0;

        await LoadResponsibilityOptionsAsync();

        if (_isEditMode)
        {
            await LoadExistingAsync();
        }
        else
        {
            _formData.ReportDate = DateTime.Today;
            _formData.PipeCategory = MaterialType.RoughTube;

            // 加载待处理卡片（非编辑模式）
            await LoadPendingChecksAsync();

            // 从卡片点击传入的参数自动填充
            if (!string.IsNullOrEmpty(batchNo))
            {
                await AutoFillFromPending(batchNo);
            }
        }
    }

    private async Task AutoFillFromPending(string batchNo)
    {
        try
        {
            // 先调取批次基本信息
            var lookup = await NcrService.LookupBatchAsync(batchNo);
            if (lookup.Success && lookup.Data != null)
            {
                _formData.WorkOrderNo = lookup.Data.WorkOrderNo;
                _formData.PlantGrade = lookup.Data.PlantGrade;
                _formData.Specification = lookup.Data.Specification;
            }

            _formData.BatchNo = batchNo;
            _formData.DefectiveQuantity = defectQty;
            _formData.DefectiveWeight = defectWeight;

            // 反馈日期 = 检验记录中的检验日期
            if (!string.IsNullOrEmpty(reportDate) && DateTime.TryParse(reportDate, out var parsedDate))
            {
                _formData.ReportDate = parsedDate;
                _reportDate = parsedDate.ToString("yyyy-MM-dd");
            }

            // 问题描述 = 次品情况描述（从检验记录取）
            _formData.ProblemDescription = defectDescription ?? "";

            // 处置方式
            if (!string.IsNullOrEmpty(disposalMethod) && Enum.TryParse<DisposalMethod>(disposalMethod, out var dm))
            {
                _formData.DisposalMethod = dm;
            }

            // 反馈部门 = 来源 + 检验项目（中文化）
            var sourceText = EnumHelper.GetDisplayName<ReportTemplateType>(sourceType);
            var itemText = GetInspectionItemDisplay(inspectionItem, sourceType);
            _formData.ReportDepartment = string.IsNullOrEmpty(itemText) ? sourceText : $"{sourceText}-{itemText}";

            // 反馈人 = 检验员
            _formData.Reporter = inspector ?? "";

            // 来源检验项目（卡片排重用）
            _formData.SourceInspectionItem = inspectionItem ?? "";

            // 钢管类别
            if (sourceType == "ProcessInspection")
            {
                _formData.PipeCategory = string.Equals(processName, ProcessKeys.RoughTubeProcessing, StringComparison.OrdinalIgnoreCase)
                    ? MaterialType.RoughTube
                    : MaterialType.WorkInProgress;
            }
            else if (sourceType == "FinalInspection")
            {
                _formData.PipeCategory = MapMaterialNameToPipeCategory(materialName);
            }
        }
        catch
        {
            // 静默处理，用户可手动填写
        }
    }

    private async Task FillFromCard(NcrPendingCheckDto item)
    {
        try
        {
            // 批次基本信息
            var lookup = await NcrService.LookupBatchAsync(item.BatchNo);
            if (lookup.Success && lookup.Data != null)
            {
                _formData.WorkOrderNo = lookup.Data.WorkOrderNo;
                _formData.PlantGrade = lookup.Data.PlantGrade;
                _formData.Specification = lookup.Data.Specification;
            }

            _formData.BatchNo = item.BatchNo;
            _formData.DefectiveQuantity = item.DefectQuantity;
            _formData.DefectiveWeight = item.DefectiveWeight;

            // 反馈日期
            _formData.ReportDate = item.ReportDate;
            _reportDate = item.ReportDate.ToString("yyyy-MM-dd");

            // 问题描述
            _formData.ProblemDescription = item.DefectDescription ?? "";

            // 处置方式
            _formData.DisposalMethod = item.DisposalMethod;

            // 反馈部门
            var sourceText = GetSourceTypeText(item.SourceType);
            var itemText = GetInspectionItemDisplay(item.InspectionItem, item.SourceType);
            _formData.ReportDepartment = string.IsNullOrEmpty(itemText) ? sourceText : $"{sourceText}-{itemText}";

            // 反馈人
            _formData.Reporter = item.Inspector ?? "";

            // 来源检验项目
            _formData.SourceInspectionItem = item.InspectionItem ?? "";

            // 钢管类别
            if (item.SourceType == "ProcessInspection")
            {
                _formData.PipeCategory = string.Equals(item.ProcessName, ProcessKeys.RoughTubeProcessing, StringComparison.OrdinalIgnoreCase)
                    ? MaterialType.RoughTube
                    : MaterialType.WorkInProgress;
            }
            else if (item.SourceType == "FinalInspection")
            {
                _formData.PipeCategory = MapMaterialNameToPipeCategory(item.MaterialName);
            }

            Snackbar.Add("已从卡片填充表单", Severity.Success);
        }
        catch
        {
            Snackbar.Add("填充失败，请手动填写", Severity.Warning);
        }
    }

    private static string GetInspectionItemDisplay(string? item, string? sourceType)
    {
        if (string.IsNullOrEmpty(item)) return "";
        if (sourceType == "FinalInspection" && Enum.TryParse<InspectionItem>(item, out var enumItem))
            return DisplayHelper.GetInspectionItemText(enumItem);
        return item; // ProcessInspection: 直接显示原始文本
    }

    private static MaterialType MapMaterialNameToPipeCategory(string? materialName)
    {
        // ManufacturingItem 存储 MaterialType 枚举英文名，直接解析
        if (string.IsNullOrEmpty(materialName)) return MaterialType.WorkInProgress;
        return Enum.TryParse<MaterialType>(materialName, true, out var mt) ? mt : MaterialType.WorkInProgress;
    }

    private async Task LoadExistingAsync()
    {
        var response = await NcrService.GetByIdAsync(Id);
        if (!response.Success || response.Data == null)
        {
            Snackbar.Add($"加载失败: {response.Message}", Severity.Error);
            return;
        }

        var dto = response.Data;
        _currentStatus = dto.Status;

        // G1
        _formData.ReportDate = dto.ReportDate;
        _formData.ReportDepartment = dto.ReportDepartment;
        _formData.Reporter = dto.Reporter;
        _formData.PipeCategory = dto.PipeCategory;
        _formData.BatchNo = dto.BatchNo;
        _formData.WorkOrderNo = dto.WorkOrderNo;
        _formData.PlantGrade = dto.PlantGrade;
        _formData.Specification = dto.Specification;
        _formData.DefectiveQuantity = dto.DefectiveQuantity;
        _formData.DefectiveWeight = dto.DefectiveWeight;
        _formData.ProblemDescription = dto.ProblemDescription;

        // G2
        _formData.DisposalMethod = dto.DisposalMethod;
        _formData.DisposalRemark = dto.DisposalRemark;
        _formData.DisposalIsCompleted = dto.DisposalIsCompleted;
        _formData.DisposalCompleteDate = dto.DisposalCompleteDate;

        // G3
        _formData.RootCauseAnalysis = dto.RootCauseAnalysis;
        _formData.Severity = dto.Severity;
        _formData.AnalysisConfirmer = dto.AnalysisConfirmer;
        _formData.AnalysisConfirmDate = dto.AnalysisConfirmDate;

        // G4
        _formData.ResponsibilityCategory = dto.ResponsibilityCategory;
        _formData.ResponsibleDept = dto.ResponsibleDept;
        _formData.OperationDate = dto.OperationDate;
        _formData.ResponsiblePerson = dto.ResponsiblePerson;
        _formData.PersonDisposition = dto.PersonDisposition;
        _formData.PersonIsCompleted = dto.PersonIsCompleted;
        _formData.PersonCompleteDate = dto.PersonCompleteDate;

        // G5
        _formData.CorrectiveAction = dto.CorrectiveAction;
        _formData.ActionPlanner = dto.ActionPlanner;
        _formData.ActionPlanDate = dto.ActionPlanDate;
        _formData.ActionVerifier = dto.ActionVerifier;
        _formData.ActionVerifyDate = dto.ActionVerifyDate;
        _formData.ActionResult = dto.ActionResult;
        _formData.VerifyResult = dto.VerifyResult;

        // 日期字符串
        _reportDate = dto.ReportDate.ToString("yyyy-MM-dd");
        _disposalCompleteDate = dto.DisposalCompleteDate?.ToString("yyyy-MM-dd") ?? "";
        _analysisConfirmDate = dto.AnalysisConfirmDate?.ToString("yyyy-MM-dd") ?? "";
        _operationDate = dto.OperationDate?.ToString("yyyy-MM-dd") ?? "";
        _actionPlanDate = dto.ActionPlanDate?.ToString("yyyy-MM-dd") ?? "";
        _actionVerifyDate = dto.ActionVerifyDate?.ToString("yyyy-MM-dd") ?? "";
        _personCompleteDate = dto.PersonCompleteDate?.ToString("yyyy-MM-dd") ?? "";
    }

    /// <summary>
    /// 生产编号变更时自动调取批次信息
    /// </summary>
    private async Task OnBatchNoChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        var response = await NcrService.LookupBatchAsync(value.Trim());
        if (response.Success && response.Data != null)
        {
            _formData.WorkOrderNo = response.Data.WorkOrderNo;
            _formData.PlantGrade = response.Data.PlantGrade;
            _formData.Specification = response.Data.Specification;
            _formData.DefectiveQuantity = response.Data.DefectiveQuantity;
            _formData.DefectiveWeight = response.Data.DefectiveWeight;
        }
        // 不清空已有字段（允许手动修改）
    }

    // ========== 责任类别字典 ==========

    /// <summary>加载责任类别字典下拉（配置表动态，失败/空兜底内置 5 值）</summary>
    private async Task LoadResponsibilityOptionsAsync()
    {
        var result = await DictValueDefinitionService.GetEnabledValuesAsync(DictValueDefaults.NcrResponsibilityKey);
        if (result.Success && result.Data is { Count: > 0 })
        {
            _responsibilityOptions = result.Data;
        }
        else
        {
            _responsibilityOptions = NcrResponsibilityKeys.All
                .Select(k => new DictValueInfoDto
                {
                    Value = k,
                    DisplayName = NcrResponsibilityKeys.ToChinese(k)!,
                    DisplayOrder = 0,
                    IsEnabled = true
                })
                .ToList();
        }
    }

    /// <summary>
    /// 新增责任类型：旁侧输入中文名 → 生成 NcrRC_n 英文 Key → 写入字典配置 → 刷新下拉并选中。
    /// </summary>
    private async Task AddResponsibilityAsync()
    {
        var name = _newResponsibilityName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            Snackbar.Add("请输入要新增的责任类型", Severity.Warning);
            return;
        }
        if (!name.Any(c => c >= 0x4E00 && c <= 0x9FFF))
        {
            Snackbar.Add("责任类型必须包含汉字", Severity.Warning);
            return;
        }
        if (_responsibilityOptions.Any(o => string.Equals(o.DisplayName, name, StringComparison.Ordinal)))
        {
            Snackbar.Add($"责任类型「{name}」已存在", Severity.Warning);
            return;
        }

        // 生成 NcrRC_{n}：取现有 NcrRC_ 前缀最大序号 + 1，首增 n=1
        var maxSeq = _responsibilityOptions
            .Select(o => o.Value.StartsWith("NcrRC_", StringComparison.Ordinal) && int.TryParse(o.Value["NcrRC_".Length..], out var seq) ? seq : 0)
            .DefaultIfEmpty(0)
            .Max();
        var key = $"NcrRC_{maxSeq + 1}";

        var result = await DictValueDefinitionService.SaveAsync(new DictValueDefinitionDto
        {
            Id = 0,
            DictKey = DictValueDefaults.NcrResponsibilityKey,
            Value = key,
            DisplayName = name,
            DisplayOrder = 999,
            IsEnabled = true
        });
        if (result.Success)
        {
            Snackbar.Add($"已添加责任类型「{name}」", Severity.Success);
            _newResponsibilityName = "";
            await LoadResponsibilityOptionsAsync();
            _formData.ResponsibilityCategory = key;
        }
        else
        {
            Snackbar.Add($"添加失败: {result.Message}", Severity.Error);
        }
    }

    private async Task Save()
    {
        await form!.Validate();
        if (!form.IsValid) return;

        // 日期字符串转 DateTime
        ApplyDateStrings();

        _isSaving = true;
        try
        {
            if (_isEditMode)
            {
                var updateRequest = new UpdateNcrRequest
                {
                    ReportDate = _formData.ReportDate,
                    ReportDepartment = _formData.ReportDepartment,
                    Reporter = _formData.Reporter,
                    PipeCategory = _formData.PipeCategory,
                    WorkOrderNo = _formData.WorkOrderNo,
                    PlantGrade = _formData.PlantGrade,
                    Specification = _formData.Specification,
                    DefectiveQuantity = _formData.DefectiveQuantity,
                    DefectiveWeight = _formData.DefectiveWeight,
                    ProblemDescription = _formData.ProblemDescription,
                    SourceInspectionItem = _formData.SourceInspectionItem,
                    DisposalMethod = _formData.DisposalMethod,
                    DisposalRemark = _formData.DisposalRemark,
                    DisposalIsCompleted = _formData.DisposalIsCompleted,
                    DisposalCompleteDate = _formData.DisposalCompleteDate,
                    RootCauseAnalysis = _formData.RootCauseAnalysis,
                    Severity = _formData.Severity,
                    AnalysisConfirmer = _formData.AnalysisConfirmer,
                    AnalysisConfirmDate = _formData.AnalysisConfirmDate,
                    ResponsibilityCategory = _formData.ResponsibilityCategory,
                    ResponsibleDept = _formData.ResponsibleDept,
                    OperationDate = _formData.OperationDate,
                    ResponsiblePerson = _formData.ResponsiblePerson,
                    PersonDisposition = _formData.PersonDisposition,
                    PersonIsCompleted = _formData.PersonIsCompleted,
                    PersonCompleteDate = _formData.PersonCompleteDate,
                    CorrectiveAction = _formData.CorrectiveAction,
                    ActionPlanner = _formData.ActionPlanner,
                    ActionPlanDate = _formData.ActionPlanDate,
                    ActionVerifier = _formData.ActionVerifier,
                    ActionVerifyDate = _formData.ActionVerifyDate,
                    ActionResult = _formData.ActionResult,
                    VerifyResult = _formData.VerifyResult,
                };

                var result = await NcrService.UpdateAsync(Id, updateRequest);
                if (result.Success)
                {
                    Snackbar.Add("保存成功", Severity.Success);
                    Navigation.NavigateTo("/quality/ncr");
                }
                else
                {
                    Snackbar.Add($"保存失败: {result.Message}", Severity.Error);
                }
            }
            else
            {
                var result = await NcrService.CreateAsync(_formData);
                if (result.Success)
                {
                    Snackbar.Add("创建成功", Severity.Success);
                    Navigation.NavigateTo("/quality/ncr");
                }
                else
                {
                    Snackbar.Add($"创建失败: {result.Message}", Severity.Error);
                }
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"操作失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void ApplyDateStrings()
    {
        _formData.ReportDate = ParseDate(_reportDate) ?? DateTime.Today;
        _formData.DisposalCompleteDate = ParseDate(_disposalCompleteDate);
        _formData.AnalysisConfirmDate = ParseDate(_analysisConfirmDate);
        _formData.OperationDate = ParseDate(_operationDate);
        _formData.ActionPlanDate = ParseDate(_actionPlanDate);
        _formData.ActionVerifyDate = ParseDate(_actionVerifyDate);
        _formData.PersonCompleteDate = ParseDate(_personCompleteDate);
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, out var dt)) return dt;
        return null;
    }

    private void GoBack()
    {
        Navigation.NavigateTo("/quality/ncr");
    }

    // ========== 待处理卡片 ==========

    private async Task LoadPendingChecksAsync()
    {
        try
        {
            var result = await NcrService.GetPendingChecksAsync();
            if (result.Success && result.Data != null)
                _pendingItems = result.Data;
        }
        catch { }
    }

    private void TogglePendingChecks() => _showPending = !_showPending;

    // ========== 枚举选项 ==========

    private string GetStatusText(NcrStatus status) => DisplayHelper.GetNcrStatusText(status);

    private static string GetSourceTypeText(string sourceType) => EnumHelper.GetDisplayName<ReportTemplateType>(sourceType);

    private static string GetDisposalMethodText(DisposalMethod method) => DisplayHelper.GetDisposalMethodText(method);

    private static Color GetWarningColor() => Color.Warning;

    private static Color GetSourceTypeColor(string sourceType)
        => sourceType == "ProcessInspection" ? Color.Info : Color.Primary;

    private static Color GetDisposalChipColor(DisposalMethod method) => method switch
    {
        DisposalMethod.Rework => Color.Warning,
        DisposalMethod.WarehouseEntry => Color.Info,
        DisposalMethod.Scrap => Color.Error,
        _ => Color.Default
    };

}
