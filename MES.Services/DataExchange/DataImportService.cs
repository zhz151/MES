using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using MES.Core.Constants;
using MES.Core.Enums;
using MES.Core.Exceptions;
using MES.Core.Helpers;
using MES.Core.Interfaces.DataExchange;
using MES.Core.Models;
using MES.Data;
using MES.Data.Entities;
using MES.Data.Entities.Auth;
using MES.Data.Entities.Batch;
using MES.Data.Entities.Configuration;
using MES.Data.Entities.Equipment;
using MES.Data.Entities.Materials;
using MES.Data.Entities.Order;
using MES.Data.Entities.StandardRegister;
using MES.Data.Entities.Quality;
using MES.Data.Entities.Scheduling;
using MES.Data.Entities.Warehouse;
using MES.Data.Entities.WorkOrder;
using MES.Services.Extensions;
using MES.Services.Helpers;
using MES.Shared.Constants;

namespace MES.Services.DataExchange;

/// <summary>
/// 数据导入服务（仅管理员可导入）
/// </summary>
public class DataImportService : IDataImportService
{
    protected readonly AppDbContext _context;
    private readonly ILogger<DataImportService> _logger;

    public DataImportService(AppDbContext context, ILogger<DataImportService> logger)
    {
        _context = context;
        _logger = logger;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

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

    #region 预览

    /// <summary>
    /// 预览导入结果（验证但不写入数据库）
    /// </summary>
    public async Task<ImportPreviewResult> PreviewAsync(string entityKey, byte[] fileData, string? userName)
    {
        if (!DataExchangeRegistry.Registry.TryGetValue(entityKey, out var def))
            throw new BusinessException($"不支持的实体类型: {entityKey}");

        var rows = ParseExcel(fileData, def);
        var result = new ImportPreviewResult { TotalRows = rows.Count };

        // 构建FK查询缓存
        var fkCache = await BuildFkCacheAsync(def);

        // 查询已存在的记录（用于重复检测）
        var existingKeys = await LoadExistingKeysAsync(def);

        foreach (var row in rows)
        {
            var errors = new List<string>();

            // 验证必填字段
            foreach (var colDef in def.Columns.Where(c => c.IsRequired && !c.IsFkColumn && !c.IsSystem))
            {
                if (!row.Values.TryGetValue(colDef.Header, out var val) || string.IsNullOrWhiteSpace(val))
                    errors.Add($"{colDef.Header} 不能为空");
            }

            // 验证FK列：用户提供了值但在FK缓存中找不到对应的引用记录
            foreach (var colDef in def.Columns.Where(c => c.IsFkColumn && c.FkEntityKey != null && !c.FkRequiresJoin))
            {
                if (!row.Values.TryGetValue(colDef.Header, out var fkVal) || string.IsNullOrWhiteSpace(fkVal))
                    continue;

                if (!fkCache.TryGetValue(colDef.FkEntityKey!, out var lookup) || !lookup.ContainsKey(fkVal))
                {
                    var entityName = DataExchangeRegistry.Registry.TryGetValue(colDef.FkEntityKey!, out var fkDef)
                        ? fkDef.DisplayName
                        : colDef.FkEntityKey!;
                    errors.Add($"{colDef.Header} 的值 \"{fkVal}\" 在 {entityName} 表中不存在");
                }
            }

            // 判定该行将被如何处理
            var rowKey = GetRowKey(def, row);
            string rowAction;
            if (rowKey == null)
            {
                rowAction = "新增";
            }
            else if (existingKeys.Contains(rowKey))
            {
                rowAction = "覆盖";
            }
            else if (rowKey.StartsWith("__ID__:"))
            {
                rowAction = "ID不存在";
                errors.Add($"ID 为 {rowKey[7..]} 的记录在数据库中不存在");
            }
            else
            {
                rowAction = "新增";
            }

            result.RowResults.Add(new ImportRowResult
            {
                RowNumber = row.RowNumber,
                Key = rowKey,
                Errors = errors,
                IsDuplicate = rowKey != null && existingKeys.Contains(rowKey),
                IsValid = errors.Count == 0,
                RowAction = rowAction,
                Data = row,
            });
        }

        result.ValidCount = result.RowResults.Count(r => r.IsValid);
        result.ErrorCount = result.RowResults.Count(r => !r.IsValid);
        result.DuplicateCount = result.RowResults.Count(r => r.IsDuplicate);
        result.AddCount = result.RowResults.Count(r => r.RowAction == "新增");
        result.OverwriteCount = result.RowResults.Count(r => r.RowAction == "覆盖");
        result.InvalidIdCount = result.RowResults.Count(r => r.RowAction == "ID不存在");

        return result;
    }

    #endregion

    #region 导入

    /// <summary>
    /// 执行导入（EF Core事务：禁约束 → 累积写入 → 批量保存 → 校验约束 → 提交/回滚）
    /// </summary>
    public async Task<ImportResult> ImportAsync(string entityKey, byte[] fileData, string? userName)
    {
        if (!DataExchangeRegistry.Registry.TryGetValue(entityKey, out var def))
            throw new BusinessException($"不支持的实体类型: {entityKey}");

        var rows = ParseExcel(fileData, def);
        var result = new ImportResult { TotalRows = rows.Count };

        // 构建FK查询缓存
        var fkCache = await BuildFkCacheAsync(def);

        // 预加载已存在记录缓存（用于重复检测）
        var existingCache = await LoadExistingEntitiesAsync(def);

        // 使用EF Core管理事务（避免MARS/savepoint冲突）
        var (transaction, dbTransaction) = await BeginImportTransactionAsync();

        using (transaction)
        {
            try
            {
                // 1. 禁用所有外键约束
                await DisableAllConstraintsAsync(dbTransaction?.Connection!, dbTransaction!);

                // 2. 逐行累积到DbContext（不逐行保存）
                // 跟踪批次内已分配的系统编码，避免重复
                var pendingCodes = def.Columns
                    .Where(c => c.IsSystem && c.Property != null && DataExchangeRegistry.CodePrefixMap.ContainsKey(c.Property))
                    .ToDictionary(c => c.Property!, _ => new HashSet<string>());

                // ProcessGroup 特殊处理：有子记录引用的工序组原地更新（保留ID），无引用的安全删除
                // 避免 FK 约束冲突（FK_ProductionRecord_ProcessGroup_ProcessGroupId 等）
                if (entityKey == "ProcessGroup")
                {
                    var batchNoCol = def.Columns.FirstOrDefault(c => c.FkEntityKey == "ProductionBatch");
                    if (batchNoCol != null && fkCache.TryGetValue("ProductionBatch", out var batchLookup))
                    {
                        var batchNos = rows
                            .Select(r => r.Values.GetValueOrDefault(batchNoCol.Header, ""))
                            .Where(v => !string.IsNullOrWhiteSpace(v))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        var batchIds = batchNos
                            .Select(bn => batchLookup.GetValueOrDefault(bn))
                            .Where(id => id > 0)
                            .ToList();
                        var existing = await _context.Set<ProcessGroup>()
                            .Where(pg => batchIds.Contains(pg.ProductionBatchId))
                            .ToListAsync();

                        if (existing.Count > 0)
                        {
                            // 检查哪些工序组有子记录引用
                            var existingIds = existing.Select(e => e.Id).ToList();
                            var referencedIds = new HashSet<int>();

                            var prodRefs = await _context.Set<ProductionRecord>()
                                .Where(r => existingIds.Contains(r.ProcessGroupId))
                                .Select(r => r.ProcessGroupId)
                                .Distinct()
                                .ToListAsync();
                            foreach (var id in prodRefs) referencedIds.Add(id);

                            var soRefs = await _context.Set<SectionOutsource>()
                                .Where(s => existingIds.Contains(s.ProcessGroupId))
                                .Select(s => s.ProcessGroupId)
                                .Distinct()
                                .ToListAsync();
                            foreach (var id in soRefs) referencedIds.Add(id);

                            var piRefs = await _context.Set<ProcessInspection>()
                                .Where(p => existingIds.Contains(p.ProcessGroupId))
                                .Select(p => p.ProcessGroupId)
                                .Distinct()
                                .ToListAsync();
                            foreach (var id in piRefs) referencedIds.Add(id);

                            var pkRefs = await _context.Set<PicklingInRecord>()
                                .Where(p => existingIds.Contains(p.ProcessGroupId))
                                .Select(p => p.ProcessGroupId)
                                .Distinct()
                                .ToListAsync();
                            foreach (var id in pkRefs) referencedIds.Add(id);

                            // 有引用的工序组：保留ID，按 (ProductionBatchId, SequenceNumber) 索引
                            var referencedPgs = existing.Where(e => referencedIds.Contains(e.Id)).ToList();
                            var pgByKey = referencedPgs.ToDictionary(
                                pg => (pg.ProductionBatchId, pg.SequenceNumber));

                            // 无引用的工序组：安全删除
                            var unreferencedPgs = existing.Where(e => !referencedIds.Contains(e.Id)).ToList();
                            if (unreferencedPgs.Count > 0)
                            {
                                _context.Set<ProcessGroup>().RemoveRange(unreferencedPgs);
                                await _context.SaveChangesAsync();
                                _logger.LogInformation("已清理 {Count} 个无引用的旧工序组记录", unreferencedPgs.Count);
                            }

                            // 对有引用的工序组，从导入行中匹配并原地更新属性
                            if (referencedPgs.Count > 0)
                            {
                                var seqCol = def.Columns.FirstOrDefault(c => c.Property == "SequenceNumber");
                                var propertyCache = BuildPropertyCache(def);
                                var now = DateTimeOffset.Now;
                                var rowsToSkip = new List<ImportRowData>();

                                foreach (var row in rows)
                                {
                                    var batchNo = row.Values.GetValueOrDefault(batchNoCol.Header, "");
                                    var batchId = batchLookup.GetValueOrDefault(batchNo);
                                    if (batchId <= 0) continue;

                                    var seqStr = seqCol != null ? row.Values.GetValueOrDefault(seqCol.Header, "") : "";
                                    if (!int.TryParse(seqStr, out var seq)) continue;

                                    if (pgByKey.TryGetValue((batchId, seq), out var existingPg))
                                    {
                                        foreach (var colDef in def.Columns)
                                        {
                                            // 跳过系统列、FK列、SequenceNumber（匹配键不更新）
                                            if (colDef.IsSystem || colDef.IsFkColumn) continue;
                                            if (colDef.Property == "SequenceNumber") continue;
                                            if (colDef.Property == null || !propertyCache.TryGetValue(colDef.Property, out var prop)) continue;
                                            if (!row.Values.TryGetValue(colDef.Header, out var cellValue)) continue;

                                            if (string.IsNullOrWhiteSpace(cellValue))
                                            {
                                                if (prop.PropertyType.IsGenericType &&
                                                    prop.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                                                    prop.SetValue(existingPg, null);
                                                continue;
                                            }

                                            var value = ConvertValue(cellValue, prop.PropertyType, colDef);
                                            prop.SetValue(existingPg, value);
                                        }

                                        // 更新审计字段
                                        if (existingPg is BaseEntity be)
                                        {
                                            be.UpdatedTime = now;
                                            be.UpdatedBy = userName ?? "system";
                                        }

                                        rowsToSkip.Add(row);
                                    }
                                }

                                // 从导入行中移除已原地更新的行，避免重复创建
                                foreach (var row in rowsToSkip)
                                    rows.Remove(row);

                                _logger.LogInformation("已原地更新 {Count} 个有引用的工序组记录", referencedPgs.Count);
                            }
                        }
                    }
                }

                // OrderItem 特殊处理：避免唯一键冲突 UK_OrderItem_Sequence_Active
                if (entityKey == "OrderItem")
                {
                    var orderNoCol = def.Columns.FirstOrDefault(c => c.FkEntityKey == "SalesOrder");
                    var seqCol = def.Columns.FirstOrDefault(c => c.Property == "Sequence");
                    if (orderNoCol != null && seqCol != null && fkCache.TryGetValue("SalesOrder", out var salesOrderLookup))
                    {
                        // 先删除关联的 ProductRequirement（避免 FK 约束冲突），再删 OrderItem
                        var orderNos = rows
                            .Select(r => r.Values.GetValueOrDefault(orderNoCol.Header, ""))
                            .Where(v => !string.IsNullOrWhiteSpace(v))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        var salesOrderIds = orderNos
                            .Select(no => salesOrderLookup.GetValueOrDefault(no))
                            .Where(id => id > 0)
                            .ToList();
                        var existing = await _context.Set<OrderItem>()
                            .Where(oi => salesOrderIds.Contains(oi.SalesOrderId))
                            .ToListAsync();
                        if (existing.Count > 0)
                        {
                            var existingOiIds = existing.Select(oi => oi.Id).ToList();
                            var existingPr = await _context.Set<ProductRequirement>()
                                .Where(pr => existingOiIds.Contains(pr.OrderItemId))
                                .ToListAsync();
                            if (existingPr.Count > 0)
                            {
                                _context.Set<ProductRequirement>().RemoveRange(existingPr);
                                _logger.LogInformation("已级联清理 {Count} 个关联的技术要求记录", existingPr.Count);
                            }
                            _context.Set<OrderItem>().RemoveRange(existing);
                            await _context.SaveChangesAsync();
                            // 清空缓存，避免 ImportRowAsync 使用已删除（Detached）的实体
                            existingCache.Clear();
                            _logger.LogInformation("已清理 {Count} 个旧的订单项次记录", existing.Count);
                        }
                    }
                }

                // ProductRequirement 特殊处理：避免唯一键冲突 UK_ProductRequirement_OrderItemId
                if (entityKey == "ProductRequirement")
                {
                    var orderNoCol = def.Columns.FirstOrDefault(c => c.FkEntityKey == "OrderItem" && c.FkLookupProperty == "Id");
                    var seqCol = def.Columns.FirstOrDefault(c => c.FkEntityKey == "OrderItem" && c.FkLookupProperty == "Sequence");
                    if (orderNoCol != null && seqCol != null && fkCache.TryGetValue("OrderItem", out var oiCache))
                    {
                        var compositeKeys = rows
                            .Select(r =>
                            {
                                var orderNo = r.Values.GetValueOrDefault(orderNoCol.Header, "");
                                var seq = r.Values.GetValueOrDefault(seqCol.Header, "");
                                return string.IsNullOrWhiteSpace(orderNo) || string.IsNullOrWhiteSpace(seq) ? null : $"{orderNo}|{seq}";
                            })
                            .Where(k => k != null)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList()!;
                        var orderItemIds = compositeKeys
                            .Select(k => oiCache.GetValueOrDefault(k!))
                            .Where(id => id > 0)
                            .ToList();

                        // 删除已有技术要求，再重新导入
                        var existing = await _context.Set<ProductRequirement>()
                            .Where(pr => orderItemIds.Contains(pr.OrderItemId))
                            .ToListAsync();
                        if (existing.Count > 0)
                        {
                            _context.Set<ProductRequirement>().RemoveRange(existing);
                            await _context.SaveChangesAsync();
                            // 清空缓存，避免 ImportRowAsync 使用已删除（Detached）的实体
                            existingCache.Clear();
                            _logger.LogInformation("已清理 {Count} 个旧的技术要求记录", existing.Count);
                        }
                    }
                }

                // SubcontractReturnItem 特殊处理：避免唯一键冲突 UK_ReturnItem_Seq
                if (entityKey == "SubcontractReturnItem")
                {
                    var orderNoCol = def.Columns.FirstOrDefault(c => c.FkEntityKey == "SubcontractOrder");
                    var seqCol = def.Columns.FirstOrDefault(c => c.Property == "Sequence");
                    if (orderNoCol != null && seqCol != null && fkCache.TryGetValue("SubcontractOrder", out var subOrderLookup))
                    {
                        var orderNos = rows
                            .Select(r => r.Values.GetValueOrDefault(orderNoCol.Header, ""))
                            .Where(v => !string.IsNullOrWhiteSpace(v))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        var subOrderIds = orderNos
                            .Select(no => subOrderLookup.GetValueOrDefault(no))
                            .Where(id => id > 0)
                            .ToList();

                        // 删除关联委外单下已有的退货项，再重新导入
                        var existing = await _context.Set<SubcontractReturnItem>()
                            .Where(sri => subOrderIds.Contains(sri.SubcontractOrderId))
                            .ToListAsync();
                        if (existing.Count > 0)
                        {
                            _context.Set<SubcontractReturnItem>().RemoveRange(existing);
                            await _context.SaveChangesAsync();
                            // 清空缓存，避免 ImportRowAsync 使用已删除（Detached）的实体
                            existingCache.Clear();
                            _logger.LogInformation("已清理 {Count} 个旧的委外退货项记录", existing.Count);
                        }
                    }
                }

                // MaterialReceiveCheck 特殊处理：避免唯一键冲突 UK_MaterialReceiveCheck_BatchId（按 ProductionBatchId 去重，而非 BatchNo 字符串）
                if (entityKey == "MaterialReceiveCheck")
                {
                    var batchNoCol = def.Columns.FirstOrDefault(c => c.FkEntityKey == "ProductionBatch");
                    if (batchNoCol != null && fkCache.TryGetValue("ProductionBatch", out var mrcBatchLookup))
                    {
                        var batchNos = rows
                            .Select(r => r.Values.GetValueOrDefault(batchNoCol.Header, "")?.Trim())
                            .Where(v => !string.IsNullOrWhiteSpace(v))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        var batchIds = batchNos
                            .Select(bn => mrcBatchLookup.GetValueOrDefault(bn!))
                            .Where(id => id > 0)
                            .ToList();

                        // 删除已有检验到料记录，再重新导入
                        var existing = await _context.Set<MaterialReceiveCheck>()
                            .Where(m => batchIds.Contains(m.ProductionBatchId))
                            .ToListAsync();
                        if (existing.Count > 0)
                        {
                            _context.Set<MaterialReceiveCheck>().RemoveRange(existing);
                            await _context.SaveChangesAsync();
                            // 清空缓存，避免 ImportRowAsync 使用已删除（Detached）的实体
                            existingCache.Clear();
                            _logger.LogInformation("已清理 {Count} 个旧的检验到料记录", existing.Count);
                        }
                    }
                }

                foreach (var row in rows)
                {
                    try
                    {
                        var processed = await ImportRowAsync(def, row, fkCache, userName, existingCache, pendingCodes);
                        if (processed)
                            result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.FailedCount++;
                        result.Errors.Add(new ImportRowError
                        {
                            RowNumber = row.RowNumber,
                            Message = ex.Message,
                        });
                    }
                }

                // 3. 批量保存所有累积的变更
                await _context.SaveChangesAsync();

                // 4. 启用并验证所有外键约束
                var checkErrors = await EnableAndCheckConstraintsAsync(dbTransaction?.Connection!, dbTransaction!);
                if (checkErrors.Count > 0)
                {
                    throw new BusinessException("外键约束验证失败，共 " + checkErrors.Count + " 个错误");
                }

                // 5. 提交事务
                await transaction.CommitAsync();
                _logger.LogInformation(
                    "导入 {Entity} 完成: 共 {Total} 行, 成功 {Success}, 失败 {Failed}",
                    entityKey, result.TotalRows, result.SuccessCount, result.FailedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导入 {Entity} 失败，已回滚: {Message}", entityKey, ex.Message);
                await transaction.RollbackAsync();
                result.SuccessCount = 0;
                result.FailedCount = result.TotalRows;
                result.HasRolledBack = true;
                result.RollbackReason = GetRollbackReason(ex);
            }

            return result;
        }
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

    private List<ImportRowData> ParseExcel(byte[] fileData, EntityDef def)
    {
        var rows = new List<ImportRowData>();

        using var stream = new MemoryStream(fileData);
        using var package = new ExcelPackage(stream);
        var sheet = package.Workbook.Worksheets[0];
        if (sheet == null || sheet.Dimension == null)
            return rows;

        // 读取表头（第一行）
        var headerCount = sheet.Dimension.Columns;
        var headers = new List<string>();
        for (int c = 1; c <= headerCount; c++)
        {
            var header = sheet.Cells[1, c].Text?.Trim();
            headers.Add(header ?? "");
        }

        // 映射表头到列定义
        var columnMapping = new List<(int colIndex, ColumnDef? colDef)>();
        foreach (var header in headers)
        {
            var colDef = def.Columns.FirstOrDefault(c => c.Header == header);
            columnMapping.Add((headers.IndexOf(header), colDef));
        }

        // 读取数据行（从第2行开始）
        for (int r = 2; r <= sheet.Dimension.Rows; r++)
        {
            var hasData = false;
            var data = new Dictionary<string, string>();
            var rowNumber = r;

            foreach (var (colIndex, colDef) in columnMapping)
            {
                if (colDef == null) continue;
                var cellValue = sheet.Cells[r, colIndex + 1]?.Text?.Trim();
                data[colDef.Header] = cellValue ?? "";
                if (!string.IsNullOrEmpty(cellValue))
                    hasData = true;
            }

            if (hasData)
                rows.Add(new ImportRowData { RowNumber = rowNumber, Values = data });
        }

        return rows;
    }

    private async Task<Dictionary<string, Dictionary<string, int>>> BuildFkCacheAsync(EntityDef def)
    {
        var cache = new Dictionary<string, Dictionary<string, int>>();

        foreach (var colDef in def.Columns.Where(c => c.IsFkColumn && !c.FkRequiresJoin))
        {
            if (colDef.FkEntityKey == null || !DataExchangeRegistry.Registry.TryGetValue(colDef.FkEntityKey, out var fkDef))
                continue;

            var key = colDef.FkEntityKey;
            if (cache.ContainsKey(key)) continue;

            var fkData = await QueryAllAsync(fkDef.Type);
            var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var idProp = fkDef.Type.GetProperty("Id");
            var lookupProp = fkDef.Type.GetProperty(colDef.FkLookupProperty!);

            if (idProp != null && lookupProp != null)
            {
                foreach (var item in fkData)
                {
                    var id = (int)idProp.GetValue(item)!;
                    var val = lookupProp.GetValue(item)?.ToString();
                    if (val != null && !lookup.ContainsKey(val))
                        lookup[val] = id;
                    else if (val != null)
                        _logger.LogWarning("FK缓存发现重复键: 实体 {Entity}, 字段 {Field}, 值 {Value}",
                            colDef.FkEntityKey, colDef.FkLookupProperty, val);
                }
            }

            cache[key] = lookup;
        }

        // 特殊处理：OrderItem FK 解析（需要 SalesOrderNo + Sequence）
        if (def.Columns.Any(c => c.FkRequiresJoin))
        {
            var orderItems = await _context.Set<OrderItem>()
                .Include(oi => oi.SalesOrder)
                .ToListAsync();

            var orderItemLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var oi in orderItems)
            {
                var key = $"{oi.SalesOrder.OrderNumber}|{oi.Sequence}";
                if (!orderItemLookup.ContainsKey(key))
                    orderItemLookup[key] = oi.Id;
            }

            cache["OrderItem"] = orderItemLookup;
        }

        // 特殊处理：ProcessGroup FK 解析（需要 BatchNo + SequenceNumber）
        if (def.Columns.Any(c => c.FkEntityKey == "ProcessGroup"))
        {
            var processGroups = await _context.Set<ProcessGroup>()
                .Include(pg => pg.ProductionBatch)
                .ToListAsync();

            var pgLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var pg in processGroups)
            {
                var key = $"{pg.ProductionBatch.BatchNo}|{pg.SequenceNumber}";
                if (!pgLookup.ContainsKey(key))
                    pgLookup[key] = pg.Id;
            }

            cache["ProcessGroup"] = pgLookup;

            // 按工段名称查找工序组的缓存（用于按"批次号+工序名称+制造规格+工段名称"匹配）
            var pgIdBySectionLk = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var pgSeqBySectionLk = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            void AddSectionLk(Dictionary<string, int> idLk, Dictionary<string, int> seqLk,
                string batchNo, string processName, string manufacturingSpec, string sectionName, int? orderVal, int pgId)
            {
                if (!orderVal.HasValue) return;
                var key = $"{batchNo}|{processName}|{manufacturingSpec}|{sectionName}";
                if (!idLk.ContainsKey(key))
                {
                    idLk[key] = pgId;
                    seqLk[key] = orderVal.Value;
                }
            }
            foreach (var pg in processGroups)
            {
                var bn = pg.ProductionBatch.BatchNo;
                var pn = pg.ProcessName;
                var ms = pg.ManufacturingSpec ?? "";
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.ColdRollDraw, pg.ColdRollDraw, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.OilPipeCut, pg.OilPipeCut, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.Degrease, pg.Degrease, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.EmulsionWash, pg.EmulsionWash, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.UltrasonicWash, pg.UltrasonicWash, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.ClothPolish, pg.ClothPolish, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.BrightAnnealing, pg.BrightAnnealing, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.Solution, pg.Solution, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.Straighten, pg.Straighten, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.Cut, pg.Cut, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.ThicknessMeasure, pg.ThicknessMeasure, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.Pickle, pg.Pickle, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.OuterPolish, pg.OuterPolish, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.InnerPolish, pg.InnerPolish, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.InnerGrinding, pg.InnerGrinding, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.OuterSpotGrinding, pg.OuterSpotGrinding, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.SandBlasting, pg.SandBlasting, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.ShotBlasting, pg.ShotBlasting, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.Inspection, pg.Inspection, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.WeldingHead, pg.WeldingHead, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.Welding, pg.Welding, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.Lubrication, pg.Lubrication, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.Packing, pg.Packing, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.Warehouse, pg.Warehouse, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.Extra1, pg.Extra1, pg.Id);
                AddSectionLk(pgIdBySectionLk, pgSeqBySectionLk, bn, pn, ms, SectionDefs.Extra2, pg.Extra2, pg.Id);
            }
            cache["ProcessGroupIdBySection"] = pgIdBySectionLk;
            cache["ProcessGroupSeqBySection"] = pgSeqBySectionLk;
        }

        // 特殊处理：SectionOutsource FK 解析（需要 BatchNo + SectionName + OutsourceVendor）
        if (def.Columns.Any(c => c.FkEntityKey == "SectionOutsource"))
        {
            var sectionOutsources = await _context.Set<SectionOutsource>()
                .Include(so => so.ProductionBatch)
                .ToListAsync();

            var soLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var so in sectionOutsources)
            {
                var key = $"{so.ProductionBatch.BatchNo}|{so.SectionName}|{so.OutsourceVendor}";
                if (!soLookup.ContainsKey(key))
                    soLookup[key] = so.Id;
            }

            cache["SectionOutsource"] = soLookup;
        }

        // 特殊处理：PicklingInRecord FK 解析（需要 BatchNo + SectionName）
        if (def.Columns.Any(c => c.FkEntityKey == "PicklingInRecord"))
        {
            var picklingInRecords = await _context.Set<PicklingInRecord>()
                .Include(p => p.ProductionBatch)
                .ToListAsync();

            var pkLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in picklingInRecords)
            {
                var key = $"{p.ProductionBatch.BatchNo}|{p.SectionName}";
                if (!pkLookup.ContainsKey(key))
                    pkLookup[key] = p.Id;
            }

            cache["PicklingInRecord"] = pkLookup;
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
    /// 获取 FK 列的示例值（模板用）
    /// </summary>
    private static string? GetFkSampleValue(ColumnDef colDef,
        Dictionary<string, Dictionary<int, string>> fkReverseCache)
    {
        if (colDef.FkEntityKey == null) return null;

        if (fkReverseCache.TryGetValue(colDef.FkEntityKey, out var lookup) && lookup.Count > 0)
        {
            return lookup.First().Value;
        }

        return null;
    }

    private async Task<HashSet<string>> LoadExistingKeysAsync(EntityDef def)
    {
        var keyProps = GetKeyProperties(def);
        var idProp = def.Type.GetProperty("Id");
        if (keyProps.Count == 0 && idProp == null) return new HashSet<string>();

        var data = await QueryAllAsync(def.Type);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in data)
        {
            // 优先按主键 ID 缓存，实现精确覆盖
            if (idProp != null)
            {
                var idVal = idProp.GetValue(item)?.ToString();
                if (idVal != null)
                    keys.Add("__ID__:" + idVal);
            }
            if (keyProps.Count == 1)
            {
                var val = keyProps[0].GetValue(item)?.ToString();
                if (val != null)
                    keys.Add(val);
            }
            else if (keyProps.Count > 1)
            {
                var key = BuildEntityKey(item, keyProps);
                if (key != null)
                    keys.Add(key);
            }
        }
        return keys;
    }

