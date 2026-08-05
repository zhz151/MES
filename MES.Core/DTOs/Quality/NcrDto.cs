using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Quality;

// ========== 不合格品报告 NCR ==========

/// <summary>
/// NCR 不合格品报告 DTO
/// </summary>
public class NcrDto
{
    public int Id { get; set; }

    // G1: 问题反馈
    public DateTime ReportDate { get; set; }
    public string? ReportDepartment { get; set; }
    public string? Reporter { get; set; }
    public MaterialType PipeCategory { get; set; }
    public string PipeCategoryDisplay => EnumHelper.GetDisplayName(PipeCategory);
    public string BatchNo { get; set; } = null!;
    public string? WorkOrderNo { get; set; }
    public string? ProductionMainNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public int? DefectiveQuantity { get; set; }
    public string? ProblemDescription { get; set; }

    /// <summary>来源检验项目（卡片排重用）</summary>
    public string? SourceInspectionItem { get; set; }

    // G2: 不合格品处置
    public DisposalMethod? DisposalMethod { get; set; }
    public string? DisposalMethodDisplay => DisposalMethod.HasValue ? EnumHelper.GetDisplayName(DisposalMethod.Value) : null;
    public string? DisposalRemark { get; set; }
    public bool DisposalIsCompleted { get; set; }
    public DateTime? DisposalCompleteDate { get; set; }

    // G3: 原因分析
    public string? RootCauseAnalysis { get; set; }
    public SeverityLevel? Severity { get; set; }
    public string? SeverityDisplay => Severity.HasValue ? EnumHelper.GetDisplayName(Severity.Value) : null;
    public string? AnalysisConfirmer { get; set; }
    public DateTime? AnalysisConfirmDate { get; set; }

    // G4: 责任人及处理
    public ResponsibilityCategory? ResponsibilityCategory { get; set; }
    public string? ResponsibilityCategoryDisplay => ResponsibilityCategory.HasValue ? EnumHelper.GetDisplayName(ResponsibilityCategory.Value) : null;
    public string? ResponsibleDept { get; set; }
    public DateTime? OperationDate { get; set; }
    public string? ResponsiblePerson { get; set; }
    public string? PersonDisposition { get; set; }
    public bool PersonIsCompleted { get; set; }
    public DateTime? PersonCompleteDate { get; set; }

    // G5: 纠正预防措施及结果验证
    public string? CorrectiveAction { get; set; }
    public string? ActionPlanner { get; set; }
    public DateTime? ActionPlanDate { get; set; }
    public string? ActionVerifier { get; set; }
    public DateTime? ActionVerifyDate { get; set; }
    public string? ActionResult { get; set; }
    public VerifyResult? VerifyResult { get; set; }
    public string? VerifyResultDisplay => VerifyResult.HasValue ? EnumHelper.GetDisplayName(VerifyResult.Value) : null;

    // 状态
    public NcrStatus Status { get; set; }
    public string StatusDisplay => EnumHelper.GetDisplayName(Status);

