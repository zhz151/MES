using MES.Core.Enums;
using MES.Data.Entities.Batch;

namespace MES.Data.Entities.Quality;

/// <summary>
/// 不合格反馈单 — 只登记问题（谁、何时、哪道工序工段、多少不合格、什么问题 + 问题照片），
/// <b>不带任何处理状态</b>：处置方式与处置闭环全部由质量负责人在「不合格报告（Ncr）」中完成，
/// 本单是否「已处理」由「是否已生成 Ncr」（<see cref="Ncr.NonconformingFeedbackId"/>）判定。
/// 记录模式对齐生产记录（批次冗余 + 工序/工段 + 数量）。
/// </summary>
public class NonconformingFeedback : BaseEntity
{
    // ========== G1: 反馈信息 ==========

    /// <summary>
    /// 反馈日期
    /// </summary>
    public DateTime ReportDate { get; set; }

    /// <summary>
    /// 反馈人
    /// </summary>
    public string Reporter { get; set; } = null!;

    /// <summary>
    /// 数据来源（SCAN=扫码报工，MANUAL=手动录入），默认 MANUAL
    /// </summary>
    public string? DataSource { get; set; }

    /// <summary>
    /// 来源类型（<see cref="NonconformingFeedbackSourceType"/> 枚举名）：
    /// ProductionSection=生产工段 / ProcessInspection=过程检验 / FinalInspection=成品检验。
    /// 决定位置信息取「工序+工段」还是「工序+检验项目」。
    /// </summary>
    public string SourceType { get; set; } = null!;

    // ========== G2: 位置信息 ==========

    /// <summary>
    /// 关联生产批次ID
    /// </summary>
    public int ProductionBatchId { get; set; }

    /// <summary>
    /// 生产编号
    /// </summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>
    /// 工单号（从批次冗余）
    /// </summary>
    public string? WorkOrderNo { get; set; }

    /// <summary>
    /// 关联工序组ID
    /// </summary>
    public int ProcessGroupId { get; set; }

    /// <summary>
    /// 工序名称（从工序组冗余，存英文 Key）
    /// </summary>
    public string ProcessName { get; set; } = null!;

    /// <summary>
    /// 制造规格（从工序组冗余）
    /// </summary>
    public string? ManufacturingSpec { get; set; }

    /// <summary>
    /// 工段名称（如"冷轧拔""矫直"，存英文 Key）。
    /// 生产工段/过程检验来源必填；成品检验来源无工段概念，为 null。
    /// </summary>
    public string? SectionName { get; set; }

    /// <summary>
    /// 执行序号（来自工序组中该工段的顺序值）。随 <see cref="SectionName"/> 同空同有。
    /// </summary>
    public int? SequenceNumber { get; set; }

    /// <summary>
    /// 检验项目（<see cref="InspectionItem"/>）：仅成品检验来源填写，其余来源为 null。
    /// </summary>
    public InspectionItem? InspectionItem { get; set; }

    /// <summary>
    /// 产类（荒管/在制/成品，系统自动计算，存英文 Key）
    /// </summary>
    public string? ProductStatus { get; set; }

    /// <summary>
    /// 工厂牌号（从批次冗余）
    /// </summary>
    public string? PlantGrade { get; set; }

    // ========== G3: 数量信息 ==========

    /// <summary>
    /// 来料支数（不合格率分母）
    /// </summary>
    public int? IncomingQuantity { get; set; }

    /// <summary>
    /// 来料重量(kg)
    /// </summary>
    public decimal? IncomingWeight { get; set; }

    /// <summary>
    /// 不合格支数
    /// </summary>
    public int? DefectQuantity { get; set; }

    /// <summary>
    /// 不合格重量(kg) = 来料重量 / 来料支数 × 不合格支数，四舍五入取整；可手工修改
    /// </summary>
    public int? DefectWeight { get; set; }

    // ========== G4: 问题信息 ==========

    /// <summary>
    /// 问题描述
    /// </summary>
    public string? ProblemDescription { get; set; }

    // ========== 导航属性 ==========

    /// <summary>
    /// 所属生产批次
    /// </summary>
    public ProductionBatch ProductionBatch { get; set; } = null!;

    /// <summary>
    /// 所属工序组
    /// </summary>
    public ProcessGroup ProcessGroup { get; set; } = null!;

    /// <summary>
    /// 问题照片附件
    /// </summary>
    public List<NonconformingFeedbackAttachment> Attachments { get; set; } = new();
}