    private async Task<Dictionary<string, object>> LoadExistingEntitiesAsync(EntityDef def)
    {
        var cache = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var keyProps = GetKeyProperties(def);
        var idProp = def.Type.GetProperty("Id");
        if (keyProps.Count == 0 && idProp == null) return cache;

        var data = await QueryAllAsync(def.Type);
        foreach (var item in data)
        {
            // 优先按主键 ID 缓存，实现精确覆盖
            if (idProp != null)
            {
                var idVal = idProp.GetValue(item)?.ToString();
                if (!string.IsNullOrWhiteSpace(idVal))
                    cache["__ID__:" + idVal] = item;
            }
            var key = keyProps.Count > 0 ? BuildEntityKey(item, keyProps) : null;
            if (key != null && !cache.ContainsKey(key))
                cache[key] = item;
        }
        return cache;
    }

    private List<PropertyInfo> GetKeyProperties(EntityDef def)
    {
        var props = new List<PropertyInfo>();
        if (def.KeyColumn != null)
        {
            var prop = def.Type.GetProperty(def.KeyColumn);
            if (prop != null) props.Add(prop);
        }
        else if (def.CompositeKeyColumns != null)
        {
            foreach (var col in def.CompositeKeyColumns)
            {
                var prop = def.Type.GetProperty(col);
                if (prop != null) props.Add(prop);
            }
        }
        return props;
    }

