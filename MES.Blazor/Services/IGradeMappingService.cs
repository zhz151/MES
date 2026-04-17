using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

public interface IGradeMappingService
{

    Task<List<StandardGradeMappingDto>> GetAllAsync();


    Task<ApiResponse<StandardGradeMappingDto>> GetByIdAsync(int id);


    Task<StandardGradeMappingDto?> GetByStandardGradeAsync(string standardGrade);

    Task<ApiResponse<StandardGradeMappingDto>> CreateAsync(CreateGradeMappingRequest request);

    Task<ApiResponse<StandardGradeMappingDto>> UpdateAsync(int id, UpdateGradeMappingRequest request);

    Task<ApiResponse<object>> DeleteAsync(int id);
}