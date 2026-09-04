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
using MES.Data.Entities.Quality;
using MES.Services.Payroll;
using MES.Tests.Tests;

namespace MES.Tests.Services;

/// <summary>
/// 每日工资服务测试（2026-09-03 非计件工资 / 个人计件工资两表）：
/// 非计件引擎（计小时=小时×时薪、计日=DailyWage×min(小时,8)/8、缺标准警告、固定月薪不纳入）；
/// 个人计件引擎（冷轧单行 PerTon + OD 档命中、成检两人合作均分、成检 PerKm 长度换算与 6000 兜底、未定价 0 元+警告）；
/// SaveMonth upsert/>0 存、空删、归口快照保留、换归口后历史月仍显示；
/// PieceRateMatchEngine 纯函数（区间档连乘、命中>1 数据违例、成检 Length 档 6000 命中）。
/// </summary>
public class PayrollDailyWageServiceTests : TestBase
{
    // ==================== 种子 Helper ====================

    private static async Task<Employee> SeedEmployeeAsync(AppDbContext ctx, string code, string name,
        SalaryMode? mode, decimal? hourly = null, decimal? daily = null)
    {
        var e = new Employee
        {
            Code = code,
            Name = name,
            SalaryMode = mode,
            HourlyWage = hourly,
            DailyWage = daily,
            IsActive = true
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

    /// <summary>生产批次种子（仅引擎读取 Specification/PlantGrade/LengthStatus，其余取测试仓库常规值）</summary>
    private static async Task<ProductionBatch> SeedBatchAsync(AppDbContext ctx,
        string spec = "60*5", string lengthStatus = "NonFixed", string plantGrade = "304")
    {
        var batch = new ProductionBatch
        {
            BatchNo = "BATCH-WG-" + Guid.NewGuid().ToString("N")[..8],
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
            LengthStatus = lengthStatus,
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

    /// <summary>
    /// 生产计件类别种子（constraintKeys 为空 = 工序/产类/阶段全选；档行于类别保存后补）。
    /// constraintKeys 可传 (约束类型, 成员 Key)，如 (PieceRateConstraintTypes.Stage, PieceRateStageKeys.InTank)
    /// 或 (PieceRateConstraintTypes.ProductStatus, ProductStatuses.RoughTube)，用于区分 In/OutTank、支/吨分价类别。
    /// </summary>
    private static async Task<PieceRateProductionCategory> SeedProdCategoryAsync(AppDbContext ctx,
        string sectionKey, decimal basePrice, string unit,
        IReadOnlyList<PieceRateProductionCategoryTier> tiers,
        IReadOnlyList<(string ConstraintType, string Key)>? constraintKeys = null)
    {
        var cat = new PieceRateProductionCategory
        {
            SectionKey = sectionKey,
            BasePrice = basePrice,
            Unit = unit,
            IsActive = true,
            ConstraintKeys = new List<PieceRateProductionCategoryKey>(),
            Tiers = new List<PieceRateProductionCategoryTier>()
        };
        if (constraintKeys != null)
            foreach (var (ct, key) in constraintKeys)
                cat.ConstraintKeys.Add(new PieceRateProductionCategoryKey { ConstraintType = ct, Key = key });
        ctx.PieceRateProductionCategories.Add(cat);
        await ctx.SaveChangesAsync();
        foreach (var t in tiers)
        {
            t.CategoryId = cat.Id;
            cat.Tiers.Add(t);
            ctx.PieceRateProductionCategoryTiers.Add(t);
        }
        await ctx.SaveChangesAsync();
        return cat;
    }

    private static PieceRateProductionCategoryTier IntervalTier(
        string dimKey, decimal? min, decimal? max, decimal ratio)
        => new()
        {
            DimensionKey = dimKey,
            MinValue = min,
            MaxValue = max,
            Ratio = ratio,
            IsActive = true
        };

    private static PieceRateFinalInspectionCategoryTier FinalIntervalTier(
        string dimKey, decimal? min, decimal? max, decimal ratio)
        => new()
        {
            DimensionKey = dimKey,
            MinValue = min,
            MaxValue = max,
            Ratio = ratio,
            IsActive = true
        };

    /// <summary>成检计件类别种子（同一成检项目仅一条启用类别，测试每库各自独立）</summary>
    private static async Task<PieceRateFinalInspectionCategory> SeedFinalCategoryAsync(AppDbContext ctx,
        string itemKey, decimal basePrice, string unit,
        IReadOnlyList<PieceRateFinalInspectionCategoryTier> tiers)
    {
        var cat = new PieceRateFinalInspectionCategory
        {
            ItemKey = itemKey,
            BasePrice = basePrice,
            Unit = unit,
            IsActive = true,
            Tiers = new List<PieceRateFinalInspectionCategoryTier>()
        };
        ctx.PieceRateFinalInspectionCategories.Add(cat);
        await ctx.SaveChangesAsync();
        foreach (var t in tiers)
        {
            t.CategoryId = cat.Id;
            cat.Tiers.Add(t);
            ctx.PieceRateFinalInspectionCategoryTiers.Add(t);
        }
        await ctx.SaveChangesAsync();
        return cat;
    }

    private static DailyWageEmployeeRowDto RowOf(DailyWageMonthDto month, string code)
        => month.Employees.Single(e => e.EmployeeCode == code);

    // ==================== 非计件引擎 ====================

    [Fact]
    public async Task GetMonthAsync_非计件_计小时与计日折算_缺标准警告_固定月薪不纳入()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollDailyWageService(ctx);

        var hourly = await SeedEmployeeAsync(ctx, "YG101", "周文", SalaryMode.Hourly, hourly: 20m);
        var daily = await SeedEmployeeAsync(ctx, "YG102", "吴芳", SalaryMode.Daily, daily: 100m);
        var noStandard = await SeedEmployeeAsync(ctx, "YG103", "郑新", SalaryMode.Hourly); // 无时薪 → 缺标准警告
        await SeedEmployeeAsync(ctx, "YG104", "王建", SalaryMode.Fixed);                  // 固定月薪 → 不进非计件表

        await SeedAttendanceAsync(ctx, hourly.Id, new DateTime(2024, 2, 5), 6m);   // 6×20=120
        await SeedAttendanceAsync(ctx, daily.Id, new DateTime(2024, 2, 6), 4m);    // 100×4/8=50（半天）
        await SeedAttendanceAsync(ctx, daily.Id, new DateTime(2024, 2, 7), 8m);    // 100×8/8=100
        await SeedAttendanceAsync(ctx, daily.Id, new DateTime(2024, 2, 10), 12m);  // 100×min(12,8)/8=100
        await SeedAttendanceAsync(ctx, noStandard.Id, new DateTime(2024, 2, 8), 8m);

        var month = await svc.GetMonthAsync(2024, 2, PayrollWageGroup.NonPiece, null);

        month.HasSaved.Should().BeFalse();
        month.Employees.Select(e => e.EmployeeCode).Should().Contain(new[] { "YG101", "YG102", "YG103" });
        month.Employees.Select(e => e.EmployeeCode).Should().NotContain("YG104"); // 固定月薪不纳入

        var h = RowOf(month, "YG101");
        h.EngineCovered.Should().BeTrue();
        h.DayEngineAmount[5].Should().Be(120m);
        h.DayEngineAmount[6].Should().BeNull();
        h.TotalEngine.Should().Be(120m);

        var d = RowOf(month, "YG102");
        d.DayEngineAmount[6].Should().Be(50m);   // 半天=半日薪
        d.DayEngineAmount[7].Should().Be(100m);  // 整日
        d.DayEngineAmount[10].Should().Be(100m); // 超 8h 按 1 日
        d.TotalEngine.Should().Be(250m);

        RowOf(month, "YG103").DayEngineAmount[8].Should().BeNull(); // 无标准不自动带出
        month.Warnings.Should().Contain(w => w.Contains("郑新"));
    }

    // ==================== 个人计件引擎（生产冷轧） ====================

    [Fact]
    public async Task GetMonthAsync_个人计件_冷轧单员工单行_OD档命中PerTon金额()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollDailyWageService(ctx);

        var zhang = await SeedEmployeeAsync(ctx, "YG001", "张三", SalaryMode.PieceIndividual);
        var batch = await SeedBatchAsync(ctx, spec: "60*5");
        await SeedProdCategoryAsync(ctx, SectionKeys.ColdRollDraw, 50m, PieceRateUnitKeys.PerTon,
            new List<PieceRateProductionCategoryTier>
            {
                IntervalTier(PieceRateDimensionKeys.OuterDiameter, 54m, null, 1.2m)
            });

        // ManufacturingSpec 留空 → 引擎回退批次 Specification = "60*5" → OD=60、WT=5
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProductionBatch = batch,
            ProcessGroupId = 1,
            ProcessName = ProcessKeys.ColdRoll60,
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = new DateTime(2024, 3, 10),
            Operator = OperatorNameHelper.Format("张三", "YG001"),
            ProductStatus = ProductStatuses.InProgress,
            Weight = 1000m,
            Quantity = null
        });
        await ctx.SaveChangesAsync();

        var month = await svc.GetMonthAsync(2024, 3, PayrollWageGroup.IndividualPiece, null);

        var row = RowOf(month, "YG001");
        row.EngineCovered.Should().BeTrue();
        // 命中 OD>54 档 ×1.2 → 单价 50×1.2=60 元/吨；1000kg=1吨 → 60 元
        row.DayEngineAmount[10].Should().Be(60m);
        row.DayEngineAmount[9].Should().BeNull();
        row.TotalEngine.Should().Be(60m);
        month.HasSaved.Should().BeFalse();
    }

