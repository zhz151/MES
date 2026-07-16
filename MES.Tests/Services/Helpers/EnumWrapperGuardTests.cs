using FluentAssertions;
using MES.Blazor.Helpers;
using MES.Core.Enums;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// 枚举包装器防护测试。
/// 防止「新建/编辑」页面中枚举字段误存中文值到数据库。
///
/// 测试覆盖所有在 BatchCreate/BatchEdit 计算属性包装器(formXxx) 中使用的枚举类型，
/// 确保 getter/setter 转化安全，且 DisplayHelper.GetXxxText() 不会错误用于数据赋值。
/// </summary>
public class EnumWrapperGuardTests
{
    #region 非可空枚举包装器模式（formProductionType / formManufacturingItem）

    /// <summary>
    /// 非可空枚举包装器 getter 模式验证：
    /// string → Enum.Parse<T>(string) → T
    /// request.ProductionType == "RoughTube" → formProductionType == ProductionType.RoughTube
    /// </summary>
    [Theory]
    [InlineData(ProductionType.RoughTube)]
    [InlineData(ProductionType.InProcess)]
    [InlineData(ProductionType.Inventory)]
    [InlineData(ProductionType.OutsourcedPurchased)]
    [InlineData(ProductionType.Rework)]
    [InlineData(ProductionType.Subcontract)]
    [InlineData(ProductionType.ExternalProcessing)]
    public void ProductionType_包装器Get_英文名解析正确(ProductionType expected)
    {
        var dbString = expected.ToString(); // 模拟 DTO 中的英文值
        if (string.IsNullOrEmpty(dbString))
            return;

        var parsed = Enum.Parse<ProductionType>(dbString);
        parsed.Should().Be(expected);
    }

    /// <summary>
    /// 非可空枚举包装器 setter 模式验证：
    /// T → value.ToString() → string
    /// formProductionType = ProductionType.RoughTube → request.ProductionType == "RoughTube"
    /// </summary>
    [Theory]
    [InlineData(ProductionType.RoughTube, "RoughTube")]
    [InlineData(ProductionType.InProcess, "InProcess")]
    [InlineData(ProductionType.Inventory, "Inventory")]
    [InlineData(ProductionType.OutsourcedPurchased, "OutsourcedPurchased")]
    [InlineData(ProductionType.Rework, "Rework")]
    [InlineData(ProductionType.Subcontract, "Subcontract")]
    [InlineData(ProductionType.ExternalProcessing, "ExternalProcessing")]
    public void ProductionType_包装器Set_存入英文(ProductionType enumVal, string expectedDbString)
    {
        var dbString = enumVal.ToString();
        dbString.Should().Be(expectedDbString);
    }

    [Theory]
    [InlineData(ManufacturingItem.OrderFinishedProduct)]
    [InlineData(ManufacturingItem.PreparedMaterial)]
    [InlineData(ManufacturingItem.SurplusStock)]
    [InlineData(ManufacturingItem.SpecialDeliveryStatus)]
    public void ManufacturingItem_包装器Get_英文名解析正确(ManufacturingItem expected)
    {
        var dbString = expected.ToString();
        if (string.IsNullOrEmpty(dbString))
            return;

        var parsed = Enum.Parse<ManufacturingItem>(dbString);
        parsed.Should().Be(expected);
    }

    [Theory]
    [InlineData(ManufacturingItem.OrderFinishedProduct, "OrderFinishedProduct")]
    [InlineData(ManufacturingItem.PreparedMaterial, "PreparedMaterial")]
    [InlineData(ManufacturingItem.SurplusStock, "SurplusStock")]
    [InlineData(ManufacturingItem.SpecialDeliveryStatus, "SpecialDeliveryStatus")]
    public void ManufacturingItem_包装器Set_存入英文(ManufacturingItem enumVal, string expectedDbString)
    {
        enumVal.ToString().Should().Be(expectedDbString);
    }

    #endregion

    #region 可空枚举包装器模式（formMaterialName / formSettlementMethod / 等）

    /// <summary>
    /// 可空枚举包装器 getter 模式：string → Enum.Parse<T?>(string) → T?
    /// 非空字符串应正确解析
    /// </summary>
    [Theory]
    [InlineData(PipeManufacturingType.SeamlessPipe)]
    [InlineData(PipeManufacturingType.WeldedPipe)]
    public void PipeManufacturingType_包装器Get_英文名解析正确(PipeManufacturingType expected)
    {
        var dbString = expected.ToString();
        Enum.Parse<PipeManufacturingType>(dbString).Should().Be(expected);
    }

