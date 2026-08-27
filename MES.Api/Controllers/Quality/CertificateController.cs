using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.DTOs.Quality;
using MES.Core.Interfaces.Quality;
using MES.Core.Models;
using MES.Shared.Constants;

namespace MES.Api.Controllers.Quality;

/// <summary>
/// 质量证明书控制器
/// </summary>
[ApiController]
[Route("api/certificate")]
[Authorize]
public class CertificateController : ControllerBase
{
    private readonly ICertificateService _service;

    public CertificateController(ICertificateService service)
    {
        _service = service;
    }

    /// <summary>
    /// 分页查询质保书列表
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<PagedResult<CertificateDto>>>> GetAll(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = false,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        var query = new QueryParams
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = sortBy ?? "issuedate",
            IsDescending = isDescending
        };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetAllAsync(query);
        return Ok(ApiResponse<PagedResult<CertificateDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取质保书详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<CertificateDetailDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<CertificateDetailDto>.Fail("质保书不存在"));
        return Ok(ApiResponse<CertificateDetailDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 创建质保书
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse<CertificateDetailDto>>> Create(
        [FromBody] CertificateCreateRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<CertificateDetailDto>.Fail("请求参数无效"));
        var result = await _service.CreateAsync(request);
        return Ok(ApiResponse<CertificateDetailDto>.Ok(result, "创建成功"));
    }

    /// <summary>
    /// 更新质保书
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse<CertificateDetailDto>>> Update(
        int id, [FromBody] CertificateUpdateRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<CertificateDetailDto>.Fail("请求参数无效"));
        var result = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<CertificateDetailDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 删除质保书
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Policies.QualityDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    /// <summary>
    /// 自动填充检验数据 — 根据炉号+生产批号查询化学分析/成品检验/拉伸检验
    /// </summary>
    [HttpPost("auto-fill")]
    [Authorize(Roles = Roles.Policies.QualityEdit)]
    public async Task<ActionResult<ApiResponse<List<CertificateItemDto>>>> AutoFill(
        [FromBody] AutoFillInspectionRequest request)
    {
        if (request.Items.Count == 0)
            return BadRequest(ApiResponse<List<CertificateItemDto>>.Fail("请提供需要填充的子项"));
        var result = await _service.AutoFillInspectionDataAsync(request.Items);
        return Ok(ApiResponse<List<CertificateItemDto>>.Ok(result, "填充成功"));
    }

    /// <summary>
    /// 获取筛选上下文
    /// </summary>
    [HttpGet("filter-contexts")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetFilterContexts()
    {
        var result = await _service.GetFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取下一个质保书编号
    /// </summary>
    [HttpGet("next-no")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<ActionResult<ApiResponse<string>>> GetNextCertificateNo(
        [FromQuery] string orderNo)
    {
        if (string.IsNullOrWhiteSpace(orderNo))
            return BadRequest(ApiResponse<string>.Fail("订单号不能为空"));
        var result = await _service.GetNextCertificateNoAsync(orderNo);
        return Ok(ApiResponse<string>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 打印 PDF：按 Id 集合（详情页单张 / 列表页选中或全部）生成质量证明书 PDF
    /// </summary>
    [HttpPost("print-file")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<IActionResult> PrintFile([FromBody] CertificatePrintRequest request)
    {
        var pdfBytes = await _service.PrintFileAsync(request);
        return File(pdfBytes, "application/pdf", "质量证明书.pdf");
    }

    /// <summary>打印选中列表（按当前可见列渲染列表 PDF，Mode A 前端已准备数据）</summary>
    [HttpPost("print-list-file")]
    [Authorize(Roles = Roles.Policies.QualityView)]
    public async Task<IActionResult> PrintListFile([FromBody] CertificatePrintListRequest request)
    {
        var pdfBytes = await _service.PrintCertificateListAsync(request.Title, request.Items, request.Columns);
        return File(pdfBytes, "application/pdf", "质量证明书列表.pdf");
    }
}
