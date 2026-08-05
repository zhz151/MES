using MES.Core.Models;

using MES.Core.DTOs.Configuration;
namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 标准工量天数服务接口
/// </summary>
public interface IStandardWorkDayService
{
    /// <summary>分页查询</summary>
    Task<PagedResult<StandardWorkDayDto>> GetPagedAsync(QueryParams query);

    /// <summary>根据 ID 获取</summary>
    Task<StandardWorkDayDto?> GetByIdAsync(int id);

    /// <summary>新增或更�?/summary>
    Task<bool> SaveAsync(StandardWorkDayDto dto);

    /// <summary>删除</summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// 获取标准天数映射表：key=SectionName, value=StandardDays
    /// 按牌号前缀优先级匹配（精确匹配 &gt; 通用 null），�?MemoryCache
    /// </summary>
    Task<Dictionary<string, double>> GetStandardDaysMapAsync(string? plantGrade);

    /// <summary>
    /// 获取启用工段列表（IsEnabled=true 且按 DisplayOrder 升序），展示层动态化用。
    /// 同工段存在牌号前缀覆盖行时，显示名/顺序以通用行（PlantGradePrefix=null）为准。
    /// </summary>
    Task<List<SectionInfoDto>> GetEnabledSectionsAsync();
}
