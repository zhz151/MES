using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using MES.Blazor.Helpers;
using MES.Blazor.Services;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Shared.Constants;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Configuration;
using MES.Blazor.Shared;

namespace MES.Blazor.Pages.Quality;

[Authorize(Roles = Roles.Policies.QualityView)]
public partial class NcrForm
{
    [Inject] private NcrService NcrService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private DictValueDefinitionService DictValueDefinitionService { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = null!;

    [Parameter] public int Id { get; set; }

    // 从卡片点击传入的查询参数
    [SupplyParameterFromQuery] public string? batchNo { get; set; }
    [SupplyParameterFromQuery] public string? flowDirection { get; set; }
    [SupplyParameterFromQuery] public string? sourceType { get; set; }
    [SupplyParameterFromQuery] public int? defectQty { get; set; }
    [SupplyParameterFromQuery] public int? defectWeight { get; set; }
    [SupplyParameterFromQuery] public string? inspector { get; set; }
    [SupplyParameterFromQuery] public string? inspectionItem { get; set; }
    [SupplyParameterFromQuery] public string? processName { get; set; }
    [SupplyParameterFromQuery] public string? materialName { get; set; }
    [SupplyParameterFromQuery] public string? reportDate { get; set; }
    [SupplyParameterFromQuery] public string? defectDescription { get; set; }

    /// <summary>工段（被动来源「反馈部门」取值；成品检验来源无工段，改取检验项目）</summary>
    [SupplyParameterFromQuery] public string? sectionName { get; set; }

    /// <summary>让步放行支数（被动来源由待处理组的让步放行合计带出）</summary>
    [SupplyParameterFromQuery] public int? concessionQty { get; set; }

    /// <summary>让步说明（被动来源由检验记录带出）</summary>
    [SupplyParameterFromQuery] public string? concessionRemark { get; set; }

    /// <summary>来源待处理记录定位键（被动来源带出，保存时写入 Ncr.SourceGroupKey 供待处理列表去重、照片入口定位）</summary>
    [SupplyParameterFromQuery] public string? groupKey { get; set; }

    /// <summary>来源不合格反馈单 Id（待处理列表的「不合格反馈」行带入，保存时写入 Ncr 完成闭环）</summary>
    [SupplyParameterFromQuery] public int? feedbackId { get; set; }

    private MudForm? form;
    private CreateNcrRequest _formData = new();
    private bool _isEditMode;
    private bool _isSaving;
    private NcrStatus _currentStatus;

    /// <summary>当前「生产编号」对应批次主键（0=未匹配到批次，此时不显示「批次执行进度」入口）</summary>
    private int _batchId;

    /// <summary>是否有权查看批次执行进度（Policies.BatchView 为逗号分隔多角色串，须逐个 IsInRole）</summary>
    private bool _canViewBatchProgress;

    /// <summary>来源记录（检验记录/不合格反馈单）是否已上传照片（有则显示照片入口，无则忽略）</summary>
    private bool _hasSourcePhotos;

    // 责任类别字典下拉：初始预置内置 5 项中文（避免依赖异步字典导致的英文/空白空窗），异步字典加载成功后覆盖为完整配置（含自定义项）
    private List<DictValueInfoDto> _responsibilityOptions = NcrResponsibilityKeys.All
        .Select(k => new DictValueInfoDto
        {
            Value = k,
            DisplayName = NcrResponsibilityKeys.ToChinese(k) ?? k,
            DisplayOrder = 0,
            IsEnabled = true
        })
        .ToList();
    private string _newResponsibilityName = "";

    // 处置方式字典下拉：初始预置内置 8 项中文（避免依赖异步字典导致的英文/空白空窗），异步字典加载成功后覆盖为完整配置（含自定义项）
    private List<DictValueInfoDto> _disposalOptions = NcrDisposalKeys.All
        .Select(k => new DictValueInfoDto
        {
            Value = k,
            DisplayName = NcrDisposalKeys.ToChinese(k) ?? k,
            DisplayOrder = 0,
            IsEnabled = true
        })
        .ToList();
    private string _newDisposalName = "";

    // 待处理卡片
    private List<NcrPendingCheckDto> _pendingItems = new();
    private bool _showPending = false;

    /// <summary>
    /// 被动来源（过程检验/成品检验超阈值建单）：G1 额外显示「次品流向 + 让步支数/重量/说明」。
    /// 主动（不合格反馈，人工上报）无让步放行概念，隐藏这 4 个字段。
    /// </summary>
    private bool _isPassiveSource;

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

        var authState = await AuthProvider.GetAuthenticationStateAsync();
        _canViewBatchProgress = Roles.Policies.BatchView
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(authState.User.IsInRole);

        await LoadResponsibilityOptionsAsync();
        await LoadDisposalOptionsAsync();

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
                _batchId = lookup.Data.ProductionBatchId;
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

            // 流向（由检验来源卡片带出，只读；处置方式由质量负责人在本页判定）
            if (!string.IsNullOrEmpty(flowDirection) && Enum.TryParse<FlowDirection>(flowDirection, out var fd))
            {
                _formData.FlowDirection = fd;
            }

            // 被动来源（过程检验/成品检验）= 超阈值建单，主动（不合格反馈）= 人工上报
            _isPassiveSource = !string.IsNullOrEmpty(sourceType)
                && sourceType != nameof(NcrPendingSourceType.NonconformingFeedback);

            // 反馈部门 = 位置：成品检验取成检项目，其余（过程检验/不合格反馈）取工段
            _formData.ReportDepartment = sourceType == nameof(NcrPendingSourceType.FinalInspection)
                ? GetInspectionItemDisplay(inspectionItem, sourceType)
                : SectionDisplayHelper.GetSectionNameText(sectionName);

            // 反馈人 = 检验员（实名串「姓名(编号)」→ 纯姓名简化）
            _formData.Reporter = DisplayHelper.FormatPersonName(inspector);

            // 来源检验项目（卡片排重用）
            _formData.SourceInspectionItem = inspectionItem ?? "";

            // 让步放行维度（仅被动来源有值）
            _formData.ConcessionQuantity = concessionQty;
            _formData.ConcessionRemark = concessionRemark ?? "";

            // 来源待处理组定位键（被动来源带出，保存后同组不再重复列出）
            _formData.SourceGroupKey = groupKey ?? "";

            // 来源不合格反馈单（仅「不合格反馈」来源有值）：保存时写入 Ncr.NonconformingFeedbackId
            _formData.NonconformingFeedbackId = feedbackId;

            // 钢管类别
            if (sourceType == nameof(NcrPendingSourceType.ProcessInspection)
                || sourceType == nameof(NcrPendingSourceType.NonconformingFeedback))
            {
                // 不合格反馈按过程检验口径：圆棒穿孔→荒管，否则在制
                _formData.PipeCategory = string.Equals(processName, ProcessKeys.RoughTubeProcessing, StringComparison.OrdinalIgnoreCase)
                    ? MaterialType.RoughTube
                    : MaterialType.WorkInProgress;
            }
            else if (sourceType == nameof(NcrPendingSourceType.FinalInspection))
            {
                _formData.PipeCategory = MapMaterialNameToPipeCategory(materialName);
            }

            await RefreshSourcePhotoEntryAsync();
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
                _batchId = lookup.Data.ProductionBatchId;
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

            // 流向（卡片带出：被动组取组内支数最多的流向；处置方式由人工在本页判定）
            _formData.FlowDirection = item.FlowDirection;

            // 被动来源（超阈值遗漏）= 显示次品流向 + 让步三字段
            _isPassiveSource = item.Bucket == NcrPendingBucket.OverageMissing;

            // 反馈部门 = 位置：成品检验取成检项目，其余取工段
            _formData.ReportDepartment = item.SourceType == nameof(NcrPendingSourceType.FinalInspection)
                ? GetInspectionItemDisplay(item.InspectionItem, item.SourceType)
                : SectionDisplayHelper.GetSectionNameText(item.SectionName);

            // 反馈人（实名串「姓名(编号)」→ 纯姓名简化）
            _formData.Reporter = DisplayHelper.FormatPersonName(item.Inspector);

            // 来源检验项目
            _formData.SourceInspectionItem = item.InspectionItem ?? "";

            // 让步放行维度（仅被动来源有值）
            _formData.ConcessionQuantity = item.ConcessionQuantity;
            _formData.ConcessionRemark = item.ConcessionRemark ?? "";

            // 来源待处理组定位键（保存后同组不再重复列出）
            _formData.SourceGroupKey = item.GroupKey ?? "";

            // 来源不合格反馈单（保存时写入 Ncr.NonconformingFeedbackId 完成闭环）
            _formData.NonconformingFeedbackId = item.NonconformingFeedbackId;

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

            await RefreshSourcePhotoEntryAsync();

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
        _formData.Reporter = DisplayHelper.FormatPersonName(dto.Reporter);
        _formData.PipeCategory = dto.PipeCategory;
        _formData.BatchNo = dto.BatchNo;
        _batchId = dto.ProductionBatchId;
        _formData.WorkOrderNo = dto.WorkOrderNo;
        _formData.PlantGrade = dto.PlantGrade;
        _formData.Specification = dto.Specification;
        _formData.DefectiveQuantity = dto.DefectiveQuantity;
        _formData.DefectiveWeight = dto.DefectiveWeight;
        _formData.ProblemDescription = dto.ProblemDescription;
        _formData.FlowDirection = dto.FlowDirection;
        _formData.ConcessionQuantity = dto.ConcessionQuantity;
        _formData.ConcessionWeight = dto.ConcessionWeight;
        _formData.ConcessionRemark = dto.ConcessionRemark;
        _formData.SourceGroupKey = dto.SourceGroupKey;
        // 来源不合格反馈单（主动来源）：供「来源照片」入口定位该反馈单的问题照片
        _formData.NonconformingFeedbackId = dto.NonconformingFeedbackId;
        _isPassiveSource = !string.IsNullOrEmpty(dto.SourceGroupKey) || dto.FlowDirection.HasValue;

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

        EnsureSelectedResponsibilityMapped();
        EnsureSelectedDisposalMapped();

        await RefreshSourcePhotoEntryAsync();
    }

