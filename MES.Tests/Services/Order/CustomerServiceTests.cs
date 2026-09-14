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

    /// <summary>种子一个含项次+订单读模型的订单；可选本年销售出库记录（InventoryBatch(SalesOrderNo) + OutboundRecord(SalesOut,当前年)）。
    /// <paramref name="outDate"/> 可显式指定出库日期（供「发货区间」用例验证按 OutboundDate 落窗口），默认当年 6/20。</summary>
    private static async Task SeedOrderWithStatsAsync(AppDbContext ctx,
        string salesman, string endCustomer, int signYear, int stage,
        SettlementMethod method, decimal contractWeight, decimal amount,
        decimal inbound, decimal stock, decimal outboundTotal, decimal outCurrentYear,
        DateTime? outDate = null)
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
                OutboundDate = outDate ?? new DateTime(year, 6, 20)
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

    // ========== 接单区间模式（SignDateFrom/SignDateTo：仅客户管理使用，2026-09-14） ==========

    [Fact]
    public async Task GetPagedAsync_接单区间_命中区间_当期桶按区间计数且累计保持全时段()
    {
        var ctx = CreateDbContext();
        // 签约日固定为 signYear-01-15（非当前年）
        await SeedCustomerAsync(ctx, salesman: "张三");
        ctx.CustomerProfiles.First().EndCustomer = "客户A";
        await ctx.SaveChangesAsync();
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: 2024, stage: 1,
            SettlementMethod.Theoretical, 1000m, 100000m, inbound: 1000m, stock: 0m, outboundTotal: 1000m, outCurrentYear: 0m);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1, PageSize = 20,
            SignDateFrom = new DateTime(2024, 1, 1),
            SignDateTo = new DateTime(2024, 12, 31)
        });

        var dto = result.Items[0];
        // 累计接单恒为全时段（前端在区间模式下该列渲染「—」，值本身不变）
        dto.TotalOrderCount.Should().Be(1);
        dto.TotalOrderWeight.Should().Be(1000m);
        dto.TotalOrderAmount.Should().Be(100000m);
        // 当期桶 = 区间接单（签约日落区间）
        dto.YearOrderCount.Should().Be(1);
        dto.YearOrderWeight.Should().Be(1000m);
        dto.YearOrderAmount.Should().Be(100000m);
    }

    [Fact]
    public async Task GetPagedAsync_接单区间_未命中区间_该客户被行过滤剔除()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, salesman: "张三");
        ctx.CustomerProfiles.First().EndCustomer = "客户A";
        await ctx.SaveChangesAsync();
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: 2024, stage: 1,
            SettlementMethod.Theoretical, 1000m, 100000m, inbound: 1000m, stock: 0m, outboundTotal: 1000m, outCurrentYear: 0m);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1, PageSize = 20,
            SignDateFrom = new DateTime(2025, 1, 1),
            SignDateTo = new DateTime(2025, 12, 31)
        });

        // 唯一客户在「区间接单」上无数据 → 行被过滤（与报表卡行过滤同口径），TotalCount 同步为 0
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_接单区间_仅传起始端_按开区间上不封顶过滤()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, salesman: "张三");
        ctx.CustomerProfiles.First().EndCustomer = "客户A";
        await ctx.SaveChangesAsync();
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: 2024, stage: 1,
            SettlementMethod.Theoretical, 1000m, 100000m, inbound: 1000m, stock: 0m, outboundTotal: 1000m, outCurrentYear: 0m);
        var svc = CreateService(ctx);

        // 只有起始端（2023-01-01 起）→ 2024-01-15 应命中；结束端缺省表示不设上限
        var hit = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1, PageSize = 20,
            SignDateFrom = new DateTime(2023, 1, 1)
        });
        hit.Items[0].YearOrderCount.Should().Be(1);

        // 起始端晚于签约日 → 不命中（行随之被过滤）
        var miss = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1, PageSize = 20,
            SignDateFrom = new DateTime(2025, 1, 1)
        });
        miss.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_接单区间_不驱动已发货窗口_后者仍按自然年()
    {
        var ctx = CreateDbContext();
        var nowYear = DateTime.Now.Year;
        await SeedCustomerAsync(ctx, salesman: "张三");
        ctx.CustomerProfiles.First().EndCustomer = "客户A";
        await ctx.SaveChangesAsync();
        // 签约日 2024-01-15；出库日 = 当前年 6/20（见 SeedOrderWithStatsAsync 默认值）
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: 2024, stage: 1,
            SettlementMethod.Theoretical, 1000m, 100000m, inbound: 1000m, stock: 0m, outboundTotal: 1000m, outCurrentYear: 1000m);
        var svc = CreateService(ctx);

        // 接单区间 = 2024：接单命中；发货维度不受接单区间影响 → 仍按自然年（出库当前年）有值
        var sign2024 = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1, PageSize = 20,
            SignDateFrom = new DateTime(2024, 1, 1),
            SignDateTo = new DateTime(2024, 12, 31)
        });
        sign2024.Items[0].YearOrderCount.Should().Be(1);
        sign2024.Items[0].ShippedCompletedWeight.Should().Be(1000m);
        sign2024.Items[0].ShippedCompletedAmount.Should().Be(100000m);
        sign2024.Items[0].ShippedCompletedCount.Should().Be(1);

        // 接单区间 = 当前年：接单（2024）不命中 → 该客户在「区间接单」上无数据，行被过滤
        var signNow = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1, PageSize = 20,
            SignDateFrom = new DateTime(nowYear, 1, 1),
            SignDateTo = new DateTime(nowYear, 12, 31)
        });
        signNow.Items.Should().BeEmpty();
        signNow.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_接单区间_待发货与待在产属存量口径不受区间影响()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, salesman: "张三");
        ctx.CustomerProfiles.First().EndCustomer = "客户A";
        await ctx.SaveChangesAsync();
        // 签约日 2024-01-15；主号未完成(阶段3)：入库300(库存在库未发)、欠产700
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: 2024, stage: 3,
            SettlementMethod.Weighing, 1000m, 100000m, inbound: 300m, stock: 300m, outboundTotal: 0m, outCurrentYear: 0m);
        var svc = CreateService(ctx);

        // 区间命中签约日（2024 年）→ 行保留；此时存量列仍照常统计（前端才把它们渲染为「—」）
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1, PageSize = 20,
            SignDateFrom = new DateTime(2024, 1, 1),
            SignDateTo = new DateTime(2024, 12, 31)
        });

        var dto = result.Items[0];
        dto.YearOrderCount.Should().Be(1);
        // ⚠️ 关键：待发货/待在产是当前存量（无日期语义），任何区间都不改变其取值
        dto.StockOtherWeight.Should().Be(300m);
        dto.StockOtherAmount.Should().Be(30000m);
        dto.StockOtherCount.Should().Be(1);
        dto.WipPartialWeight.Should().Be(700m);
        dto.WipPartialAmount.Should().Be(70000m);
        dto.WipPartialCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_未传接单区间_口径与既有自然年行为一致()
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
        // 区间两端皆空 → 完全沿用「累计 + 本年」口径（回归保护）
        dto.TotalOrderCount.Should().Be(1);
        dto.YearOrderCount.Should().Be(1);
        dto.YearOrderWeight.Should().Be(1000m);
        dto.ShippedCompletedWeight.Should().Be(1000m);
        dto.ShippedCompletedCount.Should().Be(1);
    }

    // ========== 发货区间模式（ShipDateFrom/ShipDateTo：与接单区间互相独立、可叠加，2026-09-14） ==========

    [Fact]
    public async Task GetPagedAsync_发货区间_命中区间_已发货桶按出库日期重算且接单桶不受影响()
    {
        var ctx = CreateDbContext();
        var year = DateTime.Now.Year;
        await SeedCustomerAsync(ctx, salesman: "张三");
        ctx.CustomerProfiles.First().EndCustomer = "客户A";
        await ctx.SaveChangesAsync();
        // 签约日 = 当年 1/15；出库日显式 = 当年 8/10
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: year, stage: 1,
            SettlementMethod.Theoretical, 1000m, 100000m, inbound: 1000m, stock: 0m, outboundTotal: 1000m, outCurrentYear: 1000m,
            outDate: new DateTime(year, 8, 10));
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            ShipDateFrom = new DateTime(year, 8, 1),
            ShipDateTo = new DateTime(year, 8, 31)
        });

        var dto = result.Items[0];
        dto.ShippedCompletedWeight.Should().Be(1000m);
        dto.ShippedCompletedCount.Should().Be(1);
        // 发货区间不触碰接单维度：仍按自然年 → 当年签约命中
        dto.YearOrderCount.Should().Be(1);
        dto.YearOrderWeight.Should().Be(1000m);
    }

    [Fact]
    public async Task GetPagedAsync_发货区间_未命中区间_已发货为零但接单仍按自然年()
    {
        var ctx = CreateDbContext();
        var year = DateTime.Now.Year;
        await SeedCustomerAsync(ctx, salesman: "张三");
        ctx.CustomerProfiles.First().EndCustomer = "客户A";
        await ctx.SaveChangesAsync();
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: year, stage: 1,
            SettlementMethod.Theoretical, 1000m, 100000m, inbound: 1000m, stock: 0m, outboundTotal: 1000m, outCurrentYear: 1000m,
            outDate: new DateTime(year, 8, 10));
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            ShipDateFrom = new DateTime(year, 9, 1),
            ShipDateTo = new DateTime(year, 9, 30)
        });

        // 该客户在「区间已发货」上无数据 → 行被过滤（接单维度本身不受影响，但行不再列出）
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_双区间叠加_各管各的维度互不干扰()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, salesman: "张三");
        ctx.CustomerProfiles.First().EndCustomer = "客户A";
        await ctx.SaveChangesAsync();
        // 签约 2024/1/15；出库 2025/3/10
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: 2024, stage: 1,
            SettlementMethod.Theoretical, 1000m, 100000m, inbound: 1000m, stock: 0m, outboundTotal: 1000m, outCurrentYear: 1000m,
            outDate: new DateTime(2025, 3, 10));
        var svc = CreateService(ctx);

        // 接单区间 = 2024 年（命中签约日）、发货区间 = 2025 年（命中出库日）→ 两桶同时有值
        var both = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SignDateFrom = new DateTime(2024, 1, 1),
            SignDateTo = new DateTime(2024, 12, 31),
            ShipDateFrom = new DateTime(2025, 1, 1),
            ShipDateTo = new DateTime(2025, 12, 31)
        });
        var dto = both.Items[0];
        dto.YearOrderCount.Should().Be(1);
        dto.ShippedCompletedWeight.Should().Be(1000m);

        // 接单区间 = 2024（命中）、发货区间 = 2024（出库在 2025 → 不命中）→ 仅接单桶有值
        var onlySign = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SignDateFrom = new DateTime(2024, 1, 1),
            SignDateTo = new DateTime(2024, 12, 31),
            ShipDateFrom = new DateTime(2024, 1, 1),
            ShipDateTo = new DateTime(2024, 12, 31)
        });
        var dto2 = onlySign.Items[0];
        dto2.YearOrderCount.Should().Be(1);
        dto2.ShippedCompletedWeight.Should().Be(0m);
    }

    [Fact]
    public async Task GetPagedAsync_发货区间_待发货与待在产属存量口径不受影响()
    {
        var ctx = CreateDbContext();
        await SeedCustomerAsync(ctx, salesman: "张三");
        ctx.CustomerProfiles.First().EndCustomer = "客户A";
        await ctx.SaveChangesAsync();
        // 未完成订单：入库 300 / 库存 200 / 出库 100（出库日 2025/3/10，发货区间命中该日 → 行保留）
        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: 2024, stage: 3,
            SettlementMethod.Theoretical, 1000m, 100000m, inbound: 300m, stock: 200m, outboundTotal: 1000m, outCurrentYear: 100m,
            outDate: new DateTime(2025, 3, 10));
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            ShipDateFrom = new DateTime(2025, 3, 1),
            ShipDateTo = new DateTime(2025, 3, 31)
        });

        var dto = result.Items[0];
        // 发货维度按区间重算（出库 100 落区间）
        dto.ShippedOtherWeight.Should().Be(100m);
        // 存量列不受区间影响（前端才把它们渲染为「—」）
        dto.StockOtherWeight.Should().Be(200m);
        dto.StockOtherCount.Should().Be(1);
        dto.WipPartialWeight.Should().Be(700m);
        dto.WipPartialCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_未传发货区间_口径与既有自然年行为一致()
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
        // 发货区间两端皆空 → 沿用自然年口径（回归保护）
        dto.ShippedCompletedWeight.Should().Be(1000m);
        dto.ShippedCompletedCount.Should().Be(1);
    }

    // ========== 区间模式行过滤（只保留「激活列有数据」的客户行，与报表卡同口径，2026-09-14） ==========

    /// <summary>种子三个客户：A 签约 2024 + 出库 2025-03-10；B 签约 2025 + 出库 2025-03-10；C 无任何订单</summary>
    private async Task SeedThreeCustomersAsync(AppDbContext ctx)
    {
        await SeedCustomerAsync(ctx, code: "C001", unit: "客户单位A", salesman: "张三");
        await SeedCustomerAsync(ctx, code: "C002", unit: "客户单位B", salesman: "张三");
        await SeedCustomerAsync(ctx, code: "C003", unit: "无单客户", salesman: "李四");

        ctx.CustomerProfiles.First(c => c.CustomerCode == "C001").EndCustomer = "客户A";
        ctx.CustomerProfiles.First(c => c.CustomerCode == "C002").EndCustomer = "客户B";
        await ctx.SaveChangesAsync();

        await SeedOrderWithStatsAsync(ctx, "张三", "客户A", signYear: 2024, stage: 1,
            SettlementMethod.Theoretical, 1000m, 100000m, inbound: 1000m, stock: 0m, outboundTotal: 1000m, outCurrentYear: 1000m,
            outDate: new DateTime(2025, 3, 10));
        await SeedOrderWithStatsAsync(ctx, "张三", "客户B", signYear: 2025, stage: 1,
            SettlementMethod.Theoretical, 500m, 50000m, inbound: 500m, stock: 0m, outboundTotal: 500m, outCurrentYear: 500m,
            outDate: new DateTime(2025, 3, 10));
    }

    [Fact]
    public async Task GetPagedAsync_区间模式行过滤_只保留激活列有数据的客户行()
    {
        var ctx = CreateDbContext();
        await SeedThreeCustomersAsync(ctx);
        var svc = CreateService(ctx);

        // 接单区间 = 2024：仅 A 命中；B（2025 签约）与无单 C 被过滤
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SignDateFrom = new DateTime(2024, 1, 1),
            SignDateTo = new DateTime(2024, 12, 31)
        });

        result.TotalCount.Should().Be(1);
        result.Items.Select(i => i.CustomerCode).Should().BeEquivalentTo(new[] { "C001" });
    }

    [Fact]
    public async Task GetPagedAsync_区间模式行过滤_双区间叠加时命中任一区间的客户均保留()
    {
        var ctx = CreateDbContext();
        await SeedThreeCustomersAsync(ctx);
        var svc = CreateService(ctx);

        // 接单区间 2024（命中 A）+ 发货区间 2025（A、B 出库均在 2025）→ A、B 保留，无单 C 仍被过滤
        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            SignDateFrom = new DateTime(2024, 1, 1),
            SignDateTo = new DateTime(2024, 12, 31),
            ShipDateFrom = new DateTime(2025, 1, 1),
            ShipDateTo = new DateTime(2025, 12, 31)
        });

        result.TotalCount.Should().Be(2);
        result.Items.Select(i => i.CustomerCode).Should().BeEquivalentTo(new[] { "C001", "C002" });
    }

    [Fact]
    public async Task GetPagedAsync_区间模式行过滤_分页发生在过滤之后()
    {
        var ctx = CreateDbContext();
        await SeedThreeCustomersAsync(ctx);
        var svc = CreateService(ctx);

        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = 1,
            SignDateFrom = new DateTime(2024, 1, 1),
            SignDateTo = new DateTime(2024, 12, 31),
            ShipDateFrom = new DateTime(2025, 1, 1),
            ShipDateTo = new DateTime(2025, 12, 31)
        };

        // 过滤后 2 条 → 页大小 1：两页各 1 条、TotalCount 恒为 2（「共 N 条」与过滤口径一致）
        var page1 = await svc.GetPagedAsync(query);
        page1.TotalCount.Should().Be(2);
        page1.Items.Should().HaveCount(1);

        query.PageIndex = 2;
        var page2 = await svc.GetPagedAsync(query);
        page2.TotalCount.Should().Be(2);
        page2.Items.Should().HaveCount(1);
        page2.Items[0].CustomerCode.Should().NotBe(page1.Items[0].CustomerCode);
    }
}
