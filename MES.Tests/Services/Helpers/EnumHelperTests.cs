using System.Reflection;
using FluentAssertions;
using MES.Blazor.Helpers;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// EnumHelper 注册覆盖和双向映射测试
/// 确保所有枚举类型均已注册，中文显示名正确，Parse 回退一致
/// </summary>
public class EnumHelperTests
{
    #region GetDisplayName — 所有注册枚举

    [Fact]
    public void GetDisplayName_WorkOrderStatus()
    {
        EnumHelper.GetDisplayName(WorkOrderStatus.NotGenerated).Should().Be("未编制");
        EnumHelper.GetDisplayName(WorkOrderStatus.Confirmed).Should().Be("已确定");
        EnumHelper.GetDisplayName(WorkOrderStatus.Pending).Should().Be("待修正");
    }

    [Fact]
    public void GetDisplayName_MaterialPlanStatus()
    {
        EnumHelper.GetDisplayName(MaterialPlanStatus.NotPlanned).Should().Be("未计划");
        EnumHelper.GetDisplayName(MaterialPlanStatus.Partial).Should().Be("部分");
        EnumHelper.GetDisplayName(MaterialPlanStatus.TheoreticalSatisfied).Should().Be("理论满足");
        EnumHelper.GetDisplayName(MaterialPlanStatus.Satisfied).Should().Be("满足");
        EnumHelper.GetDisplayName(MaterialPlanStatus.Excess).Should().Be("超量");
    }

    [Fact]
    public void GetDisplayName_InventoryPlanStatus()
    {
        EnumHelper.GetDisplayName(InventoryPlanStatus.Planned).Should().Be("已计划");
        EnumHelper.GetDisplayName(InventoryPlanStatus.Confirmed).Should().Be("已确认");
        EnumHelper.GetDisplayName(InventoryPlanStatus.Cancelled).Should().Be("已取消");
    }

    [Fact]
    public void GetDisplayName_LengthStatus()
    {
        EnumHelper.GetDisplayName(LengthStatus.Fixed).Should().Be("定尺");
        EnumHelper.GetDisplayName(LengthStatus.Range).Should().Be("范围尺");
        EnumHelper.GetDisplayName(LengthStatus.NonFixed).Should().Be("非定尺");
    }

    [Fact]
    public void GetDisplayName_DeliveryState()
    {
        EnumHelper.GetDisplayName(DeliveryState.SolutionAnnealedAndPickled).Should().Be("固溶酸洗");
        EnumHelper.GetDisplayName(DeliveryState.SolutionAnnealedAndPickledUTube).Should().Be("固溶酸洗-U型管");
        EnumHelper.GetDisplayName(DeliveryState.SolutionAnnealedAndPickledExternalPolished).Should().Be("固溶酸洗-外抛光");
        EnumHelper.GetDisplayName(DeliveryState.SolutionAnnealedAndPickledInternalPolished).Should().Be("固溶酸洗-内抛光");
        EnumHelper.GetDisplayName(DeliveryState.SolutionAnnealedAndPickledBothPolished).Should().Be("固溶酸洗-内外抛光");
        EnumHelper.GetDisplayName(DeliveryState.SolutionAnnealedAndPickledCoiled).Should().Be("固溶酸洗-盘管");
        EnumHelper.GetDisplayName(DeliveryState.Bright).Should().Be("光亮");
        EnumHelper.GetDisplayName(DeliveryState.BrightUTube).Should().Be("光亮-U型管");
        EnumHelper.GetDisplayName(DeliveryState.BrightCoiled).Should().Be("光亮-盘管");
        EnumHelper.GetDisplayName(DeliveryState.Hard).Should().Be("硬态");
    }

    [Fact]
    public void GetDisplayName_SettlementMethod()
    {
        EnumHelper.GetDisplayName(SettlementMethod.Theoretical).Should().Be("理算");
        EnumHelper.GetDisplayName(SettlementMethod.Weighing).Should().Be("过磅");
        EnumHelper.GetDisplayName(SettlementMethod.WeighingNegative).Should().Be("过磅-负");
    }

    [Fact]
    public void GetDisplayName_PipeManufacturingType()
    {
        EnumHelper.GetDisplayName(PipeManufacturingType.SeamlessPipe).Should().Be("无缝管");
        EnumHelper.GetDisplayName(PipeManufacturingType.WeldedPipe).Should().Be("焊管");
    }

