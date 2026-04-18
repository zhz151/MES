// 文件路径: MES.Core/Interfaces/Order/IProductRequirementService.cs
using MES.Core.DTOs;

namespace MES.Core.Interfaces.Order;

/// <summary>
/// 产品要求服务接口
/// </summary>
public interface IProductRequirementService
{
    /// <summary>
    /// 根据订单项次ID获取产品要求
    /// </summary>
    /// <param name="orderItemId">订单项次ID</param>
    Task<ProductRequirementDto?> GetByOrderItemIdAsync(int orderItemId);

    /// <summary>
    /// 创建或更新产品要求
    /// </summary>
    /// <param name="orderItemId">订单项次ID</param>
    /// <param name="request">产品要求请求</param>
    Task<ProductRequirementDto> CreateOrUpdateAsync(int orderItemId, CreateProductRequirementRequest request);

    /// <summary>
    /// 删除产品要求
    /// </summary>
    /// <param name="orderItemId">订单项次ID</param>
    Task DeleteAsync(int orderItemId);
}