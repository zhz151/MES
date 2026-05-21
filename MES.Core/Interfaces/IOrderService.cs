using MES.Core.DTOs;
using MES.Core.Models;
using MES.Core.Enums;

namespace MES.Core.Interfaces;

/// <summary>
/// 订单服务接口
/// </summary>
public interface IOrderService
{
    // ========== 订单管理 ==========

    /// <summary>
    /// 分页查询订单列表（支持技术要求状态和订单状态筛选）
    /// </summary>
    Task<PagedResult<SalesOrderListDto>> GetPagedAsync(QueryParams query, string? technicalStatus = null, string? orderStatus = null);

    Task<SalesOrderDetailDto> GetByIdAsync(int id);

    /// <summary>
    /// 根据订单号获取订单ID（用于跳转详情页）
    /// </summary>
    Task<int?> GetIdByOrderNumberAsync(string orderNo);

    Task<SalesOrderListDto> CreateAsync(CreateSalesOrderRequest request);
    Task<SalesOrderListDto> UpdateAsync(int id, UpdateSalesOrderRequest request);
    Task DeleteAsync(int id);

    // ========== 项次管理 ==========

    Task<OrderItemDto> AddItemAsync(int orderId, AddOrderItemRequest request);
    Task<OrderItemDto> UpdateItemAsync(int orderId, int itemId, UpdateOrderItemRequest request);
    Task DeleteItemAsync(int orderId, int itemId);

    /// <summary>
    /// 批量保存订单（头更新 + 全部项次增删改，单事务）
    /// </summary>
    Task<SaveAllOrderResponse> SaveAllAsync(int id, SaveAllOrderRequest request);

    // ========== 打印 ==========

    /// <summary>
    /// 获取订单详情（用于打印）
    /// </summary>
    Task<SalesOrderDetailDto> GetByIdForPrintAsync(int id);

    /// <summary>
    /// 批量获取订单详情列表（用于打印）
    /// </summary>
    Task<List<SalesOrderDetailDto>> GetByIdsForPrintAsync(int[] ids);

    /// <summary>
    /// 获取所有订单列表数据（无分页，供客户端筛选排序）
    /// </summary>
    Task<List<SalesOrderListDto>> GetAllListAsync();

    /// <summary>
    /// 按筛选条件获取全部订单详情列表（用于打印全部）
    /// </summary>
    Task<List<SalesOrderDetailDto>> GetAllByFilterForPrintAsync(string? keyword, string? technicalStatus, string? orderStatus, string? sortBy = null, bool isDescending = false);

    /// <summary>
    /// 打印单个订单PDF
    /// </summary>
    Task<byte[]> PrintOrderAsync(int id);

    /// <summary>
    /// 打印选中批次订单PDF
    /// </summary>
    Task<byte[]> PrintOrderBatchAsync(int[] ids);

    /// <summary>
    /// 打印全部筛选订单PDF
    /// </summary>
    Task<byte[]> PrintOrderAllAsync(string? keyword, string? technicalStatus, string? orderStatus, string? sortBy = null, bool isDescending = false);

    /// <summary>
    /// 打印订单技术要求PDF
    /// </summary>
    Task<byte[]> PrintOrderRequirementsAsync(int orderId);
}
