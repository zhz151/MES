using MES.Core.DTOs;

namespace MES.Core.Interfaces;

public interface IGradeMappingService
{
    Task<List<StandardGradeMappingDto>> GetAllAsync();

    Task<StandardGradeMappingDto> GetByIdAsync(int id);

    Task<StandardGradeMappingDto> CreateAsync(CreateGradeMappingRequest request);

    Task<StandardGradeMappingDto> UpdateAsync(int id, UpdateGradeMappingRequest request);

    Task DeleteAsync(int id);
}