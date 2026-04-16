// 文件路径: MES.Core/Interfaces/IProductionStandardService.cs
using MES.Core.DTOs;

namespace MES.Core.Interfaces;

/// <summary>
/// 产品标准服务接口
/// </summary>
public interface IProductionStandardService
{
    /// <summary>
    /// 获取所有产品标准（用于下拉框）
    /// </summary>
    /// <param name="onlyActive">是否只返回启用的标准，默认true</param>
    /// <returns>产品标准列表</returns>
    Task<List<ProductionStandardDto>> GetAllAsync(bool onlyActive = true);

    /// <summary>
    /// 根据ID获取产品标准详情
    /// </summary>
    /// <param name="id">产品标准ID</param>
    /// <returns>产品标准详情</returns>
    Task<ProductionStandardDto> GetByIdAsync(int id);

    /// <summary>
    /// 创建产品标准
    /// </summary>
    /// <param name="request">创建请求</param>
    /// <returns>创建的产品标准</returns>
    Task<ProductionStandardDto> CreateAsync(CreateProductionStandardRequest request);

    /// <summary>
    /// 更新产品标准
    /// </summary>
    /// <param name="id">产品标准ID</param>
    /// <param name="request">更新请求</param>
    /// <returns>更新的产品标准</returns>
    Task<ProductionStandardDto> UpdateAsync(int id, UpdateProductionStandardRequest request);

    /// <summary>
    /// 删除产品标准
    /// </summary>
    /// <param name="id">产品标准ID</param>
    Task DeleteAsync(int id);
}