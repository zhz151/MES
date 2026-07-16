using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Interfaces.Auth;
using MES.Core.DTOs.Auth;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Infrastructure;

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public class UserController : ControllerBase
{
    private readonly IUserManagementService _userManagementService;

    public UserController(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<PagedResult<UserDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true)
    {
        if (pageSize > 5000) pageSize = 5000;
        var result = await _userManagementService.GetPagedAsync(pageIndex, pageSize, keyword, sortBy, isDescending);
        return Ok(ApiResponse<PagedResult<UserDto>>.Ok(result.Data!, "查询成功"));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<UserDto>.Fail("请求参数无效"));
        var result = await _userManagementService.CreateAsync(request);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{userId}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(string userId, [FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<UserDto>.Fail("请求参数无效"));
        var result = await _userManagementService.UpdateAsync(userId, request);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{userId}/reset-password")]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(string userId, [FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("请求参数无效"));
        var result = await _userManagementService.ResetPasswordAsync(userId, request);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("{userId}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string userId)
    {
        var result = await _userManagementService.DeleteAsync(userId);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
