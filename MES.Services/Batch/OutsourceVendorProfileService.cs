using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MES.Core.Constants;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Shared;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Batch;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Batch;
using MES.Services.Helpers;
using MES.Services.Printing;

namespace MES.Services.Batch;

/// <summary>
/// 委外单位档案服务（工段委外单位主档，仿供应商档案 SupplierService，② 往来信息统计/列表打印同款）。
/// 行 = (委外单位名 × 委外工段)；编码前缀 WV；SectionName 存英文 key。
/// </summary>
public class OutsourceVendorProfileService : IOutsourceVendorProfileService
{
    private const string VendorPrefix = "WV";

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public OutsourceVendorProfileService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PagedResult<OutsourceVendorProfileDto>> GetPagedAsync(QueryParams query)
    {
        var queryable = _context.OutsourceVendorProfiles
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(v =>
                v.VendorCode.Contains(kw) ||
                v.VendorName.Contains(kw) ||
                v.SectionName.Contains(kw) ||
                (v.ContactPerson != null && v.ContactPerson.Contains(kw)) ||
                (v.ContactPhone != null && v.ContactPhone.Contains(kw)) ||
                (v.Remark != null && v.Remark.Contains(kw)));
        }

        queryable = queryable.ApplyFilters(query.Filters);
        queryable = queryable.ApplySort(query.SortBy, query.IsDescending);

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(v => new
            {
                v.Id,
                v.VendorCode,
                v.VendorName,
                v.SectionName,
                v.IsWorkshop,
                v.ContactPerson,
                v.ContactPhone,
                v.IsActive,
                v.Remark,
                v.CreatedTime
            })
            .ToListAsync();

        var dtos = items.Select(v => ToDto(v.Id, v.VendorCode, v.VendorName, v.SectionName, v.IsWorkshop, v.ContactPerson, v.ContactPhone, v.IsActive, v.Remark, v.CreatedTime)).ToList();

        // ② 往来信息统计回填（仅当前页明细行聚合）
        await AttachTradeStatsAsync(dtos);

        return new PagedResult<OutsourceVendorProfileDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    // ========== ② 工段委外往来统计（列表回填，仿供应商 SupplierService） ==========

    /// <summary>委外单位统计累加桶（键=档案行 Id，可变字段避免逐单建 DTO）</summary>
    private sealed class OutsourceVendorStatsBucket
    {
        public int TotalCount;
        public decimal TotalWeight;
        public decimal TotalAmount;
        public int YearCount;
        public decimal YearWeight;
        public decimal YearAmount;
        public decimal YearRecoveredWeight;
        public decimal YearRecoveredAmount;
        public decimal PendingWeight;
        public decimal PendingAmount;
        public decimal YearReturnWeight;
    }

    /// <summary>本页档案行 =「委外单位名 × 委外工段」组合（列表每行一行，同名跨工段多档案）</summary>
    private sealed class OutsourceVendorStatsRow
    {
        public int Id;
        public string VendorName = null!;
        public string SectionName = null!;
    }

    /// <summary>
    /// 为当前页委外单位档案明细回填 ② 往来信息 9 字段。
    /// 列表每行 =「委外单位名 + 委外工段」档案；发出单按同名 + 同工段精确分流到该行（新单已强制命中档案，历史档外文本不属本页任何行）。
    /// </summary>
    private async Task AttachTradeStatsAsync(List<OutsourceVendorProfileDto> items)
    {
        if (items.Count == 0)
            return;

        var year = DateTime.Today.Year;
        var rows = items.Select(x => new OutsourceVendorStatsRow
        {
            Id = x.Id,
            VendorName = x.VendorName,
            SectionName = x.SectionName
        }).ToList();

        var buckets = await BuildOutsourceStatsAsync(_context, year, rows);

        foreach (var dto in items)
        {
            if (!buckets.TryGetValue(dto.Id, out var b))
                continue;

            dto.TotalOrderCount = b.TotalCount;
            dto.TotalWeight = b.TotalWeight;
            dto.TotalAmount = b.TotalAmount;
            dto.YearOrderCount = b.YearCount;
            dto.YearWeight = b.YearWeight;
            dto.YearAmount = b.YearAmount;
            dto.YearRecoveredWeight = b.YearRecoveredWeight;
            dto.YearRecoveredAmount = b.YearRecoveredAmount;
            dto.PendingWeight = b.PendingWeight;
            dto.PendingAmount = b.PendingAmount;
            dto.YearReturnWeight = b.YearReturnWeight;
        }
    }

