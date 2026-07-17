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
            totalDays += dayMap.GetValueOrDefault(section.SectionName, 0);
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
        int? coldRollDraw, int? oilPipeCut, int? degrease, int? solution,
        int? straighten, int? cut, int? thicknessMeasure, int? pickle,
        int? outerPolish, int? innerGrinding, int? outerSpotGrinding,
        int? inspection, int? weldingHead, int? lubrication, int? warehouse)
    {
        var sections = new List<(string, int)>();
        if (coldRollDraw.HasValue) sections.Add((SectionDefs.ColdRollDraw, coldRollDraw.Value));
        if (oilPipeCut.HasValue) sections.Add((SectionDefs.OilPipeCut, oilPipeCut.Value));
        if (degrease.HasValue) sections.Add((SectionDefs.Degrease, degrease.Value));
        if (solution.HasValue) sections.Add((SectionDefs.Solution, solution.Value));
        if (straighten.HasValue) sections.Add((SectionDefs.Straighten, straighten.Value));
        if (cut.HasValue) sections.Add((SectionDefs.Cut, cut.Value));
        if (thicknessMeasure.HasValue) sections.Add((SectionDefs.ThicknessMeasure, thicknessMeasure.Value));
        if (pickle.HasValue) sections.Add((SectionDefs.Pickle, pickle.Value));
        if (outerPolish.HasValue) sections.Add((SectionDefs.OuterPolish, outerPolish.Value));
        if (innerGrinding.HasValue) sections.Add((SectionDefs.InnerGrinding, innerGrinding.Value));
        if (outerSpotGrinding.HasValue) sections.Add((SectionDefs.OuterSpotGrinding, outerSpotGrinding.Value));
        if (inspection.HasValue) sections.Add((SectionDefs.Inspection, inspection.Value));
        if (weldingHead.HasValue) sections.Add((SectionDefs.WeldingHead, weldingHead.Value));
        if (lubrication.HasValue) sections.Add((SectionDefs.Lubrication, lubrication.Value));
        if (warehouse.HasValue) sections.Add((SectionDefs.Warehouse, warehouse.Value));
        return sections;
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
                            ManufacturingMultiple = pg.ManufacturingMultiple,
                            Remark = pg.Remark,
                            ColdRollDraw = pg.ColdRollDraw,
                            OilPipeCut = pg.OilPipeCut,
                            Degrease = pg.Degrease,
                            Solution = pg.Solution,
                            Straighten = pg.Straighten,
                            Cut = pg.Cut,
                            ThicknessMeasure = pg.ThicknessMeasure,
                            Pickle = pg.Pickle,
                            OuterPolish = pg.OuterPolish,
                            InnerGrinding = pg.InnerGrinding,
                            OuterSpotGrinding = pg.OuterSpotGrinding,
                            Inspection = pg.Inspection,
                            WeldingHead = pg.WeldingHead,
                            Lubrication = pg.Lubrication,
                            Warehouse = pg.Warehouse
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
                        pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.Solution,
                        pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                        pg.OuterPolish, pg.InnerGrinding, pg.OuterSpotGrinding,
                        pg.Inspection, pg.WeldingHead, pg.Lubrication, pg.Warehouse));
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
                            ManufacturingMultiple = pg.ManufacturingMultiple,
                            Remark = pg.Remark,
                            ColdRollDraw = pg.ColdRollDraw,
                            OilPipeCut = pg.OilPipeCut,
                            Degrease = pg.Degrease,
                            Solution = pg.Solution,
                            Straighten = pg.Straighten,
                            Cut = pg.Cut,
                            ThicknessMeasure = pg.ThicknessMeasure,
                            Pickle = pg.Pickle,
                            OuterPolish = pg.OuterPolish,
                            InnerGrinding = pg.InnerGrinding,
                            OuterSpotGrinding = pg.OuterSpotGrinding,
                            Inspection = pg.Inspection,
                            WeldingHead = pg.WeldingHead,
                            Lubrication = pg.Lubrication,
                            Warehouse = pg.Warehouse
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
                        pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.Solution,
                        pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                        pg.OuterPolish, pg.InnerGrinding, pg.OuterSpotGrinding,
                        pg.Inspection, pg.WeldingHead, pg.Lubrication, pg.Warehouse));
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

        return plans.Select(p => p.ToDto()).ToList();
    }

    public async Task<List<InventoryPlanDto>> GetReworkPlansAsync(int workOrderId)
    {
        var plans = await _context.InventoryPlans
            .Where(p => p.WorkOrderId == workOrderId && p.ReworkType != null)
            .OrderByDescending(p => p.CreatedTime)
            .ToListAsync();

        return plans.Select(p => p.ToDto()).ToList();
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

        // 校验：批次未被其他工单的未取消库存使用计划引用（排除自身工单）
        var existingPlan = await _context.InventoryPlans
            .AnyAsync(p => p.WorkOrderId != request.WorkOrderId
                && p.InventoryBatchNo == request.InventoryBatchNo
                && p.PlanStatus != InventoryPlanStatus.Cancelled);
        if (existingPlan)
            throw new BusinessException("该库存批次已被其他工单的库存使用计划引用");

        // 校验用量
        if (request.UsageMode == "All")
        {
            request.UsedQuantity = batch.RemainingQuantity;
            request.UsedWeight = batch.RemainingWeight;
        }
        else
        {
            if (request.UsedQuantity == null || request.UsedQuantity <= 0)
                throw new BusinessException("部分使用模式下出库支数必须大于0");
            if (request.UsedWeight <= 0)
                throw new BusinessException("出库重量必须大于0");
            if (request.UsedQuantity > batch.RemainingQuantity)
                throw new BusinessException($"出库支数({request.UsedQuantity})超过库存剩余支数({batch.RemainingQuantity})");
            if (request.UsedWeight > batch.RemainingWeight)
                throw new BusinessException($"出库重量({request.UsedWeight})超过库存剩余重量({batch.RemainingWeight})");
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
        // 工艺周期（改制计划在工序组设置后通过 ProcessGroup 管理接口重新计算）
        plan.StandardCycle = defaultStandardCycle;

        _context.InventoryPlans.Add(plan);
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
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

        // 校验：批次未被其他工单的未取消库存使用计划引用（排除自身工单）
        var conflictBatchNo = await _context.InventoryPlans
            .Where(p => p.WorkOrderId != workOrderId
                && batchNos.Contains(p.InventoryBatchNo)
                && p.PlanStatus != InventoryPlanStatus.Cancelled)
            .Select(p => p.InventoryBatchNo)
            .FirstOrDefaultAsync();
        if (conflictBatchNo != null)
            throw new BusinessException($"库存批次 {conflictBatchNo} 已被其他工单的库存使用计划引用");

        var plans = new List<InventoryPlan>();
        var defaultStandardCycle = (int)await GetConfigAsync("DefaultValue", "StandardCycle", 3m);
        foreach (var request in requests)
        {
            if (!batches.TryGetValue(request.InventoryBatchNo, out var batch))
                throw new BusinessException($"库存批次不存在: {request.InventoryBatchNo}");

            // 校验用量
            if (request.UsageMode == "All")
            {
                request.UsedQuantity = batch.RemainingQuantity;
                request.UsedWeight = batch.RemainingWeight;
            }
            else
            {
                if (request.UsedQuantity == null || request.UsedQuantity <= 0)
                    throw new BusinessException($"批次 {request.InventoryBatchNo}：部分使用模式下出库支数必须大于0");
                if (request.UsedWeight <= 0)
                    throw new BusinessException($"批次 {request.InventoryBatchNo}：出库重量必须大于0");
                if (request.UsedQuantity > batch.RemainingQuantity)
                    throw new BusinessException($"批次 {request.InventoryBatchNo}：出库支数({request.UsedQuantity})超过库存剩余支数({batch.RemainingQuantity})");
                if (request.UsedWeight > batch.RemainingWeight)
                    throw new BusinessException($"批次 {request.InventoryBatchNo}：出库重量({request.UsedWeight})超过库存剩余重量({batch.RemainingWeight})");
            }

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

            // 工艺周期（改制计划在工序组设置后通过 ProcessGroup 管理接口重新计算）
            plan.StandardCycle = defaultStandardCycle;

            plans.Add(plan);
        }

        _context.InventoryPlans.AddRange(plans);
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

        _logger.LogInformation("批量创建库存使用计划成功: 工单ID {WorkOrderId}, 共 {Count} 条", workOrderId, plans.Count);
        return plans.Select(p => p.ToDto()).ToList();
    }

    public async Task DeleteInventoryPlanAsync(int id)
    {
        var plan = await _context.InventoryPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("库存使用计划不存在");

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

        // 获取已被其他未取消库存使用计划引用的批次号（排除当前编辑计划自身）
        var usedBatchNosQuery = _context.InventoryPlans
            .Where(p => p.PlanStatus != InventoryPlanStatus.Cancelled);

        if (excludePlanId.HasValue)
        {
            usedBatchNosQuery = usedBatchNosQuery.Where(p => p.Id != excludePlanId.Value);
        }

        var usedBatchNos = await usedBatchNosQuery
            .Select(p => p.InventoryBatchNo)
            .Distinct()
            .ToListAsync();

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
            .Select(b => new AvailableInventoryBatchDto
            {
                Id = b.Id,
                BatchNo = b.BatchNo,
                MaterialType = b.MaterialType,
                PlantGrade = b.PlantGrade,
                Specification = b.Specification,
                LengthStatus = b.LengthStatus,
                MinLength = b.MinLength,
                MaxLength = b.MaxLength,
                RemainingQuantity = b.RemainingQuantity,
                RemainingWeight = b.RemainingWeight,
                UnitWeight = b.UnitWeight,
                SurfaceCondition = b.SurfaceCondition,
                LocationArea = b.LocationArea,
                LocationRack = b.LocationRack,
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

        // 已被其他未取消计划引用的批次号（排除当前编辑计划自身）
        var usedBatchNosQuery = _context.InventoryPlans
            .Where(p => p.PlanStatus != InventoryPlanStatus.Cancelled);

        if (excludePlanId.HasValue)
        {
            usedBatchNosQuery = usedBatchNosQuery.Where(p => p.Id != excludePlanId.Value);
        }

        var usedBatchNos = await usedBatchNosQuery
            .Select(p => p.InventoryBatchNo)
            .Distinct()
            .ToListAsync();

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
                || (b.MaterialType == InventoryMaterialTypes.DefectSemi && b.LiabilityType == "厂部")
                || (b.MaterialType == InventoryMaterialTypes.DefectFinished && b.LiabilityType == "厂部")
                || (b.MaterialType == InventoryMaterialTypes.DefectWIP && b.LiabilityType == "厂部")),
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
            .Select(b => new AvailableInventoryBatchDto
            {
                Id = b.Id,
                BatchNo = b.BatchNo,
                MaterialType = b.MaterialType,
                PlantGrade = b.PlantGrade,
                Specification = b.Specification,
                LengthStatus = b.LengthStatus,
                MinLength = b.MinLength,
                MaxLength = b.MaxLength,
                RemainingQuantity = b.RemainingQuantity,
                RemainingWeight = b.RemainingWeight,
                UnitWeight = b.UnitWeight,
                SurfaceCondition = b.SurfaceCondition,
                LocationArea = b.LocationArea,
                LocationRack = b.LocationRack,
            })
            .ToList();

        return available;
    }

    public async Task<InventoryPlanDto> GetInventoryPlanByIdAsync(int id)
    {
        var plan = await _context.InventoryPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("库存使用计划不存在");

        return plan.ToDto();
    }

    public async Task<InventoryPlanDto> UpdateInventoryPlanAsync(int id, CreateInventoryPlanRequest request)
    {
        var plan = await _context.InventoryPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("库存使用计划不存在");

        var workOrder = await _context.WorkOrders.FindAsync(plan.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("关联工单不存在");

        // 校验：批次未被其他工单的未取消库存使用计划引用（排除自身工单和自身计划）
        var conflictBatchNo = await _context.InventoryPlans
            .AnyAsync(p => p.Id != id
                && p.WorkOrderId != plan.WorkOrderId
                && p.InventoryBatchNo == plan.InventoryBatchNo
                && p.PlanStatus != InventoryPlanStatus.Cancelled);
        if (conflictBatchNo)
            throw new BusinessException("该库存批次已被其他工单的库存使用计划引用");

        // 校验用量
        if (request.UsageMode == "All")
        {
            var batch = await _context.InventoryBatches
                .FirstOrDefaultAsync(b => b.BatchNo == plan.InventoryBatchNo);
            if (batch != null)
            {
                request.UsedQuantity = batch.RemainingQuantity;
                request.UsedWeight = batch.RemainingWeight;
            }
        }
        else
        {
            if (request.UsedQuantity == null || request.UsedQuantity <= 0)
                throw new BusinessException("部分使用模式下出库支数必须大于0");
            if (request.UsedWeight <= 0)
                throw new BusinessException("出库重量必须大于0");
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
                            ManufacturingMultiple = pg.ManufacturingMultiple,
                            Remark = pg.Remark,
                            ColdRollDraw = pg.ColdRollDraw,
                            OilPipeCut = pg.OilPipeCut,
                            Degrease = pg.Degrease,
                            Solution = pg.Solution,
                            Straighten = pg.Straighten,
                            Cut = pg.Cut,
                            ThicknessMeasure = pg.ThicknessMeasure,
                            Pickle = pg.Pickle,
                            OuterPolish = pg.OuterPolish,
                            InnerGrinding = pg.InnerGrinding,
                            OuterSpotGrinding = pg.OuterSpotGrinding,
                            Inspection = pg.Inspection,
                            WeldingHead = pg.WeldingHead,
                            Lubrication = pg.Lubrication,
                            Warehouse = pg.Warehouse
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
                        pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.Solution,
                        pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                        pg.OuterPolish, pg.InnerGrinding, pg.OuterSpotGrinding,
                        pg.Inspection, pg.WeldingHead, pg.Lubrication, pg.Warehouse));
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
                            ManufacturingMultiple = pg.ManufacturingMultiple,
                            Remark = pg.Remark,
                            ColdRollDraw = pg.ColdRollDraw,
                            OilPipeCut = pg.OilPipeCut,
                            Degrease = pg.Degrease,
                            Solution = pg.Solution,
                            Straighten = pg.Straighten,
                            Cut = pg.Cut,
                            ThicknessMeasure = pg.ThicknessMeasure,
                            Pickle = pg.Pickle,
                            OuterPolish = pg.OuterPolish,
                            InnerGrinding = pg.InnerGrinding,
                            OuterSpotGrinding = pg.OuterSpotGrinding,
                            Inspection = pg.Inspection,
                            WeldingHead = pg.WeldingHead,
                            Lubrication = pg.Lubrication,
                            Warehouse = pg.Warehouse
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
                        pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.Solution,
                        pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                        pg.OuterPolish, pg.InnerGrinding, pg.OuterSpotGrinding,
                        pg.Inspection, pg.WeldingHead, pg.Lubrication, pg.Warehouse));
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

        if (batch.WorkOrderNo != "非工单")
            throw new BusinessException("只能选择非工单批次进行在产改制");

        if (batch.Status != BatchStatus.None && batch.Status != BatchStatus.InProgress)
            throw new BusinessException("只能选择未产或在产状态的批次");

        var reworkType = request.ReworkType;

        // 校验用量
        if (request.UsedQuantity.HasValue && request.UsedQuantity <= 0)
            throw new BusinessException("使用支数必须大于0");
        if (request.UsedWeight <= 0)
            throw new BusinessException("使用重量必须大于0");
        if (request.UsedQuantity.HasValue && batch.CurrentValidQty.HasValue && request.UsedQuantity > batch.CurrentValidQty)
            throw new BusinessException($"使用支数({request.UsedQuantity})超过批次有效原料支数({batch.CurrentValidQty})");
        if (batch.CurrentValidWeight.HasValue && request.UsedWeight > batch.CurrentValidWeight)
            throw new BusinessException($"使用重量({request.UsedWeight})超过批次有效原料重量({batch.CurrentValidWeight})");

        var plan = new InProcessReworkPlan
        {
            WorkOrderId = request.WorkOrderId,
            PlanDate = request.PlanDate,
            ProductionBatchId = request.ProductionBatchId,
            BatchNo = batch.BatchNo,
            BatchTagNo = batch.TagNo,
            MaterialName = batch.MaterialName,
            PlantGrade = batch.PlantGrade,
            Specification = batch.Specification,
            LengthStatus = batch.LengthStatus,
            InputMultiple = request.InputMultiple,
            UsedQuantity = request.UsedQuantity,
            UsedWeight = request.UsedWeight,
            RequiredDate = request.RequiredDate,
            Remark = request.Remark,
            ReworkType = reworkType,
            StandardCycle = 0,
        };

        var defaultStandardCycle = (int)await GetConfigAsync("DefaultValue", "StandardCycle", 3m);
        plan.StandardCycle = defaultStandardCycle;

        _context.InProcessReworkPlans.Add(plan);
        var transaction = await _context.Database.BeginTransactionAsync();
        using (transaction)
        {
            try
            {
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

        _logger.LogInformation("创建在产改制计划成功: 工单ID {WorkOrderId}, 批次号 {BatchNo}, 重量 {Weight}",
            request.WorkOrderId, batch.BatchNo, request.UsedWeight);

        return plan.ToDto();
    }

    public async Task<InProcessReworkPlanDto> UpdateInProcessReworkPlanAsync(int id, CreateInProcessReworkPlanRequest request)
    {
        var plan = await _context.InProcessReworkPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("在产改制计划不存在");

        var workOrder = await _context.WorkOrders.FindAsync(plan.WorkOrderId);
        if (workOrder == null)
            throw new BusinessException("关联工单不存在");

        var reworkType = request.ReworkType;

        if (request.UsedQuantity.HasValue && request.UsedQuantity <= 0)
            throw new BusinessException("使用支数必须大于0");
        if (request.UsedWeight <= 0)
            throw new BusinessException("使用重量必须大于0");

        plan.PlanDate = request.PlanDate;
        plan.InputMultiple = request.InputMultiple;
        plan.UsedQuantity = request.UsedQuantity;
        plan.UsedWeight = request.UsedWeight;
        plan.RequiredDate = request.RequiredDate;
        plan.Remark = request.Remark;
        plan.ReworkType = reworkType;

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

        _logger.LogInformation("更新在产改制计划成功: ID {Id}", id);
        return plan.ToDto();
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

    public async Task<List<AvailableInProcessBatchDto>> GetAvailableInProcessBatchesAsync(int workOrderId, ReworkType? reworkType = null, int? excludePlanId = null)
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

        // 排除已被其他未取消在产改制计划引用的批次
        var usedBatchIdsQuery = _context.InProcessReworkPlans
            .Where(p => p.PlanStatus != InventoryPlanStatus.Cancelled);
        if (excludePlanId.HasValue)
            usedBatchIdsQuery = usedBatchIdsQuery.Where(p => p.Id != excludePlanId.Value);

        var usedBatchIds = await usedBatchIdsQuery
            .Select(p => p.ProductionBatchId)
            .Distinct()
            .ToListAsync();

        // 查询可用在产批次
        var query = _context.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProcessGroups)
            .Where(b => b.WorkOrderNo == "非工单")
            .Where(b => b.Status == BatchStatus.None || b.Status == BatchStatus.InProgress)
            .Where(b => eligibleGrades.Contains(b.PlantGrade))
            .Where(b => b.CurrentValidWeight.HasValue && b.CurrentValidWeight > 0);

        if (usedBatchIds.Count > 0)
            query = query.Where(b => !usedBatchIds.Contains(b.Id));

        // 规格匹配（按改制类型筛选 OD/WT）
        if (reworkType.HasValue)
        {
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
                        var spec = b.CurrentSpec ?? b.Specification;
                        var bOd = SpecificationParser.ParseOuterDiameter(spec);
                        var bWt = SpecificationParser.ParseWallThickness(spec);
                        if (bOd == null || bWt == null) return false;
                        if (bOd < odMin || bOd > odMax || bWt < wtMin || bWt > wtMax)
                            return false;

                        // 单支重量条件：重量/支数/工序组制成倍率
                        if (b.CurrentValidQty.HasValue && b.CurrentValidQty > 0)
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
                        MaterialName = b.MaterialName,
                        PlantGrade = b.PlantGrade,
                        Specification = b.CurrentSpec ?? b.Specification,
                        LengthStatus = string.IsNullOrEmpty(b.LengthStatus) ? default : Enum.Parse<LengthStatus>(b.LengthStatus),
                        TotalQuantity = b.TotalQuantity,
                        TotalWeight = b.TotalWeight,
                        CurrentValidQty = b.CurrentValidQty * GetCurrentProcessGroupMultiple(b),
                        CurrentValidWeight = b.CurrentValidWeight,
                        SourceBatchNo = b.SourceBatchNo,
                        SourceMaterialType = b.SourceMaterialType,
                        SourceHeatNo = b.SourceHeatNo,
                        SourceSpecification = b.SourceSpecification,
                        ProductionType = b.ProductionType,
                        ManufacturingItem = !string.IsNullOrEmpty(b.ManufacturingItem) && Enum.TryParse<MaterialType>(b.ManufacturingItem, out var mi) ? mi : default,
                        CurrentGroupName = b.CurrentGroupName,
                        CurrentSectionName = b.CurrentSectionName,
                        CurrentSpec = b.CurrentSpec,
                    })
                    .OrderByDescending(b => b.CurrentValidWeight)
                    .ToList();

                return available;
            }
        }

        // 无改制类型筛选时，返回所有可用批次
        var allBatches = await query.ToListAsync();
        return allBatches
            .Select(b => new AvailableInProcessBatchDto
            {
                Id = b.Id,
                BatchNo = b.BatchNo,
                TagNo = b.TagNo,
                MaterialName = b.MaterialName,
                PlantGrade = b.PlantGrade,
                Specification = b.CurrentSpec ?? b.Specification,
                LengthStatus = string.IsNullOrEmpty(b.LengthStatus) ? default : Enum.Parse<LengthStatus>(b.LengthStatus),
                TotalQuantity = b.TotalQuantity,
                TotalWeight = b.TotalWeight,
                CurrentValidQty = b.CurrentValidQty * GetCurrentProcessGroupMultiple(b),
                CurrentValidWeight = b.CurrentValidWeight,
                SourceBatchNo = b.SourceBatchNo,
                SourceMaterialType = b.SourceMaterialType,
                SourceHeatNo = b.SourceHeatNo,
                SourceSpecification = b.SourceSpecification,
                ProductionType = b.ProductionType,
                ManufacturingItem = !string.IsNullOrEmpty(b.ManufacturingItem) && Enum.TryParse<MaterialType>(b.ManufacturingItem, out var mi) ? mi : default,
                CurrentGroupName = b.CurrentGroupName,
                CurrentSectionName = b.CurrentSectionName,
                CurrentSpec = b.CurrentSpec,
            })
            .OrderByDescending(b => b.CurrentValidWeight)
            .ToList();
    }

    /// <summary>
    /// 获取当前执行工序组的制成倍数
    /// </summary>
    private static int GetCurrentProcessGroupMultiple(ProductionBatch batch)
    {
        if (batch.ProcessGroups == null || batch.ProcessGroups.Count == 0)
            return 1;

        // 优先匹配当前正在执行的工序组（通过 CurrentSpec 匹配 ManufacturingSpec）
        if (!string.IsNullOrEmpty(batch.CurrentSpec))
        {
            var matched = batch.ProcessGroups
                .Where(pg => pg.ManufacturingSpec == batch.CurrentSpec)
                .OrderByDescending(pg => pg.SequenceNumber)
                .FirstOrDefault();
            if (matched != null && matched.ManufacturingMultiple > 0)
                return matched.ManufacturingMultiple;
        }

        // 回退：取最后一个工序组的制成倍数
        var last = batch.ProcessGroups.OrderByDescending(pg => pg.SequenceNumber).FirstOrDefault();
        return last?.ManufacturingMultiple > 0 ? last.ManufacturingMultiple : 1;
    }

    /// <summary>
    /// 获取在产改制计划通知（供批次上下文使用）
    /// 通知规则：批次 WorkOrderNo == "非工单" 时显示，被正式工单认领后自动消失
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
            .Where(j => j.b.WorkOrderNo == "非工单")
            .Join(_context.WorkOrders.AsNoTracking(),
                j => j.p.WorkOrderId,
                wo => wo.Id,
                (j, wo) => new PendingPlanBatchDto
                {
                    BatchNo = j.p.BatchNo,
                    WorkOrderNo = wo.WorkOrderNo,
                    PlanType = "在产改制"
                })
            .Distinct()
            .ToListAsync();
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

        var fixedPartial = await GetConfigAsync("MaterialPlanStatus", "FixedPartial", 102m);
        var fixedSatisfied = await GetConfigAsync("MaterialPlanStatus", "FixedSatisfied", 110m);
        var nonFixedPartial = await GetConfigAsync("MaterialPlanStatus", "NonFixedPartial", 105m);
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
                fixedPartial: fixedPartial, fixedSatisfied: fixedSatisfied,
                nonFixedPartial: nonFixedPartial, nonFixedSatisfied: nonFixedSatisfied);
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
                fixedPartial: fixedPartial, fixedSatisfied: fixedSatisfied,
                nonFixedPartial: nonFixedPartial, nonFixedSatisfied: nonFixedSatisfied);
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
                fixedPartial: fixedPartial, fixedSatisfied: fixedSatisfied,
                nonFixedPartial: nonFixedPartial, nonFixedSatisfied: nonFixedSatisfied);
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
                fixedPartial: fixedPartial, fixedSatisfied: fixedSatisfied,
                nonFixedPartial: nonFixedPartial, nonFixedSatisfied: nonFixedSatisfied);
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
                fixedPartial: fixedPartial, fixedSatisfied: fixedSatisfied,
                nonFixedPartial: nonFixedPartial, nonFixedSatisfied: nonFixedSatisfied);
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
                fixedPartial, fixedSatisfied, nonFixedPartial, nonFixedSatisfied);
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

        return dto;
    }

    public async Task UpdateMaterialPlanStatusAsync(int workOrderId)
    {
        var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
        if (workOrder == null)
            return;

        var fixedPartial = await GetConfigAsync("MaterialPlanStatus", "FixedPartial", 102m);
        var fixedSatisfied = await GetConfigAsync("MaterialPlanStatus", "FixedSatisfied", 110m);
        var nonFixedPartial = await GetConfigAsync("MaterialPlanStatus", "NonFixedPartial", 105m);
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

        var hasSemi = semiPlans.Any();
        var hasFinish = finishPlans.Any();
        var hasInventory = regularInventory.Any();
        var hasRework = reworkPlans.Any();
        var hasPiercing = piercingPlans.Any();
        var hasInProcessRework = inProcessReworkPlans.Any();

        if (!hasSemi && !hasFinish && !hasInventory && !hasRework && !hasPiercing && !hasInProcessRework)
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
                    fixedPartial: fixedPartial, fixedSatisfied: fixedSatisfied,
                nonFixedPartial: nonFixedPartial, nonFixedSatisfied: nonFixedSatisfied);
                statuses.Add(s);
                rates.Add(CalculatePlanRate(workOrder, semiPlans, isSemi: true));
            }

            if (hasFinish)
            {
                var s = CalculatePlanStatus(workOrder, finishPlans, isSemi: false,
                    fixedPartial: fixedPartial, fixedSatisfied: fixedSatisfied,
                nonFixedPartial: nonFixedPartial, nonFixedSatisfied: nonFixedSatisfied);
                statuses.Add(s);
                rates.Add(CalculatePlanRate(workOrder, finishPlans, isSemi: false));
            }

            if (hasInventory)
            {
                var s = CalculateInventoryPlanStatus(workOrder, regularInventory, isRework: false,
                    fixedPartial: fixedPartial, fixedSatisfied: fixedSatisfied,
                nonFixedPartial: nonFixedPartial, nonFixedSatisfied: nonFixedSatisfied);
                statuses.Add(s);
                rates.Add(CalculateInventoryPlanRate(workOrder, regularInventory));
            }

            if (hasRework)
            {
                var s = CalculateInventoryPlanStatus(workOrder, reworkPlans, isRework: true,
                    fixedPartial: fixedPartial, fixedSatisfied: fixedSatisfied,
                nonFixedPartial: nonFixedPartial, nonFixedSatisfied: nonFixedSatisfied);
                statuses.Add(s);
                rates.Add(CalculateInventoryPlanRate(workOrder, reworkPlans));
            }

            if (hasPiercing)
            {
                var s = CalculatePlanStatus(workOrder, piercingPlans.Cast<BaseEntity>().ToList(), isSemi: false, isPiercing: true,
                    fixedPartial: fixedPartial, fixedSatisfied: fixedSatisfied,
                nonFixedPartial: nonFixedPartial, nonFixedSatisfied: nonFixedSatisfied);
                statuses.Add(s);
                rates.Add(CalculatePlanRate(workOrder, piercingPlans.Cast<BaseEntity>().ToList(), isSemi: false, isPiercing: true));
            }

            if (hasInProcessRework)
            {
                var rate = CalculateInProcessReworkPlanRate(workOrder, inProcessReworkPlans);
                rates.Add(rate);
            }

            // 工单满足率 = 6种用料相加（总覆盖率）
            var totalRate = Math.Min(rates.Sum(), 999m);
            workOrder.MaterialPlanRate = totalRate;
            workOrder.MaterialPlanStatus = CalculateOverallStatus(workOrder, totalRate,
                fixedPartial: fixedPartial, fixedSatisfied: fixedSatisfied,
                nonFixedPartial: nonFixedPartial, nonFixedSatisfied: nonFixedSatisfied);
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 计算单个计划的状态（工单级，含"理论满足"）
    /// </summary>
    private MaterialPlanStatus CalculatePlanStatus(WoEntity workOrder,
        IReadOnlyCollection<BaseEntity> plans, bool isSemi, bool isPiercing = false,
        decimal fixedPartial = 102m, decimal fixedSatisfied = 110m,
        decimal nonFixedPartial = 105m, decimal nonFixedSatisfied = 120m)
    {
        var rate = CalculatePlanRate(workOrder, plans, isSemi, isPiercing);

        if (workOrder.LengthStatus == LengthStatus.Fixed)
        {
            // 定尺：支数模式
            if (rate < 100m) return MaterialPlanStatus.Partial;
            if (rate < fixedPartial) return MaterialPlanStatus.TheoreticalSatisfied;
            if (rate <= fixedSatisfied) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
        else
        {
            // 范围尺/非定尺：重量模式
            if (rate < 100m) return MaterialPlanStatus.Partial;
            if (rate < nonFixedPartial) return MaterialPlanStatus.TheoreticalSatisfied;
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
    /// 计算库存使用计划状态（工单级，含"理论满足"）
    /// </summary>
    private MaterialPlanStatus CalculateInventoryPlanStatus(WoEntity workOrder,
        IReadOnlyCollection<InventoryPlan> plans, bool isRework = false,
        decimal fixedPartial = 102m, decimal fixedSatisfied = 110m,
        decimal nonFixedPartial = 105m, decimal nonFixedSatisfied = 120m)
    {
        var rate = CalculateInventoryPlanRate(workOrder, plans);

        if (workOrder.LengthStatus == LengthStatus.Fixed)
        {
            if (rate < 100m) return MaterialPlanStatus.Partial;
            if (rate < fixedPartial) return MaterialPlanStatus.TheoreticalSatisfied;
            if (rate <= fixedSatisfied) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
        else
        {
            if (rate < 100m) return MaterialPlanStatus.Partial;
            if (rate < nonFixedPartial) return MaterialPlanStatus.TheoreticalSatisfied;
            if (rate <= nonFixedSatisfied) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
    }

    /// <summary>
    /// 基于总满足率计算整体状态
    /// </summary>
    private static MaterialPlanStatus CalculateOverallStatus(WoEntity workOrder, decimal totalRate,
        decimal fixedPartial = 102m, decimal fixedSatisfied = 110m,
        decimal nonFixedPartial = 105m, decimal nonFixedSatisfied = 120m)
    {
        if (totalRate <= 0) return MaterialPlanStatus.NotPlanned;

        if (workOrder.LengthStatus == LengthStatus.Fixed)
        {
            if (totalRate < 100m) return MaterialPlanStatus.Partial;
            if (totalRate < fixedPartial) return MaterialPlanStatus.TheoreticalSatisfied;
            if (totalRate <= fixedSatisfied) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
        else
        {
            if (totalRate < 100m) return MaterialPlanStatus.Partial;
            if (totalRate < nonFixedPartial) return MaterialPlanStatus.TheoreticalSatisfied;
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
                pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.Solution,
                pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                pg.OuterPolish, pg.InnerGrinding, pg.OuterSpotGrinding,
                pg.Inspection, pg.WeldingHead, pg.Lubrication, pg.Warehouse));
        }

        // 计算工艺周期
        var dayMap = await _standardWorkDayService.GetStandardDaysMapAsync(batch.PlantGrade);
        var deliveryStateExtraDays = await _deliveryStateService.GetDeliveryStateExtraDaysMapAsync();
        var standardCycle = CalculateStandardCycleFromSections(sections, dayMap, deliveryStateExtraDays, null);
        if (standardCycle == 0)
            standardCycle = (int)await GetConfigAsync("DefaultValue", "StandardCycle", 3m);

        // 更新所有关联的改制库存计划
        foreach (var plan in plans)
        {
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
                p => p.BatchNo,
                b => b.BatchNo,
                (p, b) => new { p, b })
            .Where(j => j.b.WarehouseId == warehouseId)
            .Join(_context.WorkOrders.AsNoTracking(),
                j => j.p.WorkOrderId,
                wo => wo.Id,
                (j, wo) => new PendingPlanBatchDto
                {
                    BatchNo = j.p.BatchNo,
                    WorkOrderNo = wo.WorkOrderNo,
                    PlanType = j.p.ReworkType != null ? "库料改制" : "库存使用"
                })
            .Distinct()
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
            MaterialType = entity.MaterialType,
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
            PlanStatusText = entity.PlanStatus switch
            {
                InventoryPlanStatus.Planned => "已计划",
                InventoryPlanStatus.Confirmed => "已确认",
                InventoryPlanStatus.Cancelled => "已取消",
                _ => "未知"
            },
            Remark = entity.Remark,
            ReworkType = entity.ReworkType,
            ReworkTypeText = entity.ReworkType switch
            {
                ReworkType.EmptyDrawing => "空拉改制",
                ReworkType.FewerPass => "少道次改制",
                ReworkType.ManualSelect => "人工选择改制",
                _ => null
            },
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
            MaterialName = entity.MaterialName,
            PlantGrade = entity.PlantGrade,
            Specification = entity.Specification,
            LengthStatus = string.IsNullOrEmpty(entity.LengthStatus) ? default : Enum.Parse<LengthStatus>(entity.LengthStatus),
            InputMultiple = entity.InputMultiple,
            UsedQuantity = entity.UsedQuantity,
            UsedWeight = entity.UsedWeight,
            RequiredDate = entity.RequiredDate,
            PlanStatus = entity.PlanStatus,
            PlanStatusText = entity.PlanStatus switch
            {
                InventoryPlanStatus.Planned => "已计划",
                InventoryPlanStatus.Confirmed => "已确认",
                InventoryPlanStatus.Cancelled => "已取消",
                _ => "未知"
            },
            Remark = entity.Remark,
            ReworkType = entity.ReworkType,
            ReworkTypeText = entity.ReworkType switch
            {
                ReworkType.EmptyDrawing => "空拉改制",
                ReworkType.FewerPass => "少道次改制",
                ReworkType.ManualSelect => "人工选择改制",
                _ => entity.ReworkType.ToString()
            },
            StandardCycle = entity.StandardCycle,
            CreatedTime = entity.CreatedTime,
            CreatedBy = entity.CreatedBy
        };
    }
}

#endregion
