using MES.Core.Enums;

namespace MES.Data.Entities.Materials;

/// <summary>
/// 采购订单（一单一料）
/// </summary>
public class PurchaseOrder : BaseEntity
{
    /// <summary>
    /// 采购单号（CG+yyMMdd+3位流水）
    /// </summary>
    public string OrderNo { get; set; } = null!;

    /// <summary>
    /// 供应商ID（→ SupplierProfile）
    /// </summary>
    public int SupplierId { get; set; }

    /// <summary>
    /// 供应商名称（从 SupplierProfile 冗余快照）
    /// </summary>
    public string? SupplierName { get; set; }

    /// <summary>
    /// 下单日期
    /// </summary>
    public DateTime OrderDate { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Open;

    /// <summary>
    /// 强制完成（true时状态固定为已完成，false时自动计算）
    /// </summary>
    public bool IsForceCompleted { get; set; }

    /// <summary>
    /// 物料分类（MaterialType 枚举名）
    /// </summary>
    public string MaterialCategory { get; set; } = null!;

    /// <summary>
    /// 厂内钢种
    /// </summary>
    public string PlantGrade { get; set; } = null!;

    /// <summary>
    /// 名义规格
    /// </summary>
    public string Specification { get; set; } = null!;

    /// <summary>
    /// 单支重量(kg)
    /// </summary>
    public decimal? UnitWeight { get; set; }

    /// <summary>
    /// 采购支数
    /// </summary>
    public int? Quantity { get; set; }

    /// <summary>
    /// 采购重量(kg)
    /// </summary>
    public decimal Weight { get; set; }

    /// <summary>
    /// 要求到货日期
    /// </summary>
    public DateTime RequiredDate { get; set; }

    /// <summary>
    /// 单价
    /// </summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>
    /// 总金额
    /// </summary>
    public decimal? TotalAmount { get; set; }

    /// <summary>
    /// 最后到货日期（Service维护）
    /// </summary>
    public DateTime? LastArrivalDate { get; set; }

    /// <summary>
    /// 已到货支数（Service维护）
    /// </summary>
    public int ReceivedQuantity { get; set; }

    /// <summary>
    /// 已到货重量(kg)（Service维护）
    /// </summary>
    public decimal ReceivedWeight { get; set; }

    /// <summary>
    /// 来源工单号（字符串，可选）
    /// </summary>
    public string? SourceWorkOrderNo { get; set; }

    /// <summary>
    /// 投料倍率(1制几)
    /// </summary>
    public int? InputMultiple { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }
}
