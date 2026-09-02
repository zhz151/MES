using System.ComponentModel.DataAnnotations;

namespace MES.Data.Entities.Payroll;

/// <summary>
/// 成检计件类别（成检计件标准主表，2026-09-03 引入）。
/// 成检计件 = 按成品检验记录（FinalInspection）结算：类别 = 成检项目(InspectionItem 枚举名单选) × 规格维度档。
/// 与生产计件类别（PieceRateProductionCategory）差异：无「工序/产类/作业阶段」约束（不建 PieceRate*Key 子表），
/// 同一成检项目同时仅一条启用类别（过滤唯一索引 UK_FinalInspectionCategory_Item_Active 兜底；整组编辑维档）。
/// 基准价只在类别上；维度系数在子表 PieceRateFinalInspectionCategoryTier 上，档行只存系数不冗余基准。
/// 结算单价 = BasePrice × 命中档 Ratio 连乘（无例外价/绝对价概念）；某维配档但记录值不落任何档 → 该维系数 1。
/// 维度 Key 域见 PieceRateInspectionDimensionKeys（Length 档量纲 mm、全长度状态参与：Fixed=实际定尺长，
/// Range/NonFixed 取数缺省按 6000mm 折算命中与计费折算；InspectionCount 为整数支数闭带）。
/// </summary>
public class PieceRateFinalInspectionCategory : BaseEntity
{
    /// <summary>成检项目（InspectionItem 枚举名，如 EddyCurrent/Ultrasonic/HydrostaticPressure；单选必选）</summary>
    [Required]
    [MaxLength(30)]
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>基准价（元/结算单位，必填 &gt;0；精度 decimal(18,4)）</summary>
    public decimal BasePrice { get; set; }

    /// <summary>结算单位（英文 Key，见 PieceRateUnitKeys）</summary>
    [Required]
    [MaxLength(20)]
    public string Unit { get; set; } = string.Empty;

    /// <summary>当前启用（true=参与匹配；false=停用不参与。同成检项目启用唯一）</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>备注/说明</summary>
    [MaxLength(200)]
    public string? Remark { get; set; }

    /// <summary>类别维度档（子表，级联删除）</summary>
    public List<PieceRateFinalInspectionCategoryTier> Tiers { get; set; } = new();
}
