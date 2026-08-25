using Bunit;
using FluentAssertions;
using MES.Core.Models;
using MES.Blazor.Pages.Warehouse;
using MES.Blazor.Services;
using MES.Core.DTOs.Warehouse;
using MES.Core.Enums;

namespace MES.Tests.Components;

public class InboundHistoryTests : TestBase
{
    public InboundHistoryTests()
    {
        RegisterServices(typeof(InventoryService), typeof(WarehouseService), typeof(NotificationService), typeof(DictValueDefinitionService));
        ConfigureEmptyResponse("/api/inventory/list");
        ConfigureEmptyResponse("/api/warehouse/all");
        ConfigureEmptyResponse("/api/notification/by-type/WorkOrderDeleted");
        ConfigureEmptyResponse("/api/dict-value-definition/enabled-values");
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = RenderPage<InboundHistory>();
        cut.Markup.Should().Contain("入库历史记录查询及更正");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = RenderPage<InboundHistory>();
        cut.Markup.Should().Contain("模糊搜索");
    }

    [Fact]
    public void Render_DisplaysData_WhenCodeProvided()
    {
        ConfigureWarehouseAndListResponse();
        var cut = RenderPage<InboundHistory>(p => p.Add(x => x.Code, "WH01"));
        cut.WaitForState(() => cut.Markup.Contains("BATCH-IB-001"), TimeSpan.FromSeconds(10));
        cut.Markup.Should().Contain("BATCH-IB-001");
    }

    private void ConfigureWarehouseAndListResponse()
    {
        // 仓库响应
        var warehouses = new List<WarehouseDto>
        {
            new() { Id = 1, Code = "WH01", Name = "主仓库" }
        };
        var whResponse = new ApiResponse<List<WarehouseDto>> { Success = true, Code = 200, Data = warehouses };
        ConfigureResponse("/api/warehouse/all", whResponse);

        // 库存列表响应
        ConfigureEmptyResponse("/api/inventory/list");
        ConfigureEmptyResponse("/api/notification/by-type/WorkOrderDeleted");
        var pagedResult = new PagedResult<InventoryBatchDto>
        {
            Items = new List<InventoryBatchDto>
            {
                new()
                {
                    Id = 1,
                    BatchNo = "BATCH-IB-001",
                    PlantGrade = "304",
                    Specification = "219*8",
                    InboundDate = DateTime.Today,
                    InitialQuantity = 100,
                    InitialWeight = 5000m,
                    RemainingQuantity = 100,
                    RemainingWeight = 5000m,
                    MaterialType = MaterialType.RoughTube,
                    InboundSource = InboundSource.Purchase,
                    SourceName = "PO-001"
                }
            },
            TotalCount = 1,
            PageIndex = 1,
            PageSize = 20
        };
        ConfigureResponse("/api/inventory/list", new ApiResponse<PagedResult<InventoryBatchDto>>
        {
            Success = true,
            Code = 200,
            Data = pagedResult
        });
    }
}
