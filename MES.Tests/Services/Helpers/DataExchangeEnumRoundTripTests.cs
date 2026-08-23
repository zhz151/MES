using FluentAssertions;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// DataExchange 导入导出枚举 Round-trip 测试。
///
/// DataExchange 导出时：EnumHelper.GetDisplayName() → Excel 中文
/// DataExchange 导入时：Excel 中文 → EnumHelper.Parse() → 枚举英文名
///
/// 此测试验证所有注册枚举的完整双向转换链路安全。
/// </summary>
public class DataExchangeEnumRoundTripTests
{
    /// <summary>
    /// DataExchange 注册表中标记为 isEnum: true 且通过
    /// EnumHelper 进行中英文转换的所有枚举类型。
    /// </summary>
    private static readonly (Type Type, string SampleEnglish, string SampleChinese)[] EnumRoundTripSamples =
    {
        (typeof(WorkOrderStatus), "NotGenerated", "未编制"),
        (typeof(WorkOrderStatus), "Confirmed", "已确定"),
        (typeof(MaterialPlanStatus), "NotPlanned", "未计划"),
        (typeof(InventoryPlanStatus), "Planned", "已计划"),
        (typeof(LengthStatus), "Fixed", "定尺"),
        (typeof(LengthStatus), "Range", "范围尺"),
        (typeof(LengthStatus), "NonFixed", "非定尺"),
        (typeof(DeliveryState), "SolutionAnnealedAndPickled", "固溶酸洗"),
        (typeof(DeliveryState), "Bright", "光亮"),
        (typeof(DeliveryState), "Hard", "硬态"),
        (typeof(SettlementMethod), "Theoretical", "理算"),
        (typeof(SettlementMethod), "Weighing", "过磅"),
        (typeof(SalesOrderStatus), "Pending", "待处理"),
        (typeof(PipeManufacturingType), "SeamlessPipe", "无缝管"),
        (typeof(PipeManufacturingType), "WeldedPipe", "焊管"),
        (typeof(ReworkType), "EmptyDrawing", "空拉改制"),
        (typeof(FinishedProductType), "Critical", "临界成品"),
        (typeof(ProductionType), "RoughTube", "荒管生产"),
        (typeof(MaterialType), "OrderFinished", "订单成品"),
        (typeof(MaterialType), "RoundBar", "圆棒"),
        (typeof(OutboundType), "ProductionPick", "生产领用"),
        (typeof(CustomerStatus), "Active", "启用"),
        (typeof(RequirementType), "Normal", "普通"),
        (typeof(NotificationType), "NewMaterial", "新物料确认"),
        (typeof(BatchStatus), "None", "未产"),
        (typeof(PurchaseOrderStatus), "Open", "已下单"),
        (typeof(SubcontractOrderStatus), "Sent", "已发出"),
        (typeof(SectionOutsourceStatus), "PendingRecovery", "待回收"),
        (typeof(RepairPriority), "Normal", "普通"),
        (typeof(LifecycleStatus), "Active", "在用"),
        (typeof(UsageType), "Primary", "主生产设备"),
        (typeof(RunningStatus), "Normal", "正常"),
        (typeof(RepairOrderStatus), "Pending", "待维修"),
        (typeof(EquipmentTaskStatus), "NotApplicable", "不适用"),
        (typeof(TaskOrderStatus), "Pending", "待执行"),
        (typeof(SubcontractOrderStatus), "Sent", "已发出"),
        (typeof(InspectionItem), "PMIInspection", "PMI检验"),
        (typeof(DisposalMethod), "Rework", "返整"),
        (typeof(NcrStatus), "Pending", "待处理"),
        (typeof(PicklingStatus), "Soaking", "浸泡中"),
        (typeof(SeverityLevel), "Critical", "严重"),
        (typeof(VerifyResult), "Passed", "通过"),
        (typeof(MaterialType), "RoughTube", "荒管"),
        (typeof(SectionStatus), "Completed", "已完成"),
    };

    #region 导出路径：枚举 → 中文显示名

    /// <summary>
    /// 验证 DataExchange 导出路径：
    /// 每个枚举英文名 → EnumHelper.GetDisplayName() → 正确的中文显示文本。
    ///
    /// 模拟 DataExportService 中的转换逻辑：
    ///   cellValue = EnumHelper.GetDisplayName(enumType, parsedEnum)
    /// </summary>
    [Theory]
    [MemberData(nameof(ExportTestData))]
    public void 导出_枚举转中文(Type enumType, string englishName, string expectedChinese)
    {
        var enumVal = Enum.Parse(enumType, englishName);
        var display = EnumHelper.GetDisplayName(enumType, enumVal);
        display.Should().Be(expectedChinese,
            $"枚举 {enumType.Name}.{englishName} 的导出中文显示名应为 \"{expectedChinese}\"");
    }

    public static IEnumerable<object[]> ExportTestData =>
        EnumRoundTripSamples.Select(s => new object[] { s.Type, s.SampleEnglish, s.SampleChinese });

    #endregion

    #region 导入路径：中文显示名 → 枚举英文名（string 存储）

