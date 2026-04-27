using OfficeOpenXml;

namespace MES.Tools.Models;

/// <summary>
/// 产品标准行 (产品标准.xlsx)
/// 列：StandardCode, StandardName, Remark, SortOrder, IsActive
/// </summary>
public class ExcelProductStandardRow
{
    public string StandardCode { get; set; } = string.Empty;
    public string? StandardName { get; set; }
    
    public static ExcelProductStandardRow FromExcelRow(ExcelRange row)
    {
        return new ExcelProductStandardRow
        {
            StandardCode = GetStringValue(row, 0),
            StandardName = GetStringValue(row, 1)
        };
    }
    
    private static string GetStringValue(ExcelRange cell, int offset) => cell.Offset(0, offset).Text?.Trim() ?? string.Empty;
}

/// <summary>
/// 牌号对照行 (牌号对照.xlsx)
/// 列：StandardGrade, PlantGrade, Density, HeatTreatment, SpecialMaterial, SpecialNote, Remark
/// </summary>
public class ExcelGradeMappingRow
{
    public string StandardGrade { get; set; } = string.Empty;
    public string PlantGrade { get; set; } = string.Empty;
    public decimal Density { get; set; }
    public string? HeatTreatment { get; set; }
    public bool SpecialMaterial { get; set; }
    public string? SpecialNote { get; set; }
    
    public static ExcelGradeMappingRow FromExcelRow(ExcelRange row)
    {
        var specialMaterialText = GetStringValue(row, 4);
        
        return new ExcelGradeMappingRow
        {
            StandardGrade = GetStringValue(row, 0),
            PlantGrade = GetStringValue(row, 1),
            Density = GetDecimalValue(row, 2),
            HeatTreatment = GetStringValue(row, 3),
            SpecialMaterial = specialMaterialText == "是" || specialMaterialText == "1",
            SpecialNote = GetStringValue(row, 5)
        };
    }
    
    private static string GetStringValue(ExcelRange cell, int offset) => cell.Offset(0, offset).Text?.Trim() ?? string.Empty;
    private static decimal GetDecimalValue(ExcelRange cell, int offset) => decimal.TryParse(cell.Offset(0, offset).Text, out var val) ? val : 0;
}

/// <summary>
/// 客户行 (客户档案.xlsx)
/// 列：CustomerCode, Salesman, CustomerUnit, EndCustomer, ContactPerson, ContactPhone, Address, Status, Remark
/// </summary>
public class ExcelCustomerRow
{
    public string CustomerCode { get; set; } = string.Empty;
    public string Salesman { get; set; } = string.Empty;
    public string CustomerUnit { get; set; } = string.Empty;
    public string? EndCustomer { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = string.Empty;  // Active/Inactive
    public string? Remark { get; set; }
    
    public static ExcelCustomerRow FromExcelRow(ExcelRange row)
    {
        return new ExcelCustomerRow
        {
            CustomerCode = GetStringValue(row, 0),
            Salesman = GetStringValue(row, 1),
            CustomerUnit = GetStringValue(row, 2),
            EndCustomer = GetStringValue(row, 3),
            ContactPerson = GetStringValue(row, 4),
            ContactPhone = GetStringValue(row, 5),
            Address = GetStringValue(row, 6),
            Status = GetStringValue(row, 7),
            Remark = GetStringValue(row, 8)
        };
    }
    
    private static string GetStringValue(ExcelRange cell, int offset) => cell.Offset(0, offset).Text?.Trim() ?? string.Empty;
}

/// <summary>
/// 订单主表行 (销售单.xlsx)
/// 列：OrderNumber, SignDate, Salesman, CustomerUnit, EndCustomer, Status
/// </summary>
public class ExcelOrderRow
{
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime SignDate { get; set; }
    public string Salesman { get; set; } = string.Empty;
    public string CustomerUnit { get; set; } = string.Empty;
    public string? EndCustomer { get; set; }
    public string Status { get; set; } = string.Empty;  // 订单状态：已确认/待处理/已取消
    
    /// <summary>
    /// 转换为 IsDeleted 标记（订单取消时 IsDeleted = true）
    /// </summary>
    public bool IsDeleted => Status == "已取消";
    
