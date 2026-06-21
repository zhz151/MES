using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

public interface IStandardRegisterService
{
    Task<PagedResult<StandardRegisterDto>> GetPagedAsync(QueryParams query);
    Task<StandardRegisterDto?> GetByIdAsync(int id);
    Task<bool> SaveAsync(StandardRegisterDto dto);
    Task<bool> DeleteAsync(int id);
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>获取全部标准号（用于下拉选择）</summary>
    Task<List<StandardRegisterDto>> GetAllAsync();

    // 子项目
    Task<List<StandardRegisterItemDto>> GetItemsAsync(int standardRegisterId);
    Task<bool> SaveItemAsync(StandardRegisterItemDto dto);
    Task<bool> DeleteItemAsync(int id);
}
