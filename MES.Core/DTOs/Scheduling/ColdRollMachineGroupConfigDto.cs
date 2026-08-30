namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 冷轧机台组配置 DTO —— 组内工序用 <see cref="List&lt;string&gt;"/> 供前端 MudSelectMulti（实体存逗号分隔字符串，服务层互转）。
/// </summary>
public class ColdRollMachineGroupConfigDto
{
    public int Id { get; set; }

    /// <summary>组稳定 Key（字母开头仅字母数字下划线，唯一）</summary>
    public string GroupKey { get; set; } = "";

    /// <summary>组显示名（如 冷轧5060）</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>组内工序 ProcessKeys 列表（工序全局唯一归属一组）</summary>
    public List<string> ProcessKeys { get; set; } = new();

    /// <summary>显示顺序（升序，小排前）</summary>
    public int DisplayOrder { get; set; }

    /// <summary>供给目标组 Key（可空）：供给方组指向的下游需求组（如 5060 → "2030"）；空 = 非供给方</summary>
    public string? SupplyTargetGroupKey { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    public DateTime UpdatedTime { get; set; }
}
