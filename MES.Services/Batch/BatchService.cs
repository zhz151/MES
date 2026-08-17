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
using MES.Core.Models;
using MES.Core.Constants;
using MES.Core.Helpers;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.WorkOrder;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Batch;
using WoEntity = MES.Data.Entities.WorkOrder.WorkOrder;
using MES.Services.Helpers;
using Microsoft.Extensions.Caching.Memory;
using MES.Services.Printing;

namespace MES.Services.Batch;

public class BatchService : IBatchService
{
    /// <summary>无对应工单时的工单号占位符（统一引用公共哨兵常量）</summary>
    private const string NotWorkOrder = WorkOrderNoSentinel.NotWorkOrder;

    private readonly AppDbContext _context;
    private readonly ILogger<BatchService> _logger;
    private readonly IProductionRecordService _productionRecordService;
    private readonly IFinalInspectionService _finalInspectionService;
    private readonly IConfigParameterService _configService;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;
    private readonly IMaterialPlanService _materialPlanService;
    private readonly IOperationLogService _operationLogService;
    private readonly IQualityProcessTrackingService _qualityProcessTracking;
    private readonly INotificationService _notificationService;
    private readonly ISectionNameDisplayService _sectionNameDisplay;
    private readonly IProcessDefinitionService _processDefService;
    private readonly IProcessCardColumnDefinitionService _processCardColumnDefinitionService;
    private readonly IProcessCardStyleDefinitionService _processCardStyleDefinitionService;
    private readonly IMemoryCache _cache;

    public BatchService(AppDbContext context, ILogger<BatchService> logger, IProductionRecordService productionRecordService, IFinalInspectionService finalInspectionService, IConfigParameterService configService, IWorkOrderExecutionService workOrderExecutionService, IMaterialPlanService materialPlanService, IOperationLogService operationLogService, IQualityProcessTrackingService qualityProcessTracking, INotificationService notificationService, ISectionNameDisplayService sectionNameDisplay, IProcessDefinitionService processDefService, IProcessCardColumnDefinitionService processCardColumnDefinitionService, IProcessCardStyleDefinitionService processCardStyleDefinitionService, IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _productionRecordService = productionRecordService;
        _finalInspectionService = finalInspectionService;
        _configService = configService;
        _workOrderExecutionService = workOrderExecutionService;
        _materialPlanService = materialPlanService;
        _operationLogService = operationLogService;
        _qualityProcessTracking = qualityProcessTracking;
        _notificationService = notificationService;
        _sectionNameDisplay = sectionNameDisplay;
        _processDefService = processDefService;
        _processCardColumnDefinitionService = processCardColumnDefinitionService;
        _processCardStyleDefinitionService = processCardStyleDefinitionService;
        _cache = cache;
    }

    private async Task TryRefreshExecutionSummaryAsync(string? workOrderNo)
    {
        if (string.IsNullOrWhiteSpace(workOrderNo) || workOrderNo == NotWorkOrder) return;
        try
        {
            await _workOrderExecutionService.RefreshByWorkOrderNosAsync(new List<string> { workOrderNo });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工单执行状况刷新失败（不影响主流程）: WorkOrderNo={WorkOrderNo}", workOrderNo);
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

    private readonly Dictionary<string, Dictionary<string, decimal>> _configMaps = new();

    private async Task<decimal> GetConfigAsync(string category, string key, decimal defaultValue)
    {
        if (!_configMaps.TryGetValue(category, out var map))
        {
            map = await _configService.GetConfigMapAsync(category);
            _configMaps[category] = map;
        }
        return map.GetValueOrDefault(key, defaultValue);
    }

    public async Task<PagedResult<ProductionBatchListDto>> GetPagedAsync(BatchQueryParams query)
    {
        var queryable = _context.ProductionBatches
            .AsNoTracking()
            .AsQueryable();

        // 关键字搜索
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(b =>
                b.BatchNo.Contains(kw) ||
                b.WorkOrderNo.Contains(kw) ||
                b.SalesOrderNo.Contains(kw) ||
                b.ProductionMainNo.Contains(kw) ||
                (b.ProductionSubNo != null && b.ProductionSubNo.Contains(kw)) ||
                (b.TagNo != null && b.TagNo.Contains(kw)) ||
                b.CreatedBy.Contains(kw) ||
                (b.CurrentGroupName != null && b.CurrentGroupName.Contains(kw)) ||
                (b.CurrentSectionName != null && b.CurrentSectionName.Contains(kw)) ||
                (b.CurrentEquipmentName != null && b.CurrentEquipmentName.Contains(kw)) ||
                (b.CurrentOutsource != null && b.CurrentOutsource.Contains(kw)) ||
                (b.CurrentSpec != null && b.CurrentSpec.Contains(kw)) ||
                (b.NextSectionName != null && b.NextSectionName.Contains(kw)) ||
                (b.CorrespondingSpec != null && b.CorrespondingSpec.Contains(kw)) ||
                (b.NextProcess != null && b.NextProcess.Contains(kw)) ||
                b.ManufacturingItem.Contains(kw) ||
                (b.ProductionType != null && b.ProductionType.Contains(kw)) ||
                b.Salesman.Contains(kw) ||
                (b.EndCustomer != null && b.EndCustomer.Contains(kw)) ||
                b.MaterialName.Contains(kw) ||
                b.SettlementMethod.Contains(kw) ||
                b.StandardCode.Contains(kw) ||
                b.DeliveryState.Contains(kw) ||
                b.PlantGrade.Contains(kw) ||
                b.Specification.Contains(kw) ||
                b.LengthStatus.Contains(kw) ||
                b.TechnicalRequirements.Contains(kw) ||
                (b.ItemDetails != null && b.ItemDetails.Contains(kw)) ||
                (b.Remark != null && b.Remark.Contains(kw)) ||
                (b.QualityRemark != null && b.QualityRemark.Contains(kw)) ||
                (b.SourceHeatNo != null && b.SourceHeatNo.Contains(kw)) ||
                (b.SourceName != null && b.SourceName.Contains(kw)) ||
                (b.SourceBatchNo != null && b.SourceBatchNo.Contains(kw)) ||
                (b.SourceSpecification != null && b.SourceSpecification.Contains(kw)) ||
                (b.SourceMaterialType != null && b.SourceMaterialType.Contains(kw)) ||
                (b.SourceLengthStatus != null && b.SourceLengthStatus.Contains(kw)) ||
                (b.SolutionParams != null && b.SolutionParams.Contains(kw)) ||
                (b.UpdatedBy != null && b.UpdatedBy.Contains(kw)) ||
                (b.SourcePlantGrade != null && b.SourcePlantGrade.Contains(kw)) ||
                (b.SourceProductionNo != null && b.SourceProductionNo.Contains(kw)) ||
                (b.ManufacturingStatus != null && b.ManufacturingStatus.Contains(kw)) ||
                (b.SourceRemark != null && b.SourceRemark.Contains(kw)) ||
                (b.OrderItemIds != null && b.OrderItemIds.Contains(kw)));
        }

        // 筛选条件
        if (!string.IsNullOrEmpty(query.WorkOrderNo))
            queryable = queryable.Where(b => b.WorkOrderNo.Contains(query.WorkOrderNo));

        if (!string.IsNullOrEmpty(query.Status) && Enum.TryParse<BatchStatus>(query.Status, out var batchStatus))
            queryable = queryable.Where(b => b.Status == batchStatus);

        if (!string.IsNullOrEmpty(query.TagNo))
            queryable = queryable.Where(b => b.TagNo != null && b.TagNo.Contains(query.TagNo));

        if (!string.IsNullOrEmpty(query.BatchNo))
            queryable = queryable.Where(b => b.BatchNo.Contains(query.BatchNo));
        if (!string.IsNullOrEmpty(query.SalesOrderNo))
            queryable = queryable.Where(b => b.SalesOrderNo.Contains(query.SalesOrderNo));
        if (!string.IsNullOrEmpty(query.ProductionMainNo))
            queryable = queryable.Where(b => b.ProductionMainNo.Contains(query.ProductionMainNo));
        if (!string.IsNullOrEmpty(query.ProductionSubNo))
            queryable = queryable.Where(b => b.ProductionSubNo != null && b.ProductionSubNo.Contains(query.ProductionSubNo));

        // 处理 Salesman/EndCustomer 筛选（直接从 ProductionBatch 快照字段筛选，
        // 无需通过 CustomerProfile，SalesOrder 已有独立快照字段）
        if (query.Filters != null)
        {
            var salesmanFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("Salesman", StringComparison.OrdinalIgnoreCase));
            if (salesmanFilter != null && salesmanFilter.Values?.Count > 0)
            {
                queryable = queryable.Where(b => salesmanFilter.Values.Contains(b.Salesman));
                query.Filters.Remove(salesmanFilter);
            }

            var endCustomerFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("EndCustomer", StringComparison.OrdinalIgnoreCase));
            if (endCustomerFilter != null && endCustomerFilter.Values?.Count > 0)
            {
                var endCustomerValues = endCustomerFilter.Values;
                queryable = queryable.Where(b => b.EndCustomer != null && endCustomerValues.Contains(b.EndCustomer));
                query.Filters.Remove(endCustomerFilter);
            }
        }

        // 创建时间（登记日期）范围筛选
        if (query.StartDateFrom.HasValue)
            queryable = queryable.Where(b => b.CreatedTime >= query.StartDateFrom.Value);
        if (query.StartDateTo.HasValue)
            queryable = queryable.Where(b => b.CreatedTime <= query.StartDateTo.Value);

        // 通用筛选
        queryable = queryable.ApplyFilters(query.Filters);

        // 排序
        queryable = queryable.ApplySort(query.SortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        var mappedItems = items.Select(b => new ProductionBatchListDto
        {
            Id = b.Id,
            BatchNo = b.BatchNo,
            TagNo = b.TagNo,
            CreatedTime = b.CreatedTime,
            UpdatedTime = b.UpdatedTime,
            WorkOrderNo = b.WorkOrderNo,
            SalesOrderNo = b.SalesOrderNo,
            ProductionMainNo = b.ProductionMainNo,
            ProductionSubNo = b.ProductionSubNo,
            ProductionType = EnumHelper.TryParse<MES.Core.Enums.ProductionType>(b.ProductionType),
            ManufacturingItem = !string.IsNullOrEmpty(b.ManufacturingItem) && Enum.TryParse<MaterialType>(b.ManufacturingItem, out var r221) ? r221 : default,
            Status = b.Status,
            IsForceCompleted = b.IsForceCompleted,
            ProductionRatio = b.ProductionRatio,
            CurrentExecDate = b.CurrentExecDate,
            CurrentGroupName = b.CurrentGroupName,
            CurrentSectionName = b.CurrentSectionName,
            CurrentEquipmentName = b.CurrentEquipmentName,
            CurrentOutsource = b.CurrentOutsource,
            CurrentSpec = b.CurrentSpec,
            NextSectionName = b.NextSectionName,
            CorrespondingSpec = b.CorrespondingSpec,
            NextProcess = b.NextProcess,
            CurrentSectionCompleted = b.CurrentSectionCompleted,
            RemainingWorkDays = b.RemainingWorkDays,
            TotalWorkDays = b.TotalWorkDays,
            CurrentValidQty = b.CurrentValidQty,
            CurrentValidWeight = b.CurrentValidWeight,
            CreatedBy = b.CreatedBy,
            UpdatedBy = b.UpdatedBy,
            SignDate = b.SignDate,
            Salesman = b.Salesman,
            EndCustomer = b.EndCustomer,
            DeliveryDate = b.DeliveryDate,
            DelayPenalty = b.DelayPenalty,
            MaterialName = b.MaterialName,
            SettlementMethod = string.IsNullOrEmpty(b.SettlementMethod) ? default : Enum.Parse<SettlementMethod>(b.SettlementMethod),
            StandardCode = b.StandardCode,
            DeliveryState = string.IsNullOrEmpty(b.DeliveryState) ? default : Enum.Parse<DeliveryState>(b.DeliveryState),
            ManufacturingStatus = !string.IsNullOrEmpty(b.ManufacturingStatus) && Enum.TryParse<DeliveryState>(b.ManufacturingStatus, out var ms) ? ms : null,
            PlantGrade = b.PlantGrade,
            Specification = b.Specification,
            LengthStatus = string.IsNullOrEmpty(b.LengthStatus) ? default : Enum.Parse<LengthStatus>(b.LengthStatus),
            TotalQuantity = b.TotalQuantity,
            TotalMeters = b.TotalMeters,
            TotalWeight = b.TotalWeight,
            ProductUnitWeight = b.ProductUnitWeight,
            TechnicalRequirements = b.TechnicalRequirements,
            Remark = b.Remark,
            SourceHeatNo = b.SourceHeatNo,
            TotalItemCount = b.TotalItemCount,
            SourceSpecification = b.SourceSpecification,
            InputQuantity = b.InputQuantity,
            InputWeight = b.InputWeight,
            SolutionParams = b.SolutionParams,
            QualityRemark = b.QualityRemark,
            SourceMaterialType = !string.IsNullOrEmpty(b.SourceMaterialType) ? EnumHelper.TryParse<MaterialType>(b.SourceMaterialType) : null,
            SourceName = b.SourceName,
            HasInputChange = b.HasInputChange,
            ProcessInspectionQualifiedQty = b.ProcessInspectionQualifiedQty,
            ProcessInspectionQualifiedWeight = b.ProcessInspectionQualifiedWeight,
            ProcessInspectionTheoreticalQty = b.ProcessInspectionTheoreticalQty,
            ProcessInspectionNeedAdjust = b.ProcessInspectionNeedAdjust,
            ProcessInspectionReworkWeight = b.ProcessInspectionReworkWeight ?? 0,
            ProcessInspectionScrapWeight = b.ProcessInspectionScrapWeight ?? 0,
            InspectionStage = b.InspectionStage,
            CutRequirement = b.CutRequirement,
            CutExecution = b.CutExecution,
            CutQuantity = b.CutQuantity,
            CutDoubt = b.CutDoubt,
            OuterDiameterNegative = b.OuterDiameterNegative,
            OuterDiameterPositive = b.OuterDiameterPositive,
            WallThicknessNegative = b.WallThicknessNegative,
            WallThicknessPositive = b.WallThicknessPositive,
            MinLength = b.MinLength,
            MaxLength = b.MaxLength,
            SourceBatchNo = b.SourceBatchNo,
            SourcePlantGrade = b.SourcePlantGrade,
            SourceUnitWeight = b.SourceUnitWeight,
            InputType = b.InputType,
            SourceLengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(b.SourceLengthStatus),
            SourceProductionNo = b.SourceProductionNo,
            TheoreticalOutputQty = b.TheoreticalOutputQty,
            TheoreticalOutputWeight = b.TheoreticalOutputWeight,
            TheoreticalUnitWeight = b.TheoreticalUnitWeight
        }).ToList();

        return new PagedResult<ProductionBatchListDto>
        {
            Items = mappedItems,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<ProductionBatchListDto>> GetAllBatchListAsync()
    {
        var queryable = _context.ProductionBatches
            .AsNoTracking()
            .AsQueryable();

        var items = await queryable
            .ToListAsync();

        var mappedItems = items.Select(b => new ProductionBatchListDto
        {
            Id = b.Id,
            BatchNo = b.BatchNo,
            TagNo = b.TagNo,
            CreatedTime = b.CreatedTime,
            UpdatedTime = b.UpdatedTime,
            WorkOrderNo = b.WorkOrderNo,
            SalesOrderNo = b.SalesOrderNo,
            ProductionMainNo = b.ProductionMainNo,
            ProductionSubNo = b.ProductionSubNo,
            ProductionType = EnumHelper.TryParse<MES.Core.Enums.ProductionType>(b.ProductionType),
            ManufacturingItem = !string.IsNullOrEmpty(b.ManufacturingItem) && Enum.TryParse<MaterialType>(b.ManufacturingItem, out var r299) ? r299 : default,
            Status = b.Status,
            IsForceCompleted = b.IsForceCompleted,
            ProductionRatio = b.ProductionRatio,
            CurrentExecDate = b.CurrentExecDate,
            CurrentGroupName = b.CurrentGroupName,
            CurrentSectionName = b.CurrentSectionName,
            CurrentEquipmentName = b.CurrentEquipmentName,
            CurrentOutsource = b.CurrentOutsource,
            CurrentSpec = b.CurrentSpec,
            NextSectionName = b.NextSectionName,
            CorrespondingSpec = b.CorrespondingSpec,
            NextProcess = b.NextProcess,
            CurrentSectionCompleted = b.CurrentSectionCompleted,
            RemainingWorkDays = b.RemainingWorkDays,
            TotalWorkDays = b.TotalWorkDays,
            CurrentValidQty = b.CurrentValidQty,
            CurrentValidWeight = b.CurrentValidWeight,
            CreatedBy = b.CreatedBy,
            UpdatedBy = b.UpdatedBy,
            SignDate = b.SignDate,
            Salesman = b.Salesman,
            EndCustomer = b.EndCustomer,
            DeliveryDate = b.DeliveryDate,
            DelayPenalty = b.DelayPenalty,
            MaterialName = b.MaterialName,
            SettlementMethod = string.IsNullOrEmpty(b.SettlementMethod) ? default : Enum.Parse<SettlementMethod>(b.SettlementMethod),
            StandardCode = b.StandardCode,
            DeliveryState = string.IsNullOrEmpty(b.DeliveryState) ? default : Enum.Parse<DeliveryState>(b.DeliveryState),
            ManufacturingStatus = !string.IsNullOrEmpty(b.ManufacturingStatus) && Enum.TryParse<DeliveryState>(b.ManufacturingStatus, out var ms) ? ms : null,
            PlantGrade = b.PlantGrade,
            Specification = b.Specification,
            LengthStatus = string.IsNullOrEmpty(b.LengthStatus) ? default : Enum.Parse<LengthStatus>(b.LengthStatus),
            TotalQuantity = b.TotalQuantity,
            TotalMeters = b.TotalMeters,
            TotalWeight = b.TotalWeight,
            ProductUnitWeight = b.ProductUnitWeight,
            TechnicalRequirements = b.TechnicalRequirements,
            Remark = b.Remark,
            SourceHeatNo = b.SourceHeatNo,
            TotalItemCount = b.TotalItemCount,
            SourceSpecification = b.SourceSpecification,
            InputQuantity = b.InputQuantity,
            InputWeight = b.InputWeight,
            SolutionParams = b.SolutionParams,
            QualityRemark = b.QualityRemark,
            SourceMaterialType = !string.IsNullOrEmpty(b.SourceMaterialType) ? EnumHelper.TryParse<MaterialType>(b.SourceMaterialType) : null,
            SourceName = b.SourceName,
            HasInputChange = b.HasInputChange,
            ProcessInspectionQualifiedQty = b.ProcessInspectionQualifiedQty,
            ProcessInspectionQualifiedWeight = b.ProcessInspectionQualifiedWeight,
            ProcessInspectionTheoreticalQty = b.ProcessInspectionTheoreticalQty,
            ProcessInspectionNeedAdjust = b.ProcessInspectionNeedAdjust,
            ProcessInspectionReworkWeight = b.ProcessInspectionReworkWeight ?? 0,
            ProcessInspectionScrapWeight = b.ProcessInspectionScrapWeight ?? 0,
            InspectionStage = b.InspectionStage,
            CutRequirement = b.CutRequirement,
            CutExecution = b.CutExecution,
            CutQuantity = b.CutQuantity,
            CutDoubt = b.CutDoubt,
            OuterDiameterNegative = b.OuterDiameterNegative,
            OuterDiameterPositive = b.OuterDiameterPositive,
            WallThicknessNegative = b.WallThicknessNegative,
            WallThicknessPositive = b.WallThicknessPositive,
            MinLength = b.MinLength,
            MaxLength = b.MaxLength,
            SourceBatchNo = b.SourceBatchNo,
            SourcePlantGrade = b.SourcePlantGrade,
            SourceUnitWeight = b.SourceUnitWeight,
            InputType = b.InputType,
            SourceLengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(b.SourceLengthStatus),
            SourceProductionNo = b.SourceProductionNo,
            TheoreticalOutputQty = b.TheoreticalOutputQty,
            TheoreticalOutputWeight = b.TheoreticalOutputWeight,
            TheoreticalUnitWeight = b.TheoreticalUnitWeight
        }).ToList();

        return mappedItems;
    }

    public async Task<ProductionBatchDetailDto> GetByIdAsync(int id)
    {
        var entity = await _context.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProcessGroups.OrderBy(pg => pg.SequenceNumber))
            .Include(b => b.ProductionBatchInventories)
                .ThenInclude(pbi => pbi.InventoryBatch)
                .ThenInclude(ib => ib.Warehouse)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (Id={id})");

        var dto = ToDetailDto(entity);

        return dto;
    }

    public async Task<ProductionBatchDetailDto> GetByBatchNoAsync(string batchNo)
    {
        var entity = await _context.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProcessGroups.OrderBy(pg => pg.SequenceNumber))
            .Include(b => b.ProductionBatchInventories)
                .ThenInclude(pbi => pbi.InventoryBatch)
                .ThenInclude(ib => ib.Warehouse)
            .FirstOrDefaultAsync(b => b.BatchNo == batchNo);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (BatchNo={batchNo})");

        var dto = ToDetailDto(entity);

        return dto;
    }

