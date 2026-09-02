using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using MES.Core.Constants;
using MES.Core.Helpers;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.Payroll;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Payroll;

namespace MES.Services.Payroll;

/// <summary>
/// 生产计件类别专用批量导入/导出（2026-09-02，工资结算数据维护闭环）。
/// 定位键 = 工段 × 工序/产类/作业阶段三约束归一组（空=该维全选）；冲突策略 = 覆盖更新：
///   类别定义模板（category）：定位命中类别 → 更新主属性 + 整组替换三约束成员行（绝不清既有档行）；未命中 → 新建（无档行）。
///   维档系数模板（tier）：定位未命中 → 整行报错「请先导入类别定义」；命中 → 该类别 Tiers 整组替换为文件行。
/// 任一数据行无效 → 整体拒绝入库（组级原子性，预览与导入同口径解析）。
/// 模板/导出列值全用中文域值（中英容忍），工序/工段显示名反向映射自配置表，保证「导出→改→再导」闭环。
/// </summary>
public class PieceRateCategoryImportService : IPieceRateCategoryImportService
{
    private readonly AppDbContext _context;
    private readonly ISectionNameDisplayService _sectionNameDisplay;
    private readonly IProcessDefinitionService _processDefinitionService;

    public PieceRateCategoryImportService(
        AppDbContext context,
        ISectionNameDisplayService sectionNameDisplay,
        IProcessDefinitionService processDefinitionService)
    {
        _context = context;
        _sectionNameDisplay = sectionNameDisplay;
        _processDefinitionService = processDefinitionService;
    }

    // ==================== 表头常量 ====================

    /// <summary>类别定义 sheet/表头（顺序即模板列序）</summary>
    private static readonly string[] CategoryHeaders = ["工段", "工序", "产类", "阶段", "基准价", "结算单位", "启用", "备注"];

    /// <summary>维档系数 sheet/表头</summary>
    private static readonly string[] TierHeaders = ["工段", "工序", "产类", "阶段", "维度", "档值", "系数", "启用"];

    private const string CategorySheet = "类别";
    private const string TierSheet = "维档";

