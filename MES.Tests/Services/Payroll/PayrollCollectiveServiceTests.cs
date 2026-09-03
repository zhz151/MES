using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Payroll;
using MES.Services.Payroll;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 集体计件月结服务测试（2026-09-03 集体=岗位 × 月度评分 × 月结快照）：
/// 同岗位按 出勤×分值 权重分配池；混编行（集体+个人计件）互不侵蚀；同岗位非集体者不计入；
/// 缺评分/无出勤 → w=0 得 0；评分保存/读取与 1–10 越界拦截；月结保存 upsert 快照冻结；历史快照员工补集显示。
/// </summary>
public class PayrollCollectiveServiceTests : TestBase
{
    // ==================== 种子 Helper ====================

    private static async Task<Employee> SeedEmpAsync(AppDbContext ctx, string code, string name,
        SalaryMode mode, string? position = null, bool active = true)
    {
        var e = new Employee
        {
            Code = code,
            Name = name,
            SalaryMode = mode,
            Position = position,
            IsActive = active
        };
        ctx.Employees.Add(e);
        await ctx.SaveChangesAsync();
        return e;
    }

    private static async Task SeedAttendanceAsync(AppDbContext ctx, int empId, DateTime date, decimal hours)
    {
        ctx.AttendanceRecords.Add(new AttendanceRecord { EmployeeId = empId, AttendDate = date, WorkHours = hours });
        await ctx.SaveChangesAsync();
    }

    private static async Task<ProductionBatch> SeedBatchAsync(AppDbContext ctx,
        string spec = "60*5", string plantGrade = "304")
    {
        var batch = new ProductionBatch
        {
            BatchNo = "BATCH-COLL-" + Guid.NewGuid().ToString("N")[..8],
            MaterialName = "不锈钢管",
            PlantGrade = plantGrade,
            Specification = spec,
            Status = BatchStatus.InProgress,
            ProductionType = "Internal",
            ManufacturingItem = "OrderFinished",
            WorkOrderNo = "WO-1",
            SalesOrderNo = "SO-1",
            ProductionMainNo = "M-1",
            OrderItemIds = "1",
            Salesman = "张三",
            SettlementMethod = "Weighing",
            StandardCode = "GB/T 14976",
            DeliveryState = "Hard",
            LengthStatus = "NonFixed",
            TechnicalRequirements = "无",
            SignDate = DateTime.Today,
            DeliveryDate = DateTime.Today.AddMonths(1),
            TotalQuantity = 100,
            TotalMeters = 1000m,
            TotalWeight = 5000m,
            TotalItemCount = 1
        };
        ctx.ProductionBatches.Add(batch);
        await ctx.SaveChangesAsync();
        return batch;
    }

    private static async Task SeedProdCategoryAsync(AppDbContext ctx, string sectionKey,
        decimal basePrice, string unit)
    {
        ctx.PieceRateProductionCategories.Add(new PieceRateProductionCategory
        {
            SectionKey = sectionKey,
            BasePrice = basePrice,
            Unit = unit,
            IsActive = true,
            ConstraintKeys = new List<PieceRateProductionCategoryKey>(),
            Tiers = new List<PieceRateProductionCategoryTier>()
        });
        await ctx.SaveChangesAsync();
    }

