using MES.Core.Enums;

namespace MES.Data.Entities.Quality;

/// <summary>
/// NCR 不合格品报告 — 不合格品反馈、处置、分析、追责、纠正预防闭环
/// </summary>
public class Ncr : BaseEntity
{
    // ========== G1: 问题反馈 ==========

    /// <summary>反馈日期</summary>
    public DateTime ReportDate { get; set; }

    /// <summary>反馈部门</summary>
    public string? ReportDepartment { get; set; }

    /// <summary>反馈人</summary>
    public string? Reporter { get; set; }

    /// <summary>钢管类别</summary>
    public MaterialType PipeCategory { get; set; }

    /// <summary>生产编号（用户输入）</summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>工单号（从批次冗余）</summary>
    public string? WorkOrderNo { get; set; }

    /// <summary>主号（从批次冗余）</summary>
    public string? ProductionMainNo { get; set; }

    /// <summary>厂内牌号（从批次冗余）</summary>
    public string? PlantGrade { get; set; }

    /// <summary>规格（从批次冗余）</summary>
    public string? Specification { get; set; }

    /// <summary>不合格支数</summary>
    public int? DefectiveQuantity { get; set; }

    /// <summary>问题描述</summary>
    public string? ProblemDescription { get; set; }

    /// <summary>来源检验项目（卡片排重用）</summary>
    public string? SourceInspectionItem { get; set; }

    // ========== G2: 不合格品处置 ==========

    /// <summary>处置方式（返整/入库/报废）</summary>
    public DisposalMethod? DisposalMethod { get; set; }

    /// <summary>处置备注</summary>
    public string? DisposalRemark { get; set; }

    /// <summary>处置是否完结</summary>
    public bool DisposalIsCompleted { get; set; }

    /// <summary>处置完结日期</summary>
    public DateTime? DisposalCompleteDate { get; set; }

    // ========== G3: 原因分析 ==========

    /// <summary>原因分析</summary>
    public string? RootCauseAnalysis { get; set; }

    /// <summary>事故严重程度</summary>
    public SeverityLevel? Severity { get; set; }

    /// <summary>分析确认人</summary>
    public string? AnalysisConfirmer { get; set; }

    /// <summary>确认日期</summary>
    public DateTime? AnalysisConfirmDate { get; set; }

    // ========== G4: 责任人及处理 ==========

    /// <summary>责任类别</summary>
    public ResponsibilityCategory? ResponsibilityCategory { get; set; }

    /// <summary>责任部门</summary>
    public string? ResponsibleDept { get; set; }

    /// <summary>生产操作日期</summary>
    public DateTime? OperationDate { get; set; }

    /// <summary>生产责任人</summary>
    public string? ResponsiblePerson { get; set; }

    /// <summary>对责任人的处理</summary>
    public string? PersonDisposition { get; set; }

    /// <summary>责任人处理是否完结</summary>
    public bool PersonIsCompleted { get; set; }

    /// <summary>责任人处理完结日期</summary>
    public DateTime? PersonCompleteDate { get; set; }

    // ========== G5: 纠正预防措施及结果验证 ==========

    /// <summary>纠正预防措施</summary>
    public string? CorrectiveAction { get; set; }

    /// <summary>计划人</summary>
    public string? ActionPlanner { get; set; }

    /// <summary>计划日期</summary>
    public DateTime? ActionPlanDate { get; set; }

    /// <summary>验证人</summary>
    public string? ActionVerifier { get; set; }

    /// <summary>验证日期</summary>
    public DateTime? ActionVerifyDate { get; set; }

    /// <summary>结果判定（文字描述）</summary>
    public string? ActionResult { get; set; }

    /// <summary>验证结论（通过/需整改/不适用）</summary>
    public VerifyResult? VerifyResult { get; set; }

    // ========== 状态 ==========

    /// <summary>
    /// NCR 状态（处理中/已关闭），登记即处理中
    /// </summary>
    public NcrStatus Status { get; set; } = NcrStatus.Processing;
}
