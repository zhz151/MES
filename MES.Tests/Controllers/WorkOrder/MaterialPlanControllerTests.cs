using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MES.Api.Controllers.WorkOrder;
using MES.Core.DTOs;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Core.Enums;
using MES.Core.Exceptions;

namespace MES.Tests.Controllers;

public class MaterialPlanControllerTests : ControllerTestBase
{
    private readonly Mock<IMaterialPlanService> _serviceMock;
    private readonly MaterialPlanController _controller;

    public MaterialPlanControllerTests()
    {
        _serviceMock = new Mock<IMaterialPlanService>();
        _controller = new MaterialPlanController(_serviceMock.Object);
    }

    [Fact]
    public async Task GetSemiPlans_ReturnsOk()
    {
        // Arrange
        var list = new List<PurchaseSemiPlanDto> { new() { Id = 1, PlantGrade = "原料" } };
        _serviceMock.Setup(x => x.GetSemiPlansAsync(1)).ReturnsAsync(list);

        // Act
        var result = await _controller.GetSemiPlans(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<PurchaseSemiPlanDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetSemiPlanById_ReturnsOk()
    {
        // Arrange
        var dto = new PurchaseSemiPlanDto { Id = 1, PlantGrade = "原料" };
        _serviceMock.Setup(x => x.GetSemiPlanByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetSemiPlanById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PurchaseSemiPlanDto>>(result);
        Assert.Equal("原料", response.Data?.PlantGrade);
    }

    [Fact]
    public async Task CreateSemiPlan_ReturnsOk()
    {
        // Arrange
        var request = new CreatePurchaseSemiPlanRequest { PlantGrade = "原料" };
        var dto = new PurchaseSemiPlanDto { Id = 1, PlantGrade = "原料" };
        _serviceMock.Setup(x => x.CreateSemiPlanAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.CreateSemiPlan(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PurchaseSemiPlanDto>>(result);
        Assert.Equal("原料", response.Data?.PlantGrade);
    }

    [Fact]
    public async Task CreateSemiPlan_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.CreateSemiPlan(new CreatePurchaseSemiPlanRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<PurchaseSemiPlanDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task DeleteSemiPlan_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteSemiPlanAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteSemiPlan(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetFinishedPlans_ReturnsOk()
    {
        // Arrange
        var list = new List<PurchaseFinishedPlanDto> { new() { Id = 1, PlantGrade = "成品" } };
        _serviceMock.Setup(x => x.GetFinishedPlansAsync(1)).ReturnsAsync(list);

        // Act
        var result = await _controller.GetFinishedPlans(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<PurchaseFinishedPlanDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetFinishedPlanById_ReturnsOk()
    {
        // Arrange
        var dto = new PurchaseFinishedPlanDto { Id = 1, PlantGrade = "成品" };
        _serviceMock.Setup(x => x.GetFinishedPlanByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetFinishedPlanById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PurchaseFinishedPlanDto>>(result);
        Assert.Equal("成品", response.Data?.PlantGrade);
    }

    [Fact]
    public async Task CreateFinishedPlan_ReturnsOk()
    {
        // Arrange
        var request = new CreatePurchaseFinishedPlanRequest { PlantGrade = "成品" };
        var dto = new PurchaseFinishedPlanDto { Id = 1, PlantGrade = "成品" };
        _serviceMock.Setup(x => x.CreateFinishedPlanAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.CreateFinishedPlan(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<PurchaseFinishedPlanDto>>(result);
        Assert.Equal("成品", response.Data?.PlantGrade);
    }

    [Fact]
    public async Task CreateFinishedPlan_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.CreateFinishedPlan(new CreatePurchaseFinishedPlanRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<PurchaseFinishedPlanDto>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task CreateFinishedPlanBatch_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreatePurchaseFinishedPlanRequest> { new() { PlantGrade = "成品" } };
        var dtos = new List<PurchaseFinishedPlanDto> { new() { Id = 1, PlantGrade = "成品" } };
        _serviceMock.Setup(x => x.CreateFinishedPlanBatchAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.CreateFinishedPlanBatch(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<PurchaseFinishedPlanDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task DeleteFinishedPlan_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteFinishedPlanAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteFinishedPlan(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetInventoryPlans_ReturnsOk()
    {
        // Arrange
        var list = new List<InventoryPlanDto> { new() { Id = 1, PlantGrade = "库存" } };
        _serviceMock.Setup(x => x.GetInventoryPlansAsync(1)).ReturnsAsync(list);

        // Act
        var result = await _controller.GetInventoryPlans(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<InventoryPlanDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetReworkPlans_ReturnsOk()
    {
        // Arrange
        var list = new List<InventoryPlanDto> { new() { Id = 1, PlantGrade = "返工" } };
        _serviceMock.Setup(x => x.GetReworkPlansAsync(1)).ReturnsAsync(list);

        // Act
        var result = await _controller.GetReworkPlans(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<InventoryPlanDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetAvailableInventory_ReturnsOk()
    {
        // Arrange
        var list = new List<AvailableInventoryBatchDto> { new() { Id = 1, BatchNo = "BATCH001" } };
        _serviceMock.Setup(x => x.GetAvailableInventoryAsync(1, It.IsAny<int?>())).ReturnsAsync(list);

        // Act
        var result = await _controller.GetAvailableInventory(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<AvailableInventoryBatchDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetAvailableReworkInventory_ReturnsOk()
    {
        // Arrange
        var list = new List<AvailableInventoryBatchDto> { new() { Id = 1, BatchNo = "BATCH001" } };
        _serviceMock.Setup(x => x.GetAvailableReworkInventoryAsync(1, It.IsAny<ReworkType>(), It.IsAny<int?>())).ReturnsAsync(list);

        // Act
        var result = await _controller.GetAvailableReworkInventory(1, ReworkType.FewerPass);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<AvailableInventoryBatchDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task CreateInventoryPlan_ReturnsOk()
    {
        // Arrange
        var request = new CreateInventoryPlanRequest { MaterialType = "库存" };
        var dto = new InventoryPlanDto { Id = 1, PlantGrade = "库存" };
        _serviceMock.Setup(x => x.CreateInventoryPlanAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.CreateInventoryPlan(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<InventoryPlanDto>>(result);
        Assert.Equal("库存", response.Data?.PlantGrade);
    }

    [Fact]
    public async Task CreateInventoryPlanBatch_ReturnsOk()
    {
        // Arrange
        var requests = new List<CreateInventoryPlanRequest> { new() { MaterialType = "库存" } };
        var dtos = new List<InventoryPlanDto> { new() { Id = 1, PlantGrade = "库存" } };
        _serviceMock.Setup(x => x.CreateInventoryPlanBatchAsync(requests)).ReturnsAsync(dtos);

        // Act
        var result = await _controller.CreateInventoryPlanBatch(requests);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<InventoryPlanDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task DeleteInventoryPlan_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteInventoryPlanAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteInventoryPlan(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task GetPiercingPlans_ReturnsOk()
    {
        // Arrange
        var list = new List<RoundBarPiercingPlanDto> { new() { Id = 1, PlantGrade = "圆棒" } };
        _serviceMock.Setup(x => x.GetPiercingPlansAsync(1)).ReturnsAsync(list);

        // Act
        var result = await _controller.GetPiercingPlans(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<List<RoundBarPiercingPlanDto>>>(result);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetPiercingPlanById_ReturnsOk()
    {
        // Arrange
        var dto = new RoundBarPiercingPlanDto { Id = 1, PlantGrade = "圆棒" };
        _serviceMock.Setup(x => x.GetPiercingPlanByIdAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetPiercingPlanById(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<RoundBarPiercingPlanDto>>(result);
        Assert.Equal("圆棒", response.Data?.PlantGrade);
    }

    [Fact]
    public async Task CreatePiercingPlan_ReturnsOk()
    {
        // Arrange
        var request = new CreateRoundBarPiercingPlanRequest { PlantGrade = "圆棒" };
        var dto = new RoundBarPiercingPlanDto { Id = 1, PlantGrade = "圆棒" };
        _serviceMock.Setup(x => x.CreatePiercingPlanAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.CreatePiercingPlan(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<RoundBarPiercingPlanDto>>(result);
        Assert.Equal("圆棒", response.Data?.PlantGrade);
    }

    [Fact]
    public async Task DeletePiercingPlan_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeletePiercingPlanAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeletePiercingPlan(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task Calculate_ReturnsOk()
    {
        // Arrange
        var request = new MaterialCalculateRequest();
        var dto = new MaterialCalculateResult { RequiredWeight = 100m };
        _serviceMock.Setup(x => x.CalculateAsync(request)).ReturnsAsync(dto);

        // Act
        var result = await _controller.Calculate(request);

        // Assert
        var (_, response) = AssertOk<ApiResponse<MaterialCalculateResult>>(result);
        Assert.Equal(100m, response.Data?.RequiredWeight);
    }

    [Fact]
    public async Task Calculate_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.Calculate(new MaterialCalculateRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<MaterialCalculateResult>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetSummary_ReturnsOk()
    {
        // Arrange
        var dto = new WorkOrderMaterialPlanDto { WorkOrderId = 1 };
        _serviceMock.Setup(x => x.GetWorkOrderMaterialPlanAsync(1)).ReturnsAsync(dto);

        // Act
        var result = await _controller.GetSummary(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<WorkOrderMaterialPlanDto>>(result);
        Assert.Equal(1, response.Data?.WorkOrderId);
    }

    [Fact]
    public async Task RefreshStatus_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.UpdateMaterialPlanStatusAsync(1)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RefreshStatus(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task PrintSemiPlan_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintSemiPlanAsync(1)).ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintSemiPlan(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintFinishedPlan_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintFinishedPlanAsync(1)).ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintFinishedPlan(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintInventoryPlan_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintInventoryPlanAsync(1)).ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintInventoryPlan(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintReworkPlan_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintReworkPlanAsync(1)).ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintReworkPlan(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintPiercingPlan_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintPiercingPlanAsync(1)).ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintPiercingPlan(1);

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintBatch_ReturnsBadRequest_WhenModelInvalid()
    {
        // Arrange
        AddModelError(_controller);

        // Act
        var result = await _controller.PrintBatch(new MaterialPlanBatchPrintRequest());

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintBatch_ReturnsBadRequest_WhenNoWorkOrderIds()
    {
        // Act
        var result = await _controller.PrintBatch(new MaterialPlanBatchPrintRequest { IncludeSemi = true });

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintBatch_ReturnsBadRequest_WhenNoPlanType()
    {
        // Act
        var result = await _controller.PrintBatch(new MaterialPlanBatchPrintRequest { WorkOrderIds = new[] { 1 } });

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task PrintBatch_ReturnsOk()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintSelectedPlansAsync(It.IsAny<MaterialPlanBatchPrintRequest>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.PrintBatch(new MaterialPlanBatchPrintRequest
        { WorkOrderIds = new[] { 1 }, IncludeSemi = true });

        // Assert
        var (_, response) = AssertOk<ApiResponse<string>>(result);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task PrintBatch_ReturnsBadRequest_WhenBusinessException()
    {
        // Arrange
        _serviceMock.Setup(x => x.PrintSelectedPlansAsync(It.IsAny<MaterialPlanBatchPrintRequest>()))
            .ThrowsAsync(new BusinessException("打印失败"));

        // Act
        var result = await _controller.PrintBatch(new MaterialPlanBatchPrintRequest
        { WorkOrderIds = new[] { 1 }, IncludeSemi = true });

        // Assert
        var (_, response) = AssertBadRequest<ApiResponse<string>>(result);
        Assert.False(response.Success);
    }
}
