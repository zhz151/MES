using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Core.Constants;
using MES.Data;
using MES.Data.Entities;
using WoEntity = MES.Data.Entities.WorkOrder;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services;

public class BatchService : IBatchService
{
    /// <summary>无对应工单时的工单号占位符</summary>
    private const string NotWorkOrder = "非工单";

    private readonly AppDbContext _context;
    private readonly ILogger<BatchService> _logger;
    private readonly IProductionRecordService _productionRecordService;
    private readonly IConfigParameterService _configService;

    public BatchService(AppDbContext context, ILogger<BatchService> logger, IProductionRecordService productionRecordService, IConfigParameterService configService)
    {
        _context = context;
        _logger = logger;
        _productionRecordService = productionRecordService;
        _configService = configService;
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
                (b.Remark != null && b.Remark.Contains(kw)) ||
                (b.QualityRemark != null && b.QualityRemark.Contains(kw)) ||
                (b.SourceHeatNo != null && b.SourceHeatNo.Contains(kw)) ||
                (b.SourceName != null && b.SourceName.Contains(kw)) ||
                (b.SourceBatchNo != null && b.SourceBatchNo.Contains(kw)) ||
                (b.SourceSpecification != null && b.SourceSpecification.Contains(kw)) ||
                (b.SourceMaterialType != null && b.SourceMaterialType.Contains(kw)) ||
                (b.SourceLengthStatus != null && b.SourceLengthStatus.Contains(kw)) ||
                (b.InboundSource != null && b.InboundSource.Contains(kw)) ||
                (b.SolutionParams != null && b.SolutionParams.Contains(kw)));
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

        // 处理 Salesman/EndCustomer 筛选（来自 CustomerProfile，非 ProductionBatch 快照）
        if (query.Filters != null)
        {
            var salesmanFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("Salesman", StringComparison.OrdinalIgnoreCase));
            if (salesmanFilter != null && salesmanFilter.Values?.Count > 0)
            {
                var salesmanValues = salesmanFilter.Values;
                var matchedOrderNos = _context.SalesOrders
                    .AsNoTracking()
                    .Include(so => so.Customer)
                    .Where(so => so.Customer != null && salesmanValues.Contains(so.Customer.Salesman))
                    .Select(so => so.OrderNumber);
                queryable = queryable.Where(b => matchedOrderNos.Contains(b.SalesOrderNo));
                query.Filters.Remove(salesmanFilter);
            }