    /// <summary>
    /// 验证 DataExchange 导入路径（string 存储场景）：
    /// Excel 中文 → EnumHelper.Parse() → 枚举对象 → .ToString() → 英文枚举名。
    ///
    /// 模拟 DataImportService ConvertValue 中的转换逻辑（targetType == typeof(string)）：
    ///   var enumObj = EnumHelper.Parse(cellText, colDef.EnumType);
    ///   return enumObj.ToString(); // "SeamlessPipe"
    /// </summary>
    [Theory]
    [MemberData(nameof(ImportFromChineseTestData))]
    public void 导入_中文转枚举字符串(Type enumType, string chineseText, string expectedEnglish)
    {
        // 模拟 DataImportService 导入路径：中文 → Parse → ToString
        var parsed = EnumHelper.Parse(chineseText, enumType);
        parsed.Should().NotBeNull();

        var resultString = parsed.ToString()!;
        resultString.Should().Be(expectedEnglish,
            $"枚举 {enumType.Name} 导入中文 \"{chineseText}\" 应转为英文 \"{expectedEnglish}\"");
    }

    public static IEnumerable<object[]> ImportFromChineseTestData =>
        EnumRoundTripSamples.Select(s => new object[] { s.Type, s.SampleChinese, s.SampleEnglish });

    #endregion

    #region 导入路径：英文名 → 枚举英文名（直接兼容）

    /// <summary>
    /// 验证 DataExchange 导入路径（输入已是英文名）：
    /// Excel 英文 → EnumHelper.Parse() → 枚举对象 → .ToString() → 相同英文名。
    ///
    /// EnumHelper 注册时双向注册了 中文→枚举名 和 英文→枚举名，
    /// 因此 Excel 中填入英文名也应能被正确解析。
    /// </summary>
    [Theory]
    [MemberData(nameof(ImportFromEnglishTestData))]
    public void 导入_英文直接兼容(Type enumType, string englishName)
    {
        var parsed = EnumHelper.Parse(englishName, enumType);
        parsed.Should().NotBeNull();
        parsed.ToString().Should().Be(englishName);
    }

    public static IEnumerable<object[]> ImportFromEnglishTestData =>
        EnumRoundTripSamples.Select(s => new object[] { s.Type, s.SampleEnglish }).Distinct();

    #endregion

    #region 完整 Round-trip：枚举 → 中文 → 枚举

    /// <summary>
    /// 验证：枚举 → GetDisplayName → 中文 → Parse → 枚举
    /// 这是 DataExchange 完整的导出再导入 cycle。
    /// </summary>
    [Theory]
    [MemberData(nameof(RoundTripTestData))]
    public void 完整RoundTrip_枚举转中文再转枚举(Type enumType, string englishName)
    {
        // 原值
        var original = Enum.Parse(enumType, englishName);

        // 导出：枚举 → 中文显示名
        var chinese = EnumHelper.GetDisplayName(enumType, original);

        // 导入：中文 → 枚举
        var reparsed = EnumHelper.Parse(chinese, enumType);
        var reparsedEnum = (Enum)reparsed;

        // 验证 round-trip 一致性
        reparsedEnum.Should().Be(original,
            $"枚举 {enumType.Name}.{englishName} 的 round-trip 应一致: " +
            $"\"{englishName}\" → \"{chinese}\" → \"{Enum.GetName(enumType, reparsedEnum)}\"");
    }

    public static IEnumerable<object[]> RoundTripTestData =>
        EnumRoundTripSamples
            .Select(s => s.SampleEnglish)
            .Distinct()
            .SelectMany(eng =>
                EnumRoundTripSamples
                    .Where(s => s.SampleEnglish == eng)
                    .Select(s => new object[] { s.Type, s.SampleEnglish })
            )
            .Distinct();

    #endregion

    #region 容错：导入时中英文混合输入

    /// <summary>
    /// DataExchange 导入时允许用户在 Excel 中填写中文或英文，
    /// EnumHelper.Parse 应同时处理两种情况。
    /// </summary>
    [Fact]
    public void 导入_同字段中文英文混合输入()
    {
        // 中文输入
        var chineseResult = EnumHelper.Parse("定尺", typeof(LengthStatus));
        chineseResult.ToString().Should().Be("Fixed");

        // 英文输入（大小写不敏感）
        var englishResult = EnumHelper.Parse("fixed", typeof(LengthStatus));
        englishResult.ToString().Should().Be("Fixed");

        // 大写英文输入
        var upperResult = EnumHelper.Parse("FIXED", typeof(LengthStatus));
        upperResult.ToString().Should().Be("Fixed");
    }

    /// <summary>
    /// 验证：对 DataExchange 导入中不支持的输入，EnumHelper.Parse 抛出明确异常
    /// </summary>
    [Fact]
    public void 导入_无效输入_抛出明确异常()
    {
        var act = () => EnumHelper.Parse("不存在的值", typeof(LengthStatus));
        act.Should().Throw<ArgumentException>()
            .WithMessage("*无法识别*")
            .WithMessage("*定尺*")
            .WithMessage("*范围尺*");
    }

    #endregion
}
