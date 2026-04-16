// 文件路径: MES.Core/Interfaces/ICustomerService.cs
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Core.Interfaces;

/// <summary>
/// 客户档案服务接口
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// 分页查询客户列表
    /// </summary>
    /// <param name="query">分页查询参数</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<CustomerProfileDto>> GetPagedAsync(QueryParams query);

    /// <summary>
    /// 根据ID获取客户详情
    /// </summary>
    /// <param name="id">客户ID</param>
    /// <returns>客户详情</returns>
    Task<CustomerProfileDto> GetByIdAsync(int id);

    /// <summary>
    /// 创建客户
    /// </summary>
    /// <param name="request">创建请求</param>
    /// <returns>创建的客户</returns>
    Task<CustomerProfileDto> CreateAsync(CreateCustomerRequest request);

    /// <summary>
    /// 更新客户
    /// </summary>
    /// <param name="id">客户ID</param>
    /// <param name="request">更新请求</param>
    /// <returns>更新的客户</returns>
    Task<CustomerProfileDto> UpdateAsync(int id, UpdateCustomerRequest request);

    /// <summary>
    /// 删除客户（软删除）
    /// </summary>
    /// <param name="id">客户ID</param>
    Task DeleteAsync(int id);
}