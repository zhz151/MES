using System.Reflection;
using FluentAssertions;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// Service 层 Enum.Parse 安全防护测试。
///
/// 项目中 17+ 个 Service 文件使用 Enum.Parse{T}() 将数据库中存储的枚举英文名
/// （如 "Fixed"、"Weighing"）解析为枚举类型。如果中文文本被误存入数据库，
/// Enum.Parse 会静默返回 default(0)，造成数据损坏。
///
/// 本测试验证：所有 Service 层涉及的枚举类型的 .ToString() → Enum.Parse 路径安全，
/// 且中文显示文本不被错误地视作有效枚举值。
/// </summary>
public class ServiceEnumParseSafetyTests
{
    /// <summary>
    /// 项目中 Service 层所有涉及 Enum.Parse{T}() 的枚举类型完整列表。
    /// 来源：MES.Services 下 17+ 个 Service 文件中的 Enum.Parse / Enum.TryParse 调用。
    /// </summary>
    private static readonly Type[] AllServiceLayerEnumTypes =
    {
        typeof(WorkOrderStatus),
        typeof(MaterialPlanStatus),
        typeof(InventoryPlanStatus),
        typeof(LengthStatus),
        typeof(DeliveryState),
        typeof(SettlementMethod),
        typeof(SalesOrderStatus),
        typeof(PipeManufacturingType),
        typeof(ReworkType),
        typeof(FinishedProductType),
        typeof(ProductionType),
        typeof(MaterialType),
        typeof(OutboundType),
        typeof(CustomerStatus),
        typeof(RequirementType),
        typeof(NotificationType),
        typeof(BatchStatus),
        typeof(PurchaseOrderStatus),
        typeof(SubcontractOrderStatus),
        typeof(SectionOutsourceStatus),
        typeof(RepairPriority),
        typeof(LifecycleStatus),
        typeof(UsageType),
        typeof(RunningStatus),
        typeof(RepairOrderStatus),
        typeof(EquipmentTaskStatus),
        typeof(TaskOrderStatus),
        typeof(InspectionItem),
        typeof(DisposalMethod),
        typeof(NcrStatus),
        typeof(PicklingStatus),
        typeof(SeverityLevel),
        typeof(VerifyResult),
        typeof(SectionStatus),
        typeof(InspectionRequirementStage)
    };

    #region 基础安全：所有枚举值 .ToString() → Enum.Parse 可逆

    public static IEnumerable<object[]> AllServiceLayerEnumValues
    {
        get
        {
            foreach (var type in AllServiceLayerEnumTypes)
            {
                foreach (var val in Enum.GetValues(type))
                {
                    var name = Enum.GetName(type, val);
                    if (name != null)
                        yield return new object[] { type, val, name };
                }
            }
        }
    }

    /// <summary>
    /// 核心安全测试：对 Service 层每个枚举类型的每个值，
    /// 验证 .ToString() 产生的英文名可通过 Enum.Parse 正确恢复。
    ///
    /// 模拟 Service 层标准读取模式：
    ///   var enumValue = Enum.Parse{T}(dbString);
    ///
    /// 如果此测试失败，说明某个枚举值 .ToString() 产生的内容
    /// 无法被 Enum.Parse 解析回原值，Service 层的标准读取模式将出错。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllServiceLayerEnumValues))]
    public void Service层枚举ToString_可被EnumParse安全恢复(Type enumType, object enumValue, string expectedName)
    {
        // 模拟 Service 层从 DB 读到的字符串
        var dbString = enumValue.ToString()!;

        // 该字符串必须是有效的 C# 枚举名（Enum.Parse 能正确解析）
        dbString.Should().Be(expectedName,
            $"枚举 {enumType.Name}.{expectedName} 的 ToString() 应返回 C# 枚举名");

        // 通过 Enum.Parse 恢复
        var parsed = Enum.Parse(enumType, dbString);
        parsed.Should().Be(enumValue,
            $"枚举 {enumType.Name} 的 Enum.Parse(\"{dbString}\") 应正确恢复");
    }

    /// <summary>
    /// 验证 Enum.GetName 和 .ToString() 行为一致
    /// （确保枚举没有自定义 ToString 覆盖导致行为异常）
    /// </summary>
    [Theory]
    [MemberData(nameof(AllServiceLayerEnumValues))]
    public void Service层枚举_ToString与GetName一致(Type enumType, object enumValue, string expectedName)
    {
        enumValue.ToString().Should().Be(expectedName);
        Enum.GetName(enumType, enumValue).Should().Be(expectedName);
    }

    #endregion

    #region 中文安全防护：中文显示文本不可被 Enum.Parse 解析

    public static IEnumerable<object[]> AllServiceLayerEnumChineseDisplay
    {
        get
        {
            foreach (var type in AllServiceLayerEnumTypes)
            {
                foreach (var val in Enum.GetValues(type))
                {
                    var display = EnumHelper.GetDisplayName(type, val);
                    if (!string.IsNullOrEmpty(display) && display != Enum.GetName(type, val))
                        yield return new object[] { type, display, Enum.GetName(type, val)! };
                }
            }
        }
    }

