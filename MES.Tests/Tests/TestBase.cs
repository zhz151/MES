using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MES.Data;
using MES.Data.Entities;
using MES.Core.Enums;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Order;
using MES.Data.Entities.ProductionStandard;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.WorkOrder;

namespace MES.Tests.Tests;

/// <summary>
/// 测试专用的 AppDbContext：移除 SQL Server 特有的 IsRowVersion 配置
/// </summary>
public class TestAppDbContext : AppDbContext
{
    private static readonly byte[] DefaultRowVersion = new byte[8];

    public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // InMemory 不支持 IsRowVersion()：
        // 1. 为新实体手动设置 RowVersion
        // 2. 修改实体时强制 OriginalValue 为 DefaultRowVersion 引用（InMemory 用引用相等比较 byte[]）
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is SalesOrder so)
            {
                if (entry.State == EntityState.Added)
                    so.RowVersion = DefaultRowVersion;
                else if (entry.State == EntityState.Modified)
                    entry.Property(nameof(SalesOrder.RowVersion)).OriginalValue = DefaultRowVersion;
            }
            if (entry.Entity is WorkOrder wo)
            {
                if (entry.State == EntityState.Added)
                    wo.RowVersion = DefaultRowVersion;
                else if (entry.State == EntityState.Modified)
                    entry.Property(nameof(WorkOrder.RowVersion)).OriginalValue = DefaultRowVersion;
            }
            if (entry.Entity is ProductionBatch pb)
            {
                if (entry.State == EntityState.Added)
                    pb.RowVersion = DefaultRowVersion;
                else if (entry.State == EntityState.Modified)
                    entry.Property(nameof(ProductionBatch.RowVersion)).OriginalValue = DefaultRowVersion;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// 测试基类：提供 InMemory DbContext 工厂方法、种子数据初始化
/// </summary>
public abstract class TestBase
{
    protected AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MES_Test_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(options);
    }

    /// <summary>
    /// 种子一个测试仓库
    /// </summary>
    protected async Task<Warehouse> SeedWarehouseAsync(AppDbContext ctx, string name = "测试仓库")
    {
        var wh = new Warehouse { Name = name, Code = "WH001" };
        ctx.Warehouses.Add(wh);
        await ctx.SaveChangesAsync();
        return wh;
    }

    /// <summary>
    /// 种子一个测试客户
    /// </summary>
    protected async Task<CustomerProfile> SeedCustomerAsync(AppDbContext ctx, string unit = "测试客户")
    {
        var c = new CustomerProfile
        {
            CustomerCode = $"C{DateTime.Now:yyyyMMddHHmmss}",
            CustomerUnit = unit,
            Salesman = "测试业务员",
            Status = Core.Enums.CustomerStatus.Active
        };
        ctx.CustomerProfiles.Add(c);
        await ctx.SaveChangesAsync();
        return c;
    }

    /// <summary>
    /// 种子一个测试牌号映射
    /// </summary>
    protected async Task<StandardGradeMapping> SeedGradeMappingAsync(AppDbContext ctx,
        string standardGrade = "Q345B", string plantGrade = "Q345B", decimal density = 7.85m)
    {
        var gm = new StandardGradeMapping
        {
            StandardGrade = standardGrade,
            PlantGrade = plantGrade,
            Density = density
        };
        ctx.StandardGradeMappings.Add(gm);
        await ctx.SaveChangesAsync();
        return gm;
    }

    /// <summary>
    /// 种子一个测试标准号
    /// </summary>
    protected async Task<StandardRegister> SeedRegisterAsync(AppDbContext ctx,
        string standardNo = "GB/T 8163", string standardName = "流体管标准")
    {
        // 检查是否已存在相同标准号的记录，避免 ToDictionaryAsync 键冲突
        var existing = await ctx.StandardRegisters
            .FirstOrDefaultAsync(sr => sr.StandardNo == standardNo);
        if (existing != null)
            return existing;

        var sr = new StandardRegister
        {
            StandardNo = standardNo,
            StandardName = standardName
        };
        ctx.StandardRegisters.Add(sr);
        await ctx.SaveChangesAsync();
        return sr;
    }
}
