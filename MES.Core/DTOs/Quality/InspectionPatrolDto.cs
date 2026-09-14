using System.ComponentModel.DataAnnotations;

namespace MES.Core.DTOs.Quality;

// ========== 巡检单 ==========

/// <summary>
/// 巡检单 DTO
/// </summary>
public class InspectionPatrolDto
{
    public int Id { get; set; }

    // ---------- G1: 巡检信息 ----------

    /// <summary>巡检日期</summary>
    public DateTime PatrolDate { get; set; }

    /// <summary>巡检人</summary>
    public string Inspector { get; set; } = null!;

    /// <summary>数据来源（SCAN/MANUAL）</summary>
    public string? DataSource { get; set; }

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

    /// <summary>工段名称（英文 Key）</summary>
    public string SectionName { get; set; } = null!;

    public int SequenceNumber { get; set; }

    /// <summary>产类（英文 Key，系统自动计算）</summary>
    public string? ProductStatus { get; set; }

    /// <summary>工厂牌号</summary>
    public string? PlantGrade { get; set; }

    /// <summary>在产单位/车间</summary>
    public string? ProductionUnit { get; set; }

    /// <summary>在产设备名（手工录入，可留空）</summary>
    public string? EquipmentName { get; set; }

    /// <summary>在产操作人</summary>
    public string? ProductionOperator { get; set; }

    // ---------- G3: 巡检明细 ----------

    /// <summary>巡检明细（巡检项 / 巡检结果 / 备注）</summary>
    public List<InspectionPatrolItemDto> Items { get; set; } = new();

    /// <summary>巡检项条数（列表页展示，与 Items.Count 一致）</summary>
    public int ItemCount { get; set; }

    // ---------- G4: 整改闭环 ----------

    /// <summary>涉及整改</summary>
    public bool NeedRectification { get; set; }

    /// <summary>整改内容描述</summary>
    public string? RectificationDescription { get; set; }

    /// <summary>验证结果</summary>
    public string? VerificationResult { get; set; }

    /// <summary>整改人（第二次扫码补整改时的操作人）</summary>
    public string? RectificationOperator { get; set; }

    /// <summary>是否闭环</summary>
    public bool IsClosed { get; set; }

    // ---------- 附件 ----------

    /// <summary>照片附件（含类型）</summary>
    public List<InspectionPatrolAttachmentDto> Attachments { get; set; } = new();

    /// <summary>照片总张数（列表页展示）</summary>
    public int AttachmentCount { get; set; }

    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

/// <summary>
/// 巡检明细 DTO
/// </summary>
public class InspectionPatrolItemDto
{
    public int Id { get; set; }

    /// <summary>巡检项</summary>
    public string ItemName { get; set; } = null!;

    /// <summary>巡检结果（自由文本）</summary>
    public string? Result { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>
/// 巡检单附件 DTO
/// </summary>
public class InspectionPatrolAttachmentDto
{
    public int Id { get; set; }

    /// <summary>照片类型（Patrol=巡检照片 / Rectification=整改验证照片）</summary>
    public string PhotoType { get; set; } = null!;

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
/// 巡检明细请求（创建/更新共用，整组替换）
/// </summary>
public class InspectionPatrolItemRequest
{
    [Required(ErrorMessage = "巡检项不能为空")]
    [MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Result { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }
}

/// <summary>
/// 创建巡检单请求
/// </summary>
public class CreateInspectionPatrolRequest
{
    [Required(ErrorMessage = "巡检日期不能为空")]
    public DateTime PatrolDate { get; set; }

    /// <summary>
    /// 巡检人。⚠️ 不加 [Required]：扫码链（api/scan-quality）由服务端按登录账号覆写，客户端传值不作数，
    /// 加了会在覆写之前被 [ApiController] 模型校验拦下并报「巡检人不能为空」；
    /// 非空校验已下移至 InspectionPatrolService.CreateAsync（手工录入链路语义与文案不变）。
    /// </summary>
    [MaxLength(50)]
    public string Inspector { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? DataSource { get; set; }

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

    [Required(ErrorMessage = "工段名称不能为空")]
    [MaxLength(50)]
    public string SectionName { get; set; } = string.Empty;

    /// <summary>执行序号（传0则由服务端从工序组解析）</summary>
    public int SequenceNumber { get; set; }

    [MaxLength(100)]
    public string? ProductionUnit { get; set; }

    [MaxLength(100)]
    public string? EquipmentName { get; set; }

    [MaxLength(200)]
    public string? ProductionOperator { get; set; }

    /// <summary>巡检明细（至少一条，整组提交）</summary>
    public List<InspectionPatrolItemRequest> Items { get; set; } = new();

    public bool NeedRectification { get; set; }

    [MaxLength(500)]
    public string? RectificationDescription { get; set; }

    [MaxLength(500)]
    public string? VerificationResult { get; set; }

    public bool IsClosed { get; set; }
}

/// <summary>
/// 更新巡检单请求
/// </summary>
public class UpdateInspectionPatrolRequest
{
    [Required(ErrorMessage = "巡检日期不能为空")]
    public DateTime PatrolDate { get; set; }

    [MaxLength(50)]
    public string? Inspector { get; set; }

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

    [MaxLength(50)]
    public string? SectionName { get; set; }

    public int? SequenceNumber { get; set; }

    [MaxLength(100)]
    public string? ProductionUnit { get; set; }

    [MaxLength(100)]
    public string? EquipmentName { get; set; }

    [MaxLength(200)]
    public string? ProductionOperator { get; set; }

    /// <summary>巡检明细（整组替换；传空列表则清空）</summary>
    public List<InspectionPatrolItemRequest> Items { get; set; } = new();

    public bool NeedRectification { get; set; }

    [MaxLength(500)]
    public string? RectificationDescription { get; set; }

    [MaxLength(500)]
    public string? VerificationResult { get; set; }

    public bool IsClosed { get; set; }
}

/// <summary>
/// 整改回填请求（扫码第二次扫码专用窄口径）——只写 验证结果/是否闭环/整改人，<b>不动巡检明细</b>。
/// </summary>
public class InspectionPatrolRectifyRequest
{
    [MaxLength(500)]
    public string? VerificationResult { get; set; }

    /// <summary>整改人（第二次扫码的操作人，纯姓名）</summary>
    [MaxLength(200)]
    public string? RectificationOperator { get; set; }

    /// <summary>是否闭环（整改验证通过后置真）</summary>
    public bool IsClosed { get; set; }
}
