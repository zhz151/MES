using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using MES.Core.Constants;
using MES.Core.Exceptions;
using MES.Core.Interfaces.Configuration;
using MES.Core.Interfaces.DataExchange;
using MES.Data;
using MES.Data.Entities.Order;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Batch;
using MES.Services.Helpers;
using MES.Core.Helpers;

namespace MES.Services.DataExchange;

/// <summary>
/// 数据导出服务
/// </summary>
public class DataExportService : IDataExportService
{
    protected readonly AppDbContext _context;
    private readonly ILogger<DataExportService> _logger;
    private readonly ISectionNameDisplayService _sectionNameDisplay;

    public DataExportService(AppDbContext context, ILogger<DataExportService> logger,
        ISectionNameDisplayService sectionNameDisplay)
    {
        _context = context;
        _logger = logger;
        _sectionNameDisplay = sectionNameDisplay;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    #region 导出

    /// <summary>
    /// 导出指定实体的全部数据为 Excel
    /// </summary>
    public async Task<byte[]> ExportAsync(string entityKey)
    {
        if (!DataExchangeRegistry.Registry.TryGetValue(entityKey, out var def))
            throw new BusinessException($"不支持的实体类型: {entityKey}");

        var data = await QueryAllAsync(def.Type);
        var propertyCache = BuildPropertyCache(def);

        // 构建FK反向缓存（用于导出时解析外键列的显示值）
        var fkReverseCache = await BuildFkReverseCacheForExportAsync(def);
        // 特殊缓存：OrderItem 复合键（用于 ProductRequirement 导出）
        var orderItemExportCache = await BuildOrderItemExportCacheAsync(def);

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add(def.DisplayName);

        // 表头
        for (int i = 0; i < def.Columns.Count; i++)
            sheet.Cells[1, i + 1].Value = def.Columns[i].Header;
        sheet.Cells[1, 1, 1, def.Columns.Count].Style.Font.Bold = true;

        // 数据行
        var row = 2;
        foreach (var item in data)
        {
            for (int col = 0; col < def.Columns.Count; col++)
            {
                var colDef = def.Columns[col];

                // FK列：解析引用实体的业务主键值
                if (colDef.IsFkColumn)
                {
                    var fkValue = await ResolveFkExportValue(colDef, item, propertyCache, fkReverseCache, orderItemExportCache);
                    if (fkValue != null)
                        sheet.Cells[row, col + 1].Value = fkValue;
                    continue;
                }

                if (colDef.Property == null || !propertyCache.TryGetValue(colDef.Property, out var prop))
                    continue;

                var value = prop.GetValue(item);
                if (value == null)
                    continue;

                // 特殊处理：WorkOrder.OrderItemIds → 解析为"订单号|项次号"格式
                if (colDef.Property == "OrderItemIds" && value is string idsStr)
                {
                    sheet.Cells[row, col + 1].Value = await ResolveOrderItemIdsForExportAsync(idsStr);
                    continue;
                }

                if (colDef.IsEnum && colDef.EnumType != null)
                {
                    // value 可能是 string（字符串存储的枚举）或实际枚举值
                    if (value is string strValue)
                    {
                        try
                        {
                            var parsedEnum = EnumHelper.Parse(strValue, colDef.EnumType);
                            sheet.Cells[row, col + 1].Value = EnumHelper.GetDisplayName(colDef.EnumType, parsedEnum);
                        }
                        catch
                        {
                            sheet.Cells[row, col + 1].Value = strValue;
                        }
                    }
                    else
                    {
                        sheet.Cells[row, col + 1].Value = EnumHelper.GetDisplayName(colDef.EnumType, value);
                    }
                }
                else if (value is DateTime dt)
                {
                    sheet.Cells[row, col + 1].Value = dt.ToString("yyyy-MM-dd");
                }
                else if (value is DateTimeOffset dto)
                {
                    sheet.Cells[row, col + 1].Value = dto.ToString("yyyy-MM-dd HH:mm");
                }
                else if (value is bool b)
                {
                    sheet.Cells[row, col + 1].Value = b ? "是" : "否";
                }
                else if (value is decimal dec)
                {
                    sheet.Cells[row, col + 1].Value = dec.ToString("G29");
                }
                else if (colDef.Property == "SectionName" && value is string sectionName)
                {
                    // SectionName 存储为英文 Key，导出显示中文
                    sheet.Cells[row, col + 1].Value = await _sectionNameDisplay.ToDisplayAsync(sectionName);
                }
                else
                {
                    sheet.Cells[row, col + 1].Value = value.ToString();
                }
            }
            row++;
        }

        sheet.Cells[1, 1, row - 1, def.Columns.Count].AutoFitColumns();
        return await package.GetAsByteArrayAsync();
    }

    #endregion

    #region 模板

    /// <summary>
    /// 生成导入模板（含1行示例数据）
    /// </summary>
    public async Task<byte[]> GenerateTemplateAsync(string entityKey)
    {
        if (!DataExchangeRegistry.Registry.TryGetValue(entityKey, out var def))
            throw new BusinessException($"不支持的实体类型: {entityKey}");

        // 模板含主键 ID 列（与"下载数据"列完全一致）：ID 留空即新增，填写 ID 则按 ID 覆盖
        var importColumns = def.Columns;

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add(def.DisplayName);

        // 表头
        for (int i = 0; i < importColumns.Count; i++)
            sheet.Cells[1, i + 1].Value = importColumns[i].Header;
        sheet.Cells[1, 1, 1, importColumns.Count].Style.Font.Bold = true;

        // 系统字段标记为灰色底色
        for (int i = 0; i < importColumns.Count; i++)
        {
            if (importColumns[i].IsSystem)
                sheet.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        }

        // 示例数据行（尽量提供示例值）
        var sampleRow = 2;
        var fkReverseCache = await BuildFkReverseCacheForExportAsync(def);
        var orderItemExportCache = await BuildOrderItemExportCacheAsync(def);
        foreach (var colDef in importColumns)
        {
            if (colDef.IsSystem) continue;
            if (colDef.EnumType != null)
            {
                if (!colDef.EnumType.IsEnum)
                    throw new BusinessException($"列 '{colDef.Header}' 的枚举类型 '{colDef.EnumType.FullName}' 无效");
                var values = Enum.GetValues(colDef.EnumType);
                if (values.Length > 0)
                    sheet.Cells[sampleRow, importColumns.IndexOf(colDef) + 1].Value = EnumHelper.GetDisplayName(colDef.EnumType, values.GetValue(0)!);
            }
            else if (colDef.IsFkColumn)
            {
                // FK列：从缓存中取第一个示例值
                var fkSample = GetFkSampleValue(colDef, fkReverseCache);
                if (fkSample != null)
                    sheet.Cells[sampleRow, importColumns.IndexOf(colDef) + 1].Value = fkSample;
            }
            else if (colDef.PropertyType == typeof(DateTime) || colDef.PropertyType == typeof(DateTime?))
            {
                sheet.Cells[sampleRow, importColumns.IndexOf(colDef) + 1].Value = DateTime.Today.ToString("yyyy-MM-dd");
            }
            else if (colDef.PropertyType == typeof(bool) || colDef.PropertyType == typeof(bool?))
            {
                sheet.Cells[sampleRow, importColumns.IndexOf(colDef) + 1].Value = "是";
            }
            else if (colDef.PropertyType == typeof(int) || colDef.PropertyType == typeof(int?))
            {
                sheet.Cells[sampleRow, importColumns.IndexOf(colDef) + 1].Value = 1;
            }
            else if (colDef.PropertyType == typeof(decimal) || colDef.PropertyType == typeof(decimal?))
            {
                sheet.Cells[sampleRow, importColumns.IndexOf(colDef) + 1].Value = "0.00";
            }
            else if (!colDef.IsFkColumn)
            {
                sheet.Cells[sampleRow, importColumns.IndexOf(colDef) + 1].Value = colDef.Header;
            }
        }

        sheet.Cells[1, 1, 2, def.Columns.Count].AutoFitColumns();
        return await package.GetAsByteArrayAsync();
    }

    #endregion

    #region 私有方法

    private async Task<List<object>> QueryAllAsync(Type entityType)
    {
        var dbSet = _context.GetType().GetMethod("Set", Type.EmptyTypes)!
            .MakeGenericMethod(entityType)
            .Invoke(_context, null)!;

        var query = (IQueryable)dbSet;
        var result = await Task.Run(() =>
        {
            var list = new List<object>();
            foreach (var item in (IEnumerable)query)
                list.Add(item);
            return list;
        });

        return result;
    }

    private Dictionary<string, PropertyInfo> BuildPropertyCache(EntityDef def)
    {
        var cache = new Dictionary<string, PropertyInfo>();
        foreach (var prop in def.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.CanRead && prop.CanWrite)
                cache[prop.Name] = prop;
        }
        return cache;
    }

