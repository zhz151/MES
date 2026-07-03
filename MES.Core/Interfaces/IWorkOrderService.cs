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
    /// 获取已确认但无工单的订单列表（待生成工单）
    /// </summary>
    Task<List<WorkOrderListItemDto>> GetPendingOrdersAsync();

    /// <summary>
    /// 获取待生成工单的订单项次列表
    /// </summary>
    Task<List<OrderItemForWorkOrderDto>> GetOrderItemsForWorkOrderAsync(string salesOrderNo);

    /// <summary>
    /// 生成工单（支持首次生成、覆盖生成、待修正生成）
    /// </summary>
    Task<List<GeneratedWorkOrderDto>> GenerateWorkOrdersAsync(CreateWorkOrderRequest request);

    /// <summary>
    /// 分页查询工单列表（精简版，不含用料计划聚合数据）
    /// </summary>
    Task<PagedResult<WorkOrderListItemDto>> GetPagedAsync(WorkOrderQueryParams query);

    /// <summary>
    /// 分页查询工单列表（含用料计划聚合数据，供用料计划总览页使用）
    /// </summary>
    Task<PagedResult<WorkOrderListDto>> GetPagedWithPlansAsync(WorkOrderQueryParams query);

    /// <summary>
    /// 根据ID获取工单详情
    /// </summary>
    Task<WorkOrderDetailDto> GetByIdAsync(int id);

    /// <summary>
    /// 根据工单号获取工单详情
    /// </summary>
    Task<WorkOrderDetailDto> GetByWorkOrderNoAsync(string workOrderNo);

    /// <summary>
    /// 根据订单号获取工单列表（返回精简 DTO，仅含 Id/工单号等基础字段）
    /// </summary>
    Task<List<WorkOrderListItemDto>> GetBySalesOrderNoAsync(string salesOrderNo);

    /// <summary>
    /// 更新工单状态
    /// </summary>
    Task<UpdateWorkOrderStatusResponseDto> UpdateStatusAsync(int id, UpdateWorkOrderStatusRequest request);

    /// <summary>
    /// 删除工单（物理删除）
    /// </summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// 删除工单（用于"订单已取消-工单待删除"区域）
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
/// 获取所有工单首页订单状态数据（无分页，供客户端筛选排序）
/// </summary>
Task<List<OrderWorkOrderStatusDto>> GetAllOrderStatusListAsync();

/// <summary>
/// 全量刷新用料计划读模型（从 WorkOrders + 计划表重新计算）
/// </summary>
Task RefreshMaterialPlanReadModelAsync();

/// <summary>
/// 获取所有用料计划总览数据（无分页，供客户端筛选排序）
/// </summary>
Task<List<WorkOrderListDto>> GetAllListAsync();

/// <summary>
/// 获取订单的工单项次追溯关系（包含该订单下所有工单及其项次明细）
/// </summary>
/// <param name="salesOrderNo">订单号</param>
Task<OrderWorkOrderRelationDto> GetOrderWorkOrderRelationAsync(string salesOrderNo);

/// <summary>
/// 打印工单详情（返回PDF字节数组）
/// </summary>
Task<byte[]> PrintWorkOrderAsync(int id);

/// <summary>
/// 按订单号批量打印所有工单（返回PDF字节数组）
/// </summary>
Task<byte[]> PrintWorkOrdersByOrderAsync(string salesOrderNo);

/// <summary>
/// 按多个订单号批量打印工单（选中打印）
/// </summary>
Task<byte[]> PrintWorkOrdersByOrderBatchAsync(string[] salesOrderNos);

/// <summary>
/// 按筛选项打印全部工单（全部打印）
/// </summary>
Task<byte[]> PrintWorkOrdersByOrderAllAsync(WorkOrderQueryParams query);

/// <summary>
/// 获取工单筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
/// </summary>
Task<Dictionary<string, List<string>>> GetWorkOrderFilterContextsAsync();

}