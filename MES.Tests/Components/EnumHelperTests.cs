using FluentAssertions;
using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Tests.Components;

/// <summary>
/// EnumHelper 中文↔枚举 双向映射测试
/// 验证所有已注册枚举的 GetDisplayName / Parse 双向转换正确性
/// </summary>
public class EnumHelperTests
{
    [Theory]
    [InlineData(WorkOrderStatus.NotGenerated, "未编制")]
    [InlineData(WorkOrderStatus.Confirmed, "已确定")]
    [InlineData(WorkOrderStatus.Pending, "待修正")]
    public void WorkOrderStatus_GetDisplayName(WorkOrderStatus value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("未编制", WorkOrderStatus.NotGenerated)]
    [InlineData("已确定", WorkOrderStatus.Confirmed)]
    [InlineData("待修正", WorkOrderStatus.Pending)]
    public void WorkOrderStatus_Parse_Chinese(string chinese, WorkOrderStatus expected)
    {
        EnumHelper.Parse<WorkOrderStatus>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData("NotGenerated", WorkOrderStatus.NotGenerated)]
    [InlineData("confirmed", WorkOrderStatus.Confirmed)] // case-insensitive
    public void WorkOrderStatus_Parse_English(string english, WorkOrderStatus expected)
    {
        EnumHelper.Parse<WorkOrderStatus>(english).Should().Be(expected);
    }

