using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 日产估算服务接口
/// </summary>
public interface IDailyOutputEstimateService
{
    Task<PagedResult<DailyOutputEstimateDto>> GetPagedAsync(QueryParams query);
    Task<DailyOutputEstimateDto?> GetByIdAsync(int id);
    Task<bool> SaveAsync(DailyOutputEstimateDto dto);
    Task<bool> DeleteAsync(int id);

    /// <summary>获取所有日产估算配置（按 MinOuterDiameter 降序）</summary>
    Task<List<DailyOutputEstimateDto>> GetAllAsync();
}
