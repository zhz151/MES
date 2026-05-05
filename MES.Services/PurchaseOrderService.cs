using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Core.Enums;
using MES.Services.Printing;

namespace MES.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly AppDbContext _context;

    public PurchaseOrderService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<PurchaseOrderDto>> GetPagedAsync(PurchaseOrderQueryParams query)
    {
        var queryable = _context.PurchaseOrders
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;

            // 尝试按供应商名称搜索 → 查 SupplierId
            var matchedSupplierIds = await _context.SupplierProfiles
                .AsNoTracking()
                .Where(s => s.SupplierName.Contains(kw))
                .Select(s => s.Id)
                .ToListAsync();

            queryable = queryable.Where(p =>
                p.OrderNo.Contains(kw) ||
                p.MaterialCategory.Contains(kw) ||
                p.PlantGrade.Contains(kw) ||
                p.Specification.Contains(kw) ||
                (p.SourceWorkOrderNo != null && p.SourceWorkOrderNo.Contains(kw)) ||
                matchedSupplierIds.Contains(p.SupplierId));
        }

        // 状态筛选
        if (!string.IsNullOrEmpty(query.Status))
        {
            queryable = queryable.Where(p => p.Status == query.Status);
        }

        // 下单日期筛选
        if (query.DateFrom.HasValue)
        {
            var from = query.DateFrom.Value.Date;
            queryable = queryable.Where(p => p.OrderDate >= from);
        }
        if (query.DateTo.HasValue)
        {
            var to = query.DateTo.Value.Date.AddDays(1);
            queryable = queryable.Where(p => p.OrderDate < to);
        }

        // 要求到货日筛选
        if (query.RequiredDateFrom.HasValue)
        {
            var from = query.RequiredDateFrom.Value.Date;
            queryable = queryable.Where(p => p.RequiredDate >= from);
        }
        if (query.RequiredDateTo.HasValue)
        {
            var to = query.RequiredDateTo.Value.Date.AddDays(1);
            queryable = queryable.Where(p => p.RequiredDate < to);
        }

        queryable = query.SortBy?.ToLower() switch
        {
            "orderno" => query.IsDescending
                ? queryable.OrderByDescending(p => p.OrderNo)
                : queryable.OrderBy(p => p.OrderNo),
            "orderdate" => query.IsDescending
                ? queryable.OrderByDescending(p => p.OrderDate)
                : queryable.OrderBy(p => p.OrderDate),
            "requireddate" => query.IsDescending
                ? queryable.OrderByDescending(p => p.RequiredDate)
                : queryable.OrderBy(p => p.RequiredDate),
            "materialcategory" => query.IsDescending
                ? queryable.OrderByDescending(p => p.MaterialCategory)
                : queryable.OrderBy(p => p.MaterialCategory),
            "plantgrade" => query.IsDescending
                ? queryable.OrderByDescending(p => p.PlantGrade)
                : queryable.OrderBy(p => p.PlantGrade),
            "specification" => query.IsDescending
                ? queryable.OrderByDescending(p => p.Specification)
                : queryable.OrderBy(p => p.Specification),
            "sourceworkorderno" => query.IsDescending
                ? queryable.OrderByDescending(p => p.SourceWorkOrderNo ?? "")
                : queryable.OrderBy(p => p.SourceWorkOrderNo ?? ""),
            "suppliername" => query.IsDescending
                ? queryable.Join(_context.SupplierProfiles, p => p.SupplierId, s => s.Id, (p, s) => new { p, s.SupplierName }).OrderByDescending(x => x.SupplierName).Select(x => x.p)
                : queryable.Join(_context.SupplierProfiles, p => p.SupplierId, s => s.Id, (p, s) => new { p, s.SupplierName }).OrderBy(x => x.SupplierName).Select(x => x.p),
            "status" => query.IsDescending
                ? queryable.OrderByDescending(p => p.Status)
                : queryable.OrderBy(p => p.Status),
            _ => query.IsDescending
                ? queryable.OrderByDescending(p => p.CreatedTime)
                : queryable.OrderBy(p => p.CreatedTime)
        };

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(p => new PurchaseOrderDto
            {
                Id = p.Id,
                OrderNo = p.OrderNo,
                SupplierId = p.SupplierId,
                SupplierName = "",
                OrderDate = p.OrderDate,
                Status = p.Status,
                ManualStatus = p.ManualStatus,
                MaterialCategory = p.MaterialCategory,
                PlantGrade = p.PlantGrade,
                Specification = p.Specification,
                UnitWeight = p.UnitWeight,
                Quantity = p.Quantity,
                Weight = p.Weight,
                RequiredDate = p.RequiredDate,
                UnitPrice = p.UnitPrice,
                TotalAmount = p.TotalAmount,
                LastArrivalDate = p.LastArrivalDate,
                ReceivedQuantity = p.ReceivedQuantity,
                ReceivedWeight = p.ReceivedWeight,
                SourceWorkOrderNo = p.SourceWorkOrderNo,
                Remark = p.Remark,
                CreatedTime = p.CreatedTime
            })
            .ToListAsync();

        // 填充供应商名称
        var supplierIds = items.Where(i => i.SupplierId > 0).Select(i => i.SupplierId).Distinct().ToList();
        var suppliers = await _context.SupplierProfiles
            .AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.SupplierName);
        foreach (var item in items)
        {
            if (suppliers.TryGetValue(item.SupplierId, out var name))
                item.SupplierName = name;
        }

        return new PagedResult<PurchaseOrderDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<PurchaseOrderDto> GetByIdAsync(int id)
    {
        var entity = await _context.PurchaseOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) throw new BusinessException("采购单不存在");

        var dto = ToDto(entity);

        var supplier = await _context.SupplierProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == entity.SupplierId);
        if (supplier != null) dto.SupplierName = supplier.SupplierName;

        return dto;
    }

    public async Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request)
    {
        // Serializable事务：防止并发读取到相同maxSeq导致唯一键冲突
        using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var orderNo = await GenerateOrderNoAsync();

            var entity = new PurchaseOrder
            {
                OrderNo = orderNo,
                SupplierId = request.SupplierId,
                OrderDate = request.OrderDate,
                MaterialCategory = request.MaterialCategory,
                PlantGrade = request.PlantGrade,
                Specification = request.Specification,
                UnitWeight = request.UnitWeight,
                Quantity = request.Quantity,
                Weight = request.Weight,
                RequiredDate = request.RequiredDate,
                UnitPrice = request.UnitPrice,
                SourceWorkOrderNo = request.SourceWorkOrderNo,
                Remark = request.Remark
            };

            // 计算总金额
            if (request.Quantity.HasValue && request.UnitPrice.HasValue)
                entity.TotalAmount = request.Quantity.Value * request.UnitPrice.Value;

            _context.PurchaseOrders.Add(entity);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var dto = ToDto(entity);
            var supplier = await _context.SupplierProfiles.FindAsync(entity.SupplierId);
            if (supplier != null) dto.SupplierName = supplier.SupplierName;
            return dto;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<PurchaseOrderDto>> CreateBatchAsync(List<CreatePurchaseOrderRequest> requests)
    {
        if (requests == null || requests.Count == 0)
            throw new BusinessException("请求列表不能为空");

        // Serializable事务：防止并发读取到相同maxSeq导致唯一键冲突
        using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            // 一次查询，批量生成所有唯一单号（基于数值计算，避免字符串排序的009>010问题）
            var today = DateTime.Now.ToString("yyMMdd");
            var prefix = $"CG{today}";
            var existingNos = await _context.PurchaseOrders
                .Where(p => p.OrderNo.StartsWith(prefix) && p.OrderNo.Length == prefix.Length + 3)
                .Select(p => p.OrderNo)
                .ToListAsync();

            int maxSeq = 0;
            foreach (var no in existingNos)
            {
                if (int.TryParse(no[^3..], out var s) && s > maxSeq)
                    maxSeq = s;
            }
            int seq = maxSeq + 1;

            var entities = new List<PurchaseOrder>();
            foreach (var request in requests)
            {
                var orderNo = $"{prefix}{seq:D3}";
                seq++;

                var entity = new PurchaseOrder
                {
                    OrderNo = orderNo,
                    SupplierId = request.SupplierId,
                    OrderDate = request.OrderDate,
                    MaterialCategory = request.MaterialCategory,
                    PlantGrade = request.PlantGrade,
                    Specification = request.Specification,
                    UnitWeight = request.UnitWeight,
                    Quantity = request.Quantity,
                    Weight = request.Weight,
                    RequiredDate = request.RequiredDate,
                    UnitPrice = request.UnitPrice,
                    SourceWorkOrderNo = request.SourceWorkOrderNo,
                    Remark = request.Remark
                };

                if (request.Quantity.HasValue && request.UnitPrice.HasValue)
                    entity.TotalAmount = request.Quantity.Value * request.UnitPrice.Value;

                _context.PurchaseOrders.Add(entity);
                entities.Add(entity);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // 批量查询供应商名
            var supplierIds = entities.Select(e => e.SupplierId).Distinct().ToList();
            var suppliers = await _context.SupplierProfiles
                .Where(s => supplierIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.SupplierName);

            return entities.Select(e =>
            {
                var dto = ToDto(e);
                if (suppliers.TryGetValue(e.SupplierId, out var name))
                    dto.SupplierName = name;
                return dto;
            }).ToList();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<PurchaseOrderDto> UpdateAsync(int id, UpdatePurchaseOrderRequest request)
    {
        var entity = await _context.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) throw new BusinessException("采购单不存在");
        if (entity.Status == "Cancelled") throw new BusinessException("已取消的采购单无法编辑");

        entity.SupplierId = request.SupplierId;
        entity.MaterialCategory = request.MaterialCategory;
        entity.PlantGrade = request.PlantGrade;
        entity.Specification = request.Specification;
        entity.UnitWeight = request.UnitWeight;
        entity.Quantity = request.Quantity;
        entity.Weight = request.Weight;
        entity.RequiredDate = request.RequiredDate;
        entity.UnitPrice = request.UnitPrice;
        entity.SourceWorkOrderNo = request.SourceWorkOrderNo;
        entity.Remark = request.Remark;

        // 重新计算总金额
        if (request.Quantity.HasValue && request.UnitPrice.HasValue)
            entity.TotalAmount = request.Quantity.Value * request.UnitPrice.Value;
        else
            entity.TotalAmount = null;

        // 如果 ManualStatus 未变时重新计算状态
        if (string.IsNullOrEmpty(entity.ManualStatus))
            RecalcPurchaseStatus(entity);

        await _context.SaveChangesAsync();

        var dto = ToDto(entity);
        var supplier = await _context.SupplierProfiles.FindAsync(entity.SupplierId);
        if (supplier != null) dto.SupplierName = supplier.SupplierName;
        return dto;
    }

    public async Task SyncAllAsync()
    {
        var orders = await _context.PurchaseOrders
            .Where(p => p.Status != "Cancelled" && p.Status != "Completed")
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

            order.ReceivedQuantity = orderBatches.Sum(b => b.InitialQuantity);
            order.ReceivedWeight = orderBatches.Sum(b => b.InitialWeight);
            order.LastArrivalDate = orderBatches.Max(b => b.InboundDate);

            if (string.IsNullOrEmpty(order.ManualStatus))
                RecalcPurchaseStatus(order);
        }

        await _context.SaveChangesAsync();
    }

    public async Task SyncSingleAsync(int id)
    {
        var order = await _context.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == id);
        if (order == null) throw new BusinessException("采购单不存在");

        var batches = await _context.InventoryBatches
            .AsNoTracking()
            .Where(b => b.SourceOrderNo == order.OrderNo)
            .ToListAsync();

        order.ReceivedQuantity = batches.Sum(b => b.InitialQuantity);
        order.ReceivedWeight = batches.Sum(b => b.InitialWeight);
        order.LastArrivalDate = batches.Count > 0 ? batches.Max(b => b.InboundDate) : null;

        if (string.IsNullOrEmpty(order.ManualStatus))
            RecalcPurchaseStatus(order);

        await _context.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(int id, UpdateOrderStatusRequest request)
    {
        var entity = await _context.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) throw new BusinessException("采购单不存在");

        entity.ManualStatus = request.ManualStatus;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) throw new BusinessException("采购单不存在");
        if (entity.Status == "Completed") throw new BusinessException("已完成的采购单无法删除");
        if (entity.Status == "Cancelled") throw new BusinessException("该采购单已取消");

        _context.PurchaseOrders.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private static void RecalcPurchaseStatus(PurchaseOrder order)
    {
        if (order.ReceivedQuantity == 0)
            order.Status = "Open";
        else if (order.Quantity.HasValue && order.ReceivedQuantity >= order.Quantity.Value)
            order.Status = "Completed";
        else
            order.Status = "Partial";
    }

    private async Task<string> GenerateOrderNoAsync()
    {
        var today = DateTime.Now.ToString("yyMMdd");
        var prefix = $"CG{today}";
        var existingNos = await _context.PurchaseOrders
            .Where(p => p.OrderNo.StartsWith(prefix) && p.OrderNo.Length == prefix.Length + 3)
            .Select(p => p.OrderNo)
            .ToListAsync();

        int maxSeq = 0;
        foreach (var no in existingNos)
        {
            if (int.TryParse(no[^3..], out var s) && s > maxSeq)
                maxSeq = s;
        }

        return $"{prefix}{maxSeq + 1:D3}";
    }

    private static PurchaseOrderDto ToDto(PurchaseOrder entity) => new()
    {
        Id = entity.Id,
        OrderNo = entity.OrderNo,
        SupplierId = entity.SupplierId,
        SupplierName = "",
        OrderDate = entity.OrderDate,
        Status = entity.Status,
        ManualStatus = entity.ManualStatus,
        MaterialCategory = entity.MaterialCategory,
        PlantGrade = entity.PlantGrade,
        Specification = entity.Specification,
        UnitWeight = entity.UnitWeight,
        Quantity = entity.Quantity,
        Weight = entity.Weight,
        RequiredDate = entity.RequiredDate,
        UnitPrice = entity.UnitPrice,
        TotalAmount = entity.TotalAmount,
        LastArrivalDate = entity.LastArrivalDate,
        ReceivedQuantity = entity.ReceivedQuantity,
        ReceivedWeight = entity.ReceivedWeight,
        SourceWorkOrderNo = entity.SourceWorkOrderNo,
        Remark = entity.Remark,
        CreatedTime = entity.CreatedTime
    };

    public async Task<List<ProcurementStatusDto>> GetProcurementStatusAsync()
    {
        // 1. 分别查询两个表的 WorkOrderId（避免 Union 不同 DbSet 导致 EF Core 翻译失败）
        var semiWorkOrderIds = await _context.PurchaseSemiPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0)
            .Select(p => p.WorkOrderId)
            .Distinct()
            .ToListAsync();

        var finishedWorkOrderIds = await _context.PurchaseFinishedPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0)
            .Select(p => p.WorkOrderId)
            .Distinct()
            .ToListAsync();

        var workOrderIds = semiWorkOrderIds.Union(finishedWorkOrderIds).ToList();
        if (workOrderIds.Count == 0)
            return new List<ProcurementStatusDto>();

        // 2. 获取工单号映射
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(w => workOrderIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.WorkOrderNo);

        var allWorkOrderNos = workOrders.Values.ToList();

        // 3. 原料采购计划：按工单号+原料类型汇总
        var semiPlanData = await _context.PurchaseSemiPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0 && workOrderIds.Contains(p.WorkOrderId))
            .GroupBy(p => new { p.WorkOrderId, p.RawMaterialType })
            .Select(g => new
            {
                g.Key.WorkOrderId,
                CategoryName = g.Key.RawMaterialType == RawMaterialType.SemiFinished ? "荒管" : "半成品",
                PlanWeight = g.Sum(p => p.RequiredWeight)
            })
            .ToListAsync();

        // 4. 成品采购计划：按工单号+成品类型汇总
        var finishedPlanData = await _context.PurchaseFinishedPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0 && workOrderIds.Contains(p.WorkOrderId))
            .GroupBy(p => new { p.WorkOrderId, p.ProductType })
            .Select(g => new
            {
                g.Key.WorkOrderId,
                CategoryName = g.Key.ProductType == FinishedProductType.Critical ? "临界成品" : "订单成品",
                PlanWeight = g.Sum(p => p.RequiredWeight)
            })
            .ToListAsync();

        // 5. 按工单号聚合已采购重量
        var purchaseWeights = new Dictionary<string, decimal>();
        if (allWorkOrderNos.Count > 0)
        {
            var purchaseData = await _context.PurchaseOrders
                .AsNoTracking()
                .Where(p => p.SourceWorkOrderNo != null && allWorkOrderNos.Contains(p.SourceWorkOrderNo))
                .GroupBy(p => p.SourceWorkOrderNo!)
                .Select(g => new { WorkOrderNo = g.Key, Weight = g.Sum(p => p.Weight) })
                .ToListAsync();
            purchaseWeights = purchaseData.ToDictionary(x => x.WorkOrderNo, x => x.Weight);
        }

        // 6. 按工单号+物料分类聚合已委外重量
        var subcontractWeights = new Dictionary<string, decimal>();
        if (allWorkOrderNos.Count > 0)
        {
            var subcontractData = await _context.SubcontractReturnItems
                .AsNoTracking()
                .Where(r => r.SourceWorkOrderNo != null && allWorkOrderNos.Contains(r.SourceWorkOrderNo))
                .GroupBy(r => new { SourceWorkOrderNo = r.SourceWorkOrderNo!, r.MaterialCategory })
                .Select(g => new { g.Key.SourceWorkOrderNo, g.Key.MaterialCategory, Weight = g.Sum(r => r.RequiredWeight ?? 0) })
                .ToListAsync();
            subcontractWeights = subcontractData
                .ToDictionary(x => $"{x.SourceWorkOrderNo}|{x.MaterialCategory}", x => x.Weight);
        }

        // 7. 合并原料+成品计划数据，计算采购执行状态
        var allPlanData = semiPlanData.Concat(finishedPlanData)
            .Select(x =>
            {
                var workOrderNo = workOrders.GetValueOrDefault(x.WorkOrderId, "");
                var purchaseW = purchaseWeights.GetValueOrDefault(workOrderNo, 0);
                var subcontractW = subcontractWeights.GetValueOrDefault($"{workOrderNo}|{x.CategoryName}", 0);
                var total = purchaseW + subcontractW;
                return new ProcurementStatusDto
                {
                    WorkOrderNo = workOrderNo,
                    MaterialName = workOrderNo,
                    MaterialCategory = x.CategoryName,
                    PlanWeight = x.PlanWeight,
                    PurchaseWeight = purchaseW,
                    SubcontractWeight = subcontractW,
                    TotalWeight = total,
                    StatusText = total == 0 ? "未采购"
                        : total < x.PlanWeight ? "部分采购"
                        : "已采购"
                };
            })
            .Where(x => x.StatusText != "已采购")
            .OrderBy(x => x.WorkOrderNo)
            .ThenBy(x => x.MaterialCategory)
            .ToList();

        return allPlanData;
    }

    public async Task<PlanDetailDto?> GetPlanDetailAsync(string workOrderNo, string materialCategory)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.WorkOrderNo == workOrderNo);
        if (workOrder == null) return null;

        // 原料采购（荒管/半成品）
        if (materialCategory == "荒管" || materialCategory == "半成品")
        {
            var semiPlan = await _context.PurchaseSemiPlans
                .AsNoTracking()
                .Where(p => p.WorkOrderId == workOrder.Id && p.RequiredWeight > 0)
                .FirstOrDefaultAsync();
            if (semiPlan == null) return null;

            return new PlanDetailDto
            {
                WorkOrderNo = workOrderNo,
                MaterialCategory = materialCategory,
                PlantGrade = semiPlan.PlantGrade,
                Specification = semiPlan.RawMaterialSpec,
                UnitWeight = semiPlan.RequiredUnitWeight,
                Quantity = semiPlan.RequiredPieces,
                Weight = semiPlan.RequiredWeight,
                Remark = semiPlan.Remark,
                RequiredDate = semiPlan.RequiredDate
            };
        }

        // 成品采购（临界成品/订单成品）
        var finishedPlan = await _context.PurchaseFinishedPlans
            .AsNoTracking()
            .Where(p => p.WorkOrderId == workOrder.Id && p.RequiredWeight > 0)
            .FirstOrDefaultAsync();
        if (finishedPlan == null) return null;

        return new PlanDetailDto
        {
            WorkOrderNo = workOrderNo,
            MaterialCategory = materialCategory,
            PlantGrade = finishedPlan.PlantGrade,
            Specification = finishedPlan.Specification,
            Quantity = finishedPlan.RequiredPiece,
            Weight = finishedPlan.RequiredWeight,
            Remark = finishedPlan.Remark,
            RequiredDate = finishedPlan.RequiredDate
        };
    }

    // ========== 打印 ==========

    public async Task<byte[]> PrintOrderAsync(int id)
    {
        var dto = await GetByIdAsync(id);
        return PurchaseOrderPrintHelper.GeneratePdf(dto);
    }

    public async Task<byte[]> PrintOrderBatchAsync(int[] ids)
    {
        var result = new List<PurchaseOrderDto>();
        foreach (var id in ids)
        {
            try { result.Add(await GetByIdAsync(id)); }
            catch (BusinessException) { }
        }
        return PurchaseOrderPrintHelper.GenerateBatchPdf(result);
    }

    public async Task<byte[]> PrintOrderAllAsync(string? keyword, string? sortBy = null, bool isDescending = false)
    {
        var query = new PurchaseOrderQueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy,
            IsDescending = isDescending
        };
        var paged = await GetPagedAsync(query);
        return PurchaseOrderPrintHelper.GenerateBatchPdf(paged.Items);
    }
}
