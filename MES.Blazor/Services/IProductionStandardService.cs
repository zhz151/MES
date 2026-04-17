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

    // 删除返回 ApiResponse（无泛型）
    Task<ApiResponse> DeleteAsync(int id);
}