using MES.Core.Models;

using MES.Core.DTOs.Configuration;
namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 日产估算服务接口
/// </summary>
public interface IDailyOutputEstimateService
{
    Task<PagedResult<DailyOutputEstimateDto>> GetPagedAsync(QueryParams query);
    Task<DailyOutputEstimateDto?> GetByIdAsync(int id);
    Task<bool> SaveAsync(DailyOutputEstimateDto dto);
    Task<bool> DeleteAsync(int id);

    /// <summary>获取所有日产估算配置（�?MinOuterDiameter 降序�?/summary>
    Task<List<DailyOutputEstimateDto>> GetAllAsync();
}
