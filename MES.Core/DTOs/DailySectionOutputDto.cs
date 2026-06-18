namespace MES.Core.DTOs;

/// <summary>
/// 指定日期各工段产量汇总
/// </summary>
public class DailySectionOutputDto
{
    public string SectionName { get; set; } = null!;
    public decimal TotalWeight { get; set; }
    public int RecordCount { get; set; }
}
