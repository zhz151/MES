using MES.Core.Enums;

namespace MES.Data.Entities;

/// <summary>
/// 委外加工单—主表（发出+收回汇总）
/// </summary>
public class SubcontractOrder : BaseEntity
{
    /// <summary>
    /// 委外单号（WW+yyMMdd+3位流水）
    /// </summary>
    public string OrderNo { get; set; } = null!;

    /// <summary>
    /// 供应商ID（→ SupplierProfile）
    /// </summary>
    public int SupplierId { get; set; }

    /// <summary>
    /// 下单日期
    /// </summary>
    public DateTime OrderDate { get; set; }

    /// <summary>
    /// 加工类型（如：穿孔/冷拔/热处理）
    /// </summary>
    public string ProcessType { get; set; } = null!;

    /// <summary>
    /// 状态
    /// </summary>
    public SubcontractOrderStatus Status { get; set; } = SubcontractOrderStatus.Sent;

    /// <summary>
    /// 强制完成（true时状态固定为已完成，false时自动计算）
    /// </summary>
    public bool IsForceCompleted { get; set; }

    /// <summary>
    /// 发出物料分类
    /// </summary>
    public string OutMaterialCategory { get; set; } = null!;

    /// <summary>
    /// 炉号
    /// </summary>
    public string? FurnaceNumber { get; set; }

    /// <summary>
    /// 发出钢种（工厂牌号）
    /// </summary>
    public string OutPlantGrade { get; set; } = null!;

    /// <summary>
    /// 发出规格
    /// </summary>
    public string OutSpecification { get; set; } = null!;

    /// <summary>
    /// 发出支数
    /// </summary>
    public int OutQuantity { get; set; }

    /// <summary>
    /// 发出重量(kg)
    /// </summary>
    public decimal OutWeight { get; set; }

    /// <summary>
    /// 收回截止日期
    /// </summary>
    public DateTime? ReturnDeadline { get; set; }

    /// <summary>
    /// 收回支数（Service维护）
    /// </summary>
    public int? InQuantity { get; set; }

    /// <summary>
    /// 收回重量(kg)（Service维护）
    /// </summary>
    public decimal? InWeight { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 子表：委外明细要求
    /// </summary>
    public List<SubcontractReturnItem> ReturnItems { get; set; } = new();
}
