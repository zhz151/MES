using System;
using System.Collections.Generic;
using System.Linq;

namespace MES.Services.DataExchange;

public class EntityDef
{
    public string Key { get; }
    public string DisplayName { get; }
    public Type Type { get; }
    public int ImportOrder { get; }
    public string? KeyColumn { get; }
    public string[]? CompositeKeyColumns { get; }
    public List<ColumnDef> Columns { get; }

    public EntityDef(string key, string displayName, Type type, int importOrder, string? keyColumn, List<ColumnDef> columns, string[]? compositeKeyColumns = null)
    {
        Key = key;
        DisplayName = displayName;
        Type = type;
        ImportOrder = importOrder;
        KeyColumn = keyColumn;
        CompositeKeyColumns = compositeKeyColumns;
        // 统一补充主键 ID 系统列：覆盖模式按 ID 精确匹配（有 ID 行更新，无 ID 行新增）
        // ID 列类型取实体实际主键类型（默认为 int；OutboundRecord 等使用 long/bigint 需适配，防大 ID 溢出）
        if (!columns.Any(c => c.Property == "Id"))
        {
            var idProp = type.GetProperty("Id");
            var idType = idProp?.PropertyType ?? typeof(int);
            columns.Insert(0, new ColumnDef("ID", "Id", idType, isRequired: false, isSystem: true));
        }
        Columns = columns;
    }
}

public class ColumnDef
{
    public string Header { get; }
    public string? Property { get; set; }
    public Type PropertyType { get; }
    public bool IsEnum { get; }
    public Type? EnumType { get; }
    public bool IsSystem { get; }
    public bool IsRequired { get; }
    public Func<string, object>? ValueConverter { get; }

    public bool IsFkColumn { get; set; }
    public string? FkEntityKey { get; set; }
    public string? FkLookupProperty { get; set; }
    public string? FkTargetProperty { get; set; }
    public bool FkRequiresJoin { get; set; }

    public ColumnDef(string header, string? property, Type? propertyType = null,
                     bool isEnum = false, bool isSystem = false, bool isRequired = true,
                     Func<string, object>? valueConverter = null)
    {
        Header = header;
        Property = property;
        PropertyType = propertyType ?? typeof(string);
        IsEnum = isEnum;
        EnumType = isEnum && propertyType != null
            ? (Nullable.GetUnderlyingType(propertyType) ?? propertyType)
            : null;
        IsSystem = isSystem;
        IsRequired = isRequired;
        ValueConverter = valueConverter;
    }
}