    /// <summary>
    /// 可空枚举包装器 getter 处理 null/空字符串 → null
    /// </summary>
    [Fact]
    public void 可空包装器_Null输入_返回Null()
    {
        // 模拟所有可空包装器的 getter 逻辑：
        // string.IsNullOrEmpty(x) ? null : Enum.Parse<T>(x)
        string? nullInput = null;
        var result = string.IsNullOrEmpty(nullInput) ? null : (PipeManufacturingType?)Enum.Parse<PipeManufacturingType>(nullInput);
        result.Should().BeNull();

        string emptyInput = "";
        var result2 = string.IsNullOrEmpty(emptyInput) ? null : (SettlementMethod?)Enum.Parse<SettlementMethod>(emptyInput);
        result2.Should().BeNull();
    }

    /// <summary>
    /// 可空包装器 setter 存入英文（非 null）
    /// </summary>
    [Theory]
    [InlineData(SettlementMethod.Weighing, "Weighing")]
    [InlineData(SettlementMethod.WeighingNegative, "WeighingNegative")]
    [InlineData(SettlementMethod.Theoretical, "Theoretical")]
    public void SettlementMethod_包装器Set_存入英文(SettlementMethod enumVal, string expectedDbString)
    {
        enumVal.ToString().Should().Be(expectedDbString);
    }

    [Theory]
    [InlineData(DeliveryState.SolutionAnnealedAndPickled, "SolutionAnnealedAndPickled")]
    [InlineData(DeliveryState.Bright, "Bright")]
    [InlineData(DeliveryState.Hard, "Hard")]
    [InlineData(DeliveryState.BrightCoiled, "BrightCoiled")]
    public void DeliveryState_包装器Set_存入英文(DeliveryState enumVal, string expectedDbString)
    {
        enumVal.ToString().Should().Be(expectedDbString);
    }

    /// <summary>
    /// 可空包装器 setter 存入 null
    /// </summary>
    [Fact]
    public void 可空包装器_NullSet_存入Null字符串()
    {
        // 模拟 setter：value?.ToString()
        DeliveryState? nullVal = null;
        var result = nullVal?.ToString();
        result.Should().BeNull();

        DeliveryState? nonNullVal = DeliveryState.SolutionAnnealedAndPickled;
        var result2 = nonNullVal?.ToString();
        result2.Should().Be("SolutionAnnealedAndPickled");
    }

    [Theory]
    [InlineData(LengthStatus.Fixed)]
    [InlineData(LengthStatus.Range)]
    [InlineData(LengthStatus.NonFixed)]
    public void LengthStatus_包装器Get_英文名解析正确(LengthStatus expected)
    {
        var dbString = expected.ToString();
        Enum.Parse<LengthStatus>(dbString).Should().Be(expected);
    }

    [Theory]
    [InlineData(LengthStatus.Fixed, "Fixed")]
    [InlineData(LengthStatus.Range, "Range")]
    [InlineData(LengthStatus.NonFixed, "NonFixed")]
    public void LengthStatus_包装器Set_存入英文(LengthStatus enumVal, string expectedDbString)
    {
        enumVal.ToString().Should().Be(expectedDbString);
    }

    [Theory]
    [InlineData(RequirementType.Normal)]
    [InlineData(RequirementType.Special)]
    public void RequirementType_包装器Get_英文名解析正确(RequirementType expected)
    {
        var dbString = expected.ToString();
        Enum.Parse<RequirementType>(dbString).Should().Be(expected);
    }

    [Theory]
    [InlineData(RequirementType.Normal, "Normal")]
    [InlineData(RequirementType.Special, "Special")]
    public void RequirementType_包装器Set_存入英文(RequirementType enumVal, string expectedDbString)
    {
        enumVal.ToString().Should().Be(expectedDbString);
    }

    #endregion

    #region 防护测试：DisplayHelper.GetXxxText() 不可用于数据赋值

