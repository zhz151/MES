using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Quality;
namespace MES.Core.Interfaces.Quality;

/// <summary>
/// 过程检验服务接�?/// </summary>
public interface IProcessInspectionService
{
    /// <summary>
    /// 跨批次查询所有过程检验记录（分页�?    /// </summary>
    Task<PagedResult<ProcessInspectionDto>> GetAllAsync(QueryParams query);

    /// <summary>
    /// 获取筛选上下文（各列去重值），用�?ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>批量打印选中记录</summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>单据式打印选中记录（A4 竖版每条一页，含检验照片）</summary>
    Task<byte[]> PrintSelectedDocAsync(int[] ids);

    /// <summary>
    /// 批量创建过程检验记�?    /// </summary>
    Task<List<ProcessInspectionDto>> BatchCreateAsync(List<CreateProcessInspectionRequest> requests);

    /// <summary>
    /// 更新过程检验记�?    /// </summary>
    Task<ProcessInspectionDto> UpdateAsync(int id, UpdateProcessInspectionRequest request);

    /// <summary>
    /// 删除过程检验记�?    /// </summary>
    Task DeleteAsync(int id);

    // ---------- 照片附件 ----------

    /// <summary>单条记录照片上限（张，前端预校验用）</summary>
    int MaxAttachmentCount { get; }

    /// <summary>单张照片大小上限（字节，前端预校验用）</summary>
    long MaxAttachmentSizeBytes { get; }

    /// <summary>查询某条过程检验记录的照片列表</summary>
    Task<List<ProcessInspectionAttachmentDto>> GetAttachmentsAsync(int id);

    /// <summary>上传照片（超上限抛 BusinessException）</summary>
    Task<ProcessInspectionAttachmentDto> AddAttachmentAsync(
        int id, Stream content, string fileName, string contentType);

    /// <summary>读取照片内容（返回 null 表示记录或文件不存在）</summary>
    Task<AttachmentContent?> GetAttachmentContentAsync(int id, int attachmentId);

    /// <summary>删除照片（同时删除磁盘文件）</summary>
    Task DeleteAttachmentAsync(int id, int attachmentId);
}
