using Microsoft.EntityFrameworkCore;
using MES.Core.Constants;
using MES.Core.DTOs.Payroll;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Payroll;
using MES.Data.Entities.Quality;

namespace MES.Services.Payroll;

/// <summary>
/// 计件工资 5 类产量源采集器（2026-09-03 集体计件月结引入；个人计件日结同源重构）。
/// 统一从当月 生产记录 / 去油酸洗入缸 / 去油酸洗完工 / 过程检验 / 成检 五类产量源逐行按现行启用类别定价，
/// 并把「写名人头解析 → 归属（是否命中目标员工）→ 切份前置信息」一次算好返回。
/// 消费方（个人日结 / 集体月结）只做两件不同的事：按日 or 按月归集、发给 PieceIndividual or PieceCollective(按岗位)。
/// ⚠️ 口径与旧 ComputeIndividualPieceEngineAsync 完全一致：无归属对象（eligible 空）的行整行跳过且不计 unpriced；
/// unpriced 仅在「有归属对象但命中不到单价/缺数量」时计数。此共享化是防双通道口径漂移的单一事实源。
/// </summary>
public sealed class PieceRateCollector
{
    private readonly AppDbContext _context;

    /// <summary>范围尺/非定尺/解析失败 长度折算兜底（mm，业务规约 2026-09-03：6000 = 6m 常规管长）</summary>
    private const decimal FallbackLengthMm = 6000m;

    public PieceRateCollector(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>采集当月计价行；targetEmployees 为目标发放员工（个人=PieceIndividual / 集体=PieceCollective），
    /// 空则直接返回空结果。返回 Rows 均 eligible 非空（每人份 = Amount / TotalHeadcount 由消费方切分）。</summary>
    public async Task<CollectResult> CollectAsync(
        DateTime monthStart, DateTime monthEnd, IReadOnlyCollection<Employee> targetEmployees)
    {
        var result = new CollectResult();
        if (targetEmployees.Count == 0) return result;

        var byCode = new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);
        var byName = new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in targetEmployees)
        {
            byCode[e.Code] = e;
            if (!byName.ContainsKey(e.Name)) byName[e.Name] = e;
        }

        // 启用类别全集一次预取（匹配逐行复用；不随行 DB 查询）
        var prodCategories = await _context.PieceRateProductionCategories.AsNoTracking()
            .Include(c => c.Tiers)
            .Include(c => c.ConstraintKeys)
            .Where(c => c.IsActive)
            .ToListAsync();
        var finalCategories = await _context.PieceRateFinalInspectionCategories.AsNoTracking()
            .Include(c => c.Tiers)
            .Where(c => c.IsActive)
            .ToListAsync();

        // ---- 冷轧拔等生产记录（普通报工，无作业阶段）----
        var prodRecords = await _context.ProductionRecords.AsNoTracking()
            .Include(r => r.ProductionBatch)
            .Where(r => r.ExecDate >= monthStart && r.ExecDate < monthEnd)
            .ToListAsync();
        foreach (var r in prodRecords)
        {
            var (headcount, eligible) = ResolveParticipants(r.Operator, byCode, byName);
            if (eligible.Count == 0) continue;

            var spec = !string.IsNullOrWhiteSpace(r.ManufacturingSpec) ? r.ManufacturingSpec
                : r.ProductionBatch?.Specification;
            var request = new PieceRateProductionMatchRequest
            {
                SectionName = SectionKeys.ToKey(r.SectionName) ?? r.SectionName,
                ProcessName = r.ProcessName,
                ProductStatus = r.ProductStatus,
                Stage = null,
                PlantGrade = string.IsNullOrWhiteSpace(r.PlantGrade) ? r.ProductionBatch?.PlantGrade : r.PlantGrade,
                EquipmentName = r.EquipmentName,
                Remark = r.Remark,
                OuterDiameter = spec == null ? null : SpecificationParser.ParseOuterDiameter(spec),
                WallThickness = spec == null ? null : SpecificationParser.ParseWallThickness(spec)
            };
            var hit = PieceRateMatchEngine.MatchProduction(prodCategories, request);
            var total = hit == null ? (decimal?)null
                : AmountForUnit(hit.Unit, hit.UnitPrice, r.Weight, r.Quantity, null);
            if (total is null || total <= 0) { result.UnpricedCount++; continue; }

            result.Rows.Add(new PricedPieceRow { TotalHeadcount = headcount, Eligible = eligible, Amount = total.Value, Date = r.ExecDate });
        }

