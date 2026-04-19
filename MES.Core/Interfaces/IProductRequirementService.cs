// 文件路径: MES.Core/Interfaces/Order/IProductRequirementService.cs
using MES.Core.DTOs;

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
}