using MES.Core.Enums;

namespace MES.Core.DTOs.Quality;

/// <summary>
/// NCR 待处理批次卡片 DTO。
/// <para>
/// 一行 = 一条<b>待处理检验记录</b>（2026-09-13 起为记录级）：<b>超阈值遗漏</b>行逐条取过程检验 / 成品检验记录，
/// 该条记录自身的不合格合计（让步放行支 + 各流向支）严格大于绝对支数阈值与占比阈值时列出；
/// <b>正常提交</b>行来自不合格反馈单（人工上报，不受阈值约束，凡未生成 NCR 即无条件列出）。
/// </para>
/// </summary>
public class NcrPendingCheckDto
{
    /// <summary>生产编号</summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>
    /// 生产批次 Id（服务端按 BatchNo 反查 ProductionBatch 回填；0=未匹配到批次，前端降级为纯文本不渲染链接）
    /// </summary>
    public int ProductionBatchId { get; set; }

    /// <summary>工单号</summary>
    public string? WorkOrderNo { get; set; }

    /// <summary>牌号</summary>
    public string? PlantGrade { get; set; }

    /// <summary>规格（取自检验记录）</summary>
    public string? Specification { get; set; }

    /// <summary>检验日期（取检验记录的检验日期）</summary>
    public DateTime ReportDate { get; set; }

    /// <summary>
    /// 来源类型（<see cref="NcrPendingSourceType"/> 枚举名）：
    /// ProcessInspection=过程检验 / FinalInspection=成品检验 / NonconformingFeedback=不合格反馈
    /// </summary>
    public string SourceType { get; set; } = null!;

    /// <summary>
    /// 分组（<see cref="NcrPendingBucket"/> 枚举名）：
    /// NormalSubmitted=正常提交（人工上报） / OverageMissing=超阈值遗漏（系统反查）
    /// </summary>
    public NcrPendingBucket Bucket { get; set; }

    /// <summary>
    /// 来源不合格反馈单 Id（仅 SourceType=NonconformingFeedback 时有值）；
    /// 前端据此在创建 NCR 时回传，服务端写入 <c>Ncr.NonconformingFeedbackId</c> 完成闭环。
    /// </summary>
    public int? NonconformingFeedbackId { get; set; }

    /// <summary>
    /// 来源检验记录 Id（仅 SourceType=ProcessInspection/FinalInspection 时有值）：建单单位=单条检验记录，
    /// 其照片入口按此 Id 精确取该条记录的照片（一对一）。
    /// </summary>
    public int? InspectionRecordId { get; set; }

    /// <summary>检验项目（成品检验来源必填；其余来源可空）</summary>
    public string? InspectionItem { get; set; }

    /// <summary>成检类型（<see cref="InspectionType"/> 枚举名，仅成品检验来源有值：预检/终检）</summary>
    public string? InspectionType { get; set; }

    /// <summary>工序名称（过程检验来源必填，存英文 Key；其余来源可空）</summary>
    public string? ProcessName { get; set; }

    /// <summary>
    /// 记录定位键（仅「超阈值遗漏」行有值，格式 <c>{来源类型}|{批次Id}|{工序}|{成检类型}|{检验项目}|{检验记录Id}</c>）：
    /// 建单时原样回传，服务端写入 <c>Ncr.SourceGroupKey</c>，保证该条检验记录建单后不再重复列出；
    /// 照片入口亦按此键定位该条记录的照片。
    /// </summary>
    public string? GroupKey { get; set; }

    /// <summary>工段名称（过程检验来源与不合格反馈「生产工段/过程检验」来源有值，存英文 Key）</summary>
    public string? SectionName { get; set; }

    /// <summary>物料名称（成品检验用，对应钢管类别）</summary>
    public string? MaterialName { get; set; }

    /// <summary>检验员（→反馈人）</summary>
    public string? Inspector { get; set; }

    /// <summary>次品情况描述（→问题描述）</summary>
    public string? DefectDescription { get; set; }

    /// <summary>
    /// 次品流向（组内支数最多的流向；并列时按 返整 &gt; 入在制库 &gt; 可入备库 &gt; 入次品库 &gt; 退货 定序取首）。
    /// 空 = 来源为不合格反馈（人工上报不预设流向，且其 NCR 处置方式由质量负责人另行判定）。
    /// </summary>
    public FlowDirection? FlowDirection { get; set; }

    // ===== 该条记录的不合格明细（5 档流向支数，供前端展开「返整2/报废3/让步6」）=====

    /// <summary>该条记录返整支数</summary>
    public int ReworkQuantity { get; set; }

    /// <summary>该条记录入在制库支数</summary>
    public int InProcessWarehouseQuantity { get; set; }

    /// <summary>该条记录可入备库支数（仅成品检验来源有此流向）</summary>
    public int FinishedWarehouseQuantity { get; set; }

    /// <summary>该条记录入次品库支数</summary>
    public int ScrapQuantity { get; set; }

    /// <summary>该条记录退货支数</summary>
    public int ReturnQuantity { get; set; }

    /// <summary>该条记录让步放行支数（计入不合格合计，但不单独成行）</summary>
    public int ConcessionQuantity { get; set; }

    /// <summary>让步说明（取该条记录的 ConcessionRemark）</summary>
    public string? ConcessionRemark { get; set; }

    /// <summary>次品支数（= 各档流向支数合计，不含让步放行）</summary>
    public int DefectQuantity { get; set; }

    /// <summary>次品重量(kg；成品检验取实际重量、过程检验取理论重量)</summary>
    public int? DefectiveWeight { get; set; }

    /// <summary>总检验支数（占比分母）</summary>
    public int TotalQuantity { get; set; }

    /// <summary>不合格占比（百分比，如 8.5 表示 8.5%；分子 = 次品支数 + 让步放行支）</summary>
    public decimal Percentage { get; set; }
}
