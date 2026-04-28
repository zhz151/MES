using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities;

namespace MES.Services;

/// <summary>
/// 用料计划服务实现
/// </summary>
public class MaterialPlanService : IMaterialPlanService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MaterialPlanService> _logger;

    public MaterialPlanService(AppDbContext context, ILogger<MaterialPlanService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region 原料采购计划

    public async Task<List<PurchaseSemiPlanDto>> GetSemiPlansAsync(int workOrderId)
    {
        var plans = await _context.PurchaseSemiPlans
            .Where(p => p.WorkOrderId == workOrderId)
            .OrderByDescending(p => p.CreatedTime)
            .ToListAsync();

        return plans.Select(p => p.ToDto()).ToList();
    }

    public async Task<PurchaseSemiPlanDto> GetSemiPlanByIdAsync(int id)
    {
        var plan = await _context.PurchaseSemiPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("原料采购计划不存在");
        return plan.ToDto();
    }

    public async Task<PurchaseSemiPlanDto> CreateSemiPlanAsync(CreatePurchaseSemiPlanRequest request)
    {
        var workOrder = await _context.WorkOrders.FindAsync(request.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        // 非定尺: 支数不能为空（需要人工填写）
        if (workOrder.LengthStatus == LengthStatus.NonFixed && request.ManualPieces == null)
            throw new BusinessException("非定尺模式下原料支数为必填");

        // 执行测算
        var calc = await CalculateInternalAsync(workOrder, request);

        decimal requiredPieces;
        decimal requiredWeight;

        if (workOrder.LengthStatus == LengthStatus.NonFixed)
        {
            // 非定尺：人工填写
            requiredPieces = request.ManualPieces ?? 0;
            requiredWeight = request.ManualWeight ?? 0;
        }
        else
        {
            // 定尺/范围尺：自动计算
            requiredPieces = calc.RequiredPieces ?? 0;
            requiredWeight = calc.RequiredWeight ?? 0;
        }

        var plan = new PurchaseSemiPlan
        {
            WorkOrderId = request.WorkOrderId,
            PlanDate = request.PlanDate,
            AdjustedWallThickness = request.AdjustedWallThickness,
            YieldRate = request.YieldRate,
            InputMultiple = request.InputMultiple,
            QualifiedRate = request.QualifiedRate,
            Density = calc.Density,
            UnitWeight = calc.UnitWeight,
            RawUnitWeight = calc.RawUnitWeight,
            RequiredPieces = (int)requiredPieces,
            RequiredWeight = requiredWeight,
            RawMaterialType = Enum.Parse<RawMaterialType>(request.RawMaterialType),
            RawMaterialSpec = request.RawMaterialSpec,
            RequiredDate = request.RequiredDate,
            ProcessPlan = request.ProcessPlan,
            Remark = request.Remark
        };

        _context.PurchaseSemiPlans.Add(plan);
        await _context.SaveChangesAsync();

        // 刷新工单状态
        await UpdateMaterialPlanStatusAsync(request.WorkOrderId);

        _logger.LogInformation("创建原料采购计划成功: 工单ID {WorkOrderId}, 原料规格 {Spec}, 重量 {Weight}",
            request.WorkOrderId, request.RawMaterialSpec, requiredWeight);

        return plan.ToDto();
    }

    public async Task DeleteSemiPlanAsync(int id)
    {
        var plan = await _context.PurchaseSemiPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("原料采购计划不存在");

        var workOrderId = plan.WorkOrderId;
        _context.PurchaseSemiPlans.Remove(plan);
        await _context.SaveChangesAsync();

        // 刷新工单状态
        await UpdateMaterialPlanStatusAsync(workOrderId);

        _logger.LogInformation("删除原料采购计划成功: ID {Id}", id);
    }

    #endregion

    #region 成品采购计划

    public async Task<List<PurchaseFinishedPlanDto>> GetFinishedPlansAsync(int workOrderId)
    {
        var plans = await _context.PurchaseFinishedPlans
            .Where(p => p.WorkOrderId == workOrderId)
            .OrderByDescending(p => p.CreatedTime)
            .ToListAsync();

        return plans.Select(p => p.ToDto()).ToList();
    }

    public async Task<PurchaseFinishedPlanDto> GetFinishedPlanByIdAsync(int id)
    {
        var plan = await _context.PurchaseFinishedPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("成品采购计划不存在");
        return plan.ToDto();
    }

    public async Task<PurchaseFinishedPlanDto> CreateFinishedPlanAsync(CreatePurchaseFinishedPlanRequest request)
    {
        var workOrder = await _context.WorkOrders.FindAsync(request.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        // 定尺：支数必须填写
        if (workOrder.LengthStatus == LengthStatus.Fixed && (request.RequiredPiece == null || request.RequiredPiece <= 0))
            throw new BusinessException("定尺模式下采购支数不能为空");

        var plan = new PurchaseFinishedPlan
        {
            WorkOrderId = request.WorkOrderId,
            PlanDate = request.PlanDate,
            ProductType = Enum.Parse<FinishedProductType>(request.ProductType),
            RequiredPiece = request.RequiredPiece,
            RequiredWeight = request.RequiredWeight,
            RequiredDate = request.RequiredDate,
            Remark = request.Remark
        };

        _context.PurchaseFinishedPlans.Add(plan);
        await _context.SaveChangesAsync();

        // 刷新工单状态
        await UpdateMaterialPlanStatusAsync(request.WorkOrderId);

        _logger.LogInformation("创建成品采购计划成功: 工单ID {WorkOrderId}, 重量 {Weight}",
            request.WorkOrderId, request.RequiredWeight);

        return plan.ToDto();
    }

    public async Task DeleteFinishedPlanAsync(int id)
    {
        var plan = await _context.PurchaseFinishedPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("成品采购计划不存在");

        var workOrderId = plan.WorkOrderId;
        _context.PurchaseFinishedPlans.Remove(plan);
        await _context.SaveChangesAsync();

        // 刷新工单状态
        await UpdateMaterialPlanStatusAsync(workOrderId);

        _logger.LogInformation("删除成品采购计划成功: ID {Id}", id);
    }

    #endregion

    #region 用料测算

    public async Task<MaterialCalculateResult> CalculateAsync(MaterialCalculateRequest request)
    {
        var workOrder = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == request.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        return await CalculateInternalAsync(workOrder, new CreatePurchaseSemiPlanRequest
        {
            AdjustedWallThickness = request.AdjustedWallThickness,
            YieldRate = request.YieldRate,
            InputMultiple = request.InputMultiple,
            QualifiedRate = request.QualifiedRate
        });
    }

    /// <summary>
    /// 内部测算逻辑
    /// </summary>
    private async Task<MaterialCalculateResult> CalculateInternalAsync(
        WorkOrder workOrder, CreatePurchaseSemiPlanRequest request)
    {
        var result = new MaterialCalculateResult();

        // 1. 查询密度（从牌号对表按工厂牌号查找）
        var gradeMapping = await _context.StandardGradeMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.PlantGrade == workOrder.PlantGrade);
        result.Density = gradeMapping?.Density ?? 7.93m; // 默认密度

        // 2. 解析外径
        var od = ParseOuterDiameter(workOrder.Specification);

        // 3. 单米重量(kg/m) = π × 密度 × 调整壁厚 × (外径 - 调整壁厚) / 1000
        var adjustedWT = request.AdjustedWallThickness;
        result.UnitWeightPerMeter = Math.Round(
            (decimal)Math.PI * result.Density * adjustedWT * (od - adjustedWT) / 1000m, 6);

        // 4. 非定尺：不计算单重
        if (workOrder.LengthStatus == LengthStatus.NonFixed)
        {
            return result;
        }

        // 5. 成品单重(kg/支) = 单米重量 × 最大长度(m) / 1000
        var maxLengthM = (workOrder.MaxLength ?? 6000) / 1000m;
        result.UnitWeight = Math.Round(result.UnitWeightPerMeter * maxLengthM, 3);

        // 6. 原料单重(kg/支) = 成品单重 ÷ (成材率/100) × 每支产出
        if (request.YieldRate > 0)
        {
            var yieldDecimal = request.YieldRate / 100m;
            result.RawUnitWeight = Math.Round(
                result.UnitWeight.Value / yieldDecimal * request.InputMultiple, 3);
        }

        // 7. 原料支数 = ROUND(总支数 ÷ 每支产出 ÷ (正品率/100), 0)
        if (request.QualifiedRate > 0 && request.InputMultiple > 0)
        {
            var qualifiedDecimal = request.QualifiedRate / 100m;
            result.RequiredPieces = (int)Math.Round(
                workOrder.TotalQuantity / (decimal)request.InputMultiple / qualifiedDecimal);
        }

        // 8. 原料重量(kg) = 原料单重 × 原料支数
        if (result.RawUnitWeight.HasValue && result.RequiredPieces.HasValue)
        {
            result.RequiredWeight = Math.Round(
                result.RawUnitWeight.Value * result.RequiredPieces.Value, 3);
        }

        return result;
    }

    /// <summary>
    /// 从规格字符串解析外径
    /// </summary>
    private static decimal ParseOuterDiameter(string specification)
    {
        if (string.IsNullOrEmpty(specification))
            return 0;

        var parts = specification.Split('*');
        if (parts.Length > 0 && decimal.TryParse(parts[0], out var od))
            return od;

        return 0;
    }

    #endregion

    #region 计划状态

    public async Task<WorkOrderMaterialPlanDto> GetWorkOrderMaterialPlanAsync(int workOrderId)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == workOrderId);

        if (workOrder == null)
            throw new BusinessException("工单不存在");

        var dto = new WorkOrderMaterialPlanDto
        {
            WorkOrderId = workOrder.Id,
            WorkOrderNo = workOrder.WorkOrderNo,
            MaterialPlanStatus = workOrder.MaterialPlanStatus,
            MaterialPlanRate = workOrder.MaterialPlanRate,
            Items = new List<MaterialPlanItemDto>()
        };

        // 原料采购
        var semiPlans = await _context.PurchaseSemiPlans
            .Where(p => p.WorkOrderId == workOrderId)
            .ToListAsync();
        if (semiPlans.Any())
        {
            var status = CalculatePlanStatus(workOrder, semiPlans, isSemi: true);
            dto.Items.Add(new MaterialPlanItemDto
            {
                PlanType = "Semi",
                PlanTypeText = "原料采购",
                RecordCount = semiPlans.Count,
                Summary = $"{semiPlans.First().RawMaterialSpec} × {semiPlans.Sum(p => p.RequiredPieces ?? 0)}支 / {semiPlans.Sum(p => p.RequiredWeight)}kg",
                RequiredDate = semiPlans.Min(p => p.RequiredDate),
                Status = status
            });
        }

        // 成品采购
        var finishPlans = await _context.PurchaseFinishedPlans
            .Where(p => p.WorkOrderId == workOrderId)
            .ToListAsync();
        if (finishPlans.Any())
        {
            var status = CalculatePlanStatus(workOrder, finishPlans, isSemi: false);
            dto.Items.Add(new MaterialPlanItemDto
            {
                PlanType = "Finished",
                PlanTypeText = "成品采购",
                RecordCount = finishPlans.Count,
                Summary = $"{finishPlans.First().ProductType} × {finishPlans.Sum(p => p.RequiredPiece ?? 0)}支 / {finishPlans.Sum(p => p.RequiredWeight)}kg",
                RequiredDate = finishPlans.Min(p => p.RequiredDate),
                Status = status
            });
        }

        return dto;
    }

    public async Task UpdateMaterialPlanStatusAsync(int workOrderId)
    {
        var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
        if (workOrder == null)
            return;

        var semiPlans = await _context.PurchaseSemiPlans
            .Where(p => p.WorkOrderId == workOrderId)
            .ToListAsync();

        var finishPlans = await _context.PurchaseFinishedPlans
            .Where(p => p.WorkOrderId == workOrderId)
            .ToListAsync();

        var hasSemi = semiPlans.Any();
        var hasFinish = finishPlans.Any();

        if (!hasSemi && !hasFinish)
        {
            workOrder.MaterialPlanStatus = MaterialPlanStatus.NotPlanned;
            workOrder.MaterialPlanRate = 0;
        }
        else
        {
            var statuses = new List<MaterialPlanStatus>();
            var rates = new List<decimal>();

            if (hasSemi)
            {
                var s = CalculatePlanStatus(workOrder, semiPlans, isSemi: true);
                statuses.Add(s);
                rates.Add(CalculatePlanRate(workOrder, semiPlans, isSemi: true));
            }

            if (hasFinish)
            {
                var s = CalculatePlanStatus(workOrder, finishPlans, isSemi: false);
                statuses.Add(s);
                rates.Add(CalculatePlanRate(workOrder, finishPlans, isSemi: false));
            }

            // 工单状态 = 取最差
            workOrder.MaterialPlanStatus = statuses.Min();
            workOrder.MaterialPlanRate = rates.Min();
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 计算单个计划的状态（工单级，含"理论满足"）
    /// </summary>
    private MaterialPlanStatus CalculatePlanStatus(WorkOrder workOrder,
        IReadOnlyCollection<BaseEntity> plans, bool isSemi)
    {
        var rate = CalculatePlanRate(workOrder, plans, isSemi);

        if (workOrder.LengthStatus == LengthStatus.Fixed)
        {
            // 定尺：支数模式
            if (rate < 100m) return MaterialPlanStatus.Partial;
            if (rate < 102m) return MaterialPlanStatus.TheoreticalSatisfied;
            if (rate <= 110m) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
        else
        {
            // 范围尺/非定尺：重量模式
            if (rate < 100m) return MaterialPlanStatus.Partial;
            if (rate < 105m) return MaterialPlanStatus.TheoreticalSatisfied;
            if (rate <= 120m) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
    }

    /// <summary>
    /// 计算满足率
    /// </summary>
    private decimal CalculatePlanRate(WorkOrder workOrder,
        IReadOnlyCollection<BaseEntity> plans, bool isSemi)
    {
        if (workOrder.LengthStatus == LengthStatus.Fixed)
        {
            // 定尺：按支数
            int effectivePieces;

            if (isSemi)
            {
                // 原料采购：推算可产成品支数 = 原料支数 × 每支产出 × 正品率/100 × 1.02
                var semiPlans = plans.Cast<PurchaseSemiPlan>();
                var totalInputPieces = semiPlans.Sum(p => p.RequiredPieces ?? 0);
                var avgInputMultiple = semiPlans.Average(p => p.InputMultiple);
                var avgQualifiedDecimal = semiPlans.Average(p => p.QualifiedRate) / 100m;
                effectivePieces = (int)(totalInputPieces * (decimal)avgInputMultiple * avgQualifiedDecimal * 1.02m);
            }
            else
            {
                // 成品采购：采购支数 × 1.02
                var finishPlans = plans.Cast<PurchaseFinishedPlan>();
                effectivePieces = (int)(finishPlans.Sum(p => p.RequiredPiece ?? 0) * 1.02m);
            }

            if (workOrder.TotalQuantity <= 0) return 0;
            return Math.Round((decimal)effectivePieces / workOrder.TotalQuantity * 100m, 2);
        }
        else
        {
            // 范围尺/非定尺：按重量
            decimal effectiveWeight;

            if (isSemi)
            {
                var semiPlans = plans.Cast<PurchaseSemiPlan>();
                effectiveWeight = semiPlans.Sum(p => p.RequiredWeight) * 1.05m;
            }
            else
            {
                var finishPlans = plans.Cast<PurchaseFinishedPlan>();
                effectiveWeight = finishPlans.Sum(p => p.RequiredWeight) * 1.05m;
            }

            if (workOrder.TotalWeight <= 0) return 0;
            return Math.Round(effectiveWeight / workOrder.TotalWeight * 100m, 2);
        }
    }

    #endregion

    #region 打印

    public async Task<byte[]> PrintSemiPlanAsync(int planId)
    {
        var plan = await _context.PurchaseSemiPlans.FindAsync(planId);
        if (plan == null)
            throw new BusinessException("原料采购计划不存在");

        var workOrder = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == plan.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        // 简单文本PDF（先用占位，后续替换为QuestPDF）
        var content = GeneratePrintContent(plan, workOrder);
        return System.Text.Encoding.UTF8.GetBytes(content);
    }

    public async Task<byte[]> PrintFinishedPlanAsync(int planId)
    {
        var plan = await _context.PurchaseFinishedPlans.FindAsync(planId);
        if (plan == null)
            throw new BusinessException("成品采购计划不存在");

        var workOrder = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == plan.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        var content = GeneratePrintContent(plan, workOrder);
        return System.Text.Encoding.UTF8.GetBytes(content);
    }

    private static string GeneratePrintContent(PurchaseSemiPlan plan, WorkOrder workOrder)
    {
        return $"""
            ╔══════════════════════════════════════════╗
            ║              采购申请单                    ║
            ╠══════════════════════════════════════════╣
            ║  编号：PUR-{plan.PlanDate:yyyyMMdd}-{plan.Id:D4}
            ║  日期：{plan.PlanDate:yyyy-MM-dd}
            ╠══════════════════════════════════════════╣
            ║  一、采购原料
            ║  原料类型：{plan.RawMaterialType}
            ║  原料规格：{plan.RawMaterialSpec}
            ║  工厂牌号：{workOrder.PlantGrade}
            ║  采购支数：{plan.RequiredPieces} 支
            ║  采购重量：{plan.RequiredWeight:G29} kg
            ║  原料单重：{plan.RawUnitWeight?.ToString("G29") ?? "-"} kg/支
            ║  成材率：{plan.YieldRate:P2}
            ║  正品率：{plan.QualifiedRate:P2}
            ║  投料倍率：{plan.InputMultiple}
            ║  要求到货：{plan.RequiredDate:yyyy-MM-dd}
            ║  备注：{plan.Remark ?? "-"}
            ╠══════════════════════════════════════════╣
            ║  二、工单信息
            ║  工单号：{workOrder.WorkOrderNo}
            ║  订单号：{workOrder.SalesOrderNo}
            ║  工厂牌号：{workOrder.PlantGrade}
            ║  成品规格：{workOrder.Specification}
            ║  长度状态：{workOrder.LengthStatus} {workOrder.MaxLength?.ToString("G29") ?? ""}mm
            ║  总量：{workOrder.TotalQuantity}支 / {workOrder.TotalWeight:G29}kg
            ║  结算方式：{workOrder.SettlementMethod}
            ║  交货状态：{workOrder.DeliveryState}
            ╠══════════════════════════════════════════╣
            ║  制单人：            审核人：
            ║  打印日期：{DateTime.Now:yyyy-MM-dd}
            ╚══════════════════════════════════════════╝
            """;
    }

    private static string GeneratePrintContent(PurchaseFinishedPlan plan, WorkOrder workOrder)
    {
        return $"""
            ╔══════════════════════════════════════════╗
            ║              采购申请单                    ║
            ╠══════════════════════════════════════════╣
            ║  编号：PUR-{plan.PlanDate:yyyyMMdd}-{plan.Id:D4}
            ║  日期：{plan.PlanDate:yyyy-MM-dd}
            ╠══════════════════════════════════════════╣
            ║  一、采购内容
            ║  成品类型：{plan.ProductType}
            ║  采购支数：{plan.RequiredPiece?.ToString() ?? "-"} 支
            ║  采购重量：{plan.RequiredWeight:G29} kg
            ║  要求到货：{plan.RequiredDate:yyyy-MM-dd}
            ║  备注：{plan.Remark ?? "-"}
            ╠══════════════════════════════════════════╣
            ║  二、工单信息
            ║  工单号：{workOrder.WorkOrderNo}
            ║  订单号：{workOrder.SalesOrderNo}
            ║  工厂牌号：{workOrder.PlantGrade}
            ║  成品规格：{workOrder.Specification}
            ║  长度状态：{workOrder.LengthStatus} {workOrder.MaxLength?.ToString("G29") ?? ""}mm
            ║  总量：{workOrder.TotalQuantity}支 / {workOrder.TotalWeight:G29}kg
            ║  结算方式：{workOrder.SettlementMethod}
            ║  交货状态：{workOrder.DeliveryState}
            ╠══════════════════════════════════════════╣
            ║  制单人：            审核人：
            ║  打印日期：{DateTime.Now:yyyy-MM-dd}
            ╚══════════════════════════════════════════╝
            """;
    }

    #endregion
}

#region Mapping Extensions

internal static class MaterialPlanMappingExtensions
{
    public static PurchaseSemiPlanDto ToDto(this PurchaseSemiPlan entity)
    {
        return new PurchaseSemiPlanDto
        {
            Id = entity.Id,
            WorkOrderId = entity.WorkOrderId,
            PlanDate = entity.PlanDate,
            AdjustedWallThickness = entity.AdjustedWallThickness,
            YieldRate = entity.YieldRate,
            InputMultiple = entity.InputMultiple,
            QualifiedRate = entity.QualifiedRate,
            Density = entity.Density,
            UnitWeight = entity.UnitWeight,
            RawUnitWeight = entity.RawUnitWeight,
            RequiredPieces = entity.RequiredPieces,
            RequiredWeight = entity.RequiredWeight,
            RawMaterialType = entity.RawMaterialType.ToString(),
            RawMaterialSpec = entity.RawMaterialSpec,
            RequiredDate = entity.RequiredDate,
            ProcessPlan = entity.ProcessPlan,
            Remark = entity.Remark,
            CreatedTime = entity.CreatedTime,
            CreatedBy = entity.CreatedBy
        };
    }

    public static PurchaseFinishedPlanDto ToDto(this PurchaseFinishedPlan entity)
    {
        return new PurchaseFinishedPlanDto
        {
            Id = entity.Id,
            WorkOrderId = entity.WorkOrderId,
            PlanDate = entity.PlanDate,
            ProductType = entity.ProductType.ToString(),
            RequiredPiece = entity.RequiredPiece,
            RequiredWeight = entity.RequiredWeight,
            RequiredDate = entity.RequiredDate,
            Remark = entity.Remark,
            CreatedTime = entity.CreatedTime,
            CreatedBy = entity.CreatedBy
        };
    }
}

#endregion
