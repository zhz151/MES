namespace MES.Core.DTOs.Configuration;

/// <summary>
/// 工序组定义 DTO（配置页内联编辑用）
/// </summary>
public class ProcessDefinitionDto
{
    public int Id { get; set; }
    public string ProcessKey { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsColdRoll { get; set; }
    public bool IsColdDraw { get; set; }

    /// <summary>默认工段（SectionKey 列表，计划页新增该工序行自动填充；null 表示无默认）</summary>
    public List<string>? DefaultSections { get; set; }

    public string? Remark { get; set; }
}

/// <summary>
/// 启用工序信息：展示层动态化用（下拉选项/名称/顺序运行时从参数表加载）
/// </summary>
public class ProcessInfoDto
{
    /// <summary>稳定 Key（对应 ProcessKeys 常量名），程序识别/存储用</summary>
    public string ProcessKey { get; set; } = string.Empty;

    /// <summary>显示名称（管理员可改名，界面显示此值）</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>显示顺序（升序，1 排最前）</summary>
    public int DisplayOrder { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }

    /// <summary>是否冷轧系列（含三辊冷轧）</summary>
    public bool IsColdRoll { get; set; }

    /// <summary>是否冷拔工序</summary>
    public bool IsColdDraw { get; set; }

    /// <summary>默认工段（SectionKey 列表，计划页新增该工序行自动填充；null 表示无默认）</summary>
    public List<string>? DefaultSections { get; set; }
}