    /// <summary>
    /// 构建FK反向缓存（用于导出时解析外键列的显示值）
    /// 映射：FkEntityKey → { fkId → 业务主键值 }
    /// </summary>
    private async Task<Dictionary<string, Dictionary<int, string>>> BuildFkReverseCacheForExportAsync(EntityDef def)
    {
        var cache = new Dictionary<string, Dictionary<int, string>>();

        foreach (var colDef in def.Columns.Where(c => c.IsFkColumn && !c.FkRequiresJoin))
        {
            if (colDef.FkEntityKey == null || !DataExchangeRegistry.Registry.TryGetValue(colDef.FkEntityKey, out var fkDef))
                continue;
            if (cache.ContainsKey(colDef.FkEntityKey)) continue;

            var fkData = await QueryAllAsync(fkDef.Type);
            var lookup = new Dictionary<int, string>();
            var idProp = fkDef.Type.GetProperty("Id");
            var targetProp = fkDef.Type.GetProperty(colDef.FkLookupProperty!);

            if (idProp != null && targetProp != null)
            {
                foreach (var item in fkData)
                {
                    var id = (int)idProp.GetValue(item)!;
                    var val = targetProp.GetValue(item)?.ToString();
                    if (val != null && !lookup.ContainsKey(id))
                        lookup[id] = val;
                }
            }

            cache[colDef.FkEntityKey] = lookup;
        }

        // 特殊处理：ProcessGroup 反向缓存（用于导出时解析 ProcessGroupId → SequenceNumber）
        if (def.Columns.Any(c => c.FkEntityKey == "ProcessGroup"))
        {
            var processGroups = await _context.Set<ProcessGroup>()
                .Include(pg => pg.ProductionBatch)
                .ToListAsync();

            var pgReverseLookup = new Dictionary<int, string>();
            foreach (var pg in processGroups)
            {
                var key = $"{pg.ProductionBatch.BatchNo}|{pg.SequenceNumber}";
                if (!pgReverseLookup.ContainsKey(pg.Id))
                    pgReverseLookup[pg.Id] = key;
            }

            cache["ProcessGroup"] = pgReverseLookup;
        }

        // 特殊处理：SectionOutsource 反向缓存（用于导出时解析 SectionOutsourceId → BatchNo,SectionName,Vendor）
        if (def.Columns.Any(c => c.FkEntityKey == "SectionOutsource"))
        {
            var sectionOutsources = await _context.Set<SectionOutsource>()
                .Include(so => so.ProductionBatch)
                .ToListAsync();

            var soReverseLookup = new Dictionary<int, string>();
            foreach (var so in sectionOutsources)
            {
                var key = $"{so.ProductionBatch.BatchNo}|{so.SectionName}|{so.OutsourceVendor}";
                if (!soReverseLookup.ContainsKey(so.Id))
                    soReverseLookup[so.Id] = key;
            }

            cache["SectionOutsource"] = soReverseLookup;
        }

        // 特殊处理：PicklingInRecord 反向缓存（用于导出时解析 PicklingInRecordId → BatchNo,SectionName）
        if (def.Columns.Any(c => c.FkEntityKey == "PicklingInRecord"))
        {
            var picklingInRecords = await _context.Set<PicklingInRecord>()
                .Include(p => p.ProductionBatch)
                .ToListAsync();

            var pkReverseLookup = new Dictionary<int, string>();
            foreach (var p in picklingInRecords)
            {
                var key = $"{p.ProductionBatch.BatchNo}|{p.SectionName}";
                if (!pkReverseLookup.ContainsKey(p.Id))
                    pkReverseLookup[p.Id] = key;
            }

            cache["PicklingInRecord"] = pkReverseLookup;
        }

        return cache;
    }