        // ---- 成检记录（Length 档量纲 mm；Fixed=实际定尺长，Range/NonFixed 缺省 6000 折算；Quantity=检验支数）----
        var inspections = await _context.FinalInspections.AsNoTracking()
            .Include(f => f.ProductionBatch)
            .Where(f => f.InspectionDate >= monthStart && f.InspectionDate < monthEnd)
            .ToListAsync();
        foreach (var f in inspections)
        {
            var (headcount, eligible) = ResolveParticipants(f.Operator, byCode, byName);
            if (eligible.Count == 0) continue;

            var spec = f.ProductionBatch?.Specification;
            var lengthStatus = f.ProductionBatch?.LengthStatus;
            var lengthMm = ResolveLengthMm(f.FixedLength, lengthStatus);
            var request = new PieceRateFinalInspectionMatchRequest
            {
                ItemKey = f.InspectionItem.ToString(),
                LengthStatus = lengthStatus,
                Length = lengthMm,
                InspectionCount = f.Quantity,
                OuterDiameter = spec == null ? null : SpecificationParser.ParseOuterDiameter(spec),
                WallThickness = spec == null ? null : SpecificationParser.ParseWallThickness(spec),
                PlantGrade = f.ProductionBatch?.PlantGrade,
                EquipmentName = f.EquipmentName
            };
            var hit = PieceRateMatchEngine.MatchFinalInspection(finalCategories, request);
            var total = hit == null ? (decimal?)null
                : AmountForUnit(hit.Unit, hit.UnitPrice, f.Weight, f.Quantity, lengthMm);
            if (total is null || total <= 0) { result.UnpricedCount++; continue; }

            result.Rows.Add(new PricedPieceRow { TotalHeadcount = headcount, Eligible = eligible, Amount = total.Value, Date = f.InspectionDate });
        }

        // ---- 去油/酸洗入缸（生产类别 · Stage=InTank，入缸端操作独立计酬）----
        var picklingIns = await _context.PicklingInRecords.AsNoTracking()
            .Where(r => r.InDate >= monthStart && r.InDate < monthEnd)
            .ToListAsync();
        foreach (var r in picklingIns)
        {
            var (headcount, eligible) = ResolveParticipants(r.Operator, byCode, byName);
            if (eligible.Count == 0) continue;

            var request = new PieceRateProductionMatchRequest
            {
                SectionName = SectionKeys.ToKey(r.SectionName) ?? r.SectionName,
                ProcessName = r.ProcessName,
                ProductStatus = r.ProductStatus,
                Stage = PieceRateStageKeys.InTank,
                PlantGrade = r.PlantGrade,
                EquipmentName = r.EquipmentName,
                OuterDiameter = r.ManufacturingSpec == null ? null : SpecificationParser.ParseOuterDiameter(r.ManufacturingSpec),
                WallThickness = r.ManufacturingSpec == null ? null : SpecificationParser.ParseWallThickness(r.ManufacturingSpec)
            };
            var hit = PieceRateMatchEngine.MatchProduction(prodCategories, request);
            var total = hit == null ? (decimal?)null
                : AmountForUnit(hit.Unit, hit.UnitPrice, r.Weight, r.Quantity, null);
            if (total is null || total <= 0) { result.UnpricedCount++; continue; }

            result.Rows.Add(new PricedPieceRow { TotalHeadcount = headcount, Eligible = eligible, Amount = total.Value, Date = r.InDate });
        }

