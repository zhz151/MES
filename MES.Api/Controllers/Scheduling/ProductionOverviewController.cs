using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Services.Scheduling;

namespace MES.Api.Controllers.Scheduling;

[ApiController]
[Route("api/production-overview")]
[Authorize]
public class ProductionOverviewController : ControllerBase
{
    private readonly ProductionOverviewService _service;

    public ProductionOverviewController(ProductionOverviewService service)
    {
        _service = service;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<ProductionOverviewDto>>> GetOverview()
    {
        var result = await _service.GetOverviewAsync();
        return Ok(ApiResponse<ProductionOverviewDto>.Ok(result));
    }
}
