namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 实时委外在产汇总（二维表，折叠卡片展示）。
/// 行 = 在产单位，列 = 有数据的工段（近日生产量数据工段 Tab 规范序），末尾追加"合计"行（列合计+行合计）。
/// 数据口径 = 状态在产/未产且有当前委外单位的批次「有效投料重量」（CurrentValidWeight），按（在产单位, 当前工段）聚合，
/// 不依赖委外发出/回收（SectionOutsource/OutsourceRecovery）。
/// 每个单元格含三值（kg，前端 /1000 显示 t，保留 1 位）：
///   总量 = 该格所有批次有效投料重量之和；
///   流转 = 其中实时 IsFlow（批次计划流转=是）批次的有效投料重量之和；
///   特急 = 其中批次计划等级=急+（PlanFlowLevel 1，与批次计划页"特急批重量"口径一致）批次的有效投料重量之和。
/// 前端显示模式：总量/[流转]/[*特急]，0 值留空。
/// </summary>
public class BatchPlanOutsourcePendingDto
{
    /// <summary>列 = 工段中文名（仅包含有数据的工段，规范序）</summary>
    public List<string> Sections { get; set; } = new();

    /// <summary>行 = 在产单位（合计降序），末尾含"合计"行</summary>
    public List<OutsourcePendingRowDto> Rows { get; set; } = new();
}

/// <summary>
/// 实时委外在产行（一个在产单位在各单位工段的在产重量，含流转/特急附加）。
/// </summary>
public class OutsourcePendingRowDto
{
    /// <summary>在产单位（委外单位/厂内车间），"合计"行为合计</summary>
    public string OutsourceUnit { get; set; } = string.Empty;

    /// <summary>单元格，key = 工段中文名（与 BatchPlanOutsourcePendingDto.Sections 对齐）</summary>
    public Dictionary<string, OutsourcePendingCellDto> Cells { get; set; } = new();

    /// <summary>该在产单位所有工段合计（总量/流转/重点 分别求和）</summary>
    public OutsourcePendingCellDto TotalCell { get; set; } = new();
}

/// <summary>
/// 委外在产单元格（一个在产单位 × 一个工段）的三值重量。
/// </summary>
public class OutsourcePendingCellDto
{
    /// <summary>总量(kg)：该格所有批次有效投料重量之和</summary>
    public decimal Total { get; set; }

    /// <summary>流转(kg)：其中批次计划实时 IsFlow=是 的批次有效投料重量之和</summary>
    public decimal Flow { get; set; }

    /// <summary>重点(kg)：其中批次计划等级=急+（PlanFlowLevel 1）的批次有效投料重量之和</summary>
    public decimal Key { get; set; }
}
