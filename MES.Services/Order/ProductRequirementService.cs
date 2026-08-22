// 文件路径: MES.Services/ProductRequirementService.cs
using Microsoft.EntityFrameworkCore;
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
using MES.Core.Enums;
using MES.Core.Exceptions;
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
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Order;
using MES.Services.Order;

namespace MES.Services.Order;

public class ProductRequirementService : IProductRequirementService
{
    private readonly AppDbContext _context;
    private readonly IOrderService _orderService;

    public ProductRequirementService(AppDbContext context, IOrderService orderService)
    {
        _context = context;
        _orderService = orderService;
    }

    public async Task<ProductRequirementDto?> GetByOrderItemIdAsync(int orderItemId)
    {
        var entity = await _context.ProductRequirements
            .AsNoTracking()
            .FirstOrDefaultAsync(pr => pr.OrderItemId == orderItemId);

        if (entity == null) return null;
        return await MapToDtoWithSequenceAsync(entity);
    }

    public async Task<ProductRequirementDto> CreateOrUpdateAsync(int orderItemId, CreateProductRequirementRequest request)
    {
        var orderItem = await _context.OrderItems
            .FirstOrDefaultAsync(oi => oi.Id == orderItemId);
        if (orderItem == null)
            throw new BusinessException("订单项次不存在");

        var orderNo = orderItem.SalesOrder?.OrderNumber ?? await _context.SalesOrders
            .Where(so => so.Id == orderItem.SalesOrderId)
            .Select(so => so.OrderNumber)
            .FirstOrDefaultAsync() ?? "";

        var existing = await _context.ProductRequirements
            .FirstOrDefaultAsync(pr => pr.OrderItemId == orderItemId);

        if (existing != null)
        {
            // 更新现有技术要求
            existing.RequirementType = request.RequirementType;
            existing.ChemicalComposition = request.ChemicalComposition;
            existing.PmiInspection = request.PmiInspection;
            existing.SurfaceInspection = request.SurfaceInspection;
            existing.Dimension = request.Dimension;
            existing.Endoscopy = request.Endoscopy;
            existing.HydrostaticTest = request.HydrostaticTest;
            existing.UnderwaterPressure = request.UnderwaterPressure;
            existing.EddyCurrent = request.EddyCurrent;
            existing.UltrasonicTest = request.UltrasonicTest;
            existing.PortColoring = request.PortColoring;
            existing.RadiographicTest = request.RadiographicTest;
            existing.HardnessRockwell = request.HardnessRockwell;
            existing.HardnessBrinell = request.HardnessBrinell;
            existing.HardnessVickers = request.HardnessVickers;
            existing.TensileRoomTemp = request.TensileRoomTemp;
            existing.TensileHighTemp = request.TensileHighTemp;
            existing.WeldJointTensile = request.WeldJointTensile;
            existing.ImpactTest = request.ImpactTest;
            existing.WeldJointImpact = request.WeldJointImpact;
            existing.FlatteningTest = request.FlatteningTest;
            existing.FlaringTest = request.FlaringTest;
            existing.ExpandingTest = request.ExpandingTest;
            existing.BendTest = request.BendTest;
            existing.WeldJointBend = request.WeldJointBend;
            existing.GrainSize = request.GrainSize;
            existing.IntergranularCorrosion = request.IntergranularCorrosion;
            existing.PittingCorrosion = request.PittingCorrosion;
            existing.FerriteContent = request.FerriteContent;
            existing.Macrostructure = request.Macrostructure;
            existing.OtherRequirement = request.OtherRequirement;
            existing.OrderNo = orderNo;
            existing.ItemSequence = orderItem.Sequence;

            await _context.SaveChangesAsync();
            await _orderService.RefreshByOrderIdAsync(orderItem.SalesOrderId);
            return await MapToDtoWithSequenceAsync(existing, orderItem.Sequence);
        }
        else
        {
            // 只有用户主动保存时才创建新记录
            var entity = new ProductRequirement
            {
                OrderItemId = orderItemId,
                OrderNo = orderNo,
                ItemSequence = orderItem.Sequence,
                RequirementType = request.RequirementType,
                ChemicalComposition = request.ChemicalComposition,
                PmiInspection = request.PmiInspection,
                SurfaceInspection = request.SurfaceInspection,
                Dimension = request.Dimension,
                Endoscopy = request.Endoscopy,
                HydrostaticTest = request.HydrostaticTest,
                UnderwaterPressure = request.UnderwaterPressure,
                EddyCurrent = request.EddyCurrent,
                UltrasonicTest = request.UltrasonicTest,
                PortColoring = request.PortColoring,
                RadiographicTest = request.RadiographicTest,
                HardnessRockwell = request.HardnessRockwell,
                HardnessBrinell = request.HardnessBrinell,
                HardnessVickers = request.HardnessVickers,
                TensileRoomTemp = request.TensileRoomTemp,
                TensileHighTemp = request.TensileHighTemp,
                WeldJointTensile = request.WeldJointTensile,
                ImpactTest = request.ImpactTest,
                WeldJointImpact = request.WeldJointImpact,
                FlatteningTest = request.FlatteningTest,
                FlaringTest = request.FlaringTest,
                ExpandingTest = request.ExpandingTest,
                BendTest = request.BendTest,
                WeldJointBend = request.WeldJointBend,
                GrainSize = request.GrainSize,
                IntergranularCorrosion = request.IntergranularCorrosion,
                PittingCorrosion = request.PittingCorrosion,
                FerriteContent = request.FerriteContent,
                Macrostructure = request.Macrostructure,
                OtherRequirement = request.OtherRequirement
            };

            _context.ProductRequirements.Add(entity);
            await _context.SaveChangesAsync();
            await _orderService.RefreshByOrderIdAsync(orderItem.SalesOrderId);
            return await MapToDtoWithSequenceAsync(entity, orderItem.Sequence);
        }
    }