    /// <summary>
    /// 构建 OrderItem 复合键缓存（用于 ProductRequirement 导出时的 FK 解析）
    /// 映射：OrderItem.Id → { OrderNumber, Sequence }
    /// </summary>
    private async Task<Dictionary<int, (string orderNo, int sequence)>> BuildOrderItemExportCacheAsync(EntityDef def)
    {
        var cache = new Dictionary<int, (string, int)>();

        if (def.Columns.Any(c => c.FkRequiresJoin && c.FkEntityKey == "OrderItem"))
        {
            var orderItems = await _context.Set<OrderItem>()
                .Include(oi => oi.SalesOrder)
                .ToListAsync();

            foreach (var oi in orderItems)
            {
                var orderNo = oi.SalesOrder?.OrderNumber ?? "";
                cache[oi.Id] = (orderNo, oi.Sequence);
            }
        }

        return cache;
    }

    /// <summary>
    /// 解析导出时 FK 列的显示值
    /// </summary>
    private async Task<string?> ResolveFkExportValue(ColumnDef colDef, object entity,
        Dictionary<string, PropertyInfo> propertyCache,
        Dictionary<string, Dictionary<int, string>> fkReverseCache,
        Dictionary<int, (string orderNo, int sequence)> orderItemExportCache)
    {
        // 特殊处理：ProductRequirement → OrderItem 复合键
        if (colDef.FkRequiresJoin && colDef.FkEntityKey == "OrderItem")
        {
            if (colDef.FkTargetProperty != null &&
                propertyCache.TryGetValue(colDef.FkTargetProperty, out var oiIdProp))
            {
                var oiIdVal = oiIdProp.GetValue(entity);
                if (oiIdVal is int oiId && orderItemExportCache.TryGetValue(oiId, out var oiInfo))
                {
                    // "订单号"列 → 返回 SalesOrder.OrderNumber
                    if (colDef.FkLookupProperty == "Id")
                        return oiInfo.orderNo;
                    // "项次号"列 → 返回 Sequence
                    if (colDef.FkLookupProperty == "Sequence")
                        return oiInfo.sequence.ToString();
                }
            }
            return null;
        }

        // 特殊处理：ProcessGroup 复合键（BatchNo|SequenceNumber → ProcessGroupId）
        if (colDef.FkRequiresJoin && colDef.FkEntityKey == "ProcessGroup")
        {
            if (colDef.FkTargetProperty != null &&
                propertyCache.TryGetValue(colDef.FkTargetProperty, out var pgIdProp))
            {
                var pgIdVal = pgIdProp.GetValue(entity);
                if (pgIdVal is int pgId &&
                    fkReverseCache.TryGetValue("ProcessGroup", out var pgCache) &&
                    pgCache.TryGetValue(pgId, out var pgCompositeKey))
                {
                    var parts = pgCompositeKey.Split('|', 2);
                    if (colDef.FkLookupProperty == "SequenceNumber" && parts.Length > 1)
                        return parts[1]; // 返回 SequenceNumber
                }
            }
            return null;
        }

        // 特殊处理：SectionOutsource 复合键（BatchNo|SectionName|Vendor → SectionOutsourceId）
        if (colDef.FkRequiresJoin && colDef.FkEntityKey == "SectionOutsource")
        {
            if (colDef.FkTargetProperty != null &&
                propertyCache.TryGetValue(colDef.FkTargetProperty, out var soIdProp))
            {
                var soIdVal = soIdProp.GetValue(entity);
                if (soIdVal is int soId &&
                    fkReverseCache.TryGetValue("SectionOutsource", out var soCache) &&
                    soCache.TryGetValue(soId, out var soCompositeKey))
                {
                    var parts = soCompositeKey.Split('|', 3);
                    if (colDef.FkLookupProperty == "BatchNo" && parts.Length > 0)
                        return parts[0];
                    if (colDef.FkLookupProperty == "SectionName" && parts.Length > 1)
                        return await _sectionNameDisplay.ToDisplayAsync(parts[1]);
                    if (colDef.FkLookupProperty == "OutsourceVendor" && parts.Length > 2)
                        return parts[2];
                }
            }
            return null;
        }

        // 特殊处理：PicklingInRecord 复合键（BatchNo|SectionName → PicklingInRecordId）
        if (colDef.FkRequiresJoin && colDef.FkEntityKey == "PicklingInRecord")
        {
            if (colDef.FkTargetProperty != null &&
                propertyCache.TryGetValue(colDef.FkTargetProperty, out var pkIdProp))
            {
                var pkIdVal = pkIdProp.GetValue(entity);
                if (pkIdVal is int pkId &&
                    fkReverseCache.TryGetValue("PicklingInRecord", out var pkCache) &&
                    pkCache.TryGetValue(pkId, out var pkCompositeKey))
                {
                    var parts = pkCompositeKey.Split('|', 2);
                    if (colDef.FkLookupProperty == "BatchNo" && parts.Length > 0)
                        return parts[0];
                    if (colDef.FkLookupProperty == "SectionName" && parts.Length > 1)
                        return await _sectionNameDisplay.ToDisplayAsync(parts[1]);
                }
            }
            return null;
        }

        // 常规FK解析
        if (colDef.FkEntityKey == null || colDef.FkTargetProperty == null)
            return null;

        if (!propertyCache.TryGetValue(colDef.FkTargetProperty, out var fkIdProp))
            return null;

        var fkIdValue = fkIdProp.GetValue(entity);
        if (fkIdValue == null)
            return null;

        if (fkReverseCache.TryGetValue(colDef.FkEntityKey, out var fkLookup) &&
            fkLookup.TryGetValue((int)fkIdValue, out var displayVal))
        {
            return displayVal;
        }

        return null;
    }

