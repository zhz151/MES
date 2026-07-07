using MES.Core.Enums;
using MES.Data.Entities;

namespace MES.Services;

/// <summary>
/// 用料计划满足率/状态计算（静态工具类，供 WorkOrderExecutionService / WorkOrderService 共用）
/// </summary>
internal static class PlanRateCalculator
{
    /// <summary>
    /// 从 6 种用料计划数据计算工单级满足率 + 状态
    /// </summary>
    public static (decimal rate, int status) ComputeWorkOrderRate(
        Data.Entities.WorkOrder wo,
        List<PurchaseSemiPlan> semiPlans,
        List<PurchaseFinishedPlan> finishPlans,
        List<InventoryPlan> inventoryPlans,
        List<RoundBarPiercingPlan> piercingPlans,
        List<InProcessReworkPlan>? inProcessReworkPlans = null,
        decimal fixedTheoretical = 102m, decimal fixedSatisfied = 110m,
        decimal nonFixedTheoretical = 105m, decimal nonFixedSatisfied = 120m)
    {
        var rates = new List<decimal>();

        if (semiPlans.Count > 0)
            rates.Add(CalculatePlanRate(wo, semiPlans.Cast<object>().ToList(), isSemi: true, isPiercing: false));

        if (finishPlans.Count > 0)
            rates.Add(CalculatePlanRate(wo, finishPlans.Cast<object>().ToList(), isSemi: false, isPiercing: false));

        var regularInv = inventoryPlans.Where(p => p.ReworkType == null).ToList();
        if (regularInv.Count > 0)
            rates.Add(CalculateInventoryPlanRate(wo, regularInv));

        var reworkInv = inventoryPlans.Where(p => p.ReworkType != null).ToList();
        if (reworkInv.Count > 0)
            rates.Add(CalculateInventoryPlanRate(wo, reworkInv));

        if (piercingPlans.Count > 0)
            rates.Add(CalculatePlanRate(wo, piercingPlans.Cast<object>().ToList(), isSemi: false, isPiercing: true));

        if (inProcessReworkPlans is { Count: > 0 })
            rates.Add(CalculateInProcessReworkPlanRate(wo, inProcessReworkPlans));

        if (rates.Count == 0)
            return (0, 0);

        var totalRate = Math.Min(rates.Sum(), 999m);
        var status = CalculateOverallStatus(wo.LengthStatus, totalRate,
            fixedTheoretical, fixedSatisfied, nonFixedTheoretical, nonFixedSatisfied);
        return (totalRate, (int)status);
    }

    private static decimal CalculatePlanRate(Data.Entities.WorkOrder wo, List<object> plans, bool isSemi, bool isPiercing)
    {
        if (wo.LengthStatus == LengthStatus.Fixed)
        {
            int effectivePieces;
            if (isSemi || isPiercing)
            {
                // 原料采购 / 圆棒穿孔：原料支数 × 投料倍率
                effectivePieces = (int)plans.Sum(p =>
                {
                    if (isSemi && p is PurchaseSemiPlan sp)
                        return (sp.RequiredPieces ?? 0) * sp.InputMultiple;
                    if (isPiercing && p is RoundBarPiercingPlan rp)
                        return (rp.RequiredPieces ?? 0) * rp.InputMultiple;
                    return 0;
                });
            }
            else
            {
                // 成品采购：直接按实际采购支数
                effectivePieces = plans.Sum(p => p is PurchaseFinishedPlan fp ? fp.RequiredPiece ?? 0 : 0);
            }

            if (wo.TotalQuantity <= 0) return 0;
            return Math.Round((decimal)effectivePieces / wo.TotalQuantity * 100m, 0);
        }
        else
        {
            decimal effectiveWeight;
            if (isSemi || isPiercing)
            {
                effectiveWeight = plans.Sum(p =>
                {
                    if (isSemi && p is PurchaseSemiPlan sp)
                        return sp.RequiredWeight;
                    if (isPiercing && p is RoundBarPiercingPlan rp)
                        return rp.RequiredWeight;
                    return 0;
                });
            }
            else
            {
                effectiveWeight = plans.Sum(p => p is PurchaseFinishedPlan fp ? fp.RequiredWeight : 0);
            }

            if (wo.TotalWeight <= 0) return 0;
            return Math.Round(effectiveWeight / wo.TotalWeight * 100m, 0);
        }
    }

    private static decimal CalculateInventoryPlanRate(Data.Entities.WorkOrder wo, List<InventoryPlan> plans)
    {
        if (wo.LengthStatus == LengthStatus.Fixed)
        {
            var effectivePieces = (int)plans.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple);
            if (wo.TotalQuantity <= 0) return 0;
            return Math.Round((decimal)effectivePieces / wo.TotalQuantity * 100m, 0);
        }
        else
        {
            var effectiveWeight = plans.Sum(p => p.UsedWeight);
            if (wo.TotalWeight <= 0) return 0;
            return Math.Round(effectiveWeight / wo.TotalWeight * 100m, 0);
        }
    }

    private static decimal CalculateInProcessReworkPlanRate(Data.Entities.WorkOrder wo, List<InProcessReworkPlan> plans)
    {
        if (wo.LengthStatus == LengthStatus.Fixed)
        {
            var effectivePieces = (int)plans.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple);
            if (wo.TotalQuantity <= 0) return 0;
            return Math.Round((decimal)effectivePieces / wo.TotalQuantity * 100m, 0);
        }
        else
        {
            var effectiveWeight = plans.Sum(p => p.UsedWeight);
            if (wo.TotalWeight <= 0) return 0;
            return Math.Round(effectiveWeight / wo.TotalWeight * 100m, 0);
        }
    }

    private static MaterialPlanStatus CalculateOverallStatus(
        LengthStatus lengthStatus, decimal totalRate,
        decimal fixedTheoretical, decimal fixedSatisfied,
        decimal nonFixedTheoretical, decimal nonFixedSatisfied)
    {
        if (totalRate <= 0) return MaterialPlanStatus.NotPlanned;

        if (lengthStatus == LengthStatus.Fixed)
        {
            if (totalRate < 100m) return MaterialPlanStatus.Partial;
            if (totalRate < fixedTheoretical) return MaterialPlanStatus.TheoreticalSatisfied;
            if (totalRate <= fixedSatisfied) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
        else
        {
            if (totalRate < 100m) return MaterialPlanStatus.Partial;
            if (totalRate < nonFixedTheoretical) return MaterialPlanStatus.TheoreticalSatisfied;
            if (totalRate <= nonFixedSatisfied) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
    }
}