    public static ExcelOrderRow FromExcelRow(ExcelRange row)
    {
        var orderNumber = GetStringValue(row, 0);
        var signDateStr = GetStringValue(row, 1);
        var customerUnit = GetStringValue(row, 3);
        var status = GetStringValue(row, 5);
        
        if (row.Start.Row <= 6)
        {
            Console.WriteLine($"📌 [订单行{row.Start.Row}] 订单号='{orderNumber}', 签订日期='{signDateStr}', 客户='{customerUnit}', 状态='{status}'");
        }
        
        return new ExcelOrderRow
        {
            OrderNumber = orderNumber,
            SignDate = ParseDate(signDateStr),
            Salesman = GetStringValue(row, 2),
            CustomerUnit = customerUnit,
            EndCustomer = GetStringValue(row, 4),
            Status = status
        };
    }
    
    private static string GetStringValue(ExcelRange cell, int offset) => cell.Offset(0, offset).Text?.Trim() ?? string.Empty;
    
    private static DateTime ParseDate(string dateStr)
    {
        if (DateTime.TryParse(dateStr, out var date))
            return date;
        return DateTime.Now;
    }
}

/// <summary>
/// 订单项次行 (销售项次.xlsx)
/// 列：OrderNumber, Sequence, DeliveryDate, DelayPenalty, SettlementMethod, MaterialName, 
///     StandardCode, DeliveryState, StandardGrade, PlantGrade, Density, OuterDiameter, 
///     WallThickness, Specification, OuterDiameterNegative, OuterDiameterPositive, 
///     WallThicknessNegative, WallThicknessPositive, LengthStatus, MinLength, MaxLength, 
///     Quantity, Meters, ContractWeight, TheoreticalWeight, Remark
/// </summary>
public class ExcelOrderItemRow
{
    public string OrderNumber { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public DateTime DeliveryDate { get; set; }
    public bool DelayPenalty { get; set; }
    public string SettlementMethod { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string ProductStandard { get; set; } = string.Empty;
    public string DeliveryState { get; set; } = string.Empty;
    public string StandardGrade { get; set; } = string.Empty;
    public string PlantGrade { get; set; } = string.Empty;
    public decimal Density { get; set; }
    public decimal OuterDiameter { get; set; }
    public decimal WallThickness { get; set; }
    public string Specification { get; set; } = string.Empty;
    public decimal OuterDiameterNegative { get; set; }
    public decimal OuterDiameterPositive { get; set; }
    public decimal WallThicknessNegative { get; set; }
    public decimal WallThicknessPositive { get; set; }
    public string LengthStatus { get; set; } = string.Empty;
    public decimal? MinLength { get; set; }
    public decimal? MaxLength { get; set; }
    public int? Quantity { get; set; }
    public decimal? Meters { get; set; }
    public decimal ContractWeight { get; set; }
    public decimal CalculatedWeight { get; set; }
    public string? Remark { get; set; }
    
    /// <summary>
    /// 所有项次都是有效数据，没有 IsDeleted 标记
    /// </summary>
    public bool IsDeleted => false;
    
    public static ExcelOrderItemRow FromExcelRow(ExcelRange row)
    {
        var orderNumber = GetStringValue(row, 0);
        var sequence = GetIntValue(row, 1);
        
        if (row.Start.Row <= 6)
        {
            Console.WriteLine($"📌 [项次行{row.Start.Row}] 订单号='{orderNumber}', 项次={sequence}");
        }
        
        return new ExcelOrderItemRow
        {
            OrderNumber = orderNumber,
            Sequence = sequence,
            DeliveryDate = GetDateTimeValue(row, 2),
            DelayPenalty = GetIntValue(row, 3) == 1,
            SettlementMethod = GetStringValue(row, 4),
            MaterialName = GetStringValue(row, 5),
            ProductStandard = GetStringValue(row, 6),
            DeliveryState = GetStringValue(row, 7),
            StandardGrade = GetStringValue(row, 8),
            PlantGrade = GetStringValue(row, 9),
            Density = GetDecimalValue(row, 10),
            OuterDiameter = GetDecimalValue(row, 11),
            WallThickness = GetDecimalValue(row, 12),
            Specification = GetStringValue(row, 13),
            OuterDiameterNegative = GetDecimalValue(row, 14),
            OuterDiameterPositive = GetDecimalValue(row, 15),
            WallThicknessNegative = GetDecimalValue(row, 16),
            WallThicknessPositive = GetDecimalValue(row, 17),
            LengthStatus = GetStringValue(row, 18),
            MinLength = GetNullableDecimalValue(row, 19),
            MaxLength = GetNullableDecimalValue(row, 20),
            Quantity = GetNullableIntValue(row, 21),
            Meters = GetNullableDecimalValue(row, 22),
            ContractWeight = GetDecimalValue(row, 23),
            CalculatedWeight = GetDecimalValue(row, 24),
            Remark = GetStringValue(row, 25)
        };
    }
    
    private static string GetStringValue(ExcelRange cell, int offset) => cell.Offset(0, offset).Text?.Trim() ?? string.Empty;
    private static int GetIntValue(ExcelRange cell, int offset) => int.TryParse(cell.Offset(0, offset).Text, out var val) ? val : 0;
    private static decimal GetDecimalValue(ExcelRange cell, int offset) => decimal.TryParse(cell.Offset(0, offset).Text, out var val) ? val : 0;
    private static DateTime GetDateTimeValue(ExcelRange cell, int offset) => DateTime.TryParse(cell.Offset(0, offset).Text, out var val) ? val : DateTime.Now;
    private static decimal? GetNullableDecimalValue(ExcelRange cell, int offset) => decimal.TryParse(cell.Offset(0, offset).Text, out var val) ? val : null;
    private static int? GetNullableIntValue(ExcelRange cell, int offset) => int.TryParse(cell.Offset(0, offset).Text, out var val) ? val : null;
}

/// <summary>
/// 产品要求行 (技术要求.xlsx)
/// 列：OrderNumber, Sequence, RequirementType, ChemicalComposition, MechanicalProperty, 
///     ToleranceRequirement, SurfaceQuality, NdtRequirement, OtherRequirement
/// </summary>
public class ExcelProductRequirementRow
{
    public string OrderNumber { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public int RequirementTypeValue { get; set; }  // 0=普通, 1=特殊
    public string? ChemicalComposition { get; set; }
    public string? MechanicalProperty { get; set; }
    public string? ToleranceRequirement { get; set; }
    public string? SurfaceQuality { get; set; }
    public string? NdtRequirement { get; set; }
    public string? OtherRequirement { get; set; }
    
    public static ExcelProductRequirementRow FromExcelRow(ExcelRange row)
    {
        var orderNumber = GetStringValue(row, 0);
        var sequence = GetIntValue(row, 1);
        var requirementTypeText = GetStringValue(row, 2);  // 第3列：特殊/普通
        
        // 转换：特殊→1, 普通→0
        var requirementTypeVal = requirementTypeText == "特殊" ? 1 : 0;
        
        if (row.Start.Row <= 6 && !string.IsNullOrEmpty(orderNumber))
        {
            Console.WriteLine($"📌 [技术要求行{row.Start.Row}] 订单号='{orderNumber}', 项次={sequence}, 类型={(requirementTypeVal == 1 ? "特殊" : "常规")}");
        }
        
        return new ExcelProductRequirementRow
        {
            OrderNumber = orderNumber,
            Sequence = sequence,
            RequirementTypeValue = requirementTypeVal,
            ChemicalComposition = GetStringValue(row, 3),
            MechanicalProperty = GetStringValue(row, 4),
            ToleranceRequirement = GetStringValue(row, 5),
            SurfaceQuality = GetStringValue(row, 6),
            NdtRequirement = GetStringValue(row, 7),
            OtherRequirement = GetStringValue(row, 8)
        };
    }
    
    private static string GetStringValue(ExcelRange cell, int offset) => cell.Offset(0, offset).Text?.Trim() ?? string.Empty;
    private static int GetIntValue(ExcelRange cell, int offset) => int.TryParse(cell.Offset(0, offset).Text, out var val) ? val : 0;
}