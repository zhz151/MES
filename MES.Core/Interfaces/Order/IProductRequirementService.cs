// 文件路径: MES.Core/Interfaces/Order/IProductRequirementService.cs

using MES.Core.DTOs.Order;
namespace MES.Core.Interfaces.Order;

public interface IProductRequirementService
{
    Task<ProductRequirementDto?> GetByOrderItemIdAsync(int orderItemId);
    Task<ProductRequirementDto> CreateOrUpdateAsync(int orderItemId, CreateProductRequirementRequest request);
    Task DeleteAsync(int orderItemId);

    /// <summary>
    /// 根据订单ID获取所有项次的产品要求列表（包含项次号）
    /// </summary>
    Task<List<ProductRequirementDto>> GetByOrderIdAsync(int orderId);

    /// <summary>
    /// 按标准号从工厂检验项要求带出新建默认值（对应字段含"必检"→true，否则 false）
    /// </summary>
    Task<ProductRequirementDefaultsDto> GetDefaultRequirementsByStandardNoAsync(string? standardNo);

    /// <summary>
    /// 按工厂检验项要求全面回填所有技术要求（按订单项次标准号规范化匹配，含"必检"→true；液压检验仅定尺）
    /// </summary>
    Task<int> RefreshDefaultsAllAsync();

    /// <summary>
    /// 按销售订单号 + 工单关联订单项次序号列表（逗号分隔）取质量备注：
    /// OrderItemIds 存的是「项次序号 Sequence」（非 OrderItem.Id），须结合订单号唯一定位 OrderItem；
    /// 各项次技术要求的「其他要求」按项次号拼接（多行时带项次前缀）
    /// </summary>
    Task<string> GetQualityRemarkByOrderItemIdsAsync(string? salesOrderNo, string? orderItemIds);
}