            var endCustomerFilter = query.Filters.FirstOrDefault(f => f.Field.Equals("EndCustomer", StringComparison.OrdinalIgnoreCase));
            if (endCustomerFilter != null && endCustomerFilter.Values?.Count > 0)
            {
                var endCustomerValues = endCustomerFilter.Values;
                var matchedOrderNos = _context.SalesOrders
                    .AsNoTracking()
                    .Include(so => so.Customer)
                    .Where(so => so.Customer != null && so.Customer.EndCustomer != null && endCustomerValues.Contains(so.Customer.EndCustomer))
                    .Select(so => so.OrderNumber);
                queryable = queryable.Where(b => matchedOrderNos.Contains(b.SalesOrderNo));
                query.Filters.Remove(endCustomerFilter);
            }
        }

        // 通用筛选
        queryable = queryable.ApplyFilters(query.Filters);

        // 排序
        queryable = queryable.ApplySort(query.SortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(b => new ProductionBatchListDto
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
                ProductionType = b.ProductionType,
                ManufacturingItem = b.ManufacturingItem,
                Status = b.Status.ToString(),
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
                SignDate = b.SignDate,
                Salesman = b.Salesman,
                EndCustomer = b.EndCustomer,
                DeliveryDate = b.DeliveryDate,
                DelayPenalty = b.DelayPenalty,
                MaterialName = b.MaterialName,
                SettlementMethod = b.SettlementMethod,
                StandardCode = b.StandardCode,
                DeliveryState = b.DeliveryState,
                PlantGrade = b.PlantGrade,
                Specification = b.Specification,
                LengthStatus = b.LengthStatus,
                TotalQuantity = b.TotalQuantity,
                TotalMeters = b.TotalMeters,
                TotalWeight = b.TotalWeight,
                TechnicalRequirements = b.TechnicalRequirements,
                Remark = b.Remark,
                SourceHeatNo = b.SourceHeatNo,
                TotalItemCount = b.TotalItemCount,
                SourceSpecification = b.SourceSpecification,
                InputQuantity = b.InputQuantity,
                InputWeight = b.InputWeight,
                SolutionParams = b.SolutionParams,
                QualityRemark = b.QualityRemark,
                SourceMaterialType = b.SourceMaterialType,
                SourceName = b.SourceName,
                InboundDate = b.InboundDate,
                ValidInputQuestion = b.ValidInputQuestion
            })
            .ToListAsync();

        // ========== 从 CustomerProfile 覆盖 Salesman/EndCustomer ==========
        await PatchCustomerFieldsAsync(items);

        return new PagedResult<ProductionBatchListDto>
        {
            Items = items,
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
            .Select(b => new ProductionBatchListDto
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
                ProductionType = b.ProductionType,
                ManufacturingItem = b.ManufacturingItem,
                Status = b.Status.ToString(),
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
                SignDate = b.SignDate,
                Salesman = b.Salesman,
                EndCustomer = b.EndCustomer,
                DeliveryDate = b.DeliveryDate,
                DelayPenalty = b.DelayPenalty,
                MaterialName = b.MaterialName,
                SettlementMethod = b.SettlementMethod,
                StandardCode = b.StandardCode,
                DeliveryState = b.DeliveryState,
                PlantGrade = b.PlantGrade,
                Specification = b.Specification,
                LengthStatus = b.LengthStatus,
                TotalQuantity = b.TotalQuantity,
                TotalMeters = b.TotalMeters,
                TotalWeight = b.TotalWeight,
                TechnicalRequirements = b.TechnicalRequirements,
                Remark = b.Remark,
                SourceHeatNo = b.SourceHeatNo,
                TotalItemCount = b.TotalItemCount,
                SourceSpecification = b.SourceSpecification,
                InputQuantity = b.InputQuantity,
                InputWeight = b.InputWeight,
                SolutionParams = b.SolutionParams,
                QualityRemark = b.QualityRemark,
                SourceMaterialType = b.SourceMaterialType,
                SourceName = b.SourceName,
                InboundDate = b.InboundDate,
                ValidInputQuestion = b.ValidInputQuestion
            })
            .ToListAsync();

        // ========== 从 CustomerProfile 覆盖 Salesman/EndCustomer ==========
        await PatchCustomerFieldsAsync(items);

        return items;
    }

    public async Task<ProductionBatchDetailDto> GetByIdAsync(int id)
    {
        var entity = await _context.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProcessGroups.OrderBy(pg => pg.SequenceNumber))
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (Id={id})");

        var dto = ToDetailDto(entity);

        // 从 CustomerProfile 取最新 Salesman/EndCustomer
        await PatchCustomerFieldsAsync(dto);

        return dto;
    }

    public async Task<ProductionBatchDetailDto> GetByBatchNoAsync(string batchNo)
    {
        var entity = await _context.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProcessGroups.OrderBy(pg => pg.SequenceNumber))
            .FirstOrDefaultAsync(b => b.BatchNo == batchNo);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (BatchNo={batchNo})");

        var dto = ToDetailDto(entity);

        // 从 CustomerProfile 取最新 Salesman/EndCustomer
        await PatchCustomerFieldsAsync(dto);

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
        if (string.IsNullOrWhiteSpace(request.ProductionType))
            throw new BusinessException("生产类型不能为空");
        if (string.IsNullOrWhiteSpace(request.ManufacturingItem))
            throw new BusinessException("制造物品不能为空");

        // 工单规格必填
        if (string.IsNullOrWhiteSpace(request.PlantGrade))
            throw new BusinessException("工厂牌号不能为空");
        if (string.IsNullOrWhiteSpace(request.Specification))
            throw new BusinessException("规格不能为空");
        if (string.IsNullOrWhiteSpace(request.DeliveryState))
            throw new BusinessException("交货状态不能为空");
        if (string.IsNullOrWhiteSpace(request.MaterialName))
            throw new BusinessException("物料名称不能为空");
        if (string.IsNullOrWhiteSpace(request.LengthStatus))
            throw new BusinessException("长度状态不能为空");
        if (request.TotalWeight == null || request.TotalWeight <= 0)
            throw new BusinessException("总重量必须大于0");
        // 制成倍数必须大于0
        if (request.ProductionRatio <= 0)
            throw new BusinessException("制成倍数必须大于0");
        // 定尺时总支数必须大于0
        if (request.LengthStatus == LengthStatus.Fixed.ToString() && (request.TotalQuantity == null || request.TotalQuantity <= 0))
            throw new BusinessException("总支数（定尺时必须大于0）");

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

        // ========== 有工单路径额外验证 ==========
        if (request.WorkOrderNo != NotWorkOrder)
        {
            if (string.IsNullOrWhiteSpace(request.SettlementMethod))
                throw new BusinessException("结算方式不能为空");
            if (string.IsNullOrWhiteSpace(request.StandardCode))
                throw new BusinessException("产品标准编码不能为空");
            if (string.IsNullOrWhiteSpace(request.TechnicalRequirements))
                throw new BusinessException("技术要求不能为空");
        }

        var entity = new ProductionBatch
        {
            BatchNo = batchNo,
            Status = BatchStatus.None,
            TagNo = request.TagNo,
            ProductionType = request.ProductionType,
            ManufacturingItem = request.ManufacturingItem ?? "",
            ProductionRatio = CalculateProductionRatio(request, workOrder),
            IsForceCompleted = false,
            QualityRemark = request.QualityRemark,
            SolutionParams = request.SolutionParams,
            Remark = request.Remark,

            // 仓库来源
            SourceBatchNo = request.SourceBatchNo,
            WarehouseId = request.WarehouseId,
            SourceMaterialType = request.SourceMaterialType,
            InboundSource = request.InboundSource,
            SourceName = request.SourceName,
            InboundDate = request.InboundDate,
            SourceHeatNo = request.SourceHeatNo,
            SourcePlantGrade = request.SourcePlantGrade,
            SourceSpecification = request.SourceSpecification,
            SourceLengthStatus = request.SourceLengthStatus,
            SourceUnitWeight = request.SourceUnitWeight,
            InputQuantity = request.InputQuantity,
            InputWeight = request.InputWeight,
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
            MaterialName = request.MaterialName ?? workOrder?.MaterialName.ToString() ?? "",
            SettlementMethod = request.SettlementMethod ?? workOrder?.SettlementMethod.ToString() ?? "",
            StandardCode = request.StandardCode ?? workOrder?.StandardCode ?? "",
            DeliveryState = request.DeliveryState ?? workOrder?.DeliveryState.ToString() ?? "",
            PlantGrade = request.PlantGrade ?? workOrder?.PlantGrade ?? "",
            Specification = request.Specification ?? workOrder?.Specification ?? "",
            OuterDiameterNegative = request.OuterDiameterNegative ?? workOrder?.OuterDiameterNegative ?? default,
            OuterDiameterPositive = request.OuterDiameterPositive ?? workOrder?.OuterDiameterPositive ?? default,
            WallThicknessNegative = request.WallThicknessNegative ?? workOrder?.WallThicknessNegative ?? default,
            WallThicknessPositive = request.WallThicknessPositive ?? workOrder?.WallThicknessPositive ?? default,
            LengthStatus = request.LengthStatus ?? workOrder?.LengthStatus.ToString() ?? "",
            MinLength = request.MinLength ?? workOrder?.MinLength,
            MaxLength = request.MaxLength ?? workOrder?.MaxLength,
            TotalQuantity = request.TotalQuantity ?? workOrder?.TotalQuantity ?? default,
            TotalMeters = request.TotalMeters ?? workOrder?.TotalMeters ?? default,
            TotalWeight = request.TotalWeight ?? workOrder?.TotalWeight ?? default,
            TotalItemCount = request.TotalItemCount ?? workOrder?.TotalItemCount ?? default,
            ItemDetails = request.ItemDetails ?? workOrder?.ItemDetails,
            TechnicalRequirements = request.TechnicalRequirements ?? workOrder?.TechnicalRequirements.ToString() ?? ""
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
                    };
                    _context.ProcessGroups.Add(pgEntity);
                }
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        // 刷新批次跟踪字段（包括有效投料疑问等计算字段）
        await _productionRecordService.RefreshBatchTrackingFieldsAsync(entity.Id);

        _logger.LogInformation("创建生产批次 {BatchNo} (工单: {WorkOrderNo})", batchNo, entity.WorkOrderNo);

        return new ProductionBatchListDto
        {
            Id = entity.Id,
            BatchNo = entity.BatchNo,
            Status = entity.Status.ToString(),
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
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (Id={id})");

        // 乐观锁检查
        if (!request.RowVersion.SequenceEqual(entity.RowVersion))
            throw new BusinessException("数据已被其他用户修改，请刷新后重试");

        // 生产类型 / 制造物品 不允许为空
        if (string.IsNullOrWhiteSpace(request.ProductionType))
            throw new BusinessException("生产类型不能为空");
        if (string.IsNullOrWhiteSpace(request.ManufacturingItem))
            throw new BusinessException("制造物品不能为空");

        // 工厂牌号验证（高代低）- 仅当任一牌号被更新时校验
        var effectivePlantGrade = request.PlantGrade ?? entity.PlantGrade;
        var effectiveSourcePlantGrade = request.SourcePlantGrade ?? entity.SourcePlantGrade;
        if (!GradeSubstitutes.IsSubstitutable(effectivePlantGrade, effectiveSourcePlantGrade))
            throw new BusinessException("仓库工厂牌号与工单工厂牌号不一致，且不可替代（仅允许高代低）");

        // 更新可修改字段（所有可空 DTO 字段用 ?? entity.Field 防止空值覆盖）
        entity.TagNo = request.TagNo ?? entity.TagNo;
        entity.ProductionType = request.ProductionType;
        entity.ManufacturingItem = request.ManufacturingItem;
        entity.QualityRemark = request.QualityRemark ?? entity.QualityRemark;
        entity.SolutionParams = request.SolutionParams ?? entity.SolutionParams;
        entity.Remark = request.Remark ?? entity.Remark;
        entity.SourceBatchNo = request.SourceBatchNo ?? entity.SourceBatchNo;
        entity.WarehouseId = request.WarehouseId ?? entity.WarehouseId;
        entity.SourceMaterialType = request.SourceMaterialType ?? entity.SourceMaterialType;
        entity.SourceName = request.SourceName ?? entity.SourceName;
        entity.SourceHeatNo = request.SourceHeatNo ?? entity.SourceHeatNo;
        entity.SourcePlantGrade = request.SourcePlantGrade ?? entity.SourcePlantGrade;
        entity.SourceSpecification = request.SourceSpecification ?? entity.SourceSpecification;
        entity.SourceLengthStatus = request.SourceLengthStatus ?? entity.SourceLengthStatus;
        entity.SourceUnitWeight = request.SourceUnitWeight ?? entity.SourceUnitWeight;
        entity.InputQuantity = request.InputQuantity ?? entity.InputQuantity;
        entity.InputWeight = request.InputWeight ?? entity.InputWeight;
        var oldValidQty = entity.CurrentValidQty;
        var oldValidWeight = entity.CurrentValidWeight;
        entity.CurrentValidQty = request.CurrentValidQty ?? entity.CurrentValidQty;
        entity.CurrentValidWeight = request.CurrentValidWeight ?? entity.CurrentValidWeight;
        if (request.IsForceCompleted.HasValue) entity.IsForceCompleted = request.IsForceCompleted.Value;
        if (request.ProductionRatio.HasValue) entity.ProductionRatio = request.ProductionRatio.Value;

        var oldWorkOrderNo = entity.WorkOrderNo;

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
        entity.MaterialName = request.MaterialName ?? (isNonWorkOrder ? "" : entity.MaterialName);
        entity.SettlementMethod = request.SettlementMethod ?? (isNonWorkOrder ? "" : entity.SettlementMethod);
        entity.StandardCode = request.StandardCode ?? (isNonWorkOrder ? "" : entity.StandardCode);
        entity.DeliveryState = request.DeliveryState ?? (isNonWorkOrder ? "" : entity.DeliveryState);
        entity.PlantGrade = request.PlantGrade ?? (isNonWorkOrder ? "" : entity.PlantGrade);
        entity.Specification = request.Specification ?? (isNonWorkOrder ? "" : entity.Specification);
        entity.OuterDiameterNegative = request.OuterDiameterNegative ?? (isNonWorkOrder ? default : entity.OuterDiameterNegative);
        entity.OuterDiameterPositive = request.OuterDiameterPositive ?? (isNonWorkOrder ? default : entity.OuterDiameterPositive);
        entity.WallThicknessNegative = request.WallThicknessNegative ?? (isNonWorkOrder ? default : entity.WallThicknessNegative);
        entity.WallThicknessPositive = request.WallThicknessPositive ?? (isNonWorkOrder ? default : entity.WallThicknessPositive);
        entity.LengthStatus = request.LengthStatus ?? (isNonWorkOrder ? "" : entity.LengthStatus);
        entity.MinLength = isNonWorkOrder ? request.MinLength : (request.MinLength ?? entity.MinLength);
        entity.MaxLength = isNonWorkOrder ? request.MaxLength : (request.MaxLength ?? entity.MaxLength);
        entity.TotalQuantity = request.TotalQuantity ?? (isNonWorkOrder ? default : entity.TotalQuantity);
        entity.TotalMeters = request.TotalMeters ?? (isNonWorkOrder ? default : entity.TotalMeters);
        entity.TotalWeight = request.TotalWeight ?? (isNonWorkOrder ? default : entity.TotalWeight);
        entity.TotalItemCount = request.TotalItemCount ?? (isNonWorkOrder ? default : entity.TotalItemCount);
        entity.ItemDetails = isNonWorkOrder ? request.ItemDetails : (request.ItemDetails ?? entity.ItemDetails);
        entity.TechnicalRequirements = request.TechnicalRequirements ?? (isNonWorkOrder ? "" : entity.TechnicalRequirements);

        await _context.SaveChangesAsync();

        // 工单号变更时，同步更新相关记录的全部冗余字段
        if (entity.WorkOrderNo != oldWorkOrderNo)
        {
            await _context.MaterialReceiveChecks
                .Where(r => r.ProductionBatchId == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.WorkOrderNo, entity.WorkOrderNo)
                    .SetProperty(r => r.SalesOrderNo, entity.SalesOrderNo)
                    .SetProperty(r => r.ManufacturingItem, entity.ManufacturingItem)
                    .SetProperty(r => r.PlantGrade, entity.PlantGrade)
                    .SetProperty(r => r.Specification, entity.Specification)
                    .SetProperty(r => r.ProductionType, entity.ProductionType)
                    .SetProperty(r => r.LengthStatus, entity.LengthStatus)
                    .SetProperty(r => r.Salesman, entity.Salesman)
                    .SetProperty(r => r.DeliveryState, entity.DeliveryState));
            await _context.FinalInspections
                .Where(f => f.ProductionBatchId == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(f => f.WorkOrderNo, entity.WorkOrderNo)
                    .SetProperty(f => f.SalesOrderNo, entity.SalesOrderNo)
                    .SetProperty(f => f.MaterialName, entity.MaterialName)
                    .SetProperty(f => f.PlantGrade, entity.PlantGrade)
                    .SetProperty(f => f.Specification, entity.Specification)
                    .SetProperty(f => f.ProductionType, entity.ProductionType));
            await _context.Ncrs
                .Where(n => n.BatchNo == entity.BatchNo)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.WorkOrderNo, entity.WorkOrderNo)
                    .SetProperty(n => n.PlantGrade, entity.PlantGrade)
                    .SetProperty(n => n.Specification, entity.Specification));
        }

        // 刷新批次跟踪字段（包括有效投料疑问等计算字段）
        await _productionRecordService.RefreshBatchTrackingFieldsAsync(entity.Id);

        // 记录有效数量变更日志
        if (oldValidQty != request.CurrentValidQty || oldValidWeight != request.CurrentValidWeight)
        {
            var detail = $"有效数量变更: 有效支数={oldValidQty}→{request.CurrentValidQty}" +
                         $", 有效重量={oldValidWeight?.ToString("G29")}→{request.CurrentValidWeight?.ToString("G29")}kg";
            await AddOperationLogAsync(id, "有效数量变更", detail);
        }

        _logger.LogInformation("更新生产批次 {BatchNo} (Id={Id})", entity.BatchNo, id);

        var dto = ToDetailDto(entity);

        // 从 CustomerProfile 取最新 Salesman/EndCustomer
        await PatchCustomerFieldsAsync(dto);

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

        if (!Enum.TryParse<BatchStatus>(request.Status, out var newStatus))
            throw new BusinessException($"无效的批次状态: {request.Status}");

        var oldStatus = entity.Status;
        entity.Status = newStatus;

        // 强制完成逻辑
        if (newStatus == BatchStatus.Completed)
            entity.IsForceCompleted = true;

        // 从强制完成恢复时清除标记
        if (oldStatus == BatchStatus.Completed && entity.IsForceCompleted && newStatus != BatchStatus.Completed)
            entity.IsForceCompleted = false;

        // 使用事务包裹状态变更和日志写入
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();
            var logDetail = $"状态变更: {oldStatus} → {newStatus}";
            await AddOperationLogAsync(id, "状态变更", logDetail);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        _logger.LogInformation("更新批次状态 {BatchNo} → {Status}", entity.BatchNo, newStatus);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.ProductionBatches
            .Include(b => b.ProcessGroups)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (entity == null)
            throw new BusinessException($"生产批次不存在 (Id={id})");

        // 仅允许删除"未产"状态的批次
        if (entity.Status != BatchStatus.None)
            throw new BusinessException($"仅允许删除「未产」状态的批次，当前状态为 {entity.Status}");

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

        // 删除批次（ProcessGroup 通过 Cascade 自动删除，
        // 其他直接引用 ProductionBatch 的表也通过 Cascade 自动删除）
        _context.ProductionBatches.Remove(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("删除生产批次 {BatchNo} (Id={Id})", entity.BatchNo, id);
    }

    public async Task<SaveBatchResponse> SaveAllAsync(int id, SaveBatchRequest request)
    {
        var entity = await _context.ProductionBatches
            .Include(b => b.ProcessGroups)
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
        if (string.IsNullOrWhiteSpace(request.ProductionType))
            throw new BusinessException("生产类型不能为空");
        if (string.IsNullOrWhiteSpace(request.ManufacturingItem))
            throw new BusinessException("制造物品不能为空");

        // 工单规格必填（取请求值，未传则用实体现有值）
        var effectivePlantGrade = request.PlantGrade ?? entity.PlantGrade;
        var effectiveSpec = request.Specification ?? entity.Specification;
        var effectiveDelivery = request.DeliveryState ?? entity.DeliveryState;
        var effectiveMaterialName = request.MaterialName ?? entity.MaterialName;
        var effectiveLengthStatus = request.LengthStatus ?? entity.LengthStatus;
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
        if (effectiveTotalWeight <= 0)
            throw new BusinessException("总重量必须大于0");
        // 制成倍数必须大于0
        if (effectiveProductionRatio <= 0)
            throw new BusinessException("制成倍数必须大于0");
        // 定尺时总支数必须大于0
        if (effectiveLengthStatus == LengthStatus.Fixed.ToString() && effectiveTotalQuantity <= 0)
            throw new BusinessException("总支数（定尺时必须大于0）");
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
            var effectiveSettlementMethod = request.SettlementMethod ?? entity.SettlementMethod;
            var effectiveStandardCode = request.StandardCode ?? entity.StandardCode;
            var effectiveTechnicalRequirements = request.TechnicalRequirements ?? entity.TechnicalRequirements;

            if (string.IsNullOrWhiteSpace(effectiveSettlementMethod))
                throw new BusinessException("结算方式不能为空");
            if (string.IsNullOrWhiteSpace(effectiveStandardCode))
                throw new BusinessException("产品标准编码不能为空");
            if (string.IsNullOrWhiteSpace(effectiveTechnicalRequirements))
                throw new BusinessException("技术要求不能为空");
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
        entity.TagNo = request.TagNo ?? entity.TagNo;
        entity.QualityRemark = request.QualityRemark ?? entity.QualityRemark;
        entity.SolutionParams = request.SolutionParams ?? entity.SolutionParams;
        entity.ProductionType = request.ProductionType;
        entity.InboundSource = request.InboundSource ?? entity.InboundSource;
        entity.InboundDate = request.InboundDate ?? entity.InboundDate;
        entity.Remark = request.Remark ?? entity.Remark;
        entity.ManufacturingItem = request.ManufacturingItem;
        entity.SourceBatchNo = request.SourceBatchNo ?? entity.SourceBatchNo;
        entity.WarehouseId = request.WarehouseId ?? entity.WarehouseId;
        entity.SourceMaterialType = request.SourceMaterialType ?? entity.SourceMaterialType;
        entity.SourceName = request.SourceName ?? entity.SourceName;
        entity.SourceHeatNo = request.SourceHeatNo ?? entity.SourceHeatNo;
        entity.SourcePlantGrade = request.SourcePlantGrade ?? entity.SourcePlantGrade;
        entity.SourceSpecification = request.SourceSpecification ?? entity.SourceSpecification;
        entity.SourceLengthStatus = request.SourceLengthStatus ?? entity.SourceLengthStatus;
        entity.SourceUnitWeight = request.SourceUnitWeight ?? entity.SourceUnitWeight;
        entity.InputQuantity = request.InputQuantity ?? entity.InputQuantity;
        entity.InputWeight = request.InputWeight ?? entity.InputWeight;
        var oldValidQty = entity.CurrentValidQty;
        var oldValidWeight = entity.CurrentValidWeight;
        entity.CurrentValidQty = request.CurrentValidQty ?? entity.CurrentValidQty;
        entity.CurrentValidWeight = request.CurrentValidWeight ?? entity.CurrentValidWeight;
        if (request.IsForceCompleted.HasValue) entity.IsForceCompleted = request.IsForceCompleted.Value;
        if (request.ProductionRatio.HasValue) entity.ProductionRatio = request.ProductionRatio.Value;

        var oldWorkOrderNo = entity.WorkOrderNo;

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
        entity.MaterialName = request.MaterialName ?? (isNonWorkOrder ? "" : entity.MaterialName);
        entity.SettlementMethod = request.SettlementMethod ?? (isNonWorkOrder ? "" : entity.SettlementMethod);
        entity.StandardCode = request.StandardCode ?? (isNonWorkOrder ? "" : entity.StandardCode);
        entity.DeliveryState = request.DeliveryState ?? (isNonWorkOrder ? "" : entity.DeliveryState);
        entity.PlantGrade = request.PlantGrade ?? (isNonWorkOrder ? "" : entity.PlantGrade);
        entity.Specification = request.Specification ?? (isNonWorkOrder ? "" : entity.Specification);
        entity.OuterDiameterNegative = request.OuterDiameterNegative ?? (isNonWorkOrder ? default : entity.OuterDiameterNegative);
        entity.OuterDiameterPositive = request.OuterDiameterPositive ?? (isNonWorkOrder ? default : entity.OuterDiameterPositive);
        entity.WallThicknessNegative = request.WallThicknessNegative ?? (isNonWorkOrder ? default : entity.WallThicknessNegative);
        entity.WallThicknessPositive = request.WallThicknessPositive ?? (isNonWorkOrder ? default : entity.WallThicknessPositive);
        entity.LengthStatus = request.LengthStatus ?? (isNonWorkOrder ? "" : entity.LengthStatus);
        entity.MinLength = isNonWorkOrder ? request.MinLength : (request.MinLength ?? entity.MinLength);
        entity.MaxLength = isNonWorkOrder ? request.MaxLength : (request.MaxLength ?? entity.MaxLength);
        entity.TotalQuantity = request.TotalQuantity ?? (isNonWorkOrder ? default : entity.TotalQuantity);
        entity.TotalMeters = request.TotalMeters ?? (isNonWorkOrder ? default : entity.TotalMeters);
        entity.TotalWeight = request.TotalWeight ?? (isNonWorkOrder ? default : entity.TotalWeight);
        entity.TotalItemCount = request.TotalItemCount ?? (isNonWorkOrder ? default : entity.TotalItemCount);
        entity.ItemDetails = isNonWorkOrder ? request.ItemDetails : (request.ItemDetails ?? entity.ItemDetails);
        entity.TechnicalRequirements = request.TechnicalRequirements ?? (isNonWorkOrder ? "" : entity.TechnicalRequirements);

        // ===== 2. 更新状态（如有） =====
        if (!string.IsNullOrEmpty(request.Status) && request.Status != entity.Status.ToString())
        {
            if (!Enum.TryParse<BatchStatus>(request.Status, out var newStatus))
                throw new BusinessException($"无效的批次状态: {request.Status}");

            entity.Status = newStatus;

            if (newStatus == BatchStatus.Completed)
                entity.IsForceCompleted = true;
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

            referencedIds = new HashSet<int>(referencedByRecord
                .Concat(referencedByOutsource)
                .Concat(referencedByPicklingRecord)
                .Concat(referencedByProcessInspection));
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
                _logger.LogWarning("批次 {BatchNo} 的 {Count} 个工序组因存在生产记录或委外记录引用而跳过删除: {Names}",
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
                existingReferenced.ManufacturingMultiple = pgReq.ManufacturingMultiple;
                existingReferenced.Remark = pgReq.Remark;
                existingReferenced.ColdRollDraw = pgReq.ColdRollDraw;
                existingReferenced.OilPipeCut = pgReq.OilPipeCut;
                existingReferenced.Degrease = pgReq.Degrease;
                existingReferenced.Solution = pgReq.Solution;
                existingReferenced.Straighten = pgReq.Straighten;
                existingReferenced.Cut = pgReq.Cut;
                existingReferenced.ThicknessMeasure = pgReq.ThicknessMeasure;
                existingReferenced.Pickle = pgReq.Pickle;
                existingReferenced.OuterPolish = pgReq.OuterPolish;
                existingReferenced.InnerGrinding = pgReq.InnerGrinding;
                existingReferenced.OuterSpotGrinding = pgReq.OuterSpotGrinding;
                existingReferenced.Inspection = pgReq.Inspection;
                existingReferenced.WeldingHead = pgReq.WeldingHead;
                existingReferenced.Lubrication = pgReq.Lubrication;
                existingReferenced.Warehouse = pgReq.Warehouse;
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
                ManufacturingMultiple = pgReq.ManufacturingMultiple,
                Remark = pgReq.Remark,
                ColdRollDraw = pgReq.ColdRollDraw,
                OilPipeCut = pgReq.OilPipeCut,
                Degrease = pgReq.Degrease,
                Solution = pgReq.Solution,
                Straighten = pgReq.Straighten,
                Cut = pgReq.Cut,
                ThicknessMeasure = pgReq.ThicknessMeasure,
                Pickle = pgReq.Pickle,
                OuterPolish = pgReq.OuterPolish,
                InnerGrinding = pgReq.InnerGrinding,
                OuterSpotGrinding = pgReq.OuterSpotGrinding,
                Inspection = pgReq.Inspection,
                WeldingHead = pgReq.WeldingHead,
                Lubrication = pgReq.Lubrication,
                Warehouse = pgReq.Warehouse
            };
            _context.ProcessGroups.Add(pg);
        }

        // ===== 4. 提交新增工序组（此时仅有 INSERT，无冲突） =====
        await _context.SaveChangesAsync();

        // 工单号变更时，同步更新相关记录的全部冗余字段
        if (entity.WorkOrderNo != oldWorkOrderNo)
        {
            await _context.MaterialReceiveChecks
                .Where(r => r.ProductionBatchId == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.WorkOrderNo, entity.WorkOrderNo)
                    .SetProperty(r => r.SalesOrderNo, entity.SalesOrderNo)
                    .SetProperty(r => r.ManufacturingItem, entity.ManufacturingItem)
                    .SetProperty(r => r.PlantGrade, entity.PlantGrade)
                    .SetProperty(r => r.Specification, entity.Specification)
                    .SetProperty(r => r.ProductionType, entity.ProductionType)
                    .SetProperty(r => r.LengthStatus, entity.LengthStatus)
                    .SetProperty(r => r.Salesman, entity.Salesman)
                    .SetProperty(r => r.DeliveryState, entity.DeliveryState));
            await _context.FinalInspections
                .Where(f => f.ProductionBatchId == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(f => f.WorkOrderNo, entity.WorkOrderNo)
                    .SetProperty(f => f.SalesOrderNo, entity.SalesOrderNo)
                    .SetProperty(f => f.MaterialName, entity.MaterialName)
                    .SetProperty(f => f.PlantGrade, entity.PlantGrade)
                    .SetProperty(f => f.Specification, entity.Specification)
                    .SetProperty(f => f.ProductionType, entity.ProductionType));
            await _context.Ncrs
                .Where(n => n.BatchNo == entity.BatchNo)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.WorkOrderNo, entity.WorkOrderNo)
                    .SetProperty(n => n.PlantGrade, entity.PlantGrade)
                    .SetProperty(n => n.Specification, entity.Specification));
        }

        // 记录有效数量变更日志
        if (oldValidQty != request.CurrentValidQty || oldValidWeight != request.CurrentValidWeight)
        {
            var detail = $"有效数量变更: 有效支数={oldValidQty}→{request.CurrentValidQty}" +
                         $", 有效重量={oldValidWeight?.ToString("G29")}→{request.CurrentValidWeight?.ToString("G29")}kg";
            await AddOperationLogAsync(id, "有效数量变更", detail);
        }

        // ===== 5. 工序组已变更，刷新批次跟踪字段 =====
        await _productionRecordService.BatchUpdateBatchTrackingAsync(new[] { id });

        _logger.LogInformation("批量保存生产批次 {BatchNo} (Id={Id}), 工序组={GroupCount}",
            entity.BatchNo, id, request.ProcessGroups.Count);

        return new SaveBatchResponse
        {
            RowVersion = entity.RowVersion,
            Status = entity.Status.ToString()
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
            ManufacturingMultiple = request.ManufacturingMultiple,
            Remark = request.Remark,
            ColdRollDraw = request.ColdRollDraw,
            OilPipeCut = request.OilPipeCut,
            Degrease = request.Degrease,
            Solution = request.Solution,
            Straighten = request.Straighten,
            Cut = request.Cut,
            ThicknessMeasure = request.ThicknessMeasure,
            Pickle = request.Pickle,
            OuterPolish = request.OuterPolish,
            InnerGrinding = request.InnerGrinding,
            OuterSpotGrinding = request.OuterSpotGrinding,
            Inspection = request.Inspection,
            WeldingHead = request.WeldingHead,
            Lubrication = request.Lubrication,
            Warehouse = request.Warehouse
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
        var usedBatchNos = await _context.ProductionBatches
            .Where(b => b.SourceBatchNo != null)
            .Select(b => b.SourceBatchNo)
            .ToListAsync();

        var available = await _context.OutboundRecords
            .Where(o => o.OutboundType == OutboundType.ProductionPick)
            .Join(_context.InventoryBatches,
                o => o.InventoryBatchId,
                ib => ib.Id,
                (o, ib) => new { o, ib })
            .Where(x => !usedBatchNos.Contains(x.ib.BatchNo))
            .Join(_context.Warehouses,
                x => x.ib.WarehouseId,
                w => w.Id,
                (x, w) => new { x.o, x.ib, w })
            .OrderBy(x => x.ib.BatchNo)
            .Select(x => new AvailableBatchDto
            {
                BatchNo = x.ib.BatchNo,
                WarehouseId = x.ib.WarehouseId,
                WarehouseName = x.w.Name,
                MaterialType = x.ib.MaterialType,
                InboundSource = x.ib.InboundSource,
                SourceName = x.ib.SourceName,
                InboundDate = x.ib.InboundDate,
                HeatNo = x.ib.HeatNo,
                OutboundQuantity = x.o.OutboundQuantity,
                OutboundWeight = x.o.OutboundWeight,
                WorkOrderNo = x.ib.WorkOrderNo,
                PlantGrade = x.ib.PlantGrade,
                Specification = x.ib.Specification,
                LengthStatus = x.ib.LengthStatus,
                UnitWeight = x.ib.UnitWeight
            })
            .ToListAsync();

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
                ["ProductionType"] = entity.ProductionType ?? "",
                ["Status"] = entity.Status.ToString(),
                ["CurrentExecDate"] = entity.CurrentExecDate?.ToString("yyyy-MM-dd") ?? "",
                ["CurrentGroupName"] = entity.CurrentGroupName ?? "",
                ["CurrentSectionName"] = entity.CurrentSectionName ?? "",
                ["CurrentEquipmentName"] = entity.CurrentEquipmentName ?? "",
                ["CurrentOutsource"] = entity.CurrentOutsource ?? "",
                ["CurrentSpec"] = entity.CurrentSpec ?? "",
                ["NextSectionName"] = entity.NextSectionName ?? "",
                ["CorrespondingSpec"] = entity.CorrespondingSpec ?? "",
                ["NextProcess"] = entity.NextProcess ?? ""
            }
        };

        return TablePrintHelper.GeneratePdf($"生产批次 - {entity.BatchNo}", items, columns);
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
                (b.Remark != null && b.Remark.Contains(kw)) ||
                (b.QualityRemark != null && b.QualityRemark.Contains(kw)) ||
                (b.SourceHeatNo != null && b.SourceHeatNo.Contains(kw)) ||
                (b.SourceName != null && b.SourceName.Contains(kw)) ||
                (b.SourceBatchNo != null && b.SourceBatchNo.Contains(kw)) ||
                (b.SourceSpecification != null && b.SourceSpecification.Contains(kw)) ||
                (b.SourceMaterialType != null && b.SourceMaterialType.Contains(kw)) ||
                (b.SourceLengthStatus != null && b.SourceLengthStatus.Contains(kw)) ||
                (b.InboundSource != null && b.InboundSource.Contains(kw)) ||
                (b.SolutionParams != null && b.SolutionParams.Contains(kw)));
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

        var items = entities.Select(b => new Dictionary<string, object>
        {
            ["BatchNo"] = b.BatchNo,
            ["TagNo"] = b.TagNo ?? "",
            ["CreatedTime"] = b.CreatedTime,
            ["WorkOrderNo"] = b.WorkOrderNo,
            ["SalesOrderNo"] = b.SalesOrderNo,
            ["ProductionMainNo"] = b.ProductionMainNo,
            ["ProductionSubNo"] = b.ProductionSubNo ?? "",
            ["ProductionType"] = b.ProductionType ?? "",
            ["Status"] = b.Status.ToString(),
            ["CurrentExecDate"] = (object?)b.CurrentExecDate,
            ["CurrentGroupName"] = b.CurrentGroupName ?? "",
            ["CurrentSectionName"] = b.CurrentSectionName ?? "",
            ["CurrentEquipmentName"] = b.CurrentEquipmentName ?? "",
            ["CurrentOutsource"] = b.CurrentOutsource ?? "",
            ["CurrentSpec"] = b.CurrentSpec ?? "",
            ["NextSectionName"] = b.NextSectionName ?? "",
            ["CorrespondingSpec"] = b.CorrespondingSpec ?? ""
        }).ToList();

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

        return TablePrintHelper.GeneratePdf("生产批次列表", items, columns);
    }

    public async Task<byte[]> PrintBatchSelectedAsync(int[] ids)
    {
        var entities = await _context.ProductionBatches
            .AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .OrderBy(b => b.CreatedTime)
            .ToListAsync();

        if (entities.Count == 0)
            throw new BusinessException("未找到选中的批次数据");

        var items = entities.Select(b => new Dictionary<string, object>
        {
            ["BatchNo"] = b.BatchNo,
            ["TagNo"] = b.TagNo ?? "",
            ["CreatedTime"] = b.CreatedTime,
            ["WorkOrderNo"] = b.WorkOrderNo,
            ["SalesOrderNo"] = b.SalesOrderNo,
            ["ProductionMainNo"] = b.ProductionMainNo,
            ["ProductionSubNo"] = b.ProductionSubNo ?? "",
            ["ProductionType"] = b.ProductionType ?? "",
            ["Status"] = b.Status.ToString(),
            ["CurrentExecDate"] = (object?)b.CurrentExecDate,
            ["CurrentGroupName"] = b.CurrentGroupName ?? "",
            ["CurrentSectionName"] = b.CurrentSectionName ?? "",
            ["CurrentEquipmentName"] = b.CurrentEquipmentName ?? "",
            ["CurrentOutsource"] = b.CurrentOutsource ?? "",
            ["CurrentSpec"] = b.CurrentSpec ?? "",
            ["NextSectionName"] = b.NextSectionName ?? "",
            ["CorrespondingSpec"] = b.CorrespondingSpec ?? ""
        }).ToList();

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

        return TablePrintHelper.GeneratePdf("生产批次列表", items, columns);
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

        var columns = request.Columns;
        return ProcessCardPrintHelper.GeneratePdf("工 艺 流 转 卡", entities, columns);
    }

    // ========== 筛选上下文 ==========

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        // 注意：枚举列（ProductionType/Status/MaterialName 等）不在此处返回，
        // 由前端 EnumOptions fallback 直接提供带中文 Display 的选项，避免映射丢失。
        var results = await _context.ProductionBatches
            .AsNoTracking()
            .Select(b => new
            {
                b.BatchNo, b.TagNo, b.WorkOrderNo, b.SalesOrderNo,
                b.ProductionMainNo, b.ProductionSubNo,
                b.CurrentExecDate,
                b.CurrentGroupName, b.CurrentSectionName, b.CurrentEquipmentName,
                b.CurrentOutsource, b.CurrentSpec, b.NextSectionName,
                b.CorrespondingSpec, b.NextProcess, b.SignDate, b.Salesman, b.EndCustomer,
                b.DeliveryDate,
                b.StandardCode, b.PlantGrade, b.Specification, b.CreatedBy
            })
            .ToListAsync();

        // ========== 从 CustomerProfile 覆盖业务员/最终用户（与 GetPagedAsync 的 PatchCustomerFieldsAsync 一致） ==========
        var orderNos = results.Select(x => x.SalesOrderNo).Distinct().ToList();
        Dictionary<string, (string Salesman, string? EndCustomer)> customerByOrderNo = new();
        if (orderNos.Count > 0)
        {
            customerByOrderNo = await _context.SalesOrders
                .AsNoTracking()
                .Include(so => so.Customer)
                .Where(so => orderNos.Contains(so.OrderNumber))
                .ToDictionaryAsync(so => so.OrderNumber, so =>
                    (so.Customer?.Salesman ?? "", so.Customer?.EndCustomer));
        }
        // 用一个可变类型承载 patched 值
        var patchedResults = results.Select(r =>
        {
            var salesman = r.Salesman;
            var endCustomer = r.EndCustomer;
            if (customerByOrderNo.TryGetValue(r.SalesOrderNo, out var c))
            {
                salesman = c.Salesman;
                endCustomer = c.EndCustomer;
            }
            return new { r.BatchNo, r.TagNo, r.WorkOrderNo, r.SalesOrderNo, r.ProductionMainNo,
                r.ProductionSubNo, r.CurrentExecDate, r.CurrentGroupName, r.CurrentSectionName,
                r.CurrentEquipmentName, r.CurrentOutsource, r.CurrentSpec, r.NextSectionName,
                r.CorrespondingSpec, r.NextProcess, r.SignDate, Salesman = salesman, EndCustomer = endCustomer,
                r.DeliveryDate, r.StandardCode, r.PlantGrade, r.Specification, r.CreatedBy };
        }).ToList();

        return new Dictionary<string, List<string>>
        {
            ["BatchNo"] = patchedResults.Select(x => x.BatchNo).Distinct().OrderBy(x => x).ToList(),
            ["TagNo"] = patchedResults.Select(x => x.TagNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["WorkOrderNo"] = patchedResults.Select(x => x.WorkOrderNo).Distinct().OrderBy(x => x).ToList(),
            ["SalesOrderNo"] = patchedResults.Select(x => x.SalesOrderNo).Distinct().OrderBy(x => x).ToList(),
            ["ProductionMainNo"] = patchedResults.Select(x => x.ProductionMainNo).Distinct().OrderBy(x => x).ToList(),
            ["ProductionSubNo"] = patchedResults.Select(x => x.ProductionSubNo).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["CurrentExecDate"] = patchedResults.Where(x => x.CurrentExecDate.HasValue)
                .Select(x => x.CurrentExecDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            ["CurrentGroupName"] = patchedResults.Select(x => x.CurrentGroupName).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["CurrentSectionName"] = patchedResults.Select(x => x.CurrentSectionName).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["CurrentEquipmentName"] = patchedResults.Select(x => x.CurrentEquipmentName).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["CurrentOutsource"] = patchedResults.Select(x => x.CurrentOutsource).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["CurrentSpec"] = patchedResults.Select(x => x.CurrentSpec).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["NextSectionName"] = patchedResults.Select(x => x.NextSectionName).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["CorrespondingSpec"] = patchedResults.Select(x => x.CorrespondingSpec).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["NextProcess"] = patchedResults.Select(x => x.NextProcess).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["SignDate"] = patchedResults.Select(x => x.SignDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            ["Salesman"] = patchedResults.Select(x => x.Salesman).Distinct().OrderBy(x => x).ToList(),
            ["EndCustomer"] = patchedResults.Select(x => x.EndCustomer).Where(x => x != null).Distinct().OrderBy(x => x).ToList()!,
            ["DeliveryDate"] = patchedResults.Select(x => x.DeliveryDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            ["StandardCode"] = patchedResults.Select(x => x.StandardCode).Distinct().OrderBy(x => x).ToList(),
            ["PlantGrade"] = patchedResults.Select(x => x.PlantGrade).Distinct().OrderBy(x => x).ToList(),
            ["Specification"] = patchedResults.Select(x => x.Specification).Distinct().OrderBy(x => x).ToList(),
            ["CreatedBy"] = patchedResults.Select(x => x.CreatedBy).Distinct().OrderBy(x => x).ToList(),
        };
    }

    // ========== 辅助方法 ==========

    private static int CalculateProductionRatio(CreateProductionBatchRequest request, WoEntity? workOrder)
    {
        // 仅在定尺(Fixed)状态下计算
        var lengthStatus = request.LengthStatus ?? workOrder?.LengthStatus.ToString() ?? "";
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
            Status = entity.Status.ToString(),
            TagNo = entity.TagNo,
            ProductionType = entity.ProductionType,
            ManufacturingItem = entity.ManufacturingItem,
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
            SettlementMethod = entity.SettlementMethod,
            StandardCode = entity.StandardCode,
            DeliveryState = entity.DeliveryState,
            PlantGrade = entity.PlantGrade,
            Specification = entity.Specification,
            OuterDiameterNegative = entity.OuterDiameterNegative,
            OuterDiameterPositive = entity.OuterDiameterPositive,
            WallThicknessNegative = entity.WallThicknessNegative,
            WallThicknessPositive = entity.WallThicknessPositive,
            LengthStatus = entity.LengthStatus,
            MinLength = entity.MinLength,
            MaxLength = entity.MaxLength,
            TotalQuantity = entity.TotalQuantity,
            TotalMeters = entity.TotalMeters,
            TotalWeight = entity.TotalWeight,
            TotalItemCount = entity.TotalItemCount,
            ItemDetails = entity.ItemDetails,
            TechnicalRequirements = entity.TechnicalRequirements,

            // 仓库冗余
            SourceBatchNo = entity.SourceBatchNo,
            WarehouseId = entity.WarehouseId,
            SourceMaterialType = entity.SourceMaterialType,
            InboundSource = entity.InboundSource,
            SourceName = entity.SourceName,
            InboundDate = entity.InboundDate,
            SourceHeatNo = entity.SourceHeatNo,
            SourcePlantGrade = entity.SourcePlantGrade,
            SourceSpecification = entity.SourceSpecification,
            SourceLengthStatus = entity.SourceLengthStatus,
            SourceUnitWeight = entity.SourceUnitWeight,
            InputQuantity = entity.InputQuantity,
            InputWeight = entity.InputWeight,
            CurrentValidQty = entity.CurrentValidQty,
            CurrentValidWeight = entity.CurrentValidWeight,

            // 审计
            CreatedTime = entity.CreatedTime,
            CreatedBy = entity.CreatedBy,
            UpdatedTime = entity.UpdatedTime,
            UpdatedBy = entity.UpdatedBy,

            RowVersion = entity.RowVersion,

            ProcessGroups = entity.ProcessGroups?.Select(ToGroupDto).ToList() ?? new()
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
            ManufacturingMultiple = entity.ManufacturingMultiple,
            Remark = entity.Remark,
            ColdRollDraw = entity.ColdRollDraw,
            OilPipeCut = entity.OilPipeCut,
            Degrease = entity.Degrease,
            Solution = entity.Solution,
            Straighten = entity.Straighten,
            Cut = entity.Cut,
            ThicknessMeasure = entity.ThicknessMeasure,
            Pickle = entity.Pickle,
            OuterPolish = entity.OuterPolish,
            InnerGrinding = entity.InnerGrinding,
            OuterSpotGrinding = entity.OuterSpotGrinding,
            Inspection = entity.Inspection,
            WeldingHead = entity.WeldingHead,
            Lubrication = entity.Lubrication,
            Warehouse = entity.Warehouse,
            CreatedTime = entity.CreatedTime,
            CreatedBy = entity.CreatedBy
        };
    }

    /// <summary>
    /// 从 CustomerProfile 取当前最新 Salesman/EndCustomer，覆盖 ProductionBatch 冗余快照
    /// </summary>
    private async Task PatchCustomerFieldsAsync(ProductionBatchDetailDto dto)
    {
        if (string.IsNullOrEmpty(dto.SalesOrderNo)) return;

        var salesOrder = await _context.SalesOrders
            .AsNoTracking()
            .Include(so => so.Customer)
            .FirstOrDefaultAsync(so => so.OrderNumber == dto.SalesOrderNo);
        if (salesOrder?.Customer != null)
        {
            dto.Salesman = salesOrder.Customer.Salesman;
            dto.EndCustomer = salesOrder.Customer.EndCustomer;
        }
    }

    /// <summary>
    /// 批量从 CustomerProfile 覆盖 ProductionBatchListDto 的冗余快照字段
    /// </summary>
    private async Task PatchCustomerFieldsAsync(List<ProductionBatchListDto> items)
    {
        var orderNos = items.Select(i => i.SalesOrderNo).Distinct().ToList();
        if (orderNos.Count == 0) return;

        var salesOrders = await _context.SalesOrders
            .AsNoTracking()
            .Include(so => so.Customer)
            .Where(so => orderNos.Contains(so.OrderNumber))
            .ToListAsync();

        var customerByOrderNo = salesOrders.ToDictionary(so => so.OrderNumber, so => so.Customer, StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (customerByOrderNo.TryGetValue(item.SalesOrderNo, out var customer))
            {
                item.Salesman = customer.Salesman;
                item.EndCustomer = customer.EndCustomer;
            }
        }
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
                pg.ColdRollDraw, pg.OilPipeCut, pg.Degrease, pg.Solution,
                pg.Straighten, pg.Cut, pg.ThicknessMeasure, pg.Pickle,
                pg.OuterPolish, pg.InnerGrinding, pg.OuterSpotGrinding,
                pg.Inspection, pg.WeldingHead, pg.Lubrication, pg.Warehouse
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

    // ========== 批次操作日志 ==========

    public async Task AddOperationLogAsync(int batchId, string operationType, string? detail = null)
    {
        var log = new BatchOperationLog
        {
            ProductionBatchId = batchId,
            OperationType = operationType,
            Detail = detail,
            CreatedBy = "system",
            CreatedTime = DateTimeOffset.UtcNow
        };
        _context.BatchOperationLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<List<BatchOperationLogDto>> GetOperationLogsAsync(int batchId)
    {
        return await _context.BatchOperationLogs
            .Where(l => l.ProductionBatchId == batchId)
            .OrderByDescending(l => l.CreatedTime)
            .Select(l => new BatchOperationLogDto
            {
                Id = l.Id,
                OperationType = l.OperationType,
                Detail = l.Detail,
                CreatedBy = l.CreatedBy,
                CreatedTime = l.CreatedTime
            })
            .ToListAsync();
    }
}