    // 审计
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 创建 NCR 请求
/// </summary>
public class CreateNcrRequest
{
    // G1: 问题反馈
    public DateTime ReportDate { get; set; }
    public string? ReportDepartment { get; set; }
    public string? Reporter { get; set; }
    public MaterialType PipeCategory { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public string? WorkOrderNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public int? DefectiveQuantity { get; set; }
    public string? ProblemDescription { get; set; }
    public string? SourceInspectionItem { get; set; }

    // G2: 不合格品处置
    public DisposalMethod? DisposalMethod { get; set; }
    public string? DisposalMethodDisplay => DisposalMethod.HasValue ? EnumHelper.GetDisplayName(DisposalMethod.Value) : null;
    public string? DisposalRemark { get; set; }
    public bool DisposalIsCompleted { get; set; }
    public DateTime? DisposalCompleteDate { get; set; }

    // G3: 原因分析
    public string? RootCauseAnalysis { get; set; }
    public SeverityLevel? Severity { get; set; }
    public string? SeverityDisplay => Severity.HasValue ? EnumHelper.GetDisplayName(Severity.Value) : null;
    public string? AnalysisConfirmer { get; set; }
    public DateTime? AnalysisConfirmDate { get; set; }

    // G4: 责任人及处理
    public ResponsibilityCategory? ResponsibilityCategory { get; set; }
    public string? ResponsibilityCategoryDisplay => ResponsibilityCategory.HasValue ? EnumHelper.GetDisplayName(ResponsibilityCategory.Value) : null;
    public string? ResponsibleDept { get; set; }
    public DateTime? OperationDate { get; set; }
    public string? ResponsiblePerson { get; set; }
    public string? PersonDisposition { get; set; }
    public bool PersonIsCompleted { get; set; }
    public DateTime? PersonCompleteDate { get; set; }

    // G5: 纠正预防措施及结果验证
    public string? CorrectiveAction { get; set; }
    public string? ActionPlanner { get; set; }
    public DateTime? ActionPlanDate { get; set; }
    public string? ActionVerifier { get; set; }
    public DateTime? ActionVerifyDate { get; set; }
    public string? ActionResult { get; set; }
    public VerifyResult? VerifyResult { get; set; }
}

/// <summary>
/// 更新 NCR 请求（编辑页用）
/// </summary>
public class UpdateNcrRequest
{
    // G1: 问题反馈
    public DateTime ReportDate { get; set; }
    public string? ReportDepartment { get; set; }
    public string? Reporter { get; set; }
    public MaterialType PipeCategory { get; set; }
    public string? WorkOrderNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public int? DefectiveQuantity { get; set; }
    public string? ProblemDescription { get; set; }
    public string? SourceInspectionItem { get; set; }

    // G2: 不合格品处置
    public DisposalMethod? DisposalMethod { get; set; }
    public string? DisposalMethodDisplay => DisposalMethod.HasValue ? EnumHelper.GetDisplayName(DisposalMethod.Value) : null;
    public string? DisposalRemark { get; set; }
    public bool DisposalIsCompleted { get; set; }
    public DateTime? DisposalCompleteDate { get; set; }

    // G3: 原因分析
    public string? RootCauseAnalysis { get; set; }
    public SeverityLevel? Severity { get; set; }
    public string? SeverityDisplay => Severity.HasValue ? EnumHelper.GetDisplayName(Severity.Value) : null;
    public string? AnalysisConfirmer { get; set; }
    public DateTime? AnalysisConfirmDate { get; set; }

    // G4: 责任人及处理
    public ResponsibilityCategory? ResponsibilityCategory { get; set; }
    public string? ResponsibilityCategoryDisplay => ResponsibilityCategory.HasValue ? EnumHelper.GetDisplayName(ResponsibilityCategory.Value) : null;
    public string? ResponsibleDept { get; set; }
    public DateTime? OperationDate { get; set; }
    public string? ResponsiblePerson { get; set; }
    public string? PersonDisposition { get; set; }
    public bool PersonIsCompleted { get; set; }
    public DateTime? PersonCompleteDate { get; set; }

    // G5: 纠正预防措施及结果验证
    public string? CorrectiveAction { get; set; }
    public string? ActionPlanner { get; set; }
    public DateTime? ActionPlanDate { get; set; }
    public string? ActionVerifier { get; set; }
    public DateTime? ActionVerifyDate { get; set; }
    public string? ActionResult { get; set; }
    public VerifyResult? VerifyResult { get; set; }
}

/// <summary>
/// 状态变更请求
/// </summary>
public class UpdateNcrStatusRequest
{
    public NcrStatus Status { get; set; }
}

/// <summary>
/// 批次调取结果DTO（用于新建页自动填充）
/// </summary>
public class NcrLookupResultDto
{
    public string? WorkOrderNo { get; set; }
    public string? SalesOrderNo { get; set; }
    public string? ProductionMainNo { get; set; }
    public string? TagNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
}
