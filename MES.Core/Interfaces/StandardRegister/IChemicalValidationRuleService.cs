using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.StandardRegister;
namespace MES.Core.Interfaces.StandardRegister;

/// <summary>
/// 牌号验证服务接口
/// </summary>
public interface IChemicalValidationRuleService
{
    /// <summary>
    /// 根据ID获取验证规则
    /// </summary>
    Task<ChemicalValidationRuleDto?> GetByIdAsync(int id);

    /// <summary>
    /// 查询所有牌号验证规则（分页�?    /// </summary>
    Task<PagedResult<ChemicalValidationRuleDto>> GetAllAsync(QueryParams query);

    /// <summary>
    /// 批量创建牌号验证规则
    /// </summary>
    Task<List<ChemicalValidationRuleDto>> BatchCreateAsync(List<CreateChemicalValidationRuleRequest> requests);

    /// <summary>
    /// 更新牌号验证规则
    /// </summary>
    Task<ChemicalValidationRuleDto> UpdateAsync(int id, UpdateChemicalValidationRuleRequest request);

    /// <summary>
    /// 删除牌号验证规则
    /// </summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// 根据工厂牌号获取验证规则
    /// </summary>
    Task<ChemicalValidationRuleDto?> GetByPlantGradeAsync(string plantGrade);

    /// <summary>
    /// 获取筛选上下文（各列的 DISTINCT 值，用于 ExcelFilter�?    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>批量打印选中记录</summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
}