    public Task DeleteAsync(int orderItemId)
    {
        throw new BusinessException("技术要求不允许单独删除，请删除对应的订单项次");
    }

    /// <summary>
    /// 根据订单ID获取所有项次的产品要求列表
    /// 注意：只返回真正存在的技术要求，不自动创建空对象
    /// </summary>
    public async Task<List<ProductRequirementDto>> GetByOrderIdAsync(int orderId)
    {
        // 获取订单的所有项次
        var orderItems = await _context.OrderItems
            .Where(oi => oi.SalesOrderId == orderId)
            .OrderBy(oi => oi.Sequence)
            .ToListAsync();

        // 获取所有存在的技术要求
        var orderItemIds = orderItems.Select(oi => oi.Id).ToList();
        var existingRequirements = await _context.ProductRequirements
            .Where(pr => orderItemIds.Contains(pr.OrderItemId))
            .ToDictionaryAsync(pr => pr.OrderItemId, pr => pr);

        var result = new List<ProductRequirementDto>();

        foreach (var item in orderItems)
        {
            if (existingRequirements.TryGetValue(item.Id, out var requirement))
            {
                // 有技术要求，添加到结果
                result.Add(ToDto(requirement, item.Sequence));
            }
            // 没有技术要求：不添加到结果，保持 null/不存在状态
        }

        return result;
    }

    private async Task<ProductRequirementDto> MapToDtoWithSequenceAsync(ProductRequirement entity, int? explicitSequence = null)
    {
        int sequence;
        if (explicitSequence.HasValue)
        {
            sequence = explicitSequence.Value;
        }
        else
        {
            var orderItem = await _context.OrderItems
                .FirstOrDefaultAsync(oi => oi.Id == entity.OrderItemId);
            sequence = orderItem?.Sequence ?? 0;
        }

        return ToDto(entity, sequence);
    }

