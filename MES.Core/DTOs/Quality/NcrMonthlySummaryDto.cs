using MES.Core.Enums;

namespace MES.Core.DTOs.Quality;

/// <summary>
/// 不合格品月度汇总结果 — 按（责任类别→责任部门→处置方式）三级分组，12 个月次品支数/重量矩阵。
/// 责任类别/责任部门/处置方式 为空归「未填写」分组，全量守恒。分月基准 = 反馈日期（ReportDate）。
/// </summary>
public class NcrMonthlySummaryDto
{
    /// <summary>12 个月份标签（如 2026-01 ~ 2026-12）</summary>
    public List<string> MonthLabels { get; set; } = new();

    /// <summary>当前月份 0 基索引（1月=0）；后续月份尚未发生，单元格留空</summary>
    public int CurrentMonthIndex { get; set; }

    /// <summary>月度汇总行（责任类别→责任部门→处置方式 三级，已按此排序、同组相邻，便于前端合并单元格）</summary>
    public List<NcrMonthlyRowDto> Rows { get; set; } = new();
}

/// <summary>不合格品月度汇总行（处置方式粒度）</summary>
public class NcrMonthlyRowDto
{
    /// <summary>责任类别（字典英文 Key；未填写为空串，分组/排序用）</summary>
    public string ResponsibilityCategory { get; set; } = "";

    /// <summary>责任类别中文显示（未填写→「未填写」）</summary>
    public string CategoryDisplay { get; set; } = "";

    /// <summary>责任部门（未填写→「未填写」）</summary>
    public string ResponsibleDept { get; set; } = "";

    /// <summary>处置方式（未填写为 null）</summary>
    public DisposalMethod? DisposalMethod { get; set; }

    /// <summary>处置方式中文显示（未填写→「未填写」）</summary>
    public string DisposalMethodDisplay { get; set; } = "";

    /// <summary>12 个月次品支数/重量</summary>
    public List<NcrMonthValueDto> Months { get; set; } = new();

    /// <summary>处置方式全年合计（次品支数）</summary>
    public int TotalQuantity { get; set; }

    /// <summary>处置方式全年合计（次品重量 kg）</summary>
    public int? TotalWeight { get; set; }
}

/// <summary>单月次品支数/重量</summary>
public class NcrMonthValueDto
{
    /// <summary>次品支数</summary>
    public int Quantity { get; set; }

    /// <summary>次品重量(kg)</summary>
    public int? Weight { get; set; }
}