    /// <summary>类别列头 → 字段（按表头名匹配，列序无关）</summary>
    private static readonly Dictionary<string, string> CategoryFieldMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["工段"] = "Section", ["工序"] = "Processes", ["产类"] = "ProductStatuses", ["阶段"] = "Stages",
        ["基准价"] = "BasePrice", ["结算单位"] = "Unit", ["启用"] = "IsActive", ["备注"] = "Remark"
    };

    /// <summary>维档列头 → 字段</summary>
    private static readonly Dictionary<string, string> TierFieldMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["工段"] = "Section", ["工序"] = "Processes", ["产类"] = "ProductStatuses", ["阶段"] = "Stages",
        ["维度"] = "Dimension", ["档值"] = "Value", ["系数"] = "Ratio", ["启用"] = "IsActive"
    };

    // ==================== 导出（双 sheet：类别 + 维档） ====================

    public async Task<byte[]> ExportAsync()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var catWs = package.Workbook.Worksheets.Add(CategorySheet);
        var tierWs = package.Workbook.Worksheets.Add(TierSheet);

        var entities = await _context.PieceRateProductionCategories.AsNoTracking()
            .Include(c => c.Tiers)
            .Include(c => c.ConstraintKeys)
            .OrderBy(c => c.SectionKey).ThenBy(c => c.Id)
            .ToListAsync();
        var sectionMap = await _sectionNameDisplay.GetSectionNameMapAsync();
        var processMap = await _processDefinitionService.GetProcessNameMapAsync();

        WriteHeaders(catWs, CategoryHeaders);
        WriteHeaders(tierWs, TierHeaders);

        var catRow = 2;
        var tierRow = 2;
        foreach (var c in entities)
        {
            var procs = ConstraintKeysOf(c, PieceRateConstraintTypes.Process);
            var prods = ConstraintKeysOf(c, PieceRateConstraintTypes.ProductStatus);
            var stages = ConstraintKeysOf(c, PieceRateConstraintTypes.Stage);
            WriteCategoryRow(catWs, catRow++, c, procs, prods, stages, sectionMap, processMap);
            foreach (var t in c.Tiers.OrderBy(t => PieceRateDimensionIndex(t.DimensionKey)).ThenBy(t => t.Id))
                WriteTierRow(tierWs, tierRow++, c, procs, prods, stages, t, sectionMap, processMap);
        }

        AutoFit(catWs, CategoryHeaders.Length);
        AutoFit(tierWs, TierHeaders.Length);
        return await package.GetAsByteArrayAsync();
    }

    // ==================== 模板（单 sheet + 1 示例行） ====================

    public async Task<byte[]> GenerateTemplateAsync(string kind)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var isTier = IsTier(kind);

        var sectionKey = await PickExampleSectionAsync();
        var sectionMap = await _sectionNameDisplay.GetSectionNameMapAsync();
        var sectionName = sectionMap.TryGetValue(sectionKey, out var cn)
            ? cn : (SectionKeys.ToChinese(sectionKey) ?? sectionKey);

        var headers = isTier ? TierHeaders : CategoryHeaders;
        var sheet = isTier ? TierSheet : CategorySheet;
        var ws = package.Workbook.Worksheets.Add(sheet);
        WriteHeaders(ws, headers);
        ws.Cells[2, 1].Value = sectionName;
        if (isTier)
        {
            ws.Cells[2, 5].Value = PieceRateDimensionKeys.ToChinese(PieceRateDimensionKeys.OuterDiameter);
            ws.Cells[2, 6].Value = ">54";        // 档值（区间原文）
            ws.Cells[2, 7].Value = 1.1M;         // 系数
            ws.Cells[2, 8].Value = "是";         // 启用
        }
        else
        {
            ws.Cells[2, 5].Value = 40M;          // 基准价
            ws.Cells[2, 6].Value = "元/吨";       // 结算单位
            ws.Cells[2, 7].Value = "否";         // 启用（示例停用防误导入计价）
            ws.Cells[2, 8].Value = "示例行（导入前请删除本行）";
        }
        AutoFit(ws, headers.Length);
        return await package.GetAsByteArrayAsync();
    }

    /// <summary>示例工段：优先「当前无类别」的启用工段，避免原样导入模板误覆盖真实类别；兜底 SectionKeys[0]。</summary>
    private async Task<string> PickExampleSectionAsync()
    {
        var sections = await _context.StandardWorkDays.AsNoTracking()
            .Where(w => w.IsEnabled && w.SectionKey != null)
            .OrderBy(w => w.DisplayOrder)
            .Select(w => w.SectionKey!)
            .ToListAsync();
        var used = (await _context.PieceRateProductionCategories.AsNoTracking()
                .Select(c => c.SectionKey).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return sections.FirstOrDefault(s => !used.Contains(s)) ?? sections.FirstOrDefault() ?? SectionKeys.All[0];
    }

    // ==================== 预览 / 导入 ====================

    public async Task<ImportPreviewResult> PreviewImportAsync(string kind, byte[] fileData)
    {
        var doc = await ParseDocumentAsync(kind, fileData);
        return doc.ToPreviewResult();
    }

    public async Task<ImportResult> ImportAsync(string kind, byte[] fileData)
    {
        var doc = await ParseDocumentAsync(kind, fileData);
        if (doc.Fatal != null)
            return new ImportResult { HasRolledBack = true, RollbackReason = doc.Fatal };

        if (doc.HasErrors)
            return new ImportResult
            {
                TotalRows = doc.TotalRows,
                FailedCount = doc.Rows.Count(r => !r.IsValid),
                HasRolledBack = true,
                RollbackReason = $"存在 {doc.ErrorCount} 行错误，全部拒绝导入",
                Errors = doc.Rows
                    .Where(r => !r.IsValid)
                    .Select(r => new ImportRowError { RowNumber = r.RowNumber, Message = string.Join("; ", r.Errors) })
                    .ToList()
            };

        try
        {
            if (IsTier(kind)) await ApplyTierGroupsAsync(doc);
            else await ApplyCategoriesAsync(doc);
            await _context.SaveChangesAsync();
            return new ImportResult { TotalRows = doc.TotalRows, SuccessCount = doc.TotalRows };
        }
        catch (Exception ex)
        {
            return new ImportResult
            {
                TotalRows = doc.TotalRows,
                HasRolledBack = true,
                RollbackReason = $"导入异常（已回滚）: {ex.Message}"
            };
        }
    }

    // ==================== 落库应用 ====================

    private async Task ApplyCategoriesAsync(Doc doc)
    {
        foreach (var p in doc.Categories)
        {
            if (p.ExistingId.HasValue)
            {
                var entity = await _context.PieceRateProductionCategories
                    .Include(c => c.Tiers)              // 保留档行：类别模板绝不清档
                    .Include(c => c.ConstraintKeys)
                    .FirstAsync(c => c.Id == p.ExistingId.Value);
                entity.SectionKey = p.SectionKey;
                entity.BasePrice = p.BasePrice;
                entity.Unit = p.Unit;
                entity.IsActive = p.IsActive;
                entity.Remark = p.Remark;
                ReplaceKeys(entity, PieceRateConstraintTypes.Process, p.Processes);
                ReplaceKeys(entity, PieceRateConstraintTypes.ProductStatus, p.ProductStatuses);
                ReplaceKeys(entity, PieceRateConstraintTypes.Stage, p.Stages);
            }
            else
            {
                var entity = new PieceRateProductionCategory
                {
                    SectionKey = p.SectionKey,
                    BasePrice = p.BasePrice,
                    Unit = p.Unit,
                    IsActive = p.IsActive,
                    Remark = p.Remark
                };
                _context.PieceRateProductionCategories.Add(entity);
                ReplaceKeys(entity, PieceRateConstraintTypes.Process, p.Processes);
                ReplaceKeys(entity, PieceRateConstraintTypes.ProductStatus, p.ProductStatuses);
                ReplaceKeys(entity, PieceRateConstraintTypes.Stage, p.Stages);
            }
        }
    }

    private async Task ApplyTierGroupsAsync(Doc doc)
    {
        foreach (var g in doc.TierGroups)
        {
            var entity = await _context.PieceRateProductionCategories
                .Include(c => c.Tiers)
                .FirstAsync(c => c.Id == g.ExistingId);
            foreach (var old in entity.Tiers.ToList())
                _context.PieceRateProductionCategoryTiers.Remove(old);
            entity.Tiers.Clear();
            foreach (var t in g.Tiers)
            {
                entity.Tiers.Add(new PieceRateProductionCategoryTier
                {
                    DimensionKey = t.DimensionKey,
                    RangeText = t.RangeText,
                    MatchValue = t.MatchValue,
                    MinValue = t.MinValue,
                    MaxValue = t.MaxValue,
                    MinInt = t.MinInt,
                    MaxInt = t.MaxInt,
                    Ratio = t.Ratio,
                    IsActive = t.IsActive
                });
            }
        }
    }

    // ==================== 解析文档 ====================

    private async Task<Doc> ParseDocumentAsync(string kind, byte[] fileData)
    {
        var doc = new Doc();
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        try
        {
            using var ms = new MemoryStream(fileData);
            using var package = new ExcelPackage(ms);
            var preferred = IsTier(kind) ? TierSheet : CategorySheet;
            var ws = package.Workbook.Worksheets[preferred] ?? package.Workbook.Worksheets.FirstOrDefault();
            if (ws == null)
            {
                doc.Fatal = "Excel 中没有工作表";
                return doc;
            }
            var rowCount = ws.Dimension?.Rows ?? 0;
            if (rowCount < 2)
            {
                doc.Fatal = "Excel 文件没有数据行";
                return doc;
            }

            var fieldMap = IsTier(kind) ? TierFieldMap : CategoryFieldMap;
            var colIndex = BuildColumnIndex(ws, fieldMap);
            var ctx = await BuildParseContextAsync();
            var isTier = IsTier(kind);

            for (var r = 2; r <= rowCount; r++)
            {
                if (IsBlankRow(ws, r, colIndex)) continue; // 空行跳过（不计数/不报错）
                var row = new RowInfo { RowNumber = r };
                if (isTier) ParseTierRow(row, ws, r, colIndex, ctx, doc);
                else ParseCategoryRow(row, ws, r, colIndex, ctx, doc);
                doc.Rows.Add(row);
            }
            doc.TotalRows = doc.Rows.Count;
            if (doc.TotalRows == 0)
            {
                doc.Fatal = "Excel 文件没有数据行";
                return doc;
            }

            if (isTier) FinalizeTierDoc(doc);
            else FinalizeCategoryDoc(doc, ctx);

            doc.Recount();
            return doc;
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException)
        {
            doc.Fatal = $"无法读取 Excel 文件: {ex.Message}";
            return doc;
        }
    }

    private static Dictionary<string, int> BuildColumnIndex(ExcelWorksheet ws, IReadOnlyDictionary<string, string> fieldMap)
    {
        var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var c = 1; c <= (ws.Dimension?.Columns ?? 0); c++)
        {
            var header = ws.Cells[1, c].Text?.Trim();
            if (string.IsNullOrEmpty(header) || !fieldMap.ContainsKey(header)) continue;
            var field = fieldMap[header];
            if (!colIndex.ContainsKey(field)) colIndex[field] = c;
        }
        return colIndex;
    }

    private static bool IsBlankRow(ExcelWorksheet ws, int r, Dictionary<string, int> colIndex)
    {
        foreach (var c in colIndex.Values)
            if (!string.IsNullOrWhiteSpace(ws.Cells[r, c].Text)) return false;
        return true;
    }

    // ---------------- 类别行解析 ----------------

    private void ParseCategoryRow(RowInfo row, ExcelWorksheet ws, int r,
        Dictionary<string, int> colIndex, ParseContext ctx, Doc doc)
    {
        var sectionText = CellText(ws, r, colIndex, "Section");
        var sectionKey = ResolveSection(sectionText, ctx);
        if (string.IsNullOrWhiteSpace(sectionText)) AddParseError(row,"工段不能为空");
        else if (sectionKey == null) AddParseError(row,$"无效的工段: {sectionText}");

        ResolveProcessKeys(CellText(ws, r, colIndex, "Processes"), ctx, out var procRaw);
        if (procRaw.Error != null) AddParseError(row,procRaw.Error);
        ResolveFixedKeys(CellText(ws, r, colIndex, "ProductStatuses"), ProductStatuses.ToKey, "产类", out var prodRaw);
        if (prodRaw.Error != null) AddParseError(row,prodRaw.Error);
        ResolveFixedKeys(CellText(ws, r, colIndex, "Stages"), PieceRateStageKeys.ToKey, "作业阶段", out var stageRaw);
        if (stageRaw.Error != null) AddParseError(row,stageRaw.Error);

        if (!TryCellDecimal(ws, r, colIndex, "BasePrice", out var basePrice)) AddParseError(row,"基准价必须为数字");
        else if (basePrice <= 0) AddParseError(row,"基准价必须大于0");

        var unitText = CellText(ws, r, colIndex, "Unit");
        var unit = ResolveUnit(unitText);
        if (string.IsNullOrWhiteSpace(unitText)) AddParseError(row,"结算单位不能为空");
        else if (unit == null) AddParseError(row,$"无效的结算单位: {unitText}");

        var isActive = ParseBool(CellText(ws, r, colIndex, "IsActive"), true);
        var remark = string.IsNullOrWhiteSpace(CellText(ws, r, colIndex, "Remark"))
            ? null : CellText(ws, r, colIndex, "Remark")!.Trim();

        if (!row.IsValid) return;

        var procs = NormalizeArray(procRaw.Keys!, ctx.ProcessDomain);
        var prods = NormalizeArray(prodRaw.Keys!, ProductStatuses.All);
        var stages = NormalizeArray(stageRaw.Keys!, PieceRateStageKeys.All);

        var locator = LocatorKey(sectionKey!, procs, prods, stages);
        if (!doc.ClaimLocator(locator))
        {
            AddParseError(row,"文件内存在重复定位类别（同工段 + 同三约束集合），请合并或删除");
            return;
        }

        var existing = ctx.AllCategories.FirstOrDefault(c =>
            string.Equals(c.SectionKey, sectionKey, StringComparison.OrdinalIgnoreCase)
            && SameKeys(procs, ConstraintKeysOf(c, PieceRateConstraintTypes.Process))
            && SameKeys(prods, ConstraintKeysOf(c, PieceRateConstraintTypes.ProductStatus))
            && SameKeys(stages, ConstraintKeysOf(c, PieceRateConstraintTypes.Stage)));

        var pending = new PendingCategory
        {
            RowNumber = r,
            SectionKey = sectionKey!,
            Processes = procs,
            ProductStatuses = prods,
            Stages = stages,
            BasePrice = basePrice,
            Unit = unit!,
            IsActive = isActive,
            Remark = remark,
            ExistingId = existing?.Id
        };
        doc.Categories.Add(pending);
        row.Key = pending.Coverage.Describe();
        row.IsDuplicate = existing != null;
        row.RowAction = existing != null ? "覆盖" : "新增";
        row.ActionNote = existing != null
            ? $"定位命中类别 Id={existing.Id}，整组更新主属性+三约束（不动档行）"
            : "未命中既有类别，将新建（无档行）";
    }

    // ---------------- 维档行解析 ----------------

    private void ParseTierRow(RowInfo row, ExcelWorksheet ws, int r,
        Dictionary<string, int> colIndex, ParseContext ctx, Doc doc)
    {
        var sectionText = CellText(ws, r, colIndex, "Section");
        var sectionKey = ResolveSection(sectionText, ctx);
        if (string.IsNullOrWhiteSpace(sectionText)) AddParseError(row,"工段不能为空");
        else if (sectionKey == null) AddParseError(row,$"无效的工段: {sectionText}");

        ResolveProcessKeys(CellText(ws, r, colIndex, "Processes"), ctx, out var procRaw);
        if (procRaw.Error != null) AddParseError(row,procRaw.Error);
        ResolveFixedKeys(CellText(ws, r, colIndex, "ProductStatuses"), ProductStatuses.ToKey, "产类", out var prodRaw);
        if (prodRaw.Error != null) AddParseError(row,prodRaw.Error);
        ResolveFixedKeys(CellText(ws, r, colIndex, "Stages"), PieceRateStageKeys.ToKey, "作业阶段", out var stageRaw);
        if (stageRaw.Error != null) AddParseError(row,stageRaw.Error);

        var dimText = CellText(ws, r, colIndex, "Dimension");
        var dimKey = ResolveDimension(dimText);
        if (string.IsNullOrWhiteSpace(dimText)) AddParseError(row,"维度不能为空");
        else if (dimKey == null) AddParseError(row,$"无效的维度: {dimText}");

        var valueText = CellText(ws, r, colIndex, "Value");
        if (string.IsNullOrWhiteSpace(valueText)) AddParseError(row,$"档值不能为空（{dimText}）");

        if (!TryCellDecimal(ws, r, colIndex, "Ratio", out var ratio)) AddParseError(row,"系数必须为数字");
        else if (ratio <= 0) AddParseError(row,"系数必须大于0");
        var isActive = ParseBool(CellText(ws, r, colIndex, "IsActive"), true);

        if (!row.IsValid) return;

        var procs = NormalizeArray(procRaw.Keys!, ctx.ProcessDomain);
        var prods = NormalizeArray(prodRaw.Keys!, ProductStatuses.All);
        var stages = NormalizeArray(stageRaw.Keys!, PieceRateStageKeys.All);

        // 建档行（区间维解析边界；等值维取取值——特殊制造状态归一英文 Key）
        var tier = new PieceRateProductionCategoryTier { DimensionKey = dimKey!, Ratio = ratio, IsActive = isActive };
        string? displayValue;
        var rawValue = valueText!.Trim();
        if (PieceRateDimensionKeys.IsValueDimension(dimKey))
        {
            string? match = dimKey == PieceRateDimensionKeys.SpecialState
                ? PieceRateStateKeys.ToKey(rawValue)
                : rawValue;
            if (match == null)
            {
                AddParseError(row,$"无法识别的特殊制造状态: {rawValue}");
                return;
            }
            displayValue = dimKey == PieceRateDimensionKeys.SpecialState
                ? PieceRateStateKeys.ToChinese(match) ?? match
                : rawValue;
            tier.MatchValue = match;
            tier.RangeText = match;
        }
        else
        {
            if (!PieceRateRangeParser.TryParseRange(rawValue, out var min, out var max))
            {
                AddParseError(row,$"{PieceRateDimensionKeys.ToChinese(dimKey)}档必须填写可解析的区间: {rawValue}");
                return;
            }
            displayValue = rawValue;
            tier.RangeText = rawValue;
            if (dimKey == PieceRateDimensionKeys.FixedLengthCount)
            {
                var minInt = ToIntBound(min);
                var maxInt = ToIntBound(max);
                if (minInt == null || maxInt == null)
                {
                    AddParseError(row,$"{PieceRateDimensionKeys.ToChinese(dimKey)}档必须为整数区间: {rawValue}");
                    return;
                }
                tier.MinInt = minInt;
                tier.MaxInt = maxInt;
            }
            else
            {
                tier.MinValue = min;
                tier.MaxValue = max;
            }
        }

        var locator = LocatorKey(sectionKey!, procs, prods, stages);
        var existing = ctx.AllCategories.FirstOrDefault(c =>
            string.Equals(c.SectionKey, sectionKey, StringComparison.OrdinalIgnoreCase)
            && SameKeys(procs, ConstraintKeysOf(c, PieceRateConstraintTypes.Process))
            && SameKeys(prods, ConstraintKeysOf(c, PieceRateConstraintTypes.ProductStatus))
            && SameKeys(stages, ConstraintKeysOf(c, PieceRateConstraintTypes.Stage)));

        row.Key = $"{PieceRateDimensionKeys.ToChinese(dimKey) ?? dimKey}｜{displayValue}";
        if (existing == null)
        {
            AddParseError(row,"定位类别不存在，请先导入类别定义");
            return;
        }
        row.IsDuplicate = true;
        row.RowAction = "覆盖";
        row.ActionNote = $"定位命中类别 Id={existing.Id}，将整组替换其维档";
        doc.PendingTierRows.Add(new PendingTierRow
        {
            RowNumber = r,
            Locator = locator,
            SectionKey = sectionKey!,
            Processes = procs,
            ProductStatuses = prods,
            Stages = stages,
            ExistingId = existing.Id,
            Tier = tier
        });
    }

    // ==================== 组级收尾 ====================

    private static void FinalizeCategoryDoc(Doc doc, ParseContext ctx)
    {
        var pendings = doc.Categories;
        if (pendings.Count == 0) return;

        var overwrittenIds = pendings.Where(p => p.ExistingId.HasValue)
            .Select(p => p.ExistingId!.Value).ToHashSet();
        var conflictPool = ctx.AllCategories.Where(c => c.IsActive && !overwrittenIds.Contains(c.Id)).ToList();

        for (var i = 0; i < pendings.Count; i++)
        {
            if (!pendings[i].IsActive) continue;
            var mine = pendings[i].Coverage;
            for (var j = i + 1; j < pendings.Count; j++)
            {
                if (!pendings[j].IsActive) continue;
                if (mine.Intersects(pendings[j].Coverage))
                    AddRowError(doc, pendings[i].RowNumber,
                        $"类别覆盖与文件内行{pendings[j].RowNumber}冲突（禁止交集）: 「{mine.Describe()}」与「{pendings[j].Coverage.Describe()}」");
            }
            foreach (var other in conflictPool)
            {
                var theirs = CoverageOf(other);
                if (mine.Intersects(theirs))
                    AddRowError(doc, pendings[i].RowNumber,
                        $"类别覆盖与既有类别冲突（禁止交集）: 「{mine.Describe()}」与「{theirs.Describe()}」");
            }
        }
    }

    private static void FinalizeTierDoc(Doc doc)
    {
        foreach (var group in doc.PendingTierRows.GroupBy(x => x.Locator))
        {
            var rows = group.ToList();
            var error = ValidateTierGroup(rows.Select(x => x.Tier).ToList());
            if (error != null)
            {
                foreach (var x in rows) AddRowError(doc, x.RowNumber, error);
                continue;
            }
            var first = rows[0];
            doc.TierGroups.Add(new PendingTierGroup
            {
                RowNumber = first.RowNumber,
                ExistingId = first.ExistingId,
                SectionKey = first.SectionKey,
                Processes = first.Processes,
                ProductStatuses = first.ProductStatuses,
                Stages = first.Stages,
                Tiers = rows.Select(x => x.Tier).ToList()
            });
        }
    }

    // ==================== 共享纯逻辑（与 PieceRateProductionCategoryService 同口径） ====================

    private static string[] ConstraintKeysOf(PieceRateProductionCategory entity, string type)
        => entity.ConstraintKeys
            .Where(k => string.Equals(k.ConstraintType, type, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(k.Key))
            .Select(k => k.Key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static CategoryCoverageRule.CategoryCoverage CoverageOf(PieceRateProductionCategory entity)
        => CategoryCoverageRule.Create(entity.SectionKey,
            ToSet(ConstraintKeysOf(entity, PieceRateConstraintTypes.Process)),
            ToSet(ConstraintKeysOf(entity, PieceRateConstraintTypes.ProductStatus)),
            ToSet(ConstraintKeysOf(entity, PieceRateConstraintTypes.Stage)));

    private static HashSet<string> ToSet(IEnumerable<string> keys)
        => new(keys, StringComparer.OrdinalIgnoreCase);

    private static bool SameKeys(string[] a, string[] b)
    {
        var sa = ToSet(a);
        var sb = ToSet(b);
        return sa.SetEquals(sb);
    }

    private static string LocatorKey(string section, string[] procs, string[] prods, string[] stages)
        => section + "\u0001" + string.Join(",", procs) + "\u0002" + string.Join(",", prods) + "\u0003" + string.Join(",", stages);

    /// <summary>键集归一为「成员数组」：空/显式全列 → 空数组（=全选，不插行）；否则去重排序（Ordinal）。</summary>
    private static string[] NormalizeArray(IEnumerable<string>? keys, string[] fullDomain)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (keys != null)
            foreach (var key in keys)
                if (!string.IsNullOrWhiteSpace(key)) set.Add(key.Trim());
        if (set.Count == 0) return [];

        var domain = fullDomain.Where(d => !string.IsNullOrWhiteSpace(d)).ToArray();
        if (domain.Length > 0)
        {
            var domainSet = new HashSet<string>(domain, StringComparer.OrdinalIgnoreCase);
            if (domainSet.Count == set.Count && domainSet.IsSubsetOf(set)) return [];
        }
        return set.OrderBy(k => k, StringComparer.Ordinal).ToArray();
    }

    /// <summary>整组替换某类某 ConstraintType 的成员行（先移除旧同 type，再追加；空数组=移除全部=该维全选）</summary>
    private static void ReplaceKeys(PieceRateProductionCategory entity, string type, IEnumerable<string> keys)
    {
        entity.ConstraintKeys.RemoveAll(k => string.Equals(k.ConstraintType, type, StringComparison.OrdinalIgnoreCase));
        foreach (var key in keys)
            entity.ConstraintKeys.Add(new PieceRateProductionCategoryKey { ConstraintType = type, Key = key });
    }

    private static string? ValidateTierGroup(List<PieceRateProductionCategoryTier> tiers)
    {
        var active = tiers.Where(t => t.IsActive).ToList();
        foreach (var dimGroup in active.GroupBy(t => t.DimensionKey))
        {
            var dimRows = dimGroup.ToList();
            var cn = PieceRateDimensionKeys.ToChinese(dimGroup.Key);

            if (PieceRateDimensionKeys.IsValueDimension(dimGroup.Key))
            {
                var dup = PieceRateDimensionRules.FirstDuplicateOrdinalIgnoreCase(dimRows.Select(t => t.MatchValue));
                if (dup != null) return $"{cn}档取值重复: {dup}";
                continue;
            }

            for (var i = 0; i < dimRows.Count; i++)
            {
                for (var j = i + 1; j < dimRows.Count; j++)
                {
                    var a = dimRows[i];
                    var b = dimRows[j];
                    var overlap = dimGroup.Key == PieceRateDimensionKeys.FixedLengthCount
                        ? PieceRateDimensionRules.RangesOverlapInt(a.MinInt, a.MaxInt, b.MinInt, b.MaxInt)
                        : PieceRateDimensionRules.RangesOverlap(a.MinValue, a.MaxValue, b.MinValue, b.MaxValue);
                    if (overlap)
                        return $"{cn}档区间重叠: 「{a.RangeText}」与「{b.RangeText}」";
                }
            }
        }
        return null;
    }

    private static int? ToIntBound(decimal? value)
        => value is { } v && v == Math.Floor(v) && v >= int.MinValue && v <= int.MaxValue ? (int)v : null;

    private static int PieceRateDimensionIndex(string dimKey)
    {
        var idx = Array.IndexOf(PieceRateDimensionKeys.All, dimKey);
        return idx < 0 ? int.MaxValue : idx;
    }

    // ---------------- 解析上下文与值解析 ----------------

    private sealed class ParseContext
    {
        public List<PieceRateProductionCategory> AllCategories = new();
        public string[] ProcessDomain = [];
        public Dictionary<string, string> SectionNameToKey = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ProcessNameToKey = new(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<ParseContext> BuildParseContextAsync()
    {
        var ctx = new ParseContext();
        ctx.AllCategories = await _context.PieceRateProductionCategories.AsNoTracking()
            .Include(c => c.ConstraintKeys)
            .ToListAsync();

        ctx.ProcessDomain = (await _context.ProcessDefinitions.AsNoTracking()
                .Where(w => !string.IsNullOrEmpty(w.ProcessKey))
                .Select(w => w.ProcessKey!)
                .ToListAsync())
            .Concat(ProcessKeys.All)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        ctx.SectionNameToKey = ReverseMap(await _sectionNameDisplay.GetSectionNameMapAsync());
        ctx.ProcessNameToKey = ReverseMap(await _processDefinitionService.GetProcessNameMapAsync());
        return ctx;
    }

    private static Dictionary<string, string> ReverseMap(IReadOnlyDictionary<string, string> keyToName)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in keyToName)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value)) continue;
            map.TryAdd(kvp.Value, kvp.Key);
        }
        return map;
    }

    private static string? ResolveSection(string? text, ParseContext ctx)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var key = SectionKeys.ToKey(text.Trim());
        if (key != null) return key;
        return ctx.SectionNameToKey.TryGetValue(text.Trim(), out var k) ? k : null;
    }

    private static void ResolveProcessKeys(string? cell, ParseContext ctx, out (string[] Keys, string? Error) result)
    {
        var keys = new List<string>();
        foreach (var token in SplitTokens(cell))
        {
            var key = ProcessKeys.ToKey(token)
                      ?? (ctx.ProcessNameToKey.TryGetValue(token, out var k) ? k : token);
            if (!ctx.ProcessDomain.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                result = (keys.ToArray(), $"无效的工序: {token}");
                return;
            }
            keys.Add(key);
        }
        result = (keys.ToArray(), null);
    }

    private static void ResolveFixedKeys(string? cell, Func<string?, string?> toKey, string chineseName,
        out (string[] Keys, string? Error) result)
    {
        var keys = new List<string>();
        foreach (var token in SplitTokens(cell))
        {
            var key = toKey(token);
            if (key == null)
            {
                result = (keys.ToArray(), $"无效的{chineseName}: {token}");
                return;
            }
            keys.Add(key);
        }
        result = (keys.ToArray(), null);
    }

    private static string? ResolveUnit(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        if (PieceRateUnitKeys.IsKey(t)) return t;
        foreach (var kvp in PieceRateUnitKeys.KeyToChinese)
            if (string.Equals(kvp.Value, t, StringComparison.OrdinalIgnoreCase)) return kvp.Key;
        return null;
    }

    private static string? ResolveDimension(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        if (PieceRateDimensionKeys.IsKey(t)) return t;
        foreach (var kvp in PieceRateDimensionKeys.KeyToChinese)
            if (string.Equals(kvp.Value, t, StringComparison.OrdinalIgnoreCase)) return kvp.Key;
        return null;
    }

    private static IEnumerable<string> SplitTokens(string? cell)
    {
        if (string.IsNullOrWhiteSpace(cell)) yield break;
        foreach (var part in cell.Split(['、', '，', ',', ';', '；', '/', '|', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            var t = part.Trim();
            if (t.Length > 0) yield return t;
        }
    }

    private static bool ParseBool(string? text, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(text)) return defaultValue;
        return text.Trim() is "是" or "启用" or "true" or "1" ? true
            : text.Trim() is "否" or "停用" or "false" or "0" ? false
            : defaultValue;
    }

    private static string? CellText(ExcelWorksheet ws, int r, Dictionary<string, int> colIndex, string field)
        => colIndex.TryGetValue(field, out var c) ? ws.Cells[r, c].Text?.Trim() : null;

    private static bool TryCellDecimal(ExcelWorksheet ws, int r, Dictionary<string, int> colIndex,
        string field, out decimal value)
    {
        value = 0;
        if (!colIndex.TryGetValue(field, out var c)) return false;
        var cell = ws.Cells[r, c];
        switch (cell.Value)
        {
            case decimal de: value = de; return true;
            case int i: value = i; return true;
            case long l: value = l; return true;
            case double db when !double.IsNaN(db) && !double.IsInfinity(db): value = (decimal)db; return true;
            case float f when !float.IsNaN(f) && !float.IsInfinity(f): value = (decimal)f; return true;
        }
        var text = cell.Text?.Trim()?.Replace(",", "");
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value)) return true;
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }

    // ==================== 导出写入 ====================

    private static void WriteHeaders(ExcelWorksheet ws, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cells[1, i + 1].Value = headers[i];
            ws.Cells[1, i + 1].Style.Font.Bold = true;
        }
    }

    private static void AutoFit(ExcelWorksheet ws, int columns)
    {
        for (var i = 1; i <= columns; i++) ws.Column(i).AutoFit();
    }

    private static void WriteCategoryRow(ExcelWorksheet ws, int r, PieceRateProductionCategory c,
        string[] procs, string[] prods, string[] stages,
        IReadOnlyDictionary<string, string> sectionMap, IReadOnlyDictionary<string, string> processMap)
    {
        ws.Cells[r, 1].Value = SectionText(c.SectionKey, sectionMap);
        ws.Cells[r, 2].Value = JoinChinese(procs, k => processMap.TryGetValue(k, out var n) ? n : k);
        ws.Cells[r, 3].Value = JoinChinese(prods, k => ProductStatuses.ToChinese(k) ?? k);
        ws.Cells[r, 4].Value = JoinChinese(stages, k => PieceRateStageKeys.ToChinese(k) ?? k);
        ws.Cells[r, 5].Value = c.BasePrice;
        ws.Cells[r, 6].Value = PieceRateUnitKeys.ToChinese(c.Unit) ?? c.Unit;
        ws.Cells[r, 7].Value = c.IsActive ? "是" : "否";
        ws.Cells[r, 8].Value = c.Remark ?? "";
    }

    private static void WriteTierRow(ExcelWorksheet ws, int r, PieceRateProductionCategory c,
        string[] procs, string[] prods, string[] stages, PieceRateProductionCategoryTier t,
        IReadOnlyDictionary<string, string> sectionMap, IReadOnlyDictionary<string, string> processMap)
    {
        ws.Cells[r, 1].Value = SectionText(c.SectionKey, sectionMap);
        ws.Cells[r, 2].Value = JoinChinese(procs, k => processMap.TryGetValue(k, out var n) ? n : k);
        ws.Cells[r, 3].Value = JoinChinese(prods, k => ProductStatuses.ToChinese(k) ?? k);
        ws.Cells[r, 4].Value = JoinChinese(stages, k => PieceRateStageKeys.ToChinese(k) ?? k);
        ws.Cells[r, 5].Value = PieceRateDimensionKeys.ToChinese(t.DimensionKey) ?? t.DimensionKey;
        ws.Cells[r, 6].Value = TierValueText(t);
        ws.Cells[r, 7].Value = t.Ratio;
        ws.Cells[r, 8].Value = t.IsActive ? "是" : "否";
    }

    private static string SectionText(string sectionKey, IReadOnlyDictionary<string, string> sectionMap)
        => sectionMap.TryGetValue(sectionKey, out var cn) ? cn : (SectionKeys.ToChinese(sectionKey) ?? sectionKey);

    private static string TierValueText(PieceRateProductionCategoryTier t)
    {
        if (!string.IsNullOrWhiteSpace(t.RangeText)) return t.RangeText;
        if (t.DimensionKey == PieceRateDimensionKeys.SpecialState)
            return PieceRateStateKeys.ToChinese(t.MatchValue) ?? t.MatchValue ?? "";
        return t.MatchValue ?? "";
    }

    private static string? JoinChinese(string[] keys, Func<string, string> toChinese)
        => keys.Length == 0 ? null : string.Join("、", keys.Select(toChinese));

    private static bool IsTier(string kind)
        => string.Equals(kind, PieceRateImportKinds.Tier, StringComparison.OrdinalIgnoreCase);

    // ==================== 解析结果结构 ====================

    private sealed class RowInfo
    {
        public int RowNumber;
        public string Key = "";
        public List<string> Errors = new();
        public bool IsDuplicate;
        public bool IsValid = true;
        public string RowAction = "新增";
        public string? ActionNote;
    }

    private sealed class PendingCategory
    {
        public int RowNumber;
        public string SectionKey = "";
        public string[] Processes = [];
        public string[] ProductStatuses = [];
        public string[] Stages = [];
        public decimal BasePrice;
        public string Unit = "";
        public bool IsActive = true;
        public string? Remark;
        public int? ExistingId;

        public CategoryCoverageRule.CategoryCoverage Coverage
            => CategoryCoverageRule.Create(SectionKey, ToSet(Processes), ToSet(ProductStatuses), ToSet(Stages));
    }

    private sealed class PendingTierRow
    {
        public int RowNumber;
        public string Locator = "";
        public string SectionKey = "";
        public string[] Processes = [];
        public string[] ProductStatuses = [];
        public string[] Stages = [];
        public int ExistingId;
        public PieceRateProductionCategoryTier Tier = null!;
    }

    private sealed class PendingTierGroup
    {
        public int RowNumber;
        public string SectionKey = "";
        public string[] Processes = [];
        public string[] ProductStatuses = [];
        public string[] Stages = [];
        public int ExistingId;
        public List<PieceRateProductionCategoryTier> Tiers = new();
    }

    private sealed class Doc
    {
        public int TotalRows;
        public string? Fatal;
        public List<RowInfo> Rows = new();
        public List<PendingCategory> Categories = new();
        public List<PendingTierGroup> TierGroups = new();
        public List<PendingTierRow> PendingTierRows = new();
        public bool HasErrors;
        public int ErrorCount;
        private readonly HashSet<string> _seenLocators = new(StringComparer.Ordinal);

        public bool ClaimLocator(string locator) => _seenLocators.Add(locator);

        public void Add(PendingCategory pending) => Categories.Add(pending);

        public void Recount()
        {
            ErrorCount = Rows.Count(r => !r.IsValid);
            HasErrors = ErrorCount > 0;
        }

        public ImportPreviewResult ToPreviewResult()
        {
            var preview = new ImportPreviewResult
            {
                TotalRows = TotalRows,
                ValidCount = Rows.Count(r => r.IsValid),
                ErrorCount = ErrorCount,
                DuplicateCount = Rows.Count(r => r.IsDuplicate),
                AddCount = Rows.Count(r => r.IsValid && r.RowAction == "新增"),
                OverwriteCount = Rows.Count(r => r.IsValid && r.RowAction == "覆盖")
            };
            preview.RowResults = Rows.Select(r => new ImportRowResult
            {
                RowNumber = r.RowNumber,
                Key = r.Key,
                Errors = new List<string>(r.Errors),
                IsDuplicate = r.IsDuplicate,
                IsValid = r.IsValid,
                RowAction = r.RowAction,
                ActionNote = r.ActionNote
            }).ToList();
            return preview;
        }
    }

    private static void AddRowError(Doc doc, int rowNumber, string message)
    {
        var row = doc.Rows.FirstOrDefault(x => x.RowNumber == rowNumber);
        if (row == null || !row.IsValid) return;
        AddParseError(row, message);
        row.RowAction = "错误";
    }

    /// <summary>记一行解析错误并同步置无效（守卫与统计都以 IsValid 为准）</summary>
    private static void AddParseError(RowInfo row, string message)
    {
        row.Errors.Add(message);
        row.IsValid = false;
    }
}