    public async Task<AdjacentBatchDto> GetAdjacentBatchAsync(int currentId)
    {
        // 获取所有批次ID（按创建时间排序），找到当前批次的相邻记录
        var allIds = await _context.ProductionBatches
            .AsNoTracking()
            .OrderBy(b => b.CreatedTime)
            .ThenBy(b => b.Id)
            .Select(b => new { b.Id, b.BatchNo })
            .ToListAsync();

        var currentIndex = allIds.FindIndex(x => x.Id == currentId);
        if (currentIndex < 0)
            return new AdjacentBatchDto();

        return new AdjacentBatchDto
        {
            PrevId = currentIndex > 0 ? allIds[currentIndex - 1].Id : null,
            PrevBatchNo = currentIndex > 0 ? allIds[currentIndex - 1].BatchNo : null,
            NextId = currentIndex < allIds.Count - 1 ? allIds[currentIndex + 1].Id : null,
            NextBatchNo = currentIndex < allIds.Count - 1 ? allIds[currentIndex + 1].BatchNo : null
        };
    }

    public async Task<ProductionBatchListDto> CreateAsync(CreateProductionBatchRequest request)
    {
        // 生成生产编号
        var batchNo = await GenerateBatchNoAsync();

        // 工单号验证：非空；NotWorkOrder跳过；其他值必须在工单表中存在
        if (string.IsNullOrWhiteSpace(request.WorkOrderNo))
            throw new BusinessException("工单号不能为空（无对应工单请填写「非工单」）");

        WoEntity? workOrder = null;
        if (request.WorkOrderNo != NotWorkOrder)
        {
            workOrder = await _context.WorkOrders
                .FirstOrDefaultAsync(w => w.WorkOrderNo == request.WorkOrderNo);

            if (workOrder == null)
                throw new BusinessException($"工单不存在 (WorkOrderNo={request.WorkOrderNo})");
        }

        // ========== 统一必填（两路径共用） ==========

        // 生产类型 / 制造物品
        if (request.ProductionType == null)
            throw new BusinessException("生产类型不能为空");
        if (request.ManufacturingItem == null)
            throw new BusinessException("制造物品不能为空");

        // 工单规格必填
        if (string.IsNullOrWhiteSpace(request.PlantGrade))
            throw new BusinessException("工厂牌号不能为空");
        if (string.IsNullOrWhiteSpace(request.Specification))
            throw new BusinessException("规格不能为空");
        if (request.DeliveryState == null)
            throw new BusinessException("交货状态不能为空");
        if (request.ManufacturingStatus == null)
            throw new BusinessException("制造状态不能为空");
        if (request.MaterialName == null)
            throw new BusinessException("物料名称不能为空");
        if (request.LengthStatus == null)
            throw new BusinessException("长度状态不能为空");
        // 非工单不校验总重量/总支数
        if (request.WorkOrderNo != NotWorkOrder)
        {
            if (request.TotalWeight == null || request.TotalWeight <= 0)
                throw new BusinessException("总重量必须大于0");
            if (request.LengthStatus == LengthStatus.Fixed && (request.TotalQuantity == null || request.TotalQuantity <= 0))
                throw new BusinessException("总支数（定尺时必须大于0）");
        }
        // 制成倍数必须大于0
        if (request.ProductionRatio <= 0)
            throw new BusinessException("制成倍数必须大于0");

        // ========== 合并投料来源处理 ==========
        // 如果有 SourceItems，用其汇总值覆盖单字段来源
        if (request.SourceItems != null && request.SourceItems.Count > 0)
        {
            request.InputQuantity = request.SourceItems.Sum(s => s.InputQuantity);
            request.InputWeight = request.SourceItems.Sum(s => s.InputWeight);
            // SourceBatchNo 沿用第一个来源批次号（向后兼容）
            if (request.SourceBatchNo == null && request.SourceItems.Count > 0)
                request.SourceBatchNo = null; // 将在保存后从关联表回填
        }

        // 仓库来源必填
        if (string.IsNullOrWhiteSpace(request.SourcePlantGrade))
            throw new BusinessException("仓库工厂牌号不能为空");
        if (string.IsNullOrWhiteSpace(request.SourceSpecification))
            throw new BusinessException("仓库规格不能为空");
        if (request.InputWeight == null || request.InputWeight <= 0)
            throw new BusinessException("领料重量必须大于0");
        if (request.InputQuantity == null || request.InputQuantity <= 0)
            throw new BusinessException("领料支数必须大于0");

        // 工厂牌号验证（高代低）
        if (!GradeSubstitutes.IsSubstitutable(request.PlantGrade, request.SourcePlantGrade))
            throw new BusinessException("仓库工厂牌号与工单工厂牌号不一致，且不可替代（仅允许高代低）");

        // ========== 枚举字符串有效性验证（DTO已使用枚举类型，类型安全由编译器保证） ==========
        if (request.ManufacturingItem == null)
            throw new BusinessException($"无效的制造物品: {request.ManufacturingItem}");

        // ========== 有工单路径额外验证 ==========
        if (request.WorkOrderNo != NotWorkOrder)
        {
            if (request.SettlementMethod == null)
                throw new BusinessException("结算方式不能为空");
            if (string.IsNullOrWhiteSpace(request.StandardCode))
                throw new BusinessException("产品标准编码不能为空");
            if (request.TechnicalRequirements == null)
                throw new BusinessException("技术要求不能为空");
        }

        // ========== 制造状态与制造物品业务规则 ==========
        if (request.ManufacturingItem == MaterialType.SpecialDeliveryStatus)
        {
            if (request.ManufacturingStatus == request.DeliveryState)
                throw new BusinessException("制造物品为「订成-非交付态」时，制造状态不能等于交货状态");
        }
        else if (request.ManufacturingStatus != request.DeliveryState)
        {
            throw new BusinessException("制造物品不为「订成-非交付态」时，制造状态必须等于交货状态");
        }

        var entity = new ProductionBatch
        {
            BatchNo = batchNo,
            Status = BatchStatus.None,
            TagNo = request.TagNo,
            ProductionType = request.ProductionType?.ToString() ?? "",
            ManufacturingItem = request.ManufacturingItem?.ToString() ?? "",
            ProductionRatio = CalculateProductionRatio(request, workOrder),
            IsForceCompleted = false,
            QualityRemark = request.QualityRemark,
            SolutionParams = request.SolutionParams,
            Remark = request.Remark,

            // 仓库来源
            SourceBatchNo = request.SourceBatchNo,
            SourceMaterialType = request.SourceMaterialType?.ToString(),
            SourceName = request.SourceName,
            SourceHeatNo = request.SourceHeatNo,
            SourcePlantGrade = request.SourcePlantGrade,
            SourceSpecification = request.SourceSpecification,
            SourceLengthStatus = request.SourceLengthStatus?.ToString(),
            SourceUnitWeight = request.SourceUnitWeight,
            InputQuantity = request.InputQuantity,
            InputWeight = request.InputWeight,
            InputType = request.InputType,
            SourceRemark = request.SourceRemark,
            SourceProductionNo = request.SourceProductionNo,
            CurrentValidQty = request.CurrentValidQty,
            CurrentValidWeight = request.CurrentValidWeight,

            // 从工单复制冗余字段（29个，优先使用前端传入值）
            WorkOrderNo = request.WorkOrderNo ?? workOrder?.WorkOrderNo ?? "",
            SalesOrderNo = request.SalesOrderNo ?? workOrder?.SalesOrderNo ?? "",
            ProductionMainNo = request.ProductionMainNo ?? workOrder?.ProductionMainNo ?? "",
            ProductionSubNo = request.ProductionSubNo ?? workOrder?.ProductionSubNo,
            OrderItemIds = request.OrderItemIds ?? workOrder?.OrderItemIds ?? "",
            SignDate = request.SignDate ?? workOrder?.SignDate ?? default,
            Salesman = request.Salesman ?? workOrder?.Salesman ?? "",
            EndCustomer = request.EndCustomer ?? workOrder?.EndCustomer,
            DeliveryDate = request.DeliveryDate ?? workOrder?.DeliveryDate ?? default,
            DelayPenalty = request.DelayPenalty ?? workOrder?.DelayPenalty ?? default,
            MaterialName = request.MaterialName?.ToString() ?? workOrder?.PipeManufacturingType.ToString() ?? "",
            SettlementMethod = request.SettlementMethod?.ToString() ?? workOrder?.SettlementMethod.ToString() ?? "",
            StandardCode = request.StandardCode ?? workOrder?.StandardCode ?? "",
            DeliveryState = request.DeliveryState?.ToString() ?? workOrder?.DeliveryState.ToString() ?? "",
            ManufacturingStatus = request.ManufacturingStatus?.ToString(),
            PlantGrade = request.PlantGrade ?? workOrder?.PlantGrade ?? "",
            Specification = request.Specification ?? workOrder?.Specification ?? "",
            OuterDiameterNegative = request.OuterDiameterNegative ?? workOrder?.OuterDiameterNegative ?? default,
            OuterDiameterPositive = request.OuterDiameterPositive ?? workOrder?.OuterDiameterPositive ?? default,
            WallThicknessNegative = request.WallThicknessNegative ?? workOrder?.WallThicknessNegative ?? default,
            WallThicknessPositive = request.WallThicknessPositive ?? workOrder?.WallThicknessPositive ?? default,
            LengthStatus = request.LengthStatus?.ToString() ?? workOrder?.LengthStatus.ToString() ?? "",
            MinLength = request.MinLength ?? workOrder?.MinLength,
            MaxLength = request.MaxLength ?? workOrder?.MaxLength,
            TotalQuantity = request.TotalQuantity ?? workOrder?.TotalQuantity ?? default,
            TotalMeters = request.TotalMeters ?? workOrder?.TotalMeters ?? default,
            TotalWeight = request.TotalWeight ?? workOrder?.TotalWeight ?? default,
            TotalItemCount = request.TotalItemCount ?? workOrder?.TotalItemCount ?? default,
            ItemDetails = request.ItemDetails ?? workOrder?.ItemDetails,
            TechnicalRequirements = request.TechnicalRequirements?.ToString() ?? workOrder?.TechnicalRequirements.ToString() ?? ""
        };

        // 工序组数值验证
        if (request.ProcessGroups != null && request.ProcessGroups.Count > 0)
            ValidateProcessGroupValues(request.ProcessGroups);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.ProductionBatches.Add(entity);
            await _context.SaveChangesAsync();

            // 保存工序组
            if (request.ProcessGroups != null && request.ProcessGroups.Count > 0)
            {
                for (int i = 0; i < request.ProcessGroups!.Count; i++)
                {
                    var pg = request.ProcessGroups[i];
                    var pgEntity = new ProcessGroup
                    {
                        ProductionBatchId = entity.Id,
                        SequenceNumber = i + 1,
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
                    };
                    _context.ProcessGroups.Add(pgEntity);
                }
                await _context.SaveChangesAsync();
            }

            // 保存合并投料来源
            if (request.SourceItems != null && request.SourceItems.Count > 0)
            {
                foreach (var src in request.SourceItems)
                {
                    _context.ProductionBatchInventories.Add(new ProductionBatchInventory
                    {
                        ProductionBatchId = entity.Id,
                        InventoryBatchId = src.InventoryBatchId,
                        OutboundRecordId = src.OutboundRecordId,
                        InputQuantity = src.InputQuantity,
                        InputWeight = src.InputWeight
                    });
                }
                await _context.SaveChangesAsync();

                // 如果 SourceBatchNo 为空，从第一个来源批次回填
                if (string.IsNullOrEmpty(entity.SourceBatchNo))
                {
                    var firstIb = await _context.InventoryBatches
                        .Where(ib => ib.Id == request.SourceItems[0].InventoryBatchId)
                        .Select(ib => ib.BatchNo)
                        .FirstOrDefaultAsync();
                    if (firstIb != null)
                    {
                        entity.SourceBatchNo = firstIb;
                        await _context.SaveChangesAsync();
                    }
                }
            }

            // 编号拆分模式：减少源批次有效量，消除关联用料计划通知
            if (request.InputType == BatchInputType.SplitFromNumber && !string.IsNullOrEmpty(request.SourceProductionNo))
            {
                var sourceBatch = await _context.ProductionBatches
                    .FirstOrDefaultAsync(b => b.BatchNo == request.SourceProductionNo);
                if (sourceBatch != null)
                {
                    var oldValidQty = sourceBatch.CurrentValidQty;
                    var oldValidWeight = sourceBatch.CurrentValidWeight;
                    if (entity.InputQuantity.HasValue)
                        sourceBatch.CurrentValidQty = (sourceBatch.CurrentValidQty ?? 0) - entity.InputQuantity.Value;
                    if (entity.InputWeight.HasValue)
                        sourceBatch.CurrentValidWeight = (int?)((sourceBatch.CurrentValidWeight ?? 0) - entity.InputWeight.Value);
                    await _context.SaveChangesAsync();

                    // 拆分扣减留痕：记录源批次有效量扣减明细（前值→后值）
                    if (sourceBatch.CurrentValidQty != oldValidQty || sourceBatch.CurrentValidWeight != oldValidWeight)
                    {
                        var parts = new List<string>();
                        if (sourceBatch.CurrentValidQty != oldValidQty)
                            parts.Add($"有效支数: {oldValidQty} → {sourceBatch.CurrentValidQty}");
                        if (sourceBatch.CurrentValidWeight != oldValidWeight)
                            parts.Add($"有效重量: {oldValidWeight?.ToString("G29")} → {sourceBatch.CurrentValidWeight?.ToString("G29")}kg");
                        await _operationLogService.AddLogAsync("Batch", sourceBatch.Id, "变更", $"拆分扣减(子批次 {entity.BatchNo}): {string.Join("; ", parts)}");
                    }

                    await _materialPlanService.DismissInMainWorkOrderPlanByBatchAndWorkOrderAsync(sourceBatch.Id, entity.WorkOrderNo);
                    await _materialPlanService.DismissInProcessReworkPlanByBatchAndWorkOrderAsync(sourceBatch.Id, entity.WorkOrderNo);
                }
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        // 刷新批次跟踪字段（包括有效投料疑问、理论成品量等计算字段）
        await _productionRecordService.RefreshBatchTrackingFieldsAsync(entity.Id);

        // 编号拆分模式：同时刷新源批次的理论成品量（源批次 CurrentValidQty/Weight 已扣减）
        if (request.InputType == BatchInputType.SplitFromNumber && !string.IsNullOrEmpty(request.SourceProductionNo))
        {
            var sourceBatch2 = await _context.ProductionBatches
                .FirstOrDefaultAsync(b => b.BatchNo == request.SourceProductionNo);
            if (sourceBatch2 != null)
                await _productionRecordService.RefreshBatchTrackingFieldsAsync(sourceBatch2.Id);
        }

        _logger.LogInformation("创建生产批次 {BatchNo} (工单: {WorkOrderNo})", batchNo, entity.WorkOrderNo);

        await _operationLogService.AddLogAsync("Batch",entity.Id, "创建", $"工单号={entity.WorkOrderNo}, 生产类型={entity.ProductionType}, 制造物品={entity.ManufacturingItem}, 制成倍数={entity.ProductionRatio}, 有效支数={entity.CurrentValidQty}, 有效重量={entity.CurrentValidWeight?.ToString("G29")}kg");

        await TryRefreshExecutionSummaryAsync(entity.WorkOrderNo);

        return new ProductionBatchListDto
        {
            Id = entity.Id,
            BatchNo = entity.BatchNo,
            Status = entity.Status,
            TagNo = entity.TagNo,
            WorkOrderNo = entity.WorkOrderNo,
            CreatedTime = entity.CreatedTime,
            CreatedBy = entity.CreatedBy
        };
    }

    public async Task<ProductionBatchDetailDto> UpdateAsync(int id, UpdateProductionBatchRequest request)
    {
        var entity = await _context.ProductionBatches
            .Include(b => b.ProcessGroups.OrderBy(pg => pg.SequenceNumber))
            .Include(b => b.ProductionBatchInventories)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (Id={id})");

        // 乐观锁检查
        if (!request.RowVersion.SequenceEqual(entity.RowVersion))
            throw new BusinessException("数据已被其他用户修改，请刷新后重试");

        // 生产类型 / 制造物品 不允许为空
        if (request.ProductionType == null)
            throw new BusinessException("生产类型不能为空");
        if (request.ManufacturingItem == null)
            throw new BusinessException("制造物品不能为空");

        // 工厂牌号验证（高代低）- 仅当任一牌号被更新时校验
        var effectivePlantGrade = request.PlantGrade ?? entity.PlantGrade;
        var effectiveSourcePlantGrade = request.SourcePlantGrade ?? entity.SourcePlantGrade;
        if (!GradeSubstitutes.IsSubstitutable(effectivePlantGrade, effectiveSourcePlantGrade))
            throw new BusinessException("仓库工厂牌号与工单工厂牌号不一致，且不可替代（仅允许高代低）");

        // ========== 枚举字符串有效性验证（DTO已使用枚举类型，类型安全由编译器保证） ==========
        if (request.ManufacturingItem == null)
            throw new BusinessException($"无效的制造物品: {request.ManufacturingItem}");

        // 快照 6 个监控字段的旧值（用于变更日志对比）
        var oldWorkOrderNo = entity.WorkOrderNo;
        var oldProductionType = entity.ProductionType;
        var oldManufacturingItem = entity.ManufacturingItem;
        var oldProductionRatio = entity.ProductionRatio;
        var oldValidQty = entity.CurrentValidQty;
        var oldValidWeight = entity.CurrentValidWeight;
        // 快照定尺匹配派生列的上游字段旧值（LengthStatus/工单/订单/主号，变更时级联重算生产记录+成检记录）
        var oldLengthStatus = entity.LengthStatus;
        var oldSalesOrderNo = entity.SalesOrderNo;
        var oldProductionMainNo = entity.ProductionMainNo;

        // 更新可修改字段（所有可空 DTO 字段用 ?? entity.Field 防止空值覆盖）
        entity.TagNo = request.TagNo ?? entity.TagNo;
        entity.ProductionType = request.ProductionType?.ToString() ?? entity.ProductionType;
        entity.ManufacturingItem = request.ManufacturingItem?.ToString() ?? entity.ManufacturingItem;
        entity.QualityRemark = request.QualityRemark ?? entity.QualityRemark;
        entity.SolutionParams = request.SolutionParams ?? entity.SolutionParams;
        entity.Remark = request.Remark ?? entity.Remark;
        entity.SourceBatchNo = request.SourceBatchNo ?? entity.SourceBatchNo;
        entity.SourceMaterialType = request.SourceMaterialType?.ToString() ?? entity.SourceMaterialType;
        entity.SourceName = request.SourceName ?? entity.SourceName;
        entity.SourceHeatNo = request.SourceHeatNo ?? entity.SourceHeatNo;
        entity.SourcePlantGrade = request.SourcePlantGrade ?? entity.SourcePlantGrade;
        entity.SourceSpecification = request.SourceSpecification ?? entity.SourceSpecification;
        entity.SourceLengthStatus = request.SourceLengthStatus?.ToString() ?? entity.SourceLengthStatus;
        entity.SourceUnitWeight = request.SourceUnitWeight ?? entity.SourceUnitWeight;
        entity.InputQuantity = request.InputQuantity ?? entity.InputQuantity;
        entity.InputWeight = request.InputWeight ?? entity.InputWeight;
        entity.InputType = request.InputType ?? entity.InputType;
        entity.SourceRemark = request.SourceRemark ?? entity.SourceRemark;
        entity.SourceProductionNo = request.SourceProductionNo ?? entity.SourceProductionNo;
        entity.CurrentValidQty = request.CurrentValidQty ?? entity.CurrentValidQty;
        entity.CurrentValidWeight = request.CurrentValidWeight ?? entity.CurrentValidWeight;
        if (request.IsForceCompleted.HasValue) entity.IsForceCompleted = request.IsForceCompleted.Value;
        if (request.ProductionRatio.HasValue) entity.ProductionRatio = request.ProductionRatio.Value;

        // 工单冗余字段（「非工单」时允许清空；正常时保留守卫防覆盖）
        var isNonWorkOrder = request.WorkOrderNo == NotWorkOrder;
        entity.WorkOrderNo = request.WorkOrderNo ?? (isNonWorkOrder ? "" : entity.WorkOrderNo);
        entity.SalesOrderNo = request.SalesOrderNo ?? (isNonWorkOrder ? "" : entity.SalesOrderNo);
        entity.ProductionMainNo = request.ProductionMainNo ?? (isNonWorkOrder ? "" : entity.ProductionMainNo);
        entity.ProductionSubNo = isNonWorkOrder ? request.ProductionSubNo : (request.ProductionSubNo ?? entity.ProductionSubNo);
        entity.OrderItemIds = request.OrderItemIds ?? (isNonWorkOrder ? "" : entity.OrderItemIds);
        entity.SignDate = request.SignDate ?? (isNonWorkOrder ? default : entity.SignDate);
        entity.Salesman = request.Salesman ?? (isNonWorkOrder ? "" : entity.Salesman);
        entity.EndCustomer = isNonWorkOrder ? request.EndCustomer : (request.EndCustomer ?? entity.EndCustomer);
        entity.DeliveryDate = request.DeliveryDate ?? (isNonWorkOrder ? default : entity.DeliveryDate);
        entity.DelayPenalty = request.DelayPenalty ?? (isNonWorkOrder ? default : entity.DelayPenalty);
        entity.MaterialName = request.MaterialName?.ToString() ?? (isNonWorkOrder ? "" : entity.MaterialName);
        entity.SettlementMethod = request.SettlementMethod?.ToString() ?? (isNonWorkOrder ? "" : entity.SettlementMethod);
        entity.StandardCode = request.StandardCode ?? (isNonWorkOrder ? "" : entity.StandardCode);
        entity.DeliveryState = request.DeliveryState?.ToString() ?? (isNonWorkOrder ? "" : entity.DeliveryState);
        entity.ManufacturingStatus = request.ManufacturingStatus?.ToString() ?? (isNonWorkOrder ? "" : entity.ManufacturingStatus);
        entity.PlantGrade = request.PlantGrade ?? (isNonWorkOrder ? "" : entity.PlantGrade);
        entity.Specification = request.Specification ?? (isNonWorkOrder ? "" : entity.Specification);
        entity.OuterDiameterNegative = request.OuterDiameterNegative ?? (isNonWorkOrder ? default : entity.OuterDiameterNegative);
        entity.OuterDiameterPositive = request.OuterDiameterPositive ?? (isNonWorkOrder ? default : entity.OuterDiameterPositive);
        entity.WallThicknessNegative = request.WallThicknessNegative ?? (isNonWorkOrder ? default : entity.WallThicknessNegative);
        entity.WallThicknessPositive = request.WallThicknessPositive ?? (isNonWorkOrder ? default : entity.WallThicknessPositive);
        entity.LengthStatus = request.LengthStatus?.ToString() ?? (isNonWorkOrder ? "" : entity.LengthStatus);
        entity.MinLength = isNonWorkOrder ? request.MinLength : (request.MinLength ?? entity.MinLength);
        entity.MaxLength = isNonWorkOrder ? request.MaxLength : (request.MaxLength ?? entity.MaxLength);
        entity.TotalQuantity = request.TotalQuantity ?? (isNonWorkOrder ? default : entity.TotalQuantity);
        entity.TotalMeters = request.TotalMeters ?? (isNonWorkOrder ? default : entity.TotalMeters);
        entity.TotalWeight = request.TotalWeight ?? (isNonWorkOrder ? default : entity.TotalWeight);
        entity.TotalItemCount = request.TotalItemCount ?? (isNonWorkOrder ? default : entity.TotalItemCount);
        entity.ItemDetails = isNonWorkOrder ? request.ItemDetails : (request.ItemDetails ?? entity.ItemDetails);
        entity.TechnicalRequirements = request.TechnicalRequirements?.ToString() ?? (isNonWorkOrder ? "" : entity.TechnicalRequirements);

        // ========== 合并投料来源全量替换 ==========
        if (request.SourceItems != null)
        {
            var existingLinks = await _context.ProductionBatchInventories
                .Where(pbi => pbi.ProductionBatchId == entity.Id)
                .ToListAsync();
            _context.ProductionBatchInventories.RemoveRange(existingLinks);

            foreach (var src in request.SourceItems)
            {
                _context.ProductionBatchInventories.Add(new ProductionBatchInventory
                {
                    ProductionBatchId = entity.Id,
                    InventoryBatchId = src.InventoryBatchId,
                    OutboundRecordId = src.OutboundRecordId,
                    InputQuantity = src.InputQuantity,
                    InputWeight = src.InputWeight
                });
            }

            // 重新汇总 InputQuantity/InputWeight
            if (request.SourceItems.Count > 0)
            {
                entity.InputQuantity = request.SourceItems.Sum(s => s.InputQuantity);
                entity.InputWeight = request.SourceItems.Sum(s => s.InputWeight);
            }
        }

        // ========== 制造状态与制造物品业务规则 ==========
        if (entity.ManufacturingItem == MaterialType.SpecialDeliveryStatus.ToString())
        {
            if (entity.ManufacturingStatus == entity.DeliveryState)
                throw new BusinessException("制造物品为「订成-非交付态」时，制造状态不能等于交货状态");
        }
        else if (entity.ManufacturingStatus != entity.DeliveryState)
        {
            throw new BusinessException("制造物品不为「订成-非交付态」时，制造状态必须等于交货状态");
        }

        await _context.SaveChangesAsync();

        // 工单号变更时，同步更新相关记录的全部冗余字段
        if (entity.WorkOrderNo != oldWorkOrderNo)
        {
            if (_context.Database.IsRelational())
            {
                // Note: MaterialReceiveCheck/FinalInspection 冗余字段已删除，数据通过 ProductionBatch JOIN 获取
                await _context.Ncrs
                    .Where(n => n.BatchNo == entity.BatchNo)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(n => n.WorkOrderNo, entity.WorkOrderNo)
                        .SetProperty(n => n.PlantGrade, entity.PlantGrade)
                        .SetProperty(n => n.Specification, entity.Specification));
            }
            else
            {
                // InMemory 回退：加载实体后逐条更新（InMemory Provider 不支持 ExecuteUpdateAsync）
                // MaterialReceiveCheck/FinalInspection 冗余字段已删除，跳过

                var ncrs = await _context.Ncrs
                    .Where(n => n.BatchNo == entity.BatchNo)
                    .ToListAsync();
                foreach (var n in ncrs)
                {
                    n.WorkOrderNo = entity.WorkOrderNo;
                    n.PlantGrade = entity.PlantGrade;
                    n.Specification = entity.Specification;
                }

                await _context.SaveChangesAsync();
            }
        }

        // 定尺切割长度匹配标识级联：LengthStatus/工单号/订单号/主号任一变更时，重算该批次生产记录+成检记录的派生列
        if (entity.WorkOrderNo != oldWorkOrderNo || entity.SalesOrderNo != oldSalesOrderNo
            || entity.ProductionMainNo != oldProductionMainNo || entity.LengthStatus != oldLengthStatus)
        {
            await _productionRecordService.RecomputeCutLengthMatchByBatchAsync(entity.Id);
            await _finalInspectionService.RecomputeCutLengthMatchByBatchAsync(entity.Id);
        }

        // 刷新批次跟踪字段（包括有效投料疑问等计算字段）
        await _productionRecordService.RefreshBatchTrackingFieldsAsync(entity.Id);

        // 刷新质量过程跟踪（批次字段变更同步到物化读模型）
        await TryRefreshQualityProcessTrackingAsync(entity.Id);

        // 记录 6 个监控字段的变更日志（仅记录实际变化的字段）
        var changes = new List<string>();
        if (entity.WorkOrderNo != oldWorkOrderNo) changes.Add($"工单号: {oldWorkOrderNo} → {entity.WorkOrderNo}");
        if (entity.ProductionType != oldProductionType) changes.Add($"生产类型: {oldProductionType} → {entity.ProductionType}");
        if (entity.ManufacturingItem != oldManufacturingItem) changes.Add($"制造物品: {oldManufacturingItem} → {entity.ManufacturingItem}");
        if (entity.ProductionRatio != oldProductionRatio) changes.Add($"制成倍数: {oldProductionRatio} → {entity.ProductionRatio}");
        if (entity.CurrentValidQty != oldValidQty) changes.Add($"有效支数: {oldValidQty} → {entity.CurrentValidQty}");
        if (entity.CurrentValidWeight != oldValidWeight) changes.Add($"有效重量: {oldValidWeight?.ToString("G29")} → {entity.CurrentValidWeight?.ToString("G29")}kg");
        if (changes.Count > 0)
            await _operationLogService.AddLogAsync("Batch",id, "变更", string.Join("; ", changes));

        // 有效量变更时，消除关联的在产主工单计划通知
        if (entity.CurrentValidQty != oldValidQty || entity.CurrentValidWeight != oldValidWeight)
        {
            await _materialPlanService.DismissInMainWorkOrderPlansByBatchAsync(entity.Id);
        }

        // 工单号从「非工单」变更为正常工单时，消除在产改制计划通知（在产改制B模式）
        if (oldWorkOrderNo == NotWorkOrder && entity.WorkOrderNo != NotWorkOrder)
        {
            await _materialPlanService.DismissInProcessReworkPlansByBatchAsync(entity.Id);
            await _notificationService.CreateAsync(
                NotificationType.BatchPlanAutoCompleted.ToString(),
                "在产改制计划自动完成",
                $"批次 {entity.BatchNo} 的工单号从「非工单」变更为 {entity.WorkOrderNo}，关联的在产改制计划已自动完成");
        }

        _logger.LogInformation("更新生产批次 {BatchNo} (Id={Id})", entity.BatchNo, id);

        var dto = ToDetailDto(entity);

        await TryRefreshExecutionSummaryAsync(entity.WorkOrderNo);
        // 工单号变更时旧工单的投料量/可用余量须一并重算（同 OutboundWriteService 双刷模式）
        if (!string.IsNullOrEmpty(oldWorkOrderNo)
            && !string.Equals(oldWorkOrderNo, entity.WorkOrderNo, StringComparison.OrdinalIgnoreCase))
            await TryRefreshExecutionSummaryAsync(oldWorkOrderNo);

        return dto;
    }

