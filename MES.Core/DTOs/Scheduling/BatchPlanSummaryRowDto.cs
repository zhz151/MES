namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 批次计划跨工段汇总行（结构A 指标列式，折叠卡片展示）。
/// 行 = 工段 Tab（与列表工段筛选口径一致），列 = 批次数/总重量/流转/重点/等级分布。
/// 口径说明：每工段行的批次按对应工段 Tab 的筛选逻辑归桶（冷轧类/检验类/普通工段，与 GetAllAsync(sectionTab) 完全一致），
/// 一个批次可能同时命中多个工段（如"冷拔"工段做"60冷轧"工序同时命中两个 Tab），故各工段行批次数之和可能大于"合计"行；
/// 合计行 = 全量唯一批次（GetAllAsync(null)），不重复计数。
/// </summary>
public class BatchPlanSummaryRowDto
{
    /// <summary>工段中文名（60冷轧/…/在制检）或"合计"</summary>
    public string SectionName { get; set; } = string.Empty;

    /// <summary>批次数</summary>
    public int BatchCount { get; set; }

    /// <summary>总重量(kg，有效投料重量 CurrentValidWeight 之和；前端 /1000 显示 t)</summary>
    public decimal TotalWeight { get; set; }

    /// <summary>计划流转批次（PlanIsFlow=true）</summary>
    public int FlowBatchCount { get; set; }

    /// <summary>计划流转批次重量(kg)</summary>
    public decimal FlowBatchWeight { get; set; }

    /// <summary>计划重点批次（PlanFlowLevel==1 急+，与 G13 等级列口径一致）</summary>
    public int KeyBatchCount { get; set; }

    /// <summary>计划重点批次重量(kg)</summary>
    public decimal KeyBatchWeight { get; set; }

    /// <summary>等级分布：急+（PlanFlowLevel==1，与 KeyBatchCount 同值）</summary>
    public int Level1Count { get; set; }

    /// <summary>等级分布：急（PlanFlowLevel==2）</summary>
    public int Level2Count { get; set; }

    /// <summary>等级分布：急-（PlanFlowLevel==3）</summary>
    public int Level3Count { get; set; }

    /// <summary>等级分布：一般（PlanFlowLevel==4）</summary>
    public int Level4Count { get; set; }

    /// <summary>等级分布：略（PlanFlowLevel==5）</summary>
    public int Level5Count { get; set; }
}