    [Fact]
    public void GetDisplayName_ReworkType()
    {
        EnumHelper.GetDisplayName(ReworkType.EmptyDrawing).Should().Be("空拉改制");
        EnumHelper.GetDisplayName(ReworkType.FewerPass).Should().Be("少道次改制");
        EnumHelper.GetDisplayName(ReworkType.ManualSelect).Should().Be("人工选择改制");
    }

    [Fact]
    public void GetDisplayName_FinishedProductType()
    {
        EnumHelper.GetDisplayName(FinishedProductType.Critical).Should().Be("临界成品");
        EnumHelper.GetDisplayName(FinishedProductType.Order).Should().Be("订单成品");
    }

    [Fact]
    public void GetDisplayName_ProductionType()
    {
        EnumHelper.GetDisplayName(ProductionType.RoughTube).Should().Be("荒管生产");
        EnumHelper.GetDisplayName(ProductionType.InProcess).Should().Be("在制生产");
        EnumHelper.GetDisplayName(ProductionType.Inventory).Should().Be("库存");
        EnumHelper.GetDisplayName(ProductionType.OutsourcedPurchased).Should().Be("外购");
        EnumHelper.GetDisplayName(ProductionType.Rework).Should().Be("返整");
        EnumHelper.GetDisplayName(ProductionType.Subcontract).Should().Be("委外生产");
        EnumHelper.GetDisplayName(ProductionType.ExternalProcessing).Should().Be("对外加工");
    }

    [Fact]
    public void GetDisplayName_ManufacturingItem()
    {
        EnumHelper.GetDisplayName(MaterialType.OrderFinished).Should().Be("订单成品");
        EnumHelper.GetDisplayName(MaterialType.Finished).Should().Be("备料成品");
        EnumHelper.GetDisplayName(MaterialType.Surplus).Should().Be("余库料");
        EnumHelper.GetDisplayName(MaterialType.SpecialDeliveryStatus).Should().Be("订成-非交付态");
    }

    [Fact]
    public void GetDisplayName_MaterialType()
    {
        EnumHelper.GetDisplayName(MaterialType.RoundBar).Should().Be("圆棒");
        EnumHelper.GetDisplayName(MaterialType.RoughTube).Should().Be("荒管");
        EnumHelper.GetDisplayName(MaterialType.SemiFinished).Should().Be("半成品");
        EnumHelper.GetDisplayName(MaterialType.OrderFinished).Should().Be("订单成品");
        EnumHelper.GetDisplayName(MaterialType.Finished).Should().Be("备料成品");
        EnumHelper.GetDisplayName(MaterialType.CriticalFinished).Should().Be("临界成品");
        EnumHelper.GetDisplayName(MaterialType.DefectRoundBar).Should().Be("次品圆棒");
        EnumHelper.GetDisplayName(MaterialType.DefectRoughTube).Should().Be("次品荒管");
        EnumHelper.GetDisplayName(MaterialType.DefectSemi).Should().Be("次品半成品");
        EnumHelper.GetDisplayName(MaterialType.DefectFinished).Should().Be("次品成品");
        EnumHelper.GetDisplayName(MaterialType.Scrap).Should().Be("报废品");
        EnumHelper.GetDisplayName(MaterialType.Surplus).Should().Be("余库料");
        EnumHelper.GetDisplayName(MaterialType.SpecialDeliveryStatus).Should().Be("订成-非交付态");
        EnumHelper.GetDisplayName(MaterialType.WorkInProgress).Should().Be("在制品");
        EnumHelper.GetDisplayName(MaterialType.DefectWIP).Should().Be("次品在制");
    }

    [Fact]
    public void GetDisplayName_OutboundType()
    {
        EnumHelper.GetDisplayName(OutboundType.ProductionPick).Should().Be("生产领用");
        EnumHelper.GetDisplayName(OutboundType.SalesOut).Should().Be("销售出库");
        EnumHelper.GetDisplayName(OutboundType.ReturnOut).Should().Be("退货出库");
        EnumHelper.GetDisplayName(OutboundType.SubcontractOut).Should().Be("委外出库");
        EnumHelper.GetDisplayName(OutboundType.ScrapOut).Should().Be("报废出库");
        EnumHelper.GetDisplayName(OutboundType.InspectionPick).Should().Be("检验领用");
        EnumHelper.GetDisplayName(OutboundType.TransferOut).Should().Be("移库出库");
        EnumHelper.GetDisplayName(OutboundType.OtherOut).Should().Be("其他出库");
    }

