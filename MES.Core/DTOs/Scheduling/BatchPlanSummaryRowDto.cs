namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 批次计划「近日生产量数据」汇总行（结构A 指标列式，折叠卡片展示）。
/// 行 = 全部 26 个工段（SectionDefs，内抛+内修磨合并为一行），冷轧拔按所在工序组分化为「冷轧拔-&lt;工序&gt;」行（含 90 冷轧），
/// + 检验-荒管/检验-在制 + 合计行，列 = 前6日/前3日（均不含今日）/今日（实时）产量重量(kg)。
/// 统计口径：一般工段 = 生产记录（ExecDate）+ 委外回收量（RecoveryDate）；去油/酸洗 = 完工记录（CompleteDate）+ 委外回收量（不含生产记录）；
/// 荒管检/在制检 = 过程检验检验重量（InspectionDate，按产类区分）；委外回收仅回收量（不含未加工量）；
/// 前3日 = [今天−3, 今天)、前6日 = [今天−6, 今天)（均不含今日，今日单独实时统计）。
/// </summary>
public class BatchPlanSummaryRowDto
{
    /// <summary>工段中文名（60冷轧/…/在制检）或"合计"</summary>
    public string SectionName { get; set; } = string.Empty;

    /// <summary>今日（实时）产量重量(kg；前端 /1000 显示 t)</summary>
    public decimal TodayWeight { get; set; }

    /// <summary>前3日产量重量(kg，[今天−3, 今天)，不含今日)</summary>
    public decimal Last3DaysWeight { get; set; }

    /// <summary>前6日产量重量(kg，[今天−6, 今天)，不含今日)</summary>
    public decimal Last7DaysWeight { get; set; }
}
