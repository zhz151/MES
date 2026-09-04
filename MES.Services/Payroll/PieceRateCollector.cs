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
/// ⚠️ 口径（2026-09-04 定尺接线 + 提醒拆分）：无归属对象（eligible 空）的行整行跳过且不计 unpriced；
/// unpriced 仅在「有归属对象且该行已记录到量（Weight/Quantity 任一 &gt;0）但命中不到启用类别」时计数；
/// 无产出量（漏记/虚拟补录数量空）与 命中类别但数量缺失折算 0 → 静默不计（数量问题不发工资也不提醒）。
/// 生产源 4 类 → 请求经 <see cref="ProductionMatchRequestMapper"/> 共享单源（含切行定尺 Length/FixedLengthCount、
/// 光亮 SpecialState 接线），成检源 → 请求经 <see cref="FinalInspectionMatchRequestMapper"/> 共享单源——
/// 试算与结算同映射，防双通道口径漂移的单一事实源。
/// </summary>
public sealed class PieceRateCollector
{
    private readonly AppDbContext _context;

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

            // 记录→请求经 ProductionMatchRequestMapper 共享单源（含切行 Length/FixedLengthCount/光亮 SpecialState 接线）
            var request = ProductionMatchRequestMapper.BuildFromProductionRecord(r, r.ProductionBatch);
            var hit = PieceRateMatchEngine.MatchProduction(prodCategories, request);
            if (hit == null)
            {
                // 未定价：无工资。仅「已记录到量（任一计量>0）但命中不到启用类别」是真缺口进提醒；
                // 无产出量（漏记/虚拟补录空）静默不计
                if (HasRecordedOutput(r.Weight, r.Quantity)) result.UnpricedCount++;
                continue;
            }
            var total = PieceRateAmountHelper.AmountForUnit(hit.Unit, hit.UnitPrice, r.Weight, r.Quantity, null);
            if (total is null || total <= 0) continue; // 命中类别但数量缺失/不足折算 → 数量问题静默（不发工资不进提醒）

            result.Rows.Add(new PricedPieceRow { TotalHeadcount = headcount, Eligible = eligible, Amount = total.Value, Date = r.ExecDate });
        }

        // ---- 成检记录（Length 档量纲 mm；Fixed=实际定尺长，Range/NonFixed 缺省 6000 折算；Quantity=检验支数）。
        // 记录→计价请求经 FinalInspectionMatchRequestMapper 共享单源（与「按记录模拟测算」同映射，防双通道漂移）----
        var inspections = await _context.FinalInspections.AsNoTracking()
            .Include(f => f.ProductionBatch)
            .Where(f => f.InspectionDate >= monthStart && f.InspectionDate < monthEnd)
            .ToListAsync();
        foreach (var f in inspections)
        {
            var (headcount, eligible) = ResolveParticipants(f.Operator, byCode, byName);
            if (eligible.Count == 0) continue;

            var request = FinalInspectionMatchRequestMapper.BuildRequest(f, f.ProductionBatch);
            var hit = PieceRateMatchEngine.MatchFinalInspection(finalCategories, request);
            if (hit == null)
            {
                if (HasRecordedOutput(f.Weight, f.Quantity)) result.UnpricedCount++;
                continue;
            }
            var total = PieceRateAmountHelper.AmountForUnit(hit.Unit, hit.UnitPrice, request.WeightKg, request.InspectionCount, request.Length);
            if (total is null || total <= 0) continue; // 数量问题静默

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

            // 记录→请求经 ProductionMatchRequestMapper 共享单源（Stage=InTank）
            var request = ProductionMatchRequestMapper.BuildFromPicklingIn(r);
            var hit = PieceRateMatchEngine.MatchProduction(prodCategories, request);
            if (hit == null)
            {
                if (HasRecordedOutput(r.Weight, r.Quantity)) result.UnpricedCount++;
                continue;
            }
            var total = PieceRateAmountHelper.AmountForUnit(hit.Unit, hit.UnitPrice, r.Weight, r.Quantity, null);
            if (total is null || total <= 0) continue; // 数量问题静默

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

            // 记录→请求经 ProductionMatchRequestMapper 共享单源（Stage=OutTank）
            var request = ProductionMatchRequestMapper.BuildFromPicklingOut(r);
            var hit = PieceRateMatchEngine.MatchProduction(prodCategories, request);
            if (hit == null)
            {
                if (HasRecordedOutput(r.Weight, r.Quantity)) result.UnpricedCount++;
                continue;
            }
            var total = PieceRateAmountHelper.AmountForUnit(hit.Unit, hit.UnitPrice, r.Weight, r.Quantity, null);
            if (total is null || total <= 0) continue; // 数量问题静默

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

            // 记录→请求经 ProductionMatchRequestMapper 共享单源（无作业阶段；规格/牌号空回退批次）
            var request = ProductionMatchRequestMapper.BuildFromProcessInspection(r, r.ProductionBatch);
            var hit = PieceRateMatchEngine.MatchProduction(prodCategories, request);
            if (hit == null)
            {
                if (HasRecordedOutput(r.Weight, r.Quantity)) result.UnpricedCount++;
                continue;
            }
            var total = PieceRateAmountHelper.AmountForUnit(hit.Unit, hit.UnitPrice, r.Weight, r.Quantity, null);
            if (total is null || total <= 0) continue; // 数量问题静默

            result.Rows.Add(new PricedPieceRow { TotalHeadcount = headcount, Eligible = eligible, Amount = total.Value, Date = r.InspectionDate });
        }

        return result;
    }

    // ==================== 行量判定 ====================

    /// <summary>该行是否已记录到实际产出量（Weight/Quantity 任一 &gt;0；null 视为 0）。漏记/虚拟补录数量空 → false。</summary>
    private static bool HasRecordedOutput(decimal? weightKg, int? quantity)
        => (weightKg ?? 0m) > 0m || (quantity ?? 0) > 0;

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
}

/// <summary>一次采集的结果：当月已定价且含归属对象的行 + 「有量没价」行计数（仅已记录到量但命中不到启用类别者）</summary>
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
