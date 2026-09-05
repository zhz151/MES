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
/// 考勤服务测试：月视图读取（仅启用员工、稀疏记录填充日小时/出勤天数/总小时、关键字过滤、月份校验）
/// 与整月保存（upsert 有值、null/0 清空删除、日期/小时范围校验）。
/// </summary>
public class AttendanceServiceTests : TestBase
{
    private const int Year = 2026;
    private const int Month = 2; // 平年 2 月共 28 天

    private async Task<Employee> SeedEmployeeAsync(AppDbContext ctx,
        string code, string name, string? department = "Workshop", bool active = true)
    {
        var e = new Employee { Code = code, Name = name, Department = department, IsActive = active };
        ctx.Employees.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static AttendanceService CreateService(AppDbContext ctx) => new(ctx);

    private async Task SeedRecordAsync(AppDbContext ctx, int employeeId, int day, decimal hours)
    {
        ctx.AttendanceRecords.Add(new AttendanceRecord
        {
            EmployeeId = employeeId,
            AttendDate = new DateTime(Year, Month, day),
            WorkHours = hours
        });
        await ctx.SaveChangesAsync();
    }

    // ========== GetMonthAsync：月份校验 ==========

    [Theory]
    [InlineData(1999, 2)]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    public async Task GetMonthAsync_月份参数无效_抛业务异常(int year, int month)
    {
        var ctx = CreateDbContext();
        var act = async () => await CreateService(ctx).GetMonthAsync(year, month, null);
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*月份参数无效*");
    }

    // ========== GetMonthAsync：启用员工与空白网格 ==========

    [Fact]
    public async Task GetMonthAsync_仅启用员工_初始日小时全空()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, "E001", "张三");
        await SeedEmployeeAsync(ctx, "E002", "李四", active: false);

        var dto = await CreateService(ctx).GetMonthAsync(Year, Month, null);

