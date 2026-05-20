using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers;

[ApiController]
[Route("api/equipment")]
[Authorize]
public class EquipmentController : ControllerBase
{
    private readonly IEquipmentService _service;
    private readonly ILogger<EquipmentController> _logger;

    public EquipmentController(IEquipmentService service, ILogger<EquipmentController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.Equipment},{Roles.Directors.Equipment},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<EquipmentListDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] string? lifecycleStatus = null,
        [FromQuery] string? usageType = null,
        [FromQuery] string? runningStatus = null,
        [FromQuery] string? inspectionStatus = null,
        [FromQuery] string? maintStatus = null,
        [FromQuery] string? location = null,
        [FromQuery] string? relatedSection = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new EquipmentQueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy,
            IsDescending = isDescending,
            LifecycleStatus = lifecycleStatus,
            UsageType = usageType,
            RunningStatus = runningStatus,
            InspectionStatus = inspectionStatus,
            MaintStatus = maintStatus,
            Location = location,
            RelatedSection = relatedSection
        };
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<EquipmentListDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("all")]
    [Authorize(Roles = $"{Roles.Staffs.Equipment},{Roles.Directors.Equipment},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<EquipmentListDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<EquipmentListDto>>.Ok(result, "查询成功"));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Equipment},{Roles.Directors.Equipment},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<EquipmentDetailDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<EquipmentDetailDto>.Ok(result, "查询成功"));
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Staffs.Equipment},{Roles.Directors.Equipment},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<EquipmentDetailDto>>> Create([FromBody] CreateEquipmentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<EquipmentDetailDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<EquipmentDetailDto>.Ok(result, "创建成功"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Equipment},{Roles.Directors.Equipment},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<EquipmentDetailDto>>> Update(int id, [FromBody] UpdateEquipmentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<EquipmentDetailDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<EquipmentDetailDto>.Ok(result, "更新成功"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Equipment},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>
    /// 批量打印设备台账
    /// </summary>
    [HttpPost("print-batch")]
    [Authorize(Roles = $"{Roles.Staffs.Equipment},{Roles.Directors.Equipment},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintBatch([FromBody] EquipmentPrintBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintBatchAsync(request.Ids, request.Columns);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }

    /// <summary>
    /// 按筛选条件打印全部设备台账
    /// </summary>
    [HttpPost("print-all")]
    [Authorize(Roles = $"{Roles.Staffs.Equipment},{Roles.Directors.Equipment},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintAll([FromBody] EquipmentPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var query = new EquipmentQueryParams
        {
            Keyword = request.Keyword,
            SortBy = string.IsNullOrEmpty(request.SortBy) ? "CreatedTime" : request.SortBy,
            IsDescending = request.IsDescending,
            LifecycleStatus = request.LifecycleStatus,
            UsageType = request.UsageType,
            RunningStatus = request.RunningStatus,
            InspectionStatus = request.InspectionStatus,
            MaintStatus = request.MaintStatus,
            Location = request.Location,
            RelatedSection = request.RelatedSection
        };
        var pdfBytes = await _service.PrintAllAsync(query, request.Columns);
        var base64 = Convert.ToBase64String(pdfBytes);
        return Ok(ApiResponse<string>.Ok(base64, "打印成功"));
    }
}
