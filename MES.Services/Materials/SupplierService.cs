using Microsoft.EntityFrameworkCore;
using MES.Core.DTOs.Auth;
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
using MES.Core.Exceptions;
using MES.Core.Enums;
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
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Order;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Materials;
using MES.Services.Helpers;
using MES.Services.Printing;
using Microsoft.Extensions.Caching.Memory;

namespace MES.Services.Materials;

public class SupplierService : ISupplierService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public SupplierService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PagedResult<SupplierProfileDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.SupplierProfiles
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(s =>
                s.SupplierCode.Contains(kw) ||
                s.SupplierName.Contains(kw) ||
                (s.MaterialCategory != null && s.MaterialCategory.Contains(kw)) ||
                (s.ContactPerson != null && s.ContactPerson.Contains(kw)) ||
                (s.ContactPhone != null && s.ContactPhone.Contains(kw)) ||
                (s.Address != null && s.Address.Contains(kw)) ||
                (s.Remark != null && s.Remark.Contains(kw)));
        }

        // 通用筛选
        queryable = queryable.ApplyFilters(query.Filters);

        queryable = queryable.ApplySort(query.SortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(s => new SupplierProfileDto
            {
                Id = s.Id,
                SupplierCode = s.SupplierCode,
                SupplierName = s.SupplierName,
                MaterialCategory = s.MaterialCategory,
                ContactPerson = s.ContactPerson,
                ContactPhone = s.ContactPhone,
                Address = s.Address,
                IsActive = s.IsActive,
                Remark = s.Remark,
                CreatedTime = s.CreatedTime
            })
            .ToListAsync();

        return new PagedResult<SupplierProfileDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<SupplierProfileDto>> GetAllListAsync()
    {
        return await _context.SupplierProfiles
            .AsNoTracking()
            .OrderBy(s => s.SupplierCode)
            .Select(s => new SupplierProfileDto
            {
                Id = s.Id,
                SupplierCode = s.SupplierCode,
                SupplierName = s.SupplierName,
                MaterialCategory = s.MaterialCategory,
                ContactPerson = s.ContactPerson,
                ContactPhone = s.ContactPhone,
                Address = s.Address,
                IsActive = s.IsActive,
                Remark = s.Remark,
                CreatedTime = s.CreatedTime
            })
            .ToListAsync();
    }

    public async Task<SupplierProfileDto> GetByIdAsync(int id)
    {
        var entity = await _context.SupplierProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("供应商不存在");
        return ToDto(entity);
    }

    public async Task<List<SupplierProfileDto>> GetActiveAsync()
    {
        return await _context.SupplierProfiles
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SupplierName)
            .Select(s => ToDto(s))
            .ToListAsync();
    }

    public async Task<SupplierProfileDto> CreateAsync(CreateSupplierRequest request)
    {
        var supplierCode = await CodeGenerator.GenerateNextAsync(
            _context.SupplierProfiles.Select(s => s.SupplierCode), "SU");

        var entity = new SupplierProfile
        {
            SupplierCode = supplierCode,
            SupplierName = request.SupplierName,
            MaterialCategory = request.MaterialCategory,
            ContactPerson = request.ContactPerson,
            ContactPhone = request.ContactPhone,
            Address = request.Address,
            IsActive = request.IsActive,
            Remark = request.Remark
        };

        _context.SupplierProfiles.Add(entity);
        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<List<SupplierProfileDto>> CreateBatchAsync(List<CreateSupplierRequest> requests)
    {
        if (requests.Count == 0) return new List<SupplierProfileDto>();

        // 预生成编码
        var maxCode = await _context.SupplierProfiles
            .Where(s => s.SupplierCode.StartsWith("SU") && s.SupplierCode.Length == 6)
            .OrderByDescending(s => s.SupplierCode)
            .Select(s => s.SupplierCode)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (maxCode != null && int.TryParse(maxCode[2..], out var lastSeq))
            sequence = lastSeq + 1;

        var entities = new List<SupplierProfile>(requests.Count);
        for (int i = 0; i < requests.Count; i++)
        {
            var r = requests[i];
            var code = $"SU{sequence + i:D4}";
            entities.Add(new SupplierProfile
            {
                SupplierCode = code,
                SupplierName = r.SupplierName,
                MaterialCategory = r.MaterialCategory,
                ContactPerson = r.ContactPerson,
                ContactPhone = r.ContactPhone,
                Address = r.Address,
                IsActive = r.IsActive,
                Remark = r.Remark
            });
        }

        _context.SupplierProfiles.AddRange(entities);
        await _context.SaveChangesAsync();
        return entities.Select(ToDto).ToList();
    }

    public async Task<SupplierProfileDto> UpdateAsync(int id, UpdateSupplierRequest request)
    {
        var entity = await _context.SupplierProfiles
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("供应商不存在");

        if (request.SupplierName != null) entity.SupplierName = request.SupplierName;
        if (request.MaterialCategory != null) entity.MaterialCategory = request.MaterialCategory;
        if (request.ContactPerson != null) entity.ContactPerson = request.ContactPerson;
        if (request.ContactPhone != null) entity.ContactPhone = request.ContactPhone;
        if (request.Address != null) entity.Address = request.Address;
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;
        if (request.Remark != null) entity.Remark = request.Remark;

        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.SupplierProfiles
            .FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null) throw new BusinessException("供应商不存在");

        _context.SupplierProfiles.Remove(entity);
        await _context.SaveChangesAsync();
    }

    // ========== 打印 ==========

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync("SupplierService:FilterContexts", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var query = _context.SupplierProfiles.AsNoTracking();
            return new Dictionary<string, List<string>>
            {
                ["SupplierCode"] = await query.Where(s => s.SupplierCode != null).Select(s => s.SupplierCode).Distinct().OrderBy(x => x).ToListAsync(),
                ["SupplierName"] = await query.Where(s => s.SupplierName != null).Select(s => s.SupplierName).Distinct().OrderBy(x => x).ToListAsync(),
                ["MaterialCategory"] = await query.Where(s => s.MaterialCategory != null).Select(s => s.MaterialCategory!).Distinct().OrderBy(x => x).ToListAsync(),
                ["ContactPerson"] = await query.Where(s => s.ContactPerson != null).Select(s => s.ContactPerson!).Distinct().OrderBy(x => x).ToListAsync(),
                ["ContactPhone"] = await query.Where(s => s.ContactPhone != null).Select(s => s.ContactPhone!).Distinct().OrderBy(x => x).ToListAsync(),
                ["Address"] = await query.Where(s => s.Address != null).Select(s => s.Address!).Distinct().OrderBy(x => x).ToListAsync(),
                ["Remark"] = await query.Where(s => s.Remark != null).Select(s => s.Remark!).Distinct().OrderBy(x => x).ToListAsync(),
                ["IsActive"] = await query.Select(s => s.IsActive.ToString()).Distinct().OrderBy(x => x).ToListAsync(),
            };

        }) ?? new Dictionary<string, List<string>>();
    }

    public async Task<byte[]> PrintSupplierAsync(int id, List<PrintColumnDef>? columns = null)
    {
        var dto = await GetByIdAsync(id);
        return TablePrintHelper.GeneratePdf("供应商档案列表", new List<Dictionary<string, object>> { ToPrintDict(dto) }, columns ?? []);
    }

    public async Task<byte[]> PrintSupplierBatchAsync(int[] ids, List<PrintColumnDef>? columns = null)
    {
        var result = new List<SupplierProfileDto>();
        foreach (var id in ids)
        {
            try
            {
                result.Add(await GetByIdAsync(id));
            }
            catch (BusinessException) { /* 跳过不存在的供应商 */ }
        }
        return TablePrintHelper.GeneratePdf("供应商档案列表", result.Select(ToPrintDict).ToList(), columns ?? []);
    }

    public async Task<byte[]> PrintSupplierAllAsync(string? keyword, string? sortBy = null, bool isDescending = false, List<PrintColumnDef>? columns = null)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = sortBy ?? "CreatedTime",
            IsDescending = isDescending
        };
        var paged = await GetPagedAsync(query);
        return TablePrintHelper.GeneratePdf("供应商档案列表", paged.Items.Select(ToPrintDict).ToList(), columns ?? []);
    }

    private static Dictionary<string, object> ToPrintDict(SupplierProfileDto dto) => new()
    {
        ["SupplierCode"] = dto.SupplierCode,
        ["SupplierName"] = dto.SupplierName,
        ["MaterialCategory"] = string.Join(", ",
            (dto.MaterialCategory ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => EnumHelper.GetDisplayName<MaterialType>(v))),
        ["ContactPerson"] = (object?)dto.ContactPerson ?? "",
        ["ContactPhone"] = (object?)dto.ContactPhone ?? "",
        ["IsActive"] = dto.IsActive ? "启用" : "停用",
        ["Remark"] = (object?)dto.Remark ?? "",
    };

    private static SupplierProfileDto ToDto(SupplierProfile entity) => new()
    {
        Id = entity.Id,
        SupplierCode = entity.SupplierCode,
        SupplierName = entity.SupplierName,
        MaterialCategory = entity.MaterialCategory,
        ContactPerson = entity.ContactPerson,
        ContactPhone = entity.ContactPhone,
        Address = entity.Address,
        IsActive = entity.IsActive,
        Remark = entity.Remark,
        CreatedTime = entity.CreatedTime
    };
}
