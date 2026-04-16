// 文件路径: MES.Api/Controllers/CustomerController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Api.Controllers;

/// <summary>
/// 客户档案控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomerController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomerController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>
    /// 分页查询客户列表
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = "OrderStaff,OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerProfileDto>>>> GetPaged([FromQuery] QueryParams query)
    {
        var result = await _customerService.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<CustomerProfileDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 根据ID获取客户详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "OrderStaff,OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<CustomerProfileDto>>> GetById(int id)
    {
        var result = await _customerService.GetByIdAsync(id);
        return Ok(ApiResponse<CustomerProfileDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建客户
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<CustomerProfileDto>>> Create([FromBody] CreateCustomerRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<CustomerProfileDto>.Fail("请求参数无效"));
        }

        var result = await _customerService.CreateAsync(request);
        return Ok(ApiResponse<CustomerProfileDto>.Ok(result, "创建成功"));
    }

    /// <summary>
    /// 更新客户
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<CustomerProfileDto>>> Update(int id, [FromBody] UpdateCustomerRequest request)
    {
        var result = await _customerService.UpdateAsync(id, request);
        return Ok(ApiResponse<CustomerProfileDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除客户
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        await _customerService.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }
}