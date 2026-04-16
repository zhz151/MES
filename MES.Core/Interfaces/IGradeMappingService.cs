// 文件路径: MES.Core/Interfaces/IGradeMappingService.cs

using MES.Core.DTOs;

namespace MES.Core.Interfaces;

/// <summary>
/// 牌号对照服务接口
/// </summary>
public interface IGradeMappingService
{
    /// <summary>
    /// 获取所有牌号对照（用于下拉框）
    /// </summary>
    /// <returns>牌号对照列表</returns>
    Task<List<StandardGradeMappingDto>> GetAllAsync();

    /// <summary>
    /// 根据ID获取牌号对照详情
    /// </summary>
    /// <param name="id">牌号对照ID</param>
    /// <returns>牌号对照详情</returns>
    Task<StandardGradeMappingDto> GetByIdAsync(int id);

    /// <summary>
    /// 创建牌号对照
    /// </summary>
    /// <param name="request">创建请求</param>
    /// <returns>创建的牌号对照</returns>
    Task<StandardGradeMappingDto> CreateAsync(CreateGradeMappingRequest request);

    /// <summary>
    /// 更新牌号对照
    /// </summary>
    /// <param name="id">牌号对照ID</param>
    /// <param name="request">更新请求</param>
    /// <returns>更新的牌号对照</returns>
    Task<StandardGradeMappingDto> UpdateAsync(int id, UpdateGradeMappingRequest request);

    /// <summary>
    /// 删除牌号对照
    /// </summary>
    /// <param name="id">牌号对照ID</param>
    Task DeleteAsync(int id);
}