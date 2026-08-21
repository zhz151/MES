namespace MES.Core.DTOs.Quality;

/// <summary>
/// 成品检验「近日成检量数据」汇总行（结构A 指标列式，折叠卡片展示）。
/// 行 = 9 个检验项目（InspectionItem 枚举）+ 合计行，列 = 前6日/前3日（均不含今日）/今日（实时）检验重量(kg)。
/// 统计口径：每条成品检验记录按其检验重量 Weight 计入所属检验项目；预成检/正式成检合并统计；
/// 前3日 = [今天−3, 今天)、前6日 = [今天−6, 今天)（均不含今日，今日单独实时统计）。
/// </summary>
public class FinalInspectionSummaryRowDto
{
    /// <summary>检验项目中文名（PMI检验/表检/尺寸/…）或"合计"</summary>
    public string InspectionItem { get; set; } = string.Empty;

    /// <summary>今日（实时）检验重量(kg；前端 /1000 显示 t)</summary>
    public decimal TodayWeight { get; set; }

    /// <summary>前3日检验重量(kg，[今天−3, 今天)，不含今日)</summary>
    public decimal Last3DaysWeight { get; set; }

    /// <summary>前6日检验重量(kg，[今天−6, 今天)，不含今日)</summary>
    public decimal Last7DaysWeight { get; set; }
}

/// <summary>
/// 成品检验「月度成检量数据」汇总行（结构A 指标列式，折叠卡片展示）。
/// 行 = 9 个检验项目（InspectionItem 枚举）+ 合计行，列 = 本年 12 个月（1月~12月）检验重量(kg)。
/// 统计口径与「近日成检量数据」（GetRecentSummaryAsync）一致：每条成品检验记录按其检验重量 Weight 计入所属检验项目，
/// 预成检/正式成检合并统计；仅日期窗口由 [今天−6, 今天+1) 改为 [本年1月1日, 次年1月1日)，按每个月份分别统计。
/// </summary>
public class FinalInspectionMonthlySummaryRowDto
{
    /// <summary>检验项目中文名（PMI检验/表检/尺寸/…）或"合计"</summary>
    public string InspectionItem { get; set; } = string.Empty;

    /// <summary>本年各月检验重量(kg；前端 /1000 显示 t)，长度恒 12，索引 0=1月…11=12月</summary>
    public List<decimal> MonthlyWeights { get; set; } = new();
}