    [Theory]
    [InlineData(MaterialPlanStatus.NotPlanned, "未计划")]
    [InlineData(MaterialPlanStatus.Partial, "部分")]
    [InlineData(MaterialPlanStatus.TheoreticalSatisfied, "理论满足")]
    [InlineData(MaterialPlanStatus.Satisfied, "满足")]
    [InlineData(MaterialPlanStatus.Excess, "超量")]
    public void MaterialPlanStatus_GetDisplayName(MaterialPlanStatus value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("未计划", MaterialPlanStatus.NotPlanned)]
    [InlineData("超量", MaterialPlanStatus.Excess)]
    public void MaterialPlanStatus_Parse_Chinese(string chinese, MaterialPlanStatus expected)
    {
        EnumHelper.Parse<MaterialPlanStatus>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(InventoryPlanStatus.Planned, "已计划")]
    [InlineData(InventoryPlanStatus.Confirmed, "已确认")]
    [InlineData(InventoryPlanStatus.Cancelled, "已取消")]
    public void InventoryPlanStatus_GetDisplayName(InventoryPlanStatus value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("已计划", InventoryPlanStatus.Planned)]
    public void InventoryPlanStatus_Parse_Chinese(string chinese, InventoryPlanStatus expected)
    {
        EnumHelper.Parse<InventoryPlanStatus>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(LengthStatus.Fixed, "定尺")]
    [InlineData(LengthStatus.Range, "范围尺")]
    [InlineData(LengthStatus.NonFixed, "非定尺")]
    public void LengthStatus_GetDisplayName(LengthStatus value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("定尺", LengthStatus.Fixed)]
    [InlineData("非定尺", LengthStatus.NonFixed)]
    public void LengthStatus_Parse_Chinese(string chinese, LengthStatus expected)
    {
        EnumHelper.Parse<LengthStatus>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(DeliveryState.SolutionAnnealedAndPickled, "固溶酸洗")]
    [InlineData(DeliveryState.Bright, "光亮")]
    [InlineData(DeliveryState.Hard, "硬态")]
    public void DeliveryState_GetDisplayName(DeliveryState value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("固溶酸洗", DeliveryState.SolutionAnnealedAndPickled)]
    [InlineData("光亮", DeliveryState.Bright)]
    public void DeliveryState_Parse_Chinese(string chinese, DeliveryState expected)
    {
        EnumHelper.Parse<DeliveryState>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(SettlementMethod.Theoretical, "理算")]
    [InlineData(SettlementMethod.Weighing, "过磅")]
    [InlineData(SettlementMethod.WeighingNegative, "过磅-负")]
    public void SettlementMethod_GetDisplayName(SettlementMethod value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("理算", SettlementMethod.Theoretical)]
    public void SettlementMethod_Parse_Chinese(string chinese, SettlementMethod expected)
    {
        EnumHelper.Parse<SettlementMethod>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(SalesOrderStatus.Pending, "待处理")]
    [InlineData(SalesOrderStatus.Confirmed, "已确认")]
    [InlineData(SalesOrderStatus.Cancelled, "已取消")]
    public void SalesOrderStatus_GetDisplayName(SalesOrderStatus value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("待处理", SalesOrderStatus.Pending)]
    public void SalesOrderStatus_Parse_Chinese(string chinese, SalesOrderStatus expected)
    {
        EnumHelper.Parse<SalesOrderStatus>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(MaterialName.SeamlessPipe, "无缝管")]
    [InlineData(MaterialName.WeldedPipe, "焊管")]
    public void MaterialName_GetDisplayName(MaterialName value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("无缝管", MaterialName.SeamlessPipe)]
    [InlineData("焊管", MaterialName.WeldedPipe)]
    public void MaterialName_Parse_Chinese(string chinese, MaterialName expected)
    {
        EnumHelper.Parse<MaterialName>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(BatchStatus.None, "未产")]
    [InlineData(BatchStatus.InProgress, "在产")]
    [InlineData(BatchStatus.Completed, "完成")]
    [InlineData(BatchStatus.Suspended, "挂起")]
    [InlineData(BatchStatus.Cancelled, "作废")]
    public void BatchStatus_GetDisplayName(BatchStatus value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("在产", BatchStatus.InProgress)]
    [InlineData("完成", BatchStatus.Completed)]
    [InlineData("作废", BatchStatus.Cancelled)]
    public void BatchStatus_Parse_Chinese(string chinese, BatchStatus expected)
    {
        EnumHelper.Parse<BatchStatus>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(PurchaseOrderStatus.Open, "已下单")]
    [InlineData(PurchaseOrderStatus.Partial, "部分到货")]
    [InlineData(PurchaseOrderStatus.Completed, "已完成")]
    public void PurchaseOrderStatus_GetDisplayName(PurchaseOrderStatus value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("已下单", PurchaseOrderStatus.Open)]
    [InlineData("部分到货", PurchaseOrderStatus.Partial)]
    [InlineData("已完成", PurchaseOrderStatus.Completed)]
    public void PurchaseOrderStatus_Parse_Chinese(string chinese, PurchaseOrderStatus expected)
    {
        EnumHelper.Parse<PurchaseOrderStatus>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(SubcontractOrderStatus.Sent, "已发出")]
    [InlineData(SubcontractOrderStatus.PartialReturned, "部分收回")]
    [InlineData(SubcontractOrderStatus.Completed, "已完成")]
    public void SubcontractOrderStatus_GetDisplayName(SubcontractOrderStatus value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("已发出", SubcontractOrderStatus.Sent)]
    [InlineData("部分收回", SubcontractOrderStatus.PartialReturned)]
    [InlineData("已完成", SubcontractOrderStatus.Completed)]
    public void SubcontractOrderStatus_Parse_Chinese(string chinese, SubcontractOrderStatus expected)
    {
        EnumHelper.Parse<SubcontractOrderStatus>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(SectionOutsourceStatus.PendingRecovery, "待回收")]
    [InlineData(SectionOutsourceStatus.Recovered, "已回收")]
    [InlineData(SectionOutsourceStatus.InProgress, "在轧")]
    public void SectionOutsourceStatus_GetDisplayName(SectionOutsourceStatus value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("待回收", SectionOutsourceStatus.PendingRecovery)]
    [InlineData("已回收", SectionOutsourceStatus.Recovered)]
    public void SectionOutsourceStatus_Parse_Chinese(string chinese, SectionOutsourceStatus expected)
    {
        EnumHelper.Parse<SectionOutsourceStatus>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(RepairPriority.Normal, "普通")]
    [InlineData(RepairPriority.Urgent, "紧急")]
    [InlineData(RepairPriority.Emergency, "特急")]
    public void RepairPriority_GetDisplayName(RepairPriority value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("紧急", RepairPriority.Urgent)]
    public void RepairPriority_Parse_Chinese(string chinese, RepairPriority expected)
    {
        EnumHelper.Parse<RepairPriority>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(RepairOrderStatus.Pending, "待维修")]
    [InlineData(RepairOrderStatus.InProgress, "维修中")]
    [InlineData(RepairOrderStatus.Completed, "完成")]
    public void RepairOrderStatus_GetDisplayName(RepairOrderStatus value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("维修中", RepairOrderStatus.InProgress)]
    public void RepairOrderStatus_Parse_Chinese(string chinese, RepairOrderStatus expected)
    {
        EnumHelper.Parse<RepairOrderStatus>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(LifecycleStatus.Active, "在用")]
    [InlineData(LifecycleStatus.Standby, "备用")]
    [InlineData(LifecycleStatus.Scrapped, "报废")]
    public void LifecycleStatus_GetDisplayName(LifecycleStatus value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("在用", LifecycleStatus.Active)]
    [InlineData("备用", LifecycleStatus.Standby)]
    public void LifecycleStatus_Parse_Chinese(string chinese, LifecycleStatus expected)
    {
        EnumHelper.Parse<LifecycleStatus>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(UsageType.Primary, "主生产设备")]
    [InlineData(UsageType.Secondary, "辅生产设备")]
    [InlineData(UsageType.Other, "其它")]
    public void UsageType_GetDisplayName(UsageType value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("主生产设备", UsageType.Primary)]
    [InlineData("辅生产设备", UsageType.Secondary)]
    public void UsageType_Parse_Chinese(string chinese, UsageType expected)
    {
        EnumHelper.Parse<UsageType>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(RequirementType.Normal, "常规")]
    [InlineData(RequirementType.Special, "特殊")]
    public void RequirementType_GetDisplayName(RequirementType value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("常规", RequirementType.Normal)]
    [InlineData("特殊", RequirementType.Special)]
    public void RequirementType_Parse_Chinese(string chinese, RequirementType expected)
    {
        EnumHelper.Parse<RequirementType>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(RawMaterialType.SemiFinished, "荒管")]
    [InlineData(RawMaterialType.SemiProduct, "半成品")]
    public void RawMaterialType_GetDisplayName(RawMaterialType value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("荒管", RawMaterialType.SemiFinished)]
    [InlineData("半成品", RawMaterialType.SemiProduct)]
    public void RawMaterialType_Parse_Chinese(string chinese, RawMaterialType expected)
    {
        EnumHelper.Parse<RawMaterialType>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(FinishedProductType.Critical, "临界成品")]
    [InlineData(FinishedProductType.Order, "订单成品")]
    public void FinishedProductType_GetDisplayName(FinishedProductType value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(FinishedProductType.Critical, "临界成品")]
    public void FinishedProductType_Parse_Chinese(FinishedProductType value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(OutboundType.ProductionPick, "生产领用")]
    [InlineData(OutboundType.SalesOut, "销售出库")]
    [InlineData(OutboundType.ScrapOut, "报废出库")]
    public void OutboundType_GetDisplayName(OutboundType value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("生产领用", OutboundType.ProductionPick)]
    [InlineData("销售出库", OutboundType.SalesOut)]
    public void OutboundType_Parse_Chinese(string chinese, OutboundType expected)
    {
        EnumHelper.Parse<OutboundType>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(CustomerStatus.Active, "启用")]
    [InlineData(CustomerStatus.Inactive, "停用")]
    public void CustomerStatus_GetDisplayName(CustomerStatus value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("启用", CustomerStatus.Active)]
    public void CustomerStatus_Parse_Chinese(string chinese, CustomerStatus expected)
    {
        EnumHelper.Parse<CustomerStatus>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(ReworkType.EmptyDrawing, "空拉改制")]
    [InlineData(ReworkType.FewerPass, "少道次改制")]
    [InlineData(ReworkType.ManualSelect, "人工选择改制")]
    public void ReworkType_GetDisplayName(ReworkType value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("空拉改制", ReworkType.EmptyDrawing)]
    public void ReworkType_Parse_Chinese(string chinese, ReworkType expected)
    {
        EnumHelper.Parse<ReworkType>(chinese).Should().Be(expected);
    }

