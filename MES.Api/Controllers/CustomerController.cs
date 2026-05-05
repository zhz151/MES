// 文件路径: MES.Api/Controllers/CustomerController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

/// <summary>
/// 客户控制器
/// </summary>
[ApiController]
[Route("api/customer")]
[Authorize]
public class CustomerController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ILogger<CustomerController> _logger;

    public CustomerController(ICustomerService customerService, ILogger<CustomerController> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    /// <summary>
    /// 分页查询客户列表
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerProfileDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true)
    {
        // 限制最大每页数量
        if (pageSize > 5000) pageSize = 5000;

        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy,
            IsDescending = isDescending
        };

        var result = await _customerService.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<CustomerProfileDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 根据ID获取客户详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<CustomerProfileDto>>> GetById(int id)
    {
        var result = await _customerService.GetByIdAsync(id);
        return Ok(ApiResponse<CustomerProfileDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建客户
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
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
    [Authorize(Roles = $"{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<CustomerProfileDto>>> Update(int id, [FromBody] UpdateCustomerRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<CustomerProfileDto>.Fail("请求参数无效"));
        }

        var result = await _customerService.UpdateAsync(id, request);
        return Ok(ApiResponse<CustomerProfileDto>.Ok(result, "更新成功"));
    }

    // ========== 打印 ==========

    /// <summary>
    /// 打印单个客户
    /// </summary>
    [HttpGet("{id}/print")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintCustomer(int id)
    {
        var pdfBytes = await _customerService.PrintCustomerAsync(id);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    /// <summary>
    /// 批量打印客户
    /// </summary>
    [HttpPost("print-batch")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintCustomerBatch([FromBody] OrderPrintBatchRequest request)
    {
        var pdfBytes = await _customerService.PrintCustomerBatchAsync(request.Ids);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    /// <summary>
    /// 按筛选条件打印全部客户
    /// </summary>
    [HttpPost("print-all")]
    [Authorize(Roles = $"{Roles.Staffs.Order},{Roles.Directors.Order},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintCustomerAll([FromBody] OrderPrintAllRequest request)
    {
        var pdfBytes = await _customerService.PrintCustomerAllAsync(request.Keyword, request.SortBy, request.IsDescending);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    /// <summary>
    /// 删除客户（物理删除）
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _customerService.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }
}