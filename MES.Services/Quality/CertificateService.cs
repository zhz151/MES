using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Quality;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Quality;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Warehouse;
using System.Text.RegularExpressions;

namespace MES.Services.Quality;

/// <summary>
/// 质量证明书服务实现
/// </summary>
public class CertificateService : ICertificateService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CertificateService> _logger;

    public CertificateService(AppDbContext context, ILogger<CertificateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<CertificateDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.Certificates
            .AsNoTracking()
            .AsQueryable();

        // 关键词搜索
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(c =>
                c.CertificateNo.Contains(kw) ||
                (c.CustomerName != null && c.CustomerName.Contains(kw)) ||
                (c.ProductStandard != null && c.ProductStandard.Contains(kw)) ||
                (c.ProductName != null && c.ProductName.Contains(kw)) ||
                (c.DeliveryStatus != null && c.DeliveryStatus.Contains(kw)));
        }

        // ExcelFilter 筛选
        if (query.Filters?.Count > 0)
        {
            foreach (var filter in query.Filters)
            {
                if (string.IsNullOrWhiteSpace(filter.Value)) continue;
                switch (filter.Field.ToLower())
                {
                    case "certificateno":
                        queryable = queryable.Where(c => c.CertificateNo.Contains(filter.Value));
                        break;
                    case "customername":
                        queryable = queryable.Where(c => c.CustomerName != null && c.CustomerName.Contains(filter.Value));
                        break;
                    case "productstandard":
                        queryable = queryable.Where(c => c.ProductStandard != null && c.ProductStandard.Contains(filter.Value));
                        break;
                    case "productname":
                        queryable = queryable.Where(c => c.ProductName != null && c.ProductName.Contains(filter.Value));
                        break;
                    case "deliverystatus":
                        queryable = queryable.Where(c => c.DeliveryStatus != null && c.DeliveryStatus.Contains(filter.Value));
                        break;
                }
            }
        }

        // 排序
        var sortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "issuedate" : query.SortBy.ToLower();
        queryable = (sortBy, query.IsDescending) switch
        {
            ("certificateno", false) => queryable.OrderBy(c => c.CertificateNo),
            ("certificateno", true) => queryable.OrderByDescending(c => c.CertificateNo),
            ("customername", false) => queryable.OrderBy(c => c.CustomerName ?? ""),
            ("customername", true) => queryable.OrderByDescending(c => c.CustomerName ?? ""),
            ("issuedate", false) => queryable.OrderBy(c => c.IssueDate),
            ("issuedate", true) => queryable.OrderByDescending(c => c.IssueDate),
            _ => queryable.OrderByDescending(c => c.IssueDate)
        };

        var totalCount = await queryable.CountAsync();
        var items = await queryable
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new CertificateDto
            {
                Id = c.Id,
                CertificateNo = c.CertificateNo,
                IssueDate = c.IssueDate,
                CustomerName = c.CustomerName,
                ProductStandard = c.ProductStandard,
                ProductName = c.ProductName,
                DeliveryStatus = c.DeliveryStatus,
                Remark = c.Remark,
                CreatedTime = c.CreatedTime.DateTime,
                CreatedBy = c.CreatedBy,
                UpdatedTime = c.UpdatedTime.DateTime,
                UpdatedBy = c.UpdatedBy
            })
            .ToListAsync();

        return new PagedResult<CertificateDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<CertificateDetailDto?> GetByIdAsync(int id)
    {
        return await _context.Certificates
            .AsNoTracking()
            .Include(c => c.Items.OrderBy(i => i.SeqNo))
            .Where(c => c.Id == id)
            .Select(c => new CertificateDetailDto
            {
                Id = c.Id,
                CertificateNo = c.CertificateNo,
                IssueDate = c.IssueDate,
                CustomerName = c.CustomerName,
                ProductStandard = c.ProductStandard,
                ProductName = c.ProductName,
                DeliveryStatus = c.DeliveryStatus,
                Remark = c.Remark,
                Items = c.Items.Select(i => new CertificateItemDto
                {
                    Id = i.Id,
                    SeqNo = i.SeqNo,
                    InventoryBatchNo = i.InventoryBatchNo,
                    ProductionBatchNo = i.ProductionBatchNo,
                    HeatNo = i.HeatNo,
                    SteelGrade = i.SteelGrade,
                    Specification = i.Specification,
                    LengthDesc = i.LengthDesc,
                    Quantity = i.Quantity,
                    Meters = i.Meters,
                    Weight = i.Weight,
                    ChemC = i.ChemC,
                    ChemSi = i.ChemSi,
                    ChemMn = i.ChemMn,
                    ChemP = i.ChemP,
                    ChemS = i.ChemS,
                    ChemNi = i.ChemNi,
                    ChemCr = i.ChemCr,
                    ChemMo = i.ChemMo,
                    ChemCu = i.ChemCu,
                    ChemN = i.ChemN,
                    ChemNb = i.ChemNb,
                    ChemTi = i.ChemTi,
                    ChemFe = i.ChemFe,
                    ChemAl = i.ChemAl,
                    ChemW = i.ChemW,
                    ChemPREN = i.ChemPREN,
                    InspPMI = i.InspPMI,
                    InspVisual = i.InspVisual,
                    InspDimension = i.InspDimension,
                    InspEndoscopy = i.InspEndoscopy,
                    InspHydro = i.InspHydro,
                    InspUnderwaterPneumatic = i.InspUnderwaterPneumatic,
                    InspEddyCurrent = i.InspEddyCurrent,
                    InspUltrasonic = i.InspUltrasonic,
                    InspPortDye = i.InspPortDye,
                    TensileStrength_1 = i.TensileStrength_1,
                    TensileStrength_2 = i.TensileStrength_2,
                    YieldRp02_1 = i.YieldRp02_1,
                    YieldRp02_2 = i.YieldRp02_2,
                    YieldRp10_1 = i.YieldRp10_1,
                    YieldRp10_2 = i.YieldRp10_2,
                    Elongation_1 = i.Elongation_1,
                    Elongation_2 = i.Elongation_2,
                    Hardness_1 = i.Hardness_1,
                    Hardness_2 = i.Hardness_2,
                    GrainSize_1 = i.GrainSize_1,
                    GrainSize_2 = i.GrainSize_2,
                    FerriteContent_1 = i.FerriteContent_1,
                    FerriteContent_2 = i.FerriteContent_2,
                    FlaringResult = i.FlaringResult,
                    FlatteningResult = i.FlatteningResult,
                    IntergranularResult = i.IntergranularResult,
                    PittingResult = i.PittingResult
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<CertificateDetailDto> CreateAsync(CertificateCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrderNo))
            throw new BusinessException("订单号不能为空");

        if (request.Items.Count == 0)
            throw new BusinessException("请至少提供一个证书子项");

        // 生成质保书编号
        var certificateNo = await GetNextCertificateNoAsync(request.OrderNo);

        var entity = new Certificate
        {
            CertificateNo = certificateNo,
            IssueDate = DateTime.UtcNow,
            CustomerName = request.CustomerName,
            ProductStandard = request.ProductStandard,
            ProductName = request.ProductName,
            DeliveryStatus = request.DeliveryStatus,
            Remark = request.Remark,
        };

        // 如果 ProductName 未设置，通过 ProductStandard 从 StandardRegister 自动填充
        if (string.IsNullOrWhiteSpace(request.ProductName)
            && !string.IsNullOrWhiteSpace(request.ProductStandard))
        {
            var ps = request.ProductStandard;
            var stdReg = await _context.StandardRegisters
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StandardNo == ps);
            if (stdReg == null)
            {
                // 容错1：去掉年份后缀再试
                var withoutYear = Regex.Replace(ps, @"-\d{4}$", "");
                if (withoutYear != ps)
                    stdReg = await _context.StandardRegisters
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.StandardNo == withoutYear);
            }
            if (stdReg == null)
            {
                // 容错2：去除所有空白比较
                var noSpace = ps.Replace(" ", "").Replace("\t", "");
                stdReg = await _context.StandardRegisters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.StandardNo!.Replace(" ", "").Replace("\t", "") == noSpace);
            }
            if (stdReg != null)
                entity.ProductName = stdReg.StandardName;
        }

        // 直接使用前端传入的子项完整数据（含检查数据），保持 SeqNo 与前端一致
        entity.Items = request.Items.Select(dto => new CertificateItem
        {
            SeqNo = dto.SeqNo,
            InventoryBatchNo = dto.InventoryBatchNo,
            ProductionBatchNo = dto.ProductionBatchNo,
            HeatNo = dto.HeatNo,
            SteelGrade = dto.SteelGrade,
            Specification = dto.Specification,
            LengthDesc = dto.LengthDesc,
            Quantity = dto.Quantity,
            Meters = dto.Meters,
            Weight = dto.Weight,
            ChemC = dto.ChemC,
            ChemSi = dto.ChemSi,
            ChemMn = dto.ChemMn,
            ChemP = dto.ChemP,
            ChemS = dto.ChemS,
            ChemNi = dto.ChemNi,
            ChemCr = dto.ChemCr,
            ChemMo = dto.ChemMo,
            ChemCu = dto.ChemCu,
            ChemN = dto.ChemN,
            ChemNb = dto.ChemNb,
            ChemTi = dto.ChemTi,
            ChemFe = dto.ChemFe,
            ChemAl = dto.ChemAl,
            ChemW = dto.ChemW,
            ChemPREN = dto.ChemPREN,
            InspPMI = dto.InspPMI,
            InspVisual = dto.InspVisual,
            InspDimension = dto.InspDimension,
            InspEndoscopy = dto.InspEndoscopy,
            InspHydro = dto.InspHydro,
            InspUnderwaterPneumatic = dto.InspUnderwaterPneumatic,
            InspEddyCurrent = dto.InspEddyCurrent,
            InspUltrasonic = dto.InspUltrasonic,
            InspPortDye = dto.InspPortDye,
            TensileStrength_1 = dto.TensileStrength_1,
            TensileStrength_2 = dto.TensileStrength_2,
            YieldRp02_1 = dto.YieldRp02_1,
            YieldRp02_2 = dto.YieldRp02_2,
            YieldRp10_1 = dto.YieldRp10_1,
            YieldRp10_2 = dto.YieldRp10_2,
            Elongation_1 = dto.Elongation_1,
            Elongation_2 = dto.Elongation_2,
            Hardness_1 = dto.Hardness_1,
            Hardness_2 = dto.Hardness_2,
            GrainSize_1 = dto.GrainSize_1,
            GrainSize_2 = dto.GrainSize_2,
            FerriteContent_1 = dto.FerriteContent_1,
            FerriteContent_2 = dto.FerriteContent_2,
            FlaringResult = dto.FlaringResult,
            FlatteningResult = dto.FlatteningResult,
            IntergranularResult = dto.IntergranularResult,
            PittingResult = dto.PittingResult
        }).ToList();

        _context.Certificates.Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("创建质保书成功: {CertificateNo}", certificateNo);

        // 重新查询返回完整 DTO
        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<CertificateDetailDto> UpdateAsync(int id, CertificateUpdateRequest request)
    {
        var entity = await _context.Certificates
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new BusinessException("质保书不存在");

        // 更新头字段
        entity.CustomerName = request.CustomerName;
        entity.ProductStandard = request.ProductStandard;
        entity.ProductName = request.ProductName;
        entity.DeliveryStatus = request.DeliveryStatus;
        entity.Remark = request.Remark;

        // 更新子项（增/删/改）
        if (request.Items != null)
        {
            var incomingIds = request.Items.Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToHashSet();
            var toRemove = entity.Items.Where(i => !incomingIds.Contains(i.Id)).ToList();

            _context.CertificateItems.RemoveRange(toRemove);

            foreach (var itemDto in request.Items)
            {
                if (itemDto.Id.HasValue)
                {
                    var existing = entity.Items.FirstOrDefault(i => i.Id == itemDto.Id.Value);
                    if (existing != null)
                    {
                        MapItemUpdate(existing, itemDto);
                    }
                }
                else
                {
                    var newItem = new CertificateItem { CertificateId = id };
                    MapItemUpdate(newItem, itemDto);
                    entity.Items.Add(newItem);
                }
            }
        }

        await _context.SaveChangesAsync();

        return (await GetByIdAsync(id))!;
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Certificates
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new BusinessException("质保书不存在");

        _context.Certificates.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var certs = await _context.Certificates
            .AsNoTracking()
            .Select(c => new
            {
                c.CertificateNo,
                c.CustomerName,
                c.ProductStandard,
                c.ProductName,
                c.DeliveryStatus
            })
            .Distinct()
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["certificateNo"] = certs.Select(c => c.CertificateNo).Where(v => !string.IsNullOrEmpty(v)).Distinct().Take(200).Cast<string>().ToList(),
            ["customerName"] = certs.Select(c => c.CustomerName).Where(v => !string.IsNullOrEmpty(v)).Distinct().Take(200).Cast<string>().ToList(),
            ["productStandard"] = certs.Select(c => c.ProductStandard).Where(v => !string.IsNullOrEmpty(v)).Distinct().Take(200).Cast<string>().ToList(),
            ["productName"] = certs.Select(c => c.ProductName).Where(v => !string.IsNullOrEmpty(v)).Distinct().Take(200).Cast<string>().ToList(),
            ["deliveryStatus"] = certs.Select(c => c.DeliveryStatus).Where(v => !string.IsNullOrEmpty(v)).Distinct().Take(200).Cast<string>().ToList()
        };
    }

    public async Task<string> GetNextCertificateNoAsync(string orderNo)
    {
        var prefix = $"{orderNo}-";
        var existingNos = await _context.Certificates
            .AsNoTracking()
            .Where(c => c.CertificateNo.StartsWith(prefix))
            .Select(c => c.CertificateNo)
            .ToListAsync();

        int maxSeq = 0;
        foreach (var no in existingNos)
        {
            var match = Regex.Match(no, $@"{Regex.Escape(orderNo)}-(\d+)$");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var seq))
            {
                if (seq > maxSeq) maxSeq = seq;
            }
        }

        return $"{prefix}{maxSeq + 1:D2}";
    }

    public async Task<List<CertificateItemDto>> AutoFillInspectionDataAsync(List<AutoFillInspectionItem> items)
    {
        if (items.Count == 0) return new List<CertificateItemDto>();

        // 收集去重的查询 Key
        var heatNos = items.Where(i => !string.IsNullOrEmpty(i.HeatNo))
                           .Select(i => i.HeatNo!)
                           .Distinct()
                           .ToList();
        var batchNos = items.Where(i => !string.IsNullOrEmpty(i.ProductionBatchNo))
                            .Select(i => i.ProductionBatchNo!)
                            .Distinct()
                            .ToList();

        // 1. 查炉号登记中的化学成分（按炉号取最新登记记录）
        Dictionary<string, FurnaceRegistration>? furnMap = null;
        if (heatNos.Count > 0)
        {
            var furnRegs = await _context.Set<FurnaceRegistration>()
                .AsNoTracking()
                .Where(fr => heatNos.Contains(fr.FurnaceNumber))
                .GroupBy(fr => fr.FurnaceNumber)
                .Select(g => g.OrderByDescending(fr => fr.IncomingDate).First())
                .ToListAsync();
            furnMap = furnRegs.ToDictionary(f => f.FurnaceNumber, f => f, StringComparer.OrdinalIgnoreCase);
        }

        // 2. 查成品检验（按生产批号取每个检验项目的最新一条）
        Dictionary<string, List<FinalInspection>>? finalInspMap = null;
        if (batchNos.Count > 0)
        {
            var finalInspections = await _context.Set<FinalInspection>()
                .AsNoTracking()
                .Where(fi => batchNos.Contains(fi.BatchNo))
                .GroupBy(fi => new { fi.BatchNo, fi.InspectionItem })
                .Select(g => g.OrderByDescending(fi => fi.InspectionDate).First())
                .ToListAsync();

            finalInspMap = finalInspections
                .GroupBy(fi => fi.BatchNo)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        }

        // 3. 查拉伸检验（按生产编号+试样号取最新，TensileTest.FurnaceNo 实际存储生产编号）
        Dictionary<string, List<TensileTest>>? tensileMap = null;
        if (batchNos.Count > 0)
        {
            var tensileTests = await _context.Set<TensileTest>()
                .AsNoTracking()
                .Where(tt => batchNos.Contains(tt.FurnaceNo))
                .GroupBy(tt => new { tt.FurnaceNo, tt.SampleNo })
                .Select(g => g.OrderByDescending(tt => tt.InspectionDate).First())
                .ToListAsync();
            tensileMap = tensileTests
                .GroupBy(t => t.FurnaceNo)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        }

        // 3b. 查硬度检验（按生产编号+试样号取最新）
        Dictionary<string, List<HardnessTest>>? hardnessMap = null;
        if (batchNos.Count > 0)
        {
            var hardnessTests = await _context.Set<HardnessTest>()
                .AsNoTracking()
                .Where(ht => batchNos.Contains(ht.FurnaceNo))
                .GroupBy(ht => new { ht.FurnaceNo, ht.SampleNo })
                .Select(g => g.OrderByDescending(ht => ht.InspectionDate).First())
                .ToListAsync();
            hardnessMap = hardnessTests
                .GroupBy(t => t.FurnaceNo)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        }

        // 3c. 查晶粒度检验（按生产编号+试样号取最新）
        Dictionary<string, List<GrainSizeTest>>? grainSizeMap = null;
        if (batchNos.Count > 0)
        {
            var grainSizeTests = await _context.Set<GrainSizeTest>()
                .AsNoTracking()
                .Where(gt => batchNos.Contains(gt.FurnaceNo))
                .GroupBy(gt => new { gt.FurnaceNo, gt.SampleNo })
                .Select(g => g.OrderByDescending(gt => gt.InspectionDate).First())
                .ToListAsync();
            grainSizeMap = grainSizeTests
                .GroupBy(t => t.FurnaceNo)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        }

        // 3d. 查扩口试验（按生产编号取最新）
        Dictionary<string, FlaringTest>? flaringMap = null;
        if (batchNos.Count > 0)
        {
            var flaringTests = await _context.Set<FlaringTest>()
                .AsNoTracking()
                .Where(ft => batchNos.Contains(ft.FurnaceNo))
                .GroupBy(ft => ft.FurnaceNo)
                .Select(g => g.OrderByDescending(ft => ft.InspectionDate).First())
                .ToListAsync();
            flaringMap = flaringTests.ToDictionary(t => t.FurnaceNo, t => t, StringComparer.OrdinalIgnoreCase);
        }

        // 3e. 查压扁试验（按生产编号取最新）
        Dictionary<string, FlatteningTest>? flatteningMap = null;
        if (batchNos.Count > 0)
        {
            var flatteningTests = await _context.Set<FlatteningTest>()
                .AsNoTracking()
                .Where(ft => batchNos.Contains(ft.FurnaceNo))
                .GroupBy(ft => ft.FurnaceNo)
                .Select(g => g.OrderByDescending(ft => ft.InspectionDate).First())
                .ToListAsync();
            flatteningMap = flatteningTests.ToDictionary(t => t.FurnaceNo, t => t, StringComparer.OrdinalIgnoreCase);
        }

        // 3f. 查晶间腐蚀试验（按生产编号取最新）
        Dictionary<string, IntergranularCorrosionTest>? intergranularMap = null;
        if (batchNos.Count > 0)
        {
            var intergranularTests = await _context.Set<IntergranularCorrosionTest>()
                .AsNoTracking()
                .Where(it => batchNos.Contains(it.FurnaceNo))
                .GroupBy(it => it.FurnaceNo)
                .Select(g => g.OrderByDescending(it => it.InspectionDate).First())
                .ToListAsync();
            intergranularMap = intergranularTests.ToDictionary(t => t.FurnaceNo, t => t, StringComparer.OrdinalIgnoreCase);
        }

        // 3g. 查金相检验（按生产编号+试样号取最新）
        Dictionary<string, List<MetallographicTest>>? metallographicMap = null;
        if (batchNos.Count > 0)
        {
            var metallographicTests = await _context.Set<MetallographicTest>()
                .AsNoTracking()
                .Where(mt => batchNos.Contains(mt.FurnaceNo))
                .GroupBy(mt => new { mt.FurnaceNo, mt.SampleNo })
                .Select(g => g.OrderByDescending(mt => mt.InspectionDate).First())
                .ToListAsync();
            metallographicMap = metallographicTests
                .GroupBy(t => t.FurnaceNo)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        }

        // 3h. 查点腐蚀试验（按生产编号取最新）
        Dictionary<string, PittingCorrosionTest>? pittingMap = null;
        if (batchNos.Count > 0)
        {
            var pittingTests = await _context.Set<PittingCorrosionTest>()
                .AsNoTracking()
                .Where(pt => batchNos.Contains(pt.FurnaceNo))
                .GroupBy(pt => pt.FurnaceNo)
                .Select(g => g.OrderByDescending(pt => pt.InspectionDate).First())
                .ToListAsync();
            pittingMap = pittingTests.ToDictionary(t => t.FurnaceNo, t => t, StringComparer.OrdinalIgnoreCase);
        }

        // 4. 组装结果
        var result = new List<CertificateItemDto>();
        foreach (var item in items)
        {
            var dto = new CertificateItemDto
            {
                SeqNo = item.SeqNo,
                HeatNo = item.HeatNo,
                ProductionBatchNo = item.ProductionBatchNo
            };

            // 4a. 填充化学成分（按炉号匹配 FurnaceRegistration）
            if (!string.IsNullOrEmpty(item.HeatNo) && furnMap != null
                && furnMap.TryGetValue(item.HeatNo, out var furn))
            {
                dto.ChemC = furn.Carbon;
                dto.ChemSi = furn.Silicon;
                dto.ChemMn = furn.Manganese;
                dto.ChemP = furn.Phosphorus;
                dto.ChemS = furn.Sulfur;
                dto.ChemNi = furn.Nickel;
                dto.ChemCr = furn.Chromium;
                dto.ChemMo = furn.Molybdenum;
                dto.ChemCu = furn.Copper;
                dto.ChemN = furn.Nitrogen;
                dto.ChemNb = furn.Niobium;
                dto.ChemTi = furn.Titanium;
                dto.ChemFe = furn.Iron;
                dto.ChemAl = furn.Aluminum;
                dto.ChemW = furn.Tungsten;
                dto.ChemPREN = furn.PREN;
            }

            // 4b. 填充成品检验（按生产批号匹配）
            if (!string.IsNullOrEmpty(item.ProductionBatchNo) && finalInspMap != null
                && finalInspMap.TryGetValue(item.ProductionBatchNo, out var inspList))
            {
                foreach (var fi in inspList)
                {
                    var resultText = IsInspectionQualified(fi) ? "合格" : "不合格";
                    switch (fi.InspectionItem)
                    {
                        case InspectionItem.PMIInspection: dto.InspPMI = resultText; break;
                        case InspectionItem.VisualInspection: dto.InspVisual = resultText; break;
                        case InspectionItem.Dimension: dto.InspDimension = resultText; break;
                        case InspectionItem.Endoscopy: dto.InspEndoscopy = resultText; break;
                        case InspectionItem.HydrostaticPressure: dto.InspHydro = resultText; break;
                        case InspectionItem.UnderwaterPneumatic: dto.InspUnderwaterPneumatic = resultText; break;
                        case InspectionItem.EddyCurrent: dto.InspEddyCurrent = resultText; break;
                        case InspectionItem.Ultrasonic: dto.InspUltrasonic = resultText; break;
                        case InspectionItem.PortColoring: dto.InspPortDye = resultText; break;
                    }
                }
            }

            // 4c. 填充拉伸检验（按生产编号+试样号分别匹配 _1/_2）
            if (!string.IsNullOrEmpty(item.ProductionBatchNo) && tensileMap != null
                && tensileMap.TryGetValue(item.ProductionBatchNo, out var tensileList))
            {
                var sample1 = tensileList.FirstOrDefault(t => t.SampleNo == 1);
                var sample2 = tensileList.FirstOrDefault(t => t.SampleNo == 2);
                if (sample1 != null)
                {
                    dto.TensileStrength_1 = sample1.TensileStrength;
                    dto.YieldRp02_1 = sample1.YieldStrengthRp02;
                    dto.YieldRp10_1 = sample1.YieldStrengthRp1;
                    dto.Elongation_1 = sample1.Elongation;
                }
                if (sample2 != null)
                {
                    dto.TensileStrength_2 = sample2.TensileStrength;
                    dto.YieldRp02_2 = sample2.YieldStrengthRp02;
                    dto.YieldRp10_2 = sample2.YieldStrengthRp1;
                    dto.Elongation_2 = sample2.Elongation;
                }
            }

            // 4d. 填充硬度检验值（按生产编号+试样号分别匹配 _1/_2 取 HardnessValue）
            if (!string.IsNullOrEmpty(item.ProductionBatchNo) && hardnessMap != null
                && hardnessMap.TryGetValue(item.ProductionBatchNo, out var hardnessList))
            {
                var hSample1 = hardnessList.FirstOrDefault(h => h.SampleNo == 1);
                var hSample2 = hardnessList.FirstOrDefault(h => h.SampleNo == 2);
                if (hSample1 != null)
                    dto.Hardness_1 = hSample1.HardnessValue;
                if (hSample2 != null)
                    dto.Hardness_2 = hSample2.HardnessValue;
            }

            // 4e. 填充晶粒度检验值（按生产编号+试样号分别匹配 _1/_2 取 GrainSizeGrade）
            if (!string.IsNullOrEmpty(item.ProductionBatchNo) && grainSizeMap != null
                && grainSizeMap.TryGetValue(item.ProductionBatchNo, out var grainSizeList))
            {
                var gsSample1 = grainSizeList.FirstOrDefault(g => g.SampleNo == 1);
                var gsSample2 = grainSizeList.FirstOrDefault(g => g.SampleNo == 2);
                if (gsSample1 != null)
                    dto.GrainSize_1 = gsSample1.GrainSizeGrade;
                if (gsSample2 != null)
                    dto.GrainSize_2 = gsSample2.GrainSizeGrade;
            }

            // 4f. 填充扩口试验判定
            if (!string.IsNullOrEmpty(item.ProductionBatchNo) && flaringMap != null
                && flaringMap.TryGetValue(item.ProductionBatchNo, out var flaring))
            {
                dto.FlaringResult = flaring.Judgment;
            }

            // 4g. 填充压扁试验判定
            if (!string.IsNullOrEmpty(item.ProductionBatchNo) && flatteningMap != null
                && flatteningMap.TryGetValue(item.ProductionBatchNo, out var flattening))
            {
                dto.FlatteningResult = flattening.Judgment;
            }

            // 4h. 填充晶间腐蚀试验判定
            if (!string.IsNullOrEmpty(item.ProductionBatchNo) && intergranularMap != null
                && intergranularMap.TryGetValue(item.ProductionBatchNo, out var intergranular))
            {
                dto.IntergranularResult = intergranular.Judgment;
            }

            // 4i. 填充金相检验铁素体含量（按生产编号+试样号分别匹配 _1/_2）
            if (!string.IsNullOrEmpty(item.ProductionBatchNo) && metallographicMap != null
                && metallographicMap.TryGetValue(item.ProductionBatchNo, out var metallographicList))
            {
                var mSample1 = metallographicList.FirstOrDefault(m => m.SampleNo == 1);
                var mSample2 = metallographicList.FirstOrDefault(m => m.SampleNo == 2);
                // 若按 SampleNo 匹配不到，降级取列表中的记录（兼容 SampleNo 为 NULL 的情况）
                mSample1 ??= metallographicList.FirstOrDefault();
                dto.FerriteContent_1 = mSample1?.FerriteContent;
                if (mSample2 != null)
                    dto.FerriteContent_2 = mSample2.FerriteContent;
                else if (metallographicList.Count > 1)
                    dto.FerriteContent_2 = metallographicList.Last().FerriteContent;
            }

            // 4j. 填充点腐蚀试验判定
            if (!string.IsNullOrEmpty(item.ProductionBatchNo) && pittingMap != null
                && pittingMap.TryGetValue(item.ProductionBatchNo, out var pitting))
            {
                dto.PittingResult = pitting.Judgment;
            }

            result.Add(dto);
        }

        return result;
    }

    /// <summary>
    /// 判断成品检验是否合格（合格支数 > 0 或 合格支数 = 检验支数）
    /// </summary>
    private static bool IsInspectionQualified(FinalInspection fi)
    {
        if (fi.QualifiedQuantity.HasValue && fi.Quantity.HasValue)
            return fi.QualifiedQuantity.Value >= fi.Quantity.Value;
        if (fi.QualifiedQuantity.HasValue)
            return fi.QualifiedQuantity.Value > 0;
        return false;
    }

    #region Private Helpers

    private static void MapItemUpdate(CertificateItem item, CertificateItemUpdateDto dto)
    {
        item.SeqNo = dto.SeqNo;

        // 第1类：仓库信息
        item.InventoryBatchNo = dto.InventoryBatchNo;
        item.ProductionBatchNo = dto.ProductionBatchNo;
        item.HeatNo = dto.HeatNo;
        item.SteelGrade = dto.SteelGrade;
        item.Specification = dto.Specification;
        item.LengthDesc = dto.LengthDesc;
        item.Quantity = dto.Quantity;
        item.Meters = dto.Meters;
        item.Weight = dto.Weight;

        // 第2类：化学成分
        item.ChemC = dto.ChemC;
        item.ChemSi = dto.ChemSi;
        item.ChemMn = dto.ChemMn;
        item.ChemP = dto.ChemP;
        item.ChemS = dto.ChemS;
        item.ChemNi = dto.ChemNi;
        item.ChemCr = dto.ChemCr;
        item.ChemMo = dto.ChemMo;
        item.ChemCu = dto.ChemCu;
        item.ChemN = dto.ChemN;
        item.ChemNb = dto.ChemNb;
        item.ChemTi = dto.ChemTi;
        item.ChemFe = dto.ChemFe;
        item.ChemAl = dto.ChemAl;
        item.ChemW = dto.ChemW;
        item.ChemPREN = dto.ChemPREN;

        // 第3类：成品检验
        item.InspPMI = dto.InspPMI;
        item.InspVisual = dto.InspVisual;
        item.InspDimension = dto.InspDimension;
        item.InspEndoscopy = dto.InspEndoscopy;
        item.InspHydro = dto.InspHydro;
        item.InspUnderwaterPneumatic = dto.InspUnderwaterPneumatic;
        item.InspEddyCurrent = dto.InspEddyCurrent;
        item.InspUltrasonic = dto.InspUltrasonic;
        item.InspPortDye = dto.InspPortDye;

        // 第4类：理化检测
        item.TensileStrength_1 = dto.TensileStrength_1;
        item.TensileStrength_2 = dto.TensileStrength_2;
        item.YieldRp02_1 = dto.YieldRp02_1;
        item.YieldRp02_2 = dto.YieldRp02_2;
        item.YieldRp10_1 = dto.YieldRp10_1;
        item.YieldRp10_2 = dto.YieldRp10_2;
        item.Elongation_1 = dto.Elongation_1;
        item.Elongation_2 = dto.Elongation_2;
        item.Hardness_1 = dto.Hardness_1;
        item.Hardness_2 = dto.Hardness_2;
        item.GrainSize_1 = dto.GrainSize_1;
        item.GrainSize_2 = dto.GrainSize_2;
        item.FerriteContent_1 = dto.FerriteContent_1;
        item.FerriteContent_2 = dto.FerriteContent_2;
        item.FlaringResult = dto.FlaringResult;
        item.FlatteningResult = dto.FlatteningResult;
        item.IntergranularResult = dto.IntergranularResult;
        item.PittingResult = dto.PittingResult;
    }

    #endregion
}
