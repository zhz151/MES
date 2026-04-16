using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 订单服务接口
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// 分页查询订单列表
    /// </summary>
    Task<PagedResult<SalesOrderListDto>> GetPagedAsync(QueryParams query);

    /// <summary>
    /// 根据ID获取订单详情
    /// </summary>
    Task<SalesOrderDetailDto> GetByIdAsync(int id);

    /// <summary>
    /// 创建订单
    /// </summary>
    Task<SalesOrderListDto> CreateAsync(CreateSalesOrderRequest request);

    /// <summary>
    /// 更新订单
    /// </summary>
    Task<SalesOrderListDto> UpdateAsync(int id, UpdateSalesOrderRequest request);

    /// <summary>
    /// 删除订单（软删除）
    /// </summary>
    Task DeleteAsync(int id);
}