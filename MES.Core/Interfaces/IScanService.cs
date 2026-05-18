using MES.Core.DTOs;

namespace MES.Core.Interfaces;

/// <summary>
/// 扫码执行服务接口
/// </summary>
public interface IScanService
{
    /// <summary>
    /// 解析扫码内容（批次号+工序组ID），返回批次信息和可用工段
    /// </summary>
    /// <param name="batchNo">批次号</param>
    /// <param name="processGroupId">工序组ID</param>
    Task<ScanResolveResultDto> ResolveAsync(string batchNo, int processGroupId);

    /// <summary>
    /// 按批次号解析，返回批次信息和该批次下所有工序组选项
    /// </summary>
    /// <param name="batchNo">批次号</param>
    Task<ScanBatchResolveResultDto> GetBatchProcessGroupsAsync(string batchNo);
}
