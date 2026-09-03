using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Payroll;
using MES.Core.Exceptions;
using MES.Data;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Payroll;
using MES.Services.Payroll;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 杂辅工记录服务测试（2026-09-03）：
/// 台账登记（行=一条杂辅任务），金额为手工录入源头保留小数；同人同日可多条（无唯一约束）；
/// 只当月区间读取；停用员工历史行仍显示 Code/Name；新增强制启用、编辑允许停用且不改员工归属。
/// </summary>
public class PayrollMiscWorkServiceTests : TestBase
{
    private static async Task<Employee> SeedEmpAsync(AppDbContext ctx, string code, string name, bool active = true)
    {
        var e = new Employee { Code = code, Name = name, IsActive = active };
        ctx.Employees.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static async Task SeedRecordAsync(AppDbContext ctx, int empId, DateTime date, string content,
        decimal hours, decimal amount, string? remark = null)
    {
        ctx.PayrollMiscWorkRecords.Add(new PayrollMiscWorkRecord
        {
            EmployeeId = empId,
            WorkDate = date,
            Content = content,
            Hours = hours,
            Amount = amount,
            Remark = remark
        });
        await ctx.SaveChangesAsync();
    }

    private static MiscWorkRecordInputDto RecIn(int empId, string content, decimal hours, decimal amount,
        DateTime date, string? remark = null, int id = 0)
        => new()
        {
            Id = id,
            EmployeeId = empId,
            WorkDate = date,
            Content = content,
            Hours = hours,
            Amount = amount,
            Remark = remark
        };

    // ==================== 读取：月份口径 / 映射 / 合计 / 排序 ====================

    [Fact]
    public async Task GetMonthAsync_只当月记录_CodeName正确映射_条数合计整月口径()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollMiscWorkService(ctx);

        var a = await SeedEmpAsync(ctx, "YG005", "张三");
        var b = await SeedEmpAsync(ctx, "YG008", "李四");
        await SeedRecordAsync(ctx, a.Id, new DateTime(2026, 3, 3), "打扫卫生", 4m, 64m);
        await SeedRecordAsync(ctx, b.Id, new DateTime(2026, 3, 10), "喷码2小时,修磨3小时", 5m, 100m);
        await SeedRecordAsync(ctx, b.Id, new DateTime(2026, 2, 28), "跨月", 1m, 18m); // 不在 3 月
        await SeedRecordAsync(ctx, a.Id, new DateTime(2026, 4, 1), "跨月2", 1m, 20m);  // 不在 3 月

        var month = await svc.GetMonthAsync(2026, 3);
        month.RecordCount.Should().Be(2);
        month.TotalHours.Should().Be(9m);
        month.TotalAmount.Should().Be(164m);
        month.Records.Should().HaveCount(2);
        var rowA = month.Records.Single(r => r.EmployeeId == a.Id);
        rowA.EmployeeCode.Should().Be("YG005");
        rowA.EmployeeName.Should().Be("张三");
        var rowB = month.Records.Single(r => r.EmployeeId == b.Id);
        rowB.EmployeeCode.Should().Be("YG008");
        rowB.EmployeeName.Should().Be("李四");
    }

    [Fact]
    public async Task GetMonthAsync_记录引用已停用员工_仍显示CodeName不省略()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollMiscWorkService(ctx);

        var off = await SeedEmpAsync(ctx, "YG999", "离职工", active: false);
        await SeedRecordAsync(ctx, off.Id, new DateTime(2026, 3, 5), "整理仓库", 3m, 54m);

