using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Linq.Expressions;
using MES.Core.DTOs.Warehouse;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Core.Interfaces.WorkOrder;
using MES.Core.Interfaces.Warehouse;
using MES.Data;
using MES.Data.Entities.Warehouse;

namespace MES.Services.Warehouse;

public class OutboundWriteService : IOutboundWriteService
{
    private readonly AppDbContext _context;
    private readonly IWorkOrderExecutionService _workOrderExecutionService;
    private readonly ILogger<OutboundWriteService> _logger;

    private static readonly Expression<Func<OutboundRecord, OutboundRecordDto>> OutboundToDtoExpr = r => new OutboundRecordDto
    {
        Id = r.Id,
        InventoryBatchId = r.InventoryBatchId,
        BatchNo = r.BatchNo,
        OutboundType = r.OutboundType,
        WorkOrderNo = r.WorkOrderNo,
        ReturnSourceBatchNo = r.ReturnSourceBatchNo,
        SourceOrderNo = r.SourceOrderNo,
        TargetCompany = r.TargetCompany,
        OutboundQuantity = r.OutboundQuantity,
        OutboundWeight = r.OutboundWeight,
        OutboundMeters = r.OutboundMeters,
        OutboundDate = r.OutboundDate,
        Remark = r.Remark,
        CreatedBy = r.CreatedBy,
        CreatedTime = r.CreatedTime
    };
    private static readonly Func<OutboundRecord, OutboundRecordDto> OutboundToDto = OutboundToDtoExpr.Compile();

