using MES.Core.Models;

using MES.Core.DTOs.Shared;
using MES.Core.DTOs.ProductionStandard;
namespace MES.Core.Interfaces.ProductionStandard;

public interface IStandardRegisterService
{
    Task<PagedResult<StandardRegisterDto>> GetPagedAsync(QueryParams query);
    Task<StandardRegisterDto?> GetByIdAsync(int id);
    Task<bool> SaveAsync(StandardRegisterDto dto);
    Task<bool> DeleteAsync(int id);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>获取全部标准号（用于下拉选择）</summary>
    Task<List<StandardRegisterDto>> GetAllAsync();

    /// <summary>批量打印选中记录</summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>按条件打印全部记录</summary>
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns);

    // 子项目
    Task<List<StandardRegisterItemDto>> GetItemsAsync(int standardRegisterId);
    Task<bool> SaveItemAsync(StandardRegisterItemDto dto);
    Task<bool> DeleteItemAsync(int id);
}
