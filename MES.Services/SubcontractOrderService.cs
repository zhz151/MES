using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs;
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

            // 尝试按供应商名称搜索 → 查 SupplierId
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
                matchedSupplierIds.Contains(s.SupplierId));
        }

        // 状态筛选
        if (!string.IsNullOrEmpty(query.Status))
        {
            queryable = queryable.Where(s => s.Status == query.Status);
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
                ? queryable.OrderByDescending(s => s.SupplierId)
                : queryable.OrderBy(s => s.SupplierId),
            "status" => query.IsDescending
                ? queryable.OrderByDescending(s => s.Status)
                : queryable.OrderBy(s => s.Status),
            _ => query.IsDescending
                ? queryable.OrderByDescending(s => s.CreatedTime)
                : queryable.OrderBy(s => s.CreatedTime)
        };

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(s => new SubcontractOrderDto
            {
                Id = s.Id,
                OrderNo = s.OrderNo,
                SupplierId = s.SupplierId,
                SupplierName = "",
                OrderDate = s.OrderDate,
                Status = s.Status,
                ManualStatus = s.ManualStatus,
                FurnaceNumber = s.FurnaceNumber,
                ProcessType = s.ProcessType,
                OutMaterialCategory = s.OutMaterialCategory,
                OutPlantGrade = s.OutPlantGrade,
                OutSpecification = s.OutSpecification,
                OutQuantity = s.OutQuantity,
                OutWeight = s.OutWeight,
                ReturnDeadline = s.ReturnDeadline,
                InQuantity = s.InQuantity,
                InWeight = s.InWeight,
                Remark = s.Remark,
                CreatedTime = s.CreatedTime
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
            ProcessTotalAmount = r.ProcessTotalAmount,
            SourceWorkOrderNo = r.SourceWorkOrderNo
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
        if (entity.Status == "Cancelled") throw new BusinessException("已取消的委外单无法编辑");
        if (entity.Status == "Completed") throw new BusinessException("已完成的委外单无法编辑");

        entity.SupplierId = request.SupplierId;
        entity.ProcessType = request.ProcessType;
        entity.FurnaceNumber = request.FurnaceNumber;
        entity.OutMaterialCategory = request.OutMaterialCategory;
        entity.OutPlantGrade = request.OutPlantGrade;
        entity.OutSpecification = request.OutSpecification;
        entity.OutQuantity = request.OutQuantity;
        entity.OutWeight = request.OutWeight;
        entity.ReturnDeadline = request.ReturnDeadline;
        entity.Remark = request.Remark;

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
            .Where(s => s.Status != "Cancelled" && s.Status != "Completed")
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

            if (string.IsNullOrEmpty(order.ManualStatus))
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

        if (string.IsNullOrEmpty(order.ManualStatus))
            RecalcSubcontractStatus(order);

        await _context.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(int id, UpdateOrderStatusRequest request)
    {
        var entity = await _context.SubcontractOrders
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("委外单不存在");

        entity.ManualStatus = request.ManualStatus;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.SubcontractOrders
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("委外单不存在");
        if (entity.Status == "Completed") throw new BusinessException("已完成的委外单无法删除");
        if (entity.Status == "Cancelled") throw new BusinessException("该委外单已取消");

        _context.SubcontractOrders.Remove(entity);
        await _context.SaveChangesAsync();
    }

    // ========== 用料计划执行状态 ==========

    public async Task<List<ProcurementStatusDto>> GetProcurementStatusAsync()
    {
        return await _purchaseService.GetProcurementStatusAsync();
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
        var orders = new List<SubcontractOrderDto>();
        foreach (var id in ids)
        {
            orders.Add(await GetByIdAsync(id));
        }
        return SubcontractOrderPrintHelper.GenerateBatchPdf(orders);
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
            order.Status = "Sent";
        else if (order.InWeight >= order.OutWeight * 0.95m)
            order.Status = "Completed";
        else
            order.Status = "PartialReturned";
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
        ManualStatus = entity.ManualStatus,
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
}
