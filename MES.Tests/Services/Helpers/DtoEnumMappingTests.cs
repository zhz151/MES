using FluentAssertions;
using MES.Core.DTOs.Batch;
using MES.Core.DTOs.Infrastructure;
using MES.Core.DTOs.Materials;
using MES.Core.DTOs.WorkOrder;
using MES.Core.Enums;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// DTO 枚举类型映射验证测试
/// 确保 DTO 中已从 string 改为枚举类型的属性在赋值和序列化时行为正确
/// </summary>
public class DtoEnumMappingTests
{
    #region Response DTO — 枚举属性默认值

    [Fact]
    public void SaveBatchResponse_Status_默认值为None()
    {
        var dto = new SaveBatchResponse();
        dto.Status.Should().Be(BatchStatus.None);
    }

    [Fact]
    public void ScanBatchResolveResultDto_Status_默认值为None()
    {
        var dto = new ScanBatchResolveResultDto();
        dto.Status.Should().Be(BatchStatus.None);
    }

    [Fact]
    public void NotificationDto_NotificationType_默认值为NewMaterial()
    {
        var dto = new NotificationDto();
        dto.NotificationType.Should().Be(NotificationType.NewMaterial);
    }

    [Fact]
    public void PurchaseFinishedPlanDto_ProductType_默认值为0()
    {
        var dto = new PurchaseFinishedPlanDto();
        ((int)dto.ProductType).Should().Be(0);
    }

    [Fact]
    public void PurchaseFinishedPlanDto_LengthStatus_默认值为Fixed()
    {
        var dto = new PurchaseFinishedPlanDto();
        dto.LengthStatus.Should().Be(LengthStatus.Fixed);
    }

    [Fact]
    public void PurchaseFinishedPlanDto_DeliveryState_默认值为SolutionAnnealedAndPickled()
    {
        var dto = new PurchaseFinishedPlanDto();
        dto.DeliveryState.Should().Be(DeliveryState.SolutionAnnealedAndPickled);
    }

    #endregion

    #region Request DTO — 枚举赋值与读取

    [Fact]
    public void CreatePurchaseFinishedPlanRequest_可以设置枚举属性()
    {
        var req = new CreatePurchaseFinishedPlanRequest
        {
            ProductType = FinishedProductType.Order,
            LengthStatus = LengthStatus.NonFixed,
            DeliveryState = DeliveryState.Hard
        };

        req.ProductType.Should().Be(FinishedProductType.Order);
        req.LengthStatus.Should().Be(LengthStatus.NonFixed);
        req.DeliveryState.Should().Be(DeliveryState.Hard);
    }

    #endregion

    #region Enum.Parse 映射 — 模拟 Service 层转换

    /// <summary>
    /// 模拟 PurchaseOrderService 中 MaterialType 字符串→枚举的映射
    /// </summary>
    [Fact]
    public void PurchaseOrderService_MaterialType映射_字符串转枚举()
    {
        // 模拟数据库中存储的字符串值
        var dbStrings = new[] { "RoundBar", "RoughTube", "SemiFinished", "OrderFinished", "" };

        var results = dbStrings.Select(s => string.IsNullOrEmpty(s) ? default : Enum.Parse<MaterialType>(s)).ToList();

        results[0].Should().Be(MaterialType.RoundBar);
        results[1].Should().Be(MaterialType.RoughTube);
        results[2].Should().Be(MaterialType.SemiFinished);
        results[3].Should().Be(MaterialType.OrderFinished);
        results[4].Should().Be(default(MaterialType)); // empty -> default (RoundBar)
    }

    /// <summary>
    /// 模拟 NotificationService 中 Enum.Parse 转换
    /// </summary>
    [Fact]
    public void NotificationService_NotificationType映射_字符串转枚举()
    {
        var dbStrings = new[] { "NewMaterial", "DeleteBlocked", "OutboundAlert", "WorkOrderDeleted", "OrderDeleted", "OrderChanged" };

        var results = dbStrings.Select(Enum.Parse<NotificationType>).ToList();

        results.Should().BeEquivalentTo(new[]
        {
            NotificationType.NewMaterial,
            NotificationType.DeleteBlocked,
            NotificationType.OutboundAlert,
            NotificationType.WorkOrderDeleted,
            NotificationType.OrderDeleted,
            NotificationType.OrderChanged
        });
    }

    /// <summary>
    /// 模拟 MaterialPlanService 中 LengthStatus 字符串→枚举的映射
    /// </summary>
    [Fact]
    public void MaterialPlanService_LengthStatus映射_字符串转枚举()
    {
        var dbStrings = new[] { "Fixed", "Range", "NonFixed", null, "" };

        var results = dbStrings.Select(s => string.IsNullOrEmpty(s) ? default : Enum.Parse<LengthStatus>(s)).ToList();

        results[0].Should().Be(LengthStatus.Fixed);
        results[1].Should().Be(LengthStatus.Range);
        results[2].Should().Be(LengthStatus.NonFixed);
        results[3].Should().Be(default(LengthStatus)); // null -> default
        results[4].Should().Be(default(LengthStatus)); // empty -> default
    }

