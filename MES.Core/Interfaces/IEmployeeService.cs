using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 员工管理服务接口
/// </summary>
public interface IEmployeeService
{
    /// <summary>分页查询</summary>
    Task<PagedResult<EmployeeDto>> GetPagedAsync(QueryParams query);

    /// <summary>按工号查询（扫码用）</summary>
    Task<EmployeeDto?> GetByCodeAsync(string code);

    /// <summary>新增或更新</summary>
    Task<bool> SaveAsync(EmployeeDto dto);

    /// <summary>删除</summary>
    Task<bool> DeleteAsync(int id);
}