        var month = await svc.GetMonthAsync(2026, 3);
        var row = month.Records.Single();
        row.EmployeeCode.Should().Be("YG999");
        row.EmployeeName.Should().Be("离职工");
    }

    [Fact]
    public async Task GetMonthAsync_记录引用已删除员工_CodeName兜底短横()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollMiscWorkService(ctx);
        // 直接插入一条引用不存在 EmployeeId 的记录（模拟员工被删的历史残留）
        await SeedRecordAsync(ctx, 999999, new DateTime(2026, 3, 6), "杂务", 2m, 30m);

        var month = await svc.GetMonthAsync(2026, 3);
        var row = month.Records.Single();
        row.EmployeeCode.Should().Be("-");
        row.EmployeeName.Should().Be("-");
        month.RecordCount.Should().Be(1);
    }

    [Fact]
    public async Task GetMonthAsync_按日期然后工号然后Id稳定排序_小数原样保留()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollMiscWorkService(ctx);

        var a = await SeedEmpAsync(ctx, "Z001", "甲");
        var b = await SeedEmpAsync(ctx, "A001", "乙");
        // 乱序插入：同人同日两条、跨人同日
        await SeedRecordAsync(ctx, a.Id, new DateTime(2026, 3, 10), "先写", 1.5m, 37.5m);
        await SeedRecordAsync(ctx, b.Id, new DateTime(2026, 3, 3), "早日期", 2m, 40m);
        await SeedRecordAsync(ctx, a.Id, new DateTime(2026, 3, 10), "后写同日", 1m, 20m);
        await SeedRecordAsync(ctx, a.Id, new DateTime(2026, 3, 3), "同日乙早", 0.5m, 9m);

        var month = await svc.GetMonthAsync(2026, 3);
        var rows = month.Records;
        rows.Should().HaveCount(4);
        // 排序：(3/3 A001 早日期) → (3/3 Z001 同日乙早) → (3/10 Z001 先写) → (3/10 Z001 后写同日，同人同日按 Id)
        rows[0].EmployeeCode.Should().Be("A001"); rows[0].Content.Should().Be("早日期");
        rows[1].EmployeeCode.Should().Be("Z001"); rows[1].Content.Should().Be("同日乙早");
        rows[2].EmployeeCode.Should().Be("Z001"); rows[2].Content.Should().Be("先写");
        rows[3].EmployeeCode.Should().Be("Z001"); rows[3].Content.Should().Be("后写同日");
        // 小数原样保留：1.5 小时 / 37.5 元
        var x = rows[2];
        x.Hours.Should().Be(1.5m);
        x.Amount.Should().Be(37.5m);
    }

    [Fact]
    public async Task GetMonthAsync_月份参数无效抛业务异常()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollMiscWorkService(ctx);
        await FluentActions.Awaiting(() => svc.GetMonthAsync(1999, 3)).Should().ThrowAsync<BusinessException>();
        await FluentActions.Awaiting(() => svc.GetMonthAsync(2026, 13)).Should().ThrowAsync<BusinessException>();
    }

    // ==================== 保存：新增 / 编辑 ====================

    [Fact]
    public async Task Save_新增返回Id且小数原样落库_同人同日可多条()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollMiscWorkService(ctx);

        var a = await SeedEmpAsync(ctx, "YG005", "张三");
        var id1 = await svc.SaveRecordAsync(new MiscWorkRecordInputDto
        {
            Id = 0,
            EmployeeId = a.Id,
            WorkDate = new DateTime(2026, 3, 3),
            Content = "打扫卫生",
            Hours = 1.5m,
            Amount = 37.5m,
            Remark = "一楼车间"
        });
        id1.Should().BeGreaterThan(0);

        // 同人同日第二条：允许（无唯一约束）
        var id2 = await svc.SaveRecordAsync(new MiscWorkRecordInputDto
        {
            Id = 0,
            EmployeeId = a.Id,
            WorkDate = new DateTime(2026, 3, 3),
            Content = "搬运物料",
            Hours = 2m,
            Amount = 40m
        });
        id2.Should().BeGreaterThan(0).And.NotBe(id1);

        var saved = await ctx.PayrollMiscWorkRecords.ToListAsync();
        saved.Should().HaveCount(2);
        var first = saved.Single(r => r.Id == id1);
        first.EmployeeId.Should().Be(a.Id);
        first.Content.Should().Be("打扫卫生");
        first.Hours.Should().Be(1.5m);
        first.Amount.Should().Be(37.5m);
        first.Remark.Should().Be("一楼车间");
    }

    [Fact]
    public async Task Save_编辑改字段不改员工归属_编辑停用员工可改()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollMiscWorkService(ctx);

        var a = await SeedEmpAsync(ctx, "YG005", "张三");
        var b = await SeedEmpAsync(ctx, "YG008", "李四");
        var id = await svc.SaveRecordAsync(new MiscWorkRecordInputDto
        {
            Id = 0, EmployeeId = a.Id,
            WorkDate = new DateTime(2026, 3, 3), Content = "原内容", Hours = 1m, Amount = 20m
        });

        // 把 a 停用后仍可编辑其历史行（离职后改历史）；入参 EmployeeId 传 b 也不得改变归属
        var aEntity = await ctx.Employees.FindAsync(a.Id);
        aEntity!.IsActive = false;
        await ctx.SaveChangesAsync();

        await svc.SaveRecordAsync(new MiscWorkRecordInputDto
        {
            Id = id, EmployeeId = b.Id,
            WorkDate = new DateTime(2026, 3, 4), Content = "新内容", Hours = 2.5m, Amount = 62.5m, Remark = "改"
        });

        var saved = await ctx.PayrollMiscWorkRecords.SingleAsync(r => r.Id == id);
        saved.EmployeeId.Should().Be(a.Id);   // 员工归属不随编辑改变
        saved.Content.Should().Be("新内容");
        saved.WorkDate.Should().Be(new DateTime(2026, 3, 4));
        saved.Hours.Should().Be(2.5m);
        saved.Amount.Should().Be(62.5m);
        saved.Remark.Should().Be("改");
    }

    [Fact]
    public async Task Save_新增停用员工抛异常()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollMiscWorkService(ctx);

        var off = await SeedEmpAsync(ctx, "YG999", "离职工", active: false);
        await FluentActions.Awaiting(() => svc.SaveRecordAsync(new MiscWorkRecordInputDto
        {
            Id = 0, EmployeeId = off.Id,
            WorkDate = new DateTime(2026, 3, 3), Content = "杂务", Hours = 1m, Amount = 20m
        })).Should().ThrowAsync<BusinessException>()
            .WithMessage("*停用*");
    }

    [Fact]
    public async Task Save_内容空或超长_备注超长_负数_日期无效均抛()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollMiscWorkService(ctx);
        var a = await SeedEmpAsync(ctx, "YG005", "张三");
        var date = new DateTime(2026, 3, 3);

        await FluentActions.Awaiting(() => svc.SaveRecordAsync(RecIn(a.Id, "   ", 1m, 20m, date)))
            .Should().ThrowAsync<BusinessException>().WithMessage("*内容不能为空*");
        await FluentActions.Awaiting(() => svc.SaveRecordAsync(RecIn(a.Id, new string('长', 501), 1m, 20m, date)))
            .Should().ThrowAsync<BusinessException>().WithMessage("*不能超过 500*");
        await FluentActions.Awaiting(() => svc.SaveRecordAsync(RecIn(a.Id, "正常", 1m, 20m, date, remark: new string('备', 201))))
            .Should().ThrowAsync<BusinessException>().WithMessage("*备注不能超过 200*");
        await FluentActions.Awaiting(() => svc.SaveRecordAsync(RecIn(a.Id, "正常", -1m, 20m, date)))
            .Should().ThrowAsync<BusinessException>().WithMessage("*小时数不能为负数*");
        await FluentActions.Awaiting(() => svc.SaveRecordAsync(RecIn(a.Id, "正常", 1m, -0.01m, date)))
            .Should().ThrowAsync<BusinessException>().WithMessage("*杂辅工资不能为负数*");
        await FluentActions.Awaiting(() => svc.SaveRecordAsync(RecIn(a.Id, "正常", 1m, 20m, new DateTime(1999, 12, 31))))
            .Should().ThrowAsync<BusinessException>().WithMessage("*日期无效*");
        await FluentActions.Awaiting(() => svc.SaveRecordAsync(RecIn(999999, "正常", 1m, 20m, date)))
            .Should().ThrowAsync<BusinessException>().WithMessage("*员工不存在*");

        // 全部失败后不应产生任何残留记录
        (await ctx.PayrollMiscWorkRecords.CountAsync()).Should().Be(0);
    }

    // ==================== 删除 ====================

    [Fact]
    public async Task Delete_删除后消失_删除不存在抛()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollMiscWorkService(ctx);

        var a = await SeedEmpAsync(ctx, "YG005", "张三");
        var id = await svc.SaveRecordAsync(new MiscWorkRecordInputDto
        {
            Id = 0, EmployeeId = a.Id,
            WorkDate = new DateTime(2026, 3, 3), Content = "要删的", Hours = 1m, Amount = 20m
        });

        await svc.DeleteRecordAsync(id);
        (await ctx.PayrollMiscWorkRecords.CountAsync()).Should().Be(0);

        await FluentActions.Awaiting(() => svc.DeleteRecordAsync(id)).Should().ThrowAsync<BusinessException>()
            .WithMessage("*不存在或已删除*");
        await FluentActions.Awaiting(() => svc.DeleteRecordAsync(88888)).Should().ThrowAsync<BusinessException>()
            .WithMessage("*不存在或已删除*");
    }

    [Fact]
    public async Task Save_员工不存在抛_编辑记录不存在抛()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollMiscWorkService(ctx);

        // 新增引用不存在员工
        await FluentActions.Awaiting(() => svc.SaveRecordAsync(new MiscWorkRecordInputDto
        {
            Id = 0, EmployeeId = 777777,
            WorkDate = new DateTime(2026, 3, 3), Content = "内容", Hours = 1m, Amount = 20m
        })).Should().ThrowAsync<BusinessException>()
            .WithMessage("*员工不存在*");

        // 编辑不存在的记录（员工先校验存在，用已存在员工 Id 才能命中「记录不存在」分支）
        var a = await SeedEmpAsync(ctx, "YG005", "张三");
        await FluentActions.Awaiting(() => svc.SaveRecordAsync(RecIn(a.Id, "内容", 1m, 20m, new DateTime(2026, 3, 3), id: 55555)))
            .Should().ThrowAsync<BusinessException>()
            .WithMessage("*记录不存在或已删除*");
    }
}
