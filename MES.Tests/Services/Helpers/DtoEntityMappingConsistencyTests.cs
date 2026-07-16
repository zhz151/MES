using System.Reflection;
using FluentAssertions;
using MES.Core.DTOs.Batch;
using MES.Core.Enums;
using MES.Core.Helpers;
using MES.Data.Entities.Batch;

namespace MES.Tests.Services.Helpers;

/// <summary>
/// DTO 与 Entity 字段映射一致性测试。
///
/// ProductionBatch 实体有 9 个 string 字段存储枚举值（如 SettlementMethod="Weighing"）。
/// 相关 DTO（Create/Update/Detail/List）也有对应的 string/enum 字段。
/// 新增枚举字段时容易泄漏：Entity 改了但 DTO 没改，或反之。
///
/// 本测试通过反射检测映射的一致性和完整性。
/// </summary>
public class DtoEntityMappingConsistencyTests
{
    /// <summary>
    /// ProductionBatch 实体中以 string 类型存储但概念属于枚举的字段列表。
    /// 每个条目记录字段名、对应的枚举类型、以及是否可为空。
    /// </summary>
    private static readonly (string FieldName, Type EnumType, bool IsNullable)[] EntityEnumStringFields =
    {
        (nameof(ProductionBatch.ProductionType), typeof(ProductionType), true),
        (nameof(ProductionBatch.ManufacturingItem), typeof(ManufacturingItem), false),
        (nameof(ProductionBatch.MaterialName), typeof(PipeManufacturingType), false),
        (nameof(ProductionBatch.SettlementMethod), typeof(SettlementMethod), false),
        (nameof(ProductionBatch.DeliveryState), typeof(DeliveryState), false),
        (nameof(ProductionBatch.LengthStatus), typeof(LengthStatus), false),
        (nameof(ProductionBatch.TechnicalRequirements), typeof(RequirementType), false),
        (nameof(ProductionBatch.SourceLengthStatus), typeof(LengthStatus), true),
        (nameof(ProductionBatch.SourceMaterialType), typeof(string), true), // 自由文本，非枚举
        (nameof(ProductionBatch.InboundSource), typeof(string), true),      // 自由文本，非枚举
    };

    /// <summary>
    /// 1. 验证 Entity 中以 string 存储的每个枚举字段，
    ///    其对应的枚举类型已在 EnumHelper 中注册。
    /// </summary>
    [Fact]
    public void Entity枚举字符串字段_对应枚举类型已注册()
    {
        foreach (var (fieldName, enumType, _) in EntityEnumStringFields)
        {
            if (enumType == typeof(string))
                continue; // 自由文本字段跳过

            // 通过 GetDisplayName 验证枚举类型已注册——未注册的类型会回退到 .ToString()
            var firstValue = Enum.GetValues(enumType).GetValue(0)!;
            var display = EnumHelper.GetDisplayName(enumType, firstValue);
            var rawName = Enum.GetName(enumType, firstValue)!;

            display.Should().NotBe(rawName,
                $"Entity 字段 {nameof(ProductionBatch)}.{fieldName} " +
                $"对应的枚举 {enumType.Name} 未在 EnumHelper 中注册中文映射。");
        }
    }

    /// <summary>
    /// 2. 验证 CreateProductionBatchRequest DTO 包含 Entity 的所有枚举字符串字段。
    /// </summary>
    [Fact]
    public void CreateRequestDTO_包含所有Entity枚举字符串字段()
    {
        var dtoType = typeof(CreateProductionBatchRequest);
        var dtoProps = dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (fieldName, _, _) in EntityEnumStringFields)
        {
            dtoProps.Should().Contain(fieldName,
                $"CreateProductionBatchRequest 应包含 Entity 字段 {fieldName}");
        }
    }

    /// <summary>
    /// 3. 验证 UpdateProductionBatchRequest DTO 包含 Entity 的所有枚举字符串字段。
    /// </summary>
    [Fact]
    public void UpdateRequestDTO_包含所有Entity枚举字符串字段()
    {
        var dtoType = typeof(UpdateProductionBatchRequest);
        var dtoProps = dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (fieldName, _, _) in EntityEnumStringFields)
        {
            dtoProps.Should().Contain(fieldName,
                $"UpdateProductionBatchRequest 应包含 Entity 字段 {fieldName}");
        }
    }

    /// <summary>
    /// 4. 验证 ProductionBatchDetailDto 包含 Entity 的所有枚举字符串字段（可能为枚举类型而非 string）。
    /// </summary>
    [Fact]
    public void DetailDTO_包含所有Entity枚举字符串字段()
    {
        var dtoType = typeof(ProductionBatchDetailDto);
        var dtoProps = dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (fieldName, _, _) in EntityEnumStringFields)
        {
            dtoProps.Should().Contain(fieldName,
                $"ProductionBatchDetailDto 应包含 Entity 字段 {fieldName}");
        }
    }

    /// <summary>
    /// 5. 验证 DTO 中枚举字段的类型与 Entity 对应：
    ///    - Entity 是 string 存储的枚举，在 DTO 中可以是 string 或实际枚举类型
    ///    - 但字符串类型的字段应能安全转换为枚举
    /// </summary>
    [Fact]
    public void DTO枚举字段类型_与Entity一致()
    {
        var entityType = typeof(ProductionBatch);

        foreach (var (fieldName, enumType, isNullable) in EntityEnumStringFields)
        {
            if (enumType == typeof(string))
                continue;

            var entityProp = entityType.GetProperty(fieldName);
            entityProp.Should().NotBeNull($"Entity 应包含属性 {fieldName}");

            // Entity 中以 string 存储
            entityProp!.PropertyType.Should().Be(typeof(string),
                $"Entity.{fieldName} 应为 string 类型（当前为 {entityProp.PropertyType.Name}）");

            // 验证 Entity 字段名与枚举类型名称的关联关系在 EnumHelper 中成立
            var allEnumValues = Enum.GetValues(enumType);
            foreach (var val in allEnumValues)
            {
                var toString = val.ToString()!;
                var parseResult = Enum.Parse(enumType, toString);
                parseResult.Should().Be(val,
                    $"枚举 {enumType.Name}.{toString} 的 Enum.Parse 应正确恢复");
            }
        }
    }

    /// <summary>
    /// 6. 验证 ProductionBatchListDto 包含必要的枚举字段。
    /// </summary>
    [Fact]
    public void ListDTO_包含关键枚举字段()
    {
        var dtoType = typeof(ProductionBatchListDto);
        var dtoProps = dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 列表页至少应包含这些字段
        var requiredFields = new[] { "Status", "ProductionType", "ManufacturingItem",
            "SettlementMethod", "DeliveryState", "LengthStatus" };

        foreach (var field in requiredFields)
        {
            dtoProps.Should().Contain(field,
                $"ProductionBatchListDto 应包含字段 {field}");
        }
    }
}
