using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 工单服务接口
/// </summary>
public interface IWorkOrderService
{
    /// <summary>
    /// 获取工单首页订单列表（含工单状态）
    /// </summary>
    Task<PagedResult<OrderWorkOrderStatusDto>> GetOrderWorkOrderStatusPageAsync(WorkOrderQueryParams query);

    /// <summary>
    /// 获取"订单已取消-工单待删除"列表
    /// </summary>
    Task<List<CancelledOrderDto>> GetCancelledOrdersAsync();

    /// <summary>
    /// 获取待生成工单的订单项次列表
    /// </summary>
    Task<List<OrderItemForWorkOrderDto>> GetOrderItemsForWorkOrderAsync(string salesOrderNo);

    /// <summary>
    /// 生成工单（支持首次生成、覆盖生成、待修正生成）
    /// </summary>
    Task<List<GeneratedWorkOrderDto>> GenerateWorkOrdersAsync(CreateWorkOrderRequest request);

    /// <summary>
    /// 分页查询工单列表
    /// </summary>
    Task<PagedResult<WorkOrderListDto>> GetPagedAsync(WorkOrderQueryParams query);

    /// <summary>
    /// 根据ID获取工单详情
    /// </summary>
    Task<WorkOrderDetailDto> GetByIdAsync(int id);

    /// <summary>
    /// 根据订单号获取工单列表
    /// </summary>
    Task<List<WorkOrderListDto>> GetBySalesOrderNoAsync(string salesOrderNo);

    /// <summary>
    /// 获取工单包含的原始订单项次列表（用于追溯页面）
    /// </summary>
    Task<List<OrderItemForWorkOrderDto>> GetWorkOrderItemsAsync(int workOrderId);

    /// <summary>
    /// 更新工单状态
    /// </summary>
    Task<UpdateWorkOrderStatusResponseDto> UpdateStatusAsync(int id, UpdateWorkOrderStatusRequest request);

    /// <summary>
    /// 删除工单（软删除）
    /// </summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// 软删除工单（用于"订单已取消-工单待删除"区域）
    /// </summary>
    Task SoftDeleteAsync(int id);

    /// <summary>
    /// 检测单个订单变更并更新工单状态
    /// </summary>
    Task CheckAndUpdateWorkOrderStatusAsync(int salesOrderId);

    /// <summary>
    /// 检测所有已确认订单的变更并更新工单状态
    /// </summary>
    Task CheckAllOrdersChangeAsync();
/// <summary>
/// 获取订单的工单项次追溯关系（包含该订单下所有工单及其项次明细）
/// </summary>
/// <param name="salesOrderNo">订单号</param>
Task<OrderWorkOrderRelationDto> GetOrderWorkOrderRelationAsync(string salesOrderNo);

}