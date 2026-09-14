using MES.Core.DTOs.Quality;
using MES.Core.Models;

namespace MES.Core.Interfaces.Quality;

/// <summary>
/// 不合格反馈单服务 — 只登记问题（含问题照片）。<b>不带处理状态</b>：
/// 处置由质量负责人在「不合格报告（Ncr）」中完成，本单「已处理」= 已由该单生成 Ncr
/// （见 <c>Ncr.NonconformingFeedbackId</c>，待处理列表按此过滤）。
/// </summary>
public interface INonconformingFeedbackService
{
    Task<PagedResult<NonconformingFeedbackDto>> GetAllAsync(QueryParams query);

    Task<NonconformingFeedbackDto?> GetByIdAsync(int id);

    Task<NonconformingFeedbackDto> CreateAsync(CreateNonconformingFeedbackRequest request);

    Task<NonconformingFeedbackDto> UpdateAsync(int id, UpdateNonconformingFeedbackRequest request);

    Task DeleteAsync(int id);

    /// <summary>各列去重值，用于 ExcelFilter 下拉</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>按生产编号查询批次信息（前端自动带出工单号/牌号/规格/工序组）</summary>
    Task<NonconformingFeedbackLookupResultDto?> LookupBatchAsync(string batchNo);

    // ---------- 附件 ----------

    /// <summary>查询某条反馈单的附件列表</summary>
    Task<List<NonconformingFeedbackAttachmentDto>> GetAttachmentsAsync(int feedbackId);

    /// <summary>上传附件（登记方与处置方均可）</summary>
    Task<NonconformingFeedbackAttachmentDto> AddAttachmentAsync(
        int feedbackId, Stream content, string fileName, string contentType);

    /// <summary>读取附件内容（返回 null 表示记录或文件不存在）</summary>
    Task<AttachmentContent?> GetAttachmentContentAsync(int feedbackId, int attachmentId);

    /// <summary>删除附件（同时删除磁盘文件）</summary>
    Task DeleteAttachmentAsync(int feedbackId, int attachmentId);

    /// <summary>生成单条不合格反馈单打印 PDF（含问题照片原图，工序/工段名称转中文）</summary>
    Task<byte[]> GetPrintPdfAsync(int feedbackId);

    /// <summary>附件张数上限（前端预校验用）</summary>
    int MaxAttachmentCount { get; }

    /// <summary>单文件大小上限（字节，前端预校验用）</summary>
    long MaxAttachmentSizeBytes { get; }
}

/// <summary>
/// 附件内容（供 Controller 直接回写响应流）
/// </summary>
public class AttachmentContent
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "attachment";
}

/// <summary>
/// 不合格反馈单 — 生产编号带出结果
/// </summary>
public class NonconformingFeedbackLookupResultDto
{
    public int ProductionBatchId { get; set; }
    public string BatchNo { get; set; } = null!;
    public string? WorkOrderNo { get; set; }
    public string? PlantGrade { get; set; }

    /// <summary>
    /// 该批次是否存在「附加成检」工序 —— 成品检验档据此推导「预检/终检」：
    /// 有附加成检 → 附加成检工序=终检、其余工序=预检；无附加成检 → 全部=终检。
    /// </summary>
    public bool HasAdditionalFinalInspection { get; set; }

    /// <summary>该批次全部工序组（供前端选 工序名称/制造规格/工段名称）</summary>
    public List<NonconformingFeedbackProcessGroupOption> ProcessGroups { get; set; } = new();
}

/// <summary>
/// 生产编号带出 — 工序组选项
/// </summary>
public class NonconformingFeedbackProcessGroupOption
{
    public int ProcessGroupId { get; set; }

    /// <summary>工序名称（英文 Key）</summary>
    public string ProcessName { get; set; } = null!;

    /// <summary>制造规格</summary>
    public string? ManufacturingSpec { get; set; }

    /// <summary>该工序组下的工段（英文 Key，按执行顺序）</summary>
    public List<string> Sections { get; set; } = new();
}