    /// <summary>编辑加载后兜底：当前责任类别不在选项集合（字典禁用/缺失/异步未达）时补入中文项，杜绝英文直显</summary>
    private void EnsureSelectedResponsibilityMapped()
    {
        var selected = _formData.ResponsibilityCategory;
        if (string.IsNullOrWhiteSpace(selected)) return;
        if (_responsibilityOptions.Any(o => string.Equals(o.Value, selected, StringComparison.OrdinalIgnoreCase))) return;
        _responsibilityOptions.Insert(0, new DictValueInfoDto
        {
            Value = selected,
            DisplayName = NcrResponsibilityKeys.ToChinese(selected)
                ?? DictValueDisplayHelper.GetText(DictValueDefaults.NcrResponsibilityKey, selected)
                ?? selected,
            DisplayOrder = -1,
            IsEnabled = true
        });
    }

    /// <summary>
    /// 生产编号变更时自动调取批次信息
    /// </summary>
    private async Task OnBatchNoChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _batchId = 0;
            return;
        }

        var response = await NcrService.LookupBatchAsync(value.Trim());
        if (response.Success && response.Data != null)
        {
            _batchId = response.Data.ProductionBatchId;
            _formData.WorkOrderNo = response.Data.WorkOrderNo;
            _formData.PlantGrade = response.Data.PlantGrade;
            _formData.Specification = response.Data.Specification;
            _formData.DefectiveQuantity = response.Data.DefectiveQuantity;
            _formData.DefectiveWeight = response.Data.DefectiveWeight;
        }
        else
        {
            _batchId = 0;
        }
        // 不清空已有字段（允许手动修改）
    }

    /// <summary>打开「批次执行进度」卡片（复用计划排程/成检计划同一弹窗，不跳转批次详情页）</summary>
    private async Task OpenBatchProgressAsync()
    {
        if (_batchId <= 0) return;
        var parameters = new DialogParameters
        {
            { nameof(BatchProgressDialog.BatchId), _batchId },
            { nameof(BatchProgressDialog.BatchNo), _formData.BatchNo }
        };
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Large,
            CloseOnEscapeKey = true
        };
        await DialogService.ShowAsync<BatchProgressDialog>("批次执行进度", parameters, options);
    }

    /// <summary>
    /// 判定是否显示「来源照片」入口：按来源定位键（过程检验/成品检验）或来源不合格反馈单查询，
    /// 有照片才显示入口；未拍照/未上传则不显示（忽略）。
    /// </summary>
    private async Task RefreshSourcePhotoEntryAsync()
    {
        var groupKey = _formData.SourceGroupKey;
        var feedbackId = _formData.NonconformingFeedbackId;
        if (string.IsNullOrWhiteSpace(groupKey) && feedbackId is null or <= 0)
        {
            _hasSourcePhotos = false;
            return;
        }

        try
        {
            var result = await NcrService.GetSourcePhotosAsync(groupKey, feedbackId);
            _hasSourcePhotos = result.Success && result.Data is { Count: > 0 };
        }
        catch
        {
            _hasSourcePhotos = false;
        }
    }

    /// <summary>打开来源照片只读弹窗（按来源定位取该来源记录的照片）</summary>
    private async Task OpenSourcePhotosAsync()
    {
        if (!_hasSourcePhotos) return;
        var parameters = new DialogParameters
        {
            { nameof(NcrSourcePhotoDialog.GroupKey), _formData.SourceGroupKey },
            { nameof(NcrSourcePhotoDialog.FeedbackId), _formData.NonconformingFeedbackId },
            { nameof(NcrSourcePhotoDialog.BatchNo), _formData.BatchNo }
        };
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Large,
            CloseOnEscapeKey = true
        };
        await DialogService.ShowAsync<NcrSourcePhotoDialog>("来源照片", parameters, options);
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
                    DisplayName = DictValueDisplayHelper.GetText(DictValueDefaults.NcrResponsibilityKey, k) ?? k,
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

    // ========== 处置方式字典 ==========

    /// <summary>加载处置方式字典下拉（配置表动态，失败/空兜底内置 8 值）</summary>
    private async Task LoadDisposalOptionsAsync()
    {
        var result = await DictValueDefinitionService.GetEnabledValuesAsync(DictValueDefaults.NcrDisposalKey);
        if (result.Success && result.Data is { Count: > 0 })
        {
            _disposalOptions = result.Data;
        }
        else
        {
            _disposalOptions = NcrDisposalKeys.All
                .Select(k => new DictValueInfoDto
                {
                    Value = k,
                    DisplayName = DictValueDisplayHelper.GetText(DictValueDefaults.NcrDisposalKey, k) ?? k,
                    DisplayOrder = 0,
                    IsEnabled = true
                })
                .ToList();
        }
    }

    /// <summary>编辑加载后兜底：当前处置方式不在选项集合（字典禁用/缺失/异步未达）时补入中文项，杜绝英文直显</summary>
    private void EnsureSelectedDisposalMapped()
    {
        var selected = _formData.DisposalMethod;
        if (string.IsNullOrWhiteSpace(selected)) return;
        if (_disposalOptions.Any(o => string.Equals(o.Value, selected, StringComparison.OrdinalIgnoreCase))) return;
        _disposalOptions.Insert(0, new DictValueInfoDto
        {
            Value = selected,
            DisplayName = NcrDisposalKeys.ToChinese(selected)
                ?? DictValueDisplayHelper.GetText(DictValueDefaults.NcrDisposalKey, selected)
                ?? selected,
            DisplayOrder = -1,
            IsEnabled = true
        });
    }

    /// <summary>
    /// 新增处置方式：旁侧输入中文名 → 生成 NcrDM_n 英文 Key → 写入字典配置 → 刷新下拉并选中。
    /// </summary>
    private async Task AddDisposalAsync()
    {
        var name = _newDisposalName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            Snackbar.Add("请输入要新增的处置方式", Severity.Warning);
            return;
        }
        if (!name.Any(c => c >= 0x4E00 && c <= 0x9FFF))
        {
            Snackbar.Add("处置方式必须包含汉字", Severity.Warning);
            return;
        }
        if (_disposalOptions.Any(o => string.Equals(o.DisplayName, name, StringComparison.Ordinal)))
        {
            Snackbar.Add($"处置方式「{name}」已存在", Severity.Warning);
            return;
        }

        // 生成 NcrDM_{n}：取现有 NcrDM_ 前缀最大序号 + 1，首增 n=1
        var maxSeq = _disposalOptions
            .Select(o => o.Value.StartsWith("NcrDM_", StringComparison.Ordinal) && int.TryParse(o.Value["NcrDM_".Length..], out var seq) ? seq : 0)
            .DefaultIfEmpty(0)
            .Max();
        var key = $"NcrDM_{maxSeq + 1}";

        var result = await DictValueDefinitionService.SaveAsync(new DictValueDefinitionDto
        {
            Id = 0,
            DictKey = DictValueDefaults.NcrDisposalKey,
            Value = key,
            DisplayName = name,
            DisplayOrder = 999,
            IsEnabled = true
        });
        if (result.Success)
        {
            Snackbar.Add($"已添加处置方式「{name}」", Severity.Success);
            _newDisposalName = "";
            await LoadDisposalOptionsAsync();
            _formData.DisposalMethod = key;
        }
        else
        {
            Snackbar.Add($"添加失败: {result.Message}", Severity.Error);
        }
    }

    private Task Save() => SaveInternal(null);

    /// <summary>
    /// 「忽略」：走正常登记流程保存一张 状态=忽略 的不合格报告。
    /// 该条待处理记录因已有对应 NCR（SourceGroupKey 命中）不再出现在待处理列表。
    /// </summary>
    private async Task Ignore()
    {
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("确认忽略",
            new DialogParameters
            {
                ["ContentText"] = "确定忽略本待处理记录？将按正常流程登记一张「忽略」状态的不合格报告，该记录不再出现在待处理列表。",
                ["ConfirmText"] = "忽略"
            });
        if ((await dialog.Result).Canceled) return;

        await SaveInternal(NcrStatus.Ignored);
    }

    private async Task SaveInternal(NcrStatus? status)
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
                _formData.Status = status;
                var result = await NcrService.CreateAsync(_formData);
                if (result.Success)
                {
                    Snackbar.Add(status == NcrStatus.Ignored ? "已忽略（已登记忽略状态不合格报告）" : "创建成功", Severity.Success);
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

        // 主动（不合格反馈/人工上报）无让步放行概念，字段未渲染即视为不填
        if (!_isPassiveSource)
        {
            _formData.FlowDirection = null;
            _formData.ConcessionQuantity = null;
            _formData.ConcessionWeight = null;
            _formData.ConcessionRemark = null;
            _formData.SourceGroupKey = null;
        }
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

    /// <summary>待处理选择器分两组：正常提交（不合格反馈）/ 超阈值遗漏（检验数据反查）</summary>
    private List<(string Title, List<NcrPendingCheckDto> Items)> PendingGroups => new()
    {
        ("待处理批次(正常提交)", _pendingItems.Where(i => i.Bucket == NcrPendingBucket.NormalSubmitted).ToList()),
        ("待处理批次(超阈值遗漏)", _pendingItems.Where(i => i.Bucket == NcrPendingBucket.OverageMissing).ToList())
    };

    /// <summary>组内流向明细（仅被动「超阈值遗漏」组）：如「返整2/入在制库3/让步6」</summary>
    private static string GetFlowDetailText(NcrPendingCheckDto item)
    {
        if (item.Bucket != NcrPendingBucket.OverageMissing) return "";
        var parts = new List<string>();
        if (item.ReworkQuantity > 0) parts.Add($"{DisplayHelper.GetFlowDirectionText(FlowDirection.Rework)}{item.ReworkQuantity}");
        if (item.InProcessWarehouseQuantity > 0) parts.Add($"{DisplayHelper.GetFlowDirectionText(FlowDirection.InProcessWarehouse)}{item.InProcessWarehouseQuantity}");
        if (item.FinishedWarehouseQuantity > 0) parts.Add($"{DisplayHelper.GetFlowDirectionText(FlowDirection.FinishedWarehouse)}{item.FinishedWarehouseQuantity}");
        if (item.ScrapQuantity > 0) parts.Add($"{DisplayHelper.GetFlowDirectionText(FlowDirection.Scrap)}{item.ScrapQuantity}");
        if (item.ReturnQuantity > 0) parts.Add($"{DisplayHelper.GetFlowDirectionText(FlowDirection.Return)}{item.ReturnQuantity}");
        if (item.ConcessionQuantity > 0) parts.Add($"让步{item.ConcessionQuantity}");
        return string.Join("/", parts);
    }

    // ========== 枚举选项 ==========

    private string GetStatusText(NcrStatus status) => DisplayHelper.GetNcrStatusText(status);

    private static string GetSourceTypeText(string sourceType) => EnumHelper.GetDisplayName<NcrPendingSourceType>(sourceType);

    /// <summary>流向中文（枚举 5 档，检验记录带出的物料实际去向）</summary>
    private static string GetFlowDirectionText(FlowDirection? direction)
        => direction.HasValue ? DisplayHelper.GetFlowDirectionText(direction.Value) : "";

    /// <summary>处置方式中文（字典 NcrDisposalKey，含用户自定义档；优先取下拉选项显示名）</summary>
    private string GetDisposalText(string? disposal)
        => string.IsNullOrEmpty(disposal)
            ? ""
            : (_disposalOptions.FirstOrDefault(o => string.Equals(o.Value, disposal, StringComparison.OrdinalIgnoreCase))?.DisplayName
               ?? NcrDisposalKeys.ToChinese(disposal)
               ?? disposal);

    private static Color GetWarningColor() => Color.Warning;

    private static Color GetSourceTypeColor(string sourceType)
        => sourceType == nameof(NcrPendingSourceType.ProcessInspection) ? Color.Info
         : sourceType == nameof(NcrPendingSourceType.NonconformingFeedback) ? Color.Warning
         : Color.Primary;

    private static Color GetFlowDirectionChipColor(FlowDirection? direction) => direction switch
    {
        FlowDirection.Rework => Color.Warning,
        FlowDirection.InProcessWarehouse => Color.Info,
        FlowDirection.FinishedWarehouse => Color.Primary,
        FlowDirection.Scrap => Color.Error,
        FlowDirection.Return => Color.Secondary,
        _ => Color.Default
    };

    /// <summary>处置方式 chip 配色（字典 8 档内置键；自定义档回退 Default）</summary>
    private static Color GetDisposalChipColor(string? disposal) => disposal switch
    {
        NcrDisposalKeys.Concession => Color.Success,
        NcrDisposalKeys.Reprocess => Color.Warning,
        NcrDisposalKeys.Rework => Color.Warning,
        NcrDisposalKeys.InProcessWarehouse => Color.Info,
        NcrDisposalKeys.FinishedWarehouse => Color.Primary,
        NcrDisposalKeys.ScrapCorrection => Color.Error,
        NcrDisposalKeys.Scrap => Color.Error,
        NcrDisposalKeys.Return => Color.Secondary,
        _ => Color.Default
    };

}
