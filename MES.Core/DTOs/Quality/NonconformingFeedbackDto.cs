using System.ComponentModel.DataAnnotations;
using MES.Core.Enums;

namespace MES.Core.DTOs.Quality;

// ========== 不合格反馈单 ==========

/// <summary>
/// 不合格反馈单 DTO
/// </summary>
public class NonconformingFeedbackDto
{
    public int Id { get; set; }

    // ---------- G1: 反馈信息 ----------

    /// <summary>反馈日期</summary>
    public DateTime ReportDate { get; set; }

    /// <summary>反馈人</summary>
    public string Reporter { get; set; } = null!;

    /// <summary>数据来源（SCAN/MANUAL）</summary>
    public string? DataSource { get; set; }

    /// <summary>来源类型（枚举名：ProductionSection=生产工段 / ProcessInspection=过程检验 / FinalInspection=成品检验）</summary>
    public string SourceType { get; set; } = null!;

    // ---------- G2: 位置信息 ----------

    public int ProductionBatchId { get; set; }

    /// <summary>生产编号</summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>工单号（批次冗余）</summary>
    public string? WorkOrderNo { get; set; }

    public int ProcessGroupId { get; set; }

    /// <summary>工序名称（英文 Key）</summary>
    public string ProcessName { get; set; } = null!;

    /// <summary>制造规格</summary>
    public string? ManufacturingSpec { get; set; }

    /// <summary>工段名称（英文 Key）。成品检验来源为空（无工段概念）</summary>
    public string? SectionName { get; set; }

    /// <summary>执行序号（随工段同空同有）</summary>
    public int? SequenceNumber { get; set; }

    /// <summary>检验项目（仅成品检验来源有值，其余来源为空）</summary>
    public InspectionItem? InspectionItem { get; set; }

    /// <summary>产类（英文 Key，系统自动计算）</summary>
    public string? ProductStatus { get; set; }

    /// <summary>工厂牌号</summary>
    public string? PlantGrade { get; set; }

    // ---------- G3: 数量信息 ----------

    /// <summary>来料支数</summary>
    public int? IncomingQuantity { get; set; }

    /// <summary>来料重量(kg)</summary>
    public decimal? IncomingWeight { get; set; }

    /// <summary>不合格支数</summary>
    public int? DefectQuantity { get; set; }

    /// <summary>不合格重量(kg)</summary>
    public int? DefectWeight { get; set; }

    // ---------- G4: 问题信息 ----------

    /// <summary>问题描述</summary>
    public string? ProblemDescription { get; set; }

    /// <summary>问题照片附件</summary>
    public List<NonconformingFeedbackAttachmentDto> Attachments { get; set; } = new();

    /// <summary>附件张数（列表页展示，详情页与 Attachments.Count 一致）</summary>
    public int AttachmentCount { get; set; }

    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 不合格反馈单附件 DTO
/// </summary>
public class NonconformingFeedbackAttachmentDto
{
    public int Id { get; set; }

    /// <summary>原始文件名</summary>
    public string FileName { get; set; } = null!;

    /// <summary>内容类型</summary>
    public string ContentType { get; set; } = null!;

    /// <summary>文件大小（字节）</summary>
    public long SizeBytes { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedTime { get; set; }
}

/// <summary>
/// 创建不合格反馈单请求
/// </summary>
public class CreateNonconformingFeedbackRequest
{
    [Required(ErrorMessage = "反馈日期不能为空")]
    public DateTime ReportDate { get; set; }

    /// <summary>
    /// 反馈人。⚠️ 不加 [Required]：扫码链（api/scan-quality）由服务端按登录账号覆写，客户端传值不作数，
    /// 加了会在覆写之前被 [ApiController] 模型校验拦下并报「反馈人不能为空」；
    /// 非空校验已下移至 NonconformingFeedbackService.CreateAsync（手工录入链路语义与文案不变）。
    /// </summary>
    [MaxLength(50)]
    public string Reporter { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? DataSource { get; set; }

    /// <summary>来源类型（枚举名）。生产工段/过程检验需填工段；成品检验需填检验项目</summary>
    [Required(ErrorMessage = "来源类型不能为空")]
    [MaxLength(30)]
    public string SourceType { get; set; } = string.Empty;

    [Required(ErrorMessage = "生产编号不能为空")]
    [MaxLength(50)]
    public string BatchNo { get; set; } = string.Empty;

    /// <summary>生产批次ID（可由 BatchNo 自动解析）</summary>
    public int ProductionBatchId { get; set; }

    /// <summary>工序组ID（传0则由服务端按 批次号+工序名+制造规格 解析）</summary>
    public int? ProcessGroupId { get; set; }

    [Required(ErrorMessage = "工序名称不能为空")]
    [MaxLength(50)]
    public string ProcessName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ManufacturingSpec { get; set; }

    /// <summary>工段名称（生产工段/过程检验档必填；成品检验档忽略）</summary>
    [MaxLength(50)]
    public string? SectionName { get; set; }

    /// <summary>执行序号（传0或留空则由服务端从工序组解析）</summary>
    public int? SequenceNumber { get; set; }

    /// <summary>检验项目（成品检验档必填；其余档忽略）</summary>
    public InspectionItem? InspectionItem { get; set; }

    public int? IncomingQuantity { get; set; }

    public decimal? IncomingWeight { get; set; }

    public int? DefectQuantity { get; set; }

    /// <summary>不合格重量（留空则由服务端按支数比理论计算）</summary>
    public int? DefectWeight { get; set; }

    [MaxLength(500)]
    public string? ProblemDescription { get; set; }
}

/// <summary>
/// 更新不合格反馈单请求
/// </summary>
public class UpdateNonconformingFeedbackRequest
{
    [Required(ErrorMessage = "反馈日期不能为空")]
    public DateTime ReportDate { get; set; }

    [MaxLength(50)]
    public string? Reporter { get; set; }

    /// <summary>来源类型（枚举名，随全量提交覆盖）</summary>
    [Required(ErrorMessage = "来源类型不能为空")]
    [MaxLength(30)]
    public string SourceType { get; set; } = string.Empty;

    [Required(ErrorMessage = "生产编号不能为空")]
    [MaxLength(50)]
    public string BatchNo { get; set; } = string.Empty;

    /// <summary>生产批次ID（可由 BatchNo 自动解析）</summary>
    public int ProductionBatchId { get; set; }

    public int? ProcessGroupId { get; set; }

    [MaxLength(50)]
    public string? ProcessName { get; set; }

    [MaxLength(100)]
    public string? ManufacturingSpec { get; set; }

    /// <summary>工段名称（生产工段/过程检验档必填；成品检验档忽略并清空）</summary>
    [MaxLength(50)]
    public string? SectionName { get; set; }

    public int? SequenceNumber { get; set; }

    /// <summary>检验项目（成品检验档必填；其余档忽略并清空）</summary>
    public InspectionItem? InspectionItem { get; set; }

    public int? IncomingQuantity { get; set; }

    public decimal? IncomingWeight { get; set; }

    public int? DefectQuantity { get; set; }

    /// <summary>不合格重量（留空则由服务端按支数比理论计算）</summary>
    public int? DefectWeight { get; set; }

    [MaxLength(500)]
    public string? ProblemDescription { get; set; }
}