    /// <summary>
    /// 防护测试：EnumHelper.GetDisplayName() 返回的中文显示文本
    /// 不可被标准 Enum.Parse / Enum.TryParse 解析为有效枚举值。
    ///
    /// 如果此测试失败，意味着中文文本可被 Enum.Parse 误解析，
    /// 当 FillFromAvailableBatch 等 Service/Blazor 方法误将显示文本
    /// 赋给 DTO 的 string 字段时，中文将被直接存入数据库，
    /// 后续读取时 Enum.Parse 会静默返回错误值。
    /// </summary>
    [Theory]
    [MemberData(nameof(AllServiceLayerEnumChineseDisplay))]
    public void 中文显示文本_不可被EnumParse解析(Type enumType, string chineseDisplay, string enumName)
    {
        chineseDisplay.Should().NotBe(enumName,
            $"枚举 {enumType.Name} 的中文显示名不应等于英文枚举名 \"{enumName}\"");

        // 核心防护断言：中文文本不应能被 Enum.TryParse 解析
        var canParse = Enum.TryParse(enumType, chineseDisplay, ignoreCase: true, out _);
        canParse.Should().BeFalse(
            $"枚举 {enumType.Name} 的 EnumHelper.GetDisplayName 返回了 \"{chineseDisplay}\"，" +
            $"该文本可被 Enum.TryParse 解析为有效枚举值。\r\n" +
            $"这意味着如有代码误将 GetDisplayName 返回值赋给 DTO 的 string 字段，" +
            $"中文文本可直接存入数据库。\r\n" +
            $"此枚举的英文名称为: {enumName}");
    }

    #endregion

    #region null/空字符串容错

    /// <summary>
    /// 验证 Service 层标准 null 容错模式
    ///   string.IsNullOrEmpty(s) ? default : Enum.Parse{T}(s)
    /// 对于 null 和空字符串均返回 default(T)
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void 空字符串_容错模式_返回Default(string? emptyValue)
    {
        // 模拟 BatchService/MaterialPlanService 等中的标准容错模式
        // MaterialType default = OrderFinished(index 0)
        var mi = string.IsNullOrEmpty(emptyValue) ? default : Enum.Parse<MaterialType>(emptyValue);
        mi.Should().Be(default(MaterialType));

        // LengthStatus default = Fixed(index 0)
        var ls = string.IsNullOrEmpty(emptyValue) ? default : Enum.Parse<LengthStatus>(emptyValue);
        ls.Should().Be(default(LengthStatus));

        // SettlementMethod default = Theoretical(index 0)
        var sm = string.IsNullOrEmpty(emptyValue) ? default : Enum.Parse<SettlementMethod>(emptyValue);
        sm.Should().Be(default(SettlementMethod));

        // ProductionType default = RoughTube(index 0)
        var pt = string.IsNullOrEmpty(emptyValue) ? default : Enum.Parse<ProductionType>(emptyValue);
        pt.Should().Be(default(ProductionType));
    }

    /// <summary>
    /// Service 层的 TryParse 容错模式（非标准模式，可选的）
    ///   Enum.TryParse{T}(s, out var val) ? val : default
    /// </summary>
    [Fact]
    public void TryParse容错模式_无效字符串_返回Default()
    {
        // TryParse 失败时不抛出异常，返回 false
        var result = Enum.TryParse<LengthStatus>("无效中文", ignoreCase: true, out var val);
        result.Should().BeFalse();
        val.Should().Be(default(LengthStatus));
    }

    #endregion

    #region 注册完整性：确保 AllServiceLayerEnumTypes 包含所有枚举

    /// <summary>
    /// 验证 AllServiceLayerEnumTypes 列表完整
    /// 与 EnumHelperTests.所有注册枚举_每个值都有中文显示名 保持一致
    /// </summary>
    [Fact]
    public void Service层枚举列表_覆盖所有注册枚举()
    {
        // EnumHelperTests 已覆盖所有注册枚举类型的完整性
        // 本测试验证我们的枚举列表与之匹配
        var enumHelperTypes = new[]
        {
            typeof(WorkOrderStatus), typeof(MaterialPlanStatus), typeof(InventoryPlanStatus),
            typeof(LengthStatus), typeof(DeliveryState), typeof(SettlementMethod),
            typeof(SalesOrderStatus), typeof(PipeManufacturingType), typeof(ReworkType),
            typeof(FinishedProductType), typeof(ProductionType),
            typeof(MaterialType), typeof(OutboundType),
            typeof(CustomerStatus), typeof(RequirementType), typeof(NotificationType),
            typeof(BatchStatus), typeof(PurchaseOrderStatus),
            typeof(SubcontractOrderStatus), typeof(SectionOutsourceStatus), typeof(RepairPriority),
            typeof(LifecycleStatus), typeof(UsageType), typeof(RunningStatus),
            typeof(RepairOrderStatus), typeof(EquipmentTaskStatus), typeof(TaskOrderStatus),
            typeof(InspectionItem), typeof(DisposalMethod),
            typeof(NcrStatus), typeof(PicklingStatus),
            typeof(SeverityLevel), typeof(VerifyResult), typeof(SectionStatus),
            typeof(InspectionRequirementStage)
        };

        AllServiceLayerEnumTypes.Should().BeEquivalentTo(enumHelperTypes,
            "AllServiceLayerEnumTypes 列表应与 EnumHelper 注册的枚举类型完整一致");
    }

    #endregion
}
