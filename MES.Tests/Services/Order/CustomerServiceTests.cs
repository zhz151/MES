using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Interfaces.Equipment;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Materials;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.StandardRegister;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Models;
using MES.Services.Order;
using MES.Tests.Tests;
using Moq;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Order;
using MES.Data.Entities.Warehouse;
using MES.Core.Constants;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Tests.Services;

/// <summary>
/// 客户档案服务测试：CRUD、关键字搜索、排序
/// </summary>
public class CustomerServiceTests : TestBase
{
    private CustomerService CreateService(AppDbContext ctx)
    {
        return new(ctx, new MemoryCache(new MemoryCacheOptions()));
    }

    private async Task SeedCustomerAsync(AppDbContext ctx, string code = "C001", string unit = "测试客户",
        string salesman = "张三", CustomerStatus status = CustomerStatus.Active)
    {
        ctx.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerCode = code,
            CustomerUnit = unit,
            Salesman = salesman,
            Status = status
        });
        await ctx.SaveChangesAsync();
    }

    // ========== GetPagedAsync ==========

    [Fact]
    public async Task GetPagedAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_按客户编码搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, code: "C001");
        await SeedCustomerAsync(ctx, code: "C002");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "C001" });

        result.Items.Should().HaveCount(1);
        result.Items[0].CustomerCode.Should().Be("C001");
    }

    [Fact]
    public async Task GetPagedAsync_按客户单位搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, unit: "大明钢铁");
        await SeedCustomerAsync(ctx, unit: "宝钢");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "大明" });

        result.Items.Should().HaveCount(1);
        result.Items[0].CustomerUnit.Should().Be("大明钢铁");
    }

    [Fact]
    public async Task GetPagedAsync_按业务员搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, salesman: "张三");
        await SeedCustomerAsync(ctx, salesman: "李四");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "张三" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Salesman.Should().Be("张三");
    }

    [Fact]
    public async Task GetPagedAsync_关键字无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_删除后不显示()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx);
        var id = await ctx.CustomerProfiles.Select(c => c.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_按客户编码排序_成功()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, code: "B001");
        await SeedCustomerAsync(ctx, code: "A001");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "CustomerCode", IsDescending = false });

        result.Items[0].CustomerCode.Should().Be("A001");
        result.Items[1].CustomerCode.Should().Be("B001");
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx);
        var id = await ctx.CustomerProfiles.Select(c => c.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(id);

        result.Should().NotBeNull();
        result.CustomerCode.Should().Be("C001");
        result.CustomerUnit.Should().Be("测试客户");
    }

    [Fact]
    public async Task GetByIdAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetByIdAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("客户不存在");
    }

    // ========== CreateAsync ==========

    [Fact]
    public async Task CreateAsync_成功创建客户()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateCustomerRequest
        {
            CustomerCode = "C100",
            CustomerUnit = "新客户",
            Salesman = "王五",
            Status = CustomerStatus.Active
        });

        result.Should().NotBeNull();
        result.CustomerCode.Should().Be("C100");
        result.CustomerUnit.Should().Be("新客户");
        result.Salesman.Should().Be("王五");
        result.Status.Should().Be(CustomerStatus.Active);

        var saved = await ctx.CustomerProfiles.FirstAsync();
        saved.CustomerCode.Should().Be("C100");
    }

    [Fact]
    public async Task CreateAsync_重复编码_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, code: "C001");
        var svc = CreateService(ctx);

        var act = () => svc.CreateAsync(new CreateCustomerRequest
        {
            CustomerCode = "C001",
            CustomerUnit = "重复客户",
            Salesman = "张三"
        });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已存在*");
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新客户()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx);
        var id = await ctx.CustomerProfiles.Select(c => c.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateCustomerRequest
        {
            CustomerUnit = "更新单位",
            ContactPerson = "李经理"
        });

        result.CustomerUnit.Should().Be("更新单位");

        var saved = await ctx.CustomerProfiles.FirstAsync(c => c.Id == id);
        saved.CustomerUnit.Should().Be("更新单位");
        saved.ContactPerson.Should().Be("李经理");
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateCustomerRequest { CustomerUnit = "新名称" });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("客户不存在");
    }

    [Fact]
    public async Task UpdateAsync_重复编码_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, code: "C001");
        await SeedCustomerAsync(ctx, code: "C002");
        var id = await ctx.CustomerProfiles
            .Where(c => c.CustomerCode == "C001")
            .Select(c => c.Id)
            .FirstAsync();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(id, new UpdateCustomerRequest { CustomerCode = "C002" });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*已存在*");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx);
        var id = await ctx.CustomerProfiles.Select(c => c.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.CustomerProfiles.FindAsync(id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("客户不存在");
    }

    // ========== B11 专项测试 ==========

    [Fact]
    public async Task GetPagedAsync_关键词搜索联系人_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerCode = "C-CONTACT",
            CustomerUnit = "联系人测试客户",
            ContactPerson = "李经理",
            Salesman = "测试业务员",
            Status = CustomerStatus.Active
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "李经理" });

        result.Items.Should().HaveCount(1);
        result.Items[0].ContactPerson.Should().Be("李经理");
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索地址_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerCode = "C-ADDR",
            CustomerUnit = "地址测试客户",
            Address = "北京市海淀区",
            Salesman = "测试业务员",
            Status = CustomerStatus.Active
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "海淀" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Address.Should().Be("北京市海淀区");
    }

    // ========== 筛选上下文 ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        // 种子 2 个不同客户
        ctx.CustomerProfiles.AddRange(
            new CustomerProfile { CustomerCode = "C001", CustomerUnit = "客户A", Salesman = "张三", EndCustomer = "最终A", ContactPerson = "李四", ContactPhone = "13800138001", Address = "北京", Remark = "备注A", Status = CustomerStatus.Active },
            new CustomerProfile { CustomerCode = "C002", CustomerUnit = "客户B", Salesman = "王五", EndCustomer = string.Empty, ContactPerson = null, ContactPhone = null, Address = null, Remark = null, Status = CustomerStatus.Active }
        );
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result.Should().ContainKey("CustomerCode");
        result.Should().ContainKey("Salesman");
        result.Should().ContainKey("CustomerUnit");
        result.Should().ContainKey("EndCustomer");
        result.Should().ContainKey("ContactPerson");
        result.Should().ContainKey("ContactPhone");
        result.Should().ContainKey("Address");
        result.Should().ContainKey("Remark");

        result["CustomerCode"].Should().BeEquivalentTo(new[] { "C001", "C002" }, options => options.WithStrictOrdering());
        result["Salesman"].Should().BeEquivalentTo(new[] { "张三", "王五" });
        result["CustomerUnit"].Should().BeEquivalentTo(new[] { "客户A", "客户B" });
        // EndCustomer 应排除 null
        result["EndCustomer"].Should().HaveCount(1).And.Contain("最终A");
        // ContactPerson 应排除 null
        result["ContactPerson"].Should().HaveCount(1).And.Contain("李四");
        // Remark 应排除 null
        result["Remark"].Should().HaveCount(1).And.Contain("备注A");
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_各字段返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.GetFilterContextsAsync();

        result.Should().ContainKeys("CustomerCode", "Salesman", "CustomerUnit", "EndCustomer", "ContactPerson", "ContactPhone", "Address", "Remark");
        foreach (var kvp in result)
            kvp.Value.Should().BeEmpty($"字段 {kvp.Key} 应返回空列表");
    }

    // ========== 客户业务统计 9 列（服务层按「业务员+最终用户」聚合注入，金额结算分治） ==========

    /// <summary>种子一个含项次+订单读模型的订单；可选本年销售出库记录（InventoryBatch(SalesOrderNo) + OutboundRecord(SalesOut,当前年)）</summary>
    private static async Task SeedOrderWithStatsAsync(AppDbContext ctx,
        string salesman, string endCustomer, int signYear, int stage,
        SettlementMethod method, decimal contractWeight, decimal amount,
        decimal inbound, decimal stock, decimal outboundTotal, decimal outCurrentYear)
    {
        var orderNo = "SO-" + Guid.NewGuid().ToString("N")[..10];
        var signDate = new DateTime(signYear, 1, 15);
        ctx.SalesOrders.Add(new SalesOrder
        {
            OrderNumber = orderNo,
            SignDate = signDate,
            Status = SalesOrderStatus.Confirmed,
            CustomerName = "统计测试客户",
            Salesman = salesman,
            EndCustomer = endCustomer
        });
        await ctx.SaveChangesAsync();

        var orderId = await ctx.SalesOrders.Where(so => so.OrderNumber == orderNo).Select(so => so.Id).FirstAsync();
        ctx.OrderItems.Add(new OrderItem
        {
            SalesOrderId = orderId,
            Sequence = 1,
            DeliveryDate = new DateTime(signYear, 6, 1),
            SettlementMethod = method,
            PipeManufacturingType = PipeManufacturingType.SeamlessPipe,
            DeliveryState = DeliveryState.SolutionAnnealedAndPickled,
            LengthStatus = LengthStatus.Fixed,
            StandardGrade = "304",
            PlantGrade = "304",
            Specification = "219*8",
            OuterDiameter = 219m,
            WallThickness = 8m,
            ContractWeight = contractWeight,
            TotalPrice = amount
        });
        ctx.Set<OrderListSummary>().Add(new OrderListSummary
        {
            OrderId = orderId,
            OrderNumber = orderNo,
            SignDate = signDate,
            CustomerName = "统计测试客户",
            Salesman = salesman,
            EndCustomer = endCustomer,
            Status = SalesOrderStatus.Confirmed,
            TotalContractWeight = (int)contractWeight,
            ItemCount = 1,
            ScheduleStage = stage,
            FinishedInboundWeight = inbound,
            FinishedOutboundWeight = outboundTotal,
            FinishedStockWeight = stock
        });

        if (outCurrentYear > 0m)
        {
            var year = DateTime.Now.Year;
            var batch = new InventoryBatch
            {
                BatchNo = "CK-" + orderNo,
                WarehouseId = 1,
                MaterialType = InventoryMaterialTypes.OrderFinished,
                PlantGrade = "304",
                Specification = "219*8",
                InboundSource = "OrderFinished",
                SourceName = "成品入库",
                InboundDate = new DateTime(year, 1, 16),
                WorkOrderNo = "WO-" + orderNo,
                SalesOrderNo = orderNo,
                InitialQuantity = 1,
                InitialWeight = inbound,
                RemainingQuantity = 1,
                RemainingWeight = stock
            };
            ctx.InventoryBatches.Add(batch);
            await ctx.SaveChangesAsync();
            var batchId = await ctx.InventoryBatches.Where(ib => ib.BatchNo == "CK-" + orderNo).Select(ib => ib.Id).FirstAsync();
            ctx.OutboundRecords.Add(new OutboundRecord
            {
                InventoryBatchId = batchId,
                BatchNo = "CK-" + orderNo,
                OutboundType = OutboundType.SalesOut,
                OutboundQuantity = 1,
                OutboundWeight = outCurrentYear,
                OutboundDate = new DateTime(year, 6, 20)
            });
        }
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task GetPagedAsync_无业务订单_统计列全为零()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var dto = result.Items[0];
        dto.TotalOrderCount.Should().Be(0);
        dto.TotalOrderWeight.Should().Be(0m);
        dto.TotalOrderAmount.Should().Be(0m);
        dto.YearOrderCount.Should().Be(0);
        dto.WipNoneWeight.Should().Be(0m);
        dto.ShippedCompletedWeight.Should().Be(0m);
        dto.StockCompletedWeight.Should().Be(0m);
    }

    [Fact]
    public async Task GetPagedAsync_理算整单完成本年发货_发货列封顶合同金额()
    {
        var ctx = CreateDbContext();
        var year = DateTime.Now.Year;
        await SeedCustomerAsync(ctx, salesman: "张三");
        ctx.CustomerProfiles.First().EndCustomer = "客户A";
        await ctx.SaveChangesAsync();
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: year, stage: 1,
            SettlementMethod.Theoretical, 1000m, 100000m, inbound: 1000m, stock: 0m, outboundTotal: 1000m, outCurrentYear: 1000m);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var dto = result.Items[0];
        dto.TotalOrderCount.Should().Be(1);
        dto.TotalOrderWeight.Should().Be(1000m);
        dto.TotalOrderAmount.Should().Be(100000m);
        dto.YearOrderCount.Should().Be(1);
        dto.YearOrderWeight.Should().Be(1000m);
        dto.YearOrderAmount.Should().Be(100000m);
        // 理算按合同总金额计价，本年全部发货 → 发货金额=合同金额；整单完成落入发货桶单数=1
        dto.ShippedCompletedWeight.Should().Be(1000m);
        dto.ShippedCompletedAmount.Should().Be(100000m);
        dto.ShippedCompletedCount.Should().Be(1);
        // 无库存/无在产（已全部入库且已发完）
        dto.StockCompletedWeight.Should().Be(0m);
        dto.StockCompletedCount.Should().Be(0);
        dto.WipNoneWeight.Should().Be(0m);
        dto.WipNoneCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_过磅超产_发货按实际公斤不封顶()
    {
        var ctx = CreateDbContext();
        var year = DateTime.Now.Year;
        await SeedCustomerAsync(ctx, salesman: "张三");
        ctx.CustomerProfiles.First().EndCustomer = "客户A";
        await ctx.SaveChangesAsync();
        // 过磅：合同 1000kg/100000元(单价100)，实际入库1000 但出库1200(超产) → 金额按 1200×100 不封顶
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: year, stage: 1,
            SettlementMethod.Weighing, 1000m, 100000m, inbound: 1000m, stock: 0m, outboundTotal: 1200m, outCurrentYear: 1200m);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var dto = result.Items[0];
        dto.ShippedCompletedWeight.Should().Be(1200m);
        dto.ShippedCompletedAmount.Should().Be(120000m);
        dto.ShippedCompletedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_过磅负超产_发货只按合同重量计价()
    {
        var ctx = CreateDbContext();
        var year = DateTime.Now.Year;
        await SeedCustomerAsync(ctx, salesman: "张三");
        ctx.CustomerProfiles.First().EndCustomer = "客户A";
        await ctx.SaveChangesAsync();
        // 过磅-负：合同 1000kg/100000元(单价100)，出库1200>合同 → 只能按合同重量1000计价
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: year, stage: 1,
            SettlementMethod.WeighingNegative, 1000m, 100000m, inbound: 1000m, stock: 0m, outboundTotal: 1200m, outCurrentYear: 1200m);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var dto = result.Items[0];
        dto.ShippedCompletedWeight.Should().Be(1200m); // 发货重量如实显示超发公斤
        dto.ShippedCompletedAmount.Should().Be(100000m); // 金额封顶合同额
        dto.ShippedCompletedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_非整单未入库_在产与库存按扣除部分入库分列()
    {
        var ctx = CreateDbContext();
        var year = DateTime.Now.Year;
        await SeedCustomerAsync(ctx, salesman: "张三");
        ctx.CustomerProfiles.First().EndCustomer = "客户A";
        await ctx.SaveChangesAsync();
        // 主号未完成(stage3)，过磅合同1000/100000；仅入库300(库存300未发货)，其余700在产
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: year, stage: 3,
            SettlementMethod.Weighing, 1000m, 100000m, inbound: 300m, stock: 300m, outboundTotal: 0m, outCurrentYear: 0m);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var dto = result.Items[0];
        // 本年无发货
        dto.ShippedOtherWeight.Should().Be(0m);
        dto.ShippedOtherAmount.Should().Be(0m);
        dto.ShippedOtherCount.Should().Be(0);
        // 非整单待发货(库存) = 300×100，单数=1
        dto.StockOtherWeight.Should().Be(300m);
        dto.StockOtherAmount.Should().Be(30000m);
        dto.StockOtherCount.Should().Be(1);
        // 整单/库存/在产互相独立：未入库部分计入在产-扣除部分，单数=1
        dto.WipNoneWeight.Should().Be(0m);
        dto.WipNoneCount.Should().Be(0);
        dto.WipPartialWeight.Should().Be(700m);
        dto.WipPartialAmount.Should().Be(70000m);
        dto.WipPartialCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_整单完成欠产或未入库_不计在产()
    {
        var ctx = CreateDbContext();
        var year = DateTime.Now.Year;
        await SeedCustomerAsync(ctx, salesman: "张三");
        ctx.CustomerProfiles.First().EndCustomer = "客户A";
        await ctx.SaveChangesAsync();
        // 主号已完成(stage1)但欠产：合同1000仅入库300(库存300未发货)，欠产700不再计在产
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: year, stage: 1,
            SettlementMethod.Weighing, 1000m, 100000m, inbound: 300m, stock: 300m, outboundTotal: 0m, outCurrentYear: 0m);
        // 主号已完成(stage1)但完全未入库：也不计在产-整单未入库
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: year, stage: 1,
            SettlementMethod.Weighing, 500m, 50000m, inbound: 0m, stock: 0m, outboundTotal: 0m, outCurrentYear: 0m);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var dto = result.Items[0];
        // 欠产完成单：库存计入待发货(整单)=300×100
        dto.StockCompletedWeight.Should().Be(300m);
        dto.StockCompletedAmount.Should().Be(30000m);
        dto.StockCompletedCount.Should().Be(1);
        // 两个完成单都不再进在产（扣除部分/整单未入库 均归零）
        dto.WipNoneWeight.Should().Be(0m);
        dto.WipNoneCount.Should().Be(0);
        dto.WipPartialWeight.Should().Be(0m);
        dto.WipPartialCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_同一业务员不同最终用户_按最终用户分桶聚合()
    {
        var ctx = CreateDbContext();
        var year = DateTime.Now.Year;
        ctx.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerCode = "C-A",
            CustomerUnit = "客户单位A",
            Salesman = "张三",
            EndCustomer = "客户A",
            Status = CustomerStatus.Active
        });
        ctx.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerCode = "C-B",
            CustomerUnit = "客户单位B",
            Salesman = "张三",
            EndCustomer = "客户B",
            Status = CustomerStatus.Active
        });
        await ctx.SaveChangesAsync();
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: year, stage: 3,
            SettlementMethod.Weighing, 1000m, 100000m, inbound: 0m, stock: 0m, outboundTotal: 0m, outCurrentYear: 0m);
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: year, stage: 3,
            SettlementMethod.Weighing, 500m, 50000m, inbound: 0m, stock: 0m, outboundTotal: 0m, outCurrentYear: 0m);
        await SeedOrderWithStatsAsync(ctx, "张三", "客户B", signYear: year, stage: 3,
            SettlementMethod.Weighing, 200m, 20000m, inbound: 0m, stock: 0m, outboundTotal: 0m, outCurrentYear: 0m);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 50 });

        var dtoA = result.Items.First(c => c.CustomerUnit == "客户单位A");
        dtoA.TotalOrderCount.Should().Be(2);
        dtoA.TotalOrderWeight.Should().Be(1500m);
        dtoA.TotalOrderAmount.Should().Be(150000m);
        dtoA.WipNoneWeight.Should().Be(1500m);
        dtoA.WipNoneAmount.Should().Be(150000m);
        dtoA.WipNoneCount.Should().Be(2); // 两张主号未完成订单各计 1 单

        var dtoB = result.Items.First(c => c.CustomerUnit == "客户单位B");
        dtoB.TotalOrderCount.Should().Be(1);
        dtoB.TotalOrderWeight.Should().Be(200m);
        dtoB.WipNoneAmount.Should().Be(20000m);
        dtoB.WipNoneCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_取消订单不计入统计()
    {
        var ctx = CreateDbContext();
        var year = DateTime.Now.Year;
        await SeedCustomerAsync(ctx, salesman: "张三");
        ctx.CustomerProfiles.First().EndCustomer = "客户A";
        await ctx.SaveChangesAsync();

        var orderNo = "SO-CANCELLED";
        ctx.SalesOrders.Add(new SalesOrder
        {
            OrderNumber = orderNo,
            SignDate = new DateTime(year, 1, 15),
            Status = SalesOrderStatus.Cancelled,
            CustomerName = "统计测试客户",
            Salesman = "张三",
            EndCustomer = "客户A"
        });
        await ctx.SaveChangesAsync();
        var orderId = await ctx.SalesOrders.Where(so => so.OrderNumber == orderNo).Select(so => so.Id).FirstAsync();
        ctx.OrderItems.Add(new OrderItem
        {
            SalesOrderId = orderId,
            Sequence = 1,
            DeliveryDate = new DateTime(year, 6, 1),
            SettlementMethod = SettlementMethod.Weighing,
            ContractWeight = 1000m,
            TotalPrice = 100000m,
            StandardGrade = "304",
            PlantGrade = "304",
            Specification = "219*8"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });

        var dto = result.Items[0];
        dto.TotalOrderCount.Should().Be(0);
        dto.TotalOrderAmount.Should().Be(0m);
    }
}
