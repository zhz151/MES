using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MES.Core.DTOs;
using MES.Core.Exceptions;
using MES.Core.Interfaces;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Services.Helpers;
using MES.Services.Printing;
using OfficeOpenXml;

namespace MES.Services.Quality;

/// <summary>
/// 牌号化学成分服务实现
/// </summary>
public class ChemicalCompositionService : IChemicalCompositionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ChemicalCompositionService> _logger;

    public ChemicalCompositionService(
        AppDbContext context,
        ILogger<ChemicalCompositionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<ChemicalCompositionDto>> GetAllAsync(QueryParams query)
    {
        var queryable = _context.ChemicalCompositions
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword;
            queryable = queryable.Where(r =>
                r.PlantGrade.Contains(kw) ||
                (r.Carbon != null && r.Carbon.Contains(kw)) ||
                (r.Silicon != null && r.Silicon.Contains(kw)) ||
                (r.Manganese != null && r.Manganese.Contains(kw)) ||
                (r.Phosphorus != null && r.Phosphorus.Contains(kw)) ||
                (r.Sulfur != null && r.Sulfur.Contains(kw)) ||
                (r.Nickel != null && r.Nickel.Contains(kw)) ||
                (r.Chromium != null && r.Chromium.Contains(kw)) ||
                (r.Molybdenum != null && r.Molybdenum.Contains(kw)) ||
                (r.Copper != null && r.Copper.Contains(kw)) ||
                (r.Nitrogen != null && r.Nitrogen.Contains(kw)) ||
                (r.Niobium != null && r.Niobium.Contains(kw)) ||
                (r.Titanium != null && r.Titanium.Contains(kw)) ||
                (r.Iron != null && r.Iron.Contains(kw)) ||
                (r.Aluminum != null && r.Aluminum.Contains(kw)) ||
                (r.Tungsten != null && r.Tungsten.Contains(kw)) ||
                (r.PREN != null && r.PREN.Contains(kw)));
        }

        queryable = queryable.ApplyFilters(query.Filters);
        var totalCount = await queryable.CountAsync();

        queryable = ApplySorting(queryable, query.SortBy ?? "plantgrade", query.IsDescending);

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(r => new ChemicalCompositionDto
            {
                Id = r.Id,
                PlantGrade = r.PlantGrade,
                Carbon = r.Carbon,
                Silicon = r.Silicon,
                Manganese = r.Manganese,
                Phosphorus = r.Phosphorus,
                Sulfur = r.Sulfur,
                Nickel = r.Nickel,
                Chromium = r.Chromium,
                Molybdenum = r.Molybdenum,
                Copper = r.Copper,
                Nitrogen = r.Nitrogen,
                Niobium = r.Niobium,
                Titanium = r.Titanium,
                Iron = r.Iron,
                Aluminum = r.Aluminum,
                Tungsten = r.Tungsten,
                PREN = r.PREN,
                CreatedTime = r.CreatedTime,
                UpdatedTime = r.UpdatedTime
            })
            .ToListAsync();

        return new PagedResult<ChemicalCompositionDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = query.PageIndex,
            PageSize = query.PageSize
        };
    }

    public async Task<List<ChemicalCompositionDto>> GetAllListAsync()
    {
        return await _context.ChemicalCompositions
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Select(x => new ChemicalCompositionDto
            {
                Id = x.Id,
                PlantGrade = x.PlantGrade,
                Carbon = x.Carbon, Silicon = x.Silicon, Manganese = x.Manganese, Phosphorus = x.Phosphorus, Sulfur = x.Sulfur,
                Nickel = x.Nickel, Chromium = x.Chromium, Molybdenum = x.Molybdenum, Copper = x.Copper,
                Nitrogen = x.Nitrogen, Niobium = x.Niobium, Titanium = x.Titanium, Iron = x.Iron,
                Aluminum = x.Aluminum, Tungsten = x.Tungsten, PREN = x.PREN,
                CreatedTime = x.CreatedTime,
                UpdatedTime = x.UpdatedTime
            })
            .ToListAsync();
    }

    public async Task<List<ChemicalCompositionDto>> BatchCreateAsync(List<CreateChemicalCompositionRequest> requests)
    {
        if (requests.Count == 0)
            return new List<ChemicalCompositionDto>();

        // 检查牌号重复
        var plantGrades = requests.Select(r => r.PlantGrade).Distinct().ToList();
        var existing = await _context.ChemicalCompositions
            .Where(c => plantGrades.Contains(c.PlantGrade))
            .Select(c => c.PlantGrade)
            .ToListAsync();

        if (existing.Any())
            throw new BusinessException($"以下工厂牌号已存在: {string.Join(", ", existing)}");

        var entities = requests.Select(r => new ChemicalComposition
        {
            PlantGrade = r.PlantGrade,
            Carbon = r.Carbon,
            Silicon = r.Silicon,
            Manganese = r.Manganese,
            Phosphorus = r.Phosphorus,
            Sulfur = r.Sulfur,
            Nickel = r.Nickel,
            Chromium = r.Chromium,
            Molybdenum = r.Molybdenum,
            Copper = r.Copper,
            Nitrogen = r.Nitrogen,
            Niobium = r.Niobium,
            Titanium = r.Titanium,
            Iron = r.Iron,
            Aluminum = r.Aluminum,
            Tungsten = r.Tungsten,
            PREN = r.PREN
        }).ToList();

        _context.ChemicalCompositions.AddRange(entities);
        await _context.SaveChangesAsync();

        return entities.Select(e => new ChemicalCompositionDto
        {
            Id = e.Id,
            PlantGrade = e.PlantGrade,
            Carbon = e.Carbon,
            Silicon = e.Silicon,
            Manganese = e.Manganese,
            Phosphorus = e.Phosphorus,
            Sulfur = e.Sulfur,
            Nickel = e.Nickel,
            Chromium = e.Chromium,
            Molybdenum = e.Molybdenum,
            Copper = e.Copper,
            Nitrogen = e.Nitrogen,
            Niobium = e.Niobium,
            Titanium = e.Titanium,
            Iron = e.Iron,
            Aluminum = e.Aluminum,
            Tungsten = e.Tungsten,
            PREN = e.PREN,
            CreatedTime = e.CreatedTime,
            UpdatedTime = e.UpdatedTime
        }).ToList();
    }

    public async Task<ChemicalCompositionDto> UpdateAsync(int id, UpdateChemicalCompositionRequest request)
    {
        var entity = await _context.ChemicalCompositions.FindAsync(id)
            ?? throw new BusinessException($"牌号化学成分记录不存在(Id={id})");

        entity.PlantGrade = request.PlantGrade;
        entity.Carbon = request.Carbon ?? entity.Carbon;
        entity.Silicon = request.Silicon ?? entity.Silicon;
        entity.Manganese = request.Manganese ?? entity.Manganese;
        entity.Phosphorus = request.Phosphorus ?? entity.Phosphorus;
        entity.Sulfur = request.Sulfur ?? entity.Sulfur;
        entity.Nickel = request.Nickel ?? entity.Nickel;
        entity.Chromium = request.Chromium ?? entity.Chromium;
        entity.Molybdenum = request.Molybdenum ?? entity.Molybdenum;
        entity.Copper = request.Copper ?? entity.Copper;
        entity.Nitrogen = request.Nitrogen ?? entity.Nitrogen;
        entity.Niobium = request.Niobium ?? entity.Niobium;
        entity.Titanium = request.Titanium ?? entity.Titanium;
        entity.Iron = request.Iron ?? entity.Iron;
        entity.Aluminum = request.Aluminum ?? entity.Aluminum;
        entity.Tungsten = request.Tungsten ?? entity.Tungsten;
        entity.PREN = request.PREN ?? entity.PREN;

        await _context.SaveChangesAsync();

        return new ChemicalCompositionDto
        {
            Id = entity.Id,
            PlantGrade = entity.PlantGrade,
            Carbon = entity.Carbon,
            Silicon = entity.Silicon,
            Manganese = entity.Manganese,
            Phosphorus = entity.Phosphorus,
            Sulfur = entity.Sulfur,
            Nickel = entity.Nickel,
            Chromium = entity.Chromium,
            Molybdenum = entity.Molybdenum,
            Copper = entity.Copper,
            Nitrogen = entity.Nitrogen,
            Niobium = entity.Niobium,
            Titanium = entity.Titanium,
            Iron = entity.Iron,
            Aluminum = entity.Aluminum,
            Tungsten = entity.Tungsten,
            PREN = entity.PREN,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime
        };
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.ChemicalCompositions.FindAsync(id)
            ?? throw new BusinessException($"牌号化学成分记录不存在(Id={id})");

        _context.ChemicalCompositions.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<string, List<string>>> GetFilterContextsAsync()
    {
        var all = await _context.ChemicalCompositions
            .AsNoTracking()
            .Select(r => new
            {
                r.PlantGrade,
                r.Carbon,
                r.Silicon,
                r.Manganese,
                r.Phosphorus,
                r.Sulfur,
                r.Nickel,
                r.Chromium,
                r.Molybdenum,
                r.Copper,
                r.Nitrogen,
                r.Niobium,
                r.Titanium,
                r.Iron,
                r.Aluminum,
                r.Tungsten,
                r.PREN
            })
            .ToListAsync();

        return new Dictionary<string, List<string>>
        {
            ["PlantGrade"] = all.Select(x => x.PlantGrade).Where(v => !string.IsNullOrEmpty(v)).Distinct().OrderBy(v => v).ToList(),
            ["Carbon"] = all.Select(x => x.Carbon ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Silicon"] = all.Select(x => x.Silicon ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Manganese"] = all.Select(x => x.Manganese ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Phosphorus"] = all.Select(x => x.Phosphorus ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Sulfur"] = all.Select(x => x.Sulfur ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Nickel"] = all.Select(x => x.Nickel ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Chromium"] = all.Select(x => x.Chromium ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Molybdenum"] = all.Select(x => x.Molybdenum ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Copper"] = all.Select(x => x.Copper ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Nitrogen"] = all.Select(x => x.Nitrogen ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Niobium"] = all.Select(x => x.Niobium ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Titanium"] = all.Select(x => x.Titanium ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Iron"] = all.Select(x => x.Iron ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Aluminum"] = all.Select(x => x.Aluminum ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["Tungsten"] = all.Select(x => x.Tungsten ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList(),
            ["PREN"] = all.Select(x => x.PREN ?? "").Where(v => v != "").Distinct().OrderBy(v => v).ToList()
        };
    }

    public async Task<byte[]> GenerateTemplateAsync()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("牌号化学成分");

        // 表头
        var headers = new[] {
            "工厂牌号", "C", "Si", "Mn", "P", "S", "Ni", "Cr",
            "Mo", "Cu", "N", "Nb", "Ti", "Fe", "Al", "W", "PREN腐蚀当量"
        };
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cells[1, i + 1].Value = headers[i];
        }

        // 样式：表头加粗
        worksheet.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;

        // 列宽
        for (int i = 1; i <= headers.Length; i++)
        {
            worksheet.Column(i).AutoFit();
        }

        return await package.GetAsByteArrayAsync();
    }

    public async Task<ImportPreviewResult> PreviewImportAsync(byte[] fileData, string fileName)
    {
        var result = new ImportPreviewResult();

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var ms = new MemoryStream(fileData);
        using var package = new ExcelPackage(ms);
        var worksheet = package.Workbook.Worksheets[0];
        if (worksheet == null) return result;

        var rowCount = worksheet.Dimension?.Rows ?? 0;
        if (rowCount < 2) return result;

        // 解析列映射
        var colMap = ParseColumnHeaders(worksheet);
        var columnMapping = GetColumnMapping();

        var existingGrades = await _context.ChemicalCompositions
            .Select(c => c.PlantGrade)
            .ToListAsync();
        var existingSet = existingGrades.Select(g => g).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        result.TotalRows = rowCount - 1;

        for (int r = 2; r <= rowCount; r++)
        {
            var rowResult = new ImportRowResult { RowNumber = r };
            string? plantGrade = null;

            foreach (var (colIdx, header) in colMap)
            {
                if (!columnMapping.TryGetValue(header, out var fieldName))
                    continue;
                var cellValue = worksheet.Cells[r, colIdx].Text?.Trim();
                if (fieldName == "PlantGrade")
                    plantGrade = cellValue;
            }

            if (string.IsNullOrWhiteSpace(plantGrade))
            {
                rowResult.Errors.Add("工厂牌号不能为空");
                rowResult.IsValid = false;
                result.ErrorCount++;
            }
            else
            {
                rowResult.Key = plantGrade;
                if (existingSet.Contains(plantGrade))
                {
                    rowResult.IsDuplicate = true;
                    result.DuplicateCount++;
                }
                if (seenInFile.Contains(plantGrade))
                {
                    rowResult.Errors.Add("文件内存在重复的工厂牌号");
                    rowResult.IsValid = false;
                    result.ErrorCount++;
                }
                if (!rowResult.Errors.Any())
                    result.ValidCount++;
            }

            seenInFile.Add(plantGrade ?? "");
            result.RowResults.Add(rowResult);
        }

        return result;
    }

    /// <summary>
    /// 解析工作表列名
    /// </summary>
    private static Dictionary<int, string> ParseColumnHeaders(ExcelWorksheet worksheet)
    {
        var colMap = new Dictionary<int, string>();
        for (int c = 1; c <= worksheet.Dimension.Columns; c++)
        {
            var header = worksheet.Cells[1, c].Text?.Trim();
            if (!string.IsNullOrEmpty(header))
                colMap[c] = header;
        }
        return colMap;
    }

    /// <summary>
    /// 获取列名映射
    /// </summary>
    private static Dictionary<string, string> GetColumnMapping()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["工厂牌号"] = "PlantGrade",
            ["C"] = "Carbon", ["Si"] = "Silicon", ["Mn"] = "Manganese",
            ["P"] = "Phosphorus", ["S"] = "Sulfur", ["Ni"] = "Nickel",
            ["Cr"] = "Chromium", ["Mo"] = "Molybdenum", ["Cu"] = "Copper",
            ["N"] = "Nitrogen", ["Nb"] = "Niobium", ["Ti"] = "Titanium",
            ["Fe"] = "Iron", ["Al"] = "Aluminum", ["W"] = "Tungsten",
            ["PREN腐蚀当量"] = "PREN", ["PREN"] = "PREN"
        };
    }

    public async Task<ImportResult> ImportAsync(byte[] fileData, string fileName, string? userName)
    {
        var result = new ImportResult();

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var ms = new MemoryStream(fileData);
        using var package = new ExcelPackage(ms);
        var worksheet = package.Workbook.Worksheets[0];
        if (worksheet == null)
        {
            result.HasRolledBack = true;
            result.RollbackReason = "工作表为空";
            return result;
        }

        var rowCount = worksheet.Dimension?.Rows ?? 0;
        if (rowCount < 2)
        {
            result.HasRolledBack = true;
            result.RollbackReason = "Excel文件没有数据行";
            return result;
        }

        // 解析列映射（使用公共方法）
        var colMap = ParseColumnHeaders(worksheet);
        var columnMapping = GetColumnMapping();
        var entities = new List<ChemicalComposition>();
        var now = DateTimeOffset.UtcNow;
        result.TotalRows = rowCount - 1;

        for (int r = 2; r <= rowCount; r++)
        {
            var rowNumber = r;
            var errors = new List<string>();
            string? plantGrade = null;
            var values = new Dictionary<string, string?>();

            foreach (var (colIdx, header) in colMap)
            {
                if (!columnMapping.TryGetValue(header, out var fieldName))
                    continue;

                var cellValue = worksheet.Cells[r, colIdx].Text?.Trim();
                values[fieldName] = cellValue;

                if (fieldName == "PlantGrade")
                    plantGrade = cellValue;
            }

            if (string.IsNullOrWhiteSpace(plantGrade))
            {
                errors.Add("工厂牌号不能为空");
            }

            if (errors.Count > 0)
            {
                result.FailedCount++;
                result.Errors.Add(new ImportRowError { RowNumber = rowNumber, Message = string.Join("; ", errors) });
                continue;
            }

            entities.Add(new ChemicalComposition
            {
                PlantGrade = plantGrade!,
                Carbon = values.GetValueOrDefault("Carbon"),
                Silicon = values.GetValueOrDefault("Silicon"),
                Manganese = values.GetValueOrDefault("Manganese"),
                Phosphorus = values.GetValueOrDefault("Phosphorus"),
                Sulfur = values.GetValueOrDefault("Sulfur"),
                Nickel = values.GetValueOrDefault("Nickel"),
                Chromium = values.GetValueOrDefault("Chromium"),
                Molybdenum = values.GetValueOrDefault("Molybdenum"),
                Copper = values.GetValueOrDefault("Copper"),
                Nitrogen = values.GetValueOrDefault("Nitrogen"),
                Niobium = values.GetValueOrDefault("Niobium"),
                Titanium = values.GetValueOrDefault("Titanium"),
                Iron = values.GetValueOrDefault("Iron"),
                Aluminum = values.GetValueOrDefault("Aluminum"),
                Tungsten = values.GetValueOrDefault("Tungsten"),
                PREN = values.GetValueOrDefault("PREN"),
                CreatedTime = now,
                UpdatedTime = now,
                CreatedBy = userName ?? "import",
                UpdatedBy = userName ?? "import"
            });
        }

        // 检查牌号重复（数据库中已存在的）
        var plantGrades = entities.Select(e => e.PlantGrade).Distinct().ToList();
        var existingGrades = await _context.ChemicalCompositions
            .Where(c => plantGrades.Contains(c.PlantGrade))
            .Select(c => c.PlantGrade)
            .ToListAsync();

        // 移除重复的
        entities.RemoveAll(e => existingGrades.Contains(e.PlantGrade, StringComparer.OrdinalIgnoreCase));

        // 检查文件内部重复
        var internalDups = entities.GroupBy(e => e.PlantGrade, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        entities.RemoveAll(e => internalDups.Contains(e.PlantGrade));

        if (entities.Count == 0)
        {
            result.HasRolledBack = true;
            result.RollbackReason = "所有数据行均重复或无效，无数据可导入";
            return result;
        }

        try
        {
            _context.ChemicalCompositions.AddRange(entities);
            await _context.SaveChangesAsync();
            result.SuccessCount = entities.Count;
        }
        catch (Exception ex)
        {
            result.HasRolledBack = true;
            result.RollbackReason = $"导入异常: {ex.Message}";
            _logger.LogError(ex, "导入牌号化学成分失败");
        }

        return result;
    }

    public async Task<byte[]> PrintBatchAsync(int[] ids, List<PrintColumnDef> columns)
    {
        var query = new QueryParams { PageIndex = 1, PageSize = int.MaxValue };
        var result = await GetAllAsync(query);
        var selected = result.Items.Where(i => ids.Contains(i.Id)).ToList();
        return ChemicalCompositionPrintHelper.GenerateBatchPdf(selected, columns);
    }

    public async Task<byte[]> PrintAllAsync(string? keyword, string? sortBy, bool isDescending, List<PrintColumnDef> columns)
    {
        var query = new QueryParams
        {
            PageIndex = 1,
            PageSize = int.MaxValue,
            Keyword = keyword,
            SortBy = string.IsNullOrEmpty(sortBy) ? null : sortBy,
            IsDescending = isDescending
        };
        var result = await GetAllAsync(query);
        return ChemicalCompositionPrintHelper.GenerateBatchPdf(result.Items, columns);
    }

    private static IQueryable<ChemicalComposition> ApplySorting(IQueryable<ChemicalComposition> queryable, string sortBy, bool isDescending)
    {
        return queryable.ApplySort(sortBy, isDescending);
    }
}
