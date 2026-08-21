namespace MES.Core.DTOs.Scheduling;

/// <summary>
/// 批次计划「月度生产量数据」汇总行（结构A 指标列式，折叠卡片展示）。
/// 行 = 全部 26 个工段（SectionDefs，内抛+内修磨合并为一行），冷轧拔按所在工序组分化为「冷轧拔-&lt;工序&gt;」行（含 90 冷轧），
/// + 检验-荒管/检验-在制 + 合计行，列 = 本年 12 个月（1月~12月）产量重量(kg)。
/// 统计口径与「近日生产量数据」（GetSummaryAsync）一致：一般工段 = 生产记录（ExecDate）+ 委外回收量（RecoveryDate）；
/// 去油/酸洗 = 完工记录（CompleteDate）+ 委外回收量（不含生产记录）；
/// 荒管检/在制检 = 过程检验检验重量（InspectionDate，按产类区分）；委外回收仅回收量（不含未加工量）。
/// 仅日期窗口由 [今天−6, 今天+1) 改为 [本年1月1日, 次年1月1日)，按每个月份分别统计。
/// </summary>
public class BatchPlanMonthlySummaryRowDto
{
    /// <summary>工段中文名（60冷轧/…/在制检）或"合计"</summary>
    public string SectionName { get; set; } = string.Empty;

    /// <summary>本年各月产量重量(kg；前端 /1000 显示 t)，长度恒 12，索引 0=1月…11=12月</summary>
    public List<decimal> MonthlyWeights { get; set; } = new();
}
