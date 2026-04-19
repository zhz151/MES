// 文件路径: MES.Tools/ExcelImportService.cs
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using MES.Data;
using MES.Data.Entities;
using MES.Core.Enums;
using MES.Tools.Models;

namespace MES.Tools;

public class ExcelImportService
{
    private readonly AppDbContext _context;
    private readonly bool _skipExistingOrders;
    
    private readonly Dictionary<string, int> _customerCache = new();
    private readonly Dictionary<string, int> _standardCache = new();
    private readonly Dictionary<string, StandardGradeMapping> _gradeMappingCache = new();
    private readonly Dictionary<string, int> _orderIdCache = new();
    private readonly HashSet<string> _importedOrderNumbers = new();
    
    private int _maxCustomerCodeSeq = 0;

    public ExcelImportService(AppDbContext context, bool skipExistingOrders = true)
    {
        _context = context;
        _skipExistingOrders = skipExistingOrders;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public async Task<ImportResult> ImportAllAsync(string excelFolder)
    {
        var result = new ImportResult { Section = "完整导入" };

        try
        {
            await LoadCacheAsync();
            result.Log("缓存加载完成", ImportLogLevel.Success);

            // 1. 产品标准（同步方法）
            var standardFile = Path.Combine(excelFolder, "1产品标准列表.xlsx");
            if (File.Exists(standardFile))
            {
                var r = ImportProductStandards(standardFile);
                result.Merge(r);
                await _context.SaveChangesAsync();
                UpdateStandardCache();
                result.Log("产品标准已保存", ImportLogLevel.Success);
            }

            // 2. 牌号对照（同步方法）
            var gradeFile = Path.Combine(excelFolder, "1牌号对照表.xlsx");
            if (File.Exists(gradeFile))
            {
                var r = ImportGradeMappings(gradeFile);
                result.Merge(r);
                await _context.SaveChangesAsync();
                result.Log("牌号对照已保存", ImportLogLevel.Success);
            }

            // 3. 客户档案（同步方法）
            var customerFile = Path.Combine(excelFolder, "2销售员及往来单位.xlsx");
            if (File.Exists(customerFile))
            {
                var r = ImportCustomers(customerFile);
                result.Merge(r);
                await _context.SaveChangesAsync();
                UpdateCustomerCache();
                result.Log($"客户档案已保存，共 {_customerCache.Count} 条", ImportLogLevel.Success);
            }

            // 4. 订单主表（同步方法）
            var orderFile = Path.Combine(excelFolder, "3销售订单聚合根.xlsx");
            if (File.Exists(orderFile))
            {
                var r = ImportOrders(orderFile);
                result.Merge(r);
                await _context.SaveChangesAsync();
                result.Log("订单主表已保存", ImportLogLevel.Success);
            }

            // 5. 订单项次（同步方法）
            var itemFile = Path.Combine(excelFolder, "4销售订单实体.xlsx");
            if (File.Exists(itemFile))
            {
                var r = ImportOrderItems(itemFile);
                result.Merge(r);
                await _context.SaveChangesAsync();
                result.Log("订单项次已保存", ImportLogLevel.Success);
            }

            // 6. 产品要求（同步方法）
            var requirementFile = Path.Combine(excelFolder, "5技术要求.xlsx");
            if (File.Exists(requirementFile))
            {
                var r = ImportProductRequirements(requirementFile);
                result.Merge(r);
                await _context.SaveChangesAsync();
                result.Log("产品要求已保存", ImportLogLevel.Success);
            }

            result.Log("所有数据导入完成", ImportLogLevel.Success);
        }
        catch (DbUpdateException ex)
        {
            result.Log($"数据库更新失败: {ex.Message}", ImportLogLevel.Error);
            if (ex.InnerException != null)
            {
                result.Log($"内部错误: {ex.InnerException.Message}", ImportLogLevel.Error);
            }
            result.Success = false;
        }
        catch (Exception ex)
        {
            result.Log($"导入失败: {ex.Message}", ImportLogLevel.Error);
            if (ex.InnerException != null)
            {
                result.Log($"内部错误: {ex.InnerException.Message}", ImportLogLevel.Error);
            }
            result.Success = false;
        }

        return result;
    }

    private async Task LoadCacheAsync()
    {
        var customers = await _context.CustomerProfiles.Where(c => !c.IsDeleted).ToListAsync();
        foreach (var c in customers)
        {
            _customerCache[c.CustomerUnit] = c.Id;
            if (c.CustomerCode.StartsWith("KH") && c.CustomerCode.Length > 2)
            {
                if (int.TryParse(c.CustomerCode.Substring(2), out var seq) && seq > _maxCustomerCodeSeq)
                    _maxCustomerCodeSeq = seq;
            }
        }

        var standards = await _context.ProductionStandards.Where(s => !s.IsDeleted).ToListAsync();
        foreach (var s in standards) _standardCache[s.StandardCode] = s.Id;

        var grades = await _context.StandardGradeMappings.Where(g => !g.IsDeleted).ToListAsync();
        foreach (var g in grades) _gradeMappingCache[g.StandardGrade] = g;

        var orders = await _context.SalesOrders.Where(o => !o.IsDeleted).ToListAsync();
        foreach (var o in orders)
        {
            if (!string.IsNullOrEmpty(o.OrderNumber))
            {
                _importedOrderNumbers.Add(o.OrderNumber);
                _orderIdCache[o.OrderNumber] = o.Id;
            }
        }
        
        Console.WriteLine($"📌 缓存: 客户{_customerCache.Count}条, 标准{_standardCache.Count}条, 牌号{_gradeMappingCache.Count}条, 订单{_importedOrderNumbers.Count}条");
    }

    private void UpdateCustomerCache()
    {
        _customerCache.Clear();
        var customers = _context.CustomerProfiles.Where(c => !c.IsDeleted).ToList();
        foreach (var c in customers) _customerCache[c.CustomerUnit] = c.Id;
    }

    private void UpdateStandardCache()
    {
        _standardCache.Clear();
        var standards = _context.ProductionStandards.Where(s => !s.IsDeleted).ToList();
        foreach (var s in standards) _standardCache[s.StandardCode] = s.Id;
    }

    private ImportResult ImportProductStandards(string filePath)
    {
        var result = new ImportResult { Section = "产品标准" };
        using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets[0];
        var rowCount = worksheet.Dimension.Rows;

        for (int row = 1; row <= rowCount; row++)
        {
            try
            {
                var excelRow = ExcelProductStandardRow.FromExcelRow(worksheet.Cells[row, 1]);
                if (string.IsNullOrEmpty(excelRow.StandardCode)) continue;
                if (_standardCache.ContainsKey(excelRow.StandardCode)) 
                { 
                    result.Skipped++; 
                    continue; 
                }

                var entity = new ProductionStandard
                {
                    StandardCode = excelRow.StandardCode,
                    StandardName = excelRow.StandardName,
                    SortOrder = row,
                    IsActive = true
                };
                _context.ProductionStandards.Add(entity);
                result.Inserted++;
            }
            catch (Exception ex) 
            { 
                result.Failed++; 
                result.Log($"行{row}错误: {ex.Message}", ImportLogLevel.Error); 
            }
        }
        result.Log($"完成: 新增{result.Inserted}, 跳过{result.Skipped}", ImportLogLevel.Success);
        return result;
    }

    private ImportResult ImportGradeMappings(string filePath)
    {
        var result = new ImportResult { Section = "牌号对照" };
        using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets[0];
        var rowCount = worksheet.Dimension.Rows;

        for (int row = 1; row <= rowCount; row++)
        {
            try
            {
                var excelRow = ExcelGradeMappingRow.FromExcelRow(worksheet.Cells[row, 1]);
                if (string.IsNullOrEmpty(excelRow.StandardGrade)) continue;
                if (_gradeMappingCache.ContainsKey(excelRow.StandardGrade)) 
                { 
                    result.Skipped++; 
                    continue; 
                }

                var entity = new StandardGradeMapping
                {
                    StandardGrade = excelRow.StandardGrade,
                    PlantGrade = excelRow.PlantGrade,
                    Density = excelRow.Density > 0 ? excelRow.Density : 7.93m,
                    HeatTreatment = excelRow.HeatTreatment,
                    SpecialMaterial = excelRow.SpecialMaterial,
                    SpecialNote = excelRow.SpecialNote
                };
                _context.StandardGradeMappings.Add(entity);
                _gradeMappingCache[excelRow.StandardGrade] = entity;
                result.Inserted++;
            }
            catch (Exception ex) 
            { 
                result.Failed++; 
                result.Log($"行{row}错误: {ex.Message}", ImportLogLevel.Error); 
            }
        }
        result.Log($"完成: 新增{result.Inserted}, 跳过{result.Skipped}", ImportLogLevel.Success);
        return result;
    }

    private ImportResult ImportCustomers(string filePath)
    {
        var result = new ImportResult { Section = "客户档案" };
        using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets[0];
        var rowCount = worksheet.Dimension.Rows;
        var processedUnits = new HashSet<string>();
        
        var nextSeq = _maxCustomerCodeSeq + 1;

        for (int row = 1; row <= rowCount; row++)
        {
            try
            {
                var excelRow = ExcelCustomerRow.FromExcelRow(worksheet.Cells[row, 1]);
                if (string.IsNullOrEmpty(excelRow.CustomerUnit) || processedUnits.Contains(excelRow.CustomerUnit)) 
                    continue;
                processedUnits.Add(excelRow.CustomerUnit);

                if (_customerCache.ContainsKey(excelRow.CustomerUnit)) 
                { 
                    result.Skipped++; 
                    continue; 
                }

                var customerCode = $"KH{nextSeq:D5}";
                nextSeq++;
                
                var entity = new CustomerProfile
                {
                    CustomerCode = customerCode,
                    CustomerUnit = excelRow.CustomerUnit,
                    Salesman = string.IsNullOrEmpty(excelRow.Salesman) ? "系统导入" : excelRow.Salesman,
                    EndCustomer = excelRow.EndCustomer,
                    Status = CustomerStatus.Active
                };
                _context.CustomerProfiles.Add(entity);
                result.Inserted++;
            }
            catch (Exception ex) 
            { 
                result.Failed++; 
                result.Log($"行{row}错误: {ex.Message}", ImportLogLevel.Error); 
            }
        }
        
        result.Log($"完成: 新增{result.Inserted}, 跳过{result.Skipped}", ImportLogLevel.Success);
        return result;
    }

    private ImportResult ImportOrders(string filePath)
    {
        var result = new ImportResult { Section = "订单主表" };
        using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets[0];
        var rowCount = worksheet.Dimension.Rows;

        Console.WriteLine($"\n📌 开始读取订单主表，共 {rowCount} 行数据\n");

        for (int row = 1; row <= rowCount; row++)
        {
            try
            {
                var excelRow = ExcelOrderRow.FromExcelRow(worksheet.Cells[row, 1]);
                
                if (string.IsNullOrEmpty(excelRow.OrderNumber)) continue;

                if (!excelRow.OrderNumber.StartsWith("D"))
                {
                    result.Log($"行{row}: 订单号格式异常 '{excelRow.OrderNumber}'，跳过", ImportLogLevel.Warning);
                    result.Skipped++;
                    continue;
                }

                if (_importedOrderNumbers.Contains(excelRow.OrderNumber))
                {
                    if (_skipExistingOrders) 
                    { 
                        result.Skipped++; 
                        continue; 
                    }
                }

                // 安全获取客户单元名称
                var customerUnit = excelRow.CustomerUnit ?? string.Empty;
                
                if (!_customerCache.TryGetValue(customerUnit, out var customerId))
                {
                    // 模糊匹配（避免 null 引用）
customerUnit = customerUnit ?? string.Empty;

var matchedCustomer = _customerCache.Keys
    .OfType<string>()  // 过滤掉 null 键，并让编译器知道返回的是 string
    .FirstOrDefault(c => 
        c.Contains(customerUnit, StringComparison.Ordinal) || 
        customerUnit.Contains(c, StringComparison.Ordinal));
                    
                    if (matchedCustomer != null)
                    {
                        customerId = _customerCache[matchedCustomer];
                        Console.WriteLine($"行{row}: 模糊匹配客户 '{customerUnit}' -> '{matchedCustomer}'");
                    }
                    else
                    {
                        result.Log($"客户不存在: {customerUnit}", ImportLogLevel.Warning);
                        result.Failed++;
                        continue;
                    }
                }

                var entity = new SalesOrder
                {
                    OrderNumber = excelRow.OrderNumber,
                    SignDate = excelRow.SignDate,
                    CustomerId = customerId,
                    Status = excelRow.IsDeleted ? SalesOrderStatus.Cancelled : SalesOrderStatus.Pending
                };
                _context.SalesOrders.Add(entity);
                _importedOrderNumbers.Add(excelRow.OrderNumber);
                result.Inserted++;
                
                if (result.Inserted % 20 == 0)
                {
                    Console.WriteLine($"   已处理 {result.Inserted} 条订单...");
                }
            }
            catch (Exception ex) 
            { 
                result.Failed++; 
                result.Log($"行{row}错误: {ex.Message}", ImportLogLevel.Error); 
            }
        }
        result.Log($"完成: 新增{result.Inserted}, 跳过{result.Skipped}, 失败{result.Failed}", ImportLogLevel.Success);
        return result;
    }

    private ImportResult ImportOrderItems(string filePath)
    {
        var result = new ImportResult { Section = "订单项次" };
        using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets[0];
        var rowCount = worksheet.Dimension.Rows;

        // 重新加载订单 ID 缓存（确保包含刚刚导入的订单）
        var orders = _context.SalesOrders.Where(o => !o.IsDeleted).ToList();
        foreach (var o in orders) _orderIdCache[o.OrderNumber] = o.Id;
        
        Console.WriteLine($"\n📌 开始读取订单项次，共 {rowCount} 行数据\n");

        for (int row = 1; row <= rowCount; row++)
        {
            try
            {
                var excelRow = ExcelOrderItemRow.FromExcelRow(worksheet.Cells[row, 1]);
                
                if (excelRow.IsDeleted) 
                { 
                    result.Skipped++; 
                    continue; 
                }
                
                if (string.IsNullOrEmpty(excelRow.OrderNumber)) continue;

                if (!_orderIdCache.TryGetValue(excelRow.OrderNumber, out var orderId))
                {
                    result.Log($"订单不存在: {excelRow.OrderNumber}", ImportLogLevel.Warning);
                    result.Skipped++;
                    continue;
                }

                if (!_standardCache.TryGetValue(excelRow.ProductStandard, out var standardId))
                {
                    result.Log($"标准不存在: {excelRow.ProductStandard}", ImportLogLevel.Warning);
                    result.Failed++;
                    continue;
                }

                if (!_gradeMappingCache.TryGetValue(excelRow.StandardGrade, out var gradeMapping))
                {
                    var aliasMatch = _gradeMappingCache.Keys.FirstOrDefault(k => 
                        k.Equals(excelRow.StandardGrade, StringComparison.OrdinalIgnoreCase));
                    if (aliasMatch != null)
                        gradeMapping = _gradeMappingCache[aliasMatch];
                    else
                    {
                        result.Log($"牌号不存在: {excelRow.StandardGrade}", ImportLogLevel.Warning);
                        result.Failed++;
                        continue;
                    }
                }

                var settlementMethod = ParseSettlementMethod(excelRow.SettlementMethod);
                var materialName = ParseMaterialName(excelRow.MaterialName);
                var deliveryState = ParseDeliveryState(excelRow.DeliveryState);
                var lengthStatus = ParseLengthStatus(excelRow.LengthStatus);

                var meters = excelRow.Meters;
                if (lengthStatus == LengthStatus.Fixed && excelRow.Quantity.HasValue && excelRow.MaxLength.HasValue)
                {
                    meters = excelRow.MaxLength.Value * excelRow.Quantity.Value / 1000;
                }

                var entity = new OrderItem
                {
                    SalesOrderId = orderId,
                    Sequence = excelRow.Sequence,
                    DeliveryDate = excelRow.DeliveryDate,
                    DelayPenalty = excelRow.DelayPenalty,
                    SettlementMethod = settlementMethod,
                    MaterialName = materialName,
                    ProductionStandardId = standardId,
                    DeliveryState = deliveryState,
                    StandardGrade = excelRow.StandardGrade,
                    PlantGrade = gradeMapping.PlantGrade,
                    Density = gradeMapping.Density,
                    OuterDiameter = excelRow.OuterDiameter,
                    WallThickness = excelRow.WallThickness,
                    Specification = $"{excelRow.OuterDiameter}*{excelRow.WallThickness}",
                    OuterDiameterNegative = excelRow.OuterDiameterNegative,
                    OuterDiameterPositive = excelRow.OuterDiameterPositive,
                    WallThicknessNegative = excelRow.WallThicknessNegative,
                    WallThicknessPositive = excelRow.WallThicknessPositive,
                    LengthStatus = lengthStatus,
                    MinLength = excelRow.MinLength,
                    MaxLength = excelRow.MaxLength,
                    Quantity = excelRow.Quantity,
                    Meters = meters,
                    ContractWeight = excelRow.ContractWeight,
                    TheoreticalWeight = excelRow.CalculatedWeight > 0 ? excelRow.CalculatedWeight : excelRow.ContractWeight
                };
                _context.OrderItems.Add(entity);
                result.Inserted++;
                
                if (result.Inserted % 100 == 0)
                {
                    Console.WriteLine($"   已处理 {result.Inserted} 条项次...");
                }
            }
            catch (Exception ex) 
            { 
                result.Failed++; 
                result.Log($"行{row}错误: {ex.Message}", ImportLogLevel.Error); 
            }
        }
        result.Log($"完成: 新增{result.Inserted}, 跳过{result.Skipped}, 失败{result.Failed}", ImportLogLevel.Success);
        return result;
    }

    private ImportResult ImportProductRequirements(string filePath)
    {
        var result = new ImportResult { Section = "产品要求" };
        using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets[0];
        var rowCount = worksheet.Dimension.Rows;

        // 获取所有订单项次
        var orderItems = _context.OrderItems
            .Include(oi => oi.SalesOrder)
            .Where(oi => !oi.IsDeleted)
            .ToList();
        
        var orderItemIdMap = new Dictionary<(string OrderNumber, int Sequence), int>();
        foreach (var oi in orderItems)
        {
            var orderNumber = oi.SalesOrder?.OrderNumber ?? string.Empty;
            if (!string.IsNullOrEmpty(orderNumber))
            {
                orderItemIdMap[(orderNumber, oi.Sequence)] = oi.Id;
            }
        }
        
        Console.WriteLine($"\n📌 开始读取产品要求，共 {rowCount} 行数据\n");
        
        var skippedCount = 0;
        var notFoundCount = 0;

        for (int row = 1; row <= rowCount; row++)
        {
            try
            {
                var excelRow = ExcelProductRequirementRow.FromExcelRow(worksheet.Cells[row, 1]);
                
                if (string.IsNullOrEmpty(excelRow.OrderNumber)) continue;
                
                if (!orderItemIdMap.TryGetValue((excelRow.OrderNumber, excelRow.Sequence), out var orderItemId))
                {
                    notFoundCount++;
                    continue;
                }
                
                var exists = _context.ProductRequirements
                    .Any(pr => pr.OrderItemId == orderItemId && !pr.IsDeleted);
                
                if (exists)
                {
                    skippedCount++;
                    continue;
                }
                
                var requirementType = excelRow.RequirementTypeValue == 1 
                    ? RequirementType.Special 
                    : RequirementType.Normal;
                
                var entity = new ProductRequirement
                {
                    OrderItemId = orderItemId,
                     RequirementType = requirementType,
                    ChemicalComposition = excelRow.ChemicalComposition,
                    MechanicalProperty = excelRow.MechanicalProperty,
                    ToleranceRequirement = excelRow.ToleranceRequirement,
                    SurfaceQuality = excelRow.SurfaceQuality,
                    NdtRequirement = excelRow.NdtRequirement,
                    OtherRequirement = excelRow.OtherRequirement
                };
                
                _context.ProductRequirements.Add(entity);
                result.Inserted++;
                
                if (result.Inserted % 100 == 0)
                {
                    Console.WriteLine($"   已处理 {result.Inserted} 条技术要求...");
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Log($"行{row}错误: {ex.Message}", ImportLogLevel.Error);
            }
        }
        
        result.Log($"完成: 新增{result.Inserted}, 跳过{skippedCount}, 项次不存在{notFoundCount}, 失败{result.Failed}", ImportLogLevel.Success);
        return result;
    }

    private static SettlementMethod ParseSettlementMethod(string value) => value switch
    {
        "理算" => SettlementMethod.Theoretical,
        "过磅-负" => SettlementMethod.WeighingNegative,
        "过磅" => SettlementMethod.Weighing,
        _ => SettlementMethod.Theoretical
    };

    private static MaterialName ParseMaterialName(string value) =>
        string.IsNullOrEmpty(value) ? MaterialName.SeamlessPipe :
        value.Contains("无缝") ? MaterialName.SeamlessPipe : MaterialName.WeldedPipe;

    private static DeliveryState ParseDeliveryState(string value) => value switch
    {
        "固溶酸洗" => DeliveryState.SolutionAnnealedAndPickled,
        "外抛光" => DeliveryState.SolutionAnnealedAndPickledExternalPolished,
        "内抛光" => DeliveryState.SolutionAnnealedAndPickledInternalPolished,
        "盘管" => DeliveryState.SolutionAnnealedAndPickledCoiled,
        "U型管" => DeliveryState.SolutionAnnealedAndPickledUTube,
        "光亮" => DeliveryState.Bright,
        _ => DeliveryState.SolutionAnnealedAndPickled
    };

    private static LengthStatus ParseLengthStatus(string value) => value switch
    {
        "Fixed" or "定尺" => LengthStatus.Fixed,
        "Range" or "范围尺" => LengthStatus.Range,
        _ => LengthStatus.NonFixed
    };
}