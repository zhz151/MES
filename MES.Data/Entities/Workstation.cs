namespace MES.Data.Entities;

/// <summary>
/// 工位 — 固定在车间设备旁的二维码标识
/// 扫码报工时先扫工位码绑定上下文（设备+工段+报工类型），再扫批次条码
/// 工段决定这个工位做什么工序，工序组则由批次和工段共同匹配确定
/// ReportType 决定报工写入哪张表以及使用哪个表单模板
/// </summary>
public class Workstation : BaseEntity
{
    /// <summary>工位编码（如 W001, ACID-IN），二维码内容</summary>
    public string Code { get; set; } = null!;

    /// <summary>工位名称（如"3号抛光机"），仅供界面显示</summary>
    public string? Name { get; set; }

    /// <summary>设备名称（自动填入报工表单）</summary>
    public string? EquipmentName { get; set; }

    /// <summary>工段名 — 工位确定做的工序类型（如"外抛光""冷轧拔""检验"）</summary>
    public string SectionName { get; set; } = null!;

    /// <summary>
    /// 报工模板类型 — 决定扫码后写入哪张表以及表单字段布局
    /// ProductionRecord | PicklingInRecord | PicklingOutRecord
    /// | SectionOutsource | OutsourceRecovery | ProcessInspection
    /// | FinalInspection | MaterialReceiveCheck
    /// </summary>
    public string ReportType { get; set; } = null!;

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; } = true;
}
