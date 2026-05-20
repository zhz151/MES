using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;

namespace MES.Tests.Controllers;

public class ProductionRecordControllerTests : ControllerTestBase
{
    private readonly Mock<IProductionRecordService> _serviceMock;
    private readonly Mock<ILogger<ProductionRecordController>> _loggerMock;
    private readonly ProductionRecordController _controller;

    public ProductionRecordControllerTests()
    {
        _serviceMock = new Mock<IProductionRecordService>();
        _loggerMock = CreateLoggerMock<ProductionRecordController>();
        _controller = new ProductionRecordController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetProductionRecords_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<ProductionRecordDto>
        {
            Items = new List<ProductionRecordDto> { new() { Id = 1, BatchNo = "REC001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetProductionRecordsAsync(1, It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetProductionRecords(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<ProductionRecordDto>>>(result);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task CreateProductionRecord_ReturnsOk()
    {
        // Arrange
        var request = new CreateProductionRecordRequest { BatchNo = "REC001" };
        var dto = new ProductionRecordDto { Id = 1, BatchNo = "REC001" };
        _serviceMock.Setup(x => x.CreateProductionRecordAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.CreateProductionRecord(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProductionRecordDto>>(result);
        Assert.Equal("REC001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task CreateProductionRecord_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.CreateProductionRecord(new CreateProductionRecordRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ProductionRecordDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task BatchCreateProductionRecords_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateProductionRecordRequest> { new() { BatchNo = "REC001" } };
        var dtos = new List<ProductionRecordDto> { new() { Id = 1, BatchNo = "REC001" } };
        _serviceMock.Setup(x => x.BatchCreateProductionRecordsAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.BatchCreateProductionRecords(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<ProductionRecordDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task DeleteProductionRecord_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteProductionRecordAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteProductionRecord(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task UpdateProductionRecord_ReturnsOk()
    {
        // Arrange
        var request = new UpdateProductionRecordRequest { ExecDate = DateTime.Now };
        var dto = new ProductionRecordDto { Id = 1, BatchNo = "REC002" };
        _serviceMock.Setup(x => x.UpdateProductionRecordAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.UpdateProductionRecord(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<ProductionRecordDto>>(result);
        Assert.Equal("REC002", response.Data?.BatchNo);
    }

    [Fact]
    public async Task UpdateProductionRecord_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.UpdateProductionRecord(1, new UpdateProductionRecordRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<ProductionRecordDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetSectionOutsources_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<SectionOutsourceDto>
        {
            Items = new List<SectionOutsourceDto> { new() { Id = 1, BatchNo = "BATCH001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetSectionOutsourcesAsync(1, It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetSectionOutsources(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<SectionOutsourceDto>>>(result);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task CreateSectionOutsource_ReturnsOk()
    {
        // Arrange
        var request = new CreateSectionOutsourceRequest { BatchNo = "BATCH001" };
        var dto = new SectionOutsourceDto { Id = 1, BatchNo = "BATCH001" };
        _serviceMock.Setup(x => x.CreateSectionOutsourceAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.CreateSectionOutsource(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<SectionOutsourceDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task DeleteSectionOutsource_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteSectionOutsourceAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteSectionOutsource(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetOutsourceRecoveries_ReturnsOk()
    {
        // Arrange
        var list = new List<OutsourceRecoveryDto> { new() { Id = 1, SectionOutsourceId = 1 } };
        _serviceMock.Setup(x => x.GetOutsourceRecoveriesAsync(1)).ReturnsAsync(list);

        // Act
        var result = await _controller.GetOutsourceRecoveries(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<OutsourceRecoveryDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task CreateOutsourceRecovery_ReturnsOk()
    {
        // Arrange
        var request = new CreateOutsourceRecoveryRequest { SectionOutsourceId = 1 };
        var dto = new OutsourceRecoveryDto { Id = 1, SectionOutsourceId = 1 };
        _serviceMock.Setup(x => x.CreateOutsourceRecoveryAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.CreateOutsourceRecovery(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<OutsourceRecoveryDto>>(result);
        Assert.Equal(1, response.Data?.SectionOutsourceId);
    }

    [Fact]
    public async Task DeleteOutsourceRecovery_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteOutsourceRecoveryAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteOutsourceRecovery(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetMaterialReceiveCheck_ReturnsOk()
    {
        // Arrange
        var dto = new MaterialReceiveCheckDto { Id = 1, BatchNo = "BATCH001" };
        _serviceMock.Setup(x => x.GetMaterialReceiveCheckAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetMaterialReceiveCheck(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaterialReceiveCheckDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task GetMaterialReceiveCheck_ReturnsDefault_WhenNotFound()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetMaterialReceiveCheckAsync(999)).ReturnsAsync((MaterialReceiveCheckDto?)null);

        // Act
        var result = await _controller.GetMaterialReceiveCheck(999);

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaterialReceiveCheckDto>>(result);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task CreateMaterialReceiveCheck_ReturnsOk()
    {
        // Arrange
        var request = new CreateMaterialReceiveCheckRequest { BatchNo = "BATCH001" };
        var dto = new MaterialReceiveCheckDto { Id = 1, BatchNo = "BATCH001" };
        _serviceMock.Setup(x => x.CreateMaterialReceiveCheckAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.CreateMaterialReceiveCheck(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaterialReceiveCheckDto>>(result);
        Assert.Equal("BATCH001", response.Data?.BatchNo);
    }

    [Fact]
    public async Task RefreshBatchTracking_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.RefreshBatchTrackingFieldsAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RefreshBatchTracking(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetAllProductionRecords_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<ProductionRecordDto>
        {
            Items = new List<ProductionRecordDto> { new() { Id = 1, BatchNo = "REC001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetAllProductionRecordsAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAllProductionRecords();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<ProductionRecordDto>>>(result);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetAllSectionOutsources_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<SectionOutsourceDto>
        {
            Items = new List<SectionOutsourceDto> { new() { Id = 1, BatchNo = "BATCH001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetAllSectionOutsourcesAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAllSectionOutsources();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<SectionOutsourceDto>>>(result);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetAllOutsourceRecoveries_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<OutsourceRecoveryDto>
        {
            Items = new List<OutsourceRecoveryDto> { new() { Id = 1, SectionOutsourceId = 1 } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetAllOutsourceRecoveriesAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAllOutsourceRecoveries();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<OutsourceRecoveryDto>>>(result);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetAllMaterialReceiveChecks_ReturnsOk()
    {
        // Arrange
        var pagedResult = new PagedResult<MaterialReceiveCheckDto>
        {
            Items = new List<MaterialReceiveCheckDto> { new() { Id = 1, BatchNo = "BATCH001" } },
            TotalCount = 1, PageIndex = 1, PageSize = 20
        };
        _serviceMock.Setup(x => x.GetAllMaterialReceiveChecksAsync(It.IsAny<QueryParams>())).ReturnsAsync(pagedResult);

        // Act
        var result = await _controller.GetAllMaterialReceiveChecks();

        // Assert
        var (_, response) = AssertOk<ApiResponse<PagedResult<MaterialReceiveCheckDto>>>(result);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task UpdateMaterialReceiveCheck_ReturnsOk()
    {
        // Arrange
        var request = new UpdateMaterialReceiveCheckRequest { ReceiveDate = DateTime.Now };
        var dto = new MaterialReceiveCheckDto { Id = 1, BatchNo = "BATCH002" };
        _serviceMock.Setup(x => x.UpdateMaterialReceiveCheckAsync(1, request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.UpdateMaterialReceiveCheck(1, request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaterialReceiveCheckDto>>(result);
        Assert.Equal("BATCH002", response.Data?.BatchNo);
    }

    [Fact]
    public async Task DeleteMaterialReceiveCheck_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteMaterialReceiveCheckAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteMaterialReceiveCheck(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task BatchCreateSectionOutsources_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.BatchCreateSectionOutsources(new List<CreateSectionOutsourceRequest>());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<SectionOutsourceDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task BatchCreateSectionOutsources_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateSectionOutsourceRequest> { new() { BatchNo = "BATCH001" } };
        var dtos = new List<SectionOutsourceDto> { new() { Id = 1, BatchNo = "BATCH001" } };
        _serviceMock.Setup(x => x.BatchCreateSectionOutsourcesAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.BatchCreateSectionOutsources(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<SectionOutsourceDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task BatchCreateOutsourceRecoveries_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.BatchCreateOutsourceRecoveries(new List<CreateOutsourceRecoveryRequest>());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<OutsourceRecoveryDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task BatchCreateOutsourceRecoveries_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateOutsourceRecoveryRequest> { new() { SectionOutsourceId = 1 } };
        var dtos = new List<OutsourceRecoveryDto> { new() { Id = 1, SectionOutsourceId = 1 } };
        _serviceMock.Setup(x => x.BatchCreateOutsourceRecoveriesAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.BatchCreateOutsourceRecoveries(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<OutsourceRecoveryDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task BatchCreateMaterialReceiveChecks_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.BatchCreateMaterialReceiveChecks(new List<CreateMaterialReceiveCheckRequest>());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<List<MaterialReceiveCheckDto>>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task BatchCreateMaterialReceiveChecks_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateMaterialReceiveCheckRequest> { new() { BatchNo = "BATCH001" } };
        var dtos = new List<MaterialReceiveCheckDto> { new() { Id = 1, BatchNo = "BATCH001" } };
        _serviceMock.Setup(x => x.BatchCreateMaterialReceiveChecksAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.BatchCreateMaterialReceiveChecks(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<MaterialReceiveCheckDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task PrintProductionRecordBatch_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintProductionRecordBatch(new ProductionRecordPrintBatchRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintProductionRecordBatch_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintProductionRecordBatchAsync(It.IsAny<int[]>(), It.IsAny<List<PrintColumnDef>>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintProductionRecordBatch(new ProductionRecordPrintBatchRequest { Ids = new[] { 1 } });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintProductionRecordAll_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintProductionRecordAll(new ProductionRecordPrintAllRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintProductionRecordAll_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintProductionRecordAllAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<List<PrintColumnDef>>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintProductionRecordAll(new ProductionRecordPrintAllRequest { Keyword = "REC" });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintMaterialCheckBatch_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintMaterialCheckBatch(new MaterialCheckPrintBatchRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintMaterialCheckBatch_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintMaterialCheckBatchAsync(It.IsAny<int[]>(), It.IsAny<List<PrintColumnDef>>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintMaterialCheckBatch(new MaterialCheckPrintBatchRequest { Ids = new[] { 1 } });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintMaterialCheckAll_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintMaterialCheckAll(new MaterialCheckPrintAllRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintMaterialCheckAll_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintMaterialCheckAllAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<List<PrintColumnDef>>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintMaterialCheckAll(new MaterialCheckPrintAllRequest { Keyword = "BATCH" });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }
}
