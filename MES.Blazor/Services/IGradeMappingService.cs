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

    // 删除返回 ApiResponse（无泛型）
    Task<ApiResponse> DeleteAsync(int id);
}