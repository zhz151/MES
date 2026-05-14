using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 牌号验证服务接口
/// </summary>
public interface IChemicalValidationRuleService
{
    /// <summary>
    /// 查询所有牌号验证规则（分页）
    /// </summary>
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
}
