using FluentAssertions;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Services.Configuration;
using MES.Tests.Tests;
using Xunit;

namespace MES.Tests.Services.Configuration;

/// <summary>
/// 操作人实名校验服务测试：LoadActive 只含启用员工、命中/未命中/空串/禁用员工。
/// </summary>
public class OperatorNameValidatorTests : TestBase
{
    private static async Task SeedEmployeeAsync(AppDbContext ctx, string code, string name, bool isActive = true)
    {
        ctx.Employees.Add(new Employee { Code = code, Name = name, IsActive = isActive });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task LoadActiveAsync_只含启用员工()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, "1001", "张三");
        await SeedEmployeeAsync(ctx, "1002", "李四", isActive: false);
        await SeedEmployeeAsync(ctx, "1003", "王五");

        var validator = new OperatorNameValidator(ctx);
        var active = await validator.LoadActiveAsync();

        active.Names.Should().Contain(new[] { "张三", "王五" });
        active.Names.Should().NotContain("李四");
        active.ByCode.Should().ContainKey("1001");
        active.ByCode.Should().ContainKey("1003");
        active.ByCode.Should().NotContainKey("1002");
    }

    [Fact]
    public async Task EnsureValidOrThrowAsync_命中不抛()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, "1001", "张三");

        var validator = new OperatorNameValidator(ctx);
        await validator.EnsureValidOrThrowAsync("张三(1001)");
    }

    [Fact]
    public async Task EnsureValidOrThrowAsync_未命中抛BusinessException()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, "1001", "张三");

        var validator = new OperatorNameValidator(ctx);
        var act = async () => await validator.EnsureValidOrThrowAsync("切割");
        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*操作人「切割」不在启用员工表中*");
    }

    [Fact]
    public async Task EnsureValidOrThrowAsync_空串与空白不抛()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, "1001", "张三");

        var validator = new OperatorNameValidator(ctx);
        await validator.EnsureValidOrThrowAsync(null);
        await validator.EnsureValidOrThrowAsync("");
        await validator.EnsureValidOrThrowAsync("   ");
    }

    [Fact]
    public async Task EnsureValidOrThrowAsync_禁用员工被拒()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, "1001", "张三", isActive: false);

        var validator = new OperatorNameValidator(ctx);
        var act = async () => await validator.EnsureValidOrThrowAsync("张三(1001)");
        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task EnsureValidOrThrowAsync_rowLabel出现在消息前缀()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, "1001", "张三");

        var validator = new OperatorNameValidator(ctx);
        var act = async () => await validator.EnsureValidOrThrowAsync("切割", rowLabel: "第3行");
        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("第3行：操作人「切割」不在启用员工表中，请选择有效操作人");
    }
}
