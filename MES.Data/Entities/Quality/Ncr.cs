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

    /// <summary>厂内牌号（从批次冗余）</summary>
    public string? PlantGrade { get; set; }

    /// <summary>规格（从批次冗余）</summary>
    public string? Specification { get; set; }

    /// <summary>次品支数</summary>
    public int? DefectiveQuantity { get; set; }

    /// <summary>次品重量(kg，整数)</summary>
    public int? DefectiveWeight { get; set; }

    /// <summary>问题描述</summary>
    public string? ProblemDescription { get; set; }

    /// <summary>
    /// 让步放行支数（仅被动来源：过程检验/成品检验超阈值；人工上报来源无此概念，为空）。
    /// 与 <see cref="DefectiveQuantity"/>（次品支数 = 各流向支数合计）是并列的两个维度，不重叠。
    /// </summary>
    public int? ConcessionQuantity { get; set; }

    /// <summary>让步放行重量(kg，整数)。检验记录无此数据来源，由质量负责人手工填写</summary>
    public int? ConcessionWeight { get; set; }

    /// <summary>让步说明（由检验记录的 ConcessionRemark 带出，可修改）</summary>
    public string? ConcessionRemark { get; set; }

    /// <summary>
    /// 来源不合格反馈单ID（可空）。空 = 人工录入或由检验数据阈值触发创建；
    /// 有值 = 由该不合格反馈单生成，该反馈单随即视为「已处理」（不再出现在待处理列表）。
    /// </summary>
    public int? NonconformingFeedbackId { get; set; }

    /// <summary>来源检验项目（卡片排重用）</summary>
    public string? SourceInspectionItem { get; set; }

    /// <summary>
    /// 来源「待处理组」定位键（仅被动来源：过程检验/成品检验超阈值建单时写入）。
    /// 格式 = <c>{来源类型}|{生产批次Id}|{工序}|{成检类型}|{检验项目}</c>，不适用段留空。
    /// 用于「待处理批次（超阈值遗漏）」去重：同组已建单则不再重复列出；人工上报来源为空。
    /// </summary>
    public string? SourceGroupKey { get; set; }

    /// <summary>
    /// 不合格流向（检验记录带出的物料实际去向）。
    /// 有值 = 由过程检验/成品检验建档时带出该记录的不合格流向；空 = 不合格反馈来源或人工录入。
    /// 与<see cref="DisposalMethod"/>（处置方式，人工判定）是两个不同概念。
    /// </summary>
    public FlowDirection? FlowDirection { get; set; }

    // ========== G2: 不合格品处置 ==========

    /// <summary>处置方式（字典 NcrDisposalKey 英文 Key，8 档：让步放行/返工/返整(新卡流转)/入在制库(可改制)/可入备库(尺寸偏差)/入次品库(修正改制)/入次品库(报废)/退货）</summary>
    public string? DisposalMethod { get; set; }

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

    /// <summary>责任类别（字典 NcrResponsibilityKey 英文 Key）</summary>
    public string? ResponsibilityCategory { get; set; }

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
