using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.StandardRegister;
namespace MES.Core.Interfaces.StandardRegister;

public interface IStandardRegisterService
{
    Task<PagedResult<StandardRegisterDto>> GetPagedAsync(QueryParams query);
    Task<StandardRegisterDto?> GetByIdAsync(int id);
    /// <summary>保存标准号，返回 Id（0 表示失败）</summary>
    Task<int> SaveAsync(StandardRegisterDto dto);
    Task<bool> DeleteAsync(int id);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>获取全部标准号（用于下拉选择）</summary>
    Task<List<StandardRegisterDto>> GetAllAsync();

    /// <summary>根据标准号解析标准名称（含容错匹配）</summary>
    Task<string?> ResolveNameAsync(string standardNo);

    /// <summary>批量打印选中记录</summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    // 子项目
    Task<List<StandardRegisterItemDto>> GetItemsAsync(int standardRegisterId);
    /// <summary>保存子项目，返回 Id（0 表示失败）</summary>
    Task<int> SaveItemAsync(StandardRegisterItemDto dto);
    Task<bool> DeleteItemAsync(int id);

    /// <summary>清理孤儿子项及序号重复项，返回删除条数</summary>
    Task<int> CleanupOrphanedItemsAsync();
}
