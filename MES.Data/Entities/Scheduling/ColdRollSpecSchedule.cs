namespace MES.Data.Entities.Scheduling;

/// <summary>
/// 冷轧排程 — 按规格维度记录的排程决策
/// 排程员在冷轧看板上操作，保存时全量同步（保留页面现有维度，删除已失效维度）
/// </summary>
public class ColdRollSpecSchedule : BaseEntity
{
    /// <summary>冷轧类型（60冷轧/50冷轧/30冷轧/20冷轧/三辊冷轧/冷拔）</summary>
    public string ProcessType { get; set; } = null!;

    /// <summary>轧坯规格（前一工序组制造规格）</summary>
    public string BilletSpec { get; set; } = null!;

    /// <summary>轧制规格（当前冷轧工序组制造规格）</summary>
    public string RollingSpec { get; set; } = null!;

    /// <summary>是否成品（是否最后工序）</summary>
    public bool IsFinished { get; set; }

    /// <summary>轧机设备号（多台用分号分隔，如"60-1#；60-2#"）</summary>
    public string? MachineNo { get; set; }

    /// <summary>完工要求（None=无计划/All=全量完工/Urgent=急单完工/Partial=部分完工）针对"在产"维度</summary>
    public string CompletionType { get; set; } = null!;

    /// <summary>排程类型（None=无计划/All=全量冷轧/Urgent=专注急单/Partial=部分流转/Subsequent=后续轧制）针对"待轧"维度</summary>
    public string RollType { get; set; } = null!;

    /// <summary>轧制顺序（1=不换辊，2=换辊，3=再次换辊）</summary>
    public int RollOrder { get; set; }

    /// <summary>合并显示文本（冗余，方便查询）</summary>
    public string? MergeDisplay { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}
