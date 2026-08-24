using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
using MES.Core.Constants;
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
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Quality;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Quality;

/// <summary>
/// 成品检验服务实现
/// </summary>
public class FinalInspectionService : IFinalInspectionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FinalInspectionService> _logger;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;
    private readonly IQualityProcessTrackingService _qualityProcessTracking;
    private readonly IFixedLengthWorkOrderService _fixedLengthWorkOrderService;
    private readonly IMemoryCache _cache;

    public FinalInspectionService(AppDbContext context, ILogger<FinalInspectionService> logger, IWorkOrderExecutionService workOrderExecutionService, IQualityProcessTrackingService qualityProcessTracking, IFixedLengthWorkOrderService fixedLengthWorkOrderService, IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _workOrderExecutionService = workOrderExecutionService;
        _qualityProcessTracking = qualityProcessTracking;
        _fixedLengthWorkOrderService = fixedLengthWorkOrderService;
        _cache = cache;
    }

    /// <summary>
    /// 解析定尺长度字符串（如 "6000mm" 或 "6000"）为数值，无法解析返回 null。
    /// </summary>
    private static decimal? ParseFixedLength(string? fixedLength)
    {
        if (string.IsNullOrWhiteSpace(fixedLength)) return null;
        var s = fixedLength.Trim();
        if (s.EndsWith("mm", StringComparison.OrdinalIgnoreCase))
            s = s[..^2].Trim();
        return decimal.TryParse(s, out var value) ? value : null;
    }

    /// <summary>
    /// 校验成品检验定尺长度：当「订单号+主号」存在定尺工单时，定尺长度必须属于该主号下的定尺长度集合。
    /// 仅「正式成检」有此要求；「预成检」无需定尺长度归属校验。
    /// 返回 null 表示通过，否则返回错误信息（不含行号前缀，由调用方补充）。
    /// </summary>
    private async Task<string?> ValidateFixedLengthAsync(string? salesOrderNo, string? productionMainNo, string? fixedLength, string? inspectionType)
    {
        if (IsPreInspection(inspectionType)) return null; // 预成检无需定尺长度归属校验
        if (string.IsNullOrWhiteSpace(fixedLength)) return null;
        if (string.IsNullOrWhiteSpace(salesOrderNo) || string.IsNullOrWhiteSpace(productionMainNo)) return null;
        var validLengths = await _fixedLengthWorkOrderService
            .GetLengthsByMainNoAsync(salesOrderNo, productionMainNo);
        return ValidateFixedLength(salesOrderNo, productionMainNo, fixedLength, validLengths, inspectionType);
    }

    /// <summary>
    /// 定尺长度校验纯函数（预取集合版，供批量创建复用避免循环内 N+1 查询）。
    /// 仅「正式成检」有此要求；「预成检」无需定尺长度归属校验。
    /// </summary>
    private static string? ValidateFixedLength(
        string salesOrderNo, string productionMainNo, string? fixedLength, HashSet<decimal> validLengths, string? inspectionType)
    {
        if (IsPreInspection(inspectionType)) return null; // 预成检无需定尺长度归属校验
        if (string.IsNullOrWhiteSpace(fixedLength)) return null;
        var parsed = ParseFixedLength(fixedLength);
        if (parsed == null) return $"定尺长度格式不正确({fixedLength})";
        if (validLengths.Count == 0) return null; // 该订单号+主号非定尺，跳过校验
        if (validLengths.Contains(parsed.Value)) return null;
        return $"成品检验定尺长度({fixedLength})不属于该订单号+主号({salesOrderNo}/{productionMainNo})下的定尺长度";
    }

    /// <summary>
    /// 是否「预成检」：预成检无需定尺长度归属校验（其余类型含未知值均按需校验）。
    /// </summary>
    private static bool IsPreInspection(string? inspectionType)
        => !string.IsNullOrWhiteSpace(inspectionType)
           && string.Equals(inspectionType, nameof(InspectionType.PreInspection), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 计算定尺切割长度匹配标识（仅「正式成检」+ 批次长度状态=定尺 + 定尺长度可解析时计算）。
    /// 预成检/非定尺/无定尺长度一律返回 null（显示空白）。返回枚举名或 null。
    /// </summary>
    private static string? ComputeCutLengthMatch(
        string? inspectionType, string? batchLengthStatus, string? fixedLength,
        HashSet<decimal> workOrderLengths, HashSet<decimal> mainNoLengths)
    {
        if (!string.Equals(inspectionType, nameof(InspectionType.FormalInspection), StringComparison.OrdinalIgnoreCase)) return null;
        if (!string.Equals(batchLengthStatus, nameof(LengthStatus.Fixed), StringComparison.OrdinalIgnoreCase)) return null;
        var parsed = ParseFixedLength(fixedLength);
        if (parsed == null) return null;
        return CutLengthMatchHelper.Match(workOrderLengths, mainNoLengths, parsed)?.ToString();
    }

    /// <summary>
    /// 单条路径的匹配标识计算：先按适用条件守卫，命中才查询长度集合（避免无效查询）。
    /// </summary>
    private async Task<string?> ComputeCutLengthMatchAsync(
        string? inspectionType, string? batchLengthStatus, string? fixedLength,
        string? workOrderNo, string? salesOrderNo, string? productionMainNo)
    {
        if (!string.Equals(inspectionType, nameof(InspectionType.FormalInspection), StringComparison.OrdinalIgnoreCase)) return null;
        if (!string.Equals(batchLengthStatus, nameof(LengthStatus.Fixed), StringComparison.OrdinalIgnoreCase)) return null;
        var parsed = ParseFixedLength(fixedLength);
        if (parsed == null) return null;
        var woLengths = await _fixedLengthWorkOrderService.GetLengthsByWorkOrderNoAsync(workOrderNo ?? "");
        var mainLengths = await _fixedLengthWorkOrderService.GetLengthsByMainNoAsync(salesOrderNo ?? "", productionMainNo ?? "");
        return CutLengthMatchHelper.Match(woLengths, mainLengths, parsed)?.ToString();
    }

    private async Task TryRefreshExecutionSummaryAsync(string? workOrderNo)
    {
        if (string.IsNullOrWhiteSpace(workOrderNo) || workOrderNo == WorkOrderNoSentinel.NotWorkOrder) return;
        try
        {
            await _workOrderExecutionService.RefreshByWorkOrderNosAsync(new List<string> { workOrderNo });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工单执行状况刷新失败（不影响主流程）: WorkOrderNo={WorkOrderNo}", workOrderNo);
        }
    }

    private async Task TryRefreshBatchExecutionSummaryAsync(List<string> workOrderNos)
    {
        try
        {
            await _workOrderExecutionService.RefreshByWorkOrderNosAsync(workOrderNos);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工单执行状况批量刷新失败（不影响主流程）: Count={Count}", workOrderNos.Count);
        }
    }

    private async Task TryRefreshQualityProcessTrackingAsync(int productionBatchId)
    {
        try
        {
            await _qualityProcessTracking.RefreshByProductionBatchIdAsync(productionBatchId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "质量过程跟踪刷新失败（不影响主流程）: ProductionBatchId={ProductionBatchId}", productionBatchId);
        }
    }

    /// <summary>
    /// 解析制造物品，兼容历史数据中的特殊值
    /// </summary>
    private static MaterialType? ParseMaterialType(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value switch
        {
            "OrderFinishedProduct" => MaterialType.OrderFinished,
            "PreparedMaterial" or "PreparedFinished" or "StockFinished" => MaterialType.Finished,
            "SurplusStock" => MaterialType.Surplus,
            "IntermediateProduct" => MaterialType.SemiFinished,
            _ => Enum.TryParse<MaterialType>(value, true, out var r) ? r : null
        };
    }

    /// <summary>
    /// 扩展制造物品筛选值，兼容历史数据中的非标准值
    /// </summary>
    private static HashSet<string> ExpandManufacturingItemFilter(List<string> values)
    {
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in values)
        {
            expanded.Add(v);
            switch (v)
            {
                case InventoryMaterialTypes.OrderFinished:
                    expanded.Add("OrderFinishedProduct");
                    break;
                case InventoryMaterialTypes.Finished:
                    expanded.Add("PreparedMaterial");
                    expanded.Add("PreparedFinished");
                    expanded.Add("StockFinished");
                    break;
                case InventoryMaterialTypes.Surplus:
                    expanded.Add("SurplusStock");
                    break;
                case InventoryMaterialTypes.SemiFinished:
                    expanded.Add("IntermediateProduct");
                    break;
            }
        }
        return expanded;
    }

    public async Task<FinalInspectionDto?> GetByIdAsync(int id)
    {
        var entity = await _context.FinalInspections
            .AsNoTracking()
            .Include(r => r.ProductionBatch)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (entity == null) return null;
        var pb = entity.ProductionBatch;

        return new FinalInspectionDto
        {
            Id = entity.Id,
            InspectionItem = entity.InspectionItem,
            InspectionDate = entity.InspectionDate,
            BatchNo = entity.BatchNo,
            ProductionBatchId = entity.ProductionBatchId,
            ManufacturingItem = ParseMaterialType(pb?.ManufacturingItem),
            TagNo = pb?.TagNo,
            WorkOrderNo = pb?.WorkOrderNo,
            SalesOrderNo = pb?.SalesOrderNo,
            SourceUnit = pb?.SourceName,
            FurnaceNo = pb?.SourceHeatNo,
            PlantGrade = pb?.PlantGrade,
            Specification = pb?.Specification,
            ProductionType = EnumHelper.TryParse<MES.Core.Enums.ProductionType>(pb?.ProductionType),
            Salesman = pb?.Salesman,
            LengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(pb?.LengthStatus),
            DeliveryState = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(pb?.DeliveryState),
            ManufacturingStatus = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(pb?.ManufacturingStatus),
            EndCustomer = pb?.EndCustomer,
            ProductionCutQuantity = pb != null && pb.CutRequirement
                ? pb.CutQuantity
                : pb?.TheoreticalOutputQty,
            ProductionWeight = pb?.TheoreticalOutputWeight,
            FixedLength = entity.FixedLength,
            CutLengthMatchType = EnumHelper.TryParse<CutLengthMatchType>(entity.CutLengthMatchType),
            NonFixedLengthRange = entity.NonFixedLengthRange,
            EquipmentName = entity.EquipmentName,
            Shift = entity.Shift,
            Operator = entity.Operator,
            Quantity = entity.Quantity,
            Weight = entity.Weight,
            QualifiedQuantity = entity.QualifiedQuantity,
            QualifiedWeight = entity.QualifiedWeight,
            QualifiedConcessionQuantity = entity.QualifiedConcessionQuantity,
            ConcessionRemark = entity.ConcessionRemark,
            DefectReworkQuantity = entity.DefectReworkQuantity,
            DefectWarehouseQuantity = entity.DefectWarehouseQuantity,
            DefectScrapQuantity = entity.DefectScrapQuantity,
            DefectDescription = entity.DefectDescription,
            InspectionType = EnumHelper.TryParse<MES.Core.Enums.InspectionType>(entity.InspectionType),
            DefectReworkWeight = entity.DefectReworkWeight,
            DefectWarehouseWeight = entity.DefectWarehouseWeight,
            DefectScrapWeight = entity.DefectScrapWeight,
            OuterDiameterRange = entity.OuterDiameterRange,
            WallThicknessRange = entity.WallThicknessRange,
            LengthAllowanceRange = entity.LengthAllowanceRange,
            Pressure = entity.Pressure,
            HoldTime = entity.HoldTime,
            QualificationLevel = entity.QualificationLevel,
            InspectionStandard = entity.InspectionStandard,
            InspectionGrade = entity.InspectionGrade,
            InstrumentModel = entity.InstrumentModel,
            NdtMethod = entity.NdtMethod,
            StandardSampleSize = entity.StandardSampleSize,
            StandardSampleDefect = entity.StandardSampleDefect,
            ProbeType = entity.ProbeType,
            Couplant = entity.Couplant,
            CalibrationFrequency = entity.CalibrationFrequency,
            DetectionFrequency = entity.DetectionFrequency,
            DetectionSensitivity = entity.DetectionSensitivity,
            DetectionPhase = entity.DetectionPhase,
            DetectionSpeed = entity.DetectionSpeed,
            Remark = entity.Remark,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    /// <summary>
    /// 列表查询过滤（关键字/日期/自定义筛选），分页查询与健康汇总共用
    /// </summary>
    private IQueryable<FinalInspection> ApplyListQueryFilters(IQueryable<FinalInspection> queryable, QueryParams query)
    {
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(r =>
                r.BatchNo.Contains(kw) ||
                r.ProductionBatch.PlantGrade.Contains(kw) ||
                r.ProductionBatch.Specification.Contains(kw) ||
                (r.ProductionBatch.TagNo != null && r.ProductionBatch.TagNo.Contains(kw)) ||
                r.ProductionBatch.WorkOrderNo.Contains(kw) ||
                r.ProductionBatch.SalesOrderNo.Contains(kw) ||
                (r.ProductionBatch.ProductionMainNo != null && r.ProductionBatch.ProductionMainNo.Contains(kw)) ||
                (r.ProductionBatch.Salesman != null && r.ProductionBatch.Salesman.Contains(kw)) ||
                (r.ProductionBatch.SourceName != null && r.ProductionBatch.SourceName.Contains(kw)) ||
                (r.ProductionBatch.SourceHeatNo != null && r.ProductionBatch.SourceHeatNo.Contains(kw)) ||
                (r.EquipmentName != null && r.EquipmentName.Contains(kw)) ||
                (r.Operator != null && r.Operator.Contains(kw)) ||
                (r.DefectDescription != null && r.DefectDescription.Contains(kw)) ||
                (r.OuterDiameterRange != null && r.OuterDiameterRange.Contains(kw)) ||
                (r.WallThicknessRange != null && r.WallThicknessRange.Contains(kw)) ||
                (r.LengthAllowanceRange != null && r.LengthAllowanceRange.Contains(kw)) ||
                (r.QualificationLevel != null && r.QualificationLevel.Contains(kw)) ||
                (r.InspectionStandard != null && r.InspectionStandard.Contains(kw)) ||
                (r.InspectionGrade != null && r.InspectionGrade.Contains(kw)) ||
                (r.InstrumentModel != null && r.InstrumentModel.Contains(kw)) ||
                (r.NdtMethod != null && r.NdtMethod.Contains(kw)) ||
                (r.StandardSampleSize != null && r.StandardSampleSize.Contains(kw)) ||
                (r.StandardSampleDefect != null && r.StandardSampleDefect.Contains(kw)) ||
                (r.ProbeType != null && r.ProbeType.Contains(kw)) ||
                (r.Couplant != null && r.Couplant.Contains(kw)) ||
                (r.CalibrationFrequency != null && r.CalibrationFrequency.Contains(kw)) ||
                (r.DetectionFrequency != null && r.DetectionFrequency.Contains(kw)) ||
                (r.DetectionSensitivity != null && r.DetectionSensitivity.Contains(kw)) ||
                (r.DetectionPhase != null && r.DetectionPhase.Contains(kw)) ||
                (r.DetectionSpeed != null && r.DetectionSpeed.Contains(kw)) ||
                (r.ConcessionRemark != null && r.ConcessionRemark.Contains(kw)) ||
                (r.Remark != null && r.Remark.Contains(kw)));
        }

        if (query.InspectionDateFrom.HasValue)
            queryable = queryable.Where(r => r.InspectionDate >= query.InspectionDateFrom.Value);

        if (query.InspectionDateTo.HasValue)
            queryable = queryable.Where(r => r.InspectionDate <= query.InspectionDateTo.Value);

        // 自定义筛选：批量派生字段不在实体上，需通过 ProductionBatch 导航属性处理
        if (query.Filters != null && query.Filters.Count > 0)
        {
            var remainingFilters = new List<FilterDescriptor>();
            foreach (var filter in query.Filters)
            {
                if (filter.Operator != "in" || filter.Values == null || filter.Values.Count == 0)
                {
                    remainingFilters.Add(filter);
                    continue;
                }
                switch (filter.Field)
                {
                    case "ManufacturingItem":
                        queryable = queryable.Where(r => ExpandManufacturingItemFilter(filter.Values).Contains(r.ProductionBatch.ManufacturingItem));
                        break;
                    case "PlantGrade":
                        queryable = queryable.Where(r => filter.Values.Contains(r.ProductionBatch.PlantGrade));
                        break;
                    case "Specification":
                        queryable = queryable.Where(r => filter.Values.Contains(r.ProductionBatch.Specification));
                        break;
                    case "TagNo":
                        queryable = queryable.Where(r => r.ProductionBatch.TagNo != null && filter.Values.Contains(r.ProductionBatch.TagNo));
                        break;
                    case "WorkOrderNo":
                        queryable = queryable.Where(r => filter.Values.Contains(r.ProductionBatch.WorkOrderNo));
                        break;
                    case "SalesOrderNo":
                        queryable = queryable.Where(r => filter.Values.Contains(r.ProductionBatch.SalesOrderNo));
                        break;
                    case "ProductionMainNo":
                        queryable = queryable.Where(r => r.ProductionBatch.ProductionMainNo != null && filter.Values.Contains(r.ProductionBatch.ProductionMainNo));
                        break;
                    case "FurnaceNo":
                        queryable = queryable.Where(r => r.ProductionBatch.SourceHeatNo != null && filter.Values.Contains(r.ProductionBatch.SourceHeatNo));
                        break;
                    case "SourceUnit":
                        queryable = queryable.Where(r => r.ProductionBatch.SourceName != null && filter.Values.Contains(r.ProductionBatch.SourceName));
                        break;
                    case "ProductionType":
                        queryable = queryable.Where(r => filter.Values.Contains(r.ProductionBatch!.ProductionType!));
                        break;
                    case "LengthStatus":
                        queryable = queryable.Where(r => filter.Values.Contains(r.ProductionBatch.LengthStatus));
                        break;
                    case "Salesman":
                        queryable = queryable.Where(r => r.ProductionBatch.Salesman != null && filter.Values.Contains(r.ProductionBatch.Salesman));
                        break;
                    case "EndCustomer":
                        queryable = queryable.Where(r => r.ProductionBatch.EndCustomer != null && filter.Values.Contains(r.ProductionBatch.EndCustomer));
                        break;
                    case "DeliveryState":
                        queryable = queryable.Where(r => filter.Values.Contains(r.ProductionBatch.DeliveryState));
                        break;
                    case "ManufacturingStatus":
                        queryable = queryable.Where(r => r.ProductionBatch.ManufacturingStatus != null && filter.Values.Contains(r.ProductionBatch.ManufacturingStatus));
                        break;
                    case "IsDeliveryStatus":
                        queryable = queryable.Where(r => filter.Values.Contains(r.ProductionBatch.ManufacturingStatus == r.ProductionBatch.DeliveryState ? "是" : "否"));
                        break;
                    default:
                        remainingFilters.Add(filter);
                        break;
                }
            }
            query.Filters = remainingFilters;
        }

        return queryable.ApplyFilters(query.Filters);
    }

    public async Task<PagedResult<FinalInspectionDto>> GetAllAsync(QueryParams query)
    {
        var queryable = ApplyListQueryFilters(_context.FinalInspections.AsNoTracking().AsQueryable(), query);
        var totalCount = await queryable.CountAsync();

        queryable = ApplySorting(queryable, query.SortBy ?? "inspectiondate", query.IsDescending);

        // 先查询实体（含 ProductionBatch），再在内存中映射 DTO
        // 原因: ManufacturingItem 需 ParseMaterialType 处理历史特殊值
        queryable = queryable.Include(r => r.ProductionBatch);

        var entities = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        var items = entities.Select(r =>
        {
            var pb = r.ProductionBatch;
            return new FinalInspectionDto
            {
                Id = r.Id,
                InspectionItem = r.InspectionItem,
                InspectionDate = r.InspectionDate,
                InspectionType = EnumHelper.TryParse<MES.Core.Enums.InspectionType>(r.InspectionType),
                BatchNo = r.BatchNo,
                ProductionBatchId = r.ProductionBatchId,
                ManufacturingItem = ParseMaterialType(pb?.ManufacturingItem),
                TagNo = pb?.TagNo,
                WorkOrderNo = pb?.WorkOrderNo,
                SalesOrderNo = pb?.SalesOrderNo,
                ProductionMainNo = pb?.ProductionMainNo,
                SourceUnit = pb?.SourceName,
                FurnaceNo = pb?.SourceHeatNo,
                PlantGrade = pb?.PlantGrade,
                Specification = pb?.Specification,
                ProductionType = EnumHelper.TryParse<MES.Core.Enums.ProductionType>(pb?.ProductionType),
                Salesman = pb?.Salesman,
                LengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(pb?.LengthStatus),
                DeliveryState = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(pb?.DeliveryState),
                ManufacturingStatus = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(pb?.ManufacturingStatus),
                EndCustomer = pb?.EndCustomer,
                ProductionCutQuantity = pb != null && pb.CutRequirement
                    ? pb.CutQuantity
                    : pb?.TheoreticalOutputQty,
                ProductionWeight = pb?.TheoreticalOutputWeight,
                FixedLength = r.FixedLength,
                CutLengthMatchType = EnumHelper.TryParse<CutLengthMatchType>(r.CutLengthMatchType),
                NonFixedLengthRange = r.NonFixedLengthRange,
                EquipmentName = r.EquipmentName,
                Shift = r.Shift,
                Operator = r.Operator,
                Quantity = r.Quantity,
                Weight = r.Weight,
                QualifiedQuantity = r.QualifiedQuantity,
                QualifiedWeight = r.QualifiedWeight,
                QualifiedConcessionQuantity = r.QualifiedConcessionQuantity,
                ConcessionRemark = r.ConcessionRemark,
                DefectReworkQuantity = r.DefectReworkQuantity,
                DefectWarehouseQuantity = r.DefectWarehouseQuantity,
                DefectScrapQuantity = r.DefectScrapQuantity,
                DefectDescription = r.DefectDescription,
                DefectReworkWeight = r.DefectReworkWeight,
                DefectWarehouseWeight = r.DefectWarehouseWeight,
                DefectScrapWeight = r.DefectScrapWeight,
                OuterDiameterRange = r.OuterDiameterRange,
                WallThicknessRange = r.WallThicknessRange,
                LengthAllowanceRange = r.LengthAllowanceRange,
                Pressure = r.Pressure,
                HoldTime = r.HoldTime,
                QualificationLevel = r.QualificationLevel,
                InspectionStandard = r.InspectionStandard,
                InspectionGrade = r.InspectionGrade,
                InstrumentModel = r.InstrumentModel,
                NdtMethod = r.NdtMethod,
                StandardSampleSize = r.StandardSampleSize,
                StandardSampleDefect = r.StandardSampleDefect,
                ProbeType = r.ProbeType,
                Couplant = r.Couplant,
                CalibrationFrequency = r.CalibrationFrequency,
                DetectionFrequency = r.DetectionFrequency,
                DetectionSensitivity = r.DetectionSensitivity,
                DetectionPhase = r.DetectionPhase,
                DetectionSpeed = r.DetectionSpeed,
                Remark = r.Remark,
                DataSource = r.DataSource,
                CreatedTime = r.CreatedTime,
                UpdatedTime = r.UpdatedTime
            };
        }).ToList();

        return new PagedResult<FinalInspectionDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    /// <summary>
    /// 实时健康汇总（按当前筛选条件统计「成检类型与成检到料不符」的生产编号）
    /// </summary>
    public async Task<FinalInspectionHealthSummaryDto> GetFinalInspectionHealthSummaryAsync(QueryParams query)
    {
        var queryable = ApplyListQueryFilters(_context.FinalInspections.AsNoTracking().AsQueryable(), query);

        var raw = await queryable
            .Select(r => new { r.ProductionBatchId, r.InspectionType, r.BatchNo })
            .ToListAsync();
        if (raw.Count == 0)
            return new FinalInspectionHealthSummaryDto { TotalCount = 0 };

        // 取这些批次在成检到料中的 InspectionType 集合（一个批次可能多条：预成检+正式成检）
        var batchIds = raw.Select(r => r.ProductionBatchId).Distinct().ToList();
        var mrChecks = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Where(m => batchIds.Contains(m.ProductionBatchId))
            .Select(m => new { m.ProductionBatchId, m.InspectionType })
            .ToListAsync();
        var inspTypesByBatch = mrChecks
            .GroupBy(m => m.ProductionBatchId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.InspectionType)
                      .Where(t => !string.IsNullOrWhiteSpace(t))
                      .Select(t => t!.ToUpperInvariant())
                      .ToHashSet());

        var mismatchBatchNos = new List<string>();
        var noCheckBatchNos = new List<string>();
        foreach (var r in raw)
        {
            // 批次完全无成检到料（本不该存在成品检验，多为历史/批量数据）
            if (!inspTypesByBatch.TryGetValue(r.ProductionBatchId, out var types) || types.Count == 0)
            {
                noCheckBatchNos.Add(r.BatchNo ?? "");
                continue;
            }
            // 记录成检类型不在该批次到料类型集合内 → 成检类型疑问
            if (string.IsNullOrWhiteSpace(r.InspectionType)
                || !types.Contains(r.InspectionType.ToUpperInvariant()))
            {
                mismatchBatchNos.Add(r.BatchNo ?? "");
            }
        }

        return new FinalInspectionHealthSummaryDto
        {
            TotalCount = raw.Count,
            InspectionTypeMismatchBatchNos = mismatchBatchNos,
            NoMaterialCheckBatchNos = noCheckBatchNos
        };
    }

    public async Task<List<FinalInspectionDto>> GetAllListAsync()
    {
        var raw = await _context.FinalInspections
            .AsNoTracking()
            .OrderByDescending(r => r.Id)
            .Select(r => new
            {
                r.Id,
                r.InspectionItem,
                r.InspectionDate,
                r.InspectionType,
                r.BatchNo,
                r.ProductionBatchId,
                ManufacturingItem = r.ProductionBatch.ManufacturingItem,
                TagNo = r.ProductionBatch.TagNo,
                WorkOrderNo = r.ProductionBatch.WorkOrderNo,
                SalesOrderNo = r.ProductionBatch.SalesOrderNo,
                SourceUnit = r.ProductionBatch.SourceName,
                FurnaceNo = r.ProductionBatch.SourceHeatNo,
                PlantGrade = r.ProductionBatch.PlantGrade,
                Specification = r.ProductionBatch.Specification,
                ProductionType = r.ProductionBatch.ProductionType,
                Salesman = r.ProductionBatch.Salesman,
                LengthStatus = r.ProductionBatch.LengthStatus,
                DeliveryState = r.ProductionBatch.DeliveryState,
                ManufacturingStatus = r.ProductionBatch.ManufacturingStatus,
                EndCustomer = r.ProductionBatch.EndCustomer,
                ProductionCutQuantity = r.ProductionBatch.CutRequirement
                    ? r.ProductionBatch.CutQuantity
                    : r.ProductionBatch.TheoreticalOutputQty,
                ProductionWeight = r.ProductionBatch.TheoreticalOutputWeight,
                r.FixedLength,
                r.CutLengthMatchType,
                r.NonFixedLengthRange,
                r.EquipmentName,
                r.Shift,
                r.Operator,
                r.Quantity,
                r.Weight,
                r.QualifiedQuantity,
                r.QualifiedWeight,
                r.QualifiedConcessionQuantity,
                r.ConcessionRemark,
                r.DefectReworkQuantity,
                r.DefectWarehouseQuantity,
                r.DefectScrapQuantity,
                r.DefectDescription,
                r.DefectReworkWeight,
                r.DefectWarehouseWeight,
                r.DefectScrapWeight,
                r.OuterDiameterRange,
                r.WallThicknessRange,
                r.LengthAllowanceRange,
                r.Pressure,
                r.HoldTime,
                r.QualificationLevel,
                r.InspectionStandard,
                r.InspectionGrade,
                r.InstrumentModel,
                r.NdtMethod,
                r.StandardSampleSize,
                r.StandardSampleDefect,
                r.ProbeType,
                r.Couplant,
                r.CalibrationFrequency,
                r.DetectionFrequency,
                r.DetectionSensitivity,
                r.DetectionPhase,
                r.DetectionSpeed,
                r.Remark,
                r.CreatedTime,
                r.UpdatedTime
            })
            .ToListAsync();

        return raw.Select(r => new FinalInspectionDto
        {
            Id = r.Id,
            InspectionItem = r.InspectionItem,
            InspectionDate = r.InspectionDate,
            InspectionType = EnumHelper.TryParse<MES.Core.Enums.InspectionType>(r.InspectionType),
            BatchNo = r.BatchNo,
            ProductionBatchId = r.ProductionBatchId,
            ManufacturingItem = EnumHelper.TryParse<MaterialType>(r.ManufacturingItem),
            TagNo = r.TagNo,
            WorkOrderNo = r.WorkOrderNo,
            SalesOrderNo = r.SalesOrderNo,
            SourceUnit = r.SourceUnit,
            FurnaceNo = r.FurnaceNo,
            PlantGrade = r.PlantGrade,
            Specification = r.Specification,
            ProductionType = EnumHelper.TryParse<MES.Core.Enums.ProductionType>(r.ProductionType),
            Salesman = r.Salesman,
            LengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(r.LengthStatus),
            DeliveryState = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(r.DeliveryState),
            ManufacturingStatus = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(r.ManufacturingStatus),
            EndCustomer = r.EndCustomer,
            ProductionCutQuantity = r.ProductionCutQuantity,
            ProductionWeight = r.ProductionWeight,
            FixedLength = r.FixedLength,
            CutLengthMatchType = EnumHelper.TryParse<CutLengthMatchType>(r.CutLengthMatchType),
            NonFixedLengthRange = r.NonFixedLengthRange,
            EquipmentName = r.EquipmentName,
            Shift = r.Shift,
            Operator = r.Operator,
            Quantity = r.Quantity,
            Weight = r.Weight,
            QualifiedQuantity = r.QualifiedQuantity,
            QualifiedWeight = r.QualifiedWeight,
            QualifiedConcessionQuantity = r.QualifiedConcessionQuantity,
            ConcessionRemark = r.ConcessionRemark,
            DefectReworkQuantity = r.DefectReworkQuantity,
            DefectWarehouseQuantity = r.DefectWarehouseQuantity,
            DefectScrapQuantity = r.DefectScrapQuantity,
            DefectDescription = r.DefectDescription,
            DefectReworkWeight = r.DefectReworkWeight,
            DefectWarehouseWeight = r.DefectWarehouseWeight,
            DefectScrapWeight = r.DefectScrapWeight,
            OuterDiameterRange = r.OuterDiameterRange,
            WallThicknessRange = r.WallThicknessRange,
            LengthAllowanceRange = r.LengthAllowanceRange,
            Pressure = r.Pressure,
            HoldTime = r.HoldTime,
            QualificationLevel = r.QualificationLevel,
            InspectionStandard = r.InspectionStandard,
            InspectionGrade = r.InspectionGrade,
            InstrumentModel = r.InstrumentModel,
            NdtMethod = r.NdtMethod,
            StandardSampleSize = r.StandardSampleSize,
            StandardSampleDefect = r.StandardSampleDefect,
            ProbeType = r.ProbeType,
            Couplant = r.Couplant,
            CalibrationFrequency = r.CalibrationFrequency,
            DetectionFrequency = r.DetectionFrequency,
            DetectionSensitivity = r.DetectionSensitivity,
            DetectionPhase = r.DetectionPhase,
            DetectionSpeed = r.DetectionSpeed,
            Remark = r.Remark,
            CreatedTime = r.CreatedTime,
            UpdatedTime = r.UpdatedTime
        }).ToList();
    }

    public async Task<FinalInspectionDto> CreateAsync(CreateFinalInspectionRequest request)
    {
        // 如果ProductionBatchId为0，尝试根据BatchNo解析
        if (request.ProductionBatchId == 0 || request.ProductionBatchId == default)
        {
            var batch = await _context.ProductionBatches
                .AsNoTracking()
                .Where(b => b.BatchNo == request.BatchNo)
                .Select(b => new { b.Id })
                .FirstOrDefaultAsync();

            if (batch != null)
                request.ProductionBatchId = batch.Id;
        }

        // 解析批次获取工单号等信息
        var prodBatch = await _context.ProductionBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.ProductionBatchId);

        // 定尺长度校验：批次长度状态=定尺→定尺长度必填；长度状态<>定尺→定尺长度必须为空
        if (prodBatch != null)
        {
            if (prodBatch.LengthStatus == LengthStatus.Fixed.ToString() && string.IsNullOrWhiteSpace(request.FixedLength))
                throw new BusinessException("批次长度状态为'定尺'，定尺长度不能为空");
            if (prodBatch.LengthStatus != LengthStatus.Fixed.ToString() && !string.IsNullOrWhiteSpace(request.FixedLength))
                throw new BusinessException("批次长度状态非'定尺'，定尺长度必须为空");
        }

        // 成检类型：必须先存在成检到料，无到料则不允许提交成品检验
        string? inspectionType = null;
        if (prodBatch != null)
        {
            var mrChecks = await _context.MaterialReceiveChecks
                .AsNoTracking()
                .Where(m => m.ProductionBatchId == prodBatch.Id)
                .Select(m => m.InspectionType)
                .ToListAsync();
            if (mrChecks.Count == 0)
                throw new BusinessException($"批次 {request.BatchNo} 无成检到料，不能提交成品检验");

            // 前端可指定（下拉选择），否则自动判定（优先正式成检，其次预成检）
            if (request.InspectionType.HasValue)
            {
                if (!Enum.IsDefined(typeof(MES.Core.Enums.InspectionType), request.InspectionType.Value))
                    throw new BusinessException($"无效的成检类型: {request.InspectionType}");
                // 指定的成检类型必须在到料类型集合内，防止创建即制造不符（与健康通知口径一致）
                var mrCheckTypes = mrChecks
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t!.ToUpperInvariant())
                    .ToHashSet();
                if (!mrCheckTypes.Contains(request.InspectionType.Value.ToString().ToUpperInvariant()))
                    throw new BusinessException($"批次 {request.BatchNo} 成检到料不含「{request.InspectionType}」类型，不能指定");
                inspectionType = request.InspectionType.Value.ToString();
            }
            else
            {
                inspectionType = mrChecks.Contains(nameof(InspectionType.FormalInspection))
                    ? nameof(InspectionType.FormalInspection)
                    : mrChecks.Contains(nameof(InspectionType.PreInspection))
                        ? nameof(InspectionType.PreInspection)
                        : nameof(InspectionType.FormalInspection);
            }
        }

        // 成品检验定尺长度归属校验（按「订单号+主号」维度；仅正式成检要求，预成检无需）
        var fixedLengthError = await ValidateFixedLengthAsync(prodBatch?.SalesOrderNo, prodBatch?.ProductionMainNo, request.FixedLength, inspectionType);
        if (fixedLengthError != null)
            throw new BusinessException(fixedLengthError);

        // 定尺切割长度匹配标识（仅正式成检；预成检/非定尺/无定尺长度→null 显示空白）
        var cutLengthMatchType = await ComputeCutLengthMatchAsync(
            inspectionType, prodBatch?.LengthStatus, request.FixedLength,
            prodBatch?.WorkOrderNo, prodBatch?.SalesOrderNo, prodBatch?.ProductionMainNo);

        // 支数平衡：检验支数 = 合格支数 + 返整支数 + 入库支数 + 报废支数（与批量创建/更新口径一致）
        if (request.Quantity.HasValue)
        {
            var sum = (request.QualifiedQuantity ?? 0) + (request.DefectReworkQuantity ?? 0)
                + (request.DefectWarehouseQuantity ?? 0) + (request.DefectScrapQuantity ?? 0);
            if (request.Quantity.Value != sum)
                throw new BusinessException($"检验支数({request.Quantity}) ≠ 合格支数({request.QualifiedQuantity ?? 0}) + 返整({request.DefectReworkQuantity ?? 0}) + 入库({request.DefectWarehouseQuantity ?? 0}) + 报废({request.DefectScrapQuantity ?? 0}) = {sum}");
        }

        // 让步放行支数 ≤ 合格支数
        if (request.QualifiedConcessionQuantity.HasValue && request.QualifiedQuantity.HasValue
            && request.QualifiedConcessionQuantity.Value > request.QualifiedQuantity.Value)
            throw new BusinessException($"让步放行支数({request.QualifiedConcessionQuantity})不能大于合格支数({request.QualifiedQuantity})");

        // 单支重计算（自动填充重量用）：定尺=产品单支量（1位小数），非定尺=理论单支重
        // ProductUnitWeight 已由批次刷新链路对定尺批次回填（非定尺为 null），TotalWeight=0 时亦为 null
        decimal? unitWeight = null;
        if (prodBatch != null)
        {
            if (prodBatch.ProductUnitWeight.HasValue)
                unitWeight = prodBatch.ProductUnitWeight.Value;
            else if (prodBatch.TheoreticalUnitWeight.HasValue)
                unitWeight = prodBatch.TheoreticalUnitWeight.Value;
        }

        var entity = new FinalInspection
        {
            InspectionItem = request.InspectionItem,
            InspectionDate = request.InspectionDate,
            BatchNo = request.BatchNo,
            ProductionBatchId = request.ProductionBatchId,
            InspectionType = inspectionType,
            FixedLength = request.FixedLength,
            CutLengthMatchType = cutLengthMatchType,
            NonFixedLengthRange = request.NonFixedLengthRange,
            EquipmentName = request.EquipmentName,
            Shift = request.Shift ?? ShiftHelper.GetShiftByTime(),
            Operator = request.Operator,
            Quantity = request.Quantity ?? 0,
            Weight = request.Weight ?? (unitWeight.HasValue && request.Quantity.HasValue ? (int?)(unitWeight.Value * request.Quantity.Value) : 0),
            QualifiedQuantity = request.QualifiedQuantity ?? 0,
            QualifiedWeight = request.QualifiedWeight ?? (unitWeight.HasValue && request.QualifiedQuantity.HasValue ? (int?)(unitWeight.Value * request.QualifiedQuantity.Value) : 0),
            QualifiedConcessionQuantity = request.QualifiedConcessionQuantity ?? 0,
            ConcessionRemark = request.ConcessionRemark,
            DefectReworkQuantity = request.DefectReworkQuantity ?? 0,
            DefectWarehouseQuantity = request.DefectWarehouseQuantity ?? 0,
            DefectScrapQuantity = request.DefectScrapQuantity ?? 0,
            DefectReworkWeight = request.DefectReworkWeight ?? (unitWeight.HasValue && request.DefectReworkQuantity.HasValue ? (int?)(unitWeight.Value * request.DefectReworkQuantity.Value) : 0),
            DefectWarehouseWeight = request.DefectWarehouseWeight ?? (unitWeight.HasValue && request.DefectWarehouseQuantity.HasValue ? (int?)(unitWeight.Value * request.DefectWarehouseQuantity.Value) : 0),
            DefectScrapWeight = request.DefectScrapWeight ?? (unitWeight.HasValue && request.DefectScrapQuantity.HasValue ? (int?)(unitWeight.Value * request.DefectScrapQuantity.Value) : 0),
            DefectDescription = request.DefectDescription,
            OuterDiameterRange = request.OuterDiameterRange,
            WallThicknessRange = request.WallThicknessRange,
            LengthAllowanceRange = request.LengthAllowanceRange,
            Pressure = request.Pressure,
            HoldTime = request.HoldTime,
            QualificationLevel = request.QualificationLevel,
            InspectionStandard = request.InspectionStandard,
            InspectionGrade = request.InspectionGrade,
            InstrumentModel = request.InstrumentModel,
            NdtMethod = request.NdtMethod,
            StandardSampleSize = request.StandardSampleSize,
            StandardSampleDefect = request.StandardSampleDefect,
            ProbeType = request.ProbeType,
            Couplant = request.Couplant,
            CalibrationFrequency = request.CalibrationFrequency,
            DetectionFrequency = request.DetectionFrequency,
            DetectionSensitivity = request.DetectionSensitivity,
            DetectionPhase = request.DetectionPhase,
            DetectionSpeed = request.DetectionSpeed,
            Remark = request.Remark,
            DataSource = request.DataSource ?? "MANUAL"
        };

        _context.FinalInspections.Add(entity);
        await _context.SaveChangesAsync();

        await _context.Entry(entity).Reference(e => e.ProductionBatch).LoadAsync();

        await TryRefreshExecutionSummaryAsync(entity.ProductionBatch?.WorkOrderNo);
        await TryRefreshQualityProcessTrackingAsync(entity.ProductionBatchId);

        return new FinalInspectionDto
        {
            Id = entity.Id,
            InspectionItem = entity.InspectionItem,
            InspectionDate = entity.InspectionDate,
            BatchNo = entity.BatchNo,
            ProductionBatchId = entity.ProductionBatchId,
            ManufacturingItem = entity.ProductionBatch != null ? EnumHelper.TryParse<MaterialType>(entity.ProductionBatch.ManufacturingItem) : null,
            TagNo = entity.ProductionBatch?.TagNo,
            WorkOrderNo = entity.ProductionBatch?.WorkOrderNo,
            SalesOrderNo = entity.ProductionBatch?.SalesOrderNo,
            SourceUnit = entity.ProductionBatch?.SourceName,
            FurnaceNo = entity.ProductionBatch?.SourceHeatNo,
            PlantGrade = entity.ProductionBatch?.PlantGrade,
            Specification = entity.ProductionBatch?.Specification,
            ProductionType = EnumHelper.TryParse<MES.Core.Enums.ProductionType>(entity.ProductionBatch?.ProductionType),
            Salesman = entity.ProductionBatch?.Salesman,
            LengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(entity.ProductionBatch?.LengthStatus),
            DeliveryState = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(entity.ProductionBatch?.DeliveryState),
            ManufacturingStatus = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(entity.ProductionBatch?.ManufacturingStatus),
            FixedLength = entity.FixedLength,
            CutLengthMatchType = EnumHelper.TryParse<CutLengthMatchType>(entity.CutLengthMatchType),
            NonFixedLengthRange = entity.NonFixedLengthRange,
            EquipmentName = entity.EquipmentName,
            Shift = entity.Shift,
            Operator = entity.Operator,
            Quantity = entity.Quantity,
            Weight = entity.Weight,
            QualifiedQuantity = entity.QualifiedQuantity,
            QualifiedWeight = entity.QualifiedWeight,
            QualifiedConcessionQuantity = entity.QualifiedConcessionQuantity,
            ConcessionRemark = entity.ConcessionRemark,
            DefectReworkQuantity = entity.DefectReworkQuantity,
            DefectWarehouseQuantity = entity.DefectWarehouseQuantity,
            DefectScrapQuantity = entity.DefectScrapQuantity,
            DefectDescription = entity.DefectDescription,
            InspectionType = EnumHelper.TryParse<MES.Core.Enums.InspectionType>(entity.InspectionType),
            DefectReworkWeight = entity.DefectReworkWeight,
            DefectWarehouseWeight = entity.DefectWarehouseWeight,
            DefectScrapWeight = entity.DefectScrapWeight,
            OuterDiameterRange = entity.OuterDiameterRange,
            WallThicknessRange = entity.WallThicknessRange,
            LengthAllowanceRange = entity.LengthAllowanceRange,
            Pressure = entity.Pressure,
            HoldTime = entity.HoldTime,
            QualificationLevel = entity.QualificationLevel,
            InspectionStandard = entity.InspectionStandard,
            InspectionGrade = entity.InspectionGrade,
            InstrumentModel = entity.InstrumentModel,
            NdtMethod = entity.NdtMethod,
            StandardSampleSize = entity.StandardSampleSize,
            StandardSampleDefect = entity.StandardSampleDefect,
            ProbeType = entity.ProbeType,
            Couplant = entity.Couplant,
            CalibrationFrequency = entity.CalibrationFrequency,
            DetectionFrequency = entity.DetectionFrequency,
            DetectionSensitivity = entity.DetectionSensitivity,
            DetectionPhase = entity.DetectionPhase,
            DetectionSpeed = entity.DetectionSpeed,
            Remark = entity.Remark,
            DataSource = entity.DataSource,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public async Task<FinalInspectionDto> UpdateAsync(int id, UpdateFinalInspectionRequest request)
    {
        var entity = await _context.FinalInspections.FindAsync(id)
            ?? throw new BusinessException("成品检验记录不存在");

        // 合并值：前端传了就用新值，否则保留原值（与下方 ?? 赋值逻辑一致）
        var qty = request.Quantity ?? entity.Quantity;
        var qualifiedQty = request.QualifiedQuantity ?? entity.QualifiedQuantity;
        var reworkQty = request.DefectReworkQuantity ?? entity.DefectReworkQuantity;
        var warehouseQty = request.DefectWarehouseQuantity ?? entity.DefectWarehouseQuantity;
        var scrapQty = request.DefectScrapQuantity ?? entity.DefectScrapQuantity;
        var concessionQty = request.QualifiedConcessionQuantity ?? entity.QualifiedConcessionQuantity;

        // ① 支数平衡
        if (qty.HasValue)
        {
            var sum = (qualifiedQty ?? 0) + (reworkQty ?? 0) + (warehouseQty ?? 0) + (scrapQty ?? 0);
            if (qty.Value != sum)
                throw new BusinessException($"检验支数({qty}) ≠ 合格支数({qualifiedQty ?? 0}) + 返整({reworkQty ?? 0}) + 入库({warehouseQty ?? 0}) + 报废({scrapQty ?? 0}) = {sum}");
        }

        // ② 让步放行 ≤ 合格支数
        if (concessionQty.HasValue && qualifiedQty.HasValue && concessionQty.Value > qualifiedQty.Value)
            throw new BusinessException($"让步放行支数({concessionQty})不能大于合格支数({qualifiedQty})");

        // ③ 定尺长度校验（按合并后的值校验）
        var batchInfo = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => b.Id == entity.ProductionBatchId)
            .Select(b => new { b.LengthStatus, b.SalesOrderNo, b.ProductionMainNo, b.WorkOrderNo })
            .FirstOrDefaultAsync();
        var fixedLengthValue = request.FixedLength ?? entity.FixedLength;
        if (batchInfo?.LengthStatus == LengthStatus.Fixed.ToString() && string.IsNullOrWhiteSpace(fixedLengthValue))
            throw new BusinessException("批次长度状态为'定尺'，定尺长度不能为空");
        if (batchInfo?.LengthStatus != LengthStatus.Fixed.ToString() && !string.IsNullOrWhiteSpace(fixedLengthValue))
            throw new BusinessException("批次长度状态非'定尺'，定尺长度必须为空");

        // 成品检验定尺长度归属校验（按「订单号+主号」维度，用生效值校验；仅正式成检要求，预成检无需）
        var fixedLengthError = await ValidateFixedLengthAsync(batchInfo?.SalesOrderNo, batchInfo?.ProductionMainNo, fixedLengthValue, request.InspectionType?.ToString() ?? entity.InspectionType);
        if (fixedLengthError != null)
            throw new BusinessException(fixedLengthError);

        // 定尺切割长度匹配标识（用生效值重算；仅正式成检，预成检/非定尺/无定尺长度→null 显示空白）
        var cutLengthMatchType = await ComputeCutLengthMatchAsync(
            request.InspectionType?.ToString() ?? entity.InspectionType,
            batchInfo?.LengthStatus, fixedLengthValue,
            batchInfo?.WorkOrderNo, batchInfo?.SalesOrderNo, batchInfo?.ProductionMainNo);

        // 成检类型：传了则校验枚举 + 与成检到料一致性；不传保留原值（与创建口径一致，防止编辑制造不符）
        if (request.InspectionType.HasValue)
        {
            if (!Enum.IsDefined(typeof(MES.Core.Enums.InspectionType), request.InspectionType.Value))
                throw new BusinessException($"无效的成检类型: {request.InspectionType}");
            var mrCheckTypes = await _context.MaterialReceiveChecks
                .AsNoTracking()
                .Where(m => m.ProductionBatchId == entity.ProductionBatchId)
                .Select(m => m.InspectionType)
                .ToListAsync();
            var availableTypes = mrCheckTypes
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!.ToUpperInvariant())
                .ToHashSet();
            if (availableTypes.Count == 0)
                throw new BusinessException("该批次无成检到料，不能修改成检类型");
            if (!availableTypes.Contains(request.InspectionType.Value.ToString().ToUpperInvariant()))
                throw new BusinessException($"该批次成检到料不含「{request.InspectionType}」类型，不能修改");
        }

        entity.InspectionDate = request.InspectionDate;
        entity.InspectionType = request.InspectionType?.ToString() ?? entity.InspectionType;
        entity.FixedLength = request.FixedLength ?? entity.FixedLength;
        entity.CutLengthMatchType = cutLengthMatchType;
        entity.NonFixedLengthRange = request.NonFixedLengthRange ?? entity.NonFixedLengthRange;
        entity.EquipmentName = request.EquipmentName ?? entity.EquipmentName;
        entity.Shift = request.Shift ?? entity.Shift;
        entity.Operator = request.Operator ?? entity.Operator;
        entity.Quantity = request.Quantity ?? entity.Quantity;
        entity.Weight = request.Weight ?? entity.Weight;
        entity.QualifiedQuantity = request.QualifiedQuantity ?? entity.QualifiedQuantity;
        entity.QualifiedWeight = request.QualifiedWeight ?? entity.QualifiedWeight;
        entity.QualifiedConcessionQuantity = request.QualifiedConcessionQuantity ?? entity.QualifiedConcessionQuantity;
        entity.ConcessionRemark = request.ConcessionRemark ?? entity.ConcessionRemark;
        entity.DefectReworkQuantity = request.DefectReworkQuantity ?? entity.DefectReworkQuantity;
        entity.DefectWarehouseQuantity = request.DefectWarehouseQuantity ?? entity.DefectWarehouseQuantity;
        entity.DefectScrapQuantity = request.DefectScrapQuantity ?? entity.DefectScrapQuantity;
        entity.DefectReworkWeight = request.DefectReworkWeight ?? entity.DefectReworkWeight;
        entity.DefectWarehouseWeight = request.DefectWarehouseWeight ?? entity.DefectWarehouseWeight;
        entity.DefectScrapWeight = request.DefectScrapWeight ?? entity.DefectScrapWeight;
        entity.DefectDescription = request.DefectDescription ?? entity.DefectDescription;
        entity.OuterDiameterRange = request.OuterDiameterRange ?? entity.OuterDiameterRange;
        entity.WallThicknessRange = request.WallThicknessRange ?? entity.WallThicknessRange;
        entity.LengthAllowanceRange = request.LengthAllowanceRange ?? entity.LengthAllowanceRange;
        entity.Pressure = request.Pressure ?? entity.Pressure;
        entity.HoldTime = request.HoldTime ?? entity.HoldTime;
        entity.QualificationLevel = request.QualificationLevel ?? entity.QualificationLevel;
        entity.InspectionStandard = request.InspectionStandard ?? entity.InspectionStandard;
        entity.InspectionGrade = request.InspectionGrade ?? entity.InspectionGrade;
        entity.InstrumentModel = request.InstrumentModel ?? entity.InstrumentModel;
        entity.NdtMethod = request.NdtMethod ?? entity.NdtMethod;
        entity.StandardSampleSize = request.StandardSampleSize ?? entity.StandardSampleSize;
        entity.StandardSampleDefect = request.StandardSampleDefect ?? entity.StandardSampleDefect;
        entity.ProbeType = request.ProbeType ?? entity.ProbeType;
        entity.Couplant = request.Couplant ?? entity.Couplant;
        entity.CalibrationFrequency = request.CalibrationFrequency ?? entity.CalibrationFrequency;
        entity.DetectionFrequency = request.DetectionFrequency ?? entity.DetectionFrequency;
        entity.DetectionSensitivity = request.DetectionSensitivity ?? entity.DetectionSensitivity;
        entity.DetectionPhase = request.DetectionPhase ?? entity.DetectionPhase;
        entity.DetectionSpeed = request.DetectionSpeed ?? entity.DetectionSpeed;
        entity.Remark = request.Remark ?? entity.Remark;

        await _context.SaveChangesAsync();

        await _context.Entry(entity).Reference(e => e.ProductionBatch).LoadAsync();

        await TryRefreshExecutionSummaryAsync(entity.ProductionBatch?.WorkOrderNo);
        await TryRefreshQualityProcessTrackingAsync(entity.ProductionBatchId);

        return new FinalInspectionDto
        {
            Id = entity.Id,
            InspectionItem = entity.InspectionItem,
            InspectionDate = entity.InspectionDate,
            BatchNo = entity.BatchNo,
            ProductionBatchId = entity.ProductionBatchId,
            ManufacturingItem = entity.ProductionBatch != null ? EnumHelper.TryParse<MaterialType>(entity.ProductionBatch.ManufacturingItem) : null,
            TagNo = entity.ProductionBatch?.TagNo,
            WorkOrderNo = entity.ProductionBatch?.WorkOrderNo,
            SalesOrderNo = entity.ProductionBatch?.SalesOrderNo,
            SourceUnit = entity.ProductionBatch?.SourceName,
            FurnaceNo = entity.ProductionBatch?.SourceHeatNo,
            PlantGrade = entity.ProductionBatch?.PlantGrade,
            Specification = entity.ProductionBatch?.Specification,
            ProductionType = EnumHelper.TryParse<MES.Core.Enums.ProductionType>(entity.ProductionBatch?.ProductionType),
            Salesman = entity.ProductionBatch?.Salesman,
            LengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(entity.ProductionBatch?.LengthStatus),
            DeliveryState = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(entity.ProductionBatch?.DeliveryState),
            ManufacturingStatus = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(entity.ProductionBatch?.ManufacturingStatus),
            FixedLength = entity.FixedLength,
            CutLengthMatchType = EnumHelper.TryParse<CutLengthMatchType>(entity.CutLengthMatchType),
            NonFixedLengthRange = entity.NonFixedLengthRange,
            EquipmentName = entity.EquipmentName,
            Shift = entity.Shift,
            Operator = entity.Operator,
            Quantity = entity.Quantity,
            Weight = entity.Weight,
            QualifiedQuantity = entity.QualifiedQuantity,
            QualifiedWeight = entity.QualifiedWeight,
            QualifiedConcessionQuantity = entity.QualifiedConcessionQuantity,
            ConcessionRemark = entity.ConcessionRemark,
            DefectReworkQuantity = entity.DefectReworkQuantity,
            DefectWarehouseQuantity = entity.DefectWarehouseQuantity,
            DefectScrapQuantity = entity.DefectScrapQuantity,
            DefectDescription = entity.DefectDescription,
            InspectionType = EnumHelper.TryParse<MES.Core.Enums.InspectionType>(entity.InspectionType),
            DefectReworkWeight = entity.DefectReworkWeight,
            DefectWarehouseWeight = entity.DefectWarehouseWeight,
            DefectScrapWeight = entity.DefectScrapWeight,
            OuterDiameterRange = entity.OuterDiameterRange,
            WallThicknessRange = entity.WallThicknessRange,
            LengthAllowanceRange = entity.LengthAllowanceRange,
            Pressure = entity.Pressure,
            HoldTime = entity.HoldTime,
            QualificationLevel = entity.QualificationLevel,
            InspectionStandard = entity.InspectionStandard,
            InspectionGrade = entity.InspectionGrade,
            InstrumentModel = entity.InstrumentModel,
            NdtMethod = entity.NdtMethod,
            StandardSampleSize = entity.StandardSampleSize,
            StandardSampleDefect = entity.StandardSampleDefect,
            ProbeType = entity.ProbeType,
            Couplant = entity.Couplant,
            CalibrationFrequency = entity.CalibrationFrequency,
            DetectionFrequency = entity.DetectionFrequency,
            DetectionSensitivity = entity.DetectionSensitivity,
            DetectionPhase = entity.DetectionPhase,
            DetectionSpeed = entity.DetectionSpeed,
            Remark = entity.Remark,
            DataSource = entity.DataSource,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.FinalInspections.FindAsync(id)
            ?? throw new BusinessException("成品检验记录不存在");

        await _context.Entry(entity).Reference(e => e.ProductionBatch).LoadAsync();
        var workOrderNo = entity.ProductionBatch?.WorkOrderNo;
        var productionBatchId = entity.ProductionBatchId;
        _context.FinalInspections.Remove(entity);
        await _context.SaveChangesAsync();

        await TryRefreshExecutionSummaryAsync(workOrderNo);
        await TryRefreshQualityProcessTrackingAsync(productionBatchId);
    }

    public async Task<List<FinalInspectionDto>> BatchCreateAsync(List<CreateFinalInspectionRequest> requests)
    {
        if (requests.Count == 0)
            return new List<FinalInspectionDto>();

        // 预加载所有涉及的批次
        var batchNos = requests.Select(r => r.BatchNo).Distinct().ToList();
        var batchLookup = await _context.ProductionBatches
            .Where(b => batchNos.Contains(b.BatchNo))
            .ToDictionaryAsync(b => b.BatchNo);
        foreach (var bn in batchNos)
        {
            if (!batchLookup.ContainsKey(bn))
                throw new BusinessException($"批次不存在: {bn}");
        }

        // 预取：各批次所属「订单号+主号」的定尺长度集合（定尺长度校验用，避免循环内 N+1 查询）
        var fixedLengthSets = new Dictionary<string, HashSet<decimal>>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in batchLookup.Values)
        {
            if (string.IsNullOrWhiteSpace(b.SalesOrderNo) || string.IsNullOrWhiteSpace(b.ProductionMainNo)) continue;
            var key = $"{b.SalesOrderNo.Trim()}|{b.ProductionMainNo.Trim()}";
            if (fixedLengthSets.ContainsKey(key)) continue;
            fixedLengthSets[key] = await _fixedLengthWorkOrderService
                .GetLengthsByMainNoAsync(b.SalesOrderNo, b.ProductionMainNo);
        }

        // 预查询：各批次已有的成品检验记录（用于重复校验）
        var allBatchIds = batchLookup.Values.Select(b => b.Id).ToList();
        var existingRecords = await _context.FinalInspections
            .Where(f => allBatchIds.Contains(f.ProductionBatchId))
            .ToListAsync();
        var existingByBatch = existingRecords
            .GroupBy(f => f.ProductionBatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 预查询：各批次已有的成检到料（无到料不允许提交成品检验）
        var batchInspTypes = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Where(m => allBatchIds.Contains(m.ProductionBatchId))
            .Select(m => new { m.ProductionBatchId, m.InspectionType })
            .ToListAsync();

        // 各批次成检类型（已预加载 batchInspTypes，供归属校验提前使用）
        var inspTypeByBatchId = batchInspTypes
            .GroupBy(x => x.ProductionBatchId)
            .ToDictionary(g => g.Key, g =>
                g.Any(x => x.InspectionType == nameof(InspectionType.FormalInspection))
                    ? nameof(InspectionType.FormalInspection)
                    : g.Any(x => x.InspectionType == nameof(InspectionType.PreInspection))
                        ? nameof(InspectionType.PreInspection)
                        : nameof(InspectionType.FormalInspection));

        // 重复校验：日期 + 批次 + 检验项目 + 操作人 → 重复
        var errors = new List<string>();
        var seenKeys = new HashSet<string>(); // 本次提交内去重
        foreach (var (request, i) in requests.Select((r, idx) => (r, idx)))
        {
            var batch = batchLookup[request.BatchNo];
            var batchId = batch.Id;

            var operatorName = request.Operator;

            // ① 与数据库已有记录比对
            var existing = existingByBatch.GetValueOrDefault(batchId, new List<FinalInspection>());
            var dup = existing.Any(f =>
                f.InspectionDate.Date == request.InspectionDate.Date &&
                f.InspectionItem == request.InspectionItem &&
                f.Operator == operatorName);
            if (dup)
                errors.Add($"第{i + 1}行：该批次已存在相同日期/检验项目/操作人的成品检验记录，不能重复创建");

            // ② 与本次提交的前面行比对
            var key = $"{request.InspectionDate:yyyy-MM-dd}|{request.BatchNo}|{request.InspectionItem}|{operatorName}";
            if (!seenKeys.Add(key))
                errors.Add($"第{i + 1}行：与本次提交中其他行的日期/批次/检验项目/操作人重复");

            // 2) 检验支数 = 合格支数 + 返整支数 + 入库支数 + 报废支数
            if (request.Quantity.HasValue)
            {
                var sum = (request.QualifiedQuantity ?? 0)
                    + (request.DefectReworkQuantity ?? 0)
                    + (request.DefectWarehouseQuantity ?? 0)
                    + (request.DefectScrapQuantity ?? 0);
                if (request.Quantity.Value != sum)
                    errors.Add($"第{i + 1}行：检验支数({request.Quantity}) ≠ 合格支数({request.QualifiedQuantity ?? 0}) + 返整({request.DefectReworkQuantity ?? 0}) + 入库({request.DefectWarehouseQuantity ?? 0}) + 报废({request.DefectScrapQuantity ?? 0}) = {sum}");
            }

            // 3) 让步放行支数 ≤ 合格支数
            if (request.QualifiedConcessionQuantity.HasValue && request.QualifiedQuantity.HasValue
                && request.QualifiedConcessionQuantity.Value > request.QualifiedQuantity.Value)
            {
                errors.Add($"第{i + 1}行：让步放行支数({request.QualifiedConcessionQuantity})不能大于合格支数({request.QualifiedQuantity})");
            }

            // 4) 检验重量不能大于批次现有效原料重量
            if (request.Weight.HasValue && request.Weight > 0)
            {
                var maxWeight = batch.CurrentValidWeight ?? batch.InputWeight;
                if (request.Weight.Value > maxWeight)
                    errors.Add($"第{i + 1}行：检验重量({request.Weight})不能大于现有效原料重量({maxWeight})");
            }

            // 5) 定尺长度校验：批次长度状态=定尺→定尺长度必填；长度状态<>定尺→定尺长度必须为空
            if (batch.LengthStatus == LengthStatus.Fixed.ToString() && string.IsNullOrWhiteSpace(request.FixedLength))
                errors.Add($"第{i + 1}行：批次长度状态为'定尺'，定尺长度不能为空");
            if (batch.LengthStatus != LengthStatus.Fixed.ToString() && !string.IsNullOrWhiteSpace(request.FixedLength))
                errors.Add($"第{i + 1}行：批次长度状态非'定尺'，定尺长度必须为空");

            // 6) 成品检验定尺长度归属校验（按「订单号+主号」维度；仅正式成检要求，预成检无需）
            var fixedLengthErr = ValidateFixedLength(
                batch.SalesOrderNo, batch.ProductionMainNo, request.FixedLength,
                fixedLengthSets.GetValueOrDefault($"{batch.SalesOrderNo.Trim()}|{batch.ProductionMainNo.Trim()}", new HashSet<decimal>()),
                request.InspectionType.HasValue
                    ? request.InspectionType.Value.ToString()
                    : inspTypeByBatchId.GetValueOrDefault(batchId, nameof(InspectionType.FormalInspection)));
            if (fixedLengthErr != null)
                errors.Add($"第{i + 1}行：{fixedLengthErr}");

            // 7) 成检类型校验：允许不传（自动判定），传了必须是合法枚举值
            if (request.InspectionType.HasValue
                && !Enum.IsDefined(typeof(MES.Core.Enums.InspectionType), request.InspectionType.Value))
                errors.Add($"第{i + 1}行：无效的成检类型: {request.InspectionType}");

            // 8) 必须存在成检到料，无到料则不允许提交成品检验
            if (!batchInspTypes.Any(x => x.ProductionBatchId == batchId))
                errors.Add($"第{i + 1}行：批次 {request.BatchNo} 无成检到料，不能提交成品检验");
        }
        if (errors.Any())
            throw new BusinessException(string.Join("；", errors));

        // 定尺切割长度匹配标识一次预取（批量计算复用）
        var lengthMaps = await _fixedLengthWorkOrderService.GetLengthMapsAsync();

        var entities = requests.Select(r =>
        {
            var batch = batchLookup[r.BatchNo];
            return new FinalInspection
            {
                InspectionItem = r.InspectionItem,
                InspectionDate = r.InspectionDate,
                BatchNo = r.BatchNo,
                ProductionBatchId = batch.Id,
                InspectionType = r.InspectionType.HasValue
                    ? r.InspectionType.Value.ToString()
                    : inspTypeByBatchId.GetValueOrDefault(batch.Id, nameof(InspectionType.FormalInspection)),
                FixedLength = r.FixedLength,
                CutLengthMatchType = ComputeCutLengthMatch(
                    r.InspectionType.HasValue
                        ? r.InspectionType.Value.ToString()
                        : inspTypeByBatchId.GetValueOrDefault(batch.Id, nameof(InspectionType.FormalInspection)),
                    batch.LengthStatus, r.FixedLength,
                    lengthMaps.ByWorkOrderNo.GetValueOrDefault(batch.WorkOrderNo ?? "", new HashSet<decimal>()),
                    lengthMaps.ByMainKey.GetValueOrDefault($"{batch.SalesOrderNo?.Trim()}|{batch.ProductionMainNo?.Trim()}", new HashSet<decimal>())),
                NonFixedLengthRange = r.NonFixedLengthRange,
                EquipmentName = r.EquipmentName,
                Shift = r.Shift,
                Operator = r.Operator,
                Quantity = r.Quantity ?? 0,
                Weight = r.Weight ?? 0,
                QualifiedQuantity = r.QualifiedQuantity ?? 0,
                QualifiedWeight = r.QualifiedWeight ?? 0,
                QualifiedConcessionQuantity = r.QualifiedConcessionQuantity ?? 0,
                ConcessionRemark = r.ConcessionRemark,
                DefectReworkQuantity = r.DefectReworkQuantity ?? 0,
                DefectWarehouseQuantity = r.DefectWarehouseQuantity ?? 0,
                DefectScrapQuantity = r.DefectScrapQuantity ?? 0,
                DefectDescription = r.DefectDescription,
                DefectReworkWeight = r.DefectReworkWeight ?? 0,
                DefectWarehouseWeight = r.DefectWarehouseWeight ?? 0,
                DefectScrapWeight = r.DefectScrapWeight ?? 0,
                OuterDiameterRange = r.OuterDiameterRange,
                WallThicknessRange = r.WallThicknessRange,
                LengthAllowanceRange = r.LengthAllowanceRange,
                Pressure = r.Pressure,
                HoldTime = r.HoldTime,
                QualificationLevel = r.QualificationLevel,
                InspectionStandard = r.InspectionStandard,
                InspectionGrade = r.InspectionGrade,
                InstrumentModel = r.InstrumentModel,
                NdtMethod = r.NdtMethod,
                StandardSampleSize = r.StandardSampleSize,
                StandardSampleDefect = r.StandardSampleDefect,
                ProbeType = r.ProbeType,
                Couplant = r.Couplant,
                CalibrationFrequency = r.CalibrationFrequency,
                DetectionFrequency = r.DetectionFrequency,
                DetectionSensitivity = r.DetectionSensitivity,
                DetectionPhase = r.DetectionPhase,
                DetectionSpeed = r.DetectionSpeed,
                Remark = r.Remark,
                DataSource = r.DataSource ?? "MANUAL"
            };
        }).ToList();

        _context.FinalInspections.AddRange(entities);
        await _context.SaveChangesAsync();

        // 批量创建后触发增量刷新
        foreach (var e in entities)
            await TryRefreshQualityProcessTrackingAsync(e.ProductionBatchId);

        var batchIdToWorkOrder = batchLookup.ToDictionary(b => b.Value.Id, b => b.Value.WorkOrderNo);
        var workOrderNos = entities
            .Select(e => batchIdToWorkOrder.GetValueOrDefault(e.ProductionBatchId))
            .Where(w => !string.IsNullOrWhiteSpace(w) && w != WorkOrderNoSentinel.NotWorkOrder)
            .Select(w => w!)
            .Distinct()
            .ToList();
        if (workOrderNos.Count > 0)
        {
            _ = TryRefreshBatchExecutionSummaryAsync(workOrderNos);
        }

        return entities.Select(e => new FinalInspectionDto
        {
            Id = e.Id,
            InspectionItem = e.InspectionItem,
            InspectionDate = e.InspectionDate,
            BatchNo = e.BatchNo,
            ProductionBatchId = e.ProductionBatchId,
            ManufacturingItem = batchLookup.TryGetValue(e.BatchNo, out var bl) ? EnumHelper.TryParse<MaterialType>(bl.ManufacturingItem) : null,
            TagNo = batchLookup.TryGetValue(e.BatchNo, out bl) ? bl.TagNo : null,
            WorkOrderNo = batchLookup.TryGetValue(e.BatchNo, out bl) ? bl.WorkOrderNo : null,
            SalesOrderNo = batchLookup.TryGetValue(e.BatchNo, out bl) ? bl.SalesOrderNo : null,
            SourceUnit = batchLookup.TryGetValue(e.BatchNo, out bl) ? bl.SourceName : null,
            FurnaceNo = batchLookup.TryGetValue(e.BatchNo, out bl) ? bl.SourceHeatNo : null,
            PlantGrade = batchLookup.TryGetValue(e.BatchNo, out bl) ? bl.PlantGrade : null,
            Specification = batchLookup.TryGetValue(e.BatchNo, out bl) ? bl.Specification : null,
            ProductionType = batchLookup.TryGetValue(e.BatchNo, out bl) ? EnumHelper.TryParse<MES.Core.Enums.ProductionType>(bl.ProductionType) : null,
            Salesman = batchLookup.TryGetValue(e.BatchNo, out bl) ? bl.Salesman : null,
            LengthStatus = batchLookup.TryGetValue(e.BatchNo, out bl) ? EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(bl.LengthStatus) : null,
            DeliveryState = batchLookup.TryGetValue(e.BatchNo, out bl) ? EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(bl.DeliveryState) : null,
            ManufacturingStatus = batchLookup.TryGetValue(e.BatchNo, out bl) ? EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(bl.ManufacturingStatus) : null,
            EndCustomer = batchLookup.TryGetValue(e.BatchNo, out bl) ? bl.EndCustomer : null,
            ProductionCutQuantity = batchLookup.TryGetValue(e.BatchNo, out bl) ? (bl.CutRequirement ? bl.CutQuantity : bl.TheoreticalOutputQty) : null,
            ProductionWeight = batchLookup.TryGetValue(e.BatchNo, out bl) ? bl.TheoreticalOutputWeight : null,
            FixedLength = e.FixedLength,
            CutLengthMatchType = EnumHelper.TryParse<CutLengthMatchType>(e.CutLengthMatchType),
            NonFixedLengthRange = e.NonFixedLengthRange,
            EquipmentName = e.EquipmentName,
            Shift = e.Shift,
            Operator = e.Operator,
            Quantity = e.Quantity,
            Weight = e.Weight,
            QualifiedQuantity = e.QualifiedQuantity,
            QualifiedWeight = e.QualifiedWeight,
            QualifiedConcessionQuantity = e.QualifiedConcessionQuantity,
            ConcessionRemark = e.ConcessionRemark,
            DefectReworkQuantity = e.DefectReworkQuantity,
            DefectWarehouseQuantity = e.DefectWarehouseQuantity,
            DefectScrapQuantity = e.DefectScrapQuantity,
            DefectDescription = e.DefectDescription,
            InspectionType = EnumHelper.TryParse<MES.Core.Enums.InspectionType>(e.InspectionType),
            DefectReworkWeight = e.DefectReworkWeight,
            DefectWarehouseWeight = e.DefectWarehouseWeight,
            DefectScrapWeight = e.DefectScrapWeight,
            OuterDiameterRange = e.OuterDiameterRange,
            WallThicknessRange = e.WallThicknessRange,
            LengthAllowanceRange = e.LengthAllowanceRange,
            Pressure = e.Pressure,
            HoldTime = e.HoldTime,
            QualificationLevel = e.QualificationLevel,
            InspectionStandard = e.InspectionStandard,
            InspectionGrade = e.InspectionGrade,
            InstrumentModel = e.InstrumentModel,
            NdtMethod = e.NdtMethod,
            StandardSampleSize = e.StandardSampleSize,
            StandardSampleDefect = e.StandardSampleDefect,
            ProbeType = e.ProbeType,
            Couplant = e.Couplant,
            CalibrationFrequency = e.CalibrationFrequency,
            DetectionFrequency = e.DetectionFrequency,
            DetectionSensitivity = e.DetectionSensitivity,
            DetectionPhase = e.DetectionPhase,
            DetectionSpeed = e.DetectionSpeed,
            Remark = e.Remark,
            DataSource = e.DataSource,
            CreatedTime = e.CreatedTime,
            UpdatedTime = e.UpdatedTime
        }).ToList();
    }

    /// <summary>
    /// 近日成检量汇总（实时查询）：全库成品检验记录按「检验项目」统计前6日/前3日（均不含今日）/今日（实时）检验重量(kg，前端 /1000 显示 t)。
    /// 行 = 9 个检验项目（InspectionItem 枚举）+ 合计行。
    /// 统计口径：每条成品检验记录按其检验重量 Weight 计入所属检验项目；预成检/正式成检合并统计；
    /// 前3日 = [今天−3, 今天)、前6日 = [今天−6, 今天)（均不含今日，今日单独实时统计）。
    /// 整行全 0 的检验项目默认隐藏（防视觉污染），合计行始终保留。
    /// </summary>
    public async Task<List<FinalInspectionSummaryRowDto>> GetRecentSummaryAsync()
    {
        var today = DateTime.Today;
        var start = today.AddDays(-6);
        var end = today.AddDays(1);

        // ===== 1. 加载 [今天−6, 今天+1) 窗口内成品检验记录（投影仅取归行所需列） =====
        var inspections = await _context.Set<FinalInspection>()
            .Where(f => f.InspectionDate >= start && f.InspectionDate < end)
            .Select(f => new { f.InspectionDate, f.InspectionItem, f.Weight })
            .ToListAsync();

        // ===== 2. 初始化行（9 个检验项目枚举序 + 合计） =====
        var rows = Enum.GetValues<InspectionItem>()
            .Select(i => new FinalInspectionSummaryRowDto { InspectionItem = EnumHelper.GetDisplayName(i) })
            .ToList();
        var rowByItem = rows.ToDictionary(r => r.InspectionItem, StringComparer.OrdinalIgnoreCase);
        var totalRow = new FinalInspectionSummaryRowDto { InspectionItem = "合计" };
        rows.Add(totalRow);

        // 按日期窗口累加：今日=实时（date>=today）；前3日=[今天−3,今天)、前6日=[今天−6,今天)（均不含今日）
        void Accumulate(FinalInspectionSummaryRowDto row, DateTime date, decimal weight)
        {
            if (date >= today) row.TodayWeight += weight;
            if (date >= today.AddDays(-3) && date < today) row.Last3DaysWeight += weight;
            if (date >= today.AddDays(-6) && date < today) row.Last7DaysWeight += weight;
        }

        void Add(FinalInspectionSummaryRowDto row, DateTime date, decimal weight)
        {
            if (weight <= 0) return;
            Accumulate(row, date, weight);
            Accumulate(totalRow, date, weight);
        }

        // ===== 3. 归行：每条记录按检验项目计入其检验重量 =====
        foreach (var f in inspections)
        {
            var itemName = EnumHelper.GetDisplayName(f.InspectionItem);
            if (rowByItem.TryGetValue(itemName, out var row)) Add(row, f.InspectionDate, f.Weight ?? 0m);
        }

        // 整行全 0 的检验项目默认隐藏（防视觉污染），合计行始终保留
        rows.RemoveAll(r => r.InspectionItem != "合计"
            && r.Last7DaysWeight <= 0 && r.Last3DaysWeight <= 0 && r.TodayWeight <= 0);

        return rows;
    }

    /// <summary>
    /// 月度成检量汇总（实时查询）：全库成品检验记录按「检验项目」统计本年 1月~12月各月检验重量(kg，前端 /1000 显示 t)。
    /// 行 = 9 个检验项目（InspectionItem 枚举）+ 合计行，列 = MonthlyWeights 索引 0=1月…11=12月。
    /// 统计口径与 GetRecentSummaryAsync 一致（每条记录按检验重量 Weight 计入所属检验项目，预成检/正式成检合并），
    /// 仅日期窗口改为 [本年1月1日, 次年1月1日)。
    /// 整行全 0 的检验项目默认隐藏（防视觉污染），合计行始终保留。
    /// </summary>
    public async Task<List<FinalInspectionMonthlySummaryRowDto>> GetMonthlySummaryAsync()
    {
        var year = DateTime.Today.Year;
        var start = new DateTime(year, 1, 1);
        var end = start.AddYears(1);

        // ===== 1. 加载 [本年1月1日, 次年1月1日) 窗口内成品检验记录 =====
        var inspections = await _context.Set<FinalInspection>()
            .Where(f => f.InspectionDate >= start && f.InspectionDate < end)
            .Select(f => new { f.InspectionDate, f.InspectionItem, f.Weight })
            .ToListAsync();

        // ===== 2. 初始化行（9 个检验项目枚举序 + 合计），各月索引 0=1月…11=12月 =====
        var rows = Enum.GetValues<InspectionItem>()
            .Select(i => new FinalInspectionMonthlySummaryRowDto
            {
                InspectionItem = EnumHelper.GetDisplayName(i),
                MonthlyWeights = Enumerable.Repeat(0m, 12).ToList(),
            })
            .ToList();
        var rowByItem = rows.ToDictionary(r => r.InspectionItem, StringComparer.OrdinalIgnoreCase);
        var totalRow = new FinalInspectionMonthlySummaryRowDto
        {
            InspectionItem = "合计",
            MonthlyWeights = Enumerable.Repeat(0m, 12).ToList(),
        };
        rows.Add(totalRow);

        // 按月份索引累加（index = date.Month - 1）
        void Accumulate(FinalInspectionMonthlySummaryRowDto row, DateTime date, decimal weight)
            => row.MonthlyWeights[date.Month - 1] += weight;

        void Add(FinalInspectionMonthlySummaryRowDto row, DateTime date, decimal weight)
        {
            if (weight <= 0) return;
            Accumulate(row, date, weight);
            Accumulate(totalRow, date, weight);
        }

        // ===== 3. 归行：每条记录按检验项目计入其检验重量 =====
        foreach (var f in inspections)
        {
            var itemName = EnumHelper.GetDisplayName(f.InspectionItem);
            if (rowByItem.TryGetValue(itemName, out var row)) Add(row, f.InspectionDate, f.Weight ?? 0m);
        }

        // 整行全 0 的检验项目默认隐藏（防视觉污染），合计行始终保留
        rows.RemoveAll(r => r.InspectionItem != "合计" && r.MonthlyWeights.All(m => m <= 0));

        return rows;
    }

    /// <summary>
    /// 重算某批次全部成品检验记录的定尺切割长度匹配标识（CutLengthMatchType）
    /// 供批次编辑（LengthStatus/工单号等上游字段变更）后级联调用
    /// </summary>
    public async Task<int> RecomputeCutLengthMatchByBatchAsync(int batchId)
    {
        var records = await _context.FinalInspections
            .Where(r => r.ProductionBatchId == batchId)
            .ToListAsync();
        if (records.Count == 0) return 0;

        var batch = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => b.Id == batchId)
            .FirstOrDefaultAsync();
        if (batch == null) return 0;

        var maps = await _fixedLengthWorkOrderService.GetLengthMapsAsync();
        var updated = 0;
        foreach (var r in records)
        {
            var newValue = ComputeCutLengthMatch(
                r.InspectionType, batch.LengthStatus, r.FixedLength,
                maps.ByWorkOrderNo.GetValueOrDefault(batch.WorkOrderNo ?? "", new HashSet<decimal>()),
                maps.ByMainKey.GetValueOrDefault($"{batch.SalesOrderNo?.Trim()}|{batch.ProductionMainNo?.Trim()}", new HashSet<decimal>()));
            if (r.CutLengthMatchType != newValue)
            {
                r.CutLengthMatchType = newValue;
                updated++;
            }
        }
        if (updated > 0)
            await _context.SaveChangesAsync();
        return updated;
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("FinalInspectionService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var all = await _context.FinalInspections
                .AsNoTracking()
                .Select(r => new
                {
                    r.BatchNo,
                    TagNo = r.ProductionBatch.TagNo,
                    WorkOrderNo = r.ProductionBatch.WorkOrderNo,
                    SalesOrderNo = r.ProductionBatch.SalesOrderNo,
                    ProductionMainNo = r.ProductionBatch.ProductionMainNo,
                    SourceUnit = r.ProductionBatch.SourceName,
                    FurnaceNo = r.ProductionBatch.SourceHeatNo,
                    PlantGrade = r.ProductionBatch.PlantGrade,
                    Specification = r.ProductionBatch.Specification,
                    FixedLength = r.FixedLength,
                    NonFixedLengthRange = r.NonFixedLengthRange,
                    ProductionType = r.ProductionBatch.ProductionType,
                    Salesman = r.ProductionBatch.Salesman,
                    EndCustomer = r.ProductionBatch.EndCustomer,
                    r.EquipmentName,
                    r.Shift,
                    r.Operator,
                    r.InspectionType,
                    r.ConcessionRemark,
                    r.DefectDescription,
                    r.OuterDiameterRange,
                    r.WallThicknessRange,
                    r.LengthAllowanceRange,
                    r.InspectionDate,
                    r.QualificationLevel,
                    r.InspectionStandard,
                    r.InspectionGrade,
                    r.InstrumentModel,
                    r.NdtMethod,
                    r.StandardSampleSize,
                    r.StandardSampleDefect,
                    r.ProbeType,
                    r.Couplant,
                    r.CalibrationFrequency,
                    r.DetectionFrequency,
                    r.DetectionSensitivity,
                    r.DetectionPhase,
                    r.DetectionSpeed,
                    r.Remark,
                    DeliveryState = r.ProductionBatch.DeliveryState,
                    ManufacturingStatus = r.ProductionBatch.ManufacturingStatus,
                    IsDeliveryStatus = r.ProductionBatch.ManufacturingStatus == r.ProductionBatch.DeliveryState ? "是" : "否",
                    r.DataSource
                })
                .ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["BatchNo"] = all.Select(x => x.BatchNo).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
                ["TagNo"] = all.Select(x => x.TagNo ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["WorkOrderNo"] = all.Select(x => x.WorkOrderNo ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["SalesOrderNo"] = all.Select(x => x.SalesOrderNo ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["ProductionMainNo"] = all.Select(x => x.ProductionMainNo ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["SourceUnit"] = all.Select(x => x.SourceUnit ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["FurnaceNo"] = all.Select(x => x.FurnaceNo ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["PlantGrade"] = all.Select(x => x.PlantGrade ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["Specification"] = all.Select(x => x.Specification ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["FixedLength"] = all.Select(x => x.FixedLength ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["NonFixedLengthRange"] = all.Select(x => x.NonFixedLengthRange ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["ProductionType"] = all.Select(x => x.ProductionType ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["Salesman"] = all.Select(x => x.Salesman ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["EndCustomer"] = all.Select(x => x.EndCustomer ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["DeliveryState"] = all.Select(x => x.DeliveryState ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["ManufacturingStatus"] = all.Select(x => x.ManufacturingStatus ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["IsDeliveryStatus"] = all.Select(x => x.IsDeliveryStatus ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["EquipmentName"] = all.Select(x => x.EquipmentName ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["Shift"] = all.Select(x => x.Shift?.ToString() ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["Operator"] = all.Select(x => x.Operator ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["InspectionType"] = all.Select(x => x.InspectionType ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["ConcessionRemark"] = all.Select(x => x.ConcessionRemark ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["DefectDescription"] = all.Select(x => x.DefectDescription ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["OuterDiameterRange"] = all.Select(x => x.OuterDiameterRange ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["WallThicknessRange"] = all.Select(x => x.WallThicknessRange ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["LengthAllowanceRange"] = all.Select(x => x.LengthAllowanceRange ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["InspectionDate"] = all.Select(x => x.InspectionDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(v => v).ToList(),
                ["QualificationLevel"] = all.Select(x => x.QualificationLevel ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["InspectionStandard"] = all.Select(x => x.InspectionStandard ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["InspectionGrade"] = all.Select(x => x.InspectionGrade ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["InstrumentModel"] = all.Select(x => x.InstrumentModel ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["NdtMethod"] = all.Select(x => x.NdtMethod ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["StandardSampleSize"] = all.Select(x => x.StandardSampleSize ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["StandardSampleDefect"] = all.Select(x => x.StandardSampleDefect ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["ProbeType"] = all.Select(x => x.ProbeType ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["Couplant"] = all.Select(x => x.Couplant ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["CalibrationFrequency"] = all.Select(x => x.CalibrationFrequency ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["DetectionFrequency"] = all.Select(x => x.DetectionFrequency ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["DetectionSensitivity"] = all.Select(x => x.DetectionSensitivity ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["DetectionPhase"] = all.Select(x => x.DetectionPhase ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["DetectionSpeed"] = all.Select(x => x.DetectionSpeed ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["Remark"] = all.Select(x => x.Remark ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
                ["DataSource"] = all.Select(x => x.DataSource ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList()
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    public async Task<BatchLookupResultDto?> LookupBatchAsync(string batchNo)
    {
        if (string.IsNullOrWhiteSpace(batchNo))
            return null;

        var raw = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => b.BatchNo == batchNo)
            .Select(b => new
            {
                ProductionBatchId = b.Id,
                b.ManufacturingItem,
                b.TagNo,
                b.WorkOrderNo,
                b.SalesOrderNo,
                SourceUnit = b.SourceName,
                FurnaceNo = b.SourceHeatNo,
                b.PlantGrade,
                b.Specification,
                b.ProductionType,
                b.Salesman,
                b.DeliveryState,
                b.ManufacturingStatus,
                b.EndCustomer,
                ProductionCutQuantity = b.CutRequirement
                    ? b.CutQuantity
                    : b.TheoreticalOutputQty,
                ProductionWeight = b.TheoreticalOutputWeight,
                b.LengthStatus,
                b.MinLength,
                // 单支重（与 CreateAsync 自动填充逻辑一致）：定尺=产品单支量（1位小数），非定尺=理论单支重
                UnitWeight = b.ProductUnitWeight ?? b.TheoreticalUnitWeight
            })
            .FirstOrDefaultAsync();

        if (raw == null) return null;

        var batch = new BatchLookupResultDto
        {
            ProductionBatchId = raw.ProductionBatchId,
            ManufacturingItem = raw.ManufacturingItem,
            TagNo = raw.TagNo,
            WorkOrderNo = raw.WorkOrderNo,
            SalesOrderNo = raw.SalesOrderNo,
            SourceUnit = raw.SourceUnit,
            FurnaceNo = raw.FurnaceNo,
            PlantGrade = raw.PlantGrade,
            Specification = raw.Specification,
            ProductionType = EnumHelper.TryParse<MES.Core.Enums.ProductionType>(raw.ProductionType),
            Salesman = raw.Salesman,
            DeliveryState = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(raw.DeliveryState),
            ManufacturingStatus = EnumHelper.TryParse<MES.Core.Enums.DeliveryState>(raw.ManufacturingStatus),
            EndCustomer = raw.EndCustomer,
            ProductionCutQuantity = raw.ProductionCutQuantity,
            ProductionWeight = raw.ProductionWeight,
            LengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(raw.LengthStatus),
            FixedLength = raw.LengthStatus == LengthStatus.Fixed.ToString() && raw.MinLength.HasValue
                ? raw.MinLength.Value.ToString("G29")
                : null,
            UnitWeight = raw.UnitWeight
        };

        // 成检类型：优先正式成检，其次预成检；无到料则不带出（提交时由「无成检到料」校验拦截）
        var mrCheckTypes = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Where(m => m.ProductionBatchId == batch.ProductionBatchId)
            .Select(m => m.InspectionType)
            .ToListAsync();
        batch.InspectionType = mrCheckTypes.Contains(nameof(InspectionType.FormalInspection))
            ? MES.Core.Enums.InspectionType.FormalInspection
            : mrCheckTypes.Contains(nameof(InspectionType.PreInspection))
                ? MES.Core.Enums.InspectionType.PreInspection
                : (MES.Core.Enums.InspectionType?)null;

        return batch;
    }

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new QueryParams { PageIndex = 1, PageSize = int.MaxValue };
        var result = await GetAllAsync(query);
        var selected = result.Items.Where(i => ids.Contains(i.Id)).ToList();
        return FinalInspectionPrintHelper.GenerateBatchPdf(selected, columns);
    }

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns, DateTime? inspectionDateFrom = null, DateTime? inspectionDateTo = null, string? filters = null)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? null! : sortBy,
            IsDescending = isDescending,
            InspectionDateFrom = inspectionDateFrom,
            InspectionDateTo = inspectionDateTo,
            Filters = !string.IsNullOrEmpty(filters) ? System.Text.Json.JsonSerializer.Deserialize<List<FilterDescriptor>>(filters) : null
        };
        var result = await GetAllAsync(query);
        return FinalInspectionPrintHelper.GenerateBatchPdf(result.Items, columns);
    }

    private static IQueryable<FinalInspection> ApplySorting(IQueryable<FinalInspection> queryable, string sortBy, bool isDescending)
    {
        return (sortBy?.ToLower(), isDescending) switch
        {
            ("batchno", false) => queryable.OrderBy(r => r.BatchNo ?? ""),
            ("batchno", true) => queryable.OrderByDescending(r => r.BatchNo ?? ""),
            ("inspectiondate", false) => queryable.OrderBy(r => r.InspectionDate),
            ("inspectiondate", true) => queryable.OrderByDescending(r => r.InspectionDate),
            ("inspectionitem", false) => queryable.OrderBy(r => r.InspectionItem),
            ("inspectionitem", true) => queryable.OrderByDescending(r => r.InspectionItem),
            ("equipmentname", false) => queryable.OrderBy(r => r.EquipmentName ?? ""),
            ("equipmentname", true) => queryable.OrderByDescending(r => r.EquipmentName ?? ""),
            ("shift", false) => queryable.OrderBy(r => r.Shift),
            ("shift", true) => queryable.OrderByDescending(r => r.Shift),
            ("operator", false) => queryable.OrderBy(r => r.Operator ?? ""),
            ("operator", true) => queryable.OrderByDescending(r => r.Operator ?? ""),
            ("qualificationlevel", false) => queryable.OrderBy(r => r.QualificationLevel ?? ""),
            ("qualificationlevel", true) => queryable.OrderByDescending(r => r.QualificationLevel ?? ""),
            // 批量派生字段：通过 ProductionBatch 导航属性排序
            ("tagno", false) => queryable.OrderBy(r => r.ProductionBatch.TagNo ?? ""),
            ("tagno", true) => queryable.OrderByDescending(r => r.ProductionBatch.TagNo ?? ""),
            ("productiontype", false) => queryable.OrderBy(r => r.ProductionBatch.ProductionType ?? ""),
            ("productiontype", true) => queryable.OrderByDescending(r => r.ProductionBatch.ProductionType ?? ""),
            ("manufacturingitem", false) => queryable.OrderBy(r => r.ProductionBatch.ManufacturingItem ?? ""),
            ("manufacturingitem", true) => queryable.OrderByDescending(r => r.ProductionBatch.ManufacturingItem ?? ""),
            ("salesman", false) => queryable.OrderBy(r => r.ProductionBatch.Salesman ?? ""),
            ("salesman", true) => queryable.OrderByDescending(r => r.ProductionBatch.Salesman ?? ""),
            ("deliverystate", false) => queryable.OrderBy(r => r.ProductionBatch.DeliveryState ?? ""),
            ("deliverystate", true) => queryable.OrderByDescending(r => r.ProductionBatch.DeliveryState ?? ""),
            ("manufacturingstatus", false) => queryable.OrderBy(r => r.ProductionBatch.ManufacturingStatus ?? ""),
            ("manufacturingstatus", true) => queryable.OrderByDescending(r => r.ProductionBatch.ManufacturingStatus ?? ""),
            ("isdeliverystatus", false) => queryable.OrderBy(r => r.ProductionBatch.ManufacturingStatus == r.ProductionBatch.DeliveryState ? "是" : "否"),
            ("isdeliverystatus", true) => queryable.OrderByDescending(r => r.ProductionBatch.ManufacturingStatus == r.ProductionBatch.DeliveryState ? "是" : "否"),
            ("workorderno", false) => queryable.OrderBy(r => r.ProductionBatch.WorkOrderNo ?? ""),
            ("workorderno", true) => queryable.OrderByDescending(r => r.ProductionBatch.WorkOrderNo ?? ""),
            ("salesorderno", false) => queryable.OrderBy(r => r.ProductionBatch.SalesOrderNo ?? ""),
            ("salesorderno", true) => queryable.OrderByDescending(r => r.ProductionBatch.SalesOrderNo ?? ""),
            ("productionmainno", false) => queryable.OrderBy(r => r.ProductionBatch.ProductionMainNo ?? ""),
            ("productionmainno", true) => queryable.OrderByDescending(r => r.ProductionBatch.ProductionMainNo ?? ""),
            ("sourceunit", false) => queryable.OrderBy(r => r.ProductionBatch.SourceName ?? ""),
            ("sourceunit", true) => queryable.OrderByDescending(r => r.ProductionBatch.SourceName ?? ""),
            ("furnaceno", false) => queryable.OrderBy(r => r.ProductionBatch.SourceHeatNo ?? ""),
            ("furnaceno", true) => queryable.OrderByDescending(r => r.ProductionBatch.SourceHeatNo ?? ""),
            ("plantgrade", false) => queryable.OrderBy(r => r.ProductionBatch.PlantGrade ?? ""),
            ("plantgrade", true) => queryable.OrderByDescending(r => r.ProductionBatch.PlantGrade ?? ""),
            ("specification", false) => queryable.OrderBy(r => r.ProductionBatch.Specification ?? ""),
            ("specification", true) => queryable.OrderByDescending(r => r.ProductionBatch.Specification ?? ""),
            ("lengthstatus", false) => queryable.OrderBy(r => r.ProductionBatch.LengthStatus ?? ""),
            ("lengthstatus", true) => queryable.OrderByDescending(r => r.ProductionBatch.LengthStatus ?? ""),
            ("endcustomer", false) => queryable.OrderBy(r => r.ProductionBatch.EndCustomer ?? ""),
            ("endcustomer", true) => queryable.OrderByDescending(r => r.ProductionBatch.EndCustomer ?? ""),
            ("productioncutquantity", false) => queryable.OrderBy(r => r.ProductionBatch.CutRequirement ? r.ProductionBatch.CutQuantity : r.ProductionBatch.TheoreticalOutputQty),
            ("productioncutquantity", true) => queryable.OrderByDescending(r => r.ProductionBatch.CutRequirement ? r.ProductionBatch.CutQuantity : r.ProductionBatch.TheoreticalOutputQty),
            ("productionweight", false) => queryable.OrderBy(r => r.ProductionBatch.TheoreticalOutputWeight),
            ("productionweight", true) => queryable.OrderByDescending(r => r.ProductionBatch.TheoreticalOutputWeight),
            ("fixedlength", false) => queryable.OrderBy(r => r.FixedLength ?? ""),
            ("fixedlength", true) => queryable.OrderByDescending(r => r.FixedLength ?? ""),
            ("cutlengthmatchtype", false) => queryable.OrderBy(r =>
                r.CutLengthMatchType == nameof(CutLengthMatchType.FullMatch) ? 0
                : r.CutLengthMatchType == nameof(CutLengthMatchType.MainNoMatch) ? 1 : 2),
            ("cutlengthmatchtype", true) => queryable.OrderByDescending(r =>
                r.CutLengthMatchType == nameof(CutLengthMatchType.FullMatch) ? 0
                : r.CutLengthMatchType == nameof(CutLengthMatchType.MainNoMatch) ? 1 : 2),
            ("nonfixedlengthrange", false) => queryable.OrderBy(r => r.NonFixedLengthRange ?? ""),
            ("nonfixedlengthrange", true) => queryable.OrderByDescending(r => r.NonFixedLengthRange ?? ""),
            ("inspectiontype", false) => queryable.OrderBy(r => r.InspectionType ?? ""),
            ("inspectiontype", true) => queryable.OrderByDescending(r => r.InspectionType ?? ""),
            // 检验结果排序
            ("quantity", false) => queryable.OrderBy(r => r.Quantity),
            ("quantity", true) => queryable.OrderByDescending(r => r.Quantity),
            ("weight", false) => queryable.OrderBy(r => r.Weight),
            ("weight", true) => queryable.OrderByDescending(r => r.Weight),
            ("qualifiedquantity", false) => queryable.OrderBy(r => r.QualifiedQuantity),
            ("qualifiedquantity", true) => queryable.OrderByDescending(r => r.QualifiedQuantity),
            ("qualifiedweight", false) => queryable.OrderBy(r => r.QualifiedWeight),
            ("qualifiedweight", true) => queryable.OrderByDescending(r => r.QualifiedWeight),
            ("qualifiedconcessionquantity", false) => queryable.OrderBy(r => r.QualifiedConcessionQuantity),
            ("qualifiedconcessionquantity", true) => queryable.OrderByDescending(r => r.QualifiedConcessionQuantity),
            ("concessionremark", false) => queryable.OrderBy(r => r.ConcessionRemark ?? ""),
            ("concessionremark", true) => queryable.OrderByDescending(r => r.ConcessionRemark ?? ""),
            ("defectreworkquantity", false) => queryable.OrderBy(r => r.DefectReworkQuantity),
            ("defectreworkquantity", true) => queryable.OrderByDescending(r => r.DefectReworkQuantity),
            ("defectwarehousequantity", false) => queryable.OrderBy(r => r.DefectWarehouseQuantity),
            ("defectwarehousequantity", true) => queryable.OrderByDescending(r => r.DefectWarehouseQuantity),
            ("defectscrapquantity", false) => queryable.OrderBy(r => r.DefectScrapQuantity),
            ("defectscrapquantity", true) => queryable.OrderByDescending(r => r.DefectScrapQuantity),
            ("defectdescription", false) => queryable.OrderBy(r => r.DefectDescription ?? ""),
            ("defectdescription", true) => queryable.OrderByDescending(r => r.DefectDescription ?? ""),
            ("defectreworkweight", false) => queryable.OrderBy(r => r.DefectReworkWeight),
            ("defectreworkweight", true) => queryable.OrderByDescending(r => r.DefectReworkWeight),
            ("defectwarehouseweight", false) => queryable.OrderBy(r => r.DefectWarehouseWeight),
            ("defectwarehouseweight", true) => queryable.OrderByDescending(r => r.DefectWarehouseWeight),
            ("defectscrapweight", false) => queryable.OrderBy(r => r.DefectScrapWeight),
            ("defectscrapweight", true) => queryable.OrderByDescending(r => r.DefectScrapWeight),
            ("outerdiameterrange", false) => queryable.OrderBy(r => r.OuterDiameterRange ?? ""),
            ("outerdiameterrange", true) => queryable.OrderByDescending(r => r.OuterDiameterRange ?? ""),
            ("wallthicknessrange", false) => queryable.OrderBy(r => r.WallThicknessRange ?? ""),
            ("wallthicknessrange", true) => queryable.OrderByDescending(r => r.WallThicknessRange ?? ""),
            ("lengthallowancerange", false) => queryable.OrderBy(r => r.LengthAllowanceRange ?? ""),
            ("lengthallowancerange", true) => queryable.OrderByDescending(r => r.LengthAllowanceRange ?? ""),
            ("pressure", false) => queryable.OrderBy(r => r.Pressure),
            ("pressure", true) => queryable.OrderByDescending(r => r.Pressure),
            ("holdtime", false) => queryable.OrderBy(r => r.HoldTime),
            ("holdtime", true) => queryable.OrderByDescending(r => r.HoldTime),
            ("inspectionstandard", false) => queryable.OrderBy(r => r.InspectionStandard ?? ""),
            ("inspectionstandard", true) => queryable.OrderByDescending(r => r.InspectionStandard ?? ""),
            ("inspectiongrade", false) => queryable.OrderBy(r => r.InspectionGrade ?? ""),
            ("inspectiongrade", true) => queryable.OrderByDescending(r => r.InspectionGrade ?? ""),
            ("instrumentmodel", false) => queryable.OrderBy(r => r.InstrumentModel ?? ""),
            ("instrumentmodel", true) => queryable.OrderByDescending(r => r.InstrumentModel ?? ""),
            ("ndtmethod", false) => queryable.OrderBy(r => r.NdtMethod ?? ""),
            ("ndtmethod", true) => queryable.OrderByDescending(r => r.NdtMethod ?? ""),
            ("standardsamplesize", false) => queryable.OrderBy(r => r.StandardSampleSize ?? ""),
            ("standardsamplesize", true) => queryable.OrderByDescending(r => r.StandardSampleSize ?? ""),
            ("standardsampledefect", false) => queryable.OrderBy(r => r.StandardSampleDefect ?? ""),
            ("standardsampledefect", true) => queryable.OrderByDescending(r => r.StandardSampleDefect ?? ""),
            ("probetype", false) => queryable.OrderBy(r => r.ProbeType ?? ""),
            ("probetype", true) => queryable.OrderByDescending(r => r.ProbeType ?? ""),
            ("couplant", false) => queryable.OrderBy(r => r.Couplant ?? ""),
            ("couplant", true) => queryable.OrderByDescending(r => r.Couplant ?? ""),
            ("calibrationfrequency", false) => queryable.OrderBy(r => r.CalibrationFrequency ?? ""),
            ("calibrationfrequency", true) => queryable.OrderByDescending(r => r.CalibrationFrequency ?? ""),
            ("detectionfrequency", false) => queryable.OrderBy(r => r.DetectionFrequency ?? ""),
            ("detectionfrequency", true) => queryable.OrderByDescending(r => r.DetectionFrequency ?? ""),
            ("detectionsensitivity", false) => queryable.OrderBy(r => r.DetectionSensitivity ?? ""),
            ("detectionsensitivity", true) => queryable.OrderByDescending(r => r.DetectionSensitivity ?? ""),
            ("detectionphase", false) => queryable.OrderBy(r => r.DetectionPhase ?? ""),
            ("detectionphase", true) => queryable.OrderByDescending(r => r.DetectionPhase ?? ""),
            ("detectionspeed", false) => queryable.OrderBy(r => r.DetectionSpeed ?? ""),
            ("detectionspeed", true) => queryable.OrderByDescending(r => r.DetectionSpeed ?? ""),
            ("remark", false) => queryable.OrderBy(r => r.Remark ?? ""),
            ("remark", true) => queryable.OrderByDescending(r => r.Remark ?? ""),
            ("datasource", false) => queryable.OrderBy(r => r.DataSource ?? ""),
            ("datasource", true) => queryable.OrderByDescending(r => r.DataSource ?? ""),
            ("createdtime", false) => queryable.OrderBy(r => r.CreatedTime),
            ("createdtime", true) => queryable.OrderByDescending(r => r.CreatedTime),
            ("updatedtime", false) => queryable.OrderBy(r => r.UpdatedTime),
            ("updatedtime", true) => queryable.OrderByDescending(r => r.UpdatedTime),
            _ => isDescending
                ? queryable.OrderByDescending(r => r.CreatedTime)
                : queryable.OrderBy(r => r.CreatedTime)
        };
    }
}
