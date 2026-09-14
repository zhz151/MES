using MES.Core.DTOs.Quality;

namespace MES.Core.Interfaces.Quality;

/// <summary>
/// 扫码链质量服务 — 「巡检」「不合格反馈」的扫码报工入口（仅需登录，不走质量角色档）。
///
/// 与质量模块正常入口的差异：
/// 1. <b>不扫工位码、不扫员工码</b>：直接扫批次码，再由扫码人选择「工序 + 工段」；
/// 2. <b>操作人实名 = 当前登录账号</b>：服务端按登录名反查员工档案（用户名=工号）取真实姓名，
///    写入 巡检人 / 反馈人 / 整改人，客户端传入的同名字段一律被覆盖（防篡改）；
/// 3. 数据来源固定写 "SCAN"。
/// </summary>
public interface IScanQualityService
{
    /// <summary>当前扫码人真实姓名（登录名 → 员工档案姓名，查不到时回退登录名）</summary>
    Task<string> GetCurrentOperatorNameAsync();

    // ---------- 巡检 ----------

    /// <summary>按「批次 + 工序组 + 工段」定位巡检单：<b>未闭环优先</b>，同状态内新→旧</summary>
    Task<List<InspectionPatrolDto>> GetPatrolsByKeyAsync(string batchNo, int processGroupId, string sectionName);

    /// <summary>巡检单详情（含巡检明细与附件列表）</summary>
    Task<InspectionPatrolDto?> GetPatrolAsync(int id);

    /// <summary>扫码新建巡检单（巡检人 = 当前登录人，数据来源 = SCAN）</summary>
    Task<InspectionPatrolDto> CreatePatrolAsync(CreateInspectionPatrolRequest request);

    /// <summary>扫码整改回填（整改人 = 当前登录人；只写 验证结果/是否闭环/整改人，不动巡检明细）</summary>
    Task<InspectionPatrolDto> RectifyPatrolAsync(int id, InspectionPatrolRectifyRequest request);

    /// <summary>上传巡检照片（photoType 见 InspectionPatrolPhotoTypes）</summary>
    Task<InspectionPatrolAttachmentDto> AddPatrolAttachmentAsync(
        int patrolId, string photoType, Stream content, string fileName, string contentType);

    /// <summary>读取巡检照片字节（返回 null 表示记录或文件不存在）</summary>
    Task<AttachmentContent?> GetPatrolAttachmentContentAsync(int patrolId, int attachmentId);

    /// <summary>删除巡检照片（同时删除磁盘文件）</summary>
    Task DeletePatrolAttachmentAsync(int patrolId, int attachmentId);

    /// <summary>按生产编号查询批次信息（工序组下拉用）</summary>
    Task<InspectionPatrolLookupResultDto?> LookupPatrolBatchAsync(string batchNo);

    /// <summary>在产单位/车间 候选（委外单位档案）</summary>
    Task<InspectionPatrolPositionOptionsDto> GetPatrolPositionOptionsAsync();

    // ---------- 不合格反馈 ----------

    /// <summary>扫码新建不合格反馈单（反馈人 = 当前登录人，数据来源 = SCAN）</summary>
    Task<NonconformingFeedbackDto> CreateFeedbackAsync(CreateNonconformingFeedbackRequest request);

    /// <summary>上传问题照片</summary>
    Task<NonconformingFeedbackAttachmentDto> AddFeedbackAttachmentAsync(
        int feedbackId, Stream content, string fileName, string contentType);

    /// <summary>读取问题照片字节（返回 null 表示记录或文件不存在）</summary>
    Task<AttachmentContent?> GetFeedbackAttachmentContentAsync(int feedbackId, int attachmentId);

    /// <summary>删除问题照片（同时删除磁盘文件）</summary>
    Task DeleteFeedbackAttachmentAsync(int feedbackId, int attachmentId);

    /// <summary>按生产编号查询批次信息（工序组下拉用）</summary>
    Task<NonconformingFeedbackLookupResultDto?> LookupFeedbackBatchAsync(string batchNo);
}