    [Fact]
    public void GetDisplayName_BatchStatus()
    {
        EnumHelper.GetDisplayName(BatchStatus.None).Should().Be("未产");
        EnumHelper.GetDisplayName(BatchStatus.InProgress).Should().Be("在产");
        EnumHelper.GetDisplayName(BatchStatus.Completed).Should().Be("完成");
        EnumHelper.GetDisplayName(BatchStatus.Suspended).Should().Be("暂停");
    }

    [Fact]
    public void GetDisplayName_PurchaseOrderStatus()
    {
        EnumHelper.GetDisplayName(PurchaseOrderStatus.Open).Should().Be("已下单");
        EnumHelper.GetDisplayName(PurchaseOrderStatus.Partial).Should().Be("部分到货");
        EnumHelper.GetDisplayName(PurchaseOrderStatus.Completed).Should().Be("已完成");
    }

    [Fact]
    public void GetDisplayName_SubcontractOrderStatus()
    {
        EnumHelper.GetDisplayName(SubcontractOrderStatus.Sent).Should().Be("已发出");
        EnumHelper.GetDisplayName(SubcontractOrderStatus.PartialReturned).Should().Be("部分收回");
        EnumHelper.GetDisplayName(SubcontractOrderStatus.Completed).Should().Be("已完成");
    }

    [Fact]
    public void GetDisplayName_NotificationType()
    {
        EnumHelper.GetDisplayName(NotificationType.NewMaterial).Should().Be("新物料确认");
        EnumHelper.GetDisplayName(NotificationType.DeleteBlocked).Should().Be("删除拦截");
        EnumHelper.GetDisplayName(NotificationType.OutboundAlert).Should().Be("出库预警");
        EnumHelper.GetDisplayName(NotificationType.WorkOrderDeleted).Should().Be("工单已删除");
        EnumHelper.GetDisplayName(NotificationType.OrderDeleted).Should().Be("订单已删除");
        EnumHelper.GetDisplayName(NotificationType.OrderChanged).Should().Be("订单已变更");
    }

    [Fact]
    public void GetDisplayName_SectionStatus()
    {
        EnumHelper.GetDisplayName(SectionStatus.Completed).Should().Be("已完成");
        EnumHelper.GetDisplayName(SectionStatus.InProgress).Should().Be("进行中");
        EnumHelper.GetDisplayName(SectionStatus.Outsource).Should().Be("委外中");
        EnumHelper.GetDisplayName(SectionStatus.Next).Should().Be("待执行");
        EnumHelper.GetDisplayName(SectionStatus.Pending).Should().Be("待处理");
    }

    [Fact]
    public void GetDisplayName_RepairPriority()
    {
        EnumHelper.GetDisplayName(RepairPriority.Normal).Should().Be("普通");
        EnumHelper.GetDisplayName(RepairPriority.Urgent).Should().Be("紧急");
        EnumHelper.GetDisplayName(RepairPriority.Emergency).Should().Be("特急");
    }

    [Fact]
    public void GetDisplayName_NcrStatus()
    {
        EnumHelper.GetDisplayName(NcrStatus.Pending).Should().Be("待处理");
        EnumHelper.GetDisplayName(NcrStatus.Processing).Should().Be("处理中");
        EnumHelper.GetDisplayName(NcrStatus.Closed).Should().Be("已关闭");
    }

    [Fact]
    public void GetDisplayName_DisposalMethod()
    {
        EnumHelper.GetDisplayName(DisposalMethod.Rework).Should().Be("返整");
        EnumHelper.GetDisplayName(DisposalMethod.WarehouseEntry).Should().Be("入库");
        EnumHelper.GetDisplayName(DisposalMethod.Scrap).Should().Be("报废");
    }

    [Fact]
    public void GetDisplayName_PicklingStatus()
    {
        EnumHelper.GetDisplayName(PicklingStatus.Soaking).Should().Be("浸泡中");
        EnumHelper.GetDisplayName(PicklingStatus.Completed).Should().Be("已完工");
    }

    #endregion

    #region GetDisplayName — 非泛型重载

    [Fact]
    public void GetDisplayName_NonGeneric_ByTypeAndObject()
    {
        EnumHelper.GetDisplayName(typeof(BatchStatus), BatchStatus.Completed).Should().Be("完成");
        EnumHelper.GetDisplayName(typeof(LengthStatus), LengthStatus.Fixed).Should().Be("定尺");
        EnumHelper.GetDisplayName(typeof(DeliveryState), DeliveryState.Bright).Should().Be("光亮");
    }

