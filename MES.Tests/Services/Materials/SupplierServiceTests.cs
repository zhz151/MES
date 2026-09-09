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
using MES.Core.Exceptions;
using MES.Core.Models;
using MES.Services.Materials;
using MES.Tests.Tests;


using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Warehouse;
using MES.Core.Enums;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Tests.Services;

/// <summary>
/// 供应商服务测试：供应商CRUD、关键字搜索、激活查询
/// </summary>
public class SupplierServiceTests : TestBase
{
    private SupplierService CreateService(AppDbContext ctx) => new(ctx, new MemoryCache(new MemoryCacheOptions()));

    private async Task SeedSupplierAsync(AppDbContext ctx, string name = "测试供应商", string? code = null, string contact = "张三", string phone = "13800138000")
    {
        ctx.SupplierProfiles.Add(new SupplierProfile
        {
            SupplierCode = code ?? $"S{Guid.NewGuid():N}"[..10],
            SupplierName = name,
            ContactPerson = contact,
            ContactPhone = phone,
            IsActive = true
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
    public async Task GetPagedAsync_按供应商名称搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "大明钢铁");
        await SeedSupplierAsync(ctx, name: "大明不锈钢");
        await SeedSupplierAsync(ctx, name: "宝钢");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "大明" });

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPagedAsync_按联系人搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "A公司", contact: "李四");
        await SeedSupplierAsync(ctx, name: "B公司", contact: "王五");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "李四" });

        result.Items.Should().HaveCount(1);
        result.Items[0].ContactPerson.Should().Be("李四");
    }

    [Fact]
    public async Task GetPagedAsync_按电话搜索_返回匹配结果()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "A公司", phone: "13900001111");
        await SeedSupplierAsync(ctx, name: "B公司", phone: "13800002222");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "13900001111" });

        result.Items.Should().HaveCount(1);
        result.Items[0].ContactPhone.Should().Be("13900001111");
    }

    [Fact]
    public async Task GetPagedAsync_关键字无匹配_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "NONEXISTENT" });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_按名称排序_成功()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "B供应商");
        await SeedSupplierAsync(ctx, name: "A供应商");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        { PageIndex = 1, PageSize = 20, SortBy = "SupplierName", IsDescending = false });

        result.Items[0].SupplierName.Should().Be("A供应商");
        result.Items[1].SupplierName.Should().Be("B供应商");
    }

    // ========== GetByIdAsync ==========

    [Fact]
    public async Task GetByIdAsync_存在_返回Dto()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx);
        var id = await ctx.SupplierProfiles.Select(s => s.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetByIdAsync(id);

        result.Should().NotBeNull();
        result.SupplierName.Should().Be("测试供应商");
    }

    [Fact]
    public async Task GetByIdAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.GetByIdAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("供应商不存在");
    }

    // ========== GetActiveAsync ==========

    [Fact]
    public async Task GetActiveAsync_仅返回激活供应商()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "激活供应商");
        ctx.SupplierProfiles.Add(new SupplierProfile
        {
            SupplierCode = $"S{Guid.NewGuid():N}"[..10],
            SupplierName = "停用供应商",
            IsActive = false
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetActiveAsync();

        result.Should().HaveCount(1);
        result[0].SupplierName.Should().Be("激活供应商");
    }

    // ========== CreateAsync ==========

    [Fact]
    public async Task CreateAsync_成功创建供应商()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.CreateAsync(new CreateSupplierRequest
        {
            SupplierName = "新供应商",
            ContactPerson = "王经理",
            ContactPhone = "13900009999",
            Address = "上海市",
            IsActive = true
        });

        result.Should().NotBeNull();
        result.SupplierName.Should().Be("新供应商");
        result.ContactPerson.Should().Be("王经理");

        var saved = await ctx.SupplierProfiles.FirstAsync(s => s.SupplierName == "新供应商");
        saved.SupplierName.Should().Be("新供应商");
    }

    // ========== UpdateAsync ==========

    [Fact]
    public async Task UpdateAsync_成功更新供应商()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx);
        var id = await ctx.SupplierProfiles.Select(s => s.Id).FirstAsync();
        var svc = CreateService(ctx);

        var result = await svc.UpdateAsync(id, new UpdateSupplierRequest
        {
            ContactPerson = "李经理",
            Remark = "优质供应商"
        });

        result.ContactPerson.Should().Be("李经理");

        var saved = await ctx.SupplierProfiles.FirstAsync(s => s.Id == id);
        saved.ContactPerson.Should().Be("李经理");
        saved.Remark.Should().Be("优质供应商");
    }

    [Fact]
    public async Task UpdateAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.UpdateAsync(999, new UpdateSupplierRequest { SupplierName = "新名称" });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("供应商不存在");
    }

    // ========== DeleteAsync ==========

    [Fact]
    public async Task DeleteAsync_成功删除()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx);
        var id = await ctx.SupplierProfiles.Select(s => s.Id).FirstAsync();
        var svc = CreateService(ctx);

        await svc.DeleteAsync(id);

        var deleted = await ctx.SupplierProfiles.FindAsync(id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_不存在_抛出BusinessException()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var act = () => svc.DeleteAsync(999);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("供应商不存在");
    }

    // ========== B11 专项测试 ==========

    [Fact]
    public async Task GetPagedAsync_关键词搜索地址_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.SupplierProfiles.Add(new SupplierProfile
        {
            SupplierCode = $"S{Guid.NewGuid():N}"[..10],
            SupplierName = "地址测试供应商",
            Address = "上海市浦东新区",
            IsActive = true
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "浦东" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Address.Should().Be("上海市浦东新区");
    }

    [Fact]
    public async Task GetPagedAsync_关键词搜索备注_返回匹配()
    {
        var ctx = CreateDbContext();
        ctx.SupplierProfiles.Add(new SupplierProfile
        {
            SupplierCode = $"S{Guid.NewGuid():N}"[..10],
            SupplierName = "备注测试供应商",
            Remark = "优质供应商备注",
            IsActive = true
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, Keyword = "优质供应商" });

        result.Items.Should().HaveCount(1);
        result.Items[0].Remark.Should().Be("优质供应商备注");
    }

    // ========== 筛选测试（FilterDescriptor） ==========

    [Fact]
    public async Task GetPagedAsync_Filters_SupplierNameContains_返回匹配()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "大明钢铁");
        await SeedSupplierAsync(ctx, name: "宝钢");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SupplierName", Operator = "contains", Value = "大明" }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].SupplierName.Should().Be("大明钢铁");
    }

    [Fact]
    public async Task GetPagedAsync_Filters_SupplierCodeIn_返回匹配()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "供应商A", code: "SU0001");
        await SeedSupplierAsync(ctx, name: "供应商B", code: "SU0002");
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SupplierCode", Operator = "in", Values = new List<string> { "SU0002" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].SupplierName.Should().Be("供应商B");
    }

    [Fact]
    public async Task GetPagedAsync_Filters_NoMatch_返回空列表()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx);
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "SupplierName", Operator = "contains", Value = "NONEXISTENT" }
            }
        });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_Filters_IsActiveIn_返回激活()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "激活供应商");
        ctx.SupplierProfiles.Add(new SupplierProfile
        {
            SupplierCode = "SU9999",
            SupplierName = "停用供应商",
            IsActive = false
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.GetPagedAsync(new QueryParams
        {
            PageIndex = 1,
            PageSize = 20,
            Filters = new List<FilterDescriptor>
            {
                new() { Field = "IsActive", Operator = "in", Values = new List<string> { "True" } }
            }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].IsActive.Should().BeTrue();
    }

    // ========== GetFilterContextsAsync ==========

    [Fact]
    public async Task GetFilterContextsAsync_返回正确选项()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx, name: "供应商A", contact: "张三");
        await SeedSupplierAsync(ctx, name: "供应商B", contact: "李四");
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts.Should().ContainKey("SupplierName");
        contexts["SupplierName"].Should().BeEquivalentTo(new[] { "供应商A", "供应商B" }, opts => opts.WithStrictOrdering());
        contexts.Should().ContainKey("ContactPerson");
        contexts["ContactPerson"].Should().Contain("张三");
        contexts["ContactPerson"].Should().Contain("李四");
    }

    [Fact]
    public async Task GetFilterContextsAsync_无数据_返回空列表()
    {
        var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["SupplierName"].Should().BeEmpty();
        contexts["SupplierCode"].Should().BeEmpty();
        contexts["ContactPerson"].Should().BeEmpty();
    }

    [Fact]
    public async Task GetFilterContextsAsync_Nullable字段排除null()
    {
        var ctx = CreateDbContext();
        ctx.SupplierProfiles.Add(new SupplierProfile
        {
            SupplierCode = "SU0001",
            SupplierName = "测试供应商",
            IsActive = true,
            ContactPerson = null,
            Remark = null
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var contexts = await svc.GetFilterContextsAsync();

        contexts["SupplierName"].Should().HaveCount(1);
        contexts["ContactPerson"].Should().BeEmpty();
        contexts["Remark"].Should().BeEmpty();
    }

    // ========== 打印回归（2026-09-09：删手写 ToPrintDict → DTO 反射） ==========

    [Fact]
    public async Task PrintSupplierBatchAsync_覆盖页面全列_生成PDF成功()
    {
        var ctx = CreateDbContext();
        await SeedSupplierAsync(ctx);
        var id = await ctx.SupplierProfiles.Select(s => s.Id).FirstAsync();
        var svc = CreateService(ctx);

        // 打印列全集 = SupplierProfileDto 属性名（TablePrintHelper 按列 Key 反射取值，
        // IsActive 布尔由 resolver 兜底显示 启用/停用）
        var columns = new[]
        {
            "SupplierCode", "SupplierName", "MaterialCategory", "ContactPerson",
            "ContactPhone", "Address", "IsActive", "Remark"
        }.Select(k => new PrintColumnDef { Key = k }).ToList();

        var pdf = await svc.PrintSupplierBatchAsync(new[] { id }, columns);

        pdf.Should().NotBeNull();
        pdf.Length.Should().BeGreaterThan(0);
    }

    // ========== ② 往来统计桶（采购 + 委外合并，GetPagedAsync 回填 9 字段） ==========

    [Fact]
    public async Task GetPagedAsync_往来统计_采购委外合并累计与本年()
    {
        var ctx = CreateDbContext();
        var s1 = new SupplierProfile { SupplierCode = "SU0101", SupplierName = "供应商S1", MaterialCategory = "Finished", IsActive = true };
        var s2 = new SupplierProfile { SupplierCode = "SU0102", SupplierName = "供应商S2", MaterialCategory = "Finished", IsActive = true };
        ctx.SupplierProfiles.AddRange(s1, s2);
        await ctx.SaveChangesAsync();

        var y = DateTime.Today.Year;
        var cur = new DateTime(y, 6, 1);
        var prev = new DateTime(y - 1, 6, 1);

        ctx.PurchaseOrders.AddRange(
            // S1 本年部分到货
            new PurchaseOrder { OrderNo = "CG000001", SupplierId = s1.Id, SupplierName = s1.SupplierName, OrderDate = cur, Status = PurchaseOrderStatus.Partial, Weight = 10000m, ReceivedWeight = 8000m, TotalAmount = 20000m, MaterialCategory = "Finished", PlantGrade = "Q345B", Specification = "219*6", RequiredDate = cur },
            // S1 去年已完成
            new PurchaseOrder { OrderNo = "CG000002", SupplierId = s1.Id, SupplierName = s1.SupplierName, OrderDate = prev, Status = PurchaseOrderStatus.Completed, Weight = 5000m, ReceivedWeight = 5000m, TotalAmount = 15000m, MaterialCategory = "Finished", PlantGrade = "Q345B", Specification = "219*6", RequiredDate = prev },
            // S1 本年强制完成（不计待收货）
            new PurchaseOrder { OrderNo = "CG000003", SupplierId = s1.Id, SupplierName = s1.SupplierName, OrderDate = cur, Status = PurchaseOrderStatus.Completed, IsForceCompleted = true, Weight = 2000m, ReceivedWeight = 2000m, TotalAmount = 4000m, MaterialCategory = "Finished", PlantGrade = "Q345B", Specification = "219*6", RequiredDate = cur },
            // S2 本年已完成
            new PurchaseOrder { OrderNo = "CG000004", SupplierId = s2.Id, SupplierName = s2.SupplierName, OrderDate = cur, Status = PurchaseOrderStatus.Completed, Weight = 1000m, ReceivedWeight = 1000m, TotalAmount = 3000m, MaterialCategory = "Finished", PlantGrade = "Q345B", Specification = "219*6", RequiredDate = cur }
        );

        // S1 委外主表 + 两条加工费子项（Σ ProcessTotalAmount=7000）
        ctx.SubcontractOrders.Add(new SubcontractOrder
        {
            OrderNo = "WW000001", SupplierId = s1.Id, SupplierName = s1.SupplierName, OrderDate = cur, Status = SubcontractOrderStatus.Sent,
            ProcessType = "穿孔", OutMaterialCategory = "Finished", OutPlantGrade = "Q345B",
            OutSpecification = "219*6", OutQuantity = 10, OutWeight = 3000m, InWeight = 2000m,
            ReturnItems =
            {
                new SubcontractReturnItem { MaterialCategory = "Finished", ProcessSpecification = "219*6", ProcessTotalAmount = 6000m },
                new SubcontractReturnItem { MaterialCategory = "Finished", ProcessSpecification = "220*6", ProcessTotalAmount = 1000m }
            }
        });
        await ctx.SaveChangesAsync();

        // 本年到货按仓库入厂批 InboundDate 归年（采购 Purchase / 委外收回 Subcontract）；CG000002 去年到货不计本年
        ctx.InventoryBatches.AddRange(
            new InventoryBatch { BatchNo = "CK0101", WarehouseId = 1, MaterialType = "Finished", PlantGrade = "Q345B", Specification = "219*6", InboundSource = "Purchase", SourceName = "供应商", InboundDate = cur, SourceOrderNo = "CG000001", InitialQuantity = 10, InitialWeight = 8000m, RemainingQuantity = 0, RemainingWeight = 0m },
            new InventoryBatch { BatchNo = "CK0102", WarehouseId = 1, MaterialType = "Finished", PlantGrade = "Q345B", Specification = "219*6", InboundSource = "Purchase", SourceName = "供应商", InboundDate = prev, SourceOrderNo = "CG000002", InitialQuantity = 10, InitialWeight = 5000m, RemainingQuantity = 0, RemainingWeight = 0m },
            new InventoryBatch { BatchNo = "CK0103", WarehouseId = 1, MaterialType = "Finished", PlantGrade = "Q345B", Specification = "219*6", InboundSource = "Purchase", SourceName = "供应商", InboundDate = cur, SourceOrderNo = "CG000003", InitialQuantity = 10, InitialWeight = 2000m, RemainingQuantity = 0, RemainingWeight = 0m },
            new InventoryBatch { BatchNo = "CK0104", WarehouseId = 1, MaterialType = "Finished", PlantGrade = "Q345B", Specification = "219*6", InboundSource = "Purchase", SourceName = "供应商", InboundDate = cur, SourceOrderNo = "CG000004", InitialQuantity = 10, InitialWeight = 1000m, RemainingQuantity = 0, RemainingWeight = 0m },
            new InventoryBatch { BatchNo = "CK0105", WarehouseId = 1, MaterialType = "Finished", PlantGrade = "Q345B", Specification = "219*6", InboundSource = "Subcontract", SourceName = "供应商", InboundDate = cur, SourceOrderNo = "WW000001", InitialQuantity = 10, InitialWeight = 2000m, RemainingQuantity = 0, RemainingWeight = 0m }
        );
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, SortBy = "suppliercode", IsDescending = false });

        var d1 = result.Items.First(x => x.SupplierName == "供应商S1");
        d1.TotalOrderCount.Should().Be(4);          // 3 采购 + 1 委外
        d1.TotalWeight.Should().Be(20000m);         // 10000+5000+2000 + 委外 OutWeight 3000
        d1.TotalAmount.Should().Be(46000m);         // 20000+15000+4000 + 委外加工费 7000
        d1.YearOrderCount.Should().Be(3);           // 去年 CG000002 不计本年
        d1.YearWeight.Should().Be(15000m);          // 10000+2000+3000
        d1.YearAmount.Should().Be(31000m);          // 20000+4000+7000
        d1.ArrivedWeight.Should().Be(12000m);       // 本年到货毛(入厂批当年)：CG000001 8000 + CG000003 2000 + WW000001 2000；去年 CG000002 不计本年
        d1.PendingWeight.Should().Be(3000m);        // 仅未完成单：CG000001 10000-8000 + WW 3000-2000（完成/强完不计）
        d1.YearReturnWeight.Should().Be(0m);
        // 到货货款 = Σ 单金额×到货重份额（按单认领）：CG000001 20000×8000/10000 + CG000003 4000 + WW 7000×2000/3000
        d1.ArrivedAmount.Should().BeApproximately(24666.67m, 0.01m);
        // 待收货款 = 未完成单 单金额×欠交份额：CG000001 20000×2000/10000 + WW 7000×1000/3000
        d1.PendingAmount.Should().BeApproximately(6333.33m, 0.01m);

        var d2 = result.Items.First(x => x.SupplierName == "供应商S2");
        d2.TotalOrderCount.Should().Be(1);
        d2.TotalWeight.Should().Be(1000m);
        d2.ArrivedWeight.Should().Be(1000m);
        d2.ArrivedAmount.Should().Be(3000m);        // CG000004 已完成仍计到货货款 3000×1000/1000
        d2.PendingWeight.Should().Be(0m);           // 已完成不计
    }

    [Fact]
    public async Task GetPagedAsync_往来统计_本年到货净扣本年退货且跨年退货不扣()
    {
        var ctx = CreateDbContext();
        var s = new SupplierProfile { SupplierCode = "SU0201", SupplierName = "退货供应商", MaterialCategory = "Finished", IsActive = true };
        ctx.SupplierProfiles.Add(s);
        await ctx.SaveChangesAsync();

        var po = new PurchaseOrder
        {
            OrderNo = "CG000100", SupplierId = s.Id, SupplierName = s.SupplierName, OrderDate = new DateTime(DateTime.Today.Year, 3, 1),
            Status = PurchaseOrderStatus.Partial, Weight = 5000m, ReceivedWeight = 5000m, TotalAmount = 10000m,
            MaterialCategory = "Finished", PlantGrade = "Q345B", Specification = "219*6", RequiredDate = DateTime.Today
        };
        ctx.PurchaseOrders.Add(po);
        await ctx.SaveChangesAsync();

        // 采购入库仓库批（来源单号=采购单号），后被退货出库
        var batch = new InventoryBatch
        {
            BatchNo = "CK000001", WarehouseId = 1, MaterialType = "Finished", PlantGrade = "Q345B",
            Specification = "219*6", InboundSource = "Purchase", SourceName = "供应商",
            InboundDate = DateTime.Today, SourceOrderNo = po.OrderNo,
            InitialQuantity = 100, InitialWeight = 5000m, RemainingQuantity = 0, RemainingWeight = 0m
        };
        ctx.InventoryBatches.Add(batch);
        await ctx.SaveChangesAsync();

        ctx.OutboundRecords.AddRange(
            new OutboundRecord { OutboundType = OutboundType.ReturnOut, ReturnSourceBatchNo = batch.BatchNo, OutboundDate = new DateTime(DateTime.Today.Year, 6, 1), OutboundQuantity = 10, OutboundWeight = 600m },
            new OutboundRecord { OutboundType = OutboundType.ReturnOut, ReturnSourceBatchNo = batch.BatchNo, OutboundDate = new DateTime(DateTime.Today.Year - 1, 6, 1), OutboundQuantity = 5, OutboundWeight = 400m }
        );
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20 });
        var d = result.Items.Single();

        d.TotalAmount.Should().Be(10000m);
        d.ArrivedWeight.Should().Be(4400m);         // 本年到货净 = 本年入厂毛 5000 − 本年退货 600（去年退货 400 不扣本年到货）
        d.ArrivedAmount.Should().Be(8800m);         // 到货货款净 = 10000×5000/5000 毛 − 本年退货货款 10000×600/5000=1200（去年退货不扣）
        d.YearReturnWeight.Should().Be(600m);       // 本年退货仅按出库年份累计
        d.PendingWeight.Should().Be(1000m);         // Partial 单：应到 5000 − 净到 4000（净到扣跨年退货累计 1000）
        d.PendingAmount.Should().Be(2000m);         // 欠交 1000×单金额份额 10000×1000/5000
    }

    [Fact]
    public async Task GetPagedAsync_往来统计_同名供应商跨物料分类按档案行分流不混算()
    {
        var ctx = CreateDbContext();
        // 同一供应商「宏达」按物料分类拆两个档案行。真实场景：单据 SupplierId 常只指向其一档案，但单头分类各自独立
        var pF = new SupplierProfile { SupplierCode = "SU1001", SupplierName = "宏达", MaterialCategory = "Finished", IsActive = true };
        var pR = new SupplierProfile { SupplierCode = "SU1002", SupplierName = "宏达", MaterialCategory = "RoundBar", IsActive = true };
        ctx.SupplierProfiles.AddRange(pF, pR);
        await ctx.SaveChangesAsync();

        var cur = new DateTime(DateTime.Today.Year, 6, 1);
        ctx.PurchaseOrders.AddRange(
            // Finished 档案行单据
            new PurchaseOrder { OrderNo = "CG000201", SupplierId = pF.Id, SupplierName = "宏达", OrderDate = cur, Status = PurchaseOrderStatus.Partial, Weight = 10000m, ReceivedWeight = 6000m, TotalAmount = 20000m, MaterialCategory = "Finished", PlantGrade = "Q345B", Specification = "219*6", RequiredDate = cur },
            // RoundBar 单据但 SupplierId 挂 pF(Finished) → 按「名+分类」必须分流到 pR 行
            new PurchaseOrder { OrderNo = "CG000202", SupplierId = pF.Id, SupplierName = "宏达", OrderDate = cur, Status = PurchaseOrderStatus.Partial, Weight = 7000m, ReceivedWeight = 3000m, TotalAmount = 14000m, MaterialCategory = "RoundBar", PlantGrade = "45#", Specification = "40", RequiredDate = cur },
            // RoundBar 档案行单据
            new PurchaseOrder { OrderNo = "CG000203", SupplierId = pR.Id, SupplierName = "宏达", OrderDate = cur, Status = PurchaseOrderStatus.Completed, Weight = 3000m, ReceivedWeight = 3000m, TotalAmount = 6000m, MaterialCategory = "RoundBar", PlantGrade = "45#", Specification = "50", RequiredDate = cur }
        );
        await ctx.SaveChangesAsync();

        // 本年到货：CG000201 归 Finished 档案、CG000202(单头 RoundBar 但 SupplierId 挂 pF) + CG000203 归 RoundBar 档案（按单头分类分流）
        ctx.InventoryBatches.AddRange(
            new InventoryBatch { BatchNo = "CK0201", WarehouseId = 1, MaterialType = "Finished", PlantGrade = "Q345B", Specification = "219*6", InboundSource = "Purchase", SourceName = "宏达", InboundDate = cur, SourceOrderNo = "CG000201", InitialQuantity = 10, InitialWeight = 6000m, RemainingQuantity = 0, RemainingWeight = 0m },
            new InventoryBatch { BatchNo = "CK0202", WarehouseId = 1, MaterialType = "RoundBar", PlantGrade = "45#", Specification = "40", InboundSource = "Purchase", SourceName = "宏达", InboundDate = cur, SourceOrderNo = "CG000202", InitialQuantity = 10, InitialWeight = 3000m, RemainingQuantity = 0, RemainingWeight = 0m },
            new InventoryBatch { BatchNo = "CK0203", WarehouseId = 1, MaterialType = "RoundBar", PlantGrade = "45#", Specification = "50", InboundSource = "Purchase", SourceName = "宏达", InboundDate = cur, SourceOrderNo = "CG000203", InitialQuantity = 10, InitialWeight = 3000m, RemainingQuantity = 0, RemainingWeight = 0m }
        );
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetPagedAsync(new QueryParams { PageIndex = 1, PageSize = 20, SortBy = "SupplierCode", IsDescending = false });

        var f = result.Items.Single(x => x.SupplierName == "宏达" && x.MaterialCategory == MaterialType.Finished);
        f.TotalOrderCount.Should().Be(1);           // 仅 CG000201，不混入 RoundBar 两单
        f.TotalWeight.Should().Be(10000m);
        f.TotalAmount.Should().Be(20000m);
        f.ArrivedWeight.Should().Be(6000m);
        f.ArrivedAmount.Should().Be(12000m);        // CG000201 20000×6000/10000
        f.PendingWeight.Should().Be(4000m);
        f.PendingAmount.Should().Be(8000m);         // CG000201 20000×4000/10000

        var r = result.Items.Single(x => x.SupplierName == "宏达" && x.MaterialCategory == MaterialType.RoundBar);
        r.TotalOrderCount.Should().Be(2);           // CG000202（挂 pF 档案但分类 RoundBar）+ CG000203
        r.TotalWeight.Should().Be(10000m);          // 7000 + 3000
        r.TotalAmount.Should().Be(20000m);          // 14000 + 6000
        r.ArrivedWeight.Should().Be(6000m);         // 3000 + 3000
        r.ArrivedAmount.Should().Be(12000m);        // CG000202 14000×3000/7000 + CG000203 6000×3000/3000
        r.PendingWeight.Should().Be(4000m);         // 仅 CG000202：7000 − 3000（CG000203 已完成不计）
        r.PendingAmount.Should().Be(8000m);         // CG000202 14000×4000/7000
    }
}
