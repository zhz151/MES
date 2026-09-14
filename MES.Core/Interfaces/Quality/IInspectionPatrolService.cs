using MES.Core.DTOs.Quality;
using MES.Core.Models;

namespace MES.Core.Interfaces.Quality;

/// <summary>
/// 巡检单服务 — 质量检验的一种类型（过程巡检记录，含巡检明细与整改闭环）
/// </summary>
public interface IInspectionPatrolService
{
    Task<PagedResult<InspectionPatrolDto>> GetAllAsync(QueryParams query);

    Task<InspectionPatrolDto?> GetByIdAsync(int id);

    Task<InspectionPatrolDto> CreateAsync(CreateInspectionPatrolRequest request);

    Task<InspectionPatrolDto> UpdateAsync(int id, UpdateInspectionPatrolRequest request);

    Task DeleteAsync(int id);

    /// <summary>切换「是否闭环」标记</summary>
    Task SetClosedAsync(int id, bool isClosed);

    /// <summary>
    /// 按「批次 + 工序组 + 工段」查询巡检单（扫码第二次定位用）：<b>待整改优先</b>
    /// （待整改 = 涉及整改且未闭环；不涉及整改的单无需闭环，不占待整改位），同状态内按新→旧。
    /// 无待整改单时返回的列表仍含其它单，由调用方判断「新建」。
    /// </summary>
    Task<List<InspectionPatrolDto>> GetByKeyAsync(string batchNo, int processGroupId, string sectionName);

    /// <summary>
    /// 窄口径整改回填（扫码第二次扫码）：只写 验证结果 / 是否闭环 / 整改人，<b>不动巡检明细</b>。
    /// 非「涉及整改」的单拒绝回填。
    /// </summary>
    Task<InspectionPatrolDto> RectifyAsync(int id, InspectionPatrolRectifyRequest request);

    /// <summary>各列去重值，用于 ExcelFilter 下拉</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>按生产编号查询批次信息（前端自动带出工单号/牌号/规格/工序组）</summary>
    Task<InspectionPatrolLookupResultDto?> LookupBatchAsync(string batchNo);

    /// <summary>在产单位·车间候选（委外单位档案驱动，前端下拉用；在产设备名为纯手输无候选，在产操作人走员工档案不经此端点）</summary>
    Task<InspectionPatrolPositionOptionsDto> GetPositionOptionsAsync();

    // ---------- 附件 ----------

    /// <summary>查询某条巡检单的附件列表</summary>
    Task<List<InspectionPatrolAttachmentDto>> GetAttachmentsAsync(int patrolId);

    /// <summary>上传附件（按 PhotoType 分类型限张数）</summary>
    Task<InspectionPatrolAttachmentDto> AddAttachmentAsync(
        int patrolId, string photoType, Stream content, string fileName, string contentType);

    /// <summary>读取附件内容（返回 null 表示记录或文件不存在）</summary>
    Task<AttachmentContent?> GetAttachmentContentAsync(int patrolId, int attachmentId);

    /// <summary>删除附件（同时删除磁盘文件）</summary>
    Task DeleteAttachmentAsync(int patrolId, int attachmentId);

    /// <summary>生成单条巡检单打印 PDF（含照片原图，工序/工段名称转中文）</summary>
    Task<byte[]> GetPrintPdfAsync(int patrolId);

    /// <summary>每类照片张数上限（前端预校验用）</summary>
    int MaxAttachmentPerType { get; }

    /// <summary>单文件大小上限（字节，前端预校验用）</summary>
    long MaxAttachmentSizeBytes { get; }
}

/// <summary>
/// 巡检单 — 生产编号带出结果
/// </summary>
public class InspectionPatrolLookupResultDto
{
    public int ProductionBatchId { get; set; }
    public string BatchNo { get; set; } = null!;
    public string? WorkOrderNo { get; set; }
    public string? PlantGrade { get; set; }

    /// <summary>批次当前委外单位（非空 = 委外在产，前端据此置灰 在产设备名/在产操作人）</summary>
    public string? CurrentOutsource { get; set; }

    /// <summary>批次在产设备名称（厂内在产，前端带出到「在产设备名」）</summary>
    public string? CurrentEquipmentName { get; set; }

    /// <summary>该批次全部工序组（供前端选 工序名称/制造规格/工段名称）</summary>
    public List<InspectionPatrolProcessGroupOption> ProcessGroups { get; set; } = new();
}

/// <summary>
/// 生产编号带出 — 工序组选项
/// </summary>
public class InspectionPatrolProcessGroupOption
{
    public int ProcessGroupId { get; set; }

    /// <summary>工序名称（英文 Key）</summary>
    public string ProcessName { get; set; } = null!;

    /// <summary>制造规格</summary>
    public string? ManufacturingSpec { get; set; }

    /// <summary>该工序组下的工段（英文 Key，按执行顺序）</summary>
    public List<string> Sections { get; set; } = new();
}

/// <summary>
/// 巡检单 — 在产信息候选（前端下拉用，存档仍为文本）
/// </summary>
public class InspectionPatrolPositionOptionsDto
{
    /// <summary>在产单位/车间候选（委外单位档案名称，允许手填档案外值）</summary>
    public List<string> ProductionUnits { get; set; } = new();
}
