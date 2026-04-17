using MES.Core.DTOs;

namespace MES.Core.Interfaces;

public interface IProductionStandardService
{
    Task<List<ProductionStandardDto>> GetAllAsync(bool onlyActive = true);

    Task<ProductionStandardDto> GetByIdAsync(int id);

    Task<ProductionStandardDto> CreateAsync(CreateProductionStandardRequest request);


    Task<ProductionStandardDto> UpdateAsync(int id, UpdateProductionStandardRequest request);

    Task DeleteAsync(int id);
}