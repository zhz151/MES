using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Auth;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Configuration;
using MES.Core.DTOs.Equipment;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.Order;
using MES.Core.DTOs.StandardRegister;
using MES.Core.DTOs.Quality;
using MES.Core.DTOs.Scheduling;
using MES.Core.DTOs.Shared;
using MES.Core.DTOs.Warehouse;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Core.Interfaces.Batch;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Interfaces.Equipment;
using MES.Core.Interfaces.Infrastructure;
using MES.Core.Interfaces.Materials;
using MES.Core.Interfaces.Order;
using MES.Core.Interfaces.StandardRegister;
using MES.Core.Interfaces.Quality;
using MES.Core.Interfaces.Scheduling;
using MES.Core.Interfaces.Warehouse;
using MES.Core.Interfaces.WorkOrder;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.WorkOrder;
using MES.Services.Printing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using WoEntity = MES.Data.Entities.WorkOrder.WorkOrder;

namespace MES.Services.WorkOrder;

/// <summary>
/// 用料计划服务实现
/// </summary>
public class MaterialPlanService : IMaterialPlanService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MaterialPlanService> _logger;
    private readonly IStandardWorkDayService _standardWorkDayService;
    private readonly IStandardWorkDayDeliveryStateService _deliveryStateService;
    private readonly IConfigParameterService _configService;
    private readonly IWorkOrderListSummaryRefreshService _readModelRefreshService;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;

    private readonly Dictionary<string, Dictionary<string, decimal>> _configMaps = new();

    /// <summary>
    /// 工厂牌号替代映射（高级可替低级）：key=低级, value=高级
    /// </summary>
    private static readonly Dictionary<string, string> GradeSubstitutes = Core.Constants.GradeSubstitutes.Mapping;

    public MaterialPlanService(AppDbContext context, ILogger<MaterialPlanService> logger,
        IStandardWorkDayService standardWorkDayService,
        IStandardWorkDayDeliveryStateService deliveryStateService,
        IConfigParameterService configService,
        IWorkOrderListSummaryRefreshService readModelRefreshService,
        IWorkOrderExecutionService workOrderExecutionService)
    {
        _context = context;
        _logger = logger;
        _standardWorkDayService = standardWorkDayService;
        _deliveryStateService = deliveryStateService;
        _configService = configService;
        _readModelRefreshService = readModelRefreshService;
        _workOrderExecutionService = workOrderExecutionService;
    }

    #region Config cache

    private async Task<decimal> GetConfigAsync(string category, string key, decimal defaultValue)
    {
        if (!_configMaps.TryGetValue(category, out var map))
        {
            map = await _configService.GetConfigMapAsync(category);
            _configMaps[category] = map;
        }
        return map.GetValueOrDefault(key, defaultValue);
    }

    #endregion

    #region 工艺周期计算（基于工序组）

    /// <summary>
    /// 从工序组工段列表计算工艺周期（天）：累计所有工段天数 + 交货状态附加天数
    /// </summary>
    internal static int CalculateStandardCycleFromSections(
        List<(string SectionName, int Sequence)> sections,
        Dictionary<string, double> dayMap,
        Dictionary<string, double> deliveryStateExtraDays,
        string? deliveryState)
    {
        if (sections.Count == 0) return 0;

        double totalDays = 0;
        foreach (var section in sections)
        {
            var sectionKey = SectionKeys.ToKey(section.SectionName);
            totalDays += sectionKey != null ? dayMap.GetValueOrDefault(sectionKey, 0) : 0;
        }

        // 交货状态调整：从配置表读取附加天数
        if (deliveryStateExtraDays.TryGetValue(deliveryState ?? "", out var dsExtra))
            totalDays += dsExtra;
        else if (deliveryStateExtraDays.TryGetValue("", out var defaultExtra))
            totalDays += defaultExtra;

        return (int)Math.Round(totalDays, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 提取工序组的所有非空工段
    /// </summary>
    internal static List<(string SectionName, int Sequence)> ExtractSections(
        int? coldRollDraw, int? oilPipeCut, int? degrease, int? emulsionWash, int? ultrasonicWash, int? clothPolish,
        int? brightAnnealing, int? solution, int? straighten, int? cut, int? thicknessMeasure, int? pickle,
        int? outerPolish, int? innerPolish, int? innerGrinding, int? outerSpotGrinding, int? sandBlasting,
        int? shotBlasting, int? inspection, int? weldingHead, int? welding, int? lubrication, int? packing,
        int? warehouse, int? extra1, int? extra2)
    {
        var sections = new List<(string, int)>();
        if (coldRollDraw.HasValue) sections.Add((SectionDefs.ColdRollDraw, coldRollDraw.Value));
        if (oilPipeCut.HasValue) sections.Add((SectionDefs.OilPipeCut, oilPipeCut.Value));
        if (degrease.HasValue) sections.Add((SectionDefs.Degrease, degrease.Value));
        if (emulsionWash.HasValue) sections.Add((SectionDefs.EmulsionWash, emulsionWash.Value));
        if (ultrasonicWash.HasValue) sections.Add((SectionDefs.UltrasonicWash, ultrasonicWash.Value));
        if (clothPolish.HasValue) sections.Add((SectionDefs.ClothPolish, clothPolish.Value));
        if (brightAnnealing.HasValue) sections.Add((SectionDefs.BrightAnnealing, brightAnnealing.Value));
        if (solution.HasValue) sections.Add((SectionDefs.Solution, solution.Value));
        if (straighten.HasValue) sections.Add((SectionDefs.Straighten, straighten.Value));
        if (cut.HasValue) sections.Add((SectionDefs.Cut, cut.Value));
        if (thicknessMeasure.HasValue) sections.Add((SectionDefs.ThicknessMeasure, thicknessMeasure.Value));
        if (pickle.HasValue) sections.Add((SectionDefs.Pickle, pickle.Value));
        if (outerPolish.HasValue) sections.Add((SectionDefs.OuterPolish, outerPolish.Value));
        if (innerPolish.HasValue) sections.Add((SectionDefs.InnerPolish, innerPolish.Value));
        if (innerGrinding.HasValue) sections.Add((SectionDefs.InnerGrinding, innerGrinding.Value));
        if (outerSpotGrinding.HasValue) sections.Add((SectionDefs.OuterSpotGrinding, outerSpotGrinding.Value));
        if (sandBlasting.HasValue) sections.Add((SectionDefs.SandBlasting, sandBlasting.Value));
        if (shotBlasting.HasValue) sections.Add((SectionDefs.ShotBlasting, shotBlasting.Value));
        if (inspection.HasValue) sections.Add((SectionDefs.Inspection, inspection.Value));
        if (weldingHead.HasValue) sections.Add((SectionDefs.WeldingHead, weldingHead.Value));
        if (welding.HasValue) sections.Add((SectionDefs.Welding, welding.Value));
        if (lubrication.HasValue) sections.Add((SectionDefs.Lubrication, lubrication.Value));
        if (packing.HasValue) sections.Add((SectionDefs.Packing, packing.Value));
        if (warehouse.HasValue) sections.Add((SectionDefs.Warehouse, warehouse.Value));
        if (extra1.HasValue) sections.Add((SectionDefs.Extra1, extra1.Value));
        if (extra2.HasValue) sections.Add((SectionDefs.Extra2, extra2.Value));
        return sections;
    }

    /// <summary>
    /// 校验工序组不可为空，为空时抛出友好错误
    /// </summary>
    private static void EnsureProcessGroupsNotEmpty(List<SavePlanProcessGroupItem>? items, string message)
    {
        if (items is not { Count: > 0 })
            throw new BusinessException(message);
    }

    /// <summary>
    /// 从工序组请求项提取所有非空工段（供工艺周期内算）
    /// </summary>
    private static List<(string SectionName, int Sequence)> BuildProcessGroupSections(List<SavePlanProcessGroupItem> items)
    {
        var sections = new List<(string, int)>();
        foreach (var pg in items)
        {
            sections.AddRange(ExtractSections(
                pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.EmulsionWash,
                pg.UltrasonicWash, pg.ClothPolish, pg.BrightAnnealing, pg.Solution,
                pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                pg.OuterPolish, pg.InnerPolish, pg.InnerGrinding, pg.OuterSpotGrinding,
                pg.SandBlasting, pg.ShotBlasting, pg.Inspection, pg.WeldingHead,
                pg.Welding, pg.Lubrication, pg.Packing, pg.Warehouse,
                pg.Extra1, pg.Extra2));
        }
        return sections;
    }

    /// <summary>
    /// 按工单交货状态计算工序组工艺周期（天）；算不出返回 0
    /// </summary>
    private async Task<int> ComputeStandardCycleAsync(int workOrderId, List<SavePlanProcessGroupItem> items)
    {
        var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
        if (workOrder == null) return 0;
        var sections = BuildProcessGroupSections(items);
        var dayMap = await _standardWorkDayService.GetStandardDaysMapAsync(workOrder.PlantGrade);
        var deliveryStateExtraDays = await _deliveryStateService.GetDeliveryStateExtraDaysMapAsync();
        return CalculateStandardCycleFromSections(sections, dayMap, deliveryStateExtraDays, workOrder.DeliveryState.ToString());
    }

    /// <summary>
    /// 保存库料改制计划工序组（InventoryPlanProcessGroups 全量替换，需调用方先 RemoveRange 旧行）
    /// </summary>
    private async Task SaveInventoryPlanProcessGroupsAsync(int planId, List<SavePlanProcessGroupItem> items)
    {
        int seq = 1;
        foreach (var pg in items)
        {
            _context.InventoryPlanProcessGroups.Add(new InventoryPlanProcessGroup
            {
                InventoryPlanId = planId,
                SequenceNumber = seq++,
                ProcessName = pg.ProcessName,
                ManufacturingSpec = pg.ManufacturingSpec,
                OuterDiameterTolerance = pg.OuterDiameterTolerance,
                WallThicknessTolerance = pg.WallThicknessTolerance,
                ManufacturingLength = pg.ManufacturingLength,
                CuttingTreatment = pg.CuttingTreatment,
                Remark = pg.Remark,
                ColdRollDraw = pg.ColdRollDraw,
                OilPipeCut = pg.OilPipeCut,
                Degrease = pg.Degrease,
                EmulsionWash = pg.EmulsionWash,
                UltrasonicWash = pg.UltrasonicWash,
                ClothPolish = pg.ClothPolish,
                BrightAnnealing = pg.BrightAnnealing,
                Solution = pg.Solution,
                Straighten = pg.Straighten,
                Cut = pg.Cut,
                ThicknessMeasure = pg.ThicknessMeasure,
                Pickle = pg.Pickle,
                OuterPolish = pg.OuterPolish,
                InnerPolish = pg.InnerPolish,
                InnerGrinding = pg.InnerGrinding,
                OuterSpotGrinding = pg.OuterSpotGrinding,
                SandBlasting = pg.SandBlasting,
                ShotBlasting = pg.ShotBlasting,
                Inspection = pg.Inspection,
                WeldingHead = pg.WeldingHead,
                Welding = pg.Welding,
                Lubrication = pg.Lubrication,
                Packing = pg.Packing,
                Warehouse = pg.Warehouse,
                Extra1 = pg.Extra1,
                Extra2 = pg.Extra2
            });
        }
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 保存在产改制计划工序组（InProcessReworkPlanProcessGroups 全量替换，需调用方先 RemoveRange 旧行）
    /// </summary>
    private async Task SaveInProcessReworkPlanProcessGroupsAsync(int planId, List<SavePlanProcessGroupItem> items)
    {
        int seq = 1;
        foreach (var pg in items)
        {
            _context.InProcessReworkPlanProcessGroups.Add(new InProcessReworkPlanProcessGroup
            {
                InProcessReworkPlanId = planId,
                SequenceNumber = seq++,
                ProcessName = pg.ProcessName,
                ManufacturingSpec = pg.ManufacturingSpec,
                OuterDiameterTolerance = pg.OuterDiameterTolerance,
                WallThicknessTolerance = pg.WallThicknessTolerance,
                ManufacturingLength = pg.ManufacturingLength,
                CuttingTreatment = pg.CuttingTreatment,
                Remark = pg.Remark,
                ColdRollDraw = pg.ColdRollDraw,
                OilPipeCut = pg.OilPipeCut,
                Degrease = pg.Degrease,
                EmulsionWash = pg.EmulsionWash,
                UltrasonicWash = pg.UltrasonicWash,
                ClothPolish = pg.ClothPolish,
                BrightAnnealing = pg.BrightAnnealing,
                Solution = pg.Solution,
                Straighten = pg.Straighten,
                Cut = pg.Cut,
                ThicknessMeasure = pg.ThicknessMeasure,
                Pickle = pg.Pickle,
                OuterPolish = pg.OuterPolish,
                InnerPolish = pg.InnerPolish,
                InnerGrinding = pg.InnerGrinding,
                OuterSpotGrinding = pg.OuterSpotGrinding,
                SandBlasting = pg.SandBlasting,
                ShotBlasting = pg.ShotBlasting,
                Inspection = pg.Inspection,
                WeldingHead = pg.WeldingHead,
                Welding = pg.Welding,
                Lubrication = pg.Lubrication,
                Packing = pg.Packing,
                Warehouse = pg.Warehouse,
                Extra1 = pg.Extra1,
                Extra2 = pg.Extra2
            });
        }
        await _context.SaveChangesAsync();
    }

    #endregion

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

        // 非定尺: 支数不能为空
        if (workOrder.LengthStatus == LengthStatus.NonFixed && request.RequiredPieces == null)
            throw new BusinessException("非定尺模式下需求支数为必填");

        // 执行测算
        var calc = await CalculateInternalAsync(workOrder, request);

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
            PlantGrade = request.PlantGrade,
            RawMaterialType = request.RawMaterialType,
            RawMaterialSpec = request.RawMaterialSpec,
            RequiredUnitWeight = request.RequiredUnitWeight,
            RequiredPieces = request.RequiredPieces,
            RequiredWeight = request.RequiredWeight,
            RequiredDate = request.RequiredDate,
            Remark = request.Remark,
        };

        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                // 无工序组不可提交（工艺工量需工序组内算）
                EnsureProcessGroupsNotEmpty(request.ProcessGroups, "荒管采购计划必须填写工序组");

                _context.PurchaseSemiPlans.Add(plan);
                await _context.SaveChangesAsync();

                // 保存工序组
                if (request.ProcessGroups is { Count: > 0 })
                {
                    int seq = 1;
                    foreach (var pg in request.ProcessGroups)
                    {
                        _context.SemiPlanProcessGroups.Add(new SemiPlanProcessGroup
                        {
                            PurchaseSemiPlanId = plan.Id,
                            SequenceNumber = seq++,
                            ProcessName = pg.ProcessName,
                            ManufacturingSpec = pg.ManufacturingSpec,
                            OuterDiameterTolerance = pg.OuterDiameterTolerance,
                            WallThicknessTolerance = pg.WallThicknessTolerance,
                            ManufacturingLength = pg.ManufacturingLength,
                            CuttingTreatment = pg.CuttingTreatment,
                            Remark = pg.Remark,
                            ColdRollDraw = pg.ColdRollDraw,
                            OilPipeCut = pg.OilPipeCut,
                            Degrease = pg.Degrease,
                            EmulsionWash = pg.EmulsionWash,
                            UltrasonicWash = pg.UltrasonicWash,
                            ClothPolish = pg.ClothPolish,
                            BrightAnnealing = pg.BrightAnnealing,
                            Solution = pg.Solution,
                            Straighten = pg.Straighten,
                            Cut = pg.Cut,
                            ThicknessMeasure = pg.ThicknessMeasure,
                            Pickle = pg.Pickle,
                            OuterPolish = pg.OuterPolish,
                            InnerPolish = pg.InnerPolish,
                            InnerGrinding = pg.InnerGrinding,
                            OuterSpotGrinding = pg.OuterSpotGrinding,
                            SandBlasting = pg.SandBlasting,
                            ShotBlasting = pg.ShotBlasting,
                            Inspection = pg.Inspection,
                            WeldingHead = pg.WeldingHead,
                            Welding = pg.Welding,
                            Lubrication = pg.Lubrication,
                            Packing = pg.Packing,
                            Warehouse = pg.Warehouse,
                            Extra1 = pg.Extra1,
                            Extra2 = pg.Extra2
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                // 从工序组计算工艺周期
                var semiGroups = await _context.SemiPlanProcessGroups
                    .Where(g => g.PurchaseSemiPlanId == plan.Id)
                    .ToListAsync();
                var semiSections = new List<(string, int)>();
                foreach (var pg in semiGroups)
                {
                    semiSections.AddRange(ExtractSections(
                        pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.EmulsionWash,
                        pg.UltrasonicWash, pg.ClothPolish, pg.BrightAnnealing, pg.Solution,
                        pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                        pg.OuterPolish, pg.InnerPolish, pg.InnerGrinding, pg.OuterSpotGrinding,
                        pg.SandBlasting, pg.ShotBlasting, pg.Inspection, pg.WeldingHead,
                        pg.Welding, pg.Lubrication, pg.Packing, pg.Warehouse,
                        pg.Extra1, pg.Extra2));
                }
                var dayMap = await _standardWorkDayService.GetStandardDaysMapAsync(workOrder.PlantGrade);
                var deliveryStateExtraDays = await _deliveryStateService.GetDeliveryStateExtraDaysMapAsync();
                plan.StandardCycle = CalculateStandardCycleFromSections(
                    semiSections, dayMap, deliveryStateExtraDays,
                    workOrder.DeliveryState.ToString());
                if (plan.StandardCycle == 0)
                    throw new BusinessException("工艺周期计算失败：工序组工段数据不完整，无法计算工艺周期");
                _context.Entry(plan).Property(e => e.StandardCycle).IsModified = true;
                await _context.SaveChangesAsync();

                // 刷新工单状态（与创建在同一事务中）
                await UpdateMaterialPlanStatusAsync(request.WorkOrderId);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(request.WorkOrderId);

        _logger.LogInformation("创建原料采购计划成功: 工单ID {WorkOrderId}, 原料规格 {Spec}, 重量 {Weight}",
            request.WorkOrderId, request.RawMaterialSpec, request.RequiredWeight);

        return plan.ToDto();
    }

    public async Task DeleteSemiPlanAsync(int id)
    {
        var plan = await _context.PurchaseSemiPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("原料采购计划不存在");

        var workOrderId = plan.WorkOrderId;
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                _context.PurchaseSemiPlans.Remove(plan);
                await _context.SaveChangesAsync();

                // 刷新工单状态（与删除在同一事务中）
                await UpdateMaterialPlanStatusAsync(workOrderId);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(workOrderId);

        _logger.LogInformation("删除原料采购计划成功: ID {Id}", id);
    }

    public async Task<PurchaseSemiPlanDto> UpdateSemiPlanAsync(int id, CreatePurchaseSemiPlanRequest request)
    {
        var plan = await _context.PurchaseSemiPlans.FindAsync(id)
            ?? throw new BusinessException("原料采购计划不存在");

        // 更新测算参数
        plan.AdjustedWallThickness = request.AdjustedWallThickness;
        plan.YieldRate = request.YieldRate;
        plan.InputMultiple = request.InputMultiple;
        plan.QualifiedRate = request.QualifiedRate;
        plan.RawMaterialType = request.RawMaterialType;

        // 更新原料信息
        plan.PlantGrade = request.PlantGrade;
        plan.RawMaterialSpec = request.RawMaterialSpec;
        plan.RequiredUnitWeight = request.RequiredUnitWeight;
        plan.RequiredPieces = request.RequiredPieces;
        plan.RequiredWeight = request.RequiredWeight;
        plan.RequiredDate = request.RequiredDate;
        plan.Remark = request.Remark;

        var workOrder = await _context.WorkOrders.FindAsync(plan.WorkOrderId)
            ?? throw new BusinessException("关联工单不存在");

        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                // 无工序组不可提交（工艺工量需工序组内算）
                EnsureProcessGroupsNotEmpty(request.ProcessGroups, "荒管采购计划必须填写工序组");

                // 全量替换工序组
                var existingGroups = await _context.SemiPlanProcessGroups
                    .Where(g => g.PurchaseSemiPlanId == id)
                    .ToListAsync();
                _context.SemiPlanProcessGroups.RemoveRange(existingGroups);

                if (request.ProcessGroups is { Count: > 0 })
                {
                    int seq = 1;
                    foreach (var pg in request.ProcessGroups)
                    {
                        _context.SemiPlanProcessGroups.Add(new SemiPlanProcessGroup
                        {
                            PurchaseSemiPlanId = id,
                            SequenceNumber = seq++,
                            ProcessName = pg.ProcessName,
                            ManufacturingSpec = pg.ManufacturingSpec,
                            OuterDiameterTolerance = pg.OuterDiameterTolerance,
                            WallThicknessTolerance = pg.WallThicknessTolerance,
                            ManufacturingLength = pg.ManufacturingLength,
                            CuttingTreatment = pg.CuttingTreatment,
                            Remark = pg.Remark,
                            ColdRollDraw = pg.ColdRollDraw,
                            OilPipeCut = pg.OilPipeCut,
                            Degrease = pg.Degrease,
                            EmulsionWash = pg.EmulsionWash,
                            UltrasonicWash = pg.UltrasonicWash,
                            ClothPolish = pg.ClothPolish,
                            BrightAnnealing = pg.BrightAnnealing,
                            Solution = pg.Solution,
                            Straighten = pg.Straighten,
                            Cut = pg.Cut,
                            ThicknessMeasure = pg.ThicknessMeasure,
                            Pickle = pg.Pickle,
                            OuterPolish = pg.OuterPolish,
                            InnerPolish = pg.InnerPolish,
                            InnerGrinding = pg.InnerGrinding,
                            OuterSpotGrinding = pg.OuterSpotGrinding,
                            SandBlasting = pg.SandBlasting,
                            ShotBlasting = pg.ShotBlasting,
                            Inspection = pg.Inspection,
                            WeldingHead = pg.WeldingHead,
                            Welding = pg.Welding,
                            Lubrication = pg.Lubrication,
                            Packing = pg.Packing,
                            Warehouse = pg.Warehouse,
                            Extra1 = pg.Extra1,
                            Extra2 = pg.Extra2
                        });
                    }
                }

                await _context.SaveChangesAsync();

                // 从工序组重新计算工艺周期
                var semiGroups = await _context.SemiPlanProcessGroups
                    .Where(g => g.PurchaseSemiPlanId == id)
                    .ToListAsync();
                var semiSections = new List<(string, int)>();
                foreach (var pg in semiGroups)
                {
                    semiSections.AddRange(ExtractSections(
                        pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.EmulsionWash,
                        pg.UltrasonicWash, pg.ClothPolish, pg.BrightAnnealing, pg.Solution,
                        pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                        pg.OuterPolish, pg.InnerPolish, pg.InnerGrinding, pg.OuterSpotGrinding,
                        pg.SandBlasting, pg.ShotBlasting, pg.Inspection, pg.WeldingHead,
                        pg.Welding, pg.Lubrication, pg.Packing, pg.Warehouse,
                        pg.Extra1, pg.Extra2));
                }
                var dayMap = await _standardWorkDayService.GetStandardDaysMapAsync(workOrder.PlantGrade);
                var deliveryStateExtraDays = await _deliveryStateService.GetDeliveryStateExtraDaysMapAsync();
                plan.StandardCycle = CalculateStandardCycleFromSections(
                    semiSections, dayMap, deliveryStateExtraDays,
                    workOrder.DeliveryState.ToString());
                if (plan.StandardCycle == 0)
                    throw new BusinessException("工艺周期计算失败：工序组工段数据不完整，无法计算工艺周期");
                _context.Entry(plan).Property(e => e.StandardCycle).IsModified = true;
                await _context.SaveChangesAsync();

                await UpdateMaterialPlanStatusAsync(plan.WorkOrderId);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(plan.WorkOrderId);

        _logger.LogInformation("更新原料采购计划成功: ID {Id}", id);
        return plan.ToDto();
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

        var defaultStandardCycle = (int)await GetConfigAsync("DefaultValue", "StandardCycle", 3m);
        var plan = new PurchaseFinishedPlan
        {
            WorkOrderId = request.WorkOrderId,
            PlanDate = request.PlanDate,
            ProductType = request.ProductType,
            RequiredPiece = request.RequiredPiece,
            RequiredWeight = request.RequiredWeight,
            InputMultiple = request.InputMultiple,
            RequiredDate = request.RequiredDate,
            Remark = request.Remark,
            PlantGrade = request.PlantGrade,
            Specification = request.Specification,
            OuterDiameterNegative = request.OuterDiameterNegative,
            OuterDiameterPositive = request.OuterDiameterPositive,
            WallThicknessNegative = request.WallThicknessNegative,
            WallThicknessPositive = request.WallThicknessPositive,
            LengthStatus = request.LengthStatus,
            MinLength = request.MinLength,
            MaxLength = request.MaxLength,
            DeliveryState = request.DeliveryState,
            StandardCycle = defaultStandardCycle // 成品采购默认天数
        };

        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                _context.PurchaseFinishedPlans.Add(plan);
                await _context.SaveChangesAsync();

                // 刷新工单状态（与创建在同一事务中）
                await UpdateMaterialPlanStatusAsync(request.WorkOrderId);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(request.WorkOrderId);

        _logger.LogInformation("创建成品采购计划成功: 工单ID {WorkOrderId}, 重量 {Weight}",
            request.WorkOrderId, request.RequiredWeight);

        return plan.ToDto();
    }

    public async Task<List<PurchaseFinishedPlanDto>> CreateFinishedPlanBatchAsync(List<CreatePurchaseFinishedPlanRequest> requests)
    {
        if (requests.Count == 0)
            return new List<PurchaseFinishedPlanDto>();

        // 校验所有请求属于同一工单
        var distinctWorkOrderIds = requests.Select(r => r.WorkOrderId).Distinct().ToList();
        if (distinctWorkOrderIds.Count > 1)
            throw new BusinessException("批量创建成品采购计划时所有记录必须属于同一工单");

        var workOrderId = distinctWorkOrderIds[0];
        var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        var plans = new List<PurchaseFinishedPlan>();
        var defaultStandardCycle = (int)await GetConfigAsync("DefaultValue", "StandardCycle", 3m);
        foreach (var request in requests)
        {
            if (workOrder.LengthStatus == LengthStatus.Fixed && (request.RequiredPiece == null || request.RequiredPiece <= 0))
                throw new BusinessException("定尺模式下采购支数不能为空");

            plans.Add(new PurchaseFinishedPlan
            {
                WorkOrderId = workOrderId,
                PlanDate = request.PlanDate,
                ProductType = request.ProductType,
                RequiredPiece = request.RequiredPiece,
                RequiredWeight = request.RequiredWeight,
                InputMultiple = request.InputMultiple,
                RequiredDate = request.RequiredDate,
                Remark = request.Remark,
                PlantGrade = request.PlantGrade,
                Specification = request.Specification,
                OuterDiameterNegative = request.OuterDiameterNegative,
                OuterDiameterPositive = request.OuterDiameterPositive,
                WallThicknessNegative = request.WallThicknessNegative,
                WallThicknessPositive = request.WallThicknessPositive,
                LengthStatus = request.LengthStatus,
                MinLength = request.MinLength,
                MaxLength = request.MaxLength,
                DeliveryState = request.DeliveryState,
                StandardCycle = defaultStandardCycle // 成品采购默认天数
            });
        }

        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                _context.PurchaseFinishedPlans.AddRange(plans);
                await _context.SaveChangesAsync();
                await UpdateMaterialPlanStatusAsync(workOrderId);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(workOrderId);

        _logger.LogInformation("批量创建成品采购计划成功: 工单ID {WorkOrderId}, 共 {Count} 条", workOrderId, plans.Count);
        return plans.Select(p => p.ToDto()).ToList();
    }

    public async Task<PurchaseFinishedPlanDto> UpdateFinishedPlanAsync(int id, CreatePurchaseFinishedPlanRequest request)
    {
        var plan = await _context.PurchaseFinishedPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("成品采购计划不存在");

        var workOrder = await _context.WorkOrders.FindAsync(plan.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("关联工单不存在");

        // 定尺：支数必须填写
        if (workOrder.LengthStatus == LengthStatus.Fixed && (request.RequiredPiece == null || request.RequiredPiece <= 0))
            throw new BusinessException("定尺模式下采购支数不能为空");

        // 更新字段
        plan.PlanDate = request.PlanDate;
        plan.ProductType = request.ProductType;
        plan.RequiredPiece = request.RequiredPiece;
        plan.RequiredWeight = request.RequiredWeight;
        plan.InputMultiple = request.InputMultiple;
        plan.RequiredDate = request.RequiredDate;
        plan.Remark = request.Remark;
        plan.PlantGrade = request.PlantGrade;
        plan.Specification = request.Specification;
        plan.OuterDiameterNegative = request.OuterDiameterNegative;
        plan.OuterDiameterPositive = request.OuterDiameterPositive;
        plan.WallThicknessNegative = request.WallThicknessNegative;
        plan.WallThicknessPositive = request.WallThicknessPositive;
        plan.LengthStatus = request.LengthStatus;
        plan.MinLength = request.MinLength;
        plan.MaxLength = request.MaxLength;
        plan.DeliveryState = request.DeliveryState;

        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                await _context.SaveChangesAsync();
                await UpdateMaterialPlanStatusAsync(plan.WorkOrderId);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(plan.WorkOrderId);

        _logger.LogInformation("更新成品采购计划成功: ID {Id}", id);
        return plan.ToDto();
    }

    public async Task DeleteFinishedPlanAsync(int id)
    {
        var plan = await _context.PurchaseFinishedPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("成品采购计划不存在");

        var workOrderId = plan.WorkOrderId;
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                _context.PurchaseFinishedPlans.Remove(plan);
                await _context.SaveChangesAsync();

                // 刷新工单状态（与删除在同一事务中）
                await UpdateMaterialPlanStatusAsync(workOrderId);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(workOrderId);

        _logger.LogInformation("删除成品采购计划成功: ID {Id}", id);
    }

    #endregion

    #region 库存使用计划

    public async Task<List<InventoryPlanDto>> GetInventoryPlansAsync(int workOrderId)
    {
        var plans = await _context.InventoryPlans
            .Where(p => p.WorkOrderId == workOrderId && p.ReworkType == null)
            .OrderByDescending(p => p.CreatedTime)
            .ToListAsync();

        var workOrderNo = await _context.WorkOrders.AsNoTracking()
            .Where(w => w.Id == workOrderId)
            .Select(w => w.WorkOrderNo)
            .FirstOrDefaultAsync();
        var dtos = plans.Select(p => p.ToDto()).ToList();
        await MarkOutboundAsync(dtos, workOrderNo);
        return dtos;
    }

    public async Task<List<InventoryPlanDto>> GetReworkPlansAsync(int workOrderId)
    {
        var plans = await _context.InventoryPlans
            .Where(p => p.WorkOrderId == workOrderId && p.ReworkType != null)
            .OrderByDescending(p => p.CreatedTime)
            .ToListAsync();

        var workOrderNo = await _context.WorkOrders.AsNoTracking()
            .Where(w => w.Id == workOrderId)
            .Select(w => w.WorkOrderNo)
            .FirstOrDefaultAsync();
        var dtos = plans.Select(p => p.ToDto()).ToList();
        await MarkOutboundAsync(dtos, workOrderNo);
        return dtos;
    }

    /// <summary>
    /// 批量标记计划是否已生产领用出库（完成匹配：仓库批 + 出库工单号 == 计划工单号，出库类型=生产领用）
    /// </summary>
    private async Task MarkOutboundAsync(List<InventoryPlanDto> dtos, string? workOrderNo)
    {
        if (dtos.Count == 0) return;
        var batchNos = dtos.Select(d => d.InventoryBatchNo).Distinct().ToList();
        var outboundBatchNos = await _context.OutboundRecords
            .Where(or => or.BatchNo != null
                && batchNos.Contains(or.BatchNo)
                && or.OutboundType == OutboundType.ProductionPick
                && or.WorkOrderNo != null
                && or.WorkOrderNo == workOrderNo)
            .Select(or => or.BatchNo!)
            .Distinct()
            .ToListAsync();
        var set = outboundBatchNos.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var dto in dtos)
            dto.IsOutbound = set.Contains(dto.InventoryBatchNo);
    }

    public async Task<InventoryPlanDto> CreateInventoryPlanAsync(CreateInventoryPlanRequest request)
    {
        var workOrder = await _context.WorkOrders.FindAsync(request.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        var batch = await _context.InventoryBatches
            .FirstOrDefaultAsync(b => b.BatchNo == request.InventoryBatchNo);
        if (batch == null)
            throw new BusinessException("库存批次不存在");

        // 校验锁定与预留（出库计划视为执行完成不再占用，剩余料可被再次计划利用）
        // 全部使用(All)：批次上存在任一未取消未出库计划（含本工单自身的部分预留）→ 禁止全部使用（All 语义=清空整批）
        // 部分使用(Partial)：批次上存在"全部使用"计划 → 整批锁定禁止；否则累计预留 + 本次 ≤ 批次剩余
        var activePlans = await _context.InventoryPlans
            .Where(p => p.InventoryBatchNo == request.InventoryBatchNo
                && p.PlanStatus != InventoryPlanStatus.Cancelled
                && !_context.OutboundRecords.Any(or =>
                    or.BatchNo != null && or.BatchNo == p.InventoryBatchNo
                    && or.OutboundType == OutboundType.ProductionPick
                    && or.WorkOrderNo != null
                    && or.WorkOrderNo == _context.WorkOrders
                        .Where(w => w.Id == p.WorkOrderId)
                        .Select(w => w.WorkOrderNo)
                        .FirstOrDefault()))
            .Select(p => new { p.WorkOrderId, p.UsageMode, p.UsedQuantity, p.UsedWeight })
            .ToListAsync();

        if (request.UsageMode == "All")
        {
            if (activePlans.Count > 0)
                throw new BusinessException("该库存批次已被使用计划引用（含部分使用预留），不可全部使用");
            request.UsedQuantity = batch.RemainingQuantity;
            request.UsedWeight = batch.RemainingWeight;
        }
        else
        {
            if (activePlans.Any(p => p.UsageMode == "All"))
                throw new BusinessException("该库存批次已被全部使用计划占用，不可部分使用");
            if (request.UsedQuantity == null || request.UsedQuantity <= 0)
                throw new BusinessException("部分使用模式下出库支数必须大于0");
            if (request.UsedWeight <= 0)
                throw new BusinessException("出库重量必须大于0");

            var reservedQty = activePlans.Where(p => p.UsageMode == "Partial").Sum(p => p.UsedQuantity ?? 0);
            var reservedWt = activePlans.Where(p => p.UsageMode == "Partial").Sum(p => p.UsedWeight);
            var availableQty = batch.RemainingQuantity - reservedQty;
            var availableWt = batch.RemainingWeight - reservedWt;
            if (request.UsedQuantity.Value > availableQty)
                throw new BusinessException($"出库支数({request.UsedQuantity})超过剩余可用支数({availableQty})");
            if (request.UsedWeight > availableWt)
                throw new BusinessException($"出库重量({request.UsedWeight})超过剩余可用重量({availableWt.ToString("G29")})");
        }

        var plan = new InventoryPlan
        {
            WorkOrderId = request.WorkOrderId,
            PlanDate = request.PlanDate,
            InventoryBatchNo = request.InventoryBatchNo,
            BatchNo = batch.BatchNo,
            MaterialType = batch.MaterialType,
            PlantGrade = batch.PlantGrade,
            Specification = batch.Specification,
            LocationArea = batch.LocationArea,
            LocationRack = batch.LocationRack,
            InputMultiple = request.InputMultiple,
            UsageMode = request.UsageMode,
            UsedQuantity = request.UsedQuantity,
            UsedWeight = request.UsedWeight,
            RequiredDate = request.RequiredDate,
            PlanStatus = InventoryPlanStatus.Planned,
            Remark = request.Remark,
            ReworkType = request.ReworkType,
        };

        var defaultStandardCycle = (int)await GetConfigAsync("DefaultValue", "StandardCycle", 3m);
        var defaultProcessCycle = (int)await GetConfigAsync("DefaultValue", "DefaultProcessCycle", 22m);
        // 工艺周期：库料改制（有 ReworkType）必须填写工序组，创建请求内算；库存使用默认 3 天
        if (request.ReworkType.HasValue)
        {
            EnsureProcessGroupsNotEmpty(request.ProcessGroups, "库料改制必须填写工序组");
            plan.StandardCycle = await ComputeStandardCycleAsync(request.WorkOrderId, request.ProcessGroups!);
            if (plan.StandardCycle == 0)
                plan.StandardCycle = defaultProcessCycle;
        }
        else
        {
            plan.StandardCycle = defaultStandardCycle;
        }

        _context.InventoryPlans.Add(plan);
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                await _context.SaveChangesAsync();

                // 库料改制：随创建请求保存工序组（与荒管采购创建请求内算一致）
                if (request.ReworkType.HasValue && request.ProcessGroups is { Count: > 0 })
                    await SaveInventoryPlanProcessGroupsAsync(plan.Id, request.ProcessGroups);

                // 刷新工单状态（与创建在同一事务中）
                await UpdateMaterialPlanStatusAsync(request.WorkOrderId);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(request.WorkOrderId);

        _logger.LogInformation("创建库存使用计划成功: 工单ID {WorkOrderId}, 批次号 {BatchNo}, 重量 {Weight}",
            request.WorkOrderId, batch.BatchNo, request.UsedWeight);

        return plan.ToDto();
    }

    public async Task<List<InventoryPlanDto>> CreateInventoryPlanBatchAsync(List<CreateInventoryPlanRequest> requests)
    {
        if (requests.Count == 0)
            return new List<InventoryPlanDto>();

        // 校验所有请求属于同一工单
        var distinctWorkOrderIds = requests.Select(r => r.WorkOrderId).Distinct().ToList();
        if (distinctWorkOrderIds.Count > 1)
            throw new BusinessException("批量创建库存使用计划时所有记录必须属于同一工单");

        var workOrderId = distinctWorkOrderIds[0];
        var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        // 一次性加载所有库存批次
        var batchNos = requests.Select(r => r.InventoryBatchNo).Distinct().ToList();
        var batches = await _context.InventoryBatches
            .Where(b => batchNos.Contains(b.BatchNo))
            .ToDictionaryAsync(b => b.BatchNo);

        // 加载批次涉及的所有有效计划（未取消且未出库），用于锁定与预留校验
        var activePlans = await _context.InventoryPlans
            .Where(p => batchNos.Contains(p.InventoryBatchNo)
                && p.PlanStatus != InventoryPlanStatus.Cancelled
                && !_context.OutboundRecords.Any(or =>
                    or.BatchNo != null && or.BatchNo == p.InventoryBatchNo
                    && or.OutboundType == OutboundType.ProductionPick
                    && or.WorkOrderNo != null
                    && or.WorkOrderNo == _context.WorkOrders
                        .Where(w => w.Id == p.WorkOrderId)
                        .Select(w => w.WorkOrderNo)
                        .FirstOrDefault()))
            .Select(p => new { p.WorkOrderId, p.InventoryBatchNo, p.UsageMode, p.UsedQuantity, p.UsedWeight })
            .ToListAsync();

        // 批次上存在任一有效计划（All 或 Partial，含本工单自身的部分预留）→ 禁止对该批次"全部使用"
        var anyLockNos = activePlans
            .Select(p => p.InventoryBatchNo)
            .Distinct()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        // 批次上存在"全部使用"计划 → 整批锁定，禁止部分使用
        var allLockNos = activePlans
            .Where(p => p.UsageMode == "All")
            .Select(p => p.InventoryBatchNo)
            .Distinct()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        // 现有"部分使用"计划预留账本（所有工单）
        var reservedMap = activePlans
            .Where(p => p.UsageMode == "Partial")
            .GroupBy(p => p.InventoryBatchNo)
            .ToDictionary(g => g.Key,
                g => (Qty: g.Sum(p => p.UsedQuantity ?? 0), Wt: g.Sum(p => p.UsedWeight)),
                StringComparer.OrdinalIgnoreCase);

        var plans = new List<InventoryPlan>();
        var defaultStandardCycle = (int)await GetConfigAsync("DefaultValue", "StandardCycle", 3m);
        var defaultProcessCycle = (int)await GetConfigAsync("DefaultValue", "DefaultProcessCycle", 22m);
        // 请求内同批次累计跟踪（同一批次 All/Partial 不可混用，Partial 可多条累计）
        var inRequestSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inRequestAllSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inRequestReserved = new Dictionary<string, (int Qty, decimal Wt)>(StringComparer.OrdinalIgnoreCase);
        foreach (var request in requests)
        {
            if (!batches.TryGetValue(request.InventoryBatchNo, out var batch))
                throw new BusinessException($"库存批次不存在: {request.InventoryBatchNo}");

            // 校验用量（含锁定与累计预留）
            if (request.UsageMode == "All")
            {
                if (inRequestSeen.Contains(request.InventoryBatchNo))
                    throw new BusinessException($"批次 {request.InventoryBatchNo}：同一批次只能选择全部使用或部分使用中的一种");
                if (anyLockNos.Contains(request.InventoryBatchNo))
                    throw new BusinessException($"批次 {request.InventoryBatchNo}：已被库存使用计划引用（含部分使用预留），不可全部使用");
                request.UsedQuantity = batch.RemainingQuantity;
                request.UsedWeight = batch.RemainingWeight;
                inRequestAllSeen.Add(request.InventoryBatchNo);
                inRequestReserved[request.InventoryBatchNo] = (batch.RemainingQuantity, batch.RemainingWeight);
            }
            else
            {
                if (inRequestAllSeen.Contains(request.InventoryBatchNo))
                    throw new BusinessException($"批次 {request.InventoryBatchNo}：同一批次只能选择全部使用或部分使用中的一种");
                if (allLockNos.Contains(request.InventoryBatchNo))
                    throw new BusinessException($"批次 {request.InventoryBatchNo}：已被库存使用计划全部占用，不可部分使用");
                if (request.UsedQuantity == null || request.UsedQuantity <= 0)
                    throw new BusinessException($"批次 {request.InventoryBatchNo}：部分使用模式下出库支数必须大于0");
                if (request.UsedWeight <= 0)
                    throw new BusinessException($"批次 {request.InventoryBatchNo}：出库重量必须大于0");

                reservedMap.TryGetValue(request.InventoryBatchNo, out var existingReserved);
                inRequestReserved.TryGetValue(request.InventoryBatchNo, out var acc);
                var availableQty = batch.RemainingQuantity - existingReserved.Qty - acc.Qty;
                var availableWt = batch.RemainingWeight - existingReserved.Wt - acc.Wt;
                if (request.UsedQuantity.Value > availableQty)
                    throw new BusinessException($"批次 {request.InventoryBatchNo}：出库支数({request.UsedQuantity})超过剩余可用支数({availableQty})");
                if (request.UsedWeight > availableWt)
                    throw new BusinessException($"批次 {request.InventoryBatchNo}：出库重量({request.UsedWeight})超过剩余可用重量({availableWt.ToString("G29")})");
                inRequestReserved[request.InventoryBatchNo] = (acc.Qty + request.UsedQuantity.Value, acc.Wt + request.UsedWeight);
            }
            inRequestSeen.Add(request.InventoryBatchNo);

            var plan = new InventoryPlan
            {
                WorkOrderId = workOrderId,
                PlanDate = request.PlanDate,
                InventoryBatchNo = request.InventoryBatchNo,
                BatchNo = batch.BatchNo,
                MaterialType = batch.MaterialType,
                PlantGrade = batch.PlantGrade,
                Specification = batch.Specification,
                LocationArea = batch.LocationArea,
                LocationRack = batch.LocationRack,
                InputMultiple = request.InputMultiple,
                UsageMode = request.UsageMode,
                UsedQuantity = request.UsedQuantity,
                UsedWeight = request.UsedWeight,
                RequiredDate = request.RequiredDate,
                PlanStatus = InventoryPlanStatus.Planned,
                Remark = request.Remark,
                ReworkType = request.ReworkType,
            };

            // 工艺周期：库料改制（有 ReworkType）必须填写工序组，创建请求内算；库存使用默认 3 天
            if (request.ReworkType.HasValue)
            {
                EnsureProcessGroupsNotEmpty(request.ProcessGroups, "库料改制必须填写工序组");
                plan.StandardCycle = await ComputeStandardCycleAsync(workOrderId, request.ProcessGroups!);
                if (plan.StandardCycle == 0)
                    plan.StandardCycle = defaultProcessCycle;
            }
            else
            {
                plan.StandardCycle = defaultStandardCycle;
            }

            plans.Add(plan);
        }

        _context.InventoryPlans.AddRange(plans);
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                await _context.SaveChangesAsync();

                // 库料改制：为每个新计划随创建请求保存工序组（同一份工序组克隆，与批量多选共享语义一致）
                for (int i = 0; i < plans.Count; i++)
                {
                    if (requests[i].ReworkType.HasValue && requests[i].ProcessGroups is { Count: > 0 })
                        await SaveInventoryPlanProcessGroupsAsync(plans[i].Id, requests[i].ProcessGroups!);
                }

                await UpdateMaterialPlanStatusAsync(workOrderId);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(workOrderId);

        _logger.LogInformation("批量创建库存使用计划成功: 工单ID {WorkOrderId}, 共 {Count} 条", workOrderId, plans.Count);
        return plans.Select(p => p.ToDto()).ToList();
    }

    /// <summary>
    /// 判断库存批次是否已生产领用出库（完成匹配：仓库批 + 出库工单号 == 计划工单号，出库类型=生产领用）
    /// 已出库的库存使用/库料改制计划视为执行完成：不可修改，删除已放宽（2026-08-10 决策）
    /// </summary>
    private Task<bool> IsInventoryPlanOutboundAsync(string inventoryBatchNo, string? workOrderNo)
    {
        return _context.OutboundRecords.AnyAsync(or =>
            or.BatchNo != null
            && or.BatchNo == inventoryBatchNo
            && or.OutboundType == OutboundType.ProductionPick
            && or.WorkOrderNo != null
            && or.WorkOrderNo == workOrderNo);
    }

    public async Task DeleteInventoryPlanAsync(int id)
    {
        var plan = await _context.InventoryPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("库存使用计划不存在");

        // 放宽：即使批次已生产领用出库也允许删除（出库记录独立保留，删除计划仅释放预留并刷新工单状态）
        var workOrderId = plan.WorkOrderId;
        _context.InventoryPlans.Remove(plan);
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                await _context.SaveChangesAsync();

                // 刷新工单状态（与删除在同一事务中）
                await UpdateMaterialPlanStatusAsync(workOrderId);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(workOrderId);

        _logger.LogInformation("删除库存使用计划成功: ID {Id}", id);
    }

    /// <summary>
    /// 统计各批次已被"部分使用"计划预留的支数/重量（仅统计未取消且未出库的部分使用计划）。
    /// 用于可用库存列表展示"可用量 = 物理剩余 - 已预留"及新建计划的累计预留校验。
    /// </summary>
    private async Task<Dictionary<string, (int ReservedQuantity, decimal ReservedWeight)>> GetPartialReservedMapAsync(int? excludePlanId)
    {
        var query = _context.InventoryPlans
            .Where(p => p.PlanStatus != InventoryPlanStatus.Cancelled
                && p.UsageMode == "Partial"
                && !_context.OutboundRecords.Any(or =>
                    or.BatchNo != null && or.BatchNo == p.InventoryBatchNo
                    && or.OutboundType == OutboundType.ProductionPick
                    && or.WorkOrderNo != null
                    && or.WorkOrderNo == _context.WorkOrders
                        .Where(w => w.Id == p.WorkOrderId)
                        .Select(w => w.WorkOrderNo)
                        .FirstOrDefault()));

        if (excludePlanId.HasValue)
        {
            query = query.Where(p => p.Id != excludePlanId.Value);
        }

        var rows = await query
            .GroupBy(p => p.InventoryBatchNo)
            .Select(g => new
            {
                BatchNo = g.Key,
                Qty = g.Sum(p => p.UsedQuantity),
                Wt = g.Sum(p => p.UsedWeight)
            })
            .ToListAsync();

        return rows.ToDictionary(
            x => x.BatchNo,
            x => (ReservedQuantity: x.Qty ?? 0, ReservedWeight: x.Wt),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<List<AvailableInventoryBatchDto>> GetAvailableInventoryAsync(int workOrderId, int? excludePlanId = null)
    {
        var workOrder = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == workOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        // 解析外径和壁厚
        var odOrNull = SpecificationParser.ParseOuterDiameter(workOrder.Specification);
        var wtOrNull = SpecificationParser.ParseWallThickness(workOrder.Specification);
        if (odOrNull == null || wtOrNull == null)
            return new List<AvailableInventoryBatchDto>();

        var od = odOrNull.Value;
        var wt = wtOrNull.Value;

        // 计算实际OD和WT（公差中值）
        var odActual = od - 0.5m * workOrder.OuterDiameterNegative + 0.5m * workOrder.OuterDiameterPositive;
        var wtActual = wt - 0.5m * workOrder.WallThicknessNegative + 0.5m * workOrder.WallThicknessPositive;

        // 计算工单需求单支重量
        var density = await _context.StandardGradeMappings
            .AsNoTracking()
            .Where(g => g.PlantGrade == workOrder.PlantGrade)
            .Select(g => g.Density)
            .FirstOrDefaultAsync();
        if (density == 0) density = 7.93m; // 默认密度

        // 从配置表读取尺寸公差系数和默认长度
        var odLowerRatio = await GetConfigAsync("DimensionTolerance", "OdLower", 1.002m);
        var odUpperRatio = await GetConfigAsync("DimensionTolerance", "OdUpper", 0.998m);
        var wtLowerRatio = await GetConfigAsync("DimensionTolerance", "WtLower", 1.02m);
        var wtUpperRatio = await GetConfigAsync("DimensionTolerance", "WtUpper", 0.98m);
        var unitWeightLength = await GetConfigAsync("LengthDefault", "UnitWeightLength", 4500m);
        var pipeLength = await GetConfigAsync("LengthDefault", "PipeLength", 6000m);

        // 单米重量 = π × 密度 × WT_实际 × (OD_实际 - WT_实际) / 1000
        var unitWeightPerMeter = Math.Round(
            (decimal)Math.PI * density * wtActual * (odActual - wtActual) / 1000m, 6);

        decimal requiredUnitWeight;
        if (workOrder.LengthStatus == LengthStatus.NonFixed)
        {
            // 非定尺：默认长度（从配置表读取）
            requiredUnitWeight = Math.Round(unitWeightPerMeter * unitWeightLength / 1000m, 3);
        }
        else
        {
            // 定尺/范围尺：取MaxLength
            var lengthMm = workOrder.MaxLength ?? (int)pipeLength;
            requiredUnitWeight = Math.Round(unitWeightPerMeter * lengthMm / 1000m, 3);
        }

        // 外径边界
        var odMin = Math.Round((od - workOrder.OuterDiameterNegative) * odLowerRatio, 3);
        var odMax = Math.Round((od + workOrder.OuterDiameterPositive) * odUpperRatio, 3);

        // 壁厚边界
        var wtMin = Math.Round((wt - workOrder.WallThicknessNegative) * wtLowerRatio, 3);
        var wtMax = Math.Round((wt + workOrder.WallThicknessPositive) * wtUpperRatio, 3);

        // 获取已被其他"全部使用"计划锁定的批次号（排除当前编辑计划自身）
        // 全部使用(All)=整批锁定；部分使用(Partial)=仅预留不锁定，批次仍可被下个工单使用
        // 已出库（生产领用 ProductionPick）的计划视为执行完成，不再占用批次，剩余料可被再次计划利用
        var usedBatchNosQuery = _context.InventoryPlans
            .Where(p => p.PlanStatus != InventoryPlanStatus.Cancelled
                && p.UsageMode == "All"
                && !_context.OutboundRecords.Any(or =>
                    or.BatchNo != null && or.BatchNo == p.InventoryBatchNo
                    && or.OutboundType == OutboundType.ProductionPick
                    && or.WorkOrderNo != null
                    && or.WorkOrderNo == _context.WorkOrders
                        .Where(w => w.Id == p.WorkOrderId)
                        .Select(w => w.WorkOrderNo)
                        .FirstOrDefault()));

        if (excludePlanId.HasValue)
        {
            usedBatchNosQuery = usedBatchNosQuery.Where(p => p.Id != excludePlanId.Value);
        }

        var usedBatchNos = await usedBatchNosQuery
            .Select(p => p.InventoryBatchNo)
            .Distinct()
            .ToListAsync();

        // 各批次已被"部分使用"计划预留的支数/重量（排除当前编辑计划自身）
        var reservedMap = await GetPartialReservedMapAsync(excludePlanId);

        // 查询可用库存
        var query = _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.RemainingWeight > 0
                && InventoryMaterialTypes.InventoryPlanUsable.Contains(b.MaterialType)
                && !usedBatchNos.Contains(b.BatchNo));

        // 牌号条件：精确匹配 或 高级替代
        var eligibleGrades = new List<string> { workOrder.PlantGrade };
        if (GradeSubstitutes.TryGetValue(workOrder.PlantGrade, out var substitute))
        {
            eligibleGrades.Add(substitute);
        }

        query = query.Where(b => eligibleGrades.Contains(b.PlantGrade));

        var batches = await query.ToListAsync();

        // 内存筛选（外径/壁厚/长度/单支重量条件需要计算）
        var available = batches
            .Where(b =>
            {
                // 条件③④：外径/壁厚符合
                // 有实际规格则从中解析，否则从名义规格解析
                var specForBatch = b.ActualSpecification ?? b.Specification;
                var batchOd = SpecificationParser.ParseOuterDiameter(specForBatch);
                var batchWt = SpecificationParser.ParseWallThickness(specForBatch);
                if (batchOd == null || batchWt == null)
                    return false;
                if (batchOd < odMin || batchOd > odMax)
                    return false;
                if (batchWt < wtMin || batchWt > wtMax)
                    return false;

                // 条件⑤：长度符合 - 库存MinLength ≥ 工单MaxLength
                if (b.MinLength.HasValue && workOrder.MaxLength.HasValue)
                {
                    if (b.MinLength < workOrder.MaxLength)
                        return false;
                }

                // 条件⑥：单支重量符合 - 库存UnitWeight ≥ 工单需求单支重量
                if (b.UnitWeight.HasValue)
                {
                    if (b.UnitWeight < requiredUnitWeight)
                        return false;
                }

                return true;
            })
            .Select(b =>
            {
                reservedMap.TryGetValue(b.BatchNo, out var reserved);
                return new AvailableInventoryBatchDto
                {
                    Id = b.Id,
                    BatchNo = b.BatchNo,
                    MaterialType = EnumHelper.TryParse<MaterialType>(b.MaterialType),
                    PlantGrade = b.PlantGrade,
                    Specification = b.Specification,
                    ActualSpecification = b.ActualSpecification,
                    LengthStatus = EnumHelper.TryParse<LengthStatus>(b.LengthStatus),
                    MinLength = b.MinLength,
                    MaxLength = b.MaxLength,
                    RemainingQuantity = b.RemainingQuantity,
                    RemainingWeight = b.RemainingWeight,
                    ReservedQuantity = reserved.ReservedQuantity,
                    ReservedWeight = reserved.ReservedWeight,
                    UnitWeight = b.UnitWeight,
                    ManufacturingStatus = EnumHelper.TryParse<DeliveryState>(b.ManufacturingStatus),
                    LocationArea = b.LocationArea,
                    LocationRack = b.LocationRack,
                };
            })
        .ToList();

        return available;
    }

    public async Task<List<AvailableInventoryBatchDto>> GetAvailableReworkInventoryAsync(int workOrderId, ReworkType reworkType, int? excludePlanId = null)
    {
        var workOrder = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == workOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        // 解析名义外径和壁厚
        var nominalOdOrNull = SpecificationParser.ParseOuterDiameter(workOrder.Specification);
        var nominalWtOrNull = SpecificationParser.ParseWallThickness(workOrder.Specification);
        if (nominalOdOrNull == null || nominalWtOrNull == null)
            return new List<AvailableInventoryBatchDto>();

        var nominalOd = nominalOdOrNull.Value;
        var nominalWt = nominalWtOrNull.Value;

        // 计算测算OD/WT（公差中值法）
        var calculatedOd = nominalOd - 0.5m * workOrder.OuterDiameterNegative + 0.5m * workOrder.OuterDiameterPositive;
        var calculatedWt = nominalWt - 0.5m * workOrder.WallThicknessNegative + 0.5m * workOrder.WallThicknessPositive;

        // 计算工单需求单支重量
        var density = await _context.StandardGradeMappings
            .AsNoTracking()
            .Where(g => g.PlantGrade == workOrder.PlantGrade)
            .Select(g => g.Density)
            .FirstOrDefaultAsync();
        if (density == 0) density = 7.93m;

        var unitWeightPerMeter = Math.Round(
            (decimal)Math.PI * density * calculatedWt * (calculatedOd - calculatedWt) / 1000m, 6);

        // 从配置表读取默认长度和改制系数
        var unitWeightLength = await GetConfigAsync("LengthDefault", "UnitWeightLength", 4500m);
        var pipeLength = await GetConfigAsync("LengthDefault", "PipeLength", 6000m);
        var emptyDrawingOdLower = await GetConfigAsync("ReworkRatio", "EmptyDrawingOdLower", 1.05m);
        var fewerPassOdLower = await GetConfigAsync("ReworkRatio", "FewerPassOdLower", 1.1m);
        var odUpper = await GetConfigAsync("ReworkRatio", "OdUpper", 2.0m);
        var emptyDrawingWtLower = await GetConfigAsync("ReworkRatio", "EmptyDrawingWtLower", 0.95m);
        var fewerPassWtLower = await GetConfigAsync("ReworkRatio", "FewerPassWtLower", 1.05m);
        var emptyDrawingWtUpper = await GetConfigAsync("ReworkRatio", "EmptyDrawingWtUpper", 1.05m);
        var fewerPassWtUpper = await GetConfigAsync("ReworkRatio", "FewerPassWtUpper", 2.0m);
        var minUnitWeightRatio = await GetConfigAsync("ReworkRatio", "MinUnitWeightRatio", 1.05m);

        decimal requiredUnitWeight;
        if (workOrder.LengthStatus == LengthStatus.NonFixed)
        {
            requiredUnitWeight = Math.Round(unitWeightPerMeter * unitWeightLength / 1000m, 3);
        }
        else
        {
            var lengthMm = workOrder.MaxLength ?? (int)pipeLength;
            requiredUnitWeight = Math.Round(unitWeightPerMeter * lengthMm / 1000m, 3);
        }

        // 排除规则：316L0不可替代316H0
        var exclude316L0For316H0 = string.Equals(workOrder.PlantGrade, "316H0", StringComparison.OrdinalIgnoreCase);

        // 合格牌号：工单本身牌号 + 高级替代牌号
        var eligibleGrades = new List<string> { workOrder.PlantGrade };
        if (GradeSubstitutes.TryGetValue(workOrder.PlantGrade, out var higherGrade))
        {
            if (!(exclude316L0For316H0 && string.Equals(higherGrade, "316L0", StringComparison.OrdinalIgnoreCase)))
            {
                eligibleGrades.Add(higherGrade);
            }
        }

        // 获取已被其他"全部使用"计划锁定的批次号（排除当前编辑计划自身）
        // 全部使用(All)=整批锁定；部分使用(Partial)=仅预留不锁定，批次仍可被下个工单使用
        // 已出库（生产领用 ProductionPick）的计划视为执行完成，不再占用批次，剩余料可被再次计划利用
        var usedBatchNosQuery = _context.InventoryPlans
            .Where(p => p.PlanStatus != InventoryPlanStatus.Cancelled
                && p.UsageMode == "All"
                && !_context.OutboundRecords.Any(or =>
                    or.BatchNo != null && or.BatchNo == p.InventoryBatchNo
                    && or.OutboundType == OutboundType.ProductionPick
                    && or.WorkOrderNo != null
                    && or.WorkOrderNo == _context.WorkOrders
                        .Where(w => w.Id == p.WorkOrderId)
                        .Select(w => w.WorkOrderNo)
                        .FirstOrDefault()));

        if (excludePlanId.HasValue)
        {
            usedBatchNosQuery = usedBatchNosQuery.Where(p => p.Id != excludePlanId.Value);
        }

        var usedBatchNos = await usedBatchNosQuery
            .Select(p => p.InventoryBatchNo)
            .Distinct()
            .ToListAsync();

        // 各批次已被"部分使用"计划预留的支数/重量（排除当前编辑计划自身）
        var reservedMap = await GetPartialReservedMapAsync(excludePlanId);

        // 根据改制类型构建查询
        var query = _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.RemainingWeight > 0
                && !usedBatchNos.Contains(b.BatchNo)
                && eligibleGrades.Contains(b.PlantGrade));

        // 物料名称筛选
        query = reworkType switch
        {
            ReworkType.EmptyDrawing or ReworkType.FewerPass => query.Where(b =>
                InventoryMaterialTypes.EmptyDrawingReworkUsable.Contains(b.MaterialType)
                || (b.MaterialType == InventoryMaterialTypes.SemiFinished && !b.IsLinkedToWorkOrder)
                || (b.MaterialType == InventoryMaterialTypes.DefectSemi && b.LiabilityType == LiabilityTypeKeys.FactoryDepartment)
                || (b.MaterialType == InventoryMaterialTypes.DefectFinished && b.LiabilityType == LiabilityTypeKeys.FactoryDepartment)
                || (b.MaterialType == InventoryMaterialTypes.DefectWIP && b.LiabilityType == LiabilityTypeKeys.FactoryDepartment)),
            ReworkType.ManualSelect => query.Where(b =>
                !InventoryMaterialTypes.ManualSelectReworkExcluded.Contains(b.MaterialType)),
            _ => query.Where(b => false) // 未知类型返回空
        };

        var batches = await query.ToListAsync();

        // 计算各类型边界条件
        var odMin = reworkType switch
        {
            ReworkType.EmptyDrawing => Math.Round(calculatedOd * emptyDrawingOdLower, 3),
            ReworkType.FewerPass => Math.Round(calculatedOd * fewerPassOdLower, 3),
            _ => 0m // ManualSelect: 不限外径
        };
        var odMax = reworkType switch
        {
            ReworkType.EmptyDrawing => Math.Round(calculatedOd * odUpper, 3),
            ReworkType.FewerPass => Math.Round(calculatedOd * odUpper, 3),
            _ => decimal.MaxValue // ManualSelect: 外径上限无限制
        };

        var wtMin = reworkType switch
        {
            ReworkType.EmptyDrawing => Math.Round(calculatedWt * emptyDrawingWtLower, 3),
            ReworkType.FewerPass => Math.Round(calculatedWt * fewerPassWtLower, 3),
            ReworkType.ManualSelect => Math.Round(calculatedWt, 3),
            _ => 0m
        };
        var wtMax = reworkType switch
        {
            ReworkType.EmptyDrawing => Math.Round(calculatedWt * emptyDrawingWtUpper, 3),
            ReworkType.FewerPass => Math.Round(calculatedWt * fewerPassWtUpper, 3),
            _ => decimal.MaxValue // ManualSelect: 不限壁厚上限
        };

        var minUnitWeight = Math.Round(requiredUnitWeight * minUnitWeightRatio, 3);

        var available = batches
            .Where(b =>
            {
                // 外径/壁厚条件：有实际规格则从中解析，否则从名义规格解析
                var specForBatch = b.ActualSpecification ?? b.Specification;
                var batchOd = SpecificationParser.ParseOuterDiameter(specForBatch);
                var batchWt = SpecificationParser.ParseWallThickness(specForBatch);
                if (batchOd == null || batchWt == null)
                    return false;
                if (reworkType != ReworkType.ManualSelect && (batchOd < odMin || batchOd > odMax))
                    return false;
                if (batchWt < wtMin || batchWt > wtMax)
                    return false;

                // 单支重量条件
                if (b.UnitWeight.HasValue)
                {
                    if (b.UnitWeight < minUnitWeight)
                        return false;
                }

                return true;
            })
            .Select(b =>
            {
                reservedMap.TryGetValue(b.BatchNo, out var reserved);
                return new AvailableInventoryBatchDto
                {
                    Id = b.Id,
                    BatchNo = b.BatchNo,
                    MaterialType = EnumHelper.TryParse<MaterialType>(b.MaterialType),
                    PlantGrade = b.PlantGrade,
                    Specification = b.Specification,
                    ActualSpecification = b.ActualSpecification,
                    LengthStatus = EnumHelper.TryParse<LengthStatus>(b.LengthStatus),
                    MinLength = b.MinLength,
                    MaxLength = b.MaxLength,
                    RemainingQuantity = b.RemainingQuantity,
                    RemainingWeight = b.RemainingWeight,
                    UnitWeight = b.UnitWeight,
                    ManufacturingStatus = EnumHelper.TryParse<DeliveryState>(b.ManufacturingStatus),
                    LocationArea = b.LocationArea,
                    LocationRack = b.LocationRack,
                    ReservedQuantity = reserved.ReservedQuantity,
                    ReservedWeight = reserved.ReservedWeight,
                };
            })
            .ToList();

        return available;
    }

    public async Task<InventoryPlanDto> GetInventoryPlanByIdAsync(int id)
    {
        var plan = await _context.InventoryPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("库存使用计划不存在");

        var workOrderNo = await _context.WorkOrders.AsNoTracking()
            .Where(w => w.Id == plan.WorkOrderId)
            .Select(w => w.WorkOrderNo)
            .FirstOrDefaultAsync();
        var dto = plan.ToDto();
        dto.IsOutbound = await IsInventoryPlanOutboundAsync(plan.InventoryBatchNo, workOrderNo);
        return dto;
    }

    public async Task<InventoryPlanDto> UpdateInventoryPlanAsync(int id, CreateInventoryPlanRequest request)
    {
        var plan = await _context.InventoryPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("库存使用计划不存在");

        var workOrder = await _context.WorkOrders.FindAsync(plan.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("关联工单不存在");

        if (await IsInventoryPlanOutboundAsync(plan.InventoryBatchNo, workOrder.WorkOrderNo))
            throw new BusinessException($"批次{plan.InventoryBatchNo}已生产领用出库，库存使用计划不可修改");

        // 校验锁定与预留（排除自身计划；出库计划视为执行完成不再占用）
        var activePlans = await _context.InventoryPlans
            .Where(p => p.Id != id
                && p.InventoryBatchNo == plan.InventoryBatchNo
                && p.PlanStatus != InventoryPlanStatus.Cancelled
                && !_context.OutboundRecords.Any(or =>
                    or.BatchNo != null && or.BatchNo == p.InventoryBatchNo
                    && or.OutboundType == OutboundType.ProductionPick
                    && or.WorkOrderNo != null
                    && or.WorkOrderNo == _context.WorkOrders
                        .Where(w => w.Id == p.WorkOrderId)
                        .Select(w => w.WorkOrderNo)
                        .FirstOrDefault()))
            .Select(p => new { p.WorkOrderId, p.UsageMode, p.UsedQuantity, p.UsedWeight })
            .ToListAsync();
        var batch = await _context.InventoryBatches
            .FirstOrDefaultAsync(b => b.BatchNo == plan.InventoryBatchNo);

        if (request.UsageMode == "All")
        {
            if (activePlans.Count > 0)
                throw new BusinessException("该库存批次已被使用计划引用（含部分使用预留），不可全部使用");
            if (batch != null)
            {
                request.UsedQuantity = batch.RemainingQuantity;
                request.UsedWeight = batch.RemainingWeight;
            }
        }
        else
        {
            if (activePlans.Any(p => p.UsageMode == "All"))
                throw new BusinessException("该库存批次已被全部使用计划占用，不可部分使用");
            if (request.UsedQuantity == null || request.UsedQuantity <= 0)
                throw new BusinessException("部分使用模式下出库支数必须大于0");
            if (request.UsedWeight <= 0)
                throw new BusinessException("出库重量必须大于0");
            if (batch != null)
            {
                var reservedQty = activePlans.Where(p => p.UsageMode == "Partial").Sum(p => p.UsedQuantity ?? 0);
                var reservedWt = activePlans.Where(p => p.UsageMode == "Partial").Sum(p => p.UsedWeight);
                var availableQty = batch.RemainingQuantity - reservedQty;
                var availableWt = batch.RemainingWeight - reservedWt;
                if (request.UsedQuantity.Value > availableQty)
                    throw new BusinessException($"出库支数({request.UsedQuantity})超过剩余可用支数({availableQty})");
                if (request.UsedWeight > availableWt)
                    throw new BusinessException($"出库重量({request.UsedWeight})超过剩余可用重量({availableWt.ToString("G29")})");
            }
        }

        plan.PlanDate = request.PlanDate;
        plan.InputMultiple = request.InputMultiple;
        plan.UsageMode = request.UsageMode;
        plan.UsedQuantity = request.UsedQuantity;
        plan.UsedWeight = request.UsedWeight;
        plan.RequiredDate = request.RequiredDate;
        plan.Remark = request.Remark;

        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                // 库料改制（有 ReworkType）：必须填写工序组，全量替换工序组 + 重算工艺周期；库存使用固定 3 天
                var effectiveReworkType = request.ReworkType ?? plan.ReworkType;
                if (effectiveReworkType.HasValue)
                {
                    EnsureProcessGroupsNotEmpty(request.ProcessGroups, "库料改制必须填写工序组");
                    var existingGroups = await _context.InventoryPlanProcessGroups
                        .Where(g => g.InventoryPlanId == id)
                        .ToListAsync();
                    _context.InventoryPlanProcessGroups.RemoveRange(existingGroups);
                    var defaultProcessCycle = (int)await GetConfigAsync("DefaultValue", "DefaultProcessCycle", 22m);
                    plan.StandardCycle = await ComputeStandardCycleAsync(plan.WorkOrderId, request.ProcessGroups!);
                    if (plan.StandardCycle == 0)
                        plan.StandardCycle = defaultProcessCycle;
                    await SaveInventoryPlanProcessGroupsAsync(id, request.ProcessGroups!);
                }
                else
                {
                    var defaultStandardCycle = (int)await GetConfigAsync("DefaultValue", "StandardCycle", 3m);
                    plan.StandardCycle = defaultStandardCycle;
                    await _context.SaveChangesAsync();
                }

                await UpdateMaterialPlanStatusAsync(plan.WorkOrderId);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(plan.WorkOrderId);

        _logger.LogInformation("更新库存使用计划成功: ID {Id}", id);
        return plan.ToDto();
    }

    #endregion

    #region 圆棒穿孔计划

    public async Task<List<RoundBarPiercingPlanDto>> GetPiercingPlansAsync(int workOrderId)
    {
        var plans = await _context.RoundBarPiercingPlans
            .Where(p => p.WorkOrderId == workOrderId)
            .OrderByDescending(p => p.CreatedTime)
            .ToListAsync();

        return plans.Select(p => p.ToDto()).ToList();
    }

    public async Task<RoundBarPiercingPlanDto> GetPiercingPlanByIdAsync(int id)
    {
        var plan = await _context.RoundBarPiercingPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("圆棒穿孔计划不存在");
        return plan.ToDto();
    }

    public async Task<RoundBarPiercingPlanDto> CreatePiercingPlanAsync(CreateRoundBarPiercingPlanRequest request)
    {
        var workOrder = await _context.WorkOrders.FindAsync(request.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        // 非定尺: 支数不能为空
        if (workOrder.LengthStatus == LengthStatus.NonFixed && request.RequiredPieces == null)
            throw new BusinessException("非定尺模式下需求支数为必填");

        // 执行测算
        var calc = await CalculateInternalAsync(workOrder, new CreatePurchaseSemiPlanRequest
        {
            AdjustedWallThickness = request.AdjustedWallThickness,
            YieldRate = request.YieldRate,
            InputMultiple = request.InputMultiple,
            QualifiedRate = request.QualifiedRate
        });

        var plan = new RoundBarPiercingPlan
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
            PlantGrade = request.PlantGrade,
            RawMaterialType = request.RawMaterialType,
            RoundBarSpec = request.RoundBarSpec,
            PiercingSpec = request.PiercingSpec,
            RequiredUnitWeight = request.RequiredUnitWeight,
            RequiredPieces = request.RequiredPieces,
            RequiredWeight = request.RequiredWeight,
            RequiredDate = request.RequiredDate,
            Remark = request.Remark,
        };

        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                // 无工序组不可提交（工艺工量需工序组内算）
                EnsureProcessGroupsNotEmpty(request.ProcessGroups, "圆棒穿孔计划必须填写工序组");

                _context.RoundBarPiercingPlans.Add(plan);
                await _context.SaveChangesAsync();

                // 保存工序组
                if (request.ProcessGroups is { Count: > 0 })
                {
                    int seq = 1;
                    foreach (var pg in request.ProcessGroups)
                    {
                        _context.PiercingPlanProcessGroups.Add(new PiercingPlanProcessGroup
                        {
                            RoundBarPiercingPlanId = plan.Id,
                            SequenceNumber = seq++,
                            ProcessName = pg.ProcessName,
                            ManufacturingSpec = pg.ManufacturingSpec,
                            OuterDiameterTolerance = pg.OuterDiameterTolerance,
                            WallThicknessTolerance = pg.WallThicknessTolerance,
                            ManufacturingLength = pg.ManufacturingLength,
                            CuttingTreatment = pg.CuttingTreatment,
                            Remark = pg.Remark,
                            ColdRollDraw = pg.ColdRollDraw,
                            OilPipeCut = pg.OilPipeCut,
                            Degrease = pg.Degrease,
                            EmulsionWash = pg.EmulsionWash,
                            UltrasonicWash = pg.UltrasonicWash,
                            ClothPolish = pg.ClothPolish,
                            BrightAnnealing = pg.BrightAnnealing,
                            Solution = pg.Solution,
                            Straighten = pg.Straighten,
                            Cut = pg.Cut,
                            ThicknessMeasure = pg.ThicknessMeasure,
                            Pickle = pg.Pickle,
                            OuterPolish = pg.OuterPolish,
                            InnerPolish = pg.InnerPolish,
                            InnerGrinding = pg.InnerGrinding,
                            OuterSpotGrinding = pg.OuterSpotGrinding,
                            SandBlasting = pg.SandBlasting,
                            ShotBlasting = pg.ShotBlasting,
                            Inspection = pg.Inspection,
                            WeldingHead = pg.WeldingHead,
                            Welding = pg.Welding,
                            Lubrication = pg.Lubrication,
                            Packing = pg.Packing,
                            Warehouse = pg.Warehouse,
                            Extra1 = pg.Extra1,
                            Extra2 = pg.Extra2
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                // 从工序组计算工艺周期
                var pierceGroups = await _context.PiercingPlanProcessGroups
                    .Where(g => g.RoundBarPiercingPlanId == plan.Id)
                    .ToListAsync();
                var pierceSections = new List<(string, int)>();
                foreach (var pg in pierceGroups)
                {
                    pierceSections.AddRange(ExtractSections(
                        pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.EmulsionWash,
                        pg.UltrasonicWash, pg.ClothPolish, pg.BrightAnnealing, pg.Solution,
                        pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                        pg.OuterPolish, pg.InnerPolish, pg.InnerGrinding, pg.OuterSpotGrinding,
                        pg.SandBlasting, pg.ShotBlasting, pg.Inspection, pg.WeldingHead,
                        pg.Welding, pg.Lubrication, pg.Packing, pg.Warehouse,
                        pg.Extra1, pg.Extra2));
                }
                var dayMap = await _standardWorkDayService.GetStandardDaysMapAsync(workOrder.PlantGrade);
                var deliveryStateExtraDays = await _deliveryStateService.GetDeliveryStateExtraDaysMapAsync();
                plan.StandardCycle = CalculateStandardCycleFromSections(
                    pierceSections, dayMap, deliveryStateExtraDays,
                    workOrder.DeliveryState.ToString());
                if (plan.StandardCycle == 0)
                    throw new BusinessException("工艺周期计算失败：工序组工段数据不完整，无法计算工艺周期");
                _context.Entry(plan).Property(e => e.StandardCycle).IsModified = true;
                await _context.SaveChangesAsync();

                // 刷新工单状态（与创建在同一事务中）
                await UpdateMaterialPlanStatusAsync(request.WorkOrderId);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(request.WorkOrderId);

        _logger.LogInformation("创建圆棒穿孔计划成功: 工单ID {WorkOrderId}, 圆棒规格 {Spec}, 穿孔规格 {Piercing}, 重量 {Weight}",
            request.WorkOrderId, request.RoundBarSpec, request.PiercingSpec, request.RequiredWeight);

        return plan.ToDto();
    }

    public async Task<RoundBarPiercingPlanDto> UpdatePiercingPlanAsync(int id, UpdateRoundBarPiercingPlanRequest request)
    {
        var plan = await _context.RoundBarPiercingPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("圆棒穿孔计划不存在");

        var workOrder = await _context.WorkOrders.FindAsync(plan.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("关联工单不存在");

        // 非定尺: 支数不能为空
        if (workOrder.LengthStatus == LengthStatus.NonFixed && request.RequiredPieces == null)
            throw new BusinessException("非定尺模式下需求支数为必填");

        // 执行测算
        var calc = await CalculateInternalAsync(workOrder, new CreatePurchaseSemiPlanRequest
        {
            AdjustedWallThickness = request.AdjustedWallThickness,
            YieldRate = request.YieldRate,
            InputMultiple = request.InputMultiple,
            QualifiedRate = request.QualifiedRate
        });

        // 更新字段
        plan.PlanDate = request.PlanDate;
        plan.AdjustedWallThickness = request.AdjustedWallThickness;
        plan.YieldRate = request.YieldRate;
        plan.InputMultiple = request.InputMultiple;
        plan.QualifiedRate = request.QualifiedRate;
        plan.Density = calc.Density;
        plan.UnitWeight = calc.UnitWeight;
        plan.RawUnitWeight = calc.RawUnitWeight;
        plan.PlantGrade = request.PlantGrade;
        plan.RawMaterialType = request.RawMaterialType;
        plan.RoundBarSpec = request.RoundBarSpec;
        plan.PiercingSpec = request.PiercingSpec;
        plan.RequiredUnitWeight = request.RequiredUnitWeight;
        plan.RequiredPieces = request.RequiredPieces;
        plan.RequiredWeight = request.RequiredWeight;
        plan.RequiredDate = request.RequiredDate;
        plan.Remark = request.Remark;

        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                // 无工序组不可提交（工艺工量需工序组内算）
                EnsureProcessGroupsNotEmpty(request.ProcessGroups, "圆棒穿孔计划必须填写工序组");

                // 全量替换工序组
                var existingGroups = await _context.PiercingPlanProcessGroups
                    .Where(g => g.RoundBarPiercingPlanId == id)
                    .ToListAsync();
                _context.PiercingPlanProcessGroups.RemoveRange(existingGroups);

                if (request.ProcessGroups is { Count: > 0 })
                {
                    int seq = 1;
                    foreach (var pg in request.ProcessGroups)
                    {
                        _context.PiercingPlanProcessGroups.Add(new PiercingPlanProcessGroup
                        {
                            RoundBarPiercingPlanId = id,
                            SequenceNumber = seq++,
                            ProcessName = pg.ProcessName,
                            ManufacturingSpec = pg.ManufacturingSpec,
                            OuterDiameterTolerance = pg.OuterDiameterTolerance,
                            WallThicknessTolerance = pg.WallThicknessTolerance,
                            ManufacturingLength = pg.ManufacturingLength,
                            CuttingTreatment = pg.CuttingTreatment,
                            Remark = pg.Remark,
                            ColdRollDraw = pg.ColdRollDraw,
                            OilPipeCut = pg.OilPipeCut,
                            Degrease = pg.Degrease,
                            EmulsionWash = pg.EmulsionWash,
                            UltrasonicWash = pg.UltrasonicWash,
                            ClothPolish = pg.ClothPolish,
                            BrightAnnealing = pg.BrightAnnealing,
                            Solution = pg.Solution,
                            Straighten = pg.Straighten,
                            Cut = pg.Cut,
                            ThicknessMeasure = pg.ThicknessMeasure,
                            Pickle = pg.Pickle,
                            OuterPolish = pg.OuterPolish,
                            InnerPolish = pg.InnerPolish,
                            InnerGrinding = pg.InnerGrinding,
                            OuterSpotGrinding = pg.OuterSpotGrinding,
                            SandBlasting = pg.SandBlasting,
                            ShotBlasting = pg.ShotBlasting,
                            Inspection = pg.Inspection,
                            WeldingHead = pg.WeldingHead,
                            Welding = pg.Welding,
                            Lubrication = pg.Lubrication,
                            Packing = pg.Packing,
                            Warehouse = pg.Warehouse,
                            Extra1 = pg.Extra1,
                            Extra2 = pg.Extra2
                        });
                    }
                }

                await _context.SaveChangesAsync();

                // 从工序组重新计算工艺周期
                var pierceGroups = await _context.PiercingPlanProcessGroups
                    .Where(g => g.RoundBarPiercingPlanId == id)
                    .ToListAsync();
                var pierceSections = new List<(string, int)>();
                foreach (var pg in pierceGroups)
                {
                    pierceSections.AddRange(ExtractSections(
                        pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.EmulsionWash,
                        pg.UltrasonicWash, pg.ClothPolish, pg.BrightAnnealing, pg.Solution,
                        pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                        pg.OuterPolish, pg.InnerPolish, pg.InnerGrinding, pg.OuterSpotGrinding,
                        pg.SandBlasting, pg.ShotBlasting, pg.Inspection, pg.WeldingHead,
                        pg.Welding, pg.Lubrication, pg.Packing, pg.Warehouse,
                        pg.Extra1, pg.Extra2));
                }
                var dayMap = await _standardWorkDayService.GetStandardDaysMapAsync(workOrder.PlantGrade);
                var deliveryStateExtraDays = await _deliveryStateService.GetDeliveryStateExtraDaysMapAsync();
                plan.StandardCycle = CalculateStandardCycleFromSections(
                    pierceSections, dayMap, deliveryStateExtraDays,
                    workOrder.DeliveryState.ToString());
                if (plan.StandardCycle == 0)
                    throw new BusinessException("工艺周期计算失败：工序组工段数据不完整，无法计算工艺周期");
                _context.Entry(plan).Property(e => e.StandardCycle).IsModified = true;
                await _context.SaveChangesAsync();

                await UpdateMaterialPlanStatusAsync(plan.WorkOrderId);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(plan.WorkOrderId);

        _logger.LogInformation("更新圆棒穿孔计划成功: ID {Id}, 工单ID {WorkOrderId}, 圆棒规格 {Spec}",
            id, plan.WorkOrderId, request.RoundBarSpec);

        return plan.ToDto();
    }

    public async Task DeletePiercingPlanAsync(int id)
    {
        var plan = await _context.RoundBarPiercingPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("圆棒穿孔计划不存在");

        var workOrderId = plan.WorkOrderId;
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                _context.RoundBarPiercingPlans.Remove(plan);
                await _context.SaveChangesAsync();

                // 刷新工单状态（与删除在同一事务中）
                await UpdateMaterialPlanStatusAsync(workOrderId);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(workOrderId);

        _logger.LogInformation("删除圆棒穿孔计划成功: ID {Id}", id);
    }

    #endregion

    #region 在产改制计划

    /// <summary>
    /// 第6类预留账本：按 ProductionBatchId 分组累计 UsedQuantity/UsedWeight。
    /// 累计所有未取消计划（含已投料）——批次有效量不随投料扣减，计划用量即唯一占用凭证；
    /// 批次被某工单部分使用后，余料仍可被其它工单正确显示并可共享。
    /// </summary>
    private async Task<Dictionary<int, (int ReservedQuantity, decimal ReservedWeight)>> GetInProcessReworkReservedMapAsync(int? excludePlanId)
    {
        var plans = await _context.InProcessReworkPlans
            .AsNoTracking()
            .Where(p => p.PlanStatus != InventoryPlanStatus.Cancelled)
            .Select(p => new { p.Id, p.ProductionBatchId, p.BatchNo, p.UsedQuantity, p.UsedWeight })
            .ToListAsync();
        if (excludePlanId.HasValue)
            plans = plans.Where(p => p.Id != excludePlanId.Value).ToList();

        return plans
            .GroupBy(p => p.ProductionBatchId)
            .ToDictionary(g => g.Key,
                g => (ReservedQuantity: g.Sum(p => p.UsedQuantity ?? 0),
                      ReservedWeight: g.Sum(p => p.UsedWeight)));
    }

    public async Task<List<InProcessReworkPlanDto>> GetInProcessReworkPlansAsync(int workOrderId)
    {
        var plans = await _context.InProcessReworkPlans
            .Where(p => p.WorkOrderId == workOrderId)
            .OrderByDescending(p => p.CreatedTime)
            .ToListAsync();

        return plans.Select(p => p.ToDto()).ToList();
    }

    public async Task<InProcessReworkPlanDto> GetInProcessReworkPlanByIdAsync(int id)
    {
        var plan = await _context.InProcessReworkPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("在产改制计划不存在");

        return plan.ToDto();
    }

    public async Task<InProcessReworkPlanDto> CreateInProcessReworkPlanAsync(CreateInProcessReworkPlanRequest request)
    {
        var workOrder = await _context.WorkOrders.FindAsync(request.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        var batch = await _context.ProductionBatches.FindAsync(request.ProductionBatchId);
        if (batch == null)
            throw new BusinessException("生产批次不存在");

        if (batch.WorkOrderNo != WorkOrderNoSentinel.NotWorkOrder)
            throw new BusinessException("只能选择非工单批次进行在产改制");

        if (batch.Status != BatchStatus.None && batch.Status != BatchStatus.InProgress)
            throw new BusinessException("只能选择未产或在产状态的批次");

        // 校验用量
        if (request.UsedQuantity.HasValue && request.UsedQuantity <= 0)
            throw new BusinessException("使用支数必须大于0");
        if (request.UsedWeight <= 0)
            throw new BusinessException("使用重量必须大于0");
        if (request.UsedQuantity.HasValue && batch.CurrentValidQty.HasValue && request.UsedQuantity > batch.CurrentValidQty)
            throw new BusinessException($"使用支数({request.UsedQuantity})超过批次有效原料支数({batch.CurrentValidQty})");
        if (batch.CurrentValidWeight.HasValue && request.UsedWeight > batch.CurrentValidWeight)
            throw new BusinessException($"使用重量({request.UsedWeight})超过批次有效原料重量({batch.CurrentValidWeight})");

        // 累计预留校验：本次用量 ≤ 批次有效量 − 其他未取消计划已预留用量（跨工单可部分预留共享，含已投料计划）
        var reservedMap = await GetInProcessReworkReservedMapAsync(null);
        if (reservedMap.TryGetValue(request.ProductionBatchId, out var reserved))
        {
            if (request.UsedQuantity.HasValue && batch.CurrentValidQty.HasValue
                && request.UsedQuantity > batch.CurrentValidQty - reserved.ReservedQuantity)
                throw new BusinessException($"使用支数({request.UsedQuantity})超过批次可用有效支数({Math.Max(0, batch.CurrentValidQty.Value - reserved.ReservedQuantity)})（已预留{reserved.ReservedQuantity}支）");
            if (batch.CurrentValidWeight.HasValue
                && request.UsedWeight > batch.CurrentValidWeight - reserved.ReservedWeight)
                throw new BusinessException($"使用重量({request.UsedWeight})超过批次可用有效重量({Math.Max(0m, batch.CurrentValidWeight.Value - reserved.ReservedWeight)})（已预留{reserved.ReservedWeight}kg）");
        }

        var plan = new InProcessReworkPlan
        {
            WorkOrderId = request.WorkOrderId,
            PlanDate = request.PlanDate,
            ProductionBatchId = request.ProductionBatchId,
            BatchNo = batch.BatchNo,
            BatchTagNo = batch.TagNo,
            PlantGrade = batch.PlantGrade,
            Specification = batch.Specification,
            LengthStatus = batch.LengthStatus,
            InputMultiple = request.InputMultiple,
            UsedQuantity = request.UsedQuantity,
            UsedWeight = request.UsedWeight,
            RequiredDate = request.RequiredDate,
            Remark = request.Remark,
        };

        // 在产改制必须填写工序组（无工序组不可提交），工艺周期随创建请求内算
        EnsureProcessGroupsNotEmpty(request.ProcessGroups, "在产改制必须填写工序组");
        var defaultProcessCycle = (int)await GetConfigAsync("DefaultValue", "DefaultProcessCycle", 22m);
        plan.StandardCycle = await ComputeStandardCycleAsync(request.WorkOrderId, request.ProcessGroups!);
        if (plan.StandardCycle == 0)
            plan.StandardCycle = defaultProcessCycle;

        _context.InProcessReworkPlans.Add(plan);
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                await _context.SaveChangesAsync();

                // 在产改制：随创建请求保存工序组（与荒管采购创建请求内算一致）
                if (request.ProcessGroups is { Count: > 0 })
                    await SaveInProcessReworkPlanProcessGroupsAsync(plan.Id, request.ProcessGroups);

                await UpdateMaterialPlanStatusAsync(request.WorkOrderId);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(request.WorkOrderId);

        _logger.LogInformation("创建在产改制计划成功: 工单ID {WorkOrderId}, 批次号 {BatchNo}, 重量 {Weight}",
            request.WorkOrderId, batch.BatchNo, request.UsedWeight);

        return plan.ToDto();
    }

    public async Task<InProcessReworkPlanDto> UpdateInProcessReworkPlanAsync(int id, CreateInProcessReworkPlanRequest request)
    {
        var plan = await _context.InProcessReworkPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("在产改制计划不存在");

        // 完全锁死：在产改制计划不可修改（只能删除后重建）
        throw new BusinessException("在产改制计划不可修改（可删除后重建）");
    }

    public async Task DeleteInProcessReworkPlanAsync(int id)
    {
        var plan = await _context.InProcessReworkPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("在产改制计划不存在");

        var workOrderId = plan.WorkOrderId;
        _context.InProcessReworkPlans.Remove(plan);
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                await _context.SaveChangesAsync();
                await UpdateMaterialPlanStatusAsync(workOrderId);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(workOrderId);

        _logger.LogInformation("删除在产改制计划成功: ID {Id}", id);
    }

    public async Task<List<AvailableInProcessBatchDto>> GetAvailableInProcessBatchesAsync(int workOrderId, int? excludePlanId = null)
    {
        var workOrder = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == workOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        // 合格牌号：工单本身牌号 + 高级替代牌号
        var exclude316L0For316H0 = string.Equals(workOrder.PlantGrade, "316H0", StringComparison.OrdinalIgnoreCase);
        var eligibleGrades = new List<string> { workOrder.PlantGrade };
        if (GradeSubstitutes.TryGetValue(workOrder.PlantGrade, out var higherGrade))
        {
            if (!(exclude316L0For316H0 && string.Equals(higherGrade, "316L0", StringComparison.OrdinalIgnoreCase)))
                eligibleGrades.Add(higherGrade);
        }

        // 已引用批次不排除：本工单或其他工单已建计划的批次仍呈现，余量经预留账本扣减后显示净额（可继续追加），
        // 避免"已扣减批次直接消失"造成误导；创建校验按累计预留兜底拦截超量
        // 查询可用在产批次（仅未产/在产，成检等其它状态不显示）
        var query = _context.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProcessGroups)
            .Where(b => b.WorkOrderNo == WorkOrderNoSentinel.NotWorkOrder)
            .Where(b => b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress)
            .Where(b => eligibleGrades.Contains(b.PlantGrade))
            .Where(b => b.CurrentValidWeight.HasValue && b.CurrentValidWeight > 0);

        // 预留账本：跨工单可部分预留共享（含已投料计划），供显示净可用
        var reservedMap = await GetInProcessReworkReservedMapAsync(excludePlanId);

        // 规格匹配（在产改制恒为人工选择改制 ManualSelect：外径不限，壁厚≥目标壁厚）
        var nominalOdOrNull = SpecificationParser.ParseOuterDiameter(workOrder.Specification);
        var nominalWtOrNull = SpecificationParser.ParseWallThickness(workOrder.Specification);

        if (nominalOdOrNull != null && nominalWtOrNull != null)
        {
            var nominalOd = nominalOdOrNull.Value;
            var nominalWt = nominalWtOrNull.Value;
            var calculatedOd = nominalOd - 0.5m * workOrder.OuterDiameterNegative + 0.5m * workOrder.OuterDiameterPositive;
            var calculatedWt = nominalWt - 0.5m * workOrder.WallThicknessNegative + 0.5m * workOrder.WallThicknessPositive;

            // 人工选择改制：外径不限，壁厚≥目标壁厚
            var odMin = 0m;
            var odMax = decimal.MaxValue;
            var wtMin = Math.Round(calculatedWt, 3);
            var wtMax = decimal.MaxValue;

            // 计算工单需求单支重量
            var density = await _context.StandardGradeMappings
                .AsNoTracking()
                .Where(g => g.PlantGrade == workOrder.PlantGrade)
                .Select(g => g.Density)
                .FirstOrDefaultAsync();
            if (density == 0) density = 7.93m;

            var unitWeightPerMeter = Math.Round(
                (decimal)Math.PI * density * calculatedWt * (calculatedOd - calculatedWt) / 1000m, 6);
            var unitWeightLength = await GetConfigAsync("LengthDefault", "UnitWeightLength", 4500m);
            var pipeLength = await GetConfigAsync("LengthDefault", "PipeLength", 6000m);
            var minUnitWeightRatio = await GetConfigAsync("ReworkRatio", "MinUnitWeightRatio", 1.05m);

            decimal requiredUnitWeight;
            if (workOrder.LengthStatus == LengthStatus.NonFixed)
                requiredUnitWeight = Math.Round(unitWeightPerMeter * unitWeightLength / 1000m, 3);
            else
                requiredUnitWeight = Math.Round(unitWeightPerMeter * (workOrder.MaxLength ?? (int)pipeLength) / 1000m, 3);

            var minUnitWeight = Math.Round(requiredUnitWeight * minUnitWeightRatio, 3);

            var batches = await query.ToListAsync();
            var available = batches
                .Where(b =>
                {
                    // 规格判定状态感知：未产批次用"下个规格"（首道工序），在产批次用"当前规格"
                    var spec = EffectiveSpecForAvailable(b);
                    var bOd = SpecificationParser.ParseOuterDiameter(spec);
                    var bWt = SpecificationParser.ParseWallThickness(spec);
                    if (bOd == null || bWt == null) return false;
                    if (bOd < odMin || bOd > odMax || bWt < wtMin || bWt > wtMax)
                        return false;

                    // 单支重量条件：重量/支数/工序组制成倍率
                    // 未产批次（Status=None）材料为原料态、未经任何工序，"当前重量/支数/断切倍率=成品单重"换算不成立
                    // （CurrentValidQty/Weight 是原料量，ProductionRatio 断切倍率尚未实际发生），故跳过单支重量判定，
                    // 可用性由"下个规格"（首道工序制造规格）判定保证；在产批次保持原判定。
                    if (b.Status != BatchStatus.None && b.CurrentValidQty.HasValue && b.CurrentValidQty > 0)
                    {
                        var multiple = GetCurrentProcessGroupMultiple(b);
                        var actualUnitWeight = b.CurrentValidWeight.GetValueOrDefault() / b.CurrentValidQty.Value / multiple;
                        if (actualUnitWeight < minUnitWeight)
                            return false;
                    }

                    return true;
                })
                .Select(b => new AvailableInProcessBatchDto
                {
                    Id = b.Id,
                    BatchNo = b.BatchNo,
                    TagNo = b.TagNo,
                    PlantGrade = b.PlantGrade,
                    Specification = EffectiveSpecForAvailable(b),
                    LengthStatus = string.IsNullOrEmpty(b.LengthStatus) ? default : Enum.Parse<LengthStatus>(b.LengthStatus),
                    CurrentValidQty = b.CurrentValidQty,
                    CurrentValidWeight = b.CurrentValidWeight,
                    CurrentGroupName = b.CurrentGroupName,
                    CurrentSectionName = b.CurrentSectionName,
                    CurrentSpec = b.CurrentSpec,
                    NextProcess = b.NextProcess,
                    NextSectionName = b.NextSectionName,
                    CorrespondingSpec = b.CorrespondingSpec,
                    ReservedQuantity = reservedMap.TryGetValue(b.Id, out var res) ? res.ReservedQuantity : 0,
                    ReservedWeight = reservedMap.TryGetValue(b.Id, out var res2) ? res2.ReservedWeight : 0m,
                })
                .OrderByDescending(b => b.CurrentValidWeight)
                .ToList();

            return available;
        }

        // 规格无法解析时，返回所有可用批次
        var allBatches = await query.ToListAsync();
        return allBatches
            .Select(b => new AvailableInProcessBatchDto
            {
                Id = b.Id,
                BatchNo = b.BatchNo,
                TagNo = b.TagNo,
                PlantGrade = b.PlantGrade,
                Specification = EffectiveSpecForAvailable(b),
                LengthStatus = string.IsNullOrEmpty(b.LengthStatus) ? default : Enum.Parse<LengthStatus>(b.LengthStatus),
                CurrentValidQty = b.CurrentValidQty,
                CurrentValidWeight = b.CurrentValidWeight,
                CurrentGroupName = b.CurrentGroupName,
                CurrentSectionName = b.CurrentSectionName,
                CurrentSpec = b.CurrentSpec,
                NextProcess = b.NextProcess,
                NextSectionName = b.NextSectionName,
                CorrespondingSpec = b.CorrespondingSpec,
                ReservedQuantity = reservedMap.TryGetValue(b.Id, out var res) ? res.ReservedQuantity : 0,
                ReservedWeight = reservedMap.TryGetValue(b.Id, out var res2) ? res2.ReservedWeight : 0m,
            })
            .OrderByDescending(b => b.CurrentValidWeight)
            .ToList();
    }

    /// <summary>
    /// 获取可改制换算倍数：统一使用批次制成倍数 ProductionRatio（&gt;0 时），否则回退 1
    /// </summary>
    private static int GetCurrentProcessGroupMultiple(ProductionBatch batch)
    {
        return batch.ProductionRatio > 0 ? batch.ProductionRatio : 1;
    }

    /// <summary>
    /// 批次当前生效规格（用于在产改制可用料判定与显示）：
    /// 未产批次（无"当前工序/工段/规格"）取"下个规格"（首道工序制造规格，兜底批次自身规格）；
    /// 在产批次取"当前规格"（兜底批次自身规格）。
    /// </summary>
    private static string EffectiveSpecForAvailable(ProductionBatch b)
    {
        return b.Status == BatchStatus.None
            ? (b.CorrespondingSpec ?? b.Specification)
            : (b.CurrentSpec ?? b.Specification);
    }

    /// <summary>
    /// 获取在产改制计划通知（供批次上下文使用）
    /// 通知规则：批次 WorkOrderNo == WorkOrderNoSentinel.NotWorkOrder 时显示，被正式工单认领后自动消失
    /// 不限制 PlanStatus，避免维护隐患
    /// </summary>
    public async Task<List<PendingPlanBatchDto>> GetPendingInProcessReworkPlansAsync()
    {
        return await _context.InProcessReworkPlans
            .AsNoTracking()
            .Join(_context.ProductionBatches.AsNoTracking(),
                p => p.ProductionBatchId,
                b => b.Id,
                (p, b) => new { p, b })
            .Where(j => j.b.WorkOrderNo == WorkOrderNoSentinel.NotWorkOrder && j.p.PlanStatus == InventoryPlanStatus.Planned)
            .Join(_context.WorkOrders.AsNoTracking(),
                j => j.p.WorkOrderId,
                wo => wo.Id,
                (j, wo) => new PendingPlanBatchDto
                {
                    BatchNo = j.p.BatchNo,
                    WorkOrderNo = wo.WorkOrderNo,
                    PlanType = "在产改制",
                    PlanId = j.p.Id,
                    RequiredQuantity = j.p.UsedQuantity,
                    RequiredWeight = j.p.UsedWeight
                })
            .ToListAsync();
    }

    public async Task DismissInProcessReworkPlanByBatchAndWorkOrderAsync(int productionBatchId, string subWorkOrderNo)
    {
        var plans = await _context.InProcessReworkPlans
            .Where(p => p.ProductionBatchId == productionBatchId
                     && p.PlanStatus == InventoryPlanStatus.Planned)
            .Join(_context.WorkOrders,
                p => p.WorkOrderId,
                wo => wo.Id,
                (p, wo) => new { Plan = p, WorkOrder = wo })
            .Where(j => j.WorkOrder.WorkOrderNo == subWorkOrderNo)
            .Select(j => j.Plan)
            .ToListAsync();

        if (plans.Count == 0) return;

        foreach (var plan in plans)
            plan.PlanStatus = InventoryPlanStatus.Completed;

        await _context.SaveChangesAsync();

        foreach (var group in plans.GroupBy(p => p.WorkOrderId))
        {
            await UpdateMaterialPlanStatusAsync(group.Key);
            await RefreshReadModelAsync(group.Key);
        }
    }

    /// <summary>
    /// 根据批次ID消除所有待处理的在产改制计划（有效量变更/工单号变更时触发）
    /// </summary>
    public async Task DismissInProcessReworkPlansByBatchAsync(int productionBatchId)
    {
        var plans = await _context.InProcessReworkPlans
            .Where(p => p.ProductionBatchId == productionBatchId
                     && p.PlanStatus == InventoryPlanStatus.Planned)
            .ToListAsync();

        if (plans.Count == 0) return;

        foreach (var plan in plans)
            plan.PlanStatus = InventoryPlanStatus.Completed;

        await _context.SaveChangesAsync();

        // 触发对应工单的用料状态刷新
        foreach (var group in plans.GroupBy(p => p.WorkOrderId))
        {
            await UpdateMaterialPlanStatusAsync(group.Key);
            await RefreshReadModelAsync(group.Key);
        }
    }

    #endregion

    #region 在产主工单计划

    /// <summary>
    /// 第7类预留账本：按 ProductionBatchId 分组累计 AllocatedQuantity/AllocatedWeight。
    /// 累计所有未取消计划（含已投料），与第6类一致——批次有效量不随投料扣减，计划用量即唯一占用凭证。
    /// </summary>
    private async Task<Dictionary<int, (int ReservedQuantity, decimal ReservedWeight)>> GetInMainWorkOrderReservedMapAsync(int? excludePlanId)
    {
        var plans = await _context.InMainWorkOrderPlans
            .AsNoTracking()
            .Where(p => p.PlanStatus != InventoryPlanStatus.Cancelled)
            .Select(p => new { p.Id, p.ProductionBatchId, p.BatchNo, p.AllocatedQuantity, p.AllocatedWeight })
            .ToListAsync();
        if (excludePlanId.HasValue)
            plans = plans.Where(p => p.Id != excludePlanId.Value).ToList();

        return plans
            .GroupBy(p => p.ProductionBatchId)
            .ToDictionary(g => g.Key,
                g => (ReservedQuantity: g.Sum(p => p.AllocatedQuantity ?? 0),
                      ReservedWeight: g.Sum(p => p.AllocatedWeight)));
    }

    /// <summary>
    /// 主工单可分配剩余总重量 = max(0, 总有效投料重量 − 原主工单号总重量 − 总预留重量)。
    /// 总有效投料重量 = 按分工单过滤口径下的主工单可用批次 CurrentValidWeight 之和（与可用列表页口径一致）；
    /// 原主工单号总重量 = 主号级工单执行摘要 TotalWeight 之和；总预留 = 过滤批次上所有未取消计划（含已投料、跨工单）预留重量之和。
    /// 主工单富余为负归 0（批次仍呈现，校验拦截超量）。
    /// </summary>
    private async Task<decimal> GetMainOrderAllocatableRemainingAsync(int subWorkOrderId, string mainWorkOrderNo, int? excludePlanId)
    {
        var subWorkOrder = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == subWorkOrderId);
        if (subWorkOrder == null)
            throw new BusinessException("工单不存在");

        var mainWo = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.WorkOrderNo == mainWorkOrderNo);
        if (mainWo == null) return 0m;

        // 原主工单号总重量（主号级需求）
        var key = mainWo.SalesOrderNo + "|" + mainWo.ProductionMainNo;
        var summaries = await _context.WorkOrderExecutionSummaries.AsNoTracking()
            .Where(s => s.SalesOrderNo + "|" + s.ProductionMainNo == key)
            .ToListAsync();
        if (summaries.Count == 0) return 0m; // 无执行摘要 → 与页面一致无可用批次，可分配剩余为 0
        var mainTotalWeight = summaries.Sum(s => s.TotalWeight);

        // 主工单可用批次（状态/制造类型/有效重量门槛，同可用列表查询）
        var batches = await _context.ProductionBatches.AsNoTracking()
            .Where(b => b.WorkOrderNo == mainWorkOrderNo
                     && (b.Status == BatchStatus.None
                      || b.Status == BatchStatus.InProgress
                      || b.Status == BatchStatus.InFinalInspection)
                     && b.ManufacturingItem == "OrderFinished"
                     && b.CurrentValidWeight.HasValue && b.CurrentValidWeight > 0)
            .ToListAsync();

        var mainWoDict = new Dictionary<string, MES.Data.Entities.WorkOrder.WorkOrder>(StringComparer.OrdinalIgnoreCase) { [mainWorkOrderNo] = mainWo };
        var eligible = FilterMainWorkOrderBatches(subWorkOrder, batches, mainWoDict);

        var totalValidWeight = eligible.Sum(b => b.CurrentValidWeight ?? 0m);

        // 总预留重量（仅统计过滤口径内的批次，跨工单含已投料；编辑时排除自身）
        var reservedMap = await GetInMainWorkOrderReservedMapAsync(excludePlanId);
        var eligibleIds = eligible.Select(b => b.Id).ToHashSet();
        var totalReservedWeight = reservedMap
            .Where(kv => eligibleIds.Contains(kv.Key))
            .Sum(kv => kv.Value.ReservedWeight);

        return Math.Max(0m, totalValidWeight - mainTotalWeight - totalReservedWeight);
    }

    /// <summary>
    /// 过滤可分配的主工单批次（主工单存在/牌号同级或高级替代/长度状态/交货状态/技术条件/规格 OD·WT 范围）。
    /// 静态辅助，供可用列表查询与创建/更新校验共用，避免口径漂移。
    /// </summary>
    private static List<ProductionBatch> FilterMainWorkOrderBatches(
        MES.Data.Entities.WorkOrder.WorkOrder subWorkOrder,
        IEnumerable<ProductionBatch> batches,
        IReadOnlyDictionary<string, MES.Data.Entities.WorkOrder.WorkOrder> mainWoDict)
    {
        // 合格牌号：分工单本身牌号 + 高级替代牌号
        var eligibleGrades = new List<string> { subWorkOrder.PlantGrade };
        if (GradeSubstitutes.TryGetValue(subWorkOrder.PlantGrade, out var higherGrade))
            eligibleGrades.Add(higherGrade);

        // 分工单平均外径/壁厚（名义值 + 公差中值）
        var subNominalOd = SpecificationParser.ParseOuterDiameter(subWorkOrder.Specification);
        var subNominalWt = SpecificationParser.ParseWallThickness(subWorkOrder.Specification);
        var subAvgOd = subNominalOd.HasValue
            ? subNominalOd.Value + (subWorkOrder.OuterDiameterPositive - subWorkOrder.OuterDiameterNegative) / 2
            : (decimal?)null;
        var subAvgWt = subNominalWt.HasValue
            ? subNominalWt.Value + (subWorkOrder.WallThicknessPositive - subWorkOrder.WallThicknessNegative) / 2
            : (decimal?)null;

        var result = new List<ProductionBatch>();
        foreach (var batch in batches)
        {
            // 主工单存在性
            if (!mainWoDict.ContainsKey(batch.WorkOrderNo))
                continue;

            // 牌号匹配（同级或高级替代）
            if (!eligibleGrades.Contains(batch.PlantGrade))
                continue;

            // LengthStatus 一致
            if (batch.LengthStatus != subWorkOrder.LengthStatus.ToString())
                continue;

            // DeliveryState 一致
            if (batch.DeliveryState != subWorkOrder.DeliveryState.ToString())
                continue;

            // TechnicalRequirements 一致
            if (batch.TechnicalRequirements != subWorkOrder.TechnicalRequirements.ToString())
                continue;

            // 规格匹配：外径/壁厚范围法
            var mainNominalOd = SpecificationParser.ParseOuterDiameter(batch.Specification);
            var mainNominalWt = SpecificationParser.ParseWallThickness(batch.Specification);

            if (!mainNominalOd.HasValue || !mainNominalWt.HasValue || !subAvgOd.HasValue || !subAvgWt.HasValue)
                continue;

            var mainMinOd = mainNominalOd.Value - batch.OuterDiameterNegative;
            var mainMaxOd = mainNominalOd.Value + batch.OuterDiameterPositive;
            var mainMinWt = mainNominalWt.Value - batch.WallThicknessNegative;
            var mainMaxWt = mainNominalWt.Value + batch.WallThicknessPositive;

            if (subAvgOd.Value < mainMinOd || subAvgOd.Value > mainMaxOd)
                continue;
            if (subAvgWt.Value < mainMinWt || subAvgWt.Value > mainMaxWt)
                continue;

            result.Add(batch);
        }
        return result;
    }

    public async Task<List<InMainWorkOrderPlanDto>> GetInMainWorkOrderPlansAsync(int workOrderId)
    {
        var plans = await _context.InMainWorkOrderPlans
            .AsNoTracking()
            .Where(p => p.WorkOrderId == workOrderId)
            .OrderByDescending(p => p.CreatedTime)
            .ToListAsync();

        return plans.Select(p => p.ToDto()).ToList();
    }

    public async Task<InMainWorkOrderPlanDto> GetInMainWorkOrderPlanByIdAsync(int id)
    {
        var plan = await _context.InMainWorkOrderPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("在产主工单计划不存在");

        return plan.ToDto();
    }

    public async Task<InMainWorkOrderPlanDto> CreateInMainWorkOrderPlanAsync(CreateInMainWorkOrderPlanRequest request)
    {
        var workOrder = await _context.WorkOrders.FindAsync(request.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        var batch = await _context.ProductionBatches.FindAsync(request.ProductionBatchId);
        if (batch == null)
            throw new BusinessException("生产批次不存在");

        if (batch.Status != BatchStatus.None && batch.Status != BatchStatus.InProgress && batch.Status != BatchStatus.InFinalInspection)
            throw new BusinessException("只能选择未产、在产或成检状态的批次");

        if (request.AllocatedWeight <= 0)
            throw new BusinessException("分配重量必须大于0");
        if (request.AllocatedQuantity.HasValue && request.AllocatedQuantity <= 0)
            throw new BusinessException("分配支数必须大于0");

        // 可分配校验：本次分配重量 ≤ min(本批次有效投料−已预留, 主工单可分配剩余总重量)（跨工单可部分预留共享，含已投料计划）
        // 主工单可分配剩余总重量 = max(0, 总有效投料重量 − 原主工单号总重量 − 总预留重量)
        var mainRemaining = await GetMainOrderAllocatableRemainingAsync(request.WorkOrderId, batch.WorkOrderNo, null);
        var reservedMap = await GetInMainWorkOrderReservedMapAsync(null);
        reservedMap.TryGetValue(request.ProductionBatchId, out var reserved);

        var batchRemaining = (batch.CurrentValidWeight ?? 0m) - reserved.ReservedWeight;
        var weightRemaining = Math.Min(batchRemaining, mainRemaining);
        if (request.AllocatedWeight > weightRemaining)
            throw new BusinessException($"分配重量({request.AllocatedWeight})超过可用重量({Math.Max(0m, weightRemaining)})（本批次剩余{Math.Max(0m, batchRemaining)}kg、主工单剩余{Math.Max(0m, mainRemaining)}kg、已预留{reserved.ReservedWeight}kg）");

        var qtyRemaining = (batch.CurrentValidQty.HasValue ? batch.CurrentValidQty.Value : int.MaxValue) - reserved.ReservedQuantity;
        if (request.AllocatedQuantity.HasValue && request.AllocatedQuantity > qtyRemaining)
            throw new BusinessException($"分配支数({request.AllocatedQuantity})超过批次可用支数({Math.Max(0, qtyRemaining)})（已预留{reserved.ReservedQuantity}支）");

        // 取生产批次的剩余工量作为工艺周期，无剩余工量时使用默认工艺周期
        var defaultProcessCycle = (int)await GetConfigAsync("DefaultValue", "DefaultProcessCycle", 22m);
        var mainStandardCycle = batch.RemainingWorkDays > 0 ? batch.RemainingWorkDays : defaultProcessCycle;

        var plan = new InMainWorkOrderPlan
        {
            WorkOrderId = request.WorkOrderId,
            PlanDate = request.PlanDate,
            ProductionBatchId = request.ProductionBatchId,
            BatchNo = batch.BatchNo,
            MainWorkOrderNo = batch.WorkOrderNo,
            AllocatedWeight = request.AllocatedWeight,
            AllocatedQuantity = request.AllocatedQuantity,
            ProductionRatio = request.ProductionRatio,
            StandardCycle = mainStandardCycle,
            RequiredDate = request.RequiredDate,
            Remark = request.Remark,
        };

        _context.InMainWorkOrderPlans.Add(plan);
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                await _context.SaveChangesAsync();

                // 计算成品重量折扣系数
                var groupDiscountRate = await GetConfigAsync("ProcessingDiscount", "GroupDiscountRate", 0.025m);
                var effectiveGroupCount = await _context.ProcessGroups
                    .Where(pg => pg.ProductionBatchId == batch.Id)
                    .CountAsync(pg => pg.ProcessName != ProcessKeys.InProcessRepair
                        && pg.ProcessName != ProcessKeys.AdditionalFinalInspection);

                var discount = 1.0m - effectiveGroupCount * groupDiscountRate;
                if (discount < 0) discount = 0;

                var linkAllocatedQty = request.AllocatedQuantity;
                var linkAllocatedWeight = request.AllocatedWeight;

                await _context.SaveChangesAsync();

                await UpdateMaterialPlanStatusAsync(request.WorkOrderId);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(request.WorkOrderId);

        _logger.LogInformation("创建在产主工单计划成功: 工单ID {WorkOrderId}, 批次号 {BatchNo}, 主工单 {MainWorkOrderNo}, 重量 {Weight}",
            request.WorkOrderId, batch.BatchNo, batch.WorkOrderNo, request.AllocatedWeight);

        return plan.ToDto();
    }

    public async Task<InMainWorkOrderPlanDto> UpdateInMainWorkOrderPlanAsync(int id, CreateInMainWorkOrderPlanRequest request)
    {
        var plan = await _context.InMainWorkOrderPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("在产主工单计划不存在");

        // 完全锁死：在产主工单计划不可修改（只能删除后重建）
        throw new BusinessException("在产主工单计划不可修改（可删除后重建）");
    }

    public async Task DeleteInMainWorkOrderPlanAsync(int id)
    {
        var plan = await _context.InMainWorkOrderPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("在产主工单计划不存在");

        var workOrderId = plan.WorkOrderId;

        _context.InMainWorkOrderPlans.Remove(plan);
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
                await _context.SaveChangesAsync();
                await UpdateMaterialPlanStatusAsync(workOrderId);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        await RefreshReadModelAsync(workOrderId);

        _logger.LogInformation("删除在产主工单计划成功: ID {Id}", id);
    }

    public async Task<List<AvailableMainWorkOrderBatchDto>> GetAvailableMainWorkOrderBatchesAsync(int workOrderId, int? excludePlanId = null)
    {
        var subWorkOrder = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == workOrderId);
        if (subWorkOrder == null)
            throw new BusinessException("工单不存在");

        // 已引用批次不排除：本工单或其他工单已建计划的批次仍呈现，余量经预留账本扣减后显示净额（可继续追加），
        // 避免"已扣减批次直接消失"造成误导；创建校验按累计预留兜底拦截超量
        // 预留账本：跨工单可部分预留共享（含已投料计划），供显示净可用
        var reservedMap = await GetInMainWorkOrderReservedMapAsync(excludePlanId);

        // 查询批次
        var batchQuery = _context.ProductionBatches
            .AsNoTracking()
            .Where(b => b.Status == BatchStatus.None
                     || b.Status == BatchStatus.InProgress
                     || b.Status == BatchStatus.InFinalInspection)
            .Where(b => b.ManufacturingItem == "OrderFinished")
            .Where(b => b.CurrentValidWeight.HasValue && b.CurrentValidWeight > 0);

        var batches = await batchQuery.ToListAsync();

        // 获取批次关联的主工单信息
        var mainWoNos = batches.Select(b => b.WorkOrderNo).Distinct().ToList();
        var mainWorkOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => mainWoNos.Contains(wo.WorkOrderNo))
            .ToListAsync();
        var mainWoDict = mainWorkOrders.ToDictionary(wo => wo.WorkOrderNo, StringComparer.OrdinalIgnoreCase);

        // 构建主号分组键（订单号+主号）
        var mainNoKeys = mainWorkOrders
            .Select(wo => wo.SalesOrderNo + "|" + wo.ProductionMainNo)
            .Distinct()
            .ToList();

        // 从工单执行状况聚合主号级数据（原主工单号总重量 = 主号级需求）
        var mainNoAgg = new Dictionary<string, decimal>();
        if (mainNoKeys.Count > 0)
        {
            var summaries = await _context.WorkOrderExecutionSummaries
                .AsNoTracking()
                .Where(s => mainNoKeys.Contains(s.SalesOrderNo + "|" + s.ProductionMainNo))
                .ToListAsync();

            mainNoAgg = summaries
                .GroupBy(s => s.SalesOrderNo + "|" + s.ProductionMainNo)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.TotalWeight));
        }

        // 批次 WorkOrderNo → 主号分组键 映射
        var woToKeyMap = mainWorkOrders.ToDictionary(
            wo => wo.WorkOrderNo,
            wo => wo.SalesOrderNo + "|" + wo.ProductionMainNo,
            StringComparer.OrdinalIgnoreCase
        );

        // 过滤可用批次（主工单存在/牌号同级或高级替代/长度状态/交货状态/技术条件/规格 OD·WT 范围）
        // 与创建/更新校验共用 FilterMainWorkOrderBatches，避免口径漂移
        var eligibleBatches = FilterMainWorkOrderBatches(subWorkOrder, batches, mainWoDict);

        var result = new List<AvailableMainWorkOrderBatchDto>();

        foreach (var batch in eligibleBatches)
        {
            // 获取主号级聚合数据（原主工单号总重量）
            var mainKey = woToKeyMap[batch.WorkOrderNo];
            if (!mainNoAgg.TryGetValue(mainKey, out var mainTotalWeight))
                continue;

            result.Add(new AvailableMainWorkOrderBatchDto
            {
                Id = batch.Id,
                BatchNo = batch.BatchNo,
                TagNo = batch.TagNo,
                WorkOrderNo = batch.WorkOrderNo,
                PlantGrade = batch.PlantGrade,
                Specification = batch.Specification,
                LengthStatus = EnumHelper.TryParse<LengthStatus>(batch.LengthStatus),
                MaxLength = batch.MaxLength,
                Status = batch.Status,
                ProductionRatio = batch.ProductionRatio,
                CurrentValidQty = batch.CurrentValidQty,
                CurrentValidWeight = batch.CurrentValidWeight,
                MainTotalWeight = mainTotalWeight,
                ReservedQuantity = reservedMap.TryGetValue(batch.Id, out var reserved) ? reserved.ReservedQuantity : 0,
                ReservedWeight = reservedMap.TryGetValue(batch.Id, out var reserved2) ? reserved2.ReservedWeight : 0m
            });
        }

        // 主工单级聚合（按原主工单号）：总有效投料重量/总预留重量/可分配剩余总重量
        // 可分配剩余总重量 = max(0, 总有效投料 − 原主工单号总重量 − 总预留)；富余为负归 0，批次仍呈现（校验拦截超量）
        foreach (var group in result.GroupBy(b => b.WorkOrderNo, StringComparer.OrdinalIgnoreCase))
        {
            var totalValid = group.Sum(b => b.CurrentValidWeight ?? 0m);
            var totalReserved = group.Sum(b => b.ReservedWeight);
            var mainTotal = group.First().MainTotalWeight;
            var remaining = Math.Max(0m, totalValid - mainTotal - totalReserved);
            foreach (var dto in group)
            {
                dto.MainNoTotalValidWeight = totalValid;
                dto.MainNoTotalReservedWeight = totalReserved;
                dto.MainNoAllocatableRemaining = remaining;
            }
        }

        return result;
    }

    public async Task<List<PendingPlanBatchDto>> GetPendingInMainWorkOrderPlansAsync()
    {
        var result = await _context.InMainWorkOrderPlans
            .AsNoTracking()
            .Where(p => p.PlanStatus == InventoryPlanStatus.Planned)
            .Join(_context.ProductionBatches.AsNoTracking(),
                p => p.ProductionBatchId,
                b => b.Id,
                (p, b) => new { p, b })
            .Join(_context.WorkOrders.AsNoTracking(),
                j => j.p.WorkOrderId,
                wo => wo.Id,
                (j, wo) => new PendingPlanBatchDto
                {
                    BatchNo = j.b.BatchNo,
                    WorkOrderNo = wo.WorkOrderNo,
                    PlanType = "在产工单分配",
                    PlanId = j.p.Id,
                    RequiredQuantity = j.p.AllocatedQuantity,
                    RequiredWeight = j.p.AllocatedWeight
                })
            .ToListAsync();

        return result;
    }

    /// <summary>
    /// 根据批次ID消除所有待处理的在产主工单计划通知（有效量变更时触发）
    /// </summary>
    public async Task DismissInMainWorkOrderPlanByBatchAndWorkOrderAsync(int productionBatchId, string subWorkOrderNo)
    {
        var plans = await _context.InMainWorkOrderPlans
            .Where(p => p.ProductionBatchId == productionBatchId
                     && p.PlanStatus == InventoryPlanStatus.Planned)
            .Join(_context.WorkOrders,
                p => p.WorkOrderId,
                wo => wo.Id,
                (p, wo) => new { Plan = p, WorkOrder = wo })
            .Where(j => j.WorkOrder.WorkOrderNo == subWorkOrderNo)
            .Select(j => j.Plan)
            .ToListAsync();

        if (plans.Count == 0) return;

        foreach (var plan in plans)
            plan.PlanStatus = InventoryPlanStatus.Completed;

        await _context.SaveChangesAsync();

        foreach (var group in plans.GroupBy(p => p.WorkOrderId))
        {
            await UpdateMaterialPlanStatusAsync(group.Key);
            await RefreshReadModelAsync(group.Key);
        }
    }

    public async Task DismissInMainWorkOrderPlansByBatchAsync(int productionBatchId)
    {
        var plans = await _context.InMainWorkOrderPlans
            .Where(p => p.ProductionBatchId == productionBatchId
                     && p.PlanStatus == InventoryPlanStatus.Planned)
            .ToListAsync();

        if (plans.Count == 0) return;

        foreach (var plan in plans)
            plan.PlanStatus = InventoryPlanStatus.Completed;

        await _context.SaveChangesAsync();

        // 触发对应工单的用料状态刷新
        foreach (var group in plans.GroupBy(p => p.WorkOrderId))
        {
            await UpdateMaterialPlanStatusAsync(group.Key);
            await RefreshReadModelAsync(group.Key);
        }
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
        WoEntity workOrder, CreatePurchaseSemiPlanRequest request)
    {
        var result = new MaterialCalculateResult();

        // 1. 查询密度（从牌号对表按工厂牌号查找）
        var gradeMapping = await _context.StandardGradeMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.PlantGrade == workOrder.PlantGrade);
        result.Density = gradeMapping?.Density ?? 7.93m; // 默认密度

        // 从配置表读取默认长度
        var pipeLength = await GetConfigAsync("LengthDefault", "PipeLength", 6000m);

        // 2. 解析外径
        var odOrNull = SpecificationParser.ParseOuterDiameter(workOrder.Specification);

        // 3. 单米重量(kg/m) = π × 密度 × 调整壁厚 × (外径 - 调整壁厚) / 1000
        var adjustedWT = request.AdjustedWallThickness;
        if (odOrNull.HasValue)
        {
            result.UnitWeightPerMeter = Math.Round(
                (decimal)Math.PI * result.Density * adjustedWT * (odOrNull.Value - adjustedWT) / 1000m, 6);
        }

        // 4. 非定尺：不计算单重
        if (workOrder.LengthStatus == LengthStatus.NonFixed)
        {
            return result;
        }

        // 5. 成品单重(kg/支) = 单米重量 × 最大长度(m) / 1000
        var maxLengthM = (workOrder.MaxLength ?? (int)pipeLength) / 1000m;
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

    #endregion

    #region 计划状态

    public async Task<WorkOrderMaterialPlanDto> GetWorkOrderMaterialPlanAsync(int workOrderId)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == workOrderId);

        if (workOrder == null)
            throw new BusinessException("工单不存在");

        var fixedSatisfied = await GetConfigAsync("MaterialPlanStatus", "FixedSatisfied", 110m);
        var nonFixedSatisfied = await GetConfigAsync("MaterialPlanStatus", "NonFixedSatisfied", 120m);

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
            var status = CalculatePlanStatus(workOrder, semiPlans, isSemi: true,
                fixedSatisfied: fixedSatisfied, nonFixedSatisfied: nonFixedSatisfied);
            dto.Items.Add(new MaterialPlanItemDto
            {
                PlanType = "Semi",
                PlanTypeText = "原料采购",
                RecordCount = semiPlans.Count,
                Summary = $"{semiPlans.First().RawMaterialSpec} × {semiPlans.Sum(p => p.RequiredPieces ?? 0)}支 / {semiPlans.Sum(p => p.RequiredWeight):G29}kg",
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
            var status = CalculatePlanStatus(workOrder, finishPlans, isSemi: false,
                fixedSatisfied: fixedSatisfied, nonFixedSatisfied: nonFixedSatisfied);
            dto.Items.Add(new MaterialPlanItemDto
            {
                PlanType = "Finished",
                PlanTypeText = "成品采购",
                RecordCount = finishPlans.Count,
                Summary = $"{finishPlans.First().ProductType} × {finishPlans.Sum(p => p.RequiredPiece ?? 0)}支 / {finishPlans.Sum(p => p.RequiredWeight):G29}kg",
                RequiredDate = finishPlans.Min(p => p.RequiredDate),
                Status = status
            });
        }

        // 库存使用计划
        var inventoryPlans = await _context.InventoryPlans
            .Where(p => p.WorkOrderId == workOrderId && p.PlanStatus != InventoryPlanStatus.Cancelled)
            .ToListAsync();

        var regularInventory = inventoryPlans.Where(p => p.ReworkType == null).ToList();
        var reworkPlans = inventoryPlans.Where(p => p.ReworkType != null).ToList();

        if (regularInventory.Any())
        {
            var status = CalculateInventoryPlanStatus(workOrder, regularInventory, isRework: false,
                fixedSatisfied: fixedSatisfied, nonFixedSatisfied: nonFixedSatisfied);
            dto.Items.Add(new MaterialPlanItemDto
            {
                PlanType = "Inventory",
                PlanTypeText = "库存使用",
                RecordCount = regularInventory.Count,
                Summary = $"{regularInventory.First().BatchNo} × {regularInventory.Sum(p => p.UsedQuantity ?? 0)}支 / {regularInventory.Sum(p => p.UsedWeight):G29}kg",
                RequiredDate = regularInventory.Min(p => p.RequiredDate),
                Status = status
            });
        }

        if (reworkPlans.Any())
        {
            var status = CalculateInventoryPlanStatus(workOrder, reworkPlans, isRework: true,
                fixedSatisfied: fixedSatisfied, nonFixedSatisfied: nonFixedSatisfied);
            dto.Items.Add(new MaterialPlanItemDto
            {
                PlanType = "Rework",
                PlanTypeText = "库料改制",
                RecordCount = reworkPlans.Count,
                Summary = $"{reworkPlans.First().BatchNo} × {reworkPlans.Sum(p => p.UsedQuantity ?? 0)}支 / {reworkPlans.Sum(p => p.UsedWeight):G29}kg",
                RequiredDate = reworkPlans.Min(p => p.RequiredDate),
                Status = status
            });
        }

        // 圆棒穿孔计划
        var piercingPlans = await _context.RoundBarPiercingPlans
            .Where(p => p.WorkOrderId == workOrderId)
            .ToListAsync();
        if (piercingPlans.Any())
        {
            var status = CalculatePlanStatus(workOrder, piercingPlans.Cast<BaseEntity>().ToList(), isSemi: false, isPiercing: true,
                fixedSatisfied: fixedSatisfied, nonFixedSatisfied: nonFixedSatisfied);
            dto.Items.Add(new MaterialPlanItemDto
            {
                PlanType = "Piercing",
                PlanTypeText = "圆棒穿孔",
                RecordCount = piercingPlans.Count,
                Summary = $"{piercingPlans.First().RoundBarSpec} → {piercingPlans.First().PiercingSpec} × {piercingPlans.Sum(p => p.RequiredPieces ?? 0)}支 / {piercingPlans.Sum(p => p.RequiredWeight):G29}kg",
                RequiredDate = piercingPlans.Min(p => p.RequiredDate),
                Status = status
            });
        }

        // 在产改制计划
        var inProcessReworkPlans = await _context.InProcessReworkPlans
            .Where(p => p.WorkOrderId == workOrderId && p.PlanStatus != InventoryPlanStatus.Cancelled)
            .ToListAsync();
        if (inProcessReworkPlans.Any())
        {
            var inProcessRate = CalculateInProcessReworkPlanRate(workOrder, inProcessReworkPlans);
            var inProcessStatus = CalculateOverallStatus(workOrder, inProcessRate,
                fixedSatisfied, nonFixedSatisfied);
            dto.Items.Add(new MaterialPlanItemDto
            {
                PlanType = "InProcessRework",
                PlanTypeText = "在产改制",
                RecordCount = inProcessReworkPlans.Count,
                Summary = $"{inProcessReworkPlans.First().BatchNo} × {inProcessReworkPlans.Sum(p => p.UsedQuantity ?? 0)}支 / {inProcessReworkPlans.Sum(p => p.UsedWeight):G29}kg",
                RequiredDate = inProcessReworkPlans.Min(p => p.RequiredDate),
                Status = inProcessStatus
            });
        }

        // 在产主工单计划
        var inMainWorkOrderPlans = await _context.InMainWorkOrderPlans
            .Where(p => p.WorkOrderId == workOrderId && p.PlanStatus != InventoryPlanStatus.Cancelled)
            .ToListAsync();
        if (inMainWorkOrderPlans.Any())
        {
            var inMainRate = CalculateInMainWorkOrderPlanRate(workOrder, inMainWorkOrderPlans);
            var inMainStatus = CalculateOverallStatus(workOrder, inMainRate,
                fixedSatisfied, nonFixedSatisfied);
            dto.Items.Add(new MaterialPlanItemDto
            {
                PlanType = "InMainWorkOrder",
                PlanTypeText = "在产主工单",
                RecordCount = inMainWorkOrderPlans.Count,
                Summary = $"{inMainWorkOrderPlans.First().MainWorkOrderNo} × {inMainWorkOrderPlans.Sum(p => p.AllocatedQuantity ?? 0)}支 / {inMainWorkOrderPlans.Sum(p => p.AllocatedWeight):G29}kg",
                RequiredDate = inMainWorkOrderPlans.Min(p => p.RequiredDate),
                Status = inMainStatus
            });
        }

        return dto;
    }

    public async Task UpdateMaterialPlanStatusAsync(int workOrderId)
    {
        var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
        if (workOrder == null)
            return;

        var fixedSatisfied = await GetConfigAsync("MaterialPlanStatus", "FixedSatisfied", 110m);
        var nonFixedSatisfied = await GetConfigAsync("MaterialPlanStatus", "NonFixedSatisfied", 120m);

        var semiPlans = await _context.PurchaseSemiPlans
            .Where(p => p.WorkOrderId == workOrderId)
            .ToListAsync();

        var finishPlans = await _context.PurchaseFinishedPlans
            .Where(p => p.WorkOrderId == workOrderId)
            .ToListAsync();

        var inventoryPlans = await _context.InventoryPlans
            .Where(p => p.WorkOrderId == workOrderId && p.PlanStatus != InventoryPlanStatus.Cancelled)
            .ToListAsync();

        var regularInventory = inventoryPlans.Where(p => p.ReworkType == null).ToList();
        var reworkPlans = inventoryPlans.Where(p => p.ReworkType != null).ToList();

        var piercingPlans = await _context.RoundBarPiercingPlans
            .Where(p => p.WorkOrderId == workOrderId)
            .ToListAsync();

        var inProcessReworkPlans = await _context.InProcessReworkPlans
            .Where(p => p.WorkOrderId == workOrderId && p.PlanStatus != InventoryPlanStatus.Cancelled)
            .ToListAsync();

        var inMainWorkOrderPlans = await _context.InMainWorkOrderPlans
            .Where(p => p.WorkOrderId == workOrderId && p.PlanStatus != InventoryPlanStatus.Cancelled)
            .ToListAsync();

        var hasSemi = semiPlans.Any();
        var hasFinish = finishPlans.Any();
        var hasInventory = regularInventory.Any();
        var hasRework = reworkPlans.Any();
        var hasPiercing = piercingPlans.Any();
        var hasInProcessRework = inProcessReworkPlans.Any();
        var hasInMain = inMainWorkOrderPlans.Any();

        if (!hasSemi && !hasFinish && !hasInventory && !hasRework && !hasPiercing && !hasInProcessRework && !hasInMain)
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
                var s = CalculatePlanStatus(workOrder, semiPlans, isSemi: true,
                    fixedSatisfied: fixedSatisfied, nonFixedSatisfied: nonFixedSatisfied);
                statuses.Add(s);
                rates.Add(CalculatePlanRate(workOrder, semiPlans, isSemi: true));
            }

            if (hasFinish)
            {
                var s = CalculatePlanStatus(workOrder, finishPlans, isSemi: false,
                    fixedSatisfied: fixedSatisfied, nonFixedSatisfied: nonFixedSatisfied);
                statuses.Add(s);
                rates.Add(CalculatePlanRate(workOrder, finishPlans, isSemi: false));
            }

            if (hasInventory)
            {
                var s = CalculateInventoryPlanStatus(workOrder, regularInventory, isRework: false,
                    fixedSatisfied: fixedSatisfied, nonFixedSatisfied: nonFixedSatisfied);
                statuses.Add(s);
                rates.Add(CalculateInventoryPlanRate(workOrder, regularInventory));
            }

            if (hasRework)
            {
                var s = CalculateInventoryPlanStatus(workOrder, reworkPlans, isRework: true,
                    fixedSatisfied: fixedSatisfied, nonFixedSatisfied: nonFixedSatisfied);
                statuses.Add(s);
                rates.Add(CalculateInventoryPlanRate(workOrder, reworkPlans));
            }

            if (hasPiercing)
            {
                var s = CalculatePlanStatus(workOrder, piercingPlans.Cast<BaseEntity>().ToList(), isSemi: false, isPiercing: true,
                    fixedSatisfied: fixedSatisfied, nonFixedSatisfied: nonFixedSatisfied);
                statuses.Add(s);
                rates.Add(CalculatePlanRate(workOrder, piercingPlans.Cast<BaseEntity>().ToList(), isSemi: false, isPiercing: true));
            }

            if (hasInProcessRework)
            {
                var rate = CalculateInProcessReworkPlanRate(workOrder, inProcessReworkPlans);
                rates.Add(rate);
            }

            if (hasInMain)
            {
                var rate = CalculateInMainWorkOrderPlanRate(workOrder, inMainWorkOrderPlans);
                rates.Add(rate);
            }

            // 工单满足率 = 7种用料相加（总覆盖率）
            var totalRate = Math.Min(rates.Sum(), 999m);
            workOrder.MaterialPlanRate = totalRate;
            workOrder.MaterialPlanStatus = CalculateOverallStatus(workOrder, totalRate,
                fixedSatisfied: fixedSatisfied, nonFixedSatisfied: nonFixedSatisfied);
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 计算单个计划的状态（工单级，满足率<100%=部分；100%~上限=满足；超出=超量）
    /// </summary>
    private MaterialPlanStatus CalculatePlanStatus(WoEntity workOrder,
        IReadOnlyCollection<BaseEntity> plans, bool isSemi, bool isPiercing = false,
        decimal fixedSatisfied = 110m, decimal nonFixedSatisfied = 120m)
    {
        var rate = CalculatePlanRate(workOrder, plans, isSemi, isPiercing);

        if (workOrder.LengthStatus == LengthStatus.Fixed)
        {
            // 定尺：支数模式
            if (rate < 100m) return MaterialPlanStatus.Partial;
            if (rate <= fixedSatisfied) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
        else
        {
            // 范围尺/非定尺：重量模式
            if (rate < 100m) return MaterialPlanStatus.Partial;
            if (rate <= nonFixedSatisfied) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
    }

    /// <summary>
    /// 计算满足率
    /// </summary>
    private decimal CalculatePlanRate(WoEntity workOrder,
        IReadOnlyCollection<BaseEntity> plans, bool isSemi, bool isPiercing = false)
    {
        if (workOrder.LengthStatus == LengthStatus.Fixed)
        {
            // 定尺：按支数
            int effectivePieces;

            if (isSemi)
            {
                // 原料采购：原料支数 × 投料倍率
                var semiPlans = plans.Cast<PurchaseSemiPlan>();
                effectivePieces = (int)semiPlans.Sum(p => (p.RequiredPieces ?? 0) * p.InputMultiple);
            }
            else if (isPiercing)
            {
                // 圆棒穿孔：原料支数 × 投料倍率（同原料采购逻辑）
                var piercingPlans = plans.Cast<RoundBarPiercingPlan>();
                effectivePieces = (int)piercingPlans.Sum(p => (p.RequiredPieces ?? 0) * p.InputMultiple);
            }
            else
            {
                // 成品采购：直接按实际采购支数
                var finishPlans = plans.Cast<PurchaseFinishedPlan>();
                effectivePieces = finishPlans.Sum(p => p.RequiredPiece ?? 0);
            }

            if (workOrder.TotalQuantity <= 0) return 0;
            return Math.Round((decimal)effectivePieces / workOrder.TotalQuantity * 100m, 0);
        }
        else
        {
            // 范围尺/非定尺：按重量
            decimal effectiveWeight;

            if (isSemi)
            {
                var semiPlans = plans.Cast<PurchaseSemiPlan>();
                effectiveWeight = semiPlans.Sum(p => p.RequiredWeight);
            }
            else if (isPiercing)
            {
                // 圆棒穿孔：按需求重量（同原料采购逻辑）
                var piercingPlans = plans.Cast<RoundBarPiercingPlan>();
                effectiveWeight = piercingPlans.Sum(p => p.RequiredWeight);
            }
            else
            {
                // 成品采购：直接按实际采购重量
                var finishPlans = plans.Cast<PurchaseFinishedPlan>();
                effectiveWeight = finishPlans.Sum(p => p.RequiredWeight);
            }

            if (workOrder.TotalWeight <= 0) return 0;
            return Math.Round(effectiveWeight / workOrder.TotalWeight * 100m, 0);
        }
    }

    /// <summary>
    /// 计算库存使用计划满足率
    /// </summary>
    private decimal CalculateInventoryPlanRate(WoEntity workOrder,
        IReadOnlyCollection<InventoryPlan> plans)
    {
        if (workOrder.LengthStatus == LengthStatus.Fixed)
        {
            // 定尺：按支数，直接按实际出库支数 × 投料倍率
            var effectivePieces = (int)(plans.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple));

            if (workOrder.TotalQuantity <= 0) return 0;
            return Math.Round((decimal)effectivePieces / workOrder.TotalQuantity * 100m, 0);
        }
        else
        {
            // 范围尺/非定尺：按重量，直接按实际出库重量
            var effectiveWeight = plans.Sum(p => p.UsedWeight);

            if (workOrder.TotalWeight <= 0) return 0;
            return Math.Round(effectiveWeight / workOrder.TotalWeight * 100m, 0);
        }
    }

    /// <summary>
    /// 计算在产改制计划满足率
    /// </summary>
    private decimal CalculateInProcessReworkPlanRate(WoEntity workOrder,
        IReadOnlyCollection<InProcessReworkPlan> plans)
    {
        if (workOrder.LengthStatus == LengthStatus.Fixed)
        {
            var effectivePieces = (int)(plans.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple));
            if (workOrder.TotalQuantity <= 0) return 0;
            return Math.Round((decimal)effectivePieces / workOrder.TotalQuantity * 100m, 0);
        }
        else
        {
            var effectiveWeight = plans.Sum(p => p.UsedWeight);
            if (workOrder.TotalWeight <= 0) return 0;
            return Math.Round(effectiveWeight / workOrder.TotalWeight * 100m, 0);
        }
    }

    /// <summary>
    /// 计算在产主工单计划满足率
    /// </summary>
    private decimal CalculateInMainWorkOrderPlanRate(WoEntity workOrder,
        IReadOnlyCollection<InMainWorkOrderPlan> plans)
    {
        if (workOrder.LengthStatus == LengthStatus.Fixed)
        {
            var effectivePieces = plans.Sum(p => p.AllocatedQuantity ?? 0);
            if (workOrder.TotalQuantity <= 0) return 0;
            return Math.Round((decimal)effectivePieces / workOrder.TotalQuantity * 100m, 0);
        }
        else
        {
            var effectiveWeight = plans.Sum(p => p.AllocatedWeight);
            if (workOrder.TotalWeight <= 0) return 0;
            return Math.Round(effectiveWeight / workOrder.TotalWeight * 100m, 0);
        }
    }

    /// <summary>
    /// 计算库存使用计划状态（工单级，满足率<100%=部分；100%~上限=满足；超出=超量）
    /// </summary>
    private MaterialPlanStatus CalculateInventoryPlanStatus(WoEntity workOrder,
        IReadOnlyCollection<InventoryPlan> plans, bool isRework = false,
        decimal fixedSatisfied = 110m, decimal nonFixedSatisfied = 120m)
    {
        var rate = CalculateInventoryPlanRate(workOrder, plans);

        if (workOrder.LengthStatus == LengthStatus.Fixed)
        {
            if (rate < 100m) return MaterialPlanStatus.Partial;
            if (rate <= fixedSatisfied) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
        else
        {
            if (rate < 100m) return MaterialPlanStatus.Partial;
            if (rate <= nonFixedSatisfied) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
    }

    /// <summary>
    /// 基于总满足率计算整体状态（工单级，满足率<100%=部分；100%~上限=满足；超出=超量）
    /// </summary>
    private static MaterialPlanStatus CalculateOverallStatus(WoEntity workOrder, decimal totalRate,
        decimal fixedSatisfied = 110m, decimal nonFixedSatisfied = 120m)
    {
        if (totalRate <= 0) return MaterialPlanStatus.NotPlanned;

        if (workOrder.LengthStatus == LengthStatus.Fixed)
        {
            if (totalRate < 100m) return MaterialPlanStatus.Partial;
            if (totalRate <= fixedSatisfied) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
        else
        {
            if (totalRate < 100m) return MaterialPlanStatus.Partial;
            if (totalRate <= nonFixedSatisfied) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
    }

    private async Task RefreshReadModelAsync(int workOrderId)
    {
        try
        {
            var wo = await _context.WorkOrders
                .AsNoTracking()
                .Where(w => w.Id == workOrderId)
                .Select(w => new { w.SalesOrderNo, w.WorkOrderNo })
                .FirstOrDefaultAsync();

            if (wo != null && !string.IsNullOrEmpty(wo.SalesOrderNo))
            {
                await _readModelRefreshService.RefreshBySalesOrderAsync(wo.SalesOrderNo);
                await TryRefreshExecutionSummaryAsync(wo.WorkOrderNo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读模型刷新失败（不影响主流程）: WorkOrderId={WorkOrderId}", workOrderId);
        }
    }

    public async Task RecalculateStandardCycleForBatchAsync(string batchNo)
    {
        // 查找该批次关联的改制库存计划
        var plans = await _context.InventoryPlans
            .Where(p => p.BatchNo == batchNo && p.ReworkType != null)
            .ToListAsync();
        if (plans.Count == 0) return;

        // 加载批次及其工序组
        var batch = await _context.ProductionBatches
            .Include(b => b.ProcessGroups.OrderBy(pg => pg.SequenceNumber))
            .FirstOrDefaultAsync(b => b.BatchNo == batchNo);
        if (batch?.ProcessGroups == null || batch.ProcessGroups.Count == 0) return;

        // 从工序组提取工段
        var sections = new List<(string SectionName, int Sequence)>();
        foreach (var pg in batch.ProcessGroups)
        {
            sections.AddRange(ExtractSections(
                pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.EmulsionWash,
                pg.UltrasonicWash, pg.ClothPolish, pg.BrightAnnealing, pg.Solution,
                pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                pg.OuterPolish, pg.InnerPolish, pg.InnerGrinding, pg.OuterSpotGrinding,
                pg.SandBlasting, pg.ShotBlasting, pg.Inspection, pg.WeldingHead,
                pg.Welding, pg.Lubrication, pg.Packing, pg.Warehouse,
                pg.Extra1, pg.Extra2));
        }

        // 计算工艺周期：按计划关联工单的交货状态逐计划计算（补交货状态附加天数，避免传 null 丢失）
        var dayMap = await _standardWorkDayService.GetStandardDaysMapAsync(batch.PlantGrade);
        var deliveryStateExtraDays = await _deliveryStateService.GetDeliveryStateExtraDaysMapAsync();
        var planWorkOrderIds = plans.Select(p => p.WorkOrderId).Distinct().ToList();
        var workOrderDeliveryStates = await _context.WorkOrders
            .Where(w => planWorkOrderIds.Contains(w.Id))
            .Select(w => new { w.Id, w.DeliveryState })
            .ToDictionaryAsync(w => w.Id, w => w.DeliveryState.ToString());
        var defaultCycle = (int)await GetConfigAsync("DefaultValue", "StandardCycle", 3m);

        // 更新所有关联的改制库存计划
        foreach (var plan in plans)
        {
            workOrderDeliveryStates.TryGetValue(plan.WorkOrderId, out var deliveryState);
            var standardCycle = CalculateStandardCycleFromSections(sections, dayMap, deliveryStateExtraDays, deliveryState);
            if (standardCycle == 0)
                standardCycle = defaultCycle;
            plan.StandardCycle = standardCycle;
            _context.Entry(plan).Property(e => e.StandardCycle).IsModified = true;
        }
        await _context.SaveChangesAsync();

        // 刷新读模型
        var workOrderIds = plans.Select(p => p.WorkOrderId).Distinct().ToList();
        foreach (var woId in workOrderIds)
        {
            await RefreshReadModelAsync(woId);
        }
    }

    private async Task TryRefreshExecutionSummaryAsync(string? workOrderNo)
    {
        if (string.IsNullOrWhiteSpace(workOrderNo)) return;
        try
        {
            await _workOrderExecutionService.RefreshByWorkOrderNosAsync(new List<string> { workOrderNo });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工单执行状况刷新失败（不影响主流程）: WorkOrderNo={WorkOrderNo}", workOrderNo);
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

        return MaterialPlanPrintHelper.GenerateSemiPlanPdf(plan, workOrder);
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

        return MaterialPlanPrintHelper.GenerateFinishPlanPdf(plan, workOrder);
    }

    public async Task<byte[]> PrintInventoryPlanAsync(int planId)
    {
        var plan = await _context.InventoryPlans.FindAsync(planId);
        if (plan == null)
            throw new BusinessException("库存使用计划不存在");

        var workOrder = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == plan.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        return MaterialPlanPrintHelper.GenerateInventoryPlanPdf(plan, workOrder);
    }

    public async Task<byte[]> PrintPiercingPlanAsync(int planId)
    {
        var plan = await _context.RoundBarPiercingPlans.FindAsync(planId);
        if (plan == null)
            throw new BusinessException("圆棒穿孔计划不存在");

        var workOrder = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == plan.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        return MaterialPlanPrintHelper.GeneratePiercingPlanPdf(plan, workOrder);
    }

    public async Task<byte[]> PrintReworkPlanAsync(int planId)
    {
        var plan = await _context.InventoryPlans.FindAsync(planId);
        if (plan == null)
            throw new BusinessException("库料改制计划不存在");

        var workOrder = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == plan.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        return MaterialPlanPrintHelper.GenerateReworkPlanPdf(plan, workOrder);
    }

    public async Task<byte[]> PrintInProcessReworkPlanAsync(int planId)
    {
        var plan = await _context.InProcessReworkPlans.FindAsync(planId);
        if (plan == null)
            throw new BusinessException("在产改制计划不存在");

        var workOrder = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == plan.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        return MaterialPlanPrintHelper.GenerateInProcessReworkPlanPdf(plan, workOrder);
    }

    public async Task<byte[]> PrintInMainWorkOrderPlanAsync(int planId)
    {
        var plan = await _context.InMainWorkOrderPlans.FindAsync(planId);
        if (plan == null)
            throw new BusinessException("在产主工单计划不存在");

        var workOrder = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == plan.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        return MaterialPlanPrintHelper.GenerateInMainWorkOrderPlanPdf(plan, workOrder);
    }

    public async Task<byte[]> PrintSelectedPlansAsync(MaterialPlanBatchPrintRequest request)
    {
        var workOrderIds = request.WorkOrderIds;
        if (workOrderIds.Length == 0)
            throw new BusinessException("请选择工单");

        // 批量查询工单，避免 N+1
        var workOrders = await _context.WorkOrders.AsNoTracking()
            .Where(wo => workOrderIds.Contains(wo.Id))
            .ToDictionaryAsync(wo => wo.Id);

        // 按计划类型批量查询，固定最多 6 次数据库查询
        var semiItems = new List<(PurchaseSemiPlan, WoEntity)>();
        var finishItems = new List<(PurchaseFinishedPlan, WoEntity)>();
        var inventoryItems = new List<(InventoryPlan, WoEntity)>();
        var reworkItems = new List<(InventoryPlan, WoEntity)>();
        var piercingItems = new List<(RoundBarPiercingPlan, WoEntity)>();
        var inProcessReworkItems = new List<(InProcessReworkPlan, WoEntity)>();
        var inMainWorkOrderItems = new List<(InMainWorkOrderPlan, WoEntity)>();

        if (request.IncludeSemi)
        {
            var plans = await _context.PurchaseSemiPlans
                .Where(p => workOrderIds.Contains(p.WorkOrderId))
                .ToListAsync();
            semiItems = plans
                .Where(p => workOrders.ContainsKey(p.WorkOrderId))
                .Select(p => (p, workOrders[p.WorkOrderId]))
                .ToList();
        }

        if (request.IncludeFinish)
        {
            var plans = await _context.PurchaseFinishedPlans
                .Where(p => workOrderIds.Contains(p.WorkOrderId))
                .ToListAsync();
            finishItems = plans
                .Where(p => workOrders.ContainsKey(p.WorkOrderId))
                .Select(p => (p, workOrders[p.WorkOrderId]))
                .ToList();
        }

        if (request.IncludeInventory)
        {
            var plans = await _context.InventoryPlans
                .Where(p => workOrderIds.Contains(p.WorkOrderId) && p.ReworkType == null)
                .ToListAsync();
            inventoryItems = plans
                .Where(p => workOrders.ContainsKey(p.WorkOrderId))
                .Select(p => (p, workOrders[p.WorkOrderId]))
                .ToList();
        }

        if (request.IncludeRework)
        {
            var plans = await _context.InventoryPlans
                .Where(p => workOrderIds.Contains(p.WorkOrderId) && p.ReworkType != null)
                .ToListAsync();
            reworkItems = plans
                .Where(p => workOrders.ContainsKey(p.WorkOrderId))
                .Select(p => (p, workOrders[p.WorkOrderId]))
                .ToList();
        }

        if (request.IncludeRoundBarPiercing)
        {
            var plans = await _context.RoundBarPiercingPlans
                .Where(p => workOrderIds.Contains(p.WorkOrderId))
                .ToListAsync();
            piercingItems = plans
                .Where(p => workOrders.ContainsKey(p.WorkOrderId))
                .Select(p => (p, workOrders[p.WorkOrderId]))
                .ToList();
        }

        if (request.IncludeInProcessRework)
        {
            var plans = await _context.InProcessReworkPlans
                .Where(p => workOrderIds.Contains(p.WorkOrderId))
                .ToListAsync();
            inProcessReworkItems = plans
                .Where(p => workOrders.ContainsKey(p.WorkOrderId))
                .Select(p => (p, workOrders[p.WorkOrderId]))
                .ToList();
        }

        if (request.IncludeInMainWorkOrder)
        {
            var plans = await _context.InMainWorkOrderPlans
                .Where(p => workOrderIds.Contains(p.WorkOrderId))
                .ToListAsync();
            inMainWorkOrderItems = plans
                .Where(p => workOrders.ContainsKey(p.WorkOrderId))
                .Select(p => (p, workOrders[p.WorkOrderId]))
                .ToList();
        }

        // 按计划类型生成汇总文档
        var documents = new List<IDocument>();

        if (semiItems.Any())
            documents.Add(MaterialPlanPrintHelper.CreateBatchSemiPlanDocument(semiItems));
        if (finishItems.Any())
            documents.Add(MaterialPlanPrintHelper.CreateBatchFinishPlanDocument(finishItems));
        if (inventoryItems.Any())
            documents.Add(MaterialPlanPrintHelper.CreateBatchInventoryPlanDocument(inventoryItems));
        if (reworkItems.Any())
            documents.Add(MaterialPlanPrintHelper.CreateBatchReworkPlanDocument(reworkItems));
        if (piercingItems.Any())
            documents.Add(MaterialPlanPrintHelper.CreateBatchPiercingPlanDocument(piercingItems));
        if (inProcessReworkItems.Any())
            documents.Add(MaterialPlanPrintHelper.CreateBatchInProcessReworkPlanDocument(inProcessReworkItems));
        if (inMainWorkOrderItems.Any())
            documents.Add(MaterialPlanPrintHelper.CreateBatchInMainWorkOrderPlanDocument(inMainWorkOrderItems));

        if (documents.Count == 0)
            throw new BusinessException("没有找到符合条件的计划");

        if (documents.Count == 1)
            return documents[0].GeneratePdf();

        return Document.Merge(documents).GeneratePdf();
    }

    #endregion

    // ========== 仓库通知 ==========

    public async Task<List<PendingPlanBatchDto>> GetPendingPlanBatchesByWarehouseAsync(int warehouseId)
    {
        return await _context.InventoryPlans
            .AsNoTracking()
            .Where(p => p.PlanStatus == InventoryPlanStatus.Planned)
            .Join(_context.InventoryBatches.AsNoTracking(),
                p => p.InventoryBatchNo,
                b => b.BatchNo,
                (p, b) => new { p, b })
            .Where(j => j.b.WarehouseId == warehouseId)
            .Join(_context.WorkOrders.AsNoTracking(),
                j => j.p.WorkOrderId,
                wo => wo.Id,
                (j, wo) => new { p = j.p, wo })
            // 完成匹配：仓库批 + 出库工单号 == 计划工单号 且 出库类型=生产领用
            .Where(j => !_context.OutboundRecords.Any(or =>
                or.BatchNo != null
                && or.BatchNo == j.p.InventoryBatchNo
                && or.OutboundType == OutboundType.ProductionPick
                && or.WorkOrderNo != null
                && or.WorkOrderNo == j.wo.WorkOrderNo))
            .Select(j => new PendingPlanBatchDto
            {
                BatchNo = j.p.BatchNo,
                WorkOrderNo = j.wo.WorkOrderNo,
                PlanType = j.p.ReworkType != null ? "库料改制" : "库存使用",
                PlanId = j.p.Id,
                RequiredQuantity = j.p.UsedQuantity,
                RequiredWeight = j.p.UsedWeight
            })
            .ToListAsync();
    }

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
            PlantGrade = entity.PlantGrade,
            RawMaterialType = entity.RawMaterialType,
            RawMaterialSpec = entity.RawMaterialSpec,
            RequiredUnitWeight = entity.RequiredUnitWeight,
            RequiredPieces = entity.RequiredPieces,
            RequiredWeight = entity.RequiredWeight,
            RequiredDate = entity.RequiredDate,
            Remark = entity.Remark,
            StandardCycle = entity.StandardCycle,
            CreatedTime = entity.CreatedTime,
            CreatedBy = entity.CreatedBy
        };
    }

    public static RoundBarPiercingPlanDto ToDto(this RoundBarPiercingPlan entity)
    {
        return new RoundBarPiercingPlanDto
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
            PlantGrade = entity.PlantGrade,
            RawMaterialType = entity.RawMaterialType,
            RoundBarSpec = entity.RoundBarSpec,
            PiercingSpec = entity.PiercingSpec,
            RequiredUnitWeight = entity.RequiredUnitWeight,
            RequiredPieces = entity.RequiredPieces,
            RequiredWeight = entity.RequiredWeight,
            RequiredDate = entity.RequiredDate,
            Remark = entity.Remark,
            StandardCycle = entity.StandardCycle,
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
            ProductType = entity.ProductType,
            RequiredPiece = entity.RequiredPiece,
            RequiredWeight = entity.RequiredWeight,
            InputMultiple = entity.InputMultiple,
            RequiredDate = entity.RequiredDate,
            Remark = entity.Remark,
            CreatedTime = entity.CreatedTime,
            CreatedBy = entity.CreatedBy,
            PlantGrade = entity.PlantGrade,
            Specification = entity.Specification,
            OuterDiameterNegative = entity.OuterDiameterNegative,
            OuterDiameterPositive = entity.OuterDiameterPositive,
            WallThicknessNegative = entity.WallThicknessNegative,
            WallThicknessPositive = entity.WallThicknessPositive,
            LengthStatus = entity.LengthStatus,
            MinLength = entity.MinLength,
            MaxLength = entity.MaxLength,
            DeliveryState = entity.DeliveryState,
            StandardCycle = entity.StandardCycle
        };
    }

    public static InventoryPlanDto ToDto(this InventoryPlan entity)
    {
        return new InventoryPlanDto
        {
            Id = entity.Id,
            WorkOrderId = entity.WorkOrderId,
            PlanDate = entity.PlanDate,
            InventoryBatchNo = entity.InventoryBatchNo,
            BatchNo = entity.BatchNo,
            MaterialType = EnumHelper.TryParse<MaterialType>(entity.MaterialType),
            PlantGrade = entity.PlantGrade,
            Specification = entity.Specification,
            LocationArea = entity.LocationArea,
            LocationRack = entity.LocationRack,
            InputMultiple = entity.InputMultiple,
            UsageMode = entity.UsageMode,
            UsedQuantity = entity.UsedQuantity,
            UsedWeight = entity.UsedWeight,
            RequiredDate = entity.RequiredDate,
            PlanStatus = entity.PlanStatus,
            PlanStatusText = EnumHelper.GetDisplayName(entity.PlanStatus),
            Remark = entity.Remark,
            ReworkType = entity.ReworkType,
            ReworkTypeText = entity.ReworkType.HasValue ? EnumHelper.GetDisplayName(entity.ReworkType.Value) : null,
            StandardCycle = entity.StandardCycle,
            CreatedTime = entity.CreatedTime,
            CreatedBy = entity.CreatedBy
        };
    }

    public static InProcessReworkPlanDto ToDto(this InProcessReworkPlan entity)
    {
        return new InProcessReworkPlanDto
        {
            Id = entity.Id,
            WorkOrderId = entity.WorkOrderId,
            PlanDate = entity.PlanDate,
            ProductionBatchId = entity.ProductionBatchId,
            BatchNo = entity.BatchNo,
            BatchTagNo = entity.BatchTagNo,
            PlantGrade = entity.PlantGrade,
            Specification = entity.Specification,
            InputMultiple = entity.InputMultiple,
            UsedQuantity = entity.UsedQuantity,
            UsedWeight = entity.UsedWeight
        };
    }

    public static InMainWorkOrderPlanDto ToDto(this InMainWorkOrderPlan entity)
    {
        return new InMainWorkOrderPlanDto
        {
            Id = entity.Id,
            WorkOrderId = entity.WorkOrderId,
            PlanDate = entity.PlanDate,
            ProductionBatchId = entity.ProductionBatchId,
            BatchNo = entity.BatchNo,
            MainWorkOrderNo = entity.MainWorkOrderNo,
            AllocatedWeight = entity.AllocatedWeight,
            AllocatedQuantity = entity.AllocatedQuantity,
            ProductionRatio = entity.ProductionRatio,
            RequiredDate = entity.RequiredDate,
            PlanStatus = entity.PlanStatus,
            Remark = entity.Remark
        };
    }
}

#endregion
