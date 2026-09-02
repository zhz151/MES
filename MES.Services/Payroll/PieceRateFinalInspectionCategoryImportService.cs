using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Core.Interfaces.Payroll;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities.Payroll;

namespace MES.Services.Payroll;

/// <summary>
/// 成检计件类别专用批量导入/导出（2026-09-03，工资结算数据维护闭环），对标生产计件导入器（PieceRateCategoryImportService）。
/// 与生产的差异：无「工段 × 工序/产类/作业阶段」四键定位，定位键 = 成检项目（InspectionItem）单键；
/// 维度域 = PieceRateInspectionDimensionKeys（区间维 外径/壁厚/长度/检验支数[整] + 等值维 长度状态/特殊牌号/特殊制造状态/特殊设备号）。
/// 覆盖更新语义与生产一致：类别模板只动主属性不动档行、维档模板按定位整组替换 Tiers、任一行非法整批拒绝。
/// </summary>
public class PieceRateFinalInspectionCategoryImportService : IPieceRateFinalInspectionCategoryImportService
{
    private readonly AppDbContext _context;

    public PieceRateFinalInspectionCategoryImportService(AppDbContext context)
    {
        _context = context;
    }

    // ==================== 表头常量 ====================

    /// <summary>类别定义 sheet/表头（顺序即模板列序）</summary>
    private static readonly string[] CategoryHeaders = ["成检项目", "基准价", "结算单位", "启用", "备注"];

    /// <summary>维档系数 sheet/表头</summary>
    private static readonly string[] TierHeaders = ["成检项目", "维度", "档值", "系数", "启用"];

    private const string CategorySheet = "类别";
    private const string TierSheet = "维档";