    public async Task UpdateStatusAsync(int id, UpdateBatchStatusRequest request)
    {
        var entity = await _context.ProductionBatches
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (Id={id})");

        if (!request.RowVersion.SequenceEqual(entity.RowVersion))
            throw new BusinessException("数据已被其他用户修改，请刷新后重试");

        var newStatus = request.Status;
        var oldStatus = entity.Status;

        // 状态流转验证
        if (!CanTransitionTo(oldStatus, newStatus))
            throw new BusinessException($"不允许从 {oldStatus} 变更为 {newStatus}");

        entity.Status = newStatus;

        // 强制完成逻辑
        if (newStatus == BatchStatus.Completed)
            entity.IsForceCompleted = true;

        // 从强制完成恢复时清除标记
        bool isForceCompleteRelease = false;
        if (oldStatus == BatchStatus.Completed && entity.IsForceCompleted && newStatus != BatchStatus.Completed)
        {
            entity.IsForceCompleted = false;
            isForceCompleteRelease = true;
        }

        // 从暂停恢复：后续由跟踪引擎重算状态
        bool isResumeFromPause = oldStatus == BatchStatus.Suspended && newStatus != BatchStatus.Suspended;

        // 使用事务包裹状态变更和日志写入
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();

            if (isForceCompleteRelease)
            {
                await _operationLogService.AddLogAsync("Batch", id, "变更", "释放强制完成");
            }
            else
            {
                var logDetail = $"状态变更: {oldStatus} → {newStatus}";
                await _operationLogService.AddLogAsync("Batch", id, "变更", logDetail);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        if (isForceCompleteRelease || isResumeFromPause)
        {
            // 取消手动操作后，由跟踪引擎根据实际记录重新计算状态和跟踪字段
            try
            {
                await _productionRecordService.RefreshBatchTrackingFieldsAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "取消手动操作后跟踪字段重算失败（不影响主流程）: BatchId={BatchId}", id);
            }
            _logger.LogInformation("取消手动状态（强制完成/暂停）批次 {BatchNo}，跟踪字段已重算", entity.BatchNo);
        }
        else
        {
            _logger.LogInformation("更新批次状态 {BatchNo} → {Status}", entity.BatchNo, newStatus);
            await TryRefreshExecutionSummaryAsync(entity.WorkOrderNo);
        }
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.ProductionBatches
            .Include(b => b.ProcessGroups)
            .Include(b => b.ProductionBatchInventories)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (Id={id})");

        // 仅允许删除"未产"状态的批次
        if (entity.Status != BatchStatus.None)
            throw new BusinessException($"仅允许删除「未产」状态的批次，当前状态为 {entity.Status}");

        // 清理合并投料来源（FK 为 NoAction，需手动删除）
        if (entity.ProductionBatchInventories.Count != 0)
        {
            _context.ProductionBatchInventories.RemoveRange(entity.ProductionBatchInventories);
        }

        // 收集所有工序组 ID，用于清理引用 ProcessGroup 的记录
        var processGroupIds = entity.ProcessGroups.Select(g => g.Id).ToList();

        if (processGroupIds.Count != 0)
        {
            // 以下四个表的 FK → ProcessGroup 为 NoAction，会阻塞 Cascade 删除，
            // 必须在此手动删除它们
            var productionRecords = await _context.ProductionRecords
                .Where(r => processGroupIds.Contains(r.ProcessGroupId))
                .ToListAsync();
            _context.ProductionRecords.RemoveRange(productionRecords);

            var sectionOutsources = await _context.SectionOutsources
                .Where(s => processGroupIds.Contains(s.ProcessGroupId))
                .ToListAsync();
            // OutsourceRecovery 通过 SectionOutsource 级联删除
            _context.SectionOutsources.RemoveRange(sectionOutsources);

            var processInspections = await _context.ProcessInspections
                .Where(p => processGroupIds.Contains(p.ProcessGroupId))
                .ToListAsync();
            _context.ProcessInspections.RemoveRange(processInspections);

            var picklingInRecords = await _context.PicklingInRecords
                .Where(p => processGroupIds.Contains(p.ProcessGroupId))
                .ToListAsync();
            _context.PicklingInRecords.RemoveRange(picklingInRecords);
        }

        // 先记录删除日志
        await _operationLogService.AddLogAsync("Batch", id, "删除", $"批次号={entity.BatchNo}, 工单号={entity.WorkOrderNo}");

        // 删除批次（ProcessGroup 通过 Cascade 自动删除）
        _context.ProductionBatches.Remove(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("删除生产批次 {BatchNo} (Id={Id})", entity.BatchNo, id);

        await TryRefreshExecutionSummaryAsync(entity.WorkOrderNo);
    }

    public async Task<SaveBatchResponse> SaveAllAsync(int id, SaveBatchRequest request)
    {
        var entity = await _context.ProductionBatches
            .Include(b => b.ProcessGroups)
            .Include(b => b.ProductionBatchInventories)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (Id={id})");

        // 乐观锁检查
        if (!request.RowVersion.SequenceEqual(entity.RowVersion))
            throw new BusinessException("数据已被其他用户修改，请刷新后重试");

        // 工序组数值验证（全量替换前校验，失败则阻止后续修改）
        if (request.ProcessGroups != null && request.ProcessGroups.Count > 0)
            ValidateProcessGroupValues(request.ProcessGroups);

        // ========== 统一必填（两路径共用） ==========

        // 生产类型 / 制造物品
        if (request.ProductionType == null)
            throw new BusinessException("生产类型不能为空");
        if (request.ManufacturingItem == null)
            throw new BusinessException("制造物品不能为空");

        // 工单规格必填（取请求值，未传则用实体现有值）
        var effectivePlantGrade = request.PlantGrade ?? entity.PlantGrade;
        var effectiveSpec = request.Specification ?? entity.Specification;
        var effectiveDelivery = request.DeliveryState?.ToString() ?? entity.DeliveryState;
        var effectiveManufacturingStatus = request.ManufacturingStatus?.ToString() ?? entity.ManufacturingStatus;
        var effectiveMaterialName = request.MaterialName?.ToString() ?? entity.MaterialName;
        var effectiveLengthStatus = request.LengthStatus?.ToString() ?? entity.LengthStatus;
        var effectiveTotalWeight = request.TotalWeight ?? entity.TotalWeight;
        var effectiveProductionRatio = request.ProductionRatio ?? entity.ProductionRatio;
        var effectiveTotalQuantity = request.TotalQuantity ?? entity.TotalQuantity;
        var effectiveSourceGrade = request.SourcePlantGrade ?? entity.SourcePlantGrade;
        var effectiveSourceSpec = request.SourceSpecification ?? entity.SourceSpecification;
        var effectiveInputWeight = request.InputWeight ?? entity.InputWeight;
        var effectiveInputQuantity = request.InputQuantity ?? entity.InputQuantity;

        if (string.IsNullOrWhiteSpace(effectivePlantGrade))
            throw new BusinessException("工厂牌号不能为空");
        if (string.IsNullOrWhiteSpace(effectiveSpec))
            throw new BusinessException("规格不能为空");
        if (string.IsNullOrWhiteSpace(effectiveDelivery))
            throw new BusinessException("交货状态不能为空");
        if (string.IsNullOrWhiteSpace(effectiveMaterialName))
            throw new BusinessException("物料名称不能为空");
        if (string.IsNullOrWhiteSpace(effectiveLengthStatus))
            throw new BusinessException("长度状态不能为空");
        // 非工单不校验总重量/总支数
        var skipWeightQuantityCheck = request.WorkOrderNo == NotWorkOrder || entity.WorkOrderNo == NotWorkOrder;
        if (!skipWeightQuantityCheck)
        {
            if (effectiveTotalWeight <= 0)
                throw new BusinessException("总重量必须大于0");
            // 定尺时总支数必须大于0
            if (effectiveLengthStatus == LengthStatus.Fixed.ToString() && effectiveTotalQuantity <= 0)
                throw new BusinessException("总支数（定尺时必须大于0）");
        }
        if (string.IsNullOrWhiteSpace(effectiveSourceGrade))
            throw new BusinessException("仓库工厂牌号不能为空");
        if (string.IsNullOrWhiteSpace(effectiveSourceSpec))
            throw new BusinessException("仓库规格不能为空");
        if (effectiveInputWeight == null || effectiveInputWeight <= 0)
            throw new BusinessException("领料重量必须大于0");
        if (effectiveInputQuantity == null || effectiveInputQuantity <= 0)
            throw new BusinessException("领料支数必须大于0");

        // ========== 有工单路径额外验证 ==========
        var effectiveWorkOrderNo = request.WorkOrderNo ?? entity.WorkOrderNo;
        if (effectiveWorkOrderNo != NotWorkOrder)
        {
            var effectiveSettlementMethod = request.SettlementMethod?.ToString() ?? entity.SettlementMethod;
            var effectiveStandardCode = request.StandardCode ?? entity.StandardCode;
            var effectiveTechnicalRequirements = request.TechnicalRequirements?.ToString() ?? entity.TechnicalRequirements;

            if (string.IsNullOrWhiteSpace(effectiveSettlementMethod))
                throw new BusinessException("结算方式不能为空");
            if (string.IsNullOrWhiteSpace(effectiveStandardCode))
                throw new BusinessException("产品标准编码不能为空");
            if (string.IsNullOrWhiteSpace(effectiveTechnicalRequirements))
                throw new BusinessException("技术要求不能为空");
        }

        // ========== 制造状态与制造物品业务规则 ==========
        var effectiveItemStr = request.ManufacturingItem?.ToString() ?? entity.ManufacturingItem;
        if (effectiveItemStr == MaterialType.SpecialDeliveryStatus.ToString())
        {
            if (effectiveManufacturingStatus == effectiveDelivery)
                throw new BusinessException("制造物品为「订成-非交付态」时，制造状态不能等于交货状态");
        }
        else if (effectiveManufacturingStatus != effectiveDelivery)
        {
            throw new BusinessException("制造物品不为「订成-非交付态」时，制造状态必须等于交货状态");
        }

        // 工单号验证：非空；NotWorkOrder跳过；其他值必须在工单表中存在
        if (request.WorkOrderNo != null)
        {
            if (string.IsNullOrWhiteSpace(request.WorkOrderNo))
                throw new BusinessException("工单号不能为空（无对应工单请填写「非工单」）");

            if (request.WorkOrderNo != NotWorkOrder && request.WorkOrderNo != entity.WorkOrderNo)
            {
                var workOrderExists = await _context.WorkOrders.AnyAsync(w => w.WorkOrderNo == request.WorkOrderNo);
                if (!workOrderExists)
                    throw new BusinessException($"工单不存在 (WorkOrderNo={request.WorkOrderNo})");
            }
        }

        // ===== 1. 更新批次头字段 =====
        // 快照 6 个监控字段的旧值（用于变更日志对比）
        var oldWorkOrderNo = entity.WorkOrderNo;
        var oldProductionType = entity.ProductionType;
        var oldManufacturingItem = entity.ManufacturingItem;
        var oldProductionRatio = entity.ProductionRatio;
        var oldValidQty = entity.CurrentValidQty;
        var oldValidWeight = entity.CurrentValidWeight;
        // 快照定尺匹配派生列的上游字段旧值（LengthStatus/工单/订单/主号，变更时级联重算生产记录+成检记录）
        var oldLengthStatus = entity.LengthStatus;
        var oldSalesOrderNo = entity.SalesOrderNo;
        var oldProductionMainNo = entity.ProductionMainNo;

        entity.TagNo = request.TagNo ?? entity.TagNo;
        entity.QualityRemark = request.QualityRemark ?? entity.QualityRemark;
        entity.SolutionParams = request.SolutionParams ?? entity.SolutionParams;
        entity.ProductionType = request.ProductionType?.ToString() ?? entity.ProductionType;
        entity.Remark = request.Remark ?? entity.Remark;
        entity.ManufacturingItem = request.ManufacturingItem?.ToString() ?? entity.ManufacturingItem;
        entity.SourceBatchNo = request.SourceBatchNo ?? entity.SourceBatchNo;
        entity.SourceMaterialType = request.SourceMaterialType?.ToString() ?? entity.SourceMaterialType;
        entity.SourceName = request.SourceName ?? entity.SourceName;
        entity.SourceHeatNo = request.SourceHeatNo ?? entity.SourceHeatNo;
        entity.SourcePlantGrade = request.SourcePlantGrade ?? entity.SourcePlantGrade;
        entity.SourceSpecification = request.SourceSpecification ?? entity.SourceSpecification;
        entity.SourceLengthStatus = request.SourceLengthStatus?.ToString() ?? entity.SourceLengthStatus;
        entity.SourceUnitWeight = request.SourceUnitWeight ?? entity.SourceUnitWeight;
        entity.InputQuantity = request.InputQuantity ?? entity.InputQuantity;
        entity.InputWeight = request.InputWeight ?? entity.InputWeight;
        entity.InputType = request.InputType ?? entity.InputType;
        entity.SourceRemark = request.SourceRemark ?? entity.SourceRemark;
        entity.SourceProductionNo = request.SourceProductionNo ?? entity.SourceProductionNo;
        entity.CurrentValidQty = request.CurrentValidQty ?? entity.CurrentValidQty;
        entity.CurrentValidWeight = request.CurrentValidWeight ?? entity.CurrentValidWeight;
        if (request.IsForceCompleted.HasValue) entity.IsForceCompleted = request.IsForceCompleted.Value;
        if (request.ProductionRatio.HasValue) entity.ProductionRatio = request.ProductionRatio.Value;

        // 工单冗余字段（「非工单」时允许清空；正常时保留守卫防覆盖）
        var isNonWorkOrder = request.WorkOrderNo == NotWorkOrder;
        entity.WorkOrderNo = request.WorkOrderNo ?? (isNonWorkOrder ? "" : entity.WorkOrderNo);
        entity.SalesOrderNo = request.SalesOrderNo ?? (isNonWorkOrder ? "" : entity.SalesOrderNo);
        entity.ProductionMainNo = request.ProductionMainNo ?? (isNonWorkOrder ? "" : entity.ProductionMainNo);
        entity.ProductionSubNo = isNonWorkOrder ? request.ProductionSubNo : (request.ProductionSubNo ?? entity.ProductionSubNo);
        entity.OrderItemIds = request.OrderItemIds ?? (isNonWorkOrder ? "" : entity.OrderItemIds);
        entity.SignDate = request.SignDate ?? (isNonWorkOrder ? default : entity.SignDate);
        entity.Salesman = request.Salesman ?? (isNonWorkOrder ? "" : entity.Salesman);
        entity.EndCustomer = isNonWorkOrder ? request.EndCustomer : (request.EndCustomer ?? entity.EndCustomer);
        entity.DeliveryDate = request.DeliveryDate ?? (isNonWorkOrder ? default : entity.DeliveryDate);
        entity.DelayPenalty = request.DelayPenalty ?? (isNonWorkOrder ? default : entity.DelayPenalty);
        entity.MaterialName = request.MaterialName?.ToString() ?? (isNonWorkOrder ? "" : entity.MaterialName);
        entity.SettlementMethod = request.SettlementMethod?.ToString() ?? (isNonWorkOrder ? "" : entity.SettlementMethod);
        entity.StandardCode = request.StandardCode ?? (isNonWorkOrder ? "" : entity.StandardCode);
        entity.DeliveryState = request.DeliveryState?.ToString() ?? (isNonWorkOrder ? "" : entity.DeliveryState);
        entity.ManufacturingStatus = request.ManufacturingStatus?.ToString() ?? (isNonWorkOrder ? "" : entity.ManufacturingStatus);
        entity.PlantGrade = request.PlantGrade ?? (isNonWorkOrder ? "" : entity.PlantGrade);
        entity.Specification = request.Specification ?? (isNonWorkOrder ? "" : entity.Specification);
        entity.OuterDiameterNegative = request.OuterDiameterNegative ?? (isNonWorkOrder ? default : entity.OuterDiameterNegative);
        entity.OuterDiameterPositive = request.OuterDiameterPositive ?? (isNonWorkOrder ? default : entity.OuterDiameterPositive);
        entity.WallThicknessNegative = request.WallThicknessNegative ?? (isNonWorkOrder ? default : entity.WallThicknessNegative);
        entity.WallThicknessPositive = request.WallThicknessPositive ?? (isNonWorkOrder ? default : entity.WallThicknessPositive);
        entity.LengthStatus = request.LengthStatus?.ToString() ?? (isNonWorkOrder ? "" : entity.LengthStatus);
        entity.MinLength = isNonWorkOrder ? request.MinLength : (request.MinLength ?? entity.MinLength);
        entity.MaxLength = isNonWorkOrder ? request.MaxLength : (request.MaxLength ?? entity.MaxLength);
        entity.TotalQuantity = request.TotalQuantity ?? (isNonWorkOrder ? default : entity.TotalQuantity);
        entity.TotalMeters = request.TotalMeters ?? (isNonWorkOrder ? default : entity.TotalMeters);
        entity.TotalWeight = request.TotalWeight ?? (isNonWorkOrder ? default : entity.TotalWeight);
        entity.TotalItemCount = request.TotalItemCount ?? (isNonWorkOrder ? default : entity.TotalItemCount);
        entity.ItemDetails = isNonWorkOrder ? request.ItemDetails : (request.ItemDetails ?? entity.ItemDetails);
        entity.TechnicalRequirements = request.TechnicalRequirements?.ToString() ?? (isNonWorkOrder ? "" : entity.TechnicalRequirements);

        // ========== 合并投料来源全量替换 ==========
        if (request.SourceItems != null)
        {
            var existingLinks = await _context.ProductionBatchInventories
                .Where(pbi => pbi.ProductionBatchId == entity.Id)
                .ToListAsync();
            _context.ProductionBatchInventories.RemoveRange(existingLinks);

            foreach (var src in request.SourceItems)
            {
                _context.ProductionBatchInventories.Add(new ProductionBatchInventory
                {
                    ProductionBatchId = entity.Id,
                    InventoryBatchId = src.InventoryBatchId,
                    OutboundRecordId = src.OutboundRecordId,
                    InputQuantity = src.InputQuantity,
                    InputWeight = src.InputWeight
                });
            }

            if (request.SourceItems.Count > 0)
            {
                entity.InputQuantity = request.SourceItems.Sum(s => s.InputQuantity);
                entity.InputWeight = request.SourceItems.Sum(s => s.InputWeight);
            }
        }

        // ===== 2. 更新状态（如有） =====
        if (request.Status != null)
        {
            var newStatus = request.Status.Value;
            if (newStatus != entity.Status)
            {
                entity.Status = newStatus;

                if (newStatus == BatchStatus.Completed)
                    entity.IsForceCompleted = true;
            }
        }

        // ===== 3. 全量替换工序组 =====
        // 3a. 删除旧工序组（跳过有生产记录、委外记录或工序检验引用的）
        HashSet<int> referencedIds = new();
        if (entity.ProcessGroups.Any())
        {
            var oldIds = entity.ProcessGroups.Select(pg => pg.Id).ToList();

            var referencedByRecord = await _context.ProductionRecords
                .Where(r => oldIds.Contains(r.ProcessGroupId))
                .Select(r => r.ProcessGroupId)
                .Distinct()
                .ToListAsync();

            var referencedByOutsource = await _context.SectionOutsources
                .Where(s => oldIds.Contains(s.ProcessGroupId))
                .Select(s => s.ProcessGroupId)
                .Distinct()
                .ToListAsync();

            var referencedByPicklingRecord = await _context.PicklingInRecords
                .Where(p => oldIds.Contains(p.ProcessGroupId))
                .Select(p => p.ProcessGroupId)
                .Distinct()
                .ToListAsync();

            var referencedByProcessInspection = await _context.ProcessInspections
                .Where(p => oldIds.Contains(p.ProcessGroupId))
                .Select(p => p.ProcessGroupId)
                .Distinct()
                .ToListAsync();

            var referencedByMaterialReceiveCheck = await _context.MaterialReceiveChecks
                .Where(m => oldIds.Contains(m.ProcessGroupId))
                .Select(m => m.ProcessGroupId)
                .Distinct()
                .ToListAsync();

            referencedIds = new HashSet<int>(referencedByRecord
                .Concat(referencedByOutsource)
                .Concat(referencedByPicklingRecord)
                .Concat(referencedByProcessInspection)
                .Concat(referencedByMaterialReceiveCheck));
            var toRemove = entity.ProcessGroups.Where(pg => !referencedIds.Contains(pg.Id)).ToList();

            if (toRemove.Count > 0)
            {
                _context.ProcessGroups.RemoveRange(toRemove);
            }

            if (referencedIds.Count > 0)
            {
                var refNames = entity.ProcessGroups
                    .Where(pg => referencedIds.Contains(pg.Id))
                    .Select(pg => $"{pg.ProcessName}(Id={pg.Id})");
                _logger.LogWarning("批次 {BatchNo} 的 {Count} 个工序组因存在生产记录、委外记录或成检到料引用而跳过删除: {Names}",
                    entity.BatchNo, referencedIds.Count, string.Join(", ", refNames));
            }
        }

        // 3b. 先提交删除，避免后续 INSERT 与旧记录主键冲突
        await _context.SaveChangesAsync();

        // 3c. 创建新工序组
        //  - 若请求项与保留的被引用工序组序列号相同 → 原地更新（避免唯一键冲突）
        //  - 否则 → 新增插入（旧记录已在 3b 删除，同序列号可安全插入）
        for (int i = 0; i < request.ProcessGroups!.Count; i++)
        {
            var pgReq = request.ProcessGroups[i];
            var seq = i + 1;

            // 若有被引用保留的同序列号工序组，原地更新
            var existingReferenced = entity.ProcessGroups
                .FirstOrDefault(pg => referencedIds.Contains(pg.Id) && pg.SequenceNumber == seq);
            if (existingReferenced != null)
            {
                existingReferenced.ProcessName = pgReq.ProcessName;
                existingReferenced.ManufacturingSpec = pgReq.ManufacturingSpec;
                existingReferenced.OuterDiameterTolerance = pgReq.OuterDiameterTolerance;
                existingReferenced.WallThicknessTolerance = pgReq.WallThicknessTolerance;
                existingReferenced.ManufacturingLength = pgReq.ManufacturingLength;
                existingReferenced.CuttingTreatment = pgReq.CuttingTreatment;
                existingReferenced.Remark = pgReq.Remark;
                existingReferenced.ColdRollDraw = pgReq.ColdRollDraw;
                existingReferenced.OilPipeCut = pgReq.OilPipeCut;
                existingReferenced.Degrease = pgReq.Degrease;
                existingReferenced.EmulsionWash = pgReq.EmulsionWash;
                existingReferenced.UltrasonicWash = pgReq.UltrasonicWash;
                existingReferenced.ClothPolish = pgReq.ClothPolish;
                existingReferenced.BrightAnnealing = pgReq.BrightAnnealing;
                existingReferenced.Solution = pgReq.Solution;
                existingReferenced.Straighten = pgReq.Straighten;
                existingReferenced.Cut = pgReq.Cut;
                existingReferenced.ThicknessMeasure = pgReq.ThicknessMeasure;
                existingReferenced.Pickle = pgReq.Pickle;
                existingReferenced.OuterPolish = pgReq.OuterPolish;
                existingReferenced.InnerPolish = pgReq.InnerPolish;
                existingReferenced.InnerGrinding = pgReq.InnerGrinding;
                existingReferenced.OuterSpotGrinding = pgReq.OuterSpotGrinding;
                existingReferenced.SandBlasting = pgReq.SandBlasting;
                existingReferenced.ShotBlasting = pgReq.ShotBlasting;
                existingReferenced.Inspection = pgReq.Inspection;
                existingReferenced.WeldingHead = pgReq.WeldingHead;
                existingReferenced.Welding = pgReq.Welding;
                existingReferenced.Lubrication = pgReq.Lubrication;
                existingReferenced.Packing = pgReq.Packing;
                existingReferenced.Warehouse = pgReq.Warehouse;
                existingReferenced.Extra1 = pgReq.Extra1;
                existingReferenced.Extra2 = pgReq.Extra2;
                continue;
            }

            var pg = new ProcessGroup
            {
                ProductionBatchId = id,
                SequenceNumber = seq,
                ProcessName = pgReq.ProcessName,
                ManufacturingSpec = pgReq.ManufacturingSpec,
                OuterDiameterTolerance = pgReq.OuterDiameterTolerance,
                WallThicknessTolerance = pgReq.WallThicknessTolerance,
                ManufacturingLength = pgReq.ManufacturingLength,
                CuttingTreatment = pgReq.CuttingTreatment,
                Remark = pgReq.Remark,
                ColdRollDraw = pgReq.ColdRollDraw,
                OilPipeCut = pgReq.OilPipeCut,
                Degrease = pgReq.Degrease,
                EmulsionWash = pgReq.EmulsionWash,
                UltrasonicWash = pgReq.UltrasonicWash,
                ClothPolish = pgReq.ClothPolish,
                BrightAnnealing = pgReq.BrightAnnealing,
                Solution = pgReq.Solution,
                Straighten = pgReq.Straighten,
                Cut = pgReq.Cut,
                ThicknessMeasure = pgReq.ThicknessMeasure,
                Pickle = pgReq.Pickle,
                OuterPolish = pgReq.OuterPolish,
                InnerPolish = pgReq.InnerPolish,
                InnerGrinding = pgReq.InnerGrinding,
                OuterSpotGrinding = pgReq.OuterSpotGrinding,
                SandBlasting = pgReq.SandBlasting,
                ShotBlasting = pgReq.ShotBlasting,
                Inspection = pgReq.Inspection,
                WeldingHead = pgReq.WeldingHead,
                Welding = pgReq.Welding,
                Lubrication = pgReq.Lubrication,
                Packing = pgReq.Packing,
                Warehouse = pgReq.Warehouse,
                Extra1 = pgReq.Extra1,
                Extra2 = pgReq.Extra2
            };
            _context.ProcessGroups.Add(pg);
        }

        // ===== 4. 提交新增工序组（此时仅有 INSERT，无冲突） =====
        await _context.SaveChangesAsync();

        // 工单号变更时，同步更新相关记录的全部冗余字段
        if (entity.WorkOrderNo != oldWorkOrderNo)
        {
            if (_context.Database.IsRelational())
            {
                // Note: MaterialReceiveCheck/FinalInspection 冗余字段已删除，数据通过 ProductionBatch JOIN 获取
                await _context.Ncrs
                    .Where(n => n.BatchNo == entity.BatchNo)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(n => n.WorkOrderNo, entity.WorkOrderNo)
                        .SetProperty(n => n.PlantGrade, entity.PlantGrade)
                        .SetProperty(n => n.Specification, entity.Specification));
            }
            else
            {
                // InMemory 回退：加载实体后逐条更新（InMemory Provider 不支持 ExecuteUpdateAsync）
                // MaterialReceiveCheck/FinalInspection 冗余字段已删除，跳过

                var ncrs = await _context.Ncrs
                    .Where(n => n.BatchNo == entity.BatchNo)
                    .ToListAsync();
                foreach (var n in ncrs)
                {
                    n.WorkOrderNo = entity.WorkOrderNo;
                    n.PlantGrade = entity.PlantGrade;
                    n.Specification = entity.Specification;
                }

                await _context.SaveChangesAsync();
            }
        }

        // 定尺切割长度匹配标识级联：LengthStatus/工单号/订单号/主号任一变更时，重算该批次生产记录+成检记录的派生列
        if (entity.WorkOrderNo != oldWorkOrderNo || entity.SalesOrderNo != oldSalesOrderNo
            || entity.ProductionMainNo != oldProductionMainNo || entity.LengthStatus != oldLengthStatus)
        {
            await _productionRecordService.RecomputeCutLengthMatchByBatchAsync(entity.Id);
            await _finalInspectionService.RecomputeCutLengthMatchByBatchAsync(entity.Id);
        }

        // 记录 6 个监控字段的变更日志（仅记录实际变化的字段）
        var changes = new List<string>();
        if (entity.WorkOrderNo != oldWorkOrderNo) changes.Add($"工单号: {oldWorkOrderNo} → {entity.WorkOrderNo}");
        if (entity.ProductionType != oldProductionType) changes.Add($"生产类型: {oldProductionType} → {entity.ProductionType}");
        if (entity.ManufacturingItem != oldManufacturingItem) changes.Add($"制造物品: {oldManufacturingItem} → {entity.ManufacturingItem}");
        if (entity.ProductionRatio != oldProductionRatio) changes.Add($"制成倍数: {oldProductionRatio} → {entity.ProductionRatio}");
        if (entity.CurrentValidQty != oldValidQty) changes.Add($"有效支数: {oldValidQty} → {entity.CurrentValidQty}");
        if (entity.CurrentValidWeight != oldValidWeight) changes.Add($"有效重量: {oldValidWeight?.ToString("G29")} → {entity.CurrentValidWeight?.ToString("G29")}kg");
        if (changes.Count > 0)
            await _operationLogService.AddLogAsync("Batch",id, "变更", string.Join("; ", changes));

        // 有效量变更时，消除关联的在产主工单计划通知
        if (entity.CurrentValidQty != oldValidQty || entity.CurrentValidWeight != oldValidWeight)
        {
            await _materialPlanService.DismissInMainWorkOrderPlansByBatchAsync(entity.Id);
        }

        // 工单号从「非工单」变更为正常工单时，消除在产改制计划通知（在产改制B模式）
        if (oldWorkOrderNo == NotWorkOrder && entity.WorkOrderNo != NotWorkOrder)
        {
            await _materialPlanService.DismissInProcessReworkPlansByBatchAsync(entity.Id);
            await _notificationService.CreateAsync(
                NotificationType.BatchPlanAutoCompleted.ToString(),
                "在产改制计划自动完成",
                $"批次 {entity.BatchNo} 的工单号从「非工单」变更为 {entity.WorkOrderNo}，关联的在产改制计划已自动完成");
        }

        // ===== 5. 工序组已变更，刷新批次跟踪字段 =====
        await _productionRecordService.BatchUpdateBatchTrackingAsync(new[] { id });

        _logger.LogInformation("批量保存生产批次 {BatchNo} (Id={Id}), 工序组={GroupCount}",
            entity.BatchNo, id, request.ProcessGroups.Count);

        // 重新计算关联改制库存计划的工艺周期（工序组变更后）
        await _materialPlanService.RecalculateStandardCycleForBatchAsync(entity.BatchNo);

        await TryRefreshExecutionSummaryAsync(entity.WorkOrderNo);
        // 工单号变更时旧工单的投料量/可用余量须一并重算（同 OutboundWriteService 双刷模式）
        if (!string.IsNullOrEmpty(oldWorkOrderNo)
            && !string.Equals(oldWorkOrderNo, entity.WorkOrderNo, StringComparison.OrdinalIgnoreCase))
            await TryRefreshExecutionSummaryAsync(oldWorkOrderNo);

        // 刷新质量过程跟踪（批次字段变更同步到物化读模型）
        await TryRefreshQualityProcessTrackingAsync(id);

        return new SaveBatchResponse
        {
            RowVersion = entity.RowVersion,
            Status = entity.Status
        };
    }

