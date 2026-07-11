using MES.Core.Models;

using MES.Core.DTOs.Scheduling;
namespace MES.Core.Interfaces.Scheduling;

/// <summary>
/// 订单总况服务接口 �?聚合各工段产能负荷数�?/// </summary>
public interface IProductionOverviewService
{
    /// <summary>获取订单总况数据</summary>
    Task<ProductionOverviewDto> GetOverviewAsync();
}
