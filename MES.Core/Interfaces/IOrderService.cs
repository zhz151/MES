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

    /// <summary>
    /// 根据ID获取订单详情
    /// </summary>
    Task<SalesOrderDetailDto> GetByIdAsync(int id);

    /// <summary>
    /// 根据订单号获取订单ID（用于跳转详情页）
    /// </summary>
    Task<int?> GetIdByOrderNumberAsync(string orderNo);

    /// <summary>
    /// 创建订单
    /// </summary>
    Task<SalesOrderListDto> CreateAsync(CreateSalesOrderRequest request);

    /// <summary>
    /// 更新订单
    /// </summary>
    Task<SalesOrderListDto> UpdateAsync(int id, UpdateSalesOrderRequest request);

    /// <summary>
    /// 删除订单
    /// </summary>
    Task DeleteAsync(int id);

    // ========== 项次管理 ==========

    /// <summary>
    /// 添加订单项次
    /// </summary>
    Task<OrderItemDto> AddItemAsync(int orderId, AddOrderItemRequest request);

    /// <summary>
    /// 更新订单项次
    /// </summary>
    Task<OrderItemDto> UpdateItemAsync(int orderId, int itemId, UpdateOrderItemRequest request);

    /// <summary>
    /// 删除订单项次
    /// </summary>
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
    /// 打印单个订单PDF
    /// </summary>
    Task<byte[]> PrintOrderAsync(int id);

    /// <summary>
    /// 打印选中批次订单PDF
    /// </summary>
    Task<byte[]> PrintOrderBatchAsync(int[] ids);

    /// <summary>
    /// 打印订单技术要求PDF
    /// </summary>
    Task<byte[]> PrintOrderRequirementsAsync(int orderId);

    /// <summary>
    /// 获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>
    /// 刷新全部订单读模型（从源表重新聚合 OrderListSummary）
    /// </summary>
    Task RefreshAllAsync();

    /// <summary>
    /// 刷新指定订单的读模型
    /// </summary>
    Task RefreshByOrderIdAsync(int orderId);
}