    public OutboundWriteService(
        AppDbContext context,
        IWorkOrderExecutionService workOrderExecutionService,
        ILogger<OutboundWriteService> logger)
    {
        _context = context;
        _workOrderExecutionService = workOrderExecutionService;
        _logger = logger;
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

    public async Task<OutboundRecordDto> OutboundAsync(CreateOutboundRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            var batch = await _context.InventoryBatches
                .FirstOrDefaultAsync(b => b.Id == request.InventoryBatchId);

            if (batch == null)
                throw new BusinessException("批次不存在");

            if (batch.RemainingQuantity < request.OutboundQuantity)
                throw new BusinessException($"剩余支数不足（剩余{batch.RemainingQuantity}，出库{request.OutboundQuantity}）");

            if (batch.RemainingWeight < request.OutboundWeight)
                throw new BusinessException($"剩余重量不足（剩余{batch.RemainingWeight:G29}kg，出库{request.OutboundWeight:G29}kg）");

            if (request.OutboundMeters.HasValue && batch.RemainingMeters.HasValue
                && batch.RemainingMeters.Value < request.OutboundMeters.Value)
                throw new BusinessException($"剩余米数不足（剩余{batch.RemainingMeters:G29}m，出库{request.OutboundMeters:G29}m）");

            batch.RemainingQuantity -= request.OutboundQuantity;
            batch.RemainingWeight -= request.OutboundWeight;
            if (request.OutboundMeters.HasValue && batch.RemainingMeters.HasValue)
                batch.RemainingMeters -= request.OutboundMeters.Value;

            var record = new OutboundRecord
            {
                InventoryBatchId = request.InventoryBatchId,
                BatchNo = batch.BatchNo,
                OutboundType = request.OutboundType,
                WorkOrderNo = request.WorkOrderNo ?? batch.WorkOrderNo,
                ReturnSourceBatchNo = request.ReturnSourceBatchNo,
                SourceOrderNo = request.SourceOrderNo,
                TargetCompany = request.TargetCompany,
                OutboundQuantity = request.OutboundQuantity,
                OutboundWeight = request.OutboundWeight,
                OutboundMeters = request.OutboundMeters,
                OutboundDate = request.OutboundDate,
                Remark = request.Remark,
            };

            _context.OutboundRecords.Add(record);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // 刷新出库记录实际写入的出库工单号（而非批次原工单号），保证计划所属工单读模型同步
            await TryRefreshExecutionSummaryAsync(record.WorkOrderNo);

            var dto = OutboundToDto(record);
            return dto;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<BatchOutboundResult> BatchOutboundAsync(BatchOutboundRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var batchIds = request.Items.Select(i => i.InventoryBatchId).Distinct().ToList();
            var batches = await _context.InventoryBatches
                .Where(b => batchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            var results = new List<OutboundRecordDto>();

            foreach (var item in request.Items)
            {
                if (!batches.TryGetValue(item.InventoryBatchId, out var batch))
                    throw new BusinessException($"批次ID={item.InventoryBatchId}不存在");

                // 委外-穿孔号验证：委外出库+圆棒时必须填写有效的委外单号
                var resolvedOutboundType = item.OutboundType ?? request.OutboundType;
                var resolvedSourceOrderNo = item.SourceOrderNo ?? request.SourceOrderNo;
                if (resolvedOutboundType == OutboundType.SubcontractOut
                    && string.Equals(batch.MaterialType, nameof(MaterialType.RoundBar), StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(resolvedSourceOrderNo))
                    throw new BusinessException($"批次{batch.BatchNo}：出库类型为委外出库且物料为圆棒时，委外-穿孔号必填");

                if (!string.IsNullOrWhiteSpace(resolvedSourceOrderNo))
                {
                    var orderExists = await _context.SubcontractOrders.AnyAsync(o => o.OrderNo == resolvedSourceOrderNo);
                    if (!orderExists)
                        throw new BusinessException($"委外-穿孔号「{resolvedSourceOrderNo}」不存在");
                }

                if (batch.RemainingQuantity < item.OutboundQuantity)
                    throw new BusinessException($"批次{batch.BatchNo}剩余支数不足（剩余{batch.RemainingQuantity}，出库{item.OutboundQuantity}）");

                if (batch.RemainingWeight < item.OutboundWeight)
                    throw new BusinessException($"批次{batch.BatchNo}剩余重量不足（剩余{batch.RemainingWeight:G29}kg，出库{item.OutboundWeight:G29}kg）");

                if (item.OutboundMeters.HasValue && batch.RemainingMeters.HasValue
                    && batch.RemainingMeters.Value < item.OutboundMeters.Value)
                    throw new BusinessException($"批次{batch.BatchNo}剩余米数不足（剩余{batch.RemainingMeters:G29}m，出库{item.OutboundMeters:G29}m）");

                batch.RemainingQuantity -= item.OutboundQuantity;
                batch.RemainingWeight -= item.OutboundWeight;
                if (item.OutboundMeters.HasValue && batch.RemainingMeters.HasValue)
                    batch.RemainingMeters -= item.OutboundMeters.Value;

                var record = new OutboundRecord
                {
                    InventoryBatchId = item.InventoryBatchId,
                    BatchNo = batch.BatchNo,
                    OutboundType = item.OutboundType ?? request.OutboundType,
                    WorkOrderNo = item.WorkOrderNo ?? request.WorkOrderNo ?? batch.WorkOrderNo,
                    ReturnSourceBatchNo = item.ReturnSourceBatchNo ?? request.ReturnSourceBatchNo,
                    SourceOrderNo = item.SourceOrderNo ?? request.SourceOrderNo,
                    TargetCompany = item.TargetCompany ?? request.TargetCompany,
                    OutboundQuantity = item.OutboundQuantity,
                    OutboundWeight = item.OutboundWeight,
                    OutboundMeters = item.OutboundMeters,
                    OutboundDate = request.OutboundDate,
                    Remark = item.Remark ?? request.Remark,
                };

                _context.OutboundRecords.Add(record);

                var dto = OutboundToDto(record);
                results.Add(dto);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // 刷新出库记录实际写入的出库工单号（而非批次原工单号），保证计划所属工单读模型同步
            var woNos = results
                .Where(r => !string.IsNullOrWhiteSpace(r.WorkOrderNo))
                .Select(r => r.WorkOrderNo!)
                .Distinct()
                .ToList();
            foreach (var woNo in woNos)
                await TryRefreshExecutionSummaryAsync(woNo);

            return new BatchOutboundResult
            {
                SuccessCount = results.Count,
                Records = results
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<OutboundRecordDto> UpdateOutboundRecordAsync(long id, UpdateOutboundRecordRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            var entity = await _context.OutboundRecords
                .FirstOrDefaultAsync(r => r.Id == id);

            if (entity == null)
                throw new BusinessException("出库记录不存在");

            var oldQty = entity.OutboundQuantity;
            var oldWt = entity.OutboundWeight;
            var oldOutboundMeters = entity.OutboundMeters;
            var oldWorkOrderNo = entity.WorkOrderNo;

            entity.OutboundType = request.OutboundType ?? entity.OutboundType;
            entity.WorkOrderNo = request.WorkOrderNo ?? entity.WorkOrderNo;
            entity.ReturnSourceBatchNo = request.ReturnSourceBatchNo ?? entity.ReturnSourceBatchNo;
            entity.TargetCompany = request.TargetCompany ?? entity.TargetCompany;
            if (request.OutboundQuantity.HasValue) entity.OutboundQuantity = request.OutboundQuantity.Value;
            if (request.OutboundWeight.HasValue) entity.OutboundWeight = request.OutboundWeight.Value;
            if (request.OutboundMeters.HasValue) entity.OutboundMeters = request.OutboundMeters.Value;
            if (request.OutboundDate.HasValue) entity.OutboundDate = request.OutboundDate.Value;
            entity.Remark = request.Remark ?? entity.Remark;

            var deltaQty = entity.OutboundQuantity - oldQty;
            var deltaWt = entity.OutboundWeight - oldWt;
            var deltaMeters = (entity.OutboundMeters ?? 0m) - (oldOutboundMeters ?? 0m);
            if (deltaQty != 0 || deltaWt != 0 || deltaMeters != 0)
            {
                var batch = await _context.InventoryBatches
                    .FirstOrDefaultAsync(b => b.Id == entity.InventoryBatchId);
                if (batch == null)
                    throw new BusinessException("关联的库存批次不存在");
                if (batch.RemainingQuantity < deltaQty)
                    throw new BusinessException($"批次{batch.BatchNo}剩余支数不足（剩余{batch.RemainingQuantity}，调整差额{deltaQty}）");
                if (batch.RemainingWeight < deltaWt)
                    throw new BusinessException($"批次{batch.BatchNo}剩余重量不足（剩余{batch.RemainingWeight:G29}kg，调整差额{deltaWt:G29}kg）");
                if (deltaMeters > 0 && batch.RemainingMeters.HasValue && batch.RemainingMeters.Value < deltaMeters)
                    throw new BusinessException($"批次{batch.BatchNo}剩余米数不足（剩余{batch.RemainingMeters:G29}m，调整差额{deltaMeters:G29}m）");

                batch.RemainingQuantity -= deltaQty;
                batch.RemainingWeight -= deltaWt;
                if (deltaMeters != 0 && batch.RemainingMeters.HasValue)
                    batch.RemainingMeters -= deltaMeters;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // 刷新出库记录实际工单号（可能被本次修改）；若工单号被变更，旧工单号也需重算（此前计入的出库量消失）
            await TryRefreshExecutionSummaryAsync(entity.WorkOrderNo);
            if (!string.IsNullOrWhiteSpace(oldWorkOrderNo)
                && !string.Equals(oldWorkOrderNo, entity.WorkOrderNo, StringComparison.OrdinalIgnoreCase))
                await TryRefreshExecutionSummaryAsync(oldWorkOrderNo);

            var dto = OutboundToDto(entity);
            return dto;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task HardDeleteOutboundRecordAsync(long id)
    {
        var entity = await _context.OutboundRecords
            .FirstOrDefaultAsync(r => r.Id == id);

        if (entity == null)
            throw new BusinessException("出库记录不存在");

        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            var batch = await _context.InventoryBatches
                .FirstOrDefaultAsync(b => b.Id == entity.InventoryBatchId);
            if (batch != null)
            {
                batch.RemainingQuantity += entity.OutboundQuantity;
                batch.RemainingWeight += entity.OutboundWeight;
                if (entity.OutboundMeters.HasValue && batch.RemainingMeters.HasValue)
                    batch.RemainingMeters += entity.OutboundMeters.Value;
            }

            var workOrderNo = entity.WorkOrderNo;
            _context.OutboundRecords.Remove(entity);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // 刷新出库记录实际工单号（删除后该工单出库量减少）
            await TryRefreshExecutionSummaryAsync(workOrderNo);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
