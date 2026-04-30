using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Printing;

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

        var batch = await _context.InventoryBatches.FindAsync(request.InventoryBatchId);
        if (batch == null)
            throw new BusinessException("库存批次不存在");

        // 校验：批次未被其他未取消的库存使用计划引用
        var existingPlan = await _context.InventoryPlans
            .AnyAsync(p => p.InventoryBatchId == request.InventoryBatchId
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
            InventoryBatchId = request.InventoryBatchId,
            BatchNo = batch.BatchNo,
            PlantGrade = batch.PlantGrade,
            Specification = batch.Specification,
            InputMultiple = request.InputMultiple,
            UsageMode = request.UsageMode,
            UsedQuantity = request.UsedQuantity,
            UsedWeight = request.UsedWeight,
            RequiredDate = request.RequiredDate,
            PlanStatus = InventoryPlanStatus.Planned,
            Remark = request.Remark,
            ReworkType = request.ReworkType,
            ProcessPlan = request.ProcessPlan
        };

        _context.InventoryPlans.Add(plan);
        await _context.SaveChangesAsync();

        // 刷新工单状态
        await UpdateMaterialPlanStatusAsync(request.WorkOrderId);

        _logger.LogInformation("创建库存使用计划成功: 工单ID {WorkOrderId}, 批次号 {BatchNo}, 重量 {Weight}",
            request.WorkOrderId, batch.BatchNo, request.UsedWeight);

        return plan.ToDto();
    }

    public async Task DeleteInventoryPlanAsync(int id)
    {
        var plan = await _context.InventoryPlans.FindAsync(id);
        if (plan == null)
            throw new BusinessException("库存使用计划不存在");

        var workOrderId = plan.WorkOrderId;
        _context.InventoryPlans.Remove(plan);
        await _context.SaveChangesAsync();

        // 刷新工单状态
        await UpdateMaterialPlanStatusAsync(workOrderId);

        _logger.LogInformation("删除库存使用计划成功: ID {Id}", id);
    }

    public async Task<List<AvailableInventoryBatchDto>> GetAvailableInventoryAsync(int workOrderId)
    {
        var workOrder = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == workOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        // 解析外径和壁厚
        var od = ParseOuterDiameter(workOrder.Specification);
        var wt = ParseWallThickness(workOrder.Specification);

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

        // 定义5种牌号替代映射（高级可替低级）
        var gradeSubstitutes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["30400"] = "304L0",
            ["31600"] = "316L0",
            ["316H0"] = "31600",
            ["34700"] = "347H0",
            ["22051"] = "22052"
        };

        // 获取已被其他未取消库存使用计划引用的批次ID
        var usedBatchIds = await _context.InventoryPlans
            .Where(p => p.PlanStatus != InventoryPlanStatus.Cancelled)
            .Select(p => p.InventoryBatchId)
            .Distinct()
            .ToListAsync();

        // 查询可用库存
        var query = _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.RemainingWeight > 0
                && (b.MaterialType == "备料成品" || b.MaterialType == "余库料")
                && !usedBatchIds.Contains(b.Id));

        // 牌号条件：精确匹配 或 高级替代
        var eligibleGrades = new List<string> { workOrder.PlantGrade };
        if (gradeSubstitutes.TryGetValue(workOrder.PlantGrade, out var substitute))
        {
            eligibleGrades.Add(substitute);
        }
        // 反向检查：是否有其他牌号可以替代工单的牌号（即工单牌号是某高级牌号的低级版）
        foreach (var kvp in gradeSubstitutes)
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

    public async Task<List<AvailableInventoryBatchDto>> GetAvailableReworkInventoryAsync(int workOrderId, string reworkType)
    {
        var workOrder = await _context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(wo => wo.Id == workOrderId);
        if (workOrder == null)
            throw new BusinessException("工单不存在");

        // 解析名义外径和壁厚
        var nominalOd = ParseOuterDiameter(workOrder.Specification);
        var nominalWt = ParseWallThickness(workOrder.Specification);

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

        // 工厂牌号替代映射（高级可替低级）：key=低级, value=高级
        var gradeSubstitutes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["30400"] = "304L0",
            ["31600"] = "316L0",
            ["316H0"] = "31600",
            ["34700"] = "347H0",
            ["22051"] = "22052"
        };

        // 排除规则：316L0不可替代316H0
        var exclude316L0For316H0 = string.Equals(workOrder.PlantGrade, "316H0", StringComparison.OrdinalIgnoreCase);

        // 合格牌号：工单本身牌号 + 高级替代牌号
        var eligibleGrades = new List<string> { workOrder.PlantGrade };
        if (gradeSubstitutes.TryGetValue(workOrder.PlantGrade, out var higherGrade))
        {
            if (!(exclude316L0For316H0 && string.Equals(higherGrade, "316L0", StringComparison.OrdinalIgnoreCase)))
            {
                eligibleGrades.Add(higherGrade);
            }
        }

        // 已被其他未取消计划引用的批次ID
        var usedBatchIds = await _context.InventoryPlans
            .Where(p => p.PlanStatus != InventoryPlanStatus.Cancelled)
            .Select(p => p.InventoryBatchId)
            .Distinct()
            .ToListAsync();

        // 根据改制类型构建查询
        var query = _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.RemainingWeight > 0
                && !usedBatchIds.Contains(b.Id)
                && eligibleGrades.Contains(b.PlantGrade));

        // 物料名称筛选
        query = reworkType switch
        {
            "EmptyDrawing" or "FewerPass" => query.Where(b =>
                b.MaterialType == "备料成品"
                || (b.MaterialType == "中间品" && !b.IsLinkedToWorkOrder)
                || b.MaterialType == "余库料"
                || (b.MaterialType == "次品中间品" && b.LiabilityType == "厂部")
                || (b.MaterialType == "次品成品" && b.LiabilityType == "厂部")),
            "ManualSelect" => query.Where(b =>
                b.MaterialType != "圆棒"
                && b.MaterialType != "次品圆棒"
                && b.MaterialType != "次品荒管"
                && b.MaterialType != "报废品"),
            _ => query.Where(b => false) // 未知类型返回空
        };

        var batches = await query.ToListAsync();

        // 计算各类型边界条件
        var odMin = reworkType switch
        {
            "EmptyDrawing" => Math.Round(calculatedOd * 1.05m, 3),
            "FewerPass" => Math.Round(calculatedOd * 1.1m, 3),
            _ => 0m // ManualSelect: 不限外径
        };
        var odMax = Math.Round(calculatedOd * 2m, 3);

        var wtMin = reworkType switch
        {
            "EmptyDrawing" => Math.Round(calculatedWt * 0.95m, 3),
            "FewerPass" => Math.Round(calculatedWt * 1.05m, 3),
            "ManualSelect" => Math.Round(calculatedWt, 3),
            _ => 0m
        };
        var wtMax = reworkType switch
        {
            "EmptyDrawing" => Math.Round(calculatedWt * 1.05m, 3),
            "FewerPass" => Math.Round(calculatedWt * 2m, 3),
            _ => decimal.MaxValue // ManualSelect: 不限壁厚上限
        };

        var minUnitWeight = Math.Round(requiredUnitWeight * 1.05m, 3);

        var available = batches
            .Where(b =>
            {
                // 外径条件
                if (reworkType != "ManualSelect" && b.ActualOuterDiameter.HasValue)
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

    /// <summary>
    /// 从规格字符串解析壁厚
    /// </summary>
    private static decimal ParseWallThickness(string specification)
    {
        if (string.IsNullOrEmpty(specification))
            return 0;

        var parts = specification.Split('*');
        if (parts.Length > 1 && decimal.TryParse(parts[1], out var wt))
            return wt;

        return 0;
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

        var hasSemi = semiPlans.Any();
        var hasFinish = finishPlans.Any();
        var hasInventory = regularInventory.Any();
        var hasRework = reworkPlans.Any();

        if (!hasSemi && !hasFinish && !hasInventory && !hasRework)
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
                rates.Add(CalculateInventoryPlanRate(workOrder, reworkPlans, isRework: true));
            }

            // 工单满足率 = 4种用料相加（总覆盖率）
            var totalRate = Math.Min(rates.Sum(), 999m);
            workOrder.MaterialPlanRate = totalRate;
            workOrder.MaterialPlanStatus = CalculateOverallStatus(workOrder, totalRate);
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
                effectiveWeight = semiPlans.Sum(p => p.RequiredWeight) * 1.05m;
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
    private decimal CalculateInventoryPlanRate(WorkOrder workOrder,
        IReadOnlyCollection<InventoryPlan> plans, bool isRework = false)
    {
        if (workOrder.LengthStatus == LengthStatus.Fixed)
        {
            // 定尺：按支数，直接按实际出库支数 × 投料倍率
            var rawPieces = (int)(plans.Sum(p => (p.UsedQuantity ?? 0) * p.InputMultiple));
            // 库料改制涉及生产工艺，有2%损耗
            var effectivePieces = isRework ? (int)(rawPieces * 1.02m) : rawPieces;

            if (workOrder.TotalQuantity <= 0) return 0;
            return Math.Round((decimal)effectivePieces / workOrder.TotalQuantity * 100m, 0);
        }
        else
        {
            // 范围尺/非定尺：按重量，直接按实际出库重量
            var rawWeight = plans.Sum(p => p.UsedWeight);
            // 库料改制涉及生产工艺，有5%损耗
            var effectiveWeight = isRework ? rawWeight * 1.05m : rawWeight;

            if (workOrder.TotalWeight <= 0) return 0;
            return Math.Round(effectiveWeight / workOrder.TotalWeight * 100m, 0);
        }
    }

    /// <summary>
    /// 计算库存使用计划状态（工单级，含"理论满足"）
    /// </summary>
    private MaterialPlanStatus CalculateInventoryPlanStatus(WorkOrder workOrder,
        IReadOnlyCollection<InventoryPlan> plans, bool isRework = false)
    {
        var rate = CalculateInventoryPlanRate(workOrder, plans, isRework);

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
    private static MaterialPlanStatus CalculateOverallStatus(WorkOrder workOrder, decimal totalRate)
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

    public static InventoryPlanDto ToDto(this InventoryPlan entity)
    {
        return new InventoryPlanDto
        {
            Id = entity.Id,
            WorkOrderId = entity.WorkOrderId,
            PlanDate = entity.PlanDate,
            InventoryBatchId = entity.InventoryBatchId,
            BatchNo = entity.BatchNo,
            PlantGrade = entity.PlantGrade,
            Specification = entity.Specification,
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
            ReworkType = entity.ReworkType,
            ReworkTypeText = entity.ReworkType switch
            {
                "EmptyDrawing" => "空拉改制",
                "FewerPass" => "少道次改制",
                "ManualSelect" => "人工选择改制",
                _ => null
            },
            ProcessPlan = entity.ProcessPlan,
            CreatedTime = entity.CreatedTime,
            CreatedBy = entity.CreatedBy
        };
    }
}

#endregion
