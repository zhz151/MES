// 文件路径: MES.Core/Interfaces/Order/IOrderService.cs
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces.Order;

/// <summary>
/// 订单服务接口
/// </summary>
public interface IOrderService
{
    // ========== 订单管理 ==========
    
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
    
    // ========== 项次管理 ==========
    
    /// <summary>
    /// 添加订单项次
    /// </summary>
    /// <param name="orderId">订单ID</param>
    /// <param name="request">添加项次请求</param>
    Task<OrderItemDto> AddItemAsync(int orderId, AddOrderItemRequest request);
    
    /// <summary>
    /// 更新订单项次
    /// </summary>
    /// <param name="orderId">订单ID</param>
    /// <param name="itemId">项次ID</param>
    /// <param name="request">更新项次请求</param>
    Task<OrderItemDto> UpdateItemAsync(int orderId, int itemId, UpdateOrderItemRequest request);
    
    /// <summary>
    /// 删除订单项次（软删除）
    /// </summary>
    /// <param name="orderId">订单ID</param>
    /// <param name="itemId">项次ID</param>
    Task DeleteItemAsync(int orderId, int itemId);
}