    [Fact]
    public void GetDisplayName_StringOverload_ByName()
    {
        EnumHelper.GetDisplayName<BatchStatus>("Completed").Should().Be("完成");
        EnumHelper.GetDisplayName<LengthStatus>("Fixed").Should().Be("定尺");
        EnumHelper.GetDisplayName<DeliveryState>("Bright").Should().Be("光亮");
    }

    [Fact]
    public void GetDisplayName_StringOverload_ByChineseName()
    {
        EnumHelper.GetDisplayName<BatchStatus>("作废").Should().Be("作废");
        EnumHelper.GetDisplayName<LengthStatus>("范围尺").Should().Be("范围尺");
    }

    [Fact]
    public void GetDisplayName_StringOverload_NullReturnsEmpty()
    {
        EnumHelper.GetDisplayName<BatchStatus>(null).Should().Be("");
    }

    [Fact]
    public void GetDisplayName_StringOverload_InvalidReturnsInput()
    {
        EnumHelper.GetDisplayName<BatchStatus>("不存在的值").Should().Be("不存在的值");
    }

    #endregion

    #region Parse — 中文名 → 枚举值

    [Fact]
    public void Parse_ByChineseName()
    {
        EnumHelper.Parse<BatchStatus>("完成").Should().Be(BatchStatus.Completed);
        EnumHelper.Parse<BatchStatus>("在产").Should().Be(BatchStatus.InProgress);
        EnumHelper.Parse<LengthStatus>("定尺").Should().Be(LengthStatus.Fixed);
        EnumHelper.Parse<DeliveryState>("光亮").Should().Be(DeliveryState.Bright);
        EnumHelper.Parse<MaterialType>("圆棒").Should().Be(MaterialType.RoundBar);
        EnumHelper.Parse<ProductionType>("荒管生产").Should().Be(ProductionType.RoughTube);
    }

    [Fact]
    public void Parse_ByEnglishName()
    {
        EnumHelper.Parse<BatchStatus>("Completed").Should().Be(BatchStatus.Completed);
        EnumHelper.Parse<BatchStatus>("InProgress").Should().Be(BatchStatus.InProgress);
        EnumHelper.Parse<LengthStatus>("Fixed").Should().Be(LengthStatus.Fixed);
    }

    [Fact]
    public void Parse_ByEnglishName_CaseInsensitive()
    {
        EnumHelper.Parse<BatchStatus>("completed").Should().Be(BatchStatus.Completed);
        EnumHelper.Parse<BatchStatus>("INPROGRESS").Should().Be(BatchStatus.InProgress);
    }

    #endregion

    #region TryParse — 容错

    [Fact]
    public void TryParse_InvalidValue_ReturnsNull()
    {
        EnumHelper.TryParse<BatchStatus>("不存在的值").Should().BeNull();
        EnumHelper.TryParse<LengthStatus>("").Should().BeNull();
    }

    [Fact]
    public void TryParse_ValidValue_ReturnsEnum()
    {
        EnumHelper.TryParse<BatchStatus>("完成").Should().Be(BatchStatus.Completed);
        EnumHelper.TryParse<LengthStatus>("Fixed").Should().Be(LengthStatus.Fixed);
    }

    #endregion

    #region Round-trip 一致性

    [Fact]
    public void GetDisplayName_Parse_RoundTrip()
    {
        // 中文名 → Parse → 枚举 → GetDisplayName → 中文名
        EnumHelper.Parse<WorkOrderStatus>("已确定").Should().Be(WorkOrderStatus.Confirmed);
        EnumHelper.Parse<LengthStatus>("定尺").Should().Be(LengthStatus.Fixed);
        EnumHelper.Parse<DeliveryState>("光亮").Should().Be(DeliveryState.Bright);
        EnumHelper.Parse<SettlementMethod>("理算").Should().Be(SettlementMethod.Theoretical);

        // 反向：枚举 → GetDisplayName → Parse → 枚举
        EnumHelper.Parse<LengthStatus>(EnumHelper.GetDisplayName(LengthStatus.Range)).Should().Be(LengthStatus.Range);
        EnumHelper.Parse<DeliveryState>(EnumHelper.GetDisplayName(DeliveryState.Hard)).Should().Be(DeliveryState.Hard);
    }

