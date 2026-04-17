// 文件路径: MES.Blazor/Services/IGradeMappingService.cs
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 牌号对照前端服务接口
/// </summary>
public interface IGradeMappingService
{
    /// <summary>
    /// 获取所有牌号对照（用于下拉框）
    /// </summary>
    Task<List<StandardGradeMappingDto>> GetAllAsync();

    /// <summary>
    /// 根据ID获取牌号对照
    /// </summary>
    Task<ApiResponse<StandardGradeMappingDto>> GetByIdAsync(int id);

    /// <summary>
    /// 根据标准牌号获取密度和工厂牌号
    /// </summary>
    Task<StandardGradeMappingDto?> GetByStandardGradeAsync(string standardGrade);

    /// <summary>
    /// 创建牌号对照
    /// </summary>
    Task<ApiResponse<StandardGradeMappingDto>> CreateAsync(CreateGradeMappingRequest request);

    /// <summary>
    /// 更新牌号对照
    /// </summary>
    Task<ApiResponse<StandardGradeMappingDto>> UpdateAsync(int id, UpdateGradeMappingRequest request);

    /// <summary>
    /// 删除牌号对照
    /// </summary>
    Task<ApiResponse<object>> DeleteAsync(int id);
}