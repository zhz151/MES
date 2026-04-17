using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Api.Controllers;

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

    [HttpGet("list")]
    [Authorize(Roles = "OrderStaff,OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerProfileDto>>>> GetPaged([FromQuery] QueryParams query)
    {
        var result = await _customerService.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<CustomerProfileDto>>.Ok(result, "Query successful"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "OrderStaff,OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<CustomerProfileDto>>> GetById(int id)
    {
        var result = await _customerService.GetByIdAsync(id);
        return Ok(ApiResponse<CustomerProfileDto>.Ok(result, "Query successful"));
    }

    [HttpPost]
    [Authorize(Roles = "OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<CustomerProfileDto>>> Create([FromBody] CreateCustomerRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<CustomerProfileDto>.Fail("Invalid request parameters"));
        }

        var result = await _customerService.CreateAsync(request);
        return Ok(ApiResponse<CustomerProfileDto>.Ok(result, "Create successful"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "OrderDirector,Admin")]
    public async Task<ActionResult<ApiResponse<CustomerProfileDto>>> Update(int id, [FromBody] UpdateCustomerRequest request)
    {
        var result = await _customerService.UpdateAsync(id, request);
        return Ok(ApiResponse<CustomerProfileDto>.Ok(result, "Update successful"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        await _customerService.DeleteAsync(id);
        return Ok(ApiResponse.Ok("Delete successful"));
    }
}