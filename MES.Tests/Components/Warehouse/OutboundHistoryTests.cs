using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.Warehouse;
using MES.Blazor.Services;
using MES.Core.DTOs.Warehouse;

namespace MES.Tests.Components;

public class OutboundHistoryTests : TestBase
{
    public OutboundHistoryTests()
    {
        RegisterServices(typeof(InventoryService), typeof(WarehouseService));
        ConfigureEmptyResponse("/api/inventory/outbound-records");
        ConfigureEmptyResponse("/api/warehouse/all");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<OutboundHistory>();
        cut.Markup.Should().Contain("出库历史记录查询及更正");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<OutboundHistory>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Fact]
    public void Render_DisplaysData()
    {
        ConfigureListResponse();
        var cut = Ctx.RenderComponent<OutboundHistory>();
        cut.WaitForState(() => cut.Markup.Contains("BATCH-OB-001"));
        cut.Markup.Should().Contain("BATCH-OB-001");
    }

    private void ConfigureListResponse()
    {
        ConfigureEmptyResponse("/api/inventory/outbound-records");
        ConfigureEmptyResponse("/api/warehouse/all");
        var pagedResult = new PagedResult<OutboundRecordDto>
        {
            Items = new List<OutboundRecordDto>
            {
                new()
                {
                    Id = 1,
                    InventoryBatchId = 1,
                    BatchNo = "BATCH-OB-001",
                    OutboundType = "生产领料",
                    OutboundQuantity = 50,
                    OutboundWeight = 2500m,
                    OutboundDate = DateTime.Today
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/inventory/outbound-records", new ApiResponse<PagedResult<OutboundRecordDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