    /// <summary>
    /// 从Excel行数据中提取业务键值（将属性名映射到Excel表头名）
    /// </summary>
    private static string? GetRowKey(EntityDef def, ImportRowData row)
    {
        // 优先按主键 ID 匹配（导出时携带的系统列），精确覆盖单行
        var idCol = def.Columns.FirstOrDefault(c => c.Property == "Id");
        if (idCol != null && row.Values.TryGetValue(idCol.Header, out var idVal) && !string.IsNullOrWhiteSpace(idVal))
            return "__ID__:" + idVal;

        if (def.KeyColumn != null)
        {
            var header = def.Columns.FirstOrDefault(c => c.Property == def.KeyColumn)?.Header;
            if (header != null && row.Values.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
                return val;
            return null;
        }
        if (def.CompositeKeyColumns != null)
        {
            var parts = def.CompositeKeyColumns
                .Select(propName =>
                {
                    var header = def.Columns.FirstOrDefault(c => c.Property == propName)?.Header;
                    return header != null ? row.Values.GetValueOrDefault(header, "")?.Trim() ?? "" : "";
                })
                .ToArray();
            return parts.All(p => p.Length > 0) ? string.Join("|", parts) : null;
        }
        return null;
    }

    private static string? BuildEntityKey(object entity, List<PropertyInfo> keyProps)
    {
        if (keyProps.Count == 1)
            return keyProps[0].GetValue(entity)?.ToString();
        var parts = keyProps.Select(p => p.GetValue(entity)?.ToString() ?? "");
        return string.Join("|", parts);
    }

    /// <summary>
    /// 导入时解析 WorkOrder.OrderItemIds（"D26Z2117001|1;D26Z2117001|2" → "1,2,3"）
    /// </summary>
    private async Task<string> ResolveOrderItemIdsForImportAsync(string compositeKeys)
    {
        if (string.IsNullOrWhiteSpace(compositeKeys))
            return "";

        var pairs = compositeKeys.Split(';', StringSplitOptions.RemoveEmptyEntries);
        if (pairs.Length == 0)
            return "";

        var orderItemIds = new List<int>();

        foreach (var pair in pairs)
        {
            var parts = pair.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) continue;

            var orderNo = parts[0].Trim();
            if (!int.TryParse(parts[1].Trim(), out var sequence))
                continue;

            var oi = await _context.Set<OrderItem>()
                .FirstOrDefaultAsync(o => o.SalesOrder.OrderNumber == orderNo && o.Sequence == sequence);

            if (oi != null)
                orderItemIds.Add(oi.Sequence);
        }

        return string.Join(",", orderItemIds);
    }