    // ==================== 个人计件引擎（成检均分 / PerKm） ====================

    [Fact]
    public async Task GetMonthAsync_个人计件_成检两人合作行均分()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollDailyWageService(ctx);

        var li = await SeedEmployeeAsync(ctx, "YG002", "李四", SalaryMode.PieceIndividual);
        var wang = await SeedEmployeeAsync(ctx, "YG003", "王五", SalaryMode.PieceIndividual);
        var batch = await SeedBatchAsync(ctx, spec: "60*5", lengthStatus: "Range");
        await SeedFinalCategoryAsync(ctx, nameof(InspectionItem.Ultrasonic), 2m, PieceRateUnitKeys.PerPiece,
            new List<PieceRateFinalInspectionCategoryTier>());

        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.Ultrasonic,
            InspectionDate = new DateTime(2024, 3, 15),
            BatchNo = batch.BatchNo,
            ProductionBatchId = batch.Id,
            ProductionBatch = batch,
            Operator = $"{OperatorNameHelper.Format("李四", "YG002")}、{OperatorNameHelper.Format("王五", "YG003")}",
            Quantity = 60,
            Weight = 600
        });
        await ctx.SaveChangesAsync();

        var month = await svc.GetMonthAsync(2024, 3, PayrollWageGroup.IndividualPiece, null);

        var l = RowOf(month, "YG002");
        var w = RowOf(month, "YG003");
        // 2 元/支 × 60 支 = 120 → 两人均分各 60
        l.DayEngineAmount[15].Should().Be(60m);
        w.DayEngineAmount[15].Should().Be(60m);
        l.TotalEngine.Should().Be(60m);
        w.TotalEngine.Should().Be(60m);
    }

    [Fact]
    public async Task GetMonthAsync_个人计件_成检PerKm_范围尺6000兜底长度档命中()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollDailyWageService(ctx);

        var zhao = await SeedEmployeeAsync(ctx, "YG004", "赵六", SalaryMode.PieceIndividual);
        var batch = await SeedBatchAsync(ctx, spec: "60*5", lengthStatus: "Range"); // 范围尺 → 长度按 6000 兜底
        await SeedFinalCategoryAsync(ctx, nameof(InspectionItem.PMIInspection), 3m, PieceRateUnitKeys.PerKm,
            new List<PieceRateFinalInspectionCategoryTier>
            {
                FinalIntervalTier(PieceRateInspectionDimensionKeys.Length, 5001m, 7500m, 1.5m)
            });

        ctx.FinalInspections.Add(new FinalInspection
        {
            InspectionItem = InspectionItem.PMIInspection,
            InspectionDate = new DateTime(2024, 3, 18),
            BatchNo = batch.BatchNo,
            ProductionBatchId = batch.Id,
            ProductionBatch = batch,
            Operator = OperatorNameHelper.Format("赵六", "YG004"),
            Quantity = 500,
            Weight = 900
        });
        await ctx.SaveChangesAsync();

        var month = await svc.GetMonthAsync(2024, 3, PayrollWageGroup.IndividualPiece, null);

        var row = RowOf(month, "YG004");
        // 6000mm 落 5001-7500 档 ×1.5 → 单价 3×1.5=4.5 元/千米；
        // 500 支 × 6000mm/1e6 = 3 千米 × 4.5 = 13.5 元
        row.DayEngineAmount[18].Should().Be(13.5m);
        row.TotalEngine.Should().Be(13.5m);
    }

    [Fact]
    public async Task GetMonthAsync_个人计件_未定价行按0元_警告计数()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollDailyWageService(ctx);

        var sun = await SeedEmployeeAsync(ctx, "YG005", "孙七", SalaryMode.PieceIndividual);
        var batch = await SeedBatchAsync(ctx, spec: "60*5");
        // 不注册任何计件类别 → 该行未定价（0 元 + 警告计数）
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProductionBatch = batch,
            ProcessGroupId = 1,
            ProcessName = ProcessKeys.ColdRoll60,
            SectionName = SectionKeys.ColdRollDraw,
            ManufacturingSpec = "60*5",
            ExecDate = new DateTime(2024, 3, 12),
            Operator = OperatorNameHelper.Format("孙七", "YG005"),
            ProductStatus = ProductStatuses.InProgress,
            Weight = 500m
        });
        await ctx.SaveChangesAsync();

        var month = await svc.GetMonthAsync(2024, 3, PayrollWageGroup.IndividualPiece, null);

        var row = RowOf(month, "YG005");
        row.DayEngineAmount[12].Should().BeNull();
        month.Warnings.Should().Contain(w => w.Contains("1 行产量/检验记录到数量但未匹配到计件单价"));
    }

    // ==================== SaveMonth（落库快照） ====================

    [Fact]
    public async Task SaveMonthAsync_Upsert大于0存空删_快照为当前归口()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollDailyWageService(ctx);

        var emp = await SeedEmployeeAsync(ctx, "YG201", "钱八", SalaryMode.Hourly, hourly: 20m);

        await svc.SaveMonthAsync(new SaveDailyWageDto
        {
            Year = 2024,
            Month = 1,
            Group = PayrollWageGroup.NonPiece,
            Entries =
            [
                new DailyWageEntryDto { EmployeeId = emp.Id, Day = 1, Amount = 100m },
                new DailyWageEntryDto { EmployeeId = emp.Id, Day = 2, Amount = null },
                new DailyWageEntryDto { EmployeeId = emp.Id, Day = 3, Amount = 200m }
            ]
        });

        var saved = await ctx.PayrollDailyWageRecords.ToListAsync();
        saved.Should().HaveCount(2);
        saved.Should().Contain(r => r.WageDate.Day == 1 && r.Amount == 100m && r.SalaryMode == nameof(SalaryMode.Hourly));
        saved.Should().Contain(r => r.WageDate.Day == 3 && r.Amount == 200m && r.SalaryMode == nameof(SalaryMode.Hourly));

        // 第二遍：day1 改 150、day3 清空删除、day5 新增 50
        await svc.SaveMonthAsync(new SaveDailyWageDto
        {
            Year = 2024,
            Month = 1,
            Group = PayrollWageGroup.NonPiece,
            Entries =
            [
                new DailyWageEntryDto { EmployeeId = emp.Id, Day = 1, Amount = 150m },
                new DailyWageEntryDto { EmployeeId = emp.Id, Day = 3, Amount = null },
                new DailyWageEntryDto { EmployeeId = emp.Id, Day = 5, Amount = 50m }
            ]
        });

        var after = await ctx.PayrollDailyWageRecords.ToListAsync();
        after.Should().HaveCount(2);
        after.Should().Contain(r => r.WageDate.Day == 1 && r.Amount == 150m);
        after.Should().Contain(r => r.WageDate.Day == 5 && r.Amount == 50m);
        after.Should().OnlyContain(r => r.SalaryMode == nameof(SalaryMode.Hourly));
    }

    [Fact]
    public async Task SaveMonth_换归口后历史月仍显示_快照保留可续存()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollDailyWageService(ctx);

        var emp = await SeedEmployeeAsync(ctx, "YG202", "冯九", SalaryMode.Hourly, hourly: 20m);

        // 1 月先存 day5=80（快照 Hourly）
        await svc.SaveMonthAsync(new SaveDailyWageDto
        {
            Year = 2024,
            Month = 1,
            Group = PayrollWageGroup.NonPiece,
            Entries = [new DailyWageEntryDto { EmployeeId = emp.Id, Day = 5, Amount = 80m }]
        });

        // 员工换归口为 Fixed（历史月仍须按快照显示可回溯）
        emp.SalaryMode = SalaryMode.Fixed;
        await ctx.SaveChangesAsync();

        var month = await svc.GetMonthAsync(2024, 1, PayrollWageGroup.NonPiece, null);
        var row = RowOf(month, "YG202");
        row.Should().NotBeNull();
        row.EngineCovered.Should().BeFalse();   // 非当前归口启用员工 → 引擎不覆盖
        row.DaySavedAmount[5].Should().Be(80m); // 历史快照仍显示
        row.DayEngineAmount[5].Should().BeNull();
        month.HasSaved.Should().BeTrue();

        // 换归口后同月续存 day6=90 → 沿用历史快照 Hourly 归口
        await svc.SaveMonthAsync(new SaveDailyWageDto
        {
            Year = 2024,
            Month = 1,
            Group = PayrollWageGroup.NonPiece,
            Entries = [new DailyWageEntryDto { EmployeeId = emp.Id, Day = 6, Amount = 90m }]
        });

        var records = await ctx.PayrollDailyWageRecords.ToListAsync();
        records.Should().HaveCount(2);
        records.Should().OnlyContain(r => r.SalaryMode == nameof(SalaryMode.Hourly));
    }

    // ==================== PieceRateMatchEngine 纯函数 ====================

    [Fact]
    public void MatchProduction_区间档系数连乘_与现服务口径一致()
    {
        var cat = new PieceRateProductionCategory
        {
            SectionKey = SectionKeys.ColdRollDraw,
            BasePrice = 35m,
            Unit = PieceRateUnitKeys.PerTon,
            IsActive = true,
            ConstraintKeys = new List<PieceRateProductionCategoryKey>(),
            Tiers =
            [
                new PieceRateProductionCategoryTier
                {
                    DimensionKey = PieceRateDimensionKeys.OuterDiameter,
                    MinValue = 54m,
                    MaxValue = null,
                    Ratio = 1.2m,
                    IsActive = true
                },
                new PieceRateProductionCategoryTier
                {
                    DimensionKey = PieceRateDimensionKeys.WallThickness,
                    MinValue = 4.3m,
                    MaxValue = null,
                    Ratio = 1.15m,
                    IsActive = true
                }
            ]
        };

        var hit = PieceRateMatchEngine.MatchProduction(new[] { cat }, new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.ColdRollDraw,
            ProcessName = ProcessKeys.ColdRoll60,
            ProductStatus = ProductStatuses.InProgress,
            Stage = null,
            OuterDiameter = 60m,
            WallThickness = 5m
        });

        hit.Should().NotBeNull();
        hit!.TotalRatio.Should().Be(1.38m);   // 1.2 × 1.15
        hit.UnitPrice.Should().Be(48.3m);     // 35 × 1.38
        hit.Unit.Should().Be(PieceRateUnitKeys.PerTon);
    }

    [Fact]
    public void MatchProduction_命中多个启用类别_抛数据违例()
    {
        var dupA = new PieceRateProductionCategory
        {
            SectionKey = SectionKeys.ColdRollDraw,
            BasePrice = 10m,
            Unit = PieceRateUnitKeys.PerTon,
            IsActive = true,
            ConstraintKeys = new List<PieceRateProductionCategoryKey>(),
            Tiers = new List<PieceRateProductionCategoryTier>()
        };
        var dupB = new PieceRateProductionCategory
        {
            SectionKey = SectionKeys.ColdRollDraw,
            BasePrice = 20m,
            Unit = PieceRateUnitKeys.PerTon,
            IsActive = true,
            ConstraintKeys = new List<PieceRateProductionCategoryKey>(),
            Tiers = new List<PieceRateProductionCategoryTier>()
        };

        var act = () => PieceRateMatchEngine.MatchProduction(
            new[] { dupA, dupB },
            new PieceRateProductionMatchRequest
            {
                SectionName = SectionKeys.ColdRollDraw,
                ProcessName = ProcessKeys.ColdRoll60
            });
        act.Should().Throw<BusinessException>().Which.Message.Should().Contain("命中多个启用类别");
    }

    [Fact]
    public void MatchProduction_ColdDrawType_备注关键词包含命中()
    {
        var cat = new PieceRateProductionCategory
        {
            SectionKey = SectionKeys.ColdRollDraw,
            BasePrice = 100m,
            Unit = PieceRateUnitKeys.PerTon,
            IsActive = true,
            ConstraintKeys =
            [
                new PieceRateProductionCategoryKey
                {
                    ConstraintType = PieceRateConstraintTypes.Process,
                    Key = ProcessKeys.ColdDraw
                }
            ],
            Tiers =
            [
                new PieceRateProductionCategoryTier
                {
                    DimensionKey = PieceRateDimensionKeys.ColdDrawType,
                    MatchValue = "减壁",
                    Ratio = 2m,
                    IsActive = true
                },
                new PieceRateProductionCategoryTier
                {
                    DimensionKey = PieceRateDimensionKeys.ColdDrawType,
                    MatchValue = "扩孔",
                    Ratio = 1.6m,
                    IsActive = true
                }
            ]
        };
        var cats = new[] { cat };

        // 备注精确含关键词 → 命中系数
        var hit = PieceRateMatchEngine.MatchProduction(cats, new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.ColdRollDraw,
            ProcessName = ProcessKeys.ColdDraw,
            Remark = "减壁"
        });
        hit.Should().NotBeNull();
        hit!.TotalRatio.Should().Be(2m);
        hit.UnitPrice.Should().Be(200m);   // 100 × 2

        // 备注含前后缀仍命中（Contains）
        var hitPrefix = PieceRateMatchEngine.MatchProduction(cats, new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.ColdRollDraw,
            ProcessName = ProcessKeys.ColdDraw,
            Remark = "拉拔减壁处理"
        });
        hitPrefix!.TotalRatio.Should().Be(2m);

        var hitKk = PieceRateMatchEngine.MatchProduction(cats, new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.ColdRollDraw,
            ProcessName = ProcessKeys.ColdDraw,
            Remark = "扩孔道次"
        });
        hitKk!.TotalRatio.Should().Be(1.6m);

        // 备注空/不含任何关键词 → 该维系数 1
        var none = PieceRateMatchEngine.MatchProduction(cats, new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.ColdRollDraw,
            ProcessName = ProcessKeys.ColdDraw,
            Remark = "空拉"
        });
        none!.TotalRatio.Should().Be(1m);

        var noRemark = PieceRateMatchEngine.MatchProduction(cats, new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.ColdRollDraw,
            ProcessName = ProcessKeys.ColdDraw
        });
        noRemark!.TotalRatio.Should().Be(1m);
    }

    [Fact]
    public void MatchProduction_ColdDrawType_多词命中取最长关键词()
    {
        var cat = new PieceRateProductionCategory
        {
            SectionKey = SectionKeys.ColdRollDraw,
            BasePrice = 100m,
            Unit = PieceRateUnitKeys.PerTon,
            IsActive = true,
            ConstraintKeys = new List<PieceRateProductionCategoryKey>(),
            Tiers =
            [
                new PieceRateProductionCategoryTier
                {
                    DimensionKey = PieceRateDimensionKeys.ColdDrawType,
                    MatchValue = "减壁",
                    Ratio = 2m,
                    IsActive = true
                },
                new PieceRateProductionCategoryTier
                {
                    DimensionKey = PieceRateDimensionKeys.ColdDrawType,
                    MatchValue = "壁",
                    Ratio = 1.15m,
                    IsActive = true
                }
            ]
        };

        // 备注同含“减壁”与“壁” → 取最长“减壁”×2（而非×1.15）
        var hit = PieceRateMatchEngine.MatchProduction(new[] { cat }, new PieceRateProductionMatchRequest
        {
            SectionName = SectionKeys.ColdRollDraw,
            Remark = "减壁处理"
        });
        hit.Should().NotBeNull();
        hit!.TotalRatio.Should().Be(2m);
    }

    [Fact]
    public void MatchFinalInspection_长度6000落入长度档_单价连乘()
    {
        var cat = new PieceRateFinalInspectionCategory
        {
            ItemKey = nameof(InspectionItem.PMIInspection),
            BasePrice = 3m,
            Unit = PieceRateUnitKeys.PerKm,
            IsActive = true,
            Tiers =
            [
                new PieceRateFinalInspectionCategoryTier
                {
                    DimensionKey = PieceRateInspectionDimensionKeys.Length,
                    MinValue = 5001m,
                    MaxValue = 7500m,
                    Ratio = 1.5m,
                    IsActive = true
                }
            ]
        };

        var hit = PieceRateMatchEngine.MatchFinalInspection(
            new[] { cat },
            new PieceRateFinalInspectionMatchRequest
            {
                ItemKey = nameof(InspectionItem.PMIInspection),
                LengthStatus = nameof(LengthStatus.Range),
                Length = 6000m,
                InspectionCount = 500
            });

        hit.Should().NotBeNull();
        hit!.UnitPrice.Should().Be(4.5m);  // 3 × 1.5
        hit.Unit.Should().Be(PieceRateUnitKeys.PerKm);
    }

    // ==================== 归属口径（写名总人头切份，非按件者=0） ====================

    [Fact]
    public async Task GetMonthAsync_个人计件_混编计件与非计件人头_计件者按人头份非全额()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollDailyWageService(ctx);

        var zhang = await SeedEmployeeAsync(ctx, "YG001", "张三", SalaryMode.PieceIndividual);
        await SeedEmployeeAsync(ctx, "YG100", "钱十", SalaryMode.Hourly); // 非按件者：计时工不参与发放
        var batch = await SeedBatchAsync(ctx, spec: "60*5");
        await SeedProdCategoryAsync(ctx, SectionKeys.ColdRollDraw, 50m, PieceRateUnitKeys.PerTon,
            new List<PieceRateProductionCategoryTier>());

        // 2 人共干一批（1 个人计件 + 1 计时工）→ 计件者只拿总额的 1/2，计时工 0（按用户口径，非全额）
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProductionBatch = batch,
            ProcessGroupId = 1,
            ProcessName = ProcessKeys.ColdRoll60,
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = new DateTime(2024, 3, 10),
            Operator = $"{OperatorNameHelper.Format("张三", "YG001")}、{OperatorNameHelper.Format("钱十", "YG100")}",
            ProductStatus = ProductStatuses.InProgress,
            Weight = 1000m,
            Quantity = null
        });
        await ctx.SaveChangesAsync();

        var month = await svc.GetMonthAsync(2024, 3, PayrollWageGroup.IndividualPiece, null);

        // 总额 = 50 元/吨 × 1 吨 = 50；写名 2 人头 → 每人份 25；计时工不在发放名单 → 只发张三 25
        var row = RowOf(month, "YG001");
        row.DayEngineAmount[10].Should().Be(25m);
        row.TotalEngine.Should().Be(25m);
        month.Employees.Select(e => e.EmployeeCode).Should().NotContain("YG100");
    }

    [Fact]
    public async Task GetMonthAsync_个人计件_酸洗入缸源_StageInTank分价命中()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollDailyWageService(ctx);

        var zhang = await SeedEmployeeAsync(ctx, "YG001", "张三", SalaryMode.PieceIndividual);
        var batch = await SeedBatchAsync(ctx, spec: "60*5");
        // Degrease 两段分价类别（须以 Stage 约束区分，否则互撞触发命中>1 违例）
        await SeedProdCategoryAsync(ctx, SectionKeys.Degrease, 7m, PieceRateUnitKeys.PerTon,
            new List<PieceRateProductionCategoryTier>(),
            new[] { (PieceRateConstraintTypes.Stage, PieceRateStageKeys.InTank) });
        await SeedProdCategoryAsync(ctx, SectionKeys.Degrease, 18m, PieceRateUnitKeys.PerTon,
            new List<PieceRateProductionCategoryTier>(),
            new[] { (PieceRateConstraintTypes.Stage, PieceRateStageKeys.OutTank) });

        ctx.PicklingInRecords.Add(new PicklingInRecord
        {
            ProductionBatchId = batch.Id,
            ProductionBatch = batch,
            ProcessGroupId = 1,
            ProcessName = ProcessKeys.ColdRoll60,
            SectionName = SectionKeys.Degrease,
            InDate = new DateTime(2024, 3, 12),
            Operator = OperatorNameHelper.Format("张三", "YG001"),
            ProductStatus = ProductStatuses.InProgress,
            ManufacturingSpec = "60*5",
            Weight = 1000m
        });
        await ctx.SaveChangesAsync();

        var month = await svc.GetMonthAsync(2024, 3, PayrollWageGroup.IndividualPiece, null);

        // 入缸行 Stage=InTank → 命中 7 元/吨类（非 18 元/吨 OutTank 类）；1000kg=1吨 → 7 元
        var row = RowOf(month, "YG001");
        row.DayEngineAmount[12].Should().Be(7m);
        row.TotalEngine.Should().Be(7m);
    }

    [Fact]
    public async Task GetMonthAsync_个人计件_酸洗完工源_StageOutTank分价命中()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollDailyWageService(ctx);

        var zhang = await SeedEmployeeAsync(ctx, "YG001", "张三", SalaryMode.PieceIndividual);
        var batch = await SeedBatchAsync(ctx, spec: "60*5");
        // 仅 OutTank 类别：完工行若引擎漏传 Stage=OutTank（按 null）则类别含 OutTank 约束不命中 → 0 元暴露
        await SeedProdCategoryAsync(ctx, SectionKeys.Degrease, 18m, PieceRateUnitKeys.PerTon,
            new List<PieceRateProductionCategoryTier>(),
            new[] { (PieceRateConstraintTypes.Stage, PieceRateStageKeys.OutTank) });

        var inn = new PicklingInRecord
        {
            ProductionBatchId = batch.Id,
            ProductionBatch = batch,
            ProcessGroupId = 1,
            ProcessName = ProcessKeys.ColdRoll60,
            SectionName = SectionKeys.Degrease,
            InDate = new DateTime(2024, 3, 11),
            Operator = OperatorNameHelper.Format("张三", "YG001"),
            ProductStatus = ProductStatuses.InProgress,
            ManufacturingSpec = "60*5",
            Weight = 2000m
        };
        ctx.PicklingInRecords.Add(inn);
        await ctx.SaveChangesAsync();

        ctx.PicklingOutRecords.Add(new PicklingOutRecord
        {
            PicklingInRecordId = inn.Id,
            SectionName = SectionKeys.Degrease,
            ProcessName = ProcessKeys.ColdRoll60,
            CompleteDate = new DateTime(2024, 3, 13),
            Operator = OperatorNameHelper.Format("张三", "YG001"),
            ProductStatus = ProductStatuses.InProgress,
            ManufacturingSpec = "60*5",
            Weight = 2000m
        });
        await ctx.SaveChangesAsync();

        var month = await svc.GetMonthAsync(2024, 3, PayrollWageGroup.IndividualPiece, null);

        // 完工行 Stage=OutTank → 命中 18 元/吨类；2000kg=2吨 → 36 元（入缸行无匹配类 → 不计）
        var row = RowOf(month, "YG001");
        row.DayEngineAmount[13].Should().Be(36m);
        row.TotalEngine.Should().Be(36m);
    }

    [Fact]
    public async Task GetMonthAsync_个人计件_过程检验源_Inspection工段_支单价与吨单价分流()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollDailyWageService(ctx);

        var zhang = await SeedEmployeeAsync(ctx, "YG001", "张三", SalaryMode.PieceIndividual);
        var batch = await SeedBatchAsync(ctx, spec: "60*5");
        // 过程检验复用生产计件类别 · Inspection 工段：RoughTube→支单价、InProgress→吨单价（ProductStatus 约束区分）
        await SeedProdCategoryAsync(ctx, SectionKeys.Inspection, 0.25m, PieceRateUnitKeys.PerPiece,
            new List<PieceRateProductionCategoryTier>(),
            new[] { (PieceRateConstraintTypes.ProductStatus, ProductStatuses.RoughTube) });
        await SeedProdCategoryAsync(ctx, SectionKeys.Inspection, 20m, PieceRateUnitKeys.PerTon,
            new List<PieceRateProductionCategoryTier>(),
            new[] { (PieceRateConstraintTypes.ProductStatus, ProductStatuses.InProgress) });

        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = batch.Id,
            ProductionBatch = batch,
            ProcessGroupId = 1,
            ProcessName = ProcessKeys.ColdRoll50,
            SectionName = SectionKeys.Inspection,
            InspectionDate = new DateTime(2024, 3, 5),
            Inspector = OperatorNameHelper.Format("张三", "YG001"),
            ProductStatus = ProductStatuses.RoughTube,
            Quantity = 108,
            Weight = 1440m
        });
        ctx.ProcessInspections.Add(new ProcessInspection
        {
            ProductionBatchId = batch.Id,
            ProductionBatch = batch,
            ProcessGroupId = 1,
            ProcessName = ProcessKeys.ColdRoll50,
            SectionName = SectionKeys.Inspection,
            InspectionDate = new DateTime(2024, 3, 6),
            Inspector = OperatorNameHelper.Format("张三", "YG001"),
            ProductStatus = ProductStatuses.InProgress,
            Quantity = 100,
            Weight = 1000m
        });
        await ctx.SaveChangesAsync();

        var month = await svc.GetMonthAsync(2024, 3, PayrollWageGroup.IndividualPiece, null);

        var row = RowOf(month, "YG001");
        row.DayEngineAmount[5].Should().Be(27m);  // 0.25 元/支 × 108 支 = 27
        row.DayEngineAmount[6].Should().Be(20m);  // 20 元/吨 × 1 吨 = 20
        row.TotalEngine.Should().Be(47m);
    }

    [Fact]
    public async Task GetMonthAsync_个人计件_纯集体行跳过_集体计件未完成不发放()
    {
        using var ctx = CreateDbContext();
        var svc = new PayrollDailyWageService(ctx);

        await SeedEmployeeAsync(ctx, "YG001", "张三", SalaryMode.PieceIndividual);   // 组内员工（无其记录）
        await SeedEmployeeAsync(ctx, "YG200", "王二", SalaryMode.PieceCollective);   // 集体计件：未完成不发放
        var batch = await SeedBatchAsync(ctx, spec: "60*5");
        await SeedProdCategoryAsync(ctx, SectionKeys.ColdRollDraw, 50m, PieceRateUnitKeys.PerTon,
            new List<PieceRateProductionCategoryTier>());

        // 整行写名人全是集体计件 → 无个人计件发放对象 → 整行跳过（不发、不警告、不误入个人计价）
        ctx.ProductionRecords.Add(new ProductionRecord
        {
            ProductionBatchId = batch.Id,
            ProductionBatch = batch,
            ProcessGroupId = 1,
            ProcessName = ProcessKeys.ColdRoll60,
            SectionName = SectionKeys.ColdRollDraw,
            ExecDate = new DateTime(2024, 3, 10),
            Operator = OperatorNameHelper.Format("王二", "YG200"),
            ProductStatus = ProductStatuses.InProgress,
            Weight = 1000m,
            Quantity = null
        });
        await ctx.SaveChangesAsync();

        var month = await svc.GetMonthAsync(2024, 3, PayrollWageGroup.IndividualPiece, null);

        var row = RowOf(month, "YG001");
        row.DayEngineAmount[10].Should().BeNull();
        row.TotalEngine.Should().Be(0m);
        month.Employees.Select(e => e.EmployeeCode).Should().NotContain("YG200");
    }
}