    private static string BuildRowKey(string name, string section) => name + "\u0001" + section;

    /// <summary>
    /// 按「委外单位名 + 委外工段」归档案行（仅非厂内 !IsInternal 发出单，与月度汇总/读模型同约定：厂内无价、Status=Virtual、永不回收）。
    /// 口径（唯一事实源）：
    /// - 累计/本年委外：单数、吨(SendWeight)、元(TotalAmount)，年份按发出日期 SendOutDate.Year；
    /// - 本年回收：kg = 该行发出单的回收记录中 RecoveryDate.Year==今年 的 Σ RecoveryWeight（正常）；元 = Σ 单 TotalAmount×本年正常回收重/发出重 分摊（仅正常回收计费，退回不产生金额；份额截于发出重防超发回收异常放大）；
    /// - 本年退回(kg) = 同上 RecoveryDate.Year==今年 的 Σ UnprocessedWeight（非正常退回，无金额）；
    /// - 委外未回收：kg = 当前时点 Status==PendingRecovery 的行 Σ (SendWeight − Σ(Recovery+Unprocessed)全量)，负数截 0（非年份口径）；元 = 各待回收单 TotalAmount×未回收净欠/发出重 分摊。
    /// 归行键 = (OutsourceVendor, SectionName)，OrdinalIgnoreCase 精确命中档案行；档外文本/厂内行不计（累计 0 → 前端显「—」）。
    /// </summary>
    private static async Task<Dictionary<int, OutsourceVendorStatsBucket>> BuildOutsourceStatsAsync(
        AppDbContext ctx, int year, List<OutsourceVendorStatsRow> rows)
    {
        var buckets = new Dictionary<int, OutsourceVendorStatsBucket>();
        if (rows.Count == 0)
            return buckets;

        // 页内委外单位名（同名跨工段档案共享，再由工段精确分流到行）
        var names = rows.Select(r => r.VendorName).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
        if (names.Count == 0)
            return buckets;

        // 页内档案行 名+工段 → Id
        var idByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
            if (!string.IsNullOrEmpty(r.VendorName) && !string.IsNullOrEmpty(r.SectionName))
                idByKey.TryAdd(BuildRowKey(r.VendorName, r.SectionName), r.Id);

        var outsourceRows = await ctx.SectionOutsources.AsNoTracking()
            .Where(o => !o.IsInternal && names.Contains(o.OutsourceVendor))
            .Select(o => new
            {
                o.Id,
                o.OutsourceVendor,
                o.SectionName,
                o.SendOutDate,
                o.SendWeight,
                o.TotalAmount,
                o.Status
            })
            .ToListAsync();

        // 各发出单的回收/退回重量：按回收日期切本年 + 全量一次 group 带出（Chunk 防 SQL Server 2100 参数上限）
        var recovTotals = new Dictionary<int, (decimal YearRecovered, decimal YearReturn, decimal AllRecovered, decimal AllUnprocessed)>();
        if (outsourceRows.Count > 0)
        {
            foreach (var chunk in outsourceRows.Select(o => o.Id).Chunk(1000))
            {
                var ids = chunk.ToList();
                var recs = await ctx.OutsourceRecoveries.AsNoTracking()
                    .Where(r => ids.Contains(r.SectionOutsourceId))
                    .Select(r => new
                    {
                        r.SectionOutsourceId,
                        r.RecoveryDate,
                        RecoveryWeight = r.RecoveryWeight ?? 0m,
                        UnprocessedWeight = r.UnprocessedWeight ?? 0m
                    })
                    .ToListAsync();

                foreach (var r in recs)
                {
                    var t = recovTotals.GetValueOrDefault(r.SectionOutsourceId);
                    if (r.RecoveryDate.Year == year)
                    {
                        t.YearRecovered += r.RecoveryWeight;
                        t.YearReturn += r.UnprocessedWeight;
                    }
                    t.AllRecovered += r.RecoveryWeight;
                    t.AllUnprocessed += r.UnprocessedWeight;
                    recovTotals[r.SectionOutsourceId] = t;
                }
            }
        }

        foreach (var o in outsourceRows)
        {
            if (string.IsNullOrEmpty(o.OutsourceVendor) || string.IsNullOrEmpty(o.SectionName))
                continue;
            if (!idByKey.TryGetValue(BuildRowKey(o.OutsourceVendor, o.SectionName), out var targetId))
                continue; // 历史档外文本（本页无对应档案行）不计

            var t = recovTotals.GetValueOrDefault(o.Id);
            var b = GetOutsourceBucket(buckets, targetId);

            var sendWeight = o.SendWeight ?? 0m;
            var amount = o.TotalAmount ?? 0m;

            b.TotalCount++;
            b.TotalWeight += sendWeight;
            b.TotalAmount += amount;
            if (o.SendOutDate.Year == year)
            {
                b.YearCount++;
                b.YearWeight += sendWeight;
                b.YearAmount += amount;
            }
            b.YearRecoveredWeight += t.YearRecovered;
            b.YearReturnWeight += t.YearReturn;

            // 金额按发出单总价 × 重量份额分摊（仿供应商 ② 到货/待收货款：单金额×份额/发出重；退回不产生金额）
            // 本年回收金额：正常回收重量份额（截于发出重，防超发回收记录把金额放大）
            if (sendWeight > 0m && amount > 0m && t.YearRecovered > 0m)
                b.YearRecoveredAmount += amount * Math.Min(t.YearRecovered, sendWeight) / sendWeight;

            // 委外未回收净欠（仅待回收单，非年份口径）：重量 + 对应金额份额
            if (o.Status == SectionOutsourceStatus.PendingRecovery)
            {
                var pendingKg = Math.Max(0m, sendWeight - (t.AllRecovered + t.AllUnprocessed));
                b.PendingWeight += pendingKg;
                if (pendingKg > 0m && sendWeight > 0m && amount > 0m)
                    b.PendingAmount += amount * pendingKg / sendWeight;
            }
        }

        return buckets;
    }

