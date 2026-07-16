using FluentAssertions;
using MES.Blazor.Pages.Materials;
using MES.Blazor.Services;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;

namespace MES.Tests.Components;

/// <summary>
/// SubcontractOrders.razor 组件测试
/// 验证外协订单状态枚举中文显示正确性
/// </summary>
public class SubcontractOrdersTests : TestBase
{
    public SubcontractOrdersTests()
    {
        RegisterServices(typeof(SubcontractOrderService));

        // OnInitializedAsync 调用的端点
        ConfigureResponse("/api/subcontract/procurement-status", new ApiResponse<List<ProcurementStatusDto>> { Success = true, Code = 200, Data = new List<ProcurementStatusDto>() });
        ConfigureResponse("/api/subcontract/sync-all", new ApiResponse<object> { Success = true, Code = 200, Message = "OK", Data = new { } }, "POST");
        ConfigureResponse("/api/subcontract/mismatched-orders", new ApiResponse<List<OrderMismatchInfo>> { Success = true, Code = 200, Data = new List<OrderMismatchInfo>() });
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<SubcontractOrders>();
        cut.Markup.Should().Contain("圆棒穿孔");
    }

    [Fact]
    public void StatusColumn_RendersSentAs已发出未收回()
    {
        ConfigureListResponse(new List<SubcontractOrderDto>
        {
            new()
            {
                Id = 1, OrderNo = "SC-001", SupplierName = "测试供应商",
                OrderDate = DateTime.Today, Status = SubcontractOrderStatus.Sent,
                ProcessType = SubcontractProcessType.Cutting, OutMaterialCategory = MaterialCategory.RoughTube,
                OutPlantGrade = "304", OutSpecification = "89×10",
                OutQuantity = 100, OutWeight = 5000, SupplierId = 1,
                IsForceCompleted = false
            }
        });

        var cut = Ctx.RenderComponent<SubcontractOrders>();
        cut.Markup.Should().Contain("已发出");
    }

    [Fact]
    public void StatusColumn_RendersPartialReturnedAs部分收回()
    {
        ConfigureListResponse(new List<SubcontractOrderDto>
        {
            new()
            {
                Id = 2, OrderNo = "SC-002", SupplierName = "测试供应商",
                OrderDate = DateTime.Today, Status = SubcontractOrderStatus.PartialReturned,
                ProcessType = SubcontractProcessType.Cutting, OutMaterialCategory = MaterialCategory.RoughTube,
                OutPlantGrade = "304", OutSpecification = "89×10",
                OutQuantity = 100, OutWeight = 5000, SupplierId = 1,
                IsForceCompleted = false
            }
        });

        var cut = Ctx.RenderComponent<SubcontractOrders>();
        cut.Markup.Should().Contain("部分收回");
    }

    [Fact]
    public void StatusColumn_RendersCompletedAs已完成()
    {
        ConfigureListResponse(new List<SubcontractOrderDto>
        {
            new()
            {
                Id = 3, OrderNo = "SC-003", SupplierName = "测试供应商",
                OrderDate = DateTime.Today, Status = SubcontractOrderStatus.Completed,
                ProcessType = SubcontractProcessType.Cutting, OutMaterialCategory = MaterialCategory.RoughTube,
                OutPlantGrade = "304", OutSpecification = "89×10",
                OutQuantity = 100, OutWeight = 5000, SupplierId = 1,
                IsForceCompleted = false
            }
        });

        var cut = Ctx.RenderComponent<SubcontractOrders>();
        cut.Markup.Should().Contain("已完成");
    }

    private void ConfigureListResponse(List<SubcontractOrderDto> items)
    {
        ConfigureEmptyResponse("/api/subcontract/list");

        var pagedResult = new PagedResult<SubcontractOrderDto>
        {
            Items = items,
            TotalCount = items.Count,
            PageIndex = 1,
            PageSize = 20
        };
        var response = new ApiResponse<PagedResult<SubcontractOrderDto>>
        {
            Success = true,
            Code = 200,
            Message = "OK",
            Data = pagedResult
        };
        ConfigureResponse("/api/subcontract/list", response);
    }
}