    // ========== 工序组 ==========

    public async Task<List<ProcessGroupDto>> GetProcessGroupsAsync(int batchId)
    {
        var groups = await _context.ProcessGroups
            .AsNoTracking()
            .Where(pg => pg.ProductionBatchId == batchId)
            .OrderBy(pg => pg.SequenceNumber)
            .Select(pg => ToGroupDto(pg))
            .ToListAsync();

        return groups;
    }

    public async Task<ProcessGroupDto> AddProcessGroupAsync(int batchId, CreateProcessGroupRequest request)
    {
        var batch = await _context.ProductionBatches.FindAsync(batchId);
        if (batch == null)
            throw new BusinessException($"生产批次不存在 (Id={batchId})");

        // 获取下一个序号
        var maxSeq = await _context.ProcessGroups
            .Where(pg => pg.ProductionBatchId == batchId)
            .MaxAsync(pg => (int?)pg.SequenceNumber) ?? 0;

        var entity = new ProcessGroup
        {
            ProductionBatchId = batchId,
            SequenceNumber = maxSeq + 1,
            ProcessName = request.ProcessName,
            ManufacturingSpec = request.ManufacturingSpec,
            OuterDiameterTolerance = request.OuterDiameterTolerance,
            WallThicknessTolerance = request.WallThicknessTolerance,
            ManufacturingLength = request.ManufacturingLength,
            CuttingTreatment = request.CuttingTreatment,
            Remark = request.Remark,
            ColdRollDraw = request.ColdRollDraw,
            OilPipeCut = request.OilPipeCut,
            Degrease = request.Degrease,
            EmulsionWash = request.EmulsionWash,
            UltrasonicWash = request.UltrasonicWash,
            ClothPolish = request.ClothPolish,
            BrightAnnealing = request.BrightAnnealing,
            Solution = request.Solution,
            Straighten = request.Straighten,
            Cut = request.Cut,
            ThicknessMeasure = request.ThicknessMeasure,
            Pickle = request.Pickle,
            OuterPolish = request.OuterPolish,
            InnerPolish = request.InnerPolish,
            InnerGrinding = request.InnerGrinding,
            OuterSpotGrinding = request.OuterSpotGrinding,
            SandBlasting = request.SandBlasting,
            ShotBlasting = request.ShotBlasting,
            Inspection = request.Inspection,
            WeldingHead = request.WeldingHead,
            Welding = request.Welding,
            Lubrication = request.Lubrication,
            Packing = request.Packing,
            Warehouse = request.Warehouse,
            Extra1 = request.Extra1,
            Extra2 = request.Extra2
        };

        _context.ProcessGroups.Add(entity);
        await _context.SaveChangesAsync();

        // 工序组变更影响工段解析，刷新批次跟踪字段
        await _productionRecordService.BatchUpdateBatchTrackingAsync(new[] { batchId });

        _logger.LogInformation("添加工序组 {ProcessName} → 批次 {BatchId}", request.ProcessName, batchId);

        return ToGroupDto(entity);
    }