    [Theory]
    [InlineData(InspectionItem.PMIInspection, "PMI检验")]
    [InlineData(InspectionItem.VisualInspection, "表检")]
    [InlineData(InspectionItem.Dimension, "尺寸")]
    [InlineData(InspectionItem.Endoscopy, "内窥")]
    [InlineData(InspectionItem.HydrostaticPressure, "水压")]
    [InlineData(InspectionItem.Ultrasonic, "超声波")]
    [InlineData(InspectionItem.PortColoring, "端口着色")]
    public void InspectionItem_GetDisplayName(InspectionItem value, string expected)
    {
        EnumHelper.GetDisplayName(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("PMI检验", InspectionItem.PMIInspection)]
    [InlineData("表检", InspectionItem.VisualInspection)]
    [InlineData("尺寸", InspectionItem.Dimension)]
    [InlineData("超声波", InspectionItem.Ultrasonic)]
    public void InspectionItem_Parse_Chinese(string chinese, InspectionItem expected)
    {
        EnumHelper.Parse<InspectionItem>(chinese).Should().Be(expected);
    }

    // ========== 非泛型版本测试 ==========

    [Fact]
    public void GetDisplayName_NonGeneric_ReturnsSameAsGeneric()
    {
        EnumHelper.GetDisplayName(typeof(BatchStatus), BatchStatus.InProgress)
            .Should().Be(EnumHelper.GetDisplayName(BatchStatus.InProgress));
    }

    [Fact]
    public void Parse_NonGeneric_ReturnsCorrectObject()
    {
        var result = EnumHelper.Parse("在产", typeof(BatchStatus));
        result.Should().Be(BatchStatus.InProgress);
        result.Should().BeOfType<BatchStatus>();
    }

    // ========== 容错测试 ==========

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Parse_Empty_Throws(string empty)
    {
        Action act = () => EnumHelper.Parse<BatchStatus>(empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_InvalidText_Throws()
    {
        Action act = () => EnumHelper.Parse<BatchStatus>("不存在的状态");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*无法识别*");
    }

    [Fact]
    public void TryParse_Invalid_ReturnsNull()
    {
        EnumHelper.TryParse<BatchStatus>("不存在的状态").Should().BeNull();
    }

    [Fact]
    public void TryParse_ValidChinese_ReturnsValue()
    {
        EnumHelper.TryParse<BatchStatus>("在产").Should().Be(BatchStatus.InProgress);
    }

    [Fact]
    public void TryParse_ValidEnglish_ReturnsValue()
    {
        EnumHelper.TryParse<BatchStatus>("InProgress").Should().Be(BatchStatus.InProgress);
    }

    // ========== 枚举名大小写不敏感 ==========

    [Theory]
    [InlineData("inprogress")]
    [InlineData("INPROGRESS")]
    [InlineData("InProgress")]
    public void BatchStatus_Parse_CaseInsensitive(string text)
    {
        EnumHelper.Parse<BatchStatus>(text).Should().Be(BatchStatus.InProgress);
    }

    // ========== 全部枚举都已注册（无遗漏） ==========

    /// <summary>
    /// 验证所有枚举值都能获取到中文显示名（没有遗漏注册的枚举）
    /// </summary>
    public static IEnumerable<object[]> AllEnumValues()
    {
        foreach (var value in Enum.GetValues<BatchStatus>())
            yield return new object[] { typeof(BatchStatus), value! };
        foreach (var value in Enum.GetValues<PurchaseOrderStatus>())
            yield return new object[] { typeof(PurchaseOrderStatus), value! };
        foreach (var value in Enum.GetValues<SubcontractOrderStatus>())
            yield return new object[] { typeof(SubcontractOrderStatus), value! };
        foreach (var value in Enum.GetValues<SectionOutsourceStatus>())
            yield return new object[] { typeof(SectionOutsourceStatus), value! };
        foreach (var value in Enum.GetValues<WorkOrderStatus>())
            yield return new object[] { typeof(WorkOrderStatus), value! };
        foreach (var value in Enum.GetValues<LengthStatus>())
            yield return new object[] { typeof(LengthStatus), value! };
        foreach (var value in Enum.GetValues<DeliveryState>())
            yield return new object[] { typeof(DeliveryState), value! };
        foreach (var value in Enum.GetValues<SettlementMethod>())
            yield return new object[] { typeof(SettlementMethod), value! };
        foreach (var value in Enum.GetValues<SalesOrderStatus>())
            yield return new object[] { typeof(SalesOrderStatus), value! };
        foreach (var value in Enum.GetValues<MaterialName>())
            yield return new object[] { typeof(MaterialName), value! };
        foreach (var value in Enum.GetValues<InspectionItem>())
            yield return new object[] { typeof(InspectionItem), value! };
        foreach (var value in Enum.GetValues<OutboundType>())
            yield return new object[] { typeof(OutboundType), value! };
        foreach (var value in Enum.GetValues<CustomerStatus>())
            yield return new object[] { typeof(CustomerStatus), value! };
        foreach (var value in Enum.GetValues<ReworkType>())
            yield return new object[] { typeof(ReworkType), value! };
        foreach (var value in Enum.GetValues<RawMaterialType>())
            yield return new object[] { typeof(RawMaterialType), value! };
        foreach (var value in Enum.GetValues<FinishedProductType>())
            yield return new object[] { typeof(FinishedProductType), value! };
        foreach (var value in Enum.GetValues<RepairPriority>())
            yield return new object[] { typeof(RepairPriority), value! };
        foreach (var value in Enum.GetValues<RepairOrderStatus>())
            yield return new object[] { typeof(RepairOrderStatus), value! };
        foreach (var value in Enum.GetValues<LifecycleStatus>())
            yield return new object[] { typeof(LifecycleStatus), value! };
        foreach (var value in Enum.GetValues<UsageType>())
            yield return new object[] { typeof(UsageType), value! };
        foreach (var value in Enum.GetValues<RequirementType>())
            yield return new object[] { typeof(RequirementType), value! };
        foreach (var value in Enum.GetValues<MaterialPlanStatus>())
            yield return new object[] { typeof(MaterialPlanStatus), value! };
        foreach (var value in Enum.GetValues<InventoryPlanStatus>())
            yield return new object[] { typeof(InventoryPlanStatus), value! };
    }

    [Theory]
    [MemberData(nameof(AllEnumValues))]
    public void AllEnums_HaveChineseDisplayName(Type enumType, object value)
    {
        var displayName = EnumHelper.GetDisplayName(enumType, value);
        displayName.Should().NotBe(value.ToString(),
            $"枚举 {enumType.Name}.{value} 未注册中文显示名，当前返回: {displayName}");
    }
}
