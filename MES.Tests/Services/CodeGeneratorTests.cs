using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Services.Helpers;

namespace MES.Tests.Services;

/// <summary>
/// 编号生成器测试：6位编码（2字母前缀 + 4位数字）
/// </summary>
public class CodeGeneratorTests
{
    /// <summary>
    /// 用 EF InMemory 创建支持 async 的 IQueryable
    /// </summary>
    private static (IQueryable<string> query, CodeGenContext ctx) BuildCodeQuery(string prefix, params string[] codes)
    {
        var options = new DbContextOptionsBuilder<CodeGenContext>()
            .UseInMemoryDatabase($"CodeGen_{Guid.NewGuid()}")
            .Options;

        var ctx = new CodeGenContext(options);

        var entities = codes.Select(c => new CodeRow { Id = $"{prefix}{Guid.NewGuid():N}", Code = c }).ToList();
        ctx.Codes.AddRange(entities);
        ctx.SaveChanges();

        return (ctx.Codes.Where(r => r.Id.StartsWith(prefix)).Select(r => r.Code), ctx);
    }

    [Fact]
    public async Task GenerateNextAsync_空数据_返回前缀0001()
    {
        var (existing, ctx) = BuildCodeQuery("MA");
        using (ctx) { var result = await CodeGenerator.GenerateNextAsync(existing, "MA"); result.Should().Be("MA0001"); }
    }

    [Fact]
    public async Task GenerateNextAsync_有最大编码_返回递增()
    {
        var (existing, ctx) = BuildCodeQuery("MA", "MA0001", "MA0002", "MA0003");
        using (ctx)
        {
            var result = await CodeGenerator.GenerateNextAsync(existing, "MA");
            result.Should().Be("MA0004");
        }
    }

    [Fact]
    public async Task GenerateNextAsync_多个编码_取最大值加1()
    {
        var (existing, ctx) = BuildCodeQuery("MA", "MA0042", "MA0100", "MA0005");
        using (ctx)
        {
            var result = await CodeGenerator.GenerateNextAsync(existing, "MA");
            result.Should().Be("MA0101");
        }
    }

    [Fact]
    public async Task GenerateNextAsync_不同前缀_不干扰()
    {
        var (existing, ctx) = BuildCodeQuery("MA", "MA0005", "SU0003", "MA0002");
        using (ctx)
        {
            var result = await CodeGenerator.GenerateNextAsync(existing, "SU");
            result.Should().Be("SU0004");
        }
    }

    [Fact]
    public async Task GenerateNextAsync_短编码_跳过()
    {
        var (existing, ctx) = BuildCodeQuery("MA", "MA001", "MA00002", "MA");
        using (ctx)
        {
            var result = await CodeGenerator.GenerateNextAsync(existing, "MA");
            result.Should().Be("MA0001");
        }
    }

    [Fact]
    public async Task GenerateNextAsync_编码不连续_取最大值()
    {
        var (existing, ctx) = BuildCodeQuery("MA", "MA0001", "MA0005", "MA0003", "MA0002");
        using (ctx)
        {
            var result = await CodeGenerator.GenerateNextAsync(existing, "MA");
            result.Should().Be("MA0006");
        }
    }

    [Fact]
    public async Task GenerateNextAsync_D4格式化_保持4位()
    {
        var (existing, ctx) = BuildCodeQuery("MA", "MA0999");
        using (ctx)
        {
            var result = await CodeGenerator.GenerateNextAsync(existing, "MA");
            result.Should().Be("MA1000");
        }
    }

    [Fact]
    public async Task GenerateNextAsync_边缘9999_返回10000()
    {
        var (existing, ctx) = BuildCodeQuery("MA", "MA9999");
        using (ctx)
        {
            var result = await CodeGenerator.GenerateNextAsync(existing, "MA");
            result.Should().Be("MA10000");
        }
    }
}

/// <summary>
/// 用于 InMemory 存储编码的简单 DbContext
/// </summary>
public class CodeGenContext : DbContext
{
    public CodeGenContext(DbContextOptions<CodeGenContext> options) : base(options) { }

    public DbSet<CodeRow> Codes => Set<CodeRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CodeRow>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Code).IsRequired();
        });
    }
}

/// <summary>
/// 用于 InMemory 存储编码的简单实体
/// </summary>
public class CodeRow
{
    public string Id { get; set; } = null!;
    public string Code { get; set; } = null!;
}