        // ---- 去油/酸洗完工（生产类别 · Stage=OutTank，出缸端操作独立计酬；冗余字段创建时从入缸复制冻结）----
        var picklingOuts = await _context.PicklingOutRecords.AsNoTracking()
            .Where(r => r.CompleteDate >= monthStart && r.CompleteDate < monthEnd)
            .ToListAsync();
        foreach (var r in picklingOuts)
        {
            var (headcount, eligible) = ResolveParticipants(r.Operator, byCode, byName);
            if (eligible.Count == 0) continue;

            var request = new PieceRateProductionMatchRequest
            {
                SectionName = SectionKeys.ToKey(r.SectionName) ?? r.SectionName,
                ProcessName = r.ProcessName,
                ProductStatus = r.ProductStatus,
                Stage = PieceRateStageKeys.OutTank,
                PlantGrade = r.PlantGrade,
                EquipmentName = r.EquipmentName,
                OuterDiameter = r.ManufacturingSpec == null ? null : SpecificationParser.ParseOuterDiameter(r.ManufacturingSpec),
                WallThickness = r.ManufacturingSpec == null ? null : SpecificationParser.ParseWallThickness(r.ManufacturingSpec)
            };
            var hit = PieceRateMatchEngine.MatchProduction(prodCategories, request);
            var total = hit == null ? (decimal?)null
                : AmountForUnit(hit.Unit, hit.UnitPrice, r.Weight, r.Quantity, null);
            if (total is null || total <= 0) { result.UnpricedCount++; continue; }

            result.Rows.Add(new PricedPieceRow { TotalHeadcount = headcount, Eligible = eligible, Amount = total.Value, Date = r.CompleteDate });
        }

        // ---- 过程检验（生产类别 · Inspection 工段，无作业阶段；RoughTube→PerPiece 支单价、InProgress/Finished→PerTon 吨单价）----
        var processInspections = await _context.ProcessInspections.AsNoTracking()
            .Include(p => p.ProductionBatch)
            .Where(p => p.InspectionDate >= monthStart && p.InspectionDate < monthEnd)
            .ToListAsync();
        foreach (var r in processInspections)
        {
            var (headcount, eligible) = ResolveParticipants(r.Inspector, byCode, byName);
            if (eligible.Count == 0) continue;

            var spec = !string.IsNullOrWhiteSpace(r.ManufacturingSpec) ? r.ManufacturingSpec
                : r.ProductionBatch?.Specification;
            var request = new PieceRateProductionMatchRequest
            {
                SectionName = SectionKeys.ToKey(r.SectionName) ?? r.SectionName,
                ProcessName = r.ProcessName,
                ProductStatus = r.ProductStatus,
                Stage = null,
                PlantGrade = string.IsNullOrWhiteSpace(r.PlantGrade) ? r.ProductionBatch?.PlantGrade : r.PlantGrade,
                EquipmentName = r.EquipmentName,
                OuterDiameter = spec == null ? null : SpecificationParser.ParseOuterDiameter(spec),
                WallThickness = spec == null ? null : SpecificationParser.ParseWallThickness(spec)
            };
            var hit = PieceRateMatchEngine.MatchProduction(prodCategories, request);
            var total = hit == null ? (decimal?)null
                : AmountForUnit(hit.Unit, hit.UnitPrice, r.Weight, r.Quantity, null);
            if (total is null || total <= 0) { result.UnpricedCount++; continue; }

            result.Rows.Add(new PricedPieceRow { TotalHeadcount = headcount, Eligible = eligible, Amount = total.Value, Date = r.InspectionDate });
        }

