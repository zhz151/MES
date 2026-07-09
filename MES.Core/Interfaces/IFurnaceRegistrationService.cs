using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 来料炉号登记服务接口
/// </summary>
public interface IFurnaceRegistrationService
{
    /// <summary>
    /// 获取来料炉号登记详情
    /// </summary>
    Task<FurnaceRegistrationDto?> GetByIdAsync(int id);

    /// <summary>
    /// 获取所有来料炉号登记（无分页）
    /// </summary>
    Task<List<FurnaceRegistrationDto>> GetAllListAsync();

    /// <summary>
    /// 查询所有来料炉号登记（分页）
    /// </summary>
    Task<PagedResult<FurnaceRegistrationDto>> GetAllAsync(QueryParams query);

    /// <summary>
    /// 批量创建来料炉号登记
    /// </summary>
    Task<List<FurnaceRegistrationDto>> BatchCreateAsync(List<CreateFurnaceRegistrationRequest> requests);

    /// <summary>
    /// 更新来料炉号登记
    /// </summary>
    Task<FurnaceRegistrationDto> UpdateAsync(int id, UpdateFurnaceRegistrationRequest request);

    /// <summary>
    /// 删除来料炉号登记
    /// </summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// 根据登记牌号查询关联工厂牌号
    /// </summary>
    Task<string?> LookupPlantGradeAsync(string registeredGrade);

    /// <summary>
    /// 获取筛选上下文（各列的 DISTINCT 值，用于 ExcelFilter）
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>批量打印选中记录</summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>按条件打印全部记录</summary>
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? incomingDateFrom = null, DateTime? incomingDateTo = null);
}
