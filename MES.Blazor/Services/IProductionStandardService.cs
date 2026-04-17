// 文件路径: MES.Blazor/Services/IProductionStandardService.cs
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public interface IProductionStandardService
{
    Task<List<ProductionStandardDto>> GetAllAsync(bool onlyActive = true);

    Task<ApiResponse<ProductionStandardDto>> GetByIdAsync(int id);

    Task<ApiResponse<ProductionStandardDto>> CreateAsync(CreateProductionStandardRequest request);

    Task<ApiResponse<ProductionStandardDto>> UpdateAsync(int id, UpdateProductionStandardRequest request);

    // 修改：返回类型改为 Task<ApiResponse<object>>
    Task<ApiResponse<object>> DeleteAsync(int id);
}