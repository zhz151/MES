// 文件路径: MES.Blazor/Services/IProductionStandardService.cs
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 产品标准前端服务接口
/// </summary>
public interface IProductionStandardService
{
    /// <summary>
    /// 获取所有产品标准（用于下拉框）
    /// </summary>
    Task<List<ProductionStandardDto>> GetAllAsync(bool onlyActive = true);

    /// <summary>
    /// 根据ID获取产品标准
    /// </summary>
    Task<ApiResponse<ProductionStandardDto>> GetByIdAsync(int id);

    /// <summary>
    /// 创建产品标准
    /// </summary>
    Task<ApiResponse<ProductionStandardDto>> CreateAsync(CreateProductionStandardRequest request);

    /// <summary>
    /// 更新产品标准
    /// </summary>
    Task<ApiResponse<ProductionStandardDto>> UpdateAsync(int id, UpdateProductionStandardRequest request);

    /// <summary>
    /// 删除产品标准
    /// </summary>
    Task<ApiResponse<object>> DeleteAsync(int id);
}