        return result;
    }

    // ==================== 计件行 → 参与人归属 ====================

    /// <summary>
    /// 解析操作人串 → 计件行归属 (写名总人头, 命中目标发放员工)。
    /// - 总人头 = 能解析出 (工号/姓名) 的去重写名段数，不分身份（含集体计件/靠工计件/计时/日薪/月薪等，
    ///   非按件者本人不发放、记 0——其工资按时间基准另算，见 SalaryMode 取酬口径）；
    /// - 可发放 = 其中命中目标员工字典者（个人引擎=个人计件 / 集体引擎=集体计件）。
    /// eligible 为空的行整行跳过（全员非目标发放对象）。
    /// </summary>
    private static (int TotalHeadcount, List<Employee> Eligible) ResolveParticipants(
        string? operatorText,
        Dictionary<string, Employee> byCode,
        Dictionary<string, Employee> byName)
    {
        var eligible = new List<Employee>();
        var seenHeadcount = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEligible = new HashSet<int>();
        int totalHeadcount = 0;
        foreach (var seg in OperatorNameHelper.Split(operatorText))
        {
            if (!OperatorNameHelper.TryParseSegment(seg, out var name, out var code)) continue;

            var headKey = string.IsNullOrWhiteSpace(code) ? name : code;
            if (!string.IsNullOrWhiteSpace(headKey) && seenHeadcount.Add(headKey))
                totalHeadcount++;

            Employee? emp = null;
            if (!string.IsNullOrWhiteSpace(code))
            {
                if (byCode.TryGetValue(code, out var byCodeEmp)) emp = byCodeEmp;
            }
            else if (!string.IsNullOrWhiteSpace(name) && byName.TryGetValue(name, out var byNameEmp))
            {
                emp = byNameEmp;
            }
            if (emp != null && seenEligible.Add(emp.Id))
                eligible.Add(emp);
        }
        return (totalHeadcount, eligible);
    }

    /// <summary>成检单支长（mm）：定尺读 FixedLength 文本首段数字，范围尺/非定尺/解析失败按 6000 兜底</summary>
    private static decimal? ResolveLengthMm(string? fixedLength, string? lengthStatus)
    {
        if (string.Equals(lengthStatus, nameof(LengthStatus.Fixed), StringComparison.OrdinalIgnoreCase))
        {
            var mm = TryParseFirstNumber(fixedLength);
            if (mm.HasValue) return mm.Value;
        }
        return FallbackLengthMm;
    }

    private static decimal? TryParseFirstNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        // 取首个数字（含小数）段，兼容 "9150mm" / "6000" / "11036 mm"
        var num = string.Concat(text.Where(char.IsDigit).Take(12));
        return decimal.TryParse(num, out var v) ? v : null;
    }

    /// <summary>结算单价 → 行总金额（不四舍五入，累加保留精度，显示层 G29）。缺数量/长度返回 null。</summary>
    private static decimal? AmountForUnit(string unit, decimal unitPrice,
        decimal? weightKg, int? quantity, decimal? lengthMm)
    {
        return PieceRateUnitKeys.GetQuantityDimension(unit) switch
        {
            PieceRateUnitKeys.QuantityDimension.Weight =>
                weightKg.HasValue ? weightKg.Value / 1000m * unitPrice : null,          // 元/吨：kg/1000 × 价
            PieceRateUnitKeys.QuantityDimension.Meters =>
                quantity.HasValue && lengthMm.HasValue
                    ? quantity.Value * lengthMm.Value / 1_000_000m * unitPrice : null,  // 元/千米：支×mm/1e6 = km × 价
            PieceRateUnitKeys.QuantityDimension.PieceCount =>
                quantity.HasValue ? quantity.Value * unitPrice : null,                   // 元/支：支数 × 价
            _ => null                                                                     // 元/头 无类别用
        };
    }
}

/// <summary>一次采集的结果：当月已定价且含归属对象的行 + 未定价行计数</summary>
public sealed class CollectResult
{
    public List<PricedPieceRow> Rows { get; } = new();
    public int UnpricedCount { get; set; }
}

/// <summary>一条已定价的计件行（eligible 非空；消费方按 Amount / TotalHeadcount 切份累加）</summary>
public sealed class PricedPieceRow
{
    public int TotalHeadcount { get; init; }
    public List<Employee> Eligible { get; init; } = new();
    public decimal Amount { get; init; }
    public DateTime Date { get; init; }
}