    /// <summary>
    /// 核心防护测试：验证 DisplayHelper.GetXxxText() 返回中文显示文本，
    /// 该中文文本不应被 Enum.Parse / Enum.TryParse 正确解析。
    ///
    /// 如果此测试失败，说明 DisplayHelper 返回了英文枚举名，
    /// 那么 BatchCreate.FillFromAvailableBatch() 之类的赋值代码就会
    /// 不小心将中文存入数据库（已发生的真实缺陷）。
    /// </summary>
    public static IEnumerable<object[]> EnumDisplayTextData => new[]
    {
        // (显示文本, 枚举类型示例值, 应包含的中文关键词)
        CreateCase(ProductionType.RoughTube, "荒管"),
        CreateCase(ManufacturingItem.OrderFinishedProduct, "订单"),
        CreateCase(PipeManufacturingType.SeamlessPipe, "无缝"),
        CreateCase(SettlementMethod.Theoretical, "理算"),
        CreateCase(DeliveryState.SolutionAnnealedAndPickled, "固溶"),
        CreateCase(LengthStatus.Fixed, "定尺"),
        CreateCase(RequirementType.Normal, "普通"),
    };

    private static object[] CreateCase<T>(T enumVal, string expectedChinese) where T : Enum
        => new object[] { enumVal.ToString()!, enumVal.GetType(), expectedChinese };

    [Theory]
    [MemberData(nameof(EnumDisplayTextData))]
    public void DisplayHelper返回中文_不可用于EnumParse(string enumName, Type enumType, string expectedChinese)
    {
        // 通过 DisplayHelper 获取中文显示文本
        var chineseText = GetDisplayText(enumType, enumName);

        // 中文显示文本应包含预期的中文字符
        chineseText.Should().Contain(expectedChinese);

        // 核心断言：中文显示文本不应是有效的 C# 枚举名
        // 这就是 BatchCreate 缺陷的检测模式：
        // 如果 request.SourceLengthStatus = GetLengthStatusText("Fixed") → "定尺"
        // 那么 Enum.TryParse<LengthStatus>("定尺", out _) 应返回 false
        var tryResult = Enum.TryParse(enumType, chineseText, ignoreCase: true, out _);
        tryResult.Should().BeFalse(
            $"DisplayHelper.GetXxxText({enumName}) 返回了 \"{chineseText}\"，该值可被 Enum.TryParse 解析为有效的枚举名。\r\n" +
            $"这会导致 FillFromAvailableBatch 等赋值方法误将中文文本存入数据库。\r\n" +
            $"应修改 DisplayHelper 为该枚举返回中文显示文本（如 \"{expectedChinese}...\"），而非英文枚举名。");
    }

    /// <summary>
    /// 针对可空/字符串重载的 DisplayHelper 也做同样的防护检查
    /// </summary>
    [Theory]
    [InlineData("RoughTube", typeof(ProductionType), "荒管")]
    [InlineData("OrderFinishedProduct", typeof(ManufacturingItem), "订单")]
    [InlineData("SeamlessPipe", typeof(PipeManufacturingType), "无缝")]
    [InlineData("Theoretical", typeof(SettlementMethod), "理算")]
    [InlineData("SolutionAnnealedAndPickled", typeof(DeliveryState), "固溶")]
    [InlineData("Fixed", typeof(LengthStatus), "定尺")]
    [InlineData("Normal", typeof(RequirementType), "普通")]
    public void DisplayHelper字符串重载_返回中文_不可用于EnumParse(string enumName, Type enumType, string expectedChinese)
    {
        // 调用 DisplayHelper 的 string? 重载
        var chineseText = GetDisplayTextStringOverload(enumType, enumName);

        chineseText.Should().Contain(expectedChinese);

        var tryResult = Enum.TryParse(enumType, chineseText, ignoreCase: true, out _);
        tryResult.Should().BeFalse(
            $"DisplayHelper.GetXxxText(\"{enumName}\") 返回了 \"{chineseText}\"，该值可被 Enum.TryParse 解析。");
    }

    #endregion

    #region 全枚举值覆盖测试：所有枚举值必须 round-trip 安全

    /// <summary>
    /// 验证 ProductionType 的所有枚举值均可安全 round-trip
    /// </summary>
    [Fact]
    public void ProductionType_所有值可安全Roundtrip()
    {
        foreach (ProductionType val in Enum.GetValues<ProductionType>())
        {
            var str = val.ToString();
            var back = Enum.Parse<ProductionType>(str);
            back.Should().Be(val, $"ProductionType.{val} 的 ToString() 返回 \"{str}\"，无法通过 Enum.Parse 恢复");
        }
    }

