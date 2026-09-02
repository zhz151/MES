using System.ComponentModel.DataAnnotations;

namespace MES.Data.Entities.Payroll;

/// <summary>
/// 生产计件类别（生产计件标准主表，2026-09-02 模型重构引入）。
/// 类别 = 工段(必选·单选) × 工序(可空多选·空=全选) × 产类(可空多选·空=全选) × 作业阶段(可空多选·空=全选)。
/// 基准价只在类别上；维度系数在子表 PieceRateProductionCategoryTier 上，档行只存系数不冗余基准。
/// 结算单价 = BasePrice × 命中档 Ratio 连乘（无例外价/绝对价概念）。
/// 覆盖空间禁交集：同工段两条启用类别 (Section×Procs×Prods×Stages) 不得相交（见 CategoryCoverageRule）。
/// 工序/产类/阶段三约束用 JSON 数组字符串存储英文 Key（空=全选，禁止显式全列表与空并存）。
/// </summary>
public class PieceRateProductionCategory : BaseEntity
{
    /// <summary>工段（英文 Key，StandardWorkDay.SectionKey，仅 IsEnabled=true 者可选）</summary>
    [Required]
    [MaxLength(50)]
    public string SectionKey { get; set; } = string.Empty;

    /// <summary>工序约束（JSON 数组存 ProcessKey 英文 Key；空=全选工序）</summary>
    [MaxLength(500)]
    public string? ProcessKeys { get; set; }

    /// <summary>产类约束（JSON 数组存 ProductStatuses 英文 Key；空=全选产类）</summary>
    [MaxLength(100)]
    public string? ProductStatusKeys { get; set; }

    /// <summary>作业阶段约束（JSON 数组存 PieceRateStageKeys 英文 Key；空=全选阶段含普通报工无阶段）</summary>
    [MaxLength(100)]
    public string? StageKeys { get; set; }

    /// <summary>基准价（元/结算单位，必填 &gt;0；精度 decimal(18,4)）</summary>
    public decimal BasePrice { get; set; }

    /// <summary>结算单位（英文 Key，见 PieceRateUnitKeys）</summary>
    [Required]
    [MaxLength(20)]
    public string Unit { get; set; } = string.Empty;

    /// <summary>当前启用（true=参与匹配；false=停用不参与）</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>备注/说明</summary>
    [MaxLength(200)]
    public string? Remark { get; set; }

    /// <summary>类别维度档（子表，级联删除）</summary>
    public List<PieceRateProductionCategoryTier> Tiers { get; set; } = new();
}
