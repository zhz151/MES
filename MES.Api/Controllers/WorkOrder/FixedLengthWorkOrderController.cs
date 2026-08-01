using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.WorkOrder;

[ApiController]
[Route(ApiEndpoints.FixedLengthWorkOrder)]
[Authorize]
public class FixedLengthWorkOrderController : ControllerBase
{
    private readonly IFixedLengthWorkOrderService _service;

    public FixedLengthWorkOrderController(IFixedLengthWorkOrderService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取全部定尺工单定尺数据列表（主号级按长度实时聚合）
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.WorkOrder},{Roles.Directors.WorkOrder},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<FixedLengthWorkOrderListDto>>>> GetList()
    {
        var result = await _service.GetListAsync();
        return Ok(ApiResponse<List<FixedLengthWorkOrderListDto>>.Ok(result));
    }
}
