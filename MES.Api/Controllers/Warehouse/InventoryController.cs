using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MES.Core.Models;
using MES.Shared.Constants;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.Warehouse;
using MES.Core.Enums;
using MES.Core.Interfaces.Warehouse;

namespace MES.Api.Controllers.Warehouse;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _service;

    public InventoryController(IInventoryService service)
    {
        _service = service;
    }

    /// <summary>
    /// 分页查询库存列表
    /// </summary>
    [HttpGet("list")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<InventoryBatchDto>>>> GetPaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] int? warehouseId = null,
        [FromQuery] string? materialType = null,
        [FromQuery] string? plantGrade = null,
        [FromQuery] bool onlyWithStock = true,
        [FromQuery] string? workOrderNo = null,
        [FromQuery] string? batchNo = null,
        [FromQuery] string? inboundSource = null,
        [FromQuery] string? sourceName = null,
        [FromQuery] DateTime? inboundDateFrom = null,
        [FromQuery] DateTime? inboundDateTo = null,
        [FromQuery] string? heatNo = null,
        [FromQuery] string? specification = null,
        [FromQuery] string? lengthStatus = null,
        [FromQuery] string? surfaceCondition = null,
        [FromQuery] string? defectReason = null,
        [FromQuery] string? liabilityType = null,
        [FromQuery] string? productionBatchNo = null,
        [FromQuery] string? actualSpecification = null,
        [FromQuery] string? originalSupplier = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        InventoryQueryParams query = new()
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy,
            IsDescending = isDescending,
            WarehouseId = warehouseId,
            MaterialType = materialType,
            PlantGrade = plantGrade,
            OnlyWithStock = onlyWithStock,
            WorkOrderNo = workOrderNo,
            BatchNo = batchNo,
            InboundSource = string.IsNullOrEmpty(inboundSource) ? null : Enum.Parse<InboundSource>(inboundSource),
            SourceName = sourceName,
            InboundDateFrom = inboundDateFrom,
            InboundDateTo = inboundDateTo,
            HeatNo = heatNo,
            Specification = specification,
            LengthStatus = lengthStatus,
            SurfaceCondition = surfaceCondition,
            DefectReason = defectReason,
            LiabilityType = liabilityType,
            ProductionBatchNo = productionBatchNo,
            ActualSpecification = actualSpecification,
            OriginalSupplier = originalSupplier
        };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetPagedAsync(query);
        return Ok(ApiResponse<PagedResult<InventoryBatchDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 全量查询库存批次（无分页，供前端 Items 模式使用）
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<InventoryBatchDto>>>> GetAll(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] int? warehouseId = null,
        [FromQuery] string? materialType = null,
        [FromQuery] string? plantGrade = null,
        [FromQuery] bool onlyWithStock = true,
        [FromQuery] string? workOrderNo = null,
        [FromQuery] string? batchNo = null,
        [FromQuery] string? inboundSource = null,
        [FromQuery] string? sourceName = null,
        [FromQuery] DateTime? inboundDateFrom = null,
        [FromQuery] DateTime? inboundDateTo = null,
        [FromQuery] string? heatNo = null,
        [FromQuery] string? specification = null,
        [FromQuery] string? lengthStatus = null,
        [FromQuery] string? surfaceCondition = null,
        [FromQuery] string? defectReason = null,
        [FromQuery] string? liabilityType = null,
        [FromQuery] string? productionBatchNo = null,
        [FromQuery] string? actualSpecification = null,
        [FromQuery] string? originalSupplier = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        InventoryQueryParams query = new()
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy,
            IsDescending = isDescending,
            WarehouseId = warehouseId,
            MaterialType = materialType,
            PlantGrade = plantGrade,
            OnlyWithStock = onlyWithStock,
            WorkOrderNo = workOrderNo,
            BatchNo = batchNo,
            InboundSource = string.IsNullOrEmpty(inboundSource) ? null : Enum.Parse<InboundSource>(inboundSource),
            SourceName = sourceName,
            InboundDateFrom = inboundDateFrom,
            InboundDateTo = inboundDateTo,
            HeatNo = heatNo,
            Specification = specification,
            LengthStatus = lengthStatus,
            SurfaceCondition = surfaceCondition,
            DefectReason = defectReason,
            LiabilityType = liabilityType,
            ProductionBatchNo = productionBatchNo,
            ActualSpecification = actualSpecification,
            OriginalSupplier = originalSupplier
        };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetAllListAsync(query);
        return Ok(ApiResponse<List<InventoryBatchDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 获取批次详情
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<InventoryBatchDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(ApiResponse<InventoryBatchDto>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 批量入库
    /// </summary>
    [HttpPost("batch-inbound")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<BatchInboundResult>>> BatchInbound([FromBody] BatchInboundRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<BatchInboundResult>.Fail("请求参数无效"));

        var result = await _service.BatchInboundAsync(request);
        return Ok(ApiResponse<BatchInboundResult>.Ok(result, $"批量入库成功，共{result.SuccessCount}条"));
    }

    /// <summary>
    /// 入库
    /// </summary>
    [HttpPost("inbound")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<InventoryBatchDto>>> Inbound([FromBody] CreateInboundRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<InventoryBatchDto>.Fail("请求参数无效"));

        var result = await _service.InboundAsync(request);
        return Ok(ApiResponse<InventoryBatchDto>.Ok(result, "入库成功"));
    }

    /// <summary>
    /// 出库
    /// </summary>
    [HttpPost("outbound")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<OutboundRecordDto>>> Outbound([FromBody] CreateOutboundRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<OutboundRecordDto>.Fail("请求参数无效"));

        var result = await _service.OutboundAsync(request);
        return Ok(ApiResponse<OutboundRecordDto>.Ok(result, "出库成功"));
    }

    /// <summary>
    /// 批量出库
    /// </summary>
    [HttpPost("batch-outbound")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<BatchOutboundResult>>> BatchOutbound([FromBody] BatchOutboundRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<BatchOutboundResult>.Fail("请求参数无效"));

        var result = await _service.BatchOutboundAsync(request);
        return Ok(ApiResponse<BatchOutboundResult>.Ok(result, $"批量出库成功，共{result.SuccessCount}条"));
    }

    /// <summary>
    /// 查询出库记录
    /// </summary>
    [HttpGet("outbound-records")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<PagedResult<OutboundRecordDto>>>> GetOutboundRecords(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool isDescending = true,
        [FromQuery] int? inventoryBatchId = null,
        [FromQuery] int? warehouseId = null,
        [FromQuery] string? outboundType = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? filters = null)
    {
        if (pageSize > 5000) pageSize = 5000;
        OutboundQueryParams query = new()
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? "CreatedTime" : sortBy,
            IsDescending = isDescending,
            InventoryBatchId = inventoryBatchId,
            WarehouseId = warehouseId,
            OutboundType = outboundType,
            StartDate = startDate,
            EndDate = endDate
        };
        if (!string.IsNullOrEmpty(filters))
            try { query.Filters = JsonSerializer.Deserialize<List<FilterDescriptor>>(filters, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { }
        var result = await _service.GetOutboundRecordsAsync(query);
        return Ok(ApiResponse<PagedResult<OutboundRecordDto>>.Ok(result, "查询成功"));
    }

    /// <summary>
    /// 更新入库批次
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<InventoryBatchDto>>> UpdateInventoryBatch(int id, [FromBody] UpdateInventoryBatchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<InventoryBatchDto>.Fail("请求参数无效"));

        var result = await _service.UpdateInventoryBatchAsync(id, request);
        return Ok(ApiResponse<InventoryBatchDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 物理删除入库批次（仅管理员/主任）
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<bool>>> HardDeleteInventoryBatch(int id)
    {
        await _service.HardDeleteInventoryBatchAsync(id);
        return Ok(ApiResponse<bool>.Ok(true, "删除成功"));
    }

    /// <summary>
    /// 更新出库记录
    /// </summary>
    [HttpPut("outbound-records/{id:long}")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<OutboundRecordDto>>> UpdateOutboundRecord(long id, [FromBody] UpdateOutboundRecordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<OutboundRecordDto>.Fail("请求参数无效"));

        var result = await _service.UpdateOutboundRecordAsync(id, request);
        return Ok(ApiResponse<OutboundRecordDto>.Ok(result, "更新成功"));
    }

    /// <summary>
    /// 物理删除出库记录（仅管理员/主任）
    /// </summary>
    [HttpDelete("outbound-records/{id:long}")]
    [Authorize(Roles = $"{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<bool>>> HardDeleteOutboundRecord(long id)
    {
        await _service.HardDeleteOutboundRecordAsync(id);
        return Ok(ApiResponse<bool>.Ok(true, "删除成功"));
    }

    /// <summary>
    /// 验证来源单号
    /// </summary>
    [HttpPost("validate-source-order")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SourceOrderValidationResult>>> ValidateSourceOrder(
        [FromBody] SourceOrderValidationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SourceOrderValidationResult>.Fail("请求参数无效"));

        var result = await _service.ValidateSourceOrderAsync(request.SourceOrderNo, request.InboundSource.ToString(), request.SourceOrderSequence);
        return Ok(ApiResponse<SourceOrderValidationResult>.Ok(result, "验证完成"));
    }

    /// <summary>
    /// 验证生产批号（检验入库自动填充）
    /// </summary>
    [HttpPost("validate-production-batch")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<SourceOrderValidationResult>>> ValidateProductionBatch(
        [FromBody] ProductionBatchValidationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SourceOrderValidationResult>.Fail("请求参数无效"));

        var result = await _service.ValidateProductionBatchAsync(request.ProductionBatchNo);
        return Ok(ApiResponse<SourceOrderValidationResult>.Ok(result, "验证完成"));
    }

    /// <summary>
    /// 验证仓库内入库数据中的工单号是否在工单管理上下文中存在
    /// </summary>
    [HttpGet("validate-workorder-nos/{warehouseId}")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<string>>>> ValidateWorkOrderNos(int warehouseId)
    {
        var result = await _service.ValidateWarehouseWorkOrderNosAsync(warehouseId);
        return Ok(ApiResponse<List<string>>.Ok(result, "验证完成"));
    }

    /// <summary>
    /// 获取入库批次中工单号不存在的批次列表（实时扫描）
    /// </summary>
    [HttpGet("mismatched-batches")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<List<BatchWorkOrderMismatchDto>>>> GetMismatchedBatches(
        [FromQuery] int? warehouseId = null)
    {
        var result = await _service.GetMismatchedWorkOrderBatchesAsync(warehouseId);
        return Ok(ApiResponse<List<BatchWorkOrderMismatchDto>>.Ok(result, "查询成功"));
    }

    // ========== 打印 ==========

    /// <summary>
    /// 打印全部库存/入库记录
    /// </summary>
    [HttpPost("print-inventory-all")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintInventoryAll([FromBody] InventoryPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintInventoryAllAsync(request);
        return Ok(ApiResponse<string>.Ok(Convert.ToBase64String(pdfBytes), "打印成功"));
    }

    [HttpPost("print-inventory-all-file")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<IActionResult> PrintInventoryAllFile([FromBody] InventoryPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintInventoryAllAsync(request);
        return File(pdfBytes, "application/pdf", request.OnlyWithStock ? "仓库库存列表.pdf" : "入库历史列表.pdf");
    }

    /// <summary>
    /// 打印选中库存/入库记录
    /// </summary>
    [HttpPost("print-inventory-selected")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintInventorySelected([FromBody] InventoryPrintSelectedRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintInventorySelectedAsync(request);
        return Ok(ApiResponse<string>.Ok(Convert.ToBase64String(pdfBytes), "打印成功"));
    }

    [HttpPost("print-inventory-selected-file")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<IActionResult> PrintInventorySelectedFile([FromBody] InventoryPrintSelectedRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintInventorySelectedAsync(request);
        return File(pdfBytes, "application/pdf", "入库批次打印.pdf");
    }

    [HttpPost("print-stock-all-file")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<IActionResult> PrintStockAllFile([FromBody] InventoryPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintStockAllAsync(request);
        return File(pdfBytes, "application/pdf", "仓库库存列表.pdf");
    }

    [HttpPost("print-stock-selected-file")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<IActionResult> PrintStockSelectedFile([FromBody] InventoryPrintSelectedRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintStockSelectedAsync(request);
        return File(pdfBytes, "application/pdf", "库存批次打印.pdf");
    }

    [HttpPost("print-inbound-all-file")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<IActionResult> PrintInboundAllFile([FromBody] InventoryPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintInboundAllAsync(request);
        return File(pdfBytes, "application/pdf", "入库历史列表.pdf");
    }

    [HttpPost("print-inbound-selected-file")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<IActionResult> PrintInboundSelectedFile([FromBody] InventoryPrintSelectedRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintInboundSelectedAsync(request);
        return File(pdfBytes, "application/pdf", "入库批次打印.pdf");
    }

    /// <summary>
    /// 打印全部出库记录
    /// </summary>
    [HttpPost("print-outbound-all")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintOutboundAll([FromBody] OutboundPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintOutboundAllAsync(request);
        return Ok(ApiResponse<string>.Ok(Convert.ToBase64String(pdfBytes), "打印成功"));
    }

    [HttpPost("print-outbound-all-file")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<IActionResult> PrintOutboundAllFile([FromBody] OutboundPrintAllRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintOutboundAllAsync(request);
        return File(pdfBytes, "application/pdf", "出库历史列表.pdf");
    }

    /// <summary>
    /// 打印选中出库记录
    /// </summary>
    [HttpPost("print-outbound-selected")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<string>>> PrintOutboundSelected([FromBody] OutboundPrintSelectedRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintOutboundSelectedAsync(request);
        return Ok(ApiResponse<string>.Ok(Convert.ToBase64String(pdfBytes), "打印成功"));
    }

    [HttpPost("print-outbound-selected-file")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<IActionResult> PrintOutboundSelectedFile([FromBody] OutboundPrintSelectedRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<string>.Fail("请求参数无效"));

        var pdfBytes = await _service.PrintOutboundSelectedAsync(request);
        return File(pdfBytes, "application/pdf", "出库记录打印.pdf");
    }

    // ========== 筛选上下文 ==========

    /// <summary>
    /// 获取出库记录筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    [HttpGet("outbound-filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetOutboundFilterContexts()
    {
        var result = await _service.GetOutboundFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }

    /// <summary>
    /// 获取库存批次筛选上下文（各列去重值），用于 ExcelFilter 下拉选项
    /// </summary>
    [HttpGet("inventory-filter-contexts")]
    [Authorize(Roles = $"{Roles.Staffs.Warehouse},{Roles.Directors.Warehouse},{Roles.Admin}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, List<string>>>>> GetInventoryFilterContexts()
    {
        var result = await _service.GetInventoryFilterContextsAsync();
        return Ok(ApiResponse<Dictionary<string, List<string>>>.Ok(result));
    }
}
