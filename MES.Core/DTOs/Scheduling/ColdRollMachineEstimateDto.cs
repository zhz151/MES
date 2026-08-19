namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 冷轧排程排机估算 DTO — 按轧机类型聚合（冷轧5060/冷轧2030/冷轧三辊/冷拔）
/// 流转总量口径与排程汇总一致：仅已排程批次、近6天（在轧+待轧，PositionDiff≤6，不含远日）
/// 在制品/成品按排程产出形态拆分（IsFinished=最后工序组为成品），成品+在制=流转总量
/// 机台需求数 = Σ(规格流转量 ÷ 单机单日量) ÷ 6天，四舍五入得每日台数
/// </summary>
public class ColdRollMachineEstimateDto
{
    /// <summary>轧机类型显示名（冷轧5060/冷轧2030/冷轧三辊/冷拔）</summary>
    public string MachineType { get; set; } = "";

    /// <summary>流转总量(kg)：该轧机类型排程批次近6天量</summary>
    public decimal FlowTotalWeight { get; set; }

    /// <summary>在制品(kg)：IsFinished=false（中间冷轧/冷拔工序组）的流转量之和</summary>
    public decimal InProcessWeight { get; set; }

    /// <summary>成品(kg)：IsFinished=true（最后工序组）的流转量之和</summary>
    public decimal FinishedWeight { get; set; }

    /// <summary>机台需求数（每日台数，四舍五入）</summary>
    public int MachineCount { get; set; }
}