    private async Task<bool> ImportRowAsync(EntityDef def, ImportRowData row,
        Dictionary<string, Dictionary<string, int>> fkCache, string? userName,
        Dictionary<string, object> existingCache,
        Dictionary<string, HashSet<string>> pendingCodes)
    {
        var entityType = def.Type;
        var dbSet = _context.GetType().GetMethod("Set", Type.EmptyTypes)!
            .MakeGenericMethod(entityType)
            .Invoke(_context, null)!;
        var propertyCache = BuildPropertyCache(def);

        // 查找已存在的记录（从预加载缓存中查找，支持单键和复合键）
        object? existingEntity = null;
        var rowKey = GetRowKey(def, row);
        if (rowKey != null)
        {
            existingCache.TryGetValue(rowKey, out existingEntity);
        }

        // 带 ID 但库中不存在该记录 → 报错（防止静默变新增导致重复）
        if (existingEntity == null && rowKey != null && rowKey.StartsWith("__ID__:"))
            throw new BusinessException($"ID 为 {rowKey[7..]} 的记录在数据库中不存在，无法覆盖");

        object entity;
        if (existingEntity != null)
        {
            entity = existingEntity;
        }
        else
        {
            entity = Activator.CreateInstance(entityType)!;

            // 自动生成系统编码（如 SupplierCode → SU0001）
            foreach (var sysCol in def.Columns.Where(c => c.IsSystem && c.Property != null && DataExchangeRegistry.CodePrefixMap.ContainsKey(c.Property)))
            {
                if (sysCol.Property != null && propertyCache.TryGetValue(sysCol.Property, out var codeProp) && codeProp.CanWrite)
                {
                    var prefix = DataExchangeRegistry.CodePrefixMap[sysCol.Property];

                    // 查询数据库中所有已有编码
                    var dbCodes = await ((IQueryable)dbSet).Cast<BaseEntity>()
                        .Select(e => EF.Property<string>(e, sysCol.Property))
                        .ToListAsync();

                    // 合并批次内已分配的编码
                    var allCodes = dbCodes.Concat(pendingCodes[sysCol.Property]).ToList();

                    // 计算下一个可用编码
                    var matchingCodes = allCodes.Where(c => c.StartsWith(prefix) && c.Length == 6)
                        .OrderByDescending(c => c)
                        .ToList();
                    var maxCode = matchingCodes.FirstOrDefault();
                    var newCode = maxCode == null
                        ? $"{prefix}0001"
                        : $"{prefix}{int.Parse(maxCode[2..]) + 1:D4}";

                    pendingCodes[sysCol.Property].Add(newCode);
                    codeProp.SetValue(entity, newCode);
                }
            }
            // 更新缓存，避免同批次内重复行再次创建新实体
            if (existingEntity == null && rowKey != null)
            {
                existingCache[rowKey] = entity;
            }
        }

        // 设置审计字段
        var now = DateTimeOffset.Now;
        if (entity is BaseEntity be)
        {
            if (existingEntity == null)
            {
                be.CreatedTime = now;
                be.CreatedBy = userName ?? "system";
            }
            be.UpdatedTime = now;
            be.UpdatedBy = userName ?? "system";

        }

        // 设置属性值
        foreach (var colDef in def.Columns)
        {
            if (colDef.IsSystem || colDef.IsFkColumn) continue;

            if (!row.Values.TryGetValue(colDef.Header, out var cellValue))
                continue;

            if (string.IsNullOrWhiteSpace(cellValue))
            {
                if (colDef.Property != null && propertyCache.TryGetValue(colDef.Property, out var nullProp))
                {
                    if (nullProp.PropertyType.IsGenericType &&
                        nullProp.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        nullProp.SetValue(entity, null);
                    }
                    else if (nullProp.PropertyType == typeof(string))
                    {
                        // 非可空 string 字段空值时设为空字符串
                        nullProp.SetValue(entity, "");
                    }
                }
                continue;
            }

            if (colDef.Property == null || !propertyCache.TryGetValue(colDef.Property, out var prop))
                continue;

            var value = ConvertValue(cellValue, prop.PropertyType, colDef);

            // 特殊处理：WorkOrder.OrderItemIds → 将"订单号|项次号"解析回内部ID
            if (colDef.Property == "OrderItemIds")
            {
                value = await ResolveOrderItemIdsForImportAsync(cellValue);
            }

            prop.SetValue(entity, value);
        }

        // 解析FK列
        ResolveForeignKeys(def, row, fkCache, entity, propertyCache);

        // 验证FK列：用户提供了值但FK查找失败的列，记录为行错误
        // 此处逻辑与 PreviewAsync 保持一致：通过 fkCache 直接校验，不依赖 FkTargetProperty
        var unresolvedFkColumns = new List<string>();
        foreach (var colDef in def.Columns.Where(c => c.IsFkColumn && c.FkEntityKey != null && !c.FkRequiresJoin))
        {
            if (!row.Values.TryGetValue(colDef.Header, out var fkCellValue) || string.IsNullOrWhiteSpace(fkCellValue))
                continue;

            if (!fkCache.TryGetValue(colDef.FkEntityKey!, out var lookup) || !lookup.ContainsKey(fkCellValue))
            {
                unresolvedFkColumns.Add(colDef.Header);
            }
        }
        if (unresolvedFkColumns.Count > 0)
        {
            var fkNames = string.Join("、", unresolvedFkColumns);
            throw new BusinessException($"外键解析失败，在数据库中找不到对应的引用记录: {fkNames}");
        }

        // 添加新实体到DbContext
        if (existingEntity == null)
        {
            var addMethod = dbSet.GetType().GetMethod("Add");
            addMethod?.Invoke(dbSet, new[] { entity });
        }
        // 注意：不在此处SaveChanges，由ImportAsync批量保存
        return true;
    }

