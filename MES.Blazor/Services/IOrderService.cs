// 文件路径: MES.Blazor/Services/IOrderService.cs
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 订单前端服务接口
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// 分页查询订单列表
    /// </summary>
    Task<ApiResponse<PagedResult<SalesOrderListDto>>> GetPagedAsync(QueryParams query);

    /// <summary>
    /// 根据ID获取订单详情
    /// </summary>
    Task<ApiResponse<SalesOrderDetailDto>> GetByIdAsync(int id);

    /// <summary>
    /// 创建订单
    /// </summary>
    Task<ApiResponse<SalesOrderListDto>> CreateAsync(CreateSalesOrderRequest request);

    /// <summary>
    /// 更新订单
    /// </summary>
    Task<ApiResponse<SalesOrderListDto>> UpdateAsync(int id, UpdateSalesOrderRequest request);

    /// <summary>
    /// 删除订单
    /// </summary>
    Task<ApiResponse<object>> DeleteAsync(int id);
}