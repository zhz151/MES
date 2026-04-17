// 文件路径: MES.Blazor/Services/IGradeMappingService.cs
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

    // 修改：返回类型改为 Task<ApiResponse<object>>
    Task<ApiResponse<object>> DeleteAsync(int id);
}