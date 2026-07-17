using FluentAssertions;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Blazor.Pages.Batches;
using MES.Blazor.Services;
using MES.Core.DTOs.Batch;

namespace MES.Tests.Components;

public class BatchesTests : TestBase
{
    public BatchesTests()
    {
        RegisterServices(typeof(BatchService), typeof(ProductionRecordService),
            typeof(WorkOrderService), typeof(OrderService), typeof(MaterialPlanService));
        ConfigureEmptyResponse("/api/batch/list");
        ConfigureEmptyResponse("/api/batch/filter-contexts");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<Batches>();
        cut.Markup.Should().Contain("生产批次");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<Batches>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Theory]
    [InlineData(BatchStatus.None, "未产")]
    [InlineData(BatchStatus.InProgress, "在产")]
    [InlineData(BatchStatus.Completed, "完成")]
    [InlineData(BatchStatus.Suspended, "挂起")]
    [InlineData(BatchStatus.Cancelled, "作废")]
    public void StatusColumn_DisplaysCorrectText(BatchStatus status, string expectedText)
    {
        ConfigureListResponse(status);
        var cut = Ctx.RenderComponent<Batches>();
        cut.Markup.Should().Contain(expectedText);
    }

    private void ConfigureListResponse(BatchStatus status)
    {
        ConfigureEmptyResponse("/api/batch/list");
        var pagedResult = new PagedResult<ProductionBatchListDto>
        {
            Items = new List<ProductionBatchListDto>
            {
                new()
                {
                    Id = 1,
                    BatchNo = "BATCH001",
                    Status = status,
                    WorkOrderNo = "WO001",
                    SalesOrderNo = "SO001",
                    ProductionMainNo = "D01",
                    ManufacturingItem = MaterialType.OrderFinished,
                    CreatedBy = "test",
                    SignDate = DateTime.Today,
                    Salesman = "测试",
                    DeliveryDate = DateTime.Today.AddMonths(1),
                    MaterialName = "无缝管",
                    SettlementMethod = SettlementMethod.Theoretical,
                    StandardCode = "GB/T 8163",
                    DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
                    PlantGrade = "304",
                    Specification = "219*8",
                    LengthStatus = LengthStatus.Fixed,
                    TotalQuantity = 100,
                    TotalMeters = 600,
                    TotalWeight = 2500m,
                    TechnicalRequirements = "NORMAL",
                    ProductionRatio = 1
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/batch/list", new ApiResponse<PagedResult<ProductionBatchListDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
