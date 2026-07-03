using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities;
using MES.Core.Exceptions;
using MES.Services.Mapping;

namespace MES.Services.WorkOrder;

public class MaterialPlanProcessGroupService : IMaterialPlanProcessGroupService
{
    private readonly AppDbContext _context;
    private readonly IStandardWorkDayService _standardWorkDayService;
    private readonly IStandardWorkDayDeliveryStateService _deliveryStateService;
    private readonly IConfigParameterService _configService;

    public MaterialPlanProcessGroupService(AppDbContext context,
        IStandardWorkDayService standardWorkDayService,
        IStandardWorkDayDeliveryStateService deliveryStateService,
        IConfigParameterService configService)
    {
        _context = context;
        _standardWorkDayService = standardWorkDayService;
        _deliveryStateService = deliveryStateService;
        _configService = configService;
    }

    private async Task<decimal> GetConfigAsync(string category, string key, decimal defaultValue)
    {
        var map = await _configService.GetConfigMapAsync(category);
        return map.GetValueOrDefault(key, defaultValue);
    }

    public async Task<List<MaterialPlanProcessGroupDto>> GetByPlanAsync(int planType, int planId)
    {
        return planType switch
        {
            1 => await _context.SemiPlanProcessGroups
                .AsNoTracking()
                .Where(e => e.PurchaseSemiPlanId == planId)
                .OrderBy(e => e.SequenceNumber)
                .Select(e => e.ToDto())
                .ToListAsync(),

3 => await _context.InventoryPlanProcessGroups
                .AsNoTracking()
                .Where(e => e.InventoryPlanId == planId)
                .OrderBy(e => e.SequenceNumber)
                .Select(e => e.ToDto())
                .ToListAsync(),

            4 => await _context.PiercingPlanProcessGroups
                .AsNoTracking()
                .Where(e => e.RoundBarPiercingPlanId == planId)
                .OrderBy(e => e.SequenceNumber)
                .Select(e => e.ToDto())
                .ToListAsync(),

            _ => throw new BusinessException($"无效的用料计划类型: {planType}")
        };
    }

    public async Task SaveAsync(int planType, int planId, List<SavePlanProcessGroupItem> items)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (planType == 1)
            {
                var existing = await _context.SemiPlanProcessGroups
                    .Where(e => e.PurchaseSemiPlanId == planId).ToListAsync();
                _context.SemiPlanProcessGroups.RemoveRange(existing);
                int seq = 1;
                foreach (var item in items)
                {
                    _context.SemiPlanProcessGroups.Add(new SemiPlanProcessGroup
                    {
                        PurchaseSemiPlanId = planId, SequenceNumber = seq++,
                        ProcessName = item.ProcessName, ManufacturingSpec = item.ManufacturingSpec,
                        OuterDiameterTolerance = item.OuterDiameterTolerance, WallThicknessTolerance = item.WallThicknessTolerance,
                        ManufacturingLength = item.ManufacturingLength, CuttingTreatment = item.CuttingTreatment,
                        ManufacturingMultiple = item.ManufacturingMultiple, Remark = item.Remark,
                        ColdRollDraw = item.ColdRollDraw, OilPipeCut = item.OilPipeCut,
                        Degrease = item.Degrease, Solution = item.Solution,
                        Straighten = item.Straighten, Cut = item.Cut,
                        ThicknessMeasure = item.ThicknessMeasure, Pickle = item.Pickle,
                        OuterPolish = item.OuterPolish, InnerGrinding = item.InnerGrinding,
                        OuterSpotGrinding = item.OuterSpotGrinding, Inspection = item.Inspection,
                        WeldingHead = item.WeldingHead, Lubrication = item.Lubrication,
                        Warehouse = item.Warehouse
                    });
                }
            }
            else if (planType == 3)
            {
                var existing = await _context.InventoryPlanProcessGroups
                    .Where(e => e.InventoryPlanId == planId).ToListAsync();
                _context.InventoryPlanProcessGroups.RemoveRange(existing);
                int seq = 1;
                foreach (var item in items)
                {
                    _context.InventoryPlanProcessGroups.Add(new InventoryPlanProcessGroup
                    {
                        InventoryPlanId = planId, SequenceNumber = seq++,
                        ProcessName = item.ProcessName, ManufacturingSpec = item.ManufacturingSpec,
                        OuterDiameterTolerance = item.OuterDiameterTolerance, WallThicknessTolerance = item.WallThicknessTolerance,
                        ManufacturingLength = item.ManufacturingLength, CuttingTreatment = item.CuttingTreatment,
                        ManufacturingMultiple = item.ManufacturingMultiple, Remark = item.Remark,
                        ColdRollDraw = item.ColdRollDraw, OilPipeCut = item.OilPipeCut,
                        Degrease = item.Degrease, Solution = item.Solution,
                        Straighten = item.Straighten, Cut = item.Cut,
                        ThicknessMeasure = item.ThicknessMeasure, Pickle = item.Pickle,
                        OuterPolish = item.OuterPolish, InnerGrinding = item.InnerGrinding,
                        OuterSpotGrinding = item.OuterSpotGrinding, Inspection = item.Inspection,
                        WeldingHead = item.WeldingHead, Lubrication = item.Lubrication,
                        Warehouse = item.Warehouse
                    });
                }
            }
            else if (planType == 4)
            {
                var existing = await _context.PiercingPlanProcessGroups
                    .Where(e => e.RoundBarPiercingPlanId == planId).ToListAsync();
                _context.PiercingPlanProcessGroups.RemoveRange(existing);
                int seq = 1;
                foreach (var item in items)
                {
                    _context.PiercingPlanProcessGroups.Add(new PiercingPlanProcessGroup
                    {
                        RoundBarPiercingPlanId = planId, SequenceNumber = seq++,
                        ProcessName = item.ProcessName, ManufacturingSpec = item.ManufacturingSpec,
                        OuterDiameterTolerance = item.OuterDiameterTolerance, WallThicknessTolerance = item.WallThicknessTolerance,
                        ManufacturingLength = item.ManufacturingLength, CuttingTreatment = item.CuttingTreatment,
                        ManufacturingMultiple = item.ManufacturingMultiple, Remark = item.Remark,
                        ColdRollDraw = item.ColdRollDraw, OilPipeCut = item.OilPipeCut,
                        Degrease = item.Degrease, Solution = item.Solution,
                        Straighten = item.Straighten, Cut = item.Cut,
                        ThicknessMeasure = item.ThicknessMeasure, Pickle = item.Pickle,
                        OuterPolish = item.OuterPolish, InnerGrinding = item.InnerGrinding,
                        OuterSpotGrinding = item.OuterSpotGrinding, Inspection = item.Inspection,
                        WeldingHead = item.WeldingHead, Lubrication = item.Lubrication,
                        Warehouse = item.Warehouse
                    });
                }
            }
            else
            {
                throw new BusinessException($"暂不支持该计划类型的工序组保存: {planType}");
            }

