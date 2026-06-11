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
    Task<ScanResolveResultDto> ResolveAsync(string batchNo, int processGroupId);

    /// <summary>
    /// 按批次号解析，返回批次信息和该批次下所有工序组选项
    /// </summary>
    Task<ScanBatchResolveResultDto> GetBatchProcessGroupsAsync(string batchNo);

    /// <summary>
    /// 按批次号+工段名匹配工序组，返回解析结果
    /// 用于工位扫码后自动匹配：已知工段名，找到批次中该工段对应的工序组
    /// </summary>
    /// <returns>匹配到的解析结果，无匹配返回 null</returns>
    Task<ScanResolveResultDto?> ResolveByBatchAndSectionAsync(string batchNo, string sectionName);
}