    private static OutsourceVendorStatsBucket GetOutsourceBucket(Dictionary<int, OutsourceVendorStatsBucket> buckets, int vendorId)
    {
        if (!buckets.TryGetValue(vendorId, out var b))
        {
            b = new OutsourceVendorStatsBucket();
            buckets[vendorId] = b;
        }
        return b;
    }

    public async Task<OutsourceVendorProfileDto> GetByIdAsync(int id)
    {
        var entity = await _context.OutsourceVendorProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id);
        if (entity == null) throw new BusinessException("委外单位档案不存在");
        return ToDto(entity);
    }

    public async Task<List<OutsourceVendorProfileDto>> GetActiveAsync()
    {
        var items = await _context.OutsourceVendorProfiles
            .AsNoTracking()
            .Where(v => v.IsActive)
            .OrderBy(v => v.VendorName)
            .ThenBy(v => v.SectionName)
            .Select(v => new
            {
                v.Id,
                v.VendorCode,
                v.VendorName,
                v.SectionName,
                v.IsWorkshop,
                v.ContactPerson,
                v.ContactPhone,
                v.IsActive,
                v.Remark,
                v.CreatedTime
            })
            .ToListAsync();

        return items.Select(v => ToDto(v.Id, v.VendorCode, v.VendorName, v.SectionName, v.IsWorkshop, v.ContactPerson, v.ContactPhone, v.IsActive, v.Remark, v.CreatedTime)).ToList();
    }

    public async Task<OutsourceVendorProfileDto> CreateAsync(CreateOutsourceVendorRequest request)
    {
        var (name, section) = Validate(request.VendorName, request.SectionName);
        await EnsureUniqueAsync(name, section, excludeId: 0);

        var code = await CodeGenerator.GenerateNextAsync(
            _context.OutsourceVendorProfiles.Select(v => v.VendorCode), VendorPrefix);

        var entity = new OutsourceVendorProfile
        {
            VendorCode = code,
            VendorName = name,
            SectionName = section,
            IsWorkshop = request.IsWorkshop,
            ContactPerson = request.ContactPerson,
            ContactPhone = request.ContactPhone,
            IsActive = request.IsActive,
            Remark = request.Remark
        };

        _context.OutsourceVendorProfiles.Add(entity);
        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<List<OutsourceVendorProfileDto>> CreateBatchAsync(List<CreateOutsourceVendorRequest> requests)
    {
        if (requests.Count == 0) return new List<OutsourceVendorProfileDto>();

        // 预生成编码起始流水
        var maxCode = await _context.OutsourceVendorProfiles
            .Where(v => v.VendorCode.StartsWith(VendorPrefix) && v.VendorCode.Length == 6)
            .OrderByDescending(v => v.VendorCode)
            .Select(v => v.VendorCode)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (maxCode != null && int.TryParse(maxCode[2..], out var lastSeq))
            sequence = lastSeq + 1;

        var entities = new List<OutsourceVendorProfile>(requests.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < requests.Count; i++)
        {
            var (name, section) = Validate(requests[i].VendorName, requests[i].SectionName);
            var batchKey = $"{name}|{section}";
            if (!seen.Add(batchKey))
                throw new BusinessException($"第 {i + 1} 行与批内前面行重复：委外单位「{name}」工段「{section}」");
            await EnsureUniqueAsync(name, section, excludeId: 0);

            entities.Add(new OutsourceVendorProfile
            {
                VendorCode = $"{VendorPrefix}{sequence + i:D4}",
                VendorName = name,
                SectionName = section,
                IsWorkshop = requests[i].IsWorkshop,
                ContactPerson = requests[i].ContactPerson,
                ContactPhone = requests[i].ContactPhone,
                IsActive = requests[i].IsActive,
                Remark = requests[i].Remark
            });
        }

        _context.OutsourceVendorProfiles.AddRange(entities);
        await _context.SaveChangesAsync();
        return entities.Select(ToDto).ToList();
    }

    public async Task<OutsourceVendorProfileDto> UpdateAsync(int id, UpdateOutsourceVendorRequest request)
    {
        var entity = await _context.OutsourceVendorProfiles
            .FirstOrDefaultAsync(v => v.Id == id);
        if (entity == null) throw new BusinessException("委外单位档案不存在");

        if (request.VendorName != null) entity.VendorName = request.VendorName.Trim();
        if (request.SectionName != null) entity.SectionName = request.SectionName.Trim();
        if (request.IsWorkshop.HasValue) entity.IsWorkshop = request.IsWorkshop.Value;
        if (request.ContactPerson != null) entity.ContactPerson = request.ContactPerson;
        if (request.ContactPhone != null) entity.ContactPhone = request.ContactPhone;
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;
        if (request.Remark != null) entity.Remark = request.Remark;

        await EnsureUniqueAsync(entity.VendorName, entity.SectionName, excludeId: id);

        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.OutsourceVendorProfiles
            .FirstOrDefaultAsync(v => v.Id == id);
        if (entity == null) throw new BusinessException("委外单位档案不存在");

        _context.OutsourceVendorProfiles.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeys.OutsourceVendorProfileFilterContexts, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDefaults.MemoryCacheExpiry;

            var query = _context.OutsourceVendorProfiles.AsNoTracking();
            return new Dictionary<string, List<string>>
            {
                ["VendorCode"] = await query.Where(v => v.VendorCode != null).Select(v => v.VendorCode).Distinct().OrderBy(x => x).ToListAsync(),
                ["VendorName"] = await query.Where(v => v.VendorName != null).Select(v => v.VendorName).Distinct().OrderBy(x => x).ToListAsync(),
                ["SectionName"] = await query.Where(v => v.SectionName != null).Select(v => v.SectionName).Distinct().OrderBy(x => x).ToListAsync(),
                ["ContactPerson"] = await query.Where(v => v.ContactPerson != null).Select(v => v.ContactPerson!).Distinct().OrderBy(x => x).ToListAsync(),
                ["ContactPhone"] = await query.Where(v => v.ContactPhone != null).Select(v => v.ContactPhone!).Distinct().OrderBy(x => x).ToListAsync(),
                ["IsWorkshop"] = await query.Select(v => v.IsWorkshop.ToString()).Distinct().OrderBy(x => x).ToListAsync(),
                ["IsActive"] = await query.Select(v => v.IsActive.ToString()).Distinct().OrderBy(x => x).ToListAsync(),
            };

        }) ?? new Dictionary<string, List<string>>();
    }

    // ========== 打印 ==========

    /// <summary>列表打印（Mode A）：前端已按可见列把当前页转成字典行，服务端仅做表格渲染（② 往来信息统计列文本 Mode A 才带出，Mode B 反射不到派生字段）</summary>
    public Task<byte[]> PrintOutsourceVendorListAsync(string title, List<Dictionary<string, object>> items, List<PrintColumnDef> columns)
    {
        // 与供应商/客户/订单列表打印同款：自适应列宽 + 单元格居中 + 表头自动换行
        return Task.FromResult(TablePrintHelper.GeneratePdf(
            string.IsNullOrWhiteSpace(title) ? "委外单位列表" : title,
            items,
            columns ?? [],
            autoWidth: true,
            alignCenter: true,
            headerMaxLines: 0));
    }

    /// <summary>校验必填并 trim</summary>
    private static (string Name, string Section) Validate(string? vendorName, string? sectionName)
    {
        var name = vendorName?.Trim() ?? "";
        var section = sectionName?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) throw new BusinessException("委外单位名不能为空");
        if (string.IsNullOrEmpty(section)) throw new BusinessException("委外工段不能为空");
        return (name, section);
    }

    /// <summary>行键唯一校验：同单位名 × 同工段（内存/DB 均忽略大小写）</summary>
    private async Task EnsureUniqueAsync(string vendorName, string sectionName, int excludeId)
    {
        var dup = await _context.OutsourceVendorProfiles
            .AsNoTracking()
            .AnyAsync(v => v.Id != excludeId
                && v.VendorName.ToUpper() == vendorName.ToUpper()
                && v.SectionName.ToUpper() == sectionName.ToUpper());
        if (dup)
            throw new BusinessException($"委外单位「{vendorName}」在工段「{sectionName}」已存在档案，请勿重复建档");
    }

    private static OutsourceVendorProfileDto ToDto(OutsourceVendorProfile e) => new()
    {
        Id = e.Id,
        VendorCode = e.VendorCode,
        VendorName = e.VendorName,
        SectionName = e.SectionName,
        IsWorkshop = e.IsWorkshop,
        ContactPerson = e.ContactPerson,
        ContactPhone = e.ContactPhone,
        IsActive = e.IsActive,
        Remark = e.Remark,
        CreatedTime = e.CreatedTime
    };

    private static OutsourceVendorProfileDto ToDto(int id, string code, string name, string section, bool isWorkshop, string? contactPerson, string? contactPhone, bool isActive, string? remark, DateTimeOffset createdTime) => new()
    {
        Id = id,
        VendorCode = code,
        VendorName = name,
        SectionName = section,
        IsWorkshop = isWorkshop,
        ContactPerson = contactPerson,
        ContactPhone = contactPhone,
        IsActive = isActive,
        Remark = remark,
        CreatedTime = createdTime
    };
}
