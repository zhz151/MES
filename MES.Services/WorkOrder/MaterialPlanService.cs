using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Printing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using WoEntity = MES.Data.Entities.WorkOrder;

namespace MES.Services;

/// <summary>
/// 用料计划服务实现
/// </summary>
public class MaterialPlanService : IMaterialPlanService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MaterialPlanService> _logger;
    private readonly WorkOrderListSummaryService? _listSummaryService;

    /// <summary>
    /// 工厂牌号替代映射（高级可替低级）：key=低级, value=高级
    /// </summary>
    private static readonly Dictionary<string, string> GradeSubstitutes = Core.Constants.GradeSubstitutes.Mapping;

    public MaterialPlanService(AppDbContext context, ILogger<MaterialPlanService> logger,
        WorkOrderListSummaryService? listSummaryService = null)
    {
        _context = context;
        _logger = logger;
        _listSummaryService = listSummaryService;
    }

    #region 工艺周期计算（基于工序组）

    /// <summary>
    /// 从工序组工段列表计算工艺周期（天）：累计所有工段天数 + 交货状态调整
    /// </summary>
    internal static int CalculateStandardCycleFromSections(
        List<(string SectionName, int Sequence)> sections,
        DeliveryState deliveryState,
        string? plantGrade)
    {
        if (sections.Count == 0) return 0;

        double totalDays = 0;
        foreach (var section in sections)
        {
            totalDays += GetSectionDay(section.SectionName, plantGrade);
        }

        // 交货状态调整：非固溶酸洗/非硬态 +4 天
        if (deliveryState != DeliveryState.SolutionAnnealedAndPickled
            && deliveryState != DeliveryState.Hard)
        {
            totalDays += 4;
        }

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

    /// <summary>
    /// 获取工段对应的天数（与 ProductionRecordService 保持一致）
    /// </summary>
    internal static double GetSectionDay(string sectionName, string? plantGrade)
        => SectionDefs.GetStandardDays(sectionName, plantGrade);

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
            RawMaterialType = Enum.TryParse<RawMaterialType>(request.RawMaterialType, out var rt)
                ? rt
                : throw new BusinessException($"无效的原料类型: {request.RawMaterialType}"),
            RawMaterialSpec = request.RawMaterialSpec,
            RequiredUnitWeight = request.RequiredUnitWeight,
            RequiredPieces = request.RequiredPieces,
            RequiredWeight = request.RequiredWeight,
            RequiredDate = request.RequiredDate,
            Remark = request.Remark,
        };

        using var transaction = await _context.Database.BeginTransactionAsync();
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
            plan.StandardCycle = CalculateStandardCycleFromSections(
                semiSections, workOrder.DeliveryState, workOrder.PlantGrade);
            if (plan.StandardCycle == 0)
                throw new BusinessException("工艺周期计算失败：工序组工段数据不完整，无法计算工艺周期");
            _context.Entry(plan).Property(e => e.StandardCycle).IsModified = true;
            await _context.SaveChangesAsync();

            // 同步写入/更新 StandardProcessCycle 引用表
            await UpsertStandardProcessCycleAsync(
                plan.PlantGrade,
                EnumHelper.GetDisplayName(plan.RawMaterialType),
                plan.RawMaterialSpec,
                workOrder.Specification,
                EnumHelper.GetDisplayName(workOrder.DeliveryState),
                plan.StandardCycle);

            // 刷新工单状态（与创建在同一事务中）
            await UpdateMaterialPlanStatusAsync(request.WorkOrderId);

            await transaction.CommitAsync();
            await RefreshReadModelAsync(request.WorkOrderId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

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
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.PurchaseSemiPlans.Remove(plan);
            await _context.SaveChangesAsync();

            // 刷新工单状态（与删除在同一事务中）
            await UpdateMaterialPlanStatusAsync(workOrderId);

            await transaction.CommitAsync();
            await RefreshReadModelAsync(workOrderId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

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
        plan.RawMaterialType = Enum.TryParse<RawMaterialType>(request.RawMaterialType, out var rt)
            ? rt
            : throw new BusinessException($"无效的原料类型: {request.RawMaterialType}");

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

        using var transaction = await _context.Database.BeginTransactionAsync();
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
            plan.StandardCycle = CalculateStandardCycleFromSections(
                semiSections, workOrder.DeliveryState, workOrder.PlantGrade);
            if (plan.StandardCycle == 0)
                throw new BusinessException("工艺周期计算失败：工序组工段数据不完整，无法计算工艺周期");
            _context.Entry(plan).Property(e => e.StandardCycle).IsModified = true;
            await _context.SaveChangesAsync();

            // 同步写入/更新 StandardProcessCycle 引用表
            await UpsertStandardProcessCycleAsync(
                plan.PlantGrade,
                EnumHelper.GetDisplayName(plan.RawMaterialType),
                plan.RawMaterialSpec,
                workOrder.Specification,
                EnumHelper.GetDisplayName(workOrder.DeliveryState),
                plan.StandardCycle);

            await UpdateMaterialPlanStatusAsync(plan.WorkOrderId);
            await transaction.CommitAsync();
            await RefreshReadModelAsync(plan.WorkOrderId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

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

        var plan = new PurchaseFinishedPlan
        {
            WorkOrderId = request.WorkOrderId,
            PlanDate = request.PlanDate,
            ProductType = Enum.TryParse<FinishedProductType>(request.ProductType, out var pt)
                ? pt
                : throw new BusinessException($"无效的成品类型: {request.ProductType}"),
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
            LengthStatus = Enum.TryParse<LengthStatus>(request.LengthStatus, out var ls)
                ? ls
                : LengthStatus.Fixed,
            MinLength = request.MinLength,
            MaxLength = request.MaxLength,
            DeliveryState = Enum.TryParse<DeliveryState>(request.DeliveryState, out var ds)
                ? ds
                : DeliveryState.SolutionAnnealedAndPickled,
            StandardCycle = 3 // 成品采购默认3天
        };

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.PurchaseFinishedPlans.Add(plan);
            await _context.SaveChangesAsync();

            // 刷新工单状态（与创建在同一事务中）
            await UpdateMaterialPlanStatusAsync(request.WorkOrderId);

            await transaction.CommitAsync();
            await RefreshReadModelAsync(request.WorkOrderId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

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
        foreach (var request in requests)
        {
            if (workOrder.LengthStatus == LengthStatus.Fixed && (request.RequiredPiece == null || request.RequiredPiece <= 0))
                throw new BusinessException("定尺模式下采购支数不能为空");

            plans.Add(new PurchaseFinishedPlan
            {
                WorkOrderId = workOrderId,
                PlanDate = request.PlanDate,
                ProductType = Enum.TryParse<FinishedProductType>(request.ProductType, out var pt)
                    ? pt
                    : throw new BusinessException($"无效的成品类型: {request.ProductType}"),
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
                LengthStatus = Enum.TryParse<LengthStatus>(request.LengthStatus, out var ls)
                    ? ls : LengthStatus.Fixed,
                MinLength = request.MinLength,
                MaxLength = request.MaxLength,
                DeliveryState = Enum.TryParse<DeliveryState>(request.DeliveryState, out var ds)
                    ? ds : DeliveryState.SolutionAnnealedAndPickled,
                StandardCycle = 3 // 成品采购默认3天
            });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.PurchaseFinishedPlans.AddRange(plans);
            await _context.SaveChangesAsync();
            await UpdateMaterialPlanStatusAsync(workOrderId);
            await transaction.CommitAsync();
            await RefreshReadModelAsync(workOrderId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

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
        plan.ProductType = Enum.TryParse<FinishedProductType>(request.ProductType, out var pt)
            ? pt
            : throw new BusinessException($"无效的成品类型: {request.ProductType}");
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
        plan.LengthStatus = Enum.TryParse<LengthStatus>(request.LengthStatus, out var ls)
            ? ls
            : LengthStatus.Fixed;
        plan.MinLength = request.MinLength;
        plan.MaxLength = request.MaxLength;
        plan.DeliveryState = Enum.TryParse<DeliveryState>(request.DeliveryState, out var ds)
            ? ds
            : DeliveryState.SolutionAnnealedAndPickled;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();
            await UpdateMaterialPlanStatusAsync(plan.WorkOrderId);
            await transaction.CommitAsync();
            await RefreshReadModelAsync(plan.WorkOrderId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        _logger.LogInformation("更新成品采购计划成功: ID {Id}", id);
        return plan.ToDto();
    }

    public async Task DeleteFinishedPlanAsync(int id)
    {
        var plan = await _context.PurchaseFinishedPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("成品采购计划不存在");

        var workOrderId = plan.WorkOrderId;
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.PurchaseFinishedPlans.Remove(plan);
            await _context.SaveChangesAsync();

            // 刷新工单状态（与删除在同一事务中）
            await UpdateMaterialPlanStatusAsync(workOrderId);

            await transaction.CommitAsync();
            await RefreshReadModelAsync(workOrderId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

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
            ReworkType = request.ReworkType != null ? Enum.Parse<ReworkType>(request.ReworkType) : null,
        };

        // 工艺周期（改制计划在工序组设置后通过 ProcessGroup 管理接口重新计算）
        plan.StandardCycle = 3;

        _context.InventoryPlans.Add(plan);
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();

            // 刷新工单状态（与创建在同一事务中）
            await UpdateMaterialPlanStatusAsync(request.WorkOrderId);

            await transaction.CommitAsync();
            await RefreshReadModelAsync(request.WorkOrderId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

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
                ReworkType = request.ReworkType != null ? Enum.Parse<ReworkType>(request.ReworkType) : null,
            };

            // 工艺周期（改制计划在工序组设置后通过 ProcessGroup 管理接口重新计算）
            plan.StandardCycle = 3;

            plans.Add(plan);
        }

        _context.InventoryPlans.AddRange(plans);
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();
            await UpdateMaterialPlanStatusAsync(workOrderId);
            await transaction.CommitAsync();
            await RefreshReadModelAsync(workOrderId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

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
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();

            // 刷新工单状态（与删除在同一事务中）
            await UpdateMaterialPlanStatusAsync(workOrderId);

            await transaction.CommitAsync();
            await RefreshReadModelAsync(workOrderId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

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

        // 单米重量 = π × 密度 × WT_实际 × (OD_实际 - WT_实际) / 1000
        var unitWeightPerMeter = Math.Round(
            (decimal)Math.PI * density * wtActual * (odActual - wtActual) / 1000m, 6);

        decimal requiredUnitWeight;
        if (workOrder.LengthStatus == LengthStatus.NonFixed)
        {
            // 非定尺：默认长度4500mm
            requiredUnitWeight = Math.Round(unitWeightPerMeter * 4500m / 1000m, 3);
        }
        else
        {
            // 定尺/范围尺：取MaxLength
            var lengthMm = workOrder.MaxLength ?? 6000;
            requiredUnitWeight = Math.Round(unitWeightPerMeter * lengthMm / 1000m, 3);
        }

        // 外径边界
        var odMin = Math.Round((od - workOrder.OuterDiameterNegative) * 1.002m, 3);
        var odMax = Math.Round((od + workOrder.OuterDiameterPositive) * 0.998m, 3);

        // 壁厚边界
        var wtMin = Math.Round((wt - workOrder.WallThicknessNegative) * 1.02m, 3);
        var wtMax = Math.Round((wt + workOrder.WallThicknessPositive) * 0.98m, 3);

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
        // 反向检查：是否有其他牌号可以替代工单的牌号（即工单牌号是某高级牌号的低级版）
        foreach (var kvp in GradeSubstitutes)
        {
            if (kvp.Value == workOrder.PlantGrade)
            {
                eligibleGrades.Add(kvp.Key);
            }
        }

        query = query.Where(b => eligibleGrades.Contains(b.PlantGrade));

        var batches = await query.ToListAsync();

        // 内存筛选（外径/壁厚/长度/单支重量条件需要计算）
        var available = batches
            .Where(b =>
            {
                // 条件③：外径符合
                if (b.ActualOuterDiameter.HasValue)
                {
                    if (b.ActualOuterDiameter < odMin || b.ActualOuterDiameter > odMax)
                        return false;
                }

                // 条件④：壁厚符合
                if (b.ActualWallThickness.HasValue)
                {
                    if (b.ActualWallThickness < wtMin || b.ActualWallThickness > wtMax)
                        return false;
                }

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
                ActualOuterDiameter = b.ActualOuterDiameter,
                ActualWallThickness = b.ActualWallThickness
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

        decimal requiredUnitWeight;
        if (workOrder.LengthStatus == LengthStatus.NonFixed)
        {
            requiredUnitWeight = Math.Round(unitWeightPerMeter * 4500m / 1000m, 3);
        }
        else
        {
            var lengthMm = workOrder.MaxLength ?? 6000;
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
                || (b.MaterialType == InventoryMaterialTypes.DefectFinished && b.LiabilityType == "厂部")),
            ReworkType.ManualSelect => query.Where(b =>
                !InventoryMaterialTypes.ManualSelectReworkExcluded.Contains(b.MaterialType)),
            _ => query.Where(b => false) // 未知类型返回空
        };

        var batches = await query.ToListAsync();

        // 计算各类型边界条件
        var odMin = reworkType switch
        {
            ReworkType.EmptyDrawing => Math.Round(calculatedOd * 1.05m, 3),
            ReworkType.FewerPass => Math.Round(calculatedOd * 1.1m, 3),
            _ => 0m // ManualSelect: 不限外径
        };
        var odMax = Math.Round(calculatedOd * 2m, 3);

        var wtMin = reworkType switch
        {
            ReworkType.EmptyDrawing => Math.Round(calculatedWt * 0.95m, 3),
            ReworkType.FewerPass => Math.Round(calculatedWt * 1.05m, 3),
            ReworkType.ManualSelect => Math.Round(calculatedWt, 3),
            _ => 0m
        };
        var wtMax = reworkType switch
        {
            ReworkType.EmptyDrawing => Math.Round(calculatedWt * 1.05m, 3),
            ReworkType.FewerPass => Math.Round(calculatedWt * 2m, 3),
            _ => decimal.MaxValue // ManualSelect: 不限壁厚上限
        };

        var minUnitWeight = Math.Round(requiredUnitWeight * 1.05m, 3);

        var available = batches
            .Where(b =>
            {
                // 外径条件
                if (reworkType != ReworkType.ManualSelect && b.ActualOuterDiameter.HasValue)
                {
                    if (b.ActualOuterDiameter < odMin || b.ActualOuterDiameter > odMax)
                        return false;
                }

                // 壁厚条件
                if (b.ActualWallThickness.HasValue)
                {
                    if (b.ActualWallThickness < wtMin || b.ActualWallThickness > wtMax)
                        return false;
                }

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
                ActualOuterDiameter = b.ActualOuterDiameter,
                ActualWallThickness = b.ActualWallThickness
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

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();
            await UpdateMaterialPlanStatusAsync(plan.WorkOrderId);
            await transaction.CommitAsync();
            await RefreshReadModelAsync(plan.WorkOrderId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

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
            RawMaterialType = Enum.TryParse<RawMaterialType>(request.RawMaterialType, out var rt)
                ? rt
                : throw new BusinessException($"无效的原料类型: {request.RawMaterialType}"),
            RoundBarSpec = request.RoundBarSpec,
            PiercingSpec = request.PiercingSpec,
            RequiredUnitWeight = request.RequiredUnitWeight,
            RequiredPieces = request.RequiredPieces,
            RequiredWeight = request.RequiredWeight,
            RequiredDate = request.RequiredDate,
            Remark = request.Remark,
        };

        using var transaction = await _context.Database.BeginTransactionAsync();
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
            plan.StandardCycle = CalculateStandardCycleFromSections(
                pierceSections, workOrder.DeliveryState, workOrder.PlantGrade);
            if (plan.StandardCycle == 0)
                throw new BusinessException("工艺周期计算失败：工序组工段数据不完整，无法计算工艺周期");
            _context.Entry(plan).Property(e => e.StandardCycle).IsModified = true;
            await _context.SaveChangesAsync();

            // 同步写入/更新 StandardProcessCycle 引用表
            await UpsertStandardProcessCycleAsync(
                plan.PlantGrade,
                "荒管",
                plan.PiercingSpec,
                workOrder.Specification,
                EnumHelper.GetDisplayName(workOrder.DeliveryState),
                plan.StandardCycle);

            // 刷新工单状态（与创建在同一事务中）
            await UpdateMaterialPlanStatusAsync(request.WorkOrderId);

            await transaction.CommitAsync();
            await RefreshReadModelAsync(request.WorkOrderId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

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
        plan.RawMaterialType = Enum.TryParse<RawMaterialType>(request.RawMaterialType, out var rt)
            ? rt
            : throw new BusinessException($"无效的原料类型: {request.RawMaterialType}");
        plan.RoundBarSpec = request.RoundBarSpec;
        plan.PiercingSpec = request.PiercingSpec;
        plan.RequiredUnitWeight = request.RequiredUnitWeight;
        plan.RequiredPieces = request.RequiredPieces;
        plan.RequiredWeight = request.RequiredWeight;
        plan.RequiredDate = request.RequiredDate;
        plan.Remark = request.Remark;

        using var transaction = await _context.Database.BeginTransactionAsync();
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
            plan.StandardCycle = CalculateStandardCycleFromSections(
                pierceSections, workOrder.DeliveryState, workOrder.PlantGrade);
            if (plan.StandardCycle == 0)
                throw new BusinessException("工艺周期计算失败：工序组工段数据不完整，无法计算工艺周期");
            _context.Entry(plan).Property(e => e.StandardCycle).IsModified = true;
            await _context.SaveChangesAsync();

            // 同步写入/更新 StandardProcessCycle 引用表
            await UpsertStandardProcessCycleAsync(
                plan.PlantGrade,
                "荒管",
                plan.PiercingSpec,
                workOrder.Specification,
                EnumHelper.GetDisplayName(workOrder.DeliveryState),
                plan.StandardCycle);

            await UpdateMaterialPlanStatusAsync(plan.WorkOrderId);
            await transaction.CommitAsync();
            await RefreshReadModelAsync(plan.WorkOrderId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

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
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.RoundBarPiercingPlans.Remove(plan);
            await _context.SaveChangesAsync();

            // 刷新工单状态（与删除在同一事务中）
            await UpdateMaterialPlanStatusAsync(workOrderId);

            await transaction.CommitAsync();
            await RefreshReadModelAsync(workOrderId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        _logger.LogInformation("删除圆棒穿孔计划成功: ID {Id}", id);
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

        // 库存使用计划
        var inventoryPlans = await _context.InventoryPlans
            .Where(p => p.WorkOrderId == workOrderId && p.PlanStatus != InventoryPlanStatus.Cancelled)
            .ToListAsync();

        var regularInventory = inventoryPlans.Where(p => p.ReworkType == null).ToList();
        var reworkPlans = inventoryPlans.Where(p => p.ReworkType != null).ToList();

        if (regularInventory.Any())
        {
            var status = CalculateInventoryPlanStatus(workOrder, regularInventory);
            dto.Items.Add(new MaterialPlanItemDto
            {
                PlanType = "Inventory",
                PlanTypeText = "库存使用",
                RecordCount = regularInventory.Count,
                Summary = $"{regularInventory.First().BatchNo} × {regularInventory.Sum(p => p.UsedQuantity ?? 0)}支 / {regularInventory.Sum(p => p.UsedWeight)}kg",
                RequiredDate = regularInventory.Min(p => p.RequiredDate),
                Status = status
            });
        }

        if (reworkPlans.Any())
        {
            var status = CalculateInventoryPlanStatus(workOrder, reworkPlans, isRework: true);
            dto.Items.Add(new MaterialPlanItemDto
            {
                PlanType = "Rework",
                PlanTypeText = "库料改制",
                RecordCount = reworkPlans.Count,
                Summary = $"{reworkPlans.First().BatchNo} × {reworkPlans.Sum(p => p.UsedQuantity ?? 0)}支 / {reworkPlans.Sum(p => p.UsedWeight)}kg",
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
            var status = CalculatePlanStatus(workOrder, piercingPlans.Cast<BaseEntity>().ToList(), isSemi: false, isPiercing: true);
            dto.Items.Add(new MaterialPlanItemDto
            {
                PlanType = "Piercing",
                PlanTypeText = "圆棒穿孔",
                RecordCount = piercingPlans.Count,
                Summary = $"{piercingPlans.First().RoundBarSpec} → {piercingPlans.First().PiercingSpec} × {piercingPlans.Sum(p => p.RequiredPieces ?? 0)}支 / {piercingPlans.Sum(p => p.RequiredWeight)}kg",
                RequiredDate = piercingPlans.Min(p => p.RequiredDate),
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

        var inventoryPlans = await _context.InventoryPlans
            .Where(p => p.WorkOrderId == workOrderId && p.PlanStatus != InventoryPlanStatus.Cancelled)
            .ToListAsync();

        var regularInventory = inventoryPlans.Where(p => p.ReworkType == null).ToList();
        var reworkPlans = inventoryPlans.Where(p => p.ReworkType != null).ToList();

        var piercingPlans = await _context.RoundBarPiercingPlans
            .Where(p => p.WorkOrderId == workOrderId)
            .ToListAsync();

        var hasSemi = semiPlans.Any();
        var hasFinish = finishPlans.Any();
        var hasInventory = regularInventory.Any();
        var hasRework = reworkPlans.Any();
        var hasPiercing = piercingPlans.Any();

        if (!hasSemi && !hasFinish && !hasInventory && !hasRework && !hasPiercing)
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

            if (hasInventory)
            {
                var s = CalculateInventoryPlanStatus(workOrder, regularInventory);
                statuses.Add(s);
                rates.Add(CalculateInventoryPlanRate(workOrder, regularInventory));
            }

            if (hasRework)
            {
                var s = CalculateInventoryPlanStatus(workOrder, reworkPlans, isRework: true);
                statuses.Add(s);
                rates.Add(CalculateInventoryPlanRate(workOrder, reworkPlans));
            }

            if (hasPiercing)
            {
                var s = CalculatePlanStatus(workOrder, piercingPlans.Cast<BaseEntity>().ToList(), isSemi: false, isPiercing: true);
                statuses.Add(s);
                rates.Add(CalculatePlanRate(workOrder, piercingPlans.Cast<BaseEntity>().ToList(), isSemi: false, isPiercing: true));
            }

            // 工单满足率 = 5种用料相加（总覆盖率）
            var totalRate = Math.Min(rates.Sum(), 999m);
            workOrder.MaterialPlanRate = totalRate;
            workOrder.MaterialPlanStatus = CalculateOverallStatus(workOrder, totalRate);
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 计算单个计划的状态（工单级，含"理论满足"）
    /// </summary>
    private MaterialPlanStatus CalculatePlanStatus(WoEntity workOrder,
        IReadOnlyCollection<BaseEntity> plans, bool isSemi, bool isPiercing = false)
    {
        var rate = CalculatePlanRate(workOrder, plans, isSemi, isPiercing);

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
    /// 计算库存使用计划状态（工单级，含"理论满足"）
    /// </summary>
    private MaterialPlanStatus CalculateInventoryPlanStatus(WoEntity workOrder,
        IReadOnlyCollection<InventoryPlan> plans, bool isRework = false)
    {
        var rate = CalculateInventoryPlanRate(workOrder, plans);

        if (workOrder.LengthStatus == LengthStatus.Fixed)
        {
            if (rate < 100m) return MaterialPlanStatus.Partial;
            if (rate < 102m) return MaterialPlanStatus.TheoreticalSatisfied;
            if (rate <= 110m) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
        else
        {
            if (rate < 100m) return MaterialPlanStatus.Partial;
            if (rate < 105m) return MaterialPlanStatus.TheoreticalSatisfied;
            if (rate <= 120m) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
    }

    /// <summary>
    /// 基于总满足率计算整体状态
    /// </summary>
    private static MaterialPlanStatus CalculateOverallStatus(WoEntity workOrder, decimal totalRate)
    {
        if (totalRate <= 0) return MaterialPlanStatus.NotPlanned;

        if (workOrder.LengthStatus == LengthStatus.Fixed)
        {
            if (totalRate < 100m) return MaterialPlanStatus.Partial;
            if (totalRate < 102m) return MaterialPlanStatus.TheoreticalSatisfied;
            if (totalRate <= 110m) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
        else
        {
            if (totalRate < 100m) return MaterialPlanStatus.Partial;
            if (totalRate < 105m) return MaterialPlanStatus.TheoreticalSatisfied;
            if (totalRate <= 120m) return MaterialPlanStatus.Satisfied;
            return MaterialPlanStatus.Excess;
        }
    }

    private async Task RefreshReadModelAsync(int workOrderId)
    {
        if (_listSummaryService == null) return;
        var salesOrderNo = await _context.WorkOrders
            .AsNoTracking()
            .Where(wo => wo.Id == workOrderId)
            .Select(wo => wo.SalesOrderNo)
            .FirstOrDefaultAsync();
        if (salesOrderNo != null)
            await _listSummaryService.RefreshBySalesOrderAsync(salesOrderNo);
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

        if (documents.Count == 0)
            throw new BusinessException("没有找到符合条件的计划");

        if (documents.Count == 1)
            return documents[0].GeneratePdf();

        return Document.Merge(documents).GeneratePdf();
    }

    /// <summary>
    /// 同步写入/更新 StandardProcessCycle 引用表。
    /// 以 (PlantGrade, RawMaterialType, RawSpec, ProductSpec, DeliveryState) 为唯一键 upsert。
    /// </summary>
    private async Task UpsertStandardProcessCycleAsync(
        string plantGrade, string rawMaterialType, string rawSpec,
        string productSpec, string deliveryState, int cycle)
    {
        var existing = await _context.StandardProcessCycles
            .FirstOrDefaultAsync(c =>
                c.PlantGrade == plantGrade &&
                c.RawMaterialType == rawMaterialType &&
                c.RawSpec == rawSpec &&
                c.ProductSpec == productSpec &&
                c.DeliveryState == deliveryState);

        if (existing != null)
        {
            existing.StandardCycleDays = cycle;
        }
        else
        {
            _context.StandardProcessCycles.Add(new StandardProcessCycle
            {
                PlantGrade = plantGrade,
                RawMaterialType = rawMaterialType,
                RawSpec = rawSpec,
                ProductSpec = productSpec,
                DeliveryState = deliveryState,
                StandardCycleDays = cycle
            });
        }

        await _context.SaveChangesAsync();
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
            PlantGrade = entity.PlantGrade,
            RawMaterialType = entity.RawMaterialType.ToString(),
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
            RawMaterialType = entity.RawMaterialType.ToString(),
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
            ProductType = entity.ProductType.ToString(),
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
            PlanStatus = (int)entity.PlanStatus,
            PlanStatusText = entity.PlanStatus switch
            {
                InventoryPlanStatus.Planned => "已计划",
                InventoryPlanStatus.Confirmed => "已确认",
                InventoryPlanStatus.Cancelled => "已取消",
                _ => "未知"
            },
            Remark = entity.Remark,
            ReworkType = entity.ReworkType?.ToString(),
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

}

#endregion
