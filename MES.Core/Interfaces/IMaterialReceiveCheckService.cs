using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 检验到料（成检到料）服务接口
/// </summary>
public interface IMaterialReceiveCheckService
{
    /// <summary>
    /// 获取批次的检验到料记录
    /// </summary>
    Task<MaterialReceiveCheckDto?> GetMaterialReceiveCheckAsync(int batchId);

    /// <summary>
    /// 创建检验到料（将批次状态设为Completed）
    /// </summary>
    Task<MaterialReceiveCheckDto> CreateMaterialReceiveCheckAsync(CreateMaterialReceiveCheckRequest request);

    /// <summary>
    /// 批量创建检验到料（一次查询 + 一次SaveChanges + 一次批量刷新）
    /// </summary>
    Task<List<MaterialReceiveCheckDto>> BatchCreateMaterialReceiveChecksAsync(List<CreateMaterialReceiveCheckRequest> requests);

    /// <summary>
    /// 更新检验到料
    /// </summary>
    Task<MaterialReceiveCheckDto> UpdateMaterialReceiveCheckAsync(int id, UpdateMaterialReceiveCheckRequest request);

    /// <summary>
    /// 删除检验到料
    /// </summary>
    Task DeleteMaterialReceiveCheckAsync(int id);

    /// <summary>
    /// 跨批次查询所有检验到料记录
    /// </summary>
    Task<PagedResult<MaterialReceiveCheckDto>> GetAllMaterialReceiveChecksAsync(QueryParams query);

    /// <summary>
    /// 获取所有检验到料记录（不含分页）
    /// </summary>
    Task<List<MaterialReceiveCheckDto>> GetAllMaterialReceiveCheckListAsync();

    /// <summary>
    /// 获取待检验到料批次（成品检验阶段且未创建检验到料记录）
    /// </summary>
    Task<List<PendingMaterialCheckDto>> GetPendingMaterialChecksAsync();

    /// <summary>
    /// 获取检验到料筛选上下文（各列去重值）
    /// </summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>
    /// 批量打印检验到料
    /// </summary>
    Task<byte[]> PrintMaterialCheckBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>
    /// 按筛选条件打印全部检验到料
    /// </summary>
    Task<byte[]> PrintMaterialCheckAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? receiveDateFrom, DateTime? receiveDateTo);
}
