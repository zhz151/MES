using System.ComponentModel.DataAnnotations;

namespace MES.Data.Entities.Configuration;

/// <summary>
/// 重点工序日产能力
/// </summary>
public class DailyProductionCapacity : BaseEntity
{
    /// <summary>工序名称（如 Polish、Mill50_60）</summary>
    [MaxLength(50)]
    public string ProcessName { get; set; } = null!;

    /// <summary>日产能力（吨/天）</summary>
    public decimal DailyCapacity { get; set; }

    /// <summary>说明</summary>
    [MaxLength(200)]
    public string? Remark { get; set; }
}
