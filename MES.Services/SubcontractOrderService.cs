using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Printing;

namespace MES.Services;

public class SubcontractOrderService : ISubcontractOrderService
{
    private readonly AppDbContext _context;
    private readonly IPurchaseOrderService _purchaseService;

    public SubcontractOrderService(AppDbContext context, IPurchaseOrderService purchaseService)
    {
        _context = context;
        _purchaseService = purchaseService;
    }

    public async Task<PagedResult<SubcontractOrderDto>> GetPagedAsync(SubcontractQueryParams query)
    {
        var queryable = _context.SubcontractOrders
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;

            // 搜索供应商名称
            var matchedSupplierIds = await _context.SupplierProfiles
                .AsNoTracking()
                .Where(s => s.SupplierName.Contains(kw))
                .Select(s => s.Id)
                .ToListAsync();

            queryable = queryable.Where(s =>
                s.OrderNo.Contains(kw) ||
                s.ProcessType.Contains(kw) ||
                s.OutMaterialCategory.Contains(kw) ||
                s.OutPlantGrade.Contains(kw) ||
                s.OutSpecification.Contains(kw) ||
                matchedSupplierIds.Contains(s.SupplierId) ||
                (s.FurnaceNumber != null && s.FurnaceNumber.Contains(kw)) ||
                (s.Remark != null && s.Remark.Contains(kw)));
        }

        // 状态筛选
        if (!string.IsNullOrEmpty(query.Status) && Enum.TryParse<SubcontractOrderStatus>(query.Status, out var parsedStatus))
        {
            queryable = queryable.Where(s => s.Status == parsedStatus);
        }

        // 排序
        queryable = query.SortBy?.ToLower() switch
        {
            "orderno" => query.IsDescending
                ? queryable.OrderByDescending(s => s.OrderNo)
                : queryable.OrderBy(s => s.OrderNo),
            "orderdate" => query.IsDescending
                ? queryable.OrderByDescending(s => s.OrderDate)
                : queryable.OrderBy(s => s.OrderDate),
            "processtype" => query.IsDescending
                ? queryable.OrderByDescending(s => s.ProcessType)
                : queryable.OrderBy(s => s.ProcessType),
            "outmaterialcategory" => query.IsDescending
                ? queryable.OrderByDescending(s => s.OutMaterialCategory)
                : queryable.OrderBy(s => s.OutMaterialCategory),
            "outplantgrade" => query.IsDescending
                ? queryable.OrderByDescending(s => s.OutPlantGrade)
                : queryable.OrderBy(s => s.OutPlantGrade),
            "outspecification" => query.IsDescending
                ? queryable.OrderByDescending(s => s.OutSpecification)
                : queryable.OrderBy(s => s.OutSpecification),
            "outquantity" => query.IsDescending
                ? queryable.OrderByDescending(s => s.OutQuantity)
                : queryable.OrderBy(s => s.OutQuantity),
            "outweight" => query.IsDescending
                ? queryable.OrderByDescending(s => s.OutWeight)
                : queryable.OrderBy(s => s.OutWeight),
            "returndeadline" => query.IsDescending
                ? queryable.OrderByDescending(s => s.ReturnDeadline)
                : queryable.OrderBy(s => s.ReturnDeadline),
            "suppliername" => query.IsDescending
                ? queryable.Join(_context.SupplierProfiles, s => s.SupplierId, sp => sp.Id, (s, sp) => new { s, sp.SupplierName }).OrderByDescending(x => x.SupplierName).Select(x => x.s)
                : queryable.Join(_context.SupplierProfiles, s => s.SupplierId, sp => sp.Id, (s, sp) => new { s, sp.SupplierName }).OrderBy(x => x.SupplierName).Select(x => x.s),
            "status" => query.IsDescending
                ? queryable.OrderByDescending(s => s.Status)
                : queryable.OrderBy(s => s.Status),
            "isforcecompleted" => query.IsDescending
                ? queryable.OrderByDescending(s => s.IsForceCompleted)
                : queryable.OrderBy(s => s.IsForceCompleted),
            "furnacenumber" => query.IsDescending
                ? queryable.OrderByDescending(s => s.FurnaceNumber ?? "")
                : queryable.OrderBy(s => s.FurnaceNumber ?? ""),
            "inquantity" => query.IsDescending
                ? queryable.OrderByDescending(s => s.InQuantity ?? 0)
                : queryable.OrderBy(s => s.InQuantity ?? 0),
            "inweight" => query.IsDescending
                ? queryable.OrderByDescending(s => s.InWeight ?? 0)
                : queryable.OrderBy(s => s.InWeight ?? 0),
            "remark" => query.IsDescending
                ? queryable.OrderByDescending(s => s.Remark ?? "")
                : queryable.OrderBy(s => s.Remark ?? ""),
            _ => query.IsDescending
                ? queryable.OrderByDescending(s => s.CreatedTime)
                : queryable.OrderBy(s => s.CreatedTime)
        };

