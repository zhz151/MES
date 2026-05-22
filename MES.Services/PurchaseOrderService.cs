using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Core.Enums;
using MES.Services.Helpers;
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

            // 搜索工单关联字段
            var matchedWoNos = await _context.WorkOrders
                .AsNoTracking()
                .Where(w =>
                    w.SalesOrderNo.Contains(kw) ||
                    w.Salesman.Contains(kw) ||
                    w.ProductionMainNo.Contains(kw) ||
                    (w.ProductionSubNo != null && w.ProductionSubNo.Contains(kw)) ||
                    (w.EndCustomer != null && w.EndCustomer.Contains(kw)) ||
                    w.PlantGrade.Contains(kw) ||
                    w.Specification.Contains(kw))
                .Select(w => w.WorkOrderNo)
                .ToListAsync();

            queryable = queryable.Where(p =>
                p.OrderNo.Contains(kw) ||
                p.MaterialCategory.Contains(kw) ||
                p.PlantGrade.Contains(kw) ||
                p.Specification.Contains(kw) ||
                (p.SourceWorkOrderNo != null && p.SourceWorkOrderNo.Contains(kw)) ||
                (p.SourceWorkOrderNo != null && matchedWoNos.Contains(p.SourceWorkOrderNo)) ||
                matchedSupplierIds.Contains(p.SupplierId) ||
                (p.Remark != null && p.Remark.Contains(kw)));
        }

        // 状态筛选
        if (!string.IsNullOrEmpty(query.Status) && Enum.TryParse<PurchaseOrderStatus>(query.Status, out var parsedStatus))
        {
            queryable = queryable.Where(p => p.Status == parsedStatus);
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

        queryable = queryable.ApplyFilters(query.Filters);

        // 跨表计算字段筛选（非 PurchaseOrder 直接属性）
        if (query.Filters is { Count: > 0 })
        {
            foreach (var filter in query.Filters)
            {
                if (string.IsNullOrWhiteSpace(filter.Field)) continue;
                switch (filter.Field.ToLower())
                {
                    case "suppliername":
                        if (!string.IsNullOrEmpty(filter.Value))
                            queryable = queryable.Where(p => _context.SupplierProfiles.Any(s => s.Id == p.SupplierId && s.SupplierName.Contains(filter.Value)));
                        break;
                    case "wosalesorderno":
                        if (!string.IsNullOrEmpty(filter.Value))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.SalesOrderNo.Contains(filter.Value)));
                        break;
                    case "woproductionmainno":
                        if (!string.IsNullOrEmpty(filter.Value))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.ProductionMainNo.Contains(filter.Value)));
                        break;
                    case "wosigndate":
                        if (DateTime.TryParse(filter.From?.ToString(), out var wsdFrom))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.SignDate >= wsdFrom));
                        if (DateTime.TryParse(filter.To?.ToString(), out var wsdTo))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.SignDate <= wsdTo));
                        break;
                    case "wosalesman":
                        if (!string.IsNullOrEmpty(filter.Value))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.Salesman.Contains(filter.Value)));
                        break;
                    case "wodeliverydate":
                        if (DateTime.TryParse(filter.From?.ToString(), out var wddFrom))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.DeliveryDate >= wddFrom));
                        if (DateTime.TryParse(filter.To?.ToString(), out var wddTo))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.DeliveryDate <= wddTo));
                        break;
                    case "woplantgrade":
                        if (!string.IsNullOrEmpty(filter.Value))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.PlantGrade.Contains(filter.Value)));
                        break;
                    case "wospecification":
                        if (!string.IsNullOrEmpty(filter.Value))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.Specification.Contains(filter.Value)));
                        break;
                    case "womaxlength":
                        if (decimal.TryParse(filter.From?.ToString(), out var wmlMin))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.MaxLength >= wmlMin));
                        if (decimal.TryParse(filter.To?.ToString(), out var wmlMax))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.MaxLength <= wmlMax));
                        break;
                    case "wototalquantity":
                        if (decimal.TryParse(filter.From?.ToString(), out var wtqMin))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.TotalQuantity >= wtqMin));
                        if (decimal.TryParse(filter.To?.ToString(), out var wtqMax))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.TotalQuantity <= wtqMax));
                        break;
                    case "wototalweight":
                        if (decimal.TryParse(filter.From?.ToString(), out var wtwMin))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.TotalWeight >= wtwMin));
                        if (decimal.TryParse(filter.To?.ToString(), out var wtwMax))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.TotalWeight <= wtwMax));
                        break;
                    case "wototalitemcount":
                        if (int.TryParse(filter.From?.ToString(), out var wicMin))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.TotalItemCount >= wicMin));
                        if (int.TryParse(filter.To?.ToString(), out var wicMax))
                            queryable = queryable.Where(p => _context.WorkOrders.Any(wo => wo.WorkOrderNo == p.SourceWorkOrderNo && wo.TotalItemCount <= wicMax));
                        break;
                }
            }
        }

        // 工单来源字段排序（先提取带 LEFT JOIN 的 queryable）
        var withWorkOrder = queryable.GroupJoin(
            _context.WorkOrders.AsNoTracking(),
            p => p.SourceWorkOrderNo,
            w => w.WorkOrderNo,
            (p, wg) => new { p, w = wg.FirstOrDefault()! });

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
            "wosalesorderno" => query.IsDescending
                ? withWorkOrder.OrderByDescending(x => x.w.SalesOrderNo ?? "").Select(x => x.p)
                : withWorkOrder.OrderBy(x => x.w.SalesOrderNo ?? "").Select(x => x.p),
            "woproductionmainno" => query.IsDescending
                ? withWorkOrder.OrderByDescending(x => x.w.ProductionMainNo ?? "").Select(x => x.p)
                : withWorkOrder.OrderBy(x => x.w.ProductionMainNo ?? "").Select(x => x.p),
            "wosigndate" => query.IsDescending
                ? withWorkOrder.OrderByDescending(x => x.w.SignDate).Select(x => x.p)
                : withWorkOrder.OrderBy(x => x.w.SignDate).Select(x => x.p),
            "wosalesman" => query.IsDescending
                ? withWorkOrder.OrderByDescending(x => x.w.Salesman ?? "").Select(x => x.p)
                : withWorkOrder.OrderBy(x => x.w.Salesman ?? "").Select(x => x.p),
            "wodeliverydate" => query.IsDescending
                ? withWorkOrder.OrderByDescending(x => x.w.DeliveryDate).Select(x => x.p)
                : withWorkOrder.OrderBy(x => x.w.DeliveryDate).Select(x => x.p),
            "woplantgrade" => query.IsDescending
                ? withWorkOrder.OrderByDescending(x => x.w.PlantGrade ?? "").Select(x => x.p)
                : withWorkOrder.OrderBy(x => x.w.PlantGrade ?? "").Select(x => x.p),
            "wospecification" => query.IsDescending
                ? withWorkOrder.OrderByDescending(x => x.w.Specification ?? "").Select(x => x.p)
                : withWorkOrder.OrderBy(x => x.w.Specification ?? "").Select(x => x.p),
            "womaxlength" => query.IsDescending
                ? withWorkOrder.OrderByDescending(x => x.w.MaxLength).Select(x => x.p)
                : withWorkOrder.OrderBy(x => x.w.MaxLength).Select(x => x.p),
            "wototalquantity" => query.IsDescending
                ? withWorkOrder.OrderByDescending(x => x.w.TotalQuantity).Select(x => x.p)
                : withWorkOrder.OrderBy(x => x.w.TotalQuantity).Select(x => x.p),
            "wototalweight" => query.IsDescending
                ? withWorkOrder.OrderByDescending(x => x.w.TotalWeight).Select(x => x.p)
                : withWorkOrder.OrderBy(x => x.w.TotalWeight).Select(x => x.p),
            "wototalitemcount" => query.IsDescending
                ? withWorkOrder.OrderByDescending(x => x.w.TotalItemCount).Select(x => x.p)
                : withWorkOrder.OrderBy(x => x.w.TotalItemCount).Select(x => x.p),
            "unitweight" => query.IsDescending
                ? queryable.OrderByDescending(p => p.UnitWeight ?? 0)
                : queryable.OrderBy(p => p.UnitWeight ?? 0),
            "quantity" => query.IsDescending
                ? queryable.OrderByDescending(p => p.Quantity)
                : queryable.OrderBy(p => p.Quantity),
            "weight" => query.IsDescending
                ? queryable.OrderByDescending(p => p.Weight)
                : queryable.OrderBy(p => p.Weight),
            "unitprice" => query.IsDescending
                ? queryable.OrderByDescending(p => p.UnitPrice ?? 0)
                : queryable.OrderBy(p => p.UnitPrice ?? 0),
            "totalamount" => query.IsDescending
                ? queryable.OrderByDescending(p => p.TotalAmount ?? 0)
                : queryable.OrderBy(p => p.TotalAmount ?? 0),
            "lastarrivaldate" => query.IsDescending
                ? queryable.OrderByDescending(p => p.LastArrivalDate)
                : queryable.OrderBy(p => p.LastArrivalDate),
            "receivedquantity" => query.IsDescending
                ? queryable.OrderByDescending(p => p.ReceivedQuantity)
                : queryable.OrderBy(p => p.ReceivedQuantity),
            "receivedweight" => query.IsDescending
                ? queryable.OrderByDescending(p => p.ReceivedWeight)
                : queryable.OrderBy(p => p.ReceivedWeight),
            "isforcecompleted" => query.IsDescending
                ? queryable.OrderByDescending(p => p.IsForceCompleted)
                : queryable.OrderBy(p => p.IsForceCompleted),
            "remark" => query.IsDescending
                ? queryable.OrderByDescending(p => p.Remark ?? "")
                : queryable.OrderBy(p => p.Remark ?? ""),
            _ => query.IsDescending
                ? queryable.OrderByDescending(p => p.CreatedTime)
                : queryable.OrderBy(p => p.CreatedTime)
        };

        var totalCount = await queryable.CountAsync();
        var entityList = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync();

        // 填充工单来源字段
        var sourceWoNos = entityList.Where(e => e.SourceWorkOrderNo != null).Select(e => e.SourceWorkOrderNo!).Distinct().ToList();
        var workOrders = new Dictionary<string, WorkOrder>();
        if (sourceWoNos.Count > 0)
        {
            workOrders = await _context.WorkOrders
                .AsNoTracking()
                .Where(w => sourceWoNos.Contains(w.WorkOrderNo))
                .ToDictionaryAsync(w => w.WorkOrderNo, w => w);
        }

        var items = entityList.Select(p =>
        {
            var dto = ToDto(p);
            if (p.SourceWorkOrderNo != null && workOrders.TryGetValue(p.SourceWorkOrderNo, out var wo))
                FillWorkOrderFields(dto, wo);
            return dto;
        }).ToList();

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

    public async Task<List<PurchaseOrderDto>> GetAllListAsync()
    {
        var entityList = await _context.PurchaseOrders
            .AsNoTracking()
            .OrderBy(p => p.OrderDate)
            .ThenBy(p => p.OrderNo)
            .ToListAsync();

        // 填充工单来源字段
        var sourceWoNos = entityList.Where(e => e.SourceWorkOrderNo != null).Select(e => e.SourceWorkOrderNo!).Distinct().ToList();
        var workOrders = new Dictionary<string, WorkOrder>();
        if (sourceWoNos.Count > 0)
        {
            workOrders = await _context.WorkOrders
                .AsNoTracking()
                .Where(w => sourceWoNos.Contains(w.WorkOrderNo))
                .ToDictionaryAsync(w => w.WorkOrderNo, w => w);
        }

        var items = entityList.Select(p =>
        {
            var dto = ToDto(p);
            if (p.SourceWorkOrderNo != null && workOrders.TryGetValue(p.SourceWorkOrderNo, out var wo))
                FillWorkOrderFields(dto, wo);
            return dto;
        }).ToList();

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

        return items;
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

        // 填充工单来源字段
        if (!string.IsNullOrEmpty(entity.SourceWorkOrderNo))
        {
            var workOrder = await _context.WorkOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.WorkOrderNo == entity.SourceWorkOrderNo);
            if (workOrder != null)
                FillWorkOrderFields(dto, workOrder);
        }

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
        if (entity.Status == PurchaseOrderStatus.Cancelled) throw new BusinessException("已取消的采购单无法编辑");

        if (entity.Status == PurchaseOrderStatus.Completed)
        {
            // 已完成：仅允许修改来源工单号
            entity.SourceWorkOrderNo = request.SourceWorkOrderNo ?? entity.SourceWorkOrderNo;
        }
        else
        {
            entity.SupplierId = request.SupplierId;
            entity.MaterialCategory = request.MaterialCategory;
            entity.PlantGrade = request.PlantGrade;
            entity.Specification = request.Specification;
            entity.UnitWeight = request.UnitWeight ?? entity.UnitWeight;
            entity.Quantity = request.Quantity ?? entity.Quantity;
            entity.Weight = request.Weight;
            entity.RequiredDate = request.RequiredDate;
            entity.UnitPrice = request.UnitPrice ?? entity.UnitPrice;
            entity.SourceWorkOrderNo = request.SourceWorkOrderNo ?? entity.SourceWorkOrderNo;
            entity.Remark = request.Remark ?? entity.Remark;

            // 重新计算总金额
            if (request.Quantity.HasValue && request.UnitPrice.HasValue)
                entity.TotalAmount = request.Quantity.Value * request.UnitPrice.Value;
            else
                entity.TotalAmount = null;

            // 非强制完成时自动计算状态
            if (!entity.IsForceCompleted)
                RecalcPurchaseStatus(entity);
        }

        await _context.SaveChangesAsync();

        var dto = ToDto(entity);
        var supplier = await _context.SupplierProfiles.FindAsync(entity.SupplierId);
        if (supplier != null) dto.SupplierName = supplier.SupplierName;
        return dto;
    }

    public async Task SyncAllAsync()
    {
        var orders = await _context.PurchaseOrders
            .Where(p => p.Status != PurchaseOrderStatus.Cancelled && p.Status != PurchaseOrderStatus.Completed)
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

            if (!order.IsForceCompleted)
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

        if (!order.IsForceCompleted)
            RecalcPurchaseStatus(order);

        await _context.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(int id, UpdateOrderStatusRequest request)
    {
        var entity = await _context.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) throw new BusinessException("采购单不存在");

        entity.IsForceCompleted = request.IsForceCompleted;

        if (entity.IsForceCompleted)
            entity.Status = PurchaseOrderStatus.Completed;
        else
            RecalcPurchaseStatus(entity);

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null) throw new BusinessException("采购单不存在");
        if (entity.Status == PurchaseOrderStatus.Completed) throw new BusinessException("已完成的采购单无法删除");
        if (entity.Status == PurchaseOrderStatus.Cancelled) throw new BusinessException("该采购单已取消");

        _context.PurchaseOrders.Remove(entity);
        await _context.SaveChangesAsync();
    }

    private static void RecalcPurchaseStatus(PurchaseOrder order)
    {
        if (order.ReceivedQuantity == 0)
            order.Status = PurchaseOrderStatus.Open;
        else if (order.Quantity.HasValue && order.ReceivedQuantity >= order.Quantity.Value)
            order.Status = PurchaseOrderStatus.Completed;
        else
            order.Status = PurchaseOrderStatus.Partial;
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
        IsForceCompleted = entity.IsForceCompleted,
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

    private static void FillWorkOrderFields(PurchaseOrderDto dto, WorkOrder wo)
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
            purchaseWeights = purchaseData.ToDictionary(x => x.WorkOrderNo, x => x.Weight, StringComparer.OrdinalIgnoreCase);
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
                .ToDictionary(x => $"{x.SourceWorkOrderNo}|{x.MaterialCategory}", x => x.Weight, StringComparer.OrdinalIgnoreCase);
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
            .Where(x => x.StatusText != "已采购" && !string.IsNullOrEmpty(x.WorkOrderNo))
            .OrderBy(x => x.WorkOrderNo)
            .ThenBy(x => x.MaterialCategory)
            .ToList();

        return allPlanData;
    }

    public async Task<List<ProcurementStatusDto>> GetPiercingProcurementStatusAsync()
    {
        // 1. 查询圆棒穿孔计划的工单ID
        var piercingWorkOrderIds = await _context.RoundBarPiercingPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0)
            .Select(p => p.WorkOrderId)
            .Distinct()
            .ToListAsync();

        if (piercingWorkOrderIds.Count == 0)
            return new List<ProcurementStatusDto>();

        // 2. 获取工单号映射
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Where(w => piercingWorkOrderIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.WorkOrderNo);

        var allWorkOrderNos = workOrders.Values.ToList();

        // 3. 圆棒穿孔计划：按工单号汇总
        var piercingPlanData = await _context.RoundBarPiercingPlans
            .AsNoTracking()
            .Where(p => p.RequiredWeight > 0 && piercingWorkOrderIds.Contains(p.WorkOrderId))
            .GroupBy(p => p.WorkOrderId)
            .Select(g => new
            {
                WorkOrderId = g.Key,
                PlanWeight = g.Sum(p => p.RequiredWeight)
            })
            .ToListAsync();

        // 4. 按工单号聚合已委外重量
        var subcontractWeights = new Dictionary<string, decimal>();
        if (allWorkOrderNos.Count > 0)
        {
            var subcontractData = await _context.SubcontractReturnItems
                .AsNoTracking()
                .Where(r => r.SourceWorkOrderNo != null && allWorkOrderNos.Contains(r.SourceWorkOrderNo))
                .GroupBy(r => r.SourceWorkOrderNo!)
                .Select(g => new { SourceWorkOrderNo = g.Key, Weight = g.Sum(r => r.RequiredWeight ?? 0) })
                .ToListAsync();
            subcontractWeights = subcontractData.ToDictionary(x => x.SourceWorkOrderNo, x => x.Weight, StringComparer.OrdinalIgnoreCase);
        }

        // 5. 合并数据，计算执行状态
        var result = piercingPlanData
            .Select(x =>
            {
                var workOrderNo = workOrders.GetValueOrDefault(x.WorkOrderId, "");
                var subW = subcontractWeights.GetValueOrDefault(workOrderNo, 0);
                return new ProcurementStatusDto
                {
                    WorkOrderNo = workOrderNo,
                    MaterialName = workOrderNo,
                    MaterialCategory = "圆棒穿孔",
                    PlanWeight = x.PlanWeight,
                    PurchaseWeight = 0,
                    SubcontractWeight = subW,
                    TotalWeight = subW,
                    StatusText = subW == 0 ? "未穿孔"
                        : subW < x.PlanWeight ? "部分穿孔"
                        : "已穿孔"
                };
            })
            .Where(x => x.StatusText != "已穿孔" && !string.IsNullOrEmpty(x.WorkOrderNo))
            .OrderBy(x => x.WorkOrderNo)
            .ToList();

        return result;
    }

    public async Task<List<OrderMismatchInfo>> GetMismatchedPurchaseOrdersAsync()
    {
        // 1. 获取所有涉及采购的工单号（有用料计划且需采购）
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

        // 2. 查询所有采购单中 SourceWorkOrderNo 不为空的
        var purchaseOrders = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(p => p.SourceWorkOrderNo != null && p.SourceWorkOrderNo != "")
            .Select(p => new { p.OrderNo, p.SourceWorkOrderNo })
            .ToListAsync();

        // 3. 找出不匹配的
        var mismatches = purchaseOrders
            .Where(p => !validWorkOrderNos.Contains(p.SourceWorkOrderNo!))
            .GroupBy(p => p.OrderNo)
            .Select(g => new OrderMismatchInfo
            {
                OrderNo = g.Key,
                MismatchedWorkOrderNos = g.Select(p => p.SourceWorkOrderNo!).Distinct().ToList()
            })
            .ToList();

        return mismatches;
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

    // ========== 筛选上下文 ==========

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var query = from p in _context.PurchaseOrders.AsNoTracking()
                    join s in _context.SupplierProfiles.AsNoTracking() on p.SupplierId equals s.Id into sj
                    from s in sj.DefaultIfEmpty()
                    join w in _context.WorkOrders.AsNoTracking() on p.SourceWorkOrderNo equals w.WorkOrderNo into wj
                    from w in wj.DefaultIfEmpty()
                    select new
                    {
                        p.OrderNo,
                        p.OrderDate,
                        p.RequiredDate,
                        p.MaterialCategory,
                        p.PlantGrade,
                        p.Specification,
                        p.SourceWorkOrderNo,
                        SupplierName = s.SupplierName,
                        WoSalesOrderNo = w.SalesOrderNo,
                        WoProductionMainNo = w.ProductionMainNo,
                        WoProductionSubNo = w.ProductionSubNo,
                        WoSignDate = (DateTime?)w.SignDate,
                        WoSalesman = w.Salesman,
                        WoEndCustomer = w.EndCustomer,
                        WoDeliveryDate = (DateTime?)w.DeliveryDate,
                        WoPlantGrade = w.PlantGrade,
                        WoSpecification = w.Specification
                    };

        var all = await query.ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["OrderNo"] = all.Select(x => x.OrderNo).Distinct().OrderBy(x => x).ToList(),
            ["SourceWorkOrderNo"] = all.Where(x => x.SourceWorkOrderNo != null).Select(x => x.SourceWorkOrderNo!).Distinct().OrderBy(x => x).ToList(),
            ["OrderDate"] = all.Select(x => x.OrderDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            ["RequiredDate"] = all.Select(x => x.RequiredDate.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            ["MaterialCategory"] = all.Select(x => x.MaterialCategory).Distinct().OrderBy(x => x).ToList(),
            ["PlantGrade"] = all.Select(x => x.PlantGrade).Distinct().OrderBy(x => x).ToList(),
            ["Specification"] = all.Select(x => x.Specification).Distinct().OrderBy(x => x).ToList(),
            ["SupplierName"] = all.Where(x => x.SupplierName != null).Select(x => x.SupplierName!).Distinct().OrderBy(x => x).ToList(),
            ["WoSalesOrderNo"] = all.Where(x => x.WoSalesOrderNo != null).Select(x => x.WoSalesOrderNo!).Distinct().OrderBy(x => x).ToList(),
            ["WoProductionMainNo"] = all.Where(x => x.WoProductionMainNo != null).Select(x => x.WoProductionMainNo!).Distinct().OrderBy(x => x).ToList(),
            ["WoProductionSubNo"] = all.Where(x => x.WoProductionSubNo != null).Select(x => x.WoProductionSubNo!).Distinct().OrderBy(x => x).ToList(),
            ["WoSignDate"] = all.Where(x => x.WoSignDate != null).Select(x => x.WoSignDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            ["WoSalesman"] = all.Where(x => x.WoSalesman != null).Select(x => x.WoSalesman!).Distinct().OrderBy(x => x).ToList(),
            ["WoEndCustomer"] = all.Where(x => x.WoEndCustomer != null).Select(x => x.WoEndCustomer!).Distinct().OrderBy(x => x).ToList(),
            ["WoDeliveryDate"] = all.Where(x => x.WoDeliveryDate != null).Select(x => x.WoDeliveryDate!.Value.ToString("yyyy-MM-dd")).Distinct().OrderBy(x => x).ToList(),
            ["WoPlantGrade"] = all.Where(x => x.WoPlantGrade != null).Select(x => x.WoPlantGrade!).Distinct().OrderBy(x => x).ToList(),
            ["WoSpecification"] = all.Where(x => x.WoSpecification != null).Select(x => x.WoSpecification!).Distinct().OrderBy(x => x).ToList(),
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
        var orders = await GetByIdsAsync(ids);
        return PurchaseOrderPrintHelper.GenerateBatchPdf(orders);
    }

    public async Task<List<PurchaseOrderDto>> GetByIdsAsync(int[] ids)
    {
        var entities = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();

        var supplierIds = entities.Where(e => e.SupplierId > 0).Select(e => e.SupplierId).Distinct().ToList();
        var suppliers = await _context.SupplierProfiles
            .AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.SupplierName);

        var woNos = entities.Where(e => !string.IsNullOrEmpty(e.SourceWorkOrderNo)).Select(e => e.SourceWorkOrderNo!).Distinct().ToList();
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
            if (e.SourceWorkOrderNo != null && workOrders.TryGetValue(e.SourceWorkOrderNo, out var wo))
                FillWorkOrderFields(dto, wo);
            return dto;
        }).ToList();
    }

    public async Task<byte[]> PrintOrderAllAsync(string? keyword, string? sortBy = null, bool isDescending = false)
    {
        var query = new PurchaseOrderQueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy ?? "CreatedTime",
            IsDescending = isDescending
        };
        var paged = await GetPagedAsync(query);
        return PurchaseOrderPrintHelper.GenerateBatchPdf(paged.Items);
    }
}
