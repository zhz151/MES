using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 订单总况服务接口 — 聚合各工段产能负荷数据
/// </summary>
public interface IProductionOverviewService
{
    /// <summary>获取订单总况数据</summary>
    Task<ProductionOverviewDto> GetOverviewAsync();
}