    private static ProductRequirementDto ToDto(ProductRequirement entity, int sequence) => new()
    {
        Id = entity.Id,
        OrderItemId = entity.OrderItemId,
        Sequence = sequence,
        RequirementType = entity.RequirementType,
        ChemicalComposition = entity.ChemicalComposition,
        PmiInspection = entity.PmiInspection,
        SurfaceInspection = entity.SurfaceInspection,
        Dimension = entity.Dimension,
        Endoscopy = entity.Endoscopy,
        HydrostaticTest = entity.HydrostaticTest,
        UnderwaterPressure = entity.UnderwaterPressure,
        EddyCurrent = entity.EddyCurrent,
        UltrasonicTest = entity.UltrasonicTest,
        PortColoring = entity.PortColoring,
        RadiographicTest = entity.RadiographicTest,
        HardnessRockwell = entity.HardnessRockwell,
        HardnessBrinell = entity.HardnessBrinell,
        HardnessVickers = entity.HardnessVickers,
        TensileRoomTemp = entity.TensileRoomTemp,
        TensileHighTemp = entity.TensileHighTemp,
        WeldJointTensile = entity.WeldJointTensile,
        ImpactTest = entity.ImpactTest,
        WeldJointImpact = entity.WeldJointImpact,
        FlatteningTest = entity.FlatteningTest,
        FlaringTest = entity.FlaringTest,
        ExpandingTest = entity.ExpandingTest,
        BendTest = entity.BendTest,
        WeldJointBend = entity.WeldJointBend,
        GrainSize = entity.GrainSize,
        IntergranularCorrosion = entity.IntergranularCorrosion,
        PittingCorrosion = entity.PittingCorrosion,
        FerriteContent = entity.FerriteContent,
        Macrostructure = entity.Macrostructure,
        OtherRequirement = entity.OtherRequirement,
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };

    public async Task<ProductRequirementDefaultsDto> GetDefaultRequirementsByStandardNoAsync(string? standardNo)
    {
        var defaults = new ProductRequirementDefaultsDto();
        if (string.IsNullOrWhiteSpace(standardNo)) return defaults;

        // 规范化匹配：去空格 + 忽略大小写（SQL Server collation 已忽略大小写，仅需处理空格）
        // 订单产品标准与工厂检验项要求标准号存在空格书写差异（如 GB/T14976-2025 vs GB/T 14976-2025）
        var normalized = standardNo.Replace(" ", "");
        var req = await _context.FactoryInspectionRequirements
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.StandardNo != null && x.StandardNo.Replace(" ", "") == normalized);
        if (req == null) return defaults;