    public async Task DeleteProcessGroupAsync(int groupId)
    {
        var entity = await _context.ProcessGroups.FindAsync(groupId);
        if (entity == null)
            throw new BusinessException($"工序组不存在 (Id={groupId})");

        var batchId = entity.ProductionBatchId;

        // 检查是否有生产记录或委外记录引用
        var hasRecord = await _context.ProductionRecords.AnyAsync(r => r.ProcessGroupId == groupId);
        if (hasRecord)
            throw new BusinessException($"工序组 (Id={groupId}) 已被生产记录引用，无法删除。请先删除相关生产记录后再试。");

        var hasOutsource = await _context.SectionOutsources.AnyAsync(s => s.ProcessGroupId == groupId);
        if (hasOutsource)
            throw new BusinessException($"工序组 (Id={groupId}) 已被委外发出记录引用，无法删除。请先删除相关委外记录后再试。");

        var hasPicklingInRecord = await _context.PicklingInRecords.AnyAsync(p => p.ProcessGroupId == groupId);
        if (hasPicklingInRecord)
            throw new BusinessException($"工序组 (Id={groupId}) 已被酸洗记录引用，无法删除。请先删除相关酸洗记录后再试。");

        _context.ProcessGroups.Remove(entity);
        await _context.SaveChangesAsync();

        // 工序组变更影响工段解析，刷新批次跟踪字段
        await _productionRecordService.BatchUpdateBatchTrackingAsync(new[] { batchId });

        _logger.LogInformation("删除工序组 (Id={GroupId})", groupId);
    }

