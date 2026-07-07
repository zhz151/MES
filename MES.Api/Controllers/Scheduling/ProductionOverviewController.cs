using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Models;
using MES.Core.Interfaces;

namespace MES.Api.Controllers.Scheduling;

[ApiController]
[Route("api/production-overview")]
[Authorize]
public class ProductionOverviewController : ControllerBase
{
    private readonly IProductionOverviewService _service;

    public ProductionOverviewController(IProductionOverviewService service)
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
