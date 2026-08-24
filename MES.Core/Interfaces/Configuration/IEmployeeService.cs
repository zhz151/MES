using MES.Core.Models;

using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Shared;
namespace MES.Core.Interfaces.Configuration;

/// <summary>
/// 员工管理服务接口
/// </summary>
public interface IEmployeeService
{
    /// <summary>分页查询</summary>
    Task<PagedResult<EmployeeDto>> GetPagedAsync(QueryParams query);

    /// <summary>按工号查询（扫码用）</summary>
    Task<EmployeeDto?> GetByCodeAsync(string code);

    /// <summary>新增或更�?/summary>
    Task<bool> SaveAsync(EmployeeDto dto);

    /// <summary>删除</summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>列头筛选上下文（自由文本列取存量去重值，工段/成检项目列取标准选项）</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>批量打印（按ID）</summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);

    /// <summary>按条件打印全部</summary>
    Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns);
}
