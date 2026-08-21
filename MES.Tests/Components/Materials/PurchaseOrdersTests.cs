using FluentAssertions;
using MES.Blazor.Pages.Materials;
using MES.Blazor.Services;
using MES.Core.Enums;
using MES.Core.Models;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;

namespace MES.Tests.Components;

/// <summary>
/// PurchaseOrders.razor 组件测试
/// 验证采购订单状态枚举中文显示正确性
/// </summary>
public class PurchaseOrdersTests : TestBase
{
    public PurchaseOrdersTests()
    {
        // 注册服务
        RegisterServices(typeof(PurchaseOrderService), typeof(SupplierService));

        // 配置 OnInitializedAsync 中调用的端点
        ConfigureResponse("/api/purchase-order/sync-all", new ApiResponse<object> { Success = true, Code = 200, Message = "OK", Data = new { } }, "POST");
        ConfigureResponse("/api/purchase-order/procurement-status", new ApiResponse<List<ProcurementStatusDto>> { Success = true, Code = 200, Data = new List<ProcurementStatusDto>() });
        ConfigureResponse("/api/purchase-order/mismatched-orders", new ApiResponse<List<OrderMismatchInfo>> { Success = true, Code = 200, Data = new List<OrderMismatchInfo>() });
        ConfigureResponse("/api/purchase-order/filter-contexts", new ApiResponse<Dictionary<string, List<string>>> { Success = true, Code = 200, Data = new Dictionary<string, List<string>>() });
    }

    [Fact]
    public void Render_HasTitle()
    {
        var cut = Ctx.RenderComponent<PurchaseOrders>();
        cut.Markup.Should().Contain("采购订单");
    }

    [Fact]
    public void Render_HasFilter()
    {
        var cut = Ctx.RenderComponent<PurchaseOrders>();
        cut.Markup.Should().Contain("状态");
    }

    [Fact]
    public void StatusColumn_RendersOpenAs未到货()
    {
        ConfigureListResponse(new List<PurchaseOrderDto>
        {
            new()
            {
                Id = 1, OrderNo = "PO-001", SupplierName = "测试供应商",
                OrderDate = DateTime.Today, Status = PurchaseOrderStatus.Open,
                MaterialCategory = MaterialType.RoughTube, Specification = "89×10",
                Quantity = 100, Weight = 5000, RequiredDate = DateTime.Today.AddDays(30),
                UnitWeight = 50, PlantGrade = "304", SupplierId = 1,
                IsForceCompleted = false
            }
        });

        var cut = Ctx.RenderComponent<PurchaseOrders>();
        cut.Markup.Should().Contain("已下单");
    }

    [Fact]
    public void StatusColumn_RendersPartialAs部分到货()
    {
        ConfigureListResponse(new List<PurchaseOrderDto>
        {
            new()
            {
                Id = 2, OrderNo = "PO-002", SupplierName = "测试供应商",
                OrderDate = DateTime.Today, Status = PurchaseOrderStatus.Partial,
                MaterialCategory = MaterialType.RoughTube, Specification = "89×10",
                Quantity = 100, Weight = 5000, RequiredDate = DateTime.Today.AddDays(30),
                UnitWeight = 50, PlantGrade = "304", SupplierId = 1,
                IsForceCompleted = false
            }
        });

        var cut = Ctx.RenderComponent<PurchaseOrders>();
        cut.Markup.Should().Contain("部分到货");
    }

    [Fact]
    public void StatusColumn_RendersCompletedAs已完成()
    {
        ConfigureListResponse(new List<PurchaseOrderDto>
        {
            new()
            {
                Id = 3, OrderNo = "PO-003", SupplierName = "测试供应商",
                OrderDate = DateTime.Today, Status = PurchaseOrderStatus.Completed,
                MaterialCategory = MaterialType.RoughTube, Specification = "89×10",
                Quantity = 100, Weight = 5000, RequiredDate = DateTime.Today.AddDays(30),
                UnitWeight = 50, PlantGrade = "304", SupplierId = 1,
                IsForceCompleted = false
            }
        });

        var cut = Ctx.RenderComponent<PurchaseOrders>();
        cut.Markup.Should().Contain("已完成");
    }

    [Fact]
    public void WorkOrderAttentionGroup_Renders工单实时关注组()
    {
        ConfigureListResponse(new List<PurchaseOrderDto>
        {
            new()
            {
                Id = 4, OrderNo = "PO-004", SupplierName = "测试供应商",
                OrderDate = DateTime.Today, Status = PurchaseOrderStatus.Open,
                MaterialCategory = MaterialType.RoughTube, Specification = "89×10",
                Quantity = 100, Weight = 5000, RequiredDate = DateTime.Today.AddDays(30),
                UnitWeight = 50, PlantGrade = "304", SupplierId = 1,
                IsForceCompleted = false,
                SourceWorkOrderNo = "WO-EXEC-001",
                ExecutionScheduleStage = 3,
                ExecutionUrgencyLevel = "AUrgent",
                ExecutionRawMaterialLockRemark = "QualityReplenish",
                ExecutionTheoreticalCutoffDate = new DateTime(2026, 8, 15)
            }
        });

        var cut = Ctx.RenderComponent<PurchaseOrders>();
        cut.Markup.Should().Contain("工单实时关注");
        cut.Markup.Should().Contain("生产执行");      // 工单关注（ScheduleStage=3）
        cut.Markup.Should().Contain("A急");           // 计划性（AUrgent → 字典中文）
        cut.Markup.Should().Contain("2026-08-15");    // 理论截止投料日
    }

    private void ConfigureListResponse(List<PurchaseOrderDto> items)
    {
        // 先配置初始空数据响应，避免首次 OnInitializedAsync 渲染时触发查询
        ConfigureEmptyResponse("/api/purchase-order/list");

        // 再覆盖为真正返回数据的响应
        var pagedResult = new PagedResult<PurchaseOrderDto>
        {
            Items = items,
            TotalCount = items.Count,
            PageIndex = 1,
            PageSize = 20
        };
        var response = new ApiResponse<PagedResult<PurchaseOrderDto>>
        {
            Success = true,
            Code = 200,
            Message = "OK",
            Data = pagedResult
        };
        ConfigureResponse("/api/purchase-order/list", response);
    }
}
