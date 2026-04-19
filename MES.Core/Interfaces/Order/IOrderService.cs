using MES.Core.DTOs;
using MES.Core.Models;
using MES.Core.Enums;

namespace MES.Core.Interfaces.Order;

/// <summary>
/// 订单服务接口
/// </summary>
public interface IOrderService
{
    // ========== 订单管理 ==========
    
    /// <summary>
    /// 分页查询订单列表（支持技术要求状态和订单状态筛选）
    /// </summary>
    Task<PagedResult<SalesOrderListDto>> GetPagedAsync(QueryParams query, bool? hasTechnicalRequirement = null, List<SalesOrderStatus>? statuses = null);
    
    Task<SalesOrderDetailDto> GetByIdAsync(int id);
    Task<SalesOrderListDto> CreateAsync(CreateSalesOrderRequest request);
    Task<SalesOrderListDto> UpdateAsync(int id, UpdateSalesOrderRequest request);
    Task DeleteAsync(int id);
    
    // ========== 项次管理 ==========
    
    Task<OrderItemDto> AddItemAsync(int orderId, AddOrderItemRequest request);
    Task<OrderItemDto> UpdateItemAsync(int orderId, int itemId, UpdateOrderItemRequest request);
    Task DeleteItemAsync(int orderId, int itemId);
}