    private void ResolveForeignKeys(EntityDef def, ImportRowData row,
        Dictionary<string, Dictionary<string, int>> fkCache, object entity,
        Dictionary<string, PropertyInfo> propertyCache)
    {
        foreach (var colDef in def.Columns.Where(c => c.IsFkColumn))
        {
            if (!row.Values.TryGetValue(colDef.Header, out var cellValue) || string.IsNullOrWhiteSpace(cellValue))
                continue;

            if (colDef.FkEntityKey == null) continue;

            // 特殊处理：OrderItem 复合键（订单号|项次号）
            if (colDef.FkRequiresJoin && colDef.FkEntityKey == "OrderItem")
            {
                var orderNo = row.Values.GetValueOrDefault("订单号", "");
                var seq = row.Values.GetValueOrDefault("项次号", "");
                var compositeKey = $"{orderNo}|{seq}";

                if (fkCache.TryGetValue("OrderItem", out var oiCache) && oiCache.TryGetValue(compositeKey, out var oiId))
                {
                    if (colDef.FkTargetProperty != null && propertyCache.TryGetValue(colDef.FkTargetProperty, out var oiProp))
                        oiProp.SetValue(entity, oiId);
                }
                // FK列同时有属性名时，将源文本值也写入实体属性（用于覆盖导入匹配）
                if (colDef.Property != null && propertyCache.TryGetValue(colDef.Property, out var valProp))
                {
                    var convertedValue = ConvertValue(cellValue, valProp.PropertyType, colDef);
                    valProp.SetValue(entity, convertedValue);
                }
                continue;
            }

            // 特殊处理：ProcessGroup 复合键
            // 实体有 SectionName 属性（ProductionRecord/SectionOutsource/ProcessInspection）→ 按"批次号+工序名称+制造规格+工段名称"匹配
            if (colDef.FkRequiresJoin && colDef.FkEntityKey == "ProcessGroup")
            {
                if (propertyCache.ContainsKey("SectionName"))
                {
                    var batchNo = row.Values.GetValueOrDefault("批次号", "");
                    var processName = row.Values.GetValueOrDefault("工序名称", "");
                    var manufacturingSpec = row.Values.GetValueOrDefault("制造规格", "");
                    var sectionName = row.Values.GetValueOrDefault("工段名称", "");
                    var resolved = false;
                    if (!string.IsNullOrWhiteSpace(batchNo) && !string.IsNullOrWhiteSpace(sectionName))
                    {
                        var compositeKey = $"{batchNo}|{processName}|{manufacturingSpec}|{sectionName}";
                        if (fkCache.TryGetValue("ProcessGroupIdBySection", out var idCache) &&
                            idCache.TryGetValue(compositeKey, out var pgId) &&
                            fkCache.TryGetValue("ProcessGroupSeqBySection", out var seqCache) &&
                            seqCache.TryGetValue(compositeKey, out var seqNum))
                        {
                            if (propertyCache.TryGetValue("ProcessGroupId", out var pgProp))
                                pgProp.SetValue(entity, pgId);
                            if (propertyCache.TryGetValue("SequenceNumber", out var seqProp))
                                seqProp.SetValue(entity, seqNum);
                            resolved = true;
                        }
                    }
                    // 回退：按 BatchNo|SequenceNumber（组内序号）简单键查找
                    // 当工序名称/制造规格在 Excel 与数据库不完全一致时，直接用批次号+组内序号定位
                    if (!resolved && !string.IsNullOrWhiteSpace(batchNo) && !string.IsNullOrWhiteSpace(cellValue))
                    {
                        var simpleKey = $"{batchNo}|{cellValue}";
                        if (fkCache.TryGetValue("ProcessGroup", out var pgCache) && pgCache.TryGetValue(simpleKey, out var pgId))
                        {
                            if (colDef.FkTargetProperty != null && propertyCache.TryGetValue(colDef.FkTargetProperty, out var pgProp))
                                pgProp.SetValue(entity, pgId);
                            if (int.TryParse(cellValue, out var seqNum) && propertyCache.TryGetValue("SequenceNumber", out var seqProp))
                                seqProp.SetValue(entity, seqNum);
                        }
                    }
                }
                else
                {
                    // 无 SectionName 属性：按 BatchNo|SequenceNumber 复合键查找
                    var batchNo = row.Values.GetValueOrDefault("批次号", "");
                    var seq = row.Values.GetValueOrDefault("工序序号", "");
                    var compositeKey = $"{batchNo}|{seq}";
                    if (fkCache.TryGetValue("ProcessGroup", out var pgCache) && pgCache.TryGetValue(compositeKey, out var pgId))
                    {
                        if (colDef.FkTargetProperty != null && propertyCache.TryGetValue(colDef.FkTargetProperty, out var pgProp))
                            pgProp.SetValue(entity, pgId);
                        if (int.TryParse(seq, out var seqNum) && propertyCache.TryGetValue("SequenceNumber", out var seqProp))
                            seqProp.SetValue(entity, seqNum);
                    }
                }
                continue;
            }

            // 特殊处理：PicklingInRecord 复合键（入缸批次号|入缸工段 → PicklingInRecordId）
            if (colDef.FkRequiresJoin && colDef.FkEntityKey == "PicklingInRecord")
            {
                var batchNo = row.Values.GetValueOrDefault("入缸批次号", "");
                var sectionName = row.Values.GetValueOrDefault("入缸工段", "");
                var compositeKey = $"{batchNo}|{sectionName}";

                if (fkCache.TryGetValue("PicklingInRecord", out var pkCache) && pkCache.TryGetValue(compositeKey, out var pkId))
                {
                    if (colDef.FkTargetProperty != null && propertyCache.TryGetValue(colDef.FkTargetProperty, out var pkProp))
                        pkProp.SetValue(entity, pkId);
                }
                continue;
            }

            // 特殊处理：SectionOutsource 复合键（批次号|工段名称|委外单位）
            if (colDef.FkRequiresJoin && colDef.FkEntityKey == "SectionOutsource")
            {
                var batchNo = row.Values.GetValueOrDefault("批次号", "");
                var sectionName = row.Values.GetValueOrDefault("工段名称", "");
                var vendor = row.Values.GetValueOrDefault("委外单位", "");
                var compositeKey = $"{batchNo}|{sectionName}|{vendor}";

                if (fkCache.TryGetValue("SectionOutsource", out var soCache) && soCache.TryGetValue(compositeKey, out var soId))
                {
                    if (colDef.FkTargetProperty != null && propertyCache.TryGetValue(colDef.FkTargetProperty, out var soProp))
                        soProp.SetValue(entity, soId);
                }
                continue;
            }

            // 常规FK解析
            if (fkCache.TryGetValue(colDef.FkEntityKey, out var lookup) && lookup.TryGetValue(cellValue, out var fkId))
            {
                if (colDef.FkTargetProperty != null && propertyCache.TryGetValue(colDef.FkTargetProperty, out var fkProp))
                    fkProp.SetValue(entity, fkId);
                // FK列同时有属性名时，将源文本值也写入实体属性
                if (colDef.Property != null && propertyCache.TryGetValue(colDef.Property, out var valProp))
                {
                    var convertedValue = ConvertValue(cellValue, valProp.PropertyType, colDef);
                    valProp.SetValue(entity, convertedValue);
                }
            }
        }
    }

