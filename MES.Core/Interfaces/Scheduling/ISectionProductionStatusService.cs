
using MES.Core.DTOs.Scheduling;
namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 生产工段待产量现况服务接�?�?�?工序�? 工段)维度统计批次现况
/// </summary>
public interface ISectionProductionStatusService
{
    /// <summary>
    /// 获取所�?工序�? 工段)维度的生�?待产现况汇�?    /// </summary>
    Task<List<SectionProductionStatusDto>> GetStatusAsync();
}
