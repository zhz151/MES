// 文件路径: MES.Blazor/Services/IOrderService.cs
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 订单服务接口（前端）
/// </summary>
public interface IOrderService
{
    // ========== 订单管理 ==========

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
    /// 删除订单（软删除）- 返回无泛型的 ApiResponse
    /// </summary>
    Task<ApiResponse> DeleteAsync(int id);

    // ========== 项次管理 ==========

    /// <summary>
    /// 添加订单项次
    /// </summary>
    Task<ApiResponse<OrderItemDto>> AddItemAsync(int orderId, AddOrderItemRequest request);

    /// <summary>
    /// 更新订单项次
    /// </summary>
    Task<ApiResponse<OrderItemDto>> UpdateItemAsync(int orderId, int itemId, UpdateOrderItemRequest request);

    /// <summary>
    /// 删除订单项次（软删除）- 返回无泛型的 ApiResponse
    /// </summary>
    Task<ApiResponse> DeleteItemAsync(int orderId, int itemId);
}