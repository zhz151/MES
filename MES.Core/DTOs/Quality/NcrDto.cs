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
    public string BatchNo { get; set; } = null!;
    public string? WorkOrderNo { get; set; }

    /// <summary>
    /// 生产批次 Id（服务端按 BatchNo 反查 ProductionBatch 回填；0=未匹配到批次，前端降级为纯文本不渲染链接）
    /// </summary>
    public int ProductionBatchId { get; set; }

    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }
    public int? DefectiveQuantity { get; set; }
    public int? DefectiveWeight { get; set; }
    public string? ProblemDescription { get; set; }

    /// <summary>让步放行支数（仅被动来源：过程检验/成品检验超阈值；人工上报来源为空）</summary>
    public int? ConcessionQuantity { get; set; }

    /// <summary>让步放行重量(kg，整数；手工填写)</summary>
    public int? ConcessionWeight { get; set; }

    /// <summary>让步说明</summary>
    public string? ConcessionRemark { get; set; }

    /// <summary>来源检验项目（卡片排重用）</summary>
    public string? SourceInspectionItem { get; set; }

    /// <summary>来源待处理组定位键（被动来源建单时写入，用于待处理批次去重）</summary>
    public string? SourceGroupKey { get; set; }

    /// <summary>次品流向（检验记录带出；不合格反馈来源为空）</summary>
    public FlowDirection? FlowDirection { get; set; }

    /// <summary>
    /// 来源不合格反馈单 Id（可空）。有值 = 本单由该不合格反馈单生成，
    /// 该反馈单随即视为「已处理」（不再出现在待处理列表）。
    /// </summary>
    public int? NonconformingFeedbackId { get; set; }

    // G2: 不合格品处置
    /// <summary>处置方式（字典 NcrDisposalKey 英文 Key）</summary>
    public string? DisposalMethod { get; set; }
    public string? DisposalRemark { get; set; }
    public bool DisposalIsCompleted { get; set; }
    public DateTime? DisposalCompleteDate { get; set; }

    // G3: 原因分析
    public string? RootCauseAnalysis { get; set; }
    public SeverityLevel? Severity { get; set; }
    public string? AnalysisConfirmer { get; set; }
    public DateTime? AnalysisConfirmDate { get; set; }

    // G4: 责任人及处理
    public string? ResponsibilityCategory { get; set; }
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

    // 状态
    public NcrStatus Status { get; set; }

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
    public int? DefectiveWeight { get; set; }
    public string? ProblemDescription { get; set; }

    /// <summary>让步放行支数（由「待处理批次」卡片带出：组内合格中让步放行支合计；人工上报来源为空）</summary>
    public int? ConcessionQuantity { get; set; }

    /// <summary>让步放行重量(kg，整数；检验记录无此数据来源，手工填写)</summary>
    public int? ConcessionWeight { get; set; }

    /// <summary>让步说明（由检验记录的 ConcessionRemark 带出）</summary>
    public string? ConcessionRemark { get; set; }

    public string? SourceInspectionItem { get; set; }

    /// <summary>来源待处理组定位键（被动来源由「待处理批次」行带出，用于后续去重；人工上报来源留空）</summary>
    public string? SourceGroupKey { get; set; }

    /// <summary>次品流向（由「待处理批次」卡片带出：组内支数最多的流向；不合格反馈来源为空）</summary>
    public FlowDirection? FlowDirection { get; set; }

    /// <summary>来源不合格反馈单 Id（由「待处理批次」中的不合格反馈行创建时回传）</summary>
    public int? NonconformingFeedbackId { get; set; }

    /// <summary>登记状态（仅允许 Pending / Ignored；留空按 Pending；用于「忽略」动作登记即忽略）</summary>
    public NcrStatus? Status { get; set; }

    // G2: 不合格品处置
    /// <summary>处置方式（字典 NcrDisposalKey 英文 Key，人工判定）</summary>
    public string? DisposalMethod { get; set; }
    public string? DisposalRemark { get; set; }
    public bool DisposalIsCompleted { get; set; }
    public DateTime? DisposalCompleteDate { get; set; }

    // G3: 原因分析
    public string? RootCauseAnalysis { get; set; }
    public SeverityLevel? Severity { get; set; }
    public string? AnalysisConfirmer { get; set; }
    public DateTime? AnalysisConfirmDate { get; set; }

    // G4: 责任人及处理
    public string? ResponsibilityCategory { get; set; }
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
    public int? DefectiveWeight { get; set; }
    public string? ProblemDescription { get; set; }

    /// <summary>让步放行支数（仅被动来源有值）</summary>
    public int? ConcessionQuantity { get; set; }

    /// <summary>让步放行重量(kg，整数)</summary>
    public int? ConcessionWeight { get; set; }

    /// <summary>让步说明</summary>
    public string? ConcessionRemark { get; set; }

    public string? SourceInspectionItem { get; set; }

    // G2: 不合格品处置
    /// <summary>处置方式（字典 NcrDisposalKey 英文 Key；流向为只读字段，编辑时不可改）</summary>
    public string? DisposalMethod { get; set; }
    public string? DisposalRemark { get; set; }
    public bool DisposalIsCompleted { get; set; }
    public DateTime? DisposalCompleteDate { get; set; }

    // G3: 原因分析
    public string? RootCauseAnalysis { get; set; }
    public SeverityLevel? Severity { get; set; }
    public string? AnalysisConfirmer { get; set; }
    public DateTime? AnalysisConfirmDate { get; set; }

    // G4: 责任人及处理
    public string? ResponsibilityCategory { get; set; }
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
    /// <summary>批次主键（建单页「生产编号」尾部图标据此打开「批次执行进度」卡片；未匹配到批次时为 0）</summary>
    public int ProductionBatchId { get; set; }

    public string? WorkOrderNo { get; set; }
    public string? SalesOrderNo { get; set; }
    public string? TagNo { get; set; }
    public string? PlantGrade { get; set; }
    public string? Specification { get; set; }

    /// <summary>该批次检验记录的次品支数合计（手动输入生产编号时自动填入）</summary>
    public int? DefectiveQuantity { get; set; }

    /// <summary>该批次检验记录的次品重量合计（kg；过程检验取理论重量、成品检验取实际重量）</summary>
    public int? DefectiveWeight { get; set; }
}

/// <summary>
/// NCR 来源照片分组：一条检验记录（或一张不合格反馈单）为一组。
/// 建单/编辑页「生产编号」旁的照片入口据此弹窗展示（只读）；无照片的记录不产生分组。
/// </summary>
public class NcrSourcePhotoGroupDto
{
    /// <summary>
    /// 来源类型（<see cref="MES.Core.Enums.NcrPendingSourceType"/> 枚举名）：
    /// ProcessInspection / FinalInspection / NonconformingFeedback
    /// </summary>
    public string Kind { get; set; } = null!;

    /// <summary>来源记录主键（检验记录 Id / 不合格反馈单 Id；前端据此下载照片字节）</summary>
    public int RecordId { get; set; }

    /// <summary>记录日期（检验日期 / 反馈日期；前端拼分组标题用）</summary>
    public DateTime? RecordDate { get; set; }

    /// <summary>检验员（不合格反馈来源为空）</summary>
    public string? Inspector { get; set; }

    /// <summary>该记录的照片（已按排序号排序）</summary>
    public List<NcrSourcePhotoDto> Photos { get; set; } = new();
}

/// <summary>NCR 来源照片（仅元数据；字节由前端按记录 Id + 附件 Id 走各模块既有附件接口下载）</summary>
public class NcrSourcePhotoDto
{
    public int AttachmentId { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
}