    /// <summary>添加一条当月生产记录（写名人串 operatorText；Weight kg）</summary>
    private static async Task SeedProdRecordAsync(AppDbContext ctx, ProductionBatch batch,
        string operatorText, decimal weightKg, int day = 10)
    {
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProductionBatch = batch,
            ProcessGroupId = 1,
            ProcessName = ProcessKeys.ColdRoll60,
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = new DateTime(2024, 3, day),
            Operator = operatorText,
            ProductStatus = ProductStatuses.InProgress,
            Weight = weightKg,
            Quantity = null
        });
        await ctx.SaveChangesAsync();
    }

    private static CollectiveMemberDto MemberOf(CollectiveMonthDto month, string code)
        => month.Groups.SelectMany(g => g.Members).Single(m => m.EmployeeCode == code);

    // ==================== 岗位池 + 权重分配 ====================

    [Fact]
    public async Task GetMonthAsync_同岗位两成员_按出勤乘分值权重分配池()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollCollectiveService(ctx);

        var a = await SeedEmpAsync(ctx, "YG001", "张三", SalaryMode.PieceCollective, "AcidWashing");
        var b = await SeedEmpAsync(ctx, "YG002", "李四", SalaryMode.PieceCollective, "AcidWashing");
        var batch = await SeedBatchAsync(ctx);
        await SeedProdCategoryAsync(ctx, SectionKeys.ColdRollDraw, 10m, PieceRateUnitKeys.PerTon);
        await SeedProdRecordAsync(ctx, batch, $"{OperatorNameHelper.Format("张三", "YG001")}、{OperatorNameHelper.Format("李四", "YG002")}", 2000m);

        // 出勤 × 分值：A=6h×10=60，B=6h×6=36 → Σw=96；写名 2 人头均分 → 各 10 → 池 20
        await SeedAttendanceAsync(ctx, a.Id, new DateTime(2024, 3, 1), 6m);
        await SeedAttendanceAsync(ctx, b.Id, new DateTime(2024, 3, 1), 6m);
        await svc.SaveScoresAsync(new SaveCollectiveScoresDto
        {
            Year = 2024,
            Month = 3,
            Entries =
            [
                new CollectiveScoreEntryDto { EmployeeId = a.Id, Score = 10 },
                new CollectiveScoreEntryDto { EmployeeId = b.Id, Score = 6 }
            ]
        });

        var month = await svc.GetMonthAsync(2024, 3);

        month.HasSaved.Should().BeFalse();
        month.Groups.Should().ContainSingle(g => g.Position == "AcidWashing");
        var group = month.Groups.Single(g => g.Position == "AcidWashing");
        group.PoolAmount.Should().Be(20m);         // 10 元/吨 × 2 吨 = 20，2 人头均分后 A/B 份额归池
        group.SumWeight.Should().Be(96m);          // 60 + 36
        group.Members.Should().HaveCount(2);

        var ma = MemberOf(month, "YG001");
        ma.EngineAmount.Should().Be(20m * 60m / 96m);  // 12.5
        var mb = MemberOf(month, "YG002");
        mb.EngineAmount.Should().Be(20m * 36m / 96m);  // 7.5
        group.Members.Sum(m => m.EngineAmount).Should().Be(20m);
    }

    [Fact]
    public async Task GetMonthAsync_混编行集体加个人计件_池只收集体份_个人日结不受影响()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollCollectiveService(ctx);

        var a = await SeedEmpAsync(ctx, "YG001", "张三", SalaryMode.PieceCollective, "AcidWashing");
        var x = await SeedEmpAsync(ctx, "YG100", "钱十", SalaryMode.PieceIndividual);
        var batch = await SeedBatchAsync(ctx);
        await SeedProdCategoryAsync(ctx, SectionKeys.ColdRollDraw, 10m, PieceRateUnitKeys.PerTon);
        // 2 人共干一批 2000kg → 总额 20，2 人头均分 → 各份 10
        await SeedProdRecordAsync(ctx, batch, $"{OperatorNameHelper.Format("张三", "YG001")}、{OperatorNameHelper.Format("钱十", "YG100")}", 2000m);
        await SeedAttendanceAsync(ctx, a.Id, new DateTime(2024, 3, 1), 6m);
        await svc.SaveScoresAsync(new SaveCollectiveScoresDto
        {
            Year = 2024,
            Month = 3,
            Entries = [new CollectiveScoreEntryDto { EmployeeId = a.Id, Score = 10 }]
        });

        // 集体月结：A 的份额（10）入 AcidWashing 池；X 非集体不入池；Σw=wA=60 → A 独占池 10
        var month = await svc.GetMonthAsync(2024, 3);
        var group = month.Groups.Single(g => g.Position == "AcidWashing");
        group.PoolAmount.Should().Be(10m);
        group.Members.Select(m => m.EmployeeCode).Should().NotContain("YG100");
        MemberOf(month, "YG001").EngineAmount.Should().Be(10m);

        // 个人日结：X 仍收到其人头份 10（集体月结不侵蚀个人通道）
        var daily = new PayrollDailyWageService(ctx);
        var dMonth = await daily.GetMonthAsync(2024, 3, PayrollWageGroup.IndividualPiece, null);
        var dx = dMonth.Employees.Single(e => e.EmployeeCode == "YG100");
        dx.DayEngineAmount[10].Should().Be(10m);
        dx.TotalEngine.Should().Be(10m);
    }

    [Fact]
    public async Task GetMonthAsync_同岗位非集体者不计入_池按人头份非全额()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollCollectiveService(ctx);

        var a = await SeedEmpAsync(ctx, "YG001", "张三", SalaryMode.PieceCollective, "AcidWashing");
        await SeedEmpAsync(ctx, "YG200", "王二", SalaryMode.Hourly, "AcidWashing"); // 同岗位计时工：非集体者不参与
        var batch = await SeedBatchAsync(ctx);
        await SeedProdCategoryAsync(ctx, SectionKeys.ColdRollDraw, 10m, PieceRateUnitKeys.PerTon);
        await SeedProdRecordAsync(ctx, batch, $"{OperatorNameHelper.Format("张三", "YG001")}、{OperatorNameHelper.Format("王二", "YG200")}", 2000m);
        await SeedAttendanceAsync(ctx, a.Id, new DateTime(2024, 3, 1), 6m);
        await svc.SaveScoresAsync(new SaveCollectiveScoresDto
        {
            Year = 2024,
            Month = 3,
            Entries = [new CollectiveScoreEntryDto { EmployeeId = a.Id, Score = 10 }]
        });

        var month = await svc.GetMonthAsync(2024, 3);
        var group = month.Groups.Single(g => g.Position == "AcidWashing");
        group.Members.Should().ContainSingle(m => m.EmployeeCode == "YG001");
        group.Members.Select(m => m.EmployeeCode).Should().NotContain("YG200");
        // 2 人头均分 → 张三只拿总额一半 10（计时工王二的份不计发）
        group.PoolAmount.Should().Be(10m);
    }

    [Fact]
    public async Task GetMonthAsync_缺评分或无出勤_w为零得零_展示缺项()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollCollectiveService(ctx);

        var a = await SeedEmpAsync(ctx, "YG001", "张三", SalaryMode.PieceCollective, "AcidWashing");
        var b = await SeedEmpAsync(ctx, "YG002", "李四", SalaryMode.PieceCollective, "AcidWashing");
        var batch = await SeedBatchAsync(ctx);
        await SeedProdCategoryAsync(ctx, SectionKeys.ColdRollDraw, 10m, PieceRateUnitKeys.PerTon);
        await SeedProdRecordAsync(ctx, batch, $"{OperatorNameHelper.Format("张三", "YG001")}、{OperatorNameHelper.Format("李四", "YG002")}", 2000m);

        // A 有出勤但未评分；B 有评分但无出勤 → 两者 w=0，池 20 无法分配 → 引擎额 0
        await SeedAttendanceAsync(ctx, a.Id, new DateTime(2024, 3, 1), 8m);
        await svc.SaveScoresAsync(new SaveCollectiveScoresDto
        {
            Year = 2024,
            Month = 3,
            Entries = [new CollectiveScoreEntryDto { EmployeeId = b.Id, Score = 8 }]
        });

        var month = await svc.GetMonthAsync(2024, 3);
        var group = month.Groups.Single(g => g.Position == "AcidWashing");
        group.PoolAmount.Should().Be(20m);
        group.SumWeight.Should().Be(0m);

        var ma = MemberOf(month, "YG001");
        ma.Score.Should().BeNull();      // 未评分
        ma.AttendanceHours.Should().Be(8m);
        ma.Weight.Should().Be(0m);
        ma.EngineAmount.Should().Be(0m);

        var mb = MemberOf(month, "YG002");
        mb.Score.Should().Be(8m);
        mb.AttendanceHours.Should().BeNull(); // 无出勤
        mb.EngineAmount.Should().Be(0m);
    }

    // ==================== 月度评分 ====================

    [Fact]
    public async Task SaveScores_越界拦截_整月Upsert_null删除()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollCollectiveService(ctx);

        var a = await SeedEmpAsync(ctx, "YG001", "张三", SalaryMode.PieceCollective, "AcidWashing");
        var b = await SeedEmpAsync(ctx, "YG002", "李四", SalaryMode.PieceCollective, "AcidWashing");

        // 越界拦截：0(<1)、11(>10)、8.55(2 位小数) 全部拒绝
        foreach (var bad in new decimal?[] { 0m, 11m, 8.55m })
        {
            var act = async () => await svc.SaveScoresAsync(new SaveCollectiveScoresDto
            {
                Year = 2024,
                Month = 3,
                Entries = [new CollectiveScoreEntryDto { EmployeeId = a.Id, Score = bad }]
            });
            await act.Should().ThrowAsync<BusinessException>().WithMessage("*1–10*");
        }

        // 分值支持 1 位小数（如 8.5）→ 整月 upsert
        await svc.SaveScoresAsync(new SaveCollectiveScoresDto
        {
            Year = 2024,
            Month = 3,
            Entries =
            [
                new CollectiveScoreEntryDto { EmployeeId = a.Id, Score = 8.5m },
                new CollectiveScoreEntryDto { EmployeeId = b.Id, Score = null } // null → 删除（无记录则不动）
            ]
        });
        var scores = await ctx.PayrollCollectiveScores.ToListAsync();
        scores.Should().ContainSingle(s => s.EmployeeId == a.Id && s.Score == 8.5m);

        // 读取：仅返回在册集体成员 + 分值
        var read = await svc.GetScoresAsync(2024, 3);
        read.Rows.Should().ContainSingle(r => r.EmployeeCode == "YG001");
        read.Rows.Single(r => r.EmployeeCode == "YG001").Score.Should().Be(8.5m);
        read.Rows.Single(r => r.EmployeeCode == "YG002").Score.Should().BeNull();
    }

    [Fact]
    public async Task GetScores_仅集体成员_同岗位非集体者不返回()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollCollectiveService(ctx);

        await SeedEmpAsync(ctx, "YG001", "张三", SalaryMode.PieceCollective, "AcidWashing");
        await SeedEmpAsync(ctx, "YG200", "王二", SalaryMode.Hourly, "AcidWashing");

        var read = await svc.GetScoresAsync(2024, 3);
        read.Rows.Should().ContainSingle();
        read.Rows[0].EmployeeCode.Should().Be("YG001");
    }

    // ==================== 月结保存（快照冻结） ====================

    [Fact]
    public async Task SaveMonth_Upsert大于0存空删_快照冻结岗位评分出勤()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollCollectiveService(ctx);

        var a = await SeedEmpAsync(ctx, "YG001", "张三", SalaryMode.PieceCollective, "AcidWashing");
        var b = await SeedEmpAsync(ctx, "YG002", "李四", SalaryMode.PieceCollective, "AcidWashing");
        await SeedAttendanceAsync(ctx, a.Id, new DateTime(2024, 3, 1), 6m);
        await svc.SaveScoresAsync(new SaveCollectiveScoresDto
        {
            Year = 2024,
            Month = 3,
            Entries = [new CollectiveScoreEntryDto { EmployeeId = a.Id, Score = 10 }]
        });

        await svc.SaveMonthAsync(new SaveCollectiveMonthDto
        {
            Year = 2024,
            Month = 3,
            Entries =
            [
                new CollectiveMonthEntryDto { EmployeeId = a.Id, Amount = 100m },
                new CollectiveMonthEntryDto { EmployeeId = b.Id, Amount = null }
            ]
        });

        var saved = await ctx.PayrollCollectiveWageRecords.ToListAsync();
        saved.Should().ContainSingle();
        var rec = saved.Single();
        rec.EmployeeId.Should().Be(a.Id);
        rec.Amount.Should().Be(100m);
        rec.Position.Should().Be("AcidWashing");   // 岗位快照
        rec.Score.Should().Be(10m);                 // 评分快照
        rec.AttendanceHours.Should().Be(6m);        // 出勤快照

        // 第二遍：改 150 / 删除 A → 清空；B 新增 50
        await svc.SaveMonthAsync(new SaveCollectiveMonthDto
        {
            Year = 2024,
            Month = 3,
            Entries =
            [
                new CollectiveMonthEntryDto { EmployeeId = a.Id, Amount = 150m },
                new CollectiveMonthEntryDto { EmployeeId = b.Id, Amount = 50m }
            ]
        });
        var after = await ctx.PayrollCollectiveWageRecords.OrderBy(r => r.EmployeeId).ToListAsync();
        after.Should().HaveCount(2);
        after.First(r => r.EmployeeId == a.Id).Amount.Should().Be(150m);
        after.First(r => r.EmployeeId == b.Id).Amount.Should().Be(50m);
    }

    [Fact]
    public async Task GetMonth_换岗或停用后历史月仍显示_引擎不覆盖快照保留()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollCollectiveService(ctx);

        var a = await SeedEmpAsync(ctx, "YG001", "张三", SalaryMode.PieceCollective, "AcidWashing");
        await SeedAttendanceAsync(ctx, a.Id, new DateTime(2024, 3, 1), 6m);
        await svc.SaveScoresAsync(new SaveCollectiveScoresDto
        {
            Year = 2024,
            Month = 3,
            Entries = [new CollectiveScoreEntryDto { EmployeeId = a.Id, Score = 10 }]
        });

        // 先存 3 月 A=100（快照 AcidWashing）
        await svc.SaveMonthAsync(new SaveCollectiveMonthDto
        {
            Year = 2024,
            Month = 3,
            Entries = [new CollectiveMonthEntryDto { EmployeeId = a.Id, Amount = 100m }]
        });

        // 员工停用（不再是当前在册集体成员）→ 历史月仍须按快照显示可回溯
        a.IsActive = false;
        await ctx.SaveChangesAsync();

        var month = await svc.GetMonthAsync(2024, 3);
        month.HasSaved.Should().BeTrue();
        var ma = MemberOf(month, "YG001");
        ma.EngineCovered.Should().BeFalse();
        ma.SavedAmount.Should().Be(100m);
        ma.EngineAmount.Should().BeNull();
        ma.Position.Should().Be("AcidWashing");
    }

    [Fact]
    public async Task SaveMonth_员工无在册岗位_新插行岗位快照为空串不崩溃()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollCollectiveService(ctx);

        // 员工当前无岗位（历史保存的员工可能已清岗位）→ 保存不崩溃，岗位快照落空
        var e = await SeedEmpAsync(ctx, "YG099", "无名", SalaryMode.PieceCollective, null);

        await svc.SaveMonthAsync(new SaveCollectiveMonthDto
        {
            Year = 2024,
            Month = 3,
            Entries = [new CollectiveMonthEntryDto { EmployeeId = e.Id, Amount = 20m }]
        });

        var rec = await ctx.PayrollCollectiveWageRecords.SingleAsync();
        rec.Position.Should().BeEmpty();
        rec.Amount.Should().Be(20m);
    }
}