    /// <summary>类别列头 → 字段（按表头名匹配，列序无关）</summary>
    private static readonly Dictionary<string, string> CategoryFieldMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["成检项目"] = "Item", ["基准价"] = "BasePrice", ["结算单位"] = "Unit", ["启用"] = "IsActive", ["备注"] = "Remark"
    };

    /// <summary>维档列头 → 字段</summary>
    private static readonly Dictionary<string, string> TierFieldMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["成检项目"] = "Item", ["维度"] = "Dimension", ["档值"] = "Value", ["系数"] = "Ratio", ["启用"] = "IsActive"
    };

    // ==================== 导出（双 sheet：类别 + 维档） ====================

    public async Task<byte[]> ExportAsync()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var catWs = package.Workbook.Worksheets.Add(CategorySheet);
        var tierWs = package.Workbook.Worksheets.Add(TierSheet);

        var entities = await _context.PieceRateFinalInspectionCategories.AsNoTracking()
            .Include(c => c.Tiers)
            .OrderBy(c => c.ItemKey).ThenBy(c => c.Id)
            .ToListAsync();

        WriteHeaders(catWs, CategoryHeaders);
        WriteHeaders(tierWs, TierHeaders);

        var catRow = 2;
        var tierRow = 2;
        foreach (var c in entities)
        {
            WriteCategoryRow(catWs, catRow++, c);
            foreach (var t in c.Tiers.OrderBy(t => FinalInspectionDimIndex(t.DimensionKey)).ThenBy(t => t.Id))
                WriteTierRow(tierWs, tierRow++, c, t);
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

        // 示例成检项目：优先「当前无类别」的项目，避免原样导入模板误覆盖真实类别；兜底 InspectionItem 首项。
        var itemKey = await PickExampleItemAsync();

        var headers = isTier ? TierHeaders : CategoryHeaders;
        var sheet = isTier ? TierSheet : CategorySheet;
        var ws = package.Workbook.Worksheets.Add(sheet);
        WriteHeaders(ws, headers);
        ws.Cells[2, 1].Value = ItemChinese(itemKey);
        if (isTier)
        {
            ws.Cells[2, 2].Value = PieceRateInspectionDimensionKeys.ToChinese(PieceRateInspectionDimensionKeys.OuterDiameter);
            ws.Cells[2, 3].Value = ">54";        // 档值（区间原文）
            ws.Cells[2, 4].Value = 1.1M;         // 系数
            ws.Cells[2, 5].Value = "是";         // 启用
        }
        else
        {
            ws.Cells[2, 2].Value = 40M;          // 基准价
            ws.Cells[2, 3].Value = "元/吨";       // 结算单位
            ws.Cells[2, 4].Value = "否";         // 启用（示例停用防误导入计价）
            ws.Cells[2, 5].Value = "示例行（导入前请删除本行）";
        }
        AutoFit(ws, headers.Length);
        return await package.GetAsByteArrayAsync();
    }

    /// <summary>示例成检项目：优先「当前无类别」的 InspectionItem；兜底枚举首项。</summary>
    private async Task<string> PickExampleItemAsync()
    {
        var used = (await _context.PieceRateFinalInspectionCategories.AsNoTracking()
                .Select(c => c.ItemKey).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keys = Enum.GetNames<InspectionItem>();
        foreach (var key in keys)
            if (!used.Contains(key)) return key;
        return keys[0];
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
                var entity = await _context.PieceRateFinalInspectionCategories
                    .FirstAsync(c => c.Id == p.ExistingId.Value);
                entity.BasePrice = p.BasePrice;
                entity.Unit = p.Unit;
                entity.IsActive = p.IsActive;
                entity.Remark = p.Remark;
            }
            else
            {
                var entity = new PieceRateFinalInspectionCategory
                {
                    ItemKey = p.ItemKey,
                    BasePrice = p.BasePrice,
                    Unit = p.Unit,
                    IsActive = p.IsActive,
                    Remark = p.Remark
                };
                _context.PieceRateFinalInspectionCategories.Add(entity);
            }
        }
    }

    private async Task ApplyTierGroupsAsync(Doc doc)
    {
        foreach (var g in doc.TierGroups)
        {
            var entity = await _context.PieceRateFinalInspectionCategories
                .Include(c => c.Tiers)
                .FirstAsync(c => c.Id == g.ExistingId);
            foreach (var old in entity.Tiers.ToList())
                _context.PieceRateFinalInspectionCategoryTiers.Remove(old);
            entity.Tiers.Clear();
            foreach (var t in g.Tiers)
            {
                entity.Tiers.Add(new PieceRateFinalInspectionCategoryTier
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
            var allCategories = await LoadAllCategoriesAsync();
            var isTier = IsTier(kind);

            for (var r = 2; r <= rowCount; r++)
            {
                if (IsBlankRow(ws, r, colIndex)) continue; // 空行跳过（不计数/不报错）
                var row = new RowInfo { RowNumber = r };
                if (isTier) ParseTierRow(row, ws, r, colIndex, allCategories, doc);
                else ParseCategoryRow(row, ws, r, colIndex, allCategories, doc);
                doc.Rows.Add(row);
            }
            doc.TotalRows = doc.Rows.Count;
            if (doc.TotalRows == 0)
            {
                doc.Fatal = "Excel 文件没有数据行";
                return doc;
            }

            if (isTier) FinalizeTierDoc(doc);
            else FinalizeCategoryDoc(doc, allCategories);

            doc.Recount();
            return doc;
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException)
        {
            doc.Fatal = $"无法读取 Excel 文件: {ex.Message}";
            return doc;
        }
    }

    private async Task<List<PieceRateFinalInspectionCategory>> LoadAllCategoriesAsync()
        => await _context.PieceRateFinalInspectionCategories.AsNoTracking()
            .Include(c => c.Tiers)
            .ToListAsync();

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
        Dictionary<string, int> colIndex, List<PieceRateFinalInspectionCategory> allCategories, Doc doc)
    {
        var itemText = CellText(ws, r, colIndex, "Item");
        var itemKey = NormalizeItemKey(itemText);
        if (string.IsNullOrWhiteSpace(itemText)) AddParseError(row, "成检项目不能为空");
        else if (itemKey == null) AddParseError(row, $"无效的成检项目: {itemText}");

        if (!TryCellDecimal(ws, r, colIndex, "BasePrice", out var basePrice)) AddParseError(row, "基准价必须为数字");
        else if (basePrice <= 0) AddParseError(row, "基准价必须大于0");

        var unitText = CellText(ws, r, colIndex, "Unit");
        var unit = ResolveUnit(unitText);
        if (string.IsNullOrWhiteSpace(unitText)) AddParseError(row, "结算单位不能为空");
        else if (unit == null) AddParseError(row, $"无效的结算单位: {unitText}");

        var isActive = ParseBool(CellText(ws, r, colIndex, "IsActive"), true);
        var remark = string.IsNullOrWhiteSpace(CellText(ws, r, colIndex, "Remark"))
            ? null : CellText(ws, r, colIndex, "Remark")!.Trim();

        if (!row.IsValid) return;

        if (!doc.ClaimLocator(itemKey!))
        {
            AddParseError(row, "文件内存在重复定位成检项目，请合并或删除");
            return;
        }

        // 定位命中 = 同成检项目既有类别（启用优先），保持「同项目一条启用类别」的 id 稳定
        var existing = allCategories
            .Where(c => string.Equals(c.ItemKey, itemKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.IsActive)
            .ThenBy(c => c.Id)
            .FirstOrDefault();

        var pending = new PendingCategory
        {
            RowNumber = r,
            ItemKey = itemKey!,
            BasePrice = basePrice,
            Unit = unit!,
            IsActive = isActive,
            Remark = remark,
            ExistingId = existing?.Id
        };
        doc.Categories.Add(pending);
        row.Key = ItemChinese(itemKey!);
        row.IsDuplicate = existing != null;
        row.RowAction = existing != null ? "覆盖" : "新增";
        row.ActionNote = existing != null
            ? $"定位命中类别 Id={existing.Id}，整组更新主属性（不动档行）"
            : "未命中既有类别，将新建（无档行）";
    }

    // ---------------- 维档行解析 ----------------

    private void ParseTierRow(RowInfo row, ExcelWorksheet ws, int r,
        Dictionary<string, int> colIndex, List<PieceRateFinalInspectionCategory> allCategories, Doc doc)
    {
        var itemText = CellText(ws, r, colIndex, "Item");
        var itemKey = NormalizeItemKey(itemText);
        if (string.IsNullOrWhiteSpace(itemText)) AddParseError(row, "成检项目不能为空");
        else if (itemKey == null) AddParseError(row, $"无效的成检项目: {itemText}");

        var dimText = CellText(ws, r, colIndex, "Dimension");
        var dimKey = ResolveDimension(dimText);
        if (string.IsNullOrWhiteSpace(dimText)) AddParseError(row, "维度不能为空");
        else if (dimKey == null) AddParseError(row, $"无效的维度: {dimText}");

        var valueText = CellText(ws, r, colIndex, "Value");
        if (string.IsNullOrWhiteSpace(valueText)) AddParseError(row, $"档值不能为空（{dimText}）");

        if (!TryCellDecimal(ws, r, colIndex, "Ratio", out var ratio)) AddParseError(row, "系数必须为数字");
        else if (ratio <= 0) AddParseError(row, "系数必须大于0");
        var isActive = ParseBool(CellText(ws, r, colIndex, "IsActive"), true);

        if (!row.IsValid) return;

        // 建档行（区间维解析边界；等值维取值归一英文 Key——长度状态/特殊制造状态中文容忍）
        var tier = new PieceRateFinalInspectionCategoryTier { DimensionKey = dimKey!, Ratio = ratio, IsActive = isActive };
        string? displayValue;
        var rawValue = valueText!.Trim();
        if (PieceRateInspectionDimensionKeys.IsValueDimension(dimKey))
        {
            string? match = dimKey switch
            {
                PieceRateInspectionDimensionKeys.LengthStatus => NormalizeLengthStatus(rawValue),
                PieceRateInspectionDimensionKeys.SpecialState => PieceRateStateKeys.ToKey(rawValue),
                _ => rawValue
            };
            if (string.IsNullOrEmpty(match))
            {
                AddParseError(row, $"无法识别该等值取值: {rawValue}");
                return;
            }
            displayValue = dimKey switch
            {
                PieceRateInspectionDimensionKeys.LengthStatus => EnumHelper.GetDisplayName<LengthStatus>(match) ?? match,
                PieceRateInspectionDimensionKeys.SpecialState => PieceRateStateKeys.ToChinese(match) ?? match,
                _ => rawValue
            };
            tier.MatchValue = match;
            tier.RangeText = match;
        }
        else
        {
            if (!PieceRateRangeParser.TryParseRange(rawValue, out var min, out var max))
            {
                AddParseError(row, $"{PieceRateInspectionDimensionKeys.ToChinese(dimKey)}档必须填写可解析的区间: {rawValue}");
                return;
            }
            displayValue = rawValue;
            tier.RangeText = rawValue;
            if (dimKey == PieceRateInspectionDimensionKeys.InspectionCount)
            {
                var minInt = ToIntBound(min);
                var maxInt = ToIntBound(max);
                if (minInt == null || maxInt == null)
                {
                    AddParseError(row, $"{PieceRateInspectionDimensionKeys.ToChinese(dimKey)}档必须为整数区间: {rawValue}");
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

        // 定位类别（启用优先，规则同类别模板；不存在则整行报错引导先导类别）
        var existing = allCategories
            .Where(c => string.Equals(c.ItemKey, itemKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.IsActive)
            .ThenBy(c => c.Id)
            .FirstOrDefault();

        row.Key = $"{PieceRateInspectionDimensionKeys.ToChinese(dimKey) ?? dimKey}｜{displayValue}";
        if (existing == null)
        {
            AddParseError(row, "定位类别不存在，请先导入类别定义");
            return;
        }
        row.IsDuplicate = true;
        row.RowAction = "覆盖";
        row.ActionNote = $"定位命中类别 Id={existing.Id}，将整组替换其维档";
        doc.PendingTierRows.Add(new PendingTierRow
        {
            RowNumber = r,
            Locator = itemKey!,
            ExistingId = existing.Id,
            Tier = tier
        });
    }

    // ==================== 组级收尾 ====================

    private static void FinalizeCategoryDoc(Doc doc, List<PieceRateFinalInspectionCategory> allCategories)
    {
        var pendings = doc.Categories;
        if (pendings.Count == 0) return;

        var overwrittenIds = pendings.Where(p => p.ExistingId.HasValue)
            .Select(p => p.ExistingId!.Value).ToHashSet();
        // 同成检项目启用唯一：活跃候选不可与「未被本文件覆盖的其它启用类别」同项目并存
        var conflictPool = allCategories
            .Where(c => c.IsActive && !overwrittenIds.Contains(c.Id))
            .ToList();

        foreach (var p in pendings)
        {
            if (!p.IsActive) continue;
            var other = conflictPool.FirstOrDefault(c =>
                string.Equals(c.ItemKey, p.ItemKey, StringComparison.OrdinalIgnoreCase));
            if (other != null)
                AddRowError(doc, p.RowNumber,
                    $"成检项目「{ItemChinese(p.ItemKey)}」已有启用类别 Id={other.Id}（同项目启用唯一），请改为停用既有类别或编辑覆盖");
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
                Tiers = rows.Select(x => x.Tier).ToList()
            });
        }
    }

    // ==================== 校验纯逻辑（与 PieceRateFinalInspectionCategoryService 同口径） ====================

    /// <summary>成检项目中文或英文 → 枚举名；非法返回 null。</summary>
    private static string? NormalizeItemKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return EnumHelper.TryParse<InspectionItem>(raw.Trim())?.ToString();
    }

    private static string? NormalizeLengthStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return EnumHelper.TryParse<LengthStatus>(raw.Trim())?.ToString();
    }

    private static string ItemChinese(string itemKey)
        => EnumHelper.GetDisplayName<InspectionItem>(itemKey) ?? itemKey;

    private static string? ValidateTierGroup(List<PieceRateFinalInspectionCategoryTier> tiers)
    {
        var active = tiers.Where(t => t.IsActive).ToList();
        foreach (var dimGroup in active.GroupBy(t => t.DimensionKey))
        {
            var dimRows = dimGroup.ToList();
            var cn = PieceRateInspectionDimensionKeys.ToChinese(dimGroup.Key);

            if (PieceRateInspectionDimensionKeys.IsValueDimension(dimGroup.Key))
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
                    var overlap = dimGroup.Key == PieceRateInspectionDimensionKeys.InspectionCount
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

    private static int FinalInspectionDimIndex(string dimKey)
    {
        var idx = Array.IndexOf(PieceRateInspectionDimensionKeys.All, dimKey);
        return idx < 0 ? int.MaxValue : idx;
    }

    // ---------------- 值解析 ----------------

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
        if (PieceRateInspectionDimensionKeys.IsKey(t)) return t;
        foreach (var kvp in PieceRateInspectionDimensionKeys.KeyToChinese)
            if (string.Equals(kvp.Value, t, StringComparison.OrdinalIgnoreCase)) return kvp.Key;
        return null;
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

    private static void WriteCategoryRow(ExcelWorksheet ws, int r, PieceRateFinalInspectionCategory c)
    {
        ws.Cells[r, 1].Value = ItemChinese(c.ItemKey);
        ws.Cells[r, 2].Value = c.BasePrice;
        ws.Cells[r, 3].Value = PieceRateUnitKeys.ToChinese(c.Unit) ?? c.Unit;
        ws.Cells[r, 4].Value = c.IsActive ? "是" : "否";
        ws.Cells[r, 5].Value = c.Remark ?? "";
    }

    private static void WriteTierRow(ExcelWorksheet ws, int r, PieceRateFinalInspectionCategory c,
        PieceRateFinalInspectionCategoryTier t)
    {
        ws.Cells[r, 1].Value = ItemChinese(c.ItemKey);
        ws.Cells[r, 2].Value = PieceRateInspectionDimensionKeys.ToChinese(t.DimensionKey) ?? t.DimensionKey;
        ws.Cells[r, 3].Value = TierValueText(t);
        ws.Cells[r, 4].Value = t.Ratio;
        ws.Cells[r, 5].Value = t.IsActive ? "是" : "否";
    }

    private static string TierValueText(PieceRateFinalInspectionCategoryTier t)
    {
        if (!string.IsNullOrWhiteSpace(t.RangeText))
        {
            // 等值维 RangeText = MatchValue 冗余副本；长度状态/特殊状态存 Key，导出应回显中文
            if (t.DimensionKey == PieceRateInspectionDimensionKeys.LengthStatus)
                return EnumHelper.GetDisplayName<LengthStatus>(t.RangeText) ?? t.RangeText;
            if (t.DimensionKey == PieceRateInspectionDimensionKeys.SpecialState)
                return PieceRateStateKeys.ToChinese(t.RangeText) ?? t.RangeText;
            return t.RangeText;
        }
        return t.MatchValue ?? "";
    }

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
        public string ItemKey = "";
        public decimal BasePrice;
        public string Unit = "";
        public bool IsActive = true;
        public string? Remark;
        public int? ExistingId;
    }

    private sealed class PendingTierRow
    {
        public int RowNumber;
        public string Locator = "";
        public int ExistingId;
        public PieceRateFinalInspectionCategoryTier Tier = null!;
    }

    private sealed class PendingTierGroup
    {
        public int RowNumber;
        public int ExistingId;
        public List<PieceRateFinalInspectionCategoryTier> Tiers = new();
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
        private readonly HashSet<string> _seenLocators = new(StringComparer.OrdinalIgnoreCase);

        public bool ClaimLocator(string locator) => _seenLocators.Add(locator);

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