    [Fact]
    public void ManufacturingItem_所有值可安全Roundtrip()
    {
        foreach (ManufacturingItem val in Enum.GetValues<ManufacturingItem>())
        {
            var str = val.ToString();
            var back = Enum.Parse<ManufacturingItem>(str);
            back.Should().Be(val);
        }
    }

    [Fact]
    public void PipeManufacturingType_所有值可安全Roundtrip()
    {
        foreach (PipeManufacturingType val in Enum.GetValues<PipeManufacturingType>())
        {
            var str = val.ToString();
            var back = Enum.Parse<PipeManufacturingType>(str);
            back.Should().Be(val);
        }
    }

    [Fact]
    public void SettlementMethod_所有值可安全Roundtrip()
    {
        foreach (SettlementMethod val in Enum.GetValues<SettlementMethod>())
        {
            var str = val.ToString();
            var back = Enum.Parse<SettlementMethod>(str);
            back.Should().Be(val);
        }
    }

    [Fact]
    public void DeliveryState_所有值可安全Roundtrip()
    {
        foreach (DeliveryState val in Enum.GetValues<DeliveryState>())
        {
            var str = val.ToString();
            var back = Enum.Parse<DeliveryState>(str);
            back.Should().Be(val);
        }
    }

    [Fact]
    public void LengthStatus_所有值可安全Roundtrip()
    {
        foreach (LengthStatus val in Enum.GetValues<LengthStatus>())
        {
            var str = val.ToString();
            var back = Enum.Parse<LengthStatus>(str);
            back.Should().Be(val);
        }
    }

    [Fact]
    public void RequirementType_所有值可安全Roundtrip()
    {
        foreach (RequirementType val in Enum.GetValues<RequirementType>())
        {
            var str = val.ToString();
            var back = Enum.Parse<RequirementType>(str);
            back.Should().Be(val);
        }
    }

    #endregion

    #region 帮助方法

    private static string GetDisplayText(Type enumType, string enumName)
    {
        // 通过已知映射调用对应的 DisplayHelper 方法
        if (enumType == typeof(ProductionType)) return DisplayHelper.GetProductionTypeText(Enum.Parse<ProductionType>(enumName));
        if (enumType == typeof(ManufacturingItem)) return DisplayHelper.GetManufacturingItemText(Enum.Parse<ManufacturingItem>(enumName));
        if (enumType == typeof(PipeManufacturingType)) return DisplayHelper.GetPipeManufacturingTypeText(Enum.Parse<PipeManufacturingType>(enumName));
        if (enumType == typeof(SettlementMethod)) return DisplayHelper.GetSettlementMethodText(Enum.Parse<SettlementMethod>(enumName));
        if (enumType == typeof(DeliveryState)) return DisplayHelper.GetDeliveryStateText(Enum.Parse<DeliveryState>(enumName));
        if (enumType == typeof(LengthStatus)) return DisplayHelper.GetLengthStatusText(Enum.Parse<LengthStatus>(enumName));
        if (enumType == typeof(RequirementType)) return DisplayHelper.GetTechnicalRequirementsText(Enum.Parse<RequirementType>(enumName));
        throw new ArgumentException($"Unknown enum type: {enumType}");
    }

    private static string GetDisplayTextStringOverload(Type enumType, string enumName)
    {
        // 调用 DisplayHelper 的 string? 重载
        if (enumType == typeof(ProductionType)) return DisplayHelper.GetProductionTypeText(enumName);
        if (enumType == typeof(ManufacturingItem)) return DisplayHelper.GetManufacturingItemText(enumName);
        if (enumType == typeof(PipeManufacturingType)) return DisplayHelper.GetPipeManufacturingTypeText(enumName);
        if (enumType == typeof(SettlementMethod)) return DisplayHelper.GetSettlementMethodText(enumName);
        if (enumType == typeof(DeliveryState)) return DisplayHelper.GetDeliveryStateText(enumName);
        if (enumType == typeof(LengthStatus)) return DisplayHelper.GetLengthStatusText(enumName);
        if (enumType == typeof(RequirementType)) return DisplayHelper.GetTechnicalRequirementsText(enumName);
        throw new ArgumentException($"Unknown enum type: {enumType}");
    }

    #endregion
}