            // 在插入工序组数据之后、SaveChanges 之前计算 StandardCycle，
            // 使一次 SaveChanges 同时持久化工序组变更
            var cycle = await CalculateStandardCycleAsync(planType, planId, items);

            await _context.SaveChangesAsync();

            // StandardCycle 用原始 SQL 写入，彻底绕过 EF Core 跟踪状态问题
            if (cycle > 0)
            {
                var sql = planType switch
                {
                    1 => "UPDATE PurchaseSemiPlan SET StandardCycle = {0} WHERE Id = {1}",
                    3 => "UPDATE InventoryPlan SET StandardCycle = {0} WHERE Id = {1}",
                    4 => "UPDATE RoundBarPiercingPlan SET StandardCycle = {0} WHERE Id = {1}",
                    _ => null
                };
                if (sql != null)
                    await _context.Database.ExecuteSqlRawAsync(sql, cycle, planId);

            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// 从工序组工段数据计算工艺周期天数（不查 StandardProcessCycle）
    /// 返回计算值，<=0 时返回 3 作为默认值
    /// </summary>
    private async Task<int> CalculateStandardCycleAsync(int planType, int planId, List<SavePlanProcessGroupItem> items)
    {
        var defaultCycle = (int)await GetConfigAsync("DefaultValue", "StandardCycle", 3m);
        if (items.Count == 0) return defaultCycle;

        int workOrderId;

        if (planType == 1)
        {
            var plan = await _context.PurchaseSemiPlans.FindAsync(planId);
            if (plan == null) return defaultCycle;
            workOrderId = plan.WorkOrderId;
        }
        else if (planType == 3)
        {
            var plan = await _context.InventoryPlans.FindAsync(planId);
            if (plan == null) return defaultCycle;
            if (plan.ReworkType == null) return defaultCycle;
            workOrderId = plan.WorkOrderId;
        }
        else if (planType == 4)
        {
            var plan = await _context.RoundBarPiercingPlans.FindAsync(planId);
            if (plan == null) return defaultCycle;
            workOrderId = plan.WorkOrderId;
        }
        else
        {
            return defaultCycle;
        }

        var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
        if (workOrder == null) return defaultCycle;

        var allSections = new List<(string, int)>();
        foreach (var item in items)
        {
            allSections.AddRange(MaterialPlanService.ExtractSections(
                item.ColdRollDraw, item.OilPipeCut, item.Degrease, item.Solution,
                item.Straighten, item.Cut, item.ThicknessMeasure, item.Pickle,
                item.OuterPolish, item.InnerGrinding, item.OuterSpotGrinding,
                item.Inspection, item.WeldingHead, item.Lubrication, item.Warehouse));
        }

        var dayMap = await _standardWorkDayService.GetStandardDaysMapAsync(workOrder.PlantGrade);
        var deliveryStateExtraDays = await _deliveryStateService.GetDeliveryStateExtraDaysMapAsync();
        var cycle = MaterialPlanService.CalculateStandardCycleFromSections(
            allSections, dayMap, deliveryStateExtraDays,
            workOrder.DeliveryState.ToString());

        return cycle > 0 ? cycle : defaultCycle;
    }

}