    #endregion

    #region 注册完整性 — 所有枚举类型都能取得中文名

    /// <summary>
    /// 遍历 MES.Core.Enums 中所有枚举类型的每个值，
    /// 验证 GetDisplayName 返回非空且不等于 .ToString()
    /// （即已经过中文映射，而不是 fallback 到英文名）
    /// </summary>
    [Fact]
    public void 所有注册枚举_每个值都有中文显示名()
    {
        var enumTypes = new[]
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
            typeof(NcrStatus), typeof(PicklingStatus), typeof(ResponsibilityCategory),
            typeof(SeverityLevel), typeof(VerifyResult), typeof(SectionStatus)
        };

        foreach (var enumType in enumTypes)
        {
            var values = Enum.GetValues(enumType);
            foreach (var value in values)
            {
                var name = Enum.GetName(enumType, value)!;
                var display = EnumHelper.GetDisplayName(enumType, value);

                display.Should().NotBe(name,
                    $"枚举 {enumType.Name}.{name} 的中文显示名不应回退为英文名");
                display.Should().NotBeNullOrEmpty(
                    $"枚举 {enumType.Name}.{name} 的中文显示名不应为空");
            }
        }
    }

    #endregion

    #region DisplayHelper 覆盖一致性 — 每个枚举都有对应的 GetXxxText 方法

    /// <summary>
    /// DisplayHelper.GetXxxText() 与 Enum 类型的映射关系。
    /// 每对 (HelperPrefix, EnumType) 表示 DisplayHelper 中有一个
    /// Get{HelperPrefix}Text(EnumType) 方法委托给 EnumHelper。
    /// </summary>
    private static readonly (string Prefix, Type EnumType)[] DisplayHelperEnumMappings =
    {
        ("LengthStatus", typeof(LengthStatus)),
        ("DeliveryState", typeof(DeliveryState)),
        ("PipeManufacturingType", typeof(PipeManufacturingType)),
        ("SettlementMethod", typeof(SettlementMethod)),
        ("SalesOrderStatus", typeof(SalesOrderStatus)),
        ("PurchaseOrderStatus", typeof(PurchaseOrderStatus)),
        ("WorkOrderStatus", typeof(WorkOrderStatus)),
        ("BatchStatus", typeof(BatchStatus)),
        ("SectionOutsourceStatus", typeof(SectionOutsourceStatus)),
        ("ProductionType", typeof(ProductionType)),
        ("InspectionItem", typeof(InspectionItem)),
        ("LifecycleStatus", typeof(LifecycleStatus)),
        ("UsageType", typeof(UsageType)),
        ("RunningStatus", typeof(RunningStatus)),
        ("EquipmentTaskStatus", typeof(EquipmentTaskStatus)),
        ("RepairOrderStatus", typeof(RepairOrderStatus)),
        ("Priority", typeof(RepairPriority)),         // GetPriorityText(RepairPriority)
        ("TaskOrderStatus", typeof(TaskOrderStatus)),
        ("SubcontractProcessStatus", typeof(SubcontractOrderStatus)),
        ("SubcontractOrderStatus", typeof(SubcontractOrderStatus)),
        ("OutboundType", typeof(OutboundType)),
        ("MaterialPlanStatus", typeof(MaterialPlanStatus)),
        ("RequirementType", typeof(RequirementType)),  // GetRequirementTypeText(RequirementType) — 注意：是枚举版本委托 EnumHelper
        ("InventoryPlanStatus", typeof(InventoryPlanStatus)),
        ("ReworkType", typeof(ReworkType)),
        ("FinishedProductType", typeof(FinishedProductType)),
        ("MaterialType", typeof(MaterialType)),
        ("CustomerStatus", typeof(CustomerStatus)),
        ("NotificationType", typeof(NotificationType)),
        ("DisposalMethod", typeof(DisposalMethod)),
        ("NcrStatus", typeof(NcrStatus)),
        ("PicklingStatus", typeof(PicklingStatus)),
        ("ResponsibilityCategory", typeof(ResponsibilityCategory)),
        ("SeverityLevel", typeof(SeverityLevel)),
        ("VerifyResult", typeof(VerifyResult)),
        ("PipeCategory", typeof(MaterialType)),
        ("SectionStatus", typeof(SectionStatus)),
    };

    /// <summary>
    /// 验证：每个枚举类型在 DisplayHelper 中有对应的 GetXxxText(EnumType) 方法。
    /// </summary>
    [Fact]
    public void DisplayHelper_每个枚举都有GetXxxText方法()
    {
        var helperType = typeof(DisplayHelper);

        foreach (var (prefix, enumType) in DisplayHelperEnumMappings)
        {
            var methodName = $"Get{prefix}Text";
            var methods = helperType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == methodName)
                .ToList();

            methods.Should().NotBeEmpty(
                $"DisplayHelper 应包含名为 {methodName} 的静态方法");

            // 至少有一个重载接受该枚举类型
            var hasEnumOverload = methods.Any(m =>
                m.GetParameters().Any(p => p.ParameterType == enumType));
            hasEnumOverload.Should().BeTrue(
                $"DisplayHelper.{methodName} 应包含接受 {enumType.Name} 类型的重载");
        }
    }

    /// <summary>
    /// 验证：每个 DisplayHelper 的 GetXxxText 方法对枚举所有值返回非空中文。
    /// </summary>
    [Fact]
    public void DisplayHelper_每个GetXxxText覆盖所有枚举值()
    {
        foreach (var (prefix, enumType) in DisplayHelperEnumMappings)
        {
            var methodName = $"Get{prefix}Text";

            // 通过反射调用 DisplayHelper.Get{prefix}Text(EnumType) 方法
            var method = typeof(DisplayHelper).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Any(p => p.ParameterType == enumType));

            method.Should().NotBeNull($"DisplayHelper.{methodName}({enumType.Name}) 方法应存在");

            foreach (var value in Enum.GetValues(enumType))
            {
                var result = method!.Invoke(null, new[] { value });

                result.Should().NotBeNull($"DisplayHelper.{methodName}({value}) 应返回非空值");
                var resultStr = result!.ToString()!;
                var rawName = Enum.GetName(enumType, value)!;

                // 应返回中文显示文本，而非英文枚举名
                resultStr.Should().NotBe(rawName,
                    $"DisplayHelper.{methodName}({enumType.Name}.{rawName}) " +
                    $"返回了 \"{resultStr}\"，应为中文显示文本");
            }
        }
    }

    /// <summary>
    /// 验证：DisplayHelper 的 string 版本 GetXxxText(string?) 方法也能正确处理所有有效枚举名。
    /// </summary>
    [Fact]
    public void DisplayHelper_字符串版本_覆盖所有枚举值()
    {
        var helperType = typeof(DisplayHelper);

        foreach (var (prefix, enumType) in DisplayHelperEnumMappings)
        {
            var methodName = $"Get{prefix}Text";

            // 查找接受 string? 参数的重载
            var stringMethod = helperType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == methodName &&
                    m.GetParameters().Any(p => p.ParameterType == typeof(string)));

            // 部分类型只有枚举版本（如 InspectionItem），跳过
            if (stringMethod == null)
                continue;

            foreach (var value in Enum.GetValues(enumType))
            {
                var enumName = Enum.GetName(enumType, value);
                var result = stringMethod.Invoke(null, new[] { enumName });

                result.Should().NotBeNull(
                    $"DisplayHelper.{methodName}(\"{enumName}\") 应返回非空值");
            }
        }
    }

    /// <summary>
    /// 验证：GetTechnicalRequirementsText(string?) 手动 switch 覆盖了 RequirementType 的所有值。
    /// 这是少数不通过 EnumHelper 的独立映射方法。
    /// </summary>
    [Fact]
    public void GetTechnicalRequirementsText_手动映射覆盖所有枚举值()
    {
        var allRequirements = Enum.GetValues<RequirementType>();
        foreach (var req in allRequirements)
        {
            var chineseText = DisplayHelper.GetTechnicalRequirementsText(req);
            var englishName = req.ToString();

            // 应返回中文文本而非英文枚举名
            chineseText.Should().NotBe(englishName,
                $"GetTechnicalRequirementsText({englishName}) 返回了英文名 \"{englishName}\"，应为中文");
            chineseText.Should().NotBeNullOrEmpty();
        }

        // 字符串版本也应兼容
        foreach (var req in allRequirements)
        {
            var chineseText = DisplayHelper.GetTechnicalRequirementsText(req.ToString());
            chineseText.Should().NotBeNullOrEmpty(
                $"GetTechnicalRequirementsText(\"{req}\") 应返回中文文本");
        }
    }

    #endregion
}
