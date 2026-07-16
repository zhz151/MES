using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.StandardRegister;
namespace MES.Core.Interfaces.StandardRegister;

/// <summary>
/// 牌号化学成分服务接口
/// </summary>
public interface IChemicalCompositionService
{
    /// <summary>
    /// 查询所有牌号化学成分（分页�?    /// </summary>
    Task<PagedResult<ChemicalCompositionDto>> GetAllAsync(QueryParams query);

    /// <summary>
    /// 获取所有牌号化学成分（无分页）
    /// </summary>
    Task<List<ChemicalCompositionDto>> GetAllListAsync();

    /// <summary>
    /// 批量创建牌号化学成分
    /// </summary>
    Task<List<ChemicalCompositionDto>> BatchCreateAsync(List<CreateChemicalCompositionRequest> requests);

    /// <summary>
    /// 更新牌号化学成分
    /// </summary>
    Task<ChemicalCompositionDto> UpdateAsync(int id, UpdateChemicalCompositionRequest request);

    /// <summary>
    /// 删除牌号化学成分
    /// </summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// 生成Excel导入模板
    /// </summary>
    Task<byte[]> GenerateTemplateAsync();

    /// <summary>
    /// 从Excel导入牌号化学成分
    /// </summary>
    Task<ImportResult> ImportAsync(byte[] fileData, string fileName, string? userName);

    /// <summary>
    /// 预览Excel导入结果（不写入数据库）
    /// </summary>
    Task<ImportPreviewResult> PreviewImportAsync(byte[] fileData, string fileName);

    /// <summary>
    /// 获取筛选上下文（各列的 DISTINCT 值，用于 ExcelFilter�?    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>批量打印选中记录</summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>按条件打印全部记�?/summary>
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns);
}
