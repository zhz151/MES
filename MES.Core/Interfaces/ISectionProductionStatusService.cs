using MES.Core.DTOs;

namespace MES.Core.Interfaces;

/// <summary>
/// 生产工段待产量现况服务接口 — 按(工序组, 工段)维度统计批次现况
/// </summary>
public interface ISectionProductionStatusService
{
    /// <summary>
    /// 获取所有(工序组, 工段)维度的生产/待产现况汇总
    /// </summary>
    Task<List<SectionProductionStatusDto>> GetStatusAsync();
}
