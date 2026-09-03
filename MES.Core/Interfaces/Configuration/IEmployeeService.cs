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

    /// <summary>新增或更新
    /// </summary>
    Task<bool> SaveAsync(EmployeeDto dto);

    /// <summary>删除（同时删除自动创建的登录账号）</summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>一键补齐存量启用员工的登录账号（用户名=工号、密码=123456、仅扫码权限），返回新建账号数</summary>
    Task<int> SyncAccountsAsync();

    /// <summary>列头筛选上下文（自由文本列取存量去重值，工段/成检项目列取标准选项）</summary>
    Task<Dictionary<string, List<string>>> GetFilterContextsAsync();

    /// <summary>靠工岗位候选 = 计件活岗（当前在册存在 个人计件/集体计件 员工的岗位），去重英文 Position Key</summary>
    Task<List<string>> GetPiecePositionOptionsAsync();

    /// <summary>批量打印（按ID）</summary>
    Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns);
}
