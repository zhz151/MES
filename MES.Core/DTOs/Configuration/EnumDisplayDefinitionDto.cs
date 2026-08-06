namespace MES.Core.DTOs.Configuration;

/// <summary>
/// 枚举显示配置 DTO（配置页内联编辑用）
/// </summary>
public class EnumDisplayDefinitionDto
{
    public int Id { get; set; }

    /// <summary>枚举标识（枚举类型名，如 "BatchStatus"）</summary>
    public string EnumKey { get; set; } = string.Empty;

    /// <summary>枚举值名（Enum.ToString()，如 "InProgress"），稳定英文名</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>可改名中文显示</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>显示顺序（升序，1 排最前）</summary>
    public int DisplayOrder { get; set; }

    /// <summary>说明</summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 枚举显示选项（options-map 返回项：Value/DisplayName/DisplayOrder，供前端排序注入）
/// </summary>
public class EnumDisplayOptionDto
{
    /// <summary>枚举值名（Enum.ToString()，如 "InProgress"），稳定英文名</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>中文显示名（配置表优先，静态字典兜底）</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>显示顺序（升序，1 排最前）</summary>
    public int DisplayOrder { get; set; }
}

/// <summary>
/// 启用字典值信息（enabled-values 返回项：下拉选项/名称/顺序运行时从参数表加载）
/// </summary>
public class DictValueInfoDto
{
    /// <summary>稳定英文 Key（如 "ColdRollDraw"），存储/程序识别用</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>显示名称（管理员可改名，界面显示此值）</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>显示顺序（升序，1 排最前）</summary>
    public int DisplayOrder { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }
}

/// <summary>
/// 字典值配置 DTO（配置页内联编辑用）
/// </summary>
public class DictValueDefinitionDto
{
    public int Id { get; set; }

    /// <summary>字典标识（SectionKey/ProcessKey/UrgencyLevelKey/ProductStatus/ProductionFlowKey/FlowTargetKey/ProductionOverviewRowKey/LiabilityTypeKey）</summary>
    public string DictKey { get; set; } = string.Empty;

    /// <summary>英文稳定 Key（如 "ColdRollDraw"）</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>可改名中文显示</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>显示顺序（升序，1 排最前）</summary>
    public int DisplayOrder { get; set; }

    /// <summary>是否启用（false 隐藏，下拉/筛选不出现）</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>说明</summary>
    public string? Remark { get; set; }
}
