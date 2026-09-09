using MES.Core.Enums;
using MES.Core.Helpers;

namespace MES.Core.DTOs.Materials;

public class SupplierProfileDto
{
    public int Id { get; set; }
    public string SupplierCode { get; set; } = null!;
    public string SupplierName { get; set; } = null!;
    public MaterialType? MaterialCategory { get; set; }
    public string? MaterialCategoryDisplay => MaterialCategory.HasValue ? EnumHelper.GetDisplayName(MaterialCategory.Value) : null;
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public string? Remark { get; set; }
    public DateTimeOffset CreatedTime { get; set; }

    // ========== ② 往来信息（采购+委外合并统计，供应商列表 AttachTradeStatsAsync 回填） ==========
    // 单位：Count=单据数、Weight=kg、Amount=元（采购货款 TotalAmount + 委外加工费 Σ ProcessTotalAmount）
    public int TotalOrderCount { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal TotalAmount { get; set; }

    public int YearOrderCount { get; set; }
    public decimal YearWeight { get; set; }
    public decimal YearAmount { get; set; }

    /// <summary>本年到货(kg) = 本年入厂批毛(InventoryBatch.InboundDate.Year==本年) − 本年退货，负数截 0</summary>
    public decimal ArrivedWeight { get; set; }

    /// <summary>
    /// 本年到货货款(元) = Σ 单据级认领：各采购/委外单(名+分类命中档案行) 该单金额 × (本年到货净重/该单应到重)，负数截 0。
    /// 到货净重 = 本年到货毛 − 本年退货重，故金额已同扣本年退货货款；按单号聚合（非笼统全局单价），金额=采购单 TotalAmount、委外单 Σ子项 ProcessTotalAmount，参考口径。
    /// </summary>
    public decimal ArrivedAmount { get; set; }

    /// <summary>待收货(kg) = Σ Max(0, 应到 − 累计净到)，排除完成/强制完成单（当前时点欠交快照，非年份口径）</summary>
    public decimal PendingWeight { get; set; }

    /// <summary>待收货货款(元) = Σ 单据级认领：各未完成/未强完单 该单金额 × (欠交净重/该单应到重)，金额=采购单 TotalAmount、委外单 Σ子项 ProcessTotalAmount（参考口径）</summary>
    public decimal PendingAmount { get; set; }

    /// <summary>本年退货(kg)，按退货出库年份</summary>
    public decimal YearReturnWeight { get; set; }
}

public class CreateSupplierRequest
{
    public string SupplierName { get; set; } = null!;
    public MaterialType? MaterialCategory { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Remark { get; set; }
}

public class UpdateSupplierRequest
{
    public string? SupplierName { get; set; }
    public MaterialType? MaterialCategory { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public bool? IsActive { get; set; }
    public string? Remark { get; set; }
}
