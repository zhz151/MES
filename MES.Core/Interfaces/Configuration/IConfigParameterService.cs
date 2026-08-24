using MES.Core.Models;

using MES.Core.DTOs.Configuration;
namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 业务参数配置服务接口
/// </summary>
public interface IConfigParameterService
{
    /// <summary>
    /// 分页查询
    /// </summary>
    Task<PagedResult<ConfigParameterDto>> GetPagedAsync(QueryParams query);

    /// <summary>
    /// 根据 ID 获取
    /// </summary>
    Task<ConfigParameterDto?> GetByIdAsync(int id);

    /// <summary>
    /// 保存（新增或更新�?    /// </summary>
    Task<bool> SaveAsync(ConfigParameterDto dto);

    /// <summary>
    /// 删除
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// 获取指定分类下的参数映射（ParamKey �?ParamValue�?    /// </summary>
    Task<Dictionary<string, decimal>> GetConfigMapAsync(string category);

    /// <summary>
    /// 获取筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();
}