    /// <summary>
    /// 获取 FK 列的示例值（模板用）
    /// </summary>
    private string? GetFkSampleValue(ColumnDef colDef,
        Dictionary<string, Dictionary<int, string>> fkReverseCache)
    {
        if (colDef.FkEntityKey == null) return null;

        if (fkReverseCache.TryGetValue(colDef.FkEntityKey, out var lookup) && lookup.Count > 0)
        {
            return lookup.First().Value;
        }

        return null;
    }

    /// <summary>
    /// 导出时解析 WorkOrder.OrderItemIds（"1,2,3" → "D26Z2117001|1;D26Z2117001|2"）
    /// </summary>
    private async Task<string?> ResolveOrderItemIdsForExportAsync(string orderItemIds)
    {
        if (string.IsNullOrWhiteSpace(orderItemIds))
            return null;

        var ids = orderItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (ids.Length == 0)
            return null;

        var idList = new List<int>();
        foreach (var id in ids)
        {
            if (int.TryParse(id.Trim(), out var parsed))
                idList.Add(parsed);
        }

        if (idList.Count == 0)
            return null;

        var orderItems = await _context.Set<OrderItem>()
            .Include(oi => oi.SalesOrder)
            .Where(oi => idList.Contains(oi.Sequence))
            .OrderBy(oi => oi.Sequence)
            .ToListAsync();

        var result = new List<string>();
        foreach (var oi in orderItems)
        {
            var orderNo = oi.SalesOrder?.OrderNumber ?? "?";
            result.Add($"{orderNo}|{oi.Sequence}");
        }

        return string.Join(";", result);
    }

    #endregion
}