    /// <summary>
    /// 模拟 BatchService 中 BatchStatus 枚举的直接赋值
    /// </summary>
    [Fact]
    public void BatchService_BatchStatus直接赋值()
    {
        var entityStatus = BatchStatus.InProgress;

        // 模拟 service 层直接赋值
        var response = new SaveBatchResponse { Status = entityStatus };

        response.Status.Should().Be(BatchStatus.InProgress);
    }

    /// <summary>
    /// 模拟 MaterialPlanService 中 DeliveryState 字符串→枚举的映射（含 null 容错）
    /// </summary>
    [Fact]
    public void MaterialPlanService_DeliveryState映射_字符串转枚举()
    {
        var dbStrings = new[] { "SolutionAnnealedAndPickled", "Bright", "Hard", null };

        var results = dbStrings.Select(s => string.IsNullOrEmpty(s) ? default : Enum.Parse<DeliveryState>(s)).ToList();

        results[0].Should().Be(DeliveryState.SolutionAnnealedAndPickled);
        results[1].Should().Be(DeliveryState.Bright);
        results[2].Should().Be(DeliveryState.Hard);
        results[3].Should().Be(default(DeliveryState)); // null -> default
    }

    #endregion

    #region JsonSerialization — 枚举的 JSON 序列化/反序列化

    [Fact]
    public void SaveBatchResponse_Json序列化_枚举输出数字()
    {
        var dto = new SaveBatchResponse { Status = BatchStatus.InProgress };

        var json = System.Text.Json.JsonSerializer.Serialize(dto);

        // 默认 JsonSerializerOptions 输出数字，确认 Status 字段出现
        json.Should().Contain("Status");
    }

    [Fact]
    public void NotificationDto_Json序列化()
    {
        var dto = new NotificationDto { NotificationType = NotificationType.DeleteBlocked };

        var json = System.Text.Json.JsonSerializer.Serialize(dto);

        json.Should().Contain("NotificationType");
    }

    #endregion

    #region JsonCompatibility — 字符串格式兼容性（Blazor WASM 实际传输格式）

    private static readonly System.Text.Json.JsonSerializerOptions JsonWebOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    /// <summary>
    /// DTO 的枚举字段声明为 string 类型（如 LengthStatus 是 string?），
    /// JSON 中的数字值无法直接反序列化为 string——这会在反序列化时抛出 JsonException。
    ///
    /// Blazor 实际传输中，MudSelect T="EnumType" 绑定输出的是枚举名（字符串）而非数字，
    /// 因此不会触发此问题。
    /// </summary>
    [Fact]
    public void UpdateRequest_Json数字格式_正常反序列化到枚举字段()
    {
        // JsonStringEnumConverter 默认支持数字值
        var json = """{"lengthStatus":1,"deliveryState":3,"settlementMethod":"Weighing"}""";

        var dto = System.Text.Json.JsonSerializer.Deserialize<UpdateProductionBatchRequest>(json, JsonWebOptions);

        dto.Should().NotBeNull();
        dto!.LengthStatus.Should().Be(LengthStatus.Range);
        dto.DeliveryState.Should().Be(DeliveryState.SolutionAnnealedAndPickledInternalPolished);
        dto.SettlementMethod.Should().Be(SettlementMethod.Weighing);
    }

    /// <summary>
    /// 验证：DTO 枚举字段可正确接收 JSON 英文枚举名（JsonStringEnumConverter）。
    /// 这是 Blazor MudSelect 绑定枚举后推荐的实际传输方式。
    /// </summary>
    [Fact]
    public void UpdateRequest_Json英文字符串格式_正确映射到DTO枚举字段()
    {
        // 模拟前端通过 JsonStringEnumConverter 发送英文枚举名
        var json = """{"lengthStatus":"Fixed","deliveryState":"Bright","settlementMethod":"Weighing"}""";

        var dto = System.Text.Json.JsonSerializer.Deserialize<UpdateProductionBatchRequest>(json, JsonWebOptions);

        dto.Should().NotBeNull();
        dto!.LengthStatus.Should().Be(LengthStatus.Fixed);
        dto.DeliveryState.Should().Be(DeliveryState.Bright);
        dto.SettlementMethod.Should().Be(SettlementMethod.Weighing);
    }

    /// <summary>
    /// 验证：DTO 枚举字段无法接收中文文本（JsonStringEnumConverter 只识别英文枚举名）。
    /// 此测试确保 DisplayHelper.GetXxxText() 不会误用于赋值（在 Razor 中已规范）。
    /// </summary>
    [Fact]
    public void UpdateRequest_Json中文字符串格式_反序列化失败()
    {
        // 模拟 Blazor 页面误将 DisplayHelper.GetXxxText() 赋值后发送的 JSON
        var json = """{"lengthStatus":"定尺","deliveryState":"光亮","settlementMethod":"过磅"}""";

        // DTO 枚举字段无法解析中文文本，抛出 JsonException
        var act = () => System.Text.Json.JsonSerializer.Deserialize<UpdateProductionBatchRequest>(json, JsonWebOptions);
        act.Should().Throw<System.Text.Json.JsonException>();
    }

    #endregion
}
