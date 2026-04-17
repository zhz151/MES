// 文件路径: MES.Blazor/Services/ICustomerService.cs
using MES.Core.DTOs;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 客户前端服务接口
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// 分页查询客户列表
    /// </summary>
    Task<ApiResponse<PagedResult<CustomerProfileDto>>> GetPagedAsync(QueryParams query);

    /// <summary>
    /// 获取所有客户（用于下拉框）
    /// </summary>
    Task<List<CustomerProfileDto>> GetAllAsync();

    /// <summary>
    /// 根据ID获取客户详情
    /// </summary>
    Task<ApiResponse<CustomerProfileDto>> GetByIdAsync(int id);

    /// <summary>
    /// 创建客户
    /// </summary>
    Task<ApiResponse<CustomerProfileDto>> CreateAsync(CreateCustomerRequest request);

    /// <summary>
    /// 更新客户
    /// </summary>
    Task<ApiResponse<CustomerProfileDto>> UpdateAsync(int id, UpdateCustomerRequest request);

    /// <summary>
    /// 删除客户
    /// </summary>
    Task<ApiResponse<object>> DeleteAsync(int id);
}