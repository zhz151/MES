using MES.Core.DTOs.Infrastructure;

namespace MES.Core.Interfaces.Infrastructure;

/// <summary>
/// 统一操作日志服务接口
/// </summary>
public interface IOperationLogService
{
    /// <summary>
    /// 添加操作日志
    /// </summary>
    /// <param name="module">模块名称（Batch / Order / WorkOrder）</param>
    /// <param name="entityId">关联业务主键</param>
    /// <param name="operationType">操作类型（创建 / 变更 / 删除）</param>
    /// <param name="detail">操作详情</param>
    Task AddLogAsync(string module, int entityId, string operationType, string? detail = null);

    /// <summary>
    /// 获取指定模块和业务实体的操作日志列表（按时间倒序）
    /// </summary>
    Task<List<OperationLogDto>> GetLogsAsync(string module, int entityId);
}
