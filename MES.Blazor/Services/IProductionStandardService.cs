using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;


public interface IProductionStandardService
{

    Task<List<ProductionStandardDto>> GetAllAsync(bool onlyActive = true);


    Task<ApiResponse<ProductionStandardDto>> GetByIdAsync(int id);


    Task<ApiResponse<ProductionStandardDto>> CreateAsync(CreateProductionStandardRequest request);

    Task<ApiResponse<ProductionStandardDto>> UpdateAsync(int id, UpdateProductionStandardRequest request);

    Task<ApiResponse<object>> DeleteAsync(int id);
}