    // ========== 查询 ==========

    public async Task<List<AvailableBatchDto>> GetAvailableBatchesAsync()
    {
        // 按 OutboundRecordId 粒度排除（新数据，有 OutboundRecordId 的记录）
        var linkedOutboundRecordIds = await _context.ProductionBatchInventories
            .Where(pbi => pbi.OutboundRecordId != null)
            .Select(pbi => pbi.OutboundRecordId!.Value)
            .Distinct()
            .ToListAsync();

        // 按 InventoryBatchId 粒度排除（旧数据，OutboundRecordId 为 null）
        var linkedInventoryBatchIds = await _context.ProductionBatchInventories
            .Where(pbi => pbi.OutboundRecordId == null)
            .Select(pbi => pbi.InventoryBatchId)
            .Distinct()
            .ToListAsync();

        var available = (await _context.OutboundRecords
            .Where(o => o.OutboundType == OutboundType.ProductionPick)
            .Join(_context.InventoryBatches,
                o => o.InventoryBatchId,
                ib => ib.Id,
                (o, ib) => new { o, ib })
            .Where(x => !linkedOutboundRecordIds.Contains(x.o.Id)
                     && !linkedInventoryBatchIds.Contains(x.ib.Id))
            .Join(_context.Warehouses,
                x => x.ib.WarehouseId,
                w => w.Id,
                (x, w) => new { x.o, x.ib, w })
            .OrderBy(x => x.ib.BatchNo)
            .Select(x => new
            {
                x.ib.Id,
                OutboundRecordId = x.o.Id,
                x.ib.BatchNo,
                x.ib.WarehouseId,
                WarehouseName = x.w.Name,
                x.ib.MaterialType,
                x.ib.InboundSource,
                x.ib.SourceName,
                x.ib.InboundDate,
                x.ib.HeatNo,
                x.o.OutboundQuantity,
                x.o.OutboundWeight,
                x.o.OutboundDate,
                OutboundRemark = x.o.Remark,
                WorkOrderNo = x.o.WorkOrderNo ?? x.ib.WorkOrderNo,
                x.ib.PlantGrade,
                x.ib.Specification,
                x.ib.LengthStatus,
                x.ib.UnitWeight
            })
            .ToListAsync())
            .Select(x => new AvailableBatchDto
            {
                Id = x.Id,
                OutboundRecordId = x.OutboundRecordId,
                BatchNo = x.BatchNo,
                WarehouseId = x.WarehouseId,
                WarehouseName = x.WarehouseName,
                MaterialType = string.IsNullOrEmpty(x.MaterialType) ? null : EnumHelper.TryParse<MaterialType>(x.MaterialType),
                InboundSource = string.IsNullOrEmpty(x.InboundSource) ? null : EnumHelper.TryParse<InboundSource>(x.InboundSource),
                SourceName = x.SourceName,
                InboundDate = x.InboundDate,
                HeatNo = x.HeatNo,
                OutboundQuantity = x.OutboundQuantity,
                OutboundWeight = x.OutboundWeight,
                OutboundDate = x.OutboundDate,
                OutboundRemark = x.OutboundRemark,
                WorkOrderNo = x.WorkOrderNo,
                PlantGrade = x.PlantGrade,
                Specification = x.Specification,
                LengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(x.LengthStatus),
                UnitWeight = x.UnitWeight
            })
            .ToList();

        return available;
    }

    // ========== 编号生成 ==========

    public async Task<string> GetNextBatchNoAsync()
    {
        return await GenerateBatchNoAsync();
    }

    // ========== 获取上批次工序组 ==========

    public async Task<List<CreateProcessGroupRequest>> GetLastBatchProcessGroupsAsync()
    {
        var lastBatch = await _context.ProductionBatches
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedTime)
            .FirstOrDefaultAsync();

        if (lastBatch == null)
            return new List<CreateProcessGroupRequest>();

        var groups = await _context.ProcessGroups
            .AsNoTracking()
            .Where(pg => pg.ProductionBatchId == lastBatch.Id)
            .OrderBy(pg => pg.SequenceNumber)
            .Select(pg => new CreateProcessGroupRequest
            {
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
            })
            .ToListAsync();