        defaults.ChemicalComposition = IsMandatory(req.ChemicalComposition);
        defaults.PmiInspection = MapFactoryRequirement(req.PmiInspection);
        defaults.SurfaceInspection = MapFactoryRequirement(req.SurfaceInspection);
        defaults.Dimension = MapFactoryRequirement(req.Dimension);
        defaults.Endoscopy = MapFactoryRequirement(req.Endoscopy);
        defaults.HydrostaticTest = MapFactoryRequirement(req.HydrostaticTest);
        defaults.UnderwaterPressure = MapFactoryRequirement(req.UnderwaterPressure);
        defaults.EddyCurrent = MapFactoryRequirement(req.EddyCurrent);
        defaults.UltrasonicTest = MapFactoryRequirement(req.UltrasonicTest);
        defaults.PortColoring = MapFactoryRequirement(req.PortColoring);
        defaults.RadiographicTest = MapFactoryRequirement(req.RadiographicTest);
        defaults.HardnessRockwell = IsMandatory(req.HardnessRockwell);
        defaults.HardnessBrinell = IsMandatory(req.HardnessBrinell);
        defaults.HardnessVickers = IsMandatory(req.HardnessVickers);
        defaults.TensileRoomTemp = IsMandatory(req.TensileRoomTemp);
        defaults.TensileHighTemp = IsMandatory(req.TensileHighTemp);
        defaults.WeldJointTensile = IsMandatory(req.WeldJointTensile);
        defaults.ImpactTest = IsMandatory(req.ImpactTest);
        defaults.WeldJointImpact = IsMandatory(req.WeldJointImpact);
        defaults.FlatteningTest = IsMandatory(req.FlatteningTest);
        defaults.FlaringTest = IsMandatory(req.FlaringTest);
        defaults.ExpandingTest = IsMandatory(req.ExpandingTest);
        defaults.BendTest = IsMandatory(req.BendTest);
        defaults.WeldJointBend = IsMandatory(req.WeldJointBend);
        defaults.GrainSize = IsMandatory(req.GrainSize);
        defaults.IntergranularCorrosion = IsMandatory(req.IntergranularCorrosion);
        defaults.PittingCorrosion = IsMandatory(req.PittingCorrosion);
        defaults.FerriteContent = IsMandatory(req.FerriteContent);
        defaults.Macrostructure = IsMandatory(req.Macrostructure);
        return defaults;
    }

    /// <summary>
    /// 按工厂检验项要求全面回填所有技术要求：
    /// 订单项次标准号（去空格规范化）→ 工厂检验项要求匹配，字段含"必检"→true；液压检验仅定尺钢管带出，非定尺→false。
    /// 匹配不到标准号/标准号为空的项次保持现状不动。
    /// </summary>
    public async Task<int> RefreshDefaultsAllAsync()
    {
        // 工厂检验项要求：按"去空格标准号"规范化建字典（SQL collation 忽略大小写，内存用 OrdinalIgnoreCase）
        var factoryList = await _context.FactoryInspectionRequirements.AsNoTracking().ToListAsync();
        var factoryMap = factoryList
            .Where(x => !string.IsNullOrWhiteSpace(x.StandardNo))
            .GroupBy(x => x.StandardNo.Replace(" ", ""), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // 订单项次：Id → (标准号, 长度状态)
        var itemMap = (await _context.OrderItems.AsNoTracking()
                .Select(oi => new { oi.Id, oi.StandardNo, oi.LengthStatus })
                .ToListAsync())
            .Where(oi => !string.IsNullOrWhiteSpace(oi.StandardNo))
            .ToDictionary(oi => oi.Id);

        var requirements = await _context.ProductRequirements.ToListAsync();

        var updated = 0;
        foreach (var pr in requirements)
        {
            if (!itemMap.TryGetValue(pr.OrderItemId, out var item)) continue;
            if (!factoryMap.TryGetValue(item.StandardNo!.Replace(" ", ""), out var req)) continue;

            ApplyFactoryDefaults(pr, req, item.LengthStatus);
            updated++;
        }

        await _context.SaveChangesAsync();
        return updated;
    }

    /// <summary>
    /// 按销售订单号 + 工单关联订单项次序号列表（逗号分隔）取质量备注：
    /// ⚠️ OrderItemIds 存的是「项次序号 Sequence」（非 OrderItem.Id），须结合订单号唯一定位 OrderItem；
    /// 取各项次技术要求的「其他要求」，按项次号排序；多条时换行分隔并带项次前缀，单条时直接返回。
    /// </summary>
    public async Task<string> GetQualityRemarkByOrderItemIdsAsync(string? salesOrderNo, string? orderItemIds)
    {
        if (string.IsNullOrWhiteSpace(salesOrderNo) || string.IsNullOrWhiteSpace(orderItemIds)) return string.Empty;

        var seqs = orderItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .ToList();
        if (seqs.Count == 0) return string.Empty;

        var remarks = await (from oi in _context.OrderItems
                             join pr in _context.ProductRequirements on oi.Id equals pr.OrderItemId
                             where oi.OrderNumber == salesOrderNo
                                   && seqs.Contains(oi.Sequence)
                                   && pr.OtherRequirement != null
                                   && pr.OtherRequirement.Trim() != ""
                             orderby oi.Sequence
                             select new { oi.Sequence, OtherRequirement = pr.OtherRequirement! })
            .AsNoTracking()
            .ToListAsync();

        if (remarks.Count == 0) return string.Empty;
        if (remarks.Count == 1) return remarks[0].OtherRequirement;

        return string.Join(Environment.NewLine, remarks.Select(r => $"项次{r.Sequence}：{r.OtherRequirement}"));
    }

    /// <summary>
    /// 将工厂检验项要求默认值覆盖写入技术要求实体（含"必检"→true）
    /// </summary>
    private static void ApplyFactoryDefaults(ProductRequirement pr, FactoryInspectionRequirement req, LengthStatus lengthStatus)
    {
        pr.ChemicalComposition = IsMandatory(req.ChemicalComposition);
        pr.PmiInspection = MapFactoryRequirement(req.PmiInspection);
        pr.SurfaceInspection = MapFactoryRequirement(req.SurfaceInspection);
        pr.Dimension = MapFactoryRequirement(req.Dimension);
        pr.Endoscopy = MapFactoryRequirement(req.Endoscopy);
        // 液压检验仅定尺钢管按标准号带出；非定尺默认"-"（不适用）
        pr.HydrostaticTest = lengthStatus == LengthStatus.Fixed
            ? MapFactoryRequirement(req.HydrostaticTest)
            : InspectionRequirementStage.None;
        pr.UnderwaterPressure = MapFactoryRequirement(req.UnderwaterPressure);
        pr.EddyCurrent = MapFactoryRequirement(req.EddyCurrent);
        pr.UltrasonicTest = MapFactoryRequirement(req.UltrasonicTest);
        pr.PortColoring = MapFactoryRequirement(req.PortColoring);
        pr.RadiographicTest = MapFactoryRequirement(req.RadiographicTest);
        pr.HardnessRockwell = IsMandatory(req.HardnessRockwell);
        pr.HardnessBrinell = IsMandatory(req.HardnessBrinell);
        pr.HardnessVickers = IsMandatory(req.HardnessVickers);
        pr.TensileRoomTemp = IsMandatory(req.TensileRoomTemp);
        pr.TensileHighTemp = IsMandatory(req.TensileHighTemp);
        pr.WeldJointTensile = IsMandatory(req.WeldJointTensile);
        pr.ImpactTest = IsMandatory(req.ImpactTest);
        pr.WeldJointImpact = IsMandatory(req.WeldJointImpact);
        pr.FlatteningTest = IsMandatory(req.FlatteningTest);
        pr.FlaringTest = IsMandatory(req.FlaringTest);
        pr.ExpandingTest = IsMandatory(req.ExpandingTest);
        pr.BendTest = IsMandatory(req.BendTest);
        pr.WeldJointBend = IsMandatory(req.WeldJointBend);
        pr.GrainSize = IsMandatory(req.GrainSize);
        pr.IntergranularCorrosion = IsMandatory(req.IntergranularCorrosion);
        pr.PittingCorrosion = IsMandatory(req.PittingCorrosion);
        pr.FerriteContent = IsMandatory(req.FerriteContent);
        pr.Macrostructure = IsMandatory(req.Macrostructure);
    }

    /// <summary>
    /// 工厂检验项要求字段是否含"必检"（含"必检"→true，表示需检验；"按需"/"-"/空 → false）
    /// 用于理化检测项（bool 存储）
    /// </summary>
    private static bool IsMandatory(string? value) => value?.Contains("必检") == true;

    /// <summary>
    /// 工厂检验项要求字段 → 检验阶段枚举（10 个成品检验项）
    /// 含"必检"→「终」（仅正式成检，默认）；"按需"/"-"/空 →「-」（不要求）
    /// </summary>
    private static InspectionRequirementStage MapFactoryRequirement(string? value)
        => IsMandatory(value) ? InspectionRequirementStage.FinalOnly : InspectionRequirementStage.None;
}