    private object ConvertValue(string value, Type targetType, ColumnDef colDef)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                return null!;
            if (targetType == typeof(string))
                return value;
            throw new BusinessException($"值不能为空 (期望类型: {targetType.Name})");
        }

        // 自定义值转换器
        if (colDef.ValueConverter != null)
            return colDef.ValueConverter(value);

        // 枚举类型
        if (colDef.IsEnum && colDef.EnumType != null)
        {
            var enumValue = EnumHelper.Parse(value, colDef.EnumType);
            // 实体属性为 string（字符串存储的枚举，如 LifecycleStatus/UsageType/Priority），返回枚举名称
            if (targetType == typeof(string))
                return enumValue.ToString()!;
            return enumValue;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(string)) return value;
        if (underlyingType == typeof(int)) return int.Parse(value);
        if (underlyingType == typeof(decimal)) return decimal.Parse(value);
        if (underlyingType == typeof(double)) return double.Parse(value);
        if (underlyingType == typeof(DateTime)) return DateTime.Parse(value);
        if (underlyingType == typeof(bool))
        {
            if (value == "是" || value.ToLower() == "true" || value == "1") return true;
            if (value == "否" || value.ToLower() == "false" || value == "0") return false;
            return bool.Parse(value);
        }

        return value;
    }

    protected virtual async Task<(IDbContextTransaction transaction, DbTransaction? dbTransaction)> BeginImportTransactionAsync()
    {
        var t = await _context.Database.BeginTransactionAsync();
        DbTransaction? dbt;
        try
        {
            dbt = t.GetDbTransaction();
        }
        catch (InvalidOperationException)
        {
            dbt = null; // InMemory 等非关系型提供程序
        }
        return (t, dbt);
    }

    protected virtual async Task DisableAllConstraintsAsync(DbConnection connection, DbTransaction transaction)
    {
        var sql = @"
DECLARE @sql NVARCHAR(MAX) = ''
SELECT @sql = @sql + 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(parent_object_id) + '].[' + OBJECT_NAME(parent_object_id) + '] NOCHECK CONSTRAINT [' + name + '];' + CHAR(13)
FROM sys.foreign_keys
EXEC sp_executesql @sql";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = transaction;
        await cmd.ExecuteNonQueryAsync();

        _logger.LogInformation("已禁用所有外键约束");
    }

    protected virtual async Task<List<string>> EnableAndCheckConstraintsAsync(DbConnection connection, DbTransaction transaction)
    {
        var errors = new List<string>();

        // 启用所有约束
        var enableSql = @"
DECLARE @sql NVARCHAR(MAX) = ''
SELECT @sql = @sql + 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(parent_object_id) + '].[' + OBJECT_NAME(parent_object_id) + '] WITH CHECK CHECK CONSTRAINT [' + name + '];' + CHAR(13)
FROM sys.foreign_keys
EXEC sp_executesql @sql";

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = enableSql;
            cmd.Transaction = transaction;
            await cmd.ExecuteNonQueryAsync();
        }

        // 检查是否有违反约束的记录
        var checkSql = @"
SELECT
    OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id) AS TableName,
    fk.name AS ConstraintName
FROM sys.foreign_keys fk
WHERE fk.is_not_trusted = 1 OR fk.is_disabled = 1";

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = checkSql;
            cmd.Transaction = transaction;
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                errors.Add($"表 {reader.GetString(0)}, 约束 {reader.GetString(1)}");
            }
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("外键约束验证失败，发现 {Count} 个问题约束", errors.Count);
        }
        else
        {
            _logger.LogInformation("所有外键约束验证通过");
        }

        return errors;
    }

    private static string GetRollbackReason(Exception ex)
    {
        var inner = ex;
        while (inner.InnerException != null)
            inner = inner.InnerException;
        return inner.Message;
    }

    #endregion
}