        return groups;
    }

    // ========== 按批次号获取工序组 ==========

    public async Task<List<CreateProcessGroupRequest>> GetProcessGroupsByBatchNoAsync(string batchNo)
    {
        var batch = await _context.ProductionBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BatchNo == batchNo);

        if (batch == null)
            return new List<CreateProcessGroupRequest>();

        var groups = await _context.ProcessGroups
            .AsNoTracking()
            .Where(pg => pg.ProductionBatchId == batch.Id)
            .OrderBy(pg => pg.SequenceNumber)
            .Select(pg => new CreateProcessGroupRequest
            {
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
            })
            .ToListAsync();

        return groups;
    }

    // ========== 打印 ==========

    public async Task<byte[]> PrintBatchAsync(int id)
    {
        var entity = await _context.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProcessGroups.OrderBy(pg => pg.SequenceNumber))
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (Id={id})");

        var groups = entity.ProcessGroups.Select(ToGroupDto).ToList();
        var sectionNameMap = await _sectionNameDisplay.GetSectionNameMapAsync();
        var processNameMap = await _processDefService.GetProcessNameMapAsync();

        var columns = new List<PrintColumnDef>
        {
            new() { Key = "BatchNo", Label = "生产编号" },
            new() { Key = "TagNo", Label = "挂牌号" },
            new() { Key = "CreatedTime", Label = "创建时间" },
            new() { Key = "WorkOrderNo", Label = "工单号" },
            new() { Key = "SalesOrderNo", Label = "订单号" },
            new() { Key = "ProductionMainNo", Label = "主号" },
            new() { Key = "ProductionSubNo", Label = "次号" },
            new() { Key = "ProductionType", Label = "生产类型" },
            new() { Key = "Status", Label = "状态" },
            new() { Key = "CurrentExecDate", Label = "截止执行日" },
            new() { Key = "CurrentGroupName", Label = "当前工序" },
            new() { Key = "CurrentSectionName", Label = "当前工段" },
            new() { Key = "CurrentEquipmentName", Label = "当前设备" },
            new() { Key = "CurrentOutsource", Label = "当前委外" },
            new() { Key = "CurrentSpec", Label = "当前规格" },
            new() { Key = "NextSectionName", Label = "下一工段" },
            new() { Key = "CorrespondingSpec", Label = "对应规格" },
            new() { Key = "NextProcess", Label = "下一工序" }
        };

        var items = new List<Dictionary<string, object>>
        {
            new()
            {
                ["BatchNo"] = entity.BatchNo,
                ["TagNo"] = entity.TagNo ?? "",
                ["CreatedTime"] = entity.CreatedTime,
                ["WorkOrderNo"] = entity.WorkOrderNo,
                ["SalesOrderNo"] = entity.SalesOrderNo,
                ["ProductionMainNo"] = entity.ProductionMainNo,
                ["ProductionSubNo"] = entity.ProductionSubNo ?? "",
                ["ProductionType"] = !string.IsNullOrEmpty(entity.ProductionType) && Enum.TryParse<ProductionType>(entity.ProductionType, out var pt) ? EnumHelper.GetDisplayName(pt) : (entity.ProductionType ?? ""),
                ["Status"] = EnumHelper.GetDisplayName(entity.Status),
                ["CurrentExecDate"] = entity.CurrentExecDate?.ToString("yyyy-MM-dd") ?? "",
                ["CurrentGroupName"] = ProcessDisplayText(entity.CurrentGroupName, processNameMap),
                ["CurrentSectionName"] = SectionDisplayText(entity.CurrentSectionName, sectionNameMap),
                ["CurrentEquipmentName"] = entity.CurrentEquipmentName ?? "",
                ["CurrentOutsource"] = entity.CurrentOutsource ?? "",
                ["CurrentSpec"] = entity.CurrentSpec ?? "",
                ["NextSectionName"] = SectionDisplayText(entity.NextSectionName, sectionNameMap),
                ["CorrespondingSpec"] = entity.CorrespondingSpec ?? "",
                ["NextProcess"] = ProcessDisplayText(entity.NextProcess, processNameMap)
            }
        };

        return TablePrintHelper.GeneratePdf($"生产批次 - {entity.BatchNo}", items, columns);
    }

    /// <summary>工段 Key/中文 → 打印显示中文（配置表优先，SectionKeys 兜底）</summary>
    private static string SectionDisplayText(string? keyOrName, IReadOnlyDictionary<string, string>? sectionNameMap)
    {
        if (!string.IsNullOrEmpty(keyOrName) && sectionNameMap != null && sectionNameMap.TryGetValue(keyOrName, out var cn))
            return cn;
        return SectionKeys.ToChinese(keyOrName) ?? "";
    }

    /// <summary>工序 Key/中文 → 打印显示中文（配置表优先，ProcessKeys 兜底）</summary>
    private static string ProcessDisplayText(string? keyOrName, IReadOnlyDictionary<string, string>? processNameMap)
    {
        if (!string.IsNullOrEmpty(keyOrName) && processNameMap != null && processNameMap.TryGetValue(keyOrName, out var cn))
            return cn;
        return ProcessKeys.ToChinese(keyOrName) ?? "";
    }

    public async Task<byte[]> PrintBatchAllAsync(BatchPrintAllRequest request)
    {
        var queryable = _context.ProductionBatches.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(request.Keyword))
        {
            var kw = request.Keyword;
            queryable = queryable.Where(b =>
                b.BatchNo.Contains(kw) ||
                b.WorkOrderNo.Contains(kw) ||
                b.SalesOrderNo.Contains(kw) ||
                b.ProductionMainNo.Contains(kw) ||
                (b.ProductionSubNo != null && b.ProductionSubNo.Contains(kw)) ||
                (b.TagNo != null && b.TagNo.Contains(kw)) ||
                b.CreatedBy.Contains(kw) ||
                (b.CurrentGroupName != null && b.CurrentGroupName.Contains(kw)) ||
                (b.CurrentSectionName != null && b.CurrentSectionName.Contains(kw)) ||
                (b.CurrentEquipmentName != null && b.CurrentEquipmentName.Contains(kw)) ||
                (b.CurrentOutsource != null && b.CurrentOutsource.Contains(kw)) ||
                (b.CurrentSpec != null && b.CurrentSpec.Contains(kw)) ||
                (b.NextSectionName != null && b.NextSectionName.Contains(kw)) ||
                (b.CorrespondingSpec != null && b.CorrespondingSpec.Contains(kw)) ||
                (b.NextProcess != null && b.NextProcess.Contains(kw)) ||
                b.ManufacturingItem.Contains(kw) ||
                (b.ProductionType != null && b.ProductionType.Contains(kw)) ||
                b.Salesman.Contains(kw) ||
                (b.EndCustomer != null && b.EndCustomer.Contains(kw)) ||
                b.MaterialName.Contains(kw) ||
                b.SettlementMethod.Contains(kw) ||
                b.StandardCode.Contains(kw) ||
                b.DeliveryState.Contains(kw) ||
                b.PlantGrade.Contains(kw) ||
                b.Specification.Contains(kw) ||
                b.LengthStatus.Contains(kw) ||
                b.TechnicalRequirements.Contains(kw) ||
                (b.ItemDetails != null && b.ItemDetails.Contains(kw)) ||
                (b.Remark != null && b.Remark.Contains(kw)) ||
                (b.QualityRemark != null && b.QualityRemark.Contains(kw)) ||
                (b.SourceHeatNo != null && b.SourceHeatNo.Contains(kw)) ||
                (b.SourceName != null && b.SourceName.Contains(kw)) ||
                (b.SourceBatchNo != null && b.SourceBatchNo.Contains(kw)) ||
                (b.SourceSpecification != null && b.SourceSpecification.Contains(kw)) ||
                (b.SourceMaterialType != null && b.SourceMaterialType.Contains(kw)) ||
                (b.SourceLengthStatus != null && b.SourceLengthStatus.Contains(kw)) ||
                (b.SolutionParams != null && b.SolutionParams.Contains(kw)) ||
                (b.UpdatedBy != null && b.UpdatedBy.Contains(kw)) ||
                (b.SourcePlantGrade != null && b.SourcePlantGrade.Contains(kw)) ||
                (b.SourceProductionNo != null && b.SourceProductionNo.Contains(kw)) ||
                (b.ManufacturingStatus != null && b.ManufacturingStatus.Contains(kw)) ||
                (b.SourceRemark != null && b.SourceRemark.Contains(kw)) ||
                (b.OrderItemIds != null && b.OrderItemIds.Contains(kw)));
        }
        if (!string.IsNullOrEmpty(request.WorkOrderNo))
            queryable = queryable.Where(b => b.WorkOrderNo.Contains(request.WorkOrderNo));
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<BatchStatus>(request.Status, out var batchStatus))
            queryable = queryable.Where(b => b.Status == batchStatus);
        if (!string.IsNullOrEmpty(request.TagNo))
            queryable = queryable.Where(b => b.TagNo != null && b.TagNo.Contains(request.TagNo));
        if (!string.IsNullOrEmpty(request.BatchNo))
            queryable = queryable.Where(b => b.BatchNo.Contains(request.BatchNo));
        if (!string.IsNullOrEmpty(request.SalesOrderNo))
            queryable = queryable.Where(b => b.SalesOrderNo.Contains(request.SalesOrderNo));
        if (!string.IsNullOrEmpty(request.ProductionMainNo))
            queryable = queryable.Where(b => b.ProductionMainNo.Contains(request.ProductionMainNo));
        if (!string.IsNullOrEmpty(request.ProductionSubNo))
            queryable = queryable.Where(b => b.ProductionSubNo != null && b.ProductionSubNo.Contains(request.ProductionSubNo));

        var entities = await queryable
            .OrderByDescending(b => b.CreatedTime)
            .ToListAsync();

        var columns = request.Columns?.Count > 0 ? request.Columns : GetDefaultBatchPrintColumns();
        var items = BuildBatchDictItems(entities, columns);
        return TablePrintHelper.GeneratePdf("生产批次列表", items, columns);
    }

    public async Task<byte[]> PrintBatchSelectedAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var entities = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .OrderBy(b => b.CreatedTime)
            .ToListAsync();

        if (entities.Count == 0)
            throw new BusinessException("未找到选中的批次数据");

        var cols = columns?.Count > 0 ? columns : GetDefaultBatchPrintColumns();
        var items = BuildBatchDictItems(entities, cols);
        return TablePrintHelper.GeneratePdf("生产批次列表", items, cols);
    }

    private static List<PrintColumnDef> GetDefaultBatchPrintColumns() => new()
    {
        new() { Key = "BatchNo", Label = "生产编号" },
        new() { Key = "TagNo", Label = "挂牌号" },
        new() { Key = "CreatedTime", Label = "创建时间" },
        new() { Key = "WorkOrderNo", Label = "工单号" },
        new() { Key = "SalesOrderNo", Label = "订单号" },
        new() { Key = "ProductionMainNo", Label = "主号" },
        new() { Key = "ProductionSubNo", Label = "次号" },
        new() { Key = "ProductionType", Label = "生产类型" },
        new() { Key = "Status", Label = "状态" },
        new() { Key = "CurrentExecDate", Label = "截止执行日" },
        new() { Key = "CurrentGroupName", Label = "当前工序" },
        new() { Key = "CurrentSectionName", Label = "当前工段" },
        new() { Key = "CurrentEquipmentName", Label = "当前设备" },
        new() { Key = "CurrentOutsource", Label = "当前委外" },
        new() { Key = "CurrentSpec", Label = "当前规格" },
        new() { Key = "NextSectionName", Label = "下一工段" },
        new() { Key = "CorrespondingSpec", Label = "对应规格" },
        new() { Key = "NextProcess", Label = "下一工序" }
    };

    private static List<Dictionary<string, object>> BuildBatchDictItems(List<ProductionBatch> entities, List<PrintColumnDef> columns)
    {
        return entities.Select(b =>
        {
            var dict = new Dictionary<string, object>();
            foreach (var col in columns)
            {
                dict[col.Key] = GetBatchFieldValue(b, col.Key);
            }
            return dict;
        }).ToList();
    }

    private static object GetBatchFieldValue(ProductionBatch b, string key)
    {
        // 使用 switch 表达式处理字段映射 + 枚举转换
        return (key switch
        {
            // 批次基本信息
            "BatchNo" => b.BatchNo,
            "TagNo" => (object?)b.TagNo ?? "",
            "Status" => EnumHelper.GetDisplayName(b.Status),
            "ProductionType" => TryGetEnumDisplay<ProductionType>(b.ProductionType),
            "ManufacturingItem" => TryGetEnumDisplay<MaterialType>(b.ManufacturingItem),
            "ProductionRatio" => b.ProductionRatio,
            "IsForceCompleted" => b.IsForceCompleted,
            "CurrentValidQty" => (object?)b.CurrentValidQty ?? DBNull.Value,
            "CurrentValidWeight" => (object?)b.CurrentValidWeight ?? DBNull.Value,
            "HasInputChange" => b.HasInputChange,
            "Remark" => (object?)b.Remark ?? "",
            "CreatedBy" => b.CreatedBy,
            "CreatedTime" => b.CreatedTime,
            "UpdatedTime" => b.UpdatedTime,
            "UpdatedBy" => b.UpdatedBy,
            "CurrentExecDate" => (object?)b.CurrentExecDate ?? DBNull.Value,
            "CurrentSectionCompleted" => (object?)b.CurrentSectionCompleted ?? DBNull.Value,
            "InspectionStage" => string.Equals(b.InspectionStage, nameof(InspectionType.PreInspection), StringComparison.OrdinalIgnoreCase)
                ? "预检"
                : string.Equals(b.InspectionStage, nameof(InspectionType.FormalInspection), StringComparison.OrdinalIgnoreCase)
                    ? "终检"
                    : "",
            "CutRequirement" => b.CutRequirement,
            "CutExecution" => (object?)b.CutExecution ?? DBNull.Value,
            "CutQuantity" => (object?)b.CutQuantity ?? DBNull.Value,
            "CutDoubt" => b.CutDoubt.HasValue ? EnumHelper.GetDisplayName(b.CutDoubt.Value) : "",
            "RemainingWorkDays" => b.RemainingWorkDays,
            "TotalWorkDays" => b.TotalWorkDays,

            // 工单信息
            "WorkOrderNo" => b.WorkOrderNo,
            "SalesOrderNo" => b.SalesOrderNo,
            "ProductionMainNo" => b.ProductionMainNo,
            "ProductionSubNo" => (object?)b.ProductionSubNo ?? "",
            "SignDate" => b.SignDate == default ? "" : b.SignDate.ToString("yyyy-MM-dd"),
            "Salesman" => b.Salesman,
            "EndCustomer" => (object?)b.EndCustomer ?? "",
            "DeliveryDate" => b.DeliveryDate == default ? "" : b.DeliveryDate.ToString("yyyy-MM-dd"),
            "DelayPenalty" => b.DelayPenalty,
            "MaterialName" => TryGetEnumDisplay<PipeManufacturingType>(b.MaterialName),
            "SettlementMethod" => TryGetEnumDisplay<SettlementMethod>(b.SettlementMethod),
            "StandardCode" => b.StandardCode,
            "DeliveryState" => TryGetEnumDisplay<DeliveryState>(b.DeliveryState),
            "ManufacturingStatus" => TryGetEnumDisplay<DeliveryState>(b.ManufacturingStatus),
            "PlantGrade" => b.PlantGrade,
            "Specification" => b.Specification,
            "LengthStatus" => TryGetEnumDisplay<Core.Enums.LengthStatus>(b.LengthStatus),
            "TotalQuantity" => b.TotalQuantity,
            "TotalMeters" => b.TotalMeters,
            "TotalWeight" => b.TotalWeight,
            "MinLength" => (object?)b.MinLength ?? DBNull.Value,
            "MaxLength" => (object?)b.MaxLength ?? DBNull.Value,
            "OuterDiameterNegative" => b.OuterDiameterNegative,
            "OuterDiameterPositive" => b.OuterDiameterPositive,
            "WallThicknessNegative" => b.WallThicknessNegative,
            "WallThicknessPositive" => b.WallThicknessPositive,
            "TotalItemCount" => b.TotalItemCount,
            "ItemDetails" => (object?)b.ItemDetails ?? "",
            "TechnicalRequirements" => TryGetEnumDisplay<RequirementType>(b.TechnicalRequirements),

            // 生产执行
            "CurrentGroupName" => (object?)b.CurrentGroupName ?? "",
            "CurrentSectionName" => (object?)b.CurrentSectionName ?? "",
            "CurrentEquipmentName" => (object?)b.CurrentEquipmentName ?? "",
            "CurrentOutsource" => (object?)b.CurrentOutsource ?? "",
            "CurrentSpec" => (object?)b.CurrentSpec ?? "",
            "NextSectionName" => (object?)b.NextSectionName ?? "",
            "CorrespondingSpec" => (object?)b.CorrespondingSpec ?? "",
            "NextProcess" => (object?)b.NextProcess ?? "",

            // 仓库信息
            "SourceBatchNo" => (object?)b.SourceBatchNo ?? "",
            "SourceMaterialType" => (object?)b.SourceMaterialType ?? "",
            "SourceName" => (object?)b.SourceName ?? "",
            "SourceHeatNo" => (object?)b.SourceHeatNo ?? "",
            "SourcePlantGrade" => (object?)b.SourcePlantGrade ?? "",
            "SourceSpecification" => (object?)b.SourceSpecification ?? "",
            "SourceLengthStatus" => TryGetEnumDisplay<Core.Enums.LengthStatus>(b.SourceLengthStatus),
            "SourceUnitWeight" => (object?)b.SourceUnitWeight ?? DBNull.Value,
            "InputType" => EnumHelper.GetDisplayName(b.InputType),
            "SourceProductionNo" => (object?)b.SourceProductionNo ?? "",
            "InputQuantity" => (object?)b.InputQuantity ?? DBNull.Value,
            "InputWeight" => (object?)b.InputWeight ?? DBNull.Value,
            "SourceRemark" => (object?)b.SourceRemark ?? DBNull.Value,
            "TheoreticalOutputQty" => (object?)b.TheoreticalOutputQty ?? DBNull.Value,
            "TheoreticalOutputWeight" => (object?)b.TheoreticalOutputWeight ?? DBNull.Value,
            "TheoreticalUnitWeight" => (object?)b.TheoreticalUnitWeight ?? DBNull.Value,

            // 质量
            "SolutionParams" => (object?)b.SolutionParams ?? "",
            "QualityRemark" => (object?)b.QualityRemark ?? "",

            // 默认
            _ => ""
        })!;
    }

    /// <summary>
    /// 尝试将字符串枚举值转为中文显示名，失败返回原始字符串
    /// </summary>
    private static string TryGetEnumDisplay<T>(string? value) where T : struct, Enum
    {
        return !string.IsNullOrEmpty(value) && Enum.TryParse<T>(value, out var result)
            ? EnumHelper.GetDisplayName(result)
            : (value ?? "");
    }

    public async Task<byte[]> PrintProcessCardAsync(ProcessCardPrintRequest request)
    {
        List<ProductionBatch> entities;

        if (request.Ids.Length > 0)
        {
            entities = await _context.ProductionBatches
                .AsNoTracking()
                .Include(b => b.ProcessGroups)
                .Where(b => request.Ids.Contains(b.Id))
                .OrderBy(b => b.CreatedTime)
                .ToListAsync();
        }
        else
        {
            entities = await _context.ProductionBatches
                .AsNoTracking()
                .Include(b => b.ProcessGroups)
                .OrderBy(b => b.CreatedTime)
                .ToListAsync();
        }

        if (entities.Count == 0)
            throw new BusinessException("未找到批次数据");

        var columns = await MergeProcessCardColumnsAsync(request.Columns);
        var style = await _processCardStyleDefinitionService.GetStyleMapAsync();
        return ProcessCardPrintHelper.GeneratePdf("工 艺 流 转 卡", entities, columns,
            sectionNameMap: await _sectionNameDisplay.GetSectionNameMapAsync(),
            processNameMap: await _processDefService.GetProcessNameMapAsync(),
            style: style);
    }

    /// <summary>
    /// 工艺卡列定义与格式设置配置表合并：以数据库配置（ProcessCardColumnDefinition）为权威覆盖请求列定义，
    /// 使格式设置（是否启用/所属行/列顺序/列权重）保存即生效。未配置的列（如新启用工段）保留请求值并补默认权重。
    /// </summary>
    private async Task<List<ProcessCardColumnDef>> MergeProcessCardColumnsAsync(List<ProcessCardColumnDef> requestColumns)
    {
        if (requestColumns == null || requestColumns.Count == 0)
            return requestColumns ?? new List<ProcessCardColumnDef>();

        var configMap = await _processCardColumnDefinitionService.GetConfigMapAsync();
        var result = new List<ProcessCardColumnDef>(requestColumns.Count);
        foreach (var col in requestColumns)
        {
            if (configMap.TryGetValue($"{col.BlockKey}|{col.Key}", out var cfg))
            {
                // 有配置行：以数据库配置为权威（Label/Visible/RowIndex/ColumnIndex/ColumnWeight 全覆盖）
                result.Add(new ProcessCardColumnDef
                {
                    BlockKey = col.BlockKey,
                    Key = col.Key,
                    Label = cfg.Label,
                    Visible = cfg.Visible,
                    RowIndex = cfg.RowIndex,
                    ColumnIndex = cfg.ColumnIndex,
                    ColumnWeight = cfg.ColumnWeight
                });
            }
            else
            {
                // 无配置行（如新启用工段）：保留请求值，列宽权重补 ProcessCardLayoutDefaults 兜底
                var weight = col.ColumnWeight > 0 ? col.ColumnWeight : ProcessCardLayoutDefaults.GetDefaultWeight(col.BlockKey, col.Key);
                result.Add(new ProcessCardColumnDef
                {
                    BlockKey = col.BlockKey,
                    Key = col.Key,
                    Label = col.Label,
                    Visible = col.Visible,
                    RowIndex = col.RowIndex > 0 ? col.RowIndex : 1,
                    ColumnIndex = col.ColumnIndex,
                    ColumnWeight = weight
                });
            }
        }
        return result;
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("BatchService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            // 注意：枚举列（ProductionType/Status/MaterialName 等）不在此处返回，
            // 由前端 EnumOptions fallback 直接提供带中文 Display 的选项，避免映射丢失。
            var results = await _context.ProductionBatches
                .AsNoTracking()
                .Select(b => new
                {
                    b.BatchNo,
                    b.TagNo,
                    b.WorkOrderNo,
                    b.SalesOrderNo,
                    b.ProductionMainNo,
                    b.ProductionSubNo,
                    b.CurrentExecDate,
                    b.CurrentGroupName,
                    b.CurrentSectionName,
                    b.CurrentEquipmentName,
                    b.CurrentOutsource,
                    b.CurrentSpec,
                    b.NextSectionName,
                    b.CorrespondingSpec,
                    b.NextProcess,
                    b.SignDate,
                    b.Salesman,
                    b.EndCustomer,
                    b.DeliveryDate,
                    b.StandardCode,
                    b.PlantGrade,
                    b.Specification,
                    b.CreatedBy,
                    b.Remark,
                    b.UpdatedBy,
                    b.SourceBatchNo,
                    b.SourceProductionNo,
                    b.SourcePlantGrade,
                    b.SourceName,
                    b.SourceHeatNo,
                    b.SourceSpecification,
                    b.SourceLengthStatus,
                    b.SolutionParams,
                    b.QualityRemark
                })
                .ToListAsync();

            return new Dictionary<string, List<string>>
            {
                ["BatchNo"] = results.Select(x => x.BatchNo).Distinct().OrderBy(x => x).ToList(),
                ["TagNo"] = results.Select(x => x.TagNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["WorkOrderNo"] = results.Select(x => x.WorkOrderNo).Distinct().OrderBy(x => x).ToList(),
                ["SalesOrderNo"] = results.Select(x => x.SalesOrderNo).Distinct().OrderBy(x => x).ToList(),
                ["ProductionMainNo"] = results.Select(x => x.ProductionMainNo).Distinct().OrderBy(x => x).ToList(),
                ["ProductionSubNo"] = results.Select(x => x.ProductionSubNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["CurrentExecDate"] = results.Where(x => x.CurrentExecDate.HasValue)
                    .Select(x => x.CurrentExecDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["CurrentGroupName"] = results.Select(x => x.CurrentGroupName).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["CurrentSectionName"] = results.Select(x => x.CurrentSectionName).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["CurrentEquipmentName"] = results.Select(x => x.CurrentEquipmentName).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["CurrentOutsource"] = results.Select(x => x.CurrentOutsource).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["CurrentSpec"] = results.Select(x => x.CurrentSpec).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["NextSectionName"] = results.Select(x => x.NextSectionName).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["CorrespondingSpec"] = results.Select(x => x.CorrespondingSpec).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["NextProcess"] = results.Select(x => x.NextProcess).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["SignDate"] = results.Select(x => x.SignDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["Salesman"] = results.Select(x => x.Salesman).Distinct().OrderBy(x => x).ToList(),
                ["EndCustomer"] = results.Select(x => x.EndCustomer).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["DeliveryDate"] = results.Select(x => x.DeliveryDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
                ["StandardCode"] = results.Select(x => x.StandardCode).Distinct().OrderBy(x => x).ToList(),
                ["PlantGrade"] = results.Select(x => x.PlantGrade).Distinct().OrderBy(x => x).ToList(),
                ["Specification"] = results.Select(x => x.Specification).Distinct().OrderBy(x => x).ToList(),
                ["CreatedBy"] = results.Select(x => x.CreatedBy).Distinct().OrderBy(x => x).ToList(),
                ["Remark"] = results.Select(x => x.Remark).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["UpdatedBy"] = results.Select(x => x.UpdatedBy).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["SourceBatchNo"] = results.Select(x => x.SourceBatchNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["SourceProductionNo"] = results.Select(x => x.SourceProductionNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["SourcePlantGrade"] = results.Select(x => x.SourcePlantGrade).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["SourceName"] = results.Select(x => x.SourceName).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["SourceHeatNo"] = results.Select(x => x.SourceHeatNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["SourceSpecification"] = results.Select(x => x.SourceSpecification).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["SourceLengthStatus"] = results.Select(x => x.SourceLengthStatus).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["SolutionParams"] = results.Select(x => x.SolutionParams).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
                ["QualityRemark"] = results.Select(x => x.QualityRemark).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            };
        }) ?? new Dictionary<string, List<string>>();
    }

    // ========== 辅助方法 ==========

    private static int CalculateProductionRatio(CreateProductionBatchRequest request, WoEntity? workOrder)
    {
        // 仅在定尺(Fixed)状态下计算
        var lengthStatus = request.LengthStatus?.ToString() ?? workOrder?.LengthStatus.ToString() ?? "";
        if (lengthStatus != LengthStatus.Fixed.ToString())
            return request.ProductionRatio;

        // 投料单重
        if (request.InputWeight == null || request.InputQuantity == null || request.InputQuantity <= 0)
            return request.ProductionRatio;
        var unitInputWeight = request.InputWeight.Value / (decimal)request.InputQuantity.Value;

        // 工单单重
        var totalWeight = request.TotalWeight ?? workOrder?.TotalWeight;
        var totalQty = request.TotalQuantity ?? workOrder?.TotalQuantity;
        if (totalWeight == null || totalQty == null || totalQty <= 0)
            return request.ProductionRatio;
        var unitWorkWeight = totalWeight.Value / (decimal)totalQty.Value;

        if (unitWorkWeight <= 0)
            return request.ProductionRatio;

        // rounddown
        return (int)Math.Floor(unitInputWeight / unitWorkWeight);
    }

    private async Task<string> GenerateBatchNoAsync()
    {
        var now = DateTime.Now;
        var prefix = now.ToString("yyMM");
        var batchMaxSequence = (int)await GetConfigAsync("DefaultValue", "BatchMaxSequence", 9999m);

        // 查找当月最大序号
        var maxNo = await _context.ProductionBatches
            .Where(b => b.BatchNo.StartsWith(prefix))
            .OrderByDescending(b => b.BatchNo)
            .Select(b => b.BatchNo)
            .FirstOrDefaultAsync();

        int nextSeq = 1;
        if (!string.IsNullOrEmpty(maxNo) && maxNo.Length >= 9)
        {
            var seqStr = maxNo[5..9]; // 取后4位
            if (int.TryParse(seqStr, out var lastSeq))
                nextSeq = lastSeq + 1;
        }

        if (nextSeq > batchMaxSequence)
            throw new BusinessException($"当月生产编号已用尽（最大序号 {batchMaxSequence}）");

        return $"{prefix}-{nextSeq:D4}";
    }

    public async Task<List<ForcedCompletedInspectionBatchDto>> GetForcedCompletedInspectionBatchesAsync()
    {
        // 成检到料 IsForceCompleted=true，且批次仍处于「成检」（InFinalInspection）状态
        // 已转「完成」的批次（状态脱离成检）自然不在结果中，通知自动消失
        var rows = await _context.MaterialReceiveChecks
            .AsNoTracking()
            .Where(rc => rc.IsForceCompleted)
            .Join(_context.ProductionBatches,
                  rc => rc.ProductionBatchId,
                  b => b.Id,
                  (rc, b) => new { rc, b })
            .Where(x => x.b.Status == BatchStatus.InFinalInspection)
            .Select(x => new
            {
                x.b.Id,
                x.b.BatchNo,
                x.b.WorkOrderNo,
                x.rc.InspectionType,
                x.rc.ReceiveDate,
                x.rc.ProcessName
            })
            .OrderBy(x => x.BatchNo)
            .ToListAsync();

        return rows.Select(x => new ForcedCompletedInspectionBatchDto
        {
            BatchId = x.Id,
            BatchNo = x.BatchNo ?? "-",
            WorkOrderNo = x.WorkOrderNo ?? "-",
            InspectionType = x.InspectionType,
            InspectionTypeDisplay = x.InspectionType != null && Enum.TryParse<InspectionType>(x.InspectionType, out var t)
                ? EnumHelper.GetDisplayName(t)
                : null,
            ReceiveDate = x.ReceiveDate,
            ProcessName = x.ProcessName
        }).ToList();
    }

    public async Task<List<DefectRateBatchDto>> GetDefectRateAlertsAsync()
    {
        // 过程检验按批次分组，聚合次品支数和检验支数，取最新检验时间
        var defectGroups = await _context.ProcessInspections
            .AsNoTracking()
            .Where(p => p.Quantity.HasValue && p.Quantity.Value > 0)
            .GroupBy(p => p.ProductionBatchId)
            .Select(g => new
            {
                ProductionBatchId = g.Key,
                TotalDefectQty = (g.Sum(p => p.DefectReworkQuantity ?? 0)
                                + g.Sum(p => p.DefectWarehouseQuantity ?? 0)
                                + g.Sum(p => p.DefectScrapQuantity ?? 0)),
                TotalInspectionQty = g.Sum(p => p.Quantity ?? 0),
                MaxInspectionTime = g.Max(p => (DateTimeOffset?)p.CreatedTime)
            })
            .Where(x => x.TotalInspectionQty > 0
                     && (decimal)x.TotalDefectQty / (decimal)x.TotalInspectionQty > 0.03m)
            .ToListAsync();

        if (defectGroups.Count == 0)
            return new List<DefectRateBatchDto>();

        var batchIds = defectGroups.Select(x => x.ProductionBatchId).ToList();

        // 关联批次表获取批次信息（排除已完成批次）
        var batches = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => batchIds.Contains(b.Id) && b.Status != BatchStatus.Completed)
            .Select(b => new { b.Id, b.BatchNo, b.WorkOrderNo, b.HasInputChange, b.UpdatedTime })
            .ToListAsync();

        var batchMap = batches.ToDictionary(b => b.Id, b => b);

        // 消除逻辑：仅当 HasInputChange==true 且批次最后更新时间 >= 最新检验时间 → 已处理，不展示
        var result = defectGroups
            .Where(x => batchMap.ContainsKey(x.ProductionBatchId))
            .Where(x =>
            {
                var b = batchMap[x.ProductionBatchId];
                return b.HasInputChange != true || b.UpdatedTime < x.MaxInspectionTime;
            })
            .Select(x =>
            {
                var b = batchMap[x.ProductionBatchId];
                return new DefectRateBatchDto
                {
                    BatchId = x.ProductionBatchId,
                    BatchNo = b.BatchNo,
                    WorkOrderNo = b.WorkOrderNo ?? "-",
                    DefectRate = Math.Round((decimal)x.TotalDefectQty / (decimal)x.TotalInspectionQty * 100, 1)
                };
            })
            .OrderByDescending(d => d.DefectRate)
            .ToList();

        _logger.LogInformation("缺陷率预警查询完成: 发现 {Count} 个批次超阈值（已排除已处理批次）", result.Count);

        return result;
    }

    public async Task<int> PopulateManufacturingStatusAsync()
    {
        var batches = await _context.ProductionBatches
            .Where(b => string.IsNullOrEmpty(b.ManufacturingStatus) && !string.IsNullOrEmpty(b.DeliveryState))
            .ToListAsync();

        foreach (var batch in batches)
        {
            batch.ManufacturingStatus = batch.DeliveryState;
        }

        var count = await _context.SaveChangesAsync();
        _logger.LogInformation("批量回填制造状态完成: 更新 {Count} 个批次的 ManufacturingStatus", count);
        return count;
    }

    public async Task<List<BatchWorkOrderMismatchDto>> VerifyWorkOrderNosAsync()
    {
        // 获取所有生产批次中非空的工单号（排除NotWorkOrder标记）
        var batchWorkOrderNos = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => !string.IsNullOrEmpty(b.WorkOrderNo) && b.WorkOrderNo != NotWorkOrder)
            .Select(b => new { b.Id, b.BatchNo, b.WorkOrderNo })
            .ToListAsync();

        if (batchWorkOrderNos.Count == 0)
            return new List<BatchWorkOrderMismatchDto>();

        // 获取所有存在的工单号
        var existingWorkOrderNos = await _context.WorkOrders
            .AsNoTracking()
            .Select(wo => wo.WorkOrderNo)
            .Distinct()
            .ToListAsync();

        var existingSet = new HashSet<string>(existingWorkOrderNos, StringComparer.OrdinalIgnoreCase);

        // 找出批次引用了但工单表中不存在的工单号
        var mismatches = batchWorkOrderNos
            .Where(b => !existingSet.Contains(b.WorkOrderNo))
            .Select(b => new BatchWorkOrderMismatchDto
            {
                BatchId = b.Id,
                BatchNo = b.BatchNo,
                WorkOrderNo = b.WorkOrderNo
            })
            .ToList();

        _logger.LogInformation("工单号验证完成: 共检查 {Total} 个批次, 发现 {Mismatch} 个不匹配",
            batchWorkOrderNos.Count, mismatches.Count);

        return mismatches;
    }

    private static ProductionBatchDetailDto ToDetailDto(ProductionBatch entity)
    {
        return new ProductionBatchDetailDto
        {
            Id = entity.Id,
            BatchNo = entity.BatchNo,
            Status = entity.Status,
            TagNo = entity.TagNo,
            ProductionType = EnumHelper.TryParse<MES.Core.Enums.ProductionType>(entity.ProductionType),
            ManufacturingItem = !string.IsNullOrEmpty(entity.ManufacturingItem) ? EnumHelper.TryParse<MaterialType>(entity.ManufacturingItem) ?? default : default,
            ProductionRatio = entity.ProductionRatio,
            IsForceCompleted = entity.IsForceCompleted,
            QualityRemark = entity.QualityRemark,
            SolutionParams = entity.SolutionParams,
            CurrentExecDate = entity.CurrentExecDate,
            CurrentGroupName = entity.CurrentGroupName,
            CurrentSectionName = entity.CurrentSectionName,
            CurrentEquipmentName = entity.CurrentEquipmentName,
            CurrentOutsource = entity.CurrentOutsource,
            CurrentSectionCompleted = entity.CurrentSectionCompleted,
            HasInputChange = entity.HasInputChange,
            CurrentSpec = entity.CurrentSpec,
            NextSectionName = entity.NextSectionName,
            CorrespondingSpec = entity.CorrespondingSpec,
            NextProcess = entity.NextProcess,
            RemainingWorkDays = entity.RemainingWorkDays,
            TotalWorkDays = entity.TotalWorkDays,
            Remark = entity.Remark,

            // 工单冗余
            WorkOrderNo = entity.WorkOrderNo,
            SalesOrderNo = entity.SalesOrderNo,
            ProductionMainNo = entity.ProductionMainNo,
            ProductionSubNo = entity.ProductionSubNo,
            OrderItemIds = entity.OrderItemIds,
            SignDate = entity.SignDate,
            Salesman = entity.Salesman,
            EndCustomer = entity.EndCustomer,
            DeliveryDate = entity.DeliveryDate,
            DelayPenalty = entity.DelayPenalty,
            MaterialName = entity.MaterialName,
            SettlementMethod = !string.IsNullOrEmpty(entity.SettlementMethod) ? EnumHelper.TryParse<SettlementMethod>(entity.SettlementMethod) ?? default : default,
            StandardCode = entity.StandardCode,
            DeliveryState = !string.IsNullOrEmpty(entity.DeliveryState) ? EnumHelper.TryParse<DeliveryState>(entity.DeliveryState) ?? default : default,
            ManufacturingStatus = !string.IsNullOrEmpty(entity.ManufacturingStatus) && EnumHelper.TryParse<DeliveryState>(entity.ManufacturingStatus) is { } ms ? ms : null,
            PlantGrade = entity.PlantGrade,
            Specification = entity.Specification,
            OuterDiameterNegative = entity.OuterDiameterNegative,
            OuterDiameterPositive = entity.OuterDiameterPositive,
            WallThicknessNegative = entity.WallThicknessNegative,
            WallThicknessPositive = entity.WallThicknessPositive,
            LengthStatus = !string.IsNullOrEmpty(entity.LengthStatus) ? EnumHelper.TryParse<LengthStatus>(entity.LengthStatus) ?? default : default,
            MinLength = entity.MinLength,
            MaxLength = entity.MaxLength,
            TotalQuantity = entity.TotalQuantity,
            TotalMeters = entity.TotalMeters,
            TotalWeight = entity.TotalWeight,
            ProductUnitWeight = entity.ProductUnitWeight,
            TotalItemCount = entity.TotalItemCount,
            ItemDetails = entity.ItemDetails,
            TechnicalRequirements = entity.TechnicalRequirements,

            // 仓库冗余
            SourceBatchNo = entity.SourceBatchNo,
            SourceMaterialType = !string.IsNullOrEmpty(entity.SourceMaterialType) ? EnumHelper.TryParse<MaterialType>(entity.SourceMaterialType) : null,
            SourceName = entity.SourceName,
            SourceHeatNo = entity.SourceHeatNo,
            SourcePlantGrade = entity.SourcePlantGrade,
            SourceSpecification = entity.SourceSpecification,
            SourceLengthStatus = EnumHelper.TryParse<MES.Core.Enums.LengthStatus>(entity.SourceLengthStatus),
            SourceUnitWeight = entity.SourceUnitWeight,
            InputQuantity = entity.InputQuantity,
            InputWeight = entity.InputWeight,
            InputType = entity.InputType,
            SourceRemark = entity.SourceRemark,
            SourceProductionNo = entity.SourceProductionNo,
            CurrentValidQty = entity.CurrentValidQty,
            CurrentValidWeight = entity.CurrentValidWeight,
            TheoreticalOutputQty = entity.TheoreticalOutputQty,
            TheoreticalOutputWeight = entity.TheoreticalOutputWeight,
            TheoreticalUnitWeight = entity.TheoreticalUnitWeight,

            // 成检附加
            InspectionStage = entity.InspectionStage,

            // 成切跟踪
            CutRequirement = entity.CutRequirement,
            CutExecution = entity.CutExecution,
            CutQuantity = entity.CutQuantity,
            CutDoubt = entity.CutDoubt,

            // 审计
            CreatedTime = entity.CreatedTime,
            CreatedBy = entity.CreatedBy,
            UpdatedTime = entity.UpdatedTime,
            UpdatedBy = entity.UpdatedBy,

            RowVersion = entity.RowVersion,

            ProcessGroups = entity.ProcessGroups?.Select(ToGroupDto).ToList() ?? new(),

            SourceItems = entity.ProductionBatchInventories?.Select(pbi => new SourceBatchItemDto
            {
                InventoryBatchId = pbi.InventoryBatchId,
                OutboundRecordId = pbi.OutboundRecordId,
                BatchNo = pbi.InventoryBatch?.BatchNo ?? "",
                HeatNo = pbi.InventoryBatch?.HeatNo,
                PlantGrade = pbi.InventoryBatch?.PlantGrade,
                Specification = pbi.InventoryBatch?.Specification,
                MaterialType = EnumHelper.TryParse<MES.Core.Enums.MaterialType>(pbi.InventoryBatch?.MaterialType),
                SourceName = pbi.InventoryBatch?.SourceName,
                WarehouseName = pbi.InventoryBatch?.Warehouse?.Name ?? "",
                InputQuantity = pbi.InputQuantity,
                InputWeight = pbi.InputWeight
            }).ToList() ?? new()
        };
    }

    private static ProcessGroupDto ToGroupDto(ProcessGroup entity)
    {
        return new ProcessGroupDto
        {
            Id = entity.Id,
            ProductionBatchId = entity.ProductionBatchId,
            SequenceNumber = entity.SequenceNumber,
            ProcessName = entity.ProcessName,
            ManufacturingSpec = entity.ManufacturingSpec,
            OuterDiameterTolerance = entity.OuterDiameterTolerance,
            WallThicknessTolerance = entity.WallThicknessTolerance,
            ManufacturingLength = entity.ManufacturingLength,
            CuttingTreatment = entity.CuttingTreatment,
            Remark = entity.Remark,
            ColdRollDraw = entity.ColdRollDraw,
            OilPipeCut = entity.OilPipeCut,
            Degrease = entity.Degrease,
            EmulsionWash = entity.EmulsionWash,
            UltrasonicWash = entity.UltrasonicWash,
            ClothPolish = entity.ClothPolish,
            BrightAnnealing = entity.BrightAnnealing,
            Solution = entity.Solution,
            Straighten = entity.Straighten,
            Cut = entity.Cut,
            ThicknessMeasure = entity.ThicknessMeasure,
            Pickle = entity.Pickle,
            OuterPolish = entity.OuterPolish,
            InnerPolish = entity.InnerPolish,
            InnerGrinding = entity.InnerGrinding,
            OuterSpotGrinding = entity.OuterSpotGrinding,
            SandBlasting = entity.SandBlasting,
            ShotBlasting = entity.ShotBlasting,
            Inspection = entity.Inspection,
            WeldingHead = entity.WeldingHead,
            Welding = entity.Welding,
            Lubrication = entity.Lubrication,
            Packing = entity.Packing,
            Warehouse = entity.Warehouse,
            Extra1 = entity.Extra1,
            Extra2 = entity.Extra2,
            CreatedTime = entity.CreatedTime,
            CreatedBy = entity.CreatedBy
        };
    }

    /// <summary>
    /// 工序组数值验证：至少1个工段有值、同组不重复、全局从1开始连续且不重复
    /// </summary>
    private static void ValidateProcessGroupValues(List<CreateProcessGroupRequest> processGroups)
    {
        var allValues = new List<int>();
        var nameSpecCombos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < processGroups.Count; i++)
        {
            var pg = processGroups[i];
            var values = new List<int?>
            {
                pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.EmulsionWash,
                pg.UltrasonicWash, pg.ClothPolish, pg.BrightAnnealing, pg.Solution,
                pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                pg.OuterPolish, pg.InnerPolish, pg.InnerGrinding, pg.OuterSpotGrinding,
                pg.SandBlasting, pg.ShotBlasting, pg.Inspection, pg.WeldingHead,
                pg.Welding, pg.Lubrication, pg.Packing, pg.Warehouse,
                pg.Extra1, pg.Extra2
            };
            var nonNullValues = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();

            if (nonNullValues.Count == 0)
                throw new BusinessException($"工序组第{i + 1}行：至少需要1个工段有值");

            if (nonNullValues.Distinct().Count() != nonNullValues.Count)
                throw new BusinessException($"工序组第{i + 1}行：工段数值不能重复");

            // 工序名称不为空时制造规格必填
            if (!string.IsNullOrWhiteSpace(pg.ProcessName) && string.IsNullOrWhiteSpace(pg.ManufacturingSpec))
                throw new BusinessException($"工序组第{i + 1}行：工序名称「{pg.ProcessName}」已填写，制造规格不能为空");

            // 工序名称+制造规格在批次内唯一
            if (!string.IsNullOrWhiteSpace(pg.ProcessName))
            {
                var comboKey = $"{pg.ProcessName}|{pg.ManufacturingSpec ?? ""}";
                if (!nameSpecCombos.Add(comboKey))
                    throw new BusinessException($"工序组第{i + 1}行：工序名称「{pg.ProcessName}」+制造规格「{pg.ManufacturingSpec ?? ""}」组合已在其他工序组中存在");
            }

            // 检查跨工序组重复
            foreach (var v in nonNullValues)
            {
                if (allValues.Contains(v))
                    throw new BusinessException($"工段数值{v}在多个工序组中重复，同一批次中每个工段值必须唯一");
                allValues.Add(v);
            }
        }

        // 全局连续性检查：从1开始连续
        if (allValues.Count > 0)
        {
            allValues.Sort();
            if (allValues[0] != 1)
                throw new BusinessException($"工段数值必须从1开始（当前最小值为{allValues[0]}）");
            for (int i = 1; i < allValues.Count; i++)
            {
                if (allValues[i] != allValues[i - 1] + 1)
                    throw new BusinessException($"工段数值必须连续（1,2,3...），缺失值: {allValues[i - 1] + 1}");
            }
        }
    }

    #region 状态校验

    /// <summary>批次状态流转验证</summary>
    private static bool CanTransitionTo(BatchStatus current, BatchStatus target)
    {
        if (current == target) return true;
        return current switch
        {
            BatchStatus.None => target is BatchStatus.InProgress,
            BatchStatus.InProgress => target is BatchStatus.InFinalInspection or BatchStatus.Suspended or BatchStatus.Completed,
            BatchStatus.InFinalInspection => target is BatchStatus.Completed or BatchStatus.Suspended,
            BatchStatus.Suspended => target is BatchStatus.InProgress or BatchStatus.InFinalInspection or BatchStatus.Completed,
            BatchStatus.Completed => target is BatchStatus.InProgress,
            _ => false
        };
    }

    #endregion
}