        dto.Year.Should().Be(Year);
        dto.Month.Should().Be(Month);
        var row = dto.Employees.Single();
        row.EmployeeCode.Should().Be("E001");
        row.EmployeeName.Should().Be("张三");
        row.PositionCategory.Should().Be("Workshop");
        row.AttendanceDays.Should().Be(0);
        row.TotalHours.Should().Be(0m);
        row.DayHours.Keys.Should().Equal(Enumerable.Range(1, 28)); // 全 28 天键
        row.DayHours.Values.Should().AllBeEquivalentTo((decimal?)null);
    }

    [Fact]
    public async Task GetMonthAsync_员工按工号升序()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, "B002", "乙");
        await SeedEmployeeAsync(ctx, "A001", "甲");

        var dto = await CreateService(ctx).GetMonthAsync(Year, Month, null);

        dto.Employees.Select(r => r.EmployeeCode).Should().Equal("A001", "B002");
    }

    // ========== GetMonthAsync：记录填充与统计 ==========

    [Fact]
    public async Task GetMonthAsync_稀疏记录填充日小时_并累计出勤天数总小时()
    {
        var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx, "E001", "张三");
        await SeedRecordAsync(ctx, emp.Id, 5, 8m);
        await SeedRecordAsync(ctx, emp.Id, 6, 4.5m);
        await SeedRecordAsync(ctx, emp.Id, 20, 8m);

        var dto = await CreateService(ctx).GetMonthAsync(Year, Month, null);

        var row = dto.Employees.Single();
        row.DayHours[5].Should().Be(8m);
        row.DayHours[6].Should().Be(4.5m);
        row.DayHours[20].Should().Be(8m);
        row.DayHours[7].Should().BeNull(); // 未出勤日保持 null
        row.AttendanceDays.Should().Be(3);
        row.TotalHours.Should().Be(20.5m);
    }

    [Fact]
    public async Task GetMonthAsync_非当月记录忽略()
    {
        var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx, "E001", "张三");
        ctx.AttendanceRecords.AddRange(
            new AttendanceRecord { EmployeeId = emp.Id, AttendDate = new DateTime(Year, 1, 31), WorkHours = 8m },  // 上月
            new AttendanceRecord { EmployeeId = emp.Id, AttendDate = new DateTime(Year, 3, 1), WorkHours = 8m });  // 下月
        await ctx.SaveChangesAsync();

        var dto = await CreateService(ctx).GetMonthAsync(Year, Month, null);

        dto.Employees.Single().AttendanceDays.Should().Be(0);
    }

    // ========== GetMonthAsync：关键字过滤 ==========

    [Fact]
    public async Task GetMonthAsync_关键字过滤工号或姓名()
    {
        var ctx = CreateDbContext();
        await SeedEmployeeAsync(ctx, "E100", "王五");
        await SeedEmployeeAsync(ctx, "E200", "赵六");

        var byCode = await CreateService(ctx).GetMonthAsync(Year, Month, "E100");
        byCode.Employees.Select(r => r.EmployeeCode).Should().Equal("E100");

        var byName = await CreateService(ctx).GetMonthAsync(Year, Month, "赵六");
        byName.Employees.Select(r => r.EmployeeCode).Should().Equal("E200");
    }

    // ========== SaveMonthAsync：月份/日期/小时校验 ==========

    [Theory]
    [InlineData(1999, 2)]
    [InlineData(2026, 0)]
    public async Task SaveMonthAsync_月份无效_抛业务异常(int year, int month)
    {
        var ctx = CreateDbContext();
        var act = async () => await CreateService(ctx).SaveMonthAsync(new SaveAttendanceDto
        {
            Year = year, Month = month
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage("*月份参数无效*");
    }

    [Fact]
    public async Task SaveMonthAsync_日期超出当月_抛业务异常()
    {
        var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx, "E001", "张三");
        var act = async () => await CreateService(ctx).SaveMonthAsync(new SaveAttendanceDto
        {
            Year = Year, Month = Month,
            Entries = { new AttendanceEntryDto { EmployeeId = emp.Id, Day = 29, WorkHours = 8m } }
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage($"*日期 {Year}-{Month}-29 超出当月范围*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24.5)]
    public async Task SaveMonthAsync_出勤小时越界_抛业务异常(double badHours)
    {
        var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx, "E001", "张三");
        var act = async () => await CreateService(ctx).SaveMonthAsync(new SaveAttendanceDto
        {
            Year = Year, Month = Month,
            Entries = { new AttendanceEntryDto { EmployeeId = emp.Id, Day = 1, WorkHours = (decimal)badHours } }
        });
        await act.Should().ThrowAsync<BusinessException>().WithMessage($"*{emp.Id} 出勤小时*必须在 0~24 之间*");
    }

    // ========== SaveMonthAsync：新增 / 更新 / 清空 ==========

    [Fact]
    public async Task SaveMonthAsync_有值新增记录_落库可读()
    {
        var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx, "E001", "张三");

        await CreateService(ctx).SaveMonthAsync(new SaveAttendanceDto
        {
            Year = Year, Month = Month,
            Entries =
            {
                new AttendanceEntryDto { EmployeeId = emp.Id, Day = 3, WorkHours = 8m },
                new AttendanceEntryDto { EmployeeId = emp.Id, Day = 4, WorkHours = 4.5m }
            }
        });

        var dto = await CreateService(ctx).GetMonthAsync(Year, Month, null);
        var row = dto.Employees.Single();
        row.DayHours[3].Should().Be(8m);
        row.DayHours[4].Should().Be(4.5m);
        row.AttendanceDays.Should().Be(2);
        row.TotalHours.Should().Be(12.5m);
    }

    [Fact]
    public async Task SaveMonthAsync_同员工同日期覆盖更新()
    {
        var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx, "E001", "张三");
        await SeedRecordAsync(ctx, emp.Id, 10, 4m);
        var svc = CreateService(ctx);

        await svc.SaveMonthAsync(new SaveAttendanceDto
        {
            Year = Year, Month = Month,
            Entries = { new AttendanceEntryDto { EmployeeId = emp.Id, Day = 10, WorkHours = 8m } }
        });

        var row = (await svc.GetMonthAsync(Year, Month, null)).Employees.Single();
        row.DayHours[10].Should().Be(8m);
        row.AttendanceDays.Should().Be(1); // 仍 1 天而非 2 天
        row.TotalHours.Should().Be(8m);
        ctx.AttendanceRecords.Count().Should().Be(1); // 未新增重复行
    }

    [Fact]
    public async Task SaveMonthAsync_零或空清空当日记录()
    {
        var ctx = CreateDbContext();
        var emp = await SeedEmployeeAsync(ctx, "E001", "张三");
        await SeedRecordAsync(ctx, emp.Id, 15, 8m);
        var svc = CreateService(ctx);

        // 先清成 0（走删除分支）
        await svc.SaveMonthAsync(new SaveAttendanceDto
        {
            Year = Year, Month = Month,
            Entries = { new AttendanceEntryDto { EmployeeId = emp.Id, Day = 15, WorkHours = 0m } }
        });
        var afterClear = await svc.GetMonthAsync(Year, Month, null);
        afterClear.Employees.Single().AttendanceDays.Should().Be(0);
        ctx.AttendanceRecords.Should().BeEmpty();

        // null 同样删除但无既有记录时不报错
        await svc.SaveMonthAsync(new SaveAttendanceDto
        {
            Year = Year, Month = Month,
            Entries = { new AttendanceEntryDto { EmployeeId = emp.Id, Day = 20, WorkHours = null } }
        });
        ctx.AttendanceRecords.Should().BeEmpty();
    }
}