        var totalCount = await queryable.CountAsync();
        var entityList = await queryable
            .Include(s => s.ReturnItems.OrderBy(r => r.Sequence))
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        // 填充供应商名称
        var supplierIds = entityList.Where(i => i.SupplierId > 0).Select(i => i.SupplierId).Distinct().ToList();
        var suppliers = await _context.SupplierProfiles
            .AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.SupplierName);

        var items = entityList.Select(s =>
        {
            var dto = ToDto(s);
            if (suppliers.TryGetValue(s.SupplierId, out var name))
                dto.SupplierName = name;
            return dto;
        }).ToList();

        return new PagedResult<SubcontractOrderDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<SubcontractOrderDto> GetByIdAsync(int id)
    {
        var entity = await _context.SubcontractOrders
            .AsNoTracking()
            .Include(s => s.ReturnItems.OrderBy(r => r.Sequence))
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("委外单不存在");

        var dto = ToDto(entity);
        var supplier = await _context.SupplierProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == entity.SupplierId);
        if (supplier != null) dto.SupplierName = supplier.SupplierName;

        // 收集所有 ReturnItem 的 SourceWorkOrderNo，批量查询 WorkOrder
        var woNos = entity.ReturnItems
            .Where(r => !string.IsNullOrEmpty(r.SourceWorkOrderNo))
            .Select(r => r.SourceWorkOrderNo!)
            .Distinct()
            .ToList();

        var workOrders = new Dictionary<string, WorkOrder>();
        if (woNos.Count > 0)
        {
            workOrders = await _context.WorkOrders
                .AsNoTracking()
                .Where(w => woNos.Contains(w.WorkOrderNo))
                .ToDictionaryAsync(w => w.WorkOrderNo, w => w);
        }

        dto.ReturnItems = entity.ReturnItems.Select(r =>
        {
            var itemDto = new SubcontractReturnItemDto
            {
                Id = r.Id,
                SubcontractOrderId = r.SubcontractOrderId,
                Sequence = r.Sequence,
                MaterialCategory = r.MaterialCategory,
                PlantGrade = r.PlantGrade,
                ProcessSpecification = r.ProcessSpecification,
                UnitWeight = r.UnitWeight,
                RequiredQuantity = r.RequiredQuantity,
                RequiredWeight = r.RequiredWeight,
                ProcessStatusRemark = r.ProcessStatusRemark,
                Remark = r.Remark,
                ProcessUnitPrice = r.ProcessUnitPrice,
                ProcessTotalAmount = r.ProcessTotalAmount,
                SourceWorkOrderNo = r.SourceWorkOrderNo
            };

            // 按每个 ReturnItem 各自的 SourceWorkOrderNo 填充 Wo* 字段
            if (r.SourceWorkOrderNo != null && workOrders.TryGetValue(r.SourceWorkOrderNo, out var wo))
            {
                FillWorkOrderFields(itemDto, wo);
            }

            return itemDto;
        }).ToList();

        return dto;
    }

    public async Task<SubcontractOrderDto> CreateAsync(CreateSubcontractOrderRequest request)
    {
        if (request.ReturnItems == null || request.ReturnItems.Count == 0)
            throw new BusinessException("至少需要一条委外明细要求");

        // Serializable事务：防止并发读取到相同maxSeq导致唯一键冲突
        using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var orderNo = await GenerateOrderNoAsync();

            var entity = new SubcontractOrder
            {
                OrderNo = orderNo,
                SupplierId = request.SupplierId,
                OrderDate = request.OrderDate,
                ProcessType = request.ProcessType,
                FurnaceNumber = request.FurnaceNumber,
                OutMaterialCategory = request.OutMaterialCategory,
                OutPlantGrade = request.OutPlantGrade,
                OutSpecification = request.OutSpecification,
                OutQuantity = request.OutQuantity,
                OutWeight = request.OutWeight,
                ReturnDeadline = request.ReturnDeadline,
                Remark = request.Remark
            };

            int seq = 1;
            foreach (var item in request.ReturnItems)
            {
                entity.ReturnItems.Add(new SubcontractReturnItem
                {
                    Sequence = seq++,
                    MaterialCategory = item.MaterialCategory,
                    PlantGrade = item.PlantGrade,
                    ProcessSpecification = item.ProcessSpecification,
                    UnitWeight = item.UnitWeight,
                    RequiredQuantity = item.RequiredQuantity,
                    RequiredWeight = item.RequiredWeight,
                    ProcessStatusRemark = item.ProcessStatusRemark,
                    Remark = item.Remark,
                    ProcessUnitPrice = item.ProcessUnitPrice,
                    ProcessTotalAmount = item.ProcessTotalAmount,
                    SourceWorkOrderNo = item.SourceWorkOrderNo
                });
            }

            _context.SubcontractOrders.Add(entity);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var dto = ToDto(entity);
            var supplier = await _context.SupplierProfiles.FindAsync(entity.SupplierId);
            if (supplier != null) dto.SupplierName = supplier.SupplierName;
            dto.ReturnItems = entity.ReturnItems.Select(r => new SubcontractReturnItemDto
            {
                Id = r.Id,
                SubcontractOrderId = r.SubcontractOrderId,
                Sequence = r.Sequence,
                MaterialCategory = r.MaterialCategory,
                PlantGrade = r.PlantGrade,
                ProcessSpecification = r.ProcessSpecification,
                UnitWeight = r.UnitWeight,
                RequiredQuantity = r.RequiredQuantity,
                RequiredWeight = r.RequiredWeight,
                ProcessStatusRemark = r.ProcessStatusRemark,
                Remark = r.Remark,
                ProcessUnitPrice = r.ProcessUnitPrice,
                ProcessTotalAmount = r.ProcessTotalAmount
            }).ToList();

            return dto;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SubcontractOrderDto> UpdateAsync(int id, UpdateSubcontractOrderRequest request)
    {
        var entity = await _context.SubcontractOrders
            .Include(s => s.ReturnItems)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("委外单不存在");
        if (entity.Status == SubcontractOrderStatus.Cancelled) throw new BusinessException("已取消的委外单无法编辑");

        if (entity.Status == SubcontractOrderStatus.Completed)
        {
            // 已完成：仅允许修改明细中的来源工单号
            var itemSeq = 0;
            foreach (var item in request.ReturnItems)
            {
                if (itemSeq < entity.ReturnItems.Count)
                {
                    entity.ReturnItems[itemSeq].SourceWorkOrderNo = item.SourceWorkOrderNo;
                }
                itemSeq++;
            }
        }
        else
        {
            entity.SupplierId = request.SupplierId;
            entity.ProcessType = request.ProcessType;
            entity.FurnaceNumber = request.FurnaceNumber ?? entity.FurnaceNumber;
            entity.OutMaterialCategory = request.OutMaterialCategory;
            entity.OutPlantGrade = request.OutPlantGrade;
            entity.OutSpecification = request.OutSpecification;
            entity.OutQuantity = request.OutQuantity;
            entity.OutWeight = request.OutWeight;
            entity.ReturnDeadline = request.ReturnDeadline ?? entity.ReturnDeadline;
            entity.Remark = request.Remark ?? entity.Remark;

            // 全量替换子表
            _context.SubcontractReturnItems.RemoveRange(entity.ReturnItems);

            int seq = 1;
            foreach (var item in request.ReturnItems)
            {
                entity.ReturnItems.Add(new SubcontractReturnItem
                {
                    Sequence = seq++,
                    MaterialCategory = item.MaterialCategory,
                    PlantGrade = item.PlantGrade,
                    ProcessSpecification = item.ProcessSpecification,
                    UnitWeight = item.UnitWeight,
                    RequiredQuantity = item.RequiredQuantity,
                    RequiredWeight = item.RequiredWeight,
                    ProcessStatusRemark = item.ProcessStatusRemark,
                    Remark = item.Remark,
                    ProcessUnitPrice = item.ProcessUnitPrice,
                    ProcessTotalAmount = item.ProcessTotalAmount,
                    SourceWorkOrderNo = item.SourceWorkOrderNo
                });
            }
        }

        await _context.SaveChangesAsync();

        var dto = ToDto(entity);
        var supplier = await _context.SupplierProfiles.FindAsync(entity.SupplierId);
        if (supplier != null) dto.SupplierName = supplier.SupplierName;
        dto.ReturnItems = entity.ReturnItems.OrderBy(r => r.Sequence).Select(r => new SubcontractReturnItemDto
        {
            Id = r.Id,
            SubcontractOrderId = r.SubcontractOrderId,
            Sequence = r.Sequence,
            MaterialCategory = r.MaterialCategory,
            PlantGrade = r.PlantGrade,
            ProcessSpecification = r.ProcessSpecification,
            UnitWeight = r.UnitWeight,
            RequiredQuantity = r.RequiredQuantity,
            RequiredWeight = r.RequiredWeight,
            ProcessStatusRemark = r.ProcessStatusRemark,
            Remark = r.Remark,
            ProcessUnitPrice = r.ProcessUnitPrice,
            ProcessTotalAmount = r.ProcessTotalAmount,
            SourceWorkOrderNo = r.SourceWorkOrderNo
        }).ToList();

        return dto;
    }

    public async Task SyncAllAsync()
    {
        var orders = await _context.SubcontractOrders
            .Where(s => s.Status != SubcontractOrderStatus.Cancelled && s.Status != SubcontractOrderStatus.Completed)
            .ToListAsync();

        var orderNos = orders.Select(o => o.OrderNo).ToList();
        var batches = await _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.SourceOrderNo != null && orderNos.Contains(b.SourceOrderNo))
            .ToListAsync();

        foreach (var order in orders)
        {
            var orderBatches = batches.Where(b => b.SourceOrderNo == order.OrderNo).ToList();
            if (orderBatches.Count == 0) continue;

            order.InQuantity = orderBatches.Sum(b => b.InitialQuantity);
            order.InWeight = orderBatches.Sum(b => b.InitialWeight);

            if (!order.IsForceCompleted)
                RecalcSubcontractStatus(order);
        }

        await _context.SaveChangesAsync();
    }

    public async Task SyncSingleAsync(int id)
    {
        var order = await _context.SubcontractOrders
            .FirstOrDefaultAsync(s => s.Id == id);
        if (order == null) throw new BusinessException("委外单不存在");

        var batches = await _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.SourceOrderNo == order.OrderNo)
            .ToListAsync();

        order.InQuantity = batches.Sum(b => b.InitialQuantity);
        order.InWeight = batches.Sum(b => b.InitialWeight);

        if (!order.IsForceCompleted)
            RecalcSubcontractStatus(order);

        await _context.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(int id, UpdateOrderStatusRequest request)
    {
        var entity = await _context.SubcontractOrders
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("委外单不存在");

        entity.IsForceCompleted = request.IsForceCompleted;

        if (entity.IsForceCompleted)
            entity.Status = SubcontractOrderStatus.Completed;
        else
            RecalcSubcontractStatus(entity);

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.SubcontractOrders
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("委外单不存在");
        if (entity.Status == SubcontractOrderStatus.Completed) throw new BusinessException("已完成的委外单无法删除");
        if (entity.Status == SubcontractOrderStatus.Cancelled) throw new BusinessException("该委外单已取消");

        _context.SubcontractOrders.Remove(entity);
        await _context.SaveChangesAsync();
    }

    // ========== 用料计划执行状态 ==========

    public async Task<List<ProcurementStatusDto>> GetProcurementStatusAsync()
    {
        return await _purchaseService.GetPiercingProcurementStatusAsync();
    }

    public async Task<List<OrderMismatchInfo>> GetMismatchedSubcontractOrdersAsync()
    {
        // 1. 获取所有涉及采购的工单号
        var semiWoIds = await _context.PurchaseSemiPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0)
            .Select(p => p.WorkOrderId)
            .Distinct()
            .ToListAsync();

        var finishWoIds = await _context.PurchaseFinishedPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0)
            .Select(p => p.WorkOrderId)
            .Distinct()
            .ToListAsync();

        var allWoIds = semiWoIds.Union(finishWoIds).ToList();
        if (allWoIds.Count == 0)
            return new List<OrderMismatchInfo>();

        var validWorkOrderNos = (await _context.WorkOrders
            .AsNoTracking()
            .Where(w => allWoIds.Contains(w.Id))
            .Select(w => w.WorkOrderNo)
            .ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 2. 查询委外明细中 SourceWorkOrderNo 不为空的记录，关联主表获取委外单号
        var mismatchedItems = await _context.SubcontractReturnItems
            .AsNoTracking()
            .Where(r => r.SourceWorkOrderNo != null && r.SourceWorkOrderNo != "")
            .Select(r => new { r.SubcontractOrderId, r.SourceWorkOrderNo })
            .ToListAsync();

        // 3. 获取委外单号映射
        var subcontractIds = mismatchedItems.Select(r => r.SubcontractOrderId).Distinct().ToList();
        var orderNoMap = await _context.SubcontractOrders
            .AsNoTracking()
            .Where(o => subcontractIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.OrderNo);

        // 4. 找出不匹配的，按委外单号分组
        var mismatches = mismatchedItems
            .Where(r => !validWorkOrderNos.Contains(r.SourceWorkOrderNo!))
            .GroupBy(r => r.SubcontractOrderId)
            .Select(g => new OrderMismatchInfo
            {
                OrderNo = orderNoMap.GetValueOrDefault(g.Key, ""),
                MismatchedWorkOrderNos = g.Select(r => r.SourceWorkOrderNo!).Distinct().ToList()
            })
            .Where(m => !string.IsNullOrEmpty(m.OrderNo))
            .ToList();

        return mismatches;
    }

    public async Task<PlanDetailDto?> GetPlanDetailAsync(string workOrderNo, string materialCategory)
    {
        return await _purchaseService.GetPlanDetailAsync(workOrderNo, materialCategory);
    }

    // ========== 打印 ==========

    public async Task<byte[]> PrintOrderAsync(int id)
    {
        var dto = await GetByIdAsync(id);
        return SubcontractOrderPrintHelper.GeneratePdf(dto);
    }

    public async Task<byte[]> PrintOrderBatchAsync(int[] ids)
    {
        var orders = await GetByIdsAsync(ids);
        return SubcontractOrderPrintHelper.GenerateBatchPdf(orders);
    }

    public async Task<List<SubcontractOrderDto>> GetByIdsAsync(int[] ids)
    {
        var entities = await _context.SubcontractOrders
            .AsNoTracking()
            .Include(s => s.ReturnItems.OrderBy(r => r.Sequence))
            .Where(s => ids.Contains(s.Id))
            .ToListAsync();

        var supplierIds = entities.Where(e => e.SupplierId > 0).Select(e => e.SupplierId).Distinct().ToList();
        var suppliers = await _context.SupplierProfiles
            .AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.SupplierName);

        var woNos = entities.SelectMany(e => e.ReturnItems)
            .Where(r => !string.IsNullOrEmpty(r.SourceWorkOrderNo))
            .Select(r => r.SourceWorkOrderNo!)
            .Distinct()
            .ToList();
        var workOrders = new Dictionary<string, WorkOrder>();
        if (woNos.Count > 0)
        {
            workOrders = await _context.WorkOrders
                .AsNoTracking()
                .Where(w => woNos.Contains(w.WorkOrderNo))
                .ToDictionaryAsync(w => w.WorkOrderNo, w => w);
        }

        return entities.Select(e =>
        {
            var dto = ToDto(e);
            if (suppliers.TryGetValue(e.SupplierId, out var name))
                dto.SupplierName = name;

            dto.ReturnItems = e.ReturnItems.Select(r =>
            {
                var itemDto = new SubcontractReturnItemDto
                {
                    Id = r.Id,
                    SubcontractOrderId = r.SubcontractOrderId,
                    Sequence = r.Sequence,
                    MaterialCategory = r.MaterialCategory,
                    PlantGrade = r.PlantGrade,
                    ProcessSpecification = r.ProcessSpecification,
                    UnitWeight = r.UnitWeight,
                    RequiredQuantity = r.RequiredQuantity,
                    RequiredWeight = r.RequiredWeight,
                    ProcessStatusRemark = r.ProcessStatusRemark,
                    Remark = r.Remark,
                    ProcessUnitPrice = r.ProcessUnitPrice,
                    ProcessTotalAmount = r.ProcessTotalAmount,
                    SourceWorkOrderNo = r.SourceWorkOrderNo
                };
                if (r.SourceWorkOrderNo != null && workOrders.TryGetValue(r.SourceWorkOrderNo, out var wo))
                    FillWorkOrderFields(itemDto, wo);
                return itemDto;
            }).ToList();

            return dto;
        }).ToList();
    }

    public async Task<byte[]> PrintOrderAllAsync(string? keyword, string? sortBy = null, bool isDescending = false)
    {
        var query = new SubcontractQueryParams
        {
            PageIndex = 1,
            PageSize = 10000,
            Keyword = keyword,
            SortBy = sortBy ?? "CreatedTime",
            IsDescending = isDescending
        };
        var result = await GetPagedAsync(query);
        return SubcontractOrderPrintHelper.GenerateBatchPdf(result.Items);
    }

    // ========== 私有方法 ==========

    private static void RecalcSubcontractStatus(SubcontractOrder order)
    {
        if (order.InWeight == null || order.InWeight == 0)
            order.Status = SubcontractOrderStatus.Sent;
        else if (order.InWeight >= order.OutWeight * 0.95m)
            order.Status = SubcontractOrderStatus.Completed;
        else
            order.Status = SubcontractOrderStatus.PartialReturned;
    }

    private async Task<string> GenerateOrderNoAsync()
    {
        var today = DateTime.Now.ToString("yyMMdd");
        var prefix = $"WW{today}";
        var existingNos = await _context.SubcontractOrders
            .Where(s => s.OrderNo.StartsWith(prefix) && s.OrderNo.Length == prefix.Length + 3)
            .Select(s => s.OrderNo)
            .ToListAsync();

        int maxSeq = 0;
        foreach (var no in existingNos)
        {
            if (int.TryParse(no[^3..], out var s) && s > maxSeq)
                maxSeq = s;
        }

        return $"{prefix}{maxSeq + 1:D3}";
    }

    private static SubcontractOrderDto ToDto(SubcontractOrder entity) => new()
    {
        Id = entity.Id,
        OrderNo = entity.OrderNo,
        SupplierId = entity.SupplierId,
        SupplierName = "",
        OrderDate = entity.OrderDate,
        Status = entity.Status,
        IsForceCompleted = entity.IsForceCompleted,
        FurnaceNumber = entity.FurnaceNumber,
        ProcessType = entity.ProcessType,
        OutMaterialCategory = entity.OutMaterialCategory,
        OutPlantGrade = entity.OutPlantGrade,
        OutSpecification = entity.OutSpecification,
        OutQuantity = entity.OutQuantity,
        OutWeight = entity.OutWeight,
        ReturnDeadline = entity.ReturnDeadline,
        InQuantity = entity.InQuantity,
        InWeight = entity.InWeight,
        Remark = entity.Remark,
        CreatedTime = entity.CreatedTime
    };

    private static void FillWorkOrderFields(SubcontractOrderDto dto, WorkOrder wo)
    {
        dto.WoSalesOrderNo = wo.SalesOrderNo;
        dto.WoProductionMainNo = wo.ProductionMainNo;
        dto.WoProductionSubNo = wo.ProductionSubNo;
        dto.WoSignDate = wo.SignDate;
        dto.WoSalesman = wo.Salesman;
        dto.WoEndCustomer = wo.EndCustomer;
        dto.WoDeliveryDate = wo.DeliveryDate;
        dto.WoDelayPenalty = wo.DelayPenalty;
        dto.WoSettlementMethod = wo.SettlementMethod;
        dto.WoPlantGrade = wo.PlantGrade;
        dto.WoSpecification = wo.Specification;
        dto.WoLengthStatus = wo.LengthStatus;
        dto.WoMaxLength = wo.MaxLength;
        dto.WoTotalQuantity = wo.TotalQuantity;
        dto.WoTotalWeight = wo.TotalWeight;
        dto.WoDeliveryState = wo.DeliveryState;
        dto.WoTotalItemCount = wo.TotalItemCount;
    }

    private static void FillWorkOrderFields(SubcontractReturnItemDto dto, WorkOrder wo)
    {
        dto.WoSalesOrderNo = wo.SalesOrderNo;
        dto.WoProductionMainNo = wo.ProductionMainNo;
        dto.WoProductionSubNo = wo.ProductionSubNo;
        dto.WoSignDate = wo.SignDate;
        dto.WoSalesman = wo.Salesman;
        dto.WoEndCustomer = wo.EndCustomer;
        dto.WoDeliveryDate = wo.DeliveryDate;
        dto.WoDelayPenalty = wo.DelayPenalty;
        dto.WoSettlementMethod = wo.SettlementMethod;
        dto.WoPlantGrade = wo.PlantGrade;
        dto.WoSpecification = wo.Specification;
        dto.WoLengthStatus = wo.LengthStatus;
        dto.WoMaxLength = wo.MaxLength;
        dto.WoTotalQuantity = wo.TotalQuantity;
        dto.WoTotalWeight = wo.TotalWeight;
        dto.WoDeliveryState = wo.DeliveryState;
        dto.WoTotalItemCount = wo.TotalItemCount;
    }
}
