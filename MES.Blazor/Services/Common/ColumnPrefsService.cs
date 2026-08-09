using System.Text.Json;
using System.Text.Json.Serialization;
using Blazored.LocalStorage;
using MES.Core.Models;

namespace MES.Blazor.Services;

/// <summary>
/// 列定义（用于列选择器）
/// </summary>
public class ColumnDef
{
    [JsonPropertyName("k")]
    public string Key { get; set; } = "";
    [JsonPropertyName("l")]
    public string Label { get; set; } = "";
    [JsonPropertyName("v")]
    public bool Visible { get; set; } = true;

    /// <summary>
    /// 排序字段名（null表示不可排序）
    /// </summary>
    [JsonIgnore]
    public string? SortKey { get; set; }

    /// <summary>
    /// 是否适用于当前仓库类型（仓库固有属性，不序列化到 localStorage）
    /// </summary>
    [JsonIgnore]
    public bool IsApplicable { get; set; } = true;

    /// <summary>
    /// 是否为公共字段（新增行时自动从上一行复制，不序列化到 localStorage）
    /// </summary>
    [JsonIgnore]
    public bool IsCommon { get; set; }

    /// <summary>
    /// 是否为必填字段（表头显示红色星号，不序列化到 localStorage）
    /// </summary>
    [JsonIgnore]
    public bool IsRequired { get; set; }

    /// <summary>
    /// 编辑模式列宽（px），仅 compact-table 下由 table-layout: fixed 生效
    /// </summary>
    [JsonIgnore]
    public string? Width { get; set; }

    /// <summary>
    /// 筛选类型：string/enum/date/number/boolean/null（null表示不可筛选）
    /// </summary>
    [JsonIgnore]
    public string? FilterType { get; set; }

    /// <summary>
    /// 筛选时实际使用的后端字段名（默认等于 Key）。
    /// 用于 DTO 显示名与实体字段名不一致的列，如 ActualInputWeight→InputWeight、MainNoInputRatio→MainNoInputOutputRatio。
    /// </summary>
    [JsonIgnore]
    public string? FilterField { get; set; }

    /// <summary>
    /// 枚举列的可选值列表（用于多选框展示，仅 FilterType="enum" 时有效）
    /// </summary>
    [JsonIgnore]
    public List<EnumOption>? EnumOptions { get; set; }

    /// <summary>
    /// 布尔列为 true 时的按钮文案（默认"是"）
    /// </summary>
    [JsonIgnore]
    public string BoolTrueLabel { get; set; } = "是";

    /// <summary>
    /// 布尔列为 false 时的按钮文案（默认"否"）
    /// </summary>
    [JsonIgnore]
    public string BoolFalseLabel { get; set; } = "否";

    /// <summary>
    /// 当前筛选条件（用于 ColumnFilter 组件通信）
    /// </summary>
    [JsonIgnore]
    public FilterDescriptor? ActiveFilter { get; set; }

    /// <summary>
    /// 分组键（1-4），用于列分组展示，null 表示不分组
    /// </summary>
    [JsonIgnore]
    public int? GroupKey { get; set; }

    /// <summary>
    /// 分组名称（如"基础数据""用料计划"）
    /// </summary>
    [JsonIgnore]
    public string? GroupName { get; set; }

    /// <summary>
    /// 列语义层级（表头标色用）：工单级/主号级/订单级，不序列化到 localStorage
    /// </summary>
    [JsonIgnore]
    public ColumnLevel Level { get; set; } = ColumnLevel.WorkOrder;

    /// <summary>
    /// 表头标色 CSS class（主号级/订单级，带前导空格；工单级为空串）
    /// </summary>
    [JsonIgnore]
    public string LevelCssClass => Level switch
    {
        ColumnLevel.MainNo => " col-level-mainno",
        ColumnLevel.Order => " col-level-order",
        _ => ""
    };

    /// <summary>
    /// 显示转换器：原始值→中文显示文本
    /// 三处统一调用：RenderCell / ResolvePrintValue / BuildFilterOptionsFromData
    /// </summary>
    [JsonIgnore]
    public Func<object?, string?>? DisplayConverter { get; set; }
}

/// <summary>
/// 列语义层级（表头标色区分）：工单级为默认无标色，主号级青色，订单级橙色
/// </summary>
public enum ColumnLevel
{
    /// <summary>工单级（默认，无特殊标色）</summary>
    WorkOrder = 0,
    /// <summary>主号级（表头青色）</summary>
    MainNo = 1,
    /// <summary>订单级（表头橙色）</summary>
    Order = 2,
}

/// <summary>
/// 枚举选项（用于列筛选多选框）
/// </summary>
public class EnumOption
{
    /// <summary>实际存储值（如 "InProgress"）</summary>
    public string Value { get; set; } = "";
    /// <summary>前端显示文本（如 "在产"）</summary>
    public string Display { get; set; } = "";

    public EnumOption() { }
    public EnumOption(string value, string display) { Value = value; Display = display; }
}

/// <summary>
/// 列偏好持久化服务
/// </summary>
public class ColumnPrefsService
{
    private readonly ILocalStorageService _storage;
    private const string Prefix = "col_prefs";

    public ColumnPrefsService(ILocalStorageService storage)
    {
        _storage = storage;
    }

    public async Task<List<ColumnDef>> LoadAsync(string pageType, string? warehouseCode)
    {
        var key = BuildKey(pageType, warehouseCode);
        try
        {
            var json = await _storage.GetItemAsStringAsync(key);
            if (!string.IsNullOrEmpty(json))
            {
                var defs = JsonSerializer.Deserialize<List<ColumnDef>>(json);
                if (defs != null && defs.Count > 0)
                    return defs;
            }
        }
        catch
        {
            // ignore deserialization errors, use defaults
        }
        return new List<ColumnDef>();
    }

    public async Task SaveAsync(string pageType, string? warehouseCode, List<ColumnDef> columns)
    {
        var key = BuildKey(pageType, warehouseCode);
        var json = JsonSerializer.Serialize(columns);
        await _storage.SetItemAsStringAsync(key, json);
    }

    public async Task<bool> ExistsAsync(string pageType, string? warehouseCode)
    {
        var key = BuildKey(pageType, warehouseCode);
        return await _storage.ContainKeyAsync(key);
    }

    private static string BuildKey(string pageType, string? warehouseCode) =>
        $"{Prefix}_{pageType}" + (string.IsNullOrEmpty(warehouseCode) ? "" : $"_{warehouseCode.ToLowerInvariant()}");
}
